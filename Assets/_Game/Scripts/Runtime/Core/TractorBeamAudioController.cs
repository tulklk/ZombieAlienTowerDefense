using AlienDefense.Audio;
using AlienDefense.Enemies;
using AlienDefense.Player;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>Plays the tractor beam's loop while any enemy is being captured, and a rate-limited one-shot per
    /// completed capture. No gameplay logic, no volume hard-coded here.</summary>
    public sealed class TractorBeamAudioController
    {
        private const float CaptureSfxMinInterval = 0.15f;

        private readonly AudioService _audio;
        private readonly AudioSource _beamLoopSource;
        private readonly AudioClip _beamLoopClip;
        private readonly AudioClip _captureClip;

        private UFOTractorBeamController _beamController;
        private float _lastCaptureSfxTime = float.NegativeInfinity;

        public TractorBeamAudioController(AudioService audio, AudioSource beamLoopSource, AudioClip beamLoopClip, AudioClip captureClip)
        {
            _audio = audio;
            _beamLoopSource = beamLoopSource;
            _beamLoopClip = beamLoopClip;
            _captureClip = captureClip;
        }

        public void Initialize(UFOTractorBeamController beamController)
        {
            Unsubscribe();
            _beamController = beamController;

            if (_beamController != null)
            {
                _beamController.ActiveCaptureCountChanged += HandleActiveCaptureCountChanged;
                _beamController.EnemyCaptureCompleted += HandleCaptureCompleted;
            }
        }

        private void HandleActiveCaptureCountChanged(int count)
        {
            if (_beamLoopSource == null || _beamLoopClip == null)
            {
                return;
            }

            if (count > 0 && !_beamLoopSource.isPlaying)
            {
                _beamLoopSource.clip = _beamLoopClip;
                _beamLoopSource.loop = true;
                _beamLoopSource.Play();
            }
            else if (count == 0 && _beamLoopSource.isPlaying)
            {
                _beamLoopSource.Stop();
            }
        }

        private void HandleCaptureCompleted(EnemyController enemy)
        {
            if (_captureClip == null || Time.time - _lastCaptureSfxTime < CaptureSfxMinInterval)
            {
                return;
            }

            _lastCaptureSfxTime = Time.time;
            _audio?.PlaySfx(_captureClip);
        }

        public void Unsubscribe()
        {
            if (_beamController != null)
            {
                _beamController.ActiveCaptureCountChanged -= HandleActiveCaptureCountChanged;
                _beamController.EnemyCaptureCompleted -= HandleCaptureCompleted;
            }

            if (_beamLoopSource != null && _beamLoopSource.isPlaying)
            {
                _beamLoopSource.Stop();
            }
        }
    }
}
