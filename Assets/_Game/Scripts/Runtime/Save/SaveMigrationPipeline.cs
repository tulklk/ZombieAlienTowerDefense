using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Applies migrations one version step at a time (N -> N+1), never jumping straight to the current
    /// version. Version 1 is the first format that ever shipped, so there is currently no real migration step —
    /// this only stamps an unset/legacy-zero version to 1. Add one case per future version bump.</summary>
    public static class SaveMigrationPipeline
    {
        public enum MigrationOutcome
        {
            UpToDate,
            Migrated,
            FutureVersion,
            Unrecoverable
        }

        public static (MigrationOutcome outcome, PlayerProfileSaveData data) Migrate(PlayerProfileSaveData data)
        {
            if (data == null)
            {
                return (MigrationOutcome.Unrecoverable, null);
            }

            if (data.SaveVersion > SaveConstants.CurrentSaveVersion)
            {
                return (MigrationOutcome.FutureVersion, data);
            }

            if (data.SaveVersion == SaveConstants.CurrentSaveVersion)
            {
                return (MigrationOutcome.UpToDate, data);
            }

            bool migratedAtLeastOnce = false;
            int safetyCounter = 0;
            while (data.SaveVersion < SaveConstants.CurrentSaveVersion)
            {
                int previousVersion = data.SaveVersion;
                data = ApplyNextStep(data);
                migratedAtLeastOnce = true;

                safetyCounter++;
                if (data.SaveVersion <= previousVersion || safetyCounter > 100)
                {
                    Debug.LogError("[SaveMigrationPipeline] Migration step did not advance the version; aborting to avoid an infinite loop.");
                    return (MigrationOutcome.Unrecoverable, data);
                }
            }

            return (migratedAtLeastOnce ? MigrationOutcome.Migrated : MigrationOutcome.UpToDate, data);
        }

        private static PlayerProfileSaveData ApplyNextStep(PlayerProfileSaveData data)
        {
            switch (data.SaveVersion)
            {
                case 0:
                    // No prior release ever shipped with SaveVersion 0 explicitly; this only covers a JSON file
                    // where the field was missing/default-deserialized. There is no schema difference to migrate.
                    data.SaveVersion = 1;
                    return data;

                // case 1: return MigrateV1ToV2(data);

                default:
                    Debug.LogError($"[SaveMigrationPipeline] No migration step defined from version {data.SaveVersion}.");
                    data.SaveVersion = SaveConstants.CurrentSaveVersion;
                    return data;
            }
        }
    }
}
