// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Botao Amber Hu <botao@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections;
using Unity.Collections;

namespace HoloInteractive.XR.HoloKit.iOS
{

    public interface IClock
    {

        /// <summary>
        /// Current timestamp in seconds;
        /// </summary>
        double timestamp { get; }
    }

    static class PathUtil
    {
        public static string TemporaryDirectoryPath
            => Application.platform == RuntimePlatform.IPhonePlayer
                ? Application.temporaryCachePath : ".";

        public static string GetTimestampedFilename()
            => $"Record_{DateTime.Now:MMdd_HHm_ss}.mp4";

        public static string GetTemporaryFilePath()
            => TemporaryDirectoryPath + "/" + GetTimestampedFilename();
    }

    sealed class TimeQueue
    {
        Queue<double> _queue = new Queue<double>();
        double _start;
        double _last;

        public void Clear()
        {
            _queue.Clear();
            _start = 0;
        }

        public double StartTime => _start;

        public double Dequeue()
            => _queue.Dequeue();

        public bool TryEnqueueNow(double timestamp)
        {
            if (_start == 0)
            {
                _queue.Enqueue(timestamp);
                _start = timestamp;
                _last = 0;
                return true;
            }
            else
            {
                var time = timestamp;

                // Reject it if it falls into the same frame.
                if ((int)(time * 60) == (int)(_last * 60)) return false;

                _queue.Enqueue(time);
                _last = time;
                return true;
            }
        }
    }

    /// <summary>
    /// Realtime clock for generating timestamps
    /// </summary>
    public sealed class RealtimeClock : IClock
    {

        #region --Client API--
        /// <summary>
        /// Current timestamp in seconds.
        /// The very first value reported by this property will always be zero.
        /// </summary>
        public double timestamp
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                var time = stopwatch.Elapsed.TotalSeconds;
                if (!stopwatch.IsRunning)
                    stopwatch.Start();
                return time;
            }
        }

        /// <summary>
        /// Is the clock paused?
        /// </summary>
        public bool paused
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => !stopwatch.IsRunning;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set => (value ? (Action)stopwatch.Stop : stopwatch.Start)();
        }

        /// <summary>
        /// Create a realtime clock.
        /// </summary>
        public RealtimeClock() => this.stopwatch = new System.Diagnostics.Stopwatch();
        #endregion


        #region --Operations--
        private readonly System.Diagnostics.Stopwatch stopwatch;
        #endregion
    }

    internal sealed class SharedSignal
    {

        #region --Client API--
        /// <summary>
        /// Whether the shared signal has been triggered.
        /// </summary>
        public bool signaled
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            private set;
        }

        /// <summary>
        /// Event raised when the shared signal is triggered.
        /// </summary>
        public event Action OnSignal;

        /// <summary>
        /// Create a shared signal.
        /// </summary>
        /// <param name="count">Number of unique signals required to trigger the shared signal.</param>
        public SharedSignal(int count)
        {
            this.record = new HashSet<object>();
            this.count = count;
        }

        /// <summary>
        /// Send a signal.
        /// </summary>
        /// <param name-"key">Key to identify signal.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(object key)
        {
            if (signaled)
                return;
            record.Add(key);
            if (record.Count == count)
            {
                OnSignal?.Invoke();
                signaled = true;
            }
        }
        #endregion


        #region --Operations--
        private readonly int count;
        private readonly HashSet<object> record;
        #endregion
    }



    [RequireComponent(typeof(ARCameraBackground))]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(HoloKitCameraManager))]
    [RequireComponent(typeof(ARCameraManager))]
    [DisallowMultipleComponent]
    public sealed class HoloKitVideoRecorder : MonoBehaviour
    {
        private Camera _recordingCamera = null;
        private AudioListener _audioListener = null;

        private AudioSource _microphoneAudioSource = null;

        [SerializeField]
        private bool _recordMicrophone = false;

        private RealtimeClock _clock = null;
        private RenderTexture _cameraRt;
        private RenderTexture _frameRt;
        private TimeQueue _timeQueue = new TimeQueue();
        private HoloKitCameraManager _holokitCameraManager;

        // private readonly RingBuffer<float> __deviceRingBuffer;
        // private readonly RingBuffer<float> __unityRingBuffer;
        private readonly float[] _deviceSampleBuffer;
        private readonly float[] _unitySampleBuffer;
        private readonly float[] _mixedBuffer;
        private readonly SharedSignal _signal;
        private readonly object __deviceFence;
        private readonly object __unityFence;
        private const int RingBufferSize = 16384;
        private const int MixBufferSize = 1024;

        public bool IsRecording
        {
            get;
            private set;
        }

        void Start()
        {
            _recordingCamera = GetComponent<Camera>();
            _audioListener = GetComponent<AudioListener>();

            _holokitCameraManager = GetComponent<HoloKitCameraManager>();
            _holokitCameraManager.OnScreenRenderModeChanged += OnHoloKitRenderModeChanged;

            if (_recordMicrophone && Microphone.devices != null)
            {
                if (Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    Debug.Log("Microphone access granted");
                }
                else
                {
                    Debug.Log("Microphone access denied");
                }

                if (_microphoneAudioSource == null)
                {
                    _microphoneAudioSource = gameObject.AddComponent<AudioSource>();
                }
                _microphoneAudioSource = GetComponent<AudioSource>();
                _microphoneAudioSource.clip = Microphone.Start(null, true, 1, 44100);
                _microphoneAudioSource.loop = true;
                while (!(Microphone.GetPosition(null) > 0))
                {
                }
                _microphoneAudioSource.Play();
            }
        }

        public void StartRecording()
        {
            _clock = new RealtimeClock();
            // var sampleRate = _audioDevice != null ? _audioDevice.sampleRate : 24000;
            // var channelCount = _audioDevice != null ? _audioDevice.channelCount : 2;

            var path = PathUtil.GetTemporaryFilePath();

            var sampleRate = AudioSettings.outputSampleRate;
            var channelCount = 2;
            var width = Screen.width;
            var height = Screen.height;

            // ARGBHalf will make sure the camera renders post-processing.
            var _descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 32);
            _cameraRt = new RenderTexture(_descriptor);
            _frameRt = new RenderTexture(width, height, 0);
            int status = HoloKitVideoRecorder_StartRecording(path, width, height, sampleRate, channelCount);

            _timeQueue.Clear();
            if (status != 0)
            {
                Debug.LogError("Cannot Start Video");
            }
            else
            {
                IsRecording = true;
            }

            OnHoloKitRenderModeChanged(_holokitCameraManager.ScreenRenderMode);

            StartCoroutine(CommitFrames());
        }

        public void EndRecording()
        {
            if (_recordMicrophone && _microphoneAudioSource != null)
            {
                _microphoneAudioSource.Stop();
                Microphone.End(null);
            }

            AsyncGPUReadback.WaitAllRequests();
            HoloKitVideoRecorder_EndRecording();
            IsRecording = false;

            OnHoloKitRenderModeChanged(_holokitCameraManager.ScreenRenderMode);

            Destroy(_frameRt);
            _frameRt = null;
            Destroy(_cameraRt);
            _cameraRt = null;
        }

        unsafe void OnSourceReadback(AsyncGPUReadbackRequest request)
        {
            if (!IsRecording) return;
            var data = request.GetData<byte>(0);
            var ptr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data);
            var timestamp = _timeQueue.Dequeue();
            int status = HoloKitVideoRecorder_AppendVideoFrame(ptr, data.Length, timestamp);

            if (status != 0)
            {
                Debug.LogError($"Cannot Append Video Frame at time {timestamp}");
            }
        }

        private IEnumerator CommitFrames()
        {
            var yielder = new WaitForEndOfFrame();
            while (IsRecording)
            {
                // Check frame index
                yield return yielder;
                // Render cameras
                CommitFrame(_recordingCamera);
                // Commit
            }
        }

        void Update()
        {

        }

        void OnDestroy()
        {
            if (IsRecording)
            {
                EndRecording();
            }

            if (_holokitCameraManager)
                _holokitCameraManager.OnScreenRenderModeChanged -= OnHoloKitRenderModeChanged;
        }

        void OnHoloKitRenderModeChanged(ScreenRenderMode mode)
        {
            if (IsRecording)
            {
                if (mode == ScreenRenderMode.Stereo)
                {
                    GetComponent<ARCameraBackground>().enabled = true;
                    _recordingCamera.enabled = true;
                }
            }
            else
            {
                if (mode == ScreenRenderMode.Stereo)
                {
                    GetComponent<ARCameraBackground>().enabled = false;
                    _recordingCamera.enabled = false;
                }
            }
        }

        unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (!IsRecording) return;
            CommitSamples(data, channels);
        }

        // The sample buffer MUST be a linear PCM buffer interleaved by channel.
        unsafe void CommitSamples(float[] data, int channels)
        {
            var nativeArray = new NativeArray<float>(data, Allocator.Temp);
            var ptr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeArray);
            int length = nativeArray.Length * sizeof(float);
            double timestamp = _clock.timestamp;

            HoloKitVideoRecorder_AppendAudioFrame(ptr, length, timestamp);
            nativeArray.Dispose();
        }

        void CommitFrame(Camera source)
        {
            if (!IsRecording) return;
            if (!_timeQueue.TryEnqueueNow(_clock.timestamp)) return;

            var prevTarget = source.targetTexture;
            source.targetTexture = _cameraRt;
            source.Render();
            source.targetTexture = prevTarget;
            Graphics.Blit(_cameraRt, _frameRt, new Vector2(1, -1), new Vector2(0, 1));
            AsyncGPUReadback.Request(_frameRt, 0, OnSourceReadback);
        }
        public void ToggleRecording()
        {
            if (IsRecording)
                EndRecording();
            else
                StartRecording();
        }

#if !UNITY_EDITOR && UNITY_IOS
            const string DllName = "__Internal";
#else
        const string DllName = "UnityHoloKit";
#endif

        /// <param name="width">Video width.</param>
        /// <param name="height">Video height.</param>
        /// <param name="sampleRate">Audio sample rate. Pass 0 for no audio.</param>
        /// <param name="channelCount">Audio channel count. Pass 0 for no audio.</param>
        /// <param name="videoBitRate">Video bit rate in bits per second.</param>
        /// <param name="audioBitRate">Audio bit rate in bits per second.</param>
        [DllImport(DllName)]
        public static extern int HoloKitVideoRecorder_StartRecording(string filePath,
            int width, int height, float audioSampleRate, int audioChannelCount);

        [DllImport(DllName)]
        public static extern int HoloKitVideoRecorder_AppendVideoFrame(IntPtr data, int size, double time);

        [DllImport(DllName)]
        public static extern int HoloKitVideoRecorder_AppendAudioFrame(IntPtr data, int size, double time);

        [DllImport(DllName)]
        public static extern void HoloKitVideoRecorder_EndRecording();
    }
}
#endif
