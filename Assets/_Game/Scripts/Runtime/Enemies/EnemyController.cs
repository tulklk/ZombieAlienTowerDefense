using System;
using AlienDefense.Base;
using AlienDefense.Economy;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Coordinates one enemy's lifecycle: init, resolve (defeated/reached base), pool return.</summary>
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement))]
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField]
        private EnemyHealth _health;

        [SerializeField]
        private EnemyMovement _movement;

        [SerializeField]
        private Transform _targetPoint;

        [SerializeField]
        [Tooltip("Optional.")]
        private EnemyHealthBarView _healthBarView;

        private EnemyDefinition _definition;
        private EconomyService _economy;
        private BaseHealthService _baseHealth;
        private EnemyRegistry _registry;
        private Action<EnemyController> _releaseToPool;

        private bool _isResolved;

        public EnemyDefinition Definition => _definition;
        public Transform TargetPoint => _targetPoint;
        public EnemyHealth Health => _health;
        public EnemyMovement Movement => _movement;

        private void Awake()
        {
            if (_health == null || _movement == null)
            {
                Debug.LogError("[EnemyController] EnemyHealth and EnemyMovement must both be assigned.", this);
                enabled = false;
                return;
            }

            _health.Died += HandleDied;
            _movement.DestinationReached += HandleReachedBase;
        }

        public void Initialize(
            EnemyDefinition definition,
            EnemyPath3D path,
            EconomyService economy,
            BaseHealthService baseHealth,
            EnemyRegistry registry,
            Action<EnemyController> releaseToPool,
            Transform cameraTransform)
        {
            _definition = definition;
            _economy = economy;
            _baseHealth = baseHealth;
            _registry = registry;
            _releaseToPool = releaseToPool;
            _isResolved = false;

            _health.Initialize(definition.MaxHealth);
            _movement.Initialize(path, definition.MoveSpeed, definition.RotationSpeed, definition.ArrivalThreshold);
            _healthBarView?.Initialize(cameraTransform);

            _registry.Register(this);
        }

        public void ApplyDebugDamage(float amount)
        {
            _health.TryApplyDamage(amount);
        }

        /// <summary>Forces resolve for reasons the enemy itself never triggers (e.g. level cleanup).</summary>
        public void ForceResolve(EnemyResolveReason reason)
        {
            Resolve(reason);
        }

        /// <summary>Called by the owning pool when this instance is returned, including prewarm.</summary>
        public void HandleReturnedToPool()
        {
            _movement.StopMovement();
            _health.ResetState();
        }

        private void HandleDied()
        {
            Resolve(EnemyResolveReason.Defeated);
        }

        private void HandleReachedBase()
        {
            Resolve(EnemyResolveReason.ReachedBase);
        }

        private void Resolve(EnemyResolveReason reason)
        {
            if (_isResolved)
            {
                return;
            }

            _isResolved = true;
            _movement.StopMovement();

            switch (reason)
            {
                case EnemyResolveReason.Defeated:
                    _economy?.Add(_definition.RewardResource);
                    break;
                case EnemyResolveReason.ReachedBase:
                    _baseHealth?.TakeDamage(_definition.BaseDamage);
                    break;
            }

            _registry?.Unregister(this);
            _releaseToPool?.Invoke(this);
        }
    }
}
