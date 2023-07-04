// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using HoloInteractive.XR.HoloKit.iOS;

namespace HoloInteractive.XR.HoloKit.Samples.HandGestureRecognition
{
    public class HandGestureRecognitionUI : MonoBehaviour
    {
        [SerializeField] Text m_BtnText;

        [SerializeField] Text m_HandGestureText;

        [SerializeField] HandGestureRecognitionManager m_HandGestureRecognitionManager;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            m_HandGestureRecognitionManager.OnHandGestureChanged += OnHandGestureChanged;
        }

        public void SwitchRenderMode()
        {
            var holokitCamera = FindObjectOfType<HoloKitCameraManager>();
            holokitCamera.ScreenRenderMode = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
            m_BtnText.text = holokitCamera.ScreenRenderMode == ScreenRenderMode.Mono ? "Stereo" : "Mono";
        }

        private void OnHandGestureChanged(HandGesture handGesture)
        {
            m_HandGestureText.text = "Hand Gesture: " + handGesture.ToString();
        }
    }
}
