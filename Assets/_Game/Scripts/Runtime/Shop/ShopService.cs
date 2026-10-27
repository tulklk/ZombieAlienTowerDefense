using System;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Save;

namespace AlienDefense.Shop
{
    /// <summary>One reward actually paid by a purchase or a claim (after the first-purchase bonus).</summary>
    public readonly struct ShopGrantedReward
    {
        public ShopRewardData Reward { get; }
        public int Amount { get; }

        public ShopGrantedReward(ShopRewardData reward, int amount)
        {
            Reward = reward;
            Amount = amount;
        }
    }

    public enum ShopPurchaseStatus
    {
        Success = 0,
        Cancelled = 1,
        Failed = 2,
        NotAvailable = 3,
    }

    public sealed class ShopPurchaseOutcome
    {
        public ShopPurchaseStatus Status;
        public ShopItemData Item;
        public string Message;
        public readonly List<ShopGrantedReward> Rewards = new List<ShopGrantedReward>();
    }

    /// <summary>The Shop's rules. Reads ShopCatalog, asks IShopPurchaseService for the payment, then grants rewards
    /// and records the purchase through PlayerProfileService only (one batch, one save). Grants are idempotent by
    /// store transaction id. No Unity objects, no per-frame work: the views ask it for state when they refresh.</summary>
    public sealed class ShopService : IVipBenefitProvider
    {
        private readonly ShopCatalog _catalog;
        private readonly PlayerProfileService _profile;
        private readonly IShopPurchaseService _store;
        private readonly LevelCatalog _levels;
        private readonly List<ShopItemData> _scratch = new List<ShopItemData>();

        public ShopService(ShopCatalog catalog, PlayerProfileService profile, IShopPurchaseService store, LevelCatalog levels)
        {
            _catalog = catalog;
            _profile = profile;
            _store = store;
            _levels = levels;
        }

        /// <summary>Raised after anything that changes what the shop shows (purchase, claim, cycle refresh).</summary>
        public event Action Changed;

        public ShopCatalog Catalog => _catalog;
        public IShopPurchaseService Store => _store;
        public bool IsPurchaseInFlight { get; private set; }

        // ------------------------------------------------------------------ limited-offer cycle

        public TimeSpan OfferCycleLength => TimeSpan.FromHours(_catalog.OfferCycleHours);

        public long CurrentCycleStartTicks => _profile.ShopOfferCycleStartUtcTicks;

        /// <summary>Starts the first cycle, or the next one once the current has ended (limited offers refresh).
        /// Called when the shop opens / a category is shown - never per frame.</summary>
        public bool RollOfferCycleIfNeeded(DateTime nowUtc)
        {
            long start = _profile.ShopOfferCycleStartUtcTicks;
            long length = OfferCycleLength.Ticks;
            if (start > 0 && nowUtc.Ticks < start + length)
            {
                return false;
            }

            long newStart = start <= 0 ? nowUtc.Ticks : start + (nowUtc.Ticks - start) / length * length;
            _profile.SetShopOfferCycleStart(newStart);
            Changed?.Invoke();
            return true;
        }

        public TimeSpan GetOfferTimeLeft(DateTime nowUtc)
        {
            long start = _profile.ShopOfferCycleStartUtcTicks;
            if (start <= 0)
            {
                return OfferCycleLength;
            }

            long left = start + OfferCycleLength.Ticks - nowUtc.Ticks;
            return left > 0 ? new TimeSpan(left) : TimeSpan.Zero;
        }

        // ------------------------------------------------------------------ item state

        public bool IsUnlocked(ShopItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.UnlockLevelId))
            {
                return true;
            }

            if (_levels == null)
            {
                return false;
            }

            int index = _levels.IndexOf(item.UnlockLevelId);
            if (index < 0)
            {
                return false; // that campaign does not exist yet
            }

            int highest = _levels.IndexOf(_profile.HighestUnlockedLevelId);
            return highest >= index;
        }

        /// <summary>Purchases left (this cycle for limited offers); -1 = unlimited.</summary>
        public int GetRemaining(ShopItemData item)
        {
            int limit = item.EffectiveLimit;
            if (limit <= 0)
            {
                return -1;
            }

            int used = item.LimitedTime
                ? _profile.GetShopCyclePurchaseCount(item.ProductId, _profile.ShopOfferCycleStartUtcTicks)
                : _profile.GetShopPurchaseCount(item.ProductId);
            return Math.Max(0, limit - used);
        }

        public ShopItemState GetState(ShopItemData item, DateTime nowUtc)
        {
            if (!IsUnlocked(item))
            {
                return ShopItemState.Locked;
            }

            if (item.Layout == ShopCardLayout.Privilege && IsPrivilegeActive(item.ProductId, nowUtc))
            {
                return ShopItemState.Active;
            }

            if (item.LimitedTime && GetOfferTimeLeft(nowUtc) <= TimeSpan.Zero)
            {
                return ShopItemState.Expired;
            }

            return GetRemaining(item) == 0 ? ShopItemState.SoldOut : ShopItemState.Available;
        }

        public bool IsFirstPurchaseBonusAvailable(ShopItemData item)
        {
            return item != null && item.FirstPurchaseDouble && !_profile.HasUsedShopFirstBonus(item.ProductId);
        }

        /// <summary>Gems this product pays right now (doubled while the first-purchase bonus is unused).</summary>
        public int GetGemPayout(ShopItemData item)
        {
            return item.GemAmount * (IsFirstPurchaseBonusAvailable(item) ? 2 : 1);
        }

        public string GetPriceLabel(ShopItemData item)
        {
            string localized = _store?.GetLocalizedPrice(item);
            return !string.IsNullOrEmpty(localized) ? localized : ShopFormat.Vnd(item.PriceVnd);
        }

        // ------------------------------------------------------------------ purchase

        /// <summary>Pays through the store and, on success, grants the rewards exactly once. The callback always runs
        /// (success, cancel or failure); a cancel or failure changes nothing.</summary>
        public void Purchase(ShopItemData item, Action<ShopPurchaseOutcome> done)
        {
            var outcome = new ShopPurchaseOutcome { Item = item };
            DateTime now = DateTime.UtcNow;
            if (item == null || IsPurchaseInFlight || GetState(item, now) != ShopItemState.Available)
            {
                outcome.Status = ShopPurchaseStatus.NotAvailable;
                outcome.Message = "This offer is not available right now.";
                done?.Invoke(outcome);
                return;
            }

            if (_store == null || !_store.IsAvailable)
            {
                outcome.Status = ShopPurchaseStatus.Failed;
                outcome.Message = "The store is not available.";
                done?.Invoke(outcome);
                return;
            }

            IsPurchaseInFlight = true;
            _store.Purchase(item, result =>
            {
                IsPurchaseInFlight = false;
                if (result.Cancelled)
                {
                    outcome.Status = ShopPurchaseStatus.Cancelled;
                }
                else if (!result.Success)
                {
                    outcome.Status = ShopPurchaseStatus.Failed;
                    outcome.Message = string.IsNullOrEmpty(result.Error) ? "The purchase failed." : result.Error;
                }
                else if (!Grant(item, result.TransactionId, DateTime.UtcNow, outcome.Rewards))
                {
                    outcome.Status = ShopPurchaseStatus.Failed;
                    outcome.Message = "This purchase was already delivered.";
                }
                else
                {
                    outcome.Status = ShopPurchaseStatus.Success;
                }

                done?.Invoke(outcome);
                if (outcome.Status == ShopPurchaseStatus.Success)
                {
                    Changed?.Invoke();
                }
            });
        }

        /// <summary>Grants one store transaction. Public so a real store's "restore / pending purchase" callback can
        /// deliver directly; a transaction id that was already granted pays nothing and returns false.</summary>
        public bool Grant(ShopItemData item, string transactionId, DateTime nowUtc, List<ShopGrantedReward> granted)
        {
            if (item == null || _profile.HasShopTransaction(transactionId))
            {
                return false;
            }

            bool bonus = IsFirstPurchaseBonusAvailable(item);
            _profile.BeginBatch();
            try
            {
                if (!_profile.TryRecordShopPurchase(item.ProductId, transactionId, _profile.ShopOfferCycleStartUtcTicks, bonus))
                {
                    return false;
                }

                for (int i = 0; i < item.RewardCount; i++)
                {
                    ShopRewardData reward = item.GetReward(i);
                    int amount = reward.Amount;
                    switch (reward.Type)
                    {
                        case ShopRewardType.Gems:
                            amount *= bonus ? 2 : 1;
                            _profile.AddGems(amount);
                            break;
                        case ShopRewardType.Coins:
                            _profile.AddMetaCurrency(amount);
                            break;
                        case ShopRewardType.Item:
                            _profile.AddItem(reward.ItemId, amount);
                            break;
                        case ShopRewardType.ConstructionQueue:
                            _profile.AddConstructionQueues(amount);
                            break;
                        case ShopRewardType.VipDays:
                            _profile.ExtendShopSubscription(PlayerProfileService.VipSubscriptionId, amount, nowUtc, item.ProductId);
                            break;
                        case ShopRewardType.PrivilegeDays:
                            _profile.ExtendShopSubscription(item.ProductId, amount, nowUtc);
                            break;
                    }

                    granted?.Add(new ShopGrantedReward(reward, amount));
                }
            }
            finally
            {
                _profile.EndBatch();
            }

            return true;
        }

        // ------------------------------------------------------------------ VIP + privileges

        public bool IsVipActive(DateTime nowUtc) => _profile.IsVipActive(nowUtc);

        public bool IsPrivilegeActive(string productId, DateTime nowUtc) => _profile.IsShopPrivilegeActive(productId, nowUtc);

        public TimeSpan GetVipTimeLeft(DateTime nowUtc) => TimeLeft(PlayerProfileService.VipSubscriptionId, nowUtc);

        public TimeSpan GetPrivilegeTimeLeft(string productId, DateTime nowUtc) => TimeLeft(productId, nowUtc);

        public bool CanClaimVipDaily(DateTime nowUtc) => IsVipActive(nowUtc) && !_profile.HasClaimedVipDailyToday(nowUtc);

        public bool TryClaimVipDaily(DateTime nowUtc, List<ShopGrantedReward> granted)
        {
            int gems = _catalog.VipDailyGems;
            if (!_profile.TryClaimVipDaily(nowUtc, gems))
            {
                return false;
            }

            granted?.Add(new ShopGrantedReward(new ShopRewardData { Type = ShopRewardType.Gems, Amount = gems }, gems));
            Changed?.Invoke();
            return true;
        }

        private TimeSpan TimeLeft(string id, DateTime nowUtc)
        {
            long left = _profile.GetShopSubscriptionExpiry(id) - nowUtc.Ticks;
            return left > 0 ? new TimeSpan(left) : TimeSpan.Zero;
        }

        // ------------------------------------------------------------------ badges

        /// <summary>Red "!" on a category tab: VIP gift ready, a newly unlocked campaign pack, a new offer cycle.</summary>
        public bool HasBadge(ShopCategory category, DateTime nowUtc)
        {
            switch (category)
            {
                case ShopCategory.Vip:
                    return CanClaimVipDaily(nowUtc);
                case ShopCategory.ResourcePacks:
                    return _profile.ShopOfferCycleStartUtcTicks > 0
                        && _profile.ShopSeenOfferCycleUtcTicks != _profile.ShopOfferCycleStartUtcTicks;
                case ShopCategory.CampaignPacks:
                    _catalog.GetItems(ShopCategory.CampaignPacks, _scratch);
                    foreach (ShopItemData item in _scratch)
                    {
                        if (IsUnlocked(item) && GetRemaining(item) != 0 && !_profile.IsShopProductSeen(item.ProductId))
                        {
                            return true;
                        }
                    }

                    return false;
            }

            return false;
        }

        public void MarkCategorySeen(ShopCategory category)
        {
            switch (category)
            {
                case ShopCategory.ResourcePacks:
                    _profile.MarkShopOfferCycleSeen(_profile.ShopOfferCycleStartUtcTicks);
                    break;
                case ShopCategory.CampaignPacks:
                    _catalog.GetItems(ShopCategory.CampaignPacks, _scratch);
                    foreach (ShopItemData item in _scratch)
                    {
                        if (IsUnlocked(item))
                        {
                            _profile.MarkShopProductSeen(item.ProductId);
                        }
                    }

                    break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ------------------------------------------------------------------ debug

        public void DebugExpireOffers()
        {
            _profile.SetShopOfferCycleStart(DateTime.UtcNow.Ticks - OfferCycleLength.Ticks + TimeSpan.FromSeconds(5).Ticks);
            Changed?.Invoke();
        }

        public void DebugActivateVip(int days)
        {
            _profile.ExtendShopSubscription(PlayerProfileService.VipSubscriptionId, days, DateTime.UtcNow, "debug");
            Changed?.Invoke();
        }

        public void DebugExpireVip()
        {
            _profile.DebugSetShopSubscriptionExpiry(PlayerProfileService.VipSubscriptionId, 0);
            Changed?.Invoke();
        }

        public void DebugExpirePrivileges()
        {
            for (int i = 0; i < _catalog.Count; i++)
            {
                ShopItemData item = _catalog.Get(i);
                if (item != null && item.Layout == ShopCardLayout.Privilege)
                {
                    _profile.DebugSetShopSubscriptionExpiry(item.ProductId, 0);
                }
            }

            Changed?.Invoke();
        }

        public void DebugNotifyChanged() => Changed?.Invoke();
#endif
    }
}
