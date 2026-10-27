using System;
using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Shop state. Like the rest of PlayerProfileService, the only place the save's shop record is read or
    /// changed. The VIP / privilege queries double as the hook other systems (ads, battle perks) can call from any
    /// scene through ApplicationServices.PlayerProfileService.</summary>
    public sealed partial class PlayerProfileService
    {
        public const string VipSubscriptionId = "vip";
        private const int MaxRememberedShopTransactions = 64;

        private ShopSaveData Shop
        {
            get
            {
                if (_data.Shop == null)
                {
                    _data.Shop = new ShopSaveData();
                }

                return _data.Shop;
            }
        }

        // ------------------------------------------------------------------ purchases

        public int GetShopPurchaseCount(string productId)
        {
            ShopPurchaseSaveData record = FindShopPurchase(productId);
            return record != null ? Mathf.Max(0, record.Count) : 0;
        }

        /// <summary>Purchases of a limited offer inside one offer cycle.</summary>
        public int GetShopCyclePurchaseCount(string productId, long cycleUtcTicks)
        {
            ShopPurchaseSaveData record = FindShopPurchase(productId);
            return record != null && record.CycleUtcTicks == cycleUtcTicks ? Mathf.Max(0, record.CycleCount) : 0;
        }

        public bool HasUsedShopFirstBonus(string productId)
        {
            ShopPurchaseSaveData record = FindShopPurchase(productId);
            return record != null && record.FirstBonusUsed;
        }

        public bool HasShopTransaction(string transactionId)
        {
            return !string.IsNullOrEmpty(transactionId) && Shop.Transactions.Contains(transactionId);
        }

        /// <summary>Records one granted store purchase. Returns false - grant nothing - when this transaction id was
        /// already recorded, so a duplicated store callback cannot pay twice.</summary>
        public bool TryRecordShopPurchase(string productId, string transactionId, long cycleUtcTicks, bool usedFirstBonus)
        {
            if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(transactionId) || HasShopTransaction(transactionId))
            {
                return false;
            }

            Shop.Transactions.Add(transactionId);
            if (Shop.Transactions.Count > MaxRememberedShopTransactions)
            {
                Shop.Transactions.RemoveRange(0, Shop.Transactions.Count - MaxRememberedShopTransactions);
            }

            ShopPurchaseSaveData record = FindShopPurchase(productId);
            if (record == null)
            {
                record = new ShopPurchaseSaveData { ProductId = productId };
                Shop.Purchases.Add(record);
            }

            record.Count = Mathf.Max(0, record.Count) + 1;
            record.FirstBonusUsed |= usedFirstBonus;
            if (record.CycleUtcTicks != cycleUtcTicks)
            {
                record.CycleUtcTicks = cycleUtcTicks;
                record.CycleCount = 0;
            }

            record.CycleCount++;
            RequestImmediateSave();
            return true;
        }

        // ------------------------------------------------------------------ VIP + privilege cards

        public bool IsVipActive(DateTime nowUtc) => GetShopSubscriptionExpiry(VipSubscriptionId) > nowUtc.Ticks;

        public bool IsShopPrivilegeActive(string id, DateTime nowUtc) => GetShopSubscriptionExpiry(id) > nowUtc.Ticks;

        public string VipPlanId => Shop.VipPlanId;

        public long GetShopSubscriptionExpiry(string id)
        {
            ShopSubscriptionSaveData record = FindShopSubscription(id);
            return record != null ? record.ExpireUtcTicks : 0L;
        }

        /// <summary>Adds days to VIP / a privilege card: from its current end while it is running, else from now.</summary>
        public void ExtendShopSubscription(string id, int days, DateTime nowUtc, string planId = null)
        {
            if (string.IsNullOrWhiteSpace(id) || days <= 0)
            {
                return;
            }

            ShopSubscriptionSaveData record = FindShopSubscription(id);
            if (record == null)
            {
                record = new ShopSubscriptionSaveData { Id = id };
                Shop.Subscriptions.Add(record);
            }

            long from = Math.Max(record.ExpireUtcTicks, nowUtc.Ticks);
            record.ExpireUtcTicks = from + TimeSpan.FromDays(days).Ticks;
            if (id == VipSubscriptionId && !string.IsNullOrEmpty(planId))
            {
                Shop.VipPlanId = planId;
            }

            RequestImmediateSave();
        }

        public bool HasClaimedVipDailyToday(DateTime nowUtc)
        {
            return Shop.VipLastClaimUtcTicks > 0 && new DateTime(Shop.VipLastClaimUtcTicks, DateTimeKind.Utc).Date == nowUtc.Date;
        }

        /// <summary>Claims today's VIP gems once per UTC day while VIP is running.</summary>
        public bool TryClaimVipDaily(DateTime nowUtc, int gems)
        {
            if (!IsVipActive(nowUtc) || HasClaimedVipDailyToday(nowUtc) || gems <= 0)
            {
                return false;
            }

            Shop.VipLastClaimUtcTicks = nowUtc.Ticks;
            _data.Gems += gems;
            RequestImmediateSave();
            return true;
        }

        // ------------------------------------------------------------------ construction queues

        public int ExtraConstructionQueues => Mathf.Max(0, Shop.ExtraConstructionQueues);

        public void AddConstructionQueues(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Shop.ExtraConstructionQueues = Mathf.Clamp(Shop.ExtraConstructionQueues + amount, 0, 8);
            RequestImmediateSave();
        }

        // ------------------------------------------------------------------ offer cycle + seen badges

        public long ShopOfferCycleStartUtcTicks => Shop.OfferCycleStartUtcTicks;

        public void SetShopOfferCycleStart(long utcTicks)
        {
            Shop.OfferCycleStartUtcTicks = utcTicks;
            RequestImmediateSave();
        }

        public long ShopSeenOfferCycleUtcTicks => Shop.SeenOfferCycleUtcTicks;

        public void MarkShopOfferCycleSeen(long cycleUtcTicks)
        {
            if (Shop.SeenOfferCycleUtcTicks == cycleUtcTicks)
            {
                return;
            }

            Shop.SeenOfferCycleUtcTicks = cycleUtcTicks;
            RequestImmediateSave();
        }

        public bool IsShopProductSeen(string productId) => Shop.SeenProductIds.Contains(productId);

        public void MarkShopProductSeen(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId) || Shop.SeenProductIds.Contains(productId))
            {
                return;
            }

            Shop.SeenProductIds.Add(productId);
            RequestImmediateSave();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ------------------------------------------------------------------ debug (never in release builds)

        public void DebugSetGems(int gems)
        {
            _data.Gems = Mathf.Max(0, gems);
            RequestImmediateSave();
        }

        public void DebugSetCoins(int coins)
        {
            _data.MetaCurrency = Mathf.Max(0, coins);
            RequestImmediateSave();
        }

        public void DebugResetShopFirstBonuses()
        {
            foreach (ShopPurchaseSaveData record in Shop.Purchases)
            {
                record.FirstBonusUsed = false;
            }

            RequestImmediateSave();
        }

        /// <summary>Forgets every purchase count (one-time packs and limited offers become buyable again).</summary>
        public void DebugResetShopPurchaseCounts()
        {
            foreach (ShopPurchaseSaveData record in Shop.Purchases)
            {
                record.Count = 0;
                record.CycleCount = 0;
            }

            RequestImmediateSave();
        }

        public void DebugSetShopSubscriptionExpiry(string id, long expireUtcTicks)
        {
            ShopSubscriptionSaveData record = FindShopSubscription(id);
            if (record == null)
            {
                record = new ShopSubscriptionSaveData { Id = id };
                Shop.Subscriptions.Add(record);
            }

            record.ExpireUtcTicks = expireUtcTicks;
            if (id == VipSubscriptionId)
            {
                Shop.VipLastClaimUtcTicks = 0;
            }

            RequestImmediateSave();
        }

        public void DebugResetConstructionQueues()
        {
            Shop.ExtraConstructionQueues = 0;
            RequestImmediateSave();
        }
#endif

        private ShopPurchaseSaveData FindShopPurchase(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            foreach (ShopPurchaseSaveData record in Shop.Purchases)
            {
                if (record != null && record.ProductId == productId)
                {
                    return record;
                }
            }

            return null;
        }

        private ShopSubscriptionSaveData FindShopSubscription(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (ShopSubscriptionSaveData record in Shop.Subscriptions)
            {
                if (record != null && record.Id == id)
                {
                    return record;
                }
            }

            return null;
        }
    }
}
