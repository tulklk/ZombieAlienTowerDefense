using AlienDefense.Meta;

namespace AlienDefense.Save
{
    /// <summary>Immutable copies of the inventory rows, handed out by PlayerProfileService instead of its live
    /// save lists. Matches the existing LevelProgressSnapshot pattern: callers may read and cache these freely
    /// without being able to edit the save behind the service's back.</summary>
    public readonly struct MetaItemStackSnapshot
    {
        public readonly string ItemId;
        public readonly int Amount;

        public MetaItemStackSnapshot(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }

    public readonly struct EquipmentSnapshot
    {
        public readonly string ItemId;
        public readonly MetaItemRarity Rarity;
        public readonly int Level;

        /// <summary>Spare copies beyond the one owned, spent by the rarity craft.</summary>
        public readonly int Duplicates;

        public readonly bool Equipped;

        public EquipmentSnapshot(string itemId, MetaItemRarity rarity, int level, int duplicates, bool equipped)
        {
            ItemId = itemId;
            Rarity = rarity;
            Level = level;
            Duplicates = duplicates;
            Equipped = equipped;
        }

        /// <summary>False for the default struct, which is what a lookup miss returns.</summary>
        public bool Exists => !string.IsNullOrEmpty(ItemId);
    }

    public readonly struct ArtifactSnapshot
    {
        public readonly string ItemId;
        public readonly MetaItemRarity Rarity;
        public readonly int Amount;

        public ArtifactSnapshot(string itemId, MetaItemRarity rarity, int amount)
        {
            ItemId = itemId;
            Rarity = rarity;
            Amount = amount;
        }

        public bool Exists => !string.IsNullOrEmpty(ItemId);
    }
}
