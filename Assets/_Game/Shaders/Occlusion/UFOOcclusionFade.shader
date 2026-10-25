// The player UFO while scenery hides it from the camera (UFOOcclusionFade swaps the UFO body's materials for copies
// using this shader only while it is faded, and puts the originals back afterwards). The scenery is never changed:
// the UFO is drawn ON TOP of whatever covers it (ZTest Always), see-through, so the player still sees where it is.
//
// Keeps the UFO's own shape and shading - texture (_BaseMap), colour (_BaseColor), simple main-light + ambient
// lighting - recoloured green by _FadeTint, and result alpha = BaseTexture.a * BaseColor.a * _FadeAlpha. _FadeAlpha
// and _FadeTint are set per renderer with a MaterialPropertyBlock (1 / 0 = unchanged, so the green comes in and goes
// out with the fade); the tint colour is a global pushed from OcclusionSettings. URP,
// SRP-Batcher compatible, mobile-friendly, one pass, no shadows (the UFO casts none).
Shader "AlienDefense/UFOOcclusionFade"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _FadeAlpha ("Fade Alpha", Range(0, 1)) = 1
        _FadeTint ("Fade Tint Amount", Range(0, 1)) = 0
        _UseTextureAlpha ("Use Texture Alpha (0 for opaque originals)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "UFOOcclusionFade"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _FadeAlpha;
                half _FadeTint;
                half _UseTextureAlpha;
            CBUFFER_END

            half4 _UFOOcclusionTint; // global, from OcclusionSettings

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                // An opaque original ignores its texture alpha; so must the copy, or unused alpha would punch holes.
                albedo.a = lerp(_BaseColor.a, albedo.a, _UseTextureAlpha);
                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * saturate(dot(normal, mainLight.direction)) + SampleSH(normal);
                half3 color = albedo.rgb * lighting;

                // Green while hidden: the UFO's own light/dark shading (its luminance) recoloured with the tint, so it
                // reads as a green see-through UFO rather than a flat blob. A plain multiply would barely show on a
                // body that is already greenish.
                half shade = dot(color, half3(0.299h, 0.587h, 0.114h));
                half3 green = _UFOOcclusionTint.rgb * (0.55h + 0.9h * shade);
                color = lerp(color, green, _FadeTint);
                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a * _FadeAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
