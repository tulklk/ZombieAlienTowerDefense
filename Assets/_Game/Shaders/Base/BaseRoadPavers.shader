// Stylized paved road for the Base map (URP, mobile).
// The road meshes (Low Poly Road Pack) only carry palette UVs: those still decide road vs kerb colour. The paving
// pattern is projected in WORLD XZ, so every piece - stretched straights, T-junctions, crosses, curves - shows
// stones of exactly the same size and orientation, continuous across piece joins. Vertical faces (the pieces' low
// side lips) get the plain colour. Lighting uses the flat facet normal (see frag). One pass, main light + ambient, SRP-Batcher compatible.
Shader "AlienDefense/BaseRoadPavers"
{
    Properties
    {
        _Palette ("Road / Kerb Palette", 2D) = "white" {}
        _PaverTex ("Paver Pattern (grey, 1 = stone, darker = grout)", 2D) = "white" {}
        _PaverWorldSize ("Pattern Size (m per texture repeat)", Float) = 3.2
        _PaverStrength ("Pattern Strength", Range(0, 1)) = 0.55
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RoadPavers"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Palette);
            SAMPLER(sampler_Palette);
            TEXTURE2D(_PaverTex);
            SAMPLER(sampler_PaverTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Palette_ST;
                float4 _PaverTex_ST;
                float _PaverWorldSize;
                half _PaverStrength;
                half _AmbientBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // The pack's road tops carry smoothed normals (tilted ~45 deg where they share vertices with the side
                // lips), so the facet normal is rebuilt from screen derivatives: every face lights flat and the
                // paving mask sees the true orientation. Flipped toward the camera to be platform-independent.
                float3 facet = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                facet *= sign(dot(facet, GetWorldSpaceViewDir(input.positionWS)) + 1e-5);
                half3 normal = (half3)facet;
                half3 baseColor = SAMPLE_TEXTURE2D(_Palette, sampler_Palette, input.uv).rgb;

                // Paving on the (near-)horizontal surfaces only.
                half up = saturate(normal.y * 4.0 - 3.0);
                float2 paverUV = input.positionWS.xz / max(0.01, _PaverWorldSize);
                half paver = SAMPLE_TEXTURE2D(_PaverTex, sampler_PaverTex, paverUV).r;
                baseColor *= lerp(1.0, paver, _PaverStrength * up);

                // Kerb steps and lips are lit with a normal bent toward up, so they read as a soft edge rather than
                // a dark line; horizontal faces are unaffected.
                half3 lightNormal = normalize(lerp(half3(0.0, 1.0, 0.0), normal, 0.4));
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(lightNormal, mainLight.direction));
                half3 lighting = mainLight.color * ndl + SampleSH(lightNormal) * _AmbientBoost;
                half3 color = baseColor * lighting;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
