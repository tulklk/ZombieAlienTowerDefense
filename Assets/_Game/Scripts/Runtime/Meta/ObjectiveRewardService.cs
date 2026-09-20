using System;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Meta
{
    public enum ObjectiveRewardUiState
    {
        Locked = 0,
        Claimable = 1,
        Claimed = 2
    }

    public readonly struct GrantedObjectiveReward
    {
        public ObjectiveRewardKind Kind { get; }
        public string ItemId { get; }
        public int Amount { get; }

        public GrantedObjectiveReward(ObjectiveRewardKind kind, string itemId, int amount)
        {
            Kind = kind;
            ItemId = itemId;
            Amount = amount;
        }
    }

    /// <summary>Derives objective claim UI from BestRemainingHpPercent + claim flags, and performs one-shot grants.</summary>
    public sealed class ObjectiveRewardService
    {
        private readonly PlayerProfileService _profile;
        private readonly LevelCatalog _catalog;
        private bool _claimInProgress;

        public event Action<string, LevelObjectiveKind> RewardClaimed;

        public ObjectiveRewardService(PlayerProfileService profile, LevelCatalog catalog)
        {
            _profile = profile;
            _catalog = catalog;
        }

        public static bool IsObjectiveAchieved(LevelProgressSnapshot progress, LevelObjectiveKind kind)
        {
            switch (kind)
            {
                case LevelObjectiveKind.Clear:
                    return progress.IsCompleted;
                case LevelObjectiveKind.Hp50:
                    return progress.IsCompleted && progress.BestRemainingHpPercent >= 50;
                case LevelObjectiveKind.Perfect:
                    return progress.IsCompleted && progress.BestRemainingHpPercent >= 100;
                default:
                    return false;
            }
        }

        public ObjectiveRewardUiState GetUiState(string levelId, LevelObjectiveKind kind)
        {
            LevelProgressSnapshot progress = _profile != null
                ? _profile.GetLevelProgress(levelId)
                : LevelProgressSnapshot.NotStarted(levelId);

            if (progress.IsObjectiveRewardClaimed(kind))
            {
                return ObjectiveRewardUiState.Claimed;
            }

            if (IsObjectiveAchieved(progress, kind))
            {
                return ObjectiveRewardUiState.Claimable;
            }

            return ObjectiveRewardUiState.Locked;
        }

        public IReadOnlyList<ObjectiveRewardEntry> GetRewardEntries(string levelId, LevelObjectiveKind kind)
        {
            LevelCatalogEntry entry = FindEntry(levelId);
            ObjectiveRewardBundle bundle = entry != null ? entry.GetObjectiveRewards(kind) : null;
            if (bundle == null || !bundle.HasRewards)
            {
                return Array.Empty<ObjectiveRewardEntry>();
            }

            return bundle.Rewards;
        }

        public bool CanClaim(string levelId, LevelObjectiveKind kind)
        {
            return !_claimInProgress && GetUiState(levelId, kind) == ObjectiveRewardUiState.Claimable;
        }

        /// <summary>Grants rewards then marks claimed. Validates entries before mutating inventory.</summary>
        public bool TryClaim(string levelId, LevelObjectiveKind kind, out List<GrantedObjectiveReward> granted)
        {
            granted = new List<GrantedObjectiveReward>();
            if (!CanClaim(levelId, kind) || _profile == null)
            {
                return false;
            }

            IReadOnlyList<ObjectiveRewardEntry> entries = GetRewardEntries(levelId, kind);
            for (int i = 0; i < entries.Count; i++)
            {
                ObjectiveRewardEntry entry = entries[i];
                if (entry == null || entry.Amount <= 0)
                {
                    continue;
                }

                if (entry.Kind == ObjectiveRewardKind.MetaItem && string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    Debug.LogError($"[ObjectiveRewardService] MetaItem reward missing ItemId on '{levelId}'/{kind}.");
                    return false;
                }
            }

            _claimInProgress = true;
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    ObjectiveRewardEntry entry = entries[i];
                    if (entry == null || entry.Amount <= 0)
                    {
                        continue;
                    }

                    switch (entry.Kind)
                    {
                        case ObjectiveRewardKind.Coins:
                            _profile.AddMetaCurrency(entry.Amount);
                            granted.Add(new GrantedObjectiveReward(ObjectiveRewardKind.Coins, null, entry.Amount));
                            break;
                        case ObjectiveRewardKind.Gems:
                            _profile.AddGems(entry.Amount);
                            granted.Add(new GrantedObjectiveReward(ObjectiveRewardKind.Gems, null, entry.Amount));
                            break;
                        default:
                            _profile.AddItem(entry.ItemId, entry.Amount);
                            granted.Add(new GrantedObjectiveReward(ObjectiveRewardKind.MetaItem, entry.ItemId, entry.Amount));
                            break;
                    }
                }

                if (!_profile.TryMarkObjectiveRewardClaimed(levelId, kind))
                {
                    Debug.LogError($"[ObjectiveRewardService] Failed to mark claimed for '{levelId}'/{kind} after grant.");
                    return false;
                }

                RewardClaimed?.Invoke(levelId, kind);
                return true;
            }
            finally
            {
                _claimInProgress = false;
            }
        }

        private LevelCatalogEntry FindEntry(string levelId)
        {
            if (_catalog == null || string.IsNullOrWhiteSpace(levelId))
            {
                return null;
            }

            return _catalog.TryResolve(levelId, out LevelCatalogEntry entry) ? entry : null;
        }
    }
}
