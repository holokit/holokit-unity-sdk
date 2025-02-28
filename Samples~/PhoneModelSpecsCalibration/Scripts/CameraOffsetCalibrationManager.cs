// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.Samples.PhoneModelSpecsCalibration
{
    public class CameraOffsetCalibrationManager : MonoBehaviour
    {
        [SerializeField] HoloKitCameraManager m_HoloKitCameraManager;

        [SerializeField] Text m_CameraOffsetText;

        [SerializeField] Toggle m_ToggleX;

        [SerializeField] Toggle m_ToggleY;

        [SerializeField] Toggle m_ToggleZ;

        [SerializeField] Text m_DecreaseBtnText;

        [SerializeField] Text m_IncreaseBtnText;

        int m_SelectedAxisIndex = 0;

        bool m_IsToggling = false;

        const float CAMERA_OFFSET_STEP = 0.00100f;

        private void Start()
        {
            m_HoloKitCameraManager.ScreenRenderMode = ScreenRenderMode.Stereo;

            PhoneModel phoneModel = m_HoloKitCameraManager.PhoneModel;
            if (phoneModel.ModelSpecs.CameraOffset == Vector3.zero)
            {
                phoneModel.ModelSpecs.CameraOffset = new(0.042000f, -0.05810f, -0.00730f);
                m_HoloKitCameraManager.PhoneModel = phoneModel;
            }
            m_CameraOffsetText.text = "CameraOffset: " + phoneModel.ModelSpecs.CameraOffset.ToString("F5");
        }

        public void OnIncreaseBtnPressed()
        {
            PhoneModel phoneModel = m_HoloKitCameraManager.PhoneModel;
            if (m_SelectedAxisIndex == 0)
            {
                phoneModel.ModelSpecs.CameraOffset.x += CAMERA_OFFSET_STEP;
            }
            else if (m_SelectedAxisIndex == 1)
            {
                phoneModel.ModelSpecs.CameraOffset.y += CAMERA_OFFSET_STEP;
            }
            else
            {
                phoneModel.ModelSpecs.CameraOffset.z += CAMERA_OFFSET_STEP;
            }
            m_HoloKitCameraManager.PhoneModel = phoneModel;
            m_CameraOffsetText.text = "CameraOffset: " + phoneModel.ModelSpecs.CameraOffset.ToString("F5");

            Vibrator.Vibrate();
        }

        public void OnDecreaseBtnPressed()
        {
            PhoneModel phoneModel = m_HoloKitCameraManager.PhoneModel;
            if (m_SelectedAxisIndex == 0)
            {
                phoneModel.ModelSpecs.CameraOffset.x -= CAMERA_OFFSET_STEP;
            }
            else if (m_SelectedAxisIndex == 1)
            {
                phoneModel.ModelSpecs.CameraOffset.y -= CAMERA_OFFSET_STEP;
            }
            else
            {
                phoneModel.ModelSpecs.CameraOffset.z -= CAMERA_OFFSET_STEP;
            }
            m_HoloKitCameraManager.PhoneModel = phoneModel;
            m_CameraOffsetText.text = "CameraOffset: " + phoneModel.ModelSpecs.CameraOffset.ToString("F5");

            Vibrator.Vibrate();
        }

        public void OnToggledAxis(int axisIndex)
        {
            if (m_IsToggling)
                return;

            if (axisIndex == 0)
            {
                if (!m_ToggleX.isOn)
                {
                    m_IsToggling = true;
                    m_ToggleX.isOn = true;
                    m_IsToggling = false;
                }
                else
                {
                    m_SelectedAxisIndex = axisIndex;
                    m_DecreaseBtnText.text = "Decrease X";
                    m_IncreaseBtnText.text = "Increase X";
                    m_IsToggling = true;
                    m_ToggleY.isOn = false;
                    m_ToggleZ.isOn = false;
                    m_IsToggling = false;
                    Vibrator.Vibrate();
                }
            }
            else if (axisIndex == 1)
            {
                if (!m_ToggleY.isOn)
                {
                    m_IsToggling = true;
                    m_ToggleY.isOn = true;
                    m_IsToggling = false;
                }
                else
                {
                    m_SelectedAxisIndex = axisIndex;
                    m_DecreaseBtnText.text = "Decrease Y";
                    m_IncreaseBtnText.text = "Increase Y";
                    m_IsToggling = true;
                    m_ToggleX.isOn = false;
                    m_ToggleZ.isOn = false;
                    m_IsToggling = false;
                    Vibrator.Vibrate();
                }
            }
            else
            {
                if (!m_ToggleZ.isOn)
                {
                    m_IsToggling = true;
                    m_ToggleZ.isOn = true;
                    m_IsToggling = false;
                }
                else
                {
                    m_SelectedAxisIndex = axisIndex;
                    m_DecreaseBtnText.text = "Decrease Z";
                    m_IncreaseBtnText.text = "Increase Z";
                    m_IsToggling = true;
                    m_ToggleX.isOn = false;
                    m_ToggleY.isOn = false;
                    m_IsToggling = false;
                    Vibrator.Vibrate();
                }
            }
        }

        public void OnReturnBtnPressed()
        {
            SceneManager.LoadScene("PhoneModelSpecsCalibration", LoadSceneMode.Single);
            LoaderUtility.Deinitialize();
            LoaderUtility.Initialize();
        }
    }
}
