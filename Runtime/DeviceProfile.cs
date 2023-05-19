using UnityEngine;
using UnityEngine.iOS;

namespace HoloInteractive.XR.HoloKit
{
    public enum HoloKitGeneration
    {
        HoloKitX = 0
    }

    public struct HoloKitModel
    {
        // Distance beetween eyes
        public float OpticalAxisDistance;

        // 3D offset from the center of bottomline of the holokit phone display to the center of two eyes.
        // Left-handedness
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

        // Bottom of the holder to bottom of the view
        public float AxisToBottom;

        // The distance between the center of the HME and the marker
        public float HorizontalAlignmentMarkerOffset;
    }

    public struct PhoneModel
    {
        // The long screen edge of the phone
        public float ScreenWidth;

        // The short screen edge of the phone
        public float ScreenHeight;

        // The distance from the bottom of display area to the touching surface of the holokit phone holder
        public float ScreenBottom;

        // The 3D offset vector from center of the camera to the center of the display area's bottomline
        // Left-handedness
        public Vector3 CameraOffset;

        public float ScreenDpi;
    }

    public static class DeviceProfile
    {
        public static HoloKitModel GetHoloKitModel(HoloKitGeneration generation)
        {
            switch (generation)
            {
                default:
                    return new HoloKitModel
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
                        HorizontalAlignmentMarkerOffset = 0.05075f
                    };
            }
        }

        public static bool DoesDeviceSupportStereoMode()
        {
            if (Utils.IsEditor)
                return true;

            DeviceGeneration deviceGeneration = Device.generation;
            switch (deviceGeneration)
            {
                case DeviceGeneration.iPhoneXS:
                case DeviceGeneration.iPhoneXSMax:
                case DeviceGeneration.iPhone11Pro:
                case DeviceGeneration.iPhone11ProMax:
                case DeviceGeneration.iPhone12:
                case DeviceGeneration.iPhone12Pro:
                case DeviceGeneration.iPhone12ProMax:
                case DeviceGeneration.iPhone13:
                case DeviceGeneration.iPhone13Pro:
                case DeviceGeneration.iPhone13ProMax:
                case DeviceGeneration.iPhone14:
                case DeviceGeneration.iPhone14Plus:
                case DeviceGeneration.iPhone14Pro:
                case DeviceGeneration.iPhone14ProMax:
                    return true;
                default:
                    return false;

            }
        }

        public static PhoneModel GetPhoneModel(DeviceGeneration deviceGeneration)
        {
            switch (deviceGeneration)
            {
                case DeviceGeneration.iPhoneXS:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.135097f,
                        ScreenHeight = 0.062391f,
                        ScreenBottom = 0.00391f,
                        CameraOffset = new Vector3(0.05986f, -0.055215f, -0.0091f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhoneXSMax:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.14971f,
                        ScreenHeight = 0.06961f,
                        ScreenBottom = 0.00391f,
                        CameraOffset = new Vector3(0.06694f, -0.09405f, -0.00591f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhone11Pro:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13495f,
                        ScreenHeight = 0.06233f,
                        ScreenBottom = 0.00452f,
                        CameraOffset = new Vector3(0.059955f, -0.05932f, -0.00591f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhone11ProMax:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.14891f,
                        ScreenHeight = 0.06881f,
                        ScreenBottom = 0.00452f,
                        CameraOffset = new Vector3(0.066935f, -0.0658f, -0.00591f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhone12:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13977f,
                        ScreenHeight = 0.06458f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.060625f, -0.05879f, -0.00633f),
                        ScreenDpi = 460f
                    };
                case DeviceGeneration.iPhone12Pro:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13977f,
                        ScreenHeight = 0.06458f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.061195f, -0.05936f, -0.00551f),
                        ScreenDpi = 460f
                    };
                case DeviceGeneration.iPhone12ProMax:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.15390f,
                        ScreenHeight = 0.07113f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.04952f, -0.06464f, -0.00591f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhone13:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13977f,
                        ScreenHeight = 0.06458f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.06147f, -0.05964f, -0.00781f),
                        ScreenDpi = 460f
                    };
                case DeviceGeneration.iPhone13Pro:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13977f,
                        ScreenHeight = 0.06458f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.042005f, -0.05809f, -0.00727f),
                        ScreenDpi = 460f
                    };
                case DeviceGeneration.iPhone13ProMax:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.15390f,
                        ScreenHeight = 0.07113f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.04907f, -0.06464f, -0.00727f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhone14:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13977f,
                        ScreenHeight = 0.06458f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.061475f, -0.05964f, -0.00848f),
                        ScreenDpi = 460f
                    };
                case DeviceGeneration.iPhone14Plus:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.15390f,
                        ScreenHeight = 0.07113f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.06787f, -0.06552f, -0.00851f),
                        ScreenDpi = 458f
                    };
                case DeviceGeneration.iPhone14Pro:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.14109f,
                        ScreenHeight = 0.06508f,
                        ScreenBottom = 0.003185f,
                        CameraOffset = new Vector3(0.04021f, -0.05717f, -0.00784f),
                        ScreenDpi = 460f
                    };
                case DeviceGeneration.iPhone14ProMax:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.15434f,
                        ScreenHeight = 0.07121f,
                        ScreenBottom = 0.003185f,
                        CameraOffset = new Vector3(0.046835f, -0.0633f, -0.0078f),
                        ScreenDpi = 460f
                    };
                default:
                    return new PhoneModel
                    {
                        ScreenWidth = 0.13977f,
                        ScreenHeight = 0.06458f,
                        ScreenBottom = 0.00347f,
                        CameraOffset = new Vector3(0.042005f, -0.05809f, -0.00727f),
                        ScreenDpi = 460f
                    };
            }
        }
    }
}
