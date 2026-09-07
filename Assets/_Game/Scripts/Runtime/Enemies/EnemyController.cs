using System;
using System.Collections;
using AlienDefense.Base;
using AlienDefense.Combat;
using AlienDefense.Economy;
using AlienDefense.UI;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Coordinates one enemy's lifecycle: init, resolve (defeated/reached base), pool return.</summary>
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyMovement))]
    public sealed class EnemyController : MonoBehaviour, ICombatTarget
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

        [SerializeField]
        [Tooltip("Optional. Armor: reduces incoming Physical damage.")]
        private EnemyDefense _defense;

        [SerializeField]
        [Tooltip("Optional. Absorbs damage before EnemyHealth, with delayed regen.")]
        private EnemyShield _shield;

        [SerializeField]
        [Tooltip("Optional. Owns Slow/Burn and every other active status effect.")]
        private EnemyStatusController _statusController;

        [SerializeField]
        [Tooltip("Optional. Present only on the Boss prefab.")]
        private BossController _bossController;

        [SerializeField]
        [Tooltip("Optional. Owns Pull/Lift movement while this enemy is being tractor-beam captured.")]
        private EnemyCaptureController _captureController;

        [SerializeField]
        [Tooltip("Optional. Plays a Die animation and delays the pool-release just long enough for it to be " +
            "seen (see its own Duration). Enemies without one release to the pool immediately, unchanged.")]
        private EnemyDeathVisual _deathVisual;

        private EnemyDefinition _definition;
        private EconomyService _economy;
        private BaseHealthService _baseHealth;
        private EnemyRegistry _registry;
        private Action<EnemyController> _releaseToPool;

        private bool _isResolved;
        private bool _isCaptured;
        private int _generation;

        public EnemyDefinition Definition => _definition;
        public Transform TargetPoint => _targetPoint;
        public EnemyHealth Health => _health;
        public EnemyMovement Movement => _movement;

        public bool IsTargetable => !_isResolved && !_isCaptured && _health != null && _health.IsDamageable;
        public int Generation => _generation;
        public Transform AimPoint => _targetPoint != null ? _targetPoint : transform;
        public IDamageable Damageable => _health;
        public IStatusApplicable StatusController => _statusController;
        public BossController BossController => _bossController;

        /// <summary>True only while this enemy is eligible to start a fresh tractor beam capture.</summary>
        public bool IsCapturable =>
            !_isResolved && !_isCaptured &&
            _definition != null && _definition.CanBeTractorCaptured &&
            _health != null && _health.IsDamageable;

        /// <summary>Fired once per spawn instance when this enemy resolves, before it returns to the pool.</summary>
        public event Action<EnemyController, EnemyResolveReason> Resolved;

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
            _isCaptured = false;
            _generation++;

            _health.Initialize(definition.MaxHealth);
            _health.SetDefenseAndShield(_defense, _shield);
            _movement.Initialize(path, definition.MoveSpeed, definition.RotationSpeed, definition.ArrivalThreshold);

            _defense?.ResetState();
            _shield?.ResetState();
            _statusController?.Clear();
            _bossController?.ResetState();
            _captureController?.ResetState();

            if (_healthBarView != null)
            {
                _healthBarView.Initialize(cameraTransform);
            }

            _registry.Register(this);
        }

        public void ApplyDebugDamage(float amount)
        {
            _health.TryApplyDamage(amount);
        }

        /// <summary>Admission into a tractor beam's capture. Fails silently (returns false) if this enemy is not
        /// currently capturable — the beam simply tries a different enemy on its next scan.</summary>
        public bool TryBeginTractorCapture(in TractorCaptureRequest request)
        {
            if (!IsCapturable || _captureController == null)
            {
                return false;
            }

            _isCaptured = true;
            _health.SetCaptureImmune(true);
            _movement.StopMovement();
            _healthBarView?.Hide();
            _captureController.BeginCapture(request, HandleCaptureCompleted);
            return true;
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
            _defense?.ResetState();
            _shield?.ResetState();
            _statusController?.Clear();
            _bossController?.ResetState();
            _captureController?.ResetState();
            _isCaptured = false;
        }

        private void HandleDied()
        {
            Resolve(EnemyResolveReason.Defeated);
        }

        private void HandleReachedBase()
        {
            Resolve(EnemyResolveReason.ReachedBase);
        }

        private void HandleCaptureCompleted()
        {
            Resolve(EnemyResolveReason.Captured);
        }

        private void Resolve(EnemyResolveReason reason)
        {
            if (_isResolved)
            {
                return;
            }

            _isResolved = true;
            _movement.StopMovement();
            _captureController?.Abort();

            switch (reason)
            {
                case EnemyResolveReason.Defeated:
                    _economy?.Add(_definition.RewardResource);
                    break;
                case EnemyResolveReason.ReachedBase:
                    _baseHealth?.TakeDamage(_definition.BaseDamage);
                    break;

                // Captured (UFO tractor beam) intentionally falls through to no-op: capturing an enemy only
                // removes it from the battlefield. See EnemyResolutionPolicy — reward/XP come exclusively from
                // an EnergyPickup a Tower kill (Defeated) drops, actually being tractor-beamed into the UFO.
            }

            _registry?.Unregister(this);
            Resolved?.Invoke(this, reason);

            if (reason == EnemyResolveReason.Defeated && _deathVisual != null)
            {
                _deathVisual.PlayDeath();
                StartCoroutine(ReleaseAfterDeathVisual());
            }
            else
            {
                _releaseToPool?.Invoke(this);
            }
        }

        private IEnumerator ReleaseAfterDeathVisual()
        {
            yield return new WaitForSeconds(_deathVisual.Duration);
            _releaseToPool?.Invoke(this);
        }
    }
}
