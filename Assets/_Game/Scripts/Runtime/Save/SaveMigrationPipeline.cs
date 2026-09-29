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

                case 1:
                    return MigrateV1ToV2(data);

                case 2:
                    return MigrateV2ToV3(data);

                case 3:
                    return MigrateV3ToV4(data);

                case 4:
                    return MigrateV4ToV5(data);

                case 5:
                    return MigrateV5ToV6(data);

                default:
                    Debug.LogError($"[SaveMigrationPipeline] No migration step defined from version {data.SaveVersion}.");
                    data.SaveVersion = SaveConstants.CurrentSaveVersion;
                    return data;
            }
        }

        private static PlayerProfileSaveData MigrateV1ToV2(PlayerProfileSaveData data)
        {
            if (data.Statistics == null)
            {
                data.Statistics = new PlayerStatisticsSaveData();
            }

            ProfileIdentityUtility.EnsureDisplayIdentity(data);
            data.SaveVersion = 2;
            return data;
        }

        private static PlayerProfileSaveData MigrateV2ToV3(PlayerProfileSaveData data)
        {
            if (data.Inventory == null)
            {
                data.Inventory = new System.Collections.Generic.List<MetaItemStackSaveData>();
            }

            if (data.LevelProgress != null)
            {
                for (int i = 0; i < data.LevelProgress.Count; i++)
                {
                    LevelProgressSaveData entry = data.LevelProgress[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    // Claim flags default false on new fields — nothing to stamp.
                }
            }

            data.SaveVersion = 3;
            return data;
        }

        /// <summary>Fills BestRemainingHpPercent for completed levels that predate the field.
        /// Prefers BestStars mapping (aligned with prior objective unlock) — never defaults to 100.</summary>
        private static PlayerProfileSaveData MigrateV3ToV4(PlayerProfileSaveData data)
        {
            if (data.LevelProgress != null)
            {
                for (int i = 0; i < data.LevelProgress.Count; i++)
                {
                    LevelProgressSaveData entry = data.LevelProgress[i];
                    if (entry == null || !entry.IsCompleted || entry.BestRemainingHpPercent > 0)
                    {
                        continue;
                    }

                    // Keep Clear unlockable without falsely unlocking Perfect.
                    if (entry.BestStars >= 3)
                    {
                        entry.BestRemainingHpPercent = 100;
                    }
                    else if (entry.BestStars >= 2)
                    {
                        entry.BestRemainingHpPercent = 50;
                    }
                    else
                    {
                        entry.BestRemainingHpPercent = 1;
                    }
                }
            }

            data.SaveVersion = 4;
            return data;
        }

        /// <summary>Introduces the equipment and artifact inventories. Deliberately grants nothing: a save that
        /// predates the feature has never owned a piece, and quietly handing out starter gear here would make an
        /// existing player's inventory differ from a fresh one for no stated reason. Both lists simply start
        /// empty, which is also what PlayerProfileDefaultsFactory produces for a brand-new profile.</summary>
        private static PlayerProfileSaveData MigrateV4ToV5(PlayerProfileSaveData data)
        {
            if (data.Equipment == null)
            {
                data.Equipment = new System.Collections.Generic.List<EquipmentSaveData>();
            }

            if (data.Artifacts == null)
            {
                data.Artifacts = new System.Collections.Generic.List<ArtifactSaveData>();
            }

            data.SaveVersion = 5;
            return data;
        }

        /// <summary>Introduces the base building list. Like V4-to-V5 it grants nothing: BaseProgressionService
        /// treats an absent row as "never built", which is exactly right for a save from before the base existed.
        /// Seeding a free Central Building here would hand existing players a head start that a new player does
        /// not get, and would do it invisibly.</summary>
        private static PlayerProfileSaveData MigrateV5ToV6(PlayerProfileSaveData data)
        {
            if (data.BaseBuildings == null)
            {
                data.BaseBuildings = new System.Collections.Generic.List<BaseBuildingSaveData>();
            }

            data.SaveVersion = 6;
            return data;
        }
    }
}
