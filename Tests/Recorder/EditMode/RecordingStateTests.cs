#if UNITY_IOS
using HoloKit;
using HoloKit.iOS;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class RecordingStateTests
{
    [Test]
    public void StopDrain_OutOfOrderCallbacksPreserveCaptureOrderAcrossSlotWrap()
    {
        var pending = new Queue<int>(new[] { 2, 0, 1 });
        var results = new sbyte[3];
        results[1] = 1;
        results[0] = 1;
        Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out _, out _), Is.False);
        Assert.That(pending.Count, Is.EqualTo(3));
        results[2] = 1;
        foreach (int expected in new[] { 2, 0, 1 })
        {
            Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out int index, out bool success), Is.True);
            Assert.That(index, Is.EqualTo(expected));
            Assert.That(success, Is.True);
        }
        Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out _, out _), Is.False);
    }

    [Test]
    public void StopDrain_FailedCopyDoesNotSubmitOrBlockLaterFrames()
    {
        var pending = new Queue<int>(new[] { 0, 1, 2 });
        var results = new sbyte[] { 1, -1, 0 };
        Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out _, out bool first), Is.True);
        Assert.That(first, Is.True);
        Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out int failedIndex, out bool failed), Is.True);
        Assert.That(failedIndex, Is.EqualTo(1));
        Assert.That(failed, Is.False);
        Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out _, out _), Is.False);
        Assert.That(pending.Count, Is.EqualTo(1));
        results[2] = 1;
        Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out int last, out bool success), Is.True);
        Assert.That(last, Is.EqualTo(2));
        Assert.That(success, Is.True);
        Assert.That(pending, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void StopDrain_AllCallbackOrdersAndFailuresPreserveFifo(int count)
    {
        int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        for (int head = 0; head < 3; head++)
        for (int failureMask = 0; failureMask < (1 << count); failureMask++)
        foreach (var order in orders)
        {
            var pending = new Queue<int>();
            for (int i = 0; i < count; i++) pending.Enqueue((head + i) % 3);
            var results = new sbyte[3];
            int drained = 0;
            Assert.That(HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out _, out _), Is.False);
            foreach (int callback in order)
            {
                if (callback >= count) continue;
                results[(head + callback) % 3] = (failureMask & (1 << callback)) == 0 ? (sbyte)1 : (sbyte)-1;
                while (HoloKitVideoRecorder.TryDequeueCompletedSlot(pending, results, out int index, out bool success))
                {
                    Assert.That(index, Is.EqualTo((head + drained) % 3));
                    Assert.That(success, Is.EqualTo((failureMask & (1 << drained)) == 0));
                    drained++;
                }
            }
            Assert.That(drained, Is.EqualTo(count));
            Assert.That(pending, Is.Empty);
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void CapturePacing_RenderJitterDoesNotLoseFrames(int frameRate)
    {
        double next = 0;
        for (int frame = 0; frame < frameRate * 10; frame++)
        {
            double jitter = frame == 0 ? 0 : (frame % 2 == 0 ? -0.001 : 0.001);
            Assert.That(HoloKitVideoRecorder.TryAdvanceCaptureTime(
                (double)frame / frameRate + jitter, 1.0 / frameRate, ref next), Is.True,
                $"Missed frame {frame} at {frameRate} fps");
        }
    }

    [TestCase(30)]
    [TestCase(60)]
    public void CapturePacing_FasterRendererStillHonorsTargetRate(int frameRate)
    {
        double next = 0;
        int captured = 0;
        for (int frame = 0; frame < 1200; frame++)
            if (HoloKitVideoRecorder.TryAdvanceCaptureTime(frame / 120.0, 1.0 / frameRate, ref next))
                captured++;
        Assert.That(captured, Is.InRange(frameRate * 10, frameRate * 10 + 1));
    }

    [TestCase(30)]
    [TestCase(60)]
    public void CapturePacing_LongStallSkipsBacklogWithoutBurst(int frameRate)
    {
        double next = 0, interval = 1.0 / frameRate;
        Assert.That(HoloKitVideoRecorder.TryAdvanceCaptureTime(0, interval, ref next), Is.True);
        Assert.That(HoloKitVideoRecorder.TryAdvanceCaptureTime(2, interval, ref next), Is.True);
        Assert.That(HoloKitVideoRecorder.TryAdvanceCaptureTime(2.001, interval, ref next), Is.False);
        Assert.That(HoloKitVideoRecorder.TryAdvanceCaptureTime(2 + interval, interval, ref next), Is.True);
    }

    [TestCase(9f / 16, 16f / 9)]
    [TestCase(16f / 9, 9f / 16)]
    [TestCase(3f / 4, 4f / 3)]
    [TestCase(4f / 3, 3f / 4)]
    [TestCase(9f / 16, 9f / 16)]
    [TestCase(16f / 9, 16f / 9)]
    public void RotateWhileRecording_PreservesAspectAndCentersImage(float source, float output)
    {
        Rect rect = HoloKitVideoRecorder.FitViewport(source, output);
        Assert.That(rect.width * output / rect.height, Is.EqualTo(source).Within(0.0001));
        Assert.That(rect.center, Is.EqualTo(new Vector2(.5f, .5f)));
        Assert.That(rect.width, Is.InRange(0.0001f, 1f));
        Assert.That(rect.height, Is.InRange(0.0001f, 1f));
        Assert.That(Mathf.Max(rect.width, rect.height), Is.EqualTo(1f));
    }

    [TestCase(false, ScreenRenderMode.Mono)]
    [TestCase(true, ScreenRenderMode.Mono)]
    [TestCase(false, ScreenRenderMode.Stereo)]
    [TestCase(true, ScreenRenderMode.Stereo)]
    public void FourK_StartSwitchModeStop_RestoresPreference(bool preferred, ScreenRenderMode initial)
    {
        var other = initial == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, true, initial, true), Is.True);
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, true, other, true), Is.True);
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, false, ScreenRenderMode.Mono, true), Is.EqualTo(preferred));
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, false, ScreenRenderMode.Stereo, true), Is.False);
    }

    [TestCase(ScreenRenderMode.Mono, false)]
    [TestCase(ScreenRenderMode.Mono, true)]
    [TestCase(ScreenRenderMode.Stereo, false)]
    [TestCase(ScreenRenderMode.Stereo, true)]
    public void FourK_OptOut_RespectsExistingModePreference(ScreenRenderMode mode, bool preferred)
    {
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, false, mode, false),
            Is.EqualTo(mode == ScreenRenderMode.Stereo || preferred));
    }

    [Test]
    public void OutputSize_MonoIsTheScreen_StereoIs16x9()
    {
        // iPhone 15 Pro landscape: the screen, rounded down to multiples of 16.
        Assert.That(HoloKitVideoRecorder.OutputSize(false, StereoRecordingResolution.UHD4K, 2556, 1179),
            Is.EqualTo(new Vector2Int(2544, 1168)));
        Assert.That(HoloKitVideoRecorder.OutputSize(false, StereoRecordingResolution.QHD2K, 1179, 2556),
            Is.EqualTo(new Vector2Int(1168, 2544)), "portrait Mono");
        Assert.That(HoloKitVideoRecorder.OutputSize(true, StereoRecordingResolution.QHD2K, 2556, 1179),
            Is.EqualTo(new Vector2Int(2560, 1440)), "Stereo 2K, as the Swift SDK");
        Assert.That(HoloKitVideoRecorder.OutputSize(true, StereoRecordingResolution.UHD4K, 2556, 1179),
            Is.EqualTo(new Vector2Int(3840, 2160)), "Stereo 4K, as the Swift SDK");
    }

    [TestCase(2556, 1179, 2560, 1440)]
    [TestCase(2556, 1179, 3840, 2160)]
    [TestCase(2796, 1290, 3840, 2160)]
    [TestCase(2048, 1536, 2560, 1440)]
    public void CoverSize_KeepsTheScreenAspectAndCoversTheMovie(int screenW, int screenH, int outW, int outH)
    {
        var cover = HoloKitVideoRecorder.CoverSize(screenW, screenH, outW, outH);
        Assert.That(cover.x, Is.GreaterThanOrEqualTo(outW));
        Assert.That(cover.y, Is.GreaterThanOrEqualTo(outH));
        Assert.That(cover.x == outW || cover.y == outH, "no larger than needed");
        Assert.That((float)cover.x / cover.y, Is.EqualTo((float)screenW / screenH).Within(0.002f), "undistorted");
    }

    [Test]
    public void CropToFill_CutsTheCentreAndFlips()
    {
        // Same aspect: the plain vertical flip the encoder always had.
        HoloKitVideoRecorder.CropToFill(16f / 9, 16f / 9, out var scale, out var offset);
        Assert.That(scale, Is.EqualTo(new Vector2(1, -1)));
        Assert.That(offset, Is.EqualTo(new Vector2(0, 1)));

        // A 19.5:9 camera render into a 16:9 movie: its centre columns, full height.
        float source = 2556f / 1179, output = 16f / 9;
        HoloKitVideoRecorder.CropToFill(source, output, out scale, out offset);
        float left = offset.x, right = offset.x + scale.x;
        Assert.That(right - left, Is.EqualTo(output / source).Within(1e-5f));
        Assert.That((left + right) / 2, Is.EqualTo(0.5f).Within(1e-5f), "centred");
        Assert.That(offset.y + scale.y * 1, Is.EqualTo(0).Within(1e-5f), "top row maps to the bottom (flip)");
        Assert.That(offset.y, Is.EqualTo(1).Within(1e-5f));

        // A 4:3 render into 16:9: full width, centre rows.
        HoloKitVideoRecorder.CropToFill(4f / 3, output, out scale, out offset);
        Assert.That(scale.x, Is.EqualTo(1));
        Assert.That(-scale.y, Is.EqualTo((4f / 3) / output).Within(1e-5f));
        Assert.That(offset.y + scale.y / 2, Is.EqualTo(0.5f).Within(1e-5f), "centred");
    }

    [Test]
    public void CaptureCameraIn4K_KeepsTheFormerRecordIn4KValue()
    {
        var field = typeof(HoloKitVideoRecorder).GetField("_captureCameraIn4K",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        var former = (UnityEngine.Serialization.FormerlySerializedAsAttribute)System.Attribute.GetCustomAttribute(
            field, typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute));
        Assert.That(former?.oldName, Is.EqualTo("_recordIn4K"), "scenes saved with RecordIn4K keep their choice");
    }
}
#endif
