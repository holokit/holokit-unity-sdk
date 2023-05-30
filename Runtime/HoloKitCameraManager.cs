#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloInteractive.XR.HoloKit
{
    public enum ScreenRenderMode
    {
        Mono = 0,
        Stereo = 1
    }

    public class HoloKitCameraManager : MonoBehaviour
    {
        [Header("Cameras")]
        [SerializeField] Camera m_MonoCamera;

        [SerializeField] Camera m_LeftEyeCamera;

        [SerializeField] Camera m_RightEyeCamera;

        [SerializeField] Camera m_BlackCamera;

        [SerializeField] Transform m_CenterEyePose;

        [Header("Settings")]
        [SerializeField] [Range(0.054f, 0.074f)] float m_Ipd = 0.064f;

        [SerializeField] float m_FarClipPlane = 50f;

        [SerializeField] bool m_ShowAlignmentMarkerInStereoMode = true;

        [Header("Devices")]
        [SerializeField] HoloKitGeneration m_HoloKitGeneration = HoloKitGeneration.HoloKitX;

        [SerializeField] PhoneModelList m_iOSPhoneModelList;

        [SerializeField] PhoneModelList m_DefaultAndroidPhoneModelList;

        [SerializeField] PhoneModelList m_CustomAndroidPhoneModelList;

        ScreenRenderMode m_ScreenRenderMode = ScreenRenderMode.Mono;

        Vector3 m_CameraToCenterEyeOffset;

        public Transform CenterEyePose => m_CenterEyePose;

        public ScreenRenderMode ScreenRenderMode
        {
            get => m_ScreenRenderMode;
            set
            {
                if (value == ScreenRenderMode.Mono)
                {
                    GetComponent<ARCameraBackground>().enabled = true;
                    m_MonoCamera.enabled = true;
                    m_LeftEyeCamera.gameObject.SetActive(false);
                    m_RightEyeCamera.gameObject.SetActive(false);
                    m_BlackCamera.gameObject.SetActive(false);
                    m_CenterEyePose.localPosition = Vector3.zero;
                    m_ScreenRenderMode = ScreenRenderMode.Mono;
                }
                else // Stereo
                {
                    if (!DoesCurrentPhoneSupportStereoMode())
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
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (transform.childCount > 0)
                return;

            gameObject.name = "HoloKit Camera";

            GameObject centerEyePoseGo = new GameObject();
            centerEyePoseGo.name = "Center Eye Pose";
            centerEyePoseGo.transform.SetParent(transform);
            m_CenterEyePose = centerEyePoseGo.transform;

            GameObject blackCameraGo = new GameObject();
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

            PhoneModelList iOSPhoneModelList = AssetDatabase.LoadAssetAtPath<PhoneModelList>("Packages/com.holoi.xr.holokit/Assets/ScriptableObjects/iOSPhoneModelList.asset");
            m_iOSPhoneModelList = iOSPhoneModelList;
            PhoneModelList defaultAndroidPhoneModelList = AssetDatabase.LoadAssetAtPath<PhoneModelList>("Packages/com.holoi.xr.holokit/Assets/ScriptableObjects/DefaultAndroidPhoneModelList.asset");
            m_DefaultAndroidPhoneModelList = defaultAndroidPhoneModelList;

            SetupCameraData();

            m_MonoCamera = GetComponent<Camera>();
            m_LeftEyeCamera.gameObject.SetActive(false);
            m_RightEyeCamera.gameObject.SetActive(false);
            m_BlackCamera.gameObject.SetActive(false);
        }
#endif

        private void Start()
        {

#if UNITY_IOS
            // Hide the iOS home button
            UnityEngine.iOS.Device.hideHomeButton = true;
            // Prevent the device from sleeping
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
            if (!DoesCurrentPhoneSupportStereoMode())
            {
                Debug.LogWarning($"Device {SystemInfo.deviceModel} does not support Stereo mode");
                return;
            }

            HoloKitModelSpecs holokitModelSpecs = DeviceProfile.GetHoloKitModelSpecs(m_HoloKitGeneration);
            PhoneModelSpecs phoneModelSpecs = GetCurrentPhoneModelSpecs();

            float screenWidthInMeters = Utils.GetScreenWidth() / phoneModelSpecs.ScreenDpi * Utils.INCH_TO_METER_RATIO;
            float screenHeightInMeters = Utils.GetScreenHeight() / phoneModelSpecs.ScreenDpi * Utils.INCH_TO_METER_RATIO;

            float viewportWidthInMeters = holokitModelSpecs.ViewportInner + holokitModelSpecs.ViewportOuter;
            float viewportHeightInMeters = holokitModelSpecs.ViewportTop + holokitModelSpecs.ViewportBottom;
            float nearClipPlane = holokitModelSpecs.LensToEye;
            float viewportsFullWidthInMeters = holokitModelSpecs.OpticalAxisDistance + 2f * holokitModelSpecs.ViewportOuter;
            float gap = viewportsFullWidthInMeters - viewportWidthInMeters * 2f;

            // 1. Calculate projection matrices
            Matrix4x4 leftProjMat = Matrix4x4.zero;
            leftProjMat[0, 0] = 2f * nearClipPlane / viewportWidthInMeters;
            leftProjMat[1, 1] = 2f * nearClipPlane / viewportHeightInMeters;
            leftProjMat[0, 2] = (m_Ipd - viewportWidthInMeters - gap) / viewportWidthInMeters;
            leftProjMat[2, 2] = (-m_FarClipPlane - nearClipPlane) / (m_FarClipPlane - nearClipPlane);
            leftProjMat[2, 3] = -2f * m_FarClipPlane * nearClipPlane / (m_FarClipPlane - nearClipPlane);
            leftProjMat[3, 2] = -1f;
            leftProjMat[3, 3] = 0f;

            Matrix4x4 rightProjMat = leftProjMat;
            rightProjMat[0, 2] = -leftProjMat[0, 2];

            // 2. Calculate viewport rects
            float centerX = 0.5f;
            float centerY = (holokitModelSpecs.AxisToBottom - phoneModelSpecs.ScreenBottom) / screenHeightInMeters;
            float fullWidth = viewportsFullWidthInMeters / screenWidthInMeters;
            float width = viewportWidthInMeters / screenWidthInMeters;
            float height = viewportHeightInMeters / screenHeightInMeters;

            float xMinLeft = centerX - fullWidth / 2f;
            float xMaxLeft = xMinLeft + width;
            float xMinRight = centerX + fullWidth / 2f - width;
            float xMaxRight = xMinRight + width;
            float yMin = centerY - height / 2f;
            float yMax = centerY + height / 2f;

            Rect leftViewportRect = Rect.MinMaxRect(xMinLeft, yMin, xMaxLeft, yMax);
            Rect rightViewportRect = Rect.MinMaxRect(xMinRight, yMin, xMaxRight, yMax);

            // 3. Calculate offsets
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
        }

        private bool DoesCurrentPhoneSupportStereoMode()
        {
#if UNITY_IOS
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
#elif UNITY_EDITOR
            return true;
#endif
        }

        private PhoneModelSpecs GetCurrentPhoneModelSpecs()
        {
#if UNITY_IOS
            string modelName = SystemInfo.deviceModel;
            foreach (PhoneModel phoneModel in m_iOSPhoneModelList.PhoneModels)
            {
                if (modelName.Equals(phoneModel.ModelName))
                    return phoneModel.ModelSpecs;
            }
            return DeviceProfile.GetDefaultPhoneModelSpecs();
#elif UNITY_ANDROID
string modelName = SystemInfo.deviceModel;
            foreach (PhoneModel phoneModel in m_DefaultAndroidPhoneModelList.PhoneModels)
            {
                if (modelName.Equals(phoneModel.ModelName))
                    return phoneModel.ModelSpecs;
            }
            if (m_CustomAndroidPhoneModelList != null)
            {
                foreach (PhoneModel phoneModel in m_CustomAndroidPhoneModelList.PhoneModels)
                {
                    if (modelName.Equals(phoneModel.ModelName))
                        return phoneModel.ModelSpecs;
                }
            }
            return DeviceProfile.GetDefaultPhoneModelSpecs();
#elif UNITY_EDITOR
            return DeviceProfile.GetDefaultPhoneModelSpecs();
#endif
        }
    }
}
