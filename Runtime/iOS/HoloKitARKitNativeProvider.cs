// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class HoloKitARKitNativeProvider: IDisposable
    {
        IntPtr m_Ptr;

        private bool m_ARSessionDelegateIntercepted;

        static Dictionary<IntPtr, HoloKitARKitNativeProvider> s_Providers = new();

        public HoloKitARKitNativeProvider()
        {
            m_Ptr = Init_Native();
            s_Providers[m_Ptr] = this;

            RegisterCallbacks();
            var xrSessionSubsystem = GetLoadedXRSessionSubsystem();
            if (xrSessionSubsystem != null)
            {
                SetARSessionPtr_Native(m_Ptr, xrSessionSubsystem.nativePtr);
            }
        }

        public void Dispose()
        {
            if (m_Ptr != IntPtr.Zero)
            {
                if (m_ARSessionDelegateIntercepted)
                    RestoreUnityARSessionDelegate();

                s_Providers.Remove(m_Ptr);
                NativeApi.CFRelease(ref m_Ptr);
                m_Ptr = IntPtr.Zero;
            }
        }

        private void RegisterCallbacks()
        {
            RegisterCallbacks_Native(m_Ptr, OnARSessionUpdatedFrameDelegate);
        }

        public void InterceptUnityARSessionDelegate()
        {
            var xrSessionSubsystem = GetLoadedXRSessionSubsystem();
            if (xrSessionSubsystem != null)
            {
                InterceptUnityARSessionDelegate_Native(m_Ptr, xrSessionSubsystem.nativePtr);
                m_ARSessionDelegateIntercepted = true;
            }
        }

        public void RestoreUnityARSessionDelegate()
        {
            var xrSessionSubsystem = GetLoadedXRSessionSubsystem();
            if (xrSessionSubsystem != null)
            {
                RestoreUnityARSessionDelegate_Native(m_Ptr, xrSessionSubsystem.nativePtr);
                m_ARSessionDelegateIntercepted = false;
            }
        }

        public void ResetWorldOrigin(Vector3 position, Quaternion rotation)
        {
            // The coordinate system can only rotate around the y axis
            rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

            float[] p = { position.x, position.y, position.z };
            float[] r = { rotation.x, rotation.y, rotation.z, rotation.w };
            ResetOrigin_Native(m_Ptr, p, r);
        }

        public void SetVideoEnhancement(bool enabled)
        {
            SetVideoEnhancement_Native(m_Ptr, enabled);
        }

        private static XRSessionSubsystem GetLoadedXRSessionSubsystem()
        {
            List<XRSessionSubsystem> xrSessionSubsystems = new();
            SubsystemManager.GetSubsystems(xrSessionSubsystems);
            foreach (var subsystem in xrSessionSubsystems)
            {
                return subsystem;
            }
            Debug.Log("[ARKitNativeProvider] Failed to get loaded xr session subsystem");
            return null;
        }

        public event Action<double, Matrix4x4> OnARSessionUpdatedFrame;

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_init")]
        private static extern IntPtr Init_Native();

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_setARSessionPtr")]
        private static extern IntPtr SetARSessionPtr_Native(IntPtr self, IntPtr sessionPtr);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_registerCallbacks")]
        private static extern void RegisterCallbacks_Native(IntPtr self, Action<IntPtr, double, IntPtr> onARSessionUpdatedFrame);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_interceptUnityARSessionDelegate")]
        private static extern void InterceptUnityARSessionDelegate_Native(IntPtr self, IntPtr sessionPtr);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_restoreUnityARSessionDelegate")]
        private static extern void RestoreUnityARSessionDelegate_Native(IntPtr self, IntPtr sessionPtr);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_resetOrigin")]
        private static extern void ResetOrigin_Native(IntPtr self, float[] position, float[] rotation);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_setVideoEnhancement")]
        private static extern IntPtr SetVideoEnhancement_Native(IntPtr self, bool enabled);

        [AOT.MonoPInvokeCallback(typeof(Action<IntPtr, double, IntPtr>))]
        private static void OnARSessionUpdatedFrameDelegate(IntPtr providerPtr, double timestamp, IntPtr matrixPtr)
        {
            if (s_Providers.TryGetValue(providerPtr, out HoloKitARKitNativeProvider provider))
            {
                if (provider.OnARSessionUpdatedFrame == null)
                    return;

                float[] matrixData = new float[16];
                Marshal.Copy(matrixPtr, matrixData, 0, 16);
                Matrix4x4 matrix = new();
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        matrix[i, j] = matrixData[(4 * i) + j];
                    }
                }
                provider.OnARSessionUpdatedFrame?.Invoke(timestamp, matrix);
            }
        }
    }
}
#endif