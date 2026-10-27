using System;

namespace AlienDefense.Shop
{
    /// <summary>What the store said about one purchase attempt.</summary>
    public readonly struct ShopStoreResult
    {
        public bool Success { get; }
        public bool Cancelled { get; }
        public string TransactionId { get; }
        public string Error { get; }

        private ShopStoreResult(bool success, bool cancelled, string transactionId, string error)
        {
            Success = success;
            Cancelled = cancelled;
            TransactionId = transactionId;
            Error = error;
        }

        public static ShopStoreResult Succeeded(string transactionId) => new ShopStoreResult(true, false, transactionId, null);
        public static ShopStoreResult Canceled() => new ShopStoreResult(false, true, null, null);
        public static ShopStoreResult Failed(string error) => new ShopStoreResult(false, false, null, error);
    }

    /// <summary>The real-money store. The Shop only talks to this interface, so the mock used in the Editor today
    /// can be swapped for a Unity IAP / Google Play Billing implementation without touching the shop UI or the
    /// reward code. Implementations must call <c>onComplete</c> exactly once per Purchase (on the main thread), and
    /// return a stable transaction id: ShopService refuses to grant the same id twice.</summary>
    public interface IShopPurchaseService
    {
        /// <summary>False when payments cannot happen at all (no store in this build).</summary>
        bool IsAvailable { get; }

        /// <summary>True for the Editor mock: the confirm popup then says no real payment happens.</summary>
        bool IsTestStore { get; }

        /// <summary>Store-localized price, or null to show the catalog's VND price.</summary>
        string GetLocalizedPrice(ShopItemData item);

        void Purchase(ShopItemData item, Action<ShopStoreResult> onComplete);
    }
}
