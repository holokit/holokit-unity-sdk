// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;

namespace HoloKit.UI
{
    /// <summary>
    /// The stereo bar's icons, drawn as vector shapes so they look the same on every phone and
    /// need no font glyphs (the SF Symbols the Swift SDK uses are not available to Unity).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HoloKitBarGlyph : MaskableGraphic
    {
        public enum Kind { ArrowLeft, ArrowLeftOutline, ArrowRight, ArrowRightOutline, Dot, ArrowUpRight, ArrowUpLeft, Phone, Bar }

        Kind m_Shape;

        public Kind Shape
        {
            get => m_Shape;
            set { if (m_Shape != value) { m_Shape = value; SetVerticesDirty(); } }
        }

        // Block arrow pointing left in a unit box: tip, head, shaft (SF "arrowshape.left").
        static readonly Vector2[] s_ArrowLeft =
        {
            new(0f, 0.5f), new(0.55f, 1f), new(0.55f, 0.75f), new(1f, 0.75f),
            new(1f, 0.25f), new(0.55f, 0.25f), new(0.55f, 0f),
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float stroke = Mathf.Max(1f, Mathf.Min(r.width, r.height) * 0.12f);
            switch (m_Shape)
            {
                case Kind.ArrowLeft:
                case Kind.ArrowRight:
                {
                    bool right = m_Shape == Kind.ArrowRight;
                    AddTriangle(vh, P(r, s_ArrowLeft[0], right), P(r, s_ArrowLeft[1], right), P(r, s_ArrowLeft[6], right));
                    AddQuad(vh, P(r, s_ArrowLeft[5], right), P(r, s_ArrowLeft[2], right),
                        P(r, s_ArrowLeft[3], right), P(r, s_ArrowLeft[4], right));
                    break;
                }
                case Kind.ArrowLeftOutline:
                case Kind.ArrowRightOutline:
                {
                    bool right = m_Shape == Kind.ArrowRightOutline;
                    for (int i = 0; i < s_ArrowLeft.Length; i++)
                        AddLine(vh, P(r, s_ArrowLeft[i], right), P(r, s_ArrowLeft[(i + 1) % s_ArrowLeft.Length], right), stroke);
                    break;
                }
                case Kind.Dot:
                    AddCircle(vh, r.center, Mathf.Min(r.width, r.height) / 2f);
                    break;
                case Kind.ArrowUpRight:
                case Kind.ArrowUpLeft:
                {
                    bool left = m_Shape == Kind.ArrowUpLeft;
                    float thick = stroke * 1.4f;
                    Vector2 tip = P(r, new(0.88f, 0.88f), left);
                    AddLine(vh, P(r, new(0.12f, 0.12f), left), tip, thick);
                    AddLine(vh, P(r, new(0.3f, 0.88f), left), tip, thick);
                    AddLine(vh, P(r, new(0.88f, 0.3f), left), tip, thick);
                    break;
                }
                case Kind.Phone:
                {
                    // A portrait phone outline with the Dynamic Island (SF "iphone").
                    float w = r.height * 0.55f, x0 = r.center.x - w / 2f, x1 = x0 + w;
                    Vector2 a = new(x0, r.yMin), b = new(x0, r.yMax), c = new(x1, r.yMax), d = new(x1, r.yMin);
                    AddLine(vh, a, b, stroke);
                    AddLine(vh, b, c, stroke);
                    AddLine(vh, c, d, stroke);
                    AddLine(vh, d, a, stroke);
                    float island = w * 0.35f, y = r.yMax - stroke * 2.2f;
                    AddLine(vh, new(r.center.x - island / 2f, y), new(r.center.x + island / 2f, y), stroke);
                    break;
                }
                case Kind.Bar:
                    AddQuad(vh, new(r.xMin, r.yMin), new(r.xMin, r.yMax), new(r.xMax, r.yMax), new(r.xMax, r.yMin));
                    break;
            }
        }

        static Vector2 P(Rect r, Vector2 unit, bool mirror) =>
            new(r.xMin + (mirror ? 1f - unit.x : unit.x) * r.width, r.yMin + unit.y * r.height);

        void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector4.zero);
            vh.AddVert(b, color, Vector4.zero);
            vh.AddVert(c, color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }

        void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector4.zero);
            vh.AddVert(b, color, Vector4.zero);
            vh.AddVert(c, color, Vector4.zero);
            vh.AddVert(d, color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        // A segment extended by half the stroke at both ends, so corners close.
        void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            Vector2 dir = (b - a).normalized, half = dir * (width / 2f), n = new Vector2(-dir.y, dir.x) * (width / 2f);
            a -= half;
            b += half;
            AddQuad(vh, a - n, a + n, b + n, b - n);
        }

        void AddCircle(VertexHelper vh, Vector2 center, float radius)
        {
            const int segments = 24;
            int c = vh.currentVertCount;
            vh.AddVert(center, color, Vector4.zero);
            for (int s = 0; s <= segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector4.zero);
                if (s > 0)
                    vh.AddTriangle(c, c + s, c + s + 1);
            }
        }
    }
}
