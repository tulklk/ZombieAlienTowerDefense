using System.Collections.Generic;
using AlienDefense.Common;
using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Keeps the map stocked with clusters of loose Energy for the player to collect and spend on towers
    /// - some heaps sitting on the zombie road, the rest out across the open ground. This is the ambient economy
    /// source; EnergyDropService remains the separate "this enemy died, here is its Energy" path, and the two
    /// share the same pool/registry so Max Alive counts both.
    ///
    /// Each cluster is planned in full before a single ball is spawned: several candidate centres are tried and
    /// the one that fits the most balls wins, stopping early as soon as one fits them all. Spawning ball by ball
    /// against a single centre would leave clusters on busy ground (bushes, fences, rocks) visibly short.
    ///
    /// Spawns through EnergyDropService rather than touching EnergyPickupFactory directly, so scattered balls
    /// travel the exact same spawn -> beam -> collect -> reward -> pool route as dropped ones.
    ///
    /// Takes the road as a plain waypoint list rather than an EnemyPath3D: AlienDefense.Pickups deliberately
    /// never depends on AlienDefense.Enemies (see EnergyDropService), so LevelCompositionRoot - which may
    /// reference both - passes the Transforms in.</summary>
    public sealed class EnergyScatterSpawner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Per-level tuning. Without one this spawner stays completely dormant, which is how a level " +
            "opts out of ambient Energy entirely.")]
        private EnergyScatterDefinition _definition;

        [SerializeField, Min(1)]
        [Tooltip("Candidate centres tried per cluster. The best-fitting one is used.")]
        private int _centreAttempts = 10;

        [SerializeField, Min(1)]
        [Tooltip("Random spots tried per ball inside a cluster before that ball is given up on.")]
        private int _pickupAttempts = 12;

        [SerializeField, Min(1f)]
        [Tooltip("How far above a candidate point the ground probe starts when checking what is standing there.")]
        private float _obstacleProbeHeight = 60f;

        [SerializeField, Min(0f)]
        [Tooltip("A spot is rejected when solid geometry sits more than this above the terrain there - a roof, a " +
            "bush or a rock - so balls never end up buried inside something.")]
        private float _obstacleClearance = 0.6f;

        private EnergyDropService _dropService;
        private EnergyPickupRegistry _registry;
        private LevelBounds _levelBounds;
        private IReadOnlyList<Transform> _roadWaypoints;
        private Terrain[] _terrains;

        private readonly RaycastHit[] _probeHits = new RaycastHit[8];
        private readonly List<Vector3> _plan = new List<Vector3>();
        private readonly List<Vector3> _bestPlan = new List<Vector3>();
        private float _topUpTimer;
        private bool _isInitialized;

        /// <summary>roadWaypoints and levelBounds are both optional: with no road every cluster falls back to
        /// open ground, and with no bounds clusters are confined to the road. Losing both leaves nothing to place
        /// against, and the spawner reports that rather than silently doing nothing.</summary>
        public void Initialize(
            EnergyDropService dropService,
            EnergyPickupRegistry registry,
            IReadOnlyList<Transform> roadWaypoints,
            LevelBounds levelBounds)
        {
            _dropService = dropService;
            _registry = registry;
            _roadWaypoints = roadWaypoints;
            _levelBounds = levelBounds;

            // Terrain.activeTerrains allocates a fresh array per call, so it is cached rather than re-queried.
            _terrains = Terrain.activeTerrains;

            if (_definition == null)
            {
                return;
            }

            if (_dropService == null || _registry == null)
            {
                Debug.LogWarning("[EnergyScatterSpawner] No EnergyDropService/EnergyPickupRegistry; ambient " +
                    "Energy will not spawn. This level's Energy Pickup loop is probably not wired.", this);
                return;
            }

            if (!HasRoad && _levelBounds == null)
            {
                Debug.LogWarning("[EnergyScatterSpawner] Neither an enemy path nor LevelBounds was supplied; " +
                    "there is nowhere to scatter Energy.", this);
                return;
            }

            _isInitialized = true;
            _topUpTimer = _definition.TopUpInterval;

            for (int i = 0; i < _definition.InitialClusters; i++)
            {
                TrySpawnCluster();
            }
        }

        private bool HasRoad => _roadWaypoints != null && _roadWaypoints.Count >= 2;

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            _topUpTimer -= Time.deltaTime;
            if (_topUpTimer > 0f)
            {
                return;
            }

            _topUpTimer = _definition.TopUpInterval;

            // Whole clusters only: a top-up that could only fit half a heap would scatter the very stragglers
            // clustering exists to avoid, so it waits until the player has cleared room for a full one.
            if (_registry.Count + _definition.ClusterSize <= _definition.MaxAlive)
            {
                TrySpawnCluster();
            }
        }

        private void TrySpawnCluster()
        {
            _bestPlan.Clear();

            for (int attempt = 0; attempt < _centreAttempts; attempt++)
            {
                if (!TryFindClusterCentre(out Vector3 centre))
                {
                    continue;
                }

                PlanCluster(centre);
                if (_plan.Count > _bestPlan.Count)
                {
                    _bestPlan.Clear();
                    _bestPlan.AddRange(_plan);
                }

                if (_bestPlan.Count >= _definition.ClusterSize)
                {
                    break;
                }
            }

            for (int i = 0; i < _bestPlan.Count; i++)
            {
                _dropService.Spawn(_bestPlan[i], _definition.EnergyPerPickup, _definition.ExperienceReward);
            }
        }

        /// <summary>A clear, reachable spot far enough from every ball already on the ground that the new cluster
        /// reads as its own heap.</summary>
        private bool TryFindClusterCentre(out Vector3 centre)
        {
            // Re-rolled per attempt so a crowded road can still fall back to open ground.
            bool useRoad = HasRoad && Random.value < _definition.RoadShare;
            Vector3 candidate = useRoad ? SampleRoadPoint() : SampleOpenPoint();

            if (!TryResolveGroundHeight(candidate, out float groundY) || IsBlocked(candidate, groundY) ||
                IsWithinOfExistingPickup(candidate, _definition.ClusterSpacing))
            {
                centre = default;
                return false;
            }

            centre = new Vector3(candidate.x, groundY, candidate.z);
            return true;
        }

        /// <summary>Fills _plan with up to ClusterSize ball positions around centre. Offsets are drawn with a
        /// linear (not square-rooted) radius, which packs balls towards the middle and leaves only a few out
        /// near the rim - a heap with stragglers rather than an even disc.</summary>
        private void PlanCluster(Vector3 centre)
        {
            _plan.Clear();
            float spacingSquared = _definition.PickupSpacing * _definition.PickupSpacing;

            for (int ball = 0; ball < _definition.ClusterSize; ball++)
            {
                for (int attempt = 0; attempt < _pickupAttempts; attempt++)
                {
                    Vector2 direction = Random.insideUnitCircle.normalized;
                    if (direction.sqrMagnitude < 0.0001f)
                    {
                        direction = Vector2.right;
                    }

                    float distance = _definition.ClusterRadius * Random.value;
                    var candidate = new Vector3(centre.x + direction.x * distance, 0f, centre.z + direction.y * distance);

                    if (!TryResolveGroundHeight(candidate, out float groundY) || IsBlocked(candidate, groundY))
                    {
                        continue;
                    }

                    if (IsTooCloseToPlan(candidate, spacingSquared) ||
                        IsWithinOfExistingPickup(candidate, _definition.PickupSpacing))
                    {
                        continue;
                    }

                    candidate.y = groundY + _definition.GroundOffset;
                    _plan.Add(candidate);
                    break;
                }
            }
        }

        /// <summary>A uniformly random point along the road's polyline, pushed sideways by up to the definition's
        /// lateral spread.</summary>
        private Vector3 SampleRoadPoint()
        {
            int segment = Random.Range(0, _roadWaypoints.Count - 1);
            Transform from = _roadWaypoints[segment];
            Transform to = _roadWaypoints[segment + 1];

            if (from == null || to == null)
            {
                return SampleOpenPoint();
            }

            Vector3 along = Vector3.Lerp(from.position, to.position, Random.value);

            Vector3 direction = to.position - from.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return along;
            }

            Vector3 lateral = Vector3.Cross(Vector3.up, direction.normalized);
            return along + lateral * Random.Range(-_definition.RoadLateralSpread, _definition.RoadLateralSpread);
        }

        private Vector3 SampleOpenPoint()
        {
            if (_levelBounds == null)
            {
                return HasRoad ? SampleRoadPoint() : transform.position;
            }

            // ClampXZ is the only public description of the playable rectangle, so the extremes are read out of
            // it rather than duplicating centre/extents here - two huge opposite corners clamp to the real edges.
            float padding = _definition.BoundsPadding + _definition.ClusterRadius;
            Vector3 min = _levelBounds.ClampXZ(new Vector3(-99999f, 0f, -99999f), padding);
            Vector3 max = _levelBounds.ClampXZ(new Vector3(99999f, 0f, 99999f), padding);

            return new Vector3(Random.Range(min.x, max.x), 0f, Random.Range(min.z, max.z));
        }

        private bool TryResolveGroundHeight(Vector3 worldPosition, out float groundY)
        {
            if (_terrains != null)
            {
                for (int i = 0; i < _terrains.Length; i++)
                {
                    Terrain terrain = _terrains[i];
                    if (terrain == null || terrain.terrainData == null)
                    {
                        continue;
                    }

                    Vector3 origin = terrain.transform.position;
                    Vector3 size = terrain.terrainData.size;

                    if (worldPosition.x < origin.x || worldPosition.x > origin.x + size.x ||
                        worldPosition.z < origin.z || worldPosition.z > origin.z + size.z)
                    {
                        continue;
                    }

                    groundY = origin.y + terrain.SampleHeight(worldPosition);
                    return true;
                }
            }

            groundY = 0f;
            return false;
        }

        /// <summary>True when something solid stands on the terrain here - a roof, a bush, a boulder - so a ball
        /// would end up buried inside it or perched somewhere the beam can't sensibly reach.</summary>
        private bool IsBlocked(Vector3 candidate, float groundY)
        {
            var origin = new Vector3(candidate.x, groundY + _obstacleProbeHeight, candidate.z);
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _probeHits, _obstacleProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_probeHits[i].collider != null && _probeHits[i].point.y > groundY + _obstacleClearance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTooCloseToPlan(Vector3 candidate, float spacingSquared)
        {
            for (int i = 0; i < _plan.Count; i++)
            {
                Vector3 delta = _plan[i] - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < spacingSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsWithinOfExistingPickup(Vector3 candidate, float distance)
        {
            float distanceSquared = distance * distance;

            for (int i = 0; i < _registry.Count; i++)
            {
                EnergyPickupController existing = _registry.GetAt(i);
                if (existing == null)
                {
                    continue;
                }

                Vector3 delta = existing.transform.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < distanceSquared)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
