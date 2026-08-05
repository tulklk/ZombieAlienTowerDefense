using AlienDefense.Combat;
using UnityEngine;

namespace AlienDefense.Data
{
    /// <summary>Config-only description of the player's UFO: stats and movement tuning.</summary>
    [CreateAssetMenu(fileName = "PlayerDefinition", menuName = "AlienDefense/Player/Player Definition")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        private string _id = "ufo_player";

        [SerializeField]
        private string _displayName = "UFO";

        [Header("Health")]
        [SerializeField, Min(1)]
        private int _maxHealth = 150;

        [Header("Movement")]
        [SerializeField, Min(0.01f)]
        private float _moveSpeed = 5f;

        [SerializeField, Min(0f)]
        private float _rotationSpeed = 540f;

        [SerializeField, Min(0f)]
        private float _acceleration = 25f;

        [SerializeField, Min(0f)]
        private float _deceleration = 30f;

        [SerializeField, Range(0f, 0.9f)]
        private float _inputDeadZone = 0.15f;

        [SerializeField, Min(0f)]
        private float _hoverHeight = 1.5f;

        [Header("Attack (data only until Combat phase)")]
        [SerializeField, Min(0.1f)]
        private float _attackRange = 5f;

        [SerializeField, Min(0)]
        private int _attackDamage = 12;

        [SerializeField, Min(0.01f)]
        private float _attacksPerSecond = 1.5f;

        [SerializeField]
        private ProjectileDefinition _projectileDefinition;

        [Header("Resource Collection")]
        [SerializeField, Min(0f)]
        private float _collectionRadius = 2.5f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float RotationSpeed => _rotationSpeed;
        public float Acceleration => _acceleration;
        public float Deceleration => _deceleration;
        public float InputDeadZone => _inputDeadZone;
        public float HoverHeight => _hoverHeight;
        public float AttackRange => _attackRange;
        public int AttackDamage => _attackDamage;
        public float AttacksPerSecond => _attacksPerSecond;
        public ProjectileDefinition ProjectileDefinition => _projectileDefinition;
        public float CollectionRadius => _collectionRadius;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogError($"[PlayerDefinition] '{name}' has an empty Id.", this);
            }

            if (_maxHealth < 1)
            {
                _maxHealth = 1;
            }

            if (_moveSpeed <= 0f)
            {
                _moveSpeed = 0.01f;
            }

            if (_rotationSpeed < 0f)
            {
                _rotationSpeed = 0f;
            }

            if (_attackRange <= 0f)
            {
                _attackRange = 0.1f;
            }

            if (_attacksPerSecond <= 0f)
            {
                _attacksPerSecond = 0.01f;
            }

            if (_projectileDefinition == null)
            {
                Debug.LogError($"[PlayerDefinition] '{name}' has no Projectile Definition assigned; auto attack will not fire.", this);
            }
        }
    }
}
