// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HoloKit.iOS;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace HoloKit.Hands.Tests
{
    /// Gesture classification, gesture hold and joint smoothing, ported from the HoloKit Swift
    /// SDK (GestureRecognizer, HoloKitHandSmoother).
    public class HandGestureTests
    {
        // An open hand in normalized image coordinates: fingers fan out around `direction`
        // (degrees from +x), the thumb 70 degrees off, the wrist at `origin`.
        static Dictionary<JointName, Vector2> OpenHand(float direction, Vector2 origin, float scale = 0.2f)
        {
            var joints = new Dictionary<JointName, Vector2> { [JointName.Wrist] = origin };
            float[] spread = { -70, -20, 0, 20, 40 };
            JointName[][] fingers =
            {
                new[] { JointName.ThumbCMC, JointName.ThumbMP, JointName.ThumbIP, JointName.ThumbTip },
                new[] { JointName.IndexMCP, JointName.IndexPIP, JointName.IndexDIP, JointName.IndexTip },
                new[] { JointName.MiddleMCP, JointName.MiddlePIP, JointName.MiddleDIP, JointName.MiddleTip },
                new[] { JointName.RingMCP, JointName.RingPIP, JointName.RingDIP, JointName.RingTip },
                new[] { JointName.LittleMCP, JointName.LittlePIP, JointName.LittleDIP, JointName.LittleTip },
            };
            for (int f = 0; f < 5; f++)
            {
                float angle = (direction + spread[f]) * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 bottom = origin + along * scale * 0.35f;
                for (int j = 0; j < 4; j++)
                    joints[fingers[f][j]] = bottom + along * scale * 0.18f * j;
            }
            return joints;
        }

        static Dictionary<JointName, float> Confident(Dictionary<JointName, Vector2> joints, float value = 0.9f)
        {
            var confidences = new Dictionary<JointName, float>();
            foreach (var name in joints.Keys) confidences[name] = value;
            return confidences;
        }

        [Test]
        public void OpenHandIsFiveWhicheverWayItPoints()
        {
            // Upright, tilted and sideways: the Unity SDK's former rule (each joint higher in the
            // image than the last) only accepted fingers pointing up.
            foreach (float direction in new[] { 90f, 45f, 0f, 135f, 180f, -90f })
            {
                var hand = OpenHand(direction, new Vector2(0.5f, 0.5f));
                Assert.AreEqual(HandGesture.Five, HandGestureClassifier.Classify(hand, Confident(hand)), $"{direction} degrees");
            }
        }

        [Test]
        public void CurledFingerIsNotFive()
        {
            var hand = OpenHand(90f, new Vector2(0.5f, 0.5f));
            // Fold the index finger back over its base.
            hand[JointName.IndexDIP] = hand[JointName.IndexPIP] + new Vector2(0.02f, -0.01f);
            hand[JointName.IndexTip] = hand[JointName.IndexMCP] + new Vector2(0.01f, -0.005f);
            Assert.AreEqual(HandGesture.None, HandGestureClassifier.Classify(hand, Confident(hand)));
        }

        [Test]
        public void ThumbTouchingIndexIsPinched()
        {
            var hand = OpenHand(90f, new Vector2(0.5f, 0.5f));
            hand[JointName.ThumbTip] = hand[JointName.IndexTip] + new Vector2(0.05f, 0f);
            Assert.AreEqual(HandGesture.Pinched, HandGestureClassifier.Classify(hand, Confident(hand)));
            hand[JointName.ThumbTip] = hand[JointName.IndexTip] + new Vector2(0.13f, 0f);
            Assert.AreNotEqual(HandGesture.Pinched, HandGestureClassifier.Classify(hand, Confident(hand)), "0.13 apart is open");
        }

        [Test]
        public void UnconfidentJointsDoNotCount()
        {
            var hand = OpenHand(90f, new Vector2(0.5f, 0.5f));
            hand[JointName.ThumbTip] = hand[JointName.IndexTip] + new Vector2(0.05f, 0f);
            var confidences = Confident(hand);
            confidences[JointName.ThumbTip] = 0.2f;
            Assert.AreNotEqual(HandGesture.Pinched, HandGestureClassifier.Classify(hand, confidences), "a guessed thumb tip is no pinch");
            var open = OpenHand(90f, new Vector2(0.5f, 0.5f));
            var low = Confident(open);
            low[JointName.LittleTip] = 0.1f;
            Assert.AreEqual(HandGesture.None, HandGestureClassifier.Classify(open, low), "Five needs every finger seen");
            Assert.AreEqual(HandGesture.Five, HandGestureClassifier.Classify(open, null), "no confidences: all trusted");
        }

        [Test]
        public void TinyFingersAreNotJudgedStraight()
        {
            var hand = OpenHand(90f, new Vector2(0.5f, 0.5f), scale: 0.03f);
            var index = new[] { JointName.IndexMCP, JointName.IndexPIP, JointName.IndexDIP, JointName.IndexTip };
            Assert.AreEqual(0f, HandGestureClassifier.Straightness(hand, Confident(hand), index), "shorter than 0.025");
            var large = OpenHand(90f, new Vector2(0.5f, 0.5f));
            Assert.AreEqual(1f, HandGestureClassifier.Straightness(large, Confident(large), index), 1e-4f, "a straight finger");
        }

        [Test]
        public void FourMatchingObservationsChangeTheGesture()
        {
            var filter = new HandGestureFilter();
            Assert.IsFalse(filter.Observe(HandGesture.Pinched, 0.00));
            Assert.IsFalse(filter.Observe(HandGesture.Pinched, 0.05));
            Assert.IsFalse(filter.Observe(HandGesture.Pinched, 0.10));
            Assert.IsTrue(filter.Observe(HandGesture.Pinched, 0.15));
            Assert.AreEqual(HandGesture.Pinched, filter.Gesture);
            // One stray frame does not release it.
            Assert.IsFalse(filter.Observe(HandGesture.None, 0.20));
            Assert.IsFalse(filter.Observe(HandGesture.Pinched, 0.25));
            Assert.AreEqual(HandGesture.Pinched, filter.Gesture);
        }

        [Test]
        public void ShortDropoutKeepsTheGesture()
        {
            var filter = new HandGestureFilter();
            for (int i = 0; i < 4; i++) filter.Observe(HandGesture.Five, i * 0.05);
            Assert.AreEqual(HandGesture.Five, filter.Gesture);
            Assert.IsFalse(filter.Lost(0.15 + 0.19), "under 0.2 s: kept");
            Assert.IsTrue(filter.Lost(0.15 + 0.21), "past 0.2 s: released");
            Assert.AreEqual(HandGesture.None, filter.Gesture);
        }

        [Test]
        public void OneEuroFilterStillsAStillHandAndFollowsAMovingOne()
        {
            var parameters = HandSmoothing.Default;
            var filter = new OneEuroFilter();
            var random = new System.Random(7);
            float rawSpread = 0, smoothSpread = 0;
            Vector3 truth = new(0, 0, -0.4f);
            for (int i = 0; i < 200; i++)
            {
                var noise = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, 0) * 0.01f;
                Vector3 output = filter.Filter(truth + noise, i / 20.0, parameters);
                if (i > 20) { rawSpread += noise.magnitude; smoothSpread += (output - truth).magnitude; }
            }
            Assert.Less(smoothSpread, rawSpread * 0.5f, "jitter at least halved on a still hand");

            var moving = new OneEuroFilter();
            Vector3 last = default;
            for (int i = 0; i < 40; i++) last = moving.Filter(new Vector3(i * 0.02f, 0, 0), i / 20.0, parameters); // 0.4 m/s
            Assert.Less(Mathf.Abs(last.x - 39 * 0.02f), 0.05f, "a moving hand lags under 5 cm");
        }

        [Test]
        public void SmootherRestartsOnAJumpAndForgetsLostHands()
        {
            var smoother = new HandJointSmoother();
            var raw = new List<Dictionary<JointName, Vector3>> { new() { [JointName.Wrist] = Vector3.zero }, new() };
            var smoothed = new List<Dictionary<JointName, Vector3>> { new(), new() };
            int[] keys = { 1, 0 };
            smoother.Smooth(1, keys, raw, smoothed, 0, HandSmoothing.Default);
            raw[0][JointName.Wrist] = new Vector3(0.5f, 0, 0); // a jump past 15 cm
            smoother.Smooth(1, keys, raw, smoothed, 0.05, HandSmoothing.Default);
            Assert.AreEqual(0.5f, smoothed[0][JointName.Wrist].x, 1e-5f, "starts over instead of sliding across");
            raw[0][JointName.Wrist] = new Vector3(0.52f, 0, 0);
            smoother.Smooth(1, keys, raw, smoothed, 0.10, HandSmoothing.Default);
            Assert.Less(smoothed[0][JointName.Wrist].x, 0.52f, "then smooths");
        }

        /// Builds and runs Tests/Hands/Native~ (the LiDAR hand depth core) on the Mac.
        [Test]
        public void NativeHandDepth_PassesHostTests()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
                Assert.Ignore("The native core builds with Apple's clang.");
            var package = PackageInfo.FindForAssembly(typeof(HandGestureClassifier).Assembly);
            string script = Path.Combine(package.resolvedPath, "Tests/Hands/Tools~/run_native_tests.sh");
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
            StringAssert.Contains("All hand depth tests passed", output);
        }
    }
}
#endif
