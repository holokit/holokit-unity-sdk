// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem.XR;

namespace HoloInteractive.XR.HoloKit
{
    /// <summary>
    /// The script responsible for the low latency tracking feature, which communicates with
    /// the native low latency tracking system and updates the camera pose accordingly.
    /// </summary>
    public class LowLatencyTrackingManager : MonoBehaviour
    {
        TrackedPoseDriver m_TrackedPoseDriver;

        /// <summary>
        /// For ARFoundation version lower than 5.0.0
        /// </summary>
        ARPoseDriver m_ARPoseDriver;

        InputDevice m_InputDevice;

        /// <summary>
        /// The native pointer of the native low latency tracking system instance.
        /// </summary>
        IntPtr m_Ptr;

#if UNITY_IOS && !UNITY_EDITOR
        private void Start()
        {
            var arCameraManager = GetComponent<ARCameraManager>();
            if (arCameraManager == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] Failed to find ARCameraManager");
                return;
            }

            m_TrackedPoseDriver = GetComponent<TrackedPoseDriver>();
            if (m_TrackedPoseDriver == null)
            {
                m_ARPoseDriver = GetComponent<ARPoseDriver>();
                if (m_ARPoseDriver == null)
                {
                    Debug.LogWarning("[LowLatencyTrackingManager] Failed to find TrackedPoseDriver");
                    return;
                }
            }

            List<InputDevice> devices = new();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.HeadMounted, devices);
            if (devices.Count > 0)
                m_InputDevice = devices[0];
            if (m_InputDevice == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] Failed to find InputDevice");
                return;
            }

            var holokitCameraManager = GetComponent<HoloKitCameraManager>();
            holokitCameraManager.OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            arCameraManager.frameReceived += OnFrameReceived;

            Application.onBeforeRender += OnBeforeRender;
            m_Ptr = Init();
            InitHeadTracker(m_Ptr);
            PauseHeadTracker(m_Ptr);
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private void OnDestroy()
        {
            Delete(m_Ptr);
        }
#endif

        private void Update() {}

#if UNITY_IOS
        private void OnScreenRenderModeChanged(ScreenRenderMode renderMode)
        {
            if (renderMode == ScreenRenderMode.Stereo)
            {
                if (m_TrackedPoseDriver != null)
                    m_TrackedPoseDriver.enabled = false;
                else
                    m_ARPoseDriver.enabled = false;
                ResumeHeadTracker(m_Ptr);
            }
            else
            {
                if (m_TrackedPoseDriver != null)
                    m_TrackedPoseDriver.enabled = true;
                else
                    m_ARPoseDriver.enabled = true;
                PauseHeadTracker(m_Ptr);
            }
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if ((m_TrackedPoseDriver != null && m_TrackedPoseDriver.enabled) || (m_ARPoseDriver != null && m_ARPoseDriver.enabled))
                return;

            bool isPositionValid = m_InputDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position) || m_InputDevice.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            bool isRotationValid = m_InputDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation) || m_InputDevice.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);

            if (isPositionValid && isRotationValid)
            {
                float[] positionArr = new float[] { position.x, position.y, position.z };
                float[] rotationArr = new float[] { rotation.x, rotation.y, rotation.z, rotation.w };
                AddSixDoFData(m_Ptr, (long)args.timestampNs, positionArr, rotationArr);
            }
        }

        private void OnBeforeRender()
        {
            if ((m_TrackedPoseDriver != null && !m_TrackedPoseDriver.enabled) || (m_ARPoseDriver != null && !m_ARPoseDriver.enabled))
                UpdateHeadTrackerPose();
        }

        private void UpdateHeadTrackerPose()
        {
            float[] positionArr = new float[3];
            float[] rotationArr = new float[4];

            GetHeadTrackerPose(m_Ptr, positionArr, rotationArr);
            Vector3 position = new(positionArr[0], positionArr[1], positionArr[2]);
            Quaternion rotation = new(rotationArr[0], rotationArr[1], rotationArr[2], rotationArr[3]);

            transform.position = position;
            transform.rotation = rotation;
        }

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_init")]
        static extern IntPtr Init();

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_initHeadTracker")]
        static extern void InitHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_pauseHeadTracker")]
        static extern void PauseHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_resumeHeadTracker")]
        static extern void ResumeHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_addSixDoFData")]
        static extern void AddSixDoFData(IntPtr self, long timestamp, [In] float[] position, [In] float[] orientation);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_getHeadTrackerPose")]
        static extern void GetHeadTrackerPose(IntPtr self, [Out] float[] position, [Out] float[] orientation);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking_delete")]
        static extern void Delete(IntPtr self);
#endif
    }
}
