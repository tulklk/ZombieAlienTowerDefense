using System;

namespace AlienDefense.Save
{
    /// <summary>Serializable tutorial completion flag. Phase 17 will expand this if per-step tracking is needed.</summary>
    [Serializable]
    public sealed class TutorialProgressSaveData
    {
        public bool IsCompleted;
    }
}
