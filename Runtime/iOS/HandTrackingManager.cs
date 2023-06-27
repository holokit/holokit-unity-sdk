// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class HandTrackingManager : MonoBehaviour
    {
        public AppleVisionHandPoseDetector HandPoseDetector => m_HandPoseDetector;

        AppleVisionHandPoseDetector m_HandPoseDetector;

        [SerializeField] MaxHandCount m_MaxHandCount = MaxHandCount.One;

        List<GameObject> m_Hands = new();

        List<Dictionary<JointName, GameObject>> m_HandJoints = new();

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (transform.childCount > 0)
                return;

            gameObject.name = "Hand Tracking Manager";
            transform.position = Vector3.zero;

            for (int i = 0; i < 2; i++)
            {
                GameObject hand = new($"Hand {i}");
                hand.transform.SetParent(transform);
                for (int j = 0; j < 21; j++)
                {
                    GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    joint.name = ((JointName)j).ToString();
                    joint.transform.localScale = new(0.01f, 0.01f, 0.01f);
                    joint.transform.SetParent(hand.transform);
                }
            }
        }

        private void Awake()
        {
            var arCameraManager = FindObjectOfType<ARCameraManager>();
            if (arCameraManager == null)
            {
                Debug.LogWarning("HandTrackingManager won't work without ARCameraManager in the scene");
                return;
            }

            if (FindObjectOfType<AROcclusionManager>() == null)
            {
                Debug.LogWarning("HandTrackingManager won't work without AROcclusionManager in the scene");
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                var hand = transform.GetChild(i);
                m_Hands.Add(hand.gameObject);
                Dictionary<JointName, GameObject> dict = new();
                m_HandJoints.Add(dict);
                for (int j = 0; j < hand.childCount; j++)
                {
                    var joint = hand.GetChild(j);
                    m_HandJoints[i].Add((JointName)j, joint.gameObject);
                }
            }

            arCameraManager.frameReceived += OnFrameReceived;
            m_HandPoseDetector = new(m_MaxHandCount);
            m_HandPoseDetector.OnHandPoseUpdated += OnHandPoseUpdated;
            m_HandPoseDetector.OnHandPoseLost += OnHandPoseLost;
        }

        private void OnDestroy()
        {
            m_HandPoseDetector.Dispose();
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (enabled)
            {
                m_HandPoseDetector.ProcessCurrentFrame3D();
            }
        }

        private void OnHandPoseUpdated()
        {
            for (int i = 0; i < m_HandJoints.Count; i++)
            {
                if (i < m_HandPoseDetector.HandCount)
                {
                    m_Hands[i].SetActive(true);
                    for (int j = 0; j < 21; j++)
                    {
                        JointName jointName = (JointName)j;
                        m_HandJoints[i][jointName].transform.position = m_HandPoseDetector.HandPoses3D[i][jointName];
                    }
                }
                else
                {
                    m_Hands[i].SetActive(false);
                }
            }
        }

        private void OnHandPoseLost()
        {
            foreach (var hand in m_Hands)
            {
                hand.SetActive(false);
            }
        }
    }
}
