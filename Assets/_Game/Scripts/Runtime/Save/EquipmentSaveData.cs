using System;

namespace AlienDefense.Save
{
    /// <summary>One equipment piece the player owns. Plain data for JsonUtility - the definition (icon, stats,
    /// recipe) is looked up from MetaItemCatalog by ItemId, never serialized here.</summary>
    [Serializable]
    public sealed class EquipmentSaveData
    {
        public string ItemId;

        /// <summary>MetaItemRarity as an int, so a future rarity cannot break old saves.</summary>
        public int Rarity;

        public int Level;

        /// <summary>Spare copies held for crafting, on top of the one the player owns. The craft screen's
        /// requirement slots read this.</summary>
        public int Duplicates;

        public bool Equipped;
    }
}
