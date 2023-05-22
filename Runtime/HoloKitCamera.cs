using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloInteractive.XR.HoloKit
{
    public enum ScreenRenderMode
    {
        Mono = 0,
        Stereo = 1
    }

    public class HoloKitCamera : MonoBehaviour
    {
        [SerializeField] Camera m_MonoCamera;

        [SerializeField] Camera m_LeftEyeCamera;

        [SerializeField] Camera m_RightEyeCamera;

        [SerializeField] Camera m_BlackCamera;

        [SerializeField] Transform m_CenterEyePose;

        [SerializeField] [Range(0.054f, 0.074f)] float m_Ipd = 0.064f;

        [SerializeField] float m_FarClipPlane = 50f;

        [SerializeField] HoloKitGeneration m_HoloKitGeneration = HoloKitGeneration.HoloKitX;

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
                    if (!DeviceProfile.DoesDeviceSupportStereoMode())
                    {
                        Debug.LogWarning("Device does not support Stereo mode");
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

            GameObject leftEyeCameraGo = new GameObject();
            leftEyeCameraGo.name = "Left Eye Camera";
            leftEyeCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_LeftEyeCamera = leftEyeCameraGo.AddComponent<Camera>();
            m_LeftEyeCamera.clearFlags = CameraClearFlags.SolidColor;
            m_LeftEyeCamera.backgroundColor = Color.black;

            GameObject rightEyeCameraGo = new GameObject();
            rightEyeCameraGo.name = "Right Eye Camera";
            rightEyeCameraGo.transform.SetParent(centerEyePoseGo.transform);
            m_RightEyeCamera = rightEyeCameraGo.AddComponent<Camera>();
            m_RightEyeCamera.clearFlags = CameraClearFlags.SolidColor;
            m_RightEyeCamera.backgroundColor = Color.black;

            SetupCameraData();

            m_MonoCamera = GetComponent<Camera>();
            m_LeftEyeCamera.gameObject.SetActive(false);
            m_RightEyeCamera.gameObject.SetActive(false);
            m_BlackCamera.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (Utils.IsRuntime)
            {
                // Hide the iOS home button
                UnityEngine.iOS.Device.hideHomeButton = true;
                // Prevent the device from sleeping
                Screen.sleepTimeout = SleepTimeout.NeverSleep;

                if (DeviceProfile.DoesDeviceSupportStereoMode())
                {
                    SetupCameraData();
                }
                else
                {
                    Debug.LogWarning("Device does not support Stereo mode");
                }
            }
        }

        private void Update()
        {
            if (m_ScreenRenderMode == ScreenRenderMode.Stereo)
            {
                if (Utils.IsRuntime)
                {
                    // Set screen brightness

                }

                if (Screen.orientation != ScreenOrientation.LandscapeLeft)
                {
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                }
            }
        }

        private void SetupCameraData()
        {
            HoloKitModel holokitModel = DeviceProfile.GetHoloKitModel(m_HoloKitGeneration);
            PhoneModel phoneModel = DeviceProfile.GetPhoneModel(UnityEngine.iOS.Device.generation);

            float viewportWidthInMeters = holokitModel.ViewportInner + holokitModel.ViewportOuter;
            float viewportHeightInMeters = holokitModel.ViewportTop + holokitModel.ViewportBottom;
            float nearClipPlane = holokitModel.LensToEye;
            float viewportsFullWidthInMeters = holokitModel.OpticalAxisDistance + 2f * holokitModel.ViewportOuter;
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
            float centerY = (holokitModel.AxisToBottom - phoneModel.ScreenBottom) / phoneModel.ScreenHeight;
            float fullWidth = viewportsFullWidthInMeters / phoneModel.ScreenWidth;
            float width = viewportWidthInMeters / phoneModel.ScreenWidth;
            float height = viewportHeightInMeters / phoneModel.ScreenHeight;

            float xMinLeft = centerX - fullWidth / 2f;
            float xMaxLeft = xMinLeft + width;
            float xMinRight = centerX + fullWidth / 2f - width;
            float xMaxRight = xMinRight + width;
            float yMin = centerY - height / 2f;
            float yMax = centerY + height / 2f;

            Rect leftViewportRect = Rect.MinMaxRect(xMinLeft, yMin, xMaxLeft, yMax);
            Rect rightViewportRect = Rect.MinMaxRect(xMinRight, yMin, xMaxRight, yMax);

            // 3. Calculate offsets
            Vector3 cameraToCenterEyeOffset = phoneModel.CameraOffset + holokitModel.MrOffset;
            //Vector3 cameraToScreenCenterOffset = phoneModel.CameraOffset + new Vector3(0f, phoneModel.ScreenBottom + (phoneModel.ScreenHeight / 2f), 0f);
            Vector3 centerEyeToLeftEyeOffset = new Vector3(-m_Ipd / 2f, 0f, 0f);
            Vector3 centerEyeToRightEyeOffset = new Vector3(m_Ipd / 2f, 0f, 0f);

            // Apply camera data
            m_LeftEyeCamera.transform.localPosition = centerEyeToLeftEyeOffset;
            m_RightEyeCamera.transform.localPosition = centerEyeToRightEyeOffset;

            // Setup left eye camera
            m_LeftEyeCamera.nearClipPlane = nearClipPlane;
            m_LeftEyeCamera.farClipPlane = m_FarClipPlane;
            m_LeftEyeCamera.rect = leftViewportRect;
            if (Utils.IsRuntime)
                m_LeftEyeCamera.projectionMatrix = leftProjMat;

            // Setup right eye camera
            m_RightEyeCamera.nearClipPlane = nearClipPlane;
            m_RightEyeCamera.farClipPlane = m_FarClipPlane;
            m_RightEyeCamera.rect = rightViewportRect;
            if (Utils.IsRuntime)
                m_RightEyeCamera.projectionMatrix = rightProjMat;

            m_CameraToCenterEyeOffset = cameraToCenterEyeOffset;
        }
    }
}
