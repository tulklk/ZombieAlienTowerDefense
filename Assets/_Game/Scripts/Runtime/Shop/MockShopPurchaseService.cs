using System;

namespace AlienDefense.Shop
{
    /// <summary>Stand-in store until Unity IAP is set up: no payment happens. It only approves purchases in the Editor
    /// and in development builds; a release build reports the store as unavailable, so nothing is ever handed out
    /// for free in a shipped game. <see cref="NextResult"/> lets the Editor debug menu rehearse a failure or a
    /// cancel.</summary>
    public sealed class MockShopPurchaseService : IShopPurchaseService
    {
        public enum MockOutcome
        {
            Succeed = 0,
            Cancel = 1,
            Fail = 2,
        }

        private readonly bool _allowed;
        private int _counter;

        public MockShopPurchaseService()
        {
            _allowed = UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild;
        }

        /// <summary>Outcome of the next Purchase only; resets to Succeed afterwards.</summary>
        public MockOutcome NextResult { get; set; }

        public bool IsAvailable => _allowed;
        public bool IsTestStore => true;

        public string GetLocalizedPrice(ShopItemData item) => null;

        public void Purchase(ShopItemData item, Action<ShopStoreResult> onComplete)
        {
            MockOutcome outcome = NextResult;
            NextResult = MockOutcome.Succeed;
            if (!_allowed)
            {
                onComplete?.Invoke(ShopStoreResult.Failed("The store is not available in this build."));
                return;
            }

            switch (outcome)
            {
                case MockOutcome.Cancel:
                    onComplete?.Invoke(ShopStoreResult.Canceled());
                    return;
                case MockOutcome.Fail:
                    onComplete?.Invoke(ShopStoreResult.Failed("Payment declined (test)."));
                    return;
            }

            _counter++;
            string transactionId = "mock-" + DateTime.UtcNow.Ticks.ToString("x") + "-" + _counter;
            onComplete?.Invoke(ShopStoreResult.Succeeded(transactionId));
        }
    }
}
