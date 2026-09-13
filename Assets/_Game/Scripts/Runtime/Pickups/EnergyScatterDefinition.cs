using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Per-level tuning for the ambient Energy scatter (see EnergyScatterSpawner) - the loose Energy
    /// balls lying around the map for the player to hoover up and spend on towers, as opposed to the ones
    /// EnergyDropService drops when a specific enemy dies.
    ///
    /// Energy is placed in clusters rather than one ball at a time: a tight little heap the player can sweep up
    /// in a single pass of the beam reads as a reward worth flying to, where the same number of balls spread
    /// evenly just reads as noise. The defaults make one cluster worth exactly one tower (10 balls x 1 Energy
    /// against a 10-Energy build cost and a 10-Energy wallet), so "clear a cluster, build a tower" is the loop.
    ///
    /// A ScriptableObject rather than fields on the spawner so each level carries its own pacing - one asset
    /// per level, assigned on that level's EnergyScatterSpawner.</summary>
    [CreateAssetMenu(fileName = "EnergyScatterDefinition", menuName = "AlienDefense/Pickups/Energy Scatter Definition")]
    public sealed class EnergyScatterDefinition : ScriptableObject
    {
        [Header("Clusters")]
        [SerializeField, Min(1)]
        [Tooltip("Balls per cluster.")]
        private int _clusterSize = 10;

        [SerializeField, Min(0.1f)]
        [Tooltip("How far from the cluster's centre a ball may land. Balls are packed towards the centre and " +
            "thin out towards this edge, so a few stragglers sit a little apart from the main heap.")]
        private float _clusterRadius = 1.4f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Closest two balls may sit to each other. Just above the ball's own size keeps a cluster tight " +
            "without balls clipping into one another.")]
        private float _pickupSpacing = 0.45f;

        [SerializeField, Min(0f)]
        [Tooltip("Closest a new cluster's centre may be to any Energy already on the ground, so clusters stay " +
            "distinct heaps instead of merging into one sprawl.")]
        private float _clusterSpacing = 10f;

        [Header("Population")]
        [SerializeField, Min(0)]
        [Tooltip("Clusters placed the moment the level starts, so the map never opens empty.")]
        private int _initialClusters = 8;

        [SerializeField, Min(0)]
        [Tooltip("Ceiling on clusters' worth of Energy lying around at once. Top-ups stop here, which is what " +
            "keeps a level the player is ignoring from silently filling up.")]
        private int _maxClusters = 10;

        [Header("Top-up")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds between top-up passes. Each pass adds one whole cluster, and only when there is room " +
            "for all of it under Max Clusters.")]
        private float _topUpInterval = 7f;

        [Header("Placement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Share of clusters placed on the zombie road instead of out in the fields. 1 = road only, " +
            "0 = never on the road. The road share is what pulls the player into the lane where the fighting is.")]
        private float _roadShare = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("How far to either side of the road's centre line a road cluster's centre may sit. The cluster " +
            "radius is added on top, so keep this modest or road clusters spill into the verges.")]
        private float _roadLateralSpread = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Padding kept clear inside the level bounds, so clusters never land hard against the edge where " +
            "the camera can't comfortably frame them.")]
        private float _boundsPadding = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("Height above the ground surface a ball is placed at.")]
        private float _groundOffset = 0.35f;

        [Header("Reward")]
        [SerializeField, Min(1)]
        [Tooltip("Energy per ball. The wallet caps at its Max Energy and discards any overflow, so this wants to " +
            "stay small - a cluster should add up to roughly one build, not overflow the wallet on its own.")]
        private int _energyPerPickup = 1;

        [SerializeField, Range(0, 2)]
        [Tooltip("XP granted per ball alongside the Energy. 0 keeps scattered Energy a pure economy source, " +
            "leaving levelling to absorbed props and kills.")]
        private int _experienceReward = 0;

        public int ClusterSize => _clusterSize;
        public float ClusterRadius => _clusterRadius;
        public float PickupSpacing => _pickupSpacing;
        public float ClusterSpacing => _clusterSpacing;
        public int InitialClusters => _initialClusters;
        public int MaxClusters => _maxClusters;
        public float TopUpInterval => _topUpInterval;
        public float RoadShare => _roadShare;
        public float RoadLateralSpread => _roadLateralSpread;
        public float BoundsPadding => _boundsPadding;
        public float GroundOffset => _groundOffset;
        public int EnergyPerPickup => _energyPerPickup;
        public int ExperienceReward => _experienceReward;

        /// <summary>Most balls that may be on the ground at once - whole clusters' worth.</summary>
        public int MaxAlive => _maxClusters * _clusterSize;

        private void OnValidate()
        {
            if (_maxClusters < _initialClusters)
            {
                _maxClusters = _initialClusters;
            }

            // A spacing wider than the cluster itself makes a full cluster geometrically impossible to place.
            if (_pickupSpacing > _clusterRadius)
            {
                _pickupSpacing = _clusterRadius;
            }
        }
    }
}
