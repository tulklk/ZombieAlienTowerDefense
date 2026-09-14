using System;
using AlienDefense.CameraSystem;
using AlienDefense.Vfx;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Plays a one-shot DOTween takeoff from a landing pad to the gameplay sky start pose,
    /// locking movement/beam/joystick until complete.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class UFOFlightIntro : MonoBehaviour
    {
        [Header("Anchors")]
        [SerializeField]
        private Transform _ufo;

        [SerializeField]
        private Transform _launchPoint;

        [SerializeField]
        private Transform _skyStartPoint;

        [Header("Systems")]
        [SerializeField]
        private PlayerController _playerController;

        [SerializeField]
        private UFOTractorBeamController _tractorBeam;

        [SerializeField]
        private CharacterController _characterController;

        [SerializeField]
        private TopDownCameraController _cameraController;

        [SerializeField]
        [Tooltip("Optional. Hidden during intro (e.g. Canvas/.../BottomControls).")]
        private GameObject _joystickRoot;

        [SerializeField]
        [Tooltip("Optional. Takeoff effects - cued at this sequence's phase boundaries (startup, lift-off, travel, " +
            "finish). Visual only; the flight works the same without it.")]
        private UFOTakeoffVFX _takeoffVfx;

        [Header("Timing")]
        [SerializeField, Min(0f)]
        private float _startDelay = 0.45f;

        [SerializeField, Min(0.01f)]
        private float _liftDuration = 1.1f;

        [SerializeField, Min(0.01f)]
        private float _travelDuration = 1.7f;

        [SerializeField, Min(0f)]
        private float _settleDuration = 0.3f;

        [Header("Lift / Path")]
        [SerializeField, Min(0f)]
        private float _liftHeight = 5f;

        [SerializeField]
        private float _pathMidHeightBoost = 1f;

        [SerializeField]
        private float _tiltAngle = 6f;

        [Header("Animation")]
        [SerializeField]
        private Ease _liftEase = Ease.OutCubic;

        [SerializeField]
        private Ease _travelEase = Ease.InOutSine;

        [SerializeField]
        private Ease _settleEase = Ease.OutSine;

        [Header("Debug")]
        [SerializeField]
        private bool _skipIntro;

        private Sequence _introSequence;
        private PlayerMovement _playerMovement;
        private bool _introCompleted;
        private bool _controlsLockedByIntro;

        /// <summary>True while the takeoff sequence is running (or waiting to start).</summary>
        public bool IsIntroPlaying => !_introCompleted && !_skipIntro && isActiveAndEnabled;

        /// <summary>True when intro finished or was skipped.</summary>
        public bool IsIntroCompleted => _introCompleted;

        public bool SkipIntro => _skipIntro;

        public event Action IntroCompleted;

        private void Awake()
        {
            ResolveReferences();

            if (_ufo == null || _launchPoint == null || _skyStartPoint == null)
            {
                Debug.LogError("[UFOFlightIntro] Missing ufo / launchPoint / skyStartPoint. Skipping intro.", this);
                CompleteIntroImmediate(enableGameplaySystems: false);
                return;
            }

            if (_skipIntro)
            {
                ApplySkyPose();
                RestoreCharacterController(true);
                SetJoystickVisible(true);
                _cameraController?.SnapToTargetImmediate();
                CompleteIntroImmediate(enableGameplaySystems: false);
                return;
            }

            BeginLockedStartPose();
        }

        private void Start()
        {
            if (_introCompleted || _skipIntro)
            {
                return;
            }

            if (_ufo == null || _launchPoint == null || _skyStartPoint == null)
            {
                return;
            }

            // Re-assert launch pose after PlayerMovement.Initialize hover snap in Awake.
            BeginLockedStartPose();
            _cameraController?.SnapToTargetImmediate();
            _takeoffVfx?.Prepare(); // clean slate - nothing left over from a previous run of the intro
            PlayIntroSequence();
        }

        private void OnDestroy()
        {
            if (_introSequence != null && _introSequence.IsActive())
            {
                _introSequence.Kill();
                _introSequence = null;
            }
        }

        private void ResolveReferences()
        {
            if (_ufo == null)
            {
                _ufo = transform;
            }

            if (_playerController == null)
            {
                _playerController = GetComponent<PlayerController>();
                if (_playerController == null)
                {
                    _playerController = GetComponentInChildren<PlayerController>();
                }
            }

            if (_characterController == null && _ufo != null)
            {
                _characterController = _ufo.GetComponent<CharacterController>();
            }

            if (_playerMovement == null && _ufo != null)
            {
                _playerMovement = _ufo.GetComponent<PlayerMovement>();
            }

            if (_tractorBeam == null)
            {
                _tractorBeam = GetComponent<UFOTractorBeamController>();
                if (_tractorBeam == null)
                {
                    _tractorBeam = GetComponentInChildren<UFOTractorBeamController>();
                }
            }

            if (_cameraController == null)
            {
                _cameraController = FindFirstObjectByType<TopDownCameraController>();
            }
        }

        private void BeginLockedStartPose()
        {
            if (_introSequence != null && _introSequence.IsActive())
            {
                _introSequence.Kill();
                _introSequence = null;
            }

            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            _ufo.SetPositionAndRotation(_launchPoint.position, _launchPoint.rotation);
            LockControls(true);
            SetJoystickVisible(false);
        }

        private void PlayIntroSequence()
        {
            Vector3 liftTarget = _launchPoint.position + Vector3.up * _liftHeight;
            Vector3 skyPosition = ResolveSkyPosition();
            Quaternion skyRotation = _skyStartPoint.rotation;

            Vector3 mid = Vector3.Lerp(liftTarget, skyPosition, 0.5f);
            mid.y += _pathMidHeightBoost;

            Vector3[] path = { liftTarget, mid, skyPosition };

            Quaternion liftTilt = Quaternion.Euler(_tiltAngle, _launchPoint.eulerAngles.y, 0f);

            _introSequence = DOTween.Sequence().SetUpdate(UpdateType.Normal, false).SetLink(gameObject);

            _introSequence.AppendInterval(_startDelay);

            _introSequence.Append(_ufo.DOMove(liftTarget, _liftDuration).SetEase(_liftEase));
            _introSequence.Join(_ufo.DORotateQuaternion(liftTilt, _liftDuration).SetEase(_liftEase));

            _introSequence.Append(
                _ufo.DOPath(path, _travelDuration, PathType.CatmullRom)
                    .SetEase(_travelEase));
            _introSequence.Join(_ufo.DORotateQuaternion(skyRotation, _travelDuration).SetEase(_travelEase));

            if (_settleDuration > 0f)
            {
                Vector3 settlePeak = skyPosition + Vector3.up * 0.2f;
                _introSequence.Append(_ufo.DOMove(settlePeak, _settleDuration * 0.5f).SetEase(_settleEase));
                _introSequence.Append(_ufo.DOMove(skyPosition, _settleDuration * 0.5f).SetEase(_settleEase));
            }

            // Takeoff VFX cues, at the absolute times the flight phases above begin. Inserted rather than appended so
            // they sit on the existing timeline without shifting any movement.
            if (_takeoffVfx != null)
            {
                // Startup is started right here rather than from a callback at 0: a tween created inside a sequence
                // callback only starts updating on the following frame, and the level's first frame is a long one -
                // that put every startup cue a whole hitch behind the movement. Created now, both share that update.
                _takeoffVfx.PlayStartup();
                _introSequence.InsertCallback(_startDelay, _takeoffVfx.PlayLiftOff);
                _introSequence.InsertCallback(_startDelay + _liftDuration, _takeoffVfx.PlayTravel);
            }

            _introSequence.OnComplete(CompleteIntro);
        }

        private void CompleteIntro()
        {
            if (_introCompleted)
            {
                return;
            }

            ApplySkyPose();
            RestoreCharacterController(true);
            SetJoystickVisible(true);
            _takeoffVfx?.StopTakeoff();
            if (_tractorBeam != null)
            {
                _tractorBeam.SetEnabled(true);
            }

            // No camera snap here: the follow camera has been tracking the UFO through the whole flight, so all a
            // snap did was close its last ~0.2m of smoothing lag in a single frame - a visible pop on an otherwise
            // still shot. Letting the follow's own smoothing finish those last centimetres keeps the hand-off seamless.

            // Movement stays locked until LevelCompositionRoot enables it with BeginPreparingWave,
            // unless nothing is listening — then unlock here.
            bool hasListeners = IntroCompleted != null;
            if (!hasListeners)
            {
                LockControls(false);
            }

            _introCompleted = true;
            _controlsLockedByIntro = false;
            IntroCompleted?.Invoke();
        }

        private void CompleteIntroImmediate(bool enableGameplaySystems)
        {
            _introCompleted = true;
            if (enableGameplaySystems)
            {
                LockControls(false);
                if (_tractorBeam != null)
                {
                    _tractorBeam.SetEnabled(true);
                }

                SetJoystickVisible(true);
            }

            IntroCompleted?.Invoke();
        }

        private void ApplySkyPose()
        {
            if (_ufo == null || _skyStartPoint == null)
            {
                return;
            }

            _ufo.SetPositionAndRotation(ResolveSkyPosition(), _skyStartPoint.rotation);
        }

        /// <summary>The Sky Start Point's position at the height PlayerMovement will hold the UFO there, not the
        /// point's own authored Y. The flight used to end at the authored height (1.33m below hover height on
        /// Level_01) and the first movement frame after the intro teleported the UFO the rest of the way up - the
        /// visible hitch right as the beam switched on. Falls back to the authored position if PlayerMovement
        /// isn't available or initialised.</summary>
        private Vector3 ResolveSkyPosition()
        {
            Vector3 position = _skyStartPoint.position;
            if (_playerMovement != null && _playerMovement.TryGetHoverY(position, out float hoverY))
            {
                position.y = hoverY;
            }

            return position;
        }

        private void RestoreCharacterController(bool enabled)
        {
            if (_characterController != null)
            {
                _characterController.enabled = enabled;
            }
        }

        private void LockControls(bool locked)
        {
            _controlsLockedByIntro = locked;
            if (_playerController != null)
            {
                _playerController.SetMovementEnabled(!locked);
            }

            if (_tractorBeam != null && locked)
            {
                _tractorBeam.SetEnabled(false);
            }
        }

        private void SetJoystickVisible(bool visible)
        {
            if (_joystickRoot != null)
            {
                _joystickRoot.SetActive(visible);
            }
        }

        /// <summary>Called by LevelCompositionRoot after wave prep begins so GameFlow does not re-enable early.</summary>
        public void NotifyGameplaySystemsReady()
        {
            if (_playerController != null)
            {
                _playerController.SetMovementEnabled(true);
            }

            if (_tractorBeam != null)
            {
                _tractorBeam.SetEnabled(true);
            }

            SetJoystickVisible(true);
            _controlsLockedByIntro = false;
        }
    }
}
