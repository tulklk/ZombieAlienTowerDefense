using System;
using AlienDefense.Audio;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Settings
{
    /// <summary>Owns the runtime settings, applies them to engine/audio state, and persists changes through
    /// PlayerProfileService (debounced there). Never writes a save file directly.</summary>
    public sealed class SettingsService
    {
        private readonly PlayerProfileService _profileService;
        private AudioService _audioService;

        public GameSettings Current { get; private set; }

        public event Action<GameSettings> SettingsChanged;

        public SettingsService(PlayerProfileService profileService)
        {
            _profileService = profileService;
            Current = profileService.CurrentSettings;
            ApplyEngineSettings(Current);
        }

        /// <summary>AudioService is scene-scoped (only Gameplay has one); attach while that scene is alive,
        /// Detach when it unloads. SettingsService itself never assumes one exists.</summary>
        public void AttachAudioService(AudioService audioService)
        {
            _audioService = audioService;
            ApplyAudioSettings(Current);
        }

        public void DetachAudioService(AudioService audioService)
        {
            if (_audioService == audioService)
            {
                _audioService = null;
            }
        }

        public void SetMasterVolume(float value)
        {
            Apply(new GameSettings(Mathf.Clamp01(value), Current.MusicVolume, Current.SfxVolume, Current.HapticsEnabled, Current.CameraShakeEnabled, Current.QualityLevel, Current.TargetFrameRate));
        }

        public void SetMusicVolume(float value)
        {
            Apply(new GameSettings(Current.MasterVolume, Mathf.Clamp01(value), Current.SfxVolume, Current.HapticsEnabled, Current.CameraShakeEnabled, Current.QualityLevel, Current.TargetFrameRate));
        }

        public void SetSfxVolume(float value)
        {
            Apply(new GameSettings(Current.MasterVolume, Current.MusicVolume, Mathf.Clamp01(value), Current.HapticsEnabled, Current.CameraShakeEnabled, Current.QualityLevel, Current.TargetFrameRate));
        }

        public void SetHapticsEnabled(bool value)
        {
            Apply(new GameSettings(Current.MasterVolume, Current.MusicVolume, Current.SfxVolume, value, Current.CameraShakeEnabled, Current.QualityLevel, Current.TargetFrameRate));
        }

        public void SetCameraShakeEnabled(bool value)
        {
            Apply(new GameSettings(Current.MasterVolume, Current.MusicVolume, Current.SfxVolume, Current.HapticsEnabled, value, Current.QualityLevel, Current.TargetFrameRate));
        }

        public void SetQualityLevel(int level)
        {
            int maxIndex = Mathf.Max(0, QualitySettings.names.Length - 1);
            Apply(new GameSettings(Current.MasterVolume, Current.MusicVolume, Current.SfxVolume, Current.HapticsEnabled, Current.CameraShakeEnabled, Mathf.Clamp(level, 0, maxIndex), Current.TargetFrameRate));
        }

        public void SetTargetFrameRate(int fps)
        {
            int allowed = fps == 30 ? 30 : 60;
            Apply(new GameSettings(Current.MasterVolume, Current.MusicVolume, Current.SfxVolume, Current.HapticsEnabled, Current.CameraShakeEnabled, Current.QualityLevel, allowed));
        }

        private void Apply(GameSettings settings)
        {
            Current = settings;
            ApplyEngineSettings(settings);
            ApplyAudioSettings(settings);
            _profileService.UpdateSettings(settings);
            SettingsChanged?.Invoke(settings);
        }

        private static void ApplyEngineSettings(GameSettings settings)
        {
            QualitySettings.SetQualityLevel(settings.QualityLevel, true);
            Application.targetFrameRate = settings.TargetFrameRate;
        }

        private void ApplyAudioSettings(GameSettings settings)
        {
            if (_audioService == null)
            {
                return;
            }

            _audioService.SetMusicVolume(settings.MasterVolume * settings.MusicVolume);
            _audioService.SetSfxVolume(settings.MasterVolume * settings.SfxVolume);
        }
    }
}
