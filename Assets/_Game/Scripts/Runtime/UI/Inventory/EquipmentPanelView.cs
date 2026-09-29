using System.Collections.Generic;
using AlienDefense.Meta;
using AlienDefense.UI.Base;
using AlienDefense.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The Equipment tab: the six sockets around the UFO preview, the category bar, the owned-equipment
    /// grid, and the craft screen that replaces the grid when a piece is tapped.
    ///
    /// The preview area (UFO + sockets) stays up while the craft screen is open, matching the reference - only
    /// the lower half swaps. That is why the detail panel is a sibling of the grid rather than a full-screen
    /// overlay.</summary>
    public sealed class EquipmentPanelView : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField]
        [Tooltip("Category bar + grid + detail. This is what the tab bar shows and hides - NOT this component's " +
            "own GameObject, because the UFO preview and its sockets stay visible on every tab and are bound by " +
            "this same component.")]
        private GameObject _tabContentRoot;

        [Header("Preview area")]
        [SerializeField]
        [Tooltip("The six sockets. Each one carries the slot it represents.")]
        private EquipmentSlotView[] _slots = new EquipmentSlotView[6];

        [SerializeField]
        [Tooltip("Optional. Crafting currency shown above the UFO.")]
        private TMP_Text _craftCurrencyText;

        [Header("Category bar")]
        [SerializeField]
        private EquipmentCategoryButtonView[] _categoryButtons = System.Array.Empty<EquipmentCategoryButtonView>();

        [Header("Inventory grid")]
        [SerializeField]
        private GameObject _gridRoot;

        [SerializeField]
        private Transform _grid;

        [SerializeField]
        private EquipmentCardView _cardPrefab;

        [SerializeField]
        private GameObject _emptyState;

        [SerializeField]
        private TMP_Text _emptyStateText;

        [SerializeField]
        private string _emptyMessage = "No equipment available";

        [SerializeField]
        private ScrollRect _gridScroll;

        [Header("Detail")]
        [SerializeField]
        private EquipmentDetailView _detail;

        [Header("Shared")]
        [SerializeField]
        private RarityPalette _rarityPalette;

        private UiViewPool<EquipmentCardView> _cardPool;
        private System.Action<string> _cardClicked;
        private EquipmentCategoryButtonView _selectedCategory;

        public EquipmentDetailView Detail => _detail;

        /// <summary>Null means "All". Read by the presenter when it refreshes the grid.</summary>
        public EquipmentSlotType? SelectedSlot =>
            _selectedCategory != null && !_selectedCategory.IsAll ? _selectedCategory.Slot : (EquipmentSlotType?)null;

        public bool IsDetailOpen => _detail != null && _detail.gameObject.activeSelf;

        /// <summary>Shown only on the Equipment tab. The sockets above it stay up regardless.</summary>
        public void SetTabContentVisible(bool visible)
        {
            if (_tabContentRoot != null)
            {
                _tabContentRoot.SetActive(visible);
            }
        }

        public void Configure(System.Action<string> cardClicked, System.Action categoryChanged)
        {
            _cardClicked = cardClicked;

            if (_cardPool == null && _cardPrefab != null && _grid != null)
            {
                _cardPool = new UiViewPool<EquipmentCardView>(_cardPrefab, _grid);
            }

            if (_emptyStateText != null)
            {
                _emptyStateText.text = _emptyMessage;
            }

            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                if (_categoryButtons[i] == null)
                {
                    continue;
                }

                EquipmentCategoryButtonView button = _categoryButtons[i];
                button.Configure(clicked =>
                {
                    SelectCategory(clicked);
                    categoryChanged?.Invoke();
                });

                if (_selectedCategory == null && button.IsAll)
                {
                    _selectedCategory = button;
                }
            }

            // No button was marked All - fall back to the first so the bar is never drawn with nothing selected.
            if (_selectedCategory == null && _categoryButtons.Length > 0)
            {
                _selectedCategory = _categoryButtons[0];
            }

            RefreshCategorySelection();
        }

        public void ShowGrid()
        {
            if (_gridRoot != null)
            {
                _gridRoot.SetActive(true);
            }

            if (_detail != null)
            {
                _detail.gameObject.SetActive(false);
            }
        }

        public void ShowDetail()
        {
            if (_gridRoot != null)
            {
                _gridRoot.SetActive(false);
            }

            if (_detail != null)
            {
                _detail.gameObject.SetActive(true);
            }
        }

        public void BindSlots(InventoryService inventory)
        {
            if (inventory == null)
            {
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    continue;
                }

                _slots[i].Bind(inventory.GetEquippedInSlot(_slots[i].Slot), _rarityPalette, _cardClicked);
            }
        }

        public void BindCurrency(int amount)
        {
            if (_craftCurrencyText != null)
            {
                _craftCurrencyText.text = CurrencyFormatter.Format(amount);
            }
        }

        public void BindGrid(IReadOnlyList<EquipmentEntry> entries)
        {
            int count = 0;
            if (_cardPool != null)
            {
                _cardPool.Begin();
                if (entries != null)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].Definition == null)
                        {
                            continue;
                        }

                        EquipmentCardView card = _cardPool.Take();
                        if (card == null)
                        {
                            break;
                        }

                        card.Bind(entries[i], _rarityPalette, _cardClicked);
                        count++;
                    }
                }

                _cardPool.End();
            }

            if (_emptyState != null)
            {
                _emptyState.SetActive(count == 0);
            }
        }

        /// <summary>Puts the grid back at the top after a category change, so the player is never dropped into the
        /// middle of a shorter list.</summary>
        public void ResetScroll()
        {
            if (_gridScroll != null)
            {
                _gridScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void SelectCategory(EquipmentCategoryButtonView button)
        {
            _selectedCategory = button;
            RefreshCategorySelection();
            ResetScroll();
        }

        private void RefreshCategorySelection()
        {
            for (int i = 0; i < _categoryButtons.Length; i++)
            {
                if (_categoryButtons[i] != null)
                {
                    _categoryButtons[i].SetSelected(_categoryButtons[i] == _selectedCategory);
                }
            }
        }
    }
}
