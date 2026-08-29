using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Config-only defaults for a brand-new profile. Holds no runtime/save state itself — just the
    /// starting values PlayerProfileDefaultsFactory copies into a fresh PlayerProfileSaveData.</summary>
    [CreateAssetMenu(fileName = "PlayerProfileDefaults", menuName = "AlienDefense/Save/Player Profile Defaults")]
    public sealed class PlayerProfileDefaults : ScriptableObject
    {
        [Header("Default Unlocks")]
        [SerializeField]
        private string[] _defaultUnlockedTowerIds = System.Array.Empty<string>();

        [Header("Default Settings")]
        [SerializeField, Range(0f, 1f)]
        private float _defaultMasterVolume = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _defaultMusicVolume = 0.6f;

        [SerializeField, Range(0f, 1f)]
        private float _defaultSfxVolume = 0.8f;

        [SerializeField]
        private bool _defaultHapticsEnabled = true;

        [SerializeField]
        private bool _defaultCameraShakeEnabled = true;

        [SerializeField]
        private int _defaultQualityLevel = 2;

        [SerializeField]
        private int _defaultTargetFrameRate = 60;

        public string[] DefaultUnlockedTowerIds => _defaultUnlockedTowerIds;

        public SettingsSaveData CreateDefaultSettings()
        {
            return new SettingsSaveData
            {
                MasterVolume = _defaultMasterVolume,
                MusicVolume = _defaultMusicVolume,
                SfxVolume = _defaultSfxVolume,
                HapticsEnabled = _defaultHapticsEnabled,
                CameraShakeEnabled = _defaultCameraShakeEnabled,
                QualityLevel = _defaultQualityLevel,
                TargetFrameRate = _defaultTargetFrameRate
            };
        }
    }
}
