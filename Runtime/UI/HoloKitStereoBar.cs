// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoloKit.UI
{
    /// <summary>
    /// Which controls the stereo bar shows.
    /// </summary>
    [Flags]
    public enum HoloKitStereoBarItems
    {
        None = 0,
        /// <summary>MONO: tap to leave Stereo.</summary>
        RenderMode = 1 << 0,
        /// <summary>RECORD: slide left to start, slide left again to stop (iOS, with a HoloKitVideoRecorder).</summary>
        Record = 1 << 1,
        /// <summary>TRIGGER: hold; raises <see cref="HoloKitCameraManager.OnStereoTrigger"/>.</summary>
        Trigger = 1 << 2,
        /// <summary>MORE: hold for frame rate, session time, recording and thermal state, and <see cref="HoloKitCameraManager.StereoInfo"/>.</summary>
        Info = 1 << 3,
        /// <summary>OPTIONS: slide right for recording audio, camera and hand tracking choices.</summary>
        Options = 1 << 4,
        All = RenderMode | Record | Trigger | Info | Options,
    }

    /// <summary>
    /// HoloKit's standard control bar along the top edge in Stereo, after the HoloKit app's Star AR
    /// panel and the HoloKit Swift SDK's stereo bar. Left to right: MORE (hold for information),
    /// RECORD (slide left), MONO (tap), OPTIONS (slide right), TRIGGER (hold). Every touch area
    /// runs down to the headset frame, sized in millimetres for the phone, because the wearer
    /// touches the phone's top edge blind. <see cref="HoloKitCameraManager"/> shows it in Stereo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class HoloKitStereoBar : MonoBehaviour
    {
        /// <summary>The bar's accent colour, #FF5B1A.</summary>
        public static readonly Color Accent = new(1f, 91f / 255f, 26f / 255f);
        static readonly Color s_Highlight = new(1f, 1f, 1f, 0.22f);

        HoloKitCameraManager m_Manager;
        HoloKitStereoBarItems m_Items;
        bool m_ShowsTouchAreas;
        HoloKitStereoBarLayout m_Layout;
        float m_Px; // pixels per millimetre
        Font m_Font;
        readonly HoloKitBarPress m_Press = new();
        readonly System.Collections.Generic.Dictionary<HoloKitBarControl, CanvasGroup> m_Groups = new();
        RectTransform m_BarLayer;

        // MORE
        GameObject m_InfoLayer;
        RectTransform m_MoreRow;
        Image m_MoreHighlight;
        bool m_MoreHeld;

        // TRIGGER
        Image m_TriggerHighlight;
        RectTransform m_TriggerRow;
        HoloKitBarGlyph m_TriggerIcon;
        Text m_TriggerLabel;
        bool m_TriggerHeld;
        float m_LastTrigger = float.NegativeInfinity;

#if UNITY_IOS
        UnityEngine.iOS.SystemGestureDeferMode m_PreviousDeferMode;
#endif

        /// <summary>Which controls are shown.</summary>
        public HoloKitStereoBarItems Items => m_Items;

        /// <summary>The layout in millimetres for the current phone.</summary>
        public HoloKitStereoBarLayout Layout => m_Layout;

        /// <summary>True once the controls are built (the phone's geometry is known).</summary>
        public bool HasControls => m_BarLayer != null;

        internal void Rebuild()
        {
            if (m_Manager != null)
                Build();
        }

        internal void Initialize(HoloKitCameraManager manager, HoloKitStereoBarItems items, bool showsTouchAreas)
        {
            m_Manager = manager;
            m_Items = items;
            m_ShowsTouchAreas = showsTouchAreas;
            m_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!TryGetComponent<Canvas>(out var canvas))
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above app UI: the bar is how the wearer leaves Stereo
            if (!TryGetComponent<GraphicRaycaster>(out _))
                gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            Build();
        }

        void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            m_Groups.Clear();
            m_Press.Press(null);
            m_BarLayer = null;
            m_InfoLayer = m_OptionsLayer = null;
            m_MoreRow = m_TriggerRow = m_RecordRow = m_OptionsRow = null;
            m_InfoLines.Clear();
            if (!m_Manager.TryGetStereoBarGeometry(out m_Px, out m_Layout))
            {
                // Retried on the next Stereo; meanwhile the Default UI Canvas stays up.
                Debug.LogWarning("[HoloKitStereoBar] No screen geometry for this phone yet; the bar is empty.");
                return;
            }

            var root = (RectTransform)transform;
            m_BarLayer = Stretch("Bar", root);
            if ((m_Items & HoloKitStereoBarItems.Info) != 0)
                BuildInfo(root);
            if ((m_Items & HoloKitStereoBarItems.Record) != 0)
                BuildRecord();
            if ((m_Items & HoloKitStereoBarItems.RenderMode) != 0)
                BuildMono();
            if ((m_Items & HoloKitStereoBarItems.Options) != 0)
                BuildOptions(root);
            if ((m_Items & HoloKitStereoBarItems.Trigger) != 0)
                BuildTrigger();
            if ((m_Items & HoloKitStereoBarItems.Info) != 0)
                BuildMore(root);
            if (m_ShowsTouchAreas)
                BuildTouchAreaOutlines(root);
            Refresh();
        }

        void OnEnable()
        {
            m_SessionStart = Time.unscaledTime;
            if (m_Manager != null && m_BarLayer == null)
                Build();
#if UNITY_IOS
            // The controls sit on the top corners, where iOS's Notification Center and Control
            // Center gestures otherwise delay or swallow touches.
            m_PreviousDeferMode = UnityEngine.iOS.Device.deferSystemGesturesMode;
            UnityEngine.iOS.Device.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
#endif
        }

        void OnDisable()
        {
#if UNITY_IOS
            UnityEngine.iOS.Device.deferSystemGesturesMode = m_PreviousDeferMode;
#endif
            m_MoreHeld = m_TriggerHeld = false;
            CloseOptions();
            Press(null);
        }

        void Update()
        {
            CountFrame();
            if (m_Manager != null && m_BarLayer != null)
                Refresh();
        }

        void Refresh()
        {
            bool options = OptionsOpen;
            bool more = !options && m_MoreHeld;
            m_BarLayer.gameObject.SetActive(!options && !more);
            if (m_InfoLayer != null)
            {
                m_InfoLayer.SetActive(more);
                if (more)
                    RefreshInfo();
            }
            foreach (var (control, group) in m_Groups)
                group.alpha = m_Press.IsShown(control) ? 1f : 0f;
            if (m_MoreRow != null)
            {
                m_MoreRow.parent.gameObject.SetActive(!options);
                m_MoreRow.GetComponent<CanvasGroup>().alpha = more ? 0.5f : 1f;
                m_MoreHighlight.color = m_MoreHeld ? s_Highlight : Color.clear;
            }
            RefreshTrigger();
            RefreshRecord();
            RefreshOptions();
        }

        void Press(HoloKitBarControl? control)
        {
            // As in the HoloKit app, the alignment marker hides while a control is held.
            if (m_Press.Press(control) && m_Manager != null)
                m_Manager.SetAlignmentMarkerSuppressed(control != null);
        }

        // MARK: MORE, MONO, TRIGGER

        void BuildMore(RectTransform root)
        {
            var control = Control("MORE", root, HoloKitBarControl.More);
            var touch = TouchArea(control, m_Layout.MoreHitArea, out m_MoreHighlight);
            touch.Held = held =>
            {
                // MORE works like a hover: the information shows while the finger is on it.
                m_MoreHeld = held;
                Press(held ? HoloKitBarControl.More : null);
            };
            m_MoreRow = Row("Label", control, m_Layout.MoreLeading, 0f);
            m_MoreRow.gameObject.AddComponent<CanvasGroup>();
            Label(m_MoreRow, "MORE");
            Glyph(m_MoreRow, HoloKitBarGlyph.Kind.ArrowUpRight, 1.8f, 1.8f);
            LayoutRow(m_MoreRow, 1f);
        }

        void BuildMono()
        {
            var control = Control("MONO", m_BarLayer, HoloKitBarControl.Mono);
            var touch = TouchArea(control, m_Layout.ModeHitArea, out var highlight);
            touch.Held = held => highlight.color = held ? s_Highlight : Color.clear;
            touch.Tapped = () => m_Manager.ScreenRenderMode = ScreenRenderMode.Mono;
            var row = Row("Label", control, HoloKitStereoBarLayout.ModeCenter, 0.5f);
            Glyph(row, HoloKitBarGlyph.Kind.Phone, 1.4f, 2.2f);
            Label(row, "MONO");
            LayoutRow(row, 0.8f);
        }

        void BuildTrigger()
        {
            var control = Control("TRIGGER", m_BarLayer, HoloKitBarControl.Trigger);
            var touch = TouchArea(control, m_Layout.TriggerHitArea, out m_TriggerHighlight);
            // Unlike the other controls, TRIGGER leaves the bar and the alignment marker as they are:
            // it is pressed often and quickly, and hiding the rest made the whole bar blink.
            touch.Held = held =>
            {
                m_TriggerHeld = held;
                if (held)
                    FireTrigger();
            };
            m_TriggerRow = Row("Label", control, m_Layout.TriggerTrailing, 1f);
            m_TriggerIcon = Glyph(m_TriggerRow, HoloKitBarGlyph.Kind.ArrowUpLeft, 1.8f, 1.8f);
            m_TriggerLabel = Label(m_TriggerRow, "TRIGGER");
        }

        /// <summary>Fires TRIGGER as a press would: raises the event and flashes the label.</summary>
        public void FireTrigger()
        {
            m_LastTrigger = Time.unscaledTime;
            if (m_Manager != null)
                m_Manager.InvokeStereoTrigger();
        }

        void RefreshTrigger()
        {
            if (m_TriggerRow == null)
                return;
            // "↖ TRIGGER", or "TRIGGERED" in the accent colour for at least 0.4 s per press
            // (both labels set on HoloKitCameraManager).
            float since = Time.unscaledTime - m_LastTrigger;
            bool triggered = HoloKitPressFlash.IsShown(m_TriggerHeld, float.IsInfinity(since) ? null : since);
            m_TriggerIcon.gameObject.SetActive(!triggered);
            m_TriggerLabel.text = triggered ? m_Manager.StereoTriggerPressedLabel : m_Manager.StereoTriggerLabel;
            m_TriggerLabel.color = triggered ? Accent : Color.white;
            m_TriggerHighlight.color = triggered ? s_Highlight : Color.clear;
            LayoutRow(m_TriggerRow, 1f);
        }

        // MARK: Building blocks. Positions are in millimetres from the screen's top centre.

        RectTransform Node(string name, Transform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        RectTransform Stretch(string name, Transform parent)
        {
            var rt = Node(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// A full-screen group for one control; the others fade while it is held.
        RectTransform Control(string name, Transform parent, HoloKitBarControl control)
        {
            var rt = Stretch(name, parent);
            m_Groups[control] = rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        /// A rect over `area`, from the screen's top edge down `heightMm`.
        RectTransform Area(string name, Transform parent, HoloKitBarRange area, float heightMm)
        {
            var rt = Node(name, parent);
            rt.anchorMin = rt.anchorMax = new(0.5f, 1f);
            rt.pivot = new(0f, 1f);
            rt.anchoredPosition = new(area.Min * m_Px, 0f);
            rt.sizeDelta = new(area.Width * m_Px, heightMm * m_Px);
            return rt;
        }

        /// A touch area down to the headset frame; its image is the pressed highlight.
        HoloKitBarTouchArea TouchArea(Transform parent, HoloKitBarRange area, out Image highlight)
        {
            var rt = Area("Touch Area", parent, area, m_Layout.BandHeight);
            highlight = rt.gameObject.AddComponent<Image>();
            highlight.color = Color.clear; // still receives touches
            return rt.gameObject.AddComponent<HoloKitBarTouchArea>();
        }

        /// A row of labels and icons, centred on the label line, whose leading (0), centre (0.5)
        /// or trailing (1) edge sits `xMm` from the screen centre. Inside an area, pass the
        /// area's left edge as `parentMinMm`.
        RectTransform Row(string name, Transform parent, float xMm, float pivotX, float? parentMinMm = null)
        {
            var rt = Node(name, parent);
            rt.anchorMin = rt.anchorMax = parentMinMm.HasValue ? new(0f, 1f) : new(0.5f, 1f);
            rt.pivot = new(pivotX, 0.5f);
            rt.anchoredPosition = new((xMm - (parentMinMm ?? 0f)) * m_Px, -HoloKitStereoBarLayout.RowFromTop * m_Px);
            rt.sizeDelta = new(0f, HoloKitStereoBarLayout.RowFromTop * 2f * m_Px);
            return rt;
        }

        Text Label(Transform parent, string text, float sizeMm = HoloKitStereoBarLayout.LabelSize,
            FontStyle style = FontStyle.BoldAndItalic, Color? color = null)
        {
            var t = Node(text, parent).gameObject.AddComponent<Text>();
            t.font = m_Font;
            t.fontSize = Mathf.Max(1, Mathf.RoundToInt(sizeMm * m_Px));
            t.fontStyle = style;
            t.color = color ?? Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        HoloKitBarGlyph Glyph(Transform parent, HoloKitBarGlyph.Kind kind, float widthMm, float heightMm)
        {
            var node = Node(kind.ToString(), parent).gameObject;
            node.AddComponent<CanvasRenderer>();
            var g = node.AddComponent<HoloKitBarGlyph>();
            g.Shape = kind;
            g.raycastTarget = false;
            g.rectTransform.sizeDelta = new(widthMm * m_Px, heightMm * m_Px);
            return g;
        }

        float WidthOf(RectTransform child) =>
            child.TryGetComponent<Text>(out var t) ? t.preferredWidth : child.sizeDelta.x;

        /// Lays the row's active children left to right, `spacingMm` apart, and sizes the row.
        void LayoutRow(RectTransform row, float spacingMm)
        {
            float x = 0f;
            bool first = true;
            for (int i = 0; i < row.childCount; i++)
            {
                var child = (RectTransform)row.GetChild(i);
                if (!child.gameObject.activeSelf)
                    continue;
                if (!first)
                    x += spacingMm * m_Px;
                first = false;
                float width = WidthOf(child);
                child.anchorMin = child.anchorMax = new(0f, 0.5f);
                child.pivot = new(0f, 0.5f);
                child.anchoredPosition = new(x, 0f);
                if (child.TryGetComponent<Text>(out var text))
                    child.sizeDelta = new(width, text.preferredHeight);
                x += width;
            }
            row.sizeDelta = new(x, row.sizeDelta.y);
        }

        void BuildTouchAreaOutlines(RectTransform root)
        {
            var layer = Stretch("Touch Areas", root);
            var areas = new (string, Color, HoloKitBarRange)[]
            {
                ("MORE", Color.cyan, m_Layout.MoreHitArea), ("RECORD", new(1f, 0.6f, 0f), m_Layout.RecordHitArea),
                ("MONO", Color.green, m_Layout.ModeHitArea), ("OPTIONS", Color.yellow, m_Layout.OptionsHitArea),
                ("TRIGGER", Color.magenta, m_Layout.TriggerHitArea),
            };
            foreach (var (name, color, range) in areas)
            {
                var rt = Area(name, layer, range, m_Layout.BandHeight);
                var fill = rt.gameObject.AddComponent<Image>();
                fill.color = new(color.r, color.g, color.b, 0.15f);
                fill.raycastTarget = false;
                var text = Label(rt, $"{name} {Mathf.RoundToInt(range.Width)} mm", 1.2f, FontStyle.Bold, color);
                var trt = text.rectTransform;
                trt.anchorMin = trt.anchorMax = trt.pivot = Vector2.zero;
                trt.anchoredPosition = new(2f, 2f);
                trt.sizeDelta = new(text.preferredWidth, text.preferredHeight);
            }
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            // The Input System's UI module when the project uses it; no hard package dependency.
            var module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null)
            {
                go.AddComponent(module);
                return;
            }
#endif
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
