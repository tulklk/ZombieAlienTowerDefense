using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.Data
{
    /// <summary>Configuration-only description of a level: starting economy, base health, pacing, waves.</summary>
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "AlienDefense/Level/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField]
        private string _levelId = "level_01";

        [SerializeField, Min(0)]
        private int _startingResource = 250;

        [SerializeField, Min(1)]
        private int _baseMaxHealth = 20;

        [SerializeField, Min(0f)]
        private float _preparationDuration = 5f;

        [SerializeField, Min(1)]
        private int _targetFrameRate = 60;

        [SerializeField]
        private WaveDefinition[] _waves;

        public string LevelId => _levelId;
        public int StartingResource => _startingResource;
        public int BaseMaxHealth => _baseMaxHealth;
        public float PreparationDuration => _preparationDuration;
        public int TargetFrameRate => _targetFrameRate;
        public int WaveCount => _waves?.Length ?? 0;

        public WaveDefinition GetWave(int index)
        {
            return _waves[index];
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_levelId))
            {
                Debug.LogError($"[LevelDefinition] '{name}' has an empty Level Id.", this);
            }

            if (_startingResource < 0)
            {
                _startingResource = 0;
            }

            if (_baseMaxHealth < 1)
            {
                _baseMaxHealth = 1;
            }

            if (_preparationDuration < 0f)
            {
                _preparationDuration = 0f;
            }

            if (_targetFrameRate < 1)
            {
                _targetFrameRate = 1;
            }

            if (_waves == null || _waves.Length == 0)
            {
                Debug.LogError($"[LevelDefinition] '{name}' has no waves assigned.", this);
                return;
            }

            for (int i = 0; i < _waves.Length; i++)
            {
                if (_waves[i] == null)
                {
                    Debug.LogError($"[LevelDefinition] '{name}' has a null wave at index {i}.", this);
                }
            }
        }
    }
}
