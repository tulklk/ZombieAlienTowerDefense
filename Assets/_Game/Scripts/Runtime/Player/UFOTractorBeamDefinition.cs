using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Config-only tuning for the UFO's continuous multi-enemy tractor beam. No runtime state.</summary>
    [CreateAssetMenu(fileName = "UFOTractorBeamDefinition", menuName = "AlienDefense/Player/UFO Tractor Beam Definition")]
    public sealed class UFOTractorBeamDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _stableId = "ufo_tractorbeam_default";

        [SerializeField]
        private string _displayName = "UFO Tractor Beam";

        [Header("Admission")]
        [SerializeField, Min(0.1f)]
        private float _attractionRadius = 3.5f;

        [SerializeField, Min(0.01f)]
        private float _scanInterval = 0.10f;

        [SerializeField, Min(0)]
        [Tooltip("0 = unlimited concurrent captures. Enemy, Energy Pickup and Environment Prop each have their " +
            "own separate slot pool — 8 Enemies capturing never blocks Energy or Prop absorption.")]
        private int _maxConcurrentCaptures = 8;

        [SerializeField, Min(0)]
        [Tooltip("0 = unlimited concurrent Energy Pickup absorptions. Independent from Enemy/Prop limits.")]
        private int _maxConcurrentEnergyAbsorptions = 16;

        [SerializeField, Min(0)]
        [Tooltip("0 = unlimited concurrent Environment Prop absorptions. Independent from Enemy/Energy limits.")]
        private int _maxConcurrentPropAbsorptions = 8;

        [Header("Movement")]
        [SerializeField, Min(0.01f)]
        private float _pullSpeed = 8f;

        [SerializeField, Min(0.01f)]
        private float _liftSpeed = 6f;

        [SerializeField, Min(0.01f)]
        private float _beamCenterThreshold = 0.20f;

        [SerializeField, Min(0.01f)]
        private float _captureSocketThreshold = 0.15f;

        [Header("Visual")]
        [SerializeField]
        private bool _shrinkDuringLift = true;

        [SerializeField, Range(0.01f, 1f)]
        private float _minimumVisualScale = 0.25f;

        [SerializeField, Min(0f)]
        private float _liftSpinSpeedDegreesPerSecond = 240f;

        [SerializeField, Min(0f)]
        [Tooltip("Optional. Purely cosmetic radius for the beam visual, if it should differ from AttractionRadius. 0 = use AttractionRadius.")]
        private float _beamVisualRadius;

        public string StableId => _stableId;
        public string DisplayName => _displayName;
        public float AttractionRadius => _attractionRadius;
        public float ScanInterval => _scanInterval;
        public int MaxConcurrentCaptures => _maxConcurrentCaptures;
        public int MaxConcurrentEnergyAbsorptions => _maxConcurrentEnergyAbsorptions;
        public int MaxConcurrentPropAbsorptions => _maxConcurrentPropAbsorptions;
        public float PullSpeed => _pullSpeed;
        public float LiftSpeed => _liftSpeed;
        public float BeamCenterThreshold => _beamCenterThreshold;
        public float CaptureSocketThreshold => _captureSocketThreshold;
        public bool ShrinkDuringLift => _shrinkDuringLift;
        public float MinimumVisualScale => _minimumVisualScale;
        public float LiftSpinSpeedDegreesPerSecond => _liftSpinSpeedDegreesPerSecond;
        public float BeamVisualRadius => _beamVisualRadius > 0f ? _beamVisualRadius : _attractionRadius;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_stableId))
            {
                Debug.LogError($"[UFOTractorBeamDefinition] '{name}' has an empty Stable Id.", this);
            }

            if (_maxConcurrentCaptures < 0)
            {
                _maxConcurrentCaptures = 0;
            }

            if (_maxConcurrentEnergyAbsorptions < 0)
            {
                _maxConcurrentEnergyAbsorptions = 0;
            }

            if (_maxConcurrentPropAbsorptions < 0)
            {
                _maxConcurrentPropAbsorptions = 0;
            }
        }
    }
}
