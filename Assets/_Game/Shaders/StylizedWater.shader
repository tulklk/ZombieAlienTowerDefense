// Stylized river surface for the farm map: unlit, two scrolling wave layers, a shallow/deep gradient across the
// ribbon and a foam line along the banks.
//
// Why not IgniteCoders' Simple Water Shader (which this project ships): that graph drives its colour from a
// Scene Depth comparison and renders black in this Unity/URP version, and the depth texture is deliberately off
// on the mobile pipeline asset. This does the same read (deep centre, shallow banks, moving surface) with two
// texture samples, no depth buffer, no lighting - so it also stays bright inside the shaded river gully.
//
// Mesh convention: U = 0..1 across the river (0 and 1 are the banks), V = along the river, increasing upstream,
// so the default scroll carries the surface downstream.
Shader "AlienDefense/StylizedWater"
{
    Properties
    {
        [MainTexture] _BaseMap("Wave Texture", 2D) = "white" {}
        _DeepColor("Deep Color", Color) = (0.141, 0.620, 0.769, 0.92)
        _ShallowColor("Shallow Color", Color) = (0.333, 0.851, 0.910, 0.78)
        _FoamColor("Foam / Highlight Color", Color) = (0.875, 1.0, 1.0, 1.0)
        _ScrollSpeed("Flow Speed (UV/sec)", Float) = 0.10
        _SecondTiling("Second Layer Tiling", Vector) = (1.7, 0.8, 0, 0)
        _SecondSpeed("Second Layer Speed", Float) = 0.065
        _CrossDrift("Cross Drift", Float) = 0.012
        _ShallowEdge("Shallow Band Width", Range(0,1)) = 0.55
        _FoamEdge("Bank Foam Width", Range(0,0.5)) = 0.13
        _FoamThreshold("Highlight Threshold", Range(0,1)) = 0.62
        _FoamStrength("Highlight Strength", Range(0,1)) = 0.35
        _Alpha("Overall Alpha", Range(0,1)) = 1.0
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

        Pass
        {
            Name "StylizedWaterUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _ScrollSpeed;
                float4 _SecondTiling;
                float _SecondSpeed;
                float _CrossDrift;
                half _ShallowEdge;
                half _FoamEdge;
                half _FoamThreshold;
                half _FoamStrength;
                half _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv;

                // Both layers travel towards -V (downstream); the second one also drifts slightly across.
                float2 uv1 = TRANSFORM_TEX(uv, _BaseMap) + float2(0.0, _Time.y * _ScrollSpeed);
                float2 uv2 = uv * _SecondTiling.xy + float2(0.21 + _Time.y * _CrossDrift, _Time.y * _SecondSpeed);

                half w1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1).r;
                half w2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2).r;
                half waves = saturate(w1 * 0.6 + w2 * 0.6);

                // Across the ribbon: deep in the middle, shallow towards the banks, foam right at the edge.
                half edge = saturate(abs(uv.x - 0.5) * 2.0);
                half shallow = smoothstep(1.0 - _ShallowEdge, 1.0, edge);
                half3 colour = lerp(_DeepColor.rgb, _ShallowColor.rgb, shallow);

                // Moving highlights on the crests, plus the foam line where the water meets the bank.
                half crest = smoothstep(_FoamThreshold, 1.0, waves) * _FoamStrength;
                colour = lerp(colour, _FoamColor.rgb, crest);
                half bankFoam = smoothstep(1.0 - _FoamEdge, 1.0, edge) * (0.55 + 0.45 * waves);
                colour = lerp(colour, _FoamColor.rgb, bankFoam);

                half alpha = lerp(_DeepColor.a, _ShallowColor.a, shallow);
                alpha = saturate(alpha + crest * 0.4 + bankFoam * 0.3) * _Alpha;
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
