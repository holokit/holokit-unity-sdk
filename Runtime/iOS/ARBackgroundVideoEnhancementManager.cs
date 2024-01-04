// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloInteractive.XR.HoloKit.iOS
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
                    m_ARKitNativeProvider.SetVideoEnhancement(value);
                    m_IsEnabled = value;
                }
            }
        }

        [SerializeField] private bool m_IsEnabled;

        [Tooltip("Whether to disable video enhancement in stereo mode to save energy.")]
        [SerializeField] private bool m_DisableVideoEnhancementInStereoMode = true;

        private HoloKitARKitNativeProvider m_ARKitNativeProvider;

        private bool m_FirstFrame = true;

        private void Start()
        {
            m_ARKitNativeProvider = new();

            var holokitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            holokitCameraManager.OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            holokitCameraManager.GetComponent<ARCameraManager>().frameReceived += OnFrameReceived;
        }

        private void OnDestroy()
        {
            m_ARKitNativeProvider.Dispose();
        }

        private void OnFrameReceived(ARCameraFrameEventArgs obj)
        {
            if (m_FirstFrame)
            {
                m_FirstFrame = false;
                m_ARKitNativeProvider.SetVideoEnhancement(m_IsEnabled);
            }
        }

        private void OnScreenRenderModeChanged(ScreenRenderMode renderMode)
        {
            if (renderMode == ScreenRenderMode.Mono)
            {
                m_ARKitNativeProvider.SetVideoEnhancement(m_IsEnabled);
            }
            else
            {
                m_ARKitNativeProvider.SetVideoEnhancement(!m_DisableVideoEnhancementInStereoMode);
            }
        }
    }
}
#endif