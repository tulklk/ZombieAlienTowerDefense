using System;
using UnityEngine;

namespace AlienDefense.Meta
{
    [Serializable]
    public sealed class ObjectiveRewardEntry
    {
        public ObjectiveRewardKind Kind = ObjectiveRewardKind.MetaItem;

        [Tooltip("Used when Kind == MetaItem. Ignored for Coins/Gems.")]
        public string ItemId;

        public int Amount = 1;

        [Tooltip("If true, preview shows ? until the objective is achieved.")]
        public bool HideUntilUnlocked;
    }
}
