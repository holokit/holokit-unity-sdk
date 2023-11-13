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

HoloKit Unity SDK provides two main functionalities: stereoscopic rendering and hand tracking. This section will explain the mechanism of those two systems in detail.

### Stereoscopic Rendering

There are two screen render mode provided by the SDK, which are `Mono` mode and `Stereo` mode. In `Mono` mode, the app is just a normal ARFoundation app with `ARCameraBackground`. In `Stereo` mode, the SDK renders two viewports on the iPhone screen with two cameras with black background. This is the single most important feature of the SDK. The core script responsible for stereoscopic rendering is `HoloKitCameraManager`, which is attached to the `Main Camera` GameObject to transform an ARFoundation camera to a HoloKit camera. 

<img width="582" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/668880de-71da-457f-a3b2-c0300f07668e">

- `Mono Camera` is the camera used in `Mono` mode.

- `Center Eye Pose` is the transform representing the center point between the user's eyes. This is set to the iPhone camera's position in `Mono` mode.

- `Black Camera` is reponsible for rendering a black background behind the two viewports.

- `Left Eye Camera` renders the left viewport in `Stereo` mode.

- `Right Eye Camera` renders the right viewport in `Stereo` mode.

- `IPD` stands for inter-pupillary distance, which is the distance between the user's two eyes.

- `Far Clip Plane` defines the far clip plane of the two stereo cameras.

- `Show Alignment Marker In Stereo Mode` indicates whether the SDK will automatically render the alignment marker of the top right corner of the screen under `Stereo` mode.

- `Supported Mono Screen Orientations` indicates the supported screen orientations in `Mono` mode. Since the screen orientation is locked to `LandscapeLeft` under `Stereo` mode, when switching back to `Mono` mode, the SDK needs to know which screen orientations are supported.

- `HoloKit Generation` indicates the HoloKit model we want to use. Currently we only have one HoloKit model.

- `iOS Phone Model List` stores the specs of all supported iPhone models.

- `Default Android Phone Model List` stores the specs of all supported Android models.

- `Custom Android Phone Model List` is for developers to support their own Android models which are currently not supported by default. For calculating the specs of the device, see [Phone Calibration Guide](./Phone_Calibration_Guide.md).
