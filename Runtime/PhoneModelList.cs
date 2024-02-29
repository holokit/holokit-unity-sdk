// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace HoloKit
{
    [CreateAssetMenu(menuName = "HoloKit/PhoneModelList")]
    public class PhoneModelList : ScriptableObject
    {
        public PhoneModel[] PhoneModels;
    }
}
