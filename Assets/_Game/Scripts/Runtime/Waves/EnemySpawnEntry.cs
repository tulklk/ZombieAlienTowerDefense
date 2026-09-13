using System;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>One enemy type + count inside a spawn group.</summary>
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [SerializeField]
        private EnemyDefinition _enemyDefinition;

        [SerializeField, Min(1)]
        private int _count = 1;

        public EnemyDefinition EnemyDefinition => _enemyDefinition;
        public int Count => _count;

        public bool IsValid => _enemyDefinition != null && _count > 0;
    }
}
