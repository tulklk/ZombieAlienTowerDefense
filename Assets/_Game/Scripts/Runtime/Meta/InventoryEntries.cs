using System.Collections.Generic;

namespace AlienDefense.Meta
{
    /// <summary>View models the inventory UI binds to: a definition (art, names, stats) paired with the player's
    /// live numbers. Built by InventoryService so no view ever has to join a catalog lookup to a save row itself.</summary>
    public readonly struct MaterialEntry
    {
        public readonly MetaItemDefinition Definition;
        public readonly int Amount;

        public MaterialEntry(MetaItemDefinition definition, int amount)
        {
            Definition = definition;
            Amount = amount;
        }
    }

    public readonly struct EquipmentEntry
    {
        public readonly EquipmentDefinition Definition;
        public readonly MetaItemRarity Rarity;
        public readonly int Level;
        public readonly int Duplicates;
        public readonly bool Equipped;

        /// <summary>True when the player holds everything the next rarity costs - drives the green arrow badge
        /// on both the slot and the inventory card.</summary>
        public readonly bool CanUpgrade;

        public EquipmentEntry(EquipmentDefinition definition, MetaItemRarity rarity, int level, int duplicates,
            bool equipped, bool canUpgrade)
        {
            Definition = definition;
            Rarity = rarity;
            Level = level;
            Duplicates = duplicates;
            Equipped = equipped;
            CanUpgrade = canUpgrade;
        }

        public bool Exists => Definition != null;
    }

    public readonly struct ArtifactEntry
    {
        public readonly ArtifactDefinition Definition;
        public readonly MetaItemRarity Rarity;
        public readonly int Amount;

        public ArtifactEntry(ArtifactDefinition definition, MetaItemRarity rarity, int amount)
        {
            Definition = definition;
            Rarity = rarity;
            Amount = amount;
        }

        public bool Exists => Definition != null;
    }

    /// <summary>Everything the craft screen needs for one piece: the tier it is on, the tier it is going to, and
    /// exactly what is missing. Computed in one place so the stat rows, the requirement slots and the Craft
    /// button can never disagree about whether the craft is affordable.</summary>
    public readonly struct EquipmentCraftPlan
    {
        public readonly EquipmentDefinition Definition;
        public readonly EquipmentDefinition.Tier Current;

        /// <summary>Null when the piece is already at its last tier.</summary>
        public readonly EquipmentDefinition.Tier Next;

        public readonly int DuplicatesOwned;
        public readonly int DuplicatesRequired;
        public readonly int CurrencyOwned;
        public readonly int CurrencyRequired;

        public EquipmentCraftPlan(EquipmentDefinition definition, EquipmentDefinition.Tier current,
            EquipmentDefinition.Tier next, int duplicatesOwned, int duplicatesRequired,
            int currencyOwned, int currencyRequired)
        {
            Definition = definition;
            Current = current;
            Next = next;
            DuplicatesOwned = duplicatesOwned;
            DuplicatesRequired = duplicatesRequired;
            CurrencyOwned = currencyOwned;
            CurrencyRequired = currencyRequired;
        }

        public bool IsMaxRarity => Next == null;
        public bool HasDuplicates => DuplicatesOwned >= DuplicatesRequired;
        public bool HasCurrency => CurrencyOwned >= CurrencyRequired;
        public bool CanCraft => Definition != null && !IsMaxRarity && HasDuplicates && HasCurrency;
    }

    /// <summary>One row of the artifact merge screen: N copies in, one higher-rarity copy out.</summary>
    public readonly struct ArtifactMergePlan
    {
        public readonly ArtifactDefinition Definition;
        public readonly MetaItemRarity SourceRarity;
        public readonly MetaItemRarity ResultRarity;
        public readonly int InputCount;

        public ArtifactMergePlan(ArtifactDefinition definition, MetaItemRarity sourceRarity,
            MetaItemRarity resultRarity, int inputCount)
        {
            Definition = definition;
            SourceRarity = sourceRarity;
            ResultRarity = resultRarity;
            InputCount = inputCount;
        }

        public bool Exists => Definition != null;
    }

    /// <summary>Convenience for views that want a stable empty list without allocating one each refresh.</summary>
    public static class InventoryEmpty
    {
        public static readonly IReadOnlyList<MaterialEntry> Materials = new List<MaterialEntry>();
        public static readonly IReadOnlyList<EquipmentEntry> Equipment = new List<EquipmentEntry>();
        public static readonly IReadOnlyList<ArtifactEntry> Artifacts = new List<ArtifactEntry>();
    }
}
