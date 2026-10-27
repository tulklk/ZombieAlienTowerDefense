using System;
using System.Collections.Generic;

namespace AlienDefense.Save
{
    /// <summary>Everything the Shop remembers: purchase counts (one-time packs, limited offers per cycle, first-
    /// purchase bonuses), granted store transactions (so a duplicated store callback never pays twice), VIP and
    /// privilege-card expiry, extra construction queues and which offers the player has already seen.</summary>
    [Serializable]
    public sealed class ShopSaveData
    {
        public List<ShopPurchaseSaveData> Purchases = new List<ShopPurchaseSaveData>();

        /// <summary>Most recent granted store transaction ids (capped, oldest dropped).</summary>
        public List<string> Transactions = new List<string>();

        /// <summary>VIP ("vip") and privilege cards (by product id).</summary>
        public List<ShopSubscriptionSaveData> Subscriptions = new List<ShopSubscriptionSaveData>();

        public string VipPlanId;
        public long VipLastClaimUtcTicks;

        /// <summary>Permanent extra Base construction queues bought in the Shop.</summary>
        public int ExtraConstructionQueues;

        /// <summary>Start of the current limited-offer cycle (0 = not started yet).</summary>
        public long OfferCycleStartUtcTicks;

        /// <summary>The offer cycle the player last looked at (Resource Packs badge).</summary>
        public long SeenOfferCycleUtcTicks;

        /// <summary>Products whose "new" badge has been seen (Campaign Packs badge).</summary>
        public List<string> SeenProductIds = new List<string>();
    }

    [Serializable]
    public sealed class ShopPurchaseSaveData
    {
        public string ProductId;
        public int Count;
        public bool FirstBonusUsed;

        /// <summary>Offer cycle of <see cref="CycleCount"/> (limited offers count per cycle).</summary>
        public long CycleUtcTicks;
        public int CycleCount;
    }

    [Serializable]
    public sealed class ShopSubscriptionSaveData
    {
        public string Id;
        public long ExpireUtcTicks;
    }
}
