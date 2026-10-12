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
            m_HoloKitCameraManager.OnScreenRenderModeChanged += OnScreenRenderModeChanged;
            OnScreenRenderModeChanged(m_HoloKitCameraManager.ScreenRenderMode);
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
        }

        private void OnDestroy()
        {
            if (m_HoloKitCameraManager != null)
                m_HoloKitCameraManager.OnScreenRenderModeChanged -= OnScreenRenderModeChanged;
        }

        // The stereo bar (MONO, RECORD, OPTIONS) replaces these buttons in Stereo, while it has
        // controls. Checked every frame: StereoBarItems can change at any time.
        private void UpdateVisibility()
        {
            if (m_HoloKitCameraManager == null || !TryGetComponent<Canvas>(out var canvas))
                return;
            var bar = m_HoloKitCameraManager.StereoBar;
            bool barShown = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Stereo &&
                bar != null && bar.isActiveAndEnabled && bar.HasControls;
            if (canvas.enabled == barShown)
                canvas.enabled = !barShown;
        }

        private void LateUpdate() => UpdateVisibility();

        private void OnScreenRenderModeChanged(ScreenRenderMode mode)
        {
            UpdateVisibility();

            if (RenderModeButtonText != null)
                RenderModeButtonText.text = mode == ScreenRenderMode.Mono ? "Stereo" : "Mono";

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
            // The ARKit camera feed: HD or 4K (Mono movies are screen-sized either way).
            m_VideoRecorder.CaptureCameraIn4K = !m_VideoRecorder.CaptureCameraIn4K;
            UpdateRecordingQualityButton();
        }

        private void UpdateRecordingQualityButton()
        {
            if (RecordIn4KButton == null) return;
            RecordIn4KButton.interactable = m_VideoRecorder != null &&
                !m_VideoRecorder.IsRecording && !m_VideoRecorder.IsFinishing && !m_VideoRecorder.IsPreparing;
            if (m_RecordIn4KButtonText != null)
                m_RecordIn4KButtonText.text = m_VideoRecorder != null && m_VideoRecorder.CaptureCameraIn4K ? "4K30" : "HD60"; // the movie's frame rate
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
