using System;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Vfx;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Wires targeting, attack, and visual for one tower instance and applies its per-level stats.</summary>
    public sealed class TowerController : MonoBehaviour
    {
        [SerializeField]
        private TowerTargeting _targeting;

        [SerializeField]
        private TowerAttackController _attack;

        [SerializeField]
        private TowerVisual _visual;

        [SerializeField]
        [Tooltip("Optional.")]
        private RangeIndicator _rangeIndicator;

        private TowerDefinition _definition;
        private GameFlowController _gameFlow;
        private bool _isInitialized;

        public TowerDefinition Definition => _definition;
        public int CurrentLevelIndex { get; private set; }
        public int CurrentLevelNumber => CurrentLevelIndex + 1;
        public bool IsMaxLevel => _definition != null && CurrentLevelIndex >= _definition.LevelCount - 1;
        public TowerStats CurrentStats { get; private set; }
        public int TotalInvestedResource { get; private set; }
        public bool IsSold { get; private set; }
        public object BuildNodeOwner { get; private set; }

        public event Action<int> LevelChanged;

        private void Awake()
        {
            if (_targeting == null || _attack == null || _visual == null)
            {
                Debug.LogError("[TowerController] TowerTargeting, TowerAttackController, and TowerVisual must all be assigned.", this);
                enabled = false;
            }
        }

        /// <param name="startingLevelIndex">0 = build at Level 1 as always. A caller may pass a tower's saved
        /// permanent meta-upgrade level (TowerMetaUpgradeService/PlayerProfileService.GetTowerUpgradeLevel) so a
        /// permanently-upgraded tower type starts higher on its own TowerDefinition._levels curve — no separate
        /// stat-multiplier system needed. Clamped to a valid index regardless of what's passed in.</param>
        public void Initialize(TowerDefinition definition, EnemyRegistry enemyRegistry, ProjectileFactory projectileFactory, AreaDamageResolver areaDamageResolver, GameFlowController gameFlow, int startingLevelIndex = 0)
        {
            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged -= HandleGameStateChanged;
            }

            _definition = definition;
            _gameFlow = gameFlow;
            TotalInvestedResource = 0;
            IsSold = false;

            int clampedStartIndex = Mathf.Clamp(startingLevelIndex, 0, definition.LevelCount - 1);

            ITargetingStrategy targetingStrategy = TargetingStrategyFactory.Create(definition.DefaultTargetingMode);
            IAttackStrategy attackStrategy = AttackStrategyFactory.Create(definition, projectileFactory, areaDamageResolver);

            _targeting.Initialize(enemyRegistry, targetingStrategy, definition.GetLevel(clampedStartIndex).Range);
            _attack.Initialize(attackStrategy, gameObject);

            _isInitialized = true;
            ApplyLevel(clampedStartIndex);

            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged += HandleGameStateChanged;
                _attack.SetAttackEnabled(_gameFlow.CurrentState == GameState.PlayingWave);
            }
        }

        public void SetBuildNodeOwner(object owner)
        {
            BuildNodeOwner = owner;
        }

        /// <summary>Optional. Wires the muzzle flash VFX played on every successful attack.</summary>
        public void SetMuzzleVfx(VfxService vfxService, VfxDefinition muzzleVfx)
        {
            _attack.SetMuzzleVfx(vfxService, muzzleVfx);
        }

        /// <summary>Applies the next level in sequence. Intended caller: TowerUpgradeService only.</summary>
        public bool TryApplyNextLevel()
        {
            return ApplyLevel(CurrentLevelIndex + 1);
        }

        /// <summary>Adds to the running total spent on this tower (build cost + paid upgrades). Intended callers: BuildService, TowerUpgradeService.</summary>
        public void RegisterInvestment(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            TotalInvestedResource += amount;
        }

        /// <summary>Marks this tower as sold, stopping targeting/attack immediately. Intended caller: TowerSellService only.</summary>
        public void MarkSold()
        {
            IsSold = true;
        }

        public void ShowRangeIndicator()
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.Show(CurrentStats.Range);
            }
        }

        public void HideRangeIndicator()
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.Hide();
            }
        }

        public bool ApplyLevel(int levelIndex)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[TowerController] ApplyLevel called before Initialize.", this);
                return false;
            }

            if (levelIndex < 0 || levelIndex >= _definition.LevelCount)
            {
                Debug.LogWarning($"[TowerController] Invalid level index {levelIndex}.", this);
                return false;
            }

            TowerLevelData level = _definition.GetLevel(levelIndex);
            CurrentLevelIndex = levelIndex;
            CurrentStats = TowerStats.FromLevel(level);

            _targeting.SetRange(CurrentStats.Range);
            _attack.ApplyStats(CurrentStats.Damage, CurrentStats.AttacksPerSecond);
            _visual.SetRotationSpeed(CurrentStats.TurretRotationSpeed);

            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetRange(CurrentStats.Range);
            }

            LevelChanged?.Invoke(levelIndex);
            return true;
        }

        private void Update()
        {
            if (!_isInitialized || IsSold)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _targeting.Tick(deltaTime);
            _attack.Tick(deltaTime, _targeting.CurrentTarget);
            _visual.Tick(deltaTime, _targeting.CurrentTarget);
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            _attack.SetAttackEnabled(current == GameState.PlayingWave);
        }

        private void OnDestroy()
        {
            if (_gameFlow != null)
            {
                _gameFlow.GameStateChanged -= HandleGameStateChanged;
            }
        }
    }
}
