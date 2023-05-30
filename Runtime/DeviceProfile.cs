using System;
using UnityEngine;

namespace HoloInteractive.XR.HoloKit
{
    public enum HoloKitGeneration
    {
        HoloKitX = 0
    }

    [Serializable]
    public struct HoloKitModelSpecs
    {
        // Distance beetween eyes
        public float OpticalAxisDistance;

        // 3D offset from the center of bottomline of the holokit phone display to the center of two eyes.
        public Vector3 MrOffset;

        // Eye view area width
        public float ViewportInner;

        // Eye view area height
        public float ViewportOuter;

        // Eye view area spillter width
        public float ViewportTop;

        // Eye view area spillter width
        public float ViewportBottom;

        // Fresnel lens focal length
        public float FocalLength;

        // Screen To Fresnel distance
        public float ScreenToLens;

        // Fresnel To eye distance
        public float LensToEye;

        // Bottom of the holder to center of the view
        public float AxisToBottom;

        // The distance between the center of the HME and the marker
        public float AlignmentMarkerOffset;
    }

    [Serializable]
    public struct PhoneModelSpecs
    {
        //public float ScreenDpi;

        // The 3D offset vector from center of the camera to the center of the display area's bottomline
        public Vector3 CameraOffset;

        // The distance from the bottom of display area to the touching surface of the holokit phone holder
        public float ScreenBottom;
    }

    [Serializable]
    public struct PhoneModel
    {
        public string ModelName;

        public string Description;

        public PhoneModelSpecs ModelSpecs;
    }

    public static class DeviceProfile
    {
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
                        FocalLength = 0.065f,
                        ScreenToLens = 0.02715f + 0.03136f + 0.002f,
                        LensToEye = 0.02497f + 0.03898f,
                        AxisToBottom = 0.02990f,
                        AlignmentMarkerOffset = 0.05075f
                    };
            }
        }

        // We use iPhone 13 Pro's data as the default data
        public static PhoneModelSpecs GetDefaultPhoneModelSpecs()
        {
            return new PhoneModelSpecs
            {
                CameraOffset = new(0.042005f, -0.05809f, -0.00727f),
                ScreenBottom = 0.00347f
            };
        }
    }
}
