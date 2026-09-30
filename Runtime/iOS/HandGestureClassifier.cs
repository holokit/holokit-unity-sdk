// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>
    /// Classifies one hand's Vision joints (normalized image coordinates) as Pinched, Five or
    /// None, after the HoloKit Swift SDK's GestureRecognizer. Joints below
    /// <see cref="MinimumConfidence"/> are not used, and Five needs every finger straight
    /// (tip-to-base distance at least <see cref="MinimumStraightness"/> of the finger's length),
    /// whichever way the hand points. Pinched compares the thumb-to-fingertip distance with the
    /// hand's size (<see cref="PinchRatio"/>), so a hand far from the camera is not pinched just
    /// because it is small in the image.
    /// </summary>
    public static class HandGestureClassifier
    {
        /// <summary>
        /// Thumb tip to fingertip over hand size below which a finger pinches. 0.25 sits in the
        /// valley between pinches and open hands on real hand images (Tests/Hands/Tools~), and
        /// equals the Swift SDK's 0.12 image units for a hand half the image tall.
        /// </summary>
        public const float PinchRatio = 0.25f;
        /// <summary>
        /// Thumb tip to fingertip, normalized image distance, used only when the hand's size is
        /// unknown (the Swift SDK's fixed threshold).
        /// </summary>
        public const float PinchThreshold = 0.12f;
        public const float MinimumConfidence = 0.35f;
        public const float MinimumStraightness = 0.82f;
        /// <summary>Shorter fingers (normalized image length) are too small to judge.</summary>
        public const float MinimumFingerLength = 0.025f;

        static readonly JointName[][] s_Fingers =
        {
            new[] { JointName.ThumbCMC, JointName.ThumbMP, JointName.ThumbIP, JointName.ThumbTip },
            new[] { JointName.IndexMCP, JointName.IndexPIP, JointName.IndexDIP, JointName.IndexTip },
            new[] { JointName.MiddleMCP, JointName.MiddlePIP, JointName.MiddleDIP, JointName.MiddleTip },
            new[] { JointName.RingMCP, JointName.RingPIP, JointName.RingDIP, JointName.RingTip },
            new[] { JointName.LittleMCP, JointName.LittlePIP, JointName.LittleDIP, JointName.LittleTip },
        };

        /// <param name="joints">The hand's joints in normalized image coordinates.</param>
        /// <param name="confidences">Vision's confidence per joint; null treats every joint as confident.</param>
        public static HandGesture Classify(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences)
        {
            if (IsPinching(joints, confidences, JointName.IndexTip))
                return HandGesture.Pinched;
            foreach (var finger in s_Fingers)
                if (Straightness(joints, confidences, finger) < MinimumStraightness)
                    return HandGesture.None;
            return HandGesture.Five;
        }

        /// <summary>
        /// Whether the thumb tip touches `fingerTip` (IndexTip, MiddleTip, RingTip or LittleTip):
        /// their distance below <see cref="PinchRatio"/> of the hand's size.
        /// </summary>
        public static bool IsPinching(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences, JointName fingerTip)
        {
            float? ratio = PinchDistance(joints, confidences, fingerTip);
            if (ratio.HasValue)
                return ratio.Value < PinchRatio;
            return TryGet(joints, confidences, JointName.ThumbTip, out var thumb) &&
                TryGet(joints, confidences, fingerTip, out var tip) &&
                Vector2.Distance(thumb, tip) < PinchThreshold;
        }

        /// <summary>
        /// Thumb tip to `fingerTip` over the hand's size, or null when either tip is unconfident
        /// or the size is unknown.
        /// </summary>
        public static float? PinchDistance(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences, JointName fingerTip)
        {
            if (!TryGet(joints, confidences, JointName.ThumbTip, out var thumb) ||
                !TryGet(joints, confidences, fingerTip, out var tip))
                return null;
            float size = HandSize(joints, confidences);
            return size > 0 ? Vector2.Distance(thumb, tip) / size : null;
        }

        /// <summary>
        /// The hand's size in the image: the longest of the palm (wrist to middle knuckle) and the
        /// index and middle fingers' bone lengths, from confident joints; 0 when none is seen.
        /// Bone lengths do not change as a finger bends; the longest one is the least
        /// foreshortened.
        /// </summary>
        public static float HandSize(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences)
        {
            float size = 0;
            if (TryGet(joints, confidences, JointName.Wrist, out var wrist) &&
                TryGet(joints, confidences, JointName.MiddleMCP, out var knuckle))
                size = Vector2.Distance(wrist, knuckle);
            for (int f = 1; f <= 2; f++)
                size = Mathf.Max(size, Length(joints, confidences, s_Fingers[f]));
            return size;
        }

        static float Length(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences, IReadOnlyList<JointName> finger)
        {
            float length = 0;
            Vector2 previous = default;
            for (int i = 0; i < finger.Count; i++)
            {
                if (!TryGet(joints, confidences, finger[i], out var point))
                    return 0;
                if (i > 0)
                    length += Vector2.Distance(previous, point);
                previous = point;
            }
            return length;
        }

        /// <summary>
        /// A finger's tip-to-base distance over its length along the joints (1 for a straight
        /// finger), or 0 when a joint is missing or unconfident or the finger is too short.
        /// </summary>
        public static float Straightness(IReadOnlyDictionary<JointName, Vector2> joints,
            IReadOnlyDictionary<JointName, float> confidences, IReadOnlyList<JointName> finger)
        {
            float length = Length(joints, confidences, finger);
            if (length <= MinimumFingerLength)
                return 0;
            TryGet(joints, confidences, finger[0], out var bottom);
            TryGet(joints, confidences, finger[finger.Count - 1], out var tip);
            return Vector2.Distance(bottom, tip) / length;
        }

        static bool TryGet(IReadOnlyDictionary<JointName, Vector2> joints, IReadOnlyDictionary<JointName, float> confidences,
            JointName name, out Vector2 point)
        {
            if (!joints.TryGetValue(name, out point))
                return false;
            return confidences == null || (confidences.TryGetValue(name, out float confidence) && confidence >= MinimumConfidence);
        }
    }

    /// <summary>
    /// Holds a gesture until another has <see cref="RequiredEvidence"/> matching observations in
    /// a row, and keeps it through a hand-tracking dropout shorter than <see cref="LostGrace"/>
    /// (Swift SDK GestureRecognizer).
    /// </summary>
    public sealed class HandGestureFilter
    {
        public const int RequiredEvidence = 4;
        public const float LostGrace = 0.2f;

        public HandGesture Gesture { get; private set; } = HandGesture.None;
        HandGesture m_Candidate = HandGesture.None;
        int m_Evidence;
        double? m_LastSeen;

        /// <summary>Adds one observation; returns true when the gesture changed.</summary>
        public bool Observe(HandGesture observed, double time)
        {
            m_LastSeen = time;
            if (observed == m_Candidate) m_Evidence++;
            else { m_Candidate = observed; m_Evidence = 1; }
            return m_Evidence >= RequiredEvidence && Set(observed);
        }

        /// <summary>No hand this time; returns true when the gesture was released.</summary>
        public bool Lost(double time)
        {
            if (m_LastSeen.HasValue && time - m_LastSeen.Value < LostGrace)
                return false;
            m_Candidate = HandGesture.None;
            m_Evidence = 0;
            m_LastSeen = null;
            return Set(HandGesture.None);
        }

        public void Reset()
        {
            m_Candidate = HandGesture.None;
            m_Evidence = 0;
            m_LastSeen = null;
            Gesture = HandGesture.None;
        }

        bool Set(HandGesture next)
        {
            if (Gesture == next) return false;
            Gesture = next;
            return true;
        }
    }
}
#endif
