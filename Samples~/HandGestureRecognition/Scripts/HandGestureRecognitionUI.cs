// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using UnityEngine.UI;
using HoloInteractive.XR.HoloKit.iOS;

namespace HoloInteractive.XR.HoloKit.Samples.HandGestureRecognition
{
    public class HandGestureRecognitionUI : MonoBehaviour
    {
        [SerializeField] HandGestureRecognitionManager m_HandGestureRecognitionManager;

        [SerializeField] Text m_HandGestureText;

        private void Start()
        {
            m_HandGestureRecognitionManager.OnHandGestureChanged += OnHandGestureChanged;
        }

        private void OnHandGestureChanged(HandGesture handGesture)
        {
            m_HandGestureText.text = "Hand Gesture: " + handGesture.ToString();
        }
    }
}
#endif
