using UnityEngine;

namespace AlienDefense.Waves
{
    /// <summary>Pure C# counters for one wave's spawn/resolve progress; owns the completion condition.</summary>
    public sealed class WaveRuntimeTracker
    {
        public int PlannedEnemyCount { get; private set; }
        public int SuccessfulSpawnCount { get; private set; }
        public int FailedSpawnCount { get; private set; }
        public int ActiveEnemyCount { get; private set; }
        public int ResolvedEnemyCount { get; private set; }
        public bool IsSpawnSchedulingCompleted { get; private set; }

        public bool IsCompleted => IsSpawnSchedulingCompleted && ActiveEnemyCount == 0;

        public float NormalizedProgress
        {
            get
            {
                if (PlannedEnemyCount <= 0)
                {
                    return 1f;
                }

                int processed = ResolvedEnemyCount + FailedSpawnCount;
                return Mathf.Clamp01((float)processed / PlannedEnemyCount);
            }
        }

        public void Initialize(int plannedEnemyCount)
        {
            if (plannedEnemyCount < 0)
            {
                Debug.LogWarning($"[WaveRuntimeTracker] plannedEnemyCount {plannedEnemyCount} is negative, clamped to 0.");
                plannedEnemyCount = 0;
            }

            PlannedEnemyCount = plannedEnemyCount;
            SuccessfulSpawnCount = 0;
            FailedSpawnCount = 0;
            ActiveEnemyCount = 0;
            ResolvedEnemyCount = 0;
            IsSpawnSchedulingCompleted = false;
        }

        public void RecordSpawnSuccess()
        {
            SuccessfulSpawnCount++;
            ActiveEnemyCount++;
        }

        public void RecordSpawnFailure()
        {
            FailedSpawnCount++;
        }

        public bool RecordEnemyResolved()
        {
            if (ActiveEnemyCount <= 0)
            {
                Debug.LogWarning("[WaveRuntimeTracker] RecordEnemyResolved called with no active enemies; ignoring duplicate/stale call.");
                return false;
            }

            ActiveEnemyCount--;
            ResolvedEnemyCount++;
            return true;
        }

        public void MarkSpawnSchedulingCompleted()
        {
            IsSpawnSchedulingCompleted = true;
        }

        public void Reset()
        {
            Initialize(0);
        }
    }
}
