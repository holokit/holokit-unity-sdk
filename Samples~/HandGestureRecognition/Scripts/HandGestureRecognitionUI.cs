// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using UnityEngine.UI;
using HoloKit.iOS;

namespace HoloKit.Samples.HandGestureRecognition
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
