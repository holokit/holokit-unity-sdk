// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS
using HoloKit.iOS;
#endif

public class LiDARChecker : MonoBehaviour
{
    public void Start()
    {
#if UNITY_IOS
        Debug.Log($"Support LiDAR {DeviceData.SupportLiDAR()}");
#else
        Debug.Log("LiDAR device data is available on iOS only");
#endif
    }
}
