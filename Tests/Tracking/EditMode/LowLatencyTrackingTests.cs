// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace HoloKit.Tracking.Tests
{
    public class LowLatencyTrackingTests
    {
        /// Builds and runs Tests/Tracking/Native~ (the iOS tracking core on the Mac):
        /// synthetic Kalman tests, smoother, Unity/ARKit conversion, and parity with
        /// holokit-swift-sdk on an iPhone 15 Pro trace.
        [Test]
        public void NativeTrackingCore_PassesHostTestsAndMatchesSwiftSDK()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
                Assert.Ignore("The native core builds with Apple's clang and simd.");
            var package = PackageInfo.FindForAssembly(typeof(LowLatencyTrackingManager).Assembly);
            Assert.IsNotNull(package, "HoloKit package not found");
            string script = Path.Combine(package.resolvedPath, "Tests/Tracking/Tools~/run_native_tests.sh");
            var start = new ProcessStartInfo("/bin/bash", $"\"{script}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(start);
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            TestContext.WriteLine(output);
            Assert.AreEqual(0, process.ExitCode, output);
            StringAssert.Contains("All tracking core tests passed", output);
        }

        [Test]
        public void Manager_IsInactiveOutsideTheiOSPlayer()
        {
            var go = new GameObject("Stereo Tracked Pose");
            try
            {
                var manager = go.AddComponent<LowLatencyTrackingManager>();
                Assert.IsFalse(manager.IsPredicting);
                Assert.AreEqual(-1, manager.PresentLatency);
                Assert.AreEqual(-1, manager.IMUSampleAge);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
