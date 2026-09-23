using System.Collections.Generic;
using AlienDefense.Progression;

namespace AlienDefense.Core
{
    /// <summary>Carries "these rewards were just granted" from the level that granted them to the MainMenu that
    /// shows them flying into the HUD. Application-scope plain C#, owned by ApplicationCompositionRoot exactly like
    /// <see cref="LevelLaunchContext"/> - no static instance, no DontDestroyOnLoad component of its own.
    ///
    /// This is presentation only. The amounts here were already added to the profile and saved by
    /// LevelCompositionRoot before the victory panel ever appeared; nothing downstream may grant them again.
    /// <see cref="TryConsume"/> clears the queue, so re-entering or reloading MainMenu cannot replay the
    /// animation a second time.</summary>
    public sealed class PendingRewardPresentation
    {
        private readonly List<VictoryReward> _rewards = new List<VictoryReward>(4);

        public bool HasPending => _rewards.Count > 0;

        /// <summary>Replaces anything queued (a previous run whose MainMenu was never reached).</summary>
        public void Set(IReadOnlyList<VictoryReward> rewards)
        {
            _rewards.Clear();
            if (rewards == null)
            {
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].Amount > 0)
                {
                    _rewards.Add(rewards[i]);
                }
            }
        }

        /// <summary>Hands the queue over and empties it. Returns false when there is nothing to play.</summary>
        public bool TryConsume(List<VictoryReward> buffer)
        {
            if (buffer == null || _rewards.Count == 0)
            {
                return false;
            }

            buffer.Clear();
            buffer.AddRange(_rewards);
            _rewards.Clear();
            return true;
        }

        public void Clear()
        {
            _rewards.Clear();
        }
    }
}
