using System;
using UnityEngine;

namespace AlienDefense.Base.DayNight
{
    /// <summary>Everything the BaseWorld day/night switch changes, for one end of the blend. The Day preset is filled
    /// from the scene as it was authored (BaseWorldDayNightBuilder), so day keeps today's look exactly.</summary>
    [Serializable]
    public sealed class BaseWorldLightingPreset
    {
        [Header("Sun / moon (BaseSun)")]
        public Color LightColor = Color.white;

        [Min(0f)]
        public float LightIntensity = 1f;

        [Tooltip("Euler angles of the directional light.")]
        public Vector3 LightRotation = new Vector3(50f, 325f, 0f);

        [Header("Ambient")]
        [Tooltip("Per-channel multiplier on the scene's own ambient (the skybox ambient it was lit with): white keeps " +
                 "it, dark blue turns it into moonlight. Tinting the authored ambient keeps its sky/ground gradient.")]
        public Color AmbientTint = Color.white;

        [Min(0f)]
        public float AmbientIntensity = 1f;

        [Range(0f, 1f)]
        public float ReflectionIntensity = 1f;

        [Tooltip("The base camera's clear colour (the only 'sky' it shows - it does not draw a skybox).")]
        public Color CameraBackground = new Color(0.624f, 0.847f, 0.961f, 1f);

        [Header("River (AlienDefense/StylizedRiver is unlit, so it is recoloured)")]
        public Color WaterShallow = new Color(0.48f, 0.93f, 0.90f, 1f);
        public Color WaterDeep = new Color(0.13f, 0.74f, 0.84f, 1f);
        public Color WaterFoam = new Color(0.94f, 1f, 1f, 1f);

        [Range(0f, 1f)]
        public float WaterHighlight = 0.35f;
    }
}
