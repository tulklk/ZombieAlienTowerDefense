using System;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Shop;
using AlienDefense.UI.MainMenu;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.UI.Shop
{
    /// <summary>Shop tab: navigation and wiring only. Builds every card once from ShopCatalog (cached per category,
    /// never rebuilt on tab switches), switches categories (fade/slide, scroll back to top, keeps the selected tab in
    /// view), routes purchases through ShopService (confirm -> store -> rewards -> popup) and forwards the 1 Hz
    /// ShopTimer tick to the visible cards only. Rules live in ShopService, display in the card views.</summary>
    // Intentionally NOT IApplicationServicesReceiver — see BaseScreenPresenter's doc comment: only one such
    // receiver per loaded scene gets pushed services, and MainMenuPresenter must be the one that wins that slot.
    // Services instead cascade in explicitly via MenuShellPresenter.Initialize.
    public sealed class ShopScreenPresenter : MonoBehaviour
    {
        private const int CategoryCount = 6;

        [SerializeField]
        private ShopScreenView _view;

        [SerializeField]
        private ShopCatalog _catalog;

        [SerializeField]
        private MenuShellPresenter _menuShell;

        private readonly List<ShopPackCardView>[] _packCards = new List<ShopPackCardView>[CategoryCount];
        private readonly List<ShopCrystalCardView> _crystalCards = new List<ShopCrystalCardView>();
        private readonly List<ShopItemData> _scratch = new List<ShopItemData>();
        private readonly List<ShopGrantedReward> _claimed = new List<ShopGrantedReward>();

        private ApplicationServices _services;
        private ShopService _shop;
        private MockShopPurchaseService _mockStore;
        private bool _built;
        private bool _hasSelection;
        private ShopCategory _selected = ShopCategory.ConstructionQueue;
        private Sequence _switchTween;
        private Vector2 _contentRestPosition;

        public ShopService Service => _shop;
        public MockShopPurchaseService MockStore => _mockStore;
        public ShopCategory SelectedCategory => _selected;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            if (_shop == null && services?.PlayerProfileService != null && _catalog != null)
            {
                _mockStore = new MockShopPurchaseService();
                _shop = new ShopService(_catalog, services.PlayerProfileService, _mockStore, services.LevelCatalog);
                _shop.Changed += HandleShopChanged;
            }

            if (!_built && _shop != null && _view != null)
            {
                BuildCards();
                WireNavigation();
                _built = true;
            }
        }

        /// <summary>Called by MenuShellPresenter whenever the Shop tab is shown.</summary>
        public void Refresh()
        {
            if (!_built)
            {
                return;
            }

            _shop.RollOfferCycleIfNeeded(DateTime.UtcNow);
            SelectCategory(_hasSelection ? _selected : ShopCategory.ConstructionQueue, animate: false);
            RefreshAll();
        }

        /// <summary>Android back / Escape while the Shop is open: closes a popup first, then leaves the Shop.</summary>
        public void HandleBack()
        {
            if (_view != null && _view.Popup != null && _view.Popup.IsOpen)
            {
                _view.Popup.Close();
                return;
            }

            _menuShell?.LeaveShop();
        }

        // ------------------------------------------------------------------ build

        private void BuildCards()
        {
            for (int c = 0; c < CategoryCount; c++)
            {
                _packCards[c] = new List<ShopPackCardView>();
                var category = (ShopCategory)c;
                _catalog.GetItems(category, _scratch);
                if (category == ShopCategory.Vip)
                {
                    _view.VipView?.Bind(_shop, _scratch.ToArray(), HandleBuy, HandleClaimVip);
                    continue;
                }

                RectTransform root = c < _view.CategoryRoots.Length ? _view.CategoryRoots[c] : null;
                foreach (ShopItemData item in _scratch)
                {
                    if (item.Layout == ShopCardLayout.Crystal)
                    {
                        ShopCrystalCardView crystal = Instantiate(_view.CrystalCardPrefab, _view.CrystalGrid);
                        crystal.Bind(item, _shop, HandleBuy);
                        _crystalCards.Add(crystal);
                        continue;
                    }

                    ShopPackCardView prefab = PrefabFor(item.Layout);
                    if (prefab == null || root == null)
                    {
                        Debug.LogWarning($"[ShopScreenPresenter] No card prefab/container for {item.ProductId} ({item.Layout}).", this);
                        continue;
                    }

                    ShopPackCardView card = Instantiate(prefab, root);
                    card.Bind(item, _shop, HandleBuy);
                    _packCards[c].Add(card);
                }
            }
        }

        private ShopPackCardView PrefabFor(ShopCardLayout layout)
        {
            switch (layout)
            {
                case ShopCardLayout.LargeFeature: return _view.LargeCardPrefab;
                case ShopCardLayout.Campaign: return _view.CampaignCardPrefab;
                case ShopCardLayout.Resource: return _view.ResourceCardPrefab;
                case ShopCardLayout.Privilege: return _view.PrivilegeCardPrefab;
            }

            return null;
        }

        private void WireNavigation()
        {
            ShopCategoryButtonView[] buttons = _view.CategoryButtons;
            for (int i = 0; i < buttons.Length; i++)
            {
                var category = (ShopCategory)i;
                if (buttons[i] != null)
                {
                    buttons[i].Clicked += () => SelectCategory(category, animate: true);
                }
            }

            _view.BackButton?.onClick.AddListener(() => _menuShell?.LeaveShop());
            if (_view.Timer != null)
            {
                _view.Timer.Tick += HandleTick;
            }

            if (_view.ContentGroup != null)
            {
                _contentRestPosition = ((RectTransform)_view.ContentGroup.transform).anchoredPosition;
            }
        }

        // ------------------------------------------------------------------ categories

        public void SelectCategory(ShopCategory category, bool animate)
        {
            if (!_built)
            {
                return;
            }

            bool changed = !_hasSelection || category != _selected;
            _hasSelection = true;
            _selected = category;
            ShopCategoryButtonView[] buttons = _view.CategoryButtons;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i]?.SetSelected(i == (int)category, animate);
            }

            _shop.RollOfferCycleIfNeeded(DateTime.UtcNow);
            _shop.MarkCategorySeen(category);
            RefreshBadges();
            KeepTabVisible(category);

            if (!changed && animate)
            {
                return;
            }

            _switchTween?.Kill();
            CanvasGroup group = _view.ContentGroup;
            if (!animate || group == null)
            {
                ShowRoot(category);
                if (group != null)
                {
                    group.alpha = 1f;
                    ((RectTransform)group.transform).anchoredPosition = _contentRestPosition;
                }

                return;
            }

            var rect = (RectTransform)group.transform;
            _switchTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(DOTween.To(() => group.alpha, a => group.alpha = a, 0f, 0.08f))
                .AppendCallback(() =>
                {
                    ShowRoot(category);
                    rect.anchoredPosition = _contentRestPosition + new Vector2(0f, -40f);
                })
                .Append(DOTween.To(() => group.alpha, a => group.alpha = a, 1f, 0.2f))
                .Join(DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p, _contentRestPosition, 0.2f).SetEase(Ease.OutCubic));
        }

        private void ShowRoot(ShopCategory category)
        {
            RectTransform[] roots = _view.CategoryRoots;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    roots[i].gameObject.SetActive(i == (int)category);
                }
            }

            RefreshCategory(category, DateTime.UtcNow);
            if (_view.ContentScroll != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_view.ContentScroll.content);
                _view.ContentScroll.StopMovement();
                _view.ContentScroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>Scrolls the bottom bar so the selected tab is fully on screen.</summary>
        private void KeepTabVisible(ShopCategory category)
        {
            UnityEngine.UI.ScrollRect scroll = _view.CategoryScroll;
            ShopCategoryButtonView[] buttons = _view.CategoryButtons;
            int index = (int)category;
            if (scroll == null || scroll.content == null || index >= buttons.Length || buttons[index] == null)
            {
                return;
            }

            RectTransform content = scroll.content;
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float overflow = content.rect.width - viewport.rect.width;
            if (overflow <= 0f)
            {
                return;
            }

            RectTransform tab = buttons[index].Rect;
            float tabLeft = tab.anchoredPosition.x - tab.rect.width * tab.pivot.x;
            float tabRight = tabLeft + tab.rect.width;
            float viewLeft = -content.anchoredPosition.x;
            float viewRight = viewLeft + viewport.rect.width;
            float target = viewLeft;
            if (tabLeft < viewLeft)
            {
                target = tabLeft - 12f;
            }
            else if (tabRight > viewRight)
            {
                target = tabRight - viewport.rect.width + 12f;
            }

            scroll.StopMovement();
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(target / overflow);
        }

        // ------------------------------------------------------------------ refresh

        private void RefreshAll()
        {
            DateTime now = DateTime.UtcNow;
            RefreshHeader();
            for (int c = 0; c < CategoryCount; c++)
            {
                RefreshCategory((ShopCategory)c, now);
            }

            RefreshBadges();
        }

        private void RefreshCategory(ShopCategory category, DateTime now)
        {
            if (category == ShopCategory.Vip)
            {
                _view.VipView?.Refresh(now);
                return;
            }

            if (category == ShopCategory.Crystals)
            {
                foreach (ShopCrystalCardView card in _crystalCards)
                {
                    card.Refresh();
                }
            }

            List<ShopPackCardView> cards = _packCards[(int)category];
            if (cards == null)
            {
                return;
            }

            foreach (ShopPackCardView card in cards)
            {
                card.Refresh(now);
            }
        }

        private void RefreshHeader()
        {
            var profile = _services?.PlayerProfileService;
            if (profile == null)
            {
                return;
            }

            if (_view.GemText != null) _view.GemText.text = CurrencyFormatter.Format(profile.Gems);
            if (_view.CoinText != null) _view.CoinText.text = CurrencyFormatter.Format(profile.MetaCurrency);
        }

        private void RefreshBadges()
        {
            DateTime now = DateTime.UtcNow;
            ShopCategoryButtonView[] buttons = _view.CategoryButtons;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i]?.SetBadge(_shop.HasBadge((ShopCategory)i, now));
            }
        }

        private void HandleTick(DateTime now)
        {
            if (!_built)
            {
                return;
            }

            if (_selected == ShopCategory.Vip)
            {
                _view.VipView?.Tick(now);
            }
            else
            {
                List<ShopPackCardView> cards = _packCards[(int)_selected];
                if (cards != null)
                {
                    for (int i = 0; i < cards.Count; i++)
                    {
                        cards[i].Tick(now);
                    }
                }
            }

            if (now.Second % 10 == 0)
            {
                RefreshBadges(); // the VIP gift unlocks at the UTC day change
            }
        }

        private void HandleShopChanged()
        {
            if (_built && isActiveAndEnabled)
            {
                RefreshAll();
            }

            _menuShell?.RefreshHud();
        }

        // ------------------------------------------------------------------ purchase

        private void HandleBuy(ShopItemData item)
        {
            if (_shop == null || item == null || _shop.IsPurchaseInFlight)
            {
                return;
            }

            ShopPopupView popup = _view.Popup;
            string price = _shop.GetPriceLabel(item);
            if (popup == null)
            {
                Buy(item);
                return;
            }

            popup.ShowConfirm(item.DisplayName, price, _shop.Store != null && _shop.Store.IsTestStore, () => Buy(item));
        }

        private void Buy(ShopItemData item)
        {
            _shop.Purchase(item, outcome =>
            {
                ShopPopupView popup = _view.Popup;
                switch (outcome.Status)
                {
                    case ShopPurchaseStatus.Success:
                        RefreshAll();
                        _menuShell?.RefreshHud();
                        popup?.ShowSuccess("Purchase Successful!", outcome.Rewards, _catalog);
                        break;
                    case ShopPurchaseStatus.Cancelled:
                        break; // nothing changes, no popup
                    default:
                        popup?.ShowError(string.IsNullOrEmpty(outcome.Message) ? "The purchase failed." : outcome.Message);
                        RefreshAll();
                        break;
                }
            });
        }

        private void HandleClaimVip()
        {
            _claimed.Clear();
            if (_shop.TryClaimVipDaily(DateTime.UtcNow, _claimed))
            {
                _view.Popup?.ShowSuccess("VIP Daily Reward", _claimed, _catalog);
            }
        }

        private void OnDestroy()
        {
            _switchTween?.Kill();
            if (_shop != null)
            {
                _shop.Changed -= HandleShopChanged;
            }

            if (_view != null && _view.Timer != null)
            {
                _view.Timer.Tick -= HandleTick;
            }
        }
    }
}
