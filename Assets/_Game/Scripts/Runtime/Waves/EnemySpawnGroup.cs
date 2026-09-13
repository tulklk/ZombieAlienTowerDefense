using System;
using System.Collections.Generic;
using System.Reflection;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>One paced batch inside a wave. Supports multiple enemy types and optional interleaving.</summary>
    [Serializable]
    public sealed class EnemySpawnGroup
    {
        private static readonly FieldInfo EntryDefinitionField =
            typeof(EnemySpawnEntry).GetField("_enemyDefinition", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo EntryCountField =
            typeof(EnemySpawnEntry).GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField]
        private EnemySpawnEntry[] _entries;

        [SerializeField, Min(0f)]
        private float _delayBeforeGroup;

        [SerializeField, Min(0f)]
        private float _spawnInterval = 1f;

        [SerializeField]
        [Tooltip("When true, mix entry types in the spawn queue (with anti-streak limits). When false, spawn each entry block in order.")]
        private bool _interleaveEntries;

        // Legacy single-type fields kept so older assets/tests still deserialize until rebuilt.
        [SerializeField, HideInInspector]
        private EnemyDefinition _enemyDefinition;

        [SerializeField, HideInInspector, Min(1)]
        private int _count = 1;

        public float DelayBeforeGroup => _delayBeforeGroup;
        public float SpawnInterval => _spawnInterval;
        public bool InterleaveEntries => _interleaveEntries;

        public int EntryCount
        {
            get
            {
                EnsureLegacyMigrated();
                return _entries?.Length ?? 0;
            }
        }

        public int Count
        {
            get
            {
                EnsureLegacyMigrated();
                int total = 0;
                if (_entries == null)
                {
                    return 0;
                }

                for (int i = 0; i < _entries.Length; i++)
                {
                    EnemySpawnEntry entry = _entries[i];
                    if (entry != null && entry.IsValid)
                    {
                        total += entry.Count;
                    }
                }

                return total;
            }
        }

        /// <summary>First valid definition (used by pool prewarm / legacy callers).</summary>
        public EnemyDefinition EnemyDefinition
        {
            get
            {
                EnsureLegacyMigrated();
                if (_entries == null)
                {
                    return null;
                }

                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].IsValid)
                    {
                        return _entries[i].EnemyDefinition;
                    }
                }

                return null;
            }
        }

        public bool IsValid => Count > 0;

        public EnemySpawnEntry GetEntry(int index)
        {
            EnsureLegacyMigrated();
            return _entries[index];
        }

        public void CollectDefinitions(HashSet<EnemyDefinition> destination)
        {
            if (destination == null)
            {
                return;
            }

            EnsureLegacyMigrated();
            if (_entries == null)
            {
                return;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                EnemySpawnEntry entry = _entries[i];
                if (entry != null && entry.IsValid)
                {
                    destination.Add(entry.EnemyDefinition);
                }
            }
        }

        /// <summary>Builds the ordered spawn list for this group. Null/invalid entries are skipped with a warning.</summary>
        public List<EnemyDefinition> BuildSpawnQueue()
        {
            EnsureLegacyMigrated();
            var queue = new List<EnemyDefinition>(Mathf.Max(0, Count));
            if (_entries == null || _entries.Length == 0)
            {
                return queue;
            }

            if (!_interleaveEntries)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    EnemySpawnEntry entry = _entries[i];
                    if (entry == null || entry.EnemyDefinition == null || entry.Count <= 0)
                    {
                        if (entry != null && entry.EnemyDefinition == null)
                        {
                            Debug.LogWarning("[EnemySpawnGroup] Skipping spawn entry with null EnemyDefinition.");
                        }

                        continue;
                    }

                    for (int c = 0; c < entry.Count; c++)
                    {
                        queue.Add(entry.EnemyDefinition);
                    }
                }

                return queue;
            }

            return BuildInterleavedQueue();
        }

        private List<EnemyDefinition> BuildInterleavedQueue()
        {
            var remaining = new List<int>(_entries.Length);
            int totalRemaining = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                EnemySpawnEntry entry = _entries[i];
                if (entry == null || entry.EnemyDefinition == null || entry.Count <= 0)
                {
                    if (entry != null && entry.EnemyDefinition == null)
                    {
                        Debug.LogWarning("[EnemySpawnGroup] Skipping spawn entry with null EnemyDefinition.");
                    }

                    remaining.Add(0);
                    continue;
                }

                remaining.Add(entry.Count);
                totalRemaining += entry.Count;
            }

            var queue = new List<EnemyDefinition>(totalRemaining);
            EnemyDefinition last = null;
            int streak = 0;

            while (totalRemaining > 0)
            {
                int chosen = -1;
                for (int pass = 0; pass < 2 && chosen < 0; pass++)
                {
                    bool ignoreStreak = pass > 0;
                    int bestRemaining = -1;
                    for (int i = 0; i < _entries.Length; i++)
                    {
                        if (remaining[i] <= 0)
                        {
                            continue;
                        }

                        EnemyDefinition candidate = _entries[i].EnemyDefinition;
                        int maxStreak = GetMaxSameTypeInRow(candidate);
                        if (!ignoreStreak && last == candidate && streak >= maxStreak)
                        {
                            continue;
                        }

                        if (remaining[i] > bestRemaining)
                        {
                            bestRemaining = remaining[i];
                            chosen = i;
                        }
                    }
                }

                if (chosen < 0)
                {
                    break;
                }

                EnemyDefinition picked = _entries[chosen].EnemyDefinition;
                queue.Add(picked);
                remaining[chosen]--;
                totalRemaining--;

                if (picked == last)
                {
                    streak++;
                }
                else
                {
                    last = picked;
                    streak = 1;
                }
            }

            return queue;
        }

        private static int GetMaxSameTypeInRow(EnemyDefinition definition)
        {
            if (definition == null)
            {
                return 2;
            }

            switch (definition.Archetype)
            {
                case EnemyArchetype.Normal:
                    return 3;
                case EnemyArchetype.Fast:
                    return 2;
                case EnemyArchetype.Tank:
                case EnemyArchetype.Elite:
                case EnemyArchetype.Boss:
                    return 1;
                default:
                    return 2;
            }
        }

        private void EnsureLegacyMigrated()
        {
            if (_entries != null && _entries.Length > 0)
            {
                return;
            }

            if (_enemyDefinition == null || _count <= 0)
            {
                return;
            }

            _entries = new[] { CreateEntry(_enemyDefinition, _count) };
        }

        private static EnemySpawnEntry CreateEntry(EnemyDefinition definition, int count)
        {
            var entry = new EnemySpawnEntry();
            EntryDefinitionField?.SetValue(entry, definition);
            EntryCountField?.SetValue(entry, count);
            return entry;
        }
    }
}
