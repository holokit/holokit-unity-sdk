# Contributing to HoloKit Unity SDK

This guide outlines how to enhance and introduce new features to the HoloKit Unity SDK.

## Adding New iPhone Models

HoloKit Unity SDK optimizes render parameters based on iPhone hardware specifications, ensuring accurate viewport positioning and sizing.

To add a new iPhone model to the `Assets/ScriptableObjects/iOSPhoneModelList` asset:

<img width="548" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/269828f1-22f6-4067-b4f5-a921c0b9060c">

- `Model Name`: This is Apple's identifier for each device. Locate the model name [here](https://github.com/pluwen/apple-device-model-list).
   
- `Description`: Provides clarity for internal developers, supplementing the less descriptive Model Name.
   
- `Screen Resolution`: Found on Apple’s official website, list the screen height (larger number) first, followed by the width.
   
- `Screen DPI`: Also obtainable from Apple’s website, refers to the screen's DPI (Dots Per Inch), synonymous with PPI (Pixels Per Inch).
 
- `Viewport Bottom Offset`: Indicates the offset between the device screen bottom and the viewport bottom measured in meters. For a detailed explanation, see the [Phone Calibration Guide](./Phone_Calibration_Guide.md#step-2-determine-viewportbottomoffset). However, you can only measure this property manually. You can leave this value to 0 and use `Screen Bottom Border` instead.

- `Camera Offset`: The 3D distance from the iPhone's main camera to the screen's bottom center, detailed in the [Phone Calibration Guide](./Phone_Calibration_Guide.md#step-3-determine-cameraoffset). Calculate this using [Accessory Design Guidelines for Apple Devices](https://developer.apple.com/accessories/Accessory-Design-Guidelines.pdf).

- `Screen Bottom Border`: Distance between the screen's bottom border and the phone's frame bottom. Determine this using the [Accessory Design Guidelines for Apple Devices](https://developer.apple.com/accessories/Accessory-Design-Guidelines.pdf). You can use either `Viewport Bottom Offset` or `Screen Bottom Border` to set viewport positioning, but not both. While `Viewport Bottom Offset` offers higher accuracy, it is more challenging to calculate. In contrast, `Screen Bottom Border` is easier to compute but less precise. If both values are provided, the SDK defaults to using `Screen Bottom Border` for viewport positioning. If `Viewport Bottom Offset` is set to 0, the SDK will automatically utilize `Screen Bottom Border`.

The [Accessory Design Guidelines for Apple Devices](https://developer.apple.com/accessories/Accessory-Design-Guidelines.pdf) provide precise specifications for Apple devices, crucial for accurately calculating `Camera Offset` and `Screen Bottom Border`.

<img width="1050" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/f6807def-4a59-4155-8f82-eee438e7d76b">

## How HoloKit Unity SDK Works

The HoloKit Unity SDK primarily focuses on two functionalities: stereoscopic rendering and hand pose detection. This section delves into the intricacies of these systems.

### Stereoscopic Rendering

The SDK offers two rendering modes: `Mono` and `Stereo`.

- `Mono` Mode: Operates as a standard ARFoundation app, utilizing `ARCameraBackground`.

- `Stereo` Mode: The SDK's key feature, it renders two separate viewports on the iPhone screen using dual cameras against a black background. `HoloKitCameraManager`, attached to the `Main Camera` GameObject, transforms an ARFoundation camera into a HoloKit camera.

<img width="582" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/668880de-71da-457f-a3b2-c0300f07668e">

Key Components:

- `Mono Camera`: Used in `Mono` mode.

- `Center Eye Pose`: Represents the midpoint between the user's eyes in `Stereo` mode, aligning with the iPhone camera in `Mono` mode.

- `Black Camera`: Renders the black background in `Stereo` mode.

- `Left/Right Eye Camera`: Render the respective viewports in `Stereo` mode.
  
- `IPD`: stands for inter-pupillary distance, the distance between the user's two eyes.

- `Far Clip Plane`: Sets the farthest visible boundary for the stereo cameras.

- `Show Alignment Marker In Stereo Mode`: An optional feature to display an alignment marker in the top-right corner in `Stereo` mode.

- `Supported Mono Screen Orientations`: The list of supported screen orientations in `Mono` mode. Since the screen orientation is locked to `LandscapeLeft` under `Stereo` mode, when switching back to `Mono` mode, the SDK needs to know which screen orientations are supported.

- `HoloKit Generation`: Specifies the model of HoloKit being used.

- `iOS Phone Model List`: Specs for supported iOS devices.

- `Default Android Phone Model List`: Specs for supported Android devices.

- `Custom Android Phone Model List`: Custom list for unsupported Android models. Refer to the [Phone Calibration Guide](./Phone_Calibration_Guide.md) for custom model specifications.

### Hand Pose Detection

HoloKit Unity SDK provides two hand detection related functionalities, which are hand tracking and hand gesture recognition. Both of these functionalities relies on [Apple Vision's hand pose detection algorithm](https://developer.apple.com/documentation/vision/detecting_hand_poses_with_vision). Apple Vision's hand pose detection algorithm is a 2D algorithm, which can detect the 2D coordinates of the 21 joints of the user's hands. 2D coordinates are sufficient for hand gesture recognition, but is not enough for 3D hand tracking, whose objecive is to track the 3D positions of the 21 hand joints of the user's hands. In order to get the depth of the user's hand joints, we use Apple Vision hand pose detection algorithm together with iPhone's LiDAR sensor. More specifically, we first get the 2D coordinate of the 21 hand joints of the user's hands, then we find the corresponding depth value for each hand joint with the depth map provided by the LiDAR sensor.
