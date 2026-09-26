// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;

namespace HoloKit
{
    /// <summary>
    /// The list of supported screen orientations under Mono mode.
    /// </summary>
    [Flags]
    public enum MonoScreenOrientation
    {
        Portrait = 1, // 000001
        PortraitUpsideDown = 2, // 000010
        LandscapeRight = 4, // 000100
        LandscapeLeft = 8 // 001000
    }
}
