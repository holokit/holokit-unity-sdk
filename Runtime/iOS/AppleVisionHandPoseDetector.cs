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
    [Serializable]
    public enum MaxHandCount
    {
        One = 1,
        Both = 2
    }

    public class AppleVisionHandPoseDetector : IDisposable
    {
        public int HandCount => m_HandCount;

        public List<Dictionary<JointName, Vector2>> HandPoses2D => m_HandPoses2D;

        public List<Dictionary<JointName, Vector3>> HandPoses3D => m_HandPoses3D;

        public List<Dictionary<JointName, float>> HandPosesConfidence => m_HandPosesConfidence;

        public event Action OnHandPoseUpdated;

        public event Action OnHandPoseLost;

        IntPtr m_Ptr;

        int m_HandCount;

        List<Dictionary<JointName, Vector2>> m_HandPoses2D;

        List<Dictionary<JointName, Vector3>> m_HandPoses3D;

        List<Dictionary<JointName, float>> m_HandPosesConfidence;

        static Dictionary<IntPtr, AppleVisionHandPoseDetector> s_Detectors = new();

        public AppleVisionHandPoseDetector(MaxHandCount maxHandCount)
        {
            List<XRSessionSubsystem> xrSessionSubsystems = new();
            SubsystemManager.GetSubsystems(xrSessionSubsystems);
            if (xrSessionSubsystems.Count == 0)
            {
                Debug.LogWarning("Cannot find XRSessionSubsystem");
                return;
            }
            XRSessionSubsystem xrSessionSubsystem = xrSessionSubsystems[0];

            m_Ptr = InitWithARSession(xrSessionSubsystem.nativePtr, (int)maxHandCount);
            m_HandCount = 0;
            m_HandPoses2D = new();
            m_HandPoses3D = new();
            m_HandPosesConfidence = new();
            for (int i = 0; i < 2; i++)
            {
                m_HandPoses2D.Add(new Dictionary<JointName, Vector2>());
                m_HandPoses3D.Add(new Dictionary<JointName, Vector3>());
                m_HandPosesConfidence.Add(new Dictionary<JointName, float>());
            }
            RegisterCallbacks(m_Ptr, OnHandPoseUpdatedCallback);

            s_Detectors[m_Ptr] = this;
        }

        public void ProcessCurrentFrame2D()
        {
            ProcessCurrentFrame2D(m_Ptr);
        }

        public void ProcessCurrentFrame3D()
        {
            ProcessCurrentFrame3D(m_Ptr);
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

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_registerCallbacks")]
        static extern IntPtr RegisterCallbacks(IntPtr self, Action<IntPtr, int, IntPtr, IntPtr, IntPtr> onHandPoseUpdatedCallback);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_processCurrentFrame2D")]
        static extern bool ProcessCurrentFrame2D(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_AppleVisionHandPoseDetector_processCurrentFrame3D")]
        static extern bool ProcessCurrentFrame3D(IntPtr self);

        [AOT.MonoPInvokeCallback(typeof(Action<IntPtr, int, IntPtr, IntPtr, IntPtr>))]
        static void OnHandPoseUpdatedCallback(IntPtr detectorPtr, int handCount, IntPtr results2DPtr, IntPtr results3DPtr, IntPtr confidencesPtr)
        {
            if (s_Detectors.TryGetValue(detectorPtr, out AppleVisionHandPoseDetector detector))
            {
                if (handCount == 0)
                {
                    if (detector.m_HandCount > 0)
                    {
                        detector.m_HandCount = handCount;
                        detector.OnHandPoseLost?.Invoke();
                    }
                    return;
                }
                detector.m_HandCount = handCount;

                if (results2DPtr != IntPtr.Zero)
                {
                    int length = 2 * 21 * handCount;
                    float[] results = new float[length];
                    Marshal.Copy(results2DPtr, results, 0, length);
                    for (int i = 0; i < handCount; i++)
                    {
                        for (int j = 0; j < 21; j++)
                        {
                            detector.m_HandPoses2D[i][(JointName)j] = new Vector2(results[i * 2 * 21 + j * 2], results[i * 2 * 21 + j * 2 + 1]);
                        }
                    }
                }

                if (results3DPtr != IntPtr.Zero)
                {
                    int length = 3 * 21 * handCount;
                    float[] results = new float[length];
                    Marshal.Copy(results3DPtr, results, 0, length);
                    for (int i = 0; i < handCount; i++)
                    {
                        for (int j = 0; j < 21; j++)
                        {
                            detector.m_HandPoses3D[i][(JointName)j] = new Vector3(results[i * 3 * 21 + j * 3], results[i * 3 * 21 + j * 3 + 1], results[i * 3 * 21 + j * 3 + 2]);
                        }
                    }
                }

                if (confidencesPtr != IntPtr.Zero)
                {
                    int length = 21 * handCount;
                    float[] confidences = new float[length];
                    Marshal.Copy(confidencesPtr, confidences, 0, length);
                    for (int i = 0; i < handCount; i++)
                    {
                        for (int j = 0; j < 21; j++)
                        {
                            detector.m_HandPosesConfidence[i][(JointName)j] = confidences[i * 21 + j];
                        }
                    }
                }
                
                detector.OnHandPoseUpdated?.Invoke();
            }
        }
    }
}
