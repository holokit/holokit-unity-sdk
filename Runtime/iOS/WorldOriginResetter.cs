// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class WorldOriginResetter : MonoBehaviour
    {
        private void Start()
        {
            if (HoloKitARKitManager.Instance == null)
            {
                Debug.LogWarning("[ARBackgroundVideoEnhancementManager] Failed to find HoloKitARKitManager instance in the scene.");
                return;
            }
        }

        public void ResetWorldOrigin(Vector3 position, Quaternion rotation)
        {
            HoloKitARKitManager.Instance.ARKitNativeProvider.ResetWorldOrigin(position, rotation);
        }
    }
}
#endif
