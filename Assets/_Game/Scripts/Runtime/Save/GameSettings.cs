namespace AlienDefense.Save
{
    /// <summary>Read-only runtime view of persisted settings. Lives next to the save DTOs (not in Settings/) so
    /// PlayerProfileService can expose/accept it without Save depending back on the Settings namespace.</summary>
    public readonly struct GameSettings
    {
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public bool HapticsEnabled { get; }
        public bool CameraShakeEnabled { get; }
        public int QualityLevel { get; }
        public int TargetFrameRate { get; }

        public GameSettings(float masterVolume, float musicVolume, float sfxVolume, bool hapticsEnabled, bool cameraShakeEnabled, int qualityLevel, int targetFrameRate)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            HapticsEnabled = hapticsEnabled;
            CameraShakeEnabled = cameraShakeEnabled;
            QualityLevel = qualityLevel;
            TargetFrameRate = targetFrameRate;
        }

        public static GameSettings From(SettingsSaveData data)
        {
            return new GameSettings(data.MasterVolume, data.MusicVolume, data.SfxVolume, data.HapticsEnabled, data.CameraShakeEnabled, data.QualityLevel, data.TargetFrameRate);
        }
    }
}
