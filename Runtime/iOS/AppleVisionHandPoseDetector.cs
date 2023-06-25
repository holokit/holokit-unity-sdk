// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class AppleVisionHandPoseDetector : IDisposable
    {
        IntPtr m_Ptr;

        public AppleVisionHandPoseDetector(int maximumHandCount)
        {
            List<XRSessionSubsystem> xrSessionSubsystems = new();
            SubsystemManager.GetSubsystems(xrSessionSubsystems);
            if (xrSessionSubsystems.Count == 0)
            {
                Debug.LogWarning("Cannot find XRSessionSubsystem");
                return;
            }
            XRSessionSubsystem xrSessionSubsystem = xrSessionSubsystems[0];
            m_Ptr = InitWithARSession(xrSessionSubsystem.nativePtr, maximumHandCount);
        }

        public bool ProcessCurrentFrame()
        {
            return ProcessCurrentFrame(m_Ptr);
        }

        public int GetHandCount()
        {
            return GetResultCount(m_Ptr);
        }

        public Vector2 GetHandJointLocation(int handIndex, JointName jointName)
        {
            GetHandJointLocation(m_Ptr, handIndex, (int)jointName, out float x, out float y);
            return new Vector2(x, y);
        }

        public float GetHandJointConfidence(int handIndex, JointName jointName)
        {
            return GetHandJointConfidence(m_Ptr, handIndex, (int)jointName);
        }

        public void Dispose()
        {
            if (m_Ptr != IntPtr.Zero)
            {
                NativeApi.CFRelease(ref m_Ptr);
                m_Ptr = IntPtr.Zero;
            }
        }

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_initWithARSession")]
        static extern IntPtr InitWithARSession(IntPtr arSessionPtr, int maximumHandCount);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_processCurrentFrame")]
        static extern bool ProcessCurrentFrame(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_getResultCount")]
        static extern int GetResultCount(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_getHandJointLocation")]
        static extern void GetHandJointLocation(IntPtr self, int handIndex, int jointIndex, out float x, out float y);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_getHandJointConfidence")]
        static extern float GetHandJointConfidence(IntPtr self, int handIndex, int jointIndex);
    }
}
