using UnityEngine;

namespace HoloInteractive.XR.HoloKit
{
    public static class Utils
    {
        public static bool IsEditor => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.WindowsPlayer;

        public static bool IsRuntime => Application.platform == RuntimePlatform.IPhonePlayer;
    }
}
