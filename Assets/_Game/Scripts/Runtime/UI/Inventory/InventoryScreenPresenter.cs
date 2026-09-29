using System.Collections;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Meta;
using UnityEngine;

namespace AlienDefense.UI.Inventory
{
    /// <summary>Drives the inventory screen: owns which tab is showing, reads the InventoryService, and pushes
    /// the results into the views.
    ///
    /// Refresh is event-driven, not polled. It subscribes to InventoryService.Changed on enable and drops the
    /// subscription on disable, so a craft in one tab updates the sockets in another without any Update() in the
    /// whole screen. Only the tab that is on screen is re-bound; the other two are re-bound when they are next
    /// shown, which keeps a craft from touching three grids at once.</summary>
    public sealed class InventoryScreenPresenter : MonoBehaviour
    {
        [SerializeField]
        private InventoryScreenView _view;

        [SerializeField]
        private InventoryTab _defaultTab = InventoryTab.Equipment;

        [SerializeField, Min(0f)]
        [Tooltip("How long the merge rows stay locked after a merge, covering the result animation.")]
        private float _mergeLockSeconds = 0.35f;

        private InventoryService _inventory;
        private AlienDefense.Save.PlayerProfileService _profile;
        private InventoryTab _activeTab;
        private bool _subscribed;
        private bool _isMerging;

        public InventoryTab ActiveTab => _activeTab;

        public void Initialize(ApplicationServices services)
        {
            _inventory = services != null ? services.Inventory : null;
            _profile = services != null ? services.PlayerProfileService : null;

            if (_view == null)
            {
                Debug.LogError("[InventoryScreenPresenter] No InventoryScreenView assigned.", this);
                return;
            }

            _view.ConfigureTabs(ShowTab);
            _view.EquipmentPanel?.Configure(HandleEquipmentClicked, RefreshEquipmentGrid);
            _view.EquipmentPanel?.Detail?.Configure(HandleCraftRequested, HandleDetailBack);
            _view.ArtifactsPanel?.Configure(RefreshMerge, RefreshArtifacts, HandleMergeRequested);
            _view.MaterialsPanel?.Configure(null);

            Subscribe();
            ShowTab(_defaultTab);
        }

        private void OnEnable()
        {
            Subscribe();

            // Coming back to the screen: whatever happened elsewhere (a victory reward, a shop purchase) may
            // have changed the inventory while this was off.
            if (_inventory != null)
            {
                RefreshActiveTab();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || _inventory == null)
            {
                return;
            }

            _inventory.Changed += RefreshActiveTab;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _inventory == null)
            {
                return;
            }

            _inventory.Changed -= RefreshActiveTab;
            _subscribed = false;
        }

        public void ShowTab(InventoryTab tab)
        {
            _activeTab = tab;
            _view?.ShowTab(tab);

            // Leaving a sub-screen open would strand the player on a craft or merge view they cannot see.
            _view?.EquipmentPanel?.ShowGrid();
            if (tab != InventoryTab.Artifacts)
            {
                _view?.ArtifactsPanel?.ShowInventory();
            }

            RefreshActiveTab();
        }

        public void ShowEquipment() => ShowTab(InventoryTab.Equipment);
        public void ShowArtifacts() => ShowTab(InventoryTab.Artifacts);
        public void ShowMaterials() => ShowTab(InventoryTab.Materials);

        private void RefreshActiveTab()
        {
            if (_inventory == null || _view == null)
            {
                return;
            }

            // The sockets and the crafting currency sit in the preview area, which is on screen for every tab -
            // so they refresh regardless of which tab is selected.
            EquipmentPanelView preview = _view.EquipmentPanel;
            if (preview != null)
            {
                preview.BindSlots(_inventory);
                preview.BindCurrency(_profile != null ? _profile.MetaCurrency : 0);
            }

            switch (_activeTab)
            {
                case InventoryTab.Equipment:
                    RefreshEquipment();
                    break;

                case InventoryTab.Artifacts:
                    if (_view.ArtifactsPanel != null && _view.ArtifactsPanel.IsMergeOpen)
                    {
                        RefreshMerge();
                    }
                    else
                    {
                        RefreshArtifacts();
                    }

                    break;

                case InventoryTab.Materials:
                    RefreshMaterials();
                    break;
            }
        }

        // ------------------------------------------------------------------ Equipment

        private void RefreshEquipment()
        {
            EquipmentPanelView panel = _view.EquipmentPanel;
            if (panel == null)
            {
                return;
            }

            if (panel.IsDetailOpen && panel.Detail != null)
            {
                RefreshDetail(panel.Detail.EquipmentId);
            }
            else
            {
                RefreshEquipmentGrid();
            }
        }

        private void RefreshEquipmentGrid()
        {
            EquipmentPanelView panel = _view.EquipmentPanel;
            if (panel == null)
            {
                return;
            }

            List<EquipmentEntry> entries = _inventory.GetEquipment(panel.SelectedSlot);
            panel.BindGrid(entries);
        }

        private void HandleEquipmentClicked(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                return;
            }

            RefreshDetail(equipmentId);
            _view.EquipmentPanel?.ShowDetail();
        }

        private void RefreshDetail(string equipmentId)
        {
            EquipmentDetailView detail = _view.EquipmentPanel != null ? _view.EquipmentPanel.Detail : null;
            if (detail == null || string.IsNullOrEmpty(equipmentId))
            {
                return;
            }

            List<EquipmentEntry> owned = _inventory.GetEquipment();
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].Definition != null && owned[i].Definition.Id == equipmentId)
                {
                    detail.Bind(owned[i], _inventory.GetCraftPlan(equipmentId));
                    return;
                }
            }
        }

        private void HandleCraftRequested(string equipmentId)
        {
            if (_inventory == null || !_inventory.TryCraftEquipment(equipmentId))
            {
                return;
            }

            // TryCraftEquipment already raised Changed, which re-bound the detail panel with the new tier.
            _view.EquipmentPanel?.Detail?.PlayCraftSuccess();
        }

        private void HandleDetailBack()
        {
            _view.EquipmentPanel?.ShowGrid();
            RefreshEquipmentGrid();
        }

        // ------------------------------------------------------------------ Artifacts

        private void RefreshArtifacts()
        {
            _view.ArtifactsPanel?.BindInventory(_inventory.GetArtifacts(), null);
        }

        private void RefreshMerge()
        {
            _view.ArtifactsPanel?.BindMerge(_inventory.GetMergePlans());
        }

        private void HandleMergeRequested(ArtifactMergePlan plan)
        {
            if (_isMerging || _inventory == null)
            {
                return;
            }

            _isMerging = true;
            _view.ArtifactsPanel?.SetMergeInteractable(false);

            if (!_inventory.TryMergeArtifact(plan))
            {
                _isMerging = false;
                _view.ArtifactsPanel?.SetMergeInteractable(true);
                return;
            }

            if (isActiveAndEnabled)
            {
                StartCoroutine(ReleaseMergeLock());
            }
            else
            {
                _isMerging = false;
            }
        }

        /// <summary>Realtime: the menu can be paused, and a scaled wait would leave the rows locked forever.</summary>
        private IEnumerator ReleaseMergeLock()
        {
            if (_mergeLockSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(_mergeLockSeconds);
            }

            _isMerging = false;
            _view?.ArtifactsPanel?.SetMergeInteractable(true);
        }

        // ------------------------------------------------------------------ Materials

        private void RefreshMaterials()
        {
            _view.MaterialsPanel?.Bind(_inventory.GetContainers(), _inventory.GetMaterials());
        }
    }
}
