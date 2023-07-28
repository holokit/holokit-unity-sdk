# Phone Calibration Guide

To enable a smartphone to display stereo images compatible with the HoloKit headset, accurate phone specifications are required by the HoloKit Unity SDK. If your Android phone is not on the [supported list](Supported_Android_Devices.md), this article will guide you on how to determine your device's specs.

## Step 0: Check the phone's physical compatibility

All iPhone cameras are positioned at the top-left corner of their back, aligning seamlessly with the unobstructed corner of the HoloKit headset's baffle. Therefore, it's essential to ensure that your Android phone, when inserted into the HoloKit headset, leaves the camera unblocked.

<img width="500" alt="holokit-phone compatibility" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/be40b6e2-0732-4a70-83e5-9b3dd8502d59">

Another aspect to consider is your phone's screen size; it should be large enough to display stereo images (width > 130mm, height > 65mm). Also, the thickness of your device should fall within a specified range (7mm < thickness < 9mm). Devices too thick may not fit into the headset, while those too thin may not be securely stabilized.

Once your Android phone is confirmed to be physically compatible with the HoloKit headset, we're ready to initiate the calibration process!

## Step 1: Deploy the calibration sample project to your phone

Begin by creating am empty Unity project and installing the HoloKit Unity SDK. Follow the guideline in the [How to Use HoloKit Unity SDK](./README.md#How-to-Use-HoloKit-Unity-SDK) section to configure your project settings correctly. Finally, import the "Phone Model Specs Calibration" sample from the SDK.

<img width="500" alt="import sample" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/9d0887bf-2573-4bfe-862a-633421713b28">
