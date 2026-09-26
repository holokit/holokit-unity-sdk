// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace HoloKit
{
    /// <summary>
    /// The HoloKit model specs needed for rendering parameter calculation.
    /// </summary>
    [Serializable]
    public struct HoloKitModelSpecs
    {
        // Distance beetween the eyes
        public float OpticalAxisDistance;

        // 3D offset from the center of bottomline of the holokit phone display to the center of the two eyes
        public Vector3 MrOffset;

        // Eye view area width
        public float ViewportInner;

        // Eye view area height
        public float ViewportOuter;

        // Eye view area spillter width
        public float ViewportTop;

        // Eye view area spillter width
        public float ViewportBottom;

        // Fresnel To eye distance
        public float LensToEye;

        // Bottom of the holder to center of the view
        public float AxisToBottom;

        // The distance between the center of the HME and the marker
        public float AlignmentMarkerOffset;
    }
}
