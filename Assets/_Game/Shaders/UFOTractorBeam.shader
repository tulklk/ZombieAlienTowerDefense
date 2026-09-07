Shader "AlienDefense/UFOTractorBeam"
{
    // Soft additive tractor beam cone with upward energy streaks.
    // Math lives in UFOTractorBeamFunction.hlsl (shared with Shader Graph Custom Function).
    //
    // Mesh_TractorBeamCone: UV.y = 0 at ground, UV.y = 1 at UFO. Flow subtracts time so
    // streaks travel upward — do not flip that sign without re-verifying mesh UVs.
    Properties
    {
        [HDR] _BeamColor ("Beam Color", Color) = (0.22, 0.95, 0.32, 1)
        [HDR] _CoreColor ("Core / Rim Color", Color) = (0.75, 1, 0.75, 1)
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.18
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 1.0

        [HDR] _FlowColor ("Flow Color", Color) = (0.85, 1, 0.85, 1)
        _FlowSpeed ("Flow Speed", Range(0, 10)) = 1.5
        _FlowDensity ("Flow Density", Range(1, 100)) = 18
        _FlowWidth ("Flow Width", Range(0.01, 1)) = 0.12
        _FlowStrength ("Flow Strength", Range(0, 5)) = 1.1
        _FlowDistortion ("Flow Distortion", Range(0, 1)) = 0.28

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.08

        _EdgeSoftness ("Edge / Rim Softness", Range(0.01, 1)) = 0.25
        _TopFade ("Top Fade", Range(0, 1)) = 0.10
        _BottomFade ("Bottom Fade", Range(0, 1)) = 0.15
        _RadialSoftness ("Rim Strength", Range(0, 1)) = 0.4

        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 3
        _NoiseSpeed ("Noise Speed", Range(0, 5)) = 0.3
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.05
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

        // True additive: FinalColor from UFOTractorBeam_float is already premultiplied by FinalAlpha
        // (matches Shader Graph Additive + Part 13 of the beam spec).
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UFOTractorBeamFunction.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                half4 _CoreColor;
                float _BaseAlpha;
                float _EmissionStrength;
                half4 _FlowColor;
                float _FlowSpeed;
                float _FlowDensity;
                float _FlowWidth;
                float _FlowStrength;
                float _FlowDistortion;
                float _PulseSpeed;
                float _PulseAmount;
                float _EdgeSoftness;
                float _TopFade;
                float _BottomFade;
                float _RadialSoftness;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 finalColor;
                float finalAlpha;

                UFOTractorBeam_float(
                    IN.uv,
                    _Time.y,
                    _BeamColor,
                    _CoreColor,
                    _BaseAlpha,
                    _EmissionStrength,
                    _FlowColor,
                    _FlowSpeed,
                    _FlowDensity,
                    _FlowWidth,
                    _FlowStrength,
                    _FlowDistortion,
                    _PulseSpeed,
                    _PulseAmount,
                    _EdgeSoftness,
                    _TopFade,
                    _BottomFade,
                    _RadialSoftness,
                    _NoiseScale,
                    _NoiseSpeed,
                    _NoiseStrength,
                    IN.normalWS,
                    viewDirWS,
                    finalColor,
                    finalAlpha);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
