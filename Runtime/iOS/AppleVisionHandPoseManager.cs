// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloInteractive.XR.HoloKit.iOS
{
    [Serializable]
    public enum MaxHandCount
    {
        One = 1,
        Two = 2
    }

    public class AppleVisionHandPoseManager : MonoBehaviour
    {
        public bool Active
        {
            get => m_Active;
            set
            {
                m_Active = value;
            }
        }

        public int HandCount { get; private set; }

        ARCameraManager m_ARCameraManager;

        AppleVisionHandPoseDetector m_HandPoseDetector;

        [SerializeField] bool m_Active = true;

        [Tooltip("This value can only be either 1 or 2")]
        [SerializeField] MaxHandCount m_MaxHandCount = MaxHandCount.One;

        public event Action OnHandPoseUpdated;

        private void Start()
        {
            m_ARCameraManager = FindObjectOfType<ARCameraManager>();
            if (m_ARCameraManager == null)
            {
                Debug.LogWarning("Failed to find ARCameraManager");
                return;
            }

            m_HandPoseDetector = new((int)m_MaxHandCount);
            m_ARCameraManager.frameReceived += OnFrameReceived;
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (m_Active)
            {
                if (m_HandPoseDetector.ProcessCurrentFrame())
                {
                    HandCount = m_HandPoseDetector.GetHandCount();
                    if (HandCount > 0)
                    {
                        OnHandPoseUpdated?.Invoke();
                    }
                }
                else
                {
                    HandCount = 0;
                }
            }
        }

        public Vector2 GetHandJointLocation(int handIndex, JointName jointName)
        {
            return m_HandPoseDetector.GetHandJointLocation(handIndex, jointName);
        }

        public float GetHandJointConfidence(int handIndex, JointName jointName)
        {
            return m_HandPoseDetector.GetHandJointConfidence(handIndex, jointName);
        }

        private void OnDestroy()
        {
            m_HandPoseDetector.Dispose();
        }
    }
}
