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
    struct HandSample
    {
        public string stimulus;
        public float time;
        public int count, bothCount;
        public bool finite;
        public float depth, wristX, wristY;
    }

    IEnumerator HandSuite()
    {
        var tracking = FindFirstObjectByType<HandTrackingManager>();
        var arCamera = FindFirstObjectByType<ARCameraManager>();
        if (!Check("B0.present", tracking != null && tracking.HandPoseDetector != null && arCamera != null,
                "scene has HandTrackingManager and ARCameraManager")) yield break;
        int lost = 0;
        Action onLost = () => lost++;
        tracking.HandPoseDetector.OnHandPoseLost += onLost;
        // A second detector checks two-hand detection independently of the scene's MaxHandCount.
        var both = new AppleVisionHandPoseDetector(MaxHandCount.Both);
        Action<ARCameraFrameEventArgs> process = _ => both.ProcessCurrentFrame2D();
        arCamera.frameReceived += process;
        var samples = new List<HandSample>();
        Transform camera = arCamera.transform;
        int requestsBefore = tracking.HandPoseDetector.CompletedRequests;
        float suiteStart = Time.realtimeSinceStartup;
        bool shot = false;
        yield return During(() =>
        {
            // What the camera sees while the one-hand photo should be on screen.
            if (!shot && m_Stimulus == "one" && StimulusAge > 3)
            {
                ScreenCapture.CaptureScreenshot("sdk-hand-one.png");
                shot = true;
            }
            if (!Settled) return;
            var sample = new HandSample { stimulus = m_Stimulus, time = StimulusAge, count = tracking.HandCount,
                bothCount = both.HandCount, finite = true, depth = float.NaN, wristX = float.NaN, wristY = float.NaN };
            if (sample.count > 0)
            {
                var joints = tracking.HandPoseDetector.HandPoses3D[0];
                sample.finite = joints.Count == 21 && joints.Values.All(Finite);
                sample.depth = Vector3.Distance(joints[JointName.Wrist], camera.position);
                Vector2 wrist2D = tracking.HandPoseDetector.HandPoses2D[0][JointName.Wrist];
                sample.wristX = wrist2D.x;
                sample.wristY = wrist2D.y;
            }
            samples.Add(sample);
        }, ("blank", 8), ("one", 10), ("blank", 8), ("two", 10), ("sweep", 12), ("blank", 3));
        arCamera.frameReceived -= process;
        tracking.HandPoseDetector.OnHandPoseLost -= onLost;
        // Vision must run whether or not a hand is in view.
        float requestRate = (tracking.HandPoseDetector.CompletedRequests - requestsBefore) / (Time.realtimeSinceStartup - suiteStart);
        float expectedRequests = 60f / tracking.FrameInterval;
        Check("B0.vision-runs", requestRate >= 0.75f * expectedRequests,
            $"visionRequests={requestRate:F1}/s expected={expectedRequests:F0}/s secondDetector={both.CompletedRequests}");
        both.Dispose();

        float Fraction(string stimulus, Func<HandSample, bool> predicate)
        {
            var window = samples.Where(s => s.stimulus == stimulus).ToList();
            return window.Count == 0 ? 0 : window.Count(predicate) / (float)window.Count;
        }
        float blank = Fraction("blank", s => s.count == 0);
        Check("B1.no-hand", blank >= 0.9f, $"noHandFraction={blank:F2}");
        float one = Fraction("one", s => s.count >= 1);
        var detected = samples.Where(s => s.stimulus == "one" && s.count > 0).ToList();
        bool finite = detected.All(s => s.finite);
        float depth = Median(detected.Select(s => s.depth).ToList());
        Check("B2.one-hand", one >= 0.6f && finite, $"detectedFraction={one:F2} jointsFinite={finite}");
        Check("B2.depth", depth > 0.15f && depth < 2.5f, $"medianWristDistance={depth:F3}m");
        float two = Fraction("two", s => s.bothCount == 2);
        Check("B3.two-hands", two >= 0.4f, $"twoHandFraction={two:F2}");
        var sweep = samples.Where(s => s.stimulus == "sweep" && s.count > 0).ToList();
        // Vision reports camera-image coordinates, so a horizontal sweep maps to x or y by device orientation.
        var times = sweep.Select(s => s.time).ToList();
        float rx = Correlation(times, sweep.Select(s => s.wristX).ToList());
        float ry = Correlation(times, sweep.Select(s => s.wristY).ToList());
        float r = Mathf.Max(Mathf.Abs(rx), Mathf.Abs(ry));
        Check("B4.sweep", sweep.Count >= 20 && r >= 0.7f, $"points={sweep.Count} rX={rx:F2} rY={ry:F2}");
        Check("B5.lost-event", lost >= 1, $"lostEvents={lost}");
    }

    IEnumerator GestureSuite()
    {
        var recognition = FindFirstObjectByType<HandGestureRecognitionManager>();
        if (!Check("C0.present", recognition != null, "scene has HandGestureRecognitionManager")) yield break;
        var events = new List<(string stimulus, float time, HandGesture gesture)>();
        Action<HandGesture> onChanged = g =>
        {
            events.Add((m_Stimulus, StimulusAge, g));
            Debug.Log($"[SDK-GESTURE] {g} during={m_Stimulus} t={StimulusAge:F2}");
        };
        recognition.OnHandGestureChanged += onChanged;
        // Diagnostics use the same 2D joints the recognizer reads.
        AppleVisionHandPoseDetector detector = Field<AppleVisionHandPoseDetector>(recognition, "m_HandPoseDetector");
        var tracking = Field<HandTrackingManager>(recognition, "m_HandTrackingManager");
        if (tracking != null) detector = tracking.HandPoseDetector;
        var diagnostics = new Dictionary<string, (int frames, int hands, List<float> pinch, List<float> straight, List<float> indexAngle)>();
        yield return During(() =>
        {
            if (!Settled || detector == null) return;
            if (!diagnostics.TryGetValue(m_Stimulus, out var d)) d = (0, 0, new(), new(), new());
            d.frames++;
            if (detector.HandCount > 0)
            {
                var j = detector.HandPoses2D[0];
                d.hands++;
                d.pinch.Add(Vector2.Distance(j[JointName.ThumbTip], j[JointName.IndexTip]));
                d.straight.Add(StraightFingers(j));
                Vector2 index = j[JointName.IndexTip] - j[JointName.IndexMCP];
                d.indexAngle.Add(Mathf.Atan2(index.y, index.x) * Mathf.Rad2Deg);
            }
            diagnostics[m_Stimulus] = d;
        }, ("blank", 5), ("open", 5), ("pinch", 5), ("open", 5), ("pinch", 5), ("open", 5), ("pinch", 5), ("blank", 4));
        recognition.OnHandGestureChanged -= onChanged;
        foreach (var (stimulus, d) in diagnostics)
            Debug.Log($"[SDK-GESTURE-DIAG] {stimulus} frames={d.frames} handFrames={d.hands} pinchDistance={Median(d.pinch):F3} " +
                $"straightFingers={Median(d.straight):F1} indexAngle={Median(d.indexAngle):F0}deg");
        int pinchedIn = events.Count(e => e.gesture == HandGesture.Pinched && e.stimulus == "pinch");
        int pinchedOut = events.Count(e => e.gesture == HandGesture.Pinched && e.stimulus != "pinch" && e.time > Settle);
        Check("C1.pinch", pinchedIn >= 2 && pinchedOut == 0, $"pinchedInPinch={pinchedIn} pinchedElsewhere={pinchedOut}");
        int five = events.Count(e => e.gesture == HandGesture.Five && e.stimulus == "open");
        Check("C2.five", five >= 1, $"fiveInOpen={five}");
    }

    IEnumerator GazeSuite()
    {
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        var recognition = FindFirstObjectByType<HandGestureRecognitionManager>();
        var gaze = FindFirstObjectByType<GazeRaycastInteractor>();
        if (!Check("D0.present", manager != null && recognition != null, "scene has camera manager and gesture recognition"))
            yield break;
        // Report the scene wiring separately, then add missing interactors so the SDK components are still tested.
        Check("D0.scene-wiring", gaze != null && gaze.GetComponent<GazeGestureInteractor>() != null,
            $"sceneGazeInteractor={gaze != null} sceneGestureInteractor={gaze != null && gaze.GetComponent<GazeGestureInteractor>() != null}");
        if (gaze == null)
        {
            gaze = new GameObject("Probe Gaze Interactor").AddComponent<GazeRaycastInteractor>();
            gaze.gameObject.AddComponent<GazeGestureInteractor>();
            yield return null; // let Start() bind the camera manager and gesture events
        }
        else if (gaze.GetComponent<GazeGestureInteractor>() == null)
        {
            gaze.gameObject.AddComponent<GazeGestureInteractor>();
            yield return null;
        }
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.localScale = Vector3.one * 0.12f;
        var target = cube.AddComponent<ProbeGazeTarget>();
        bool onAxis = true;
        Action place = () =>
        {
            Transform eye = manager.CenterEyePose;
            cube.transform.position = eye.position + eye.forward * 0.35f + (onAxis ? Vector3.zero : eye.right * 0.6f);
            Physics.SyncTransforms();
        };
        yield return During(place, ("open", 4));
        Check("D1.enter-select", target.Entered >= 1 && target.SelectedFrames >= 20,
            $"entered={target.Entered} selectedFrames={target.SelectedFrames}");
        onAxis = false;
        yield return During(place, ("open", 3));
        Check("D1.exit", target.Exited >= 1 && gaze.Target == null, $"exited={target.Exited}");
        onAxis = true;
        yield return During(place, ("open", 3), ("pinch", 5), ("open", 4), ("pinch", 5));
        int selected = target.GestureSelected;
        Check("D2.gesture-select", selected >= 1, $"gestureSelected={selected}");
        onAxis = false;
        yield return During(place, ("open", 3), ("pinch", 5), ("open", 3));
        Check("D2.no-select-off-target", target.GestureSelected == selected, $"gestureSelected={target.GestureSelected}");
        Destroy(cube);
    }

    // Fingers the gesture classifier counts as straight (confidence ignored here).
    static float StraightFingers(Dictionary<JointName, Vector2> j)
    {
        JointName[][] fingers =
        {
            new[] { JointName.ThumbCMC, JointName.ThumbMP, JointName.ThumbIP, JointName.ThumbTip },
            new[] { JointName.IndexMCP, JointName.IndexPIP, JointName.IndexDIP, JointName.IndexTip },
            new[] { JointName.MiddleMCP, JointName.MiddlePIP, JointName.MiddleDIP, JointName.MiddleTip },
            new[] { JointName.RingMCP, JointName.RingPIP, JointName.RingDIP, JointName.RingTip },
            new[] { JointName.LittleMCP, JointName.LittlePIP, JointName.LittleDIP, JointName.LittleTip },
        };
        return fingers.Count(f => HandGestureClassifier.Straightness(j, null, f) >= HandGestureClassifier.MinimumStraightness);
    }

    static float Correlation(List<float> x, List<float> y)
    {
        if (x.Count < 3) return 0;
        float mx = x.Average(), my = y.Average();
        float sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < x.Count; i++)
        {
            sxy += (x[i] - mx) * (y[i] - my);
            sxx += (x[i] - mx) * (x[i] - mx);
            syy += (y[i] - my) * (y[i] - my);
        }
        return sxx == 0 || syy == 0 ? 0 : sxy / Mathf.Sqrt(sxx * syy);
    }
}

public class ProbeGazeTarget : MonoBehaviour, IGazeGestureInteractable
{
    public int Entered, Exited, SelectedFrames, GestureSelected;
    public void OnSelectionEntered() => Entered++;
    public void OnSelectionExited() => Exited++;
    public void OnSelected(float deltaTime) => SelectedFrames++;
    public void OnGestureSelected()
    {
        GestureSelected++;
        Debug.Log($"[SDK-GAZE] gesture selected count={GestureSelected}");
    }
}
}
#endif
