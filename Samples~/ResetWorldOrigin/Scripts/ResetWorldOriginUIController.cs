// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
#if UNITY_IOS
using HoloKit.iOS;
#endif

namespace HoloKit.Samples.ResetWorldOrigin
{
    public class ResetWorldOriginUIController : MonoBehaviour
    {
#if UNITY_IOS
        [SerializeField] private WorldOriginResetter m_WorldOriginResetter;
#else
        [SerializeField] private MonoBehaviour m_WorldOriginResetter;
#endif

        // Reset world origin to the current center eye pose
        public void ResetWorldOriginToCurrentLocation()
        {
#if UNITY_IOS
            var holoKitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            var centerEyePose = holoKitCameraManager.CenterEyePose;
            m_WorldOriginResetter.ResetWorldOrigin(centerEyePose.position, centerEyePose.rotation);
#else
            Debug.LogWarning("[HoloKit] Resetting the ARKit world origin is only available on iOS.");
#endif
        }
    }
}
