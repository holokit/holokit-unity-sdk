// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace HoloKit.iOS
{
    [Serializable]
    public enum AppleThermalState
    {
        Normal = 0, // blue
        Fair = 1, // green
        Serious = 2, // yellow
        Critical = 3 // red
    }

    public class AppleNativeProvider : IDisposable
    {
        IntPtr m_Ptr;

        static Dictionary<IntPtr, AppleNativeProvider> s_Providers = new();

        public event Action<AppleThermalState> OnThermalStateChanged;

        public AppleNativeProvider()
        {
            m_Ptr = Init();
            RegisterCallbacks(m_Ptr, OnThermalStateChangedCallback);
            s_Providers[m_Ptr] = this;
        }

        public AppleThermalState GetThermalState()
        {
            return (AppleThermalState)GetThermalState_Native(m_Ptr);
        }

        public double GetSystemUptime()
        {
            return GetSystemUptime_Native(m_Ptr);
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

        [DllImport("__Internal", EntryPoint = "HoloKit_AppleNativeProvider_init")]
        static extern IntPtr Init();

        [DllImport("__Internal", EntryPoint = "HoloKit_AppleNativeProvider_registerCallbacks")]
        static extern void RegisterCallbacks(IntPtr self, Action<IntPtr, int> onThermalStateChangedCallback);

        [DllImport("__Internal", EntryPoint = "HoloKit_AppleNativeProvider_getThermalState")]
        static extern int GetThermalState_Native(IntPtr self);

        [DllImport("__Internal", EntryPoint = "HoloKit_AppleNativeProvider_getSystemUptime")]
        static extern double GetSystemUptime_Native(IntPtr self);

        [AOT.MonoPInvokeCallback(typeof(Action<IntPtr, int>))]
        static void OnThermalStateChangedCallback(IntPtr providerPtr, int thermalState)
        {
            if (s_Providers.TryGetValue(providerPtr, out AppleNativeProvider provider))
            {
                provider.OnThermalStateChanged?.Invoke((AppleThermalState)thermalState);
            }
        }
    }
}
#endif
