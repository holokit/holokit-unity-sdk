// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>The finger the thumb pinches.</summary>
    public enum PinchFinger
    {
        None = -1,
        Index = 0,
        Middle = 1,
        Ring = 2,
        Little = 3,
    }

    /// <summary>
    /// Which finger one hand's thumb pinches: the finger whose tip is closest to the thumb tip,
    /// when closer than <see cref="HandGestureClassifier.PinchRatio"/> of the hand's size. A
    /// change needs <see cref="RequiredEvidence"/> matching observations in a row, so a pinch
    /// begins and ends once. Feed it each update of one hand's Vision joints.
    /// </summary>
    public sealed class FingerPinchDetector
    {
        public const int RequiredEvidence = 3;

        static readonly JointName[] s_Tips = { JointName.IndexTip, JointName.MiddleTip, JointName.RingTip, JointName.LittleTip };

        /// <summary>The finger pinched now, or None.</summary>
        public PinchFinger Pinched { get; private set; } = PinchFinger.None;

        PinchFinger m_Candidate = PinchFinger.None;
        int m_Evidence;

        /// <summary>The fingertip joint of a finger.</summary>
        public static JointName TipOf(PinchFinger finger) => s_Tips[(int)finger];

        /// <summary>The finger the thumb touches in these joints, without the hold.</summary>
        public static PinchFinger Observe(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences)
        {
            var closest = PinchFinger.None;
            float best = float.MaxValue;
            for (int f = 0; f < s_Tips.Length; f++)
            {
                if (!HandGestureClassifier.IsPinching(joints, confidences, s_Tips[f]))
                    continue;
                float distance = HandGestureClassifier.PinchDistance(joints, confidences, s_Tips[f]) ?? 0;
                if (distance < best)
                {
                    best = distance;
                    closest = (PinchFinger)f;
                }
            }
            return closest;
        }

        /// <summary>
        /// Adds one observation. When the pinched finger changes, `ended` is the finger released
        /// (or None) and `began` the finger now pinched (or None); returns true then.
        /// </summary>
        public bool Update(IReadOnlyDictionary<JointName, Vector2> joints, IReadOnlyDictionary<JointName, float> confidences,
            out PinchFinger began, out PinchFinger ended)
            => Update(Observe(joints, confidences), out began, out ended);

        /// <inheritdoc cref="Update(IReadOnlyDictionary{JointName, Vector2}, IReadOnlyDictionary{JointName, float}, out PinchFinger, out PinchFinger)"/>
        public bool Update(PinchFinger observed, out PinchFinger began, out PinchFinger ended)
        {
            began = ended = PinchFinger.None;
            if (observed == m_Candidate) m_Evidence++;
            else { m_Candidate = observed; m_Evidence = 1; }
            if (m_Evidence < RequiredEvidence || observed == Pinched)
                return false;
            ended = Pinched;
            began = observed;
            Pinched = observed;
            return true;
        }

        /// <summary>The hand is gone: releases its pinch, if any, into `ended`.</summary>
        public bool Reset(out PinchFinger ended)
        {
            ended = Pinched;
            Pinched = m_Candidate = PinchFinger.None;
            m_Evidence = 0;
            return ended != PinchFinger.None;
        }
    }
}
#endif
