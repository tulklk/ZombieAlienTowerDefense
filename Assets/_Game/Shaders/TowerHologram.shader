Shader "AlienDefense/TowerHologram"
{
    // Cyan/ghost hologram look for a Tower-build preview: flat hologram color, brighter Fresnel rim, thin
    // scanlines moving up the model, and a very light overall pulse. See the node-map comment at the bottom of
    // this file for how each block below maps to the Shader Graph node graph it stands in for (this project's
    // Unity build cannot construct a .shadergraph asset from script — see the chat report for why).
    Properties
    {
        [HDR] _HologramColor ("Hologram Color", Color) = (0, 0.961, 1, 1)
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.30

        [HDR] _FresnelColor ("Fresnel Color", Color) = (0.55, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _FresnelStrength ("Fresnel Strength", Range(0, 5)) = 1.5

        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.5

        [HDR] _ScanlineColor ("Scanline Color", Color) = (0.75, 1, 1, 1)
        _ScanlineDensity ("Scanline Density", Range(1, 100)) = 25
        _ScanlineSpeed ("Scanline Speed", Range(-10, 10)) = 1.5
        _ScanlineWidth ("Scanline Width", Range(0.01, 1)) = 0.15
        _ScanlineStrength ("Scanline Strength", Range(0, 3)) = 0.6

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.12
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

        // Surface Type = Transparent, Blend = Alpha, Depth Write = Off, Render Face = Both (Cull Off).
        // Alpha Clipping stays OFF per spec — see the "TRANSPARENT SORTING" note near the bottom of this file
        // for the one tradeoff that comes with that choice on a multi-mesh Tower model.
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  objectY     : TEXCOORD2; // object-space Y, feeds the scanline (see node map)
            };

            // SRP Batcher friendly: every exposed property lives in one CBUFFER.
            CBUFFER_START(UnityPerMaterial)
                half4 _HologramColor;
                float _BaseAlpha;
                half4 _FresnelColor;
                float _FresnelPower;
                float _FresnelStrength;
                float _EmissionStrength;
                half4 _ScanlineColor;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineWidth;
                float _ScanlineStrength;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.objectY = IN.positionOS.y;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ---- Fresnel: dim in the middle of a face, bright right at the silhouette edge. ----
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 normalWS = normalize(IN.normalWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirWS)), _FresnelPower);

                // ---- Scanline: thin bands riding up the model's own local Y, independent of world position. ----
                float scanPhase = IN.objectY * _ScanlineDensity + _Time.y * _ScanlineSpeed;
                float scanSine = sin(scanPhase);
                float scanBand = smoothstep(1.0 - _ScanlineWidth, 1.0, abs(scanSine));

                // ---- Pulse: gentle breathing, never a hard on/off flicker. ----
                float pulseWave = sin(_Time.y * _PulseSpeed);       // -1..1
                float pulseEmission01 = pulseWave * 0.5 + 0.5;      // 0..1, brightens glow a little on each beat
                float pulseAlphaOffset = pulseWave * min(_PulseAmount, 0.10) * 0.5; // small +/- alpha breathing

                half3 fresnelEmission = fresnel * _FresnelStrength * _FresnelColor.rgb;
                half3 scanlineEmission = scanBand * _ScanlineStrength * _ScanlineColor.rgb;
                half3 baseEmission = _HologramColor.rgb * _EmissionStrength;

                half3 emission = baseEmission + fresnelEmission + scanlineEmission;
                emission *= 1.0 + pulseEmission01 * _PulseAmount;

                float alpha = _BaseAlpha + fresnel * 0.35 + scanBand * 0.25 + pulseAlphaOffset;
                alpha = saturate(alpha);

                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

/*
==================== NODE MAP (see chat report for why this is a hand-written HLSL shader
instead of a .shadergraph asset — the two produce an identical visual result) ====================

BASE COLOR
    Property: _HologramColor  ->  Base Color

FRESNEL BLOCK
    Normal Vector (World)  ─┐
    View Direction (World) ─┤─> Fresnel Effect (Power = _FresnelPower)
                             │        │
                             │        ▼
                             │     Multiply (x _FresnelStrength)
                             │        │
                             │        ▼
                             │     Multiply (x _FresnelColor)
                             │        │
                             │        ▼
                             │   FresnelEmission ──────────────┐
                                                                │
SCANLINE BLOCK                                                 │
    Position (Object)                                          │
        │                                                      │
        ▼ (Split, take Y)                                      │
    Multiply (x _ScanlineDensity)                               │
        │                                                      │
        ▼                                                      │
    Add ( + Time.y x _ScanlineSpeed )                           │
        │                                                      │
        ▼                                                      │
      Sine                                                     │
        │                                                      │
        ▼                                                      │
      Abs                                                      │
        │                                                      │
        ▼                                                      │
    Smoothstep(edge0 = 1 - _ScanlineWidth, edge1 = 1)            │
        │                                                      │
        ▼                                                      │
      Scanline                                                 │
        │                                                      │
        ▼                                                      │
    Multiply (x _ScanlineStrength)                              │
        │                                                      │
        ▼                                                      │
    Multiply (x _ScanlineColor)                                 │
        │                                                      │
        ▼                                                      │
    ScanlineEmission ─────────────────────────────────────────┤
                                                                │
PULSE BLOCK                                                     │
    Time.y                                                     │
        │                                                      │
        ▼ (x _PulseSpeed)                                       │
    Multiply                                                    │
        │                                                      │
        ▼                                                      │
      Sine  (-1..1) ───────────────┐                            │
        │                          │                            │
        ▼ Remap(-1..1 -> 0..1)      ▼ (used directly, not remapped)
    PulseEmission01              PulseWave
        │                          │
        ▼ (x _PulseAmount)          ▼ (x min(_PulseAmount,0.10) x 0.5)
    EmissionPulseTerm            AlphaPulseOffset

FINAL EMISSION
    (_HologramColor x _EmissionStrength)
        + FresnelEmission
        + ScanlineEmission
        = SumEmission
    SumEmission x (1 + EmissionPulseTerm)  ->  Emission (Master Stack)

FINAL ALPHA
    _BaseAlpha
        + Fresnel x 0.35
        + Scanline x 0.25
        + AlphaPulseOffset
    Saturate(0..1)  ->  Alpha (Master Stack)

Graph Settings a real Shader Graph version of this would use:
    Material type: Unlit
    Surface Type: Transparent
    Blend Mode: Alpha
    Render Face: Both
    Depth Write: Off
    Alpha Clipping: Off
====================================================================================================
*/
