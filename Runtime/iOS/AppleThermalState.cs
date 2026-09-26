// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System;

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
}
#endif
