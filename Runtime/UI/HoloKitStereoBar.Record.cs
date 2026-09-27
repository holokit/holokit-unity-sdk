// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using HoloKit.iOS;
#endif

namespace HoloKit.UI
{
    // RECORD: slide left to start or stop recording, as in the HoloKit app. The arrows follow the
    // finger; reaching the end toggles, releasing springs back. The label shows RECORD, the
    // elapsed time while recording, or STOP while dragged during a recording.
    public sealed partial class HoloKitStereoBar
    {
        HoloKitSlideTrack m_RecordTrack;
        RectTransform m_RecordRow, m_RecordTrail;
        HoloKitBarGlyph m_RecordDot, m_RecordEndBar;
        Text m_RecordText;
        float m_RecordReleaseFrom, m_RecordReleaseAt = float.NegativeInfinity;

#if UNITY_IOS
        HoloKitVideoRecorder m_Recorder;

        HoloKitVideoRecorder Recorder
        {
            get
            {
                if (m_Recorder == null && m_Manager != null)
                    m_Recorder = m_Manager.GetComponentInChildren<HoloKitVideoRecorder>(true);
                return m_Recorder;
            }
        }

        bool RecorderBusy => Recorder != null && (Recorder.IsPreparing || Recorder.IsFinishing);
        bool IsRecording => Recorder != null && Recorder.IsRecording;
        // From the recorder, so a take started in Mono keeps its time in Stereo.
        float RecordStart => Recorder != null ? Recorder.RecordingStartTime : 0f;
#else
        float RecordStart => 0f;
        bool IsRecording => false;
#endif

        void BuildRecord()
        {
#if UNITY_IOS
            if (Recorder == null)
                return;
            var control = Control("RECORD", m_BarLayer, HoloKitBarControl.Record);
            var area = m_Layout.RecordHitArea;
            var touch = TouchArea(control, area, out _);
            // The mask: sliding arrows stop at RECORD's left edge (the label's italic overhang on
            // the right is kept).
            var mask = Area("Mask", control, new(area.Min, area.Max + 4f), m_Layout.BandHeight);
            mask.gameObject.AddComponent<RectMask2D>();
            m_RecordEndBar = EndBar(mask, 0f, 0f);
            m_RecordRow = Row("Slider", mask, HoloKitStereoBarLayout.RecordTrailing, 1f, area.Min);
            var arrow = Glyph(m_RecordRow, HoloKitBarGlyph.Kind.ArrowLeft, 2.4f, 2f);
            m_RecordTrail = Trail(arrow.rectTransform, HoloKitBarGlyph.Kind.ArrowLeftOutline, 5, 1.5f, right: false);
            m_RecordDot = Glyph(m_RecordRow, HoloKitBarGlyph.Kind.Dot, 2.2f, 2.2f);
            m_RecordText = Label(m_RecordRow, "RECORD");
            m_RecordTrack = new HoloKitSlideTrack(m_Layout.RecordSlideLength, -1);
            touch.Dragging = dragging =>
            {
                if (dragging)
                {
                    Press(HoloKitBarControl.Record);
                    return;
                }
                m_RecordReleaseFrom = m_RecordTrack.Offset;
                m_RecordReleaseAt = Time.unscaledTime;
                m_RecordTrack.Release();
                Press(null);
            };
            touch.Dragged = px =>
            {
                // Only a slide to the end counts, so a brush against the phone's top edge never
                // starts or stops a recording.
                if (m_RecordTrack.Drag(px / m_Px) && !RecorderBusy)
                    ToggleRecording();
            };
#endif
        }

        void RefreshRecord()
        {
            if (m_RecordRow == null)
                return;
            bool recording = IsRecording;
            bool dragging = m_RecordTrack.IsDragging;
            m_RecordEndBar.gameObject.SetActive(dragging);
            m_RecordTrail.gameObject.SetActive(dragging);
            // Hidden, not removed, so the arrow does not move.
            m_RecordDot.color = recording && dragging ? Color.clear : recording ? Accent : Color.white;
            m_RecordText.text = !recording ? "RECORD"
                : dragging ? "STOP"
                : HoloKitClock.Mmss(Time.unscaledTime - RecordStart);
            m_RecordText.color = recording && !dragging ? Accent : Color.white;
            LayoutRow(m_RecordRow, 1.5f);
            float offset = ShownOffset(m_RecordTrack, m_RecordReleaseFrom, m_RecordReleaseAt);
            m_RecordRow.anchoredPosition = new(
                (HoloKitStereoBarLayout.RecordTrailing - m_Layout.RecordHitArea.Min + offset) * m_Px,
                m_RecordRow.anchoredPosition.y);
        }

#if UNITY_IOS
        void ToggleRecording()
        {
            var recorder = Recorder;
            if (recorder.IsRecording)
            {
                recorder.EndRecording();
                return;
            }
            // The OPTIONS choices apply, the voice only when it needs no permission alert: an
            // alert in the headset is invisible and blocks every touch. Without it this take
            // records the game audio alone.
            var mode = recorder.AudioMode;
            bool microphone = recorder.IncludeMicrophone;
            bool voice = mode == RecordingAudioMode.Environment || microphone;
            if (voice && !Application.isEditor && !Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.Log("[HoloKitStereoBar] No microphone permission yet; recording game audio only. " +
                          "Record once in Mono to grant it.");
                recorder.AudioMode = RecordingAudioMode.GameAndMicrophone;
                recorder.IncludeMicrophone = false;
            }
            if (recorder.AudioMode == RecordingAudioMode.GameAndMicrophone &&
                (recorder.GameAudioListener == null || !recorder.GameAudioListener.isActiveAndEnabled))
                recorder.GameAudioListener = FindActiveListener();
            recorder.StartRecording();
            recorder.AudioMode = mode;
            recorder.IncludeMicrophone = microphone;
        }

        static AudioListener FindActiveListener()
        {
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled)
                    return listener;
            return null;
        }
#endif
    }
}
