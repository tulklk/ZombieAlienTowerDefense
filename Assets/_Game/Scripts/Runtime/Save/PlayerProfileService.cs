using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Owns the current in-memory PlayerProfileSaveData and is the only way anything else may read or
    /// mutate it. Never exposes the mutable internal lists directly (only read-only snapshots), validates every
    /// mutation, and drives SaveService (immediate for progression, debounced for settings).</summary>
    public sealed class PlayerProfileService
    {
        private const float SettingsSaveDebounceSeconds = 0.75f;

        private readonly SaveService _saveService;
        private readonly PlayerProfileSaveData _data;

        private bool _hasPendingDebouncedSave;
        private float _debounceSecondsRemaining;

        public PlayerProfileService(SaveService saveService, PlayerProfileSaveData initialData)
        {
            _saveService = saveService;
            _data = initialData;
        }

        public string ProfileId => _data.ProfileId;
        public string HighestUnlockedLevelId => _data.HighestUnlockedLevelId;
        public int MetaCurrency => _data.MetaCurrency;
        public bool IsTutorialCompleted => _data.Tutorial.IsCompleted;

        public GameSettings CurrentSettings => GameSettings.From(_data.Settings);

        public LevelProgressSnapshot GetLevelProgress(string levelId)
        {
            LevelProgressSaveData entry = FindLevelProgress(levelId);
            return entry != null ? LevelProgressSnapshot.From(entry) : LevelProgressSnapshot.NotStarted(levelId);
        }

        public bool IsTowerUnlocked(string towerId)
        {
            return !string.IsNullOrEmpty(towerId) && _data.UnlockedTowerIds.Contains(towerId);
        }

        public int GetTowerUpgradeLevel(string towerId)
        {
            for (int i = 0; i < _data.TowerUpgrades.Count; i++)
            {
                if (_data.TowerUpgrades[i].TowerId == towerId)
                {
                    return _data.TowerUpgrades[i].UpgradeLevel;
                }
            }

            return 0;
        }

        /// <summary>Validates and applies a finished level's result. Never computes stars itself — the caller
        /// (gameplay domain) already did that.</summary>
        public void SetLevelCompleted(LevelCompletedResult result)
        {
            if (string.IsNullOrWhiteSpace(result.LevelId))
            {
                Debug.LogError("[PlayerProfileService] Ignored SetLevelCompleted with an empty LevelId.");
                return;
            }

            LevelProgressSaveData progress = FindOrCreateLevelProgress(result.LevelId);
            progress.IsCompleted = true;
            progress.CompletionCount++;

            int clampedStars = Mathf.Clamp(result.Stars, 0, 3);
            if (clampedStars > progress.BestStars)
            {
                progress.BestStars = clampedStars;
            }

            int clampedHealth = Mathf.Max(0, result.RemainingBaseHealth);
            if (clampedHealth > progress.BestRemainingBaseHealth)
            {
                progress.BestRemainingBaseHealth = clampedHealth;
            }

            if (string.IsNullOrEmpty(_data.HighestUnlockedLevelId))
            {
                _data.HighestUnlockedLevelId = result.LevelId;
            }

            RequestImmediateSave();
        }

        public void SetHighestUnlockedLevel(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId) || levelId == _data.HighestUnlockedLevelId)
            {
                return;
            }

            _data.HighestUnlockedLevelId = levelId;
            RequestImmediateSave();
        }

        public void UnlockTower(string towerId)
        {
            if (string.IsNullOrWhiteSpace(towerId) || _data.UnlockedTowerIds.Contains(towerId))
            {
                return;
            }

            _data.UnlockedTowerIds.Add(towerId);
            RequestImmediateSave();
        }

        public void SetTowerUpgradeLevel(string towerId, int level)
        {
            if (string.IsNullOrWhiteSpace(towerId) || level < 0)
            {
                Debug.LogError($"[PlayerProfileService] Ignored SetTowerUpgradeLevel('{towerId}', {level}); invalid id or negative level.");
                return;
            }

            PermanentTowerUpgradeSaveData upgrade = FindOrCreateTowerUpgrade(towerId);
            upgrade.UpgradeLevel = level;
            RequestImmediateSave();
        }

        public void SetTutorialCompleted(bool completed)
        {
            if (_data.Tutorial.IsCompleted == completed)
            {
                return;
            }

            _data.Tutorial.IsCompleted = completed;
            RequestImmediateSave();
        }

        /// <summary>Applies already-validated settings and schedules a debounced save (settings change rapidly
        /// while a slider is dragged; critical progression above always saves immediately instead).</summary>
        public void UpdateSettings(GameSettings settings)
        {
            _data.Settings.MasterVolume = Mathf.Clamp01(settings.MasterVolume);
            _data.Settings.MusicVolume = Mathf.Clamp01(settings.MusicVolume);
            _data.Settings.SfxVolume = Mathf.Clamp01(settings.SfxVolume);
            _data.Settings.HapticsEnabled = settings.HapticsEnabled;
            _data.Settings.CameraShakeEnabled = settings.CameraShakeEnabled;
            _data.Settings.QualityLevel = settings.QualityLevel;
            _data.Settings.TargetFrameRate = settings.TargetFrameRate;

            RequestDebouncedSave();
        }

        /// <summary>Call once per frame from an Application-scope MonoBehaviour to drive the settings debounce timer.</summary>
        public void Tick(float deltaTime)
        {
            if (!_hasPendingDebouncedSave)
            {
                return;
            }

            _debounceSecondsRemaining -= deltaTime;
            if (_debounceSecondsRemaining <= 0f)
            {
                _hasPendingDebouncedSave = false;
                _saveService.RequestSave(_data);
            }
        }

        /// <summary>Immediately saves any pending debounced change. Call before the app might be suspended/killed.</summary>
        public void FlushPendingSave()
        {
            if (!_hasPendingDebouncedSave)
            {
                return;
            }

            _hasPendingDebouncedSave = false;
            _saveService.RequestSave(_data);
        }

        private void RequestImmediateSave()
        {
            _hasPendingDebouncedSave = false;
            _saveService.RequestSave(_data);
        }

        private void RequestDebouncedSave()
        {
            _hasPendingDebouncedSave = true;
            _debounceSecondsRemaining = SettingsSaveDebounceSeconds;
        }

        private LevelProgressSaveData FindLevelProgress(string levelId)
        {
            for (int i = 0; i < _data.LevelProgress.Count; i++)
            {
                if (_data.LevelProgress[i].LevelId == levelId)
                {
                    return _data.LevelProgress[i];
                }
            }

            return null;
        }

        private LevelProgressSaveData FindOrCreateLevelProgress(string levelId)
        {
            LevelProgressSaveData existing = FindLevelProgress(levelId);
            if (existing != null)
            {
                return existing;
            }

            var created = new LevelProgressSaveData { LevelId = levelId };
            _data.LevelProgress.Add(created);
            return created;
        }

        private PermanentTowerUpgradeSaveData FindOrCreateTowerUpgrade(string towerId)
        {
            for (int i = 0; i < _data.TowerUpgrades.Count; i++)
            {
                if (_data.TowerUpgrades[i].TowerId == towerId)
                {
                    return _data.TowerUpgrades[i];
                }
            }

            var created = new PermanentTowerUpgradeSaveData { TowerId = towerId, UpgradeLevel = 0 };
            _data.TowerUpgrades.Add(created);
            return created;
        }
    }
}
