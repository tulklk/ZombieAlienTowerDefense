// Solid-reading ice for the Frost Tower's lance projectile (a mesh particle). The Particle Pack's "Ice Shapes" graph is a
// pure refraction of Scene Color, which vanishes against the ground from the top-down camera; this one paints the
// silhouette itself: white core where the surface faces the camera, pale cyan body, blue-cyan rim, a little facet light
// and a small emission boost so Bloom gives a soft glow. One pass, main light only, no opaque texture - mobile-friendly.
// Particle meshes are baked in world space, so everything here works from world normals and the particle's vertex color.
Shader "AlienDefense/IceLance"
{
    Properties
    {
        _CoreColor ("Core (facing camera)", Color) = (0.961, 1, 1, 1)
        _BodyColor ("Body", Color) = (0.725, 0.961, 1, 1)
        _EdgeColor ("Edge / Rim", Color) = (0.388, 0.875, 1, 1)
        _CoreWidth ("Core Width", Range(0, 1)) = 0.45
        _EdgeStart ("Edge Start", Range(0, 1)) = 0.55
        _BodyAlpha ("Body Opacity", Range(0, 1)) = 0.8
        _EdgeAlpha ("Edge Opacity", Range(0, 1)) = 0.95
        _FacetShading ("Facet Light Contrast", Range(0, 1)) = 0.35
        _GlintPower ("Glint Power", Range(4, 128)) = 32
        _GlintStrength ("Glint Strength", Range(0, 2)) = 0.6
        _Emission ("Emission (HDR boost for Bloom)", Range(0, 2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "IceLanceForward"
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
                half4 _CoreColor;
                half4 _BodyColor;
                half4 _EdgeColor;
                half _CoreWidth;
                half _EdgeStart;
                half _BodyAlpha;
                half _EdgeAlpha;
                half _FacetShading;
                half _GlintPower;
                half _GlintStrength;
                half _Emission;
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
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();

                half rim = 1.0 - saturate(dot(normalWS, viewWS));
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half glint = pow(saturate(dot(normalWS, normalize(mainLight.direction + viewWS))), _GlintPower) * _GlintStrength;

                // Core -> body -> edge across the silhouette: a clear, readable shard shape from any angle.
                half3 color = lerp(_CoreColor.rgb, _BodyColor.rgb, smoothstep(0.0, max(_CoreWidth, 0.001), rim));
                color = lerp(color, _EdgeColor.rgb, smoothstep(_EdgeStart, 1.0, rim));
                color *= lerp(1.0, 0.75 + 0.35 * nDotL, _FacetShading);
                color += _CoreColor.rgb * glint;
                color *= (1.0 + _Emission) * input.color.rgb;

                half alpha = lerp(_BodyAlpha, _EdgeAlpha, smoothstep(_EdgeStart, 1.0, rim));
                alpha = saturate(alpha + glint * 0.3) * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
