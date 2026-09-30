// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS

using System.Collections;
using System.Collections.Generic;
using HoloKit.iOS;
using UnityEngine;

namespace HoloKit.Samples.PinchToPlace
{
    /// <summary>
    /// Pinch the thumb with any finger to place an object where they meet: a sphere for the
    /// index finger, a cube for the middle, a capsule for the ring and a cylinder for the little
    /// finger. Dots on the five fingertips light up in the finger's colour as it nears the thumb.
    /// Works with either hand, in Mono and Stereo.
    /// </summary>
    public class PinchToPlace : MonoBehaviour
    {
        [Tooltip("The scene's hand tracking; found automatically when empty.")]
        [SerializeField] HandTrackingManager m_HandTracking;

        [Tooltip("Placed object size in metres.")]
        [SerializeField] float m_Size = 0.03f;

        [Tooltip("The oldest objects go once there are more than this.")]
        [SerializeField] int m_MaxObjects = 60;

        static readonly Color[] s_Colors =
        {
            new(1f, 0.36f, 0.1f),   // index: HoloKit orange
            new(0.2f, 0.6f, 1f),    // middle: blue
            new(0.35f, 0.9f, 0.4f), // ring: green
            new(1f, 0.85f, 0.2f),   // little: yellow
        };
        static readonly PrimitiveType[] s_Shapes =
            { PrimitiveType.Sphere, PrimitiveType.Cube, PrimitiveType.Capsule, PrimitiveType.Cylinder };

        readonly FingerPinchDetector[] m_Detectors = { new(), new() };
        readonly List<Renderer[]> m_Dots = new();
        readonly Queue<GameObject> m_Placed = new();

        void Start()
        {
            if (m_HandTracking == null)
                m_HandTracking = FindFirstObjectByType<HandTrackingManager>();
            if (m_HandTracking == null || m_HandTracking.HandPoseDetector == null)
            {
                Debug.LogWarning("[PinchToPlace] Needs a HandTrackingManager (and AROcclusionManager) in the scene.");
                enabled = false;
                return;
            }
            for (int hand = 0; hand < 2; hand++)
            {
                var dots = new Renderer[5];
                for (int i = 0; i < 5; i++)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(dot.GetComponent<Collider>());
                    dot.name = $"Fingertip Dot {hand}.{i}";
                    dot.transform.SetParent(transform, false);
                    dot.transform.localScale = Vector3.one * 0.008f;
                    dot.SetActive(false);
                    dots[i] = dot.GetComponent<Renderer>();
                }
                m_Dots.Add(dots);
            }
            m_HandTracking.HandPoseDetector.OnHandPoseUpdated += OnHandPoseUpdated;
            m_HandTracking.HandPoseDetector.OnHandPoseLost += OnHandPoseLost;
        }

        void OnDestroy()
        {
            if (m_HandTracking != null && m_HandTracking.HandPoseDetector != null)
            {
                m_HandTracking.HandPoseDetector.OnHandPoseUpdated -= OnHandPoseUpdated;
                m_HandTracking.HandPoseDetector.OnHandPoseLost -= OnHandPoseLost;
            }
        }

        void OnHandPoseUpdated()
        {
            var detector = m_HandTracking.HandPoseDetector;
            int hands = Mathf.Min(detector.HandCount, 2);
            for (int hand = 0; hand < 2; hand++)
            {
                if (hand >= hands)
                {
                    m_Detectors[hand].Reset(out _);
                    ShowDots(hand, false);
                    continue;
                }
                var joints = detector.HandPoses2D[hand];
                var confidences = detector.HandPosesConfidence[hand];
                var world = m_HandTracking.HandPoses3D[hand];
                if (m_Detectors[hand].Update(joints, confidences, out var began, out _) && began != PinchFinger.None)
                    Place(began, world);
                UpdateDots(hand, joints, confidences, world);
            }
        }

        void OnHandPoseLost()
        {
            for (int hand = 0; hand < 2; hand++)
            {
                m_Detectors[hand].Reset(out _);
                ShowDots(hand, false);
            }
        }

        // An object where the thumb and the pinching finger meet.
        void Place(PinchFinger finger, Dictionary<JointName, Vector3> world)
        {
            Vector3 position = (world[JointName.ThumbTip] + world[FingerPinchDetector.TipOf(finger)]) / 2f;
            var placed = GameObject.CreatePrimitive(s_Shapes[(int)finger]);
            placed.name = $"Placed {finger}";
            placed.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            placed.GetComponent<Renderer>().material.color = s_Colors[(int)finger];
            StartCoroutine(Pop(placed.transform, s_Shapes[(int)finger] == PrimitiveType.Sphere ? m_Size : m_Size * 0.8f));
            m_Placed.Enqueue(placed);
            while (m_Placed.Count > m_MaxObjects)
                Destroy(m_Placed.Dequeue());
        }

        static IEnumerator Pop(Transform target, float size)
        {
            for (float t = 0; t < 0.15f && target != null; t += Time.deltaTime)
            {
                float s = Mathf.SmoothStep(0f, 1.2f, t / 0.15f);
                target.localScale = Vector3.one * size * s;
                yield return null;
            }
            if (target != null)
                target.localScale = Vector3.one * size;
        }

        // Thumb and four fingertips; a fingertip turns its finger's colour as it nears the thumb.
        void UpdateDots(int hand, Dictionary<JointName, Vector2> joints, Dictionary<JointName, float> confidences,
            Dictionary<JointName, Vector3> world)
        {
            var dots = m_Dots[hand];
            dots[4].transform.position = world[JointName.ThumbTip];
            dots[4].material.color = Color.white;
            dots[4].gameObject.SetActive(true);
            for (int f = 0; f < 4; f++)
            {
                var tip = FingerPinchDetector.TipOf((PinchFinger)f);
                dots[f].transform.position = world[tip];
                dots[f].gameObject.SetActive(true);
                float? distance = HandGestureClassifier.PinchDistance(joints, confidences, tip);
                // 1 at the pinch threshold and below, 0 at twice the threshold and beyond.
                float near = distance.HasValue
                    ? Mathf.InverseLerp(2f * HandGestureClassifier.PinchRatio, HandGestureClassifier.PinchRatio, distance.Value)
                    : 0f;
                dots[f].material.color = Color.Lerp(new Color(1f, 1f, 1f, 0.6f), s_Colors[f], near);
                dots[f].transform.localScale = Vector3.one * Mathf.Lerp(0.008f, 0.014f, near);
            }
        }

        void ShowDots(int hand, bool show)
        {
            foreach (var dot in m_Dots[hand])
                dot.gameObject.SetActive(show);
        }
    }
}
#endif
