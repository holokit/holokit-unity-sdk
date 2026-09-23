// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    public class ARBackgroundVideoEnhancementManager : MonoBehaviour
    {
        [SerializeField] bool m_IsEnabled;
        [SerializeField] bool m_DisableVideoEnhancementInStereoMode = true;
        bool m_RecordingOverride;
        bool m_Ready;
        bool? m_Applied;
        HoloKitCameraManager m_CameraManager;
        ARCameraManager m_ARCamera;

        public bool IsEnabled
        {
            get => m_IsEnabled;
            set { m_IsEnabled = value; Apply(); }
        }

        // Separate from the user's preference so stopping restores it exactly.
        // Stereo recordings also render the mono AR camera into the movie.
        public bool RecordingOverride
        {
            get => m_RecordingOverride;
            set { m_RecordingOverride = value; Apply(); }
        }

        public static bool ShouldEnhance(bool preferred, bool recording, ScreenRenderMode mode, bool disableInStereo)
            => recording || (mode == ScreenRenderMode.Stereo ? !disableInStereo : preferred);

        void Start()
        {
            m_CameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            if (m_CameraManager == null) return;
            m_ARCamera = m_CameraManager.GetComponentInChildren<ARCameraManager>();
            m_CameraManager.OnScreenRenderModeChanged += OnModeChanged;
            if (m_ARCamera != null) m_ARCamera.frameReceived += OnFrameReceived;
        }

        void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (m_Ready) return;
            m_Ready = true;
            Apply();
        }

        void OnModeChanged(ScreenRenderMode mode) => Apply();

        void Apply()
        {
            if (!m_Ready || m_CameraManager == null || HoloKitARKitManager.Instance == null) return;
            bool enabled = ShouldEnhance(m_IsEnabled, m_RecordingOverride,
                m_CameraManager.ScreenRenderMode, m_DisableVideoEnhancementInStereoMode);
            if (m_Applied == enabled) return;
            HoloKitARKitManager.Instance.ARKitNativeProvider.SetVideoEnhancement(enabled);
            m_Applied = enabled;
        }

        void OnDestroy()
        {
            if (m_CameraManager != null) m_CameraManager.OnScreenRenderModeChanged -= OnModeChanged;
            if (m_ARCamera != null) m_ARCamera.frameReceived -= OnFrameReceived;
            if (m_Applied == true && HoloKitARKitManager.Instance != null)
                HoloKitARKitManager.Instance.ARKitNativeProvider.SetVideoEnhancement(false);
        }
    }
}
#endif
