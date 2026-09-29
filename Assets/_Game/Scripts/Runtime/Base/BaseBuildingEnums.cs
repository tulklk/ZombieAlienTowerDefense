namespace AlienDefense.Base
{
    /// <summary>What a base building is for. Drives which popup sections show up and nothing else - the actual
    /// numbers all live in BuildingLevelDefinition, so adding a building of an existing type needs no code.
    ///
    /// Deliberately NOT reused from AlienDefense.Building: that namespace is the in-level tower placement system
    /// (BuildNode, BuildService) and has nothing to do with the meta base.</summary>
    public enum BaseBuildingType
    {
        /// <summary>The progression gate every other building checks its level against.</summary>
        Central = 0,

        /// <summary>Generates material over time, collected from the world bubble.</summary>
        Production = 1,

        /// <summary>Unlocks or boosts combat tech.</summary>
        Workshop = 2,

        /// <summary>Unlocks research / passive bonuses.</summary>
        Research = 3,

        /// <summary>Anything else that only grants Force and bonuses.</summary>
        Support = 4
    }

    /// <summary>Lifecycle of one plot. Locked and Available describe an empty plot; the rest describe a building
    /// that exists on it.</summary>
    public enum BaseBuildingState
    {
        /// <summary>Requirements not met yet - the plot is visible but cannot be tapped into a build.</summary>
        Locked = 0,

        /// <summary>Empty and buildable right now.</summary>
        Available = 1,

        /// <summary>First build in progress.</summary>
        Constructing = 2,

        /// <summary>Finished and idle.</summary>
        Built = 3,

        /// <summary>Built, and a level-up is in progress.</summary>
        Upgrading = 4
    }

    /// <summary>The kinds of gate a building level can put in front of itself. One evaluator handles all of them
    /// (see BuildingRequirementEvaluator) so a new requirement never means a new popup code path.</summary>
    public enum BuildingRequirementType
    {
        /// <summary>Another base building must be at least this level. TargetId = that building's id.</summary>
        BuildingLevel = 0,

        /// <summary>A campaign level must be completed. TargetId = level id.</summary>
        CampaignLevel = 1,

        /// <summary>Meta currency. TargetId unused.</summary>
        Currency = 2,

        /// <summary>A meta inventory item. TargetId = MetaItemIds value.</summary>
        Material = 3,

        /// <summary>Player display level. TargetId unused.</summary>
        PlayerLevel = 4,

        /// <summary>Gems. TargetId unused.</summary>
        Gems = 5
    }
}
