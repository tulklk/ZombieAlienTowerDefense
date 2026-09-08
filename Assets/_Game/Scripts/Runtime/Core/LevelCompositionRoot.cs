using System.Collections.Generic;
using AlienDefense.Audio;
using AlienDefense.Base;
using AlienDefense.Building;
using AlienDefense.Combat;
using AlienDefense.Data;
using AlienDefense.DebugTools;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Pickups;
using AlienDefense.Player;
using AlienDefense.Progression;
using AlienDefense.Save;
using AlienDefense.Towers;
using AlienDefense.UI;
using AlienDefense.Vfx;
using AlienDefense.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.Core
{
    /// <summary>Wires up a level's pure C# services from its LevelDefinition and Scene objects. Which LevelDefinition
    /// to use is resolved from LevelLaunchContext (set by Level Selection) once ApplicationServices arrive; the
    /// serialized field below is an Editor-only fallback for pressing Play directly on this scene.</summary>
    public sealed class LevelCompositionRoot : MonoBehaviour, IApplicationServicesReceiver
    {
        [SerializeField]
        [Tooltip("Editor-only fallback used when this scene is played directly, with no LevelLaunchContext selection (e.g. no Bootstrap/MainMenu/LevelSelection run first). Ignored in release builds.")]
        private LevelDefinition _developmentLevelDefinition;

        [SerializeField]
        [Tooltip("Optional (not present until Phase 2's Player is in the scene).")]
        private PlayerController _player;

        [SerializeField]
        [Tooltip("Optional. The UFO's continuous multi-enemy tractor beam.")]
        private UFOTractorBeamController _tractorBeamController;

        [SerializeField]
        [Tooltip("Optional.")]
        private UFOTractorBeamVisual _tractorBeamVisual;

        [SerializeField]
        [Tooltip("Optional. Exactly the 5 fixed skills (Radius/Speed/Missile/Capacity/Magnet) offered on level-up.")]
        private SkillDefinition[] _skillCatalog;

        [SerializeField]
        [Tooltip("Optional. Reused for the Missile skill's auto-fired shot (only its speed/lifetime/hit-distance/VFX - damage comes from the current rank instead).")]
        private ProjectileDefinition _missileProjectileDefinition;

        [SerializeField]
        [Tooltip("Optional.")]
        private PlayerSkillEffectApplier _playerSkillEffectApplier;

        [SerializeField]
        [Tooltip("Optional.")]
        private PlayerMissileController _playerMissileController;

        [SerializeField]
        [Tooltip("Optional. The 'pick 1 of 3' level-up popup.")]
        private SkillChoicePresenter _skillChoicePresenter;

        [SerializeField]
        [Tooltip("Optional. Also receives PlayerLevelProgression once it exists (see Initialize's 2nd call) - its own WaveController wiring still happens in its own Awake.")]
        private WaveHUDPresenter _waveHUDPresenter;

        [SerializeField]
        [Tooltip("Optional. Dedicated looping AudioSource for the beam hum; never shared with music.")]
        private AudioSource _tractorBeamLoopSource;

        [SerializeField]
        private AudioClip _tractorBeamLoopClip;

        [SerializeField]
        private AudioClip _tractorBeamCaptureClip;

        [SerializeField]
        private Transform _enemyRuntimeParent;

        [SerializeField]
        [Tooltip("Optional. Enables the Energy Pickup loop (Tower kill → drop → UFO collects → Wallet/XP). " +
            "Both must be set for it to activate; either left empty means Defeated kills simply grant no Energy Pickup.")]
        private EnergyPickupController _energyPickupPrefab;

        [SerializeField]
        [Tooltip("Optional. Pooled EnergyPickup instances are parented here.")]
        private Transform _energyPickupRuntimeParent;

        [SerializeField, Min(1)]
        private int _energyPickupPoolDefaultCapacity = 16;

        [SerializeField, Min(1)]
        private int _energyPickupPoolMaxSize = 64;

        [SerializeField]
        [Tooltip("Every TractorAbsorbableProp already placed in the scene is collected once here at level start " +
            "— see TractorAbsorbablePropRegistry's doc comment for why. Uncheck to disable Environment Prop " +
            "absorption entirely for this level.")]
        private bool _environmentPropAbsorptionEnabled = true;

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
        [Tooltip("Optional. Shown only while a Boss is alive.")]
        private BossHealthBarPresenter _bossHealthBarPresenter;

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
        [Tooltip("Optional. Flying the Player onto a BuildNode and holding position for a few seconds builds/upgrades it via Energy Ball, see EnergyTowerTransactionService.")]
        private PlayerBuildNodeProximityController _playerBuildNodeProximity;

        [SerializeField]
        [Tooltip("Optional. The 'Choose Tower' popup opened by _playerBuildNodeProximity after a successful build channel.")]
        private TowerChoicePresenter _towerChoicePresenter;

        [SerializeField]
        [Tooltip("Optional. Every buildable TowerDefinition - offered by _towerChoicePresenter.")]
        private TowerDefinition[] _towerCatalog;

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
        private TractorBeamAudioController _tractorBeamAudio;
        private ApplicationServices _applicationServices;
        private LevelDefinition _resolvedLevelDefinition;
        private string _resolvedLevelId;

        private EnergyPickupPool _energyPickupPool;
        private EnergyPickupRegistry _energyPickupRegistry;
        private EnergyPickupFactory _energyPickupSpawner;
        private EnergyDropService _energyDropService;
        private TractorAbsorbablePropRegistry _environmentPropRegistry;

        public GameFlowController GameFlow { get; private set; }
        public GameSpeedController GameSpeed { get; private set; }
        public EconomyService Economy { get; private set; }
        public BaseHealthService BaseHealth { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public EnemyFactory EnemySpawner { get; private set; }

        /// <summary>Separate from Economy (which still funds Tower build/upgrade). Increases ONLY through
        /// EnergyCollection — see AlienDefense.Enemies.EnemyResolutionPolicy for the full reward rule.</summary>
        public EnergyWalletService EnergyWallet { get; private set; }

        public PlayerLevelProgressionService PlayerLevelProgression { get; private set; }
        public EnergyCollectionService EnergyCollection { get; private set; }
        public PlayerSkillService PlayerSkills { get; private set; }
        public ProjectileFactory ProjectileSpawner { get; private set; }
        public TowerFactory TowerSpawner { get; private set; }
        public AreaDamageResolver AreaDamage { get; private set; }
        public BuildSelectionService BuildSelection { get; private set; }
        public BuildService BuildService { get; private set; }
        public EnergyTowerTransactionService EnergyTransactions { get; private set; }
        public TowerSelectionService TowerSelection { get; private set; }
        public TowerUpgradeService TowerUpgrade { get; private set; }
        public TowerSellService TowerSell { get; private set; }
        public LevelRestartService RestartService { get; private set; }
        public VfxService Vfx { get; private set; }

        /// <summary>Pushed by ApplicationRuntime after this scene's Awake phase but before Start,
        /// so ResolveLevelDefinition() (called from Start) always sees it in time.</summary>
        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _applicationServices = services;
        }

        private void Start()
        {
            LevelDefinition levelDefinition = ResolveLevelDefinition();
            if (levelDefinition == null)
            {
                Debug.LogError("[LevelCompositionRoot] No LevelDefinition could be resolved. Level will not start.", this);
                enabled = false;
                return;
            }

            BuildLevel(levelDefinition);
            GameFlow.BeginPreparingWave();
        }

        private LevelDefinition ResolveLevelDefinition()
        {
            if (_applicationServices != null && _applicationServices.LevelLaunchContext.HasSelection)
            {
                string levelId = _applicationServices.LevelLaunchContext.SelectedLevelId;
                if (_applicationServices.LevelCatalog != null && _applicationServices.LevelCatalog.TryResolve(levelId, out LevelCatalogEntry entry))
                {
                    _resolvedLevelId = entry.LevelId;
                    return entry.LevelDefinition;
                }

                Debug.LogError($"[LevelCompositionRoot] LevelLaunchContext selected '{levelId}' but the LevelCatalog could not resolve it. Not falling back silently.", this);
                return null;
            }

#if UNITY_EDITOR
            if (_developmentLevelDefinition != null)
            {
                Debug.LogWarning("[LevelCompositionRoot] No LevelLaunchContext selection found; using the Development Level Definition fallback (Editor-only, stripped from release builds).", this);
                _resolvedLevelId = _developmentLevelDefinition.LevelId;
                return _developmentLevelDefinition;
            }
#endif

            return null;
        }

        private void BuildLevel(LevelDefinition levelDefinition)
        {
            _resolvedLevelDefinition = levelDefinition;

            GameFlow = new GameFlowController();
            GameSpeed = new GameSpeedController(new UnityTimeScaleTarget());
            Economy = new EconomyService(levelDefinition.StartingResource);
            BaseHealth = new BaseHealthService(levelDefinition.BaseMaxHealth);

            // Separate currency/progression from Economy — see EnergyWalletService's doc comment. Constructed
            // here (not inside InitializeEnergyEconomySystem) so they exist even if the EnergyPickup prefab/
            // runtime parent were left unassigned; only the pickup-spawning half of the loop is optional.
            EnergyWallet = new EnergyWalletService();
            PlayerLevelProgression = new PlayerLevelProgressionService();
            EnergyCollection = new EnergyCollectionService(EnergyWallet, PlayerLevelProgression);
            PlayerSkills = new PlayerSkillService(_skillCatalog);

            Application.targetFrameRate = levelDefinition.TargetFrameRate;

            BaseHealth.Destroyed += HandleBaseDestroyed;
            GameFlow.GameStateChanged += HandleGameStateChanged;

            InitializeVfxSystem();
            InitializeEnergyEconomySystem();
            InitializeEnvironmentPropSystem();
            InitializeEnemySystem();
            InitializeCombatSystem();
            InitializeTractorBeamSystem();
            InitializeTowerSystem();
            InitializeBuildSystem();
            InitializeWaveSystem();
            InitializeAudioSystem();
            InitializeGameFlowUI();
        }

        private void OnDestroy()
        {
            if (BaseHealth != null)
            {
                BaseHealth.Destroyed -= HandleBaseDestroyed;
            }

            if (EnergyWallet != null)
            {
                EnergyWallet.EnergyChanged -= HandleEnergyChangedForBuildBadges;
                EnergyWallet.MaxEnergyChanged -= HandleMaxEnergyChangedForBuildBadges;
            }

            if (EnergyTransactions != null)
            {
                EnergyTransactions.BuildCompleted -= HandleBuildCompletedForBuildBadges;
                EnergyTransactions.TowerUpgraded -= HandleTowerUpgradedForBuildBadges;
            }

            if (_tractorBeamController != null)
            {
                _tractorBeamController.PropAbsorbed -= HandlePropAbsorbedForExperience;
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
                _waveController.BossSpawned -= HandleBossSpawned;
            }

            _buildLifecycleVfx?.Unsubscribe();
            _gameplayAudio?.Unsubscribe();
            _tractorBeamAudio?.Unsubscribe();
            _tractorBeamVisual?.Unsubscribe();
            _applicationServices?.SettingsService?.DetachAudioService(_audioService);

            Enemies?.Clear();
            _enemyPoolRegistry?.Clear();
            _projectilePoolRegistry?.Clear();
            _vfxPoolRegistry?.Clear();
            _energyPickupRegistry?.Clear();
            _energyPickupPool?.Clear();
            _environmentPropRegistry?.Clear();
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

        /// <summary>Wallet/Progression themselves are always constructed (see BuildLevel) — this only wires the
        /// EnergyPickup spawn/collect loop that feeds them. Runs before InitializeEnemySystem (EnemyFactory needs
        /// EnergyDropService) and InitializeTractorBeamSystem (the beam needs EnergyPickupRegistry).</summary>
        private void InitializeEnergyEconomySystem()
        {
            if (_energyPickupPrefab == null || _energyPickupRuntimeParent == null)
            {
                Debug.LogWarning("[LevelCompositionRoot] No Energy Pickup prefab/runtime parent assigned; " +
                    "Tower kills will not drop Energy Pickups (EnergyWallet/PlayerLevelProgression still exist, " +
                    "just nothing feeds them). Assign both to enable the loop.", this);
                return;
            }

            _energyPickupPool = new EnergyPickupPool(
                _energyPickupPrefab, _energyPickupRuntimeParent, _energyPickupPoolDefaultCapacity, _energyPickupPoolMaxSize,
                collectionChecks: Debug.isDebugBuild);
            _energyPickupRegistry = new EnergyPickupRegistry();
            _energyPickupSpawner = new EnergyPickupFactory(_energyPickupPool, _energyPickupRegistry, EnergyCollection);
            _energyDropService = new EnergyDropService(_energyPickupSpawner);
        }

        /// <summary>One-time collection of every TractorAbsorbableProp already placed in the scene — see
        /// TractorAbsorbablePropRegistry's doc comment for why this is a single FindObjectsByType at level start
        /// rather than per-prop OnEnable/OnDisable self-registration. Must run before InitializeTractorBeamSystem.</summary>
        private void InitializeEnvironmentPropSystem()
        {
            if (!_environmentPropAbsorptionEnabled)
            {
                return;
            }

            _environmentPropRegistry = new TractorAbsorbablePropRegistry();

            TractorAbsorbableProp[] props = FindObjectsByType<TractorAbsorbableProp>(FindObjectsSortMode.None);
            for (int i = 0; i < props.Length; i++)
            {
                props[i].Register(_environmentPropRegistry);
            }
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
            EnemySpawner = new EnemyFactory(_enemyPoolRegistry, Enemies, Economy, BaseHealth, _cameraTransform, Vfx, _energyDropService);

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
        }

        /// <summary>UFO combat is a continuous multi-enemy tractor beam, not a shooting weapon; see
        /// UFOTractorBeamController for the admission/capture flow.</summary>
        private void InitializeTractorBeamSystem()
        {
            if (_tractorBeamController == null || Enemies == null)
            {
                return;
            }

            _tractorBeamController.Initialize(Enemies, _energyPickupRegistry, _environmentPropRegistry);
            _tractorBeamVisual?.Initialize(_tractorBeamController);
            _tractorBeamController.PropAbsorbed += HandlePropAbsorbedForExperience;

            _tractorBeamAudio = new TractorBeamAudioController(_audioService, _tractorBeamLoopSource, _tractorBeamLoopClip, _tractorBeamCaptureClip);
            _tractorBeamAudio.Initialize(_tractorBeamController);

            if (_playerSkillEffectApplier != null && PlayerSkills != null)
            {
                PlayerMovement movement = _player != null ? _player.GetComponent<PlayerMovement>() : null;
                _playerSkillEffectApplier.Initialize(PlayerSkills, _tractorBeamController, movement, EnergyWallet);
            }

            if (_playerMissileController != null && PlayerSkills != null && Enemies != null && ProjectileSpawner != null)
            {
                _playerMissileController.Initialize(PlayerSkills, Enemies, ProjectileSpawner, _missileProjectileDefinition);
            }
        }

        /// <summary>Environment Props (trees, rocks, mushrooms...) grant only Experience, never Energy - bypasses
        /// EnergyCollectionService entirely instead of routing through it like EnergyPickups do, since props
        /// never touch the Energy wallet. The amount comes from the prop's own ExperienceReward (per-instance,
        /// capped at 2 like EnemyDefinition.ExperienceReward) rather than one flat value for every prop.</summary>
        private void HandlePropAbsorbedForExperience(TractorAbsorbableProp prop)
        {
            PlayerLevelProgression?.AddExperience(prop.ExperienceReward);
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

            AreaDamage = new AreaDamageResolver(new EnemyRegistrySplashProvider(Enemies));

            TowerMetaUpgradeService metaUpgradeService = _applicationServices?.PlayerProfileService != null
                ? new TowerMetaUpgradeService(_applicationServices.PlayerProfileService)
                : null;

            TowerSpawner = new TowerFactory(_towerRuntimeParent, Enemies, ProjectileSpawner, AreaDamage, GameFlow, Vfx, metaUpgradeService);

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
            EnergyTransactions = new EnergyTowerTransactionService(EnergyWallet, TowerSpawner, GameFlow);
            EnergyWallet.EnergyChanged += HandleEnergyChangedForBuildBadges;
            EnergyWallet.MaxEnergyChanged += HandleMaxEnergyChangedForBuildBadges;
            EnergyTransactions.BuildCompleted += HandleBuildCompletedForBuildBadges;
            EnergyTransactions.TowerUpgraded += HandleTowerUpgradedForBuildBadges;

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

            if (_towerChoicePresenter != null)
            {
                _towerChoicePresenter.Initialize(EnergyTransactions, _towerCatalog, GameSpeed);
            }

            if (_playerBuildNodeProximity != null && _player != null)
            {
                _playerBuildNodeProximity.Initialize(_player.transform, EnergyTransactions, _towerChoicePresenter, _cameraTransform);
            }

            if (_towerDetailsPresenter != null)
            {
                _towerDetailsPresenter.Initialize(TowerSelection, Economy, TowerUpgrade, TowerSell);
            }
        }

        /// <summary>Keeps every BuildNode's "{wallet}/{cost}" Energy badge showing the current wallet balance -
        /// cheap (just text), so it's fine to refresh every node on every wallet change rather than only the
        /// nearest one.</summary>
        private void HandleEnergyChangedForBuildBadges(int currentEnergy)
        {
            _playerBuildNodeProximity?.RefreshAllCostBadges();
        }

        private void HandleMaxEnergyChangedForBuildBadges(int maxEnergy)
        {
            _playerBuildNodeProximity?.RefreshAllCostBadges();
        }

        /// <summary>A just-built node flips Available -> Occupied, switching its badge from the catalog's cheapest
        /// build cost to this specific tower's next-level upgrade cost - EnergyChanged alone doesn't cover this
        /// since TrySpend fires before the node's state/level actually changes.</summary>
        private void HandleBuildCompletedForBuildBadges(BuildNode node, TowerController tower)
        {
            _playerBuildNodeProximity?.RefreshAllCostBadges();
        }

        /// <summary>An upgraded tower's next-level cost changes - EnergyChanged alone is stale here because
        /// TrySpend (and its event) fires before TryApplyNextLevel actually advances the tower's level.</summary>
        private void HandleTowerUpgradedForBuildBadges(TowerController tower)
        {
            _playerBuildNodeProximity?.RefreshAllCostBadges();
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

            if (_resolvedLevelDefinition.WaveCount == 0)
            {
                Debug.LogError("[LevelCompositionRoot] LevelDefinition has no waves; Wave system will not start.", this);
                return;
            }

            PrewarmPoolsForWaves();

            var waves = new WaveDefinition[_resolvedLevelDefinition.WaveCount];
            for (int i = 0; i < waves.Length; i++)
            {
                waves[i] = _resolvedLevelDefinition.GetWave(i);
            }

            _waveController.Initialize(EnemySpawner, waves, _resolvedLevelDefinition.PreparationDuration);

            _waveController.WaveStarted += HandleWaveStarted;
            _waveController.WaveCompleted += HandleWaveCompletedForFlow;
            _waveController.AllWavesCompleted += HandleAllWavesCompleted;
            _waveController.BossSpawned += HandleBossSpawned;

            if (_bossHealthBarPresenter != null)
            {
                _bossHealthBarPresenter.Initialize(_waveController);
            }

            if (_waveDebugControls != null)
            {
                _waveDebugControls.Initialize(_waveController, Enemies);
            }
        }

        private void PrewarmPoolsForWaves()
        {
            var seenDefinitions = new HashSet<EnemyDefinition>();
            for (int w = 0; w < _resolvedLevelDefinition.WaveCount; w++)
            {
                WaveDefinition wave = _resolvedLevelDefinition.GetWave(w);
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

            _applicationServices?.SettingsService?.AttachAudioService(_audioService);
        }

        private void InitializeGameFlowUI()
        {
            RestartService = new LevelRestartService(SceneManager.GetActiveScene().name);

            if (_gameHUDPresenter != null)
            {
                _gameHUDPresenter.Initialize(Economy, EnergyWallet, BaseHealth, GameSpeed, GameFlow);
            }

            _waveHUDPresenter?.Initialize(PlayerLevelProgression);
            _skillChoicePresenter?.Initialize(PlayerLevelProgression, PlayerSkills, GameSpeed);

            if (_gameStateUIController != null)
            {
                _gameStateUIController.Initialize(GameFlow, GameSpeed, RestartService, _applicationServices, _resolvedLevelId);
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

                if (current == GameState.Victory)
                {
                    GrantVictoryRewardsAndProgress();
                }
            }

            if (_player != null)
            {
                bool movementEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _player.SetMovementEnabled(movementEnabled);
            }

            if (_worldSelectionController != null)
            {
                bool buildInputEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _worldSelectionController.SetInputEnabled(buildInputEnabled);
            }

            if (_playerBuildNodeProximity != null)
            {
                bool buildInputEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _playerBuildNodeProximity.SetInputEnabled(buildInputEnabled);
            }
        }

        /// <summary>Computes the win result, then grants Coin (+ a rare first-time-perfect Gem bonus, both
        /// scaled by VipTierTable's Coin bonus) and marks the daily quest complete — all via PlayerProfileService's
        /// own validated mutators, never touching save data directly here.</summary>
        private void GrantVictoryRewardsAndProgress()
        {
            PlayerProfileService profile = _applicationServices?.PlayerProfileService;
            if (profile == null)
            {
                return;
            }

            LevelCompletedResult completionResult = BuildLevelCompletedResult();
            bool isFirstCompletion = !profile.GetLevelProgress(completionResult.LevelId).IsCompleted;

            float vipCoinBonus = VipTierTable.GetMultiplierForTier(profile.VipTier);
            int coinReward = LevelRewardCalculator.CalculateCoinReward(completionResult.Stars, vipCoinBonus);
            int gemReward = LevelRewardCalculator.CalculateGemReward(completionResult.Stars, isFirstCompletion);

            profile.SetLevelCompleted(completionResult);
            profile.AddMetaCurrency(coinReward);
            if (gemReward > 0)
            {
                profile.AddGems(gemReward);
            }

            profile.MarkDailyQuestCompleted(System.DateTime.UtcNow);
        }

        /// <summary>Placeholder star formula for Phase 13's save foundation: 3 stars for undamaged base, 2 for
        /// at least half base health remaining, 1 for a plain win. Phase 14 may replace this with richer rules;
        /// PlayerProfileService never computes stars itself, only stores whatever this returns.</summary>
        private LevelCompletedResult BuildLevelCompletedResult()
        {
            int remaining = BaseHealth.CurrentHealth;
            int max = BaseHealth.MaxHealth;

            int stars = 1;
            if (remaining >= max)
            {
                stars = 3;
            }
            else if (max > 0 && remaining >= max / 2)
            {
                stars = 2;
            }

            return new LevelCompletedResult(_resolvedLevelId, stars, remaining);
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

        private void HandleBossSpawned(EnemyController boss, BossController bossController)
        {
            bossController.Initialize(_waveController);
        }
    }
}
