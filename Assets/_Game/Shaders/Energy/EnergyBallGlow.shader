Shader "AlienDefense/EnergyBallGlow"
{
    // Opaque sci-fi energy core: base blue + bright center + soft Fresnel rim + light pulse + procedural noise.
    // Self-lit (Unlit); looks emissive even when Bloom is off. SRP Batcher friendly.
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.0, 0.455, 0.851, 1)
        [HDR] _CoreColor ("Core Color", Color) = (0.333, 1.0, 1.0, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.549, 1.0, 1.0, 1)

        _EmissionStrength ("Emission Strength", Range(0, 5)) = 2.0
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.0
        _RimStrength ("Rim Strength", Range(0, 5)) = 1.4
        _CoreIntensity ("Core Intensity", Range(0, 5)) = 1.3

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.12

        _NoiseScale ("Noise Scale", Range(0, 16)) = 3.0
        _NoiseSpeed ("Noise Speed", Range(0, 8)) = 0.7
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite On
        ZTest LEqual
        Blend One Zero

        Pass
        {
            Name "EnergyBallCore"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
                half4 _RimColor;
                float _EmissionStrength;
                float _RimPower;
                float _RimStrength;
                float _CoreIntensity;
                float _PulseSpeed;
                float _PulseAmount;
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
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float ndotv = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - ndotv, _RimPower);
                float center = pow(ndotv, 1.5);

                half3 rim = _RimColor.rgb * fresnel * _RimStrength;
                half3 core = _CoreColor.rgb * center * _CoreIntensity;

                float n1 = sin(IN.positionWS.x * _NoiseScale + _Time.y * _NoiseSpeed);
                float n2 = sin(IN.positionWS.y * (_NoiseScale * 1.3) - _Time.y * _NoiseSpeed);
                float n3 = sin(IN.positionWS.z * (_NoiseScale * 0.8) + _Time.y * (_NoiseSpeed * 1.2));
                float noise = (n1 + n2 + n3) * (1.0 / 3.0);
                noise = noise * 0.5 + 0.5;

                half3 color = _BaseColor.rgb + core + rim;
                color += _CoreColor.rgb * noise * _NoiseStrength;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                color *= pulse;

                half3 emission = color * _EmissionStrength;
                return half4(emission, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
