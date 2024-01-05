// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using HoloInteractive.XR.HoloKit.iOS;
#endif

namespace HoloInteractive.XR.HoloKit.UI
{
    public class HoloKitDefaultUICanvas : MonoBehaviour
    {
        public Text RenderModeButtonText;

        private HoloKitCameraManager m_HoloKitCameraManager;

#if UNITY_IOS
        public Text RecordButtonText;

        private HoloKitVideoRecorder m_VideoRecorder;
#endif

        private void Start()
        {
            m_HoloKitCameraManager = FindObjectOfType<HoloKitCameraManager>();
#if UNITY_IOS
            m_VideoRecorder = m_HoloKitCameraManager.GetComponentInChildren<HoloKitVideoRecorder>();
#endif
        }

        public void SwitchRenderMode()
        {
            m_HoloKitCameraManager.ScreenRenderMode = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
            RenderModeButtonText.text = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";

#if UNITY_IOS
            RecordButtonText.text = m_VideoRecorder.IsRecording ? "Stop Recording" : "Start Recording";
#endif
        }

#if UNITY_IOS
        public void ToggleRecording()
        {
            m_VideoRecorder.ToggleRecording();
            RecordButtonText.text = m_VideoRecorder.IsRecording ? "Stop Recording" : "Start Recording";
        }
#endif
    }
}
