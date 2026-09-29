// Cartoon river for the Base map (URP, mobile).
// One opaque unlit pass, no depth/opaque texture, no reflections. Everything comes from the mesh:
//   uv.x  = distance along the river (metres / 8) - the flow scrolls along it, so it follows every bend;
//   uv.y  = 0 at one bank, 1 at the other - drives shallow/deep colour and the shoreline foam;
//   COLOR.r = cave darkness (0 in the open, 1 deep inside the cave).
Shader "AlienDefense/StylizedRiver"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.48, 0.93, 0.90, 1)
        _DeepColor ("Deep Color", Color) = (0.13, 0.74, 0.84, 1)
        _FoamColor ("Foam Color", Color) = (0.94, 1.0, 1.0, 1)
        _CaveColor ("Cave Color", Color) = (0.03, 0.13, 0.17, 1)
        _NoiseTex ("Noise (tileable)", 2D) = "gray" {}
        _FlowSpeed ("Flow Speed", Float) = 0.05
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.35
        _FoamWidth ("Foam Width (0..0.5 of river)", Range(0, 0.5)) = 0.09
        _DepthFalloff ("Shallow Band (0..1 of half-width)", Range(0.05, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RiverUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _CaveColor;
                float4 _NoiseTex_ST;
                float _FlowSpeed;
                half _HighlightStrength;
                half _FoamWidth;
                half _DepthFalloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half cave : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.uv = input.uv;
                output.cave = input.color.r;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                float along = input.uv.x;
                float across = input.uv.y;
                half edge = min(across, 1.0 - across) * 2.0;           // 0 at the banks, 1 mid-river

                // Two noise layers flowing downstream at different rates: moving highlights and soft brightness.
                float2 flowA = float2(along * 0.9 - t * 4.0, across * 1.3);
                float2 flowB = float2(along * 1.7 - t * 2.6 + 0.37, across * 2.1 + 0.19);
                half nA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowA).r;
                half nB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowB).r;

                half depth = smoothstep(0.0, _DepthFalloff, edge);
                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);
                color *= 0.95 + (nA - 0.5) * 0.14;

                half highlight = smoothstep(0.66, 0.78, nA * 0.55 + nB * 0.55);
                color += highlight * _HighlightStrength * (1.0 - input.cave);

                // Shoreline foam: a thin, slightly wobbling band along both banks.
                half foamEdge = edge + (nB - 0.5) * 0.06;
                half foam = 1.0 - smoothstep(_FoamWidth * 0.45, _FoamWidth, foamEdge);
                color = lerp(color, _FoamColor.rgb, foam * 0.85 * (1.0 - input.cave * 0.7));

                color = lerp(color, _CaveColor.rgb, input.cave);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
