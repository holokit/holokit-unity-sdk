// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using HoloInteractive.XR.HoloKit.iOS;

namespace HoloInteractive.XR.HoloKit.Samples.ResetWorldOrigin
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
