// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System;

namespace HoloKit
{
    [Obsolete("Use HoloKitScreenUtils.")]
    public static class Utils
    {
        public const float INCH_TO_METER_RATIO = HoloKitScreenUtils.InchToMeter;

        public const float METER_TO_INCH_RATIO = HoloKitScreenUtils.MeterToInch;

        public static float GetScreenWidth() => HoloKitScreenUtils.GetScreenWidth();

        public static float GetScreenHeight() => HoloKitScreenUtils.GetScreenHeight();
    }
}
