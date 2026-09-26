// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
//
// iOS glue for HoloKitTrackingCore: Core Motion samples, the newest ARFrame,
// the display's presentation time, and the C API LowLatencyTrackingManager calls.
// Everything runs on Unity's main thread except Core Motion's queue and Metal's
// presented handlers, which only touch the locked buffers below.
#import <ARKit/ARKit.h>
#import <CoreMotion/CoreMotion.h>
#import <Metal/Metal.h>
#import <QuartzCore/QuartzCore.h>

#import "UnityAppController.h"
#import "PluginBase/RenderPluginDelegate.h"
#include "Unity/UnityRendering.h"
#include "Unity/DisplayManager.h"

#import "HoloKitARKitNativeProvider.h"
#include "HoloKitTrackingCore.h"

#include <algorithm>
#include <atomic>
#include <mutex>
#include <vector>

namespace {

struct Tracker {
    CMMotionManager* motion = nil;
    NSOperationQueue* queue = nil;
    std::mutex lock;                              // guards history and presentation
    std::vector<holokit::GyroSample> history;     // newest last, ~1.5 s
    holokit::KalmanPredictor kalman;
    holokit::PoseSmoother smoother;
    double lastFrameTimestamp = 0;
    std::atomic<double> renderStart{0};            // last markRenderStart (render thread reads it)
    double measuredPresentLatency = -1;            // smoothed, -1 until measured
    double lastIMUAge = -1;
    bool running = false;

    Tracker() { configureSmoother(); }

    void configureSmoother() {
        smoother.timeConstant = kalman.parameters.outputSmoothing;
        if (kalman.parameters.positionSmoothing > 0)
            smoother.positionTimeConstant = kalman.parameters.positionSmoothing;
        else
            smoother.positionTimeConstant.reset();
    }

    std::vector<holokit::GyroSample> samplesAfter(double time) {
        std::lock_guard<std::mutex> guard(lock);
        std::vector<holokit::GyroSample> result;
        for (const auto& sample : history)
            if (sample.timestamp > time) result.push_back(sample);
        return result;
    }

    double leadTime() {
        std::lock_guard<std::mutex> guard(lock);
        return measuredPresentLatency > 0 ? measuredPresentLatency + kalman.parameters.scanoutDelay
                                          : kalman.parameters.displayLatency;
    }
};

Tracker& tracker() {
    static Tracker instance;
    return instance;
}

}  // namespace

// MARK: - Presentation time

/// Adds a presented handler to the drawable Unity is about to present. The
/// frame's render start is the last HoloKit_Tracking_markRenderStart call.
@interface HoloKitPresentationObserver : RenderPluginDelegate
@end

@implementation HoloKitPresentationObserver
- (void)onFrameResolved {
    UnityDisplaySurfaceMTL* surface = (UnityDisplaySurfaceMTL*)GetMainDisplaySurface();
    if (!surface) return;
    id<CAMetalDrawable> drawable = surface->swapchain.drawable;
    double renderStart = tracker().renderStart.load();
    if (!drawable || renderStart <= 0) return;
    [drawable addPresentedHandler:^(id<MTLDrawable> presented) {
        double latency = presented.presentedTime - renderStart;
        if (presented.presentedTime <= 0 || latency <= 0 || latency >= 0.1) return;  // dropped
        Tracker& t = tracker();
        std::lock_guard<std::mutex> guard(t.lock);
        t.measuredPresentLatency = t.measuredPresentLatency > 0
            ? t.measuredPresentLatency + (latency - t.measuredPresentLatency) * 0.05 : latency;
    }];
}
@end

static void InstallPresentationObserver() {
    UnityAppController* app = GetAppController();
    if (!app) return;
    id current = app.renderDelegate;
    if ([current isKindOfClass:HoloKitPresentationObserver.class]) return;
    if ([current isKindOfClass:RenderPluginArrayDelegate.class]) {
        RenderPluginArrayDelegate* array = current;
        for (id delegate in array.delegateArray)
            if ([delegate isKindOfClass:HoloKitPresentationObserver.class]) return;
        array.delegateArray = [array.delegateArray arrayByAddingObject:[HoloKitPresentationObserver new]];
        return;
    }
    HoloKitPresentationObserver* observer = [HoloKitPresentationObserver new];
    if (current) {
        // Keep whatever delegate another plugin installed.
        RenderPluginArrayDelegate* array = [RenderPluginArrayDelegate new];
        array.delegateArray = @[current, observer];
        app.renderDelegate = array;
    } else {
        app.renderDelegate = observer;
    }
}

// MARK: - C API

extern "C" {

/// Starts Core Motion (device motion, 100 Hz on iPhone) and the presentation observer.
void HoloKit_Tracking_start(void) {
    Tracker& t = tracker();
    InstallPresentationObserver();
    if (t.running) return;
    t.running = true;
    if (!t.motion) {
        t.motion = [CMMotionManager new];
        t.queue = [NSOperationQueue new];
        t.queue.name = @"io.holokit.tracking.imu";
        t.queue.qualityOfService = NSQualityOfServiceUserInteractive;
        t.queue.maxConcurrentOperationCount = 1;
    }
    if (!t.motion.isDeviceMotionAvailable) return;
    t.motion.deviceMotionUpdateInterval = 1.0 / 200.0;
    [t.motion startDeviceMotionUpdatesToQueue:t.queue withHandler:^(CMDeviceMotion* data, NSError*) {
        if (!data) return;
        holokit::GyroSample sample;
        sample.timestamp = data.timestamp;
        sample.cameraAngularVelocity = holokit::cameraAxes(simd_make_float3(
            float(data.rotationRate.x), float(data.rotationRate.y), float(data.rotationRate.z)));
        // Core Motion reports user acceleration in g with the opposite sign of
        // the device's acceleration (verified against ARKit on device).
        sample.cameraAcceleration = holokit::cameraAxes(simd_make_float3(
            float(data.userAcceleration.x), float(data.userAcceleration.y),
            float(data.userAcceleration.z))) * -9.80665f;
        Tracker& tr = tracker();
        std::lock_guard<std::mutex> guard(tr.lock);
        tr.history.push_back(sample);
        double oldest = sample.timestamp - 1.5;
        auto keep = std::find_if(tr.history.begin(), tr.history.end(),
                                 [&](const holokit::GyroSample& s) { return s.timestamp >= oldest; });
        tr.history.erase(tr.history.begin(), keep);
    }];
}

void HoloKit_Tracking_stop(void) {
    Tracker& t = tracker();
    t.running = false;
    [t.motion stopDeviceMotionUpdates];
    std::lock_guard<std::mutex> guard(t.lock);
    t.history.clear();
    t.kalman.reset();
    t.smoother.reset();
    t.lastFrameTimestamp = 0;
}

/// Corrects the filter with the session's newest ARFrame (once per frame).
/// Returns false when there is no new, normally tracked frame.
bool HoloKit_Tracking_observeSession(void* nativeSession) {
    if (!nativeSession) return false;
    ARSession* session = (__bridge ARSession*)((UnityXRNativeSession*)nativeSession)->sessionPtr;
    ARFrame* frame = session.currentFrame;
    Tracker& t = tracker();
    if (!frame || frame.timestamp <= t.lastFrameTimestamp) return false;
    t.lastFrameTimestamp = frame.timestamp;
    if (frame.camera.trackingState != ARTrackingStateNormal) return false;
    t.kalman.observe(frame.timestamp, frame.camera.transform, t.samplesAfter(frame.timestamp - 0.3));
    return true;
}

/// Host time (CACurrentMediaTime, ARKit's clock) for this frame's render start.
double HoloKit_Tracking_markRenderStart(void) {
    double now = CACurrentMediaTime();
    tracker().renderStart.store(now);
    return now;
}

/// Predicts the camera pose at the moment this frame is seen, in Unity's
/// session space (left-handed). Returns false before the first ARKit frame.
bool HoloKit_Tracking_predict(double now, float* position, float* rotation) {
    Tracker& t = tracker();
    auto anchor = t.kalman.anchorTimestamp();
    if (!anchor) { t.smoother.reset(); return false; }
    double target = now + t.leadTime();
    auto prediction = t.kalman.predict(target, t.samplesAfter(*anchor - 0.05));
    if (!prediction) { t.smoother.reset(); return false; }
    t.lastIMUAge = prediction->newestIMUTimestamp ? now - *prediction->newestIMUTimestamp : -1;
    simd_float4x4 pose = t.smoother.smooth(prediction->transform, prediction->angularVelocity,
                                           prediction->velocity, target);
    simd_float3 p; simd_quatf q;
    holokit::unityPoseFromARKit(pose, &p, &q);
    position[0] = p.x; position[1] = p.y; position[2] = p.z;
    rotation[0] = q.vector.x; rotation[1] = q.vector.y; rotation[2] = q.vector.z; rotation[3] = q.vector.w;
    return true;
}

/// Smoothed render-start-to-presented time, or -1 before the first measurement.
double HoloKit_Tracking_presentLatency(void) {
    Tracker& t = tracker();
    std::lock_guard<std::mutex> guard(t.lock);
    return t.measuredPresentLatency;
}

/// How far past render start the pose is predicted, seconds.
double HoloKit_Tracking_leadTime(void) { return tracker().leadTime(); }

/// Seconds from the newest IMU sample used to render start, or -1.
double HoloKit_Tracking_imuSampleAge(void) { return tracker().lastIMUAge; }

float HoloKit_Tracking_lastInnovationDegrees(void) { return tracker().kalman.lastInnovationDegrees(); }

void HoloKit_Tracking_setSmoothing(double rotationSeconds, double positionSeconds) {
    Tracker& t = tracker();
    t.kalman.parameters.outputSmoothing = rotationSeconds;
    t.kalman.parameters.positionSmoothing = positionSeconds;
    t.configureSmoother();
}

}  // extern "C"
