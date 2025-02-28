// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    public class HandTrackingManager : MonoBehaviour
    {
        public AppleVisionHandPoseDetector HandPoseDetector => m_HandPoseDetector;

        public int HandCount => m_HandPoseDetector.HandCount;

        AppleVisionHandPoseDetector m_HandPoseDetector;

        [Tooltip("The maximum number of hands to be detected. We recommend to set this value to 1 to save energy if you don't need to detect both hands.")]
        [SerializeField] MaxHandCount m_MaxHandCount = MaxHandCount.One;

        [Tooltip("Set this value to true to show the position of each hand joint. Set this value to false the hide hand joints.")]
        [SerializeField] bool m_HandJointsVisibility = true;

        List<GameObject> m_Hands = new();

        List<Dictionary<JointName, GameObject>> m_HandJoints = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null && gameObject != null) // Check to make sure the object hasn't been destroyed in the meantime
                    {
                        InitializeInEditor();
                    }
                };
            }
        }

        private void InitializeInEditor()
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
#endif

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
                    if (!m_HandJointsVisibility)
                        joint.GetComponent<MeshRenderer>().enabled = false;
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

        /// <summary>
        /// Get the position of the specific hand joint of the given hand.
        /// </summary>
        /// <param name="handIndex">The index of the hand</param>
        /// <param name="jointName">The hand joint name</param>
        /// <returns>The position of the hand joint</returns>
        public Vector3 GetHandJointPosition(int handIndex, JointName jointName)
        {
            if (handIndex < HandCount)
            {
                return m_HandPoseDetector.HandPoses3D[handIndex][jointName];
            }
            else
            {
                return Vector3.zero;
            }
        }
    }
}
#endif
