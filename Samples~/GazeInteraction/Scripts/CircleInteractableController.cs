// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HoloInteractive.XR.HoloKit.Samples.GazeInteraction
{
    public class CircleInteractableController : MonoBehaviour, IGazeRaycastInteractable
    {
        [SerializeField] private TMP_Text m_PercentageText;

        [SerializeField] private Image m_BackgroundImage;

        [SerializeField] private float m_MaxLoad = 3f;

        [SerializeField] private Color m_NormalBackgroundColor = Color.white;

        [SerializeField] private Color m_ActiveBackgroundColor = Color.green;

        private bool isSelected = false;

        private float load = 0;

        private void Start()
        {
            UpdatePercentageText();
            m_BackgroundImage.color = m_NormalBackgroundColor;
        }

        private void Update()
        {
            if (!isSelected && load > 0f)
            {
                load -= Time.fixedTime;
                if (load < 0f)
                    load = 0f;
                UpdatePercentageText();
                m_BackgroundImage.color = m_NormalBackgroundColor;
            }
        }

        private void UpdatePercentageText()
        {
            m_PercentageText.text = Mathf.FloorToInt(load / m_MaxLoad * 100f).ToString();
        }

        public void OnSelectionEntered()
        {
            isSelected = true;
        }

        public void OnSelectionExited()
        {
            isSelected = false;
        }

        public void OnSelected(float deltaTime)
        {
            load += deltaTime;
            if (load > m_MaxLoad)
            {
                load = m_MaxLoad;
                m_BackgroundImage.color = m_ActiveBackgroundColor;
            }
            UpdatePercentageText();
        }
    }
}
