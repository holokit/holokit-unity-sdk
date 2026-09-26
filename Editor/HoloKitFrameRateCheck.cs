// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HoloKit.Editor
{
    /// <summary>
    /// Warns when an iOS build will drop to 30 fps as the phone warms up.
    /// </summary>
    /// <remarks>
    /// "Adjust iOS FPS based on thermal state" caps Unity at
    /// <c>thermalStateSeriousIOSFPS</c> (30 by default) once iOS reports a serious
    /// thermal state, which HoloKit reaches within minutes. At 30 fps stereo
    /// content steps visibly and the display lags twice as far behind the head.
    /// Measured on iPhone 15 Pro: 30 fps and 33 ms render-to-display with the
    /// setting on, 120 fps and 17 ms with it off.
    /// </remarks>
    class HoloKitFrameRateCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var adjust = settings.FindProperty("adjustIOSFPSUsingThermalState");
            var serious = settings.FindProperty("thermalStateSeriousIOSFPS");
            if (adjust != null && adjust.boolValue && serious != null && serious.intValue < 60)
            {
                Debug.LogWarning(
                    "[HoloKit] Player Settings > iOS > 'Adjust iOS FPS based on thermal state' will cap stereo " +
                    $"rendering at {serious.intValue} fps when the phone warms up, making content step and lag. " +
                    "Turn it off, or raise the Serious thermal state FPS to 60 or more. See Documentation~/LowLatencyTracking.md.");
            }
        }
    }
}
