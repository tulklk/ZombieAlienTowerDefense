using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        private WaveSpawnSettings _spawnSettings;
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
            Initialize(enemyFactory, waves, defaultPreparationDuration, WaveSpawnSettings.CreateDefault(), autoStartFirstWave: null);
        }

        public void Initialize(
            EnemyFactory enemyFactory,
            WaveDefinition[] waves,
            float defaultPreparationDuration,
            WaveSpawnSettings spawnSettings)
        {
            Initialize(enemyFactory, waves, defaultPreparationDuration, spawnSettings, autoStartFirstWave: null);
        }

        /// <param name="autoStartFirstWave">Null keeps the serialized inspector flag; otherwise overrides it for this run.</param>
        public void Initialize(
            EnemyFactory enemyFactory,
            WaveDefinition[] waves,
            float defaultPreparationDuration,
            WaveSpawnSettings spawnSettings,
            bool? autoStartFirstWave)
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
            _spawnSettings = spawnSettings;
            if (_spawnSettings.MaxSpeedMultiplier < 1f)
            {
                _spawnSettings.MaxSpeedMultiplier = 1f;
            }

            if (autoStartFirstWave.HasValue)
            {
                _autoStartFirstWave = autoStartFirstWave.Value;
            }

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

            int planned = wave.TotalPlannedEnemyCount();
            if (planned <= 0)
            {
                Debug.LogWarning($"[WaveController] Wave '{wave.name}' has no valid enemies; completing immediately.", this);
                _tracker.Initialize(0);
                _tracker.MarkSpawnSchedulingCompleted();
                CurrentState = WaveState.Spawning;
                WaveStarted?.Invoke(CurrentWaveNumber, TotalWaveCount);
                RaiseProgressChanged();
                TryCompleteWave(runId);
                yield break;
            }

            _tracker.Initialize(planned);
            CurrentState = WaveState.Spawning;
            WaveStarted?.Invoke(CurrentWaveNumber, TotalWaveCount);
            RaiseProgressChanged();
            LogWaveDebug(wave);

            for (int groupIndex = 0; groupIndex < wave.SpawnGroupCount; groupIndex++)
            {
                EnemySpawnGroup group = wave.GetSpawnGroup(groupIndex);
                if (group == null || !group.IsValid)
                {
                    Debug.LogWarning($"[WaveController] Skipping invalid spawn group {groupIndex} in wave '{wave.name}'.", this);
                    continue;
                }

                if (group.DelayBeforeGroup > 0f)
                {
                    yield return WaitSeconds(group.DelayBeforeGroup);
                }

                List<EnemyDefinition> queue = group.BuildSpawnQueue();
                float interval = Mathf.Max(0f, group.SpawnInterval);
                for (int i = 0; i < queue.Count; i++)
                {
                    EnemyDefinition definition = queue[i];
                    if (definition == null)
                    {
                        Debug.LogWarning("[WaveController] Skipping null EnemyDefinition in spawn queue.");
                        _tracker.RecordSpawnFailure();
                        RaiseProgressChanged();
                        continue;
                    }

                    yield return WaitForAliveCapacity(runId);
                    if (runId != _waveRunId)
                    {
                        yield break;
                    }

                    SpawnTrackedEnemy(definition, runId);

                    if (i < queue.Count - 1 && interval > 0f)
                    {
                        yield return WaitSeconds(interval);
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

        private IEnumerator WaitForAliveCapacity(int runId)
        {
            int maxAlive = _spawnSettings.MaxAliveEnemies;
            if (maxAlive <= 0)
            {
                yield break;
            }

            while (runId == _waveRunId && _tracker.ActiveEnemyCount >= maxAlive)
            {
                yield return null;
            }
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
            EnemySpawnModifiers modifiers = ComputeModifiers(CurrentWaveNumber, definition);

            EnemyController enemy = _enemyFactory.Spawn(definition, _path, spawnPosition, spawnRotation, modifiers);
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

        public EnemySpawnModifiers ComputeModifiers(int waveNumber, EnemyDefinition definition)
        {
            int safeWave = Mathf.Max(1, waveNumber);
            float baseHp = 1f + (safeWave - 1) * _spawnSettings.HealthPerWaveStep;
            float baseSpd = 1f + (safeWave - 1) * _spawnSettings.SpeedPerWaveStep;
            float baseDmg = 1f + (safeWave - 1) * _spawnSettings.DamagePerWaveStep;

            float hpFactor = definition != null ? definition.HealthScaleFactor : 1f;
            float spdFactor = definition != null ? definition.SpeedScaleFactor : 1f;
            float dmgFactor = definition != null ? definition.DamageScaleFactor : 1f;

            float hpMul = 1f + (baseHp - 1f) * hpFactor;
            float spdMul = Mathf.Min(_spawnSettings.MaxSpeedMultiplier, 1f + (baseSpd - 1f) * spdFactor);
            float dmgMul = 1f + (baseDmg - 1f) * dmgFactor;
            return new EnemySpawnModifiers(hpMul, spdMul, dmgMul);
        }

        private void LogWaveDebug(WaveDefinition wave)
        {
            if (!_spawnSettings.DebugWaveLogs || wave == null)
            {
                return;
            }

            var counts = new Dictionary<string, int>();
            for (int g = 0; g < wave.SpawnGroupCount; g++)
            {
                EnemySpawnGroup group = wave.GetSpawnGroup(g);
                if (group == null || !group.IsValid)
                {
                    continue;
                }

                List<EnemyDefinition> queue = group.BuildSpawnQueue();
                for (int i = 0; i < queue.Count; i++)
                {
                    EnemyDefinition def = queue[i];
                    if (def == null)
                    {
                        continue;
                    }

                    string key = def.DisplayName;
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                }
            }

            var sb = new StringBuilder(128);
            sb.Append("[Wave ").Append(CurrentWaveNumber).Append("]\n");
            foreach (KeyValuePair<string, int> pair in counts)
            {
                sb.Append(pair.Key).Append(": ").Append(pair.Value).Append('\n');
            }

            EnemySpawnModifiers sample = ComputeModifiers(CurrentWaveNumber, null);
            sb.Append("HP Multiplier (Normal curve): ").Append(sample.HealthMultiplier.ToString("0.00")).Append('\n');
            if (wave.SpawnGroupCount > 0)
            {
                EnemySpawnGroup first = wave.GetSpawnGroup(0);
                if (first != null)
                {
                    sb.Append("Spawn Interval: ").Append(first.SpawnInterval.ToString("0.00"));
                }
            }

            Debug.Log(sb.ToString(), this);
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
