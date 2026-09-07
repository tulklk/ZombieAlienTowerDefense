Shader "AlienDefense/RingIndicator"
{
    // A thin flat cylinder (BuildNode's Available/Disabled/Selected indicators) rendered as a hollow ring
    // outline instead of a solid filled disc: the fragment discards everything inside _InnerRadius, keeping
    // only a border band out to the mesh's own edge (radius 0.5 on Unity's built-in Cylinder primitive).
    // Uses object-space XZ distance from center rather than the mesh's UVs, so it doesn't depend on exactly how
    // Unity's primitive cylinder happens to map its cap UVs.
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.85, 0.2, 0.7)
        _InnerRadius ("Inner Radius (0-1, fraction of the disc's own radius)", Range(0, 0.95)) = 0.82
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.3)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Ring"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionOSxz : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _InnerRadius;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOSxz = IN.positionOS.xz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Unity's built-in Cylinder primitive has radius 0.5 in object space, so this ranges 0 (center)
                // to 1 (outer rim) — the ring's own Transform scale is what sets its actual world-space size.
                float dist = length(IN.positionOSxz) / 0.5;

                float ring = smoothstep(_InnerRadius - _EdgeSoftness, _InnerRadius, dist);
                ring *= smoothstep(1.0, 1.0 - _EdgeSoftness, dist); // soften the outer edge too

                half4 color = _BaseColor;
                color.a *= ring;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
