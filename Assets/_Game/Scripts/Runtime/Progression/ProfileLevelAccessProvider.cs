using AlienDefense.Core;
using AlienDefense.Save;

namespace AlienDefense.Progression
{
    /// <summary>Real unlock provider backed by PlayerProfileService: first level always unlocked, any level is
    /// unlocked once completed, and the level right after the most recently completed one is unlocked too. Full
    /// branching/requirement-based campaign rules are Phase 14 — this stays deliberately simple so Phase 14 can
    /// replace it wholesale instead of extending duplicated logic.</summary>
    public sealed class ProfileLevelAccessProvider : ILevelAccessProvider
    {
        private readonly PlayerProfileService _profileService;
        private readonly LevelCatalog _catalog;

        public ProfileLevelAccessProvider(PlayerProfileService profileService, LevelCatalog catalog)
        {
            _profileService = profileService;
            _catalog = catalog;
        }

        public bool IsUnlocked(string levelId)
        {
            if (_catalog == null || _catalog.Count == 0 || string.IsNullOrEmpty(levelId))
            {
                return false;
            }

            if (_catalog.GetEntry(0).LevelId == levelId)
            {
                return true;
            }

            if (_profileService.GetLevelProgress(levelId).IsCompleted)
            {
                return true;
            }

            for (int i = 1; i < _catalog.Count; i++)
            {
                LevelCatalogEntry entry = _catalog.GetEntry(i);
                if (entry.LevelId == levelId)
                {
                    LevelCatalogEntry previous = _catalog.GetEntry(i - 1);
                    return _profileService.GetLevelProgress(previous.LevelId).IsCompleted;
                }
            }

            return false;
        }
    }
}
