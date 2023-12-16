// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using HoloInteractive.XR.HoloKit.iOS;

namespace HoloInteractive.XR.HoloKit.Samples.GazeGestureInteraction
{
    public class CubeInteractableController : MonoBehaviour, IGazeGestureInteractable
    {
        [SerializeField] private GameObject m_HoverIndicator;

        private MeshRenderer m_MeshRenderer;

        private void Start()
        {
            m_MeshRenderer = GetComponent<MeshRenderer>();
        }

        public void OnGestureSelected()
        {
            m_MeshRenderer.material.color = Random.ColorHSV();
        }

        public void OnSelected(float deltaTime)
        {
            
        }

        public void OnSelectionEntered()
        {
            m_HoverIndicator.SetActive(true);
        }

        public void OnSelectionExited()
        {
            m_HoverIndicator.SetActive(false);
        }
    }
}
