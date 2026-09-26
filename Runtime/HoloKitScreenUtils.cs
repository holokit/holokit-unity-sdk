// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace HoloKit
{
    /// <summary>
    /// Screen size helpers for the landscape stereo layout.
    /// </summary>
    public static class HoloKitScreenUtils
    {
        public const float InchToMeter = 0.0254f;

        public const float MeterToInch = 39.3701f;

        /// <summary>The screen's long side in pixels.</summary>
        public static float GetScreenWidth() => Mathf.Max(Screen.width, Screen.height);

        /// <summary>The screen's short side in pixels.</summary>
        public static float GetScreenHeight() => Mathf.Min(Screen.width, Screen.height);
    }
}
