// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>
    /// 1€ filter settings (Casiez et al., CHI 2012): <see cref="MinimumCutoff"/> (Hz) sets how
    /// still a still hand is, <see cref="Beta"/> how quickly the filter follows speed,
    /// <see cref="DerivativeCutoff"/> smooths that speed. Defaults from the HoloKit Swift SDK.
    /// </summary>
    [Serializable]
    public struct HandSmoothing
    {
        public float MinimumCutoff;
        public float Beta;
        public float DerivativeCutoff;

        public static HandSmoothing Default => new() { MinimumCutoff = 1.5f, Beta = 4f, DerivativeCutoff = 1f };
    }

    /// <summary>
    /// The 1€ filter for a 3D point: a low-pass whose cutoff rises with speed, so a still hand
    /// stops jittering while a moving one lags little.
    /// </summary>
    public struct OneEuroFilter
    {
        /// Frame spacing assumed when timestamps do not advance (tests, replays).
        public const float FallbackInterval = 1f / 20f;

        public Vector3? Value { get; private set; }
        Vector3 m_Derivative;
        double m_Time;

        public Vector3 Filter(Vector3 sample, double time, HandSmoothing parameters)
        {
            if (!Value.HasValue)
            {
                Value = sample;
                m_Time = time;
                return sample;
            }
            float dt = time > m_Time ? (float)(time - m_Time) : FallbackInterval;
            Vector3 previous = Value.Value;
            Vector3 rawDerivative = (sample - previous) / dt;
            m_Derivative += Alpha(parameters.DerivativeCutoff, dt) * (rawDerivative - m_Derivative);
            float cutoff = parameters.MinimumCutoff + parameters.Beta * m_Derivative.magnitude;
            Vector3 next = previous + Alpha(cutoff, dt) * (sample - previous);
            Value = next;
            m_Time = Math.Max(m_Time, time);
            return next;
        }

        public static float Alpha(float cutoff, float dt)
        {
            float r = 2f * Mathf.PI * cutoff * dt;
            return r / (r + 1f);
        }
    }

    /// <summary>
    /// Smooths every joint's world position with its own 1€ filter, per hand. A hand keeps its
    /// filters by position: each one takes those of the previous update's hand whose wrist is
    /// nearest. Not by Vision's chirality, which on device gave both hands the same side in two
    /// of three updates, so the hands shared filters and nearby joints were drawn between them.
    /// A hand that disappears, or a joint that jumps more than <see cref="ResetDistance"/> in one
    /// update, starts over instead of sliding across.
    /// </summary>
    public sealed class HandJointSmoother
    {
        public const float ResetDistance = 0.15f;
        readonly Dictionary<(int hand, JointName joint), OneEuroFilter> m_Filters = new();
        readonly HashSet<(int, JointName)> m_Seen = new();
        readonly List<(int, JointName)> m_Stale = new();
        readonly List<(int Id, Vector3 Wrist)> m_Previous = new();
        readonly List<int> m_Ids = new();
        readonly List<bool> m_Taken = new();
        int m_NextId;

        public void Reset()
        {
            m_Filters.Clear();
            m_Previous.Clear();
        }

        /// <summary>Smooths `raw` into `smoothed` for the first `handCount` hands of this update.</summary>
        public void Smooth(int handCount, IReadOnlyList<Dictionary<JointName, Vector3>> raw,
            IReadOnlyList<Dictionary<JointName, Vector3>> smoothed, double time, HandSmoothing parameters)
        {
            MatchHands(handCount, raw);
            m_Seen.Clear();
            for (int i = 0; i < handCount; i++)
            {
                foreach (var (joint, position) in raw[i])
                {
                    var id = (m_Ids[i], joint);
                    m_Seen.Add(id);
                    m_Filters.TryGetValue(id, out var filter);
                    if (filter.Value.HasValue && Vector3.Distance(filter.Value.Value, position) > ResetDistance)
                        filter = default;
                    smoothed[i][joint] = filter.Filter(position, time, parameters);
                    m_Filters[id] = filter;
                }
            }
            m_Stale.Clear();
            foreach (var id in m_Filters.Keys)
                if (!m_Seen.Contains(id))
                    m_Stale.Add(id);
            foreach (var id in m_Stale)
                m_Filters.Remove(id);
        }

        // m_Ids[i]: the id of the previous update's hand nearest hand i (by wrist), else a new one.
        // Two hands against two take the pairing with the smaller total distance; otherwise the
        // closest remaining pair is matched first.
        void MatchHands(int handCount, IReadOnlyList<Dictionary<JointName, Vector3>> raw)
        {
            m_Ids.Clear();
            for (int i = 0; i < handCount; i++)
                m_Ids.Add(-1);
            if (handCount == 2 && m_Previous.Count == 2)
            {
                Vector3 a = Wrist(raw[0]), b = Wrist(raw[1]);
                bool crossed = Vector3.Distance(a, m_Previous[1].Wrist) + Vector3.Distance(b, m_Previous[0].Wrist)
                    < Vector3.Distance(a, m_Previous[0].Wrist) + Vector3.Distance(b, m_Previous[1].Wrist);
                m_Ids[0] = m_Previous[crossed ? 1 : 0].Id;
                m_Ids[1] = m_Previous[crossed ? 0 : 1].Id;
            }
            else
            {
                m_Taken.Clear();
                for (int p = 0; p < m_Previous.Count; p++)
                    m_Taken.Add(false);
                for (int round = 0; round < Mathf.Min(handCount, m_Previous.Count); round++)
                {
                    int bestHand = -1, bestPrevious = -1;
                    float best = float.MaxValue;
                    for (int i = 0; i < handCount; i++)
                    {
                        if (m_Ids[i] >= 0)
                            continue;
                        for (int p = 0; p < m_Previous.Count; p++)
                        {
                            float distance = m_Taken[p] ? float.MaxValue : Vector3.Distance(Wrist(raw[i]), m_Previous[p].Wrist);
                            if (distance < best) { best = distance; bestHand = i; bestPrevious = p; }
                        }
                    }
                    if (bestHand < 0)
                        break; // no finite distance left (a NaN wrist): the rest start over
                    m_Ids[bestHand] = m_Previous[bestPrevious].Id;
                    m_Taken[bestPrevious] = true;
                }
            }
            m_Previous.Clear();
            for (int i = 0; i < handCount; i++)
            {
                if (m_Ids[i] < 0)
                    m_Ids[i] = m_NextId++;
                m_Previous.Add((m_Ids[i], Wrist(raw[i])));
            }
        }

        static Vector3 Wrist(Dictionary<JointName, Vector3> hand)
        {
            if (hand.TryGetValue(JointName.Wrist, out var wrist))
                return wrist;
            foreach (var position in hand.Values)
                return position;
            return Vector3.zero;
        }
    }
}
#endif
