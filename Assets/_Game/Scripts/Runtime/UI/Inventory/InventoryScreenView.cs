using DG.Tweening;
using UnityEngine;

namespace AlienDefense.UI.Inventory
{
    /// <summary>Holds the three inventory panels and the tab bar that switches between them.
    ///
    /// Panels are activated and deactivated, never rebuilt: switching tabs keeps every pooled card alive, and an
    /// inactive panel costs nothing because its whole subtree is off.
    ///
    /// The tab bar is a child of this view but a SIBLING of the panels, which is what keeps it fixed while a
    /// panel's own ScrollRect scrolls underneath it. The bottom navigation is not part of this object at all.</summary>
    public sealed class InventoryScreenView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField]
        private EquipmentPanelView _equipmentPanel;

        [SerializeField]
        private ArtifactsPanelView _artifactsPanel;

        [SerializeField]
        private MaterialsPanelView _materialsPanel;

        [Header("Tab bar")]
        [SerializeField]
        private InventoryTabButtonView[] _tabButtons = System.Array.Empty<InventoryTabButtonView>();

        [Header("Transition")]
        [SerializeField]
        [Tooltip("Faded on every tab change. Optional - leave empty to switch instantly.")]
        private CanvasGroup _contentGroup;

        [SerializeField, Min(0f)]
        private float _tabFadeDuration = 0.18f;

        private Tweener _fadeTween;

        public EquipmentPanelView EquipmentPanel => _equipmentPanel;
        public ArtifactsPanelView ArtifactsPanel => _artifactsPanel;
        public MaterialsPanelView MaterialsPanel => _materialsPanel;

        private void OnDestroy()
        {
            _fadeTween?.Kill();
        }

        public void ConfigureTabs(System.Action<InventoryTab> tabClicked)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                _tabButtons[i]?.Configure(tabClicked);
            }
        }

        public void ShowTab(InventoryTab tab)
        {
            // Only the equipment panel's TAB CONTENT is toggled: the component itself stays enabled on every tab
            // because it also owns the UFO sockets, which the reference keeps visible throughout.
            if (_equipmentPanel != null)
            {
                _equipmentPanel.SetTabContentVisible(tab == InventoryTab.Equipment);
            }

            if (_artifactsPanel != null)
            {
                _artifactsPanel.gameObject.SetActive(tab == InventoryTab.Artifacts);
            }

            if (_materialsPanel != null)
            {
                _materialsPanel.gameObject.SetActive(tab == InventoryTab.Materials);
            }

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] != null)
                {
                    _tabButtons[i].SetSelected(_tabButtons[i].Tab == tab);
                }
            }

            PlayFade();
        }

        /// <summary>Unscaled: the menu may be sitting behind a paused overlay.</summary>
        private void PlayFade()
        {
            if (_contentGroup == null || _tabFadeDuration <= 0f)
            {
                return;
            }

            _fadeTween?.Kill();
            _contentGroup.alpha = 0f;
            _fadeTween = DOTween.To(() => _contentGroup.alpha, a => _contentGroup.alpha = a, 1f, _tabFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
