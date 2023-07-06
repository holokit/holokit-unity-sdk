// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

namespace HoloInteractive.XR.HoloKit
{
    public enum ScreenRenderMode
    {
        Mono = 0,
        Stereo = 1
    }

    [RequireComponent(typeof(LowLatencyTrackingManager))]
    public class HoloKitCameraManager : MonoBehaviour
    {
        public Transform CenterEyePose => m_CenterEyePose;

        public ScreenRenderMode ScreenRenderMode
        {
            get => m_ScreenRenderMode;
            set
            {
                if (value == m_ScreenRenderMode)
                    return;

                if (value == ScreenRenderMode.Mono)
                {
                    GetComponent<ARCameraBackground>().enabled = true;
                    m_MonoCamera.enabled = true;
                    m_LeftEyeCamera.gameObject.SetActive(false);
                    m_RightEyeCamera.gameObject.SetActive(false);
                    m_BlackCamera.gameObject.SetActive(false);
                    m_CenterEyePose.localPosition = Vector3.zero;
                    m_ScreenRenderMode = ScreenRenderMode.Mono;

                    if (m_AlignmentMarkerCanvas)
                        m_AlignmentMarkerCanvas.SetActive(false);
                }
                else // Stereo
                {
                    if (!DoesCurrentPhoneModelSupportStereoMode())
                    {
                        Debug.LogWarning($"Device {SystemInfo.deviceModel} does not support Stereo mode");
                        return;
                    }

                    GetComponent<ARCameraBackground>().enabled = false;
                    m_MonoCamera.enabled = false;
                    m_LeftEyeCamera.gameObject.SetActive(true);
                    m_RightEyeCamera.gameObject.SetActive(true);
                    m_BlackCamera.gameObject.SetActive(true);
                    m_CenterEyePose.localPosition = m_CameraToCenterEyeOffset;
                    m_ScreenRenderMode = ScreenRenderMode.Stereo;

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

        public event Action<ScreenRenderMode> OnScreenRenderModeChanged;

        [SerializeField] Camera m_MonoCamera;

        [SerializeField] Transform m_CenterEyePose;

        [SerializeField] Camera m_BlackCamera;

        [SerializeField] Camera m_LeftEyeCamera;

        [SerializeField] Camera m_RightEyeCamera;

        [Header("Settings")]
        [SerializeField] [Range(0.054f, 0.074f)] float m_Ipd = 0.064f;

        [SerializeField] float m_FarClipPlane = 50f;

        [SerializeField] bool m_ShowAlignmentMarkerInStereoMode = true;

        [Header("Devices")]
        [SerializeField] HoloKitGeneration m_HoloKitGeneration = HoloKitGeneration.HoloKitX;

        [SerializeField] PhoneModelList m_iOSPhoneModelList;

        [SerializeField] PhoneModelList m_DefaultAndroidPhoneModelList;

        [SerializeField] PhoneModelList m_CustomAndroidPhoneModelList;

        PhoneModel m_PhoneModel;

        ScreenRenderMode m_ScreenRenderMode = ScreenRenderMode.Mono;

        Vector3 m_CameraToCenterEyeOffset;

        GameObject m_AlignmentMarkerCanvas;

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

            if (transform.childCount > 0)
                return;

            gameObject.name = "HoloKit Camera";

            GameObject centerEyePoseGo = new();
            centerEyePoseGo.name = "Center Eye Pose";
            centerEyePoseGo.transform.SetParent(transform);
            m_CenterEyePose = centerEyePoseGo.transform;

            GameObject blackCameraGo = new();
            blackCameraGo.name = "Black Camera";
            blackCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_BlackCamera = blackCameraGo.AddComponent<Camera>();
            m_BlackCamera.clearFlags = CameraClearFlags.SolidColor;
            m_BlackCamera.backgroundColor = Color.black;
            m_BlackCamera.cullingMask = 0;
            m_BlackCamera.depth = -1;

            GameObject leftEyeCameraGo = new();
            leftEyeCameraGo.name = "Left Eye Camera";
            leftEyeCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_LeftEyeCamera = leftEyeCameraGo.AddComponent<Camera>();
            m_LeftEyeCamera.clearFlags = CameraClearFlags.SolidColor;
            m_LeftEyeCamera.backgroundColor = Color.black;

            GameObject rightEyeCameraGo = new();
            rightEyeCameraGo.name = "Right Eye Camera";
            rightEyeCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_RightEyeCamera = rightEyeCameraGo.AddComponent<Camera>();
            m_RightEyeCamera.clearFlags = CameraClearFlags.SolidColor;
            m_RightEyeCamera.backgroundColor = Color.black;

            m_iOSPhoneModelList = AssetDatabase.LoadAssetAtPath<PhoneModelList>("Packages/com.holoi.xr.holokit/Assets/ScriptableObjects/iOSPhoneModelList.asset");
            m_DefaultAndroidPhoneModelList = AssetDatabase.LoadAssetAtPath<PhoneModelList>("Packages/com.holoi.xr.holokit/Assets/ScriptableObjects/DefaultAndroidPhoneModelList.asset");

            SetupCameraData();

            m_MonoCamera = GetComponent<Camera>();
            m_BlackCamera.gameObject.SetActive(false);
            m_LeftEyeCamera.gameObject.SetActive(false);
            m_RightEyeCamera.gameObject.SetActive(false);
        }
#endif

        private void Awake()
        {
#if UNITY_IOS
            UnityEngine.iOS.Device.hideHomeButton = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#elif UNITY_ANDROID
            
#endif
            SetupCameraData();
        }

        private void Update()
        {
            if (m_ScreenRenderMode == ScreenRenderMode.Stereo)
            {
                // Set screen brightness

                if (Screen.orientation != ScreenOrientation.LandscapeLeft)
                {
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                }
            }
        }

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
