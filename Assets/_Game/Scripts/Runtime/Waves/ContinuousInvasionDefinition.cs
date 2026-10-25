using System;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>Config for a level that is ONE continuous attack instead of separate waves: for BossCountdown seconds
    /// of combat time enemies keep coming one after another (never waiting for anything to be cleared), the mix and
    /// the pace shifting smoothly with combat progress, with scheduled mini bosses dropped into the stream; then
    /// regular spawning stops and the level's BossEncounterDefinition arrives. Played by WaveController when the
    /// LevelDefinition has one (see LevelDefinition.ContinuousInvasion). Max alive enemies comes from the
    /// LevelDefinition (WaveSpawnSettings.MaxAliveEnemies) like every other level; mini bosses and the final boss
    /// are spawned even when that cap is reached. Optional Lanes split the stream into per-route time windows
    /// (Level 3: left first, right from 1:00, left again from 2:40), and a mini boss entry can be a convoy
    /// (Count members, Spacing seconds apart) on a chosen route.</summary>
    [CreateAssetMenu(fileName = "ContinuousInvasion", menuName = "AlienDefense/Waves/Continuous Invasion Definition")]
    public sealed class ContinuousInvasionDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class EnemyEntry
        {
            public EnemyDefinition Enemy;

            [Min(0f)]
            [Tooltip("Relative weight at combat second 0 (the weights of every entry are normalised together).")]
            public float StartWeight = 1f;

            [Min(0f)]
            [Tooltip("Relative weight at BossCountdown. Interpolated linearly in between.")]
            public float EndWeight = 1f;

            [Min(0f)]
            [Tooltip("Combat seconds before this type can spawn at all (0 = from the start).")]
            public float UnlockTime;

            [Min(1)]
            [Tooltip("Never more than this many of this type in a row; the next pick is weighted among the others.")]
            public int MaxConsecutive = 3;

            [Min(0)]
            [Tooltip("Base damage this type deals when it reaches the base in this level. 0 = the definition's " +
                "BaseDamage scaled by the level's Enemy Damage Multiplier.")]
            public int BaseDamage;

            public bool IsValid => Enemy != null;
        }

        [Serializable]
        public sealed class MiniBossEntry
        {
            [Min(0f)]
            [Tooltip("Combat seconds when this mini boss joins the stream (the regular stream does not pause).")]
            public float Time;

            public EnemyDefinition Enemy;

            [Min(0.1f)]
            [Tooltip("Uniform scale on top of the prefab's own scale.")]
            public float Scale = 1.25f;

            [Min(0.1f)]
            [Tooltip("Health multiplier on top of the level's normal health scaling for this enemy type.")]
            public float HealthMultiplier = 3f;

            [Min(0.1f)]
            [Tooltip("Speed multiplier on top of the level's normal speed scaling for this enemy type.")]
            public float SpeedMultiplier = 0.95f;

            [Min(1)]
            [Tooltip("Base damage dealt on reaching the base.")]
            public int BaseDamage = 2;

            [Tooltip("Route index (0 = WaveController Path, 1 = first Additional Path, ...). -1 = picked like a regular " +
                "enemy (random route).")]
            public int PathIndex = -1;

            [Min(1)]
            [Tooltip("How many of this mini boss come in a row (a convoy). 1 = a single one at Time.")]
            public int Count = 1;

            [Min(0.1f)]
            [Tooltip("Seconds between the members of a convoy (Count > 1), so they walk the route spread out.")]
            public float Spacing = 2f;

            [Min(1)]
            [Tooltip("Its health bar is split into this many layers, shown as \"x5\" etc. (1 = a single bar).")]
            public int HealthLayers = 1;

            public bool IsValid => Enemy != null;
        }

        /// <summary>A time window in which one route has its own regular stream. With no lanes the level has one
        /// stream whose enemies pick a route at random (the original behaviour).</summary>
        [Serializable]
        public sealed class LaneWindow
        {
            [Tooltip("Shown in the debug log, e.g. \"Left\".")]
            public string Label = "Lane";

            [Min(0)]
            [Tooltip("Route index (0 = WaveController Path, 1 = first Additional Path, ...).")]
            public int PathIndex;

            [Min(0f)]
            [Tooltip("Combat seconds when this lane starts spawning regular enemies.")]
            public float StartTime;

            [Min(0f)]
            [Tooltip("Combat seconds when this lane stops spawning NEW regular enemies (enemies already on it carry on).")]
            public float EndTime = 60f;
        }

        [Header("Timing")]
        [SerializeField, Min(1f)]
        [Tooltip("Combat seconds (counted from the end of the 3-2-1 countdown, never during the intro) until " +
            "regular spawning stops and the final boss arrives.")]
        private float _bossCountdown = 240f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between regular spawns at combat second 0.")]
        private float _spawnIntervalStart = 1.15f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between regular spawns at BossCountdown. Interpolated linearly in between.")]
        private float _spawnIntervalEnd = 0.78f;

        [Header("Regular stream")]
        [SerializeField]
        private EnemyEntry[] _enemies = Array.Empty<EnemyEntry>();

        [Header("Lanes (optional - several routes with their own timing)")]
        [SerializeField]
        [Tooltip("Empty = one regular stream, random route per enemy. Otherwise every window is its own stream on " +
            "its route, each at GetSpawnInterval; the level's Max Alive Enemies is shared by all of them.")]
        private LaneWindow[] _lanes = Array.Empty<LaneWindow>();

        [SerializeField, Min(0)]
        [Tooltip("With lanes: regular enemies alive per ROUTE that a lane may keep (each route has its own budget, so " +
            "a route opening later is not starved by enemies still walking the other one). 0 = the level's Max Alive " +
            "Enemies, shared by all lanes. Mini bosses and the final boss never count and are never held back.")]
        private int _maxAlivePerLane;

        [SerializeField, Min(1)]
        [Tooltip("With lanes: each spawn tick releases a pack of this many enemies at least (a crowd walking side by " +
            "side, not single file).")]
        private int _packSizeMin = 1;

        [SerializeField, Min(1)]
        [Tooltip("With lanes: ...and at most this many (picked at random per tick).")]
        private int _packSizeMax = 1;

        [SerializeField, Min(0f)]
        [Tooltip("Metres between the members of a pack along the route, so they do not stand inside each other.")]
        private float _packStagger = 0.8f;

        [Header("Mini bosses (dropped into the stream)")]
        [SerializeField]
        private MiniBossEntry[] _miniBosses = Array.Empty<MiniBossEntry>();

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Editor/Development only: logs the start, each unlock, each mini boss and the final boss.")]
        private bool _debugLogs = true;

        public float BossCountdown => _bossCountdown;
        public bool DebugLogs => _debugLogs && (Application.isEditor || Debug.isDebugBuild);
        public int EnemyCount => _enemies?.Length ?? 0;
        public int MiniBossCount => _miniBosses?.Length ?? 0;
        public int LaneCount => _lanes?.Length ?? 0;
        public int MaxAlivePerLane => _maxAlivePerLane;
        public int PackSizeMin => Mathf.Max(1, _packSizeMin);
        public int PackSizeMax => Mathf.Max(PackSizeMin, _packSizeMax);
        public float PackStagger => _packStagger;

        public LaneWindow GetLane(int index) => _lanes[index];

        public EnemyEntry GetEnemy(int index) => _enemies[index];

        public MiniBossEntry GetMiniBoss(int index) => _miniBosses[index];

        public bool IsValid
        {
            get
            {
                for (int i = 0; i < EnemyCount; i++)
                {
                    if (_enemies[i] != null && _enemies[i].IsValid)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Seconds until the next regular spawn at the given combat time.</summary>
        public float GetSpawnInterval(float combatElapsed)
        {
            return Mathf.Lerp(_spawnIntervalStart, _spawnIntervalEnd, Progress(combatElapsed));
        }

        /// <summary>Weight of one entry at the given combat time: 0 before its unlock, then its start->end weight.</summary>
        public float GetWeight(int index, float combatElapsed)
        {
            EnemyEntry entry = _enemies[index];
            if (entry == null || !entry.IsValid || combatElapsed < entry.UnlockTime)
            {
                return 0f;
            }

            return Mathf.Lerp(entry.StartWeight, entry.EndWeight, Progress(combatElapsed));
        }

        public float Progress(float combatElapsed)
        {
            return Mathf.Clamp01(combatElapsed / Mathf.Max(0.01f, _bossCountdown));
        }

        private void OnValidate()
        {
            if (_enemies == null || _enemies.Length == 0)
            {
                Debug.LogError($"[ContinuousInvasionDefinition] '{name}' has no enemy entries.", this);
            }

            for (int i = 0; i < LaneCount; i++)
            {
                if (_lanes[i] != null && _lanes[i].EndTime <= _lanes[i].StartTime)
                {
                    Debug.LogWarning($"[ContinuousInvasionDefinition] '{name}': lane {i} ends before it starts.", this);
                }
            }
        }
    }
}
