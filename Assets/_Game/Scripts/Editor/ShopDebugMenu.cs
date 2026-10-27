using AlienDefense.Core;
using AlienDefense.Save;
using AlienDefense.Shop;
using AlienDefense.UI.Shop;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Editor-only Shop test controls (Play mode): set currencies, reset first-purchase bonuses / one-time
    /// packs, turn VIP on or off, end the limited offers or privilege cards, and make the mock store fail or cancel
    /// its next purchase. Lives in the Editor assembly, so none of it reaches a build.</summary>
    public static class ShopDebugMenu
    {
        private const string Root = "AlienDefense/Debug/Shop/";

        [MenuItem(Root + "Set Gems 10000")]
        private static void SetGems() => WithProfile((profile, shop) => profile.DebugSetGems(10000));

        [MenuItem(Root + "Set Coins 500000")]
        private static void SetCoins() => WithProfile((profile, shop) => profile.DebugSetCoins(500000));

        [MenuItem(Root + "Reset First Purchase Bonuses")]
        private static void ResetFirst() => WithProfile((profile, shop) => profile.DebugResetShopFirstBonuses());

        [MenuItem(Root + "Reset One-Time Packs And Limits")]
        private static void ResetOneTime() => WithProfile((profile, shop) => profile.DebugResetShopPurchaseCounts());

        [MenuItem(Root + "Reset Construction Queues")]
        private static void ResetQueues() => WithProfile((profile, shop) => profile.DebugResetConstructionQueues());

        [MenuItem(Root + "Activate VIP (10 days)")]
        private static void ActivateVip() => WithProfile((profile, shop) => shop.DebugActivateVip(10));

        [MenuItem(Root + "Expire VIP")]
        private static void ExpireVip() => WithProfile((profile, shop) => shop.DebugExpireVip());

        [MenuItem(Root + "Expire Privilege Cards")]
        private static void ExpirePrivileges() => WithProfile((profile, shop) => shop.DebugExpirePrivileges());

        [MenuItem(Root + "Expire Resource Packs (5 s)")]
        private static void ExpireOffers() => WithProfile((profile, shop) => shop.DebugExpireOffers());

        [MenuItem(Root + "Mock Store - Fail Next Purchase")]
        private static void FailNext() => WithPresenter(p => p.MockStore.NextResult = MockShopPurchaseService.MockOutcome.Fail);

        [MenuItem(Root + "Mock Store - Cancel Next Purchase")]
        private static void CancelNext() => WithPresenter(p => p.MockStore.NextResult = MockShopPurchaseService.MockOutcome.Cancel);

        private static void WithProfile(System.Action<PlayerProfileService, ShopService> action)
        {
            WithPresenter(presenter =>
            {
                var root = Object.FindFirstObjectByType<ApplicationCompositionRoot>();
                PlayerProfileService profile = root != null ? root.Services?.PlayerProfileService : null;
                if (profile == null || presenter.Service == null)
                {
                    Debug.LogWarning("[ShopDebugMenu] Profile or shop service not ready.");
                    return;
                }

                action(profile, presenter.Service);
                presenter.Service.DebugNotifyChanged();
                Debug.Log("[ShopDebugMenu] Done.");
            });
        }

        private static void WithPresenter(System.Action<ShopScreenPresenter> action)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ShopDebugMenu] Enter Play mode first.");
                return;
            }

            var presenter = Object.FindFirstObjectByType<ShopScreenPresenter>(FindObjectsInactive.Include);
            if (presenter == null || presenter.MockStore == null)
            {
                Debug.LogWarning("[ShopDebugMenu] Open the MainMenu (Shop not initialised yet).");
                return;
            }

            action(presenter);
        }
    }
}
