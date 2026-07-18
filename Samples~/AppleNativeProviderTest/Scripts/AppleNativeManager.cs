// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;
#if UNITY_IOS
using HoloKit.iOS;
#endif
using TMPro;

namespace HoloKit.Samples.AppleNativeProviderTest
{
    public class AppleNativeManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_ThermalStateText;

        [SerializeField] private TMP_Text m_SystemUptimeText;

#if UNITY_IOS
        private AppleNativeProvider m_Provider;
#endif

        private void Start()
        {
#if UNITY_IOS
            m_Provider = new();
            m_Provider.OnThermalStateChanged += OnThermalStateChanged;
            m_ThermalStateText.text = $"Thermal State: {m_Provider.GetThermalState()}";
#else
            m_ThermalStateText.text = "Thermal State: available on iOS only";
            m_SystemUptimeText.text = "System Uptime: available on iOS only";
#endif
        }

#if UNITY_IOS
        private void OnThermalStateChanged(AppleThermalState thermalState)
        {
            m_ThermalStateText.text = $"Thermal State: {thermalState}";
        }

        private void Update()
        {
            m_SystemUptimeText.text = $"System Uptime: {m_Provider.GetSystemUptime():F4}";
        }
#endif
    }
}
