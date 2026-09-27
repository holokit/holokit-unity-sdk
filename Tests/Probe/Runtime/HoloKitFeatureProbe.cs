// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_SDK_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HoloKit.iOS;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.XR.ARFoundation;

// Keep this assembly through managed code stripping; nothing in a scene references it.
[assembly: AlwaysLinkAssembly]

// Device feature probe. Runs only when a one-shot Documents/sdk-test-mode.txt exists in a
// development build with HOLOKIT_SDK_TESTS. Stimuli are requested with "[SDK-STIM] <name> <s>"
// console lines; Tools/run_feature_probe.py shows them in Safari or plays them on the Mac.
namespace HoloKit.Tests
{
[Preserve]
public partial class HoloKitFeatureProbe : MonoBehaviour
{
    static readonly string[] AllSuites =
        { "device", "rendering", "orientation", "ui", "tracking", "enhancement", "microphone", "hand", "handmotion", "handvideo", "gesture", "gaze", "coexist", "stereobar" };
    static readonly Dictionary<string, string> Scenes = new()
    {
        ["device"] = "StereoscopicRendering", ["rendering"] = "StereoscopicRendering",
        ["orientation"] = "StereoscopicRendering", ["ui"] = "StereoscopicRendering",
        ["tracking"] = "ResetWorldOrigin", ["enhancement"] = "ARBackgroundVideoEnhancement",
        ["microphone"] = "StereoscopicRendering", ["hand"] = "HandTracking", ["handmotion"] = "HandTracking", ["handvideo"] = "HandTracking", ["coexist"] = "StereoscopicRendering", ["stereobar"] = "StereoscopicRendering",
        ["gesture"] = "HandGestureRecognition", ["gaze"] = "GazeGestureInteraction",
    };
    const float Settle = 1.5f;

    [Serializable] class Result { public string id, detail; public bool pass; }
    [Serializable] class Report
    {
        public string runId, deviceModel, operatingSystem;
        public bool completed;
        public int failures;
        public List<Result> results = new();
    }

    static string[] s_Suites;
    static string s_RunId;
    static bool s_ExpectLiDAR = true;
    readonly Report m_Report = new();
    string m_Stimulus = "none";
    float m_StimulusStart;
    int m_Exceptions;
    bool m_SequenceDone;

    float StimulusAge => Time.realtimeSinceStartup - m_StimulusStart;
    bool Settled => StimulusAge > Settle;

    [Preserve, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string path = Path.Combine(Application.persistentDataPath, "sdk-test-mode.txt");
        if (!File.Exists(path)) return;
        string[] lines = File.ReadAllText(path).Trim().Split('\n');
        File.Delete(path);
        string first = lines[0].Trim();
        s_Suites = first == "all" ? AllSuites : first.Split(',');
        s_RunId = lines.Length > 1 ? lines[1].Trim() : "";
        // Optional "scene.<suite>=<scene>" and "expect.lidar=0" overrides.
        for (int i = 2; i < lines.Length; i++)
        {
            string[] pair = lines[i].Trim().Split('=');
            if (pair.Length != 2) continue;
            if (pair[0].StartsWith("scene.")) Scenes[pair[0].Substring(6)] = pair[1];
            else if (pair[0] == "expect.lidar") s_ExpectLiDAR = pair[1] != "0";
        }
        var probe = new GameObject(nameof(HoloKitFeatureProbe)).AddComponent<HoloKitFeatureProbe>();
        DontDestroyOnLoad(probe.gameObject);
    }

    IEnumerator Start()
    {
        m_Report.runId = s_RunId;
        m_Report.deviceModel = SystemInfo.deviceModel;
        m_Report.operatingSystem = SystemInfo.operatingSystem;
        Application.logMessageReceived += OnLog;
        // Unattended runs have no touches; keep the phone from auto-locking mid-suite.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        foreach (string suite in s_Suites)
        {
            Debug.Log($"[SDK-TEST] SUITE {suite}");
            if (!Scenes.TryGetValue(suite, out string scene)) { Check(suite, false, "unknown suite"); continue; }
            yield return LoadScene(scene);
            m_Exceptions = 0;
            yield return suite switch
            {
                "device" => DeviceSuite(),
                "rendering" => RenderingSuite(),
                "orientation" => OrientationSuite(),
                "ui" => UISuite(),
                "tracking" => TrackingSuite(),
                "enhancement" => EnhancementSuite(),
                "microphone" => MicrophoneSuite(),
                "hand" => HandSuite(),
                "handmotion" => HandMotionSuite(),
                "handvideo" => HandVideoSuite(),
                "coexist" => CoexistSuite(),
                "stereobar" => StereoBarSuite(),
                "gesture" => GestureSuite(),
                "gaze" => GazeSuite(),
                _ => null,
            };
            Check($"{suite}.no-exceptions", m_Exceptions == 0, $"exceptions={m_Exceptions}");
        }
        yield return Stimulus("done", 0);
        m_Report.completed = true;
        Write();
        Debug.Log($"[SDK-TEST] COMPLETE failures={m_Report.failures}");
    }

    void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception) m_Exceptions++;
    }

    IEnumerator LoadScene(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Check($"scene.{scene}", false, "scene is not in the build");
            yield break;
        }
        yield return SceneManager.LoadSceneAsync(scene);
        float deadline = Time.realtimeSinceStartup + 15;
        while (ARSession.state < ARSessionState.SessionTracking && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return new WaitForSecondsRealtime(2);
        Debug.Log($"[SDK-TEST] SCENE {scene} session={ARSession.state}");
    }

    protected bool Check(string id, bool pass, string detail)
    {
        m_Report.results.Add(new Result { id = id, pass = pass, detail = detail });
        if (!pass) m_Report.failures++;
        Debug.Log($"[SDK-TEST] {(pass ? "PASS" : "FAIL")} {id} {detail}");
        Write();
        return pass;
    }

    void Write() => File.WriteAllText(Path.Combine(Application.persistentDataPath, "sdk-probe-results.json"),
        JsonUtility.ToJson(m_Report, true));

    IEnumerator Stimulus(string name, float seconds)
    {
        m_Stimulus = name;
        m_StimulusStart = Time.realtimeSinceStartup;
        Debug.Log($"[SDK-STIM] {name} {seconds:F1}");
        if (seconds > 0) yield return new WaitForSecondsRealtime(seconds);
    }

    IEnumerator Sequence((string name, float seconds)[] steps)
    {
        m_SequenceDone = false;
        foreach (var (name, seconds) in steps) yield return Stimulus(name, seconds);
        m_SequenceDone = true;
    }

    // Runs the stimulus steps while calling perFrame once per rendered frame.
    IEnumerator During(Action perFrame, params (string, float)[] steps)
    {
        StartCoroutine(Sequence(steps));
        while (!m_SequenceDone)
        {
            perFrame();
            yield return null;
        }
    }

    IEnumerator Capture(string name)
    {
        string file = $"sdk-{name}.png";
        ScreenCapture.CaptureScreenshot(file);
        yield return new WaitForSecondsRealtime(1);
        Debug.Log($"[SDK-SHOT] {file}");
    }

    static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
        float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    static float Median(List<float> values)
    {
        if (values.Count == 0) return float.NaN;
        var sorted = new List<float>(values);
        sorted.Sort();
        return sorted[sorted.Count / 2];
    }

    IEnumerator DeviceSuite()
    {
        bool lidar = DeviceData.SupportsLiDAR();
        Check("A9.lidar", lidar == s_ExpectLiDAR, $"supportLiDAR={lidar} expected={s_ExpectLiDAR}");
        var apple = new AppleNativeProvider();
        AppleThermalState state = apple.GetThermalState();
        Check("A9.thermal", Enum.IsDefined(typeof(AppleThermalState), state), $"state={state}");
        double uptime0 = apple.GetSystemUptime();
        float real0 = Time.realtimeSinceStartup;
        yield return new WaitForSecondsRealtime(2);
        double elapsed = apple.GetSystemUptime() - uptime0;
        float realElapsed = Time.realtimeSinceStartup - real0;
        Check("A9.uptime", Math.Abs(elapsed - realElapsed) < 0.05, $"uptime={elapsed:F3}s realtime={realElapsed:F3}s");
        apple.Dispose();
    }

    IEnumerator RenderingSuite()
    {
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        if (!Check("A0.camera-manager", manager != null, "scene has HoloKitCameraManager")) yield break;
        PhoneModel model = manager.PhoneModel;
        Check("A1.phone-model", model.ModelName == SystemInfo.deviceModel,
            $"device={SystemInfo.deviceModel} profile={model.ModelName} ({model.Description}) dpi={model.ModelSpecs.ScreenDpi}");

        int events = 0;
        Action<ScreenRenderMode> onChanged = _ => events++;
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        manager.OnScreenRenderModeChanged += onChanged;
        yield return Stimulus("fiducial", 1);
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSecondsRealtime(2);
        int afterStereo = events;
        manager.ScreenRenderMode = ScreenRenderMode.Stereo; // no-op must not raise
        yield return null;
        Check("A2.stereo-event", manager.ScreenRenderMode == ScreenRenderMode.Stereo && afterStereo == 1 && events == 1,
            $"mode={manager.ScreenRenderMode} events={events}");

        var left = Field<Camera>(manager, "m_LeftEyeCamera");
        var right = Field<Camera>(manager, "m_RightEyeCamera");
        Rect l = left.rect, r = right.rect;
        bool inside = l.xMin >= 0 && r.xMax <= 1.0001f && l.yMin >= 0 && l.yMax <= 1.0001f;
        bool ordered = l.xMax <= r.xMin + 0.0001f;
        bool equalSize = Mathf.Abs(l.width - r.width) < 0.001f && Mathf.Abs(l.height - r.height) < 0.001f;
        bool symmetric = Mathf.Abs(l.xMin - (1 - r.xMax)) < 0.002f;
        Check("A3.viewports", left.gameObject.activeInHierarchy && right.gameObject.activeInHierarchy &&
            inside && ordered && equalSize && symmetric, $"left={l} right={r}");
        float ipd = Vector3.Distance(left.transform.position, right.transform.position);
        Check("A3.eye-separation", ipd > 0.05f && ipd < 0.08f, $"ipd={ipd:F4}m");
        Vector3 offset = Field<Vector3>(manager, "m_CameraToCenterEyeOffset");
        Check("A5.center-eye-stereo", (manager.CenterEyePose.localPosition - offset).magnitude < 1e-4f && offset.magnitude > 0.01f,
            $"local={manager.CenterEyePose.localPosition:F4} offset={offset:F4}");
        yield return Capture("stereo");

        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        yield return new WaitForSecondsRealtime(2);
        Check("A2.mono-event", manager.ScreenRenderMode == ScreenRenderMode.Mono && events == 2, $"events={events}");
        Check("A5.center-eye-mono", manager.CenterEyePose.localPosition.magnitude < 1e-4f,
            $"local={manager.CenterEyePose.localPosition:F4}");
        yield return Capture("mono");
        manager.OnScreenRenderModeChanged -= onChanged;
    }

    IEnumerator OrientationSuite()
    {
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        if (manager != null) manager.ScreenRenderMode = ScreenRenderMode.Mono;
        StartCoroutine(Stimulus("fiducial", 0));
        if (SystemInfo.operatingSystem.Contains(" 27."))
        {
            var arCamera = FindFirstObjectByType<ARCameraManager>();
            Check("A4.ios27-compatibility", arCamera != null && arCamera.GetComponent<ARKitCameraCompatibility>() != null,
                "iOS 27 camera compatibility attached to this scene's AR camera");
        }
        foreach (var orientation in new[] { ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft,
                     ScreenOrientation.LandscapeRight, ScreenOrientation.PortraitUpsideDown })
        {
            Screen.orientation = orientation;
            yield return new WaitForSecondsRealtime(3);
            // The Mac runner compares the fiducial layout across these screenshots. The AR camera pose
            // separates a physically moved phone from a wrongly rotated background.
            Transform camera = FindFirstObjectByType<ARCameraManager>()?.transform;
            Vector3 position = camera != null ? camera.position : Vector3.zero;
            Vector3 forward = camera != null ? camera.forward : Vector3.zero;
            Vector3 up = camera != null ? camera.up : Vector3.zero;
            Debug.Log($"[SDK-ORIENT] requested={orientation} effective={Screen.orientation} size={Screen.width}x{Screen.height} " +
                $"pos={position.x:F4},{position.y:F4},{position.z:F4} fwd={forward.x:F4},{forward.y:F4},{forward.z:F4} up={up.x:F4},{up.y:F4},{up.z:F4}");
            yield return Capture($"orient-{orientation}");
            if (Screen.orientation == orientation)
            {
                // Read the projection that was actually rendered (compatibility code sets it before render).
                yield return new WaitForEndOfFrame();
                var fov = MeasureFov(FindFirstObjectByType<ARCameraManager>()?.GetComponent<Camera>());
                Check($"A4.fov-{orientation}", fov.valid && fov.matches,
                    $"unity {fov.unityH:F2}x{fov.unityV:F2} native {fov.nativeH:F2}x{fov.nativeV:F2} deg");
            }
        }
        Screen.orientation = ScreenOrientation.AutoRotation;
        Check("A4.orientation-captured", true, "screenshots are evaluated by the Mac runner");
    }
}
}
#endif
