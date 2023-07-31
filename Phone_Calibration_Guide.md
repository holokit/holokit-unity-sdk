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

Navigate to `Assets->Samples->HoloKit Unity SDK->[Version]->PhoneModelSpecsCalibration->ScriptableObject->CustomAndroidPhoneModelList`. Create a new list element and populate it with your device model, screen resolution, and screen DPI.

<img width="400" alt="enter phone model information" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/7511b13c-37be-4f3d-8640-25c3a316ff4a">

Please note, once you've added the entry for your phone model in the `CustomAndroidPhoneModelList` scriptable object, it's necessary to rebuild the sample. This step ensures the calibration procedure utilizes the correct screen resolution and DPI.

We are now ready for calibration! There are two types of data we need to determine: the `ViewportBottomOffset` and the `CameraOffset`.

## Step 2: Determine `ViewportBottomOffset`

`ViewportBottomOffset` represents the distance between the bottom of the phone screen and the base of the viewport, as illustrated in the subsequent image. For the SDK to properly align the stereo images on the phone screen with the headset's viewports, it requires the accurate value of this offset.

<img width="500" alt="ViewportBottomOffset" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/a9d055fd-9364-4f68-a4ba-7d00c7db8b1e">

You can adjust the `ViewportBottomOffset` using the up and down buttons located at the top corners of the screen. Ideally, perform this adjustment with your phone inserted into the HoloKit headset while wearing it. The goal of this step is to maximize the headset's Field of View (FOV). Please note that perfection isn't necessary; achieving 80% accuracy is sufficiently effective.

Once you've identified a suitable value for `ViewportBottomOffset`, input this value into the `CustomAndroidPhoneModelList` and rebuild the sample. Having the correct `ViewportBottomOffset` value is essential for proceeding to the next step.

<img width="400" alt="update entry" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/b87ea2e0-439f-4679-82bb-23057df1d0e0">

## Step 3: Determine `CameraOffset`

`CameraOffset` refers to the 3D vector offset from the center of the device's AR camera to the bottom center of the phone screen.

To determine the `CameraOffset`, we first need to identity the AR camera. You can figure out this by launching any AR app and subsequently blocking each camera to discern which one functions as the AR camera.

<img width="450" alt="starting point" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/68b51122-8447-40ab-9cb7-ec17f81efbd8">

<img width="450" alt="end point" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/fd252170-36ab-436a-a913-3028f19ada3c">

As demonstrated in the above images, `CameraOffset` is a vector spanning from the starting point to the end point. We utilize a right-handed coordinate system, as depicted below.

<img width="500" alt="right-handedness" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/d95b6274-8fcd-471f-90c8-86d952d22b1e">

Consequently, `CameraOffset.x` should always be positive, while `CameraOffset.y` and `CameraOffset.z` should always be negative.

There are three methods to determine the accurate `CameraOffset` value.

The first and ideal method involves calculating the value using the official product design diagram. We used this approach for all iPhone models, but sourcing the product design diagram may prove challenging.

The second method encourages physical measurement of your phone using a ruler. A relatively precise result suffices, eliminating the need for extreme accuracy.

Lastly, you can utilize the calibration tool provided in the third scene of our `PhoneModelSpecsCalibration` sample. This tool not only aids in identifying the `CameraOffset` value but also serves as a verification instrument for the calibration results if you opted for the previous methods. We recommend the first two methods as this one is somewhat complicated and challenging to execute.

The subsequent section will guide you on how to utilize the calibration tool to both determine and verify the `CameraOffset` value.

Press the `CameraOffset` button on your phone to access the scene. In this scene, we employ image tracking to track a QR Code image. Once the phone identifies the QR Code, it will render a virtual cube on top of it. If both the `ViewportBottomOffset` value and the `CameraOffset` value are accurate, the virtual cube will be rendered directly above the QR Code. As we've already determined the correct value for `ViewportBottomOffset` ealier, we only need to find the `CameraOffset` value.

Start by navigating to `Assets->Samples->HoloKit Unity SDK->[Version]->PhoneModelSpecsCalibration->Textures->T_TrackingQRCode.png` and open it on your computer. Ensure that the QR Code image displayed on your computer measures 10cm in both width and height, as the correct size of the image is crucial for tracking. [An online ruler](https://www.ginifab.com/feeds/cm_to_inch/actual_size_ruler.html) can be helpful for measuring the actual size of an image on the screen.

<img width="500" alt="online ruler" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/73e5793f-9147-4ec6-b572-92d536ee3831">

When the correctly sized QR Code is displayed on the screen, insert your Android phone onto the HoloKit headset to observe the QR Code. If the current `CameraOffset` value in the `CustomAndroidPhoneModelList` ScriptableObject is precise, the virtual cube should be rendered directly above the QR Code. Should you notice a significant deviation, you can adjust each axis of the `CameraOffset` value using the top bar of the phone screen. The accurate alignment of the virtual cube atop the QR Code signals a successful calibration.

<img width="500" alt="view the image" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/d0da7a33-2eb3-406c-910e-9e04763060f9">

## Use the calibration result in your own project

A complete calibration result for an Android phone encompasses the model name, screen resolution, screen DPI, `ViewportBottomOffset` and `CameraOffset`.

<img width="400" alt="create PhoneModelList" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/2f8a2b37-9663-4109-8354-00ff44a5429e">

In your project, generate your own `PhoneModelList` ScriptableObject, add an entry for your phone model, and drag the ScriptableObject into the `CustomAndroidPhoneModelList` field of the `HoloKitCameraManager` component present in your scene. This will enable the `HoloKitCameraManager` to identify your Android model's specs when rendering stereo images.

<img width="400" alt="reference" src="https://github.com/holoi/holokit-unity-sdk/assets/44870300/80e3bee1-1379-4afc-a9c8-163ed4aa3430">

## Share your calibration result with us

We sincerely appreciate your effort in completing this calibration task -- it's no small feat!

We encourage you to share your calibration results with us, so that other developers with the same phone model can benefit from your work!

You can either share your calibration results on our [Discord](https://discord.gg/dkah5sWR) channel or submit an issue in this repository. Your contribution is truly valuable and appreciated.
