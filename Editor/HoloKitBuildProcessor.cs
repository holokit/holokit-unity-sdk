// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace HoloInteractive.XR.HoloKit.Editor
{
    public static class HoloKitBuildProcessor
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget == BuildTarget.iOS)
            {
                // For build settings
                string projectPath = PBXProject.GetPBXProjectPath(buildPath);
                PBXProject project = new();
                project.ReadFromString(File.ReadAllText(projectPath));

                string mainTargetGuid = project.GetUnityMainTargetGuid();
                string unityFrameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

                project.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
                project.SetBuildProperty(unityFrameworkTargetGuid, "ENABLE_BITCODE", "NO");
                project.SetBuildProperty(unityFrameworkTargetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
                project.AddBuildProperty(unityFrameworkTargetGuid, "OTHER_LDFLAGS", "-ld64");

                project.WriteToFile(projectPath);
            }
        }
    }
}
#endif