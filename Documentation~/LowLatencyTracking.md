# Low-latency tracking

In Stereo mode `LowLatencyTrackingManager` predicts the camera pose for the moment each
frame is seen, so world-locked content stays put while the head moves. It is added
automatically by `HoloKitCameraManager` (on "Stereo Tracked Pose") and needs no setup.

## Why prediction is needed

Measured on iPhone 15 Pro at 120 fps:

| Stage | Time |
| --- | --- |
| ARKit camera capture → ARFrame delivered to the app | 36–60 ms |
| ARFrame delivered → render start | 0–8 ms |
| Render start → `presentedTime` (frame on the display) | 17 ms |
| `presentedTime` → seen (panel scan-out, estimated) | ~4 ms |

Rendering the latest ARKit pose therefore shows where the head was 60–90 ms ago:
about 4° median error while turning, 10° above 120 °/s.

## How it works

The algorithm is the one in [holokit-swift-sdk](https://github.com/holokit/holokit-swift-sdk)
(`HoloKitKalmanPredictor`), ported line by line to C++:

1. **Kalman filter** (Cardboard-style sensor fusion with ARKit as the absolute
   measurement): an error-state EKF over orientation and gyro bias, and a Kalman filter
   over position and velocity. Core Motion device motion (100 Hz, ~1 ms delivery)
   drives it; every ARFrame corrects it at the frame's capture time.
2. **Prediction to the display time**: before each render the IMU samples received
   since the last ARFrame are integrated forward, and the last ~25 ms without sensor
   data are extrapolated with the measured angular acceleration. The target is not
   assumed: a render plugin delegate reads each frame's `CAMetalDrawable.presentedTime`,
   and the smoothed render-start-to-present time plus 4 ms scan-out sets the lead.
3. **Output smoothing**: the previous pose is carried forward with the IMU rate and
   only frame-to-frame jumps are low-passed (10 ms rotation, 30 ms position).

| File | Language | Role |
| --- | --- | --- |
| `Runtime/iOS/NativeCode/HoloKitTrackingCore.h/.cpp` | C++ (`<simd/simd.h>`) | Kalman filter, smoother, Unity ↔ ARKit conversion. No ARKit or Unity, so it runs in host tests. |
| `Runtime/iOS/NativeCode/HoloKitTracking.mm` | Objective-C++ | Core Motion, the session's newest ARFrame, `presentedTime` via `RenderPluginDelegate`, C API |
| `Runtime/LowLatencyTrackingManager.cs` | C# | Starts it in Stereo, corrects on `frameReceived`, applies the pose in `Application.onBeforeRender` |

The render delegate is chained with any delegate another plugin installed
(`RenderPluginArrayDelegate`).

## Accuracy

Orientation at the moment it is seen compared with where ARKit later reported the
camera (iPhone 15 Pro, wide head motion, ARKit 60 Hz; Swift SDK, same algorithm):

| | Median | p95 |
| --- | --- | --- |
| No prediction | 4.31° | 12.5° |
| Kalman prediction | **0.17°** | **0.80°** |
| Position, no prediction | 8.3 mm | 45.8 mm |
| Position, Kalman | **2.7 mm** | **9.1 mm** |

## Frame rate: required settings

Stereo needs a steady frame rate. `HoloKitCameraManager` sets `Application.targetFrameRate`
to 60 fps in Stereo by default (`m_StereoFrameRate`, 0 = 60, as in the Swift SDK; 120 is allowed on
ProMotion iPhones but heats the phone) and turns off
`ARSession.matchFrameRateRequested`, which otherwise blocks every frame until the next
ARKit frame (30 Hz on a warm phone). Both are restored in Mono.

One setting must be changed in the project:

**Player Settings > iOS > Other Settings > "Adjust iOS FPS based on thermal state": off**
(or raise "Serious" to 60+). With it on, Unity drops to 30 fps as soon as iOS reports a
serious thermal state, which a phone in HoloKit reaches within minutes: measured 30 fps
and 33 ms render-to-display, against 120 fps and 17 ms with it off.

HoloKit checks this for you: **Project Settings > XR Plug-in Management > Project
Validation** lists HoloKit's rules with a **Fix** button, and every build runs the same
rules (`Editor/HoloKitProjectValidation.cs`). The thermal frame-rate cap is an error: an iOS
build stops until it is fixed. The other rules log warnings:

| Rule | Why |
| --- | --- |
| Thermal FPS cap off (or Serious ≥ 60 fps) — **error** | Stereo drops to 30 fps when the phone warms up |
| Microphone Usage Description set | `HoloKitVideoRecorder` records the microphone |
| "Force iOS Speakers When Recording" on | With Unity's `Microphone` in use, game audio otherwise plays from the earpiece (measured, iOS 27) |
| Android: OpenGLES3 only, Auto Graphics API off | Required by the Android stereo path |

## Tuning

`LowLatencyTrackingManager` exposes `m_RotationSmoothing` and `m_PositionSmoothing`
(seconds) and read-only `IsPredicting`, `PresentLatency`, `PredictionLeadTime`,
`IMUSampleAge` and `LastCorrectionDegrees` for diagnostics.

## Tests

- `Tests/Tracking/Native~/tracking_core_tests.cpp` (run `Tests/Tracking/Tools~/run_native_tests.sh`
  on a Mac): 90 ms of rotation and accelerating translation bridged, gyro bias learned,
  extrapolation past the newest sample, relocalization reset, ARKit fallback without IMU,
  smoother, Unity ↔ ARKit conversion, and **parity with the Swift SDK**: a 6 s iPhone 15 Pro
  trace (`fixtures/`) replayed causally gives the same error statistics as the Swift
  SDK's `HoloKitPredictionBenchmark` to four decimals.
- `Tests/Tracking/EditMode/LowLatencyTrackingTests.cs`: runs the host tests from the Unity
  Test Runner on macOS, and checks the manager stays inactive outside the iOS player.
- `Tests/Validation/EditMode/ProjectValidationTests.cs`: each validation rule detects a bad
  setting and its Fix repairs it.
