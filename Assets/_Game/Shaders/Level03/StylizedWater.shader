// Level 3's cartoon water: bright turquoise, a lighter shallow band and a soft animated foam line along every shore,
// and slow drifting white highlights. Built for mobile - opaque, unlit, one texture, no depth/opaque texture, no
// reflection or refraction. "How close to the shore" is not read from the depth buffer: Level03Builder bakes it into
// the water mesh's vertex colour (red: 0 = open water, 1 = waterline) from the same mask that shapes the terrain, so
// the shallow band and the foam always follow the real coastline.
Shader "AlienDefense/Level03/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow Colour", Color) = (0.42, 0.95, 0.90, 1)
        _DeepColor ("Deep Colour", Color) = (0.05, 0.72, 0.80, 1)
        _FoamColor ("Foam Colour", Color) = (1, 1, 1, 1)
        _HighlightColor ("Highlight Colour", Color) = (0.85, 1, 1, 1)
        _NoiseTex ("Highlight Pattern (R)", 2D) = "white" {}
        _NoiseScale ("Pattern Scale (1/m)", Float) = 0.11
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.45
        _FoamWidth ("Foam Width", Range(0.02, 0.5)) = 0.16
        _Speed ("Drift Speed", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _HighlightColor;
                float4 _NoiseTex_ST;
                float _NoiseScale;
                half _HighlightStrength;
                half _FoamWidth;
                float _Speed;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
                half shore : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.worldXZ = positionWS.xz;
                output.shore = input.color.r;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = input.worldXZ * _NoiseScale;
                half a = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv + float2(0.021, 0.013) * t).r;
                half b = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 1.37 + float2(-0.017, 0.019) * t + 0.37).r;

                // Body colour: turquoise, lighter toward the shore.
                half shore = saturate(input.shore);
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, shore * shore);

                // Drifting highlights where the two pattern layers cross.
                half highlight = smoothstep(0.55, 0.95, a * b * 2.0);
                color = lerp(color, _HighlightColor.rgb, highlight * _HighlightStrength);

                // Foam: a band at the waterline whose inner edge breathes and breaks up with the pattern.
                half wobble = (a - 0.5) * 0.22 + sin(t * 1.3 + (input.worldXZ.x + input.worldXZ.y) * 0.7) * 0.035;
                half foam = smoothstep(1.0 - _FoamWidth, 1.0 - _FoamWidth * 0.35, shore + wobble);
                color = lerp(color, _FoamColor.rgb, foam * 0.92);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
