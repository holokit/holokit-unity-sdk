// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace HoloKit
{
    public class GazeRaycastInteractor : MonoBehaviour
    {
        public IGazeRaycastInteractable Target => m_Target;

        private Transform m_CenterEyePose;

        private IGazeRaycastInteractable m_Target;

        private void Start()
        {
            var holokitCameraManager = FindObjectOfType<HoloKitCameraManager>();
            m_CenterEyePose = holokitCameraManager.CenterEyePose;
        }

        private void Update()
        {
            Ray ray = new Ray(m_CenterEyePose.position, m_CenterEyePose.forward);
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                if (hitInfo.transform.TryGetComponent<IGazeRaycastInteractable>(out var interactable))
                {
                    // A new target is selected
                    if (m_Target == null)
                    {
                        m_Target = interactable;
                        m_Target.OnSelectionEntered();
                        m_Target.OnSelected(Time.deltaTime);
                    }
                    // Still selecting the old target
                    else if (m_Target == interactable)
                    {
                        m_Target.OnSelected(Time.deltaTime);
                    }
                    // Target switched in the last frame
                    else if (m_Target != interactable)
                    {
                        m_Target.OnSelectionExited();
                        m_Target = interactable;
                        m_Target.OnSelectionEntered();
                        m_Target.OnSelected(Time.deltaTime);
                    }
                }
            }
            else
            {
                if (m_Target != null)
                {
                    m_Target.OnSelectionExited();
                    m_Target = null;
                }
            }
        }
    }
}
