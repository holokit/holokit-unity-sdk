// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HoloKit.Validation.Tests
{
    /// Sample scenes read as YAML from the package's Samples~ folder (not imported, so no device
    /// or scene load is needed).
    public class SampleSceneTests
    {
        static IEnumerable<string> Scenes()
        {
            string samples = Path.Combine(PackageInfo.FindForAssembly(typeof(HoloKit.Editor.HoloKitProjectValidation).Assembly).resolvedPath, "Samples~");
            return Directory.GetFiles(samples, "*.unity", SearchOption.AllDirectories).OrderBy(p => p);
        }

        static string ScriptGuid(string className)
        {
            string guid = AssetDatabase.FindAssets($"{className} t:MonoScript")
                .FirstOrDefault(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g))?.GetClass()?.Name == className);
            Assert.IsNotNull(guid, $"{className} script not found");
            return guid;
        }

        static string Field(string doc, string name) => Regex.Match(doc, $@"\n  {name}: ([^\n]*)").Groups[1].Value;
        static string GameObjectOf(string doc) => Regex.Match(doc, @"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;

        /// Nothing may draw over the AR camera image in Mono: the AR camera (the one with
        /// ARCameraBackground) must not clear to the skybox, and no other enabled camera on an active
        /// object may clear the screen at the same or a later depth. AROcclusionManager's
        /// RequireComponent(Camera) once added such a camera (Skybox, depth 0) beside it in three
        /// samples, which showed Unity's sky instead of the real world.
        [Test]
        public void NothingCoversTheARBackground()
        {
            string background = ScriptGuid("ARCameraBackground");
            var problems = new List<string>();
            int arCameras = 0;
            foreach (string scene in Scenes())
            {
                string[] docs = Regex.Split(File.ReadAllText(scene), @"\n--- !u!");
                var activeObjects = new HashSet<string>(docs
                    .Where(d => d.StartsWith("1 &") && Field(d, "m_IsActive") == "1")
                    .Select(d => Regex.Match(d, @"^1 &(\d+)").Groups[1].Value));
                var arObjects = new HashSet<string>(docs
                    .Where(d => d.StartsWith("114 &") && d.Contains($"guid: {background}"))
                    .Select(GameObjectOf));
                var cameras = docs.Where(d => d.StartsWith("20 &")).ToList();
                var arCamera = cameras.FirstOrDefault(c => arObjects.Contains(GameObjectOf(c)));
                if (arCamera == null)
                    continue;
                arCameras++;
                string name = Path.GetFileName(scene);
                if (Field(arCamera, "m_ClearFlags") == "1")
                    problems.Add($"{name}: the AR camera clears to Skybox");
                float arDepth = float.Parse(Field(arCamera, "m_Depth"), CultureInfo.InvariantCulture);
                foreach (string camera in cameras.Where(c => c != arCamera))
                {
                    bool renders = Field(camera, "m_Enabled") == "1" && activeObjects.Contains(GameObjectOf(camera));
                    bool clears = Field(camera, "m_ClearFlags") is "1" or "2"; // Skybox or Solid Color
                    if (renders && clears && float.Parse(Field(camera, "m_Depth"), CultureInfo.InvariantCulture) >= arDepth)
                        problems.Add($"{name}: camera on object {GameObjectOf(camera)} clears over the AR camera image");
                }
            }
            Assert.Greater(arCameras, 0, "no AR camera found in the sample scenes");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
