// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Runtime.InteropServices;

namespace HoloKit.iOS
{
    public static class DeviceData
    {
        public static bool SupportLiDAR()
        {
#if UNITY_EDITOR
            return false;
#else
            return SupportLiDAR_Native();
#endif
        }

        [DllImport("__Internal", EntryPoint = "HoloKit_DeviceData_supportLiDAR")]
        static extern bool SupportLiDAR_Native();
    }
}
#endif
