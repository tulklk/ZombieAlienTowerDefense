using System.Collections.Generic;
using AlienDefense.Audio;
using AlienDefense.Base;
using AlienDefense.Building;
using AlienDefense.Combat;
using AlienDefense.Common;
using AlienDefense.Data;
using AlienDefense.DebugTools;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Level;
using AlienDefense.Pickups;
using AlienDefense.Player;
using AlienDefense.Progression;
using AlienDefense.Meta;
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
        [Tooltip("Optional. Level-start takeoff cinematic from landing pad to sky start.")]
        private UFOFlightIntro _ufoFlightIntro;

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
        [Tooltip("Optional. Keeps loose Energy cubes scattered around the map and along the zombie road for the " +
            "player to collect. Leave empty for a level with no ambient Energy - kills still drop their own.")]
        private EnergyScatterSpawner _energyScatterSpawner;

        [SerializeField]
        [Tooltip("Optional. The road the Energy scatter strings cubes along. Leave empty to scatter over open " +
            "ground only.")]
        private EnemyPath3D _energyScatterPath;

        [SerializeField]
        [Tooltip("Optional. Bounds the Energy scatter's open-ground placement. Leave empty to place along the " +
            "road only.")]
        private LevelBounds _energyScatterBounds;

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
        [Tooltip("Optional. Plays the boss introduction cinematic for a LevelDefinition with a Boss Encounter. " +
            "Without it the boss group is released the moment it spawns.")]
        private BossIntroController _bossIntroController;

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

        /// <summary>Per-match damage totals behind the pause panel's damage leaders.</summary>
        public CombatStatsService CombatStats { get; private set; }
        private CareerStatisticsTracker _careerStatistics;
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

            if (ShouldDeferGameplayForIntro())
            {
                _ufoFlightIntro.IntroCompleted += HandleUfoIntroCompleted;
                if (_ufoFlightIntro.IsIntroCompleted)
                {
                    HandleUfoIntroCompleted();
                }
            }
            else
            {
                GameFlow.BeginPreparingWave();
            }
        }

        private bool ShouldDeferGameplayForIntro()
        {
            return _ufoFlightIntro != null && !_ufoFlightIntro.SkipIntro;
        }

        private void HandleUfoIntroCompleted()
        {
            if (_ufoFlightIntro != null)
            {
                _ufoFlightIntro.IntroCompleted -= HandleUfoIntroCompleted;
            }

            if (_waveController != null && _waveController.CurrentWaveIndex < 0)
            {
                _waveController.StartFirstWave();
            }

            if (GameFlow != null && GameFlow.CurrentState == GameState.Initializing)
            {
                GameFlow.BeginPreparingWave();
            }

            _ufoFlightIntro?.NotifyGameplaySystemsReady();
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
            CombatStats = new CombatStatsService();
            _careerStatistics = new CareerStatisticsTracker(_applicationServices?.PlayerProfileService);

            Application.targetFrameRate = levelDefinition.TargetFrameRate;

            BaseHealth.Destroyed += HandleBaseDestroyed;
            GameFlow.GameStateChanged += HandleGameStateChanged;

            InitializeVfxSystem();
            InitializeEnergyEconomySystem();
            InitializeEnergyScatterSystem();
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
                _tractorBeamController.EnergyCargoFullRefused -= HandleEnergyCargoFullRefused;
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

            if (_ufoFlightIntro != null)
            {
                _ufoFlightIntro.IntroCompleted -= HandleUfoIntroCompleted;
            }

            _buildLifecycleVfx?.Unsubscribe();
            _gameplayAudio?.Unsubscribe();
            _tractorBeamAudio?.Unsubscribe();
            _tractorBeamVisual?.Unsubscribe();
            _applicationServices?.SettingsService?.DetachAudioService(_audioService);
            CombatStats?.Dispose();
            _careerStatistics?.Dispose();
            _careerStatistics = null;

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

        /// <summary>Ambient Energy scatter. Runs straight after InitializeEnergyEconomySystem because it needs
        /// that method's EnergyDropService/EnergyPickupRegistry, and does nothing without them — a level with no
        /// EnergyPickup prefab simply has no scatter either.
        ///
        /// This is where AlienDefense.Enemies meets AlienDefense.Pickups: the spawner takes the road as bare
        /// Transforms so Pickups keeps its "never depends on Enemies" rule, and Core, which already references
        /// both, is the one place allowed to bridge them.</summary>
        private void InitializeEnergyScatterSystem()
        {
            if (_energyScatterSpawner == null || _energyDropService == null)
            {
                return;
            }

            _energyScatterSpawner.Initialize(
                _energyDropService,
                _energyPickupRegistry,
                _energyScatterPath != null ? _energyScatterPath.Waypoints : null,
                _energyScatterBounds);
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

            _tractorBeamController.Initialize(Enemies, _energyPickupRegistry, _environmentPropRegistry, EnergyWallet, _applicationServices?.SettingsService);
            _tractorBeamVisual?.Initialize(_tractorBeamController, _cameraTransform, Vfx);
            _tractorBeamController.PropAbsorbed += HandlePropAbsorbedForExperience;
            _tractorBeamController.EnergyCargoFullRefused += HandleEnergyCargoFullRefused;

            _tractorBeamAudio = new TractorBeamAudioController(_audioService, _tractorBeamLoopSource, _tractorBeamLoopClip, _tractorBeamCaptureClip);
            _tractorBeamAudio.Initialize(_tractorBeamController);

            if (_playerSkillEffectApplier != null && PlayerSkills != null)
            {
                PlayerMovement movement = _player != null ? _player.GetComponent<PlayerMovement>() : null;
                _playerSkillEffectApplier.Initialize(PlayerSkills, _tractorBeamController, movement, EnergyWallet);
            }

            if (_playerMissileController != null && PlayerSkills != null && Enemies != null && ProjectileSpawner != null)
            {
                // Its own resolver: the towers' AreaDamage may not exist yet at this point of startup.
                _playerMissileController.Initialize(PlayerSkills, Enemies, ProjectileSpawner, _missileProjectileDefinition,
                    new AreaDamageResolver(new EnemyRegistrySplashProvider(Enemies)));
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

        private void HandleEnergyCargoFullRefused()
        {
            _gameHUDPresenter?.NotifyCargoFullRefuse();
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

        /// <summary>Keeps every BuildNode's "{deposited}/{cost}" Energy badge and its "can finish" arrow current -
        /// deposits move Energy out of the wallet, and the arrow depends on the wallet balance. Cheap (just text),
        /// so it's fine to refresh every node on every wallet change rather than only the nearest one.</summary>
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

            _waveController.ConfigureBossEncounter(_resolvedLevelDefinition.BossEncounter);
            if (_bossIntroController != null)
            {
                _bossIntroController.Initialize(_waveController);
            }

            _waveController.Initialize(
                EnemySpawner,
                waves,
                _resolvedLevelDefinition.PreparationDuration,
                _resolvedLevelDefinition.CreateWaveSpawnSettings(),
                autoStartFirstWave: !ShouldDeferGameplayForIntro());

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

        /// <summary>Fills every enemy pool, while the level loads, with as many instances as the level can have of that
        /// type at once - the biggest wave's count, plus the boss group (which arrives while the last wave's leftovers
        /// are still walking), capped by Max Alive and the pool size. Before, pools only held their definition's small
        /// default (e.g. 10 Normals for a 24-Normal wave), so the rest were instantiated - Awake, canvases, skinned
        /// mesh setup - in the middle of combat.</summary>
        private void PrewarmPoolsForWaves()
        {
            var demand = new Dictionary<EnemyDefinition, int>();
            var lastWaveCounts = new Dictionary<EnemyDefinition, int>();
            for (int w = 0; w < _resolvedLevelDefinition.WaveCount; w++)
            {
                WaveDefinition wave = _resolvedLevelDefinition.GetWave(w);
                if (wave == null)
                {
                    continue;
                }

                lastWaveCounts.Clear();
                for (int g = 0; g < wave.SpawnGroupCount; g++)
                {
                    EnemySpawnGroup group = wave.GetSpawnGroup(g);
                    if (group == null)
                    {
                        continue;
                    }

                    for (int e = 0; e < group.EntryCount; e++)
                    {
                        EnemySpawnEntry entry = group.GetEntry(e);
                        if (entry != null && entry.IsValid)
                        {
                            lastWaveCounts[entry.EnemyDefinition] = Count(lastWaveCounts, entry.EnemyDefinition) + entry.Count;
                        }
                    }
                }

                foreach (KeyValuePair<EnemyDefinition, int> pair in lastWaveCounts)
                {
                    demand[pair.Key] = Mathf.Max(Count(demand, pair.Key), pair.Value);
                }
            }

            int escortTotal = 0;
            BossEncounterDefinition encounter = _resolvedLevelDefinition.BossEncounter;
            if (encounter != null && encounter.IsValid)
            {
                demand[encounter.BossDefinition] = Mathf.Max(Count(demand, encounter.BossDefinition), 1);
                for (int e = 0; e < encounter.EscortEntryCount; e++)
                {
                    EnemySpawnEntry entry = encounter.GetEscortEntry(e);
                    if (entry != null && entry.IsValid)
                    {
                        escortTotal += entry.Count;
                        int withLeftovers = Count(lastWaveCounts, entry.EnemyDefinition) + entry.Count;
                        demand[entry.EnemyDefinition] = Mathf.Max(Count(demand, entry.EnemyDefinition), withLeftovers);
                    }
                }
            }

            int maxAlive = _resolvedLevelDefinition.CreateWaveSpawnSettings().MaxAliveEnemies;
            foreach (KeyValuePair<EnemyDefinition, int> pair in demand)
            {
                EnemyPool pool = pair.Key != null ? _enemyPoolRegistry.GetOrCreatePool(pair.Key) : null;
                if (pool == null)
                {
                    continue;
                }

                int target = Mathf.Min(pair.Value, pair.Key.PoolMaximumSize);
                if (maxAlive > 0)
                {
                    target = Mathf.Min(target, maxAlive + escortTotal);
                }

                pool.Prewarm(target); // tops up to at least this many; instances already created are reused
            }
        }

        private static int Count(Dictionary<EnemyDefinition, int> counts, EnemyDefinition definition)
        {
            return counts.TryGetValue(definition, out int value) ? value : 0;
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
                _gameStateUIController.BindPauseServices(_applicationServices?.SettingsService, PlayerSkills, CombatStats, BaseHealth);
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

            // The boss intro owns player input while it plays and hands it back itself when it ends - a pause /
            // resume in the middle must not re-enable movement or build input underneath the cinematic.
            bool bossIntroOwnsInput = _bossIntroController != null && _bossIntroController.IsPlaying
                && current != GameState.Victory && current != GameState.Defeat;

            if (_player != null && !bossIntroOwnsInput)
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

            if (_playerBuildNodeProximity != null && !bossIntroOwnsInput)
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
            LevelCompletedResult completionResult = BuildLevelCompletedResult();
            PlayerProfileService profile = _applicationServices?.PlayerProfileService;
            int coinReward = 0;
            int gemReward = 0;

            if (profile != null)
            {
                bool isFirstCompletion = !profile.GetLevelProgress(completionResult.LevelId).IsCompleted;

                float vipCoinBonus = VipTierTable.GetMultiplierForTier(profile.VipTier);
                coinReward = LevelRewardCalculator.CalculateCoinReward(completionResult.Stars, vipCoinBonus);
                gemReward = LevelRewardCalculator.CalculateGemReward(completionResult.Stars, isFirstCompletion);

                profile.SetLevelCompleted(completionResult);
                profile.AddMetaCurrency(coinReward);
                if (gemReward > 0)
                {
                    profile.AddGems(gemReward);
                }

                profile.MarkDailyQuestCompleted(System.DateTime.UtcNow);
                UnlockNextLevel(profile);
            }

            // The panel only shows what was granted right here - it can never hand a reward out a second time.
            _gameStateUIController?.ShowVictoryResult(BuildVictoryResult(completionResult, coinReward, gemReward));
        }

        /// <summary>Beating a level opens the next one in the catalog (the campaign's only unlock rule today).</summary>
        private void UnlockNextLevel(PlayerProfileService profile)
        {
            LevelCatalog catalog = _applicationServices?.LevelCatalog;
            if (catalog != null && catalog.TryGetNext(_resolvedLevelId, out LevelCatalogEntry next) && next.LevelId != null)
            {
                profile.SetHighestUnlockedLevel(next.LevelId);
            }
        }

        /// <summary>Everything the victory panel prints: the granted rewards, the star result and this match's
        /// damage breakdown (CombatStatsService, which lives and dies with the level, so it never carries over).</summary>
        private LevelVictoryResult BuildVictoryResult(LevelCompletedResult completionResult, int coinReward, int gemReward)
        {
            var rewards = new List<VictoryReward>(3);
            if (coinReward > 0)
            {
                rewards.Add(new VictoryReward(VictoryRewardType.Coins, coinReward));
            }

            if (gemReward > 0)
            {
                rewards.Add(new VictoryReward(VictoryRewardType.Gems, gemReward));
            }

            int experience = PlayerLevelProgression != null ? PlayerLevelProgression.CurrentExperience : 0;
            if (experience > 0)
            {
                rewards.Add(new VictoryReward(VictoryRewardType.Experience, experience));
            }

            IReadOnlyList<CombatStatsService.Contributor> sources = CombatStats != null
                ? CombatStats.GetLeaders(0)
                : System.Array.Empty<CombatStatsService.Contributor>();
            var sourcesCopy = new List<CombatStatsService.Contributor>(sources);

            bool hasNext = _applicationServices?.LevelCatalog != null
                && _applicationServices.LevelCatalog.TryGetNext(_resolvedLevelId, out LevelCatalogEntry _);

            return new LevelVictoryResult(
                completionResult.LevelId,
                BuildLevelDisplayName(),
                completionResult.Stars,
                BaseHealth != null && BaseHealth.CurrentHealth >= BaseHealth.MaxHealth,
                BaseHealth != null ? BaseHealth.CurrentHealth : 0,
                BaseHealth != null ? BaseHealth.MaxHealth : 0,
                rewards,
                sourcesCopy,
                CombatStats != null ? CombatStats.TotalDamage : 0f,
                hasNext);
        }

        /// <summary>"CAMPAIGN LEVEL 2" from the catalog position; falls back to the level id when a level is run
        /// outside the campaign (the Editor-only development fallback, for instance).</summary>
        private string BuildLevelDisplayName()
        {
            LevelCatalog catalog = _applicationServices?.LevelCatalog;
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    LevelCatalogEntry entry = catalog.GetEntry(i);
                    if (entry != null && entry.LevelId == _resolvedLevelId)
                    {
                        return "CAMPAIGN LEVEL " + (i + 1);
                    }
                }
            }

            return string.IsNullOrEmpty(_resolvedLevelId) ? "LEVEL" : _resolvedLevelId.Replace("_", " ").ToUpperInvariant();
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
