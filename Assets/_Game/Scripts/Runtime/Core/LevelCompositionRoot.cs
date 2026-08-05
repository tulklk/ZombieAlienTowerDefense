using System.Collections.Generic;
using AlienDefense.Audio;
using AlienDefense.Base;
using AlienDefense.Building;
using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.DebugTools;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Towers;
using AlienDefense.UI;
using AlienDefense.Vfx;
using AlienDefense.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Wires up a level's pure C# services from its LevelDefinition and Scene objects.</summary>
    public sealed class LevelCompositionRoot : MonoBehaviour
    {
        [SerializeField]
        private LevelDefinition _levelDefinition;

        [SerializeField]
        [Tooltip("Optional (not present until Phase 2's Player is in the scene).")]
        private PlayerController _player;

        [SerializeField]
        [Tooltip("Optional.")]
        private PlayerAutoAttack _playerAutoAttack;

        [SerializeField]
        private Transform _enemyRuntimeParent;

        [SerializeField]
        private Transform _projectileRuntimeParent;

        [SerializeField]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Optional.")]
        private EnemyDebugSpawner _debugSpawner;

        [SerializeField]
        private WaveController _waveController;

        [SerializeField]
        [Tooltip("Optional.")]
        private WaveDebugControls _waveDebugControls;

        [SerializeField]
        [Tooltip("Optional (Phase 6, before the Building system exists).")]
        private Transform _towerRuntimeParent;

        [SerializeField]
        [Tooltip("Optional.")]
        private TowerDebugSpawner _towerDebugSpawner;

        [SerializeField]
        [Tooltip("Optional (Phase 7, before the Building system exists).")]
        private WorldSelectionController _worldSelectionController;

        [SerializeField]
        [Tooltip("Optional.")]
        private BuildBarPresenter _buildBarPresenter;

        [SerializeField]
        [Tooltip("Optional.")]
        private BuildNodeVisualCoordinator _buildNodeVisualCoordinator;

        [SerializeField]
        [Tooltip("Optional (Phase 8, before Selection/Upgrade/Sell exist).")]
        private TowerDetailsPresenter _towerDetailsPresenter;

        [SerializeField]
        [Tooltip("Optional (Phase 9, before the Game HUD exists).")]
        private GameHUDPresenter _gameHUDPresenter;

        [SerializeField]
        [Tooltip("Optional (Phase 9, before Pause/Victory/Defeat panels exist).")]
        private GameStateUIController _gameStateUIController;

        [SerializeField]
        [Tooltip("Optional (Phase 10, before the Vfx system exists). Pooled VFX instances are parented here.")]
        private Transform _vfxRuntimeParent;

        [SerializeField]
        [Tooltip("Optional. Played at the tower's position when a build finishes.")]
        private VfxDefinition _buildVfxDefinition;

        [SerializeField]
        [Tooltip("Optional. Played at the tower's position when an upgrade finishes.")]
        private VfxDefinition _upgradeVfxDefinition;

        [SerializeField]
        [Tooltip("Optional. Played at the tower's position when it is sold.")]
        private VfxDefinition _sellVfxDefinition;

        [SerializeField]
        [Tooltip("Optional (Phase 10, before the Audio system exists).")]
        private AudioService _audioService;

        [SerializeField]
        [Tooltip("Optional SFX clips. Any left empty simply stay silent.")]
        private AudioClip _buildClip;

        [SerializeField]
        private AudioClip _upgradeClip;

        [SerializeField]
        private AudioClip _sellClip;

        [SerializeField]
        private AudioClip _waveStartClip;

        [SerializeField]
        private AudioClip _victoryClip;

        [SerializeField]
        private AudioClip _defeatClip;

        private EnemyPoolRegistry _enemyPoolRegistry;
        private ProjectilePoolRegistry _projectilePoolRegistry;
        private VfxPoolRegistry _vfxPoolRegistry;
        private BuildLifecycleVfxController _buildLifecycleVfx;
        private GameplayAudioController _gameplayAudio;

        public GameFlowController GameFlow { get; private set; }
        public GameSpeedController GameSpeed { get; private set; }
        public EconomyService Economy { get; private set; }
        public BaseHealthService BaseHealth { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public EnemyFactory EnemySpawner { get; private set; }
        public ProjectileFactory ProjectileSpawner { get; private set; }
        public TowerFactory TowerSpawner { get; private set; }
        public BuildSelectionService BuildSelection { get; private set; }
        public BuildService BuildService { get; private set; }
        public TowerSelectionService TowerSelection { get; private set; }
        public TowerUpgradeService TowerUpgrade { get; private set; }
        public TowerSellService TowerSell { get; private set; }
        public LevelRestartService RestartService { get; private set; }
        public VfxService Vfx { get; private set; }

        private void Awake()
        {
            if (_levelDefinition == null)
            {
                Debug.LogError("[LevelCompositionRoot] No LevelDefinition assigned. Level will not start.", this);
                enabled = false;
                return;
            }

            GameFlow = new GameFlowController();
            GameSpeed = new GameSpeedController(new UnityTimeScaleTarget());
            Economy = new EconomyService(_levelDefinition.StartingResource);
            BaseHealth = new BaseHealthService(_levelDefinition.BaseMaxHealth);

            Application.targetFrameRate = _levelDefinition.TargetFrameRate;

            BaseHealth.Destroyed += HandleBaseDestroyed;
            GameFlow.GameStateChanged += HandleGameStateChanged;

            InitializeVfxSystem();
            InitializeEnemySystem();
            InitializeCombatSystem();
            InitializeTowerSystem();
            InitializeBuildSystem();
            InitializeWaveSystem();
            InitializeAudioSystem();
            InitializeGameFlowUI();
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            GameFlow.BeginPreparingWave();
        }

        private void OnDestroy()
        {
            if (BaseHealth != null)
            {
                BaseHealth.Destroyed -= HandleBaseDestroyed;
            }

            if (GameFlow != null)
            {
                GameFlow.GameStateChanged -= HandleGameStateChanged;
            }

            if (_waveController != null)
            {
                _waveController.WaveStarted -= HandleWaveStarted;
                _waveController.WaveCompleted -= HandleWaveCompletedForFlow;
                _waveController.AllWavesCompleted -= HandleAllWavesCompleted;
            }

            _buildLifecycleVfx?.Unsubscribe();
            _gameplayAudio?.Unsubscribe();

            Enemies?.Clear();
            _enemyPoolRegistry?.Clear();
            _projectilePoolRegistry?.Clear();
            _vfxPoolRegistry?.Clear();
        }

        private void InitializeVfxSystem()
        {
            if (_vfxRuntimeParent == null)
            {
                return;
            }

            _vfxPoolRegistry = new VfxPoolRegistry(_vfxRuntimeParent);
            Vfx = new VfxService(_vfxPoolRegistry);
        }

        private void InitializeEnemySystem()
        {
            if (_enemyRuntimeParent == null)
            {
                Debug.LogError("[LevelCompositionRoot] No enemy runtime parent assigned; Enemy system will not be available.", this);
                return;
            }

            Enemies = new EnemyRegistry();
            _enemyPoolRegistry = new EnemyPoolRegistry(_enemyRuntimeParent);
            EnemySpawner = new EnemyFactory(_enemyPoolRegistry, Enemies, Economy, BaseHealth, _cameraTransform, Vfx);

            if (_debugSpawner != null)
            {
                _debugSpawner.Initialize(EnemySpawner);
            }
        }

        private void InitializeCombatSystem()
        {
            if (_projectileRuntimeParent == null)
            {
                Debug.LogError("[LevelCompositionRoot] No projectile runtime parent assigned; Combat system will not be available.", this);
                return;
            }

            _projectilePoolRegistry = new ProjectilePoolRegistry(_projectileRuntimeParent);
            ProjectileSpawner = new ProjectileFactory(_projectilePoolRegistry, Vfx);

            if (_playerAutoAttack == null || _player == null || Enemies == null)
            {
                return;
            }

            _playerAutoAttack.Initialize(_player.Definition, Enemies, ProjectileSpawner);
        }

        private void InitializeTowerSystem()
        {
            if (_towerRuntimeParent == null)
            {
                return;
            }

            if (ProjectileSpawner == null || Enemies == null)
            {
                return;
            }

            TowerSpawner = new TowerFactory(_towerRuntimeParent, Enemies, ProjectileSpawner, GameFlow, Vfx);

            if (_towerDebugSpawner != null)
            {
                _towerDebugSpawner.Initialize(TowerSpawner);
            }
        }

        private void InitializeBuildSystem()
        {
            BuildSelection = new BuildSelectionService();

            if (TowerSpawner == null)
            {
                Debug.LogError("[LevelCompositionRoot] Tower system unavailable; Build system will not be available.", this);
                return;
            }

            BuildService = new BuildService(BuildSelection, Economy, TowerSpawner, GameFlow);

            TowerSelection = new TowerSelectionService();
            TowerUpgrade = new TowerUpgradeService(Economy, GameFlow);
            TowerSell = new TowerSellService(Economy, GameFlow, TowerSpawner, TowerSelection);

            _buildLifecycleVfx = new BuildLifecycleVfxController(Vfx, _buildVfxDefinition, _upgradeVfxDefinition, _sellVfxDefinition);
            _buildLifecycleVfx.Initialize(BuildService, TowerUpgrade, TowerSell);

            if (_worldSelectionController != null)
            {
                _worldSelectionController.Initialize(BuildService, TowerSelection);
            }

            if (_buildBarPresenter != null)
            {
                _buildBarPresenter.Initialize(Economy, BuildSelection);
            }

            if (_buildNodeVisualCoordinator != null)
            {
                _buildNodeVisualCoordinator.Initialize(BuildSelection);
            }

            if (_towerDetailsPresenter != null)
            {
                _towerDetailsPresenter.Initialize(TowerSelection, Economy, TowerUpgrade, TowerSell);
            }
        }

        private void InitializeWaveSystem()
        {
            if (_waveController == null)
            {
                Debug.LogError("[LevelCompositionRoot] No WaveController assigned; Wave system will not be available.", this);
                return;
            }

            if (EnemySpawner == null)
            {
                Debug.LogError("[LevelCompositionRoot] Enemy system unavailable; cannot initialize Wave system.", this);
                return;
            }

            if (_levelDefinition.WaveCount == 0)
            {
                Debug.LogError("[LevelCompositionRoot] LevelDefinition has no waves; Wave system will not start.", this);
                return;
            }

            PrewarmPoolsForWaves();

            var waves = new WaveDefinition[_levelDefinition.WaveCount];
            for (int i = 0; i < waves.Length; i++)
            {
                waves[i] = _levelDefinition.GetWave(i);
            }

            _waveController.Initialize(EnemySpawner, waves, _levelDefinition.PreparationDuration);

            _waveController.WaveStarted += HandleWaveStarted;
            _waveController.WaveCompleted += HandleWaveCompletedForFlow;
            _waveController.AllWavesCompleted += HandleAllWavesCompleted;

            if (_waveDebugControls != null)
            {
                _waveDebugControls.Initialize(_waveController, Enemies);
            }
        }

        private void PrewarmPoolsForWaves()
        {
            var seenDefinitions = new HashSet<EnemyDefinition>();
            for (int w = 0; w < _levelDefinition.WaveCount; w++)
            {
                WaveDefinition wave = _levelDefinition.GetWave(w);
                if (wave == null)
                {
                    continue;
                }

                for (int g = 0; g < wave.SpawnGroupCount; g++)
                {
                    EnemySpawnGroup group = wave.GetSpawnGroup(g);
                    if (group != null && group.IsValid && seenDefinitions.Add(group.EnemyDefinition))
                    {
                        _enemyPoolRegistry.GetOrCreatePool(group.EnemyDefinition);
                    }
                }
            }
        }

        private void InitializeAudioSystem()
        {
            if (_audioService == null)
            {
                return;
            }

            _gameplayAudio = new GameplayAudioController(
                _audioService, _buildClip, _upgradeClip, _sellClip, _waveStartClip, _victoryClip, _defeatClip);
            _gameplayAudio.Initialize(BuildService, TowerUpgrade, TowerSell, _waveController, GameFlow);
        }

        private void InitializeGameFlowUI()
        {
            RestartService = new LevelRestartService(SceneManager.GetActiveScene().name);

            if (_gameHUDPresenter != null)
            {
                _gameHUDPresenter.Initialize(Economy, BaseHealth, GameSpeed, GameFlow);
            }

            if (_gameStateUIController != null)
            {
                _gameStateUIController.Initialize(GameFlow, GameSpeed, RestartService);
            }
        }

        private void HandleBaseDestroyed()
        {
            GameFlow.ReportDefeat();
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Victory || current == GameState.Defeat)
            {
                if (GameSpeed.IsPaused)
                {
                    GameSpeed.Resume();
                }

                GameSpeed.Lock();

                if (_waveController != null)
                {
                    _waveController.StopWaves();
                }

                DespawnAllEnemies();
            }

            if (_player != null)
            {
                bool movementEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _player.SetMovementEnabled(movementEnabled);

                bool combatEnabled = current == GameState.PlayingWave;
                _player.SetCombatEnabled(combatEnabled);
            }

            if (_worldSelectionController != null)
            {
                bool buildInputEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _worldSelectionController.SetInputEnabled(buildInputEnabled);
            }
        }

        private void DespawnAllEnemies()
        {
            if (Enemies == null)
            {
                return;
            }

            while (Enemies.Count > 0)
            {
                EnemyController enemy = Enemies.GetAt(0);
                enemy.ForceResolve(EnemyResolveReason.LevelEnded);
            }
        }

        private void HandleWaveStarted(int waveNumber, int totalWaves)
        {
            GameFlow.BeginPlayingWave();
        }

        private void HandleWaveCompletedForFlow(int waveNumber)
        {
            if (waveNumber < _waveController.TotalWaveCount)
            {
                GameFlow.BeginPreparingWave();
            }
        }

        private void HandleAllWavesCompleted()
        {
            GameFlow.ReportVictory();
        }
    }
}
