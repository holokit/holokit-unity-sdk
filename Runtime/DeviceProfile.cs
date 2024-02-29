// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace HoloKit
{
    /// <summary>
    /// You can add new HoloKit model here.
    /// </summary>
    public enum HoloKitGeneration
    {
        HoloKitX = 0
    }

    /// <summary>
    /// The HoloKit model specs needed for rendering parameter calculation.
    /// </summary>
    [Serializable]
    public struct HoloKitModelSpecs
    {
        // Distance beetween the eyes
        public float OpticalAxisDistance;

        // 3D offset from the center of bottomline of the holokit phone display to the center of the two eyes
        public Vector3 MrOffset;

        // Eye view area width
        public float ViewportInner;

        // Eye view area height
        public float ViewportOuter;

        // Eye view area spillter width
        public float ViewportTop;

        // Eye view area spillter width
        public float ViewportBottom;

        // Fresnel To eye distance
        public float LensToEye;

        // Bottom of the holder to center of the view
        public float AxisToBottom;

        // The distance between the center of the HME and the marker
        public float AlignmentMarkerOffset;
    }

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

    [Serializable]
    public struct PhoneModel
    {
        [Tooltip("Apple's identifier for each device.")]
        public string ModelName;

        [Tooltip("Provides clarity for internal developers, supplementing the less descriptive Model Name")]
        public string Description;

        public PhoneModelSpecs ModelSpecs;
    }

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
