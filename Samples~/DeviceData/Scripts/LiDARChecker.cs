// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace HoloKit.Samples.DeviceData
{
    public class LiDARChecker : MonoBehaviour
    {
        void Start()
        {
#if UNITY_IOS
            Debug.Log($"Supports LiDAR: {HoloKit.iOS.DeviceData.SupportsLiDAR()}");
#endif
        }
    }
}
