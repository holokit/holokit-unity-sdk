// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class WorldOriginResetter : MonoBehaviour
    {
        public void ResetWorldOrigin(Vector3 position, Quaternion rotation)
        {
            HoloKitARKitManager.Instance.ARKitNativeProvider.ResetWorldOrigin(position, rotation);
        }
    }
}
#endif
