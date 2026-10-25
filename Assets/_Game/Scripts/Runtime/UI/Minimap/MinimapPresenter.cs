using Unity.Profiling;
using System.Collections.Generic;
using AlienDefense.Building;
using AlienDefense.Common;
using AlienDefense.Core;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Waves;
using UnityEngine;

namespace AlienDefense.UI.Minimap
{
    /// <summary>Subscribes to a fixed WaveController (the boss countdown on levels that have one - see
    /// WaveController.BossCountdownChanged - otherwise the wave-preparation countdown, see
    /// WaveController.PreparationTimeRemaining/PreparationTimeChanged) and reads a fixed EnemyPath3D once at
    /// startup, forwarding both to a MinimapController. No gameplay timer or waypoint list is created here -
    /// mirrors this project's existing WaveHUDPresenter/WaveHUDView split exactly.
    ///
    /// Also optionally polls LevelCompositionRoot.Enemies (the project's one EnemyRegistry, owned by
    /// LevelCompositionRoot - see its InitializeEnemySystem) at a throttled rate to feed enemy dot positions to
    /// the minimap. Never Instantiates a UI object per enemy and never runs every frame - see
    /// _enemyMarkerUpdateInterval and MinimapController.SetEnemyPositions's own pool.
    ///
    /// Also optionally places one static dot per BuildNode (see _buildNodes/InitializeBuildNodeMarkers - built
    /// once, BuildNodes never move) and, every frame (a single object, no throttling needed), forwards the
    /// live player UFO's position (see _player/PlayerController) so MinimapController's existing UFO marker
    /// tracks the real player in real time instead of sitting on the static Goal marker.
    ///
    /// On the same throttled tick it mirrors each BuildNode's live state onto its dot (hidden when the node is
    /// gone/inactive, optionally tinted while occupied) and forwards the furthest enemy's EnemyMovement.PathProgress
    /// to the controller's optional active-route overlay. Optionally maps the level's LevelBounds rather than just
    /// the path's extents (_mapBounds). All three are no-ops on levels that don't wire them.</summary>
    public sealed class MinimapPresenter : MonoBehaviour
    {
        [SerializeField]
        private WaveController _waveController;

        [SerializeField]
        private EnemyPath3D _enemyPath;

        [SerializeField]
        [Tooltip("Optional. The level's other enemy routes (same order as WaveController's Additional Paths and as " +
            "MinimapController's Extra Path Graphics). Empty on single-route levels.")]
        private EnemyPath3D[] _additionalEnemyPaths = new EnemyPath3D[0];

        [SerializeField]
        private MinimapController _controller;

        [Header("Enemy markers (optional)")]
        [SerializeField]
        [Tooltip("Optional - auto-found via FindFirstObjectByType if left unassigned. Only its public Enemies " +
            "registry is read; nothing here is ever mutated.")]
        private LevelCompositionRoot _compositionRoot;

        [SerializeField, Min(0.05f)]
        [Tooltip("Enemy marker refresh period in seconds (0.15 = ~6-7Hz) - well under the 5-10Hz the spec calls " +
            "for, deliberately not every frame.")]
        private float _enemyMarkerUpdateInterval = 0.15f;

        [Header("Base/BuildNode markers (optional)")]
        [SerializeField]
        [Tooltip("Every BuildNode to show as a static dot on the minimap. Scene-local list, not a global " +
            "registry - matches PlayerBuildNodeProximityController/BuildNodeVisualCoordinator's own convention.")]
        private BuildNode[] _buildNodes;

        [Header("Map area (optional)")]
        [SerializeField]
        [Tooltip("Optional. When assigned, the minimap maps this level's whole playable rectangle (plus the path) " +
            "instead of only the path's own extents - see MinimapController.SetWorldBounds.")]
        private LevelBounds _mapBounds;

        [Header("Live player/UFO marker (optional)")]
        [SerializeField]
        [Tooltip("Optional - auto-found via FindFirstObjectByType if left unassigned. Its transform.position is " +
            "read every frame (a single object, not throttled like the enemy markers) - see Update.")]
        private PlayerController _player;

        private EnemyRegistry _enemyRegistry;
        private float _enemyMarkerTimer;
        private readonly List<Vector3> _enemyPositionsBuffer = new List<Vector3>(32);
        private readonly List<bool> _enemyBossFlagsBuffer = new List<bool>(32);
        private readonly List<BuildNode> _markedBuildNodes = new List<BuildNode>();
        private float[] _extraFurthestProgress = new float[0];

        private void Awake()
        {
            if (_waveController == null || _enemyPath == null || _controller == null)
            {
                Debug.LogError("[MinimapPresenter] WaveController, EnemyPath3D, and MinimapController must all be assigned.", this);
                enabled = false;
                return;
            }

            if (_mapBounds != null)
            {
                Vector2 center = _mapBounds.Center;
                Vector2 extents = _mapBounds.Extents;
                _controller.SetWorldBounds(new Rect(center - extents, extents * 2f));
            }

            _controller.InitializePath(_enemyPath.Waypoints);
            _extraFurthestProgress = new float[_additionalEnemyPaths.Length];
            for (int i = 0; i < _additionalEnemyPaths.Length; i++)
            {
                if (_additionalEnemyPaths[i] != null)
                {
                    _controller.InitializeExtraPath(i, _additionalEnemyPaths[i].Waypoints);
                }
            }
            InitializeBuildNodeMarkers();

            _waveController.PreparationTimeChanged += HandlePreparationTimeChanged;
            _waveController.WaveStarted += HandleWaveStarted;
            _waveController.BossCountdownChanged += HandleBossCountdownChanged;
        }

        private void InitializeBuildNodeMarkers()
        {
            if (_buildNodes == null || _buildNodes.Length == 0)
            {
                return; // feature left off for this level - no BuildNodes wired
            }

            var buildNodePositions = new List<Vector3>(_buildNodes.Length);
            _markedBuildNodes.Clear();
            for (int i = 0; i < _buildNodes.Length; i++)
            {
                if (_buildNodes[i] != null)
                {
                    buildNodePositions.Add(_buildNodes[i].BuildPoint.position);
                    _markedBuildNodes.Add(_buildNodes[i]);
                }
            }

            _controller.InitializeBuildNodes(buildNodePositions);
            SyncBuildNodeMarkers();
        }

        /// <summary>Mirrors each BuildNode's live state onto its dot: hidden once the node is destroyed or
        /// inactive, "occupied" while a tower stands on it. Runs on the throttled tick; the controller only
        /// touches a dot when its state actually changed.</summary>
        private void SyncBuildNodeMarkers()
        {
            for (int i = 0; i < _markedBuildNodes.Count; i++)
            {
                BuildNode node = _markedBuildNodes[i];
                bool visible = node != null && node.isActiveAndEnabled;
                _controller.SetBuildNodeMarkerState(i, visible, visible && node.State == BuildNodeState.Occupied);
            }
        }

        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("AlienDefense.Minimap.Update");

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                UpdateCore();
            }
        }

        private void UpdateCore()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
            }

            if (_player != null)
            {
                _controller.SetUFOPosition(_player.transform.position);
            }

            _enemyMarkerTimer += Time.deltaTime;
            if (_enemyMarkerTimer < _enemyMarkerUpdateInterval)
            {
                return;
            }

            _enemyMarkerTimer = 0f;
            SyncBuildNodeMarkers();

            if (_enemyRegistry == null && !TryResolveEnemyRegistry())
            {
                return; // LevelCompositionRoot/its registry isn't ready yet - retry on the next throttled tick
            }

            _enemyPositionsBuffer.Clear();
            _enemyBossFlagsBuffer.Clear();
            float furthestProgress = 0f;
            for (int i = 0; i < _extraFurthestProgress.Length; i++)
            {
                _extraFurthestProgress[i] = 0f;
            }
            int count = _enemyRegistry.Count;

            // Bosses first, so they always get a marker even when more enemies are alive than the marker pool holds.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    EnemyController enemy = _enemyRegistry.GetAt(i);
                    if (enemy == null)
                    {
                        continue;
                    }

                    bool boss = enemy.Rank != EnemyRank.Normal || enemy.BossController != null;
                    if (boss != (pass == 0))
                    {
                        continue;
                    }

                    _enemyPositionsBuffer.Add(enemy.transform.position);
                    _enemyBossFlagsBuffer.Add(boss);
                    if (enemy.Movement == null)
                    {
                        continue;
                    }

                    // Each route's overlay follows the furthest enemy walking THAT route.
                    float progress = enemy.Movement.PathProgress;
                    int extraIndex = _extraFurthestProgress.Length > 0 ? System.Array.IndexOf(_additionalEnemyPaths, enemy.Movement.Path) : -1;
                    if (extraIndex >= 0)
                    {
                        if (progress > _extraFurthestProgress[extraIndex])
                        {
                            _extraFurthestProgress[extraIndex] = progress;
                        }
                    }
                    else if (progress > furthestProgress)
                    {
                        furthestProgress = progress;
                    }
                }
            }

            _controller.SetEnemyPositions(_enemyPositionsBuffer, _enemyBossFlagsBuffer);
            _controller.SetActivePathProgress(furthestProgress);
            for (int i = 0; i < _extraFurthestProgress.Length; i++)
            {
                _controller.SetExtraActivePathProgress(i, _extraFurthestProgress[i]);
            }
        }

        private bool TryResolveEnemyRegistry()
        {
            if (_compositionRoot == null)
            {
                _compositionRoot = FindFirstObjectByType<LevelCompositionRoot>();
            }

            if (_compositionRoot == null)
            {
                return false;
            }

            _enemyRegistry = _compositionRoot.Enemies;
            return _enemyRegistry != null;
        }

        private void OnDestroy()
        {
            if (_waveController == null)
            {
                return;
            }

            _waveController.PreparationTimeChanged -= HandlePreparationTimeChanged;
            _waveController.WaveStarted -= HandleWaveStarted;
            _waveController.BossCountdownChanged -= HandleBossCountdownChanged;
        }

        // On a level whose boss arrives on a timer the minimap timer is that boss countdown; otherwise it shows
        // the wave-preparation countdown as before.
        private void HandleBossCountdownChanged(float secondsRemaining)
        {
            if (_waveController.HasBossCountdown)
            {
                _controller.SetTime(secondsRemaining);
            }
        }

        private void HandlePreparationTimeChanged(float secondsRemaining)
        {
            if (_waveController.HasBossCountdown)
            {
                return;
            }

            _controller.SetTime(secondsRemaining);
        }

        private void HandleWaveStarted(int waveNumber, int totalWaves)
        {
            if (_waveController.HasBossCountdown)
            {
                return;
            }

            // No preparation countdown while a wave is actively spawning/fighting - park the timer at 0 rather
            // than leaving it showing a stale "0:03" from the last tick of preparation.
            _controller.SetTime(0f);
        }
    }
}
