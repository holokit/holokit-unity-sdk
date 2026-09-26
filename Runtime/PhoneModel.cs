// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace HoloKit
{
    [Serializable]
    public struct PhoneModel
    {
        [Tooltip("Apple's identifier for each device.")]
        public string ModelName;

        [Tooltip("Provides clarity for internal developers, supplementing the less descriptive Model Name")]
        public string Description;

        public PhoneModelSpecs ModelSpecs;
    }
}
