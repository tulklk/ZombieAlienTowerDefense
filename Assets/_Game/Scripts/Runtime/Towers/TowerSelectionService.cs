using System;

namespace AlienDefense.Towers
{
    /// <summary>Owns which already-built tower the player currently has selected. No UI, no economy knowledge.</summary>
    public sealed class TowerSelectionService
    {
        public TowerController SelectedTower { get; private set; }

        public event Action<TowerController> SelectionChanged;

        public bool Select(TowerController tower)
        {
            if (tower == null || tower.IsSold)
            {
                return false;
            }

            if (SelectedTower == tower)
            {
                return true;
            }

            SelectedTower = tower;
            SelectionChanged?.Invoke(SelectedTower);
            return true;
        }

        public void Clear()
        {
            if (SelectedTower == null)
            {
                return;
            }

            SelectedTower = null;
            SelectionChanged?.Invoke(null);
        }

        /// <summary>Clears the selection only if the given tower is the one currently selected. Used by TowerSellService.</summary>
        public void ClearIfSelected(TowerController tower)
        {
            if (SelectedTower == tower)
            {
                Clear();
            }
        }
    }
}
