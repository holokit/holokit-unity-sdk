// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using System.Runtime.InteropServices;

namespace HoloKit.iOS
{
    public static class DeviceData
    {
        /// <summary>Whether the device has a LiDAR scanner.</summary>
        public static bool SupportsLiDAR()
        {
#if UNITY_EDITOR
            return false;
#else
            return SupportLiDAR_Native();
#endif
        }

        [Obsolete("Use SupportsLiDAR.")]
        public static bool SupportLiDAR() => SupportsLiDAR();

        [DllImport("__Internal", EntryPoint = "HoloKit_DeviceData_supportLiDAR")]
        static extern bool SupportLiDAR_Native();
    }
}
#endif
