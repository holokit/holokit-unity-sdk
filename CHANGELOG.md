# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.0] - Unreleased

Previews: 0.6.0-preview.1 (2026-10-12), 0.6.0-preview.2 (2026-10-27).

Requires Unity 6000.3 LTS or later, AR Foundation / ARKit 6.7.0.

### Added

- Stereo bar, ported from the HoloKit Swift SDK: HoloKit's standard controls along the top edge
  in Stereo, in the band above the headset frame and sized in millimetres for each phone.
  MORE (hold: fps, session and recording time, thermal state, `StereoInfo`, in the bar's bold
  italic), RECORD (slide left to start or stop), MONO (tap), OPTIONS (slide right: audio
  VOICE / GAME / BOTH, HANDS ON / OFF, CAMERA HD / 4K; closes after 4 s) and TRIGGER (hold:
  `HoloKitCameraManager.OnStereoTrigger`). Holding MORE, RECORD or OPTIONS hides the others and
  the alignment marker; TRIGGER leaves the bar as it is, so quick presses do not make it blink.
  iOS edge gestures are deferred while it shows. `HoloKitCameraManager.StereoBarItems` picks the
  controls, and `StereoTriggerLabel` / `StereoTriggerPressedLabel` rename TRIGGER. Off by default
  (None), so the Default UI Canvas keeps its Stereo / Mono button in Stereo; with controls, the
  canvas hides its buttons in Stereo. Stereo recordings never prompt for the microphone.
- Stereo alignment guide from the HoloKit app and Swift SDK: the outline of HoloKit X's phone
  opening (1 mm around the viewports, from 2.5 mm above them, open at the bottom) and a grey
  marker line at the notch, replacing the white marker. It hides after 10 s without a touch
  (`AlignmentGuideAutoHideDelay`), and the marker hides while a stereo bar control is held.
- Stereo frame rate follows the thermal state as in the Swift SDK: Serious caps it at 40 fps,
  Critical at 30 (`AdaptStereoFrameRateToThermalState`, on by default).
- `HandTrackingManager.FrameInterval` / `HandGestureRecognitionManager.FrameInterval`: Vision on
  every third AR frame (about 20 Hz) as in the Swift SDK; it ran on every frame.
- The device probe builds from the package (`HoloKit.Tests.Editor.HoloKitProbeBuild.Build`) and
  first copies `Samples~` into the carrier project (`HoloKitSampleSync`), so it never runs stale
  sample copies; it builds without the Unity splash screen. New `lowlatency` and `pinchtoplace`
  suites. Sample scenes re-saved through Unity so their files carry the new and renamed fields.
- Device probe `gesturevideo` suite: real first-person hands making gestures while they move (Pexels
  clips); the runner applies the SDK's gesture classifier and filter to Vision on the source frames
  and checks the phone's `HandGestureRecognitionManager` (gesture agreement, pinch-event recall and
  precision). The runner first checks that the camera sees the whole stimulus window and the phone
  is not thermally Serious, and stops with what to fix; failures caused by a touch (now blocked by an
  overlay) or by heat are reported as untested. Hand and camera suites run first, while the phone is
  cool. Hand stimuli are shown at arm's length, as a head-worn HoloKit sees hands (0.25-0.65 m):
  every photo and video hand spans about 85% of the window's height, and the preflight turns the
  window's size in the camera into the distance its hands look (it stops beyond 0.65 m and says how
  far to move the phone). The earlier stimuli looked like hands 0.8-1.5 m away, beyond reach, which
  made hand checks fail with the phone's position. The `handvideo` suite now uses the arm's-length
  clips too, and both video suites score only hands wholly inside the shown crop. The preflight
  also checks that Vision finds a hand photo and that nothing (e.g. a notification) covers the
  window, and the runner keeps the Mac's display awake. During a run the runner watches for a window
  in front of the stimulus (failures then are untested, naming the app), leaves out video frames where
  the phone tracked a real hand outside the video, and stops at once when the app goes to the
  background. The video suites' hands are not yet reliably detectable through the camera, so their
  results and W1/W2 thresholds are provisional (Tests/Probe/README.md); `handmotion` and `gesture`
  cover moving hands and gestures.
- Hand skeleton from the Swift SDK: `HandTrackingManager` (Hand Joints Visibility, on by default) shows
  the first hand as yellow joints and cyan bones (`HandSkeletonView`, topology in `HandSkeleton`), drawn
  over the real hand so AR occlusion never hides it. It replaces the white joint spheres, which the
  real hand's own depth covered with an `AROcclusionManager` in the scene; the joint objects stay as
  positioned markers.
- `FingerPinchDetector`: which finger the thumb pinches (the closest under `PinchRatio`), once per
  pinch after three matching observations; the **Pinch To Place** sample places a different object
  per finger where the thumb and finger meet.
- Hand tracking and gestures ported from the HoloKit Swift SDK. `HandGestureClassifier`: Five
  needs every finger straight (tip-to-base over length at least 0.82) whichever way the hand points
  (it required fingers pointing up in the image), and joints below 0.35 confidence are ignored;
  Pinched is thumb-to-fingertip below 0.25 of the hand's size (`PinchRatio`; the Swift SDK's
  fixed 0.12 image units called small, distant hands pinched), for any finger with `IsPinching`;
  `HandGestureFilter` holds a gesture until four matching observations. Joint depth
  (`HoloKitHandDepth`, host tests in `Tests/Hands`): a 3x3 consistent LiDAR depth within 10 cm of
  the hand's median depth, else the hand's depth remembered for 0.5 s, else a 0.65 m plane.
  `HandTrackingManager` smooths joints with a 1€ filter (`SmoothJoints`, `Smoothing`,
  `HandPoses3D`); Vision runs on a background queue, dropping frames while busy; the detector
  reports `HandChiralities`.
- Low-latency tracking rewritten in source: the Swift SDK's Kalman filter ported to C++
  (`HoloKitTrackingCore`) with Objective-C++ glue (`HoloKitTracking.mm`) replacing the closed
  `libHoloKitLowLatencyTracking.a` in `LowLatencyTrackingManager`. It fuses ARKit with Core
  Motion (gyro, gravity-free acceleration), predicts to the display time measured from
  `CAMetalDrawable.presentedTime` plus scan-out, and smooths frame-to-frame jumps. Host tests
  (`Tests/Tracking`) include parity with the Swift SDK on an iPhone 15 Pro trace. See
  `Documentation~/LowLatencyTracking.md`.
- Stereo mode renders at 60 fps by default, as the Swift SDK does: `HoloKitCameraManager` sets
  `targetFrameRate` (`m_StereoFrameRate`, 0 = 60; 120 on ProMotion if set, but the phone heats up)
  and turns off `ARSession.matchFrameRateRequested`, which otherwise blocks each frame on the next
  ARKit frame; both are restored in Mono.
- HoloKit Project Validation rules (Project Settings > XR Plug-in Management > Project
  Validation, with Fix buttons), also checked before every build: the iOS thermal frame-rate
  cap that drops stereo to 30 fps (an error: the build stops), the microphone usage
  description, "Force iOS Speakers When Recording" (game audio otherwise plays from the
  earpiece when Unity's Microphone is used), and Android's OpenGLES3-only graphics API.
- GPU video recording path for `HoloKitVideoRecorder`, with asynchronous stop and bounded GPU copies.
- Recording audio: separate AAC game and microphone tracks on a common host-clock timebase
  (`RecordingAudioMode` Environment / GameAndMicrophone), independent `GameGain` / `MicrophoneGain`,
  and an optional single-track mixed sharing copy. See `Documentation~/RecordingAudio.md`.
- `HoloKitMicrophoneCapture`: shared native microphone with owner-counted `Acquire`/`Release`,
  optional voice-processing echo cancellation, and `ReadLatestSamples`/`ReadSpectrum` for analysis.
- `HoloKitGameAudioTap` and a recorder Inspector.
- `MicrophoneProcessing.Default`: iOS default input processing with automatic gain (as Unity's
  `Microphone`), about 17 dB louder than raw `Environment` in a measured room. The recorder's
  Environment mode can use it (`EnvironmentProcessing`), so sound-reactive apps keep their thresholds.
- `MicrophoneProcessing.Voice`: voice processing with automatic gain (chat session mode), for apps that
  react to the user's voice. Under `Default`, speech 5 cm from the microphone still reads about
  -47 dBFS (iPhone 15 Pro, iOS 27), below typical sound-reactive thresholds.
- `HoloKitMicrophoneCapture.Position` (Automatic, Bottom, Front, Back): prefers one built-in
  microphone, omnidirectional where supported, under Environment and Default processing; under voice
  processing iOS owns the input.
- Standard `HoloKit Default UI Canvas` prefab (`Assets/Prefabs`) with render mode, record and 4K
  buttons; the GameObject > UI > HoloKit menu now places it. Every interactive sample uses it and has
  a `HoloKitVideoRecorder`.
- Recording Audio sample: Environment and Game And Microphone (AEC) recorder presets.
- Recorder regression tests (`Tests/Recorder`): EditMode tests, host-side native writer tests and
  opt-in device probes (`HOLOKIT_RECORDER_TESTS`) with artifact collectors.
- Phone models iPhone 13 Mini (`iPhone14,4`), iPhone 16e/17 series (`iPhone17,5`, `iPhone18,x`) and
  `iPhone19,x` entries; new models reuse previous-generation parameters until calibrated.

### Changed

- AR Foundation and ARKit XR Plugin 6.7.0 (were 6.4.3). ARKit XR Plugin 6.7.0 fixes AR content
  rotated 90 degrees in apps built with the iOS 27 SDK: it reads the interface orientation from
  `UIWindowScene`. A project that lists either package in its own manifest must raise it to 6.7.0.
- Removed the `-ld64` linker flag, which fails to link with Xcode 27.
- Gaze gesture interaction, default UI canvas and AR background video enhancement updated for the
  recorder and 4K handling.
- Recording video modes follow the HoloKit Swift SDK. Mono records the screen; Stereo records a
  16:9 movie at `HoloKitVideoRecorder.StereoResolution`, 2K (2560 × 1440, default) or 4K
  (3840 × 2160), cut from the centre of the AR camera view rendered at the screen's aspect (it was
  screen-sized). `RecordIn4K` is renamed `CaptureCameraIn4K` (serialized values kept): it only
  switches ARKit's camera feed to 4K, and the movie then runs at 30 fps. The Default UI Canvas
  button toggles it and reads HD60 / 4K30, the movie's frame rate with each camera feed; the
  stereo bar's OPTIONS has VIDEO RECORDING 2K / 4K and
  CAMERA HD / 4K.
- Renamed with `[Obsolete]` forwarders: `Utils` → `HoloKitScreenUtils` (`InchToMeter`,
  `MeterToInch`), `DeviceData.SupportLiDAR` → `DeviceData.SupportsLiDAR`.
- Folders without spaces: `Runtime/Interaction/Gaze`, `Runtime/iOS/Interaction/GazeGesture`.
  Types, namespaces and GUIDs are unchanged.
- One type per file: `ScreenRenderMode`, `MonoScreenOrientation`, `HoloKitGeneration`,
  `HoloKitModelSpecs`, `PhoneModelSpecs`, `PhoneModel`, `HandGesture`, `MaxHandCount`,
  `AppleThermalState`, `HoloKitBuildValidation`.
- Phone calibration guide and supported device lists moved to `Documentation~`.
- `LowLatencyTrackingManager.LastCorrectionDegrees` reports the last ARKit orientation correction.
- Head Interaction and Device Data samples listed in `package.json`.

### Removed

- The Apple Native Provider Test sample (never listed in package.json): the device probe's `device`
  suite checks the thermal state, its event and the uptime clock.
- `libHoloKitLowLatencyTracking.a`: the closed tracking library, replaced by the source in
  `Runtime/iOS/NativeCode/HoloKitTracking*`.
- `ENABLE_BITCODE` build settings (bitcode was removed in Xcode 14).
- Custom AR Background sample (empty scene) and three sample UI scripts no scene referenced.
- `ARKitCameraCompatibility` (0.6.0-preview.1), which replaced AR Foundation's camera pose, projection
  and background transform on iOS 27; ARKit XR Plugin 6.7.0 gets them right.

### Fixed

- `HoloKitMicrophoneCapture` stopped for good when the app called Unity's `Microphone.Start` after it
  started (iOS stops an `AVAudioEngine` when another component reconfigures audio input). It now
  rebuilds the engine on the new input format and keeps its consumers, up to three times in five
  seconds; it keeps the audio session as configured, since restoring it would stop the other recording.
- Hand Tracking, Pinch To Place and Glowing Orbs showed Unity's skybox instead of the camera image in
  Mono: `AROcclusionManager`'s `RequireComponent(Camera)` had added a default camera (Skybox, depth 0)
  beside it, which rendered after the AR camera (and could cover Stereo too). That camera is disabled;
  the occlusion manager still provides the LiDAR depth for 3D hand joints. EditMode `SampleSceneTests`
  fails if any camera draws over a sample's AR camera image.
- Hand joints ignored the XR Origin: they were placed in ARKit session space, so with the XR Origin's
  default Camera Y Offset (1.1176 m) the hand skeleton, pinch points and gestures sat 1.12 m below the
  real hand. `AppleVisionHandPoseDetector.SessionSpace` (set by `HandTrackingManager` to the XR
  Origin's camera offset) now takes them to world space, as the AR camera's pose is.
- GameObject > XR > HoloKit > HoloKit XR Origin left XROrigin's 1.1176 m Camera Y Offset in place; it
  now uses Floor and 0 m, as every sample does.
- Other apps' music (Apple Music, YouTube) stopped while the microphone ran. Environment and Default
  processing use Bluetooth A2DP instead of hands-free (HFP), and when other audio is playing the
  microphone deactivates the session with NotifyOthersOnDeactivation after starting and stopping, so
  that music resumes in 0.1-0.4 s and keeps playing (iPhone 15 Pro, iOS 27).
- Phone model lists failed to load when the package was installed from git or a tarball: they are
  now loaded by GUID instead of a `Packages/` path.

- Hand tracking (3D) could crash with `EXC_BAD_ACCESS` at address 0: Vision, depth and unprojection
  each re-read `ARSession.currentFrame` and could mix frames, including one without scene depth.
  Each call now uses one frame snapshot and clamps depth lookups to the depth map.
- Disabling AR background 4K right after enabling it (e.g. stopping a recording and switching render
  mode in the same frame) could leave the camera in 4K. The non-4K format is now captured once.
- Hand gesture recognition emitted each gesture and then `None` on the next frame; a gesture now
  persists until another has enough consecutive evidence.
- A one-frame hand-tracking dropout reset the gesture to `None` at once, so one pinch could become
  Pinched/None/Pinched and select a gazed object several times. The gesture now survives a 0.2 s dropout.
- Recorders without an assigned Microphone Input (all scenes created before 0.6.0, including the samples)
  failed to start. They now share the scene's `HoloKitMicrophoneCapture` or add one.
- Gaze Gesture Interaction sample had no `GazeRaycastInteractor`/`GazeGestureInteractor`, so nothing
  could be selected.
- Duplicate `iPhone14,5` entry in the iOS phone list; the iPhone 13 mini data is now `iPhone14,4`.

## [0.5.1] - 2024.9.21

### Added

- Added iPhone 16, iPhone 16 Plus, iPhone 16 Pro and iPhone 16 Pro Max to the list of supported devices.

## [0.5.0] - 2024.2.29

### Changed

- Refactored the package name from `com.holoi.xr.holokit` to `io.holokit.unity-sdk`
- Removed `LowLatencyTrackingManager_3DoF` and `3DoFTracking` scene.

## Known Issues

- Error message from 4K Video Recording. 
```
Video Writer pixelBufferPool is empty.
Cannot Append Video Frame at time 3.4967351
HoloKit.iOS.HoloKitVideoRecorder:OnSourceReadback(AsyncGPUReadbackRequest)
```

## [0.4.4] - 2024.1.5

### Changed

- Changed the structure of `HoloKit XR Origin` to support stable video recording in stereo mode.

## [0.4.3] - 2024.1.4

### Added

- Added `HoloKitARKitManager` script as a singleton class to manage a `HoloKitARKitNativeProvider` instance.

### Changed

- Changed `HoloKitARKitNativeProvider` to let it intercept Unity's ARSessionDelegate so that it can receive ARKit's ARSessionDelegate callbacks in C#.

## [0.4.2] - 2024.1.4

### Added

- Added `HoloKitARKitNativeProvider` script to control the ARSession in native environment.
- Added `WorldOriginResetter` script to reset the ARSession's world origin.
- Added Reset World Origin sample.
- Added `ARBackgroundVideoEnhancementManager` script to enable ARKit's 4K and HDR options.
- Added AR Background Video Enhancement sample.

## [0.4.1] - 2024.1.3

### Added

- Added `HoloKitVideoRecorder` as a default component on HoloKit Camera.
- Added record button on HoloKit default UI canvas.

## [0.4.0] - 2023.12.16

### Added

- Added Gaze Interaction sample
- Added Gaze Gesture Interaction sample
- Added Glowing Orbs sample

## [0.3.12] - 2023.10.28

### Added

- Added `SupportedMonoScreenOrientation` property in `HoloKitCameraManager` to reset the enabled auto rotate orientations after switching back to mono mode.

### Fixed

- Fixed stereo mono crashing due to Xcode 15 by adding `-ld64` to `Other Linker Flags` property in Xcode Build Settings of UnityFramework target.

## [0.3.11] - 2023.10.27

### Added

- Added AppleNativeProvider for getting iOS thermal state and SystemUptime.
- Added package dependency for ARKit.

### Changed

- Changed ARFoundation package dependency version from 5.0.6 to 5.1.0.

## [0.3.10] - 2023.10.26

### Added

- Added checking if the device supports LiDAR depth sensor.

## [0.3.9] - 2023.10.26

### Added

- Added support for iPhone 15, iPhone 15 Plus, iPhone 15 Pro and iPhone 15 Pro Max.
- Added gaze interaction components.
- Added gaze gesture interaction components.

## [0.3.8] - 2023.9.20

### Added

- Added `LowLatencyTrackingManager_3DoF` for 3DoF tracking.
- Added `3DoFTracking` sample scene.

### Removed

- Removed 3DoF tracking mode in `LowLatencyTrackingManager`. 

## [0.3.7] - 2023.9.7

### Changed

- Now the deprecated `ARPoseDriver` is also supported by `LowLatencyTrackingManager`.

## [0.3.6] - 2023.9.7

### Changed

- Script `HoloKitDefaultUICanvas` no longer locks the screen orientation.

## [0.3.5] - 2023.9.1

### Added

- Added 3DoF tracking mode for LowLatencyTrackingManager. This is only for internal use.
- Now when you create a HoloKitDefaultUICanvas, if there is no EventSystem in the scene, the SDK will create one for you.

## [0.3.4] - 2023.8.11

### Added

- Now you can add HoloKit DefaultUICanvas in the Unity Editor's Hierarchy Window

## [0.3.3] - 2023.8.10

### Added

- Now you can add HoloKit XROrigin in the Unity Editor's Hierarchy Window

## [0.3.2] - 2023.7.23

### Added

- HoloKit Unity SDK now supports Android devices
- Added PhoneModelSpecsCalibration sample
- Force screen brightness to 1.0 under Stereo mode on iOS

## [0.3.1] - 2023.7.4

### Added

- Added LowLatencyTrackingManager

### Changed

- Changed minimum compatible ARFoundation version to 5.+

### Removed

- Removed ARKit package dependency
