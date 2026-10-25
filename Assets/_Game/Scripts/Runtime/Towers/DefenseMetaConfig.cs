using System;
using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Config-only numbers behind the Defense tab's meta progression (user-approved 2026-10-12):
    /// - tower critical hits: a base chance and a base damage multiplier, plus extra crit damage for every permanent
    ///   upgrade level owned across the whole tower collection (the "Towers Crit Damage" figure);
    /// - the permanent UFO Base level: each level adds a share of every level's BaseMaxHealth and costs Coins plus
    ///   UFO upgrade cards.
    /// Referenced from TowerCatalog, so both the menu and a level reach it through ApplicationServices.TowerCatalog.</summary>
    [CreateAssetMenu(fileName = "DefenseMetaConfig", menuName = "AlienDefense/Towers/Defense Meta Config")]
    public sealed class DefenseMetaConfig : ScriptableObject
    {
        [Serializable]
        public struct BaseLevelCost
        {
            [Min(0)] public int Coins;
            [Min(0)] public int Cards;
        }

        [Header("Tower critical hits")]
        [SerializeField, Range(0f, 1f)]
        private float _critChance = 0.1f;

        [SerializeField, Min(1f)]
        [Tooltip("Damage multiplier of a critical hit before any collection bonus (1.5 = 150%).")]
        private float _baseCritDamage = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra crit damage for every permanent upgrade level owned, summed over all towers (0.02 = +2%).")]
        private float _critDamagePerUpgradeLevel = 0.02f;

        [Header("Permanent tower upgrades (Defense tab)")]
        [SerializeField, Min(0f)]
        [Tooltip("Extra shot damage per permanent level above 1, as a share of the tower's own damage at every in-level level (0.1 = +10% each). This is the ONLY thing a Defense-tab level-up changes: in a level a tower is still bought at its lowest level and upgraded there as usual.")]
        private float _towerDamageBonusPerLevel = 0.1f;

        [SerializeField]
        [Tooltip("Cost of each permanent tower level-up, shared by every tower: element 0 is the step from level 1 to 2. Cards are the tower's own upgrade card. Its length + 1 is the max level.")]
        private BaseLevelCost[] _towerLevelCosts = Array.Empty<BaseLevelCost>();

        [Header("UFO Base")]
        [SerializeField]
        private string _baseDisplayName = "UFO Base";

        [SerializeField, TextArea]
        private string _baseDescription = "UFO Base is the central hub of your alien fleet. Protect it at all costs: when its health runs out, the level is lost. Every Base level raises its health in every level.";

        [SerializeField]
        private Sprite _baseIcon;

        [SerializeField]
        private Sprite _baseTypeIcon;

        [SerializeField]
        [Tooltip("Optional. Model shown in the Defense popup's 3D showcase for the Base (visual only). None = the icon.")]
        private GameObject _basePreviewModel;

        [SerializeField]
        [Tooltip("MetaItem id of the card a Base level-up consumes.")]
        private string _baseCardItemId = "card_ufo";

        [SerializeField, Min(0f)]
        [Tooltip("Share of a level's BaseMaxHealth added per Base level above 1 (0.1 = +10% each).")]
        private float _baseHealthBonusPerLevel = 0.1f;

        [SerializeField]
        [Tooltip("Cost of each level-up: element 0 is the step from level 1 to 2. Its length + 1 is the max level.")]
        private BaseLevelCost[] _baseLevelCosts = Array.Empty<BaseLevelCost>();

        /// <summary>A card of the Tower Collection that only announces a tower: a picture, how it will be obtained and
        /// a padlock. Display only - it is not a tower, cannot be opened, bought or upgraded.</summary>
        [Serializable]
        public struct ComingSoonCard
        {
            [Tooltip("Shown on the card, e.g. \"Event activity\" or \"Unlock in Engineer's House\".")]
            public string Label;
            public Sprite Icon;
            public Sprite TypeIcon;
            [Tooltip("On = the pink event card, off = the dim blue locked card.")]
            public bool IsEvent;
        }

        [Header("Tower Collection - announced cards")]
        [SerializeField]
        [Tooltip("Locked cards listed after the real towers. Display only.")]
        private ComingSoonCard[] _comingSoonCards = Array.Empty<ComingSoonCard>();

        public int ComingSoonCardCount => _comingSoonCards?.Length ?? 0;

        public ComingSoonCard GetComingSoonCard(int index)
        {
            return _comingSoonCards[index];
        }

        public float CritChance => _critChance;
        public float BaseCritDamage => _baseCritDamage;
        public float CritDamagePerUpgradeLevel => _critDamagePerUpgradeLevel;
        public string BaseDisplayName => _baseDisplayName;
        public string BaseDescription => _baseDescription;
        public Sprite BaseIcon => _baseIcon;
        public Sprite BaseTypeIcon => _baseTypeIcon;
        public GameObject BasePreviewModel => _basePreviewModel;
        public string BaseCardItemId => _baseCardItemId;
        public float BaseHealthBonusPerLevel => _baseHealthBonusPerLevel;

        public float TowerDamageBonusPerLevel => _towerDamageBonusPerLevel;

        /// <summary>Highest permanent tower level index (0 = level 1).</summary>
        public int TowerMaxLevelIndex => _towerLevelCosts?.Length ?? 0;

        public bool TryGetTowerLevelCost(int levelIndex, out BaseLevelCost cost)
        {
            if (_towerLevelCosts == null || levelIndex < 0 || levelIndex >= _towerLevelCosts.Length)
            {
                cost = default;
                return false;
            }

            cost = _towerLevelCosts[levelIndex];
            return true;
        }

        /// <summary>Shot damage multiplier at a permanent tower level index.</summary>
        public float GetTowerDamageMultiplier(int levelIndex)
        {
            return 1f + Mathf.Max(0, levelIndex) * _towerDamageBonusPerLevel;
        }

        /// <summary>Highest Base level index (0 = level 1).</summary>
        public int BaseMaxLevelIndex => _baseLevelCosts?.Length ?? 0;

        /// <summary>Cost of going from <paramref name="levelIndex"/> to the next one; false at max level.</summary>
        public bool TryGetBaseLevelCost(int levelIndex, out BaseLevelCost cost)
        {
            if (_baseLevelCosts == null || levelIndex < 0 || levelIndex >= _baseLevelCosts.Length)
            {
                cost = default;
                return false;
            }

            cost = _baseLevelCosts[levelIndex];
            return true;
        }

        /// <summary>Multiplier on a level's BaseMaxHealth at this Base level index.</summary>
        public float GetBaseHealthMultiplier(int levelIndex)
        {
            return 1f + Mathf.Max(0, levelIndex) * _baseHealthBonusPerLevel;
        }

        /// <summary>Crit damage multiplier with <paramref name="totalUpgradeLevels"/> permanent tower levels owned.</summary>
        public float GetCritDamage(int totalUpgradeLevels)
        {
            return _baseCritDamage + Mathf.Max(0, totalUpgradeLevels) * _critDamagePerUpgradeLevel;
        }
    }
}
