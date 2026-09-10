using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Per-level tuning for the ambient Energy scatter (see EnergyScatterSpawner) - the loose Energy
    /// cubes lying around the map for the player to hoover up and spend on towers, as opposed to the ones
    /// EnergyDropService drops when a specific enemy dies.
    ///
    /// A ScriptableObject rather than fields on the spawner so each level can carry its own economy pacing: an
    /// early level can afford a dense, generous field of cubes while a later, larger map wants them sparse and
    /// worth hunting for. One asset per level, assigned on that level's EnergyScatterSpawner.</summary>
    [CreateAssetMenu(fileName = "EnergyScatterDefinition", menuName = "AlienDefense/Pickups/Energy Scatter Definition")]
    public sealed class EnergyScatterDefinition : ScriptableObject
    {
        [Header("Population")]
        [SerializeField, Min(0)]
        [Tooltip("Cubes placed in one go the moment the level starts, so the map never opens empty.")]
        private int _initialCount = 28;

        [SerializeField, Min(0)]
        [Tooltip("Ceiling on cubes lying around at once. Top-ups stop here; this is what keeps a level the " +
            "player is ignoring from silently filling up with hundreds of pickups.")]
        private int _maxAlive = 40;

        [Header("Top-up")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds between top-up passes that refill what the player has collected.")]
        private float _topUpInterval = 4f;

        [SerializeField, Min(0)]
        [Tooltip("Cubes added per top-up pass, while under Max Alive.")]
        private int _topUpCount = 3;

        [Header("Placement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Share of cubes placed along the zombie road instead of loose around the map. 1 = road only, " +
            "0 = never on the road. The road share is what pulls the player into the lane where the fighting is.")]
        private float _roadShare = 0.45f;

        [SerializeField, Min(0f)]
        [Tooltip("How far to either side of the road's centre line road-placed cubes may sit. Keep this near the " +
            "visual width of the road or cubes will drift into the fields and stop reading as 'on the road'.")]
        private float _roadLateralSpread = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Padding kept clear inside the level bounds, so cubes never spawn hard against the edge where " +
            "the camera can't comfortably frame them.")]
        private float _boundsPadding = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("Closest two cubes may be placed to each other. Stops the scatter clumping into a single pile " +
            "the beam swallows in one pass.")]
        private float _minSpacing = 2.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Height above the ground surface a cube is placed at.")]
        private float _groundOffset = 0.35f;

        [Header("Reward")]
        [SerializeField, Min(1)]
        private int _energyValueMin = 1;

        [SerializeField, Min(1)]
        private int _energyValueMax = 3;

        [SerializeField, Range(0, 2)]
        [Tooltip("XP granted alongside the Energy. 0 keeps scattered cubes a pure economy source, leaving " +
            "levelling to absorbed props and kills.")]
        private int _experienceReward = 0;

        public int InitialCount => _initialCount;
        public int MaxAlive => _maxAlive;
        public float TopUpInterval => _topUpInterval;
        public int TopUpCount => _topUpCount;
        public float RoadShare => _roadShare;
        public float RoadLateralSpread => _roadLateralSpread;
        public float BoundsPadding => _boundsPadding;
        public float MinSpacing => _minSpacing;
        public float GroundOffset => _groundOffset;
        public int ExperienceReward => _experienceReward;

        /// <summary>Inclusive on both ends - Random.Range's int overload is exclusive on the upper bound, which
        /// would quietly make Energy Value Max unreachable.</summary>
        public int RollEnergyValue()
        {
            int min = Mathf.Min(_energyValueMin, _energyValueMax);
            int max = Mathf.Max(_energyValueMin, _energyValueMax);
            return Random.Range(min, max + 1);
        }

        private void OnValidate()
        {
            if (_energyValueMax < _energyValueMin)
            {
                _energyValueMax = _energyValueMin;
            }

            if (_maxAlive < _initialCount)
            {
                _maxAlive = _initialCount;
            }
        }
    }
}
