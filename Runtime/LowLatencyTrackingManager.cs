// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem.XR;

namespace HoloKit
{
    /// <summary>
    /// The script responsible for the low latency tracking feature, which communicates with
    /// the native low latency tracking system and updates the camera pose accordingly.
    /// </summary>
    public class LowLatencyTrackingManager : MonoBehaviour
    {
        InputDevice m_InputDevice;

        /// <summary>
        /// The native pointer of the native low latency tracking system instance.
        /// </summary>
        IntPtr m_Ptr;

        private ARCameraManager m_ARCameraManager;

#if UNITY_IOS && !UNITY_EDITOR
        private void Start()
        {
            m_ARCameraManager = FindFirstObjectByType<ARCameraManager>();
            if (m_ARCameraManager == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] Failed to find ARCameraManager in the scene.");
                return;
            }

            List<InputDevice> devices = new();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, devices);
            if (devices.Count > 0)
                m_InputDevice = devices[0];
            if (m_InputDevice == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] Failed to find InputDevice.");
                return;
            }

            var holoKitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            if (holoKitCameraManager == null)
            {
                Debug.LogWarning("[LowLatencyTrackingManager] Failed to find HoloKitCameraManager in the scene.");
                return;
            }

            holoKitCameraManager.OnScreenRenderModeChanged += OnScreenRenderModeChanged;

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
                m_ARCameraManager.frameReceived += OnFrameReceived;
                Application.onBeforeRender += OnBeforeRender;
                ResumeHeadTracker(m_Ptr);
            }
            else
            {
                m_ARCameraManager.frameReceived -= OnFrameReceived;
                Application.onBeforeRender -= OnBeforeRender;
                PauseHeadTracker(m_Ptr);
            }
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
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
            UpdateHeadTrackerPose();
        }

        private void UpdateHeadTrackerPose()
        {
            float[] positionArr = new float[3];
            float[] rotationArr = new float[4];

            GetHeadTrackerPose(m_Ptr, positionArr, rotationArr);
            Vector3 position = new(positionArr[0], positionArr[1], positionArr[2]);
            Quaternion rotation = new(rotationArr[0], rotationArr[1], rotationArr[2], rotationArr[3]);

            transform.SetPositionAndRotation(position, rotation);
        }

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_init")]
        static extern IntPtr Init();

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_initHeadTracker")]
        static extern void InitHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_pauseHeadTracker")]
        static extern void PauseHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_resumeHeadTracker")]
        static extern void ResumeHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_addSixDoFData")]
        static extern void AddSixDoFData(IntPtr self, long timestamp, [In] float[] position, [In] float[] orientation);

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_getHeadTrackerPose")]
        static extern void GetHeadTrackerPose(IntPtr self, [Out] float[] position, [Out] float[] orientation);

        [DllImport("__Internal", EntryPoint = "HoloKit_LowLatencyTracking_delete")]
        static extern void Delete(IntPtr self);
#endif
    }
}
