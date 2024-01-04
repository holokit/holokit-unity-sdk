# HoloKit Unity SDK

[HoloKit](https://holokit.io/) is an optical see-through mixed reality headset that transforms your iPhone into a powerful stereoscopic AR device. With the HoloKit Unity SDK, developers can create immersive AR experiences in Unity, which can be viewed with HoloKit headset.

The SDK currently provides three core features:

- Stereoscopic Rendering
- Hand Tracking
- Hand Gesture Recognition

Stereoscopic rendering is the central feature of the SDK, allowing for the display of stereo images on the iPhone screen. By attaching your iPhone to the HoloKit, you can enjoy a captivating AR experience. Utilizing [Apple Vision framework](https://developer.apple.com/documentation/vision?language=objc), the SDK detects user hand poses. Together with LiDAR depth sensor, it allows your iPhone to track the 3D positions of the user's hands. Furthermore, the SDK can recognize hand gestures such as pinching, serving as a trigger for specific operations in your project.

The SDK offers a built-in video recording feature which allows the user to record videos under both screen AR mode and stereoscopic mode.

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
- ARFoundation 5.1

We aim to continually test and verify compatibility with newer versions of these softwares.

## Phone Compatibility

### iOS

HoloKit was primarily built for iOS devices. Please see the [Supported iOS Devices](Supported_iOS_Devices.md) to check if your iPhone is supported.

### Android

HoloKit currently only supports a limited number of Android phones. Please see the [Supported Android Devices](Supported_Android_Devices.md) for more details.

If your Android phone is not supported, we also provide a calibration method to let you figure out the specs of your Android phone yourself. You can then enter your Android phone specs to the SDK to have your device rendering stereo images. Please see [Phone Calibration Guide](Phone_Calibration_Guide.md), which is a step-by-step guide on how to figure out the specs of your phone model.

## Project Environment Setup

Before using the HoloKit Unity SDK, ensure to adjust the project settings to meet ARFoundation's requirements. If you are already comfortable with ARFoundation, feel free to skip this section.

Upon successfully installation of the SDK package, the ARFoundation package will be installed automatically. Please note that the HoloKit Unity SDK is only compatible with ARFoundation 5.+. If you are using ARFoundation 4.+, please update to a more recent version. 

For iOS-targeted projects, install the `Apple ARKit XR Plugin` package. For Android-targeted projects, the `Google ARCore XR Plugin` is required.

### iOS Project Settings

1. **Set Camera and Microphone Usage Descriptions**: Navigate to `Project Settings > Player > Other Settings > Configuration` and provide a user-friendly text for `Camera Usage Description` and `Microphone usage description`. The `Camera Usage Description` will pop up when the iPhone requests access to the camera. The `Microphone usage description` is required because the video recording feature of the SDK will access the microphone to record audio.

<img width="722" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/848a1bf3-b877-48a0-8430-92d2d448c7a5">

2. **Enable Apple ARKit**: Under `Project Settings > XR Plug-in Management > Plug-in Providers`, enable the `Apple ARKit` option.

<img width="600" alt="Enable Apple ARKit" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/ab5f3554-9530-464d-a336-12957a857192">

### Android Project Settings

1. **Disable Auto Graphics API**: Navigate to `Project Settings > Player > Other Settings > Rendering` and disable `Auto Graphics API`. In the `Graphics APIs` list, remove `Vulkan` and ensure only `OpenGLES3` is listed.

<img width="500" alt="Graphics APIs" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/4f66611d-d912-4fe8-b4d9-ca1a08e15a5d">

2. **Set Minimum API Level**: Proceed to `Project Settings > Player > Other Settings > Identification` and set `Minimum API Level` to `Android 7.0 'Nougat' (API level 24)` or higher.

<img width="500" alt="Minimum API Level" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/fb16a187-651c-4ace-b266-77f2e18f67dc">

3. **Configure Scripting Backend**: In `Project Settings > Player > Other Settings > Configuration`, set `Scripting Backend` to `IL2CPP`. Under `Target Architectures`, uncheck `ARMv7` and check `ARM64`.

<img width="500" alt="Configuration" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/049fc7a9-e8a2-4b21-8e1c-cf241df6aa3c">

4. **Enable Google ARCore**: Under `Project Settings > XR Plug-in Management > Plug-in Providers`, enable the `Google ARCore` option.

<img width="600" alt="Enable Google ARCore" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/daccfe62-c90e-4ed3-8fcc-218bcae59a31">

## Core Samples

The SDK package offers a samples for each of the three key features: stereoscopic rendering, hand tracking, and hand gesture recognition. Import these samples into your project and deploy them to mobile devices for a quick overview of each feature. These samples can also be used as templates for your projects.

### Stereoscopic Rendering

Stereoscopic rendering is the core feature of the SDK, your app needs to display stereo images on the phone screen so that it can be viewed with the HoloKit headset.

The SDK provides two rendering modes: mono and stereo. Mono mode renders the background camera image with virtual content layered on top, akin to a standard ARFoundation project. In constrast, stereo mode renders two stereo images on a black background one the phone's screen, so that you can insert your phone onto the HoloKit headset to have stereoscopic AR experience.

| <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a9d048a0-b2b5-4f0e-a7f8-03ca782b9725" alt="ScreenRenderMode.Mono" width="450"/> | <img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/01bf3231-41ec-4a29-9d94-a48dcebfeefb" alt="ScreenRenderMode.Stereo" width="450"/> |
|:---:|:---:|
| Mono rendering mode | Stereo rendering mode |

To integrate stereoscopic rendering into your project, the `HoloKitCameraManager` component is required. In fact, `HoloKitCameraManager` is the only mandatory component in the SDK that you must add into the scene. By default, adding an `XR Origin` object to the scene includes a grandchild object called `Main Camera`. Append the `HoloKitCameraManager` component to the `Main Camera` object for automatic camera setting configuration.

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/92152b99-dec7-47fc-bb49-35a5f6711b8f" alt="Add HoloKitCameraManager component" width="700"/>

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

<img src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/1c1d0144-004f-4528-a1e9-61af5c7ad600" alt="Add AROcclusionManager" width="700"/>

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

## Quick Scene Setup

HoloKit Unity SDK is based on ARFoundation. For a minimal ARFoundation scene requires two primary objects: an `AR Session` object and an `XR Origin` object with the Main Camera Object as its child.

<img width="377" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/feeb658f-ec65-4461-bf5a-e42fc423cafa">

To enable stereoscopic rendering, we need to upgrade the default Main Camera to a HoloKit Camera. To create an `XR Origin` with a HoloKit Camera as its child, right-click in the Unity Editor and select `XR/HoloKit/HoloKit XR Origin` to create a `HoloKit XR Origin`.

<img width="660" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/ad56a592-147f-4969-9942-d3445bf038f6">

The `HoloKit XR Orign` object includes a HoloKit Camera capable of both screen AR and stereoscopic rendering.

<img width="377" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/9ca668c8-729e-4bde-8dda-8f83410411da">

To switch between the rendering modes, a UI button is necessary. To create a default UI panel which includes this mode-switching button, right-click and navigate to `UI/HoloKit/HoloKit Default UI Canvas`.

<img width="690" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/d3c5a561-43c2-48b4-9f24-cbb770b3e773">

The default canvas also has a button for controlling video recording.

<img width="374" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/6daa7737-49e6-4403-9f95-a1eedd88506d">

After setting up the `AR Session`, `HoloKit XROrigin`, and UI components, you can add any 3D object to the scene, such as a cube. When you build your project onto an iPhone, you'll be able to view your object in both screen AR mode and stereoscopic rendering mode with a HoloKit headset.

## Advanced Features

### Reset World Origin

The SDK enables resetting the ARKit `ARSession`'s world origin to a specified position and rotation by using the `WorldOriginResetter` script. Import the `Reset World Origin` package sample for how to use this feature.

### AR Background Video Enhancement

The 4K HDR (High Dynamic Range) AR background video is a feature of ARKit 6, available on iOS 16 and later. This enhancement improves the quality of ARBackground video, leading to better video recording quality. To enable the 4K HDR option, use the `ARBackgroundVideoEnhancementManager` component.

<img width="675" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/24c9274f-d30c-47e3-acb3-10b7d0bce816">

Import the `AR Background Video Enhancement` package sample for how to use this feature. Please note that not all iOS devices support 4K or HDR. For more information, please visit the [ARKit's website](https://developer.apple.com/augmented-reality/arkit/) and check out this [WWDC session](https://developer.apple.com/videos/play/wwdc2022/10126/#:~:text=In%204K%20mode%2C%20an%20image,at%2030%20frames%20per%20second.) on ARKit 6.

## Community and Feedback

If you encounter any issues, have queries, suggestions, or discover any bugs, we welcome you to join our [Discord](https://discord.gg/9Stseyje) community or submit an issue. Your feedback is invaluable as we continually strive to update and improve the HoloKit Unity SDK.
