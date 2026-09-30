// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_SDK_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HoloKit.iOS;
using UnityEngine;

namespace HoloKit.Tests
{
public partial class HoloKitFeatureProbe
{
    // Low Latency Tracking sample: prediction on in Stereo, with display latency, lead time and IMU
    // freshness in the ranges measured on iPhone 15 Pro (LowLatencyTracking.md).
    IEnumerator LowLatencySuite()
    {
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        var tracking = FindFirstObjectByType<LowLatencyTrackingManager>();
        var grid = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "TrackingGrid");
        if (!Check("L0.present", manager != null && tracking != null && grid != null,
                $"camera={manager != null} lowLatency={tracking != null} trackingGrid={grid != null}")) yield break;
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSecondsRealtime(2);
        int frames = 0, predicted = 0;
        var latency = new List<float>();
        var lead = new List<float>();
        var imuAge = new List<float>();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 4)
        {
            frames++;
            if (tracking.IsPredicting) predicted++;
            if (tracking.PresentLatency > 0) latency.Add((float)tracking.PresentLatency * 1000);
            lead.Add((float)tracking.PredictionLeadTime * 1000);
            if (tracking.IMUSampleAge >= 0) imuAge.Add((float)tracking.IMUSampleAge * 1000);
            yield return null;
        }
        float fps = frames / (Time.realtimeSinceStartup - start);
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        Check("L1.predicting", predicted >= 0.9f * frames, $"predicted {predicted}/{frames} frames");
        float latencyMs = Median(latency), leadMs = Median(lead), ageMs = Median(imuAge);
        Check("L2.present-latency", latencyMs > 4 && latencyMs < 50, $"renderToPresent p50={latencyMs:F1}ms n={latency.Count}");
        Check("L3.lead-time", leadMs > 4 && leadMs < 70, $"predictionLead p50={leadMs:F1}ms");
        Check("L4.imu-fresh", imuAge.Count > 0 && ageMs >= 0 && ageMs < 25, $"imuSampleAge p50={ageMs:F1}ms");
        Check("L5.frame-rate", fps >= 0.8f * Application.targetFrameRate || Application.targetFrameRate <= 0,
            $"fps={fps:F0} target={Application.targetFrameRate}");
    }

    // Pinch To Place sample: its fingertip dots and placement, then placement on the pinch photos.
    IEnumerator PinchToPlaceSuite()
    {
        var sample = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetType().Name == "PinchToPlace");
        var tracking = FindFirstObjectByType<HandTrackingManager>();
        if (!Check("P0.present", sample != null && tracking != null && sample.enabled,
                $"pinchToPlace={sample != null} handTracking={tracking != null} enabled={sample != null && sample.enabled}")) yield break;
        yield return null;
        int dots = sample.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Fingertip Dot"));
        Check("P1.fingertip-dots", dots == 10, $"dots={dots} (5 per hand)");

        // Placement without a camera: a synthetic middle-finger pinch 40 cm ahead.
        Vector3 ahead = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 0.4f : new Vector3(0, 0, 0.4f);
        var world = new Dictionary<JointName, Vector3>
        {
            [JointName.ThumbTip] = ahead + new Vector3(-0.005f, 0, 0),
            [JointName.MiddleTip] = ahead + new Vector3(0.005f, 0, 0),
        };
        sample.GetType().GetMethod("Place", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(sample, new object[] { PinchFinger.Middle, world });
        yield return new WaitForSecondsRealtime(0.3f);
        var placed = GameObject.Find("Placed Middle");
        Check("P2.place", placed != null && Vector3.Distance(placed.transform.position, ahead) < 0.001f &&
            placed.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Cube") && placed.transform.localScale.x > 0.02f,
            placed == null ? "nothing placed" : $"at {Vector3.Distance(placed.transform.position, ahead) * 1000:F1}mm from the pinch, " +
            $"mesh={placed.GetComponent<MeshFilter>().sharedMesh.name} size={placed.transform.localScale.x:F3}m");
        if (placed != null) Destroy(placed);

        // On the stimulus page: objects appear while the pinch photo shows, none on the open hand.
        var events = new List<string>();
        int Count() => FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m => m.name.StartsWith("Placed "));
        int before = Count();
        int last = before;
        yield return During(() =>
        {
            int now = Count();
            for (; last < now; last++) events.Add(m_Stimulus);
            last = now;
        }, ("blank", 4), ("open", 5), ("pinch", 5), ("open", 5), ("pinch", 5), ("blank", 3));
        int inPinch = events.Count(s => s == "pinch"), elsewhere = events.Count(s => s != "pinch");
        Check("P3.pinch-places", inPinch >= 1 && elsewhere == 0, $"placedInPinch={inPinch} placedElsewhere={elsewhere}");
    }
}
}
#endif
