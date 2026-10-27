// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>
    /// The hand topology of the HoloKit Swift SDK (HandSkeleton): the joints at or above a confidence,
    /// and the bones along the five wrist-to-fingertip chains whose two ends are both confident and
    /// between 3 mm and 20 cm apart. A full hand has 21 joints and 20 bones.
    /// </summary>
    public static class HandSkeleton
    {
        public const float MinimumConfidence = 0.3f;
        public const float MinimumBoneLength = 0.003f;
        public const float MaximumBoneLength = 0.2f;
        public const int MaxJoints = 21;
        public const int MaxBones = 20;

        static readonly JointName[][] s_Chains =
        {
            new[] { JointName.Wrist, JointName.ThumbCMC, JointName.ThumbMP, JointName.ThumbIP, JointName.ThumbTip },
            new[] { JointName.Wrist, JointName.IndexMCP, JointName.IndexPIP, JointName.IndexDIP, JointName.IndexTip },
            new[] { JointName.Wrist, JointName.MiddleMCP, JointName.MiddlePIP, JointName.MiddleDIP, JointName.MiddleTip },
            new[] { JointName.Wrist, JointName.RingMCP, JointName.RingPIP, JointName.RingDIP, JointName.RingTip },
            new[] { JointName.Wrist, JointName.LittleMCP, JointName.LittlePIP, JointName.LittleDIP, JointName.LittleTip },
        };

        /// <summary>Fills one hand's joints and bones (both lists are cleared first).</summary>
        public static void Build(IReadOnlyDictionary<JointName, Vector3> positions,
            IReadOnlyDictionary<JointName, float> confidences,
            List<Vector3> joints, List<(Vector3 Start, Vector3 End)> bones,
            float minimumConfidence = MinimumConfidence)
        {
            joints.Clear();
            bones.Clear();
            foreach (var pair in positions)
                if (Confident(confidences, pair.Key, minimumConfidence))
                    joints.Add(pair.Value);
            foreach (var chain in s_Chains)
            {
                for (int i = 0; i + 1 < chain.Length; i++)
                {
                    JointName a = chain[i], b = chain[i + 1];
                    if (!Confident(confidences, a, minimumConfidence) || !Confident(confidences, b, minimumConfidence))
                        continue;
                    if (!positions.TryGetValue(a, out var start) || !positions.TryGetValue(b, out var end))
                        continue;
                    float length = Vector3.Distance(start, end);
                    if (length <= MinimumBoneLength || length >= MaximumBoneLength)
                        continue;
                    bones.Add((start, end));
                }
            }
        }

        static bool Confident(IReadOnlyDictionary<JointName, float> confidences, JointName joint, float minimum) =>
            confidences.TryGetValue(joint, out var confidence) && confidence >= minimum;
    }
}
#endif
