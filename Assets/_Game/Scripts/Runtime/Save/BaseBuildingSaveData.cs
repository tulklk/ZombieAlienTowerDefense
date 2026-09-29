using System;

namespace AlienDefense.Save
{
    /// <summary>Runtime state of one base building. Plain data for JsonUtility - the definition (name, costs,
    /// prefabs) is looked up from BaseBuildingCatalog by BuildingId and never serialized here.
    ///
    /// Construction uses absolute UTC ticks, not a countdown: the player can close the game mid-build, and a
    /// remaining-seconds field would either freeze or have to be ticked, both of which are wrong. Comparing
    /// CompleteUtcTicks against DateTime.UtcNow makes offline completion fall out for free.</summary>
    [Serializable]
    public sealed class BaseBuildingSaveData
    {
        public string BuildingId;

        /// <summary>0 = not built yet. The build that is running raises this only on completion.</summary>
        public int Level;

        /// <summary>BaseBuildingState as an int.</summary>
        public int State;

        public long ConstructionStartUtcTicks;

        /// <summary>When the running build finishes. 0 when nothing is running.</summary>
        public long ConstructionCompleteUtcTicks;

        /// <summary>Anchor for offline production. Everything produced since this instant is owed to the player.</summary>
        public long LastCollectUtcTicks;
    }
}
