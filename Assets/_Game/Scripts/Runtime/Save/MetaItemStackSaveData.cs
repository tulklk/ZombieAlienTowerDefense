using System;

namespace AlienDefense.Save
{
    /// <summary>One stack in the profile meta-inventory (objective rewards, etc.). Plain data for JsonUtility.</summary>
    [Serializable]
    public sealed class MetaItemStackSaveData
    {
        public string ItemId;
        public int Amount;
    }
}
