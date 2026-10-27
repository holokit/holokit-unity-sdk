// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using System;

namespace HoloKit.UI
{
    /// <summary>
    /// A horizontal span in millimetres from the screen centre.
    /// </summary>
    public readonly struct HoloKitBarRange
    {
        public readonly float Min, Max;
        public HoloKitBarRange(float min, float max) { Min = min; Max = max; }
        public float Width => Max - Min;
        public float Center => (Min + Max) / 2f;
        public bool Contains(float x) => x >= Min && x <= Max;
    }

    /// <summary>
    /// Where the stereo bar's parts go, in millimetres from the screen centre (x) and the
    /// screen's top edge (y), after the HoloKit app's Star AR panel. Same values as the
    /// HoloKit Swift SDK's <c>HoloKitStereoBarLayout</c>.
    /// </summary>
    public readonly struct HoloKitStereoBarLayout
    {
        /// Centre line of every label, almost at the screen's top edge (HoloKit X's top edge
        /// hides anything lower).
        public const float RowFromTop = 2.1f;
        /// MORE's label hugs the left: its leading edge is this far from the screen's left edge.
        public const float MoreInsetFromEdge = 6f;
        public const float RecordTrailing = -9f;
        public const float ModeCenter = 0f;
        /// MORE's and TRIGGER's touch areas, mm in from the screen edges; RECORD and OPTIONS
        /// fill the room between them and MONO.
        public const float MoreWidth = 16f;
        public const float TriggerWidth = 20f;
        /// Area boundaries around MONO, mm from the centre.
        public const float RecordModeBoundary = -6f;
        public const float ModeOptionsBoundary = 9f;
        /// OPTIONS' label starts here (the HoloKit app's Boost button position).
        public const float OptionsLeading = 10f;
        /// Right edge of OPTIONS' arrow at rest, mm from the centre.
        public const float OptionsArrowAtRest = 24f;
        /// Left edge of RECORD's arrow at rest, mm from the centre.
        public const float RecordArrowAtRest = -25f;
        /// TRIGGER's label hugs the right edge, mirroring MORE.
        public const float TriggerInsetFromEdge = 6f;
        /// Label size: 2 mm tall text, heavy italic.
        public const float LabelSize = 2f;

        /// Alignment marker, mm right of centre.
        public readonly float MarkerX;
        /// Half the screen width, mm.
        public readonly float HalfWidth;
        /// From the screen's top edge to the headset frame, mm.
        public readonly float BandHeight;

        /// The headset frame's top edge sits this far above the viewports (HoloKit app geometry).
        public const float FrameAboveViewports = 2.5f;

        public HoloKitStereoBarLayout(float markerX, float halfWidth, float bandHeight)
        {
            MarkerX = markerX;
            HalfWidth = halfWidth;
            BandHeight = bandHeight;
        }

        /// <summary>
        /// The layout for a phone in landscape: its DPI and native resolution (long side first),
        /// the viewports' top edge as a fraction of the screen height from the bottom (a stereo
        /// camera's <c>rect.yMax</c>), and the alignment marker's offset from the centre in metres.
        /// </summary>
        public static HoloKitStereoBarLayout ForPhone(float dpi, float nativeWidth, float nativeHeight,
            float viewportTop, float markerOffsetMetres)
        {
            float pixelsPerMillimetre = dpi / 25.4f;
            float aboveViewports = (1f - viewportTop) * nativeHeight / pixelsPerMillimetre;
            return new HoloKitStereoBarLayout(
                markerOffsetMetres * 1000f,
                nativeWidth / 2f / pixelsPerMillimetre,
                Math.Max(0f, aboveViewports - FrameAboveViewports));
        }

        // Hit areas tile the whole band above the headset frame, edge to edge and down to the
        // frame line: the phone is inside the headset and the wearer touches its top edge blind.
        public HoloKitBarRange MoreHitArea => new(-HalfWidth, -HalfWidth + MoreWidth);
        public HoloKitBarRange RecordHitArea => new(MoreHitArea.Max, RecordModeBoundary);
        public HoloKitBarRange ModeHitArea => new(RecordModeBoundary, ModeOptionsBoundary);
        public HoloKitBarRange OptionsHitArea => new(ModeOptionsBoundary, TriggerHitArea.Min);
        public HoloKitBarRange TriggerHitArea => new(HalfWidth - TriggerWidth, HalfWidth);

        /// Leftward travel that records: the arrow slides all the way to the end bar at MORE's
        /// boundary. Only a slide to the end counts (no flick), so a brush against the phone's
        /// top edge never starts or stops a recording.
        public float RecordSlideLength => RecordArrowAtRest - RecordHitArea.Min;
        /// OPTIONS' end bar sits on the alignment marker beside TRIGGER, or on TRIGGER's
        /// boundary where that is left of the marker.
        public float OptionsEndBar => Math.Min(MarkerX, OptionsHitArea.Max);
        /// Rightward travel that opens OPTIONS: the arrow slides to that end bar.
        public float OptionsSlideLength => OptionsEndBar - OptionsArrowAtRest;
        public float TriggerTrailing => HalfWidth - TriggerInsetFromEdge;
        public float MoreLeading => -HalfWidth + MoreInsetFromEdge;
    }

    /// <summary>
    /// Slide-to-activate: drag in <see cref="Direction"/> past <see cref="Length"/> to fire
    /// once; releasing resets. RECORD slides left, OPTIONS right.
    /// </summary>
    public sealed class HoloKitSlideTrack
    {
        public readonly float Length;
        /// -1 slides left, +1 right.
        public readonly int Direction;
        public float Offset { get; private set; }
        public bool IsDragging { get; private set; }
        bool m_Fired;

        public HoloKitSlideTrack(float length, int direction = -1)
        {
            Length = length;
            Direction = direction < 0 ? -1 : 1;
        }

        /// Follows a horizontal drag (negative is left). Returns true exactly once per drag,
        /// when the end is reached.
        public bool Drag(float translation)
        {
            IsDragging = true;
            float travel = Math.Max(0f, Math.Min(Length, translation * Direction));
            Offset = travel * Direction;
            if (travel < Length || m_Fired)
                return false;
            m_Fired = true;
            return true;
        }

        public void Release()
        {
            IsDragging = false;
            m_Fired = false;
            Offset = 0f;
        }
    }

    public enum HoloKitBarControl { More, Record, Mono, Options, Trigger }

    /// <summary>
    /// Which stereo control is held. As in the HoloKit app, holding one hides the others
    /// (and the alignment marker); TRIGGER is never held here, so it leaves the bar as it is.
    /// </summary>
    public sealed class HoloKitBarPress
    {
        public HoloKitBarControl? Held { get; private set; }

        /// Returns true when the held state changed.
        public bool Press(HoloKitBarControl? control)
        {
            if (Held == control)
                return false;
            Held = control;
            return true;
        }

        public bool IsShown(HoloKitBarControl control) => Held == null || Held == control;

        /// A panel (OPTIONS) replaced the bar mid-drag: that drag never ends, so forget it.
        public bool PanelOpened() => Press(null);
    }

    /// <summary>
    /// Press feedback lasts at least <see cref="Minimum"/> seconds: a quick tap is ~0.1 s,
    /// too short to see; a longer hold lasts until release.
    /// </summary>
    public static class HoloKitPressFlash
    {
        public const float Minimum = 0.4f;

        public static bool IsShown(bool held, float? sinceLastPress) =>
            held || (sinceLastPress.HasValue && sinceLastPress.Value < Minimum);
    }

    public static class HoloKitClock
    {
        public static string Mmss(double seconds)
        {
            int total = Math.Max(0, (int)seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}

namespace HoloKit.UI
{
    /// <summary>
    /// The stereo alignment guide in screen pixels (origin at the screen's top-left), as the HoloKit
    /// app and Swift SDK draw it: the outline of HoloKit X's phone opening around both viewports,
    /// 1 mm wider on each side, from 2.5 mm above them down to the screen's bottom edge (open at the
    /// bottom), and the marker line from the top edge down to the outline, at the headset's notch.
    /// </summary>
    public readonly struct HoloKitAlignmentGuideGeometry
    {
        /// Tolerance on each side of the viewports, mm: drawn exactly at their edges, the outline
        /// showed through the lenses at the slightest misplacement.
        public const float SideTolerance = 1f;
        /// Corner radius, mm: small enough to stay outside the viewports' corners.
        public const float CornerRadius = 3f;
        /// Line width in pixels, as in the HoloKit app.
        public const float Hairline = 3f;

        public readonly float Left, Right, Top, Bottom;
        public readonly float MarkerX;
        public readonly float CornerRadiusPixels;

        HoloKitAlignmentGuideGeometry(float left, float right, float top, float bottom, float markerX, float radius)
        {
            Left = left; Right = right; Top = top; Bottom = bottom; MarkerX = markerX; CornerRadiusPixels = radius;
        }

        /// <param name="screenWidth">Screen width in pixels (landscape).</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <param name="pixelsPerMillimetre">Screen pixels per millimetre.</param>
        /// <param name="viewportLeft">Left viewport's left edge, 0–1 of the width.</param>
        /// <param name="viewportRight">Right viewport's right edge, 0–1 of the width.</param>
        /// <param name="viewportTop">Viewports' top edge, 0–1 of the height from the bottom (camera rect yMax).</param>
        /// <param name="markerX">Alignment marker, mm right of the centre.</param>
        public static HoloKitAlignmentGuideGeometry Compute(float screenWidth, float screenHeight, float pixelsPerMillimetre,
            float viewportLeft, float viewportRight, float viewportTop, float markerX)
        {
            float side = SideTolerance * pixelsPerMillimetre;
            float top = (1f - viewportTop) * screenHeight - HoloKitStereoBarLayout.FrameAboveViewports * pixelsPerMillimetre;
            return new HoloKitAlignmentGuideGeometry(
                viewportLeft * screenWidth - side,
                viewportRight * screenWidth + side,
                Math.Max(0f, top),
                screenHeight,
                screenWidth / 2f + markerX * pixelsPerMillimetre,
                CornerRadius * pixelsPerMillimetre);
        }
    }

    /// <summary>
    /// The stereo alignment guide hides after <see cref="Delay"/> seconds without a touch; a touch
    /// brings it back. The controls stay. A delay of 0 keeps it.
    /// </summary>
    public sealed class HoloKitChromeTimer
    {
        public float Delay;
        public float LastTouch { get; private set; }

        public HoloKitChromeTimer(float delay, float now)
        {
            Delay = delay;
            LastTouch = now;
        }

        public void Touch(float time) => LastTouch = Math.Max(LastTouch, time);

        public bool IsVisible(float time) => Delay <= 0f || time - LastTouch < Delay;
    }
}
