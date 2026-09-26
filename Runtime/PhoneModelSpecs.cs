// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace HoloKit
{
    /// <summary>
    /// The phone model specs needed for rendering parameter calculation.
    /// </summary>
    [Serializable]
    public struct PhoneModelSpecs
    {
        [Tooltip("Custom screen resolution value in pixels. If 0, the program will default to Unity's screen resolution.")]
        public Vector2 ScreenResolution;

        [Tooltip("Custom DPI value. If 0, the program will default to Unity's DPI.")]
        public float ScreenDpi;

        [Tooltip("The offset from the bottom of the phone screen to the bottom of the viewport.")]
        public float ViewportBottomOffset;

        [Tooltip("The 3D offset from the phone camera to the bottom center of the phone screen.")]
        public Vector3 CameraOffset;

        [Tooltip("The width of the non-display area on the bottom side of the phone screen. This value is deprecated.")]
        [Header("Deprecated")]
        public float ScreenBottomBorder;
    }
}
