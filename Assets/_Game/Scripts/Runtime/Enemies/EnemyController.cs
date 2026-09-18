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

        [SerializeField]
        [Tooltip("Optional. On reaching PlayerBase, plays a looping Attack animation and deals BaseDamage " +
            "repeatedly - once per its own Duration - instead of vanishing after a single hit. Stays alive and " +
            "fully targetable the whole time, so a Tower can still kill it mid-attack; otherwise it keeps " +
            "attacking until BaseHealthService.IsDestroyed. Enemies without one keep resolving ReachedBase " +
            "instantly with a single hit, unchanged.")]
        private EnemyBaseAttackVisual _baseAttackVisual;

        private EnemyDefinition _definition;
        private EconomyService _economy;
        private BaseHealthService _baseHealth;
        private EnemyRegistry _registry;
        private Action<EnemyController> _releaseToPool;
        private int _scaledBaseDamage = 1;

        private bool _isResolved;
        private bool _isCaptured;
        private bool _isCombatActive = true;
        private Animator _animator;
        private float _animatorSpeedBeforeFreeze = 1f;
        private int _generation;

        public EnemyDefinition Definition => _definition;
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

        /// <summary>True only while this enemy is eligible to start a fresh tractor beam capture.</summary>
        public bool IsCapturable =>
            !_isResolved && !_isCaptured && _isCombatActive &&
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
            _generation++;

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

        private void HandleReachedBase()
        {
            if (_isResolved)
            {
                return;
            }

            if (_baseAttackVisual != null)
            {
                // Deliberately NOT Resolve(ReachedBase) here: this enemy is meant to stay alive, registered and
                // fully targetable, parked at the base attacking it over and over, until either a Tower kills it
                // (the normal Defeated path still applies at any point, via HandleDied -> Resolve) or the level
                // cleans it up (LevelEnded, via LevelCompositionRoot.DespawnAllEnemies -> ForceResolve). Resolved
                // is intentionally NOT fired early either: this zombie is still a live threat sitting on the
                // base, so it should keep counting against its wave's ActiveEnemyCount (see WaveRuntimeTracker)
                // for as long as it's parked there - a wave with an unresolved base-attacker at the end of it is
                // not actually "done", and neither is the level (HandleAllWavesCompleted/ReportVictory), until a
                // Tower kills it. That's intended pressure, not a bug: leaving a zombie alive at the base is what
                // keeps damaging PlayerBase, so the player has to clear it out to progress.
                _baseAttackVisual.PlayAttack();
                StartCoroutine(AttackBaseRepeatedly());
            }
            else
            {
                Resolve(EnemyResolveReason.ReachedBase);
            }
        }

        /// <summary>Deals BaseDamage on a loop, once per EnemyBaseAttackVisual.Duration (one Attack clip cycle),
        /// for as long as this enemy is still alive and the base still has health. Captures _generation up front
        /// so a stale coroutine from a PREVIOUS life of this pooled instance can never damage the base on behalf
        /// of a instance that's since been re-initialized for a different spawn (same safety pattern as
        /// CaptureHandle/EnergyHandle/PropHandle elsewhere in this project) - and stops immediately once this
        /// instance actually resolves (Tower kill mid-attack) or the base is destroyed.</summary>
        private IEnumerator AttackBaseRepeatedly()
        {
            int generationAtStart = _generation;
            var wait = new WaitForSeconds(_baseAttackVisual.Duration);

            while (true)
            {
                yield return wait;

                if (_generation != generationAtStart || _isResolved)
                {
                    yield break;
                }

                if (_baseHealth != null && _baseHealth.IsDestroyed)
                {
                    yield break;
                }

                _baseHealth?.TakeDamage(_scaledBaseDamage);
            }
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
