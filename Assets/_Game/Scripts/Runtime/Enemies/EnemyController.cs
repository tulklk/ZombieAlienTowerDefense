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
        private int _scaledBaseDamage = 1;

        private bool _isResolved;
        private bool _isCaptured;
        private bool _cinematicHold;
        private bool _releaseDeferredByHold;
        private bool _isCombatActive = true;
        private Animator _animator;
        private EnemyShieldBarView _shieldBarView;
        private float _animatorSpeedBeforeFreeze = 1f;
        private int _generation;
        private Vector3 _baseLocalScale = Vector3.one;

        public EnemyDefinition Definition => _definition;

        /// <summary>Normal, MiniBoss or FinalBoss for this spawn (see SetRank). Reset to Normal on every spawn.</summary>
        public EnemyRank Rank { get; private set; }
        public Transform TargetPoint => _targetPoint;
        public EnemyHealth Health => _health;
        public EnemyMovement Movement => _movement;
        public int ScaledBaseDamage => _scaledBaseDamage;

        public bool IsTargetable => !_isResolved && !_isCaptured && _isCombatActive && _health != null && _health.IsDamageable;

        /// <summary>False while this enemy is part of a boss-intro group that has been spawned for the camera but not
        /// released yet: it stands still, cannot be targeted, damaged or tractor-captured.</summary>
        public bool IsCombatActive => _isCombatActive;
        public int Generation => _generation;
        public Transform AimPoint => _targetPoint != null ? _targetPoint : transform;
        public IDamageable Damageable => _health;
        public IStatusApplicable StatusController => _statusController;
        public BossController BossController => _bossController;

        /// <summary>True only while this enemy is eligible to start a fresh tractor beam capture. Types whose
        /// definition says so (heavy ones, bosses) and every mini boss can only be brought down by the towers.</summary>
        public bool IsCapturable =>
            !_isResolved && !_isCaptured && _isCombatActive && Rank != EnemyRank.MiniBoss &&
            _definition != null && _definition.CanBeTractorCaptured &&
            _health != null && _health.IsDamageable;

        /// <summary>Fired once per spawn instance when this enemy resolves, before it returns to the pool.</summary>
        public event Action<EnemyController, EnemyResolveReason> Resolved;

        /// <summary>Fires for every resolved enemy (including pooled). Career stats and other app-scope listeners
        /// subscribe here instead of attaching to each instance.</summary>
        public static event Action<EnemyController, EnemyResolveReason> AnyResolved;

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
            _animator = GetComponentInChildren<Animator>();
            // The shield bar is not a serialized reference on the Enemy_* prefabs; look it up once (pooled enemies
            // keep it for life) so its billboard gets the camera like the health bar does.
            _shieldBarView = GetComponentInChildren<EnemyShieldBarView>(true);
            _baseLocalScale = transform.localScale;
        }

        /// <summary>Marks this spawn as a mini boss / final boss (WaveController, right after spawning it).</summary>
        public void SetRank(EnemyRank rank)
        {
            Rank = rank;
            if (_healthBarView != null)
            {
                _healthBarView.SetBossStyle(rank == EnemyRank.MiniBoss);
            }
        }

        /// <summary>Splits a mini boss's health bar into this many layers ("x5"). Call after SetRank.</summary>
        public void SetHealthBarLayers(int layers)
        {
            if (_healthBarView != null)
            {
                _healthBarView.SetBossStyle(Rank == EnemyRank.MiniBoss, layers);
            }
        }

        /// <summary>Holds (false) or releases (true) this enemy for a boss intro: while held it keeps its place and
        /// its pose (the walk cycle slowed to <paramref name="heldAnimatorSpeed"/> of normal so it reads as an idle
        /// shuffle), and is excluded from every targeting, damage and capture path. Releasing restores all of it.</summary>
        public void SetCombatActive(bool active, float heldAnimatorSpeed = 0.3f)
        {
            if (_isCombatActive == active || _isResolved)
            {
                return;
            }

            _isCombatActive = active;
            _movement.SetPaused(!active);
            _health.SetEncounterImmune(!active);

            if (_animator != null)
            {
                if (!active)
                {
                    _animatorSpeedBeforeFreeze = _animator.speed;
                    _animator.speed = _animatorSpeedBeforeFreeze * Mathf.Clamp01(heldAnimatorSpeed);
                }
                else
                {
                    _animator.speed = _animatorSpeedBeforeFreeze;
                }
            }
        }

        public void Initialize(
            EnemyDefinition definition,
            EnemyPath3D path,
            EconomyService economy,
            BaseHealthService baseHealth,
            EnemyRegistry registry,
            Action<EnemyController> releaseToPool,
            Transform cameraTransform,
            EnemySpawnModifiers modifiers = default)
        {
            if (modifiers.HealthMultiplier <= 0f
                && modifiers.SpeedMultiplier <= 0f
                && modifiers.DamageMultiplier <= 0f)
            {
                modifiers = EnemySpawnModifiers.Identity;
            }

            _definition = definition;
            _economy = economy;
            _baseHealth = baseHealth;
            _registry = registry;
            _releaseToPool = releaseToPool;
            RestoreCombatActive();
            _isResolved = false;
            _isCaptured = false;
            _cinematicHold = false;
            _releaseDeferredByHold = false;
            _generation++;
            Rank = EnemyRank.Normal;
            if (_healthBarView != null)
            {
                _healthBarView.SetBossStyle(false);
            }

            // Set on every spawn (not only when != 1) so a pooled instance never keeps a previous mini-boss size.
            float scale = modifiers.ScaleMultiplier > 0f ? modifiers.ScaleMultiplier : 1f;
            transform.localScale = _baseLocalScale * scale;

            float health = definition.MaxHealth * modifiers.HealthMultiplier;
            float moveSpeed = definition.MoveSpeed * modifiers.SpeedMultiplier;
            _scaledBaseDamage = Mathf.Max(1, Mathf.RoundToInt(definition.BaseDamage * modifiers.DamageMultiplier));

            _health.Initialize(health);
            _health.SetDefenseAndShield(_defense, _shield);
            _movement.Initialize(path, moveSpeed, definition.RotationSpeed, definition.ArrivalThreshold);

            _defense?.ResetState();
            _shield?.ResetState();
            _statusController?.Clear();
            _bossController?.ResetState();
            _captureController?.ResetState();

            if (_healthBarView != null)
            {
                _healthBarView.Initialize(cameraTransform);
            }

            if (_shieldBarView != null)
            {
                _shieldBarView.Initialize(cameraTransform);
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

        /// <summary>Keeps this instance out of the pool after it dies so something else can keep showing it - the
        /// victory cinematic orbits the boss's body long after EnemyDeathVisual's hold would have recycled it.
        /// The enemy is already resolved by then (unregistered, untargetable, no rewards left to give), so this
        /// only postpones the pool release. Returns false when it is already back in the pool, so the caller can
        /// fall back instead of animating around a recycled object.</summary>
        public bool TryHoldForCinematic()
        {
            if (!gameObject.activeSelf)
            {
                return false;
            }

            _cinematicHold = true;
            return true;
        }

        /// <summary>Ends a <see cref="TryHoldForCinematic"/> hold and performs the pool release that was waiting
        /// on it. Safe to call when no hold is active.</summary>
        public void ReleaseCinematicHold()
        {
            if (!_cinematicHold)
            {
                return;
            }

            _cinematicHold = false;
            if (_releaseDeferredByHold)
            {
                _releaseDeferredByHold = false;
                _releaseToPool?.Invoke(this);
            }
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
            _scaledBaseDamage = 1;
            RestoreCombatActive();
        }

        private void RestoreCombatActive()
        {
            if (_isCombatActive)
            {
                return;
            }

            _isCombatActive = true;
            _movement.SetPaused(false);
            _health.SetEncounterImmune(false);
            if (_animator != null)
            {
                _animator.speed = _animatorSpeedBeforeFreeze;
            }
        }

        private void HandleDied()
        {
            Resolve(EnemyResolveReason.Defeated);
        }

        /// <summary>Walking -> reached the base -> one hit of BaseDamage -> straight back to the pool. No attack
        /// animation and no standing at the base: Resolve(ReachedBase) deals the damage exactly once (its
        /// _isResolved guard makes a second arrival a no-op) and fires Resolved, which is what frees this enemy's
        /// slot in the wave's alive count so the spawner can continue.</summary>
        private void HandleReachedBase()
        {
            if (_isResolved)
            {
                return;
            }

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
                    _baseHealth?.TakeDamage(_scaledBaseDamage);
                    break;

                // Captured (UFO tractor beam) intentionally falls through to no-op: capturing an enemy only
                // removes it from the battlefield. See EnemyResolutionPolicy — reward/XP come exclusively from
                // an EnergyPickup a Tower kill (Defeated) drops, actually being tractor-beamed into the UFO.
            }

            _registry?.Unregister(this);
            Resolved?.Invoke(this, reason);
            AnyResolved?.Invoke(this, reason);

            if (reason == EnemyResolveReason.Defeated && _deathVisual != null)
            {
                _deathVisual.PlayDeath();
                StartCoroutine(ReleaseAfterDeathVisual());
            }
            else
            {
                ReleaseToPool();
            }
        }

        private IEnumerator ReleaseAfterDeathVisual()
        {
            yield return new WaitForSeconds(_deathVisual.Duration);
            ReleaseToPool();
        }

        /// <summary>The single exit back to the pool, so a cinematic hold cannot be bypassed by one of the paths.</summary>
        private void ReleaseToPool()
        {
            if (_cinematicHold)
            {
                _releaseDeferredByHold = true;
                return;
            }

            _releaseToPool?.Invoke(this);
        }
    }
}
