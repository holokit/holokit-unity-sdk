using UnityEngine;
using UnityEngine.UI;

namespace HoloInteractive.XR.HoloKit.Samples.PhoneModelSpecsCalibration
{
    public class CalibrationUIManager : MonoBehaviour
    {
        [SerializeField] Text m_SwitchRenderModeBtnText;

        [SerializeField] Text m_ModelNameText;

        [SerializeField] Text m_ScreenResolutionText;

        [SerializeField] Text m_ScreenDpiText;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            m_ModelNameText.text = "Model Name: " + SystemInfo.deviceModel;
            m_ScreenResolutionText.text = $"Screen Resolution: ({Utils.GetScreenWidth()}, {Utils.GetScreenHeight()})";
            m_ScreenDpiText.text = $"Screen DPI: {Screen.dpi}";
        }

        public void SwitchRenderMode()
        {
            var holokitCamera = FindObjectOfType<HoloKitCameraManager>();
            holokitCamera.ScreenRenderMode = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
            m_SwitchRenderModeBtnText.text = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";
        }
    }
}
