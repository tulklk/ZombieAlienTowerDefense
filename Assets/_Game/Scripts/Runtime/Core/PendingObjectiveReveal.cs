namespace AlienDefense.Core
{
    /// <summary>Application-scope, memory-only note left by a level that was just won: "this level's objective
    /// chests went from N achieved to M achieved". MainMenu consumes it once to play the chest reveal for the newly
    /// achieved ones only. Nothing is saved - the saved progress stays the single source of truth for what the
    /// chests look like - so a menu reload, a tab change or an app restart never replays the reveal.</summary>
    public sealed class PendingObjectiveReveal
    {
        public string LevelId { get; private set; }
        public int PreviousCount { get; private set; }
        public int CurrentCount { get; private set; }

        public bool HasPending => !string.IsNullOrEmpty(LevelId) && CurrentCount > PreviousCount;

        /// <summary>Replaces anything queued. A win that achieved nothing new clears the note instead.</summary>
        public void Set(string levelId, int previousCount, int currentCount)
        {
            if (string.IsNullOrEmpty(levelId) || currentCount <= previousCount)
            {
                Clear();
                return;
            }

            LevelId = levelId;
            PreviousCount = previousCount < 0 ? 0 : previousCount;
            CurrentCount = currentCount;
        }

        /// <summary>Hands the note over and empties it, but only to the level it was written for.</summary>
        public bool TryConsume(string levelId, out int previousCount, out int currentCount)
        {
            previousCount = 0;
            currentCount = 0;
            if (!HasPending || LevelId != levelId)
            {
                return false;
            }

            previousCount = PreviousCount;
            currentCount = CurrentCount;
            Clear();
            return true;
        }

        public void Clear()
        {
            LevelId = null;
            PreviousCount = 0;
            CurrentCount = 0;
        }
    }
}
