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

        [Header("Play Energy")]
        [SerializeField, Min(1)]
        [Tooltip("Energy bar size. A new player starts full.")]
        private int _maxPlayEnergy = 60;

        [SerializeField, Min(0)]
        [Tooltip("Energy spent to start a level (shown on the Start button).")]
        private int _playEnergyCostPerLevel = 5;

        [SerializeField, Min(0.05f)]
        [Tooltip("Real-time minutes to regain 1 energy, also while the game is closed.")]
        private float _playEnergyRegenMinutes = 8f;

        [Header("Player Level (XP)")]
        [SerializeField, Min(1)]
        [Tooltip("XP needed to go from level 1 to level 2.")]
        private int _xpForLevel2 = 1000;

        [SerializeField, Min(0)]
        [Tooltip("How much more XP each following level needs than the one before (1000, 1500, 2000, ...).")]
        private int _xpIncreasePerLevel = 500;

        [SerializeField, Min(1)]
        private int _maxPlayerLevel = 99;

        public string[] DefaultUnlockedTowerIds => _defaultUnlockedTowerIds;
        public int XpForLevel2 => _xpForLevel2;
        public int XpIncreasePerLevel => _xpIncreasePerLevel;
        public int MaxPlayerLevel => _maxPlayerLevel;
        public int MaxPlayEnergy => _maxPlayEnergy;
        public int PlayEnergyCostPerLevel => _playEnergyCostPerLevel;
        public float PlayEnergyRegenMinutes => _playEnergyRegenMinutes;

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
