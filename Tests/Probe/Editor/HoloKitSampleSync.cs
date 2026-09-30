// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HoloKit.Tests.Editor
{
    /// <summary>
    /// Keeps a carrier project's Assets/Samples/&lt;Sample&gt; folders identical to the package's
    /// Samples~ (the one source of the samples). Files keep their .meta GUIDs, so scene references
    /// hold. Available when the package is listed in the project's "testables".
    /// </summary>
    public static class HoloKitSampleSync
    {
        public const string ProjectSamples = "Assets/Samples";

        public static string PackageSamples =>
            Path.Combine(PackageInfo.FindForAssembly(typeof(HoloKitSampleSync).Assembly).resolvedPath, "Samples~");

        /// <summary>The package's sample folders (Samples~/&lt;Name&gt;).</summary>
        public static IEnumerable<string> SampleNames =>
            Directory.GetDirectories(PackageSamples).Select(Path.GetFileName).OrderBy(n => n);

        /// <summary>
        /// Copies every package sample into Assets/Samples/&lt;Name&gt;, replacing changed files and
        /// deleting files the package no longer has. Folders in Assets/Samples that are not package
        /// samples are reported, not touched.
        /// </summary>
        [MenuItem("HoloKit/Samples/Copy Package Samples Into Project")]
        public static void CopyIntoProject()
        {
            int copied = 0, deleted = 0;
            foreach (string name in SampleNames)
            {
                var (c, d) = Mirror(Path.Combine(PackageSamples, name), Path.Combine(ProjectSamples, name));
                copied += c;
                deleted += d;
            }
            var samples = new HashSet<string>(SampleNames);
            foreach (string extra in Directory.Exists(ProjectSamples)
                         ? Directory.GetDirectories(ProjectSamples).Select(Path.GetFileName).Where(n => !samples.Contains(n))
                         : Enumerable.Empty<string>())
                Debug.LogWarning($"[HoloKitSampleSync] {ProjectSamples}/{extra} is not a package sample; left as is.");
            AssetDatabase.Refresh();
            Debug.Log($"[HoloKitSampleSync] package -> project: {copied} files copied, {deleted} stale files deleted");
        }

        /// <summary>
        /// Copies Assets/Samples back over the package's Samples~ for files the package already has
        /// (e.g. scenes re-saved by Unity). Never adds or deletes package files.
        /// </summary>
        [MenuItem("HoloKit/Samples/Copy Project Samples Back To Package")]
        public static void CopyBackToPackage()
        {
            int copied = 0;
            foreach (string name in SampleNames)
            {
                string package = Path.Combine(PackageSamples, name), project = Path.Combine(ProjectSamples, name);
                if (!Directory.Exists(project)) continue;
                foreach (string file in Directory.GetFiles(package, "*", SearchOption.AllDirectories))
                {
                    string source = Path.Combine(project, Path.GetRelativePath(package, file));
                    if (!File.Exists(source) || SameBytes(source, file)) continue;
                    File.Copy(source, file, true);
                    copied++;
                }
            }
            Debug.Log($"[HoloKitSampleSync] project -> package: {copied} files updated");
        }

        /// <summary>
        /// Re-saves every sample scene and the package's prefabs through Unity, so their files carry
        /// the components' current serialized fields (renamed and new ones), then copies the scenes
        /// back into Samples~.
        /// </summary>
        [MenuItem("HoloKit/Samples/Resave Samples And Prefabs")]
        public static void ResaveSamples()
        {
            CopyIntoProject();
            int scenes = 0;
            foreach (string name in SampleNames)
            {
                foreach (string path in Directory.GetFiles(Path.Combine(ProjectSamples, name), "*.unity", SearchOption.AllDirectories))
                {
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path.Replace('\\', '/'));
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    scenes++;
                }
            }
            string package = "Packages/" + PackageInfo.FindForAssembly(typeof(HoloKitSampleSync).Assembly).name;
            int prefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { package }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                prefabs++;
            }
            AssetDatabase.SaveAssets();
            CopyBackToPackage();
            Debug.Log($"[HoloKitSampleSync] re-saved {scenes} scenes and {prefabs} prefabs");
        }

        static (int copied, int deleted) Mirror(string from, string to)
        {
            int copied = 0, deleted = 0;
            var wanted = new HashSet<string>();
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == ".DS_Store") continue;
                string relative = Path.GetRelativePath(from, file);
                wanted.Add(relative);
                string target = Path.Combine(to, relative);
                if (File.Exists(target) && SameBytes(file, target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                copied++;
            }
            // The folder's own .meta sits next to it.
            string folderMeta = from.TrimEnd('/') + ".meta";
            if (File.Exists(folderMeta) && (!File.Exists(to + ".meta") || !SameBytes(folderMeta, to + ".meta")))
            {
                File.Copy(folderMeta, to + ".meta", true);
                copied++;
            }
            foreach (string file in Directory.GetFiles(to, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(to, file);
                if (Path.GetFileName(file) == ".DS_Store" || wanted.Contains(relative)) continue;
                File.Delete(file);
                deleted++;
            }
            foreach (string dir in Directory.GetDirectories(to, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                if (Directory.EnumerateFileSystemEntries(dir).Any(e => Path.GetFileName(e) != ".DS_Store")) continue;
                Directory.Delete(dir, true);
                if (File.Exists(dir + ".meta")) { File.Delete(dir + ".meta"); deleted++; }
            }
            return (copied, deleted);
        }

        static bool SameBytes(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            return fa.Length == fb.Length && File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
    }
}
