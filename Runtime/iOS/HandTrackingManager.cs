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

        [Tooltip("Show every tracked hand as the HoloKit Swift SDK's skeleton (yellow joints, cyan bones), " +
                 "drawn over the real hand so AR occlusion never hides it (HandSkeletonView).")]
        [SerializeField] bool m_HandJointsVisibility = true;

        List<GameObject> m_Hands = new();

        List<Dictionary<JointName, GameObject>> m_HandJoints = new();

        [Tooltip("Run Vision on every Nth AR frame. 3 (the Swift SDK's default) is about 20 Hz at ARKit's 60 Hz: " +
                 "enough for gestures at a third of the cost of every frame. 1 runs it on every frame.")]
        [SerializeField, Min(1)] int m_FrameInterval = 3;

        int m_FrameIndex;

        /// <summary>Vision runs on every <c>FrameInterval</c>-th AR frame (3 by default, about 20 Hz).</summary>
        public int FrameInterval
        {
            get => m_FrameInterval;
            set => m_FrameInterval = Mathf.Max(1, value);
        }

        [Tooltip("Smooth each joint's world position with a 1€ filter, as the HoloKit Swift SDK does: a still hand stops jittering, a moving one lags little.")]
        [SerializeField] bool m_SmoothJoints = true;

        [Tooltip("1€ filter settings: Minimum Cutoff (Hz) sets how still a still hand is, Beta how quickly it follows speed.")]
        [SerializeField] HandSmoothing m_Smoothing = HandSmoothing.Default;

        readonly HandJointSmoother m_Smoother = new();
        readonly List<Dictionary<JointName, Vector3>> m_SmoothedPoses = new() { new(), new() };

        /// <summary>Smooth joint world positions (1€ filter); off returns Vision and LiDAR's raw positions.</summary>
        public bool SmoothJoints
        {
            get => m_SmoothJoints;
            set { m_SmoothJoints = value; m_Smoother.Reset(); }
        }

        /// <summary>The 1€ filter settings for joint world positions.</summary>
        public HandSmoothing Smoothing
        {
            get => m_Smoothing;
            set { m_Smoothing = value; m_Smoother.Reset(); }
        }

        /// <summary>
        /// Joint world positions of each hand: smoothed when <see cref="SmoothJoints"/> is on.
        /// <see cref="HandPoseDetector"/> keeps the raw ones.
        /// </summary>
        public IReadOnlyList<Dictionary<JointName, Vector3>> HandPoses3D =>
            m_SmoothJoints ? m_SmoothedPoses : m_HandPoseDetector.HandPoses3D;

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
            var arCameraManager = FindFirstObjectByType<ARCameraManager>();
            if (arCameraManager == null)
            {
                Debug.LogWarning("HandTrackingManager won't work without ARCameraManager in the scene");
                return;
            }

            if (FindFirstObjectByType<AROcclusionManager>() == null)
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
                    // The joint objects stay as positioned markers for code that reads them;
                    // HandSkeletonView draws the visible hands.
                    if (joint.TryGetComponent<MeshRenderer>(out var jointRenderer))
                        jointRenderer.enabled = false;
                }
            }
            if (m_HandJointsVisibility && !TryGetComponent<HandSkeletonView>(out _))
                gameObject.AddComponent<HandSkeletonView>();

            arCameraManager.frameReceived += OnFrameReceived;
            m_HandPoseDetector = new(m_MaxHandCount);
            // Joints arrive in ARKit session space, like the AR camera's pose: the XR Origin's camera offset.
            var origin = arCameraManager.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            m_HandPoseDetector.SessionSpace = origin != null && origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform
                : arCameraManager.transform.parent;
            m_HandPoseDetector.OnHandPoseUpdated += OnHandPoseUpdated;
            m_HandPoseDetector.OnHandPoseLost += OnHandPoseLost;
        }

        private void OnDestroy()
        {
            m_HandPoseDetector.Dispose();
        }

        // Switched off (e.g. HANDS OFF in the stereo bar's OPTIONS): no stale hands stay in view.
        private void OnDisable()
        {
            // Not while the whole object deactivates: its hands go with it.
            if (gameObject.activeInHierarchy)
                OnHandPoseLost();
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (enabled && ++m_FrameIndex % Mathf.Max(1, m_FrameInterval) == 0)
            {
                m_HandPoseDetector.ProcessCurrentFrame3D();
            }
        }

        private void OnHandPoseUpdated()
        {
            if (m_SmoothJoints)
                m_Smoother.Smooth(Mathf.Min(m_HandPoseDetector.HandCount, 2), m_HandPoseDetector.HandPoses3D,
                    m_SmoothedPoses, m_HandPoseDetector.LastUpdateTime, m_Smoothing);
            var poses = HandPoses3D;
            for (int i = 0; i < m_HandJoints.Count; i++)
            {
                if (i < m_HandPoseDetector.HandCount)
                {
                    m_Hands[i].SetActive(true);
                    for (int j = 0; j < 21; j++)
                    {
                        JointName jointName = (JointName)j;
                        m_HandJoints[i][jointName].transform.position = poses[i][jointName];
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
            m_Smoother.Reset();
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
                return HandPoses3D[handIndex][jointName];
            }
            else
            {
                return Vector3.zero;
            }
        }
    }
}
#endif
