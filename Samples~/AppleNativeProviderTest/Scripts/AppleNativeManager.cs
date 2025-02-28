// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
using HoloKit.iOS;
using TMPro;

namespace HoloKit.Samples.AppleNativeProviderTest
{
    public class AppleNativeManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_ThermalStateText;

        [SerializeField] private TMP_Text m_SystemUptimeText;

        private AppleNativeProvider m_Provider;

        private void Start()
        {
            m_Provider = new();
            m_Provider.OnThermalStateChanged += OnThermalStateChanged;
            m_ThermalStateText.text = $"Thermal State: {m_Provider.GetThermalState()}";
        }

        private void OnThermalStateChanged(AppleThermalState thermalState)
        {
            m_ThermalStateText.text = $"Thermal State: {thermalState}";
        }

        private void Update()
        {
            m_SystemUptimeText.text = $"System Uptime: {m_Provider.GetSystemUptime():F4}";
        }
    }
}
