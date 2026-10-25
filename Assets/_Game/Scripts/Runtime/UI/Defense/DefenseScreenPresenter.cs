using System.Collections.Generic;
using System.Globalization;
using AlienDefense.Core;
using AlienDefense.Meta;
using AlienDefense.Towers;
using AlienDefense.UI.Base;
using AlienDefense.UI.MainMenu;
using UnityEngine;

namespace AlienDefense.UI.Defense
{
    // Intentionally NOT IApplicationServicesReceiver — see BaseScreenPresenter's doc comment: only one such
    // receiver per loaded scene gets pushed services, and MainMenuPresenter must be the one that wins that slot.
    // Services instead cascade in explicitly via MenuShellPresenter.Initialize.
    /// <summary>Drives the Defense tab: the Tower Collection grid (the UFO Base first, then every tower of the
    /// TowerCatalog), the upgrade card inventory and the detail popup with its level-up / unlock flow. All numbers
    /// come from TowerDefinition, DefenseMetaConfig, PlayerProfileService and InventoryService; spending goes
    /// through TowerMetaUpgradeService, BaseDefenseUpgradeService and TowerUnlockService - never from a view.</summary>
    public sealed class DefenseScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private DefenseScreenView _view;

        [SerializeField]
        [Tooltip("Legacy (the old unlock list's card). Unused by the Tower Collection; kept for the old scaffolder.")]
        private TowerUnlockCardView _cardPrefab;

        [SerializeField]
        private MenuShellPresenter _menuShell;

        [SerializeField]
        private TowerDetailPopupView _popup;

        [SerializeField]
        private DefenseStatIcons _statIcons = new DefenseStatIcons();

        private readonly List<DefenseEntry> _entries = new List<DefenseEntry>();
        private readonly Dictionary<TowerDefinition, GameObject> _previewModels = new Dictionary<TowerDefinition, GameObject>();
        private UiViewPool<TowerCollectionCardView> _cardPool;
        private UiViewPool<UpgradeCardView> _upgradeCardPool;

        private ApplicationServices _services;
        private TowerUnlockService _unlockService;
        private TowerMetaUpgradeService _upgradeService;
        private BaseDefenseUpgradeService _baseService;
        private DefenseMetaConfig _config;
        private int _selected = -1;
        private bool _isInitialized;

        public void ReceiveApplicationServices(ApplicationServices services)
        {
            _services = services;
            if (services?.PlayerProfileService == null || services.TowerCatalog == null || _view == null)
            {
                Debug.LogError("[DefenseScreenPresenter] Missing profile, TowerCatalog or view; Defense tab left empty.", this);
                return;
            }

            _config = services.TowerCatalog.DefenseConfig;
            _unlockService = new TowerUnlockService(services.PlayerProfileService);
            _upgradeService = new TowerMetaUpgradeService(services.PlayerProfileService, _config);
            _baseService = new BaseDefenseUpgradeService(services.PlayerProfileService, _config);

            if (!_isInitialized)
            {
                _cardPool = new UiViewPool<TowerCollectionCardView>(_view.TowerCardTemplate, _view.TowerGrid);
                _upgradeCardPool = new UiViewPool<UpgradeCardView>(_view.UpgradeCardTemplate, _view.UpgradeCardGrid);
                WireView();
                _isInitialized = true;
            }

            Refresh();
        }

        private void WireView()
        {
            if (_view.GetCardsButton != null)
            {
                _view.GetCardsButton.onClick.AddListener(HandleGetCards);
            }

            if (_view.CritInfoButton != null)
            {
                _view.CritInfoButton.onClick.AddListener(HandleCritInfo);
            }

            if (_view.CritTooltipDismiss != null)
            {
                _view.CritTooltipDismiss.onClick.AddListener(HideCritInfo);
            }

            if (_popup != null)
            {
                _popup.CloseRequested += HandlePopupClose;
                _popup.PreviousRequested += HandlePrevious;
                _popup.NextRequested += HandleNext;
                _popup.LevelUpRequested += HandleLevelUp;
                _popup.HideImmediate();
            }

            HideCritInfo();
        }

        /// <summary>Re-reads everything and redraws the page (and the popup, if it is open).</summary>
        public void Refresh()
        {
            if (!_isInitialized)
            {
                return;
            }

            BuildEntries();
            RefreshCritPanel();
            RefreshTowerGrid();
            RefreshUpgradeCards();

            if (_popup != null && _popup.IsOpen && _selected >= 0 && _selected < _entries.Count)
            {
                _popup.Bind(_entries[_selected]);
            }
        }

        // ------------------------------------------------------------------ data

        private float CurrentCritDamage => _config != null ? _config.GetCritDamage(_upgradeService.GetTotalUpgradeLevels(_services.TowerCatalog)) : 1f;

        private void BuildEntries()
        {
            _entries.Clear();
            int coins = _services.PlayerProfileService.MetaCurrency;
            float critDamage = CurrentCritDamage;

            if (_config != null)
            {
                var entry = new DefenseEntry
                {
                    Id = "ufo_base",
                    Title = _config.BaseDisplayName,
                    Icon = _config.BaseIcon,
                    TypeIcon = _config.BaseTypeIcon,
                    IsBase = true,
                    IsUnlocked = true,
                    LevelIndex = _baseService.CurrentLevelIndex,
                    IsMaxLevel = _baseService.IsMaxLevel,
                    OwnedCoins = coins,
                    OwnedCards = _baseService.OwnedCards,
                    CardIcon = CardIcon(_config.BaseCardItemId),
                    UpgradeCheck = _baseService.CheckUpgrade(),
                    About = _config.BaseDescription,
                    PreviewModel = _config.BasePreviewModel,
                };
                _baseService.TryGetNextLevelCost(out entry.RequiredCoins, out entry.RequiredCards);
                DefenseStatBuilder.BuildBase(entry, _baseService, _config, ReferenceBaseHealth(), _statIcons);
                _entries.Add(entry);
            }

            TowerCatalog catalog = _services.TowerCatalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                TowerDefinition tower = catalog.GetTower(i);
                if (tower == null)
                {
                    continue;
                }

                bool unlocked = _unlockService.IsUnlocked(tower);
                int level = _upgradeService.GetCurrentLevelIndex(tower);
                var entry = new DefenseEntry
                {
                    Id = tower.Id,
                    Title = tower.DisplayName,
                    Icon = tower.StatsIcon,
                    TypeIcon = tower.StatsTypeIcon,
                    Tower = tower,
                    IsUnlocked = unlocked,
                    UnlockCost = tower.UnlockCost,
                    LevelIndex = level,
                    IsMaxLevel = unlocked && _upgradeService.IsMaxLevel(tower),
                    OwnedCoins = coins,
                    OwnedCards = _upgradeService.GetOwnedCards(tower),
                    CardIcon = CardIcon(tower.UpgradeCardItemId),
                    UpgradeCheck = _upgradeService.CheckUpgrade(tower),
                    UpgradeBonus = _config != null ? level * _config.CritDamagePerUpgradeLevel : 0f,
                    About = tower.Description,
                    PreviewModel = PreviewModelOf(tower),
                };

                if (unlocked)
                {
                    _upgradeService.TryGetNextLevelCost(tower, out entry.RequiredCoins, out entry.RequiredCards);
                }
                else
                {
                    entry.RequiredCoins = tower.UnlockCost;
                }


                DefenseStatBuilder.BuildTower(entry, tower, level, _config, critDamage, _statIcons);
                _entries.Add(entry);
            }
        }

        /// <summary>The purely visual branch of a tower prefab (its "Model..." child: meshes only), so the showcase
        /// never clones targeting, attack or build logic. Cached per tower; falls back to the first child that has
        /// mesh renderers.</summary>
        private GameObject PreviewModelOf(TowerDefinition tower)
        {
            if (tower == null || tower.Prefab == null)
            {
                return null;
            }

            if (_previewModels.TryGetValue(tower, out GameObject cached))
            {
                return cached;
            }

            GameObject found = null;
            Transform root = tower.Prefab.transform;
            for (int i = 0; i < root.childCount && found == null; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith("Model", System.StringComparison.Ordinal) && child.GetComponentInChildren<MeshRenderer>(true) != null)
                {
                    found = child.gameObject;
                }
            }

            for (int i = 0; i < root.childCount && found == null; i++)
            {
                Transform child = root.GetChild(i);
                if (child.gameObject.activeSelf && child.GetComponent<ParticleSystem>() == null && child.GetComponentInChildren<MeshRenderer>(false) != null)
                {
                    found = child.gameObject;
                }
            }

            _previewModels[tower] = found;
            return found;
        }

        private int CurrentForce()
        {
            return PlayerPowerCalculator.Compute(_services.PlayerProfileService, _services.TowerCatalog, _services.BaseProgression);
        }

        /// <summary>Base health the UFO Base card quotes: the first level's BaseMaxHealth (every level's value scales
        /// by the same factor).</summary>
        private int ReferenceBaseHealth()
        {
            LevelCatalog levels = _services.LevelCatalog;
            if (levels != null)
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    LevelCatalogEntry level = levels.GetEntry(i);
                    if (level != null && level.LevelDefinition != null)
                    {
                        return level.LevelDefinition.BaseMaxHealth;
                    }
                }
            }

            return 100;
        }

        private Sprite CardIcon(string itemId)
        {
            return _services.Inventory != null && _services.Inventory.TryGetDefinition(itemId, out MetaItemDefinition item) ? item.Icon : null;
        }

        // ------------------------------------------------------------------ page

        private void RefreshCritPanel()
        {
            if (_view.CritPanel != null)
            {
                _view.CritPanel.SetActive(_config != null);
            }

            if (_view.CritValue != null && _config != null)
            {
                _view.CritValue.text = DefenseStatBuilder.Percent(CurrentCritDamage);
            }
        }

        private void RefreshTowerGrid()
        {
            _cardPool.Begin();
            for (int i = 0; i < _entries.Count; i++)
            {
                TowerCollectionCardView card = _cardPool.Take();
                if (card == null)
                {
                    continue;
                }

                card.Clicked -= HandleCardClicked;
                card.Clicked += HandleCardClicked;
                card.Configure(i, _entries[i]);
            }

            // Announced towers (event / Engineer's House): locked look-only cards after the real ones.
            int announced = _config != null ? _config.ComingSoonCardCount : 0;
            for (int i = 0; i < announced; i++)
            {
                TowerCollectionCardView card = _cardPool.Take();
                if (card == null)
                {
                    continue;
                }

                DefenseMetaConfig.ComingSoonCard soon = _config.GetComingSoonCard(i);
                card.Clicked -= HandleCardClicked;
                card.Clicked += HandleCardClicked; // index -1: HandleCardClicked ignores it
                card.ConfigureComingSoon(soon.Label, soon.Icon, soon.TypeIcon, soon.IsEvent);
            }

            _cardPool.End();
        }

        private void RefreshUpgradeCards()
        {
            _upgradeCardPool.Begin();
            int shown = 0;
            if (_services.Inventory != null)
            {
                List<MaterialEntry> materials = _services.Inventory.GetMaterials();
                materials.Sort((a, b) => b.Amount.CompareTo(a.Amount));
                for (int i = 0; i < materials.Count; i++)
                {
                    MaterialEntry material = materials[i];
                    if (material.Amount <= 0 || !IsUpgradeCard(material.Definition))
                    {
                        continue;
                    }

                    UpgradeCardView card = _upgradeCardPool.Take();
                    if (card != null)
                    {
                        card.Configure(material.Definition.Icon, material.Amount);
                        shown++;
                    }
                }
            }

            _upgradeCardPool.End();
            if (_view.NoCardsLabel != null)
            {
                _view.NoCardsLabel.SetActive(shown == 0);
            }
        }

        /// <summary>An upgrade card is an item some Defense entry levels up with, or any other "card_" material
        /// (cards of towers that are not in the game yet still show in the collection's inventory).</summary>
        private bool IsUpgradeCard(MetaItemDefinition item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
            {
                return false;
            }

            if (item.Id.StartsWith("card_", System.StringComparison.Ordinal))
            {
                return true;
            }

            if (_config != null && item.Id == _config.BaseCardItemId)
            {
                return true;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Tower != null && _entries[i].Tower.UpgradeCardItemId == item.Id)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ interaction

        private void HandleCardClicked(int index)
        {
            if (_popup == null || index < 0 || index >= _entries.Count)
            {
                return;
            }

            _selected = index;
            HideCritInfo();
            _popup.Open(_entries[index], _entries.Count > 1);
            Log("Selected: " + _entries[index].Id);
        }

        private void HandlePopupClose()
        {
            _popup.Close();
        }

        private void HandlePrevious()
        {
            Step(-1);
        }

        private void HandleNext()
        {
            Step(1);
        }

        private void Step(int direction)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _selected = (_selected + direction + _entries.Count) % _entries.Count;
            _popup.SwitchTo(_entries[_selected], direction);
        }

        private void HandleLevelUp()
        {
            if (_selected < 0 || _selected >= _entries.Count)
            {
                return;
            }

            DefenseEntry entry = _entries[_selected];
            int forceBefore = CurrentForce();
            MetaUpgradeResult result;
            if (entry.IsBase)
            {
                result = _baseService.TryUpgrade();
            }
            else if (!entry.IsUnlocked)
            {
                result = _unlockService.TryUnlock(entry.Tower) ? MetaUpgradeResult.Success : MetaUpgradeResult.InsufficientCoins;
            }
            else
            {
                result = _upgradeService.TryUpgradeWithResult(entry.Tower);
            }

            if (result != MetaUpgradeResult.Success)
            {
                Log("Upgrade failed: " + result + " (" + entry.Id + ")");
                if (result == MetaUpgradeResult.InsufficientCoins)
                {
                    _popup.ShowToast(DefenseStrings.NotEnoughCoins);
                }
                else if (result == MetaUpgradeResult.InsufficientCards)
                {
                    _popup.ShowToast(DefenseStrings.NotEnoughCards);
                }

                return;
            }

            Log("Upgrade success: " + entry.Id);
            Refresh();                               // cards, crit line, inventory and the open popup, in place
            _popup.PlayUpgradeFeedback();
            _popup.PlayForceGain(forceBefore, CurrentForce()); // the existing Power total; silent when it did not change
            _menuShell?.RefreshHud();                // coins and power in the top bar
            _services.Inventory?.NotifyChanged();    // the Inventory tab's material grid
        }

        private void HandleGetCards()
        {
            // Cards come from the shop's exchange and from level rewards; there is no free / ad claim in the game.
            _menuShell?.SwitchTab(MenuTab.Shop);
        }

        private void HandleCritInfo()
        {
            if (_config == null || _view.CritTooltip == null)
            {
                return;
            }

            if (_view.CritTooltipText != null)
            {
                _view.CritTooltipText.text = string.Format(CultureInfo.InvariantCulture, DefenseStrings.CritInfo,
                    DefenseStatBuilder.Percent(_config.CritChance), DefenseStatBuilder.Percent(CurrentCritDamage),
                    DefenseStatBuilder.Percent(_config.BaseCritDamage), DefenseStatBuilder.Percent(_config.CritDamagePerUpgradeLevel));
            }

            _view.CritTooltip.SetActive(true);
        }

        private void HideCritInfo()
        {
            if (_view != null && _view.CritTooltip != null)
            {
                _view.CritTooltip.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (_popup != null && _popup.IsOpen)
            {
                _popup.HideImmediate();
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void Log(string message)
        {
            Debug.Log("[Defense] " + message);
        }

        private void OnDestroy()
        {
            if (_popup != null)
            {
                _popup.CloseRequested -= HandlePopupClose;
                _popup.PreviousRequested -= HandlePrevious;
                _popup.NextRequested -= HandleNext;
                _popup.LevelUpRequested -= HandleLevelUp;
            }
        }
    }
}
