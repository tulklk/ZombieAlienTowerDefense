using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Config-only ordered list of campaign levels (Stable ID -> LevelDefinition + gameplay scene name).
    /// Holds no runtime state: no current selection, no stars, no unlock flags. Not a save system.</summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "AlienDefense/Level/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField]
        private LevelCatalogEntry[] _entries = System.Array.Empty<LevelCatalogEntry>();

        public int Count => _entries?.Length ?? 0;

        public LevelCatalogEntry GetEntry(int index)
        {
            return _entries[index];
        }

        public bool TryResolve(string levelId, out LevelCatalogEntry entry)
        {
            if (_entries != null && !string.IsNullOrEmpty(levelId))
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].LevelId == levelId)
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public bool TryGetNext(string currentLevelId, out LevelCatalogEntry nextEntry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].LevelId == currentLevelId)
                    {
                        if (i + 1 < _entries.Length)
                        {
                            nextEntry = _entries[i + 1];
                            return true;
                        }

                        break;
                    }
                }
            }

            nextEntry = null;
            return false;
        }

        private void OnValidate()
        {
            if (_entries == null || _entries.Length == 0)
            {
                Debug.LogError($"[LevelCatalog] '{name}' has no entries.", this);
                return;
            }

            var seenIds = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < _entries.Length; i++)
            {
                LevelCatalogEntry entry = _entries[i];
                if (entry == null || entry.LevelDefinition == null)
                {
                    Debug.LogError($"[LevelCatalog] '{name}' has a null entry or missing LevelDefinition at index {i}.", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.SceneName))
                {
                    Debug.LogError($"[LevelCatalog] '{name}' entry {i} ('{entry.LevelId}') has no scene name.", this);
                }

                if (string.IsNullOrWhiteSpace(entry.LevelId))
                {
                    Debug.LogError($"[LevelCatalog] '{name}' entry {i} references a LevelDefinition with an empty Level Id.", this);
                    continue;
                }

                if (!seenIds.Add(entry.LevelId))
                {
                    Debug.LogError($"[LevelCatalog] '{name}' has a duplicate Level Id '{entry.LevelId}' at index {i}.", this);
                }
            }
        }
    }
}
