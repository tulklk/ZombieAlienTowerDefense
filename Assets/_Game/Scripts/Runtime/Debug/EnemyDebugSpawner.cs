using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.DebugTools
{
    /// <summary>Manual enemy spawn trigger for testing before WaveController exists (Phase 4+).</summary>
    public sealed class EnemyDebugSpawner : MonoBehaviour
    {
        [SerializeField]
        private EnemyDefinition _definition;

        [SerializeField]
        private EnemyPath3D _path;

        [SerializeField]
        private bool _autoSpawnOnPlay;

        private EnemyFactory _factory;

        public void Initialize(EnemyFactory factory)
        {
            _factory = factory;

            if (_autoSpawnOnPlay)
            {
                SpawnOne();
            }
        }

        [ContextMenu("Spawn One Enemy")]
        public void SpawnOne()
        {
            if (_factory == null)
            {
                Debug.LogWarning("[EnemyDebugSpawner] Not initialized with an EnemyFactory yet.", this);
                return;
            }

            if (_definition == null || _path == null)
            {
                Debug.LogWarning("[EnemyDebugSpawner] EnemyDefinition and EnemyPath3D must both be assigned.", this);
                return;
            }

            Vector3 spawnPosition = _path.GetPoint(0);
            Quaternion spawnRotation = Quaternion.identity;
            _factory.Spawn(_definition, _path, spawnPosition, spawnRotation);
        }
    }
}
