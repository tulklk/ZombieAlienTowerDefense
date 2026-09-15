// Translucent faceted ice for the Frost stun crystals (IceStunVisual) and their shards / frost patch. Mobile-friendly: one unlit-style forward pass,
// main light only, no refraction / opaque texture. Faceting comes from the mesh (flat normals), the edges from Fresnel.
// _Alpha is the per-renderer fade driven through a MaterialPropertyBlock.
Shader "AlienDefense/IceCrystal"
{
    Properties
    {
        _BaseColor ("Centre Color (alpha = body opacity)", Color) = (0.725, 0.973, 1, 0.55)
        _EdgeColor ("Edge / Fresnel Color", Color) = (0.518, 0.937, 1, 1)
        _HighlightColor ("Highlight Color", Color) = (0.914, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.6
        _FresnelAlpha ("Fresnel Opacity", Range(0, 1)) = 0.5
        _FacetShading ("Facet Light Contrast", Range(0, 1)) = 0.5
        _GlintPower ("Facet Glint Power", Range(4, 128)) = 40
        _GlintStrength ("Facet Glint Strength", Range(0, 2)) = 0.55
        _Emission ("Emission", Range(0, 1)) = 0.1
        _BottomFrost ("Bottom Frost", Range(0, 1)) = 0.35
        _TipGlow ("Tip Glow", Range(0, 1)) = 0.45
        _Alpha ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "IceCrystalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _HighlightColor;
                half _FresnelPower;
                half _FresnelAlpha;
                half _FacetShading;
                half _GlintPower;
                half _GlintStrength;
                half _Emission;
                half _BottomFrost;
                half _TipGlow;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half height01 : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Crystal meshes are authored with their base at y = 0 and their tip at y = 1.
                output.height01 = saturate(input.positionOS.y);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();

                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), _FresnelPower);
                half glint = pow(saturate(dot(normalWS, normalize(mainLight.direction + viewWS))), _GlintPower) * _GlintStrength;

                // Each flat facet catches the light differently - that is what reads as cut crystal.
                half facet = lerp(1.0, 0.55 + 0.45 * nDotL, _FacetShading);
                half3 color = _BaseColor.rgb * facet;
                color = lerp(color, _HighlightColor.rgb, _BottomFrost * saturate(1.0 - input.height01 * 3.0));
                color = lerp(color, _HighlightColor.rgb, _TipGlow * smoothstep(0.75, 1.0, input.height01));
                color += _EdgeColor.rgb * fresnel;
                color += _HighlightColor.rgb * glint;
                color += _BaseColor.rgb * _Emission;
                color *= input.color.rgb;

                half alpha = saturate(_BaseColor.a + fresnel * _FresnelAlpha + glint * 0.4);
                alpha *= _Alpha * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
