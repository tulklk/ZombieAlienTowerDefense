using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Config-only description of an enemy type: stats, pool sizing, prefab reference.</summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "AlienDefense/Enemies/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _id = "enemy_normal";

        [SerializeField]
        private string _displayName = "Enemy";

        [SerializeField]
        private Sprite _icon;

        [Header("Prefab")]
        [SerializeField]
        private EnemyController _prefab;

        [Header("Stats")]
        [SerializeField, Min(1f)]
        private float _maxHealth = 100f;

        [SerializeField, Min(0.01f)]
        private float _moveSpeed = 1.8f;

        [SerializeField, Min(0f)]
        private float _rotationSpeed = 360f;

        [SerializeField, Min(0)]
        private int _rewardResource = 10;

        [SerializeField, Min(1)]
        private int _baseDamage = 1;

        [SerializeField, Min(0.01f)]
        private float _arrivalThreshold = 0.1f;

        [SerializeField, Min(0f)]
        private float _healthBarHeightOffset = 2f;

        [Header("Tractor Beam")]
        [SerializeField]
        private bool _canBeTractorCaptured = true;

        [SerializeField, Min(0.01f)]
        [Tooltip("Divides the beam's base pull speed. 1 = normal, >1 = resists (pulled slower), <1 = pulled faster.")]
        private float _tractorResistance = 1f;

        [Header("VFX (optional)")]
        [SerializeField]
        private VfxDefinition _defeatedVfxDefinition;

        [Header("Pool")]
        [SerializeField, Min(0)]
        private int _poolPrewarmCount = 10;

        [SerializeField, Min(0)]
        private int _poolDefaultCapacity = 20;

        [SerializeField, Min(0)]
        private int _poolMaximumSize = 100;

        public string Id => _id;
        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public EnemyController Prefab => _prefab;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float RotationSpeed => _rotationSpeed;
        public int RewardResource => _rewardResource;
        public int BaseDamage => _baseDamage;
        public float ArrivalThreshold => _arrivalThreshold;
        public float HealthBarHeightOffset => _healthBarHeightOffset;
        public bool CanBeTractorCaptured => _canBeTractorCaptured;
        public float TractorResistance => _tractorResistance;
        public VfxDefinition DefeatedVfxDefinition => _defeatedVfxDefinition;
        public int PoolPrewarmCount => _poolPrewarmCount;
        public int PoolDefaultCapacity => _poolDefaultCapacity;
        public int PoolMaximumSize => _poolMaximumSize;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[EnemyDefinition] '{name}' has an empty Id.", this);
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                Debug.LogError($"[EnemyDefinition] '{name}' has an empty Display Name.", this);
            }

            if (_prefab == null)
            {
                Debug.LogError($"[EnemyDefinition] '{name}' has no Prefab assigned.", this);
            }

            if (_maxHealth < 1f)
            {
                _maxHealth = 1f;
            }

            if (_moveSpeed <= 0f)
            {
                _moveSpeed = 0.01f;
            }

            if (_rotationSpeed < 0f)
            {
                _rotationSpeed = 0f;
            }

            if (_rewardResource < 0)
            {
                _rewardResource = 0;
            }

            if (_baseDamage < 1)
            {
                _baseDamage = 1;
            }

            if (_arrivalThreshold <= 0f)
            {
                _arrivalThreshold = 0.05f;
            }

            if (_tractorResistance <= 0f)
            {
                Debug.LogError($"[EnemyDefinition] '{name}': Tractor Resistance must be > 0, clamped to 0.01.", this);
                _tractorResistance = 0.01f;
            }

            if (_poolPrewarmCount < 0)
            {
                _poolPrewarmCount = 0;
            }

            if (_poolDefaultCapacity < _poolPrewarmCount)
            {
                Debug.LogError($"[EnemyDefinition] '{name}': Pool Default Capacity ({_poolDefaultCapacity}) must be >= Pool Prewarm Count ({_poolPrewarmCount}).", this);
            }

            if (_poolMaximumSize < _poolDefaultCapacity)
            {
                Debug.LogError($"[EnemyDefinition] '{name}': Pool Maximum Size ({_poolMaximumSize}) must be >= Pool Default Capacity ({_poolDefaultCapacity}).", this);
            }
        }
    }
}
