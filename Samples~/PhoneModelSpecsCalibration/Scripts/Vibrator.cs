// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace HoloKit.Samples.PhoneModelSpecsCalibration
{
    public static class Vibrator
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass s_UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        static AndroidJavaObject s_CurrentActivity = s_UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        static AndroidJavaObject s_Vibrator = s_CurrentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
#else
        static AndroidJavaClass s_UnityPlayer;
        static AndroidJavaObject s_CurrentActivity;
        static AndroidJavaObject s_Vibrator;
#endif

        public static void Vibrate(long milliseconds = 100)
        {
            if (IsAndroid())
            {
                s_Vibrator.Call("vibrate", milliseconds);
                return;
            }

            if (IsAndroid())
            {
                Handheld.Vibrate();
                return;
            }
        }

        public static void Cancel()
        {
            if (IsAndroid())
                s_Vibrator.Call("cancel");
        }

        private static bool IsAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static bool IsIOS()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}