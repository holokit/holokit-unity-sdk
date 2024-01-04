// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class WorldOriginResetter : MonoBehaviour
    {
        private HoloKitARKitNativeProvider m_ARKitNativeProvider;

        private void Start()
        {
            m_ARKitNativeProvider = new();
        }

        private void OnDestroy()
        {
            m_ARKitNativeProvider.Dispose();
        }

        public void ResetWorldOrigin(Vector3 position, Quaternion rotation)
        {
            // The coordinate system can only rotate around the y axis
            Quaternion finalRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            m_ARKitNativeProvider.ResetOrigin(position, finalRotation);
        }
    }
}
#endif
