// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_SDK_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HoloKit.iOS;
using UnityEngine;

namespace HoloKit.Tests
{
public partial class HoloKitFeatureProbe
{
    const int UnityMicRate = 48000;

    // An app's own Unity Microphone next to the recorder's native microphone: does either break the
    // other, and does the Unity input level change when the recorder picks a processing mode?
    IEnumerator CoexistSuite()
    {
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        var recorder = manager != null ? manager.GetComponentInChildren<HoloKitVideoRecorder>() : null;
        if (!Check("X0.present", recorder != null && Microphone.devices.Length > 0,
                $"recorder={recorder != null} unityMicrophones={Microphone.devices.Length}")) yield break;

        // Spoken audio from the Mac for the whole suite (plus room noise), so both inputs hear the same voice.
        StartCoroutine(Stimulus("speech", 0));
        yield return new WaitForSecondsRealtime(1);

        // 1. The app's Unity microphone.
        AudioClip app = Microphone.Start(null, true, 2, UnityMicRate);
        float deadline = Time.realtimeSinceStartup + 5;
        while (Microphone.GetPosition(null) <= 0 && Time.realtimeSinceStartup < deadline) yield return null;
        var baseline = new UnityMicWindow();
        yield return baseline.Measure(app, 3, "baseline");
        Check("X1.unity-mic", baseline.Advancing, baseline.Describe());

        // 2. Record with each processing while the Unity microphone keeps running.
        var listener = FindFirstObjectByType<AudioListener>();
        foreach (var (label, mode, processing) in new[] {
                     ("default", RecordingAudioMode.Environment, MicrophoneProcessing.Default),
                     ("raw", RecordingAudioMode.Environment, MicrophoneProcessing.Environment),
                     ("aec", RecordingAudioMode.GameAndMicrophone, MicrophoneProcessing.EchoCancellation) })
        {
            recorder.AudioMode = mode;
            recorder.EnvironmentProcessing = processing == MicrophoneProcessing.EchoCancellation ? MicrophoneProcessing.Environment : processing;
            recorder.IncludeMicrophone = true;
            if (mode == RecordingAudioMode.GameAndMicrophone) recorder.GameAudioListener = listener;
            recorder.CaptureCameraIn4K = false;
            recorder.StartRecording();
            deadline = Time.realtimeSinceStartup + 8;
            while (!recorder.IsRecording && Time.realtimeSinceStartup < deadline) yield return null;
            bool started = recorder.IsRecording;
            var during = new UnityMicWindow();
            var nativeRms = new List<float>();
            var native = new float[1024];
            yield return during.Measure(app, 4, $"{label}-recording", () =>
            {
                var input = recorder.MicrophoneInput;
                if (input == null || !input.IsReady) return;
                int n = input.ReadLatestSamples(native);
                if (n > 0) nativeRms.Add(Mathf.Sqrt(native.Take(n).Sum(s => s * s) / n));
            });
            recorder.EndRecording();
            deadline = Time.realtimeSinceStartup + 15;
            while ((recorder.IsRecording || recorder.IsFinishing) && Time.realtimeSinceStartup < deadline) yield return null;
            string movie = recorder.LastRecordingPath;
            long bytes = !string.IsNullOrEmpty(movie) && System.IO.File.Exists(movie) ? new System.IO.FileInfo(movie).Length : 0;
            Debug.Log($"[SDK-MOVIE] {label} {movie}");
            var after = new UnityMicWindow();
            yield return after.Measure(app, 3, $"{label}-after");
            float change = during.LevelDb - baseline.LevelDb, recover = after.LevelDb - baseline.LevelDb;
            Check($"X2.{label}.recorder", started && bytes > 0,
                $"started={started} bytes={bytes} nativeRms={Median(nativeRms):F5} error={recorder.LastError}");
            Check($"X2.{label}.unity-alive", during.Advancing && after.Advancing,
                $"during {during.Describe()} | after {after.Describe()}");
            // Informational: how much the recorder's processing changed the app's Unity input level.
            Check($"X2.{label}.unity-level", true,
                $"Unity mic level vs baseline: during {change:+0.0;-0.0} dB, after {recover:+0.0;-0.0} dB (native {20 * Mathf.Log10(Mathf.Max(Median(nativeRms), 1e-6f)):F1} dBFS)");
        }

        // 3. Native microphone first, then a fresh Unity microphone.
        Microphone.End(null);
        yield return new WaitForSecondsRealtime(1);
        var input2 = recorder.MicrophoneInput;
        var owner = new GameObject("coexist-owner");
        if (input2 != null)
        {
            input2.Acquire(owner, MicrophoneProcessing.Default);
            deadline = Time.realtimeSinceStartup + 6;
            while (!input2.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            var buffer = new float[1024];
            int before = input2.ReadLatestSamples(buffer);
            float nativeBefore = before > 0 ? Mathf.Sqrt(buffer.Take(before).Sum(s => s * s) / before) : 0;
            AudioClip late = Microphone.Start(null, true, 2, UnityMicRate);
            var lateWindow = new UnityMicWindow();
            yield return lateWindow.Measure(late, 3, "native-then-unity");
            int afterCount = input2.ReadLatestSamples(buffer);
            float nativeAfter = afterCount > 0 ? Mathf.Sqrt(buffer.Take(afterCount).Sum(s => s * s) / afterCount) : 0;
            Check("X3.native-then-unity", input2.IsReady && afterCount > 0 && lateWindow.Advancing,
                $"nativeReady={input2.IsReady} nativeRms {nativeBefore:F5}->{nativeAfter:F5} error={input2.LastError} | unity {lateWindow.Describe()}");
            input2.Release(owner);
            Microphone.End(null);
        }
        Destroy(owner);
        yield return new WaitForSecondsRealtime(1);

        // 4. Two Unity Microphone.Start calls on the same device.
        AudioClip first = Microphone.Start(null, true, 2, UnityMicRate);
        yield return new WaitForSecondsRealtime(1);
        int firstPosition = Microphone.GetPosition(null);
        AudioClip second = Microphone.Start(null, true, 2, UnityMicRate);
        yield return new WaitForSecondsRealtime(1);
        var firstWindow = new UnityMicWindow();
        yield return firstWindow.Measure(first, 2, "first-after-second-start");
        Check("X4.double-unity-start", true,
            $"sameClip={ReferenceEquals(first, second)} secondNull={second == null} positionBefore={firstPosition} " +
            $"firstClip {firstWindow.Describe()}");
        Microphone.End(null);
    }

    // Samples a looping Unity microphone clip: position progress and RMS of the newest 1024 samples.
    sealed class UnityMicWindow
    {
        public float LevelDb = -120, Rate;
        public bool Advancing;
        readonly List<float> rms = new();

        public IEnumerator Measure(AudioClip clip, float seconds, string label, System.Action perFrame = null)
        {
            if (clip == null) { Debug.Log($"[SDK-COEXIST] {label} clip=null"); yield break; }
            int length = clip.samples, last = Microphone.GetPosition(null);
            long advanced = 0;
            var data = new float[1024];
            float end = Time.realtimeSinceStartup + seconds, start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < end)
            {
                perFrame?.Invoke();
                int position = Microphone.GetPosition(null);
                advanced += (position - last + length) % length;
                last = position;
                int from = (position - data.Length + length) % length;
                if (clip.GetData(data, from)) rms.Add(Mathf.Sqrt(data.Sum(s => s * s) / data.Length));
                yield return null;
            }
            Rate = advanced / (Time.realtimeSinceStartup - start);
            Advancing = Rate > UnityMicRate * 0.8f;
            var sorted = rms.OrderBy(v => v).ToList();
            float median = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
            LevelDb = 20 * Mathf.Log10(Mathf.Max(median, 1e-6f));
            Debug.Log($"[SDK-COEXIST] {label} {Describe()}");
        }

        public string Describe() => $"rate={Rate:F0}/s level={LevelDb:F1} dBFS";
    }
}
}
#endif
