#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define PERF_MONITOR_ENABLED
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AlienDefense.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.DebugTools.Performance
{
    /// <summary>Development-only frame recorder for the gameplay benchmark: it samples frame time, CPU/GPU timings,
    /// GC, memory, rendering counters and this game's own load counters (enemies, projectiles, VFX, towers) once per
    /// frame, splits them into named phases (Idle / Normal Combat / Heavy Wave / Boss Battle / Boss Death / Victory)
    /// and writes a JSON + CSV + text report when the run ends.
    ///
    /// The whole class is compiled out of release builds (UNITY_EDITOR || DEVELOPMENT_BUILD) and creates itself, so
    /// no scene, prefab or gameplay script references it and nothing has to be wired by hand. It never touches game
    /// state: every game counter it reads is an existing read-only property.
    ///
    /// Cost per frame is a handful of ProfilerRecorder reads plus writes into pre-allocated arrays - no allocation,
    /// no string building, no Find calls. Counters that a given Unity version or platform does not provide are
    /// detected once and reported as "Unavailable" instead of throwing.</summary>
    public sealed class RuntimePerformanceMonitor : MonoBehaviour
    {
#if PERF_MONITOR_ENABLED
        public const string IdlePhase = "Idle";
        public const string NormalCombatPhase = "Normal Combat";
        public const string HeavyWavePhase = "Heavy Wave";
        public const string BossBattlePhase = "Boss Battle";
        public const string BossDeathPhase = "Boss Death / Cinematic";
        public const string VictoryPhase = "Victory Panel";
        public const string OtherPhase = "Menu / Loading";

        private const int MaxFrames = 120000;          // ~33 min at 60 FPS; the recorder stops rather than grows
        private const float GameCounterInterval = 0.2f; // 5 Hz is plenty for enemy/projectile/VFX counts
        private const int HeavyWaveEnemyThreshold = 12;

        public static RuntimePerformanceMonitor Instance { get; private set; }

        /// <summary>Frame samples, kept in parallel arrays so a long run allocates once.</summary>
        private double[] _frameMs;
        private double[] _mainThreadMs;
        private double[] _renderThreadMs;
        private double[] _gpuMs;
        private long[] _gcAllocBytes;
        private long[] _totalMemory;
        private long[] _gcUsedMemory;
        private int[] _drawCalls;
        private int[] _batches;
        private int[] _setPassCalls;
        private long[] _triangles;
        private long[] _vertices;
        private int[] _shadowCasters;
        private int[] _enemies;
        private int[] _projectiles;
        private int[] _vfx;
        private int[] _towers;
        private byte[] _phaseIds;

        private readonly List<string> _phaseNames = new List<string>();
        private readonly Dictionary<string, byte> _phaseIdsByName = new Dictionary<string, byte>();
        private readonly List<MemorySample> _memoryMarks = new List<MemorySample>();

        private readonly Dictionary<string, ProfilerRecorder> _recorders = new Dictionary<string, ProfilerRecorder>();
        private readonly List<string> _unavailableCounters = new List<string>();

        private int _frameCount;
        private bool _recording;
        private string _currentPhase = OtherPhase;
        private byte _currentPhaseId;
        private float _startRealtime;
        private float _nextGameCounterSample;
        private int _lastEnemies, _lastProjectiles, _lastVfx, _lastTowers;
        private LevelCompositionRoot _level;
        private bool _exported;
        private string _lastReportFolder;

        private readonly struct MemorySample
        {
            public readonly string Label;
            public readonly long TotalBytes;
            public readonly long GcUsedBytes;
            public readonly float Seconds;

            public MemorySample(string label, long totalBytes, long gcUsedBytes, float seconds)
            {
                Label = label;
                TotalBytes = totalBytes;
                GcUsedBytes = gcUsedBytes;
                Seconds = seconds;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("[RuntimePerformanceMonitor]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<RuntimePerformanceMonitor>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AllocateBuffers();
            CreateRecorders();
        }

        private void AllocateBuffers()
        {
            _frameMs = new double[MaxFrames];
            _mainThreadMs = new double[MaxFrames];
            _renderThreadMs = new double[MaxFrames];
            _gpuMs = new double[MaxFrames];
            _gcAllocBytes = new long[MaxFrames];
            _totalMemory = new long[MaxFrames];
            _gcUsedMemory = new long[MaxFrames];
            _drawCalls = new int[MaxFrames];
            _batches = new int[MaxFrames];
            _setPassCalls = new int[MaxFrames];
            _triangles = new long[MaxFrames];
            _vertices = new long[MaxFrames];
            _shadowCasters = new int[MaxFrames];
            _enemies = new int[MaxFrames];
            _projectiles = new int[MaxFrames];
            _vfx = new int[MaxFrames];
            _towers = new int[MaxFrames];
            _phaseIds = new byte[MaxFrames];
        }

        /// <summary>Opens one recorder per counter, keeping only the ones this Unity version and platform actually
        /// provide. Counter names differ between versions, so each is probed and the misses are reported once.</summary>
        private void CreateRecorders()
        {
            TryAddRecorder(ProfilerCategory.Internal, "Main Thread");
            TryAddRecorder(ProfilerCategory.Internal, "Render Thread");
            TryAddRecorder(ProfilerCategory.Internal, "GPU Frame Time");
            TryAddRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            TryAddRecorder(ProfilerCategory.Memory, "GC Reserved Memory");
            TryAddRecorder(ProfilerCategory.Memory, "GC Used Memory");
            TryAddRecorder(ProfilerCategory.Memory, "Total Used Memory");
            TryAddRecorder(ProfilerCategory.Memory, "Total Reserved Memory");
            TryAddRecorder(ProfilerCategory.Memory, "System Used Memory");
            TryAddRecorder(ProfilerCategory.Render, "Draw Calls Count");
            TryAddRecorder(ProfilerCategory.Render, "Batches Count");
            TryAddRecorder(ProfilerCategory.Render, "SetPass Calls Count");
            TryAddRecorder(ProfilerCategory.Render, "Triangles Count");
            TryAddRecorder(ProfilerCategory.Render, "Vertices Count");
            TryAddRecorder(ProfilerCategory.Render, "Shadow Casters Count");
            TryAddRecorder(ProfilerCategory.Physics, "Physics Queries");
            TryAddRecorder(ProfilerCategory.Physics, "Active Rigidbodies");
            TryAddRecorder(ProfilerCategory.Physics, "Active Dynamic Bodies");
        }

        private void TryAddRecorder(ProfilerCategory category, string counterName)
        {
            try
            {
                var recorder = ProfilerRecorder.StartNew(category, counterName);
                if (recorder.Valid)
                {
                    _recorders[counterName] = recorder;
                    return;
                }

                recorder.Dispose();
            }
            catch (Exception)
            {
                // A counter this build does not know about: recorded as unavailable, never fatal.
            }

            _unavailableCounters.Add(counterName);
        }

        private long ReadCounter(string counterName)
        {
            return _recorders.TryGetValue(counterName, out ProfilerRecorder recorder) && recorder.Valid
                ? recorder.LastValue
                : -1L;
        }

        // ------------------------------------------------------------------------------------------------------
        // Control
        // ------------------------------------------------------------------------------------------------------

        public static void Begin()
        {
            Instance?.BeginInternal();
        }

        public static void End()
        {
            Instance?.EndInternal();
        }

        /// <summary>Overrides the automatic phase detection until the next call (or until gameplay detection takes
        /// over again on the next scene).</summary>
        public static void SetPhase(string phase)
        {
            Instance?.SetPhaseInternal(phase, manual: true);
        }

        private void BeginInternal()
        {
            _frameCount = 0;
            _exported = false;
            _memoryMarks.Clear();
            _phaseNames.Clear();
            _phaseIdsByName.Clear();
            _currentPhaseId = PhaseId(_currentPhase);
            _startRealtime = Time.realtimeSinceStartup;
            _recording = true;
            MarkMemory("Test Start");
            UnityEngine.Debug.Log("[Perf] Recording started. Press F10 (or call RuntimePerformanceMonitor.End()) to stop and export.");
        }

        private void EndInternal()
        {
            if (!_recording)
            {
                return;
            }

            _recording = false;
            MarkMemory("End of Test");
            Export();
        }

        private void SetPhaseInternal(string phase, bool manual)
        {
            if (string.IsNullOrEmpty(phase) || phase == _currentPhase)
            {
                return;
            }

            _currentPhase = phase;
            _currentPhaseId = PhaseId(phase);
            if (_recording)
            {
                MarkMemory(phase + (manual ? " (manual)" : string.Empty));
            }
        }

        private byte PhaseId(string phase)
        {
            if (_phaseIdsByName.TryGetValue(phase, out byte id))
            {
                return id;
            }

            id = (byte)_phaseNames.Count;
            _phaseNames.Add(phase);
            _phaseIdsByName[phase] = id;
            return id;
        }

        private void MarkMemory(string label)
        {
            _memoryMarks.Add(new MemorySample(label, ReadCounter("Total Used Memory"), ReadCounter("GC Used Memory"),
                Time.realtimeSinceStartup - _startRealtime));
        }

        // ------------------------------------------------------------------------------------------------------
        // Sampling
        // ------------------------------------------------------------------------------------------------------

        private void Update()
        {
            HandleHotkeys();

            if (!_recording)
            {
                return;
            }

            DetectPhase();
            SampleGameCounters();
            RecordFrame();
        }

        private void HandleHotkeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                BeginInternal();
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.F10))
            {
                EndInternal();
            }
        }

        /// <summary>Phases come from the game's own state, so a run needs no manual marking: the level's GameFlow,
        /// its boss encounter phase and the live enemy count are enough to tell the six phases apart.</summary>
        private void DetectPhase()
        {
            if (_level == null)
            {
                _level = FindFirstObjectByType<LevelCompositionRoot>(); // only while no level is known, not per frame
                if (_level == null)
                {
                    SetPhaseInternal(OtherPhase, manual: false);
                    return;
                }
            }

            GameState state = _level.GameFlow != null ? _level.GameFlow.CurrentState : GameState.Initializing;
            if (state == GameState.Victory)
            {
                SetPhaseInternal(VictoryPhase, manual: false);
                return;
            }

            if (_level.VictoryCinematicIsPlaying)
            {
                SetPhaseInternal(BossDeathPhase, manual: false);
                return;
            }

            if (_level.BossEncounterIsFighting)
            {
                SetPhaseInternal(BossBattlePhase, manual: false);
                return;
            }

            int enemies = _lastEnemies;
            if (enemies >= HeavyWaveEnemyThreshold)
            {
                SetPhaseInternal(HeavyWavePhase, manual: false);
            }
            else if (enemies > 0)
            {
                SetPhaseInternal(NormalCombatPhase, manual: false);
            }
            else
            {
                SetPhaseInternal(IdlePhase, manual: false);
            }
        }

        private void SampleGameCounters()
        {
            if (Time.realtimeSinceStartup < _nextGameCounterSample)
            {
                return;
            }

            _nextGameCounterSample = Time.realtimeSinceStartup + GameCounterInterval;
            if (_level == null)
            {
                _lastEnemies = _lastProjectiles = _lastVfx = _lastTowers = 0;
                return;
            }

            _lastEnemies = _level.Enemies != null ? _level.Enemies.Count : 0;
            _lastProjectiles = _level.ActiveProjectileCount;
            _lastVfx = _level.ActiveVfxCount;
            _lastTowers = _level.ActiveTowerCount;
        }

        private void RecordFrame()
        {
            if (_frameCount >= MaxFrames)
            {
                UnityEngine.Debug.LogWarning("[Perf] Frame buffer full; stopping the recording and exporting.");
                EndInternal();
                return;
            }

            int i = _frameCount++;
            _frameMs[i] = Time.unscaledDeltaTime * 1000.0;
            _mainThreadMs[i] = NanosToMs(ReadCounter("Main Thread"));
            _renderThreadMs[i] = NanosToMs(ReadCounter("Render Thread"));
            _gpuMs[i] = NanosToMs(ReadCounter("GPU Frame Time"));
            _gcAllocBytes[i] = ReadCounter("GC Allocated In Frame");
            _totalMemory[i] = ReadCounter("Total Used Memory");
            _gcUsedMemory[i] = ReadCounter("GC Used Memory");
            _drawCalls[i] = (int)ReadCounter("Draw Calls Count");
            _batches[i] = (int)ReadCounter("Batches Count");
            _setPassCalls[i] = (int)ReadCounter("SetPass Calls Count");
            _triangles[i] = ReadCounter("Triangles Count");
            _vertices[i] = ReadCounter("Vertices Count");
            _shadowCasters[i] = (int)ReadCounter("Shadow Casters Count");
            _enemies[i] = _lastEnemies;
            _projectiles[i] = _lastProjectiles;
            _vfx[i] = _lastVfx;
            _towers[i] = _lastTowers;
            _phaseIds[i] = _currentPhaseId;
        }

        private static double NanosToMs(long nanoseconds)
        {
            return nanoseconds < 0 ? -1.0 : nanoseconds * 1e-6;
        }

        // ------------------------------------------------------------------------------------------------------
        // Export
        // ------------------------------------------------------------------------------------------------------

        private void Export()
        {
            if (_exported || _frameCount == 0)
            {
                UnityEngine.Debug.LogWarning("[Perf] Nothing recorded; no report written.");
                return;
            }

            _exported = true;
            string folder = Path.Combine(Application.persistentDataPath, "PerfReports",
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            _lastReportFolder = folder;

            var overall = PhaseStats.Build("ALL", this, 0, _frameCount);
            var perPhase = new List<PhaseStats>();
            for (byte id = 0; id < _phaseNames.Count; id++)
            {
                PhaseStats stats = PhaseStats.BuildForPhase(_phaseNames[id], this, id);
                if (stats.Frames > 0)
                {
                    perPhase.Add(stats);
                }
            }

            File.WriteAllText(Path.Combine(folder, "PerformanceSummary.txt"), BuildSummary(overall, perPhase));
            File.WriteAllText(Path.Combine(folder, "PerformanceReport.json"), BuildJson(overall, perPhase));
            WriteCsv(Path.Combine(folder, "PerformanceFrames.csv"));

            UnityEngine.Debug.Log($"[Perf] Report written to: {folder}\n" +
                $"  PerformanceSummary.txt\n  PerformanceReport.json\n  PerformanceFrames.csv\n" +
                $"  {_frameCount} frames, {overall.AverageFps:F1} avg FPS, {overall.OnePercentLowFps:F1} 1% low.");
        }

        private void WriteCsv(string path)
        {
            var builder = new StringBuilder(_frameCount * 80);
            builder.AppendLine("frame,phase,frame_ms,main_thread_ms,render_thread_ms,gpu_ms,gc_alloc_bytes," +
                "total_memory_bytes,gc_used_bytes,draw_calls,batches,setpass,triangles,vertices,shadow_casters," +
                "enemies,projectiles,vfx,towers");

            var culture = CultureInfo.InvariantCulture;
            for (int i = 0; i < _frameCount; i++)
            {
                builder.Append(i).Append(',')
                    .Append(_phaseNames[_phaseIds[i]]).Append(',')
                    .Append(_frameMs[i].ToString("F3", culture)).Append(',')
                    .Append(_mainThreadMs[i].ToString("F3", culture)).Append(',')
                    .Append(_renderThreadMs[i].ToString("F3", culture)).Append(',')
                    .Append(_gpuMs[i].ToString("F3", culture)).Append(',')
                    .Append(_gcAllocBytes[i]).Append(',')
                    .Append(_totalMemory[i]).Append(',')
                    .Append(_gcUsedMemory[i]).Append(',')
                    .Append(_drawCalls[i]).Append(',')
                    .Append(_batches[i]).Append(',')
                    .Append(_setPassCalls[i]).Append(',')
                    .Append(_triangles[i]).Append(',')
                    .Append(_vertices[i]).Append(',')
                    .Append(_shadowCasters[i]).Append(',')
                    .Append(_enemies[i]).Append(',')
                    .Append(_projectiles[i]).Append(',')
                    .Append(_vfx[i]).Append(',')
                    .Append(_towers[i]).Append('\n');
            }

            File.WriteAllText(path, builder.ToString());
        }

        private string BuildSummary(PhaseStats overall, List<PhaseStats> perPhase)
        {
            var text = new StringBuilder(4096);
            text.AppendLine("========================================");
            text.AppendLine("TOWER DEFENSE PERFORMANCE REPORT");
            text.AppendLine("========================================");
            text.AppendLine($"Source          : {(Application.isEditor ? "UNITY EDITOR (not device performance)" : "DEVICE / PLAYER BUILD")}");
            text.AppendLine($"Platform        : {Application.platform}");
            text.AppendLine($"Unity           : {Application.unityVersion}");
            text.AppendLine($"Device          : {SystemInfo.deviceModel} / {SystemInfo.processorType} / {SystemInfo.graphicsDeviceName}");
            text.AppendLine($"Graphics API    : {SystemInfo.graphicsDeviceType}");
            text.AppendLine($"Screen          : {Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio.value:F0} Hz");
            text.AppendLine($"vSyncCount      : {QualitySettings.vSyncCount}   targetFrameRate: {Application.targetFrameRate}");
            text.AppendLine($"Quality level   : {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            text.AppendLine($"Scene           : {SceneManager.GetActiveScene().name}");
            text.AppendLine($"Test duration   : {overall.DurationSeconds:F1} s");
            text.AppendLine($"Total frames    : {overall.Frames}");
            if (_unavailableCounters.Count > 0)
            {
                text.AppendLine($"Unavailable     : {string.Join(", ", _unavailableCounters)}");
            }

            text.AppendLine();
            AppendPhaseBlock(text, overall);

            foreach (PhaseStats phase in perPhase)
            {
                text.AppendLine();
                text.AppendLine("========================================");
                text.AppendLine($"PHASE: {phase.Name}");
                text.AppendLine("========================================");
                AppendPhaseBlock(text, phase);
            }

            text.AppendLine();
            text.AppendLine("----------------------------------------");
            text.AppendLine("MEMORY MARKS");
            text.AppendLine("----------------------------------------");
            foreach (MemorySample mark in _memoryMarks)
            {
                text.AppendLine($"{mark.Seconds,7:F1}s  {mark.Label,-28} total={Mb(mark.TotalBytes)}  gcUsed={Mb(mark.GcUsedBytes)}");
            }

            text.AppendLine();
            text.AppendLine("----------------------------------------");
            text.AppendLine("10 WORST FRAMES");
            text.AppendLine("----------------------------------------");
            foreach (int index in WorstFrames(10))
            {
                text.AppendLine($"frame {index,6}  {_frameMs[index],7:F2} ms  phase={_phaseNames[_phaseIds[index]],-24} " +
                    $"enemies={_enemies[index],3} proj={_projectiles[index],3} vfx={_vfx[index],3} " +
                    $"gcAlloc={_gcAllocBytes[index],9} B  draws={_drawCalls[index],5}");
            }

            return text.ToString();
        }

        private static void AppendPhaseBlock(StringBuilder text, PhaseStats s)
        {
            text.AppendLine("----------------------------------------");
            text.AppendLine("FRAME PERFORMANCE");
            text.AppendLine("----------------------------------------");
            text.AppendLine($"Frames            : {s.Frames}  ({s.DurationSeconds:F1} s)");
            text.AppendLine($"Average FPS       : {s.AverageFps:F1}");
            text.AppendLine($"Median FPS        : {s.MedianFps:F1}");
            text.AppendLine($"Min / Max FPS     : {s.MinFps:F1} / {s.MaxFps:F1}");
            text.AppendLine($"1% low FPS        : {s.OnePercentLowFps:F1}");
            text.AppendLine($"0.1% low FPS      : {(s.PointOnePercentLowFps > 0 ? s.PointOnePercentLowFps.ToString("F1") : "n/a (too few frames)")}");
            text.AppendLine($"Average frame     : {s.AverageFrameMs:F2} ms");
            text.AppendLine($"Median frame      : {s.MedianFrameMs:F2} ms");
            text.AppendLine($"P95 / P99 frame   : {s.P95FrameMs:F2} / {s.P99FrameMs:F2} ms");
            text.AppendLine($"Worst frame       : {s.MaxFrameMs:F2} ms");
            text.AppendLine();
            text.AppendLine("Frame budget breaches:");
            text.AppendLine($"  >16.67 ms : {s.Over1667,6} ({Percent(s.Over1667, s.Frames)})");
            text.AppendLine($"  >20 ms    : {s.Over20,6} ({Percent(s.Over20, s.Frames)})");
            text.AppendLine($"  >25 ms    : {s.Over25,6} ({Percent(s.Over25, s.Frames)})");
            text.AppendLine($"  >33.33 ms : {s.Over3333,6} ({Percent(s.Over3333, s.Frames)})");
            text.AppendLine($"  >50 ms    : {s.Over50,6} ({Percent(s.Over50, s.Frames)})");
            text.AppendLine($"  >100 ms   : {s.Over100,6} ({Percent(s.Over100, s.Frames)})");
            text.AppendLine();
            text.AppendLine("----------------------------------------");
            text.AppendLine("CPU / GPU");
            text.AppendLine("----------------------------------------");
            text.AppendLine($"Main thread avg / max   : {Ms(s.MainThreadAvgMs)} / {Ms(s.MainThreadMaxMs)}");
            text.AppendLine($"Render thread avg / max : {Ms(s.RenderThreadAvgMs)} / {Ms(s.RenderThreadMaxMs)}");
            text.AppendLine($"GPU avg / max           : {Ms(s.GpuAvgMs)} / {Ms(s.GpuMaxMs)}");
            text.AppendLine();
            text.AppendLine("----------------------------------------");
            text.AppendLine("GC");
            text.AppendLine("----------------------------------------");
            text.AppendLine($"Average alloc / frame : {Bytes(s.GcAvgBytes)}");
            text.AppendLine($"Peak alloc / frame    : {Bytes(s.GcMaxBytes)}");
            text.AppendLine($"Total during phase    : {Bytes(s.GcTotalBytes)}");
            text.AppendLine($"Frames allocating     : {s.GcFrames} ({Percent(s.GcFrames, s.Frames)})");
            text.AppendLine();
            text.AppendLine("----------------------------------------");
            text.AppendLine("MEMORY / RENDERING / LOAD");
            text.AppendLine("----------------------------------------");
            text.AppendLine($"Total memory avg / peak : {Mb((long)s.MemoryAvgBytes)} / {Mb(s.MemoryPeakBytes)}");
            text.AppendLine($"Draw calls avg / peak   : {Num(s.DrawCallsAvg)} / {Num(s.DrawCallsMax)}");
            text.AppendLine($"Batches avg / peak      : {Num(s.BatchesAvg)} / {Num(s.BatchesMax)}");
            text.AppendLine($"SetPass avg / peak      : {Num(s.SetPassAvg)} / {Num(s.SetPassMax)}");
            text.AppendLine($"Triangles avg / peak    : {Num(s.TrianglesAvg)} / {Num(s.TrianglesMax)}");
            text.AppendLine($"Vertices avg / peak     : {Num(s.VerticesAvg)} / {Num(s.VerticesMax)}");
            text.AppendLine($"Shadow casters avg/peak : {Num(s.ShadowCastersAvg)} / {Num(s.ShadowCastersMax)}");
            text.AppendLine($"Enemies avg / peak      : {s.EnemiesAvg:F1} / {s.EnemiesMax}");
            text.AppendLine($"Projectiles avg / peak  : {s.ProjectilesAvg:F1} / {s.ProjectilesMax}");
            text.AppendLine($"VFX avg / peak          : {s.VfxAvg:F1} / {s.VfxMax}");
            text.AppendLine($"Towers avg / peak       : {s.TowersAvg:F1} / {s.TowersMax}");
        }

        private string BuildJson(PhaseStats overall, List<PhaseStats> perPhase)
        {
            var json = new StringBuilder(8192);
            json.Append("{\n");
            json.Append($"  \"source\": \"{(Application.isEditor ? "UNITY_EDITOR" : "PLAYER_BUILD")}\",\n");
            json.Append($"  \"platform\": \"{Application.platform}\",\n");
            json.Append($"  \"unityVersion\": \"{Application.unityVersion}\",\n");
            json.Append($"  \"device\": \"{Escape(SystemInfo.deviceModel)}\",\n");
            json.Append($"  \"gpu\": \"{Escape(SystemInfo.graphicsDeviceName)}\",\n");
            json.Append($"  \"graphicsApi\": \"{SystemInfo.graphicsDeviceType}\",\n");
            json.Append($"  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            json.Append($"  \"vSyncCount\": {QualitySettings.vSyncCount},\n");
            json.Append($"  \"targetFrameRate\": {Application.targetFrameRate},\n");
            json.Append($"  \"qualityLevel\": \"{Escape(QualitySettings.names[QualitySettings.GetQualityLevel()])}\",\n");
            json.Append($"  \"scene\": \"{Escape(SceneManager.GetActiveScene().name)}\",\n");
            json.Append("  \"unavailableCounters\": [");
            for (int i = 0; i < _unavailableCounters.Count; i++)
            {
                json.Append(i > 0 ? ", " : string.Empty).Append('"').Append(Escape(_unavailableCounters[i])).Append('"');
            }

            json.Append("],\n");
            json.Append("  \"overall\": ").Append(overall.ToJson()).Append(",\n");
            json.Append("  \"phases\": [\n");
            for (int i = 0; i < perPhase.Count; i++)
            {
                json.Append("    ").Append(perPhase[i].ToJson());
                json.Append(i < perPhase.Count - 1 ? ",\n" : "\n");
            }

            json.Append("  ],\n");
            json.Append("  \"memoryMarks\": [\n");
            for (int i = 0; i < _memoryMarks.Count; i++)
            {
                MemorySample m = _memoryMarks[i];
                json.Append($"    {{ \"label\": \"{Escape(m.Label)}\", \"seconds\": {m.Seconds.ToString("F2", CultureInfo.InvariantCulture)}, " +
                    $"\"totalBytes\": {m.TotalBytes}, \"gcUsedBytes\": {m.GcUsedBytes} }}");
                json.Append(i < _memoryMarks.Count - 1 ? ",\n" : "\n");
            }

            json.Append("  ],\n");
            json.Append("  \"worstFrames\": [\n");
            List<int> worst = WorstFrames(10);
            for (int i = 0; i < worst.Count; i++)
            {
                int f = worst[i];
                json.Append($"    {{ \"frame\": {f}, \"frameMs\": {_frameMs[f].ToString("F2", CultureInfo.InvariantCulture)}, " +
                    $"\"phase\": \"{Escape(_phaseNames[_phaseIds[f]])}\", \"enemies\": {_enemies[f]}, \"projectiles\": {_projectiles[f]}, " +
                    $"\"vfx\": {_vfx[f]}, \"gcAllocBytes\": {_gcAllocBytes[f]}, \"drawCalls\": {_drawCalls[f]} }}");
                json.Append(i < worst.Count - 1 ? ",\n" : "\n");
            }

            json.Append("  ]\n}\n");
            return json.ToString();
        }

        private List<int> WorstFrames(int count)
        {
            var indices = new List<int>(_frameCount);
            for (int i = 0; i < _frameCount; i++)
            {
                indices.Add(i);
            }

            indices.Sort((a, b) => _frameMs[b].CompareTo(_frameMs[a]));
            if (indices.Count > count)
            {
                indices.RemoveRange(count, indices.Count - count);
            }

            return indices;
        }

        private static string Percent(int part, int total) => total > 0 ? (100.0 * part / total).ToString("F2") + "%" : "n/a";
        private static string Ms(double value) => value < 0 ? "Unavailable" : value.ToString("F2") + " ms";
        private static string Num(double value) => value < 0 ? "Unavailable" : value.ToString("F0");
        private static string Bytes(double value) => value < 0 ? "Unavailable" : (value / 1024.0).ToString("F1") + " KB";
        private static string Mb(long value) => value < 0 ? "Unavailable" : (value / (1024.0 * 1024.0)).ToString("F1") + " MB";
        private static string Escape(string value) => value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;

        private void OnApplicationQuit()
        {
            if (_recording)
            {
                EndInternal();
            }

            foreach (ProfilerRecorder recorder in _recorders.Values)
            {
                recorder.Dispose();
            }

            _recorders.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Statistics over one slice of the recording (a phase, or the whole run).</summary>
        private sealed class PhaseStats
        {
            public string Name;
            public int Frames;
            public double DurationSeconds;
            public double AverageFps, MedianFps, MinFps, MaxFps, OnePercentLowFps, PointOnePercentLowFps;
            public double AverageFrameMs, MedianFrameMs, P95FrameMs, P99FrameMs, MaxFrameMs;
            public int Over1667, Over20, Over25, Over3333, Over50, Over100;
            public double MainThreadAvgMs = -1, MainThreadMaxMs = -1, RenderThreadAvgMs = -1, RenderThreadMaxMs = -1;
            public double GpuAvgMs = -1, GpuMaxMs = -1;
            public double GcAvgBytes = -1, GcMaxBytes = -1, GcTotalBytes = -1;
            public int GcFrames;
            public double MemoryAvgBytes = -1;
            public long MemoryPeakBytes = -1;
            public double DrawCallsAvg = -1, BatchesAvg = -1, SetPassAvg = -1, TrianglesAvg = -1, VerticesAvg = -1, ShadowCastersAvg = -1;
            public double DrawCallsMax = -1, BatchesMax = -1, SetPassMax = -1, TrianglesMax = -1, VerticesMax = -1, ShadowCastersMax = -1;
            public double EnemiesAvg, ProjectilesAvg, VfxAvg, TowersAvg;
            public int EnemiesMax, ProjectilesMax, VfxMax, TowersMax;

            public static PhaseStats BuildForPhase(string name, RuntimePerformanceMonitor m, byte phaseId)
            {
                var frames = new List<int>();
                for (int i = 0; i < m._frameCount; i++)
                {
                    if (m._phaseIds[i] == phaseId)
                    {
                        frames.Add(i);
                    }
                }

                return Build(name, m, frames);
            }

            public static PhaseStats Build(string name, RuntimePerformanceMonitor m, int from, int to)
            {
                var frames = new List<int>(to - from);
                for (int i = from; i < to; i++)
                {
                    frames.Add(i);
                }

                return Build(name, m, frames);
            }

            private static PhaseStats Build(string name, RuntimePerformanceMonitor m, List<int> frames)
            {
                var s = new PhaseStats { Name = name, Frames = frames.Count };
                if (frames.Count == 0)
                {
                    return s;
                }

                var sorted = new List<double>(frames.Count);
                double sum = 0, mainSum = 0, renderSum = 0, gpuSum = 0, gcSum = 0, memSum = 0;
                double drawSum = 0, batchSum = 0, setPassSum = 0, triSum = 0, vertSum = 0, shadowSum = 0;
                int mainN = 0, renderN = 0, gpuN = 0, gcN = 0, memN = 0, drawN = 0, batchN = 0, setPassN = 0, triN = 0, vertN = 0, shadowN = 0;
                double enemySum = 0, projSum = 0, vfxSum = 0, towerSum = 0;

                foreach (int i in frames)
                {
                    double ms = m._frameMs[i];
                    sorted.Add(ms);
                    sum += ms;
                    if (ms > 16.67) s.Over1667++;
                    if (ms > 20.0) s.Over20++;
                    if (ms > 25.0) s.Over25++;
                    if (ms > 33.33) s.Over3333++;
                    if (ms > 50.0) s.Over50++;
                    if (ms > 100.0) s.Over100++;

                    Accumulate(m._mainThreadMs[i], ref mainSum, ref mainN, ref s.MainThreadMaxMs);
                    Accumulate(m._renderThreadMs[i], ref renderSum, ref renderN, ref s.RenderThreadMaxMs);
                    Accumulate(m._gpuMs[i], ref gpuSum, ref gpuN, ref s.GpuMaxMs);
                    Accumulate(m._gcAllocBytes[i], ref gcSum, ref gcN, ref s.GcMaxBytes);
                    if (m._gcAllocBytes[i] > 0) s.GcFrames++;

                    if (m._totalMemory[i] >= 0)
                    {
                        memSum += m._totalMemory[i];
                        memN++;
                        if (m._totalMemory[i] > s.MemoryPeakBytes) s.MemoryPeakBytes = m._totalMemory[i];
                    }

                    Accumulate(m._drawCalls[i], ref drawSum, ref drawN, ref s.DrawCallsMax);
                    Accumulate(m._batches[i], ref batchSum, ref batchN, ref s.BatchesMax);
                    Accumulate(m._setPassCalls[i], ref setPassSum, ref setPassN, ref s.SetPassMax);
                    Accumulate(m._triangles[i], ref triSum, ref triN, ref s.TrianglesMax);
                    Accumulate(m._vertices[i], ref vertSum, ref vertN, ref s.VerticesMax);
                    Accumulate(m._shadowCasters[i], ref shadowSum, ref shadowN, ref s.ShadowCastersMax);

                    enemySum += m._enemies[i];
                    projSum += m._projectiles[i];
                    vfxSum += m._vfx[i];
                    towerSum += m._towers[i];
                    if (m._enemies[i] > s.EnemiesMax) s.EnemiesMax = m._enemies[i];
                    if (m._projectiles[i] > s.ProjectilesMax) s.ProjectilesMax = m._projectiles[i];
                    if (m._vfx[i] > s.VfxMax) s.VfxMax = m._vfx[i];
                    if (m._towers[i] > s.TowersMax) s.TowersMax = m._towers[i];
                }

                sorted.Sort();
                s.DurationSeconds = sum / 1000.0;
                s.AverageFrameMs = sum / frames.Count;
                s.MedianFrameMs = Percentile(sorted, 0.50);
                s.P95FrameMs = Percentile(sorted, 0.95);
                s.P99FrameMs = Percentile(sorted, 0.99);
                s.MaxFrameMs = sorted[sorted.Count - 1];
                s.AverageFps = s.AverageFrameMs > 0 ? 1000.0 / s.AverageFrameMs : 0;
                s.MedianFps = s.MedianFrameMs > 0 ? 1000.0 / s.MedianFrameMs : 0;
                s.MinFps = 1000.0 / sorted[sorted.Count - 1];
                s.MaxFps = 1000.0 / Mathf.Max(0.0001f, (float)sorted[0]);
                s.OnePercentLowFps = LowFps(sorted, 0.01);
                s.PointOnePercentLowFps = sorted.Count >= 1000 ? LowFps(sorted, 0.001) : -1;

                s.MainThreadAvgMs = mainN > 0 ? mainSum / mainN : -1;
                s.RenderThreadAvgMs = renderN > 0 ? renderSum / renderN : -1;
                s.GpuAvgMs = gpuN > 0 ? gpuSum / gpuN : -1;
                s.GcAvgBytes = gcN > 0 ? gcSum / gcN : -1;
                s.GcTotalBytes = gcN > 0 ? gcSum : -1;
                s.MemoryAvgBytes = memN > 0 ? memSum / memN : -1;
                s.DrawCallsAvg = drawN > 0 ? drawSum / drawN : -1;
                s.BatchesAvg = batchN > 0 ? batchSum / batchN : -1;
                s.SetPassAvg = setPassN > 0 ? setPassSum / setPassN : -1;
                s.TrianglesAvg = triN > 0 ? triSum / triN : -1;
                s.VerticesAvg = vertN > 0 ? vertSum / vertN : -1;
                s.ShadowCastersAvg = shadowN > 0 ? shadowSum / shadowN : -1;
                s.EnemiesAvg = enemySum / frames.Count;
                s.ProjectilesAvg = projSum / frames.Count;
                s.VfxAvg = vfxSum / frames.Count;
                s.TowersAvg = towerSum / frames.Count;
                return s;
            }

            private static void Accumulate(double value, ref double sum, ref int count, ref double max)
            {
                if (value < 0)
                {
                    return;
                }

                sum += value;
                count++;
                if (value > max)
                {
                    max = value;
                }
            }

            /// <summary>The mean of the slowest share of frames - the usual "1% low" definition.</summary>
            private static double LowFps(List<double> sortedFrameMs, double share)
            {
                int count = Mathf.Max(1, (int)(sortedFrameMs.Count * share));
                double sum = 0;
                for (int i = sortedFrameMs.Count - count; i < sortedFrameMs.Count; i++)
                {
                    sum += sortedFrameMs[i];
                }

                double averageMs = sum / count;
                return averageMs > 0 ? 1000.0 / averageMs : 0;
            }

            private static double Percentile(List<double> sorted, double p)
            {
                int index = Mathf.Clamp(Mathf.CeilToInt((float)(p * sorted.Count)) - 1, 0, sorted.Count - 1);
                return sorted[index];
            }

            public string ToJson()
            {
                var c = CultureInfo.InvariantCulture;
                return "{ " +
                    $"\"phase\": \"{Escape(Name)}\", \"frames\": {Frames}, \"seconds\": {DurationSeconds.ToString("F2", c)}, " +
                    $"\"avgFps\": {AverageFps.ToString("F2", c)}, \"medianFps\": {MedianFps.ToString("F2", c)}, " +
                    $"\"minFps\": {MinFps.ToString("F2", c)}, \"maxFps\": {MaxFps.ToString("F2", c)}, " +
                    $"\"onePercentLowFps\": {OnePercentLowFps.ToString("F2", c)}, \"pointOnePercentLowFps\": {PointOnePercentLowFps.ToString("F2", c)}, " +
                    $"\"avgFrameMs\": {AverageFrameMs.ToString("F3", c)}, \"medianFrameMs\": {MedianFrameMs.ToString("F3", c)}, " +
                    $"\"p95FrameMs\": {P95FrameMs.ToString("F3", c)}, \"p99FrameMs\": {P99FrameMs.ToString("F3", c)}, \"maxFrameMs\": {MaxFrameMs.ToString("F3", c)}, " +
                    $"\"over16_67\": {Over1667}, \"over20\": {Over20}, \"over25\": {Over25}, \"over33_33\": {Over3333}, \"over50\": {Over50}, \"over100\": {Over100}, " +
                    $"\"mainThreadAvgMs\": {MainThreadAvgMs.ToString("F3", c)}, \"mainThreadMaxMs\": {MainThreadMaxMs.ToString("F3", c)}, " +
                    $"\"renderThreadAvgMs\": {RenderThreadAvgMs.ToString("F3", c)}, \"renderThreadMaxMs\": {RenderThreadMaxMs.ToString("F3", c)}, " +
                    $"\"gpuAvgMs\": {GpuAvgMs.ToString("F3", c)}, \"gpuMaxMs\": {GpuMaxMs.ToString("F3", c)}, " +
                    $"\"gcAvgBytesPerFrame\": {GcAvgBytes.ToString("F1", c)}, \"gcMaxBytes\": {GcMaxBytes.ToString("F0", c)}, " +
                    $"\"gcTotalBytes\": {GcTotalBytes.ToString("F0", c)}, \"gcFrames\": {GcFrames}, " +
                    $"\"memoryAvgBytes\": {MemoryAvgBytes.ToString("F0", c)}, \"memoryPeakBytes\": {MemoryPeakBytes}, " +
                    $"\"drawCallsAvg\": {DrawCallsAvg.ToString("F1", c)}, \"drawCallsMax\": {DrawCallsMax.ToString("F0", c)}, " +
                    $"\"batchesAvg\": {BatchesAvg.ToString("F1", c)}, \"batchesMax\": {BatchesMax.ToString("F0", c)}, " +
                    $"\"setPassAvg\": {SetPassAvg.ToString("F1", c)}, \"setPassMax\": {SetPassMax.ToString("F0", c)}, " +
                    $"\"trianglesAvg\": {TrianglesAvg.ToString("F0", c)}, \"trianglesMax\": {TrianglesMax.ToString("F0", c)}, " +
                    $"\"verticesAvg\": {VerticesAvg.ToString("F0", c)}, \"verticesMax\": {VerticesMax.ToString("F0", c)}, " +
                    $"\"shadowCastersAvg\": {ShadowCastersAvg.ToString("F1", c)}, \"shadowCastersMax\": {ShadowCastersMax.ToString("F0", c)}, " +
                    $"\"enemiesAvg\": {EnemiesAvg.ToString("F2", c)}, \"enemiesMax\": {EnemiesMax}, " +
                    $"\"projectilesAvg\": {ProjectilesAvg.ToString("F2", c)}, \"projectilesMax\": {ProjectilesMax}, " +
                    $"\"vfxAvg\": {VfxAvg.ToString("F2", c)}, \"vfxMax\": {VfxMax}, " +
                    $"\"towersAvg\": {TowersAvg.ToString("F2", c)}, \"towersMax\": {TowersMax} }}";
            }
        }
#endif
    }
}
