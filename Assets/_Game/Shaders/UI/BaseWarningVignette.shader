Shader "AlienDefense/UI/BaseWarningVignette"
{
    // Red/orange glow around the screen edges for the "Base under attack!" warning. Drawn on one full-screen UI
    // Image (raycasts off). The mask is pure maths on the Image's UVs - no texture sample, no screen grab, no blur -
    // so it is one cheap pass. The centre stays clear; edges and especially corners glow. The pulse is driven from
    // C# through the Image colour's alpha (no material instance per frame).
    //
    // Follows the UI/Default contract (stencil block, _ClipRect, unity_GUIZTestMode) so it sorts like any Graphic.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WarningColor ("Edge Colour", Color) = (1, 0.08, 0.04, 1)
        _InnerColor ("Inner Glow Colour", Color) = (1, 0.45, 0.1, 1)
        _Intensity ("Intensity", Range(0, 3)) = 1
        _EdgePower ("Edge Power (higher = thinner)", Range(0.5, 6)) = 2.2
        _Inner ("Clear Centre Radius", Range(0, 1)) = 0.42
        _Opacity ("Opacity", Range(0, 1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            Name "BaseWarningVignette"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                float2 uv         : TEXCOORD0;
                float4 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _WarningColor;
                half4 _InnerColor;
                half _Intensity;
                half _EdgePower;
                half _Inner;
                half _Opacity;
                float4 _ClipRect;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = mul(UNITY_MATRIX_M, IN.positionOS);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // -1..1 from the centre; a rounded-rectangle distance so all four edges glow, corners the most.
                float2 p = abs(IN.uv * 2.0 - 1.0);
                float edge = max(p.x, p.y) * 0.6 + length(p) * 0.4;
                half mask = saturate((edge - _Inner) / max(0.001, 1.0 - _Inner));
                mask = pow(mask, _EdgePower);

                half3 rgb = lerp(_InnerColor.rgb, _WarningColor.rgb, saturate(mask * 1.6));
                half alpha = saturate(mask * _Intensity) * _Opacity * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(IN.positionWS.xy, _ClipRect);
                #endif

                return half4(rgb * IN.color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
