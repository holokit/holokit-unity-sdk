// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HoloKit.Tests.Editor
{
    /// <summary>
    /// Builds the development iOS player for the device probes (HOLOKIT_SDK_TESTS,
    /// HOLOKIT_RECORDER_TESTS) from the package's own samples: it first copies Samples~ into the
    /// carrier project (HoloKitSampleSync), so the probe never runs stale scenes. The scene list,
    /// defines, bundle id and the thermal frame-rate cap (which the SDK's validation rejects) apply
    /// to this build only.
    ///   Unity -batchmode -quit -projectPath &lt;carrier&gt; -buildTarget iOS \
    ///     -executeMethod HoloKit.Tests.Editor.HoloKitProbeBuild.Build -buildOutput Build_Probe [-probeBundleId id]
    /// </summary>
    public static class HoloKitProbeBuild
    {
        /// <summary>Sample scenes the probe suites load (see HoloKitFeatureProbe.Scenes) or the recorder probes use.</summary>
        public static readonly string[] Scenes =
        {
            "StereoscopicRendering/Scenes/StereoscopicRendering.unity",
            "LowLatencyTracking/Scenes/LowLatencyTracking.unity",
            "HandTracking/Scenes/HandTracking.unity",
            "HandGestureRecognition/Scenes/HandGestureRecognition.unity",
            "PinchToPlace/Scenes/PinchToPlace.unity",
            "GazeGestureInteraction/Scenes/GazeGestureInteraction.unity",
            "GazeInteraction/Scenes/GazeInteraction.unity",
            "ResetWorldOrigin/Scenes/ResetWorldOrigin.unity",
            "ARBackgroundVideoEnhancement/Scenes/ARBackgroundVideoEnhancement.unity",
            "RecordingAudio/Scenes/RecordingEnvironment.unity",
            "RecordingAudio/Scenes/RecordingGameAndMicrophone.unity",
        };

        [MenuItem("HoloKit/Samples/Build Device Probe")]
        public static void Build()
        {
            HoloKitSampleSync.CopyIntoProject();
            string[] scenes = Scenes.Select(s => $"{HoloKitSampleSync.ProjectSamples}/{s}").ToArray();
            string[] missing = scenes.Where(s => !File.Exists(s)).ToArray();
            if (missing.Length > 0)
                throw new Exception("Probe scenes missing: " + string.Join(", ", missing));

            string output = Argument("-buildOutput") ?? "Build_Probe";
            string bundleId = Argument("-probeBundleId");
            string previousId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var thermalCap = settings.FindProperty("adjustIOSFPSUsingThermalState");
            bool previousCap = thermalCap.boolValue;
            try
            {
                if (bundleId != null)
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
                thermalCap.boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "HOLOKIT_SDK_TESTS", "HOLOKIT_RECORDER_TESTS" },
                };
                Debug.Log($"[HoloKitProbeBuild] {scenes.Length} scenes -> {output} bundle={bundleId ?? previousId}");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception($"Probe build failed: {report.summary.result}");
                // The orientation suite's ARKit reference goes into this export only, not into apps.
                string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HoloKitProbeBuild).Assembly).resolvedPath;
                File.AppendAllText(Path.Combine(output, "Libraries/io.holokit.unity-sdk/Runtime/iOS/NativeCode/HoloKitARKitNativeProvider.mm"),
                    "\n" + File.ReadAllText(Path.Combine(package, "Tests/Probe/Native~/ARKitCameraReference.mm")));
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, previousId);
                thermalCap.boolValue = previousCap;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
