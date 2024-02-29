// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    public enum HandGesture
    {
        None = 0,
        Pinched = 1,
        Five = 2
    }

    public class HandGestureRecognitionManager : MonoBehaviour
    {
        public HandGesture HandGesture => m_HandGesture;

        public event Action<HandGesture> OnHandGestureChanged;

        AppleVisionHandPoseDetector m_HandPoseDetector;

        HandTrackingManager m_HandTrackingManager;

        HandGesture m_HandGesture = HandGesture.None;

        int m_PinchEvidenceCounter = 0;

        int m_FiveEvidenceCounter = 0;

        int m_NoneEvidenceCounter = 0;

        const float PINCH_THRESHOLD = 0.12f;

        const int EVIDENCE_COUNTER_TRIGGER = 3;

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
                m_PinchEvidenceCounter++;
                m_FiveEvidenceCounter = 0;
                m_NoneEvidenceCounter = 0;
                if (m_PinchEvidenceCounter > EVIDENCE_COUNTER_TRIGGER)
                    handGesture = HandGesture.Pinched;
            }
            else
            {
                bool isThumbStraight = handJoints[JointName.ThumbTip].y > handJoints[JointName.ThumbIP].y && handJoints[JointName.ThumbIP].y > handJoints[JointName.ThumbMP].y && handJoints[JointName.ThumbMP].y > handJoints[JointName.ThumbCMC].y;
                bool isIndexStraight = handJoints[JointName.IndexTip].y > handJoints[JointName.IndexDIP].y && handJoints[JointName.IndexDIP].y > handJoints[JointName.IndexPIP].y && handJoints[JointName.IndexPIP].y > handJoints[JointName.IndexMCP].y;
                bool isMiddleStraight = handJoints[JointName.MiddleTip].y > handJoints[JointName.MiddleDIP].y && handJoints[JointName.MiddleDIP].y > handJoints[JointName.MiddlePIP].y && handJoints[JointName.MiddlePIP].y > handJoints[JointName.MiddleMCP].y;
                bool isRingStraight = handJoints[JointName.RingTip].y > handJoints[JointName.RingDIP].y && handJoints[JointName.RingDIP].y > handJoints[JointName.RingPIP].y && handJoints[JointName.RingPIP].y > handJoints[JointName.RingMCP].y;
                bool isLittleStraight = handJoints[JointName.LittleTip].y > handJoints[JointName.LittleDIP].y && handJoints[JointName.LittleDIP].y > handJoints[JointName.LittlePIP].y && handJoints[JointName.LittlePIP].y > handJoints[JointName.LittleMCP].y;
                
                if (isThumbStraight && isIndexStraight && isMiddleStraight && isRingStraight && isLittleStraight) // Five
                {
                    m_PinchEvidenceCounter = 0;
                    m_FiveEvidenceCounter++;
                    m_NoneEvidenceCounter = 0;
                    if (m_FiveEvidenceCounter > EVIDENCE_COUNTER_TRIGGER)
                        handGesture = HandGesture.Five;
                }
                else // None
                {
                    m_PinchEvidenceCounter = 0;
                    m_FiveEvidenceCounter = 0;
                    m_NoneEvidenceCounter++;
                    if (m_NoneEvidenceCounter > EVIDENCE_COUNTER_TRIGGER)
                        handGesture = HandGesture.None;
                }
            }

            if (m_HandGesture != handGesture)
            {
                m_HandGesture = handGesture;
                OnHandGestureChanged?.Invoke(m_HandGesture);
            }
        }

        private void OnHandPoseLost()
        {
            if (m_HandGesture != HandGesture.None)
            {
                m_HandGesture = HandGesture.None;
                m_PinchEvidenceCounter = 0;
                m_FiveEvidenceCounter = 0;
                m_NoneEvidenceCounter = EVIDENCE_COUNTER_TRIGGER;
                OnHandGestureChanged?.Invoke(m_HandGesture);
            }
        }
    }
}
#endif
