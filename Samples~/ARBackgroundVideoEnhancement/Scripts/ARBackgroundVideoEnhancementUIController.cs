// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using HoloKit.iOS;
#endif

namespace HoloKit.Samples.ARBackgroundVideoEnhancement
{
    public class ARBackgroundVideoEnhancementUIController : MonoBehaviour
    {
        [SerializeField] private GameObject m_ToggleVideoEnhancementButton;

        [SerializeField] private Text m_ToggleVideoEnhancementButtonText;

#if UNITY_IOS
        [SerializeField] private ARBackgroundVideoEnhancementManager m_VideoEnhancementManager;
#else
        [SerializeField] private MonoBehaviour m_VideoEnhancementManager;
#endif

        private void Start()
        {
#if UNITY_IOS
            FindFirstObjectByType<HoloKitCameraManager>().OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            UpdateVideoEnhancementButtonText();
#else
            m_ToggleVideoEnhancementButton.SetActive(false);
            m_ToggleVideoEnhancementButtonText.text = "Video Enhancement is available on iOS only";
#endif
        }

#if UNITY_IOS
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
#endif

        public void ToggleVideoEnhancement()
        {
#if UNITY_IOS
            m_VideoEnhancementManager.IsEnabled = !m_VideoEnhancementManager.IsEnabled;
            UpdateVideoEnhancementButtonText();
#else
            Debug.LogWarning("[HoloKit] AR background video enhancement is only available on iOS.");
#endif
        }

#if UNITY_IOS
        private void UpdateVideoEnhancementButtonText()
        {
            if (m_VideoEnhancementManager.IsEnabled)
                m_ToggleVideoEnhancementButtonText.text = "Disable Video Enhancement";
            else
                m_ToggleVideoEnhancementButtonText.text = "Enable Video Enhancement";
        }
#endif
    }
}
