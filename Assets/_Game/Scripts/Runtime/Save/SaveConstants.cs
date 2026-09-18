namespace AlienDefense.Save
{
    /// <summary>Single source of truth for save format version and file names.</summary>
    public static class SaveConstants
    {
        public const int CurrentSaveVersion = 2;

        public const string ProfileFileName = "profile.json";
        public const string TempFileName = "profile.tmp";
        public const string BackupFileName = "profile.backup.json";
    }
}
