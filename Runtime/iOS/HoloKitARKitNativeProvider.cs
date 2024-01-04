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

        static Dictionary<IntPtr, HoloKitARKitNativeProvider> s_Providers = new();

        public HoloKitARKitNativeProvider()
        {
            m_Ptr = Init_Native();
            s_Providers[m_Ptr] = this;

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
                s_Providers.Remove(m_Ptr);
                NativeApi.CFRelease(ref m_Ptr);
                m_Ptr = IntPtr.Zero;
            }
        }

        public void ResetOrigin(Vector3 position, Quaternion rotation)
        {
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

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_init")]
        private static extern IntPtr Init_Native();

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_setARSessionPtr")]
        private static extern IntPtr SetARSessionPtr_Native(IntPtr self, IntPtr sessionPtr);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_resetOrigin")]
        private static extern void ResetOrigin_Native(IntPtr self, float[] position, float[] rotation);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_HoloKitARKitNativeProvider_setVideoEnhancement")]
        private static extern IntPtr SetVideoEnhancement_Native(IntPtr self, bool enabled);
    }
}
#endif