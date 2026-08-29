using System;
using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Orchestrates SaveFileRepository + SaveValidator + SaveMigrationPipeline: loads (with backup
    /// recovery and default-profile creation) and persists a PlayerProfileSaveData. Owns no campaign business
    /// rules, never computes stars/unlocks, and never touches Unity scene state.</summary>
    public sealed class SaveService
    {
        private readonly SaveFileRepository _repository;
        private bool _isSaving;

        public SaveService(SaveFileRepository repository)
        {
            _repository = repository;
        }

        /// <summary>firstLevelId seeds a brand-new profile's HighestUnlockedLevelId (resolved by the caller from
        /// LevelCatalog — SaveService itself never references Core/LevelCatalog, to keep Save a one-way dependency).</summary>
        public PlayerProfileSaveData LoadOrCreateDefault(PlayerProfileDefaults defaults, string firstLevelId)
        {
            _repository.RecoverInterruptedWrite();

            if (_repository.TryReadMain(out string mainJson) && TryDeserializeAndPrepare(mainJson, out PlayerProfileSaveData mainData))
            {
                return mainData;
            }

            Debug.LogWarning("[SaveService] Main save missing or invalid; trying backup.");

            if (_repository.TryReadBackup(out string backupJson) && TryDeserializeAndPrepare(backupJson, out PlayerProfileSaveData backupData))
            {
                Debug.LogWarning("[SaveService] Restored profile from backup.");
                RequestSave(backupData);
                return backupData;
            }

            Debug.LogWarning("[SaveService] No valid save or backup found; creating a new default profile.");
            PlayerProfileSaveData fresh = PlayerProfileDefaultsFactory.CreateDefault(defaults, firstLevelId);
            RequestSave(fresh);
            return fresh;
        }

        public bool RequestSave(PlayerProfileSaveData data)
        {
            if (data == null)
            {
                Debug.LogError("[SaveService] Refused to save a null profile.");
                return false;
            }

            if (_isSaving)
            {
                Debug.LogWarning("[SaveService] A save is already in progress; this request was ignored.");
                return false;
            }

            data.LastUpdatedUtcTicks = DateTime.UtcNow.Ticks;

            string json;
            try
            {
                json = JsonUtility.ToJson(data);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Serialize failed: {exception.Message}");
                return false;
            }

            _isSaving = true;
            SaveWriteResult result = _repository.WriteAtomic(json);
            _isSaving = false;

            if (!result.Success)
            {
                Debug.LogError($"[SaveService] Save failed: {result.ErrorMessage}");
            }

            return result.Success;
        }

        private static bool TryDeserializeAndPrepare(string json, out PlayerProfileSaveData data)
        {
            data = null;

            PlayerProfileSaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Deserialize failed: {exception.Message}");
                return false;
            }

            if (parsed == null)
            {
                return false;
            }

            (SaveMigrationPipeline.MigrationOutcome outcome, PlayerProfileSaveData migrated) = SaveMigrationPipeline.Migrate(parsed);

            if (outcome == SaveMigrationPipeline.MigrationOutcome.FutureVersion)
            {
                Debug.LogError($"[SaveService] Save version {parsed.SaveVersion} is newer than the version this build supports ({SaveConstants.CurrentSaveVersion}). Refusing to load or overwrite it.");
                return false;
            }

            if (outcome == SaveMigrationPipeline.MigrationOutcome.Unrecoverable)
            {
                return false;
            }

            if (!SaveValidator.ValidateAndRepair(migrated))
            {
                return false;
            }

            data = migrated;
            return true;
        }
    }
}
