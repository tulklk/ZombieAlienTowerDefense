using AlienDefense.Core;

namespace AlienDefense.Progression
{
    /// <summary>Phase 12 placeholder: only the first catalog entry is unlocked. No save data exists yet, so every
    /// later entry stays locked until Phase 13/14 provide a real progression-backed ILevelAccessProvider.</summary>
    public sealed class DefaultLevelAccessProvider : ILevelAccessProvider
    {
        private readonly LevelCatalog _catalog;

        public DefaultLevelAccessProvider(LevelCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool IsUnlocked(string levelId)
        {
            if (_catalog == null || _catalog.Count == 0 || string.IsNullOrEmpty(levelId))
            {
                return false;
            }

            return _catalog.GetEntry(0).LevelId == levelId;
        }
    }
}
