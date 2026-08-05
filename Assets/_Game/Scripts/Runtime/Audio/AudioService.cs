using UnityEngine;

namespace AlienDefense.Audio
{
    /// <summary>Plays SFX through a small round-robin pool of AudioSources and music through one dedicated AudioSource.
    /// Never creates a GameObject per sound and never plays more simultaneous SFX than the pool size.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        [SerializeField]
        private AudioSource _musicSource;

        [SerializeField]
        [Tooltip("Round-robin pool used for all one-shot SFX. Size bounds max simultaneous SFX.")]
        private AudioSource[] _sfxSources = System.Array.Empty<AudioSource>();

        [SerializeField, Range(0f, 1f)]
        private float _musicVolume = 0.6f;

        [SerializeField, Range(0f, 1f)]
        private float _sfxVolume = 0.8f;

        private int _nextSfxIndex;

        public float MusicVolume => _musicVolume;
        public float SfxVolume => _sfxVolume;

        private void Awake()
        {
            ApplyMusicVolume();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            ApplyMusicVolume();
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (_musicSource == null || clip == null)
            {
                return;
            }

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null)
            {
                _musicSource.Stop();
            }
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || _sfxSources.Length == 0)
            {
                return;
            }

            AudioSource source = _sfxSources[_nextSfxIndex];
            _nextSfxIndex = (_nextSfxIndex + 1) % _sfxSources.Length;

            if (source != null)
            {
                source.PlayOneShot(clip, _sfxVolume);
            }
        }

        private void ApplyMusicVolume()
        {
            if (_musicSource != null)
            {
                _musicSource.volume = _musicVolume;
            }
        }
    }
}
