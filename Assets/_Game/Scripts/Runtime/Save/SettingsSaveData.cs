using System;

namespace AlienDefense.Save
{
    /// <summary>Serializable persisted settings. Values only; applying them to engine/audio state is SettingsService's job.</summary>
    [Serializable]
    public sealed class SettingsSaveData
    {
        public float MasterVolume = 1f;
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.8f;
        public bool HapticsEnabled = true;
        public bool CameraShakeEnabled = true;
        public int QualityLevel = 2;
        public int TargetFrameRate = 60;
    }
}
