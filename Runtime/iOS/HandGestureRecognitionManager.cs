// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public enum HandGesture
    {
        None = 0,
        Pinched = 1,
        Apart = 2
    }

    public class HandGestureRecognitionManager : MonoBehaviour
    {
        public HandGesture HandGesture => m_HandGesture;

        public event Action<HandGesture> OnHandGestureChanged;

        AppleVisionHandPoseDetector m_HandPoseDetector;

        HandTrackingManager m_HandTrackingManager;

        HandGesture m_HandGesture = HandGesture.None;

        private const float PINCH_THRESHOLD = 0.12f;

        private void Start()
        {
            m_HandTrackingManager = FindObjectOfType<HandTrackingManager>();
            if (m_HandTrackingManager == null)
            {
                var arCameraManager = FindObjectOfType<ARCameraManager>();
                if (arCameraManager == null)
                {
                    Debug.Log("HandGestureManager won't work without ARCameraManager in the scene");
                    return;
                }

                arCameraManager.frameReceived += OnFrameReceived;
                m_HandPoseDetector = new(MaxHandCount.One);
                m_HandPoseDetector.OnHandPoseUpdated += OnHandPoseUpdated;
                m_HandPoseDetector.OnHandPoseLost += OnHandPoseLost;
            }
            else
            {
                m_HandTrackingManager.HandPoseDetector.OnHandPoseUpdated += OnHandPoseUpdated;
                m_HandTrackingManager.HandPoseDetector.OnHandPoseLost += OnHandPoseLost;
            }
        }

        private void OnDestroy()
        {
            if (m_HandPoseDetector != null)
                m_HandPoseDetector.Dispose();
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (m_HandTrackingManager == null && enabled)
            {
                m_HandPoseDetector.ProcessCurrentFrame2D();
            }
        }

        private void OnHandPoseUpdated()
        {
            HandGesture handGesture = HandGesture.None;
            var handJoints = m_HandTrackingManager == null ? m_HandPoseDetector.HandPoses2D[0] : m_HandTrackingManager.HandPoseDetector.HandPoses2D[0];
            if (Vector2.Distance(handJoints[JointName.ThumbTip], handJoints[JointName.IndexTip]) < PINCH_THRESHOLD)
            {
                handGesture = HandGesture.Pinched;
            }
            else if (handJoints[JointName.ThumbTip].y > handJoints[JointName.ThumbIP].y && handJoints[JointName.ThumbIP].y > handJoints[JointName.ThumbMP].y && handJoints[JointName.ThumbMP].y > handJoints[JointName.ThumbCMC].y &&
                     handJoints[JointName.IndexTip].y > handJoints[JointName.IndexDIP].y && handJoints[JointName.IndexDIP].y > handJoints[JointName.IndexPIP].y && handJoints[JointName.IndexPIP].y > handJoints[JointName.IndexMCP].y &&
                     handJoints[JointName.MiddleTip].y > handJoints[JointName.MiddleDIP].y && handJoints[JointName.MiddleDIP].y > handJoints[JointName.MiddlePIP].y && handJoints[JointName.MiddlePIP].y > handJoints[JointName.MiddleMCP].y &&
                     handJoints[JointName.RingTip].y > handJoints[JointName.RingDIP].y && handJoints[JointName.RingDIP].y > handJoints[JointName.RingPIP].y && handJoints[JointName.RingPIP].y > handJoints[JointName.RingMCP].y &&
                     handJoints[JointName.LittleTip].y > handJoints[JointName.LittleDIP].y && handJoints[JointName.LittleDIP].y > handJoints[JointName.LittlePIP].y && handJoints[JointName.LittlePIP].y > handJoints[JointName.LittleMCP].y)
            {
                handGesture = HandGesture.Apart;
            }

            if (m_HandGesture != handGesture)
            {
                m_HandGesture = handGesture;
                OnHandGestureChanged?.Invoke(m_HandGesture);
                Debug.Log($"Hand gesture changed to {m_HandGesture}");
                return;
            }
        }

        private void OnHandPoseLost()
        {
            if (m_HandGesture != HandGesture.None)
            {
                m_HandGesture = HandGesture.None;
                OnHandGestureChanged?.Invoke(m_HandGesture);
            }
        }
    }
}
