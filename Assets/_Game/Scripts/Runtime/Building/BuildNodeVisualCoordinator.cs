using UnityEngine;
using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>Highlights every Available BuildNode with its ring + hologram ghost. Shown by default (using
    /// _defaultPreviewTower) even before the player has picked a tower type to build — see HandleSelectedTowerChanged
    /// — and updated to whichever tower the player actually selects. Holds a scene-local list, not a global
    /// registry.</summary>
    public sealed class BuildNodeVisualCoordinator : MonoBehaviour
    {
        [SerializeField]
        private BuildNode[] _nodes;

        [SerializeField]
        [Tooltip("Ring + hologram shown on every Available node before the player has picked a tower type to " +
            "build (and again after Cancel). Leave empty to fall back to the old behavior (nothing shown until " +
            "a tower type is actually selected).")]
        private TowerDefinition _defaultPreviewTower;

        private BuildSelectionService _selectionService;

        public void Initialize(BuildSelectionService selectionService)
        {
            if (_selectionService != null)
            {
                _selectionService.SelectedTowerChanged -= HandleSelectedTowerChanged;
            }

            _selectionService = selectionService;

            if (_selectionService != null)
            {
                _selectionService.SelectedTowerChanged += HandleSelectedTowerChanged;
            }

            HandleSelectedTowerChanged(_selectionService != null ? _selectionService.SelectedTowerDefinition : null);
        }

        private void HandleSelectedTowerChanged(TowerDefinition selected)
        {
            // No explicit selection (including right after Cancel) falls back to the default preview tower
            // instead of showing nothing, so the ring/hologram read as "you can build here" at all times.
            TowerDefinition effective = selected != null ? selected : _defaultPreviewTower;
            bool highlightAvailable = effective != null;

            for (int i = 0; i < _nodes.Length; i++)
            {
                BuildNode node = _nodes[i];
                if (node == null)
                {
                    continue;
                }

                bool shouldShow = highlightAvailable && node.State == BuildNodeState.Available;
                node.SetHighlighted(shouldShow);

                if (shouldShow)
                {
                    node.ShowHologramPreview(effective.Prefab.gameObject);
                }
                else
                {
                    node.HideHologramPreview();
                }
            }
        }

        private void OnDestroy()
        {
            if (_selectionService != null)
            {
                _selectionService.SelectedTowerChanged -= HandleSelectedTowerChanged;
            }
        }
    }
}
