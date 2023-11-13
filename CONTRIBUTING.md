# Contributing to HoloKit Unity SDK

This guide outlines how to enhance and introduce new features to the HoloKit Unity SDK.

## Adding New iPhone Models

HoloKit Unity SDK optimizes render parameters based on iPhone hardware specifications, ensuring accurate viewport positioning and sizing.

To add a new iPhone model to the `Assets/ScriptableObjects/iOSPhoneModelList` asset:

<img width="548" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/269828f1-22f6-4067-b4f5-a921c0b9060c">

1. `Model Name`: This is Apple's identifier for each device. Locate the model name [here](https://github.com/pluwen/apple-device-model-list).
   
2. `Description`: Provides clarity for internal developers, supplementing the less descriptive Model Name.
   
3. `Screen Resolution`: Found on Apple’s official website, list the screen height (larger number) first, followed by the width.
   
4. `Screen DPI`: Also obtainable from Apple’s website, refers to the screen's DPI (Dots Per Inch), synonymous with PPI (Pixels Per Inch).
 
5. `Viewport Bottom Offset`: Indicates the offset between the device screen bottom and the viewport bottom measured in meters. For a detailed explanation, see the [Phone Calibration Guide](./Phone_Calibration_Guide.md#step-2-determine-viewportbottomoffset). However, you can only measure this property manually. You can leave this value to 0 and use `Screen Bottom Border` instead.

6. `Camera Offset`: The 3D distance from the iPhone's main camera to the screen's bottom center, detailed in the [Phone Calibration Guide](./Phone_Calibration_Guide.md#step-3-determine-cameraoffset). Calculate this using [Accessory Design Guidelines for Apple Devices](https://developer.apple.com/accessories/Accessory-Design-Guidelines.pdf).

7. `Screen Bottom Border`: Distance between the screen's bottom border and the phone's frame bottom. Determine this using the [Accessory Design Guidelines for Apple Devices](https://developer.apple.com/accessories/Accessory-Design-Guidelines.pdf). You can use either `Viewport Bottom Offset` or `Screen Bottom Border` to set viewport positioning, but not both. While `Viewport Bottom Offset` offers higher accuracy, it is more challenging to calculate. In contrast, `Screen Bottom Border` is easier to compute but less precise. If both values are provided, the SDK defaults to using `Screen Bottom Border` for viewport positioning. If `Viewport Bottom Offset` is set to 0, the SDK will automatically utilize `Screen Bottom Border`.

The [Accessory Design Guidelines for Apple Devices](https://developer.apple.com/accessories/Accessory-Design-Guidelines.pdf) provide precise specifications for Apple devices, crucial for accurately calculating `Camera Offset` and `Screen Bottom Border`.

<img width="1050" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/f6807def-4a59-4155-8f82-eee438e7d76b">

## How HoloKit Unity SDK Works


