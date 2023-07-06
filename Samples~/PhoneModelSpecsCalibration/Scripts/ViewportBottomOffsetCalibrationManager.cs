// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HoloInteractive.XR.HoloKit.Samples.PhoneModelSpecsCalibration
{
    public class ViewportBottomOffsetCalibrationManager : MonoBehaviour
    {
        [SerializeField] HoloKitCameraManager m_HoloKitCameraManager;

        [SerializeField] Text m_ViewportBottomOffsetText;

        const float VIEWPORT_BOTTOM_OFFSET_STEP = 0.00005f;

        private void Start()
        {
            m_HoloKitCameraManager.ScreenRenderMode = ScreenRenderMode.Stereo;

            PhoneModel phoneModel = m_HoloKitCameraManager.PhoneModel;
            m_ViewportBottomOffsetText.text = "ViewportBottomOffset: " + phoneModel.ModelSpecs.ViewportBottomOffset.ToString("F5");
        }

        public void OnUpBtnPressed()
        {
            PhoneModel phoneModel = m_HoloKitCameraManager.PhoneModel;
            phoneModel.ModelSpecs.ViewportBottomOffset += VIEWPORT_BOTTOM_OFFSET_STEP;
            m_HoloKitCameraManager.PhoneModel = phoneModel;
            m_ViewportBottomOffsetText.text = "ViewportBottomOffset: " + phoneModel.ModelSpecs.ViewportBottomOffset.ToString("F5");

            Vibrator.Vibrate();
        }

        public void OnDownBtnPressed()
        {
            PhoneModel phoneModel = m_HoloKitCameraManager.PhoneModel;
            phoneModel.ModelSpecs.ViewportBottomOffset -= VIEWPORT_BOTTOM_OFFSET_STEP;
            m_HoloKitCameraManager.PhoneModel = phoneModel;
            m_ViewportBottomOffsetText.text = "ViewportBottomOffset: " + phoneModel.ModelSpecs.ViewportBottomOffset.ToString("F5");

            Vibrator.Vibrate();
        }

        public void OnReturnBtnPressed()
        {
            SceneManager.LoadScene("PhoneModelSpecsCalibration", LoadSceneMode.Single);
        }
    }
}
