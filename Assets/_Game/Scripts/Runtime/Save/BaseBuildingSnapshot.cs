using System;
using AlienDefense.Base;

namespace AlienDefense.Save
{
    /// <summary>Immutable copy of one base building row, following the same pattern as LevelProgressSnapshot and
    /// the inventory snapshots: callers read and cache freely without being able to edit the save behind the
    /// service's back.</summary>
    public readonly struct BaseBuildingSnapshot
    {
        public readonly string BuildingId;
        public readonly int Level;
        public readonly BaseBuildingState State;
        public readonly long ConstructionStartUtcTicks;
        public readonly long ConstructionCompleteUtcTicks;
        public readonly long LastCollectUtcTicks;

        public BaseBuildingSnapshot(string buildingId, int level, BaseBuildingState state,
            long constructionStartUtcTicks, long constructionCompleteUtcTicks, long lastCollectUtcTicks)
        {
            BuildingId = buildingId;
            Level = level;
            State = state;
            ConstructionStartUtcTicks = constructionStartUtcTicks;
            ConstructionCompleteUtcTicks = constructionCompleteUtcTicks;
            LastCollectUtcTicks = lastCollectUtcTicks;
        }

        public bool Exists => !string.IsNullOrEmpty(BuildingId);

        public bool IsBuilding => State == BaseBuildingState.Constructing || State == BaseBuildingState.Upgrading;

        /// <summary>True once the clock has passed the stored completion instant. This is what makes a build that
        /// finished while the game was closed complete on the next open, with no ticking involved.</summary>
        public bool IsConstructionFinished(DateTime nowUtc)
        {
            return IsBuilding && ConstructionCompleteUtcTicks > 0 && nowUtc.Ticks >= ConstructionCompleteUtcTicks;
        }

        public TimeSpan GetRemaining(DateTime nowUtc)
        {
            if (!IsBuilding || ConstructionCompleteUtcTicks <= 0)
            {
                return TimeSpan.Zero;
            }

            long remaining = ConstructionCompleteUtcTicks - nowUtc.Ticks;
            return remaining <= 0 ? TimeSpan.Zero : TimeSpan.FromTicks(remaining);
        }

        /// <summary>0..1 across the whole build. Guards a zero-length build so the bar never divides by zero.</summary>
        public float GetProgress01(DateTime nowUtc)
        {
            if (!IsBuilding || ConstructionCompleteUtcTicks <= 0)
            {
                return 0f;
            }

            long total = ConstructionCompleteUtcTicks - ConstructionStartUtcTicks;
            if (total <= 0)
            {
                return 1f;
            }

            long elapsed = nowUtc.Ticks - ConstructionStartUtcTicks;
            if (elapsed <= 0)
            {
                return 0f;
            }

            return elapsed >= total ? 1f : (float)((double)elapsed / total);
        }
    }
}
