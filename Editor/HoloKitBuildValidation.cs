// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HoloKit.Editor
{
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
