// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class HoloKitARKitManager : MonoBehaviour
    {
        public static HoloKitARKitManager Instance { get { return s_Instance; } }

        private static HoloKitARKitManager s_Instance;

        public HoloKitARKitNativeProvider ARKitNativeProvider => m_ARKitNativeProvider;

        private HoloKitARKitNativeProvider m_ARKitNativeProvider;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(this);
                return;
            }
            else
            {
                s_Instance = this;
            }

            m_ARKitNativeProvider = new();
        }

        private void OnDestroy()
        {
            if (m_ARKitNativeProvider != null)
                m_ARKitNativeProvider.Dispose();
        }
    }
}
#endif
