// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Runtime.InteropServices;

namespace HoloInteractive.XR.HoloKit.iOS
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

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_DeviceData_supportLiDAR")]
        static extern bool SupportLiDAR_Native();
    }
}
#endif
