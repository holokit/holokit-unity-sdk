using UnityEngine;
using UnityEngine.UI;

namespace HoloInteractive.XR.HoloKit.Samples.StereoscopicRendering
{
    public class StereoUIManager : MonoBehaviour
    {
        [SerializeField] Text m_BtnText;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        public void SwitchRenderMode()
        {
            var holokitCamera = FindObjectOfType<HoloKitCameraManager>();
            holokitCamera.ScreenRenderMode = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
            m_BtnText.text = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";
        }
    }
}
