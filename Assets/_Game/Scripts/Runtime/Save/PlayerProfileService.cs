using System;
using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Owns the current in-memory PlayerProfileSaveData and is the only way anything else may read or
    /// mutate it. Never exposes the mutable internal lists directly (only read-only snapshots), validates every
    /// mutation, and drives SaveService (immediate for progression, debounced for settings).</summary>
    public sealed class PlayerProfileService
    {
        private const float SettingsSaveDebounceSeconds = 0.75f;
        private const int MaxRememberedRewardTransactions = 32;

        private readonly SaveService _saveService;
        private readonly PlayerProfileSaveData _data;

        private bool _hasPendingDebouncedSave;
        private float _debounceSecondsRemaining;
        private int _batchDepth;
        private bool _batchDirty;

        public PlayerProfileService(SaveService saveService, PlayerProfileSaveData initialData)
        {
            _saveService = saveService;
            _data = initialData;
        }

        public string ProfileId => _data.ProfileId;
        public string DisplayName
        {
            get
            {
                ProfileIdentityUtility.EnsureDisplayIdentity(_data);
                return _data.DisplayName;
            }
        }

        public int AvatarId => Mathf.Max(0, _data.AvatarId);
        public long TotalTowerDamage => _data.Statistics != null ? _data.Statistics.TotalTowerDamage : 0L;
        public int ZombiesKilled => _data.Statistics != null ? _data.Statistics.ZombiesKilled : 0;
        public int BossesKilled => _data.Statistics != null ? _data.Statistics.BossesKilled : 0;

        /// <summary>Display-only meta level derived from campaign completions (not an XP economy).</summary>
        public int DisplayLevel => Mathf.Max(1, GetCompletedLevelCount() + 1);

        public string HighestUnlockedLevelId => _data.HighestUnlockedLevelId;
        public int MetaCurrency => _data.MetaCurrency;
        public int Gems => _data.Gems;
        public int VipTier => _data.VipTier;
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

            int clampedPercent = Mathf.Clamp(result.RemainingHpPercent, 0, 100);
            if (clampedPercent > progress.BestRemainingHpPercent)
            {
                progress.BestRemainingHpPercent = clampedPercent;
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

        /// <summary>Spends MetaCurrency if, and only if, the full amount can be afforded. Same all-or-nothing
        /// contract as EconomyService.TrySpend, so callers (e.g. TowerMetaUpgradeService) never need to check
        /// affordability themselves first.</summary>
        public bool TrySpendMetaCurrency(int amount)
        {
            if (amount <= 0 || _data.MetaCurrency < amount)
            {
                return false;
            }

            _data.MetaCurrency -= amount;
            RequestImmediateSave();
            return true;
        }

        public void AddMetaCurrency(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError($"[PlayerProfileService] Ignored AddMetaCurrency({amount}); amount must be positive.");
                return;
            }

            _data.MetaCurrency += amount;
            RequestImmediateSave();
        }

        public bool TrySpendGems(int amount)
        {
            if (amount <= 0 || _data.Gems < amount)
            {
                return false;
            }

            _data.Gems -= amount;
            RequestImmediateSave();
            return true;
        }

        public void AddGems(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError($"[PlayerProfileService] Ignored AddGems({amount}); amount must be positive.");
                return;
            }

            _data.Gems += amount;
            RequestImmediateSave();
        }

        public int GetItemAmount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || _data.Inventory == null)
            {
                return 0;
            }

            for (int i = 0; i < _data.Inventory.Count; i++)
            {
                MetaItemStackSaveData stack = _data.Inventory[i];
                if (stack != null && stack.ItemId == itemId)
                {
                    return Mathf.Max(0, stack.Amount);
                }
            }

            return 0;
        }

        public void AddItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                Debug.LogError($"[PlayerProfileService] Ignored AddItem('{itemId}', {amount}).");
                return;
            }

            if (_data.Inventory == null)
            {
                _data.Inventory = new System.Collections.Generic.List<MetaItemStackSaveData>();
            }

            for (int i = 0; i < _data.Inventory.Count; i++)
            {
                MetaItemStackSaveData stack = _data.Inventory[i];
                if (stack != null && stack.ItemId == itemId)
                {
                    stack.Amount = Mathf.Max(0, stack.Amount) + amount;
                    RequestImmediateSave();
                    return;
                }
            }

            _data.Inventory.Add(new MetaItemStackSaveData { ItemId = itemId, Amount = amount });
            RequestImmediateSave();
        }

        public int StoredPlayEnergy => Mathf.Max(0, _data.PlayEnergy);
        public long PlayEnergyUpdatedUtcTicks => _data.PlayEnergyUpdatedUtcTicks;

        /// <summary>Only PlayEnergyService writes this: the amount it settled on and the moment it settled.</summary>
        public void SetPlayEnergyState(int amount, long updatedUtcTicks)
        {
            _data.PlayEnergy = Mathf.Max(0, amount);
            _data.PlayEnergyUpdatedUtcTicks = updatedUtcTicks;
            RequestImmediateSave();
        }

        public int PlayerExperience => Mathf.Max(0, _data.PlayerExperience);

        public void AddPlayerExperience(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError($"[PlayerProfileService] Ignored AddPlayerExperience({amount}); amount must be positive.");
                return;
            }

            _data.PlayerExperience = Mathf.Max(0, _data.PlayerExperience) + amount;
            RequestImmediateSave();
        }

        public bool IsFirstClearRewardClaimed(string levelId)
        {
            LevelProgressSaveData progress = FindLevelProgress(levelId);
            return progress != null && progress.FirstClearRewardClaimed;
        }

        /// <summary>Flags a level's first-clear-only victory rewards as paid. False if they already were.</summary>
        public bool TryMarkFirstClearRewardClaimed(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return false;
            }

            LevelProgressSaveData progress = FindOrCreateLevelProgress(levelId);
            if (progress.FirstClearRewardClaimed)
            {
                return false;
            }

            progress.FirstClearRewardClaimed = true;
            RequestImmediateSave();
            return true;
        }

        public bool HasRewardTransaction(string transactionId)
        {
            return !string.IsNullOrEmpty(transactionId)
                && _data.RewardTransactions != null
                && _data.RewardTransactions.Contains(transactionId);
        }

        /// <summary>Records a reward payout id (level + run). Returns false - pay nothing - when that id was
        /// already recorded, which is what makes a duplicated victory callback harmless.</summary>
        public bool TryRegisterRewardTransaction(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (_data.RewardTransactions == null)
            {
                _data.RewardTransactions = new System.Collections.Generic.List<string>();
            }

            if (_data.RewardTransactions.Contains(transactionId))
            {
                return false;
            }

            _data.RewardTransactions.Add(transactionId);
            if (_data.RewardTransactions.Count > MaxRememberedRewardTransactions)
            {
                _data.RewardTransactions.RemoveRange(0, _data.RewardTransactions.Count - MaxRememberedRewardTransactions);
            }

            RequestImmediateSave();
            return true;
        }

        /// <summary>Groups several mutations into one disk write (a victory pays up to ten reward lines). Every
        /// BeginBatch must be paired with EndBatch; the write happens once, at the outermost EndBatch.</summary>
        public void BeginBatch()
        {
            _batchDepth++;
        }

        public void EndBatch()
        {
            if (_batchDepth <= 0)
            {
                return;
            }

            _batchDepth--;
            if (_batchDepth == 0 && _batchDirty)
            {
                _batchDirty = false;
                RequestImmediateSave();
            }
        }

        /// <summary>Marks one objective reward as claimed for a level. Returns false if already claimed or level id empty.</summary>
        public bool TryMarkObjectiveRewardClaimed(string levelId, AlienDefense.Meta.LevelObjectiveKind kind)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return false;
            }

            LevelProgressSaveData progress = FindOrCreateLevelProgress(levelId);
            switch (kind)
            {
                case AlienDefense.Meta.LevelObjectiveKind.Clear:
                    if (progress.ClearRewardClaimed)
                    {
                        return false;
                    }

                    progress.ClearRewardClaimed = true;
                    break;
                case AlienDefense.Meta.LevelObjectiveKind.Hp50:
                    if (progress.Hp50RewardClaimed)
                    {
                        return false;
                    }

                    progress.Hp50RewardClaimed = true;
                    break;
                case AlienDefense.Meta.LevelObjectiveKind.Perfect:
                    if (progress.PerfectRewardClaimed)
                    {
                        return false;
                    }

                    progress.PerfectRewardClaimed = true;
                    break;
                default:
                    return false;
            }

            RequestImmediateSave();
            return true;
        }

        /// <summary>VIP tier only ever goes up — a lower tier is silently ignored rather than downgrading a purchase.</summary>
        public void SetVipTier(int tier)
        {
            if (tier <= _data.VipTier)
            {
                return;
            }

            _data.VipTier = tier;
            RequestImmediateSave();
        }

        public DateTime? LastDailyRewardClaimUtc => _data.DailyReward.LastClaimUtcTicks > 0
            ? new DateTime(_data.DailyReward.LastClaimUtcTicks, DateTimeKind.Utc)
            : (DateTime?)null;

        public int DailyRewardStreakDay => _data.DailyReward.StreakDay;

        /// <summary>Validates via DailyRewardCalculator, grants Coin (+Gems on day 7), and persists the new
        /// streak — all-or-nothing. Returns the granted amounts so the caller can show them without recomputing.</summary>
        public bool TryClaimDailyReward(DateTime nowUtc, out int coinReward, out int gemReward)
        {
            if (!DailyRewardCalculator.CanClaim(LastDailyRewardClaimUtc, nowUtc))
            {
                coinReward = 0;
                gemReward = 0;
                return false;
            }

            int nextStreakDay = DailyRewardCalculator.ComputeNextStreakDay(LastDailyRewardClaimUtc, _data.DailyReward.StreakDay, nowUtc);
            coinReward = DailyRewardCalculator.CoinReward(nextStreakDay);
            gemReward = DailyRewardCalculator.GemReward(nextStreakDay);

            _data.DailyReward.LastClaimUtcTicks = nowUtc.Ticks;
            _data.DailyReward.StreakDay = nextStreakDay;
            _data.MetaCurrency += coinReward;
            _data.Gems += gemReward;
            RequestImmediateSave();
            return true;
        }

        public bool IsDailyQuestCompleted(DateTime nowUtc) => IsSameDailyQuestDay(nowUtc) && _data.DailyQuest.CompletedToday;
        public bool IsDailyQuestClaimed(DateTime nowUtc) => IsSameDailyQuestDay(nowUtc) && _data.DailyQuest.ClaimedToday;

        /// <summary>Intended caller: LevelCompositionRoot, right when a level is won.</summary>
        public void MarkDailyQuestCompleted(DateTime nowUtc)
        {
            EnsureDailyQuestDay(nowUtc);
            if (_data.DailyQuest.CompletedToday)
            {
                return;
            }

            _data.DailyQuest.CompletedToday = true;
            RequestImmediateSave();
        }

        public bool TryClaimDailyQuest(DateTime nowUtc, int coinReward, out int grantedCoin)
        {
            EnsureDailyQuestDay(nowUtc);
            if (!_data.DailyQuest.CompletedToday || _data.DailyQuest.ClaimedToday || coinReward <= 0)
            {
                grantedCoin = 0;
                return false;
            }

            _data.DailyQuest.ClaimedToday = true;
            _data.MetaCurrency += coinReward;
            grantedCoin = coinReward;
            RequestImmediateSave();
            return true;
        }

        private bool IsSameDailyQuestDay(DateTime nowUtc)
        {
            return _data.DailyQuest.DayResetUtcTicks > 0
                && new DateTime(_data.DailyQuest.DayResetUtcTicks, DateTimeKind.Utc).Date == nowUtc.Date;
        }

        private void EnsureDailyQuestDay(DateTime nowUtc)
        {
            if (IsSameDailyQuestDay(nowUtc))
            {
                return;
            }

            _data.DailyQuest.DayResetUtcTicks = nowUtc.Ticks;
            _data.DailyQuest.CompletedToday = false;
            _data.DailyQuest.ClaimedToday = false;
        }

        public int GetTotalStarsEarned()
        {
            int total = 0;
            for (int i = 0; i < _data.LevelProgress.Count; i++)
            {
                total += _data.LevelProgress[i].BestStars;
            }

            return total;
        }

        public int GetCompletedLevelCount()
        {
            int count = 0;
            for (int i = 0; i < _data.LevelProgress.Count; i++)
            {
                if (_data.LevelProgress[i].IsCompleted)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetUnlockedTowerCount() => _data.UnlockedTowerIds.Count;

        public bool TrySetDisplayName(string rawName, out string error)
        {
            if (!ProfileIdentityUtility.TryNormalizeDisplayName(rawName, out string normalized, out error))
            {
                return false;
            }

            if (string.Equals(_data.DisplayName, normalized, StringComparison.Ordinal))
            {
                return true;
            }

            _data.DisplayName = normalized;
            RequestImmediateSave();
            return true;
        }

        public void SetAvatarId(int avatarId)
        {
            int clamped = Mathf.Max(0, avatarId);
            if (_data.AvatarId == clamped)
            {
                return;
            }

            _data.AvatarId = clamped;
            RequestImmediateSave();
        }

        /// <summary>Adds actual applied damage (not theoretical hit values). Debounced to disk.</summary>
        public void AddTowerDamage(long actualDamage)
        {
            if (actualDamage <= 0)
            {
                return;
            }

            EnsureStatistics();
            _data.Statistics.TotalTowerDamage += actualDamage;
            RequestDebouncedSave();
        }

        public void RegisterZombieKill(bool isBoss = false)
        {
            EnsureStatistics();
            _data.Statistics.ZombiesKilled++;
            if (isBoss)
            {
                _data.Statistics.BossesKilled++;
            }

            RequestDebouncedSave();
        }

        private void EnsureStatistics()
        {
            if (_data.Statistics == null)
            {
                _data.Statistics = new PlayerStatisticsSaveData();
            }
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

        // ------------------------------------------------------------------ Meta inventory (materials)

        /// <summary>Every material/container stack the player holds, as immutable copies. Allocates a list per
        /// call, so the inventory UI calls it on open and on change events - never per frame.</summary>
        public System.Collections.Generic.List<MetaItemStackSnapshot> GetInventoryStacks()
        {
            var result = new System.Collections.Generic.List<MetaItemStackSnapshot>();
            if (_data.Inventory == null)
            {
                return result;
            }

            for (int i = 0; i < _data.Inventory.Count; i++)
            {
                MetaItemStackSaveData stack = _data.Inventory[i];
                if (stack != null && !string.IsNullOrWhiteSpace(stack.ItemId) && stack.Amount > 0)
                {
                    result.Add(new MetaItemStackSnapshot(stack.ItemId, stack.Amount));
                }
            }

            return result;
        }

        /// <summary>All-or-nothing: spends nothing and returns false unless the full amount is available, so a
        /// craft that needs several materials can never half-charge the player.</summary>
        public bool TrySpendItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || _data.Inventory == null)
            {
                return false;
            }

            for (int i = 0; i < _data.Inventory.Count; i++)
            {
                MetaItemStackSaveData stack = _data.Inventory[i];
                if (stack == null || stack.ItemId != itemId)
                {
                    continue;
                }

                if (stack.Amount < amount)
                {
                    return false;
                }

                stack.Amount -= amount;
                RequestImmediateSave();
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ Equipment

        public System.Collections.Generic.List<EquipmentSnapshot> GetEquipmentEntries()
        {
            var result = new System.Collections.Generic.List<EquipmentSnapshot>();
            if (_data.Equipment == null)
            {
                return result;
            }

            for (int i = 0; i < _data.Equipment.Count; i++)
            {
                EquipmentSaveData entry = _data.Equipment[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    result.Add(ToSnapshot(entry));
                }
            }

            return result;
        }

        /// <summary>The default struct (Exists == false) when the player does not own that piece.</summary>
        public EquipmentSnapshot GetEquipment(string itemId)
        {
            EquipmentSaveData entry = FindEquipment(itemId);
            return entry != null ? ToSnapshot(entry) : default;
        }

        /// <summary>Grants a piece, or adds a spare copy of one already owned. Spare copies are what the rarity
        /// craft consumes, which is why a second grant does not silently do nothing.</summary>
        public void GrantEquipment(string itemId, AlienDefense.Meta.MetaItemRarity rarity)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError("[PlayerProfileService] Ignored GrantEquipment with an empty item id.");
                return;
            }

            if (_data.Equipment == null)
            {
                _data.Equipment = new System.Collections.Generic.List<EquipmentSaveData>();
            }

            EquipmentSaveData entry = FindEquipment(itemId);
            if (entry != null)
            {
                entry.Duplicates = Mathf.Max(0, entry.Duplicates) + 1;
            }
            else
            {
                _data.Equipment.Add(new EquipmentSaveData
                {
                    ItemId = itemId,
                    Rarity = (int)rarity,
                    Level = 1,
                    Duplicates = 0,
                    Equipped = false
                });
            }

            RequestImmediateSave();
        }

        /// <summary>Raises a piece one rung and pays for it in the same step. Returns false without touching
        /// anything when the spare copies are not there, so the caller cannot end up charging a failed craft.</summary>
        public bool TryUpgradeEquipmentRarity(string itemId, AlienDefense.Meta.MetaItemRarity newRarity, int duplicatesToSpend)
        {
            EquipmentSaveData entry = FindEquipment(itemId);
            if (entry == null || duplicatesToSpend < 0 || entry.Duplicates < duplicatesToSpend)
            {
                return false;
            }

            entry.Duplicates -= duplicatesToSpend;
            entry.Rarity = (int)newRarity;
            RequestImmediateSave();
            return true;
        }

        /// <summary>Sets the equipped flag on one piece only. Clearing the other piece in the same slot is the
        /// caller's job - this layer does not know which slot a piece belongs to (that lives in the catalog).</summary>
        public void SetEquipmentEquipped(string itemId, bool equipped)
        {
            EquipmentSaveData entry = FindEquipment(itemId);
            if (entry == null || entry.Equipped == equipped)
            {
                return;
            }

            entry.Equipped = equipped;
            RequestImmediateSave();
        }

        private EquipmentSaveData FindEquipment(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || _data.Equipment == null)
            {
                return null;
            }

            for (int i = 0; i < _data.Equipment.Count; i++)
            {
                EquipmentSaveData entry = _data.Equipment[i];
                if (entry != null && entry.ItemId == itemId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static EquipmentSnapshot ToSnapshot(EquipmentSaveData entry)
        {
            return new EquipmentSnapshot(
                entry.ItemId,
                (AlienDefense.Meta.MetaItemRarity)Mathf.Max(0, entry.Rarity),
                Mathf.Max(1, entry.Level),
                Mathf.Max(0, entry.Duplicates),
                entry.Equipped);
        }

        // ------------------------------------------------------------------ Artifacts

        public System.Collections.Generic.List<ArtifactSnapshot> GetArtifactEntries()
        {
            var result = new System.Collections.Generic.List<ArtifactSnapshot>();
            if (_data.Artifacts == null)
            {
                return result;
            }

            for (int i = 0; i < _data.Artifacts.Count; i++)
            {
                ArtifactSaveData entry = _data.Artifacts[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ItemId) && entry.Amount > 0)
                {
                    result.Add(new ArtifactSnapshot(
                        entry.ItemId,
                        (AlienDefense.Meta.MetaItemRarity)Mathf.Max(0, entry.Rarity),
                        entry.Amount));
                }
            }

            return result;
        }

        public void AddArtifact(string itemId, AlienDefense.Meta.MetaItemRarity rarity, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                Debug.LogError($"[PlayerProfileService] Ignored AddArtifact('{itemId}', {amount}).");
                return;
            }

            if (_data.Artifacts == null)
            {
                _data.Artifacts = new System.Collections.Generic.List<ArtifactSaveData>();
            }

            ArtifactSaveData entry = FindArtifact(itemId, rarity);
            if (entry != null)
            {
                entry.Amount = Mathf.Max(0, entry.Amount) + amount;
            }
            else
            {
                _data.Artifacts.Add(new ArtifactSaveData { ItemId = itemId, Rarity = (int)rarity, Amount = amount });
            }

            RequestImmediateSave();
        }

        public bool TrySpendArtifact(string itemId, AlienDefense.Meta.MetaItemRarity rarity, int amount)
        {
            ArtifactSaveData entry = FindArtifact(itemId, rarity);
            if (entry == null || amount <= 0 || entry.Amount < amount)
            {
                return false;
            }

            entry.Amount -= amount;
            RequestImmediateSave();
            return true;
        }

        private ArtifactSaveData FindArtifact(string itemId, AlienDefense.Meta.MetaItemRarity rarity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || _data.Artifacts == null)
            {
                return null;
            }

            for (int i = 0; i < _data.Artifacts.Count; i++)
            {
                ArtifactSaveData entry = _data.Artifacts[i];
                if (entry != null && entry.ItemId == itemId && entry.Rarity == (int)rarity)
                {
                    return entry;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ Base buildings

        public System.Collections.Generic.List<BaseBuildingSnapshot> GetBaseBuildings()
        {
            var result = new System.Collections.Generic.List<BaseBuildingSnapshot>();
            if (_data.BaseBuildings == null)
            {
                return result;
            }

            for (int i = 0; i < _data.BaseBuildings.Count; i++)
            {
                BaseBuildingSaveData entry = _data.BaseBuildings[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.BuildingId))
                {
                    result.Add(ToSnapshot(entry));
                }
            }

            return result;
        }

        /// <summary>The default struct (Exists == false) for a building the player has never touched, which the
        /// base treats as an empty plot at level 0.</summary>
        public BaseBuildingSnapshot GetBaseBuilding(string buildingId)
        {
            BaseBuildingSaveData entry = FindBaseBuilding(buildingId);
            return entry != null ? ToSnapshot(entry) : default;
        }

        /// <summary>Writes the whole row in one call. Construction is a state machine with four fields that must
        /// agree with each other, so exposing them one setter at a time would invite half-applied transitions.</summary>
        public void SetBaseBuilding(string buildingId, int level, AlienDefense.Base.BaseBuildingState state,
            long constructionStartUtcTicks, long constructionCompleteUtcTicks, long lastCollectUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                Debug.LogError("[PlayerProfileService] Ignored SetBaseBuilding with an empty building id.");
                return;
            }

            if (_data.BaseBuildings == null)
            {
                _data.BaseBuildings = new System.Collections.Generic.List<BaseBuildingSaveData>();
            }

            BaseBuildingSaveData entry = FindBaseBuilding(buildingId);
            if (entry == null)
            {
                entry = new BaseBuildingSaveData { BuildingId = buildingId };
                _data.BaseBuildings.Add(entry);
            }

            entry.Level = Mathf.Max(0, level);
            entry.State = (int)state;
            entry.ConstructionStartUtcTicks = Math.Max(0L, constructionStartUtcTicks);
            entry.ConstructionCompleteUtcTicks = Math.Max(0L, constructionCompleteUtcTicks);
            entry.LastCollectUtcTicks = Math.Max(0L, lastCollectUtcTicks);
            RequestImmediateSave();
        }

        private BaseBuildingSaveData FindBaseBuilding(string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId) || _data.BaseBuildings == null)
            {
                return null;
            }

            for (int i = 0; i < _data.BaseBuildings.Count; i++)
            {
                BaseBuildingSaveData entry = _data.BaseBuildings[i];
                if (entry != null && entry.BuildingId == buildingId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static BaseBuildingSnapshot ToSnapshot(BaseBuildingSaveData entry)
        {
            return new BaseBuildingSnapshot(
                entry.BuildingId,
                Mathf.Max(0, entry.Level),
                (AlienDefense.Base.BaseBuildingState)Mathf.Max(0, entry.State),
                entry.ConstructionStartUtcTicks,
                entry.ConstructionCompleteUtcTicks,
                entry.LastCollectUtcTicks);
        }

        private void RequestImmediateSave()
        {
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }

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
