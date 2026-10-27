// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
using HoloKit.iOS;
using NUnit.Framework;
using UnityEngine;

namespace HoloKit.Hands.Tests
{
    /// Hand skeleton topology, ported from the HoloKit Swift SDK (HandSkeleton).
    public class HandSkeletonTests
    {
        static readonly JointName[][] s_Fingers =
        {
            new[] { JointName.ThumbCMC, JointName.ThumbMP, JointName.ThumbIP, JointName.ThumbTip },
            new[] { JointName.IndexMCP, JointName.IndexPIP, JointName.IndexDIP, JointName.IndexTip },
            new[] { JointName.MiddleMCP, JointName.MiddlePIP, JointName.MiddleDIP, JointName.MiddleTip },
            new[] { JointName.RingMCP, JointName.RingPIP, JointName.RingDIP, JointName.RingTip },
            new[] { JointName.LittleMCP, JointName.LittlePIP, JointName.LittleDIP, JointName.LittleTip },
        };

        // A hand 0.4 m ahead: five fingers fanned out from the wrist, 3 cm per segment.
        static Dictionary<JointName, Vector3> Hand(float segment = 0.03f)
        {
            var wrist = new Vector3(0f, 0f, 0.4f);
            var joints = new Dictionary<JointName, Vector3> { [JointName.Wrist] = wrist };
            for (int f = 0; f < s_Fingers.Length; f++)
            {
                var direction = Quaternion.Euler(0f, 0f, -40f + 20f * f) * Vector3.up;
                for (int j = 0; j < 4; j++)
                    joints[s_Fingers[f][j]] = wrist + direction * segment * (j + 1);
            }
            return joints;
        }

        static Dictionary<JointName, float> Confidences(float value = 0.9f)
        {
            var confidences = new Dictionary<JointName, float>();
            for (int j = 0; j < 21; j++)
                confidences[(JointName)j] = value;
            return confidences;
        }

        readonly List<Vector3> m_Joints = new();
        readonly List<(Vector3 Start, Vector3 End)> m_Bones = new();

        [Test]
        public void FullHandHas21JointsAnd20Bones()
        {
            HandSkeleton.Build(Hand(), Confidences(), m_Joints, m_Bones);
            Assert.AreEqual(21, m_Joints.Count);
            Assert.AreEqual(20, m_Bones.Count);
        }

        [Test]
        public void UnconfidentJointDropsItselfAndItsBones()
        {
            var confidences = Confidences();
            confidences[JointName.IndexPIP] = 0.2f;
            HandSkeleton.Build(Hand(), confidences, m_Joints, m_Bones);
            Assert.AreEqual(20, m_Joints.Count);
            Assert.AreEqual(18, m_Bones.Count, "MCP-PIP and PIP-DIP go");
        }

        [Test]
        public void ConfidenceThresholdIsInclusive()
        {
            HandSkeleton.Build(Hand(), Confidences(HandSkeleton.MinimumConfidence), m_Joints, m_Bones);
            Assert.AreEqual(21, m_Joints.Count);
            Assert.AreEqual(20, m_Bones.Count);
        }

        [Test]
        public void ImplausibleBonesAreDropped()
        {
            HandSkeleton.Build(Hand(0.25f), Confidences(), m_Joints, m_Bones);
            Assert.AreEqual(0, m_Bones.Count, "25 cm segments are longer than any finger bone");
            HandSkeleton.Build(Hand(0.002f), Confidences(), m_Joints, m_Bones);
            Assert.AreEqual(0, m_Bones.Count, "2 mm segments are collapsed joints");
            Assert.AreEqual(21, m_Joints.Count, "the joints still show");
        }

        [Test]
        public void BuildClearsThePreviousHand()
        {
            HandSkeleton.Build(Hand(), Confidences(), m_Joints, m_Bones);
            HandSkeleton.Build(Hand(), Confidences(0.1f), m_Joints, m_Bones);
            Assert.AreEqual(0, m_Joints.Count);
            Assert.AreEqual(0, m_Bones.Count);
        }
    }
}
#endif
