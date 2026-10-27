// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using HoloKit.iOS;
#endif

namespace HoloKit.UI
{
    // MORE's information row and the OPTIONS panel.
    public sealed partial class HoloKitStereoBar
    {
        const float InfoInterval = 0.2f;
        const float OptionsAutoClose = 4f;

        float m_SessionStart;
        int m_FrameCount;
        float m_FrameWindowStart;
        float m_Fps;
        float m_NextInfo;
        RectTransform m_InfoRow, m_Thermal;
        Text m_FpsText, m_MainText, m_ClockText, m_RecText;
        HoloKitBarGlyph m_ThermalDot;
        readonly List<Text> m_InfoLines = new();
        readonly FrameTiming[] m_Timings = new FrameTiming[1];
#if UNITY_IOS && !UNITY_EDITOR
        AppleNativeProvider m_Apple;
#endif

        sealed class ChoiceGroup
        {
            public string Title;
            public string[] Labels;
            public Func<int> Selected;
            public Action<int> Choose;
            public Func<bool> Enabled = () => true;
            public Text[] Texts;
            public CanvasGroup Group;
        }

        readonly List<ChoiceGroup> m_ChoiceGroups = new();
        GameObject m_OptionsLayer;
        HoloKitSlideTrack m_OptionsTrack;
        RectTransform m_OptionsRow, m_OptionsTrail;
        HoloKitBarGlyph m_OptionsEndBar;
        float m_OptionsReleaseFrom, m_OptionsReleaseAt = float.NegativeInfinity;
        bool m_OptionsOpen;
        float m_OptionsTouched;

        bool OptionsOpen => m_OptionsOpen;

        // MARK: MORE information

        void BuildInfo(RectTransform root)
        {
            var layer = Stretch("Info", root);
            m_InfoLayer = layer.gameObject;
            var band = Area("Band", layer, new(-m_Layout.HalfWidth, m_Layout.HalfWidth), m_Layout.BandHeight);
            var black = band.gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;
            // The same type as the bar's controls (bold italic), so MORE reads as part of the menu.
            m_InfoRow = Row("Info", layer, -50f, 0f);
            m_FpsText = Label(m_InfoRow, "");
            m_MainText = Label(m_InfoRow, "");
            m_ClockText = Label(m_InfoRow, "");
            m_RecText = Label(m_InfoRow, "", color: Accent);
            m_Thermal = Node("Thermal", m_InfoRow);
            Label(m_Thermal, "Thermal");
            m_ThermalDot = Glyph(m_Thermal, HoloKitBarGlyph.Kind.Dot, 1.6f, 1.6f);
            LayoutRow(m_Thermal, 0.8f);
            m_InfoLayer.SetActive(false);
        }

        void CountFrame()
        {
            m_FrameCount++;
            float now = Time.unscaledTime, window = now - m_FrameWindowStart;
            if (window >= 0.5f)
            {
                m_Fps = m_FrameCount / window;
                m_FrameCount = 0;
                m_FrameWindowStart = now;
            }
            if (m_MoreHeld)
                FrameTimingManager.CaptureFrameTimings();
        }

        void RefreshInfo()
        {
            float now = Time.unscaledTime;
            if (now < m_NextInfo)
                return;
            m_NextInfo = now + InfoInterval;
            m_FpsText.text = $"{Mathf.RoundToInt(m_Fps)}fps";
            // CPU main thread time needs Frame Timing Stats in Player Settings.
            bool timed = FrameTimingManager.GetLatestTimings(1, m_Timings) > 0;
            m_MainText.gameObject.SetActive(timed);
            if (timed)
                m_MainText.text = $"main {m_Timings[0].cpuMainThreadFrameTime:0.0}ms";
            m_ClockText.text = HoloKitClock.Mmss(now - m_SessionStart);
            bool recording = IsRecording;
            m_RecText.gameObject.SetActive(recording);
            if (recording)
                m_RecText.text = "REC " + HoloKitClock.Mmss(now - RecordStart);
#if UNITY_IOS && !UNITY_EDITOR
            m_Apple ??= new AppleNativeProvider();
            m_ThermalDot.color = m_Apple.GetThermalState() switch
            {
                AppleThermalState.Normal => Color.blue,
                AppleThermalState.Fair => Color.green,
                AppleThermalState.Serious => Color.yellow,
                _ => Color.red,
            };
#else
            m_Thermal.gameObject.SetActive(false);
#endif
            var lines = m_Manager.StereoInfo ?? Array.Empty<string>();
            while (m_InfoLines.Count < lines.Length)
                m_InfoLines.Add(Label(m_InfoRow, ""));
            for (int i = 0; i < m_InfoLines.Count; i++)
            {
                m_InfoLines[i].gameObject.SetActive(i < lines.Length);
                if (i < lines.Length)
                    m_InfoLines[i].text = lines[i];
            }
            LayoutRow(m_InfoRow, 3f);
        }

        void OnDestroy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            m_Apple?.Dispose();
            m_Apple = null;
#endif
        }

        // MARK: OPTIONS

        void CollectChoiceGroups()
        {
            m_ChoiceGroups.Clear();
#if UNITY_IOS
            var recorder = Recorder;
            if (recorder != null)
            {
                // The Stereo movie's size: 2560 × 1440 or 3840 × 2160.
                m_ChoiceGroups.Add(new ChoiceGroup
                {
                    Title = "VIDEO RECORDING",
                    Labels = new[] { "2K", "4K" },
                    Selected = () => recorder.StereoResolution == StereoRecordingResolution.UHD4K ? 1 : 0,
                    Choose = i => recorder.StereoResolution = i == 1 ? StereoRecordingResolution.UHD4K : StereoRecordingResolution.QHD2K,
                    Enabled = () => !RecorderBusy && !recorder.IsRecording,
                });
                // Voice (microphone), game (the app's sound), or both, as separate tracks.
                m_ChoiceGroups.Add(new ChoiceGroup
                {
                    Title = "AUDIO",
                    Labels = new[] { "VOICE", "GAME", "BOTH" },
                    Selected = () => recorder.AudioMode == RecordingAudioMode.Environment ? 0 : recorder.IncludeMicrophone ? 2 : 1,
                    Choose = i =>
                    {
                        recorder.AudioMode = i == 0 ? RecordingAudioMode.Environment : RecordingAudioMode.GameAndMicrophone;
                        recorder.IncludeMicrophone = i != 1;
                    },
                    Enabled = () => !RecorderBusy && !recorder.IsRecording,
                });
            }
            var hands = FindObjectsByType<HandTrackingManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (hands.Length > 0)
            {
                m_ChoiceGroups.Add(new ChoiceGroup
                {
                    Title = "HANDS",
                    Labels = new[] { "ON", "OFF" },
                    Selected = () => Array.Exists(hands, h => h != null && h.enabled) ? 0 : 1,
                    Choose = i => { foreach (var h in hands) if (h != null) h.enabled = i == 0; },
                });
            }
            if (recorder != null)
            {
                // ARKit's 4K camera for sharper takes; the movie then runs at 30 fps.
                m_ChoiceGroups.Add(new ChoiceGroup
                {
                    Title = "CAMERA",
                    Labels = new[] { "HD", "4K" },
                    Selected = () => recorder.CaptureCameraIn4K ? 1 : 0,
                    Choose = i => recorder.CaptureCameraIn4K = i == 1,
                    Enabled = () => !RecorderBusy && !recorder.IsRecording,
                });
            }
#endif
        }

        void BuildOptions(RectTransform root)
        {
            CollectChoiceGroups();
            if (m_ChoiceGroups.Count == 0)
                return;

            // Slide right to open, as the HoloKit app's Boost button.
            var control = Control("OPTIONS", m_BarLayer, HoloKitBarControl.Options);
            var area = m_Layout.OptionsHitArea;
            var touch = TouchArea(control, area, out _);
            var mask = Area("Mask", control, new(area.Min - 4f, area.Max), m_Layout.BandHeight);
            mask.gameObject.AddComponent<RectMask2D>(); // arrows stop at OPTIONS' right edge
            m_OptionsEndBar = EndBar(mask, m_Layout.OptionsEndBar - (area.Min - 4f), 1f);
            m_OptionsRow = Row("Slider", mask, HoloKitStereoBarLayout.OptionsLeading, 0f, area.Min - 4f);
            Label(m_OptionsRow, "OPTIONS");
            var arrow = Glyph(m_OptionsRow, HoloKitBarGlyph.Kind.ArrowRight, 2.4f, 2f);
            m_OptionsTrail = Trail(arrow.rectTransform, HoloKitBarGlyph.Kind.ArrowRightOutline, 4, 1.2f, right: true);
            LayoutRow(m_OptionsRow, 1.2f);
            m_OptionsTrack = new HoloKitSlideTrack(m_Layout.OptionsSlideLength, +1);
            touch.Dragging = dragging =>
            {
                if (dragging)
                {
                    Press(HoloKitBarControl.Options);
                    return;
                }
                m_OptionsReleaseFrom = m_OptionsTrack.Offset;
                m_OptionsReleaseAt = Time.unscaledTime;
                m_OptionsTrack.Release();
                Press(null);
            };
            touch.Dragged = px => { if (m_OptionsTrack.Drag(px / m_Px)) OpenOptions(); };

            BuildOptionsPanel(root);
        }

        /// Choices across the band: DONE where MORE was (back to the bar), then VIDEO RECORDING,
        /// AUDIO, HANDS and CAMERA, packed tight. Closes on DONE or after 4 s without a touch.
        void BuildOptionsPanel(RectTransform root)
        {
            var layer = Stretch("Options Panel", root);
            m_OptionsLayer = layer.gameObject;
            var band = Area("Band", layer, new(-m_Layout.HalfWidth, m_Layout.HalfWidth), m_Layout.BandHeight);
            band.gameObject.AddComponent<Image>().color = Color.black;

            var done = TouchArea(layer, m_Layout.MoreHitArea, out _);
            done.Tapped = CloseOptions;
            var doneRow = Row("DONE", layer, m_Layout.MoreLeading, 0f);
            Label(doneRow, "DONE");
            LayoutRow(doneRow, 0f);

            float x = m_Layout.MoreHitArea.Max;
            foreach (var group in m_ChoiceGroups)
            {
                var node = Stretch(group.Title, layer);
                group.Group = node.gameObject.AddComponent<CanvasGroup>();
                x += PlaceText(Label(node, group.Title, 1.6f, FontStyle.Bold, new Color(1f, 1f, 1f, 0.55f)), x) + 1.5f;
                group.Texts = new Text[group.Labels.Length];
                for (int i = 0; i < group.Labels.Length; i++)
                {
                    if (i > 0)
                        x += PlaceText(Label(node, "/", color: new Color(1f, 1f, 1f, 0.4f)), x);
                    var text = Label(node, group.Labels[i]);
                    float width = text.preferredWidth / m_Px + 2f; // 1 mm each side
                    int index = i;
                    var choice = TouchArea(node, new(x, x + width), out _);
                    choice.Tapped = () =>
                    {
                        m_OptionsTouched = Time.unscaledTime;
                        if (group.Enabled())
                            group.Choose(index);
                    };
                    text.transform.SetAsLastSibling();
                    PlaceText(text, x + 1f);
                    group.Texts[i] = text;
                    x += width;
                }
                x += 4f;
            }
            m_OptionsLayer.SetActive(false);
        }

        /// Puts a label's leading edge `xMm` from the screen centre on the label line; returns its width in mm.
        float PlaceText(Text text, float xMm)
        {
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new(0.5f, 1f);
            rt.pivot = new(0f, 0.5f);
            rt.anchoredPosition = new(xMm * m_Px, -HoloKitStereoBarLayout.RowFromTop * m_Px);
            rt.sizeDelta = new(text.preferredWidth, text.preferredHeight);
            return text.preferredWidth / m_Px;
        }

        void OpenOptions()
        {
            // The panel replaces the bar mid-drag: that drag never ends, so forget it.
            if (m_Press.PanelOpened() && m_Manager != null)
                m_Manager.SetAlignmentMarkerSuppressed(false);
            m_OptionsOpen = true;
            m_OptionsTouched = Time.unscaledTime;
        }

        void CloseOptions() => m_OptionsOpen = false;

        void RefreshOptions()
        {
            if (m_OptionsLayer == null)
                return;
            if (m_OptionsOpen && Time.unscaledTime - m_OptionsTouched > OptionsAutoClose)
                m_OptionsOpen = false;
            m_OptionsLayer.SetActive(m_OptionsOpen);
            if (m_OptionsOpen)
            {
                foreach (var group in m_ChoiceGroups)
                {
                    int selected = group.Selected();
                    group.Group.alpha = group.Enabled() ? 1f : 0.4f;
                    for (int i = 0; i < group.Texts.Length; i++)
                        group.Texts[i].color = i == selected ? Accent : Color.white;
                }
                return;
            }
            bool dragging = m_OptionsTrack.IsDragging;
            m_OptionsEndBar.gameObject.SetActive(dragging);
            m_OptionsTrail.gameObject.SetActive(dragging);
            float offset = ShownOffset(m_OptionsTrack, m_OptionsReleaseFrom, m_OptionsReleaseAt);
            var area = m_Layout.OptionsHitArea;
            m_OptionsRow.anchoredPosition = new(
                (HoloKitStereoBarLayout.OptionsLeading - (area.Min - 4f) + offset) * m_Px, m_OptionsRow.anchoredPosition.y);
        }

        // MARK: Slide parts shared by RECORD and OPTIONS

        /// The white end bar the arrow must reach, 0.5 × 3 mm, with its `pivotX` edge `xMm` from the mask's left edge.
        HoloKitBarGlyph EndBar(RectTransform mask, float xMm, float pivotX)
        {
            var bar = Glyph(mask, HoloKitBarGlyph.Kind.Bar, 0.5f, 3f);
            var rt = bar.rectTransform;
            rt.anchorMin = rt.anchorMax = new(0f, 1f);
            rt.pivot = new(pivotX, 0.5f);
            rt.anchoredPosition = new(xMm * m_Px, -HoloKitStereoBarLayout.RowFromTop * m_Px);
            bar.gameObject.SetActive(false);
            return bar;
        }

        /// Hollow arrows beside the filled one: the path still to travel, shown while sliding.
        /// They hang off the arrow, so they take no room in the row.
        RectTransform Trail(RectTransform arrow, HoloKitBarGlyph.Kind kind, int count, float spacingMm, bool right)
        {
            var trail = Node("Trail", arrow);
            trail.anchorMin = trail.anchorMax = new(right ? 1f : 0f, 0.5f);
            trail.pivot = new(right ? 0f : 1f, 0.5f);
            trail.anchoredPosition = new((right ? spacingMm : -spacingMm) * m_Px, 0f);
            for (int i = 0; i < count; i++)
                Glyph(trail, kind, 2.4f, 2f);
            LayoutRow(trail, spacingMm);
            trail.gameObject.SetActive(false);
            return trail;
        }

        /// The slide offset shown: the finger while dragging, then an ease-out back to rest over 0.3 s.
        static float ShownOffset(HoloKitSlideTrack track, float releasedFrom, float releasedAt)
        {
            if (track.IsDragging)
                return track.Offset;
            float t = (Time.unscaledTime - releasedAt) / 0.3f;
            if (t >= 1f)
                return 0f;
            return releasedFrom * (1f - t) * (1f - t);
        }
    }
}
