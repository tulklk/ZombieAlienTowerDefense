using System.Collections.Generic;
using System.Text;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Save;
using AlienDefense.UI.Base;
using AlienDefense.UI.MainMenu;
using AlienDefense.UI.Profile;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.UI.Inventory
{
    /// <summary>Drives the Inventory tab: the UFO sockets and bonus, the part filters, the three pages (Equipment /
    /// Artifacts / Materials), the popups (detail, craft, container, rewards) and the artifact merge screen. Every
    /// rule (equip, level-up, rarity craft, recipes, merging, containers, badges) lives in InventoryService; this only
    /// binds. Refresh is event-driven (InventoryService.Changed), grids reuse pooled cards, no Update anywhere. The 3D
    /// UFO is the Profile garage preview (ProfileUfoPreviewController) borrowed while this tab is on screen.</summary>
    public sealed class InventoryScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private InventoryScreenView _view;

        [SerializeField]
        private ProfileUfoPreviewController _ufoPreview;

        [SerializeField]
        private InventoryTab _defaultTab = InventoryTab.Equipment;

        [SerializeField, Tooltip("The blurred hangar behind the top HUD (a sibling before the HUD so it stays under " +
            "it); shown only while this tab is.")]
        private GameObject _backdrop;

        private InventoryService _inventory;
        private UiViewPool<InvItemCardView> _equipmentPool;
        private UiViewPool<InvItemCardView> _artifactPool;
        private UiViewPool<InvItemCardView> _containerPool;
        private UiViewPool<InvItemCardView> _materialPool;
        private UiViewPool<InvItemCardView> _rewardPool;
        private UiViewPool<InvCraftRowView> _craftPool;
        private readonly List<EquipmentDefinition> _recipes = new List<EquipmentDefinition>();
        private readonly List<InventoryRewardLine> _rewardLines = new List<InventoryRewardLine>();
        private List<ArtifactMergePlan> _mergePlans = new List<ArtifactMergePlan>();
        private readonly StringBuilder _text = new StringBuilder(256);

        private InventoryTab _activeTab;
        private int _filter; // 0 = All, 1..6 = EquipmentSlotNames.BarOrder
        private string _detailId;
        private ArtifactEntry _detailArtifact;
        private string _materialId;
        private int _mergeRow;
        private bool _initialized;
        private bool _subscribed;
        private Tween _craftPulse;

        public InventoryTab ActiveTab => _activeTab;

        // ================================================================== lifecycle

        public void Initialize(ApplicationServices services)
        {
            _inventory = services?.Inventory;
            if (_view == null || _inventory == null)
            {
                Debug.LogError("[InventoryScreenPresenter] View or InventoryService missing.", this);
                return;
            }

            _equipmentPool = new UiViewPool<InvItemCardView>(_view.CardTemplate, _view.EquipmentGrid);
            _artifactPool = new UiViewPool<InvItemCardView>(_view.CardTemplate, _view.ArtifactGrid);
            _containerPool = new UiViewPool<InvItemCardView>(_view.CardTemplate, _view.ContainersGrid);
            _materialPool = new UiViewPool<InvItemCardView>(_view.CardTemplate, _view.MaterialsGrid);
            _rewardPool = new UiViewPool<InvItemCardView>(_view.Popups.Reward.Template, _view.Popups.Reward.Grid);
            _craftPool = new UiViewPool<InvCraftRowView>(_view.Popups.Craft.RowTemplate, _view.Popups.Craft.List);

            for (int i = 0; i < _view.Tabs.Length; i++)
            {
                var tab = (InventoryTab)i;
                if (_view.Tabs[i] != null) _view.Tabs[i].Clicked += () => ShowTab(tab);
            }

            for (int i = 0; i < _view.Filters.Length; i++)
            {
                int index = i;
                if (_view.Filters[i] != null) _view.Filters[i].Clicked += () => SelectFilter(index);
            }

            for (int i = 0; i < _view.Slots.Length; i++)
            {
                if (_view.Slots[i] != null)
                {
                    _view.Slots[i].Index = i;
                    _view.Slots[i].ClickHandler = HandleSlotClicked;
                }
            }

            _view.CraftButton?.onClick.AddListener(OpenCraft);
            _view.MergeButton?.onClick.AddListener(OpenMerge);
            InventoryPopupsView popups = _view.Popups;
            popups.Equipment.EquipButton?.onClick.AddListener(HandleEquipToggle);
            popups.Equipment.UpgradeButton?.onClick.AddListener(HandleLevelUpgrade);
            popups.Equipment.RarityButton?.onClick.AddListener(HandleRarityUpgrade);
            popups.Material.OpenOne?.onClick.AddListener(() => OpenContainer(1));
            popups.Material.OpenMax?.onClick.AddListener(() => OpenContainer(int.MaxValue));
            _view.Merge.Wire();
            _view.Merge.BackClicked += CloseMerge;
            _view.Merge.MergeClicked += HandleMerge;
            _view.Merge.RowClicked += row =>
            {
                _mergeRow = row;
                BindMerge();
            };

            _initialized = true;
            Subscribe();
            ShowTab(_defaultTab);
        }

        private void OnEnable()
        {
            if (_backdrop != null) _backdrop.SetActive(true);
            if (!_initialized)
            {
                return;
            }

            Subscribe();
            if (_view.UfoPreview != null && _ufoPreview != null)
            {
                _ufoPreview.ShowOn(_view.UfoPreview);
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            if (_backdrop != null) _backdrop.SetActive(false);
            Unsubscribe();
            if (_ufoPreview != null && _view != null)
            {
                _ufoPreview.HideFrom(_view.UfoPreview); // the preview camera renders only while this tab is shown
            }

            if (_activeTab == InventoryTab.Materials)
            {
                _inventory?.MarkAllSeen(InventoryNewKeys.ItemPrefix);
            }

            _view?.Popups?.Close();
            if (_view != null && _view.Merge != null) _view.Merge.gameObject.SetActive(false);
            _craftPulse?.Kill();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _craftPulse?.Kill();
        }

        private void Subscribe()
        {
            if (_subscribed || _inventory == null) return;
            _inventory.Changed += RefreshAll;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _inventory == null) return;
            _inventory.Changed -= RefreshAll;
            _subscribed = false;
        }

        // ================================================================== tabs + filters

        public void ShowTab(InventoryTab tab)
        {
            if (!_initialized)
            {
                return;
            }

            if (_activeTab == InventoryTab.Materials && tab != InventoryTab.Materials)
            {
                _inventory.MarkAllSeen(InventoryNewKeys.ItemPrefix); // materials were on screen: no longer new
            }

            bool changed = tab != _activeTab;
            _activeTab = tab;
            for (int i = 0; i < _view.Tabs.Length; i++)
            {
                _view.Tabs[i]?.SetSelected(i == (int)tab, changed);
            }

            _view.EquipmentPage.SetActive(tab == InventoryTab.Equipment);
            _view.ArtifactPage.SetActive(tab == InventoryTab.Artifacts);
            _view.MaterialsPage.SetActive(tab == InventoryTab.Materials);
            _view.UfoArea.SetActive(tab != InventoryTab.Artifacts);
            if (_view.Progress != null) _view.Progress.gameObject.SetActive(tab == InventoryTab.Artifacts);
            _view.FilterIcons.SetActive(tab != InventoryTab.Materials);
            _view.CraftButton.gameObject.SetActive(tab == InventoryTab.Equipment);
            _view.MergeButton.gameObject.SetActive(tab == InventoryTab.Artifacts);
            if (_view.Merge.IsOpen && tab != InventoryTab.Artifacts) _view.Merge.gameObject.SetActive(false);

            RefreshAll();
            ResetScroll();
            if (changed && _view.ContentGroup != null)
            {
                CanvasGroup group = _view.ContentGroup;
                group.DOKill();
                group.alpha = 0f;
                DOTween.To(() => group.alpha, a => group.alpha = a, 1f, 0.18f).SetUpdate(true).SetTarget(group).SetLink(gameObject);
            }
        }

        /// <summary>Android back while the Inventory is shown: closes a popup, then the merge screen (back to the
        /// Artifacts tab). False when there was nothing to close.</summary>
        public bool TryHandleBack()
        {
            if (!_initialized)
            {
                return false;
            }

            if (_view.Popups.IsOpen)
            {
                _view.Popups.Close();
                return true;
            }

            if (_view.Merge.IsOpen)
            {
                CloseMerge();
                return true;
            }

            return false;
        }

        public void ShowEquipment() => ShowTab(InventoryTab.Equipment);
        public void ShowArtifacts() => ShowTab(InventoryTab.Artifacts);
        public void ShowMaterials() => ShowTab(InventoryTab.Materials);

        public void SelectFilter(int index)
        {
            _filter = Mathf.Clamp(index, 0, EquipmentSlotNames.Count);
            RefreshAll();
            ResetScroll();
        }

        private EquipmentSlotType? FilterSlot => _filter == 0 ? (EquipmentSlotType?)null : EquipmentSlotNames.BarOrder[_filter - 1];

        private void ResetScroll()
        {
            if (_view.EquipmentScroll != null) _view.EquipmentScroll.verticalNormalizedPosition = 1f;
            if (_view.ArtifactScroll != null) _view.ArtifactScroll.verticalNormalizedPosition = 1f;
            if (_view.MaterialsScroll != null) _view.MaterialsScroll.verticalNormalizedPosition = 1f;
        }

        // ================================================================== refresh

        private void RefreshAll()
        {
            if (!_initialized || _inventory == null || !_inventory.IsReady)
            {
                return;
            }

            BindUfoArea();
            BindFilterBar();
            switch (_activeTab)
            {
                case InventoryTab.Equipment: BindEquipment(); break;
                case InventoryTab.Artifacts: BindArtifacts(); break;
                case InventoryTab.Materials: BindMaterials(); break;
            }

            _view.Tabs[0]?.SetBadge(_inventory.HasEquipmentBadge());
            _view.Tabs[1]?.SetBadge(_inventory.HasArtifactBadge());
            _view.Tabs[2]?.SetBadge(_inventory.HasMaterialsBadge());

            if (_view.Popups.IsOpen)
            {
                RefreshOpenPopup();
            }

            if (_view.Merge.IsOpen)
            {
                BindMerge();
            }
        }

        private void BindUfoArea()
        {
            if (_view.BonusText != null)
            {
                _view.BonusText.text = Mathf.RoundToInt(_inventory.GetTotalBonusPercent()) + "%";
            }

            for (int s = 0; s < _view.Slots.Length; s++)
            {
                InvItemCardView card = _view.Slots[s];
                if (card == null) continue;
                var slot = (EquipmentSlotType)s;
                EquipmentEntry e = _inventory.GetEquippedInSlot(slot);
                if (!e.Exists)
                {
                    card.ShowEmpty(_view.EmptySlotColor, Glyph(slot));
                    card.Key = null;
                    continue;
                }

                card.Key = e.Definition.Id;
                card.Show(e.Definition.Icon, Rarity(e.Rarity), Glyph(slot), "Lvl." + e.Level, null,
                    upgrade: _inventory.CanImprove(e), isNew: _inventory.IsNewEquipment(e.Definition.Id));
            }

            if (_view.Progress != null && _activeTab == InventoryTab.Artifacts)
            {
                var icons = new Sprite[EquipmentSlotNames.Count];
                foreach (ArtifactDefinition a in _inventory.Catalog.Artifacts)
                {
                    if (a != null && (int)a.Slot < icons.Length && icons[(int)a.Slot] == null) icons[(int)a.Slot] = a.Icon;
                }

                _view.Progress.Bind(_inventory.GetArtifactProgress(), icons, _view.RarityColors, _view.LockedColor);
            }
        }

        private void BindFilterBar()
        {
            for (int i = 0; i < _view.Filters.Length; i++)
            {
                InvToggleButtonView filter = _view.Filters[i];
                if (filter == null) continue;
                filter.SetSelected(i == _filter, false);
                bool badge = false;
                if (i > 0 && _activeTab == InventoryTab.Equipment)
                {
                    badge = _inventory.HasSlotBadge(EquipmentSlotNames.BarOrder[i - 1]);
                }

                filter.SetBadge(badge);
            }

            bool craftReady = _inventory.HasNewCraftAvailable();
            if (_view.CraftBadge != null) _view.CraftBadge.SetActive(craftReady);
            if (craftReady && (_craftPulse == null || !_craftPulse.IsActive()) && _view.CraftButton != null)
            {
                Transform t = _view.CraftButton.transform;
                t.localScale = Vector3.one;
                _craftPulse = t.DOScale(1.05f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(t.gameObject);
            }
            else if (!craftReady && _craftPulse != null)
            {
                _craftPulse.Kill();
                _craftPulse = null;
                if (_view.CraftButton != null) _view.CraftButton.transform.localScale = Vector3.one;
            }

            bool canMerge = _inventory.GetMergePlans().Count > 0;
            if (_view.MergeImage != null) _view.MergeImage.sprite = canMerge ? _view.ButtonGreen : _view.ButtonGrey;
        }

        // ------------------------------------------------------------------ equipment page

        private void BindEquipment()
        {
            List<EquipmentEntry> entries = _inventory.GetEquipment(FilterSlot);
            entries.Sort(CompareEquipment);
            _equipmentPool.Begin();
            foreach (EquipmentEntry e in entries)
            {
                InvItemCardView card = _equipmentPool.Take();
                card.Key = e.Definition.Id;
                card.ClickHandler = HandleEquipmentClicked;
                card.Show(e.Definition.Icon, Rarity(e.Rarity), Glyph(e.Definition.Slot), "Lvl." + e.Level,
                    e.Duplicates > 0 ? "+" + e.Duplicates : null, upgrade: _inventory.CanImprove(e),
                    isNew: _inventory.IsNewEquipment(e.Definition.Id), equipped: e.Equipped);
            }

            _equipmentPool.End();
            SetEmpty(_view.EquipmentEmpty, entries.Count == 0, "No equipment available.\nCraft your first piece!");
        }

        private int CompareEquipment(EquipmentEntry a, EquipmentEntry b)
        {
            int c = _inventory.IsNewEquipment(b.Definition.Id).CompareTo(_inventory.IsNewEquipment(a.Definition.Id));
            if (c != 0) return c;
            c = _inventory.CanImprove(b).CompareTo(_inventory.CanImprove(a));
            if (c != 0) return c;
            c = b.Rarity.CompareTo(a.Rarity);
            if (c != 0) return c;
            c = b.Level.CompareTo(a.Level);
            return c != 0 ? c : string.CompareOrdinal(a.Definition.DisplayName, b.Definition.DisplayName);
        }

        private void HandleSlotClicked(InvItemCardView card)
        {
            if (!string.IsNullOrEmpty(card.Key))
            {
                OpenEquipmentDetail(card.Key);
                return;
            }

            // Empty socket: show that part's pieces.
            var slot = (EquipmentSlotType)card.Index;
            int index = System.Array.IndexOf(EquipmentSlotNames.BarOrder, slot) + 1;
            if (_activeTab != InventoryTab.Equipment) ShowTab(InventoryTab.Equipment);
            SelectFilter(index);
        }

        private void HandleEquipmentClicked(InvItemCardView card) => OpenEquipmentDetail(card.Key);

        private void OpenEquipmentDetail(string id)
        {
            _detailId = id;
            _inventory.MarkSeen(InventoryNewKeys.Equipment(id));
            BindEquipmentDetail();
            _view.Popups.Open(_view.Popups.Equipment.Root);
            RefreshAll();
        }

        private void BindEquipmentDetail()
        {
            InventoryPopupsView.EquipmentPanel p = _view.Popups.Equipment;
            if (!_inventory.TryGetOwnedEquipment(_detailId, out EquipmentEntry e))
            {
                _view.Popups.Close();
                return;
            }

            EquipmentDefinition d = e.Definition;
            p.Card.Show(d.Icon, Rarity(e.Rarity), Glyph(d.Slot), "Lvl." + e.Level, null, equipped: e.Equipped);
            p.Name.text = d.DisplayName;
            p.Rarity.text = e.Rarity.ToString();
            p.Rarity.color = Color.Lerp(Rarity(e.Rarity), Color.white, 0.45f);
            EquipmentLevelPlan level = _inventory.GetLevelPlan(d.Id);
            p.Level.text = "Lvl. " + e.Level + " / " + level.MaxLevel;
            _text.Clear();
            _text.Append(EquipmentSlotNames.Get(d.Slot)).Append('\n');
            _text.Append("UFO bonus: <color=#7CFF6B>+").Append(Mathf.RoundToInt(_inventory.GetPieceBonusPercent(e))).Append("%</color>\n");
            _text.Append("Weapon power: <color=#FFE35A>").Append(Mathf.RoundToInt(_inventory.GetPieceWeaponPower(e))).Append("</color>");
            if (d.TryGetTier(e.Rarity, out EquipmentDefinition.Tier tier))
            {
                _text.Append('\n').Append(d.AbilityDisplayName).Append(": +").Append(Mathf.RoundToInt(tier.AbilityPercent * d.LevelMultiplier(e.Level))).Append('%');
            }

            p.Stats.text = _text.ToString();
            p.State.text = e.Equipped ? "Equipped" : (e.Duplicates > 0 ? "Spare copies: " + e.Duplicates : "");
            p.EquipLabel.text = e.Equipped ? "Unequip" : "Equip";

            bool canLevel = level.CanUpgrade;
            p.UpgradeButton.interactable = canLevel;
            p.UpgradeImage.sprite = canLevel ? _view.ButtonGreen : _view.ButtonGrey;
            p.UpgradeLabel.text = level.IsMaxLevel ? "Max level" : "Upgrade";
            p.UpgradeCost.text = level.IsMaxLevel ? "" : Cost(level.CoinCost, level.CoinsOwned, level.MaterialId, level.MaterialCost, level.MaterialOwned);

            EquipmentCraftPlan rarity = _inventory.GetCraftPlan(d.Id);
            bool hasNext = !rarity.IsMaxRarity && rarity.Definition != null;
            p.RarityButton.gameObject.SetActive(hasNext);
            if (hasNext)
            {
                p.RarityButton.interactable = rarity.CanCraft;
                p.RarityImage.sprite = rarity.CanCraft ? _view.ButtonGreen : _view.ButtonGrey;
                p.RarityCost.text = "Rarity up -> " + rarity.Next.Rarity + "\n" + Amount("Copies", rarity.DuplicatesOwned, rarity.DuplicatesRequired)
                    + "  " + Amount("Coins", rarity.CurrencyOwned, rarity.CurrencyRequired);
            }
        }

        private void HandleEquipToggle()
        {
            if (!_inventory.TryGetOwnedEquipment(_detailId, out EquipmentEntry e))
            {
                return;
            }

            if (e.Equipped)
            {
                _inventory.Unequip(_detailId);
            }
            else
            {
                _inventory.Equip(_detailId);
                _view.Slots[(int)e.Definition.Slot]?.Pulse();
            }
        }

        private void HandleLevelUpgrade()
        {
            if (!_inventory.TryGetOwnedEquipment(_detailId, out EquipmentEntry before))
            {
                return;
            }

            float bonus = _inventory.GetPieceBonusPercent(before);
            if (_inventory.TryUpgradeLevel(_detailId) && _inventory.TryGetOwnedEquipment(_detailId, out EquipmentEntry after))
            {
                _view.Popups.PlayGain("+" + Mathf.Max(1, Mathf.RoundToInt(_inventory.GetPieceBonusPercent(after) - bonus)) + "%");
                _view.Popups.Equipment.Card.Pulse();
                if (after.Equipped) _view.Slots[(int)after.Definition.Slot]?.Pulse();
            }
        }

        private void HandleRarityUpgrade()
        {
            if (_inventory.TryCraftEquipment(_detailId))
            {
                _view.Popups.PlayGain("Rarity up!");
                _view.Popups.Equipment.Card.Pulse();
            }
        }

        // ------------------------------------------------------------------ craft

        private void OpenCraft()
        {
            BindCraft();
            _view.Popups.Open(_view.Popups.Craft.Root);
            if (_view.Popups.Craft.Scroll != null) _view.Popups.Craft.Scroll.verticalNormalizedPosition = 1f;
        }

        private void BindCraft()
        {
            _inventory.GetRecipes(FilterSlot, _recipes);
            _recipes.Sort((a, b) =>
            {
                int c = _inventory.CanCraft(b).CompareTo(_inventory.CanCraft(a));
                return c != 0 ? c : ((int)a.Slot * 10 + (int)a.BaseRarity).CompareTo((int)b.Slot * 10 + (int)b.BaseRarity);
            });
            _craftPool.Begin();
            foreach (EquipmentDefinition d in _recipes)
            {
                InvCraftRowView row = _craftPool.Take();
                row.Key = d.Id;
                row.ClickHandler = HandleCraftRow;
                bool owned = _inventory.TryGetOwnedEquipment(d.Id, out _);
                row.Result.Show(d.Icon, Rarity(d.BaseRarity), Glyph(d.Slot), null, null);
                _text.Clear();
                foreach (MetaItemAmount cost in d.Recipe)
                {
                    string name = _inventory.TryGetDefinition(cost.ItemId, out MetaItemDefinition item) ? item.DisplayName : cost.ItemId;
                    _text.Append(Amount(name, _inventory.GetOwnedAmount(cost.ItemId), cost.Amount)).Append('\n');
                }

                if (d.RecipeCoins > 0) _text.Append(Amount("Coins", _inventory.Coins, d.RecipeCoins));
                row.Bind(d.DisplayName + (owned ? "  <size=70%><color=#BFE6FF>(owned: +1 copy)</color></size>" : ""),
                    _text.ToString(), _inventory.CanCraft(d), "Craft");
            }

            _craftPool.End();
            SetEmpty(_view.Popups.Craft.Empty, _recipes.Count == 0, "No recipes for this part.");
        }

        private void HandleCraftRow(InvCraftRowView row)
        {
            if (!_inventory.Catalog.TryGetEquipment(row.Key, out EquipmentDefinition d) || !_inventory.TryCraft(d))
            {
                return;
            }

            _rewardPool.Begin();
            InvItemCardView card = _rewardPool.Take();
            card.ClickHandler = null;
            card.Show(d.Icon, Rarity(d.BaseRarity), Glyph(d.Slot), null, null, label: d.DisplayName);
            card.Pop(0.15f, 0.3f);
            _rewardPool.End();
            _view.Popups.Reward.Title.text = "Crafted!";
            _view.Popups.Open(_view.Popups.Reward.Root);
            _view.Popups.PlayRewardFx();
        }

        // ------------------------------------------------------------------ artifacts page + merge

        private void BindArtifacts()
        {
            List<ArtifactEntry> artifacts = _inventory.GetArtifacts();
            EquipmentSlotType? slot = FilterSlot;
            artifacts.RemoveAll(a => slot.HasValue && a.Definition.Slot != slot.Value);
            artifacts.Sort((a, b) =>
            {
                int c = _inventory.IsNewArtifact(b.Definition.Id, b.Rarity).CompareTo(_inventory.IsNewArtifact(a.Definition.Id, a.Rarity));
                if (c != 0) return c;
                c = b.Rarity.CompareTo(a.Rarity);
                return c != 0 ? c : string.CompareOrdinal(a.Definition.DisplayName, b.Definition.DisplayName);
            });

            _artifactPool.Begin();
            foreach (ArtifactEntry a in artifacts)
            {
                InvItemCardView card = _artifactPool.Take();
                card.Key = a.Definition.Id;
                card.Index = (int)a.Rarity;
                card.ClickHandler = HandleArtifactClicked;
                bool mergeable = a.Definition.CanMergeFrom(a.Rarity) && a.Amount >= a.Definition.MergeInputCount;
                card.Show(a.Definition.Icon, Rarity(a.Rarity), Glyph(a.Definition.Slot), null, a.Amount > 1 ? "x" + a.Amount : null,
                    upgrade: mergeable, newTag: _inventory.IsNewArtifact(a.Definition.Id, a.Rarity));
            }

            _artifactPool.End();
            SetEmpty(_view.ArtifactEmpty, artifacts.Count == 0, "No artifacts available.");
        }

        private void HandleArtifactClicked(InvItemCardView card)
        {
            var rarity = (MetaItemRarity)card.Index;
            _detailArtifact = default;
            foreach (ArtifactEntry a in _inventory.GetArtifacts())
            {
                if (a.Definition.Id == card.Key && a.Rarity == rarity)
                {
                    _detailArtifact = a;
                    break;
                }
            }

            if (!_detailArtifact.Exists)
            {
                return;
            }

            _inventory.MarkSeen(InventoryNewKeys.Artifact(card.Key, rarity));
            BindArtifactDetail();
            _view.Popups.Open(_view.Popups.Artifact.Root);
            RefreshAll();
        }

        private void BindArtifactDetail()
        {
            InventoryPopupsView.ArtifactPanel p = _view.Popups.Artifact;
            ArtifactEntry a = _detailArtifact;
            ArtifactDefinition d = a.Definition;
            p.Card.Show(d.Icon, Rarity(a.Rarity), Glyph(d.Slot), null, a.Amount > 1 ? "x" + a.Amount : null);
            p.Name.text = d.DisplayName;
            p.Rarity.text = a.Rarity + "  -  " + EquipmentSlotNames.Get(d.Slot);
            p.Rarity.color = Color.Lerp(Rarity(a.Rarity), Color.white, 0.45f);
            p.Effect.text = "+" + d.GetBonusPercent(a.Rarity) + "% " + d.EffectName;
            p.Progress.text = d.CanMergeFrom(a.Rarity)
                ? "Merge: " + Amount("copies", a.Amount, d.MergeInputCount) + " -> 1 " + (a.Rarity + 1)
                : "Max rarity";
            p.Description.text = d.Description;
        }

        private void OpenMerge()
        {
            _mergeRow = 0;
            _view.Merge.gameObject.SetActive(true);
            BindMerge();
        }

        private void CloseMerge()
        {
            _view.Merge.gameObject.SetActive(false);
            RefreshAll();
        }

        private void BindMerge()
        {
            _mergePlans = _inventory.GetMergePlans();
            InvMergeView.Row[] rows = _view.Merge.Rows;
            if (_mergeRow >= _mergePlans.Count) _mergeRow = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                InvMergeView.Row row = rows[r];
                bool has = r < _mergePlans.Count;
                if (row.Highlight != null) row.Highlight.SetActive(has && r == _mergeRow);
                if (row.Button != null) row.Button.interactable = has;
                for (int s = 0; s < row.Sources.Length; s++)
                {
                    if (has && s < _mergePlans[r].InputCount)
                    {
                        ArtifactMergePlan plan = _mergePlans[r];
                        row.Sources[s].Show(plan.Definition.Icon, Rarity(plan.SourceRarity), null, null, null);
                    }
                    else
                    {
                        row.Sources[s].ShowEmpty(_view.EmptySlotColor, null);
                    }
                }

                if (has)
                {
                    ArtifactMergePlan plan = _mergePlans[r];
                    row.Result.Show(plan.Definition.Icon, Rarity(plan.ResultRarity), Glyph(plan.Definition.Slot), null, null);
                }
                else
                {
                    row.Result.ShowEmpty(_view.EmptySlotColor, null);
                }
            }

            _view.Merge.SetMessage(_mergePlans.Count == 0 ? "There are no artifacts available for merging" : null);
            _view.Merge.SetMergeEnabled(_mergePlans.Count > 0);
        }

        private void HandleMerge()
        {
            if (_mergeRow >= _mergePlans.Count)
            {
                return;
            }

            int row = _mergeRow;
            if (_inventory.TryMergeArtifact(_mergePlans[row]))
            {
                _view.Merge.PlayFlash();
                InvMergeView.Row[] rows = _view.Merge.Rows;
                if (row < rows.Length) rows[row].Result.Pop(0f, 0.4f);
            }
        }

        // ------------------------------------------------------------------ materials page + containers

        private void BindMaterials()
        {
            List<MaterialEntry> containers = _inventory.GetContainers();
            List<MaterialEntry> materials = _inventory.GetMaterials();
            SortByCatalog(materials);
            BindItems(_containerPool, containers, true);
            BindItems(_materialPool, materials, false);
            _view.ContainersSection.SetActive(containers.Count > 0);
            _view.MaterialsSection.SetActive(materials.Count > 0);
            SetEmpty(_view.MaterialsEmpty, containers.Count == 0 && materials.Count == 0, "No materials available.");
        }

        private void BindItems(UiViewPool<InvItemCardView> pool, List<MaterialEntry> entries, bool containers)
        {
            pool.Begin();
            foreach (MaterialEntry m in entries)
            {
                InvItemCardView card = pool.Take();
                card.Key = m.Definition.Id;
                card.ClickHandler = HandleMaterialClicked;
                card.Show(m.Definition.Icon, m.Definition.HeaderColor, null, null, ShopCompact(m.Amount),
                    isNew: containers || _inventory.IsNewItem(m.Definition.Id));
            }

            pool.End();
        }

        private void SortByCatalog(List<MaterialEntry> entries)
        {
            IReadOnlyList<MetaItemDefinition> order = _inventory.Catalog.Items;
            entries.Sort((a, b) => IndexOf(order, a.Definition).CompareTo(IndexOf(order, b.Definition)));
        }

        private static int IndexOf(IReadOnlyList<MetaItemDefinition> list, MetaItemDefinition item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == item) return i;
            }

            return int.MaxValue;
        }

        private void HandleMaterialClicked(InvItemCardView card)
        {
            _materialId = card.Key;
            BindMaterialDetail();
            _view.Popups.Open(_view.Popups.Material.Root);
        }

        private void BindMaterialDetail()
        {
            InventoryPopupsView.MaterialPanel p = _view.Popups.Material;
            if (!_inventory.TryGetDefinition(_materialId, out MetaItemDefinition d))
            {
                return;
            }

            int owned = _inventory.GetOwnedAmount(d.Id);
            if (owned <= 0)
            {
                _view.Popups.Close();
                return;
            }

            p.Card.Show(d.Icon, d.HeaderColor, null, null, ShopCompact(owned));
            p.Name.text = d.DisplayName;
            p.Description.text = d.Description;
            p.Amount.text = "Owned: " + owned.ToString("#,0");
            bool container = d.Kind == MetaItemKind.Container;
            p.OpenOne.gameObject.SetActive(container);
            p.OpenMax.gameObject.SetActive(container && owned > 1);
            if (p.OpenMaxLabel != null) p.OpenMaxLabel.text = "Open x" + owned;
        }

        private void OpenContainer(int count)
        {
            if (!_inventory.TryOpenContainer(_materialId, count, _rewardLines))
            {
                return;
            }

            _rewardPool.Begin();
            for (int i = 0; i < _rewardLines.Count; i++)
            {
                InventoryRewardLine line = _rewardLines[i];
                InvItemCardView card = _rewardPool.Take();
                card.ClickHandler = null;
                ResolveReward(line, out Sprite icon, out Color bg, out string name);
                card.Show(icon, bg, null, null, ShopCompact(line.Amount), label: name);
                card.Pop(0.12f + i * 0.07f, 0.3f);
            }

            _rewardPool.End();
            _view.Popups.Reward.Title.text = "Rewards!";
            _view.Popups.Open(_view.Popups.Reward.Root);
            _view.Popups.PlayRewardFx();
        }

        private void ResolveReward(in InventoryRewardLine line, out Sprite icon, out Color bg, out string name)
        {
            icon = null;
            bg = _view.RarityColors.Length > 2 ? _view.RarityColors[2] : Color.cyan;
            name = line.Id;
            switch (line.Kind)
            {
                case ContainerRewardKind.Coins:
                    icon = _view.CoinIcon;
                    bg = new Color(0.23f, 0.7f, 0.3f);
                    name = "Coins";
                    break;
                case ContainerRewardKind.Gems:
                    icon = _view.GemIcon;
                    name = "Crystals";
                    break;
                case ContainerRewardKind.Item:
                    if (_inventory.TryGetDefinition(line.Id, out MetaItemDefinition item))
                    {
                        icon = item.Icon;
                        bg = item.HeaderColor;
                        name = item.DisplayName;
                    }

                    break;
                case ContainerRewardKind.Artifact:
                    if (_inventory.Catalog.TryGetArtifact(line.Id, out ArtifactDefinition artifact))
                    {
                        icon = artifact.Icon;
                        bg = Rarity(artifact.BaseRarity);
                        name = artifact.DisplayName;
                    }

                    break;
            }
        }

        // ------------------------------------------------------------------ popups

        private void RefreshOpenPopup()
        {
            RectTransform open = _view.Popups.OpenPanel;
            if (open == _view.Popups.Equipment.Root) BindEquipmentDetail();
            else if (open == _view.Popups.Craft.Root) BindCraft();
            else if (open == _view.Popups.Material.Root) BindMaterialDetail();
        }

        // ================================================================== helpers

        private Color Rarity(MetaItemRarity rarity)
        {
            int i = (int)rarity;
            return i >= 0 && i < _view.RarityColors.Length ? _view.RarityColors[i] : Color.gray;
        }

        private Sprite Glyph(EquipmentSlotType slot)
        {
            int i = (int)slot;
            return i >= 0 && i < _view.SlotGlyphs.Length ? _view.SlotGlyphs[i] : null;
        }

        private static string ShopCompact(int amount) => CurrencyFormatter.Format(amount);

        private static string Amount(string name, int owned, int required)
        {
            string color = owned >= required ? "#7CFF6B" : "#FF6B5E";
            return name + " <color=" + color + ">" + CurrencyFormatter.Format(owned) + "/" + CurrencyFormatter.Format(required) + "</color>";
        }

        private string Cost(int coins, int coinsOwned, string materialId, int materialCost, int materialOwned)
        {
            string text = Amount("Coins", coinsOwned, coins);
            if (materialCost > 0)
            {
                string name = _inventory.TryGetDefinition(materialId, out MetaItemDefinition m) ? m.DisplayName : materialId;
                text += "   " + Amount(name, materialOwned, materialCost);
            }

            return text;
        }

        private static void SetEmpty(TMPro.TMP_Text text, bool empty, string message)
        {
            if (text == null) return;
            text.gameObject.SetActive(empty);
            if (empty) text.text = message;
        }
    }
}
