using System;

namespace AlienDefense.Save
{
    /// <summary>One artifact stack: a family at a given rarity, and how many copies of it the player holds.
    ///
    /// Rarity is part of the key, not a property of the family - the same artifact can sit in the inventory at
    /// several rarities at once while the player merges their way up.</summary>
    [Serializable]
    public sealed class ArtifactSaveData
    {
        public string ItemId;

        /// <summary>MetaItemRarity as an int.</summary>
        public int Rarity;

        public int Amount;
    }
}
