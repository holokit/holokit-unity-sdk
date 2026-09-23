// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_SDK_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HoloKit.iOS;
using HoloKit.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.Tests
{
public partial class HoloKitFeatureProbe
{
    IEnumerator UISuite()
    {
        var canvas = FindFirstObjectByType<HoloKitDefaultUICanvas>();
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        if (!Check("E0.default-ui", canvas != null && manager != null, "scene has HoloKitDefaultUICanvas")) yield break;
        // Invoke the actual Button wiring; a physical touch is not simulated here.
        Button button = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b =>
            Enumerable.Range(0, b.onClick.GetPersistentEventCount())
                .Any(i => b.onClick.GetPersistentMethodName(i) == nameof(HoloKitDefaultUICanvas.SwitchRenderMode)));
        if (!Check("E1.render-button", button != null, "a Button is wired to SwitchRenderMode")) yield break;
        ScreenRenderMode before = manager.ScreenRenderMode;
        button.onClick.Invoke();
        yield return new WaitForSecondsRealtime(2);
        ScreenRenderMode after = manager.ScreenRenderMode;
        string label = canvas.RenderModeButtonText != null ? canvas.RenderModeButtonText.text : "";
        string expected = after == ScreenRenderMode.Mono ? "Stereo" : "Mono";
        Check("E1.render-toggle", after != before && label == expected, $"{before}->{after} label='{label}'");
        yield return Capture($"ui-{after}");
        button.onClick.Invoke();
        yield return new WaitForSecondsRealtime(2);
        Check("E1.render-restore", manager.ScreenRenderMode == before, $"mode={manager.ScreenRenderMode}");
    }

    IEnumerator TrackingSuite()
    {
        var arkit = HoloKitARKitManager.Instance != null ? HoloKitARKitManager.Instance
            : new GameObject(nameof(HoloKitARKitManager)).AddComponent<HoloKitARKitManager>();
        var provider = arkit.ARKitNativeProvider;
        var apple = new AppleNativeProvider();
        var stamps = new List<double>();
        double maxAge = 0;
        Action<double, Matrix4x4> onFrame = (time, _) =>
        {
            double age = apple.GetSystemUptime() - time;
            lock (stamps) { stamps.Add(time); maxAge = Math.Max(maxAge, age); }
        };
        provider.OnARSessionUpdatedFrame += onFrame;
        provider.InterceptUnityARSessionDelegate();
        yield return new WaitForSecondsRealtime(3);
        provider.RestoreUnityARSessionDelegate();
        provider.OnARSessionUpdatedFrame -= onFrame;
        bool monotonic;
        int count;
        lock (stamps)
        {
            count = stamps.Count;
            monotonic = stamps.Zip(stamps.Skip(1), (a, b) => b > a).All(x => x);
        }
        Check("A6.native-frames", count >= 60 && monotonic && maxAge < 0.25,
            $"frames={count}/3s monotonic={monotonic} maxAge={maxAge * 1000:F0}ms");

        var arCamera = FindFirstObjectByType<ARCameraManager>();
        int unityFrames = 0;
        Action<ARCameraFrameEventArgs> countFrames = _ => unityFrames++;
        arCamera.frameReceived += countFrames;
        yield return new WaitForSecondsRealtime(2);
        arCamera.frameReceived -= countFrames;
        Check("A6.delegate-restored", unityFrames >= 40, $"unityFrames={unityFrames}/2s");

        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        var lowLatency = FindFirstObjectByType<LowLatencyTrackingManager>();
        if (manager != null && lowLatency != null)
        {
            manager.ScreenRenderMode = ScreenRenderMode.Stereo;
            int frames = 0;
            bool finite = true;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 3)
            {
                frames++;
                finite &= Finite(manager.CenterEyePose.position);
                yield return null;
            }
            manager.ScreenRenderMode = ScreenRenderMode.Mono;
            Check("A6.low-latency-stereo", finite && frames >= 60, $"frames={frames}/3s finitePose={finite}");
        }
        else Check("A6.low-latency-stereo", false, "LowLatencyTrackingManager or HoloKitCameraManager missing");

        Transform camera = arCamera.transform;
        Vector3 p0 = camera.localPosition;
        provider.ResetWorldOrigin(new Vector3(0.5f, 0, 0), Quaternion.identity);
        yield return new WaitForSecondsRealtime(2);
        Vector3 delta = camera.localPosition - p0;
        Check("A7.origin-translate", (delta - new Vector3(-0.5f, 0, 0)).magnitude < 0.1f, $"delta={delta:F3} expected=(-0.5,0,0)");
        float yaw0 = camera.localEulerAngles.y;
        provider.ResetWorldOrigin(Vector3.zero, Quaternion.Euler(0, 90, 0));
        yield return new WaitForSecondsRealtime(2);
        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yaw0, camera.localEulerAngles.y));
        Check("A7.origin-rotate", Mathf.Abs(yawDelta - 90) < 10, $"|yawDelta|={yawDelta:F1}");
        apple.Dispose();
    }

    IEnumerator EnhancementSuite()
    {
        var enhancement = FindFirstObjectByType<ARBackgroundVideoEnhancementManager>();
        var arCamera = FindFirstObjectByType<ARCameraManager>();
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        if (!Check("A8.present", enhancement != null && arCamera != null && manager != null, "scene has enhancement manager")) yield break;
        Vector2Int size = default;
        Action<ARCameraFrameEventArgs> onFrame = args =>
        {
            if (args.textures.Count > 0) size = new Vector2Int(args.textures[0].width, args.textures[0].height);
        };
        arCamera.frameReceived += onFrame;
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        enhancement.IsEnabled = false;
        yield return new WaitForSecondsRealtime(4);
        Vector2Int off = size;
        enhancement.IsEnabled = true;
        yield return new WaitForSecondsRealtime(5);
        Vector2Int on = size;
        Check("A8.enable", on.x * on.y > off.x * off.y, $"off={off} on={on}");
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSecondsRealtime(5);
        Vector2Int stereo = size;
        enhancement.RecordingOverride = true;
        yield return new WaitForSecondsRealtime(5);
        Vector2Int recording = size;
        enhancement.RecordingOverride = false;
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        enhancement.IsEnabled = false;
        yield return new WaitForSecondsRealtime(5);
        Vector2Int restored = size;
        arCamera.frameReceived -= onFrame;
        Check("A8.stereo-disables", stereo == off, $"stereo={stereo}");
        Check("A8.recording-override", recording == on, $"recording={recording}");
        Check("A8.restore", restored == off, $"restored={restored}");
        bool table = ARBackgroundVideoEnhancementManager.ShouldEnhance(true, false, ScreenRenderMode.Mono, true)
            && !ARBackgroundVideoEnhancementManager.ShouldEnhance(false, false, ScreenRenderMode.Mono, true)
            && !ARBackgroundVideoEnhancementManager.ShouldEnhance(true, false, ScreenRenderMode.Stereo, true)
            && ARBackgroundVideoEnhancementManager.ShouldEnhance(false, false, ScreenRenderMode.Stereo, false)
            && ARBackgroundVideoEnhancementManager.ShouldEnhance(false, true, ScreenRenderMode.Stereo, true);
        Check("A8.rules", table, "ShouldEnhance truth table");
    }

    IEnumerator MicrophoneSuite()
    {
        var microphone = FindFirstObjectByType<HoloKitMicrophoneCapture>();
        if (microphone == null) microphone = gameObject.AddComponent<HoloKitMicrophoneCapture>();
        var a = new GameObject("mic-owner-a");
        var b = new GameObject("mic-owner-b");
        var c = new GameObject("mic-owner-c");
        microphone.Acquire(a, MicrophoneProcessing.Environment);
        float deadline = Time.realtimeSinceStartup + 8;
        while (!microphone.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
        Check("G1.ready", microphone.IsReady && microphone.SampleRate > 0,
            $"ready={microphone.IsReady} rate={microphone.SampleRate} error={microphone.LastError}");
        bool second = microphone.Acquire(b, MicrophoneProcessing.Environment);
        Check("G1.shared", second && microphone.ConsumerCount == 2, $"second={second} consumers={microphone.ConsumerCount}");
        bool conflict = microphone.Acquire(c, MicrophoneProcessing.EchoCancellation);
        Check("G1.conflict-rejected", !conflict && microphone.ConsumerCount == 2, $"accepted={conflict} error={microphone.LastError}");
        microphone.Acquire(a, MicrophoneProcessing.Environment);
        Check("G1.idempotent", microphone.ConsumerCount == 2, $"consumers={microphone.ConsumerCount}");

        // The Mac runner plays the tone only when explicitly allowed; otherwise it reports G2 untested.
        var samples = new float[4096];
        var rms = new Dictionary<string, List<float>> { ["silence"] = new(), ["tone-440"] = new() };
        var ratio = new List<float>();
        float nextRead = 0;
        yield return During(() =>
        {
            if (!Settled || Time.realtimeSinceStartup < nextRead || !rms.ContainsKey(m_Stimulus)) return;
            nextRead = Time.realtimeSinceStartup + 0.2f;
            int n = microphone.ReadLatestSamples(samples);
            if (n <= 0) return;
            rms[m_Stimulus].Add(Mathf.Sqrt(samples.Take(n).Sum(s => s * s) / n));
            if (m_Stimulus == "tone-440")
            {
                int rate = microphone.SampleRate;
                float peak = Goertzel(samples, n, 440, rate);
                ratio.Add(peak / Mathf.Max(1e-9f, (Goertzel(samples, n, 330, rate) + Goertzel(samples, n, 560, rate)) / 2));
            }
        }, ("silence", 5), ("tone-440", 6), ("silence", 3));
        float quiet = Median(rms["silence"]), loud = Median(rms["tone-440"]), selectivity = Median(ratio);
        Debug.Log($"[SDK-AUDIO] silenceRms={quiet:F5} toneRms={loud:F5} ratio440={selectivity:F1}");
        Check("G2.tone-440", loud > 3 * quiet && selectivity > 5,
            $"silenceRms={quiet:F5} toneRms={loud:F5} ratio440={selectivity:F1}");

        microphone.Release(a);
        microphone.Release(b);
        microphone.Release(c);
        yield return null;
        Check("G1.released", microphone.ConsumerCount == 0 && !microphone.IsReady, $"consumers={microphone.ConsumerCount}");
        Destroy(a); Destroy(b); Destroy(c);
    }

    static float Goertzel(float[] samples, int count, float frequency, int rate)
    {
        float coefficient = 2 * Mathf.Cos(2 * Mathf.PI * frequency / rate);
        float s1 = 0, s2 = 0;
        for (int i = 0; i < count; i++)
        {
            float s = samples[i] + coefficient * s1 - s2;
            s2 = s1;
            s1 = s;
        }
        return Mathf.Sqrt(Mathf.Max(0, s1 * s1 + s2 * s2 - coefficient * s1 * s2)) / count;
    }
}
}
#endif
