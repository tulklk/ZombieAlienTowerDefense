using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Meta
{
    [Serializable]
    public sealed class ObjectiveRewardBundle
    {
        [SerializeField]
        private List<ObjectiveRewardEntry> _rewards = new List<ObjectiveRewardEntry>();

        public IReadOnlyList<ObjectiveRewardEntry> Rewards => _rewards;

        public bool HasRewards => _rewards != null && _rewards.Count > 0;

        public void SetRewards(IEnumerable<ObjectiveRewardEntry> rewards)
        {
            _rewards = rewards != null ? new List<ObjectiveRewardEntry>(rewards) : new List<ObjectiveRewardEntry>();
        }
    }
}
