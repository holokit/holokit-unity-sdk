// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;

namespace HoloKit.iOS
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
