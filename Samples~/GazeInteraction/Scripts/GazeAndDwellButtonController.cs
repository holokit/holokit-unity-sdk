// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Sizheng Hao <sizheng@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;

namespace HoloKit.Samples.GazeInteraction
{
    public class GazeAndDwellButtonController : MonoBehaviour, IGazeRaycastInteractable
    {
        [SerializeField] private Transform m_ActiveBG;

        [SerializeField] private float m_MaxLoad = 1f;

        [SerializeField] Image[] m_PageDot = new Image[3];

        private Color m_DotColorInactive = new(.5f, .5f, .5f);
        private Color m_DotColorActive = new(1f, 1f, 1f);

        private int m_CurrentAvtiveDot = 0;

        private bool m_IsSelected = false;

        private float m_Load = 0;

        private float m_PercentageLoad = 0;

        private void Start()
        {
            UpdatePercentageState();
        }

        private void Update()
        {
            if (!m_IsSelected && m_Load > 0f)
            {
                m_Load -= Time.fixedTime;
                if (m_Load < 0f)
                    m_Load = 0f;
                UpdatePercentageState();
            }

            if(m_IsSelected)
            {
                if (m_PercentageLoad == 1)
                {
                    m_Load = 0;
                    UpdatePercentageState();

                    m_CurrentAvtiveDot++;
                    if (m_CurrentAvtiveDot > 2) m_CurrentAvtiveDot = 0;
                    UpdatePageDots();
                }
            }
        }

        private void UpdatePercentageState()
        {
            m_PercentageLoad = m_Load / m_MaxLoad;
            m_ActiveBG.localScale = new Vector3(m_PercentageLoad, 1,1);
        }

        private void UpdatePageDots()
        {

            for (int i = 0; i < m_PageDot.Length; i++)
            {
                if(i == m_CurrentAvtiveDot)
                {
                    m_PageDot[i].color = m_DotColorActive;
                    Debug.Log("set" + i + "to active color");
                }
                else
                {
                    m_PageDot[i].color = m_DotColorInactive;
                }
            }
        }

        public void OnSelectionEntered()
        {
            m_IsSelected = true;
        }

        public void OnSelectionExited()
        {
            m_IsSelected = false;
        }

        public void OnSelected(float deltaTime)
        {
            m_Load += deltaTime;
            if (m_Load > m_MaxLoad)
            {
                m_Load = m_MaxLoad;
            }
            UpdatePercentageState();
        }
    }
}
