using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Meta
{
    /// <summary>One equipment piece, with its stat line written out per rarity tier.
    ///
    /// Stats are keyed by RARITY rather than by level because that is what the upgrade screen compares: the
    /// player sees "Epic 200 -> Legendary 400" before spending anything. Level is a separate, per-save number
    /// that the tier only bounds (MaxLevel).
    ///
    /// The four stats match the ones the design calls for exactly - Weapon Power, an ability percentage whose
    /// NAME differs per slot (Cargo Bay, Flight Range, ...), Flight Speed and Max Level - so the comparison rows
    /// are built from data and no stat name is hard-coded in the UI.</summary>
    [CreateAssetMenu(fileName = "Equipment_", menuName = "AlienDefense/Meta/Equipment Definition")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        /// <summary>One rung of the rarity ladder, plus what it costs to climb off it.</summary>
        [Serializable]
        public sealed class Tier
        {
            [SerializeField]
            private MetaItemRarity _rarity = MetaItemRarity.Common;

            [SerializeField]
            [Tooltip("Flat weapon power granted while this piece is equipped.")]
            private float _weaponPower;

            [SerializeField]
            [Tooltip("The slot's signature bonus, as a percentage. What it actually boosts is named by the " +
                "definition's Ability Display Name.")]
            private float _abilityPercent;

            [SerializeField]
            private float _flightSpeedPercent;

            [SerializeField, Min(1)]
            private int _maxLevel = 10;

            [Header("Cost to upgrade to the NEXT tier")]
            [SerializeField, Min(0)]
            [Tooltip("Spare copies of this same piece that the craft consumes. 0 on the last tier.")]
            private int _upgradeDuplicates;

            [SerializeField, Min(0)]
            [Tooltip("Meta currency the craft consumes on top of the duplicates.")]
            private int _upgradeCurrency;

            public MetaItemRarity Rarity => _rarity;
            public float WeaponPower => _weaponPower;
            public float AbilityPercent => _abilityPercent;
            public float FlightSpeedPercent => _flightSpeedPercent;
            public int MaxLevel => _maxLevel;
            public int UpgradeDuplicates => _upgradeDuplicates;
            public int UpgradeCurrency => _upgradeCurrency;
        }

        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        [TextArea(2, 4)]
        private string _description;

        [SerializeField]
        private Sprite _icon;

        [SerializeField]
        private EquipmentSlotType _slot = EquipmentSlotType.Controls;

        [SerializeField]
        [Tooltip("Shown as the second stat row's label, e.g. \"Ability: Cargo Bay\".")]
        private string _abilityDisplayName = "Cargo Bay";

        [SerializeField]
        [Tooltip("Lowest to highest. The craft screen reads the next entry as the upgrade target, so the order " +
            "here IS the rarity ladder for this piece.")]
        private Tier[] _tiers = Array.Empty<Tier>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public EquipmentSlotType Slot => _slot;
        public string AbilityDisplayName => _abilityDisplayName;
        public IReadOnlyList<Tier> Tiers => _tiers;

        /// <summary>The lowest rarity this piece can exist at - what a freshly-granted copy starts as.</summary>
        public MetaItemRarity BaseRarity => _tiers != null && _tiers.Length > 0 ? _tiers[0].Rarity : MetaItemRarity.Common;

        public MetaItemRarity MaxRarity => _tiers != null && _tiers.Length > 0 ? _tiers[_tiers.Length - 1].Rarity : MetaItemRarity.Common;

        public bool TryGetTier(MetaItemRarity rarity, out Tier tier)
        {
            if (_tiers != null)
            {
                for (int i = 0; i < _tiers.Length; i++)
                {
                    if (_tiers[i] != null && _tiers[i].Rarity == rarity)
                    {
                        tier = _tiers[i];
                        return true;
                    }
                }
            }

            tier = null;
            return false;
        }

        /// <summary>The tier one rung up, or false when this piece is already at its last tier. Walks the array
        /// rather than doing rarity + 1 so a piece whose ladder skips a rarity still upgrades correctly.</summary>
        public bool TryGetNextTier(MetaItemRarity rarity, out Tier next)
        {
            if (_tiers != null)
            {
                for (int i = 0; i < _tiers.Length - 1; i++)
                {
                    if (_tiers[i] != null && _tiers[i].Rarity == rarity)
                    {
                        next = _tiers[i + 1];
                        return next != null;
                    }
                }
            }

            next = null;
            return false;
        }
    }
}
