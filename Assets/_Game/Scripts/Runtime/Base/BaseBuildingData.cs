using System;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>One gate in front of a build or an upgrade. Kept generic on purpose: the popup draws a row per
    /// requirement and the evaluator answers every type, so a new gate is a data edit, not a new code path.</summary>
    [Serializable]
    public sealed class BuildingRequirement
    {
        [SerializeField]
        private BuildingRequirementType _type = BuildingRequirementType.BuildingLevel;

        [SerializeField]
        [Tooltip("Meaning depends on Type: a building id, a campaign level id, or a MetaItemIds value. Leave " +
            "empty for Currency / PlayerLevel / Gems.")]
        private string _targetId;

        [SerializeField, Min(0)]
        private int _requiredValue = 1;

        public BuildingRequirementType Type => _type;
        public string TargetId => _targetId;
        public int RequiredValue => Mathf.Max(0, _requiredValue);

        /// <summary>True when this gate charges the player rather than merely checking them. A Material or
        /// Currency requirement is spent on Start; a BuildingLevel one is not. Keeping the distinction here means
        /// the construction service does not need its own list of "which ones cost something".</summary>
        public bool IsConsumable =>
            _type == BuildingRequirementType.Currency ||
            _type == BuildingRequirementType.Material ||
            _type == BuildingRequirementType.Gems;
    }

    /// <summary>A named stat this level grants, shown in the popup's Upgrade Bonus section. Purely descriptive
    /// plus a value other systems can read by id - nothing here applies itself.</summary>
    [Serializable]
    public sealed class BuildingStat
    {
        [SerializeField]
        [Tooltip("Shown as the row label, e.g. \"Building power\" or \"Tower Damage\".")]
        private string _displayName = "Building power";

        [SerializeField]
        [Tooltip("Stable id other systems match on, e.g. \"tower_damage_percent\". Never shown to the player.")]
        private string _statId;

        [SerializeField]
        private float _value;

        [SerializeField]
        [Tooltip("Appends % to the displayed value. Does not change the number itself.")]
        private bool _isPercent;

        [SerializeField]
        private Sprite _icon;

        public string DisplayName => _displayName;
        public string StatId => _statId;
        public float Value => _value;
        public bool IsPercent => _isPercent;
        public Sprite Icon => _icon;
    }

    /// <summary>Resource generation for a Production building. Interval is in seconds and is what the offline
    /// catch-up divides elapsed real time by.</summary>
    [Serializable]
    public sealed class BuildingProductionConfig
    {
        [SerializeField]
        [Tooltip("Meta item granted per tick. Empty disables production for this level.")]
        private string _materialId;

        [SerializeField, Min(0)]
        private int _amountPerInterval;

        [SerializeField, Min(1f)]
        [Tooltip("Seconds between ticks. Offline time is divided by this, so it must never be 0.")]
        private float _intervalSeconds = 60f;

        [SerializeField, Min(0)]
        [Tooltip("Ceiling on what can pile up while away. 0 disables the cap, which lets an idle base bank " +
            "unlimited material - usually not what you want.")]
        private int _storageCapacity = 100;

        public string MaterialId => _materialId;
        public int AmountPerInterval => Mathf.Max(0, _amountPerInterval);
        public float IntervalSeconds => Mathf.Max(1f, _intervalSeconds);
        public int StorageCapacity => Mathf.Max(0, _storageCapacity);

        public bool IsActive => !string.IsNullOrWhiteSpace(_materialId) && AmountPerInterval > 0;
    }

    /// <summary>One step of the base's guided progression ("Build Patrol Post lvl. 1"). The tracker shows the first
    /// goal that is not yet met.
    ///
    /// Deliberately stateless: progress is read straight from the building's saved level, so there is nothing
    /// extra to save, nothing to migrate, and no way for the tracker to disagree with the base itself. The project
    /// has no general quest system (only the single daily quest), so this is the smallest thing that works rather
    /// than a second quest framework.</summary>
    [Serializable]
    public sealed class BaseBuildingGoal
    {
        [SerializeField]
        private string _buildingId;

        [SerializeField, Min(1)]
        private int _level = 1;

        [SerializeField]
        [Tooltip("Optional. Leave empty to generate \"Build <name> lvl. <level>\".")]
        private string _labelOverride;

        public string BuildingId => _buildingId;
        public int Level => Mathf.Max(1, _level);
        public string LabelOverride => _labelOverride;
    }

    /// <summary>Everything about one level of one building: what it costs to reach, how long it takes, what it
    /// gives, and what it produces afterwards.</summary>
    [Serializable]
    public sealed class BuildingLevelDefinition
    {
        [SerializeField, Min(1)]
        private int _level = 1;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds. The Start button prints this, so it is never written into the label by hand.")]
        private float _constructionSeconds = 2f;

        [SerializeField, Min(0)]
        [Tooltip("Added to the player's Force the moment this level completes.")]
        private int _forceReward;

        [SerializeField]
        [Tooltip("Checked before Start becomes interactable. Consumable ones are also spent on Start.")]
        private BuildingRequirement[] _requirements = Array.Empty<BuildingRequirement>();

        [SerializeField]
        private BuildingStat[] _bonuses = Array.Empty<BuildingStat>();

        [SerializeField]
        private BuildingProductionConfig _production = new BuildingProductionConfig();

        [SerializeField]
        [Tooltip("Optional. Swapped in when this level completes. Leave empty to keep the previous visual.")]
        private GameObject _visualPrefab;

        public int Level => Mathf.Max(1, _level);
        public float ConstructionSeconds => Mathf.Max(0f, _constructionSeconds);
        public int ForceReward => Mathf.Max(0, _forceReward);
        public BuildingRequirement[] Requirements => _requirements ?? Array.Empty<BuildingRequirement>();
        public BuildingStat[] Bonuses => _bonuses ?? Array.Empty<BuildingStat>();
        public BuildingProductionConfig Production => _production;
        public GameObject VisualPrefab => _visualPrefab;
    }
}
