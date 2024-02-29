using UnityEngine;

namespace HoloKit
{
    public static class Utils
    {
        public const float INCH_TO_METER_RATIO = 0.0254f;

        public const float METER_TO_INCH_RATIO = 39.3701f;

        public static float GetScreenWidth()
        {
            return Screen.width > Screen.height ? Screen.width : Screen.height;
        }

        public static float GetScreenHeight()
        {
            return Screen.width > Screen.height ? Screen.height : Screen.width;
        }
    }
}
