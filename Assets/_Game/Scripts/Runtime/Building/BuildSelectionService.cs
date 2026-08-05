using System;
using AlienDefense.Towers;
using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Owns which TowerDefinition the player currently has selected to build. No spawning, no economy.</summary>
    public sealed class BuildSelectionService
    {
        public TowerDefinition SelectedTowerDefinition { get; private set; }

        public event Action<TowerDefinition> SelectedTowerChanged;

        public bool SelectTower(TowerDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogWarning("[BuildSelectionService] Cannot select a null TowerDefinition.");
                return false;
            }

            if (SelectedTowerDefinition == definition)
            {
                return true;
            }

            SelectedTowerDefinition = definition;
            SelectedTowerChanged?.Invoke(SelectedTowerDefinition);
            return true;
        }

        public void ClearSelection()
        {
            if (SelectedTowerDefinition == null)
            {
                return;
            }

            SelectedTowerDefinition = null;
            SelectedTowerChanged?.Invoke(null);
        }
    }
}
