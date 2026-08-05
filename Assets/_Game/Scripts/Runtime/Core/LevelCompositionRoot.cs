using AlienDefense.Base;
using AlienDefense.Data;
using AlienDefense.DebugTools;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Player;
using UnityEngine;

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
        private Transform _enemyRuntimeParent;

        [SerializeField]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Optional.")]
        private EnemyDebugSpawner _debugSpawner;

        private EnemyPoolRegistry _enemyPoolRegistry;

        public GameFlowController GameFlow { get; private set; }
        public GameSpeedController GameSpeed { get; private set; }
        public EconomyService Economy { get; private set; }
        public BaseHealthService BaseHealth { get; private set; }
        public EnemyRegistry Enemies { get; private set; }
        public EnemyFactory EnemySpawner { get; private set; }

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

            InitializeEnemySystem();
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

            Enemies?.Clear();
            _enemyPoolRegistry?.Clear();
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
            EnemySpawner = new EnemyFactory(_enemyPoolRegistry, Enemies, Economy, BaseHealth, _cameraTransform);

            _debugSpawner?.Initialize(EnemySpawner);
        }

        private void HandleBaseDestroyed()
        {
            GameFlow.ReportDefeat();
        }

        private void HandleGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Victory || current == GameState.Defeat)
            {
                GameSpeed.Lock();
                DespawnAllEnemies();
            }

            if (_player != null)
            {
                bool movementEnabled = current != GameState.Paused
                    && current != GameState.Victory
                    && current != GameState.Defeat;
                _player.SetMovementEnabled(movementEnabled);
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
    }
}
