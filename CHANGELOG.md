# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
