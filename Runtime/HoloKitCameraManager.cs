// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using HoloKit.UI;

namespace HoloKit
{
    /// <summary>
    /// The core script of the SDK responsible for the rendering of the two viewports on the phone's screen.
    /// </summary>
    public class HoloKitCameraManager : MonoBehaviour
    {
        /// <summary>
        /// The transform of the middle point between the user's eyes under Stereo mode.
        /// The transform of the phone's camera under Mono mode.
        /// </summary>
        public Transform CenterEyePose => m_CenterEyePose;

        /// <summary>
        /// Get and set the current screen render mode.
        /// </summary>
        public ScreenRenderMode ScreenRenderMode
        {
            get => m_ScreenRenderMode;
            set
            {
                if (value == m_ScreenRenderMode)
                    return;

                if (value == ScreenRenderMode.Mono)
                {
                    m_ARCameraBackground.enabled = true; // Turn on the ARCameraBackground
                    m_MonoCamera.enabled = true;
                    m_LeftEyeCamera.gameObject.SetActive(false);
                    m_RightEyeCamera.gameObject.SetActive(false);
                    m_BlackCamera.gameObject.SetActive(false);
                    m_CenterEyePose.localPosition = Vector3.zero; // Reset the CenterEyePose to the phone's camera position
                    m_ScreenRenderMode = ScreenRenderMode.Mono;
                    RestoreMonoFrameRate();

                    // Deactivate the alignment marker UI and the stereo bar
                    if (m_AlignmentGuide)
                        m_AlignmentGuide.gameObject.SetActive(false);
                    if (m_StereoBar)
                        m_StereoBar.gameObject.SetActive(false);

                    // Re-enable supported screen orientations for Mono mode
                    Screen.orientation = ScreenOrientation.AutoRotation;
                    if ((m_SupportedMonoScreenOrientations & MonoScreenOrientation.Portrait) != 0)
                    {
                        Screen.autorotateToPortrait = true;
                    }
                    if ((m_SupportedMonoScreenOrientations & MonoScreenOrientation.PortraitUpsideDown) != 0)
                    {
                        Screen.autorotateToPortraitUpsideDown = true;
                    }
                    if ((m_SupportedMonoScreenOrientations & MonoScreenOrientation.LandscapeRight) != 0)
                    {
                        Screen.autorotateToLandscapeRight = true;
                    }
                    if ((m_SupportedMonoScreenOrientations & MonoScreenOrientation.LandscapeLeft) != 0)
                    {
                        Screen.autorotateToLandscapeLeft = true;
                    }
                }
                else // Stereo
                {
                    if (!DoesCurrentPhoneModelSupportStereoMode())
                    {
                        Debug.LogWarning($"Device {SystemInfo.deviceModel} does not support Stereo mode");
                        return;
                    }

                    m_ARCameraBackground.enabled = false; // Turn off the ARCameraBackground
                    m_MonoCamera.enabled = false;
                    m_LeftEyeCamera.gameObject.SetActive(true);
                    m_RightEyeCamera.gameObject.SetActive(true);
                    m_BlackCamera.gameObject.SetActive(true);
                    m_CenterEyePose.localPosition = m_CameraToCenterEyeOffset; // Set the CenterEyePose to the middle point between the user's eyes
                    m_ScreenRenderMode = ScreenRenderMode.Stereo;

                    // Activate the alignment marker UI and the stereo bar
                    SpawnAlignmentGuide();
                    SpawnStereoBar();
                    ApplyStereoFrameRate();
                }
                OnScreenRenderModeChanged?.Invoke(m_ScreenRenderMode);
            }
        }

        public PhoneModel PhoneModel
        {
            get => m_PhoneModel;
            set
            {
                m_PhoneModel = value;
                HoloKitModelSpecs holokitModelSpecs = DeviceProfile.GetHoloKitModelSpecs(m_HoloKitGeneration);
                SetupCameraData(holokitModelSpecs, m_PhoneModel.ModelSpecs);
                // Millimetres follow the new phone.
                if (m_StereoBar)
                    m_StereoBar.Rebuild();
                if (m_AlignmentGuide)
                    m_AlignmentGuide.Rebuild();
            }
        }

        /// <summary>
        /// Invoked when the screen render mode changed.
        /// </summary>
        public event Action<ScreenRenderMode> OnScreenRenderModeChanged;

        /// <summary>
        /// Invoked once per press of the stereo bar's TRIGGER, a button every app can use in Stereo.
        /// </summary>
        public event Action OnStereoTrigger;

        /// <summary>
        /// Extra lines the stereo bar's MORE shows after frame rate, session time, recording and
        /// thermal state, e.g. <c>new[] { "anchors 3" }</c>.
        /// </summary>
        public string[] StereoInfo { get; set; } = Array.Empty<string>();

        /// <summary>
        /// TRIGGER's label in the stereo bar ("TRIGGER" by default), e.g. "FIRE".
        /// </summary>
        public string StereoTriggerLabel
        {
            get => m_StereoTriggerLabel;
            set => m_StereoTriggerLabel = value;
        }

        /// <summary>
        /// TRIGGER's label while pressed ("TRIGGERED" by default), e.g. "FIRED".
        /// </summary>
        public string StereoTriggerPressedLabel
        {
            get => m_StereoTriggerPressedLabel;
            set => m_StereoTriggerPressedLabel = value;
        }

        /// <summary>
        /// The stereo control bar, created the first time Stereo starts; null before, or when
        /// <see cref="StereoBarItems"/> is <see cref="HoloKitStereoBarItems.None"/>.
        /// </summary>
        public HoloKitStereoBar StereoBar => m_StereoBar;

        /// <summary>
        /// Which controls the stereo bar shows; <see cref="HoloKitStereoBarItems.None"/> for none
        /// (place your own, and a way back to Mono).
        /// </summary>
        public HoloKitStereoBarItems StereoBarItems
        {
            get => m_StereoBarItems;
            set
            {
                if (value == m_StereoBarItems)
                    return;
                m_StereoBarItems = value;
                if (m_StereoBar)
                {
                    // Disable first: Destroy waits for the frame's end, and the old bar must
                    // restore iOS's edge gestures before the new one saves them.
                    m_StereoBar.gameObject.SetActive(false);
                    Destroy(m_StereoBar.gameObject);
                    m_StereoBar = null;
                }
                if (m_ScreenRenderMode == ScreenRenderMode.Stereo)
                    SpawnStereoBar();
            }
        }

        [Tooltip("The ARCamera used in Mono mode.")]
        [SerializeField] Camera m_MonoCamera;

        [SerializeField] Transform m_CenterEyePose;

        [Tooltip("The background camera in Stereo mode for rendering the black ground image behind the two viewports.")]
        [SerializeField] Camera m_BlackCamera;

        [Tooltip("The camera rendering the left viewport in Stereo mode.")]
        [SerializeField] Camera m_LeftEyeCamera;

        [Tooltip("The camera rendering the right viewport in Stereo mode.")]
        [SerializeField] Camera m_RightEyeCamera;

        [Header("Settings")]
        [Tooltip("The Interpupillary distance value used to calculate the rendering parameters.")]
        [SerializeField] [Range(0.054f, 0.074f)] float m_Ipd = 0.064f;

        [Tooltip("The far clip plane of two viewport cameras in Stereo mode.")]
        [SerializeField] float m_FarClipPlane = 50f;

        [Tooltip("Show the alignment guide in Stereo mode: the outline of the headset's phone opening and the marker line at its notch.")]
        [SerializeField] bool m_ShowAlignmentMarkerInStereoMode = true;

        [Tooltip("Seconds without a touch before the alignment guide hides in Stereo mode (a touch brings it back); 0 keeps it.")]
        [SerializeField] float m_AlignmentGuideAutoHideDelay = 10f;

        [Tooltip("The controls of the stereo bar along the screen's top edge in Stereo mode: MORE, RECORD, MONO, OPTIONS and TRIGGER. Nothing for none.")]
        [SerializeField] HoloKitStereoBarItems m_StereoBarItems = HoloKitStereoBarItems.All;

        [Tooltip("TRIGGER's label in the stereo bar, e.g. FIRE. Keep it short: it sits in a 20 mm touch area.")]
        [SerializeField] string m_StereoTriggerLabel = "TRIGGER";

        [Tooltip("TRIGGER's label while pressed, e.g. FIRED.")]
        [SerializeField] string m_StereoTriggerPressedLabel = "TRIGGERED";

        [Tooltip("Debug: outline each stereo bar control's touch area.")]
        [SerializeField] bool m_ShowStereoBarTouchAreas;

        [Tooltip("The list of supported screen orientations under Mono mode.")]
        [SerializeField] MonoScreenOrientation m_SupportedMonoScreenOrientations = MonoScreenOrientation.Portrait | MonoScreenOrientation.LandscapeLeft;

        [Tooltip("Target frame rate in Stereo mode. 0 uses the display's maximum (120 Hz on ProMotion iPhones). " +
                 "Unity's iOS default of 30 fps makes world-locked content step visibly.")]
        [SerializeField] int m_StereoFrameRate = 0;

        [Tooltip("Lower the Stereo frame rate as the phone heats up: Serious caps it at 40 fps, Critical at 30 " +
                 "(divisors of 120 Hz, so frames stay evenly paced). Fewer frames is the main way to cool down.")]
        [SerializeField] bool m_AdaptStereoFrameRateToThermalState = true;

        /// <summary>
        /// Lower the Stereo frame rate as the phone heats up (Serious: 40 fps, Critical: 30), as the
        /// HoloKit Swift SDK does. On by default.
        /// </summary>
        public bool AdaptStereoFrameRateToThermalState
        {
            get => m_AdaptStereoFrameRateToThermalState;
            set
            {
                m_AdaptStereoFrameRateToThermalState = value;
                if (m_StereoFrameRateApplied)
                    ApplyStereoFrameRate();
            }
        }

        /// <summary>
        /// The Stereo frame rate for a thermal state (0 nominal, 1 fair, 2 serious, 3 critical).
        /// </summary>
        public static int StereoFrameRate(int maximum, int thermalState, bool adapts)
        {
            if (!adapts)
                return maximum;
            if (thermalState >= 3)
                return Math.Min(maximum, 30);
            if (thermalState == 2)
                return Math.Min(maximum, 40);
            return maximum;
        }

        int m_ThermalState;
        float m_NextThermalCheck;
#if UNITY_IOS && !UNITY_EDITOR
        iOS.AppleNativeProvider m_AppleNative;
#endif

        // Polled on the main thread: the thermal notification arrives on any thread.
        void UpdateThermalFrameRate()
        {
            if (!m_AdaptStereoFrameRateToThermalState || Time.unscaledTime < m_NextThermalCheck)
                return;
            m_NextThermalCheck = Time.unscaledTime + 1f;
#if UNITY_IOS && !UNITY_EDITOR
            m_AppleNative ??= new iOS.AppleNativeProvider();
            int state = (int)m_AppleNative.GetThermalState();
            if (state != m_ThermalState)
            {
                m_ThermalState = state;
                ApplyStereoFrameRate();
            }
#endif
        }

        int m_MonoFrameRate;
        bool m_MonoMatchFrameRate;
        bool m_StereoFrameRateApplied;

        void ApplyStereoFrameRate()
        {
            // ARSession's "Match Frame Rate" resets Unity's frame rate to ARKit's
            // camera rate every frame (30 Hz when the phone is warm), so it is
            // turned off in Stereo and restored in Mono.
            var session = FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARSession>();
            if (!m_StereoFrameRateApplied)
            {
                m_MonoFrameRate = Application.targetFrameRate;
                m_MonoMatchFrameRate = session != null && session.matchFrameRateRequested;
            }
            if (session != null)
                session.matchFrameRateRequested = false;
            m_StereoFrameRateApplied = true;
            int maximum = m_StereoFrameRate > 0
                ? m_StereoFrameRate
                : Mathf.Max(60, (int)Math.Round(Screen.currentResolution.refreshRateRatio.value));
            Application.targetFrameRate = StereoFrameRate(maximum, m_ThermalState, m_AdaptStereoFrameRateToThermalState);
        }

        void RestoreMonoFrameRate()
        {
            if (!m_StereoFrameRateApplied)
                return;
            var session = FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARSession>();
            if (session != null)
                session.matchFrameRateRequested = m_MonoMatchFrameRate;
            Application.targetFrameRate = m_MonoFrameRate;
            m_StereoFrameRateApplied = false;
        }

        [Header("Devices")]
        [SerializeField] HoloKitGeneration m_HoloKitGeneration = HoloKitGeneration.HoloKitX;

        [SerializeField] PhoneModelList m_iOSPhoneModelList;

        [SerializeField] PhoneModelList m_DefaultAndroidPhoneModelList;

        [Tooltip("Developer's custom phone model list for officially unsupported devices.")]
        [SerializeField] PhoneModelList m_CustomAndroidPhoneModelList;

        PhoneModel m_PhoneModel;

        ScreenRenderMode m_ScreenRenderMode = ScreenRenderMode.Mono;

        ARCameraBackground m_ARCameraBackground;

        Vector3 m_CameraToCenterEyeOffset;

        HoloKitAlignmentGuide m_AlignmentGuide;

        HoloKitStereoBar m_StereoBar;

        List<ScreenOrientation> m_SupportedScreenOrientations;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null && gameObject != null) // Check to make sure the object hasn't been destroyed in the meantime
                    {
                        InitializeInEditor();
                    }
                };
            }
        }

        const string IOSPhoneModelListGuid = "97e080f80a8ae49d78e255ae53b17e11";
        const string DefaultAndroidPhoneModelListGuid = "f192a44dbc7c041758df0ca7a4e8befe";

        static T LoadByGuid<T>(string guid) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

        private void InitializeInEditor()
        {
            if (Application.isPlaying)
                return;

            if (transform.childCount > 1)
                return;

            gameObject.name = "HoloKit Camera";

            // Setup the main camera reference
            m_MonoCamera = GetComponentInChildren<Camera>();
            #if UNITY_IOS
            m_MonoCamera.gameObject.AddComponent<iOS.HoloKitVideoRecorder>();
            #endif

            // Setup the StereoTrackedPose GameObject
            GameObject stereoTrackedPoseGo = new();
            stereoTrackedPoseGo.name = "Stereo Tracked Pose";
            stereoTrackedPoseGo.transform.SetParent(transform);
            stereoTrackedPoseGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Setup the CenterEyePose GameObject
            GameObject centerEyePoseGo = new();
            centerEyePoseGo.name = "Center Eye Pose";
            centerEyePoseGo.transform.SetParent(stereoTrackedPoseGo.transform);
            centerEyePoseGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_CenterEyePose = centerEyePoseGo.transform;

            // Setup the BlackCamera GameObject
            GameObject blackCameraGo = new();
            blackCameraGo.name = "Black Camera";
            blackCameraGo.transform.SetParent(centerEyePoseGo.transform);
            blackCameraGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_BlackCamera = blackCameraGo.AddComponent<Camera>();
            m_BlackCamera.clearFlags = CameraClearFlags.SolidColor;
            m_BlackCamera.backgroundColor = Color.black;
            m_BlackCamera.cullingMask = 0;
            m_BlackCamera.depth = -1;

            // Setup the LeftEyeCamera GameObject
            GameObject leftEyeCameraGo = new();
            leftEyeCameraGo.name = "Left Eye Camera";
            leftEyeCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_LeftEyeCamera = leftEyeCameraGo.AddComponent<Camera>();
            m_LeftEyeCamera.clearFlags = CameraClearFlags.SolidColor;
            m_LeftEyeCamera.backgroundColor = Color.black;

            // Setup the RightEyeCamera GameObject
            GameObject rightEyeCameraGo = new();
            rightEyeCameraGo.name = "Right Eye Camera";
            rightEyeCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_RightEyeCamera = rightEyeCameraGo.AddComponent<Camera>();
            m_RightEyeCamera.clearFlags = CameraClearFlags.SolidColor;
            m_RightEyeCamera.backgroundColor = Color.black;

            // The SDK's phone model lists, by asset GUID so they load whatever the package id
            // (a path under the old "com.holokit.xr.holokit" id returned null).
            m_iOSPhoneModelList = LoadByGuid<PhoneModelList>(IOSPhoneModelListGuid);
            m_DefaultAndroidPhoneModelList = LoadByGuid<PhoneModelList>(DefaultAndroidPhoneModelListGuid);

            SetupCameraData();

            m_BlackCamera.gameObject.SetActive(false);
            m_LeftEyeCamera.gameObject.SetActive(false);
            m_RightEyeCamera.gameObject.SetActive(false);

            stereoTrackedPoseGo.AddComponent<LowLatencyTrackingManager>();

            var xrOrigin = GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            xrOrigin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
#endif

        private void Awake()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if UNITY_IOS
            UnityEngine.iOS.Device.hideHomeButton = true;
#elif UNITY_ANDROID

#endif
            SetupCameraData();
            m_ARCameraBackground = GetComponentInChildren<ARCameraBackground>();
        }

        private void OnDestroy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            m_AppleNative?.Dispose();
            m_AppleNative = null;
#endif
        }

        private void Update()
        {
            if (m_ScreenRenderMode == ScreenRenderMode.Stereo)
            {
                UpdateThermalFrameRate();

                // In Stereo mode, ensure the screen orientation is always LeftScapeLeft
                if (Screen.orientation != ScreenOrientation.LandscapeLeft)
                {
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                }

                // Set screen brightness to 1.0
                if (Screen.brightness < 1.0f)
                {
                    // If we don't do this, the screen brightness cannot be directly set to 1.0.
                    Screen.brightness += 0.005f;
                    Screen.brightness = 1.0f;
                }
            }
        }

        /// <summary>
        /// Setup the camera rendering data for Stereo mode based on the current HoloKit model and phone model.
        /// </summary>
        private void SetupCameraData()
        {
            if (!DoesCurrentPhoneModelSupportStereoMode())
            {
                Debug.LogWarning($"Device {SystemInfo.deviceModel} does not support Stereo mode");
                return;
            }

            HoloKitModelSpecs holokitModelSpecs = DeviceProfile.GetHoloKitModelSpecs(m_HoloKitGeneration);
            m_PhoneModel = GetCurrentPhoneModel();
            SetupCameraData(holokitModelSpecs, m_PhoneModel.ModelSpecs);
        }

        /// <summary>
        /// Setup the camera rendering data for Stereo mode based on the given Holokit model and phone model.
        /// </summary>
        /// <param name="holokitModelSpecs">The given HoloKit model specs</param>
        /// <param name="phoneModelSpecs">The given phone model specs</param>
        private void SetupCameraData(HoloKitModelSpecs holokitModelSpecs, PhoneModelSpecs phoneModelSpecs)
        {
            float screenDpi = phoneModelSpecs.ScreenDpi == 0f ? Screen.dpi : phoneModelSpecs.ScreenDpi;
            float screenWidth = phoneModelSpecs.ScreenResolution == Vector2.zero ? HoloKitScreenUtils.GetScreenWidth() : phoneModelSpecs.ScreenResolution.x;
            float screenHeight = phoneModelSpecs.ScreenResolution == Vector2.zero ? HoloKitScreenUtils.GetScreenHeight() : phoneModelSpecs.ScreenResolution.y;
            float screenWidthInMeters = screenWidth / screenDpi * HoloKitScreenUtils.InchToMeter;
            float screenHeightInMeters = screenHeight / screenDpi * HoloKitScreenUtils.InchToMeter;

            float viewportWidthInMeters = holokitModelSpecs.ViewportInner + holokitModelSpecs.ViewportOuter;
            float viewportHeightInMeters = holokitModelSpecs.ViewportTop + holokitModelSpecs.ViewportBottom;
            float nearClipPlane = holokitModelSpecs.LensToEye;
            float viewportsFullWidthInMeters = holokitModelSpecs.OpticalAxisDistance + 2f * holokitModelSpecs.ViewportOuter;
            float viewportWidthGap = viewportsFullWidthInMeters - viewportWidthInMeters * 2f;

            // Calculate projection matrices
            Matrix4x4 leftProjMat = Matrix4x4.zero;
            leftProjMat[0, 0] = 2f * nearClipPlane / viewportWidthInMeters;
            leftProjMat[1, 1] = 2f * nearClipPlane / viewportHeightInMeters;
            leftProjMat[0, 2] = (m_Ipd - viewportWidthInMeters - viewportWidthGap) / viewportWidthInMeters;
            leftProjMat[2, 2] = (-m_FarClipPlane - nearClipPlane) / (m_FarClipPlane - nearClipPlane);
            leftProjMat[2, 3] = -2f * m_FarClipPlane * nearClipPlane / (m_FarClipPlane - nearClipPlane);
            leftProjMat[3, 2] = -1f;
            leftProjMat[3, 3] = 0f;

            Matrix4x4 rightProjMat = leftProjMat;
            rightProjMat[0, 2] = -leftProjMat[0, 2];

            // Calculate viewport rects
            float fullWidth = viewportsFullWidthInMeters / screenWidthInMeters;
            float width = viewportWidthInMeters / screenWidthInMeters;
            float height = viewportHeightInMeters / screenHeightInMeters;
            float centerX = 0.5f;
            float centerY = ((phoneModelSpecs.ViewportBottomOffset != 0f) || phoneModelSpecs.ScreenBottomBorder == 0f) ?
                (phoneModelSpecs.ViewportBottomOffset + viewportHeightInMeters / 2f) / screenHeightInMeters :
                (holokitModelSpecs.AxisToBottom - phoneModelSpecs.ScreenBottomBorder) / screenHeightInMeters;

            float xMinLeft = centerX - fullWidth / 2f;
            float xMaxLeft = xMinLeft + width;
            float xMinRight = centerX + fullWidth / 2f - width;
            float xMaxRight = xMinRight + width;
            float yMin = centerY - height / 2f;
            float yMax = centerY + height / 2f;

            Rect leftViewportRect = Rect.MinMaxRect(xMinLeft, yMin, xMaxLeft, yMax);
            Rect rightViewportRect = Rect.MinMaxRect(xMinRight, yMin, xMaxRight, yMax);

            // Calculate offsets
            Vector3 cameraToCenterEyeOffset = phoneModelSpecs.CameraOffset + holokitModelSpecs.MrOffset;
            Vector3 centerEyeToLeftEyeOffset = new(-m_Ipd / 2f, 0f, 0f);
            Vector3 centerEyeToRightEyeOffset = new(m_Ipd / 2f, 0f, 0f);

            // Apply camera data
            m_LeftEyeCamera.transform.localPosition = centerEyeToLeftEyeOffset;
            m_RightEyeCamera.transform.localPosition = centerEyeToRightEyeOffset;

            // Setup left eye camera
            m_LeftEyeCamera.nearClipPlane = nearClipPlane;
            m_LeftEyeCamera.farClipPlane = m_FarClipPlane;
            m_LeftEyeCamera.rect = leftViewportRect;
#if !UNITY_EDITOR
            m_LeftEyeCamera.projectionMatrix = leftProjMat;
#endif

            // Setup right eye camera
            m_RightEyeCamera.nearClipPlane = nearClipPlane;
            m_RightEyeCamera.farClipPlane = m_FarClipPlane;
            m_RightEyeCamera.rect = rightViewportRect;
#if !UNITY_EDITOR
            m_RightEyeCamera.projectionMatrix = rightProjMat;
#endif

            m_CameraToCenterEyeOffset = cameraToCenterEyeOffset;
            if (m_ScreenRenderMode == ScreenRenderMode.Stereo)
                m_CenterEyePose.localPosition = m_CameraToCenterEyeOffset;
        }

        /// <summary>
        /// Check if the current phone model supports Stereo mode.
        /// </summary>
        /// <returns>True if the curent phone model supports Stereo mode</returns>
        private bool DoesCurrentPhoneModelSupportStereoMode()
        {
#if UNITY_EDITOR
            return true;
#elif UNITY_IOS
            string modelName = SystemInfo.deviceModel;
            foreach (PhoneModel phoneModel in m_iOSPhoneModelList.PhoneModels)
            {
                if (modelName.Equals(phoneModel.ModelName))
                    return true;
            }
            return false;
#elif UNITY_ANDROID
            string modelName = SystemInfo.deviceModel;
            foreach (PhoneModel phoneModel in m_DefaultAndroidPhoneModelList.PhoneModels)
            {
                if (modelName.Equals(phoneModel.ModelName))
                    return true;
            }
            if (m_CustomAndroidPhoneModelList != null)
            {
                foreach (PhoneModel phoneModel in m_CustomAndroidPhoneModelList.PhoneModels)
                {
                    if (modelName.Equals(phoneModel.ModelName))
                        return true;
                }
            }
            return false;
#endif
        }

        /// <summary>
        /// Get the current phone model spces if the device is supported.
        /// </summary>
        /// <returns>The current phone model spces</returns>
        private PhoneModel GetCurrentPhoneModel()
        {
#if UNITY_EDITOR
            return DeviceProfile.GetDefaultPhoneModel();
#elif UNITY_IOS
            string modelName = SystemInfo.deviceModel;
            foreach (PhoneModel phoneModel in m_iOSPhoneModelList.PhoneModels)
            {
                if (modelName.Equals(phoneModel.ModelName))
                    return phoneModel;
            }
            return DeviceProfile.GetDefaultPhoneModel();
#elif UNITY_ANDROID
            string modelName = SystemInfo.deviceModel;
            foreach (PhoneModel phoneModel in m_DefaultAndroidPhoneModelList.PhoneModels)
            {
                if (modelName.Equals(phoneModel.ModelName))
                    return phoneModel;
            }
            if (m_CustomAndroidPhoneModelList != null)
            {
                foreach (PhoneModel phoneModel in m_CustomAndroidPhoneModelList.PhoneModels)
                {
                    if (modelName.Equals(phoneModel.ModelName))
                        return phoneModel;
                }
            }
            return DeviceProfile.GetDefaultPhoneModel();     
#endif
        }

        /// <summary>
        /// The alignment guide, created the first time Stereo starts; null before, or when it is turned off.
        /// </summary>
        public HoloKitAlignmentGuide AlignmentGuide => m_AlignmentGuide;

        /// <summary>
        /// Show the alignment guide (headset outline and marker), creating it the first time.
        /// </summary>
        private void SpawnAlignmentGuide()
        {
            if (!m_ShowAlignmentMarkerInStereoMode)
                return;
            if (m_AlignmentGuide)
            {
                m_AlignmentGuide.gameObject.SetActive(true);
                return;
            }
            m_AlignmentGuide = new GameObject("HoloKit Alignment Guide").AddComponent<HoloKitAlignmentGuide>();
            m_AlignmentGuide.Initialize(this, m_AlignmentGuideAutoHideDelay);
        }

        internal Rect LeftViewport => m_LeftEyeCamera != null ? m_LeftEyeCamera.rect : default;
        internal Rect RightViewport => m_RightEyeCamera != null ? m_RightEyeCamera.rect : default;

        /// <summary>
        /// Show the stereo bar, creating it the first time.
        /// </summary>
        private void SpawnStereoBar()
        {
            if (m_StereoBarItems == HoloKitStereoBarItems.None)
                return;
            if (m_StereoBar)
            {
                m_StereoBar.gameObject.SetActive(true);
                return;
            }
            m_StereoBar = new GameObject("HoloKit Stereo Bar").AddComponent<HoloKitStereoBar>();
            m_StereoBar.Initialize(this, m_StereoBarItems, m_ShowStereoBarTouchAreas);
        }

        internal void InvokeStereoTrigger() => OnStereoTrigger?.Invoke();

        /// <summary>
        /// Hides the alignment marker while a stereo bar control is held, as the HoloKit app does.
        /// </summary>
        internal void SetAlignmentMarkerSuppressed(bool suppressed)
        {
            if (m_AlignmentGuide)
                m_AlignmentGuide.MarkerSuppressed = suppressed;
        }

        /// <summary>
        /// The stereo bar's geometry for this phone: screen pixels per millimetre, and the layout
        /// in millimetres (alignment marker, half the screen width, and the band from the screen's
        /// top edge to 2.5 mm above the viewports, where the headset frame sits).
        /// </summary>
        internal bool TryGetStereoBarGeometry(out float pixelsPerMillimetre, out HoloKitStereoBarLayout layout)
        {
            pixelsPerMillimetre = 0f;
            layout = default;
            if (m_LeftEyeCamera == null)
                return false;
            var specs = m_PhoneModel.ModelSpecs;
            float dpi = specs.ScreenDpi != 0f ? specs.ScreenDpi : Screen.dpi;
            float width = HoloKitScreenUtils.GetScreenWidth(), height = HoloKitScreenUtils.GetScreenHeight();
            // Millimetres from the profile's native resolution, as the viewports are; Unity may
            // render fewer pixels than that.
            float nativeWidth = specs.ScreenResolution == Vector2.zero ? width : specs.ScreenResolution.x;
            float nativeHeight = specs.ScreenResolution == Vector2.zero ? height : specs.ScreenResolution.y;
            if (dpi <= 0f || nativeWidth <= 0f || width <= 0f)
                return false;
            var holokit = DeviceProfile.GetHoloKitModelSpecs(m_HoloKitGeneration);
            layout = HoloKitStereoBarLayout.ForPhone(dpi, nativeWidth, nativeHeight,
                m_LeftEyeCamera.rect.yMax, holokit.AlignmentMarkerOffset);
            pixelsPerMillimetre = width / (layout.HalfWidth * 2f);
            return true;
        }
    }
}
