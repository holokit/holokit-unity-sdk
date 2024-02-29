// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HoloKit.Samples.PhoneModelSpecsCalibration
{
    public class PhoneModelSpecsCalibrationUI : MonoBehaviour
    {
        [SerializeField] Text m_ModelName;

        [SerializeField] Text m_ScreenResolution;

        [SerializeField] Text m_ScreenDpi;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            m_ModelName.text = "Model Name: " + SystemInfo.deviceModel;
            m_ScreenResolution.text = $"Screen Resolution: ({Utils.GetScreenWidth()}, {Utils.GetScreenHeight()})";
            m_ScreenDpi.text = "Screen DPI: " + Screen.dpi;
        }

        public void LoadViewportBottomOffsetCalibrationScene()
        {
            SceneManager.LoadScene("ViewportBottomOffsetCalibration", LoadSceneMode.Single);
        }

        public void LoadCameraOffsetCalibrationScene()
        {
            SceneManager.LoadScene("CameraOffsetCalibration", LoadSceneMode.Single);
        }
    }
}

