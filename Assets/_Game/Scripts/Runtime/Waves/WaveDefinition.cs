using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>Config-only description of one wave: its spawn groups and optional pacing override.</summary>
    [CreateAssetMenu(fileName = "WaveDefinition", menuName = "AlienDefense/Waves/Wave Definition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [SerializeField]
        private string _id = "wave_01";

        [SerializeField]
        private string _displayName = "Wave";

        [SerializeField]
        private EnemySpawnGroup[] _spawnGroups;

        [SerializeField]
        [Tooltip("Negative = use the level's default preparation duration.")]
        private float _preparationDurationOverride = -1f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int SpawnGroupCount => _spawnGroups?.Length ?? 0;
        public bool HasPreparationOverride => _preparationDurationOverride >= 0f;
        public float PreparationDurationOverride => _preparationDurationOverride;

        public EnemySpawnGroup GetSpawnGroup(int index)
        {
            return _spawnGroups[index];
        }

        public int TotalPlannedEnemyCount()
        {
            int total = 0;
            for (int i = 0; i < SpawnGroupCount; i++)
            {
                EnemySpawnGroup group = _spawnGroups[i];
                if (group != null && group.IsValid)
                {
                    total += group.Count;
                }
            }

            return total;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[WaveDefinition] '{name}' has an empty Id.", this);
            }

            if (_spawnGroups == null || _spawnGroups.Length == 0)
            {
                Debug.LogError($"[WaveDefinition] '{name}' has no spawn groups.", this);
                return;
            }

            for (int i = 0; i < _spawnGroups.Length; i++)
            {
                EnemySpawnGroup group = _spawnGroups[i];
                if (group == null || !group.IsValid)
                {
                    Debug.LogError($"[WaveDefinition] '{name}' has an invalid spawn group at index {i} " +
                        "(no valid EnemySpawnEntry counts).", this);
                }
            }
        }
    }
}
