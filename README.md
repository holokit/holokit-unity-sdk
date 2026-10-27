# HoloKit Unity SDK

[HoloKit](https://holokit.io/) is an optical see-through mixed reality headset that transforms your iPhone into a powerful stereoscopic AR device. With the HoloKit Unity SDK, developers can create immersive AR experiences in Unity, which can be viewed with HoloKit headset.

The SDK provides:

- **Stereoscopic rendering** for the HoloKit headset, switchable at runtime with Mono (screen AR)
- **Low-latency tracking**: in Stereo, the camera pose is predicted with a Kalman filter over the IMU to the moment each frame is on the display (see [Low-Latency Tracking](#low-latency-tracking))
- **Video recording** in Mono and Stereo, with microphone and game audio (see [Recording Audio](Documentation~/RecordingAudio.md))
- **Hand tracking and hand gesture recognition** with the [Apple Vision framework](https://developer.apple.com/documentation/vision) and the LiDAR depth sensor
- **Gaze and gaze-gesture interaction**

HoloKit Unity SDK is built on AR Foundation, so AR Foundation features such as image tracking and plane detection keep working. Upgrading an AR Foundation project to stereoscopic AR means replacing the XR Origin with the HoloKit XR Origin.

## How to Install

You can install HoloKit Unity SDK from the following git URL in Package Manager:
```
https://github.com/holokit/holokit-unity-sdk.git
```
<img width="279" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/a82656c9-aa73-4158-83b5-20d5178c8a81">

or by directly adding the following line to the `Packages/manifest.json` file.
```
"io.holokit.unity-sdk": "https://github.com/holokit/holokit-unity-sdk.git"
```

## Supported Software Versions

HoloKit Unity SDK has been tested and found compatible with the following software versions:

### Unity
- Unity 6000.3 LTS or higher

### ARFoundation
- ARFoundation and ARKit XR Plugin 6.7.0 (fixes AR content rotated 90° in apps built with the iOS 27 SDK)

We aim to continually test and verify compatibility with newer versions of these softwares.

## Phone Compatibility

### iOS
- iOS 27.0 or higher (Player Settings > Target minimum iOS Version 27.0; Project Validation checks it and blocks older targets)

HoloKit was primarily built for iOS devices. Please see the [Supported iOS Devices](Documentation~/Supported_iOS_Devices.md) to check if your iPhone is supported.

### Android

HoloKit currently only supports a limited number of Android phones. Please see the [Supported Android Devices](Documentation~/Supported_Android_Devices.md) for more details.

If your Android phone is not supported, we also provide a calibration method to let you figure out the specs of your Android phone yourself. You can then enter your Android phone specs to the SDK to have your device rendering stereo images. Please see [Phone Calibration Guide](Documentation~/Phone_Calibration_Guide.md), which is a step-by-step guide on how to figure out the specs of your phone model.

## Project Environment Setup

Before using the HoloKit Unity SDK, ensure to adjust the project settings to meet ARFoundation's requirements. If you are already comfortable with ARFoundation, feel free to skip this section.

Upon successfully installation of the SDK package, the ARFoundation package will be installed automatically. Please note that the HoloKit Unity SDK is only compatible with ARFoundation 6.+. 

For iOS-targeted projects, install the `Apple ARKit XR Plugin` package. For Android-targeted projects, the `Google ARCore XR Plugin` is required.

### iOS Project Settings

1. **Set Camera and Microphone Usage Descriptions**: Navigate to `Project Settings > Player > Other Settings > Configuration` and provide a user-friendly text for `Camera Usage Description` and `Microphone usage description`. The `Camera Usage Description` will pop up when the iPhone requests access to the camera. The `Microphone usage description` is required because the video recording feature of the SDK will access the microphone to record audio.

<img width="722" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/848a1bf3-b877-48a0-8430-92d2d448c7a5">

2. **Enable Apple ARKit**: Under `Project Settings > XR Plug-in Management > Plug-in Providers`, enable the `Apple ARKit` option.

<img width="600" alt="Enable Apple ARKit" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/ab5f3554-9530-464d-a336-12957a857192">

### Android Project Settings

1. **Disable Auto Graphics API**: Navigate to `Project Settings > Player > Other Settings > Rendering` and disable `Auto Graphics API`. In the `Graphics APIs` list, remove `Vulkan` and ensure only `OpenGLES3` is listed.

<img width="500" alt="Graphics APIs" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/4f66611d-d912-4fe8-b4d9-ca1a08e15a5d">

2. **Set Minimum API Level**: Proceed to `Project Settings > Player > Other Settings > Identification` and set `Minimum API Level` to `Android 7.0 'Nougat' (API level 24)` or higher.

<img width="500" alt="Minimum API Level" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/fb16a187-651c-4ace-b266-77f2e18f67dc">

3. **Configure Scripting Backend**: In `Project Settings > Player > Other Settings > Configuration`, set `Scripting Backend` to `IL2CPP`. Under `Target Architectures`, uncheck `ARMv7` and check `ARM64`.

<img width="500" alt="Configuration" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/049fc7a9-e8a2-4b21-8e1c-cf241df6aa3c">

4. **Enable Google ARCore**: Under `Project Settings > XR Plug-in Management > Plug-in Providers`, enable the `Google ARCore` option.

<img width="600" alt="Enable Google ARCore" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/daccfe62-c90e-4ed3-8fcc-218bcae59a31">

## Core Samples

The SDK package offers a sample for each feature (Package Manager > HoloKit Unity SDK > Samples). Import these samples into your project and deploy them to mobile devices for a quick overview of each feature. These samples can also be used as templates for your projects.

### Stereoscopic Rendering

Stereoscopic rendering is the core feature of the SDK, your app needs to display stereo images on the phone screen so that it can be viewed with the HoloKit headset.

The SDK provides two rendering modes: mono and stereo. Mono mode renders the background camera image with virtual content layered on top, akin to a standard ARFoundation project. In contrast, stereo mode renders two stereo images on a black background on the phone's screen, so that you can insert your phone onto the HoloKit headset to have stereoscopic AR experience.

| <img src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/a9d048a0-b2b5-4f0e-a7f8-03ca782b9725" alt="ScreenRenderMode.Mono" width="450"/> | <img src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/01bf3231-41ec-4a29-9d94-a48dcebfeefb" alt="ScreenRenderMode.Stereo" width="450"/> |
|:---:|:---:|
| Mono rendering mode | Stereo rendering mode |

To integrate stereoscopic rendering into your project, the `HoloKitCameraManager` component is required. In fact, `HoloKitCameraManager` is the only mandatory component in the SDK that you must add into the scene. Instead of adding a default ARFoundation `XR Origin` object, we add a `HoloKit XR Origin` object into the scene by right-clicking in Unity Editor's hierarchy window and selecting `XR/HoloKit/HoloKit XR Origin`. The `HoloKit XR Origin` contains a `HoloKit Camera` object with the `HoloKitCameraManager` script, which enables both mono and stereo rendering modes.

<img width="903" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/a5d5a3d8-c600-405f-8743-a5266d024368">

When you start the game, `HoloKitCameraManager` initially enters mono mode. A UI button is required to transition between rendering modes. The current rendering mode can be retrieved and set with `HoloKitCameraManager`. The following code snippet illustrates how to switch the rendering mode.

```csharp
// A reference to the HoloKitCameraManager component is required
[SerializeField] HoloKitCameraManager m_HoloKitCameraManager;

public void SwitchRenderMode()
{
    // Get the current ScreenRenderMode value and assign a new value
    m_HoloKitCameraManager.ScreenRenderMode = m_HoloKitCameraManager.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
}
```

When the stereo mode is activated, the SDK shows the alignment guide, as in the HoloKit app: the outline of the headset's phone opening around both viewports and a marker line at the headset's notch. Seat the phone so the line sits on the notch and the outline fits the opening. The guide hides after 10 seconds without a touch (**Alignment Guide Auto Hide Delay**; 0 keeps it) and comes back on a touch; **Show Alignment Marker In Stereo Mode** turns it off.

As the phone heats up, stereo mode lowers its frame rate: 40 fps at the Serious thermal state and 30 fps at Critical, as the HoloKit Swift SDK does (**Adapt Stereo Frame Rate To Thermal State**, on by default).

By default the Default UI Canvas keeps its Stereo / Mono button in stereo mode. Set **Stereo Bar Items** to show the stereo bar instead: HoloKit's controls along the screen's top edge, in the band above the headset frame, as in the HoloKit app. The Default UI Canvas then hides its buttons in stereo mode. The wearer touches the phone's top edge blind, so every control's touch area runs down to the frame:

| Control | Gesture | Action |
|---|---|---|
| MORE | hold | Frame rate, session time, recording time, thermal state, and your `StereoInfo` lines |
| RECORD | slide left to the end | Start recording; slide again to stop (iOS, needs a `HoloKitVideoRecorder`) |
| MONO | tap | Back to mono mode |
| OPTIONS | slide right to the end | VIDEO RECORDING 2K / 4K, AUDIO VOICE / GAME / BOTH, HANDS ON / OFF, CAMERA HD / 4K |
| TRIGGER | hold | Raises `HoloKitCameraManager.OnStereoTrigger`, a button every app can use in stereo |

```csharp
m_HoloKitCameraManager.OnStereoTrigger += () => Fire();
m_HoloKitCameraManager.StereoInfo = new[] { $"anchors {anchorCount}" };
```

Configure the bar on `HoloKitCameraManager`:

| Setting | Code | Default |
|---|---|---|
| Stereo Bar Items: which of MORE, RECORD, MONO, OPTIONS and TRIGGER to show | `StereoBarItems` | none (`HoloKitStereoBarItems.All` for all five) |
| Stereo Trigger Label | `StereoTriggerLabel` | `TRIGGER` |
| Stereo Trigger Pressed Label | `StereoTriggerPressedLabel` | `TRIGGERED` |

```csharp
m_HoloKitCameraManager.StereoBarItems = HoloKitStereoBarItems.RenderMode | HoloKitStereoBarItems.Trigger;
m_HoloKitCameraManager.StereoTriggerLabel = "FIRE";
m_HoloKitCameraManager.StereoTriggerPressedLabel = "FIRED";
```

Recordings follow the HoloKit Swift SDK: mono mode records the screen; stereo mode records a 16:9 movie from the AR camera view at `HoloKitVideoRecorder.StereoResolution`, 2K (2560 × 1440, default) or 4K (3840 × 2160). `CaptureCameraIn4K` switches ARKit's camera feed to 4K while recording, so the real scene is not upscaled; the movie then runs at 30 fps.

Keep the TRIGGER labels short: they sit in a 20 mm touch area. With *Nothing* selected the bar is hidden, and then your app needs its own way back to mono mode. The bar never asks for microphone permission, because an alert in the headset is invisible and blocks every touch: until the app has the permission, stereo recordings from the bar carry game audio only.

### Hand Tracking

The SDK tracks the user's hand, providing the 3D positions of the [21 hand joints](https://developer.apple.com/documentation/vision/vnhumanhandposeobservationjointname?language=objc) of each hand.

To incorporate the hand tracking into your project, create an empty GameObject and add `HandTrackingManager` component to it. The script will automatically setup all necessary objects as child GameObjects.

<img src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/54c5dc5e-ca2b-4cac-b132-d1aab5f47fa0" alt="HandTrackingManager" width="800"/>

`HandTrackingManager` requires depth information to calculate 3D hand positions, so add `AROcclusionManager` component to the `HoloKit Camera` GameObject and adjust its settings accordingly.

<img width="903" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/e38f3c36-a673-46ec-a41c-339572fc11a5">

You can now build the project onto an iPhone to view the results.

<img src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/aba052ed-6433-41ba-9329-a6219364f7ea" alt="Hand Tracking Result" width="600"/>

With **Hand Joints Visibility** on (the default), the first hand shows as the Swift SDK's skeleton: yellow joints and cyan bones, drawn over the real hand so AR occlusion never hides it.

`HandTrackingManager` can be configured to track either one hand or both hands. As hand tracking is energy-intensive, if dual hand tracking isn't necessary, we advise tracking only one hand to conserve energy. Set `HandTrackingManager.MaxHandCount` in its inspector within the Unity editor.

To obtain specific hand joint positions in code, use `HandTrackingManager.GetHandJointPosition(int handIndex, JointName jointName)` or `HandTrackingManager.HandPoses3D`. The parameter `handIndex` is the hand detection sequence; `HandPoseDetector.HandChiralities[handIndex]` tells left from right (iOS 15 and later). When only one hand is detected, `handIndex` should always be 0. If two hands are detected, `handIndex` can be either 0 or 1. The number of detected hands can be accessed via `HandTrackingManager.HandCount`.

Hand tracking follows the HoloKit Swift SDK. Vision runs on a background queue, so the main thread never waits for it, and on every third AR frame (**Frame Interval**, about 20 Hz): enough for gestures at a third of the cost. Each joint takes its own LiDAR depth only when the 3x3 neighbourhood agrees and it lies within 10 cm of the hand's median depth; otherwise it takes the hand's depth (remembered for 0.5 s per hand), else a 0.65 m plane. So a fingertip over the background no longer jumps to the wall. Joint world positions are smoothed with a 1€ filter (**Smooth Joints**, **Smoothing**); `HandPoseDetector.HandPoses3D` keeps the raw ones.

Please note, the hand tracking feature needs a device with a LiDAR depth sensor.

### Hand Gesture Recognition

The SDK can also recognizes user's hand gesture, which can serve as input triggers in your project. The gestures are `HandGesture.None`, `HandGesture.Pinched` (thumb tip on index tip, closer than a quarter of the hand's size, so a hand far from the camera is not pinched just for being small) and `HandGesture.Five` (an open hand). Classification follows the HoloKit Swift SDK (`HandGestureClassifier`): joints Vision is unsure of (confidence below 0.35) are ignored, and Five needs every finger straight whichever way the hand points, so a tilted or sideways open hand counts. A gesture changes after four matching observations and survives a tracking dropout shorter than 0.2 s. `HandGestureClassifier.IsPinching(joints, confidences, JointName.MiddleTip)` asks the same for the middle, ring or little finger.

<img src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/0fe0010f-7854-41ad-b9b8-95ee8039fb4c" alt="Hand Gesture Recognition" width="600"/>

To utilize hand gesture recognition, add `HandGestureRecognitionManager` to the scene. The callback function `HandGestureRecognitionManager.OnHandGestureChanged` is invoked when a hand gesture transition is detected. Register this callback to receive notifications. You can also access the user's current hand gesture via `HandGestureRecognitionManager.HandGesture`. The following code snippet illustrates how to capture user hand gesture transitions.

```
[SerializeField] HandGestureRecognitionManager m_HandGestureRecognitionManager;

private void Start()
{
    // Register the callback
    m_HandGestureRecognitionManager.OnHandGestureChanged += OnHandGestureChanged;
}

private void OnHandGestureChanged(HandGesture handGesture)
{
    // Execute desired action
}
```

Hand tracking and gesture recognition use Apple Vision (iOS 14 and later; handedness from iOS 15).

You can use hand tracking and hand gesture recognition at the same time, simply add both `HandTrackingManager` and `HandGestureRecognitionManager` components to your scene.

`FingerPinchDetector` tells which finger the thumb pinches (index, middle, ring or little), once per pinch. The **Pinch To Place** sample places an object wherever you pinch, a different one per finger:

```csharp
readonly FingerPinchDetector m_Pinch = new();

void OnHandPoseUpdated()   // HandTrackingManager.HandPoseDetector.OnHandPoseUpdated
{
    var detector = m_HandTracking.HandPoseDetector;
    if (m_Pinch.Update(detector.HandPoses2D[0], detector.HandPosesConfidence[0], out var began, out _) &&
        began != PinchFinger.None)
    {
        var world = m_HandTracking.HandPoses3D[0];
        Place(began, (world[JointName.ThumbTip] + world[FingerPinchDetector.TipOf(began)]) / 2);
    }
}
```

## Quick Scene Setup

HoloKit Unity SDK is based on ARFoundation. For a minimal ARFoundation scene requires two primary objects: an `AR Session` object and an `XR Origin` object with the Main Camera Object as its child.

<img width="377" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/feeb658f-ec65-4461-bf5a-e42fc423cafa">

To enable stereoscopic rendering, we need to upgrade the default Main Camera to a HoloKit Camera. To create an `XR Origin` with a HoloKit Camera as its child, right-click in the Unity Editor and select `XR/HoloKit/HoloKit XR Origin` to create a `HoloKit XR Origin`.

<img width="660" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/ad56a592-147f-4969-9942-d3445bf038f6">

The `HoloKit XR Origin` object includes a HoloKit Camera capable of both screen AR and stereoscopic rendering.

<img width="355" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/cdd3b0fa-6d69-45eb-9a8b-f129ce14df2a">

To switch between the rendering modes, a UI button is necessary. To create a default UI panel which includes this mode-switching button, right-click and navigate to `UI/HoloKit/HoloKit Default UI Canvas`.

<img width="690" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/d3c5a561-43c2-48b4-9f24-cbb770b3e773">

The default canvas also has a button for controlling video recording. In stereo mode it hides its buttons, and the stereo bar takes over.

<img width="374" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/6daa7737-49e6-4403-9f95-a1eedd88506d">

After setting up the `AR Session`, `HoloKit XROrigin`, and UI components, you can add any 3D object to the scene, such as a cube. When you build your project onto an iPhone, you'll be able to view your object in both screen AR mode and stereoscopic rendering mode with a HoloKit headset.

## Advanced Features

### Low-Latency Tracking

In Stereo mode the SDK predicts the camera pose for the moment each frame is seen. A native
Kalman filter (the same algorithm as the HoloKit Swift SDK) fuses every ARKit frame with the
gyroscope and accelerometer at 100 Hz, and predicts to the display time measured from each
frame's Metal `presentedTime`. On iPhone 15 Pro the orientation you see is 0.17° (median) from
where the head is, against about 4° without prediction. It is on by default in Stereo mode.

Turn off **Player Settings > iOS > "Adjust iOS FPS based on thermal state"**: with it on,
Unity drops stereo rendering to 30 fps once the phone warms up. HoloKit's rules under
**Project Settings > XR Plug-in Management > Project Validation** flag this (with a Fix
button) and are checked on every build. See
[Documentation~/LowLatencyTracking.md](Documentation~/LowLatencyTracking.md) for how it works,
the measurements and the tests. Import the `Low Latency Tracking` package sample to check it
on a real table: detected planes show as a dot grid with rows of small cubes standing on it.

### Reset World Origin

The SDK enables resetting the ARKit `ARSession`'s world origin to a specified position and rotation by using the `WorldOriginResetter` script. Please notice that there must be a `HoloKitARKitManager` component in the scene, which is used by the `WorldOriginResetter` script.

Import the `Reset World Origin` package sample for how to use this feature.

### AR Background Video Enhancement

The 4K HDR (High Dynamic Range) AR background video is a feature of ARKit 6, available on iOS 16 and later. This enhancement improves the quality of ARBackground video, leading to better video recording quality. To enable the 4K HDR option, use the `ARBackgroundVideoEnhancementManager` component. Please notice that there must be a `HoloKitARKitManager` component in the scene, which is used by the `ARBackgroundVideoEnhancementManager` script.

<img width="675" alt="image" src="https://github.com/holokit/holokit-unity-sdk/assets/44870300/24c9274f-d30c-47e3-acb3-10b7d0bce816">

Import the `AR Background Video Enhancement` package sample for how to use this feature. Please note that not all iOS devices support 4K or HDR. For more information, please visit the [ARKit's website](https://developer.apple.com/augmented-reality/arkit/) and check out this [WWDC session](https://developer.apple.com/videos/play/wwdc2022/10126/#:~:text=In%204K%20mode%2C%20an%20image,at%2030%20frames%20per%20second.) on ARKit 6.

## Community and Feedback

If you encounter any issues, have queries, suggestions, or discover any bugs, we welcome you to join our [Discord](https://discord.gg/9Stseyje) community or submit an issue. Your feedback is invaluable as we continually strive to update and improve the HoloKit Unity SDK.
