// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS && DEVELOPMENT_BUILD && HOLOKIT_SDK_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HoloKit.iOS;
using HoloKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HoloKit.Tests
{
public partial class HoloKitFeatureProbe
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    // The stereo bar, alignment guide, thermal frame rate and Stereo 2K / 4K recording on the phone.
    // Controls are driven through the bar's own handlers; a physical touch is not simulated.
    IEnumerator StereoBarSuite()
    {
        var manager = FindFirstObjectByType<HoloKitCameraManager>();
        if (!Check("S0.manager", manager != null, "scene has HoloKitCameraManager")) yield break;
        HoloKitStereoBarItems items = manager.StereoBarItems;
        manager.StereoBarItems = HoloKitStereoBarItems.All; // off by default
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSecondsRealtime(3);

        // Bar geometry on this phone: millimetres from the profile, pixels from the screen.
        var bar = manager.StereoBar;
        if (!Check("S1.bar", bar != null && bar.isActiveAndEnabled && bar.HasControls,
                $"bar={bar != null} active={bar != null && bar.isActiveAndEnabled} controls={bar != null && bar.HasControls}")) yield break;
        var layout = bar.Layout;
        // Screen width from iOS's own DPI, against the profile's millimetres.
        float screenMm = Mathf.Max(Screen.width, Screen.height) / Screen.dpi * 25.4f;
        Check("S1.layout", layout.BandHeight > 8f && layout.HalfWidth * 2f > 120f && layout.HalfWidth * 2f < 170f,
            $"band={layout.BandHeight:F1}mm width={layout.HalfWidth * 2f:F1}mm (iOS dpi: {screenMm:F1}mm) screen={Screen.width}x{Screen.height}");
        var canvas = FindFirstObjectByType<HoloKitDefaultUICanvas>();
        Check("S1.canvas-hidden", canvas == null || !canvas.GetComponent<Canvas>().enabled, "the Default UI Canvas gives way to the bar");
        yield return Capture("stereobar");

        // Alignment guide: shown on entering Stereo, hidden after the delay without a touch.
        var guide = manager.AlignmentGuide;
        if (Check("S2.guide", guide != null && guide.IsVisible, $"guide={guide != null} visible={guide != null && guide.IsVisible}"))
        {
            guide.AutoHideDelay = 2f;
            yield return new WaitForSecondsRealtime(3f);
            Check("S2.guide-autohide", !guide.IsVisible, $"visible after 3 s without a touch={guide.IsVisible}");
            guide.AutoHideDelay = 10f;
        }

        // TRIGGER: one event per press; the label flashes, then shows the app's own text.
        int fired = 0;
        Action onTrigger = () => fired++;
        manager.OnStereoTrigger += onTrigger;
        manager.StereoTriggerLabel = "FIRE";
        bar.FireTrigger();
        yield return null;
        var label = (Text)typeof(HoloKitStereoBar).GetField("m_TriggerLabel", Any).GetValue(bar);
        string pressed = label.text;
        yield return new WaitForSecondsRealtime(0.6f);
        Check("S3.trigger", fired == 1 && pressed == "TRIGGERED" && label.text == "FIRE",
            $"events={fired} pressed='{pressed}' after='{label.text}'");
        manager.OnStereoTrigger -= onTrigger;
        manager.StereoTriggerLabel = "TRIGGER";

        // Holding TRIGGER through its touch handler leaves the other controls and the marker shown.
        var trigger = bar.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "TRIGGER");
        var triggerArea = trigger != null ? trigger.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(b => b.GetType().Name == "HoloKitBarTouchArea") : null;
        var triggerHeld = triggerArea != null ? triggerArea.GetType().GetField("Held", Any).GetValue(triggerArea) as Action<bool> : null;
        if (Check("S3.trigger-control", triggerHeld != null, "TRIGGER has a hold handler"))
        {
            triggerHeld(true);
            yield return null;
            var press = (HoloKitBarPress)typeof(HoloKitStereoBar).GetField("m_Press", Any).GetValue(bar);
            Check("S3.trigger-keeps-bar", press.Held == null, $"held control={press.Held?.ToString() ?? "none"} while TRIGGER is down");
            triggerHeld(false);
        }

        // Frame rate follows the thermal state.
        var apple = new AppleNativeProvider();
        int thermal = (int)apple.GetThermalState();
        apple.Dispose();
        int configured = Field<int>(manager, "m_StereoFrameRate");
        int maximum = configured > 0 ? configured : HoloKitCameraManager.DefaultStereoFrameRate;
        int expected = HoloKitCameraManager.StereoFrameRate(maximum, thermal, manager.AdaptStereoFrameRateToThermalState);
        yield return new WaitForSecondsRealtime(1.5f);
        Check("S4.framerate", Application.targetFrameRate == expected,
            $"target={Application.targetFrameRate} expected={expected} thermal={thermal} maximum={maximum}");

        // Stereo takes through the bar's RECORD handler: 2K with the HD camera, 4K with the 4K camera.
        var recorder = manager.GetComponentInChildren<HoloKitVideoRecorder>();
        if (Check("S5.recorder", recorder != null, "camera rig has a HoloKitVideoRecorder"))
        {
            var resolution = recorder.StereoResolution;
            bool camera4K = recorder.CaptureCameraIn4K;
            yield return StereoTake(bar, recorder, "2k", StereoRecordingResolution.QHD2K, false);
            yield return StereoTake(bar, recorder, "4k", StereoRecordingResolution.UHD4K, true);
            recorder.StereoResolution = resolution;
            recorder.CaptureCameraIn4K = camera4K;
        }

        // MONO: the bar's tap handler leaves Stereo; the bar hides and the canvas returns.
        var mono = bar.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MONO");
        var area = mono != null ? mono.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(b => b.GetType().Name == "HoloKitBarTouchArea") : null;
        var tapped = area != null ? area.GetType().GetField("Tapped", Any).GetValue(area) as Action : null;
        if (Check("S7.mono-control", tapped != null, "MONO has a tap handler"))
        {
            tapped();
            yield return new WaitForSecondsRealtime(1.5f);
            Check("S7.mono", manager.ScreenRenderMode == ScreenRenderMode.Mono && !bar.isActiveAndEnabled &&
                (canvas == null || canvas.GetComponent<Canvas>().enabled),
                $"mode={manager.ScreenRenderMode} bar={bar.isActiveAndEnabled} canvas={canvas == null || canvas.GetComponent<Canvas>().enabled}");
        }
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        manager.StereoBarItems = items;
    }

    IEnumerator StereoTake(HoloKitStereoBar bar, HoloKitVideoRecorder recorder, string name,
        StereoRecordingResolution resolution, bool camera4K)
    {
        recorder.StereoResolution = resolution;
        recorder.CaptureCameraIn4K = camera4K;
        var toggle = typeof(HoloKitStereoBar).GetMethod("ToggleRecording", Any);
        long memoryBefore = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
        toggle.Invoke(bar, null);
        float deadline = Time.realtimeSinceStartup + 8;
        while (!recorder.IsRecording && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Check($"S6.{name}-start", recorder.IsRecording, $"error={recorder.LastError}")) yield break;
        // Display frame rate while recording.
        int frames = 0;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 5f) { frames++; yield return null; }
        float fps = frames / (Time.realtimeSinceStartup - start);
        long memoryDuring = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
        yield return Capture($"stereobar-recording-{name}");
        toggle.Invoke(bar, null);
        deadline = Time.realtimeSinceStartup + 20;
        while ((recorder.IsRecording || recorder.IsFinishing) && Time.realtimeSinceStartup < deadline) yield return null;
        string movie = recorder.LastRecordingPath;
        bool exists = !string.IsNullOrEmpty(movie) && File.Exists(movie);
        if (exists)
            File.Copy(movie, Path.Combine(Application.persistentDataPath, $"sdk-movie-stereo-{name}.mp4"), true);
        Check($"S6.{name}-stop", exists && recorder.SubmittedFrames > 0,
            $"frames={recorder.SubmittedFrames} dropped={recorder.DroppedFrames} displayFps={fps:F0} " +
            $"unityMemoryMB={memoryBefore >> 20}->{memoryDuring >> 20} error={recorder.LastError}");
        Debug.Log($"[SDK-MOVIE] stereo-{name} {movie}");
        yield return new WaitForSecondsRealtime(1);
    }
}
}
#endif
