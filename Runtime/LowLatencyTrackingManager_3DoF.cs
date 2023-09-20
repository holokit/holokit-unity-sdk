// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

namespace HoloInteractive.XR.HoloKit
{
    public class LowLatencyTrackingManager_3DoF : MonoBehaviour
    {
        IntPtr m_Ptr;

        private void Start()
        {
            Application.onBeforeRender += OnBeforeRender;
            m_Ptr = Init();
            InitHeadTracker(m_Ptr);
        }

        private void OnDestroy()
        {
            Delete(m_Ptr);
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

            transform.position = position;
            transform.rotation = rotation;
        }

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking3DoF_init")]
        static extern IntPtr Init();

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking3DoF_initHeadTracker")]
        static extern void InitHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking3DoF_pauseHeadTracker")]
        static extern void PauseHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking3DoF_resumeHeadTracker")]
        static extern void ResumeHeadTracker(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking3DoF_getHeadTrackerPose")]
        static extern void GetHeadTrackerPose(IntPtr self, [Out] float[] position, [Out] float[] orientation);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_LowLatencyTracking3DoF_delete")]
        static extern void Delete(IntPtr self);
    }
}
