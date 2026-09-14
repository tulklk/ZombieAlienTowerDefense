// Stylized waterfall sheet: two scrolling streak layers on an unlit transparent surface.
//
// Deliberately not the Simple Water Shader: that one is a lit surface shader whose colour comes from a
// depth comparison against the scene, which reads as a flat wash on a vertical face (and this project runs
// with URP's depth texture off for mobile). Here the motion is pure UV scrolling driven by _Time, so the
// fall animates with no Animator, no per-frame script and two texture samples.
//
// Mesh convention: V = 0 at the bottom of the fall, 1 at the lip; U across the sheet.
Shader "AlienDefense/WaterfallScroll"
{
    Properties
    {
        [MainTexture] _BaseMap("Streak Texture", 2D) = "white" {}
        [MainColor] _TopColor("Top Color", Color) = (0.33, 0.85, 0.95, 0.75)
        _BottomColor("Bottom Color", Color) = (0.87, 1.0, 1.0, 1.0)
        _ScrollSpeed("Scroll Speed (UV/sec)", Float) = 0.9
        _SecondScrollSpeed("Second Layer Speed", Float) = 1.45
        _SecondTiling("Second Layer Tiling", Vector) = (1.3, 0.6, 0, 0)
        _SecondWeight("Second Layer Weight", Range(0,1)) = 0.55
        _EdgeSoftness("Side Edge Softness", Range(0.001,0.5)) = 0.18
        _TopFade("Top Fade", Range(0,0.5)) = 0.10
        _BottomFade("Bottom Fade", Range(0,0.5)) = 0.06
        _Alpha("Overall Alpha", Range(0,1)) = 0.85
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
            Name "WaterfallUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
                half4 _TopColor;
                half4 _BottomColor;
                float _ScrollSpeed;
                float _SecondScrollSpeed;
                float4 _SecondTiling;
                half _SecondWeight;
                half _EdgeSoftness;
                half _TopFade;
                half _BottomFade;
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

                // Both layers scroll towards -V, i.e. down the sheet, at different speeds and tilings.
                float2 uv1 = TRANSFORM_TEX(uv, _BaseMap) + float2(0.0, _Time.y * _ScrollSpeed);
                float2 uv2 = uv * _SecondTiling.xy + float2(0.37, _Time.y * _SecondScrollSpeed);

                half streak1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1).r;
                half streak2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2).r;
                half streaks = saturate(streak1 * (1.0 - _SecondWeight) + streak2 * _SecondWeight + streak1 * streak2 * 0.35);

                // Soft sides so the sheet has no hard silhouette, plus short fades where it meets the lip foam
                // at the top and the splash at the bottom.
                half sides = saturate(smoothstep(0.0, _EdgeSoftness, uv.x) * smoothstep(0.0, _EdgeSoftness, 1.0 - uv.x));
                half top = smoothstep(0.0, max(_TopFade, 0.0001), 1.0 - uv.y);
                half bottom = smoothstep(0.0, max(_BottomFade, 0.0001), uv.y);

                half3 colour = lerp(_BottomColor.rgb, _TopColor.rgb, saturate(uv.y));
                half alpha = streaks * sides * top * bottom * _Alpha * lerp(_BottomColor.a, _TopColor.a, saturate(uv.y));
                return half4(colour, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
