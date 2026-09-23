using Unity.Profiling;
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

        private BossEncounterDefinition _bossEncounter;
        private bool _holdBossEncounterForIntro;
        private bool _bossEncounterRunning;
        private readonly List<EnemyController> _bossEncounterGroup = new List<EnemyController>();
        private EnemyController _bossEncounterBoss;

        // Normal enemies still alive when the countdown summoned the boss keep counting toward the encounter.
        private int _carriedRunId = -1;

        private bool _bossCountdownTicking;
        private bool _bossCountdownExpired;

        public WaveState CurrentState { get; private set; } = WaveState.Idle;

        /// <summary>True when this level's boss arrives on a timer (see BossEncounterDefinition.BossCountdown).</summary>
        public bool HasBossCountdown => HasBossEncounter && _bossEncounter.BossCountdown > 0f;

        /// <summary>Seconds until the boss arrives; 0 once it is on its way.</summary>
        public float BossCountdownRemaining { get; private set; }

        /// <summary>Raised whenever BossCountdownRemaining changes (every frame while it runs).</summary>
        public event Action<float> BossCountdownChanged;

        /// <summary>Where the level's closing boss encounter is (None for levels without one).</summary>
        public BossEncounterPhase EncounterPhase { get; private set; } = BossEncounterPhase.None;
        public bool HasBossEncounter => _bossEncounter != null && _bossEncounter.IsValid;

        /// <summary>Boss + escorts were spawned and laid out; if an intro holds them they are still frozen.
        /// Args: the boss, then the whole group (boss first).</summary>
        public event Action<EnemyController, IReadOnlyList<EnemyController>> BossEncounterSpawned;

        /// <summary>The boss group was released and the fight is on.</summary>
        public event Action BossEncounterActivated;
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

            if (HasBossEncounter && _bossEncounter.DebugSkipNormalWaves)
            {
                Debug.Log("[WaveController] Debug: skipping normal waves, starting the boss encounter.", this);
                CurrentWaveIndex = TotalWaveCount - 1;
                StartScheduler(BossEncounterRoutine(0.1f, raiseWaveStarted: true));
                return true;
            }

            CurrentWaveIndex = 0;
            BeginPreparation();
            return true;
        }

        /// <summary>Optional closing encounter. Must be called before Initialize (which may auto-start waves).</summary>
        public void ConfigureBossEncounter(BossEncounterDefinition encounter)
        {
            _bossEncounter = encounter;
            EncounterPhase = HasBossEncounter ? BossEncounterPhase.Pending : BossEncounterPhase.None;
            ResetBossCountdown();
        }

        private void ResetBossCountdown()
        {
            _bossCountdownTicking = false;
            _bossCountdownExpired = false;
            SetBossCountdown(HasBossCountdown ? _bossEncounter.BossCountdown : 0f);
        }

        private void SetBossCountdown(float seconds)
        {
            BossCountdownRemaining = Mathf.Max(0f, seconds);
            BossCountdownChanged?.Invoke(BossCountdownRemaining);
        }

        /// <summary>Scaled time, so pausing the game pauses the countdown too.</summary>
        private void Update()
        {
            if (!_bossCountdownTicking)
            {
                return;
            }

            SetBossCountdown(BossCountdownRemaining - Time.deltaTime);
            if (BossCountdownRemaining > 0f)
            {
                return;
            }

            _bossCountdownTicking = false;
            _bossCountdownExpired = true;
            TryStartBossFromCountdown();
        }

        /// <summary>The countdown ran out: bring the boss in now, provided the last normal wave has finished
        /// spawning (otherwise the rest of that wave would be dropped - the boss then follows as soon as it has).
        /// If the waves were already cleared the normal completion path has started the encounter itself.</summary>
        private void TryStartBossFromCountdown()
        {
            if (!_bossCountdownExpired || !HasBossEncounter || EncounterPhase != BossEncounterPhase.Pending)
            {
                return;
            }

            if (CurrentWaveIndex < TotalWaveCount - 1 || CurrentState != WaveState.WaitingForRemainingEnemies)
            {
                return;
            }

            StartScheduler(BossEncounterRoutine(0f, raiseWaveStarted: false));
        }

        private void StopBossCountdown()
        {
            _bossCountdownTicking = false;
            if (BossCountdownRemaining > 0f)
            {
                SetBossCountdown(0f);
            }
        }

        /// <summary>When on, the boss group is spawned frozen and waits for ActivateBossEncounter (called by the
        /// intro cinematic when it hands control back). When off, it is released the moment it spawns.</summary>
        public void SetBossIntroHold(bool hold)
        {
            _holdBossEncounterForIntro = hold;
        }

        /// <summary>Releases the frozen boss group. Safe to call more than once.</summary>
        public void ActivateBossEncounter()
        {
            if (EncounterPhase != BossEncounterPhase.Intro)
            {
                return;
            }

            for (int i = 0; i < _bossEncounterGroup.Count; i++)
            {
                EnemyController enemy = _bossEncounterGroup[i];
                if (enemy != null && !enemy.IsCombatActive)
                {
                    enemy.SetCombatActive(true);
                }
            }

            if (_bossEncounterBoss != null && _bossEncounterBoss.BossController != null)
            {
                _bossEncounterBoss.BossController.SetBehaviorPaused(false);
            }

            EncounterPhase = BossEncounterPhase.Fight;
            BossEncounterActivated?.Invoke();
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
            _bossEncounterRunning = false;
            _bossEncounterGroup.Clear();
            _bossEncounterBoss = null;
            _carriedRunId = -1;
            EncounterPhase = HasBossEncounter ? BossEncounterPhase.Pending : BossEncounterPhase.None;
            ResetBossCountdown();
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
            if (CurrentWaveIndex == 0 && HasBossCountdown && EncounterPhase == BossEncounterPhase.Pending)
            {
                _bossCountdownTicking = true; // counts from the first wave starting
            }

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

            // The countdown may have run out while this wave was still spawning.
            if (runId == _waveRunId)
            {
                TryStartBossFromCountdown();
            }
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

        private static readonly ProfilerMarker SpawnMarker = new ProfilerMarker("AlienDefense.WaveController.SpawnEnemy");

        private EnemyController SpawnTrackedEnemy(EnemyDefinition definition, int runId)
        {
            using (SpawnMarker.Auto())
            {
                return SpawnTrackedEnemyCore(definition, runId);
            }
        }

        private EnemyController SpawnTrackedEnemyCore(EnemyDefinition definition, int runId)
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

                bool carriedIntoBossEncounter = _bossEncounterRunning && runId == _carriedRunId;
                if (runId != _waveRunId && !carriedIntoBossEncounter)
                {
                    return;
                }

                _tracker.RecordEnemyResolved();
                RaiseProgressChanged();

                // Killing the boss wins the level on the spot, whatever is still walking - LevelCompositionRoot
                // clears the stragglers. Only a real kill counts: a boss that walks into the base does not win.
                if (_bossEncounterRunning && resolvedEnemy == _bossEncounterBoss
                    && (reason == EnemyResolveReason.Defeated || reason == EnemyResolveReason.Captured))
                {
                    CompleteBossEncounter();
                    return;
                }

                TryCompleteWave(_waveRunId);
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

            // The level's own strength scale sits on top (the speed cap only limits per-wave growth).
            hpMul *= LevelScale(_spawnSettings.LevelHealthMultiplier);
            spdMul *= LevelScale(_spawnSettings.LevelSpeedMultiplier);
            dmgMul *= LevelScale(_spawnSettings.LevelDamageMultiplier);
            return new EnemySpawnModifiers(hpMul, spdMul, dmgMul);
        }

        private static float LevelScale(float value)
        {
            return value > 0f ? value : 1f;
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

            if (_bossEncounterRunning)
            {
                // The boss group (and anything the boss summoned) is gone - that closes the level.
                _bossEncounterRunning = false;
                _bossEncounterGroup.Clear();
                _bossEncounterBoss = null;
                EncounterPhase = BossEncounterPhase.Cleared;
                FireAllWavesCompleted();
                return;
            }

            WaveCompleted?.Invoke(CurrentWaveNumber);

            if (CurrentWaveIndex >= TotalWaveCount - 1)
            {
                if (HasBossEncounter && EncounterPhase == BossEncounterPhase.Pending)
                {
                    StartScheduler(BossEncounterRoutine(_bossEncounter.DelayAfterNormalWaves, raiseWaveStarted: false));
                    return;
                }

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

        /// <summary>Closes the level from the boss kill: the encounter counts as cleared even with escorts,
        /// summoned minions or earlier stragglers still alive. Anything still tracked resolves harmlessly afterwards -
        /// TryCompleteWave ignores a run that is already Completed.</summary>
        private void CompleteBossEncounter()
        {
            if (CurrentState == WaveState.Completed && !_bossEncounterRunning)
            {
                return;
            }

            CurrentState = WaveState.Completed;
            _bossEncounterRunning = false;
            _bossEncounterGroup.Clear();
            _bossEncounterBoss = null;
            EncounterPhase = BossEncounterPhase.Cleared;
            StopBossCountdown();
            FireAllWavesCompleted();
        }

        private void FireAllWavesCompleted()
        {
            if (_allWavesCompletedFired)
            {
                return;
            }

            _allWavesCompletedFired = true;
            AllWavesCompleted?.Invoke();
        }

        /// <summary>Spawns the boss + escorts in their formation in one frame, tracked as one extra "wave" whose
        /// completion ends the level. Held frozen if an intro asked for it.</summary>
        private IEnumerator BossEncounterRoutine(float delay, bool raiseWaveStarted)
        {
            // Everything up to the first yield runs synchronously inside StartScheduler, so the switch to the
            // encounter's run happens in the same frame the decision was made - no normal enemy can resolve
            // against the old run in between and complete it (which would end the level before the boss).
            EncounterPhase = BossEncounterPhase.Spawning;
            StopBossCountdown();

            int carried = _tracker.IsSpawnSchedulingCompleted ? _tracker.ActiveEnemyCount : 0;
            _carriedRunId = carried > 0 ? _waveRunId : -1;

            _waveRunId++;
            int runId = _waveRunId;
            _bossEncounterRunning = true;
            _bossEncounterGroup.Clear();

            int planned = carried + 1 + _bossEncounter.TotalEscortCount();
            _tracker.Initialize(planned);
            for (int i = 0; i < carried; i++)
            {
                _tracker.RecordSpawnSuccess(); // still-alive normal enemies now count toward the encounter
            }

            CurrentState = WaveState.Spawning;
            RaiseProgressChanged();

            yield return null;
            if (delay > 0f)
            {
                yield return WaitSeconds(delay);
            }

            if (runId != _waveRunId)
            {
                yield break;
            }
            if (raiseWaveStarted)
            {
                WaveStarted?.Invoke(CurrentWaveNumber, TotalWaveCount);
            }

            RaiseProgressChanged();

            bool hold = _holdBossEncounterForIntro;

            // Escorts first, then the boss last: BossSpawned listeners (boss health bar, BossController wiring)
            // then see a fully laid-out group.
            int escortIndex = 0;
            for (int e = 0; e < _bossEncounter.EscortEntryCount; e++)
            {
                EnemySpawnEntry entry = _bossEncounter.GetEscortEntry(e);
                if (entry == null || !entry.IsValid)
                {
                    continue;
                }

                for (int n = 0; n < entry.Count; n++)
                {
                    EnemyController escort = SpawnTrackedEnemy(entry.EnemyDefinition, runId);
                    BossEncounterDefinition.FormationSlot slot = _bossEncounter.GetEscortSlot(escortIndex++);
                    if (escort == null)
                    {
                        continue;
                    }

                    escort.Movement.PlaceAlongPath(slot.DistanceAlongPath, slot.LateralOffset);
                    if (hold)
                    {
                        escort.SetCombatActive(false);
                    }

                    _bossEncounterGroup.Add(escort);
                }
            }

            EnemyController boss = SpawnTrackedEnemy(_bossEncounter.BossDefinition, runId);
            if (boss != null)
            {
                BossEncounterDefinition.FormationSlot bossSlot = _bossEncounter.BossSlot;
                boss.Movement.PlaceAlongPath(bossSlot.DistanceAlongPath, bossSlot.LateralOffset);
                if (hold)
                {
                    boss.SetCombatActive(false);
                }

                if (boss.BossController != null)
                {
                    boss.BossController.SetMinionsEnabled(_bossEncounter.BossMinionsEnabled);
                    boss.BossController.SetBehaviorPaused(hold);
                }

                _bossEncounterGroup.Insert(0, boss);
            }

            _bossEncounterBoss = boss;
            _tracker.MarkSpawnSchedulingCompleted();
            CurrentState = WaveState.WaitingForRemainingEnemies;
            EncounterPhase = hold ? BossEncounterPhase.Intro : BossEncounterPhase.Fight;
            RaiseProgressChanged();
            _schedulingCoroutine = null;

            BossEncounterSpawned?.Invoke(boss, _bossEncounterGroup);
            if (!hold)
            {
                BossEncounterActivated?.Invoke();
            }

            TryCompleteWave(runId);
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
