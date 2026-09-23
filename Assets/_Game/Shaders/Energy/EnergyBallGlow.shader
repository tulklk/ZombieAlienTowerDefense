Shader "AlienDefense/EnergyBallGlow"
{
    // Stylised glossy energy orb: bright cyan core where the (camera-relative) key light hits, blue body,
    // darker blue outline at the silhouette, and a small glossy highlight - the look of a hand-painted orb.
    //
    // Why the light is camera-relative: the key direction is built in VIEW space, so the lit side and the
    // highlight always sit in the same corner of the sphere on screen no matter how the camera or the orb
    // turns. That is what makes a 3D sphere read like the 2D reference painting.
    //
    // Deliberately Unlit: pickups must look identical wherever they land on the map. SRP Batcher friendly.
    Properties
    {
        [HDR] _CoreColor ("Core Color (lit centre)", Color) = (0.42, 0.93, 1.0, 1)
        [HDR] _MidColor ("Mid Color (body)", Color) = (0.05, 0.60, 0.95, 1)
        [HDR] _RimColor ("Rim Color (dark outline)", Color) = (0.02, 0.22, 0.55, 1)
        [HDR] _HighlightColor ("Highlight Color", Color) = (0.55, 0.92, 1.0, 1)

        // The pulse adds this colour, NOT the core colour. A pale cyan has a high red channel, so multiplying it
        // up drives R towards 1 along with G and B and the flash reads as white. A saturated blue keeps red near
        // zero, so even when the blue channel clips the orb still reads blue.
        [HDR] _PulseColor ("Pulse Color (what the flash adds)", Color) = (0.02, 0.45, 1.0, 1)

        _CoreIntensity ("Core Intensity", Range(0, 2)) = 1.0
        _RimStrength ("Rim (outline) Strength", Range(0, 1)) = 0.85
        _RimStart ("Rim Start", Range(0, 1)) = 0.55
        _RimPower ("Rim Falloff", Range(0.5, 8)) = 1.6

        _HighlightStrength ("Highlight Strength", Range(0, 3)) = 0.9
        _HighlightPower ("Highlight Tightness", Range(1, 128)) = 26

        _EmissionStrength ("Emission Strength", Range(0, 4)) = 1.15

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.6
        _PulseAmount ("Pulse Gain (hotspot brightness)", Range(0, 3)) = 1.55
        _PulseFocus ("Pulse Focus (smaller = wider hotspot)", Range(0, 1)) = 0.18
        _PulseDim ("Pulse Dim (how far it dips at the trough)", Range(0, 0.7)) = 0.32

        _NoiseScale ("Noise Scale", Range(0, 16)) = 4.0
        _NoiseSpeed ("Noise Speed", Range(0, 8)) = 0.8
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite On
        ZTest LEqual
        Blend One Zero

        Pass
        {
            Name "EnergyBallCore"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Key light in view space: up, slightly left, tilted towards the camera. In Unity's view space the
            // camera looks down -Z, so +Z points back at the viewer.
            static const float3 kKeyDirVS = float3(-0.42, 0.55, 0.72);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _MidColor;
                half4 _RimColor;
                half4 _HighlightColor;
                half4 _PulseColor;
                float _CoreIntensity;
                float _RimStrength;
                float _RimStart;
                float _RimPower;
                float _HighlightStrength;
                float _HighlightPower;
                float _EmissionStrength;
                float _PulseSpeed;
                float _PulseAmount;
                float _PulseFocus;
                float _PulseDim;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS));
                float3 L = normalize(kKeyDirVS);

                // Wrapped lambert: 1 on the lit side, 0 on the far side. Gives the ball its volume.
                float lit = saturate(dot(normalVS, L) * 0.5 + 0.5);

                // Silhouette mask: 0 in the middle of the disc, 1 right at the edge.
                float facing = saturate(normalVS.z);
                float edge = pow(1.0 - facing, _RimPower);
                edge = smoothstep(_RimStart, 1.0, edge);

                // Body: dark blue on the unlit side -> mid blue -> bright cyan core on the lit side.
                // The core band starts late (0.62) on purpose: a wide bright band flattens the sphere into a
                // pale disc, which is exactly how the old version failed.
                half3 color = lerp(_RimColor.rgb, _MidColor.rgb, smoothstep(0.15, 0.78, lit));
                color = lerp(color, _CoreColor.rgb, smoothstep(0.62, 1.0, lit) * _CoreIntensity);

                // Darker outline all the way round the silhouette - this is what reads as "depth".
                color = lerp(color, _RimColor.rgb, edge * _RimStrength);

                // Glossy highlight, kept inside the silhouette so it never bleeds onto the outline.
                float spec = pow(saturate(dot(normalVS, L)), _HighlightPower);
                color += _HighlightColor.rgb * spec * _HighlightStrength * (1.0 - edge);

                // Slow drifting energy in OBJECT space, so the pattern travels with the orb instead of
                // sliding across it while the pickup moves or is pulled by the tractor beam.
                float n1 = sin(IN.positionOS.x * _NoiseScale + _Time.y * _NoiseSpeed);
                float n2 = sin(IN.positionOS.y * (_NoiseScale * 1.3) - _Time.y * _NoiseSpeed);
                float n3 = sin(IN.positionOS.z * (_NoiseScale * 0.8) + _Time.y * (_NoiseSpeed * 1.2));
                float noise = ((n1 + n2 + n3) * (1.0 / 3.0)) * 0.5 + 0.5;
                color += _CoreColor.rgb * noise * _NoiseStrength * (1.0 - edge);

                // Two frequencies that are not multiples of each other, so the cycle never repeats exactly and
                // the light reads as alive instead of as a metronome.
                float wave = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float flicker = sin(_Time.y * _PulseSpeed * 2.37) * 0.5 + 0.5;
                float pulse01 = saturate(wave * 0.85 + flicker * 0.15);

                // The pulse works in BOTH directions, and the downward half is what actually sells it.
                // Tonemapping compresses everything above 1.0 hard, so pushing the orb brighter and brighter
                // barely moves the pixel that reaches the screen. Letting it dip below its settled brightness
                // costs nothing in compression - dark values pass through untouched - so the dip is what the
                // eye reads as "breathing".
                float bodyLevel = lerp(1.0 - _PulseDim, 1.0, pulse01);
                half3 emission = color * (_EmissionStrength * bodyLevel);

                // Rising half: an additive hotspot that crosses the Bloom threshold, so the orb also flares.
                float hotspot = smoothstep(_PulseFocus, 1.0, lit) * (1.0 - edge);
                emission += _PulseColor.rgb * hotspot * _PulseAmount * pulse01;

                return half4(emission, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
