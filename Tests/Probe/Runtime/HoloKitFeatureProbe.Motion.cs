// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_SDK_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HoloKit.iOS;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.Tests
{
public partial class HoloKitFeatureProbe
{
    const float MotionPeriod = 4f; // must match sdk_stimuli.html

    // Real-time tracking of a moving first-person hand: update rate, capture-to-result latency,
    // continuity, trajectory fidelity, depth stability, and gestures while moving.
    IEnumerator HandMotionSuite()
    {
        var tracking = FindFirstObjectByType<HandTrackingManager>();
        var arCamera = FindFirstObjectByType<ARCameraManager>();
        if (!Check("M0.present", tracking != null && tracking.HandPoseDetector != null && arCamera != null,
                "scene has HandTrackingManager and ARCameraManager")) yield break;
        var recognition = FindFirstObjectByType<HandGestureRecognitionManager>();
        if (recognition == null) recognition = gameObject.AddComponent<HandGestureRecognitionManager>();
        yield return null; // let it bind to the tracking detector

        var apple = new AppleNativeProvider();
        double lastFrameTime = -1;
        var latencies = new List<float>();
        var updates = new Dictionary<string, int>();
        Action<ARCameraFrameEventArgs> onFrame = args =>
        {
            if (args.timestampNs.HasValue) lastFrameTime = args.timestampNs.Value * 1e-9;
        };
        Action onUpdate = () =>
        {
            updates[m_Stimulus] = updates.TryGetValue(m_Stimulus, out int n) ? n + 1 : 1;
            if (lastFrameTime > 0 && Settled) latencies.Add((float)((apple.GetSystemUptime() - lastFrameTime) * 1000));
        };
        arCamera.frameReceived += onFrame;
        tracking.HandPoseDetector.OnHandPoseUpdated += onUpdate;
        var pinches = new List<(string stimulus, float time)>();
        Action<HandGesture> onGesture = g => { if (g == HandGesture.Pinched) pinches.Add((m_Stimulus, StimulusAge)); };
        recognition.OnHandGestureChanged += onGesture;

        var t = new List<float>(); var x = new List<float>(); var y = new List<float>(); var depth = new List<float>();
        var frames = new List<(float time, bool hand)>();
        var frameTimes = new List<float>();
        Transform camera = arCamera.transform;
        yield return During(() =>
        {
            if (m_Stimulus != "motion" || !Settled) return;
            frameTimes.Add(Time.unscaledDeltaTime * 1000);
            bool hand = tracking.HandCount > 0;
            frames.Add((StimulusAge, hand));
            if (!hand) return;
            Vector2 wrist = tracking.HandPoseDetector.HandPoses2D[0][JointName.Wrist];
            t.Add(StimulusAge); x.Add(wrist.x); y.Add(wrist.y);
            depth.Add(Vector3.Distance(tracking.HandPoseDetector.HandPoses3D[0][JointName.Wrist], camera.position));
        }, ("blank", 3), ("motion", 14), ("motion-pinch", 12), ("blank", 2));
        arCamera.frameReceived -= onFrame;
        tracking.HandPoseDetector.OnHandPoseUpdated -= onUpdate;
        recognition.OnHandGestureChanged -= onGesture;
        apple.Dispose();

        float seconds = 14 - Settle;
        float rate = (updates.TryGetValue("motion", out int u) ? u : 0) / 14f;
        // Vision runs on every FrameInterval-th AR frame (ARKit delivers 60 per second).
        float expected = 60f / tracking.FrameInterval;
        Check("M1.update-rate", rate >= 0.8f * expected,
            $"poseUpdates={rate:F1}/s expected={expected:F0}/s (frameInterval={tracking.FrameInterval}) medianFrame={Median(frameTimes):F1}ms");
        latencies.Sort();
        float p50 = latencies.Count > 0 ? latencies[latencies.Count / 2] : float.NaN;
        float p95 = latencies.Count > 0 ? latencies[(int)(latencies.Count * 0.95f)] : float.NaN;
        Check("M2.latency", latencies.Count > 20 && p95 < 100, $"captureToPose p50={p50:F0}ms p95={p95:F0}ms n={latencies.Count}");
        float detected = frames.Count == 0 ? 0 : frames.Count(f => f.hand) / (float)frames.Count;
        float gap = 0, run = 0, prev = -1;
        foreach (var f in frames)
        {
            if (prev >= 0 && !f.hand) run += f.time - prev; else if (f.hand) run = 0;
            gap = Mathf.Max(gap, run); prev = f.time;
        }
        // Where in the 4 s sweep are dropouts? Near 0.25/0.75 = hand at the screen edges (framing).
        var bins = new int[8];
        var totals = new int[8];
        foreach (var f in frames)
        {
            int bin = (int)((f.time % MotionPeriod) / MotionPeriod * 8) % 8;
            totals[bin]++;
            if (!f.hand) bins[bin]++;
        }
        Debug.Log("[SDK-MOTION] dropout by sweep phase (0..1 in eighths): " +
            string.Join(" ", Enumerable.Range(0, 8).Select(i => $"{i / 8f:F3}:{bins[i]}/{totals[i]}")));
        Check("M3.continuity", detected >= 0.9f && gap < 0.25f, $"detected={detected:F2} longestDropout={gap * 1000:F0}ms over {seconds:F1}s");
        float r2 = Mathf.Max(SineFit(t, x), SineFit(t, y));
        Check("M4.trajectory", t.Count >= 60 && r2 >= 0.9f, $"sineFitR2={r2:F3} points={t.Count} (x {SineFit(t, x):F3}, y {SineFit(t, y):F3})");
        float mean = depth.Count > 0 ? depth.Average() : float.NaN;
        float sd = depth.Count > 1 ? Mathf.Sqrt(depth.Sum(d => (d - mean) * (d - mean)) / (depth.Count - 1)) : float.NaN;
        Check("M5.depth-stability", depth.Count > 30 && sd < 0.05f, $"wristDistance mean={mean:F3}m sd={sd * 100:F1}cm");
        int moving = pinches.Count(p => p.stimulus == "motion-pinch");
        int still = pinches.Count(p => p.stimulus == "motion" && p.time > Settle);
        Check("M6.pinch-while-moving", moving >= 3 && still == 0, $"pinchesWhileMoving={moving} (6 pinch phases) pinchesOnOpenHand={still}");
    }

    const float HandVideoSeconds = 60f; // hand-test.mp4 (59.8 s)

    // Real first-person hand video on the Mac screen. Per-frame phone tracking is written to
    // Documents/sdk-handvideo.csv; the Mac runner compares it with Vision on the source frames.
    IEnumerator HandVideoSuite()
    {
        var tracking = FindFirstObjectByType<HandTrackingManager>();
        var arCamera = FindFirstObjectByType<ARCameraManager>();
        if (!Check("V0.present", tracking != null && tracking.HandPoseDetector != null && arCamera != null,
                "scene has HandTrackingManager and ARCameraManager")) yield break;
        var both = new AppleVisionHandPoseDetector(MaxHandCount.Both);
        Action<ARCameraFrameEventArgs> process = _ => both.ProcessCurrentFrame2D();
        arCamera.frameReceived += process;
        var csv = new System.Text.StringBuilder("t,hands,bothHands,x,y,pinch,depth\n");
        Transform camera = arCamera.transform;
        yield return During(() =>
        {
            if (m_Stimulus != "handvideo") return;
            int n = tracking.HandCount;
            string row = $"{StimulusAge:F4},{n},{both.HandCount}";
            if (n > 0)
            {
                var j = tracking.HandPoseDetector.HandPoses2D[0];
                float pinch = Vector2.Distance(j[JointName.ThumbTip], j[JointName.IndexTip]);
                float depth = Vector3.Distance(tracking.HandPoseDetector.HandPoses3D[0][JointName.Wrist], camera.position);
                row += $",{j[JointName.Wrist].x:F5},{j[JointName.Wrist].y:F5},{pinch:F5},{depth:F4}";
            }
            else row += ",,,,";
            csv.Append(row).Append('\n');
        }, ("blank", 2), ("handvideo", HandVideoSeconds + 1), ("blank", 1));
        arCamera.frameReceived -= process;
        both.Dispose();
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "sdk-handvideo.csv"), csv.ToString());
        Check("V0.recorded", csv.Length > 1000, $"csvBytes={csv.Length} (analysed by the Mac runner)");
    }

    const float GestureVideoSeconds = 46f; // gesture-test.mp4 (45.7 s)

    // Real hands making gestures while they move (gesture-test.mp4). The gesture a
    // HandGestureRecognitionManager reports, with the first hand's wrist for time alignment, goes to
    // Documents/sdk-gesturevideo.csv; the Mac runner applies the same classifier and filter to Vision
    // on the source frames and compares.
    IEnumerator GestureVideoSuite()
    {
        var tracking = FindFirstObjectByType<HandTrackingManager>();
        if (!Check("W0.present", tracking != null && tracking.HandPoseDetector != null,
                "scene has HandTrackingManager")) yield break;
        var gestures = tracking.gameObject.AddComponent<HandGestureRecognitionManager>();
        yield return null; // Start() subscribes to the tracking manager's detector
        var csv = new System.Text.StringBuilder("t,hands,gesture,x,y,pinchRatio\n");
        yield return During(() =>
        {
            if (m_Stimulus != "gesturevideo") return;
            var detector = tracking.HandPoseDetector;
            int n = tracking.HandCount;
            string row = $"{StimulusAge:F4},{n},{(int)gestures.HandGesture}";
            if (n > 0)
            {
                var j = detector.HandPoses2D[0];
                float? ratio = HandGestureClassifier.PinchDistance(j, detector.HandPosesConfidence[0], JointName.IndexTip);
                row += $",{j[JointName.Wrist].x:F5},{j[JointName.Wrist].y:F5},{(ratio.HasValue ? $"{ratio.Value:F4}" : "")}";
            }
            else row += ",,,";
            csv.Append(row).Append('\n');
        }, ("blank", 2), ("gesturevideo", GestureVideoSeconds + 1), ("blank", 1));
        Destroy(gestures);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "sdk-gesturevideo.csv"), csv.ToString());
        Check("W0.recorded", csv.Length > 1000, $"csvBytes={csv.Length} (analysed by the Mac runner)");
    }

    // R^2 of y ~ a*sin(wt) + b*cos(wt) + c at the stimulus frequency (phase-free).
    static float SineFit(List<float> t, List<float> v)
    {
        int n = t.Count;
        if (n < 10) return 0;
        double w = 2 * Math.PI / MotionPeriod;
        double[,] a = new double[3, 3]; double[] b = new double[3];
        for (int i = 0; i < n; i++)
        {
            double[] f = { Math.Sin(w * t[i]), Math.Cos(w * t[i]), 1 };
            for (int r = 0; r < 3; r++) { b[r] += f[r] * v[i]; for (int c = 0; c < 3; c++) a[r, c] += f[r] * f[c]; }
        }
        double[] k = Solve3(a, b);
        if (k == null) return 0;
        double mean = v.Average(), ssTot = 0, ssRes = 0;
        for (int i = 0; i < n; i++)
        {
            double fit = k[0] * Math.Sin(w * t[i]) + k[1] * Math.Cos(w * t[i]) + k[2];
            ssRes += (v[i] - fit) * (v[i] - fit);
            ssTot += (v[i] - mean) * (v[i] - mean);
        }
        return ssTot <= 0 ? 0 : (float)(1 - ssRes / ssTot);
    }

    static double[] Solve3(double[,] m, double[] y)
    {
        double Det(double[,] q) => q[0, 0] * (q[1, 1] * q[2, 2] - q[1, 2] * q[2, 1])
            - q[0, 1] * (q[1, 0] * q[2, 2] - q[1, 2] * q[2, 0]) + q[0, 2] * (q[1, 0] * q[2, 1] - q[1, 1] * q[2, 0]);
        double d = Det(m);
        if (Math.Abs(d) < 1e-12) return null;
        var result = new double[3];
        for (int c = 0; c < 3; c++)
        {
            var q = (double[,])m.Clone();
            for (int r = 0; r < 3; r++) q[r, c] = y[r];
            result[c] = Det(q) / d;
        }
        return result;
    }
}
}
#endif
