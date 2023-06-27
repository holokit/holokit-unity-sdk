# HoloKit Unity SDK

[HoloKit](https://holokit.io/) is an Augmented Reality(AR) headset that transforms your iPhone into a powerful stereoscopic AR device. With the HoloKit Unity SDK, developers can create immersive AR experiences in Unity, which can be viewed with HoloKit headset.

The SDK's key feature is stereoscopic rendering, allowing for the display of stereo images on the iPhone screen. By attaching your iPhone to the HoloKit, you can enjoy  a captivating AR experience.

HoloKit Unity SDK is compatible with ARFoundation and other AR SDKs.

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

The SDK current provides three main features:
- Stereoscopic Rendering: Renders two stereo images on the phone's screen which can be used together with HoloKit headset.
- Hand Tracking: Tracks the 3D positions of the 21 hand joints of each hand.
- Hand Gesture Recognition: Recognizes the hand gesture of the user's hand.

For each feature, the SDK package provides a sample project showing how to use it. The Stereoscopic Rendering sample is a minimal use case of the SDK, you can use that sample as a basic template for your project. Please note that the hand tracking and hand gesture recognition features use Apple Vision framework to detect the user's hand poses, which can only be used on iOS devices. For detailed information of the algorithm, please see [Apple's document](https://developer.apple.com/documentation/vision/detecting_hand_poses_with_vision?language=objc).

### Stereoscopic Rendering

Stereoscopic rendering is the most important feature of the SDK, which can easily upgrade an existing screen AR project to a stereoscopic one. In this section, we will show how to add stereoscopic rendering feature to your ARFoundation project.

First, you need to setup an AR scene using ARFoundation. More specifically, you need to add `AR Session` and `AR Session Origin` components into the scene. After adding `AR Session Origin` into the scene, the GameObject will have an `AR Camera` child GameObject by default. Choose the child `AR Camera` GameObject and add component `HoloKit Camera Manager` onto the GameObject. By adding `HoloKit Camera Manager` component, the script will automatically add some necessary GameObjects under the camera.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/064d5575-dd8a-4a1b-8c7a-669942a2c58c" alt="HoloKitCameraManager" width="800"/>

In HoloKit Unity SDK, there are two screen render modes, which are `ScreenRenderMode.Mono` and `ScreenRenderMode.Stereo`. In mono mode, the phone screen will render the camera background image together on top of which virtual content is rendered. In stereo mode, the phone screen will render two viewports with black background and you can put your phone onto HoloKit to have stereoscopic AR experience.

| <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a9d048a0-b2b5-4f0e-a7f8-03ca782b9725" alt="ScreenRenderMode.Mono" width="450"/> | <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/01bf3231-41ec-4a29-9d94-a48dcebfeefb" alt="ScreenRenderMode.Stereo" width="450"/> |
|:---:|:---:|
| `ScreenRenderMode.Mono` | `ScreenRenderMode.Stereo` |

`HoloKit Camera Manager` is responsible for switching between those two render modes.
```
public void SwitchRenderMode()
{
    var holokitCamera = FindObjectOfType<HoloKitCameraManager>();
    holokitCamera.ScreenRenderMode = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
}
```

### Hand Tracking

`HandTrackingManager` tracks the user's hand and provides the positions of the [21 hand joints](https://developer.apple.com/documentation/vision/vnhumanhandposeobservationjointname?language=objc) of each hand.

To use `HandTrackingManager`, you need to do is to create a new empty GameObject in the scene and add `HandTrackingManager` component onto it. The script will automatically setup the GameObject for you.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/54c5dc5e-ca2b-4cac-b132-d1aab5f47fa0" alt="HandTrackingManager" width="800"/>

Since the algorithm requires depth information to compute the 3D positions, we need to have an `AROcclusionManager` component in the scene. We add `AROcclusionManager` component onto the `HoloKit Camera` GameObject and configure the component as shown below.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/322b66f7-dd66-436b-b77a-3385926690bf" alt="HandTrackingManager" width="800"/>

That is all you need to do to setup the hand tracking. If you want to acquire the position of a specific hand joint in code, you should use `HandTrackingManager.GetHandJointPosition(int handIndex, JointName jointName)`. The hand tracking algorithm cannot distinguish the handedness of the detected hands, so `handIndex` only represents the detection order. When there is only one hand being detected, the `handIndex` should always be 0. When there are 2 hands being detected, the `handIndex` can be either 0 or 1.

The hand tracking feature can only run on iOS devices equipped with LiDAR depth sensor.

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
