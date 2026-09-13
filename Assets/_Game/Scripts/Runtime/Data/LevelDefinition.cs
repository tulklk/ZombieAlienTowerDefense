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

        [Header("Wave Difficulty")]
        [SerializeField, Min(0)]
        [Tooltip("0 = unlimited. Spawner waits when this many enemies are alive.")]
        private int _maxAliveEnemies = 15;

        [SerializeField, Min(0f)]
        [Tooltip("Added to HP multiplier each wave after wave 1: 1 + (wave-1) * step.")]
        private float _healthPerWaveStep = 0.10f;

        [SerializeField, Min(0f)]
        private float _speedPerWaveStep = 0.015f;

        [SerializeField, Min(0f)]
        private float _damagePerWaveStep = 0.07f;

        [SerializeField, Min(1f)]
        [Tooltip("Hard cap on speed multiplier (keeps Fast enemies from becoming absurd).")]
        private float _maxSpeedMultiplier = 1.22f;

        [SerializeField]
        private bool _debugWaveLogs;

        [SerializeField]
        private WaveDefinition[] _waves;

        public string LevelId => _levelId;
        public int StartingResource => _startingResource;
        public int BaseMaxHealth => _baseMaxHealth;
        public float PreparationDuration => _preparationDuration;
        public int TargetFrameRate => _targetFrameRate;
        public int MaxAliveEnemies => _maxAliveEnemies;
        public float HealthPerWaveStep => _healthPerWaveStep;
        public float SpeedPerWaveStep => _speedPerWaveStep;
        public float DamagePerWaveStep => _damagePerWaveStep;
        public float MaxSpeedMultiplier => _maxSpeedMultiplier;
        public bool DebugWaveLogs => _debugWaveLogs;
        public int WaveCount => _waves?.Length ?? 0;

        public WaveSpawnSettings CreateWaveSpawnSettings()
        {
            return new WaveSpawnSettings
            {
                MaxAliveEnemies = _maxAliveEnemies,
                HealthPerWaveStep = _healthPerWaveStep,
                SpeedPerWaveStep = _speedPerWaveStep,
                DamagePerWaveStep = _damagePerWaveStep,
                MaxSpeedMultiplier = _maxSpeedMultiplier,
                DebugWaveLogs = _debugWaveLogs,
            };
        }

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

            if (_maxAliveEnemies < 0)
            {
                _maxAliveEnemies = 0;
            }

            if (_healthPerWaveStep < 0f)
            {
                _healthPerWaveStep = 0f;
            }

            if (_speedPerWaveStep < 0f)
            {
                _speedPerWaveStep = 0f;
            }

            if (_damagePerWaveStep < 0f)
            {
                _damagePerWaveStep = 0f;
            }

            if (_maxSpeedMultiplier < 1f)
            {
                _maxSpeedMultiplier = 1f;
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
