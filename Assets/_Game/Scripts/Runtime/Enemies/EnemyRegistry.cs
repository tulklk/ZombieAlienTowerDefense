using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Tracks currently active enemies for targeting/queries, without exposing a mutable list.</summary>
    public sealed class EnemyRegistry
    {
        private readonly List<EnemyController> _activeEnemies = new List<EnemyController>();

        public int Count => _activeEnemies.Count;

        public EnemyController GetAt(int index)
        {
            return _activeEnemies[index];
        }

        public bool Register(EnemyController enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (_activeEnemies.Contains(enemy))
            {
                Debug.LogWarning("[EnemyRegistry] Enemy already registered; ignoring duplicate.", enemy);
                return false;
            }

            _activeEnemies.Add(enemy);
            return true;
        }

        public bool Unregister(EnemyController enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            return _activeEnemies.Remove(enemy);
        }

        public void Clear()
        {
            _activeEnemies.Clear();
        }
    }
}
