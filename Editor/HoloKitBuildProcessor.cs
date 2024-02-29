// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
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

                string mainTargetGuid = project.GetUnityMainTargetGuid();
                string unityFrameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

                project.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
                project.SetBuildProperty(unityFrameworkTargetGuid, "ENABLE_BITCODE", "NO");
                project.SetBuildProperty(unityFrameworkTargetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
                project.AddBuildProperty(unityFrameworkTargetGuid, "OTHER_LDFLAGS", "-ld64");

                // Modify Info.plist for NSMicrophoneUsageDescription and NSPhotoLibraryAddUsageDescription
                string plistPath = buildPath + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromFile(plistPath);

                PlistElementDict rootDict = plist.root;

                // Set NSMicrophoneUsageDescription if not set
                // We have to fill this text in Unity Editor's Project Settings window
                //if (!rootDict.values.ContainsKey("NSMicrophoneUsageDescription"))
                //{
                //    rootDict.SetString("NSMicrophoneUsageDescription", "To record audio with your videos, we need access to your microphone.");
                //}

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