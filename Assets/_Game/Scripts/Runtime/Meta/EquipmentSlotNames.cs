namespace AlienDefense.Meta
{
    /// <summary>Display names of the six UFO sockets. The enum keeps its original (serialized) names; the UI and the
    /// design call them Flight stick / Chair / Reactor / Cargo / Antigravity / Engine.</summary>
    public static class EquipmentSlotNames
    {
        public const int Count = 6;

        public static string Get(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Controls: return "Flight stick";
                case EquipmentSlotType.Seat: return "Chair";
                case EquipmentSlotType.Core: return "Reactor";
                case EquipmentSlotType.CargoBay: return "Cargo";
                case EquipmentSlotType.AntiGravity: return "Antigravity";
                case EquipmentSlotType.Engine: return "Engine";
            }

            return slot.ToString();
        }

        /// <summary>Filter-bar order (after "All"): Flight stick, Chair, Reactor, Cargo, Antigravity, Engine.</summary>
        public static readonly EquipmentSlotType[] BarOrder =
        {
            EquipmentSlotType.Controls, EquipmentSlotType.Seat, EquipmentSlotType.Core,
            EquipmentSlotType.CargoBay, EquipmentSlotType.AntiGravity, EquipmentSlotType.Engine,
        };
    }

    /// <summary>An item id and a count (craft recipes, level-up costs).</summary>
    [System.Serializable]
    public struct MetaItemAmount
    {
        public string ItemId;
        public int Amount;

        public MetaItemAmount(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }

    public enum ContainerRewardKind
    {
        Item = 0,
        Coins = 1,
        Gems = 2,
        Artifact = 3,
    }

    /// <summary>One roll of a container: <see cref="Chance"/> to grant Min..Max of it.</summary>
    [System.Serializable]
    public struct ContainerReward
    {
        public ContainerRewardKind Kind;

        [UnityEngine.Tooltip("Item id (Item) or artifact id (Artifact, granted at its base rarity).")]
        public string Id;

        public int Min;
        public int Max;

        [UnityEngine.Range(0f, 1f)]
        public float Chance;
    }
}
