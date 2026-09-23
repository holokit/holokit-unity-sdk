// SPDX-License-Identifier: MIT
#if UNITY_IOS
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>Copies this application's listener output without changing playback.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioListener))]
    public sealed class HoloKitGameAudioTap : MonoBehaviour
    {
        sealed class Session
        {
            public HoloKitVideoRecorder Owner;
            public double Host, Dsp;
            public int Rate;
        }
        volatile Session session;
        public bool Begin(HoloKitVideoRecorder owner)
        {
            if (session != null) return false;
            // Bracket the DSP read on the native host clock. Choose the tightest
            // pair; a delayed main-thread sample must not shift an entire track.
            double span = double.PositiveInfinity, host = 0, dsp = 0;
            for (int i = 0; i < 5; i++)
            {
                double before = HoloKit_AudioClockTime();
                double candidate = AudioSettings.dspTime;
                double after = HoloKit_AudioClockTime();
                if (after - before < span)
                {
                    span = after - before; host = (before + after) * 0.5; dsp = candidate;
                }
            }
            session = new Session { Owner = owner, Host = host, Dsp = dsp, Rate = AudioSettings.outputSampleRate };
            return true;
        }
        public void End(HoloKitVideoRecorder owner)
        {
            if (session?.Owner == owner) session = null;
        }
        unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            var current = session;
            if (current == null || channels <= 0) return;
            double host = current.Host + AudioSettings.dspTime - current.Dsp;
            fixed (float* samples = data)
                HoloKit_RecordingAudioPush(1, (IntPtr)samples, data.Length / channels,
                    channels, current.Rate, host);
        }
        void OnDisable() { session = null; }
        [DllImport("__Internal")] static extern double HoloKit_AudioClockTime();
        [DllImport("__Internal")] static extern int HoloKit_RecordingAudioPush(int source,
            IntPtr data, int frames, int channels, int rate, double hostTime);
    }
}
#endif
