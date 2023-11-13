# How to Contribute HoloKit Unity SDK

The objective of this article is to guide future developers how to refine and add new functionalities to HoloKit Unity SDK.

## How to Add New iPhone Models

HoloKit Unity SDK uses iPhone's hardware specs to determine the render parameters, which ensures that the two viewports on the phone screen are rendered in the correct position and size.

All iPhone models are listed in SDK's `Assets/ScriptableObjects/iOSPhoneModelList` asset. For adding new iPhone models, you nend to add a new iPhone model entry and enter the correct information.

<img width="548" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/269828f1-22f6-4067-b4f5-a921c0b9060c">

- The `Model Name` property is the identifier Apple used to distinguish between different devices. To find the `Model Name` of your desired device, you can check [this repo](https://github.com/pluwen/apple-device-model-list).

- The `Description` property is for internal developers to identify the iPhone model, since the `Model Name` property is usually not obvious enough.

- The `Screen Resolution` property is the screen resolution of the iPhone, which can be found on Apple's official website. The first value is the screen height (the larget one) and the second value is the screen width (the smaller one).

- The `Screen DPI` property is the screen DPI (Dots Per Inch) of the iPhone, which can alos be found on Apple's official website. Please note that the name DPI and PPI (Pixels Per Inch) can be used interchangeably.

- The `Viewport Bottom Offset` property is the offset between the iPhone's screen bottom and the bottom of the rendered viewport. This property is measured by meters. The mechanism of this property is explained in detail in [Phone Calibration Guide](./Phone_Calibration_Guide.md#step-2-determine-viewportbottomoffset). However, you can only measure this property by manual configuration. You can leave this value to 0 and use `Screen Bottom Border` instead.

- The `Camera Offset` property is the 3D offset from the iPhone's main camera to the center of the screen bottom. This property is measured by meters. The mechanism of this proerty is explained in detail in [Phone Calibration Guide](./Phone_Calibration_Guide.md#step-3-determine-cameraoffset).

