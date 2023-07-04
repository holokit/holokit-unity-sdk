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

## Supported Software Versions

HoloKit Unity SDK has been tested and found compatible with the following software versions:

### Unity
- Unity 2021.3 LTS
- Unity 2022.3 LTS

### ARFoundation
- ARFoundation 5.0

We aim to continually test and verify compatibility with newer versions of these softwares.

## Phone Compatibility

### iOS

HoloKit was primarily built for iOS devices. Please see the [Supported iOS Devices](Supported_iOS_Devices.md) to check if your iPhone is supported.

### Android

HoloKit currently only supports a limited number of Android phones. Please see the [Supported Android Devices](Supported_Android_Devices.md) for more details.

If your Android phone is not supported, we also provide a calibration method to let you figure out the specs of your Android phone yourself. You can then enter your Android phone specs to the SDK to have your device rendering stereo images. Please see [Phone Model Specs Calibration], which is a step-by-step guide on how to figure out the specs of your phone model.

## How to Use HoloKit Unity SDK

### Setup Project Settings

Before using the HoloKit Unity SDK, ensure to adjust the project settings to meet ARFoundation's requirements. If you are already comfortable with ARFoundation, feel free to skip this section.

Upon successfully installation of the SDK package, the ARFoundation package will be installed automatically. Please note that the HoloKit Unity SDK is only compatible with ARFoundation 5+. If you are using ARFoundation 4, please update to a more recent version. 

For iOS-targeted projects, install the `Apple ARKit XR Plugin` package. For Android-targeted projects, the `Google ARCore XR Plugin` is required.

#### iOS Project Settings

Set a user-friendly text for `Project Settings->Player->Other Settings->Configuration->Camera Usage Description`. This message will appear when the iPhone requests camera access.

Under `Project Settings->XR Plug-in Management->Plug-in Providers`, enable the `Apple ARKit` option.

#### Android Project Settings

### Samples

The SDK package offers samples for each key feature. Import these samples into your project and deploy them to mobile devices for a quick overview of each function. These samples can also be used as templates for your projects.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/d9c59958-81f6-4ae6-82de-e9683658c3db" alt="Add HoloKitCameraManager component" width="800"/>

### Stereoscopic Rendering

Stereoscopic rendering is the core feature of the SDK, your app needs to display stereo images on the phone screen so that it can be viewed with the HoloKit headset.

The SDK provides two rendering modes: mono and stereo. Mono mode renders the background camera image with virtual content layered on top, akin to a standard ARFoundation project. In constrast, stereo mode renders two stereo images on a black background one the phone's screen, so that you can insert your phone onto the HoloKit headset to have stereoscopic AR experience.

| <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a9d048a0-b2b5-4f0e-a7f8-03ca782b9725" alt="ScreenRenderMode.Mono" width="450"/> | <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/01bf3231-41ec-4a29-9d94-a48dcebfeefb" alt="ScreenRenderMode.Stereo" width="450"/> |
|:---:|:---:|
| Mono rendering mode | Stereo rendering mode |

To integrate stereoscopic rendering into your project, the `HoloKitCameraManager` component is required. In fact, `HoloKitCameraManager` is the only mandatory component in the SDK that you must add into the scene. By default, adding an `ARSessionOrigin` objet to the scene includes a child object called `AR Camera`. Append the `HoloKitCameraManager` component to the `AR Camera` object for automatic camera setting configuration.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/064d5575-dd8a-4a1b-8c7a-669942a2c58c" alt="Add HoloKitCameraManager component" width="800"/>

When you start the game, `HoloKitCameraManager` initially enters mono mode. A UI button is required to transition between rendering modes. The current rendering mode can be retrieved and set with `HoloKitCameraManager`. The following code snippet illustrates how to switch the rendering mode.

```
// A reference to the HoloKitCameraManager component is required
[SerializedField] HoloKitCameraManager m_HoloKitCameraManager;

public void SwitchRenderMode()
{
    // Get the current ScreenRenderMode value and assign a new value
    holokitCamera.ScreenRenderMode = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
}
```

When the stereo mode is activated, the SDK spawns an alignment marker UI on the screen's upper right corner, enabling you to align your phone accurately once attached to the HoloKit headset.

### Hand Tracking

The SDK tracks the user's hand, providing the 3D positions of the [21 hand joints](https://developer.apple.com/documentation/vision/vnhumanhandposeobservationjointname?language=objc) of each hand.

To incorporate the hand tracking into your project, create an empty GameObject and add `HandTrackingManager` component to it. The script will automatically setup all necessary objects as child GameObjects.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/54c5dc5e-ca2b-4cac-b132-d1aab5f47fa0" alt="HandTrackingManager" width="800"/>

`HandTrackingManager` requires depth information to calculate 3D hand positions, so add `AROcclusionManager` component to the `HoloKit Camera` GameObject and adjust its settings accordingly.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/322b66f7-dd66-436b-b77a-3385926690bf" alt="HandTrackingManager" width="800"/>

You can now build the project onto an iPhone to view the results.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/aba052ed-6433-41ba-9329-a6219364f7ea" alt="Hand Tracking Result" width="600"/>

`HandTrackingManager` can be configured to track either one hand or both hands. As hand tracking is energy-intensive, if dual hand tracking isn't necessary, we advise tracking only one hand to conserve energy. Set `HandTrackingManager.MaxHandCount` in its inspector within the Unity editor.

To obtain specific hand joint positions in code, use `HandTrackingManager.GetHandJointPosition(int handIndex, JointName jointName)`. As the hand tracking algorithm can't distinguish handedness, the parameter `handIndex` simply represents the hand detection sequence. When only one hand is detected, `handIndex` should always be 0. If two hands are detected, `handIndex` can be either 0 or 1. The number of detected hands can be accessed via `HandTrackingManager.HandCount`.

Please note, the hand tracking feature is only compatible with iOS 14.0 or higher devices equipped with a LiDAR depth sensor.

### Hand Gesture Recognition

The SDK can also recognizes user's hand gesture, which can serve as input triggers in your project. Currently there are only two available hand gestures: `HandGesture.None` and `HandGesture.Pinched`.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/0fe0010f-7854-41ad-b9b8-95ee8039fb4c" alt="Hand Gesture Recognition" width="600"/>

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

Please note, the hand gesture recognition feature is only available on iOS 14.0 or higher devices.

You can use hand tracking and hand gesture recognition at the same time, simply add both `HandTrackingManager` and `HandGestureRecognitionManager` components to your scene.

## Community and Feedback

If you encounter any issues, have queries, suggestions, or discover any bugs, we welcome you to join our [Discord](https://discord.gg/dkah5sWR) community or submit an issue. Your feedback is invaluable as we continually strive to update and improve the HoloKit Unity SDK.
