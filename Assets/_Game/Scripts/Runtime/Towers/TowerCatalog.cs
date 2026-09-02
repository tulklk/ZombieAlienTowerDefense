using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Config-only ordered list of every tower type in the game. Holds no runtime state — mirrors
    /// LevelCatalog's role for levels. Needed by the Upgrade screen (and any other meta-progression UI) to
    /// enumerate towers; per-level build bars keep their own smaller TowerDefinition[] selection independently.</summary>
    [CreateAssetMenu(fileName = "TowerCatalog", menuName = "AlienDefense/Towers/Tower Catalog")]
    public sealed class TowerCatalog : ScriptableObject
    {
        [SerializeField]
        private TowerDefinition[] _towers = System.Array.Empty<TowerDefinition>();

        public int Count => _towers?.Length ?? 0;

        public TowerDefinition GetTower(int index)
        {
            return _towers[index];
        }

        private void OnValidate()
        {
            if (_towers == null || _towers.Length == 0)
            {
                Debug.LogError($"[TowerCatalog] '{name}' has no towers.", this);
                return;
            }

            var seenIds = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < _towers.Length; i++)
            {
                TowerDefinition tower = _towers[i];
                if (tower == null)
                {
                    Debug.LogError($"[TowerCatalog] '{name}' has a null entry at index {i}.", this);
                    continue;
                }

                if (!seenIds.Add(tower.Id))
                {
                    Debug.LogError($"[TowerCatalog] '{name}' has a duplicate Tower Id '{tower.Id}' at index {i}.", this);
                }
            }
        }
    }
}
