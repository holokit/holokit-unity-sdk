// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

namespace HoloKit
{
    /// <summary>
    /// Mono mode for ARFoundation screen AR and Stereo mode for HoloKit.
    /// </summary>
    public enum ScreenRenderMode
    {
        Mono = 0,
        Stereo = 1
    }

    /// <summary>
    /// The list of supported screen orientations under Mono mode.
    /// </summary>
    [Flags]
    public enum MonoScreenOrientation
    {
        Portrait = 1, // 000001
        PortraitUpsideDown = 2, // 000010
        LandscapeRight = 4, // 000100
        LandscapeLeft = 8 // 001000
    }

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

                    // Deactivate the alignment marker UI
                    if (m_AlignmentMarkerCanvas)
                        m_AlignmentMarkerCanvas.SetActive(false);

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

                    // Activate the alignment marker UI
                    SpawnAlignmentMarker();
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
            }
        }

        /// <summary>
        /// Invoked when the screen render mode changed.
        /// </summary>
        public event Action<ScreenRenderMode> OnScreenRenderModeChanged;

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

        [Tooltip("Whether to show the alignment marker UI in stereo mode?")]
        [SerializeField] bool m_ShowAlignmentMarkerInStereoMode = true;

        [Tooltip("The list of supported screen orientations under Mono mode.")]
        [SerializeField] MonoScreenOrientation m_SupportedMonoScreenOrientations = MonoScreenOrientation.Portrait | MonoScreenOrientation.LandscapeLeft;

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

        GameObject m_AlignmentMarkerCanvas;

        List<ScreenOrientation> m_SupportedScreenOrientations;

        const float ALIGNMENT_MARKER_THICKNESS = 6f; // In pixels

        const float ALIGNMENT_MARKER_HEIGHT_OFFSET = 0.006f; // In meters

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

            // Load the iOS phone model list in the SDK
            m_iOSPhoneModelList = AssetDatabase.LoadAssetAtPath<PhoneModelList>("Packages/com.holokit.xr.holokit/Assets/ScriptableObjects/iOSPhoneModelList.asset");
            // Load the default Android phone model list in the SDK
            m_DefaultAndroidPhoneModelList = AssetDatabase.LoadAssetAtPath<PhoneModelList>("Packages/com.holokit.xr.holokit/Assets/ScriptableObjects/DefaultAndroidPhoneModelList.asset");

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

        private void Update()
        {
            if (m_ScreenRenderMode == ScreenRenderMode.Stereo)
            {
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
            float screenWidth = phoneModelSpecs.ScreenResolution == Vector2.zero ? Utils.GetScreenWidth() : phoneModelSpecs.ScreenResolution.x;
            float screenHeight = phoneModelSpecs.ScreenResolution == Vector2.zero ? Utils.GetScreenHeight() : phoneModelSpecs.ScreenResolution.y;
            float screenWidthInMeters = screenWidth / screenDpi * Utils.INCH_TO_METER_RATIO;
            float screenHeightInMeters = screenHeight / screenDpi * Utils.INCH_TO_METER_RATIO;

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
            //Vector3 cameraToScreenCenterOffset = phoneModel.CameraOffset + new Vector3(0f, phoneModel.ScreenBottom + (phoneModel.ScreenHeight / 2f), 0f);
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
        /// Spawn the alignment marker UI at the appropriate position.
        /// </summary>
        private void SpawnAlignmentMarker()
        {
            if (!m_ShowAlignmentMarkerInStereoMode)
                return;
            if (m_AlignmentMarkerCanvas)
            {
                m_AlignmentMarkerCanvas.SetActive(true);
                return;
            }

            m_AlignmentMarkerCanvas = new();
            m_AlignmentMarkerCanvas.name = "Alignment Marker Canvas";
            var canvas = m_AlignmentMarkerCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject alignmentMarker = new();
            alignmentMarker.name = "Alignment Marker";
            alignmentMarker.transform.SetParent(m_AlignmentMarkerCanvas.transform);
            var img = alignmentMarker.AddComponent<Image>();
            img.color = Color.white;
            var rectTransform = alignmentMarker.GetComponent<RectTransform>();
            rectTransform.pivot = new(0.5f, 1f);
            rectTransform.anchorMin = new(0.5f, 1f);
            rectTransform.anchorMax = new(0.5f, 1f);
            // Calculate anchored position X
            var holokitModelSpecs = DeviceProfile.GetHoloKitModelSpecs(m_HoloKitGeneration);
            var phoneModelSpecs = GetCurrentPhoneModel().ModelSpecs;
            float screenDpi = phoneModelSpecs.ScreenDpi != 0 ? phoneModelSpecs.ScreenDpi : Screen.dpi;
            float posX = holokitModelSpecs.AlignmentMarkerOffset * Utils.METER_TO_INCH_RATIO * screenDpi;
            rectTransform.anchoredPosition = new(posX, 0f);
            // Calculate width and height
            float screenHeight = Utils.GetScreenHeight();
            float heightOffset = ALIGNMENT_MARKER_HEIGHT_OFFSET * Utils.METER_TO_INCH_RATIO * screenDpi / screenHeight;
            float height = (1f - m_LeftEyeCamera.rect.yMax - heightOffset) * screenHeight;
            rectTransform.sizeDelta = new(ALIGNMENT_MARKER_THICKNESS, height);
        }
    }
}
