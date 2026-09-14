// Tower construction reveal. A drop-in stand-in for Universal Render Pipeline/Lit that a tower wears only for
// the ~1 second it is being "built": everything above the construction line is clipped away, a thin cyan band
// glows right at the line, and everything below it renders exactly like URP/Lit (same LitInput/LitForwardPass
// code, same textures, normal map, occlusion and emission - the build materials are property copies of the
// originals). TowerConstructionVFX swaps these materials in, drives the build values through a
// MaterialPropertyBlock, and swaps the original Lit materials back the moment the build finishes.
//
// The line is measured in WORLD height, normalised by _BuildBaseY/_BuildHeight, which the controller computes
// once from the combined bounds of every renderer in the tower - so base, mount, head and barrels all share one
// continuous reveal instead of each part filling in on its own.
Shader "AlienDefense/TowerBuildReveal"
{
    Properties
    {
        [Header(Build Reveal)]
        _BuildProgress("Build Progress", Range(0, 1)) = 1
        _BuildBaseY("Build Base Y (world)", Float) = 0
        _BuildHeight("Build Height (world)", Float) = 1
        [HDR] _BuildEdgeColor("Edge Color", Color) = (0.306, 0.961, 1, 1)
        _BuildEdgeWidth("Edge Width (normalized)", Range(0.005, 0.2)) = 0.05
        _BuildEdgeIntensity("Edge Intensity", Range(0, 8)) = 2.5
        _BuildNoiseScale("Noise Scale", Float) = 4
        _BuildNoiseAmount("Noise Amount", Range(0, 0.15)) = 0.03
        _BuildPulse("Completion Pulse", Range(0, 1)) = 0

        [Header(Surface (copied from the source URP Lit material))]
        _WorkflowMode("WorkflowMode", Float) = 1.0
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
    }

    HLSLINCLUDE
        // Plain uniforms rather than UnityPerMaterial members: they are only ever fed per renderer through a
        // MaterialPropertyBlock (which opts those renderers out of the SRP Batcher anyway), and the tower is back
        // on its batched URP/Lit materials as soon as the build ends.
        float  _BuildProgress;
        float  _BuildBaseY;
        float  _BuildHeight;
        half4  _BuildEdgeColor;
        float  _BuildEdgeWidth;
        half   _BuildEdgeIntensity;
        float  _BuildNoiseScale;
        float  _BuildNoiseAmount;
        half   _BuildPulse;

        float BuildHash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        // Cheap 2D value noise (4 hashes, no textures).
        float BuildValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = BuildHash21(i);
            float b = BuildHash21(i + float2(1, 0));
            float c = BuildHash21(i + float2(0, 1));
            float d = BuildHash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Returns the height above the construction line in normalized units (<= 0 means "not built yet" and the
        // caller clips it). Progress 0 hides everything, progress 1 shows everything including the noisy fringe.
        float BuildRevealDistance(float3 positionWS)
        {
            float h = (positionWS.y - _BuildBaseY) / max(_BuildHeight, 0.001);
            float margin = _BuildNoiseAmount + _BuildEdgeWidth;
            float buildLine = lerp(-margin, 1.0 + margin, saturate(_BuildProgress));
            float noise = BuildValueNoise(positionWS.xz * _BuildNoiseScale + positionWS.y * _BuildNoiseScale * 0.37);
            return (buildLine + (noise - 0.5) * 2.0 * _BuildNoiseAmount) - h;
        }

        void BuildRevealClip(float3 positionWS)
        {
            clip(BuildRevealDistance(positionWS));
        }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment BuildRevealFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            // LitPassFragment with the reveal clip and the construction-line glow added. Kept otherwise identical
            // so the built part of the tower is indistinguishable from its URP/Lit self.
            void BuildRevealFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float distanceAboveLine = BuildRevealDistance(input.positionWS);
                clip(distanceAboveLine);

            #if defined(_PARALLAXMAP)
            #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS = input.viewDirTS;
            #else
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
            #endif
                ApplyPerPixelDisplacement(viewDirTS, input.uv);
            #endif

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

                // Glow only in a thin band right under the line; zero everywhere else, and zero at progress 1
                // because the line has by then moved a full band past the top of the tower.
                half edge = 1.0 - smoothstep(0.0, max(_BuildEdgeWidth, 0.0001), distanceAboveLine);
                half3 glow = _BuildEdgeColor.rgb * (edge * _BuildEdgeIntensity + _BuildPulse * 0.6);
                // Tint the albedo too, so the band still reads as bright cyan on a phone with no Bloom.
                surfaceData.albedo = lerp(surfaceData.albedo, _BuildEdgeColor.rgb, saturate(edge * 0.85));
                surfaceData.emission += glow;

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
                InitializeBakedGIData(input, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                outColor = color;

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex BuildShadowVertex
            #pragma fragment BuildShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // The unbuilt part must not cast a shadow either.
            struct BuildShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            BuildShadowVaryings BuildShadowVertex(Attributes input)
            {
                BuildShadowVaryings output = (BuildShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 BuildShadowFragment(BuildShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                BuildRevealClip(input.positionWS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex BuildDepthVertex
            #pragma fragment BuildDepthFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            struct BuildDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            BuildDepthVaryings BuildDepthVertex(Attributes input)
            {
                BuildDepthVaryings output = (BuildDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.position.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half BuildDepthFragment(BuildDepthVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                BuildRevealClip(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
