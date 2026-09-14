using System;
using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Sibling component on the Boss prefab: simple two-phase behavior (minion spawning, phase-two speed/resistance).
    /// Never mutates WaveRuntimeTracker directly; minions are spawned through an injected IEnemySpawnCoordinator.</summary>
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField]
        private EnemyController _enemyController;

        [SerializeField]
        private EnemyHealth _health;

        [SerializeField]
        private EnemyMovement _movement;

        [SerializeField]
        [Tooltip("Optional. Used for phase-two temporary resistance.")]
        private EnemyDefense _defense;

        [SerializeField]
        private BossBehaviorDefinition _behavior;

        private IEnemySpawnCoordinator _spawnCoordinator;
        private float _minionTimer;
        private bool _isActive;
        private bool _isPaused;
        private bool _minionsEnabled = true;

        public BossState State { get; private set; } = BossState.PhaseOne;

        public event Action<BossState> PhaseChanged;

        private void Awake()
        {
            if (_enemyController != null)
            {
                _enemyController.Resolved += HandleEnemyResolved;
            }
        }

        /// <summary>Wired externally (LevelCompositionRoot) once, right after WaveController reports this boss spawned.</summary>
        public void Initialize(IEnemySpawnCoordinator spawnCoordinator)
        {
            _spawnCoordinator = spawnCoordinator;
            State = BossState.PhaseOne;
            _minionTimer = _behavior != null ? _behavior.MinionSpawnInterval : 0f;

            _defense?.SetPhysicalDamageReductionPercent(_behavior != null ? _behavior.PhaseOneDamageReductionPercent : 0f);
            _movement?.SetBehaviorSpeedMultiplier(1f);

            _isActive = _behavior != null && _behavior.MinionDefinition != null && _spawnCoordinator != null;
        }

        /// <summary>Called by EnemyController on Initialize/pool reuse so a fresh spawn always starts clean.</summary>
        public void ResetState()
        {
            State = BossState.PhaseOne;
            _isActive = false;
            _isPaused = false;
            _minionsEnabled = true;
            _minionTimer = 0f;
        }

        /// <summary>Freezes phase checks and the minion timer (boss intro). Resuming continues where it left off.</summary>
        public void SetBehaviorPaused(bool paused)
        {
            _isPaused = paused;
        }

        /// <summary>Per-encounter switch for the BossBehaviorDefinition minion bursts. Phase-two speed/armour still apply.</summary>
        public void SetMinionsEnabled(bool enabled)
        {
            _minionsEnabled = enabled;
        }

        private void Update()
        {
            if (!_isActive || _isPaused || _health == null || State == BossState.Defeated)
            {
                return;
            }

            if (State == BossState.PhaseOne && HealthRatio() <= _behavior.PhaseTwoHealthThreshold)
            {
                EnterPhaseTwo();
            }

            if (!_minionsEnabled)
            {
                return;
            }

            _minionTimer -= Time.deltaTime;
            if (_minionTimer <= 0f)
            {
                SpawnMinionBurst();
                _minionTimer = CurrentMinionInterval();
            }
        }

        private float HealthRatio()
        {
            return _health.MaximumHealth > 0f ? _health.CurrentHealth / _health.MaximumHealth : 0f;
        }

        private void EnterPhaseTwo()
        {
            State = BossState.PhaseTwo;
            _movement?.SetBehaviorSpeedMultiplier(_behavior.PhaseTwoSpeedMultiplier);
            _defense?.SetPhysicalDamageReductionPercent(_behavior.PhaseTwoDamageReductionPercent);
            PhaseChanged?.Invoke(State);
        }

        private float CurrentMinionInterval()
        {
            float interval = _behavior.MinionSpawnInterval;
            if (State == BossState.PhaseTwo)
            {
                interval *= _behavior.PhaseTwoMinionIntervalMultiplier;
            }

            return Mathf.Max(0.5f, interval);
        }

        private void SpawnMinionBurst()
        {
            for (int i = 0; i < _behavior.MinionCountPerBurst; i++)
            {
                _spawnCoordinator.SpawnTrackedEnemy(_behavior.MinionDefinition);
            }
        }

        private void HandleEnemyResolved(EnemyController enemy, EnemyResolveReason reason)
        {
            _isActive = false;
            State = BossState.Defeated;
            PhaseChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            if (_enemyController != null)
            {
                _enemyController.Resolved -= HandleEnemyResolved;
            }
        }
    }
}
