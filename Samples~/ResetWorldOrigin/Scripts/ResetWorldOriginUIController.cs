// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using HoloKit.iOS;

namespace HoloKit.Samples.ResetWorldOrigin
{
    public class ResetWorldOriginUIController : MonoBehaviour
    {
        [SerializeField] private WorldOriginResetter m_WorldOriginResetter;

        // Reset world origin to the current center eye pose
        public void ResetWorldOriginToCurrentLocation()
        {
            var holoKitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            var centerEyePose = holoKitCameraManager.CenterEyePose;
            m_WorldOriginResetter.ResetWorldOrigin(centerEyePose.position, centerEyePose.rotation);
        }
    }
}
