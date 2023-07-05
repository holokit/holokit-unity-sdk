// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using System.Runtime.InteropServices;

namespace HoloInteractive.XR.HoloKit.iOS
{
    internal static class NativeApi
    {
        public static void CFRelease(ref IntPtr ptr)
        {
            CFRelease(ptr);
            ptr = IntPtr.Zero;
        }

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_NativeApi_CFRelease")]
        public static extern void CFRelease(IntPtr ptr);
    }
}
#endif
