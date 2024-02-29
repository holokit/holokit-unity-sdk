// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using HoloKit.iOS;

namespace HoloKit.Samples.ARBackgroundVideoEnhancement
{
    public class ARBackgroundVideoEnhancementUIController : MonoBehaviour
    {
        [SerializeField] private GameObject m_ToggleVideoEnhancementButton;

        [SerializeField] private Text m_ToggleVideoEnhancementButtonText;

        [SerializeField] private ARBackgroundVideoEnhancementManager m_VideoEnhancementManager;

        private void Start()
        {
            FindFirstObjectByType<HoloKitCameraManager>().OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            UpdateVideoEnhancementButtonText();
        }

        private void OnScreenRenderModeChanged(ScreenRenderMode renderMode)
        {
            if (renderMode == ScreenRenderMode.Mono)
            {
                m_ToggleVideoEnhancementButton.SetActive(true);
                UpdateVideoEnhancementButtonText();
            }
            else
            {
                m_ToggleVideoEnhancementButton.SetActive(false);
                UpdateVideoEnhancementButtonText();
            }
        }

        public void ToggleVideoEnhancement()
        {
            m_VideoEnhancementManager.IsEnabled = !m_VideoEnhancementManager.IsEnabled;
            UpdateVideoEnhancementButtonText();
        }

        private void UpdateVideoEnhancementButtonText()
        {
            if (m_VideoEnhancementManager.IsEnabled)
                m_ToggleVideoEnhancementButtonText.text = "Disable Video Enhancement";
            else
                m_ToggleVideoEnhancementButtonText.text = "Enable Video Enhancement";
        }
    }
}
