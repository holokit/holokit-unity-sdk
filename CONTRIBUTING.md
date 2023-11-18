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

## How The SDK Works

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

HoloKit Unity SDK offers two hand pose detection releated features: hand tracking and hand gesture recognition, both utilizing [Apple Vision's hand pose detection algorithm](https://developer.apple.com/documentation/vision/detecting_hand_poses_with_vision). This algorithm, primarily 2D in nature, identifies the 2D coordinates of 21 joints in the user's hands. While these coordinates are adequate for recognizing hand gestures, they fall short for 3D hand tracking, which aims to ascertain the 3D positions of these joints. To overcome this, we integrate the Apple Vision algorithm with the iPhone's LiDAR sensor. This combination allows us to first pinpoint the 21 joints' 2D coordinates and then match them with their depth values from the LiDAR sensor's depth map, thus achieving accurate 3D tracking.

Scripts related to hand pose detection are located in the `Runtime\iOS` folder, reflecting their exclusivity to iOS devices. We have written native Objective-C code to capture the user's hand pose and facilitate data marshalling from Objective-C to C#. This approach underpins the implementation of both hand tracking and hand gesture recognition functionalities.

The `HandTrackingManager` script manages the provision of 3D positions for the user's hand joints, while the `HandGestureRecognitionManager` script identifies the user's hand gestures. Both scripts utilize `AppleVisionHandPoseDetector` to server as a conduit, fetching native data from the Objective-C side.

The `Runtime/iOS/NativeCode` folder houses all the native Objective-C code for the SDK. Each native functionality is represented by three types of files: a header file, an implementation file, and a bridge file. The header file outlines the interface of the native class, the implementation file provides its actual functionality, and the bridge file facilitates marshalling between the unmanaged (Objective-C) and managed (C#) code. For instance, within the `Runtime/iOS/NativeCode/AppleVisionHandPoseDetector-C-Bridge` file, there are four native C functions. These functions correspond to and are linked with four marshalling functions in the `AppleVisionHandPoseDetector` C# class.

<img width="1134" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/03a69774-7472-4793-8b6f-9ccae84e6abb">

<img width="1263" alt="image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/f465e9d1-99f7-4a6c-acdd-0e4314a83eba">

`AppleVisionHandPoseDetector` offers the flexibility to process video frame images in either 2D or 3D mode. The `HandGestureRecognitionManager` requires only 2D hand poses, while the `HandTrackingManager` necessitates 3D hand poses. Consequently, when using `HandGestureRecognitionManager` alone, 2D hand poses can be obtained without activating the LiDAR sensor. In contrast, `HandTrackingManager` usage mandates turning on the LiDAR sensor to capture the user's 3D hand poses. Both managers rely on `AppleVisionHandPoseDetector` to access hand data from native code. When operated concurrently, they share the same `AppleVisionHandPoseDetector` instance, optimizing efficiency.

### Low Latency Tracking

See [HoloKit Low Latency Tracking repository](https://github.com/holoi/holokit-low-latency-tracking) for detailed explanation.

## How To Work With The SDK

This SDK, being a Unity package, requires a carrier Unity project for development and testing. If you want to contribute or modify the SDK, start by cloning [the carrier project](https://github.com/holoi/holokit-unity-sdk-project). This repository includes the SDK as a git submodule. Within the carrier project, the SDK is integrated as a local folder, allowing for direct modifications and testing.

In the `Assets/Samples` folder of the carrier project, you'll find multiple SDK samples. To update existing samples or create new ones, simply copy the desired sample folder into the SDK's `Samples~` folder. After making changes or additions, don't forget to update the `package.json` file in the SDK to reflect these modifications. This process ensures that your contributions are properly integrated and accessible within the SDK structure.

## Potential Future Improvements

### Enhancing the Smoothness of 3D Hand Tracking

The inherent limitation of using Apple Vision's natively 2D hand pose detection algorithm for 3D hand tracking introduces certain inaccuracies. A notable issue arises from the misalignment between hand pose detection and the depth map. Often, fingertips are incorrectly mapped to the background of the depth map, resulting in exaggerated depth values. Currently, we employ a basic method of using the second fingertip to correct these anomalies, but this leads to occasional glitches. A more effective solution would involve interpolating the movement of each hand joint between frames to achieve smoother results. Although this correction is presently handled in the Objective-C code, it could potentially be implemented either there or on the C# side for improved performance.

### Enhancing Robustness and Variety in Hand Gesture Recognition

At present, our system recognizes only two hand gestures: Pinched and Five. Theoretically, it's feasible to expand this range to include gestures like One, Two, Three, and others. However, the main challenge lies in the potential conflicts that may arise when `HandGestureRecognitionManager` supports a broader array of gestures simultaneously. For instance, distinguishing between gestures like Pinched and Four can be particularly difficult. This area calls for further research and development to improve accuracy and ensure that the system can robustly recognize a wider variety of gestures without confusion.

### Advancing Low Latency Tracking Performance

There is a continuous need for further optimization in our low latency tracking system. This involves refining the system's responsiveness and accuracy to minimize latency even further. For detailed information and specific areas of focus, please refer to the [HoloKit Low Latency Tracking repository](https://github.com/holoi/holokit-low-latency-tracking).
