// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HoloKit.UI.Tests
{
    /// The stereo bar's requirements, ported from the HoloKit Swift SDK's StereoControlsTests.
    public class HoloKitStereoBarTests
    {
        const string IOSPhoneModelListGuid = "97e080f80a8ae49d78e255ae53b17e11";
        static readonly string[] s_Phones = { "iPhone16,1", "iPhone14,2", "iPhone17,1" };

        static PhoneModel Phone(string model)
        {
            var list = AssetDatabase.LoadAssetAtPath<PhoneModelList>(AssetDatabase.GUIDToAssetPath(IOSPhoneModelListGuid));
            return list.PhoneModels.First(p => p.ModelName == model);
        }

        /// The layout HoloKitCameraManager computes for a real phone profile.
        static HoloKitStereoBarLayout LayoutFor(string model) => LayoutFor(model, out _, out _);

        static HoloKitStereoBarLayout LayoutFor(string model, out Rect left, out Rect right)
        {
            var phone = Phone(model);
            var go = new GameObject("Stereo Bar Test Camera");
            try
            {
                var manager = go.AddComponent<HoloKitCameraManager>();
                var so = new SerializedObject(manager);
                so.FindProperty("m_LeftEyeCamera").objectReferenceValue = new GameObject("Left").AddComponent<Camera>();
                so.FindProperty("m_RightEyeCamera").objectReferenceValue = new GameObject("Right").AddComponent<Camera>();
                so.FindProperty("m_CenterEyePose").objectReferenceValue = new GameObject("Center").transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                foreach (var t in new[] { "m_LeftEyeCamera", "m_RightEyeCamera" })
                    ((Camera)so.FindProperty(t).objectReferenceValue).transform.SetParent(go.transform);
                ((Transform)so.FindProperty("m_CenterEyePose").objectReferenceValue).SetParent(go.transform);
                manager.PhoneModel = phone;
                Assert.IsTrue(manager.TryGetStereoBarGeometry(out float px, out var layout), model);
                Assert.Greater(px, 0f);
                left = manager.LeftViewport;
                right = manager.RightViewport;
                return layout;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// Left to right, as in the HoloKit app: MORE, RECORD, MONO, marker, TRIGGER.
        [Test]
        public void BarOrderMatchesTheHoloKitApp()
        {
            var bar = LayoutFor("iPhone16,1");
            Assert.IsTrue(bar.MoreHitArea.Contains(bar.MoreLeading));
            Assert.Less(bar.MoreLeading, HoloKitStereoBarLayout.RecordTrailing - 30f);
            Assert.Less(HoloKitStereoBarLayout.RecordTrailing, bar.ModeHitArea.Min, "record must not overlap MONO");
            Assert.LessOrEqual(bar.ModeHitArea.Max, bar.MarkerX);
            Assert.Greater(bar.TriggerTrailing, bar.MarkerX, "TRIGGER's label sits right of the marker");
            Assert.IsTrue(bar.TriggerHitArea.Contains(bar.TriggerTrailing));
        }

        /// Every touch area spans its whole block: the areas tile the band edge to edge.
        [Test]
        public void HitAreasTileTheWholeBandEdgeToEdge()
        {
            foreach (var model in s_Phones)
            {
                var bar = LayoutFor(model);
                var areas = new[] { bar.MoreHitArea, bar.RecordHitArea, bar.ModeHitArea, bar.OptionsHitArea, bar.TriggerHitArea };
                Assert.AreEqual(-bar.HalfWidth, areas[0].Min, 0.001f, "from the left edge");
                Assert.AreEqual(bar.HalfWidth, areas[^1].Max, 0.001f, "to the right edge");
                for (int i = 1; i < areas.Length; i++)
                    Assert.AreEqual(areas[i - 1].Max, areas[i].Min, 0.001f, "no gap, no overlap");
                foreach (var area in areas)
                    Assert.GreaterOrEqual(area.Width, 12f, model);
                Assert.AreEqual(16f, bar.MoreHitArea.Width, 0.001f, model);
                Assert.AreEqual(20f, bar.TriggerHitArea.Width, 0.001f, model);
                Assert.Less(bar.ModeHitArea.Width, 30f, "MONO does not take the room MORE and TRIGGER need");
                Assert.IsTrue(bar.OptionsHitArea.Contains(HoloKitStereoBarLayout.OptionsLeading));
                Assert.IsTrue(bar.RecordHitArea.Contains(HoloKitStereoBarLayout.RecordTrailing));
                Assert.IsTrue(bar.ModeHitArea.Contains(HoloKitStereoBarLayout.ModeCenter));
            }
        }

        /// The band reaches down to the headset frame, deep enough to touch blind, and the
        /// labels hug the top edge.
        [Test]
        public void BandIsDeepAndLabelsSitAtTheTopEdge()
        {
            foreach (var model in s_Phones)
            {
                var bar = LayoutFor(model);
                Assert.Greater(bar.BandHeight, 8f, model);
                Assert.LessOrEqual(HoloKitStereoBarLayout.RowFromTop, 2.5f);
                Assert.Less(HoloKitStereoBarLayout.RowFromTop, bar.BandHeight / 2f, model);
            }
        }

        [Test]
        public void BandEndsTwoAndAHalfMillimetresAboveTheViewports()
        {
            // 460 dpi, 1179 px tall, viewports' top at 60 % of the height from the bottom.
            var bar = HoloKitStereoBarLayout.ForPhone(460f, 2556f, 1179f, 0.6f, 0.05075f);
            float mm = 25.4f / 460f;
            Assert.AreEqual(0.4f * 1179f * mm - 2.5f, bar.BandHeight, 0.001f);
            Assert.AreEqual(2556f / 2f * mm, bar.HalfWidth, 0.001f);
            Assert.AreEqual(50.75f, bar.MarkerX, 0.001f, "HoloKit X's notch");
        }

        /// The slides are long and end at their end bars; OPTIONS slides to the marker line.
        [Test]
        public void SlidesAreLongAndEndAtTheirEndBars()
        {
            foreach (var model in s_Phones)
            {
                var bar = LayoutFor(model);
                Assert.GreaterOrEqual(bar.RecordSlideLength, 25f, model);
                Assert.GreaterOrEqual(bar.OptionsSlideLength, 25f, model);
                Assert.AreEqual(bar.MarkerX, bar.OptionsEndBar, 1f, model);
                Assert.LessOrEqual(bar.OptionsEndBar, bar.TriggerHitArea.Min, "inside OPTIONS");
            }
        }

        [Test]
        public void SlideLeftToTheEndFiresOnce()
        {
            var track = new HoloKitSlideTrack(22f);
            Assert.IsFalse(track.Drag(-10f));
            Assert.AreEqual(-10f, track.Offset);
            Assert.IsTrue(track.Drag(-22f));
            Assert.IsFalse(track.Drag(-40f), "one start per slide");
            Assert.AreEqual(-22f, track.Offset, "clamped at the end bar");
            track.Release();
            Assert.AreEqual(0f, track.Offset);
            Assert.IsFalse(track.IsDragging);
            Assert.IsTrue(track.Drag(-30f), "a new slide fires again (stop)");
        }

        [Test]
        public void OnlyASlideToTheEndCounts()
        {
            float length = LayoutFor("iPhone16,1").RecordSlideLength;
            var track = new HoloKitSlideTrack(length);
            Assert.IsFalse(track.Drag(-(length - 0.5f)), "almost is not enough");
            track.Release();
            Assert.AreEqual(0f, track.Offset, "springs back");
            Assert.IsTrue(track.Drag(-length));
        }

        [Test]
        public void SlidesOnlyMoveTheirWay()
        {
            var left = new HoloKitSlideTrack(22f);
            Assert.IsFalse(left.Drag(50f));
            Assert.AreEqual(0f, left.Offset);
            var right = new HoloKitSlideTrack(22f, +1);
            Assert.IsFalse(right.Drag(-50f));
            Assert.IsTrue(right.Drag(22f), "OPTIONS slides right");
            Assert.AreEqual(22f, right.Offset);
        }

        /// Holding one control hides the others; after the OPTIONS panel closes the bar is whole again.
        [Test]
        public void ClosingOptionsShowsTheWholeBarAgain()
        {
            var press = new HoloKitBarPress();
            Assert.IsTrue(press.Press(HoloKitBarControl.Options), "sliding OPTIONS hides the others");
            Assert.IsFalse(press.IsShown(HoloKitBarControl.Record));
            Assert.IsTrue(press.IsShown(HoloKitBarControl.Options));
            Assert.IsTrue(press.PanelOpened(), "the panel replaces the drag");
            foreach (HoloKitBarControl control in System.Enum.GetValues(typeof(HoloKitBarControl)))
                Assert.IsTrue(press.IsShown(control), $"{control} is back after DONE");
            Assert.IsFalse(press.PanelOpened(), "nothing to forget twice");
        }

        [Test]
        public void PressFeedbackOutlastsAQuickTap()
        {
            Assert.IsTrue(HoloKitPressFlash.IsShown(false, 0.1f), "tap released at 0.1 s: still shown");
            Assert.IsTrue(HoloKitPressFlash.IsShown(false, 0.39f));
            Assert.IsFalse(HoloKitPressFlash.IsShown(false, 0.4f), "then it goes");
            Assert.IsTrue(HoloKitPressFlash.IsShown(true, 5f), "held: until release");
            Assert.IsFalse(HoloKitPressFlash.IsShown(false, null), "never pressed");
        }

        [Test]
        public void RecordingClockFormat()
        {
            Assert.AreEqual("00:00", HoloKitClock.Mmss(0));
            Assert.AreEqual("01:15", HoloKitClock.Mmss(75.9));
            Assert.AreEqual("00:00", HoloKitClock.Mmss(-3));
        }

        [Test]
        public void AllItemsAreTheFiveControls()
        {
            Assert.AreEqual(HoloKitStereoBarItems.RenderMode | HoloKitStereoBarItems.Record | HoloKitStereoBarItems.Trigger
                | HoloKitStereoBarItems.Info | HoloKitStereoBarItems.Options, HoloKitStereoBarItems.All);
        }

        /// The guide on the phone's native screen, as the Swift SDK's HoloKitAlignmentGuide.Geometry.
        static HoloKitAlignmentGuideGeometry GuideFor(string model, out HoloKitStereoBarLayout bar, out float px,
            out Rect left, out Rect right)
        {
            var specs = Phone(model).ModelSpecs;
            bar = LayoutFor(model, out left, out right);
            px = specs.ScreenDpi / 25.4f;
            return HoloKitAlignmentGuideGeometry.Compute(specs.ScreenResolution.x, specs.ScreenResolution.y, px,
                left.xMin, right.xMax, left.yMax, bar.MarkerX);
        }

        /// The outline is 1 mm wider than the viewports on each side, open at the bottom, and its top
        /// is 2.5 mm above them: the stereo bar's band ends exactly there.
        [Test]
        public void GuideFrameSurroundsTheViewportsAndMeetsTheBar()
        {
            foreach (var model in s_Phones)
            {
                var specs = Phone(model).ModelSpecs;
                var guide = GuideFor(model, out var bar, out float px, out var left, out var right);
                float w = specs.ScreenResolution.x, h = specs.ScreenResolution.y;
                Assert.AreEqual(left.xMin * w - px, guide.Left, 0.01f, model);
                Assert.AreEqual(right.xMax * w + px, guide.Right, 0.01f, model);
                Assert.AreEqual((1f - left.yMax) * h - 2.5f * px, guide.Top, 0.01f, model);
                Assert.AreEqual(h, guide.Bottom, 0.01f, "reaches the bottom edge");
                Assert.AreEqual(bar.BandHeight, guide.Top / px, 0.01f, "the bar's touch areas end at the outline");
            }
        }

        [Test]
        public void GuideMarkerSitsAtTheHeadsetNotch()
        {
            var specs = Phone("iPhone16,1").ModelSpecs;
            var guide = GuideFor("iPhone16,1", out _, out float px, out _, out _);
            Assert.AreEqual(specs.ScreenResolution.x / 2f + 50.75f * px, guide.MarkerX, 0.01f);
            Assert.Less(guide.MarkerX, guide.Right, "the marker meets the outline's top edge");
        }

        /// The rounded corners stay outside the viewports' corners on every phone.
        [Test]
        public void GuideCornersStayOutsideTheViewports()
        {
            foreach (var model in s_Phones)
            {
                var specs = Phone(model).ModelSpecs;
                var g = GuideFor(model, out _, out _, out var left, out var right);
                float w = specs.ScreenResolution.x, h = specs.ScreenResolution.y, r = g.CornerRadiusPixels;
                var corners = new[] { new Vector2(left.xMin * w, (1f - left.yMax) * h), new Vector2(right.xMax * w, (1f - right.yMax) * h) };
                var centres = new[] { new Vector2(g.Left + r, g.Top + r), new Vector2(g.Right - r, g.Top + r) };
                for (int i = 0; i < 2; i++)
                {
                    bool beyondArc = corners[i].y < centres[i].y && (i == 0 ? corners[i].x < centres[i].x : corners[i].x > centres[i].x);
                    Assert.IsTrue(!beyondArc || Vector2.Distance(corners[i], centres[i]) <= r, $"{model}: corner {i} cut by the arc");
                }
            }
        }

        /// Without a touch the guide hides after 10 s; a touch brings it back; 0 keeps it.
        [Test]
        public void GuideHidesAfterTenSecondsWithoutTouch()
        {
            var timer = new HoloKitChromeTimer(10f, 100f);
            Assert.IsTrue(timer.IsVisible(109.9f));
            Assert.IsFalse(timer.IsVisible(110f));
            timer.Touch(111f);
            Assert.IsTrue(timer.IsVisible(120f), "a touch brings it back");
            Assert.IsFalse(timer.IsVisible(121.1f));
            Assert.IsTrue(new HoloKitChromeTimer(0f, 0f).IsVisible(1e6f), "0 keeps it");
        }

        /// Serious caps Stereo at 40 fps, Critical at 30, as the Swift SDK.
        [Test]
        public void StereoFrameRateFollowsTheThermalState()
        {
            Assert.AreEqual(120, HoloKitCameraManager.StereoFrameRate(120, 0, true));
            Assert.AreEqual(120, HoloKitCameraManager.StereoFrameRate(120, 1, true));
            Assert.AreEqual(40, HoloKitCameraManager.StereoFrameRate(120, 2, true));
            Assert.AreEqual(30, HoloKitCameraManager.StereoFrameRate(120, 3, true));
            Assert.AreEqual(30, HoloKitCameraManager.StereoFrameRate(30, 2, true), "never above the maximum");
            Assert.AreEqual(120, HoloKitCameraManager.StereoFrameRate(120, 3, false), "off: the maximum");
        }
    }
}
