// SPDX-License-Identifier: MIT
#if UNITY_IOS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>How iOS processes the shared microphone input. Values are serialized; append only.</summary>
    public enum MicrophoneProcessing
    {
        /// <summary>Raw environmental level (Measurement mode): no automatic gain or voice processing.</summary>
        Environment,
        /// <summary>Voice processing with echo cancellation, for recording game audio and voice together.</summary>
        EchoCancellation,
        /// <summary>iOS default input processing, without voice processing: about 17 dB above
        /// Environment, but speech still reads low, about -47 dBFS 5 cm from the microphone
        /// (iPhone 15 Pro, iOS 27). Use Voice to react to speech.</summary>
        Default,
        /// <summary>Voice processing with automatic gain: speech at a steady level whatever the
        /// distance, the app's own sound cancelled and steady noise suppressed. For apps that react to
        /// the user's voice; levels are not calibrated.</summary>
        Voice,
    }

    /// <summary>Which built-in microphone to prefer. Values are serialized; append only.</summary>
    public enum MicrophonePosition
    {
        /// <summary>iOS chooses (usually the bottom microphone).</summary>
        Automatic,
        Bottom,
        Front,
        /// <summary>Beside the rear cameras, facing what the camera sees.</summary>
        Back,
    }

    /// <summary>One shared native input for recording and application analysis.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class HoloKitMicrophoneCapture : MonoBehaviour
    {
        static HoloKitMicrophoneCapture activeInput;
        readonly HashSet<UnityEngine.Object> consumers = new();
        Coroutine preparing;
        bool ownsNativeReference, paused;
        MicrophoneProcessing processing;
        // Restarts after another component stopped the engine; more than MaxRestarts within
        // RestartWindow seconds means two inputs keep fighting, and the input reports an error instead.
        const int MaxRestarts = 3;
        const float RestartWindow = 5f;
        int restarts;
        float firstRestart;
        [SerializeField, Tooltip("Built-in microphone to prefer (omnidirectional where supported). Applies to " +
            "Environment and Default processing; iOS chooses the input under voice processing.")]
        MicrophonePosition _position = MicrophonePosition.Automatic;
        /// <summary>Takes effect the next time the input starts.</summary>
        public MicrophonePosition Position { get => _position; set => _position = value; }
        public bool IsReady { get; private set; }
        public bool IsPreparing => preparing != null;
        public string LastError { get; private set; }
        public int SampleRate => IsReady ? HoloKit_MicrophoneSampleRate() : 0;
        public int ConsumerCount => consumers.Count;
        public MicrophoneProcessing Processing => processing;

        /// <summary>Whether iOS has granted microphone permission; false in the Editor. Use this rather than
        /// Unity's Application.HasUserAuthorization, which stays false in a build that does not use Unity's
        /// Microphone class (Unity then compiles its permission check out).</summary>
        public static bool HasPermission => !Application.isEditor && HoloKit_MicrophonePermission() == 1;

        /// <summary>Call on the main thread. Repeated acquisition by the same owner is idempotent.</summary>
        public bool Acquire(UnityEngine.Object owner, MicrophoneProcessing requested)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (!isActiveAndEnabled) { LastError = "Microphone input must be enabled."; return false; }
            if (activeInput != null && activeInput != this)
            {
                LastError = "Share the same HoloKitMicrophoneCapture component between audio consumers.";
                return false;
            }
            if (consumers.Count > 0 && processing != requested)
            {
                LastError = "The shared microphone is already using a different processing mode.";
                return false;
            }
            if (consumers.Add(owner) && consumers.Count == 1)
            {
                activeInput = this;
                processing = requested;
                LastError = null;
                if (!paused) preparing = StartCoroutine(Prepare());
            }
            return IsReady;
        }

        public void Release(UnityEngine.Object owner)
        {
            consumers.Remove(owner);
            if (consumers.Count != 0) return;
            StopInput();
            if (activeInput == this) activeInput = null;
        }

        IEnumerator Prepare()
        {
            // Ensure the Coroutine handle is assigned even on immediate failure.
            yield return null;
            if (Application.isEditor) LastError = "Native audio capture requires an iOS device.";
            else
            {
                if (HoloKit_MicrophonePermission() == 0)
                {
                    HoloKit_MicrophoneRequestPermission();
                    while (HoloKit_MicrophonePermission() == 0) yield return null;
                }
                Debug.Log($"[Microphone] Native permission={HoloKit_MicrophonePermission()} mode={processing}");
                if (HoloKit_MicrophonePermission() != 1)
                    LastError = "Microphone permission was denied.";
                else if (SetPositionAndStart() != 0)
                    LastError = NativeError() ?? "Could not start the microphone.";
                else
                {
                    ownsNativeReference = true;
                    float deadline = Time.realtimeSinceStartup + 3;
                    while (HoloKit_MicrophoneIsRunning() != 0 && HoloKit_MicrophoneBufferedSamples() == 0 &&
                           Time.realtimeSinceStartup < deadline) yield return null;
                    LastError = NativeError();
                    IsReady = LastError == null && HoloKit_MicrophoneIsRunning() != 0 &&
                        HoloKit_MicrophoneBufferedSamples() > 0;
                    if (!IsReady) LastError ??= "Microphone did not provide timestamped samples.";
                }
            }
            preparing = null;
            if (!IsReady && ownsNativeReference)
            {
                HoloKit_MicrophoneStop();
                ownsNativeReference = false;
            }
        }

        int SetPositionAndStart()
        {
            HoloKit_MicrophoneSetPosition((int)_position);
            return HoloKit_MicrophoneStart((int)processing);
        }

        public int ReadLatestSamples(float[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (!IsReady) { Array.Clear(samples, 0, samples.Length); return 0; }
            return HoloKit_MicrophoneReadLatest(samples, samples.Length);
        }

        /// <summary>1024 Blackman-Harris spectrum bins, from DC to Nyquist.</summary>
        public void ReadSpectrum(float[] spectrum)
        {
            if (spectrum == null || spectrum.Length != 1024)
                throw new ArgumentException("Supply a reusable array of 1024 spectrum bins.", nameof(spectrum));
            if (IsReady) HoloKit_MicrophoneReadSpectrum(spectrum, spectrum.Length);
            else Array.Clear(spectrum, 0, spectrum.Length);
        }

        void Update()
        {
            if (!IsReady) return;
            string error = NativeError();
            // iOS stops the engine when another component reconfigures audio input, e.g. Unity's
            // Microphone.Start after this input started, or a route change; rebuild it for the consumers.
            if (error == null && HoloKit_MicrophoneIsRunning() == 0 && TryRestart()) return;
            error ??= NativeError();
            if (error != null || HoloKit_MicrophoneIsRunning() == 0)
            {
                LastError = error ?? "Microphone stopped after an audio-route change or interruption.";
                IsReady = false;
            }
        }
        bool TryRestart()
        {
            if (Time.realtimeSinceStartup - firstRestart > RestartWindow)
            {
                restarts = 0;
                firstRestart = Time.realtimeSinceStartup;
            }
            if (++restarts > MaxRestarts || HoloKit_MicrophoneRestart() != 0) return false;
            Debug.Log($"[Microphone] restarted after an audio configuration change ({restarts} in {RestartWindow:F0} s)");
            return true;
        }
        void StopInput()
        {
            if (preparing != null) StopCoroutine(preparing);
            preparing = null;
            IsReady = false;
            if (ownsNativeReference) HoloKit_MicrophoneStop();
            ownsNativeReference = false;
        }
        void OnApplicationPause(bool value)
        {
            paused = value;
            if (paused) StopInput();
            else if (consumers.Count > 0 && isActiveAndEnabled)
            {
                LastError = null;
                preparing = StartCoroutine(Prepare());
            }
        }
        void OnDisable()
        {
            StopInput();
            consumers.Clear();
            if (activeInput == this) activeInput = null;
        }
        static string NativeError()
        {
            IntPtr error = HoloKit_MicrophoneLastError();
            return error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(error);
        }
        const string Library = "__Internal";
        [DllImport(Library)] static extern int HoloKit_MicrophonePermission();
        [DllImport(Library)] static extern void HoloKit_MicrophoneRequestPermission();
        [DllImport(Library)] static extern int HoloKit_MicrophoneStart(int processing);
        [DllImport(Library)] static extern void HoloKit_MicrophoneSetPosition(int position);
        [DllImport(Library)] static extern void HoloKit_MicrophoneStop();
        [DllImport(Library)] static extern int HoloKit_MicrophoneRestart();
        [DllImport(Library)] static extern int HoloKit_MicrophoneIsRunning();
        [DllImport(Library)] static extern int HoloKit_MicrophoneSampleRate();
        [DllImport(Library)] static extern int HoloKit_MicrophoneBufferedSamples();
        [DllImport(Library)] static extern IntPtr HoloKit_MicrophoneLastError();
        [DllImport(Library)] static extern int HoloKit_MicrophoneReadLatest([Out] float[] destination, int count);
        [DllImport(Library)] static extern int HoloKit_MicrophoneReadSpectrum([Out] float[] destination, int bins);
    }
}
#endif
