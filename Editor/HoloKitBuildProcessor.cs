// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace HoloKit.Editor
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

                string unityFrameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

                project.SetBuildProperty(unityFrameworkTargetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");

                // Info.plist: NSPhotoLibraryAddUsageDescription for saving recordings
                string plistPath = buildPath + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromFile(plistPath);

                PlistElementDict rootDict = plist.root;

                // NSMicrophoneUsageDescription comes from Player Settings (checked by HoloKitProjectValidation).
                // Set NSPhotoLibraryAddUsageDescription if not set
                if (!rootDict.values.ContainsKey("NSPhotoLibraryAddUsageDescription"))
                {
                    rootDict.SetString("NSPhotoLibraryAddUsageDescription", "Allow us to save your video recordings in your photo library for viewing and sharing.");
                }

                File.WriteAllText(plistPath, plist.WriteToString());

                project.WriteToFile(projectPath);
            }
        }
    }
}
#endif