// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloKit.Samples.HeadInteraction
{
    public class CatController : MonoBehaviour
    {
        [SerializeField]
        Transform m_CatNeck;

        Transform head;
        // Start is called before the first frame update
        void Start()
        {
            head = FindObjectOfType<HoloKitCameraManager>().CenterEyePose;
        }

        private void LateUpdate()
        {
            m_CatNeck.LookAt(head);

            m_CatNeck.eulerAngles += new Vector3(60f,0,0);
        }
    }
}

