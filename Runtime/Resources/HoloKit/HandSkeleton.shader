// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

// Unlit colour drawn over the camera image and the scene (ZTest Always), so AR occlusion
// never hides the hand skeleton (HandSkeletonView). Loaded from Resources, so builds include it.
Shader "Hidden/HoloKit/HandSkeleton"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 0, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ZTest Always
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
