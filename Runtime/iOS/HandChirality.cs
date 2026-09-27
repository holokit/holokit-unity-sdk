// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
namespace HoloKit.iOS
{
    /// <summary>Left or right hand, as Apple Vision sees it.</summary>
    public enum HandChirality
    {
        Left = -1,
        Unknown = 0,
        Right = 1,
    }
}
#endif
