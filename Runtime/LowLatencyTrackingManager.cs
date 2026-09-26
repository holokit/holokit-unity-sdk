// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace HoloKit
{
    /// <summary>
    /// Predicts the stereo camera pose for the moment each frame is seen.
    /// </summary>
    /// <remarks>
    /// ARKit poses reach Unity 40-60 ms after capture. In Stereo mode a native
    /// Kalman filter (Runtime/iOS/NativeCode/HoloKitTrackingCore, the same filter
    /// as holokit-swift-sdk) is corrected with every ARFrame and integrates the
    /// gyroscope and accelerometer (Core Motion, 100 Hz) forward. Before each
    /// render it predicts to the frame's display time, measured from the Metal
    /// drawable's presentedTime plus scan-out, and smooths frame-to-frame jumps.
    /// See Documentation~/LowLatencyTracking.md.
    /// </remarks>
    public class LowLatencyTrackingManager : MonoBehaviour
    {
        [Tooltip("Rotation smoothing time constant in seconds; 0 disables.")]
        [SerializeField] float m_RotationSmoothing = 0.01f;

        [Tooltip("Position smoothing time constant in seconds; 0 disables.")]
        [SerializeField] float m_PositionSmoothing = 0.03f;

        /// <summary>Whether the last rendered stereo pose was predicted.</summary>
        public bool IsPredicting { get; private set; }

        /// <summary>Smoothed render-start-to-presented time in seconds, or -1 before it is measured.</summary>
        public double PresentLatency => IsNative ? HoloKit_Tracking_presentLatency() : -1;

        /// <summary>How far past render start the pose is predicted, in seconds.</summary>
        public double PredictionLeadTime => IsNative ? HoloKit_Tracking_leadTime() : 0;

        /// <summary>Age of the newest IMU sample at render start in seconds, or -1.</summary>
        public double IMUSampleAge => IsNative ? HoloKit_Tracking_imuSampleAge() : -1;

        /// <summary>Orientation error in degrees the last ARKit frame corrected (filter vs. ARKit at the capture time).</summary>
        public float LastCorrectionDegrees => IsNative ? HoloKit_Tracking_lastInnovationDegrees() : 0;

#if UNITY_IOS && !UNITY_EDITOR
        static readonly bool IsNative = true;
#else
        static readonly bool IsNative = false;
#endif

        ARCameraManager m_ARCameraManager;
        HoloKitCameraManager m_HoloKitCameraManager;
        Transform m_SessionSpace;
        XRSessionSubsystem m_Session;
        bool m_Running;
        readonly float[] m_Position = new float[3];
        readonly float[] m_Rotation = new float[4];

        void Start()
        {
            if (!IsNative)
                return;
            m_ARCameraManager = FindFirstObjectByType<ARCameraManager>();
            m_HoloKitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            if (m_ARCameraManager == null || m_HoloKitCameraManager == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] ARCameraManager or HoloKitCameraManager missing.");
                return;
            }
            // ARKit poses are in session space: the XR Origin's camera offset.
            var origin = GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            m_SessionSpace = origin != null && origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform
                : m_ARCameraManager.transform.parent;
            m_HoloKitCameraManager.OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            OnScreenRenderModeChanged(m_HoloKitCameraManager.ScreenRenderMode);
        }

        void OnDestroy()
        {
            if (m_HoloKitCameraManager != null)
                m_HoloKitCameraManager.OnScreenRenderModeChanged -= OnScreenRenderModeChanged;
            Stop();
        }

        void OnValidate()
        {
            if (m_Running)
                HoloKit_Tracking_setSmoothing(m_RotationSmoothing, m_PositionSmoothing);
        }

        void OnScreenRenderModeChanged(ScreenRenderMode mode)
        {
            if (mode == ScreenRenderMode.Stereo) Run();
            else Stop();
        }

        void Run()
        {
            if (m_Running || !IsNative)
                return;
            var sessions = new List<XRSessionSubsystem>();
            SubsystemManager.GetSubsystems(sessions);
            m_Session = sessions.Count > 0 ? sessions[0] : null;
            if (m_Session == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] No XR session subsystem.");
                return;
            }
            HoloKit_Tracking_setSmoothing(m_RotationSmoothing, m_PositionSmoothing);
            HoloKit_Tracking_start();
            m_ARCameraManager.frameReceived += OnFrameReceived;
            Application.onBeforeRender += OnBeforeRender;
            m_Running = true;
        }

        void Stop()
        {
            if (!m_Running)
                return;
            m_ARCameraManager.frameReceived -= OnFrameReceived;
            Application.onBeforeRender -= OnBeforeRender;
            HoloKit_Tracking_stop();
            m_Running = false;
            IsPredicting = false;
        }

        void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (m_Session != null)
                HoloKit_Tracking_observeSession(m_Session.nativePtr);
        }

        // Registered after ARFoundation's pose driver, so this pose wins.
        void OnBeforeRender()
        {
            double renderStart = HoloKit_Tracking_markRenderStart();
            IsPredicting = HoloKit_Tracking_predict(renderStart, m_Position, m_Rotation);
            if (!IsPredicting)
                return;
            var position = new Vector3(m_Position[0], m_Position[1], m_Position[2]);
            var rotation = new Quaternion(m_Rotation[0], m_Rotation[1], m_Rotation[2], m_Rotation[3]);
            if (m_SessionSpace != null)
                transform.SetPositionAndRotation(m_SessionSpace.TransformPoint(position),
                                                 m_SessionSpace.rotation * rotation);
            else
                transform.SetPositionAndRotation(position, rotation);
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void HoloKit_Tracking_start();
        [DllImport("__Internal")] static extern void HoloKit_Tracking_stop();
        [DllImport("__Internal")] static extern bool HoloKit_Tracking_observeSession(System.IntPtr nativeSession);
        [DllImport("__Internal")] static extern double HoloKit_Tracking_markRenderStart();
        [DllImport("__Internal")] static extern bool HoloKit_Tracking_predict(double now, [Out] float[] position, [Out] float[] rotation);
        [DllImport("__Internal")] static extern double HoloKit_Tracking_presentLatency();
        [DllImport("__Internal")] static extern double HoloKit_Tracking_leadTime();
        [DllImport("__Internal")] static extern double HoloKit_Tracking_imuSampleAge();
        [DllImport("__Internal")] static extern float HoloKit_Tracking_lastInnovationDegrees();
        [DllImport("__Internal")] static extern void HoloKit_Tracking_setSmoothing(double rotationSeconds, double positionSeconds);
#else
        static void HoloKit_Tracking_start() {}
        static void HoloKit_Tracking_stop() {}
        static bool HoloKit_Tracking_observeSession(System.IntPtr nativeSession) => false;
        static double HoloKit_Tracking_markRenderStart() => 0;
        static bool HoloKit_Tracking_predict(double now, float[] position, float[] rotation) => false;
        static double HoloKit_Tracking_presentLatency() => -1;
        static double HoloKit_Tracking_leadTime() => 0;
        static double HoloKit_Tracking_imuSampleAge() => -1;
        static float HoloKit_Tracking_lastInnovationDegrees() => 0;
        static void HoloKit_Tracking_setSmoothing(double rotationSeconds, double positionSeconds) {}
#endif
    }
}
