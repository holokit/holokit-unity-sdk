// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;

namespace HoloInteractive.XR.HoloKit.UI
{
    public class HoloKitDefaultUICanvas : MonoBehaviour
    {
        public Text ButtonText;

        private HoloKitCameraManager m_HoloKitCameraManager;

        private void Start()
        {
            // Lock screen orientation for convenience. This is not necessary.
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            m_HoloKitCameraManager = FindObjectOfType<HoloKitCameraManager>();
        }

        public void SwitchRenderMode()
        {
            m_HoloKitCameraManager.ScreenRenderMode = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
            ButtonText.text = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";
        }
    }
}
