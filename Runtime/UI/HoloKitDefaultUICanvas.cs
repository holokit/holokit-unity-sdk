// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using HoloKit.iOS;
#endif

namespace HoloKit.UI
{
    public class HoloKitDefaultUICanvas : MonoBehaviour
    {
        public Text RenderModeButtonText;

        private HoloKitCameraManager m_HoloKitCameraManager;

#if UNITY_IOS
        public Text RecordButtonText;
        public Button RecordIn4KButton;
        private Text m_RecordIn4KButtonText;
        private Button m_RecordButton;

        private HoloKitVideoRecorder m_VideoRecorder;
#endif

        private void Start()
        {
            m_HoloKitCameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            if (m_HoloKitCameraManager == null)
            {
                Debug.LogWarning($"[{nameof(HoloKitDefaultUICanvas)}] No HoloKitCameraManager in the scene; this canvas is inert.");
                return;
            }
#if UNITY_IOS
            m_VideoRecorder = m_HoloKitCameraManager.GetComponentInChildren<HoloKitVideoRecorder>();
            if (RecordButtonText != null)
                m_RecordButton = RecordButtonText.GetComponentInParent<Button>();
            if (RecordIn4KButton != null)
                m_RecordIn4KButtonText = RecordIn4KButton.GetComponentInChildren<Text>();
            UpdateRecordingQualityButton();
            OnRectTransformDimensionsChange();
#endif
        }

        public void SwitchRenderMode()
        {
            if (m_HoloKitCameraManager == null)
                return;

            m_HoloKitCameraManager.ScreenRenderMode = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;

            if (RenderModeButtonText != null)
                RenderModeButtonText.text = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";

#if UNITY_IOS
            // RecordButtonText is optional: a project may drive the record UI itself
            // rather than through this default canvas.
            UpdateRecordButtonText();
#endif
        }

#if UNITY_IOS
        private void Update()
        {
            UpdateRecordingQualityButton();
            UpdateRecordButtonText();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (RecordIn4KButton == null) return;
            // The compact pair fits to the left of the Dynamic Island on iPhone.
            ((RectTransform)RecordIn4KButton.transform).anchoredPosition =
                ((RectTransform)transform).rect.width < 700f
                    ? new(80f, -184f) : new(252f, -40f);
        }

        public void ToggleRecording4K()
        {
            if (m_VideoRecorder == null || m_VideoRecorder.IsRecording || m_VideoRecorder.IsFinishing || m_VideoRecorder.IsPreparing)
                return;
            m_VideoRecorder.RecordIn4K = !m_VideoRecorder.RecordIn4K;
            UpdateRecordingQualityButton();
        }

        private void UpdateRecordingQualityButton()
        {
            if (RecordIn4KButton == null) return;
            RecordIn4KButton.interactable = m_VideoRecorder != null &&
                !m_VideoRecorder.IsRecording && !m_VideoRecorder.IsFinishing && !m_VideoRecorder.IsPreparing;
            if (m_RecordIn4KButtonText != null)
                m_RecordIn4KButtonText.text = m_VideoRecorder != null && m_VideoRecorder.RecordIn4K ? "4K" : "2K";
        }

        public void ToggleRecording()
        {
            if (m_VideoRecorder == null)
                return;

            m_VideoRecorder.ToggleRecording();
            UpdateRecordButtonText();
        }

        private void UpdateRecordButtonText()
        {
            if (RecordButtonText == null || m_VideoRecorder == null)
                return;

            bool finishing = m_VideoRecorder.IsFinishing;
            RecordButtonText.text = finishing ? "Saving…" : m_VideoRecorder.IsRecording ? "■ STOP" : "● REC";
            RecordButtonText.color = finishing ? Color.gray : Color.red;
            if (m_RecordButton != null) m_RecordButton.interactable = !finishing;
        }
#endif
    }
}
