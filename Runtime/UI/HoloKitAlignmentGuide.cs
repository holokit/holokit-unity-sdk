// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HoloKit.UI
{
    /// <summary>
    /// Stereo alignment aid, as in the HoloKit app and Swift SDK: the outline of HoloKit X's phone
    /// opening (open at the bottom) around both viewports, and the marker line that meets the
    /// headset's alignment notch. Seat the phone so the line sits on the notch and the outline fits
    /// the opening. The guide hides after <see cref="AutoHideDelay"/> seconds without a touch (a
    /// touch brings it back); the marker also hides while a stereo bar control is held.
    /// <see cref="HoloKitCameraManager"/> shows it in Stereo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoloKitAlignmentGuide : MonoBehaviour
    {
        static readonly Color s_OutlineColor = new(94f / 255f, 94f / 255f, 94f / 255f);
        static readonly Color s_MarkerColor = new(127f / 255f, 127f / 255f, 127f / 255f);

        HoloKitCameraManager m_Manager;
        readonly HoloKitChromeTimer m_Timer = new(10f, 0f);
        GameObject m_Content;
        Image m_Marker;
        bool m_MarkerSuppressed;

        /// <summary>Seconds without a touch before the guide hides; 0 keeps it.</summary>
        public float AutoHideDelay
        {
            get => m_Timer.Delay;
            set => m_Timer.Delay = value;
        }

        /// <summary>Whether the guide shows now (it hides after <see cref="AutoHideDelay"/> without a touch).</summary>
        public bool IsVisible => m_Content != null && m_Content.activeSelf;

        internal void Initialize(HoloKitCameraManager manager, float autoHideDelay)
        {
            m_Manager = manager;
            m_Timer.Delay = autoHideDelay;
            if (!TryGetComponent<Canvas>(out var canvas))
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31999; // just below the stereo bar
            Rebuild();
        }

        /// <summary>The marker hides while a stereo bar control is held.</summary>
        internal bool MarkerSuppressed
        {
            set
            {
                m_MarkerSuppressed = value;
                if (m_Marker != null)
                    m_Marker.enabled = !value;
            }
        }

        internal void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            m_Content = null;
            m_Marker = null;
            if (m_Manager == null || !m_Manager.TryGetStereoBarGeometry(out float px, out var layout))
                return;

            float width = HoloKitScreenUtils.GetScreenWidth(), height = HoloKitScreenUtils.GetScreenHeight();
            var geometry = HoloKitAlignmentGuideGeometry.Compute(width, height, px,
                m_Manager.LeftViewport.xMin, m_Manager.RightViewport.xMax, m_Manager.LeftViewport.yMax, layout.MarkerX);

            var content = TopLeft(new GameObject("Guide", typeof(RectTransform)), width, height);
            content.SetParent(transform, false);
            m_Content = content.gameObject;

            var outlineNode = TopLeft(new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer)), width, height);
            outlineNode.SetParent(content, false);
            var outline = outlineNode.gameObject.AddComponent<HoloKitGuideOutline>();
            outline.raycastTarget = false;
            outline.color = s_OutlineColor;
            outline.Geometry = geometry;

            var markerNode = TopLeft(new GameObject("Marker", typeof(RectTransform)),
                HoloKitAlignmentGuideGeometry.Hairline, geometry.Top);
            markerNode.SetParent(content, false);
            markerNode.anchoredPosition = new(geometry.MarkerX - HoloKitAlignmentGuideGeometry.Hairline / 2f, 0f);
            m_Marker = markerNode.gameObject.AddComponent<Image>();
            m_Marker.color = s_MarkerColor;
            m_Marker.raycastTarget = false;
            m_Marker.enabled = !m_MarkerSuppressed;
        }

        static RectTransform TopLeft(GameObject go, float width, float height)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new(width, height);
            return rt;
        }

        void OnEnable()
        {
            m_Timer.Touch(Time.unscaledTime);
            if (m_Content != null)
                m_Content.SetActive(true);
            else if (m_Manager != null)
                Rebuild();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (AnyTouch())
                m_Timer.Touch(now);
            if (m_Content != null)
            {
                bool visible = m_Timer.IsVisible(now);
                if (m_Content.activeSelf != visible)
                    m_Content.SetActive(visible);
            }
        }

        static bool AnyTouch()
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
                return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.touchCount > 0 || Input.GetMouseButton(0);
#else
            return false;
#endif
        }
    }

    /// <summary>The headset opening's outline: up the left side, round the top corners, down the right.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HoloKitGuideOutline : MaskableGraphic
    {
        const int ArcSegments = 12;
        HoloKitAlignmentGuideGeometry m_Geometry;
        readonly List<Vector2> m_Points = new();

        public HoloKitAlignmentGuideGeometry Geometry
        {
            set { m_Geometry = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var g = m_Geometry;
            float r = g.CornerRadiusPixels;
            if (g.Right - g.Left <= 2f * r)
                return;
            // Local coordinates: origin at the screen's top-left, y up (so screen y is negated).
            m_Points.Clear();
            m_Points.Add(new(g.Left, -g.Bottom));
            AddArc(new(g.Left + r, -(g.Top + r)), r, 180f, 90f);
            AddArc(new(g.Right - r, -(g.Top + r)), r, 90f, 0f);
            m_Points.Add(new(g.Right, -g.Bottom));
            for (int i = 1; i < m_Points.Count; i++)
                AddLine(vh, m_Points[i - 1], m_Points[i], HoloKitAlignmentGuideGeometry.Hairline);
        }

        void AddArc(Vector2 center, float radius, float fromDegrees, float toDegrees)
        {
            for (int s = 0; s <= ArcSegments; s++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, s / (float)ArcSegments) * Mathf.Deg2Rad;
                m_Points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            if ((b - a).sqrMagnitude < 1e-6f)
                return;
            Vector2 dir = (b - a).normalized, half = dir * (width / 2f), n = new Vector2(-dir.y, dir.x) * (width / 2f);
            a -= half;
            b += half;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector4.zero);
            vh.AddVert(a + n, color, Vector4.zero);
            vh.AddVert(b + n, color, Vector4.zero);
            vh.AddVert(b - n, color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
