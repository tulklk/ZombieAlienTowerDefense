Shader "AlienDefense/EnergyBallGlowShell"
{
    // Soft additive Fresnel shell around the energy core. Near-invisible in the center; cyan glow on the rim.
    // Cull Front so the outer surface softens the silhouette without reading as a second solid ball.
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (0.271, 0.961, 1.0, 1)
        _GlowStrength ("Glow Strength", Range(0, 5)) = 1.6
        _GlowAlpha ("Glow Alpha", Range(0, 1)) = 0.28
        _RimPower ("Rim Power", Range(0.5, 8)) = 1.4
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.8
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.08
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

        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Front

        Pass
        {
            Name "EnergyBallGlowShell"
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
                half4 _GlowColor;
                float _GlowStrength;
                float _GlowAlpha;
                float _RimPower;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                // Flip normal for Cull Front so Fresnel still uses outward-facing silhouette.
                OUT.normalWS = -TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half3 rgb = _GlowColor.rgb * fresnel * _GlowStrength * pulse;
                float alpha = fresnel * _GlowAlpha * pulse;
                alpha = saturate(alpha);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
