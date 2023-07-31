# Phone Calibration Guide

To enable a smartphone to display stereo images compatible with the HoloKit headset, accurate phone specifications are required by the HoloKit Unity SDK. If your Android phone is not on the [supported list](Supported_Android_Devices.md), this article will guide you on how to determine your device's specs.

## Step 0: Check the phone's physical compatibility

All iPhone cameras are positioned at the top-left corner of their back, aligning seamlessly with the unobstructed corner of the HoloKit headset's baffle. Therefore, it's essential to ensure that your Android phone, when inserted into the HoloKit headset, leaves the camera unblocked.

<img width="500" alt="holokit-phone compatibility" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/be40b6e2-0732-4a70-83e5-9b3dd8502d59">

Another aspect to consider is your phone's screen size; it should be large enough to display stereo images (width > 130mm, height > 65mm). Also, the thickness of your device should fall within a specified range (7mm < thickness < 9mm). Devices too thick may not fit into the headset, while those too thin may not be securely stabilized.

Once your Android phone is confirmed to be physically compatible with the HoloKit headset, we're ready to initiate the calibration process!

## Step 1: Deploy the calibration sample project to your phone

Begin by creating am empty Unity project and installing the HoloKit Unity SDK. Follow the guideline in the [How to Use HoloKit Unity SDK](./README.md#How-to-Use-HoloKit-Unity-SDK) section to configure your project settings correctly. Then, import the "Phone Model Specs Calibration" sample from the SDK.

<img width="400" alt="deploy" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/9d0887bf-2573-4bfe-862a-633421713b28">

Deploy the three scenes from the sample to your Android device, following the sequence outlined below.

<img width="600" alt="import sample" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/9a579a40-10ac-4b02-afa8-bc45fee5e3a4">

Upon successful deployment of the sample to your device, the following screen should appear.

<img width="500" alt="sample scene screen" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/7efbb46d-de18-4032-9395-7d3bdc983d56">

The top-left corner displays the model name, screen resolution, and screen DPI(Dots Per Inch) of your phone. The model name uniquely identifies different phone models for the SDK. The screen resolution and DPI displayed are values provided by Unity, which may occasionally be incorrect. Therefore, it's crucial to cross-verify these two values with your phone's official specifications listed on the manufacturer's website. If the Unity provided values are not correct, you need to override them. In fact, we strongly recommend overriding these values regardless, to ensure more accurate calibration.

Navigate to `Assets->Samples->PhoneModelSpecsCalibration->ScriptableObject->CustomAndroidPhoneModelList`. Create a new list element and populate it with your device model, screen resolution, and screen DPI.

<img width="400" alt="enter phone model information" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/7511b13c-37be-4f3d-8640-25c3a316ff4a">

Please note, once you've added the entry for your phone model in the `CustomAndroidPhoneModelList` scriptable object, it's necessary to rebuild the sample. This step ensures the calibration procedure utilizes the correct screen resolution and DPI.

We are now ready for calibration! There are two types of data we need to determine: the `ViewportBottomOffset` and the `CameraOffset`.

## Step 2: Determine `ViewportBottomOffset`

`ViewportBottomOffset` represents the distance between the bottom of the phone screen and the base of the viewport, as illustrated in the subsequent image. For the SDK to properly align the stereo images on the phone screen with the headset's viewports, it requires the accurate value of this offset.

<img width="500" alt="ViewportBottomOffset" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a9d055fd-9364-4f68-a4ba-7d00c7db8b1e">

You can adjust the `ViewportBottomOffset` using the up and down buttons located at the top corners of the screen. Ideally, perform this adjustment with your phone inserted into the HoloKit headset while wearing it. The goal of this step is to maximize the headset's Field of View (FOV). Please note that perfection isn't necessary; achieving 80% accuracy is sufficiently effective.

Once you've identified a suitable value for `ViewportBottomOffset`, input this value into the `CustomAndroidPhoneModelList` and rebuild the sample. Having the correct `ViewportBottomOffset` value is essential for proceeding to the next step.

## Step 3: Determine `CameraOffset`
