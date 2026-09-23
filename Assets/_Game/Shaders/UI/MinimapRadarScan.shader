Shader "AlienDefense/UI/MinimapRadarScan"
{
    // Radar sweep for the minimap: a soft horizontal band that travels top -> bottom on a loop, with a brighter
    // core line, over a faint darkening tint that makes the sweep read.
    //
    // Built on the UI/Default pipeline (stencil block, _ClipRect, unity_GUIZTestMode) so it behaves like any other
    // UI material: works inside Mask/RectMask2D, respects the Graphic's own colour, and sorts by hierarchy order.
    //
    // The sweep position comes from OBJECT SPACE, not from UV. The minimap panel sprite is drawn as a 9-sliced
    // Image, and 9-slicing emits several quads whose UVs each run 0..1 over their own slice - so a UV-driven
    // sweep would restart inside every slice and tear at the seams. Object-space Y is continuous across the whole
    // rect, so the band stays a single unbroken line. _RectHeight is fed by MinimapRadarScanOverlay.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Scan)]
        _ScanColor ("Scan Color", Color) = (0.35, 0.85, 1.0, 1)
        _ScanHotColor ("Scan Core Color", Color) = (0.85, 0.98, 1.0, 1)
        _BaseTint ("Base Tint (un-scanned area)", Color) = (0.04, 0.22, 0.42, 1)

        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.10
        _ScanIntensity ("Scan Intensity", Range(0, 1)) = 0.38
        _ScanLineIntensity ("Scan Core Intensity", Range(0, 1)) = 0.55

        _ScanWidth ("Scan Width (0-1 of height)", Range(0.01, 0.6)) = 0.22
        _ScanSoftness ("Scan Softness", Range(0, 1)) = 0.85
        _ScanLineWidth ("Scan Core Width", Range(0.001, 0.2)) = 0.025
        _ScanSpeed ("Scan Speed (sweeps per second)", Range(0, 3)) = 0.35
        // Added to the animated phase. With _ScanSpeed = 0 this parks the band at a fixed height, which is how
        // the sweep was verified, and it also lets gameplay drive the sweep by hand if that is ever wanted.
        _ScanOffset ("Scan Offset (0-1)", Range(0, 1)) = 0

        _StripeStrength ("CRT Stripe Strength", Range(0, 0.5)) = 0.0
        _StripeCount ("CRT Stripe Count", Range(4, 128)) = 42

        _RectHeight ("Rect Height (set by script)", Float) = 200

        // Standard UI plumbing - do not remove, Mask/RectMask2D drive these.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "MinimapRadarScan"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Defined here rather than included: the UI clipping helper lives in a different file depending on
            // the Unity/SRP version, and it is four lines. Same maths as UnityUI.cginc.
            float UnityGet2DClipping(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                // Normalised rect coordinates written by MinimapRadarScanOverlay (y: 0 = bottom, 1 = top).
                float2 rectUV     : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 positionWS : TEXCOORD1;
                float  localY     : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ScanColor;
                half4 _ScanHotColor;
                half4 _BaseTint;
                half _BaseAlpha;
                half _ScanIntensity;
                half _ScanLineIntensity;
                half _ScanWidth;
                half _ScanSoftness;
                half _ScanLineWidth;
                half _ScanSpeed;
                half _ScanOffset;
                half _StripeStrength;
                half _StripeCount;
                float _RectHeight;
                float4 _ClipRect;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = mul(UNITY_MATRIX_M, IN.positionOS);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;

                // 0 at the bottom edge of the rect, 1 at the top, supplied per vertex in UV1.
                //
                // This cannot be derived from positionOS: Unity batches UI graphics into shared meshes whose
                // vertices are already in canvas space, so positionOS is the position on the whole canvas, not
                // inside this rect. Nor can it come from UV0, because a 9-sliced sprite restarts UV0 in every
                // slice. UV1 written at mesh-build time is the only coordinate that survives both.
                OUT.localY = IN.rectUV.y;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // The sprite supplies the shape (rounded corners of the minimap panel); the scan never spills
                // outside it because everything is multiplied by this alpha.
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // Sweep head runs 1 -> 0 so the band travels downwards.
                float head = 1.0 - frac(_Time.y * _ScanSpeed + _ScanOffset);

                // Shortest wrapped distance, so the band crosses the top/bottom seam without popping.
                float delta = IN.localY - head;
                delta = delta - round(delta);
                float dist = abs(delta);

                // Soft band. Softness slides the inner edge of the smoothstep: 0 = hard edge, 1 = fully feathered.
                float inner = _ScanWidth * (1.0 - _ScanSoftness);
                float band = 1.0 - smoothstep(inner, _ScanWidth, dist);

                // Thin bright core at the head of the sweep.
                float core = 1.0 - smoothstep(0.0, _ScanLineWidth, dist);

                // Optional CRT striping. Off by default; costs one sin when enabled.
                float stripe = 1.0;
                if (_StripeStrength > 0.0)
                {
                    stripe = 1.0 - _StripeStrength * (0.5 + 0.5 * sin(IN.localY * _StripeCount * 6.2831853));
                }

                half3 rgb = lerp(_BaseTint.rgb, _ScanColor.rgb, saturate(band));
                rgb = lerp(rgb, _ScanHotColor.rgb, saturate(core));

                half alpha = _BaseAlpha + band * _ScanIntensity + core * _ScanLineIntensity;
                alpha = saturate(alpha) * stripe * sprite.a;

                half4 color = half4(rgb, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.positionWS.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
