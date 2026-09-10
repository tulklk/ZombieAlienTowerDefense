using System.Collections.Generic;
using AlienDefense.Common;
using UnityEngine;

namespace AlienDefense.Pickups
{
    /// <summary>Keeps the map stocked with loose Energy cubes for the player to collect and spend on towers -
    /// some strung along the zombie road, the rest scattered across the open ground. This is the ambient economy
    /// source; EnergyDropService remains the separate "this enemy died, here is its Energy" path, and the two
    /// share the same pool/registry so Max Alive counts both.
    ///
    /// Spawns through EnergyDropService rather than touching EnergyPickupFactory directly, so scattered cubes
    /// travel the exact same spawn -> beam -> collect -> reward -> pool route as dropped ones and need no
    /// special-casing anywhere downstream.
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
        [Tooltip("How many random placements to try before giving up on one cube for this pass. Placement can " +
            "fail legitimately - too close to another cube, off the terrain, or inside a building.")]
        private int _placementAttempts = 12;

        [SerializeField, Min(1f)]
        [Tooltip("How far above a candidate point the ground probe starts when checking what is standing there.")]
        private float _obstacleProbeHeight = 60f;

        [SerializeField, Min(0f)]
        [Tooltip("A candidate is rejected when solid geometry sits more than this above the terrain there - that " +
            "is a roof or a rock, and a cube placed on it would be unreachable or float visibly.")]
        private float _obstacleClearance = 0.6f;

        private EnergyDropService _dropService;
        private EnergyPickupRegistry _registry;
        private LevelBounds _levelBounds;
        private IReadOnlyList<Transform> _roadWaypoints;
        private Terrain[] _terrains;

        private readonly RaycastHit[] _probeHits = new RaycastHit[8];
        private float _topUpTimer;
        private bool _isInitialized;

        /// <summary>roadWaypoints and levelBounds are both optional: with no road every cube falls back to open
        /// scatter, and with no bounds the scatter is confined to the road. Losing both leaves nothing to place
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

            SpawnBatch(_definition.InitialCount);
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
            SpawnBatch(_definition.TopUpCount);
        }

        /// <summary>Places up to count cubes, stopping early at Max Alive. Individual placements are allowed to
        /// fail silently - a crowded map simply gets fewer cubes this pass and tries again on the next.</summary>
        private void SpawnBatch(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_registry.Count >= _definition.MaxAlive)
                {
                    return;
                }

                if (TryFindSpawnPoint(out Vector3 point))
                {
                    _dropService.Spawn(point, _definition.RollEnergyValue(), _definition.ExperienceReward);
                }
            }
        }

        private bool TryFindSpawnPoint(out Vector3 point)
        {
            for (int attempt = 0; attempt < _placementAttempts; attempt++)
            {
                // Re-rolled per attempt, not per batch, so a road that happens to be crowded can still fall back
                // to open ground instead of burning every attempt on the same lane.
                bool useRoad = HasRoad && Random.value < _definition.RoadShare;

                Vector3 candidate = useRoad ? SampleRoadPoint() : SampleOpenPoint();
                if (!TryResolveGroundHeight(candidate, out float groundY))
                {
                    continue;
                }

                candidate.y = groundY + _definition.GroundOffset;

                if (IsBlocked(candidate, groundY) || IsTooCloseToExistingPickup(candidate))
                {
                    continue;
                }

                point = candidate;
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>A uniformly random point along the road's polyline, pushed sideways by up to the definition's
        /// lateral spread so cubes line the lane rather than sitting in a single file down its centre.</summary>
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
            Vector3 min = _levelBounds.ClampXZ(new Vector3(-99999f, 0f, -99999f), _definition.BoundsPadding);
            Vector3 max = _levelBounds.ClampXZ(new Vector3(99999f, 0f, 99999f), _definition.BoundsPadding);

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

        /// <summary>True when something solid stands on the terrain here - a building roof, a boulder - so the
        /// cube would end up buried inside it or perched somewhere the beam can't sensibly reach.</summary>
        private bool IsBlocked(Vector3 candidate, float groundY)
        {
            var origin = new Vector3(candidate.x, groundY + _obstacleProbeHeight, candidate.z);
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _probeHits, _obstacleProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_probeHits[i].collider == null)
                {
                    continue;
                }

                if (_probeHits[i].point.y > groundY + _obstacleClearance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTooCloseToExistingPickup(Vector3 candidate)
        {
            float minSquared = _definition.MinSpacing * _definition.MinSpacing;

            for (int i = 0; i < _registry.Count; i++)
            {
                EnergyPickupController existing = _registry.GetAt(i);
                if (existing == null)
                {
                    continue;
                }

                Vector3 delta = existing.transform.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < minSquared)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
