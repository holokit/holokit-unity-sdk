// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    // Enable ARKit's 4K HDR background video.
    public class ARBackgroundVideoEnhancementManager : MonoBehaviour
    {
        public bool IsEnabled
        {
            get => m_IsEnabled;
            set
            {
                if (value != m_IsEnabled)
                {
                    HoloKitARKitManager.Instance.ARKitNativeProvider.SetVideoEnhancement(value);
                    m_IsEnabled = value;
                }
            }
        }

        [SerializeField] private bool m_IsEnabled;

        [Tooltip("Whether to disable video enhancement in stereo mode to save energy.")]
        [SerializeField] private bool m_DisableVideoEnhancementInStereoMode = true;

        private bool m_FirstFrame = true;

        private void Start()
        {
            if (HoloKitARKitManager.Instance == null)
            {
                Debug.LogWarning("[ARBackgroundVideoEnhancementManager] Failed to find HoloKitARKitManager instance in the scene.");
                return;
            }

            var holokitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            holokitCameraManager.OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            holokitCameraManager.GetComponentInChildren<ARCameraManager>().frameReceived += OnFrameReceived;
        }

        private void OnFrameReceived(ARCameraFrameEventArgs obj)
        {
            if (m_FirstFrame)
            {
                m_FirstFrame = false;
                HoloKitARKitManager.Instance.ARKitNativeProvider.SetVideoEnhancement(m_IsEnabled);
            }
        }

        private void OnScreenRenderModeChanged(ScreenRenderMode renderMode)
        {
            if (renderMode == ScreenRenderMode.Mono)
            {
                HoloKitARKitManager.Instance.ARKitNativeProvider.SetVideoEnhancement(m_IsEnabled);
            }
            else
            {
                HoloKitARKitManager.Instance.ARKitNativeProvider.SetVideoEnhancement(!m_DisableVideoEnhancementInStereoMode);
            }
        }
    }
}
#endif