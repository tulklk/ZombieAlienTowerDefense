Shader "AlienDefense/UI/MinimapGrid"
{
    // Procedural, pixel-crisp grid for the minimap panel.
    //
    // Why this exists: the grid used to be a 32x32 tiled texture. The cell count was exactly right (10 x 10
    // across a 250px panel) but the CanvasScaler's scaleFactor is not 1 - on a 1080x2280 screen it is ~1.0897,
    // so each 25-canvas-pixel cell lands on 27.24 SCREEN pixels. A 32px texture resampled into 27.24px with
    // bilinear filtering puts every line at a different sub-pixel offset: some land on a pixel centre and come
    // out sharp, others straddle two pixels and come out blurred and dimmer. That reads as uneven cells even
    // though the geometry is perfectly even. No ppuMultiplier fixes it, because scaleFactor changes per device.
    //
    // Drawing the lines from the quad's own UV instead means the line width is measured in screen pixels via
    // fwidth, so every line is the same weight at any canvas scale, on any screen.
    //
    // The Image using this must have NO sprite: Image.GenerateSimpleSprite falls back to (0,0,1,1) outer UVs for
    // a null sprite, which is what guarantees uv spans the whole rect. An atlased sprite would give a sub-rect.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _CellsX ("Cells Across", Range(1, 40)) = 8
        _CellsY ("Cells Down", Range(1, 40)) = 8
        _LineWidth ("Line Half-Width (screen px)", Range(0.1, 4)) = 0.6
        _EdgeFade ("Edge Line Strength", Range(0, 1)) = 1

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
            Name "MinimapGrid"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Defined inline rather than included: the helper's include path moves between SRP versions.
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
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _CellsX;
                float _CellsY;
                float _LineWidth;
                float _EdgeFade;
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
                // Cell coordinates: integer values land exactly on the grid lines.
                float2 cell = IN.uv * float2(_CellsX, _CellsY);

                // Distance to the nearest line, in cell units.
                float2 toLine = abs(frac(cell) - 0.5);
                toLine = 0.5 - toLine;

                // fwidth is how much `cell` moves across one screen pixel, so dividing by it converts the
                // distance into screen pixels. THIS is what keeps every line the same weight no matter what
                // the canvas scale works out to.
                float2 pixels = toLine / max(fwidth(cell), 1e-5);

                // 1 on the line, fading to 0 one pixel past the requested half-width - free anti-aliasing.
                float2 strength = 1.0 - smoothstep(_LineWidth, _LineWidth + 1.0, pixels);
                float lines = max(strength.x, strength.y);

                // The lines at uv 0 and 1 sit half outside the rect, so they read thinner than the rest.
                // _EdgeFade lets them be dialled back instead of looking like a broken border.
                float2 edge = min(IN.uv, 1.0 - IN.uv) * float2(_CellsX, _CellsY);
                float onEdge = 1.0 - saturate(min(edge.x, edge.y) / max(_LineWidth * 0.5, 1e-5));
                lines *= lerp(1.0, _EdgeFade, onEdge);

                half4 color = IN.color;
                color.a *= lines;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.positionWS.xy, _ClipRect);
                #endif

                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
