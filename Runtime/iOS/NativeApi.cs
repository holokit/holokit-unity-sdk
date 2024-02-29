// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;
using System.Runtime.InteropServices;

namespace HoloKit.iOS
{
    internal static class NativeApi
    {
        public static void CFRelease(ref IntPtr ptr)
        {
            CFRelease(ptr);
            ptr = IntPtr.Zero;
        }

        [DllImport("__Internal", EntryPoint = "HoloKit_NativeApi_CFRelease")]
        public static extern void CFRelease(IntPtr ptr);
    }
}
#endif
