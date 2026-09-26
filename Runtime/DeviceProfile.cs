// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace HoloKit
{
    public static class DeviceProfile
    {
        /// <summary>
        /// Get the hardware specs necessary for rendering parameter calculation for the given HoloKit model.
        /// </summary>
        /// <param name="generation">The HoloKit model generation</param>
        /// <returns>The hardware specs of the given HoloKit model</returns>
        public static HoloKitModelSpecs GetHoloKitModelSpecs(HoloKitGeneration generation)
        {
            switch (generation)
            {
                default:
                    return new HoloKitModelSpecs
                    {
                        OpticalAxisDistance = 0.064f,
                        MrOffset = new Vector3(0f, -0.02894f, -0.07055f),
                        ViewportInner = 0.0292f,
                        ViewportOuter = 0.0292f,
                        ViewportTop = 0.02386f,
                        ViewportBottom = 0.02386f,
                        //FocalLength = 0.065f,
                        //ScreenToLens = 0.02715f + 0.03136f + 0.002f,
                        LensToEye = 0.02497f + 0.03898f,
                        AxisToBottom = 0.02990f,
                        AlignmentMarkerOffset = 0.05075f
                    };
            }
        }
        
        // We use iPhone 13 Pro's specs as the default specs for testing purpose.
        public static PhoneModel GetDefaultPhoneModel()
        {
            return new PhoneModel
            {
                ModelName = "iPhone14,2",
                Description = "iPhone 13 Pro",
                ModelSpecs = new PhoneModelSpecs
                {
                    CameraOffset = new(0.042005f, -0.05809f, -0.00727f),
                    ScreenBottomBorder = 0.00347f
                }
            };
        }
    }
}
