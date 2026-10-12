// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    public enum RecordingAudioMode { Environment, GameAndMicrophone }

    /// <summary>
    /// Output size of Stereo recordings (landscape, 16:9), as in the HoloKit Swift SDK. Mono
    /// recordings use the screen size.
    /// </summary>
    public enum StereoRecordingResolution
    {
        /// <summary>2560 × 1440.</summary>
        QHD2K = 0,
        /// <summary>3840 × 2160.</summary>
        UHD4K = 1,
    }

    [DisallowMultipleComponent]
    public sealed class HoloKitVideoRecorder : MonoBehaviour
    {
        [Header("Recording audio")]
        [SerializeField] RecordingAudioMode _audioMode = RecordingAudioMode.Environment;
        [SerializeField] HoloKitMicrophoneCapture _microphoneInput;
        [Tooltip("Main Camera's listener for game audio. Environment mode does not use it.")]
        [SerializeField] AudioListener _gameAudioListener;
        [Tooltip("In GameAndMicrophone mode, turn off for game-only recording without microphone permission.")]
        [SerializeField] bool _includeMicrophone = true;
        [Tooltip("Environment mode only: Environment records the raw level; Default the iOS default input (about " +
                 "17 dB louder); Voice adds voice processing with automatic gain, for speech. Must match other consumers of the shared input.")]
        [SerializeField] MicrophoneProcessing _environmentProcessing = MicrophoneProcessing.Environment;
        [Range(0, 2)] [SerializeField] float _gameGain = 1;
        [Range(0, 2)] [SerializeField] float _microphoneGain = 1;
        [Tooltip("Keep the original source tracks and also export a single mixed audio track for sharing. Video is copied without re-encoding.")]
        [SerializeField] bool _createMixedSharingCopy;
        public bool CreateMixedSharingCopy { get => _createMixedSharingCopy; set => _createMixedSharingCopy = value; }
        public HoloKitMicrophoneCapture MicrophoneInput { get => _microphoneInput; set => _microphoneInput = value; }
        public AudioListener GameAudioListener { get => _gameAudioListener; set => _gameAudioListener = value; }
        public bool IncludeMicrophone { get => _includeMicrophone; set => _includeMicrophone = value; }
        /// <summary>Microphone processing in Environment mode (Environment raw level, Default, or Voice with automatic gain).
        /// Takes effect at the next recording.</summary>
        public MicrophoneProcessing EnvironmentProcessing
        {
            get => _environmentProcessing;
            set => _environmentProcessing = value == MicrophoneProcessing.EchoCancellation ? MicrophoneProcessing.Environment : value;
        }
        public float GameGain { get => _gameGain; set => _gameGain = Mathf.Clamp(value, 0, 2); }
        public float MicrophoneGain { get => _microphoneGain; set => _microphoneGain = Mathf.Clamp(value, 0, 2); }
        public RecordingAudioMode AudioMode { get => _audioMode; set => _audioMode = value; }
        public bool IsPreparing => _prepare != null;
        string _lastError;
        bool _startedNative;
        public string LastError
        {
            get => _lastError ?? (!_startedNative || Application.isEditor ? null :
                Marshal.PtrToStringUTF8(HoloKitVideoRecorder_LastError()));
            private set => _lastError = value;
        }
        Coroutine _prepare;
        HoloKitMicrophoneCapture _leasedMicrophone;
        HoloKitGameAudioTap _gameTap;
        double _recordingOrigin;
        [Header("Video")]
        [Tooltip("Stereo recordings: movie size, 2K (2560 × 1440) or 4K (3840 × 2160), 16:9, filled from the AR camera view. Mono recordings use the screen size.")]
        [SerializeField] StereoRecordingResolution _stereoResolution = StereoRecordingResolution.QHD2K;
        [Tooltip("Switch ARKit to its 4K camera format while recording, so the real scene is not upscaled. The movie then runs at 30 fps.")]
        [FormerlySerializedAs("_recordIn4K")]
        [SerializeField] bool _captureCameraIn4K = true;

        /// <summary>Stereo movie size (2K or 4K, 16:9); takes effect at the next recording.</summary>
        public StereoRecordingResolution StereoResolution { get => _stereoResolution; set => _stereoResolution = value; }

        /// <summary>ARKit's 4K camera format while recording (the movie runs at 30 fps); takes effect at the next recording.</summary>
        public bool CaptureCameraIn4K { get => _captureCameraIn4K; set => _captureCameraIn4K = value; }

        [Obsolete("Renamed to CaptureCameraIn4K: it switches the ARKit camera feed to 4K. For a 4K movie in Stereo, set StereoResolution.")]
        public bool RecordIn4K { get => _captureCameraIn4K; set => _captureCameraIn4K = value; }
        ARBackgroundVideoEnhancementManager _enhancement;

        public bool IsRecording { get; private set; }
        /// <summary><c>Time.unscaledTime</c> when the current or last recording started.</summary>
        public float RecordingStartTime { get; private set; }
        public string LastRecordingPath { get; private set; }
        public string LastSharingPath { get; private set; }
        public bool IsFinishing => _draining || (!Application.isEditor && HoloKitVideoRecorder_IsFinishing() != 0);
        public bool UsesGpuPath => _useGpuPath;
        public bool AudioFailed => !Application.isEditor && HoloKitVideoRecorder_AudioFailed() != 0;
        public int SubmittedFrames { get; private set; }
        public int DroppedFrames { get; private set; }

        Camera _camera;
        ARCameraBackground _background;
        HoloKitCameraManager _cameraManager;
        RenderTexture _cameraRt, _frameRt;
        Coroutine _capture;
        double _frameInterval, _nextCaptureTime;
        bool _useGpuPath;
        const int SlotCount = 3;
        Texture2D[] _slots;
        readonly GraphicsFence[] _fences = new GraphicsFence[SlotCount];
        readonly double[] _timestamps = new double[SlotCount];
        readonly Queue<int> _pending = new();
        readonly sbyte[] _stopResults = new sbyte[SlotCount]; // 0 pending, 1 ready, -1 failed
        readonly AsyncGPUReadbackRequest[] _stopRequests = new AsyncGPUReadbackRequest[SlotCount];
        readonly Dictionary<int, (AsyncGPUReadbackRequest request, double timestamp)> _readbacks = new();
        bool _draining;
        int _generation, _readbackId;
        int _cursor;

        void Start()
        {
            _camera = GetComponent<Camera>();
            _background = GetComponent<ARCameraBackground>();
            _cameraManager = GetComponentInParent<HoloKitCameraManager>();
            _cameraManager.OnScreenRenderModeChanged += ApplyRenderMode;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
        }

        public void ToggleRecording()
        {
            if (IsRecording) EndRecording();
            else StartRecording();
        }

        public void StartRecording()
        {
            if (Application.isEditor)
            {
                Debug.LogWarning("[Recorder] Native recording requires an iOS device.");
                return;
            }
            if (IsRecording || IsFinishing || IsPreparing) return;
            LastError = null;
            _startedNative = false;
            bool needsMicrophone = _audioMode == RecordingAudioMode.Environment || _includeMicrophone;
            if (needsMicrophone && _microphoneInput == null)
            {
                // Recorders configured before 0.6.0 have no input assigned. Share the scene's
                // input if there is one, otherwise add one here, so they keep recording audio.
                _microphoneInput = FindFirstObjectByType<HoloKitMicrophoneCapture>();
                if (_microphoneInput == null) _microphoneInput = gameObject.AddComponent<HoloKitMicrophoneCapture>();
                Debug.Log($"[Recorder] No Microphone Input assigned; using {_microphoneInput.name}'s HoloKitMicrophoneCapture.");
            }
            if (needsMicrophone && (_microphoneInput == null || !_microphoneInput.IsReady))
            {
                if (_microphoneInput == null) { Fail("Assign a shared HoloKitMicrophoneCapture input."); return; }
                _leasedMicrophone = _microphoneInput;
                _microphoneInput.Acquire(this, Processing);
                if (_microphoneInput.IsPreparing) _prepare = StartCoroutine(PrepareRecording());
                else Fail(_microphoneInput.LastError ?? "Microphone is not ready.");
                return;
            }
            if (needsMicrophone)
            {
                _leasedMicrophone = _microphoneInput;
                if (!_microphoneInput.Acquire(this, Processing)) { Fail(_microphoneInput.LastError); return; }
            }
            if (_camera == null)
            {
                Fail("Camera must be ready before recording.");
                return;
            }

            var speakerMode = AudioSettings.speakerMode;
            int channels = speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            if (_audioMode == RecordingAudioMode.GameAndMicrophone && speakerMode != AudioSpeakerMode.Mono && speakerMode != AudioSpeakerMode.Stereo)
            {
                Fail($"Unsupported audio layout: {speakerMode}");
                return;
            }
            int rate = AudioSettings.outputSampleRate;
            bool game = _audioMode == RecordingAudioMode.GameAndMicrophone;
            if (game && (_gameAudioListener == null || !_gameAudioListener.isActiveAndEnabled))
            { Fail("Assign an active game AudioListener."); return; }
            int sources = (game ? 1 : 0) | (needsMicrophone ? 2 : 0);
            _recordingOrigin = HoloKit_AudioClockTime();
            int frameRate = _captureCameraIn4K ? 30 : 60;
            // Mono records the screen; Stereo a 16:9 movie filled from the mono AR camera view.
            bool stereo = _cameraManager.ScreenRenderMode == ScreenRenderMode.Stereo;
            var size = OutputSize(stereo, _stereoResolution, Screen.width, Screen.height);
            int width = size.x, height = size.y;
            var cameraSize = stereo ? CoverSize(Screen.width, Screen.height, width, height) : size;
            LastRecordingPath = Path.Combine(Application.temporaryCachePath,
                $"Record_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.mp4");
            if (HoloKitVideoRecorder_StartRecording(LastRecordingPath, width, height, frameRate, rate, channels, sources, needsMicrophone ? _microphoneInput.SampleRate : 0, _recordingOrigin, _gameGain, _microphoneGain, _createMixedSharingCopy ? 1 : 0) != 0)
            {
                Fail("Native writer could not start.");
                return;
            }

            _startedNative = true;
            LastSharingPath = _createMixedSharingCopy && sources == 3
                ? Path.ChangeExtension(LastRecordingPath, "share.mp4") : LastRecordingPath;
            _cameraRt = new RenderTexture(cameraSize.x, cameraSize.y, 24, RenderTextureFormat.ARGBHalf);
            bool bgra = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA32);
            _frameRt = new RenderTexture(width, height, 0, bgra ? RenderTextureFormat.BGRA32 : RenderTextureFormat.ARGB32);
            _cameraRt.Create();
            _frameRt.Create();
            _pending.Clear();
            _generation++;
            _cursor = 0;
            _useGpuPath = bgra && SystemInfo.supportsGraphicsFence &&
                (SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) != 0 &&
                HoloKitVideoRecorder_AllocateGPUSlots(SlotCount, width, height) == SlotCount;
            if (_useGpuPath) _slots = new Texture2D[SlotCount];
            else HoloKitVideoRecorder_ReleaseGPUSlots();
            SubmittedFrames = DroppedFrames = 0;
            _frameInterval = 1.0 / frameRate;
            _nextCaptureTime = 0;
            IsRecording = true;
            RecordingStartTime = Time.unscaledTime;
            HoloKitVideoRecorder_SetAudioGain(_gameGain, _microphoneGain);
            if (game)
            {
                _gameTap = _gameAudioListener.GetComponent<HoloKitGameAudioTap>();
                if (_gameTap == null) _gameTap = _gameAudioListener.gameObject.AddComponent<HoloKitGameAudioTap>();
                if (!_gameTap.isActiveAndEnabled || !_gameTap.Begin(this))
                { EndRecording(); Fail("Game audio tap is unavailable or already in use."); return; }
            }
            _enhancement = FindFirstObjectByType<ARBackgroundVideoEnhancementManager>();
            if (_enhancement != null) _enhancement.RecordingOverride = _captureCameraIn4K;
            ApplyRenderMode(_cameraManager.ScreenRenderMode);
            _capture = StartCoroutine(CaptureFrames());
            Debug.Log($"[Recorder] START {width}x{height} fps={frameRate} GPU={_useGpuPath} sources={sources} game={rate}/{channels} mic={_microphoneInput?.SampleRate ?? 0} path={LastRecordingPath}");
        }

        MicrophoneProcessing Processing => _audioMode == RecordingAudioMode.Environment
            ? (_environmentProcessing == MicrophoneProcessing.EchoCancellation ? MicrophoneProcessing.Environment : _environmentProcessing)
            : MicrophoneProcessing.EchoCancellation;

        IEnumerator PrepareRecording()
        {
            yield return null;
            while (_leasedMicrophone != null && _leasedMicrophone.IsPreparing) yield return null;
            _prepare = null;
            if (_leasedMicrophone != null && _leasedMicrophone.IsReady) StartRecording();
            else Fail(_leasedMicrophone?.LastError ?? "Microphone preparation was interrupted.");
        }
        void ReleaseMicrophone()
        {
            if (_leasedMicrophone != null) _leasedMicrophone.Release(this);
            _leasedMicrophone = null;
        }
        void Fail(string reason)
        {
            LastError = reason ?? "Recording audio could not start.";
            ReleaseMicrophone();
            Debug.LogError($"[Recorder] {LastError}");
        }

        public void EndRecording()
        {
            if (_prepare != null) { StopCoroutine(_prepare); _prepare = null; ReleaseMicrophone(); }
            if (!IsRecording) return;
            IsRecording = false;
            _draining = true;
            if (_capture != null) StopCoroutine(_capture);
            _capture = null;
            HoloKitVideoRecorder_StopAudio(HoloKit_AudioClockTime());
            if (_gameTap != null) _gameTap.End(this);
            // The native writer retains input until its timestamped tail drains.
            ReleaseMicrophone();

            // Callbacks outlive a disabled/destroyed camera; a coroutine would not.
            // Keep each texture alive until its exact GPU copy completes.
            Array.Clear(_stopResults, 0, _stopResults.Length);
            int generation = _generation;
            foreach (int index in _pending.ToArray())
            {
                _stopRequests[index] = AsyncGPUReadback.Request(_slots[index], 0, 0, 1, 0, 1, 0, 1,
                    request => CompleteStoppedSlot(index, request.hasError, generation));
            }
            FinishRecordingIfReady();
        }

        void CompleteStoppedSlot(int index, bool failed, int generation)
        {
            if (!_draining || generation != _generation) return;
            _stopResults[index] = failed ? (sbyte)-1 : (sbyte)1;
            // GPU callbacks may arrive out of order; video timestamps must not.
            while (TryDequeueCompletedSlot(_pending, _stopResults, out int ready, out bool success))
            {
                if (success) SubmitSlot(ready);
                else
                {
                    DroppedFrames++;
                    Debug.LogError("[Recorder] GPU copy could not be finalized.");
                }
            }
            FinishRecordingIfReady();
        }

        public static bool TryDequeueCompletedSlot(Queue<int> pending, sbyte[] results, out int index, out bool success)
        {
            index = pending.Count > 0 ? pending.Peek() : -1;
            success = index >= 0 && results[index] > 0;
            if (index < 0 || results[index] == 0) return false;
            pending.Dequeue();
            return true;
        }

        void FinishRecordingIfReady()
        {
            if (!_draining || _pending.Count > 0 || _readbacks.Count > 0) return;
            if (_enhancement != null) _enhancement.RecordingOverride = false;
            HoloKitVideoRecorder_EndRecording();
            if (_slots != null)
                foreach (var slot in _slots) if (slot != null) Destroy(slot);
            _slots = null;
            HoloKitVideoRecorder_ReleaseGPUSlots();
            _pending.Clear();
            Destroy(_cameraRt);
            Destroy(_frameRt);
            _cameraRt = _frameRt = null;
            if (_cameraManager != null) ApplyRenderMode(_cameraManager.ScreenRenderMode);
            _draining = false;
            Debug.Log($"[Recorder] STOP submitted={SubmittedFrames} dropped={DroppedFrames} path={LastRecordingPath}");
        }

        IEnumerator CaptureFrames()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (IsRecording)
            {
                yield return endOfFrame;
                if (IsRecording) CaptureFrame();
            }
        }

        void CaptureFrame()
        {
            DrainSlots();
            double timestamp = HoloKit_AudioClockTime() - _recordingOrigin;
            if (!TryAdvanceCaptureTime(timestamp, _frameInterval, ref _nextCaptureTime)) return;
            int index = _cursor;
            if (_useGpuPath)
            {
                if (_pending.Count == SlotCount) { DroppedFrames++; return; }
                IntPtr texture = HoloKitVideoRecorder_AcquireGPUSlot(index);
                if (texture == IntPtr.Zero) { DroppedFrames++; return; }
                if (_slots[index] == null)
                    _slots[index] = Texture2D.CreateExternalTexture(_frameRt.width, _frameRt.height,
                        TextureFormat.BGRA32, false, false, texture);
                else _slots[index].UpdateExternalTexture(texture);
            }

            // Preserve the live viewport aspect when the phone rotates during
            // a movie: fit it inside the movie's fixed dimensions, without stretch.
            var target = _camera.targetTexture;
            var rect = _camera.rect;
            try
            {
                var previousActive = RenderTexture.active;
                RenderTexture.active = _cameraRt;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = previousActive;
                _camera.targetTexture = _cameraRt;
                _camera.rect = FitViewport((float)Screen.width / Screen.height,
                    (float)_cameraRt.width / _cameraRt.height);
                _camera.Render();
            }
            finally { _camera.targetTexture = target; _camera.rect = rect; }
            // Flip for the encoder; in Stereo also crop the wider camera render to the movie's 16:9.
            CropToFill((float)_cameraRt.width / _cameraRt.height, (float)_frameRt.width / _frameRt.height,
                out var scale, out var offset);
            Graphics.Blit(_cameraRt, _frameRt, scale, offset);
            if (!_useGpuPath)
            {
                int id = ++_readbackId;
                _readbacks.Add(id, (default, timestamp));
                var request = AsyncGPUReadback.Request(_frameRt, 0, TextureFormat.RGBA32,
                    completed => SubmitReadback(completed, timestamp, id));
                if (_readbacks.ContainsKey(id)) _readbacks[id] = (request, timestamp);
                return;
            }

            Graphics.CopyTexture(_frameRt, _slots[index]);
            _fences[index] = Graphics.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation,
                SynchronisationStageFlags.AllGPUOperations);
            _timestamps[index] = timestamp;
            _pending.Enqueue(index);
            _cursor = (index + 1) % SlotCount;
        }

        public static bool TryAdvanceCaptureTime(double timestamp, double interval, ref double nextTime)
        {
            // Select the nearest scheduled tick, tolerating render-loop jitter.
            // Skip missed ticks after a stall instead of capturing a catch-up burst.
            if (timestamp + interval * 0.5 < nextTime) return false;
            nextTime += Math.Max(1, Math.Floor((timestamp - nextTime) / interval + 0.5) + 1) * interval;
            return true;
        }

        /// <summary>The movie size: the screen in Mono (multiples of 16), the chosen 16:9 size in Stereo.</summary>
        public static Vector2Int OutputSize(bool stereo, StereoRecordingResolution resolution, int screenWidth, int screenHeight)
        {
            if (!stereo)
                return new Vector2Int(screenWidth & ~15, screenHeight & ~15);
            return resolution == StereoRecordingResolution.UHD4K ? new Vector2Int(3840, 2160) : new Vector2Int(2560, 1440);
        }

        /// <summary>
        /// The smallest render at the screen's aspect that covers `output`: the AR camera renders
        /// undistorted at its own aspect, then <see cref="CropToFill"/> cuts the movie from its centre.
        /// </summary>
        public static Vector2Int CoverSize(int screenWidth, int screenHeight, int outputWidth, int outputHeight)
        {
            float screen = (float)screenWidth / screenHeight, output = (float)outputWidth / outputHeight;
            return screen >= output
                ? new Vector2Int(Mathf.CeilToInt(outputHeight * screen) & ~1, outputHeight)
                : new Vector2Int(outputWidth, Mathf.CeilToInt(outputWidth / screen) & ~1);
        }

        /// <summary>
        /// Blit scale and offset that take the centre of a `sourceAspect` image at `outputAspect`,
        /// flipped vertically for the encoder.
        /// </summary>
        public static void CropToFill(float sourceAspect, float outputAspect, out Vector2 scale, out Vector2 offset)
        {
            float sx = Mathf.Min(1f, outputAspect / sourceAspect), sy = Mathf.Min(1f, sourceAspect / outputAspect);
            scale = new Vector2(sx, -sy);
            offset = new Vector2((1f - sx) / 2f, (1f + sy) / 2f);
        }

        public static Rect FitViewport(float sourceAspect, float outputAspect)
        {
            if (sourceAspect > outputAspect)
            {
                float height = outputAspect / sourceAspect;
                return new Rect(0, (1 - height) / 2, 1, height);
            }
            float width = sourceAspect / outputAspect;
            return new Rect((1 - width) / 2, 0, width, 1);
        }

        void DrainSlots()
        {
            while (_pending.Count > 0 && _fences[_pending.Peek()].passed)
            {
                int index = _pending.Dequeue();
                SubmitSlot(index);
            }
        }

        void SubmitSlot(int index)
        {
            if (HoloKitVideoRecorder_SubmitGPUSlot(index, _timestamps[index]) == 0) SubmittedFrames++;
            else DroppedFrames++;
        }

        unsafe void SubmitReadback(AsyncGPUReadbackRequest request, double timestamp, int id)
        {
            if (!_readbacks.Remove(id)) return;
            try
            {
                if (request.hasError) { DroppedFrames++; return; }
                var data = request.GetData<byte>();
                int result = HoloKitVideoRecorder_AppendVideoFrame(
                    (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data), data.Length, timestamp);
                if (result == 0) SubmittedFrames++; else DroppedFrames++;
            }
            finally
            {
                FinishRecordingIfReady();
            }
        }

        void Update()
        {
            if (!IsRecording) return;
            HoloKitVideoRecorder_SetAudioGain(_gameGain, _microphoneGain);
            if (AudioFailed ||
                (_leasedMicrophone != null && !_leasedMicrophone.IsReady))
            {
                LastError = "Audio capture failed, overran its queue, or changed format; ending this segment.";
                Debug.LogError($"[Recorder] {LastError}");
                EndRecording();
            }
        }

        void AudioConfigurationChanged(bool deviceChanged)
        {
            if (!IsRecording) return;
            LastError = "Audio output configuration changed; ending this segment before its format or DSP clock changes.";
            Debug.LogWarning($"[Recorder] {LastError}");
            EndRecording();
        }

        void ApplyRenderMode(ScreenRenderMode mode)
        {
            bool renderMono = mode == ScreenRenderMode.Mono || IsRecording;
            if (_background != null) _background.enabled = renderMono;
            if (_camera != null) _camera.enabled = renderMono;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            EndRecording();
            // Unity stops dispatching managed callbacks while suspended. Finish
            // only this recorder's outstanding copies before that boundary.
            // Normal stop/disable/destroy remain asynchronous.
            foreach (int index in _pending.ToArray())
            {
                if (!_pending.Contains(index)) continue;
                var request = _stopRequests[index];
                request.WaitForCompletion();
                CompleteStoppedSlot(index, request.hasError, _generation);
            }
            foreach (int id in new List<int>(_readbacks.Keys))
            {
                if (!_readbacks.TryGetValue(id, out var pending)) continue;
                pending.request.WaitForCompletion();
                SubmitReadback(pending.request, pending.timestamp, id);
            }
        }
        void OnDisable() { EndRecording(); }
        void OnDestroy()
        {
            EndRecording();
            if (_cameraManager != null) _cameraManager.OnScreenRenderModeChanged -= ApplyRenderMode;
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
        }

        const string DllName = "__Internal";
        [DllImport(DllName)] public static extern int HoloKitVideoRecorder_StartRecording(string path, int width, int height, int frameRate, float sampleRate, int channels, int sources, int microphoneRate, double origin, float gameGain, float microphoneGain, int mixedCopy);
        [DllImport(DllName)] public static extern int HoloKitVideoRecorder_AppendVideoFrame(IntPtr data, int size, double time);
        [DllImport(DllName)] static extern IntPtr HoloKitVideoRecorder_LastError();
        [DllImport(DllName)] static extern double HoloKit_AudioClockTime();
        [DllImport(DllName)] static extern void HoloKitVideoRecorder_StopAudio(double hostTime);
        [DllImport(DllName)] static extern void HoloKitVideoRecorder_SetAudioGain(float game, float microphone);
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_AudioFailed();
        [DllImport(DllName)] public static extern void HoloKitVideoRecorder_EndRecording();
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_IsFinishing();
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_AllocateGPUSlots(int count, int width, int height);
        [DllImport(DllName)] static extern IntPtr HoloKitVideoRecorder_AcquireGPUSlot(int index);
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_SubmitGPUSlot(int index, double time);
        [DllImport(DllName)] static extern void HoloKitVideoRecorder_ReleaseGPUSlots();
    }
}
#endif
