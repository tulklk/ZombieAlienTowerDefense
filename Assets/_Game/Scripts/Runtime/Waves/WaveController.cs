using System;
using System.Collections;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>Drives wave preparation/spawning/completion using EnemyFactory and a WaveRuntimeTracker.</summary>
    public sealed class WaveController : MonoBehaviour, IEnemySpawnCoordinator
    {
        [SerializeField]
        private EnemyPath3D _path;

        [SerializeField]
        private bool _autoStartFirstWave = true;

        [SerializeField]
        private bool _autoStartNextWaves = true;

        private readonly WaveRuntimeTracker _tracker = new WaveRuntimeTracker();

        private EnemyFactory _enemyFactory;
        private WaveDefinition[] _waves;
        private float _defaultPreparationDuration;
        private Coroutine _schedulingCoroutine;
        private int _waveRunId;
        private bool _isInitialized;
        private bool _allWavesCompletedFired;

        public WaveState CurrentState { get; private set; } = WaveState.Idle;
        public int CurrentWaveIndex { get; private set; } = -1;
        public int CurrentWaveNumber => CurrentWaveIndex + 1;
        public int TotalWaveCount => _waves?.Length ?? 0;
        public float PreparationTimeRemaining { get; private set; }

        public event Action<int, int> WavePrepared;
        public event Action<float> PreparationTimeChanged;
        public event Action<int, int> WaveStarted;
        public event Action<WaveProgressSnapshot> WaveProgressChanged;
        public event Action<int> WaveCompleted;
        public event Action AllWavesCompleted;
        public event Action WaveStopped;
        public event Action<EnemyController, BossController> BossSpawned;

        public void Initialize(EnemyFactory enemyFactory, WaveDefinition[] waves, float defaultPreparationDuration)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[WaveController] Already initialized; ignoring duplicate call.", this);
                return;
            }

            if (enemyFactory == null)
            {
                Debug.LogError("[WaveController] EnemyFactory is null; wave system will not start.", this);
                return;
            }

            if (waves == null || waves.Length == 0)
            {
                Debug.LogError("[WaveController] No WaveDefinitions provided; wave system will not start.", this);
                return;
            }

            if (_path == null || _path.Count < 2)
            {
                Debug.LogError("[WaveController] EnemyPath3D is missing or invalid; wave system will not start.", this);
                return;
            }

            _enemyFactory = enemyFactory;
            _waves = waves;
            _defaultPreparationDuration = Mathf.Max(0f, defaultPreparationDuration);
            _isInitialized = true;

            if (_autoStartFirstWave)
            {
                StartFirstWave();
            }
        }

        public bool StartFirstWave()
        {
            if (!_isInitialized || CurrentWaveIndex >= 0)
            {
                return false;
            }

            CurrentWaveIndex = 0;
            BeginPreparation();
            return true;
        }

        /// <summary>Manually advances to the next wave's preparation. Only needed when AutoStartNextWaves is off.</summary>
        public bool RequestStartPreparedWave()
        {
            if (!_isInitialized)
            {
                return false;
            }

            if (CurrentWaveIndex < 0)
            {
                return StartFirstWave();
            }

            if (CurrentState != WaveState.Completed)
            {
                return false;
            }

            if (CurrentWaveIndex >= TotalWaveCount - 1)
            {
                return false;
            }

            CurrentWaveIndex++;
            BeginPreparation();
            return true;
        }

        public void StopWaves()
        {
            _waveRunId++;

            if (_schedulingCoroutine != null)
            {
                StopCoroutine(_schedulingCoroutine);
                _schedulingCoroutine = null;
            }

            CurrentState = WaveState.Stopped;
            WaveStopped?.Invoke();
        }

        public void ResetWaves()
        {
            StopWaves();
            CurrentWaveIndex = -1;
            CurrentState = WaveState.Idle;
            PreparationTimeRemaining = 0f;
            _tracker.Reset();
            _allWavesCompletedFired = false;
        }

        private void OnDestroy()
        {
            if (_schedulingCoroutine != null)
            {
                StopCoroutine(_schedulingCoroutine);
                _schedulingCoroutine = null;
            }

            _waveRunId++;
        }

        private void BeginPreparation()
        {
            CurrentState = WaveState.Preparing;
            WaveDefinition wave = _waves[CurrentWaveIndex];
            float preparationDuration = wave.HasPreparationOverride ? wave.PreparationDurationOverride : _defaultPreparationDuration;

            WavePrepared?.Invoke(CurrentWaveNumber, TotalWaveCount);
            StartScheduler(PrepareThenSpawnRoutine(wave, preparationDuration));
        }

        private void StartScheduler(IEnumerator routine)
        {
            if (_schedulingCoroutine != null)
            {
                StopCoroutine(_schedulingCoroutine);
            }

            _schedulingCoroutine = StartCoroutine(routine);
        }

        private IEnumerator PrepareThenSpawnRoutine(WaveDefinition wave, float preparationDuration)
        {
            PreparationTimeRemaining = preparationDuration;
            PreparationTimeChanged?.Invoke(PreparationTimeRemaining);

            while (PreparationTimeRemaining > 0f)
            {
                yield return null;
                PreparationTimeRemaining = Mathf.Max(0f, PreparationTimeRemaining - Time.deltaTime);
                PreparationTimeChanged?.Invoke(PreparationTimeRemaining);
            }

            yield return SpawnWaveRoutine(wave);
        }

        private IEnumerator SpawnWaveRoutine(WaveDefinition wave)
        {
            _waveRunId++;
            int runId = _waveRunId;

            _tracker.Initialize(wave.TotalPlannedEnemyCount());
            CurrentState = WaveState.Spawning;
            WaveStarted?.Invoke(CurrentWaveNumber, TotalWaveCount);
            RaiseProgressChanged();

            for (int groupIndex = 0; groupIndex < wave.SpawnGroupCount; groupIndex++)
            {
                EnemySpawnGroup group = wave.GetSpawnGroup(groupIndex);
                if (group == null || !group.IsValid)
                {
                    Debug.LogError($"[WaveController] Skipping invalid spawn group {groupIndex} in wave '{wave.name}'.", this);
                    continue;
                }

                if (group.DelayBeforeGroup > 0f)
                {
                    yield return WaitSeconds(group.DelayBeforeGroup);
                }

                for (int i = 0; i < group.Count; i++)
                {
                    SpawnOneEnemy(group.EnemyDefinition, runId);

                    if (i < group.Count - 1 && group.SpawnInterval > 0f)
                    {
                        yield return WaitSeconds(group.SpawnInterval);
                    }
                }
            }

            _tracker.MarkSpawnSchedulingCompleted();
            if (_tracker.ActiveEnemyCount > 0)
            {
                CurrentState = WaveState.WaitingForRemainingEnemies;
            }

            RaiseProgressChanged();
            TryCompleteWave(runId);
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private void SpawnOneEnemy(EnemyDefinition definition, int runId)
        {
            SpawnTrackedEnemy(definition, runId);
        }

        /// <summary>IEnemySpawnCoordinator entry point: spawns and tracks an enemy outside the normal group-spawn
        /// schedule (e.g. a Boss's minions), attributed to the currently running wave.</summary>
        public EnemyController SpawnTrackedEnemy(EnemyDefinition definition)
        {
            return SpawnTrackedEnemy(definition, _waveRunId);
        }

        private EnemyController SpawnTrackedEnemy(EnemyDefinition definition, int runId)
        {
            Vector3 spawnPosition = _path.GetPoint(0);
            Quaternion spawnRotation = ComputeSpawnRotation();

            EnemyController enemy = _enemyFactory.Spawn(definition, _path, spawnPosition, spawnRotation);
            if (enemy == null)
            {
                _tracker.RecordSpawnFailure();
                RaiseProgressChanged();
                return null;
            }

            _tracker.RecordSpawnSuccess();
            RaiseProgressChanged();

            void HandleResolved(EnemyController resolvedEnemy, EnemyResolveReason reason)
            {
                resolvedEnemy.Resolved -= HandleResolved;

                if (runId != _waveRunId)
                {
                    return;
                }

                _tracker.RecordEnemyResolved();
                RaiseProgressChanged();
                TryCompleteWave(runId);
            }

            enemy.Resolved += HandleResolved;

            if (enemy.BossController != null)
            {
                BossSpawned?.Invoke(enemy, enemy.BossController);
            }

            return enemy;
        }

        private Quaternion ComputeSpawnRotation()
        {
            Vector3 from = _path.GetPoint(0);
            Vector3 to = _path.GetPoint(1);
            Vector3 flatDirection = new Vector3(to.x - from.x, 0f, to.z - from.z);
            return flatDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flatDirection.normalized, Vector3.up)
                : Quaternion.identity;
        }

        private void TryCompleteWave(int runId)
        {
            if (runId != _waveRunId)
            {
                return;
            }

            if (CurrentState == WaveState.Completed || CurrentState == WaveState.Stopped)
            {
                return;
            }

            if (!_tracker.IsCompleted)
            {
                return;
            }

            CurrentState = WaveState.Completed;
            WaveCompleted?.Invoke(CurrentWaveNumber);

            if (CurrentWaveIndex >= TotalWaveCount - 1)
            {
                if (!_allWavesCompletedFired)
                {
                    _allWavesCompletedFired = true;
                    AllWavesCompleted?.Invoke();
                }

                return;
            }

            if (_autoStartNextWaves)
            {
                CurrentWaveIndex++;
                BeginPreparation();
            }
        }

        private void RaiseProgressChanged()
        {
            var snapshot = new WaveProgressSnapshot(
                CurrentWaveNumber,
                TotalWaveCount,
                _tracker.PlannedEnemyCount,
                _tracker.SuccessfulSpawnCount,
                _tracker.FailedSpawnCount,
                _tracker.ActiveEnemyCount,
                _tracker.ResolvedEnemyCount,
                _tracker.NormalizedProgress);

            WaveProgressChanged?.Invoke(snapshot);
        }
    }
}
