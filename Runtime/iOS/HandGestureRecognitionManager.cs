// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    public class HandGestureRecognitionManager : MonoBehaviour
    {
        public HandGesture HandGesture => m_Filter.Gesture;

        public event Action<HandGesture> OnHandGestureChanged;

        AppleVisionHandPoseDetector m_HandPoseDetector;

        HandTrackingManager m_HandTrackingManager;

        // Swift SDK GestureRecognizer: four matching observations change the gesture, and a
        // Vision dropout shorter than 0.2 s keeps it, so one pinch is not Pinched/None/Pinched.
        readonly HandGestureFilter m_Filter = new();

        private void Start()
        {
            m_HandTrackingManager = FindFirstObjectByType<HandTrackingManager>();
            if (m_HandTrackingManager == null)
            {
                var arCameraManager = FindFirstObjectByType<ARCameraManager>();
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

        AppleVisionHandPoseDetector Detector =>
            m_HandTrackingManager != null ? m_HandTrackingManager.HandPoseDetector : m_HandPoseDetector;

        private void OnHandPoseUpdated()
        {
            var detector = Detector;
            if (detector == null || detector.HandCount == 0)
                return;
            // Classifies Vision's 2D image joints of the first hand; smoothing does not touch them.
            HandGesture observed = HandGestureClassifier.Classify(detector.HandPoses2D[0], detector.HandPosesConfidence[0]);
            if (m_Filter.Observe(observed, Time.realtimeSinceStartupAsDouble))
                OnHandGestureChanged?.Invoke(m_Filter.Gesture);
        }

        private void OnHandPoseLost() => Release();

        private void Update()
        {
            var detector = Detector;
            if (detector != null && detector.HandCount == 0)
                Release();
        }

        void Release()
        {
            if (m_Filter.Lost(Time.realtimeSinceStartupAsDouble))
                OnHandGestureChanged?.Invoke(m_Filter.Gesture);
        }

        private void OnDisable()
        {
            bool had = m_Filter.Gesture != HandGesture.None;
            m_Filter.Reset();
            if (had)
                OnHandGestureChanged?.Invoke(HandGesture.None);
        }
    }
}
#endif
