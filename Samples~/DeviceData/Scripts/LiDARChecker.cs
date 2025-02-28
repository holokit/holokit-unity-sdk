// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloKit.iOS;

public class LiDARChecker : MonoBehaviour
{
    public void Start()
    {
        Debug.Log($"Support LiDAR {DeviceData.SupportLiDAR()}");
    }
}
