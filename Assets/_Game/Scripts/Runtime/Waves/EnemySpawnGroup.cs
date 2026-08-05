using System;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>Config for one batch of same-type enemies within a wave.</summary>
    [Serializable]
    public sealed class EnemySpawnGroup
    {
        [SerializeField]
        private EnemyDefinition _enemyDefinition;

        [SerializeField, Min(1)]
        private int _count = 1;

        [SerializeField, Min(0f)]
        private float _delayBeforeGroup;

        [SerializeField, Min(0f)]
        private float _spawnInterval = 1f;

        public EnemyDefinition EnemyDefinition => _enemyDefinition;
        public int Count => _count;
        public float DelayBeforeGroup => _delayBeforeGroup;
        public float SpawnInterval => _spawnInterval;

        public bool IsValid => _enemyDefinition != null && _count > 0;
    }
}
