// UFO Tractor Beam — shared math for Shader Graph Custom Function + AlienDefense/UFOTractorBeam.shader
//
// Shader Graph Custom Function setup:
//   Type: File
//   Source: this file
//   Name: UFOTractorBeam
//   Precision: float
//
// UV.y = 0 at ground, UV.y = 1 at UFO. Flow uses SUBTRACT (UV.y * Density - Time * Speed)
// so streaks travel upward. Do not flip the sign unless mesh UV is verified inverted.

#ifndef UFO_TRACTOR_BEAM_FUNCTION_INCLUDED
#define UFO_TRACTOR_BEAM_FUNCTION_INCLUDED

void UFOTractorBeam_float(
    float2 UV,
    float TimeValue,
    float4 BeamColor,
    float4 CoreColor,
    float BaseAlpha,
    float EmissionStrength,
    float4 FlowColor,
    float FlowSpeed,
    float FlowDensity,
    float FlowWidth,
    float FlowStrength,
    float FlowDistortion,
    float PulseSpeed,
    float PulseAmount,
    float EdgeSoftness,
    float TopFade,
    float BottomFade,
    float RadialSoftness,
    float NoiseScale,
    float NoiseSpeed,
    float NoiseStrength,
    float3 NormalWS,
    float3 ViewDirectionWS,
    out float3 FinalColor,
    out float FinalAlpha)
{
    // ---- 01 Distortion ----
    float Distortion = sin(UV.x * 10.0 + TimeValue * 0.5) * FlowDistortion;

    // ---- 02 Noise offset (cheap sine; no texture / Perlin) ----
    float Noise01 = sin(UV.y * NoiseScale * 6.28318530718 + TimeValue * NoiseSpeed * 6.28318530718) * 0.5 + 0.5;
    float NoiseOffset = (Noise01 - 0.5) * NoiseStrength;

    // ---- 03 Flow position (MUST subtract time for upward travel) ----
    float FlowPos = frac(UV.y * FlowDensity - TimeValue * FlowSpeed + Distortion + NoiseOffset);

    // ---- 04 Flow mask ----
    float FlowMaskRaw = 1.0 - smoothstep(FlowWidth, FlowWidth * 2.0, FlowPos);

    // ---- 05 Stripe variation along UV.x ----
    float Stripe01 = sin(UV.x * 25.0) * 0.5 + 0.5;
    float StripeMask = lerp(0.55, 1.0, Stripe01);
    float FlowMask = FlowMaskRaw * StripeMask;

    // ---- 06 Fresnel / rim ----
    float3 normalWS = normalize(NormalWS);
    float3 viewDirWS = normalize(ViewDirectionWS);
    float fresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), max(0.01, EdgeSoftness) * 4.0);
    float3 RimEmission = fresnel * RadialSoftness * CoreColor.rgb * EmissionStrength;

    // ---- 07 Height fade ----
    float BottomFadeMask = smoothstep(0.0, BottomFade, UV.y);
    float TopFadeMask = 1.0 - smoothstep(1.0 - TopFade, 1.0, UV.y);
    float HeightFade = BottomFadeMask * TopFadeMask;

    // ---- 08 Pulse (gentle) ----
    float Pulse = 1.0 + (sin(TimeValue * PulseSpeed) * 0.5 + 0.5) * PulseAmount;

    // ---- 09 Emission sum ----
    float3 BeamEmission = BeamColor.rgb * EmissionStrength;
    float3 FlowEmission = FlowMask * FlowColor.rgb * FlowStrength;
    float3 EmissionSum = BeamEmission + RimEmission + FlowEmission;

    // ---- 10 Beam color alpha (runtime fade via MaterialPropertyBlock) ----
    float BeamColorAlpha = BeamColor.a;

    // ---- 11 / 12 Final alpha ----
    float AlphaBase = BaseAlpha + FlowMask * 0.1;
    FinalAlpha = saturate(AlphaBase * HeightFade * BeamColorAlpha);

    // ---- 13 Additive: premultiply so Base Color * Alpha still fades correctly ----
    float3 FinalColorBeforeAlpha = EmissionSum * HeightFade * Pulse * BeamColorAlpha;
    FinalColor = FinalColorBeforeAlpha * FinalAlpha;
}

#endif
