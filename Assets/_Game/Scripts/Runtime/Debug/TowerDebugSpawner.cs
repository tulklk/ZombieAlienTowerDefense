using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.DebugTools
{
    /// <summary>Manual tower spawn trigger for testing before the Building system exists (Phase 7+).</summary>
    public sealed class TowerDebugSpawner : MonoBehaviour
    {
        [SerializeField]
        private TowerDefinition _definition;

        [SerializeField]
        private Transform _spawnPoint;

        [SerializeField]
        private bool _autoSpawnOnPlay;

        private TowerFactory _factory;

        public void Initialize(TowerFactory factory)
        {
            _factory = factory;

            if (_autoSpawnOnPlay)
            {
                SpawnOne();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Spawn One Tower")]
#endif
        public void SpawnOne()
        {
            if (_factory == null)
            {
                Debug.LogWarning("[TowerDebugSpawner] Not initialized with a TowerFactory yet.", this);
                return;
            }

            if (_definition == null || _spawnPoint == null)
            {
                Debug.LogWarning("[TowerDebugSpawner] TowerDefinition and SpawnPoint must both be assigned.", this);
                return;
            }

            _factory.Create(_definition, _spawnPoint.position, _spawnPoint.rotation);
        }
    }
}
