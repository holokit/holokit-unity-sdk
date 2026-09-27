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
    /// Smooths every joint's world position with its own 1€ filter, per hand (keyed by
    /// chirality, else by slot). A hand that disappears, or a joint that jumps more than
    /// <see cref="ResetDistance"/> in one update, starts over instead of sliding across.
    /// </summary>
    public sealed class HandJointSmoother
    {
        public const float ResetDistance = 0.15f;
        readonly Dictionary<(int hand, JointName joint), OneEuroFilter> m_Filters = new();
        readonly HashSet<(int, JointName)> m_Seen = new();
        readonly List<(int, JointName)> m_Stale = new();

        public void Reset() => m_Filters.Clear();

        /// <summary>
        /// Smooths `raw` into `smoothed` for the hands in this update; `handKeys[i]` identifies
        /// hand i across updates.
        /// </summary>
        public void Smooth(int handCount, IReadOnlyList<int> handKeys, IReadOnlyList<Dictionary<JointName, Vector3>> raw,
            IReadOnlyList<Dictionary<JointName, Vector3>> smoothed, double time, HandSmoothing parameters)
        {
            m_Seen.Clear();
            for (int i = 0; i < handCount; i++)
            {
                foreach (var (joint, position) in raw[i])
                {
                    var id = (handKeys[i], joint);
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
    }
}
#endif
