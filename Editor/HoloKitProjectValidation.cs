// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoloKit.Editor
{
    /// <summary>
    /// HoloKit's project settings checks. They appear under Project Settings >
    /// XR Plug-in Management > Project Validation with a Fix button, and run
    /// before every build (<see cref="HoloKitBuildValidation"/>).
    /// </summary>
    [InitializeOnLoad]
    public static class HoloKitProjectValidation
    {
        const string Category = "HoloKit";
        const string Docs = "https://github.com/holokit/holokit-unity-sdk";
        const string ForceSpeakers = "Force IOS Speakers When Recording";

        /// iOS rules, also run by <see cref="HoloKitBuildValidation"/>.
        public static readonly BuildValidationRule[] iOSRules =
        {
            new()
            {
                Category = Category,
                Message = "'Adjust iOS FPS based on thermal state' caps rendering at " +
                          "the Serious thermal state FPS once the phone warms up. HoloKit reaches that state " +
                          "within minutes, and at 30 fps world-locked content steps and lags twice as far " +
                          "(measured on iPhone 15 Pro: 30 fps, 33 ms render-to-display; off: 120 fps, 17 ms).",
                CheckPredicate = () => !ThermalCapBelow60(),
                FixIt = () => SetPlayerSetting("adjustIOSFPSUsingThermalState", false),
                FixItMessage = "Turn off Player Settings > iOS > Other Settings > 'Adjust iOS FPS based on thermal state'.",
                HelpLink = Docs + "/blob/main/Documentation~/LowLatencyTracking.md",
                // A build with this setting stutters in Stereo after a few minutes.
                Error = true,
            },
            new()
            {
                Category = Category,
                Message = "Microphone Usage Description is empty. HoloKitVideoRecorder records the " +
                          "microphone, and iOS terminates an app that requests it without a description.",
                CheckPredicate = () => !string.IsNullOrWhiteSpace(PlayerSettings.iOS.microphoneUsageDescription),
                FixIt = () => PlayerSettings.iOS.microphoneUsageDescription =
                    "The microphone records sound with your HoloKit videos.",
                FixItMessage = "Set Player Settings > iOS > Other Settings > Microphone Usage Description.",
                HelpLink = Docs + "#ios-project-settings",
                Error = false,
            },
            new()
            {
                Category = Category,
                Message = "'Force iOS Speakers When Recording' is off. When anything in the app uses Unity's " +
                          "Microphone, iOS then routes all game audio to the earpiece, which is inaudible in " +
                          "HoloKit (measured on iPhone 15 Pro, iOS 27).",
                CheckPredicate = () => PlayerSettingsObject().FindProperty(ForceSpeakers)?.boolValue != false,
                FixIt = () => SetPlayerSetting(ForceSpeakers, true),
                FixItMessage = "Turn on Player Settings > iOS > Other Settings > 'Force iOS Speakers When Recording'.",
                HelpLink = Docs + "/blob/main/Documentation~/RecordingAudio.md",
                Error = false,
            },
        };

        /// Android rules, also run by <see cref="HoloKitBuildValidation"/>.
        public static readonly BuildValidationRule[] AndroidRules =
        {
            new()
            {
                Category = Category,
                Message = "HoloKit on Android needs OpenGLES3 only: turn off Auto Graphics API and remove Vulkan.",
                CheckPredicate = () => !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) &&
                                       PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)
                                           .SequenceEqual(new[] { GraphicsDeviceType.OpenGLES3 }),
                FixIt = () =>
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                },
                FixItMessage = "Player Settings > Android > Other Settings > Rendering: Auto Graphics API off, Graphics APIs = OpenGLES3.",
                HelpLink = Docs + "#android-project-settings",
                Error = false,
            },
        };

        static HoloKitProjectValidation()
        {
            BuildValidator.AddRules(BuildTargetGroup.iOS, iOSRules);
            BuildValidator.AddRules(BuildTargetGroup.Android, AndroidRules);
        }

        /// Rules for a platform, for the build check and tests.
        public static IReadOnlyList<BuildValidationRule> RulesFor(BuildTargetGroup group) => group switch
        {
            BuildTargetGroup.iOS => iOSRules,
            BuildTargetGroup.Android => AndroidRules,
            _ => System.Array.Empty<BuildValidationRule>(),
        };

        static bool ThermalCapBelow60()
        {
            var settings = PlayerSettingsObject();
            var adjust = settings.FindProperty("adjustIOSFPSUsingThermalState");
            var serious = settings.FindProperty("thermalStateSeriousIOSFPS");
            return adjust != null && adjust.boolValue && serious != null && serious.intValue < 60;
        }

        static SerializedObject PlayerSettingsObject() =>
            new(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));

        static void SetPlayerSetting(string name, bool value)
        {
            var settings = PlayerSettingsObject();
            settings.FindProperty(name).boolValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// Runs HoloKit's project validation rules before every build. Failing rules
    /// are logged; a rule marked as an error stops the build.
    /// </summary>
    class HoloKitBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (var rule in HoloKitProjectValidation.RulesFor(report.summary.platformGroup))
            {
                if (!rule.IsRuleEnabled() || rule.CheckPredicate())
                    continue;
                string text = $"[HoloKit] {rule.Message} Fix: {rule.FixItMessage} " +
                              "(Project Settings > XR Plug-in Management > Project Validation)";
                if (rule.Error)
                    throw new BuildFailedException(text);
                Debug.LogWarning(text);
            }
        }
    }
}
