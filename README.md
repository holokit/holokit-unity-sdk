# HoloKit Unity SDK

[HoloKit](https://holokit.io/) is an Augmented Reality(AR) headset that transforms your iPhone into a powerful stereoscopic AR device. With the HoloKit Unity SDK, developers can create immersive AR experiences in Unity, which can be viewed with HoloKit headset.

The SDK currently provides three core features:

- Stereoscopic Rendering
- Hand Tracking
- Hand Gesture Recognition

Stereoscopic rendering is the central feature of the SDK, allowing for the display of stereo images on the iPhone screen. By attaching your iPhone to the HoloKit, you can enjoy a captivating AR experience. Utilizing [Apple Vision framework](https://developer.apple.com/documentation/vision?language=objc), the SDK detects user hand poses. Together with LiDAR depth sensor, it allows your iPhone to track the 3D positions of the user's hands. Furthermore, the SDK can recognize hand gestures such as pinching, serving as a trigger for specific operations in your project.

HoloKit Unity SDK, built on the foundation of ARFoundation, is compatible with most ARFoundation features such as image tracking and plane detection. Upgrading your ARFoundation project to a stereoscopic AR project is straightforward with the HoloKit Unity SDK.

## How to Install

You can install HoloKit Unity SDK from the following git URL in Package Manager:
```
https://github.com/holoi/holokit-unity-sdk.git
```
<img width="279" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a82656c9-aa73-4158-83b5-20d5178c8a81">
<img width="326" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/511c748f-251c-42a5-b481-fd3160c19c63">

or by directly adding the following line to the `Packages/manifest.json` file.
```
"com.holoi.xr.holokit": "https://github.com/holoi/holokit-unity-sdk.git"
```

## Phone Compatibility

### iOS

HoloKit was primarily built for iOS devices. Please see the [Supported iOS Devices](Supported_iOS_Devices.md) to check if your iPhone is supported.

### Android

HoloKit currently only supports a limited number of Android phones. Please see the [Supported Android Devices](Supported_Android_Devices.md) for more details.

If your Android phone is not supported, we also provide a calibration method to let you figure out the specs of your Android phone yourself. You can then enter your Android phone specs to the SDK to have your device rendering stereo images. Please see [Phone Model Specs Calibration], which is a step-by-step guide on how to figure out the specs of your phone model.

## How to Use HoloKit Unity SDK

### Project Setup

Before using HoloKit Unity SDK, you first need to setup a basic ARFoundation scene together with proper project settings. If ARFoundation is new to your, you can follow this [Unity official tutorial](https://learn.unity.com/tutorial/setting-up-ar-foundation#) to get started.

### Samples

The package provides samples for each key feature of the SDK. You can import the samples into your project and build them onto mobile devices to have a quick look of each feature. You can also use those samples as templates for your projects.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/d9c59958-81f6-4ae6-82de-e9683658c3db" alt="Add HoloKitCameraManager component" width="800"/>

### Stereoscopic Rendering

Stereoscopic rendering is the core feature of the SDK, your app needs the display of the stereo images on the screen so that it can be viewed with HoloKit headset.

There are two rendering modes provided by the SDK, which are mono mode and stereo mode. In mono mode, the background camera image is rendered together with virtual content on top of it, just as a normal ARFoundation project. In stereo mode, two stereo images with black background are rendered on the phone's screen, so that you can insert your phone onto HoloKit headset to have stereoscopic AR experience.

| <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a9d048a0-b2b5-4f0e-a7f8-03ca782b9725" alt="ScreenRenderMode.Mono" width="450"/> | <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/01bf3231-41ec-4a29-9d94-a48dcebfeefb" alt="ScreenRenderMode.Stereo" width="450"/> |
|:---:|:---:|
| Mono rendering mode | Stereo rendering mode |

To add the stereoscopic rendering functionality into your project, you need `HoloKitCameraManager` component. By default, when you add an `ARSessionOrigin` object into the scene, it has a child object called `AR Camera`. Add `HoloKitCameraManager` component onto the `AR Camera` object and it will automatically setup all camera settings for you.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/064d5575-dd8a-4a1b-8c7a-669942a2c58c" alt="Add HoloKitCameraManager component" width="800"/>

When you start the game, `HoloKitCameraManager` will first enter mono mode by default. If will need a UI button to switch the rendering mode. You can get and set the rendering mode with `HoloKitCameraManager`, the following code snippet gives an example of how to switch the rendering mode value.

```
public void SwitchRenderMode()
{
    // We need a reference of the HoloKitCameraManager component
    var holokitCamera = FindObjectOfType<HoloKitCameraManager>();
    // Get the current ScreenRenderMode value and set new value for it
    holokitCamera.ScreenRenderMode = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
}
```

When switching to the stereo mode, the SDK will spawn an alignment marker UI for you on the upper right corner of the screen, so that you can align your phone properly aftering inserting it onto HoloKit headset.

### Hand Tracking

The SDK tracks the user's hand and provides the positions of the [21 hand joints](https://developer.apple.com/documentation/vision/vnhumanhandposeobservationjointname?language=objc) of each hand.

To add the hand tracking functionality into your project, create an empty GameObject and add `HandTrackingManager` component onto it. The script will automatically setup all necessary objects as child GameObjects.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/54c5dc5e-ca2b-4cac-b132-d1aab5f47fa0" alt="HandTrackingManager" width="800"/>

`HandTrackingManager` requires depth information to compute the 3D hand positions, we add `AROcclusionManager` component onto the `HoloKit Camera` GameObject and configure its settings as shown below.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/322b66f7-dd66-436b-b77a-3385926690bf" alt="HandTrackingManager" width="800"/>

You can then build the project onto an iPhone to have the following result.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/aba052ed-6433-41ba-9329-a6219364f7ea" alt="Hand Tracking Result" width="500"/>

You can configure `HandTrackingManager` to make it track only one hand or both hands. Since the hand track algorithm is very energy consuming, if you don't need to track user's both hands, we recommend you to only track one hand to save energy. You can set `HandTrackingManager.MaxHandCount` in its inspector in Unity editor.

If you want to acquire the positions of specific hand joints in code, you should use `HandTrackingManager.GetHandJointPosition(int handIndex, JointName jointName)`. The hand tracking algorithm cannot distinguish handedness, the parameter `handIndex` only represents the hand detection order. When there is only one hand being detected, `handIndex` should always be 0. When there are 2 hands being detected, `handIndex` can be either 0 or 1. You can get the number of detected hands via `HandTrackingManager.HandCount`.

The hand tracking feature can only run on iOS 14.0 or higher devices equipped with LiDAR depth sensor.

### Hand Gesture Recognition

`HandGestureRecognitionManager` recognizes the user's hand gesture, you can use the gesture transitions as inputs to trigger some operations in your project. There are three hand gestures available:
```
public enum HandGesture
{
    None = 0,
    Pinched = 1,
    Apart = 2
}
```
To use hand gesture recognition, all you need to do is to add `HandGestureRecognitionManager` into the scene. The callback function `HandGestureRecognitionManager.OnHandGestureChanged` is invoked when the algorithm detects a transition of the user's hand gesture, you can register this callback to get notified. You can also get the user's current hand gesture via `HandGestureRecognitionManager.HandGesture`.

The hand gesture recognition feature is only available on iOS 14.0 or higher devices.

## Community and Feedback

If you have any problem, question, suggestion, or find a bug, please join our [Discord](https://discord.gg/dkah5sWR) or submit an issue. We will continue to update this SDK and your feedbacks are appreciated.
