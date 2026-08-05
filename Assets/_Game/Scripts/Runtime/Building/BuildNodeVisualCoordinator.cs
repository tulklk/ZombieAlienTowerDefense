using UnityEngine;
using AlienDefense.Towers;

namespace AlienDefense.Building
{
    /// <summary>Highlights available BuildNodes while a tower type is selected. Holds a scene-local list, not a global registry.</summary>
    public sealed class BuildNodeVisualCoordinator : MonoBehaviour
    {
        [SerializeField]
        private BuildNode[] _nodes;

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
        }

        private void HandleSelectedTowerChanged(TowerDefinition selected)
        {
            bool highlightAvailable = selected != null;

            for (int i = 0; i < _nodes.Length; i++)
            {
                BuildNode node = _nodes[i];
                if (node != null)
                {
                    node.SetHighlighted(highlightAvailable && node.State == BuildNodeState.Available);
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
