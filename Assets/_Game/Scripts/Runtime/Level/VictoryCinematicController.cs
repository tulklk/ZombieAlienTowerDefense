using System;
using System.Collections.Generic;
using AlienDefense.Audio;
using AlienDefense.Building;
using AlienDefense.CameraSystem;
using AlienDefense.Enemies;
using AlienDefense.Player;
using AlienDefense.Vfx;
using AlienDefense.Waves;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace AlienDefense.Level
{
    /// <summary>The finishing move: the boss dies, the camera leaves the gameplay rig and orbits the body, the
    /// player's own UFO flies in and celebrates, the boss goes up in cartoon explosions, the UFO boosts off screen,
    /// and only then does the level report Victory (which is what opens the victory panel and banks the rewards).
    ///
    /// Deliberately the mirror image of <see cref="BossIntroController"/> and built on the same pieces: it borrows
    /// the camera by disabling TopDownCameraController, locks the same inputs, fades the same HUD group, and drives
    /// its timeline with a DOTween sequence on scaled time (so pausing pauses the cinematic).
    ///
    /// It owns no gameplay rules. It never grants a reward, never decides the result, never spawns an enemy, and
    /// never destroys the boss - it asks EnemyController to keep the already-defeated body out of the pool
    /// (TryHoldForCinematic) and hands it back at the end. Every continuous motion is evaluated in LateUpdate from
    /// the live camera, so nothing breaks if the boss died at the edge of the map or the camera was still moving.
    ///
    /// Anything missing is skipped rather than fatal: no boss body, no VFX asset, no UFO, no camera - the sequence
    /// still runs to the end and still reports the win. A watchdog forces the finish if the timeline is ever
    /// stalled, because a soft-lock here would cost the player their level.</summary>
    public sealed class VictoryCinematicController : MonoBehaviour
    {
        private enum Phase
        {
            Idle = 0,
            Approach = 1,
            Orbit = 2,

            /// <summary>Parked in front of the boss's face, holding still while it blows up.</summary>
            FrontShot = 3,
            Celebrate = 4,
            Exit = 5,
            Finished = 6,

            /// <summary>First beat: the UFO boosts off the gameplay screen before the camera leaves to circle the boss.</summary>
            UfoLeave = 7,
        }

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("Optional. Disabled while the cinematic borrows the camera.")]
        private TopDownCameraController _cameraController;

        [SerializeField]
        [Tooltip("The camera the cinematic moves (Main Camera).")]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Optional. The player's UFO - moved by the cinematic once its input is off.")]
        private PlayerController _player;

        [SerializeField]
        [Tooltip("Optional. Beam is switched off for the celebration.")]
        private UFOTractorBeamController _tractorBeam;

        [SerializeField]
        [Tooltip("Optional. Build-node channel input is locked during the cinematic.")]
        private PlayerBuildNodeProximityController _buildProximity;

        [SerializeField]
        [Tooltip("Optional. Joystick / bottom controls hidden during the cinematic.")]
        private GameObject _joystickRoot;

        [SerializeField]
        [Tooltip("Optional. Gameplay HUD faded out during the cinematic.")]
        private CanvasGroup _hudCanvasGroup;

        [Header("Enable")]
        [SerializeField]
        private bool _cinematicEnabled = true;

        [SerializeField]
        [Tooltip("Debug: win instantly with no cinematic. Editor / development builds only.")]
        private bool _skipCinematic;

        [Header("Victory Camera")]
        [SerializeField, Range(0.2f, 2f)]
        private float _approachDuration = 0.8f;

        [SerializeField, Range(0.5f, 6f)]
        private float _orbitDuration = 2.2f;

        [SerializeField, Range(2f, 20f)]
        [Tooltip("Orbit radius in metres, before the boss-size fit below widens it.")]
        private float _orbitRadius = 7f;

        [SerializeField, Range(15f, 80f)]
        [Tooltip("How steeply the camera looks down at the boss while circling it (0 = level with it, 90 = straight " +
                 "down). High values keep the gameplay's top-down feel instead of a sideways shot.")]
        private float _orbitPitch = 55f;

        [SerializeField, Range(-180f, 180f)]
        [FormerlySerializedAs("_orbitStartAngle")]
        [Tooltip("Yaw offset from dead-front. The orbit always ENDS looking at the boss's face; this nudges that " +
                 "final shot off centre (0 = straight in front of it).")]
        private float _frontAngleOffset;

        [SerializeField, Range(-360f, 360f)]
        private float _orbitDegrees = 360f;

        [SerializeField, Range(0.3f, 1.2f)]
        [Tooltip("Portion of the screen height the boss should fill; the orbit radius grows to respect it.")]
        private float _bossScreenFill = 0.42f;

        [SerializeField, Range(0f, 0.35f)]
        [Tooltip("Pushes the boss below screen centre (viewport fraction), leaving the upper half of the frame for " +
                 "the celebrating UFO instead of the two overlapping.")]
        private float _bossScreenDrop = 0.14f;

        [Header("UFO Leave (before the orbit)")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Seconds the UFO takes to fly off the gameplay screen once the boss dies, before the camera starts " +
                 "circling the body. 0 = hide it instantly.")]
        private float _ufoLeaveDuration = 0.9f;

        [SerializeField]
        [Tooltip("Viewport point of the gameplay camera the UFO leaves through (>1 is off screen). Default: up and right.")]
        private Vector2 _ufoLeaveViewport = new Vector2(1.3f, 1.35f);

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Extra height the UFO gains while leaving, metres.")]
        private float _ufoLeaveRise = 1.5f;

        [Header("UFO Celebration")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Seconds after the big explosion before the UFO flies in - it arrives to celebrate the wreck, not " +
                 "to sit in front of it while it is still blowing up.")]
        private float _ufoEntryAfterExplosion = 0.5f;

        [SerializeField, Range(0.2f, 2f)]
        private float _ufoFlyInDuration = 0.8f;

        [SerializeField, Range(0.5f, 6f)]
        private float _ufoCelebrationDuration = 1.6f;

        [SerializeField, Range(0f, 0.6f)]
        [Tooltip("Vertical hover, in metres.")]
        private float _ufoHoverAmount = 0.22f;

        [SerializeField, Range(0.2f, 6f)]
        private float _ufoHoverSpeed = 2.4f;

        [SerializeField, Range(0f, 0.4f)]
        [FormerlySerializedAs("_ufoSideSwing")]
        [Tooltip("Half-width of the victory loop, as a fraction of screen width.")]
        private float _ufoLoopRadiusX = 0.22f;

        [SerializeField, Range(0f, 0.4f)]
        [Tooltip("Half-height of the victory loop, as a fraction of screen height.")]
        private float _ufoLoopRadiusY = 0.12f;

        [SerializeField, Range(0.5f, 3f)]
        [FormerlySerializedAs("_ufoSwingSpeed")]
        [Tooltip("How many full loops the UFO flies before it leaves.")]
        private float _ufoLoopCount = 1f;

        [SerializeField, Range(0f, 25f)]
        [Tooltip("Bank angle at full sideways speed, degrees.")]
        private float _ufoBankAngle = 12f;

        [SerializeField, Range(0.2f, 2f)]
        private float _ufoExitDuration = 0.8f;

        [SerializeField]
        [Tooltip("Viewport point the UFO leaves through (>1 is off screen). Default exits up and to the right.")]
        private Vector2 _ufoExitViewport = new Vector2(1.35f, 1.3f);

        [SerializeField, Range(0f, 6f)]
        [Tooltip("How far above the centre of the explosion the UFO celebrates, metres. It flies to the spot where the " +
                 "boss blew up, at its real in-world size.")]
        private float _ufoHeightAboveExplosion = 1.2f;

        [Header("Energy Burst")]
        [SerializeField]
        [Tooltip("Optional. Prefab whose looks are used for the energy balls the boss bursts into (EnergyPickup). " +
                 "Only its 'Visual' child is copied: these are decoration, never collectable and never counted.")]
        private GameObject _energyBurstPrefab;

        [SerializeField, Range(0, 80)]
        private int _energyBurstCount = 36;

        [SerializeField]
        [Tooltip("Launch speed range, metres per second.")]
        private Vector2 _energyBurstSpeed = new Vector2(5f, 12f);

        [SerializeField, Range(1f, 40f)]
        private float _energyBurstGravity = 22f;

        [SerializeField, Range(0.2f, 3f)]
        private float _energyBurstScale = 1f;

        [Header("Explosion")]
        [SerializeField]
        [Tooltip("Optional. Reuses the pooled cartoon explosion (VfxDefinition_Vfx_RocketExplosion).")]
        private VfxDefinition _explosionVfx;

        [SerializeField]
        [Tooltip("Optional. Falls back to the explosion above when empty.")]
        private VfxDefinition _smallExplosionVfx;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Beat between the camera settling in front of the boss and the first small explosion.")]
        private float _explosionStartDelay = 0.25f;

        [SerializeField, Range(0, 10)]
        private int _smallExplosionCount = 5;

        [SerializeField, Range(0.02f, 0.4f)]
        private float _smallExplosionInterval = 0.13f;

        [SerializeField, Range(0f, 1f)]
        private float _smallExplosionScale = 0.55f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Pause between the last small explosion and the big one.")]
        private float _finalExplosionDelay = 0.25f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Big explosion size, multiplied by the boss's own size.")]
        private float _finalExplosionScale = 1.6f;

        [SerializeField, Range(0f, 0.6f)]
        [Tooltip("How long after the big explosion the boss body is hidden.")]
        private float _bossHideDelay = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float _screenShakeStrength = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float _screenShakeDuration = 0.28f;

        [Header("Hand-off")]
        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Pause after the UFO leaves before the level reports Victory (which opens the panel).")]
        private float _panelDelay = 0.3f;

        [SerializeField, Range(0.05f, 0.6f)]
        private float _hudFadeDuration = 0.25f;

        [Header("Audio")]
        [SerializeField]
        [Tooltip("Optional. Played through the level's AudioService, never a new AudioSource.")]
        private AudioClip _smallExplosionSfx;

        [SerializeField]
        [Tooltip("Optional.")]
        private AudioClip _bigExplosionSfx;

        [SerializeField]
        [Tooltip("Optional. Fanfare as the UFO leaves.")]
        private AudioClip _victoryStingerSfx;

        private WaveController _waves;
        private VfxService _vfx;
        private AudioService _audio;
        private Camera _camera;

        private EnemyController _boss;
        private bool _bossHeld;
        private Vector3 _bossCenter;
        private float _bossSize = 3f;
        private bool _hasBossCenter;
        private Vector3 _bossForward = Vector3.forward;
        private Renderer[] _bossRenderers = Array.Empty<Renderer>();
        private int _hiddenBossRendererCount;

        private Action _onCompleted;
        private Sequence _sequence;
        private Tween _cameraDriver;
        private Tween _hudTween;

        private Phase _phase = Phase.Idle;
        private float _phaseElapsed;
        private float _watchdog;
        private bool _started;
        private bool _completedRaised;
        private bool _locked;
        private bool _joystickWasActive;

        private Vector3 _orbitCenter;
        private Vector3 _approachFromPosition;
        private Quaternion _approachFromRotation;
        private float _cameraBlend;
        private float _shakeTimeLeft;
        private float _effectiveOrbitRadius;

        private Transform _ufo;
        private Vector3 _ufoVelocity;
        private float _ufoCelebrationTime;
        private bool _ufoActive;
        private Quaternion _ufoBaseRotation;
        private Vector3 _ufoVisualOffset;
        private Vector3 _ufoLeaveStart;
        private Vector3 _ufoLeaveEnd;
        private Vector2 _ufoAnchorViewport = new Vector2(0.5f, 0.5f);
        private float _ufoDepth = 10f;
        private Transform[] _burstItems = Array.Empty<Transform>();
        private Vector3[] _burstVelocity = Array.Empty<Vector3>();
        private Vector3[] _burstSpin = Array.Empty<Vector3>();
        private float _burstGroundY;
        private float _burstRadius = 0.25f;
        private bool _burstActive;
        private readonly List<Renderer> _hiddenUfoRenderers = new List<Renderer>();

        public bool IsPlaying => _started && !_completedRaised;

        private bool SkipEffective => _skipCinematic && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>Called once by LevelCompositionRoot. Subscribing to the boss spawn is how the cinematic knows
        /// which body to orbit later - it never searches the scene for one.</summary>
        public void Initialize(WaveController waves, VfxService vfx, AudioService audio)
        {
            Unsubscribe();
            _waves = waves;
            _vfx = vfx;
            _audio = audio;
            _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;

            if (_waves != null)
            {
                _waves.BossEncounterSpawned += HandleBossEncounterSpawned;
                _waves.BossSpawned += HandleBossSpawned;
            }
        }

        /// <summary>Plays the finishing cinematic and calls back when the level may report Victory. Returns false
        /// when the cinematic is off or already running, so the caller reports the win immediately instead.</summary>
        public bool TryPlay(Action onCompleted)
        {
            if (_started || !_cinematicEnabled || SkipEffective || _cameraTransform == null)
            {
                return false;
            }

            // A level without a boss encounter has nothing to orbit; win the way the game always did.
            if (!_hasBossCenter)
            {
                return false;
            }

            _started = true;
            _onCompleted = onCompleted;

            // The boss is already resolved by now; holding it only postpones its trip back to the pool.
            _bossHeld = _boss != null && _boss.TryHoldForCinematic();
            MeasureBoss();

            _orbitCenter = _bossCenter;
            _effectiveOrbitRadius = ComputeOrbitRadius();
            LockGameplay();

            // The UFO leaves first, on the gameplay camera; only then does the camera go and circle the boss.
            if (!TryStartUfoLeave())
            {
                HideUfo();
                BuildTimeline();
            }

            return true;
        }

        // ------------------------------------------------------------------ boss tracking

        private void HandleBossSpawned(EnemyController boss, BossController controller)
        {
            TrackBoss(boss);
        }

        private void HandleBossEncounterSpawned(EnemyController boss, System.Collections.Generic.IReadOnlyList<EnemyController> group)
        {
            TrackBoss(boss);
        }

        private void TrackBoss(EnemyController boss)
        {
            if (boss == null || _boss == boss)
            {
                return;
            }

            if (_boss != null)
            {
                _boss.Resolved -= HandleBossResolved;
            }

            _boss = boss;
            _boss.Resolved += HandleBossResolved;
            MeasureBoss();
        }

        /// <summary>Grabs the hold the moment the boss dies rather than waiting for the wave to close, because the
        /// death visual would otherwise recycle the body first. Also remembers where it fell, so the orbit still has
        /// a subject even if the body is gone by the time the level ends.</summary>
        private void HandleBossResolved(EnemyController boss, EnemyResolveReason reason)
        {
            MeasureBoss();
            if (reason == EnemyResolveReason.Defeated && !_started)
            {
                _bossHeld = boss.TryHoldForCinematic();
            }
        }

        private void MeasureBoss()
        {
            if (_boss == null || !_boss.gameObject.activeSelf)
            {
                return;
            }

            Bounds bounds = default;
            bool any = false;
            Renderer[] renderers = _boss.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer || renderers[i].GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderers[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            _bossRenderers = renderers;
            _bossForward = _boss.transform.forward;
            if (any)
            {
                _bossCenter = bounds.center;
                _bossSize = Mathf.Max(1f, bounds.size.y);
            }
            else
            {
                _bossCenter = _boss.transform.position + Vector3.up * 1.5f;
                _bossSize = 3f;
            }

            _hasBossCenter = true;
        }

        /// <summary>Where "in front of the boss" is on the orbit circle: straight out along the way it was facing,
        /// so the camera ends the turn looking it in the face rather than at its back.</summary>
        private float FrontAngleDegrees
        {
            get
            {
                Vector3 forward = _bossForward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }

                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + _frontAngleOffset;
            }
        }

        private float ComputeOrbitRadius()
        {
            if (_camera == null)
            {
                return _orbitRadius;
            }

            float visibleHeight = _bossSize / Mathf.Max(0.1f, _bossScreenFill);
            float fitDistance = visibleHeight * 0.5f / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(_orbitRadius, fitDistance);
        }

        // ------------------------------------------------------------------ timeline

        private void BuildTimeline()
        {
            _approachFromPosition = _cameraTransform.position;
            _approachFromRotation = _cameraTransform.rotation;
            if (_cameraController != null)
            {
                _cameraController.enabled = false;
            }

            SetPhase(Phase.Approach);
            _cameraDriver?.Kill();
            _cameraBlend = 0f;
            _cameraDriver = DOTween.To(() => _cameraBlend, value => _cameraBlend = value, 1f, _approachDuration)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);

            // One beat at a time, never two at once: circle the boss, settle on its face, blow it up, and only then
            // bring the UFO in. Overlapping them put the saucer on top of the explosion it was meant to be watching.
            float orbitStart = _approachDuration;
            float frontShotStart = orbitStart + _orbitDuration;
            float explosionStart = frontShotStart + _explosionStartDelay;
            float finalExplosionTime = explosionStart + _smallExplosionCount * _smallExplosionInterval + _finalExplosionDelay;
            float bossHideTime = finalExplosionTime + _bossHideDelay;
            float ufoEntryTime = finalExplosionTime + _ufoEntryAfterExplosion;
            float ufoLoopStart = ufoEntryTime + _ufoFlyInDuration;
            float exitStart = ufoLoopStart + _ufoCelebrationDuration;
            float finishTime = exitStart + _ufoExitDuration + _panelDelay;

            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.InsertCallback(orbitStart, () => SetPhase(Phase.Orbit));
            _sequence.InsertCallback(frontShotStart, () => SetPhase(Phase.FrontShot));

            for (int i = 0; i < _smallExplosionCount; i++)
            {
                int index = i;
                _sequence.InsertCallback(explosionStart + i * _smallExplosionInterval, () => PlaySmallExplosion(index));
            }

            _sequence.InsertCallback(finalExplosionTime, PlayFinalExplosion);
            _sequence.InsertCallback(bossHideTime, HideBoss);
            _sequence.InsertCallback(ufoEntryTime, StartUfoEntry);
            _sequence.InsertCallback(ufoLoopStart, () => SetPhase(Phase.Celebrate));
            _sequence.InsertCallback(exitStart, StartUfoExit);
            _sequence.InsertCallback(finishTime, Finish);

            // Never shorter than its own callbacks, or DOTween would complete before the last one fires.
            _sequence.AppendInterval(finishTime + 0.05f);
            _watchdog = finishTime + 3f;
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseElapsed = 0f;
        }

        private void LateUpdate()
        {
            if (!_started || _completedRaised)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _phaseElapsed += deltaTime;
            _watchdog -= deltaTime;

            UpdateUfoLeave();
            UpdateCamera(deltaTime);
            UpdateUfo(deltaTime);
            UpdateEnergyBurst(deltaTime);

            if (_watchdog <= 0f)
            {
                Debug.LogWarning("[VictoryCinematicController] Timeline overran its budget; finishing early so the win is not lost.");
                Finish();
            }
        }

        private void UpdateCamera(float deltaTime)
        {
            if (_cameraTransform == null || _phase == Phase.Idle || _phase == Phase.Finished || _phase == Phase.UfoLeave)
            {
                return;
            }

            Vector3 position;
            Quaternion rotation;

            if (_phase == Phase.Approach)
            {
                ComputeOrbitPose(0f, out Vector3 orbitPosition, out Quaternion orbitRotation);
                position = Vector3.LerpUnclamped(_approachFromPosition, orbitPosition, _cameraBlend);
                rotation = Quaternion.SlerpUnclamped(_approachFromRotation, orbitRotation, _cameraBlend);
            }
            else
            {
                float orbitProgress = _phase == Phase.Orbit
                    ? Mathf.Clamp01(_phaseElapsed / Mathf.Max(0.01f, _orbitDuration))
                    : 1f;
                float eased = Mathf.SmoothStep(0f, 1f, orbitProgress);
                ComputeOrbitPose(_orbitDegrees * eased, out position, out rotation);
            }

            if (_shakeTimeLeft > 0f)
            {
                _shakeTimeLeft -= deltaTime;
                float falloff = Mathf.Clamp01(_shakeTimeLeft / Mathf.Max(0.01f, _screenShakeDuration));
                float amount = _screenShakeStrength * falloff;
                position += rotation * new Vector3(
                    (Mathf.PerlinNoise(Time.time * 37f, 0f) - 0.5f) * 2f * amount,
                    (Mathf.PerlinNoise(0f, Time.time * 41f) - 0.5f) * 2f * amount,
                    0f);
            }

            _cameraTransform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Camera pose on the circle around the body, measured backwards from the final front-on shot: at
        /// degrees = _orbitDegrees the camera is exactly in front of the boss's face, whatever direction it happened
        /// to be walking when it died. Yaw only - the roll a plain LookAt would introduce on a moving orbit is what
        /// makes this kind of shot feel sick to watch.</summary>
        private void ComputeOrbitPose(float degrees, out Vector3 position, out Quaternion rotation)
        {
            // The fitted radius is the camera's distance to the boss along its line of sight; the pitch splits it
            // into height and ground distance, so a steeper look-down never changes how big the boss reads.
            float angle = (FrontAngleDegrees - _orbitDegrees + degrees) * Mathf.Deg2Rad;
            float pitch = _orbitPitch * Mathf.Deg2Rad;
            float ground = Mathf.Cos(pitch) * _effectiveOrbitRadius;
            var offset = new Vector3(
                Mathf.Sin(angle) * ground,
                Mathf.Sin(pitch) * _effectiveOrbitRadius,
                Mathf.Cos(angle) * ground);

            position = _orbitCenter + offset;
            Vector3 toCenter = _orbitCenter - position;
            if (toCenter.sqrMagnitude <= 0.0001f)
            {
                rotation = Quaternion.identity;
                return;
            }

            // Aim a little above the body so it sits low in frame and the UFO has the top half to itself.
            float fov = _camera != null ? _camera.fieldOfView : 40f;
            float dropDegrees = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * _bossScreenDrop * 2f) * Mathf.Rad2Deg;
            rotation = Quaternion.LookRotation(toCenter, Vector3.up) * Quaternion.Euler(-dropDegrees, 0f, 0f);
        }

        // ------------------------------------------------------------------ UFO

        private void StartUfoEntry()
        {
            if (_player == null)
            {
                return;
            }

            _ufo = _player.transform;
            _ufoBaseRotation = _ufo.rotation;
            _ufoVelocity = Vector3.zero;
            _ufoCelebrationTime = 0f;
            ShowUfo(); // before measuring: a hidden renderer is not a reliable size. Moved below in the same frame.
            _ufoVisualOffset = MeasureUfoVisualOffset();
            _ufoActive = true;

            // Celebrate right over the wreck: the anchor is a real point above the explosion, and the UFO is held at
            // that point's distance from the camera, so it reads at the same scale as the boss did.
            Vector3 anchor = _bossCenter + Vector3.up * _ufoHeightAboveExplosion;
            Vector3 anchorViewport = _camera != null ? _camera.WorldToViewportPoint(anchor) : new Vector3(0.5f, 0.5f, 10f);
            _ufoAnchorViewport = new Vector2(anchorViewport.x, anchorViewport.y);
            _ufoDepth = Mathf.Max(2f, anchorViewport.z);

            // Enter from just off the side of the frame rather than teleporting into it.
            _ufo.position = UfoRootPositionFor(new Vector2(1.3f, _ufoAnchorViewport.y));
        }

        /// <summary>Starts the UFO's exit from the gameplay view. The camera is frozen where it is first - it follows
        /// the player, and would otherwise chase the UFO off screen instead of watching it go. False when there is
        /// nothing to fly (no UFO, no camera, or the leave is switched off), so the orbit starts straight away.</summary>
        private bool TryStartUfoLeave()
        {
            if (_player == null || _camera == null || _ufoLeaveDuration <= 0f)
            {
                return false;
            }

            if (_cameraController != null)
            {
                _cameraController.enabled = false;
            }

            _ufo = _player.transform;
            _ufoBaseRotation = _ufo.rotation;
            _ufoLeaveStart = _ufo.position;

            // Leave through the chosen screen edge at the UFO's own depth, so it slides across the view rather than
            // flying into the lens.
            float depth = Mathf.Max(1f, _camera.WorldToViewportPoint(_ufoLeaveStart).z);
            _ufoLeaveEnd = _camera.ViewportToWorldPoint(new Vector3(_ufoLeaveViewport.x, _ufoLeaveViewport.y, depth))
                + Vector3.up * _ufoLeaveRise;

            SetPhase(Phase.UfoLeave);
            _watchdog = _ufoLeaveDuration + 3f; // BuildTimeline sets the real budget once the UFO is gone
            return true;
        }

        /// <summary>Accelerates away (a slow lift, then a boost), banking into the turn. When it is off screen it is
        /// hidden and the camera heads for the boss.</summary>
        private void UpdateUfoLeave()
        {
            if (_phase != Phase.UfoLeave || _ufo == null)
            {
                return;
            }

            float t = Mathf.Clamp01(_phaseElapsed / _ufoLeaveDuration);
            float eased = t * t * t;
            Vector3 position = Vector3.LerpUnclamped(_ufoLeaveStart, _ufoLeaveEnd, eased);
            position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.4f); // small hop before the boost
            _ufo.position = position;

            Vector3 screenDirection = _camera.transform.InverseTransformDirection(_ufoLeaveEnd - _ufoLeaveStart);
            float bank = Mathf.Sign(screenDirection.x) * _ufoBankAngle * Mathf.SmoothStep(0f, 1f, t);
            _ufo.rotation = _ufoBaseRotation * Quaternion.Euler(0f, 0f, -bank);

            if (t >= 1f)
            {
                _ufo.rotation = _ufoBaseRotation;
                HideUfo();
                BuildTimeline();
            }
        }

        /// <summary>The UFO is still parked wherever the player left it, usually right next to the boss, so the
        /// orbit would sweep straight through it. It stays invisible until its own entrance after the explosion.
        /// Only renderers that were on are switched off, and exactly those come back.</summary>
        private void HideUfo()
        {
            if (_player == null || _hiddenUfoRenderers.Count > 0)
            {
                return;
            }

            Renderer[] renderers = _player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled)
                {
                    renderers[i].enabled = false;
                    _hiddenUfoRenderers.Add(renderers[i]);
                }
            }
        }

        private void ShowUfo()
        {
            for (int i = 0; i < _hiddenUfoRenderers.Count; i++)
            {
                if (_hiddenUfoRenderers[i] != null)
                {
                    _hiddenUfoRenderers[i].enabled = true;
                }
            }

            _hiddenUfoRenderers.Clear();
        }

        private void StartUfoExit()
        {
            SetPhase(Phase.Exit);
            if (_audio != null && _victoryStingerSfx != null)
            {
                _audio.PlaySfx(_victoryStingerSfx);
            }
        }

        private void UpdateUfo(float deltaTime)
        {
            if (!_ufoActive || _ufo == null || _camera == null)
            {
                return;
            }

            float smoothTime;
            Vector2 viewport;

            if (_phase == Phase.Exit)
            {
                viewport = _ufoExitViewport;
                smoothTime = _ufoExitDuration * 0.5f;
            }
            else if (_phase == Phase.Celebrate)
            {
                // A full victory loop. The circle is offset so its starting point is exactly where the fly-in
                // finished - the UFO rolls straight into the loop instead of snapping sideways to begin it.
                _ufoCelebrationTime += deltaTime;
                float loop = Mathf.Clamp01(_ufoCelebrationTime / Mathf.Max(0.01f, _ufoCelebrationDuration));
                float angle = Mathf.PI + loop * Mathf.PI * 2f * Mathf.Max(0.1f, _ufoLoopCount);
                viewport = new Vector2(
                    _ufoAnchorViewport.x + _ufoLoopRadiusX + Mathf.Cos(angle) * _ufoLoopRadiusX,
                    _ufoAnchorViewport.y + Mathf.Sin(angle) * _ufoLoopRadiusY);
                smoothTime = 0.12f;
            }
            else
            {
                viewport = _ufoAnchorViewport;
                smoothTime = _ufoFlyInDuration * 0.45f;
            }

            Vector3 target = UfoRootPositionFor(viewport);
            target += Vector3.up * (Mathf.Sin(Time.time * _ufoHoverSpeed) * _ufoHoverAmount);

            Vector3 previous = _ufo.position;
            _ufo.position = Vector3.SmoothDamp(previous, target, ref _ufoVelocity, Mathf.Max(0.02f, smoothTime));

            // Bank into the movement, cartoon style, without ever spinning the model around.
            Vector3 screenVelocity = _camera.transform.InverseTransformDirection(_ufo.position - previous);
            float bank = Mathf.Clamp(screenVelocity.x / Mathf.Max(0.0001f, deltaTime) * 0.6f, -1f, 1f) * _ufoBankAngle;
            _ufo.rotation = Quaternion.Slerp(_ufo.rotation, _ufoBaseRotation * Quaternion.Euler(0f, 0f, -bank), deltaTime * 6f);
        }

        private Vector3 ViewportToWorld(Vector2 viewport)
        {
            return _camera != null
                ? _camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, _ufoDepth))
                : _orbitCenter;
        }

        /// <summary>Where the UFO's transform has to be for its saucer to land on that point of the screen. The
        /// player prefab's visible mesh sits about 2 m above its gameplay root, and at this distance that is a third
        /// of a screen - aiming the root would fly the saucer off the top of the frame.</summary>
        private Vector3 UfoRootPositionFor(Vector2 viewport)
        {
            return ViewportToWorld(viewport) - _ufoVisualOffset;
        }

        /// <summary>Centre of the saucer itself. The tractor beam's cone hangs 7.5 m below the hull and is a live
        /// renderer, so including it drags the measured centre down about 2 m - which is exactly how far off the top
        /// of the screen the UFO ended up the first time this was measured over every renderer.</summary>
        private Vector3 MeasureUfoVisualOffset()
        {
            if (_ufo == null)
            {
                return Vector3.zero;
            }

            Transform beamRoot = _tractorBeam != null ? _tractorBeam.transform : null;
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer renderer in _ufo.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || !renderer.enabled
                    || renderer.GetComponentInParent<Canvas>() != null
                    || (beamRoot != null && renderer.transform.IsChildOf(beamRoot)))
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any ? bounds.center - _ufo.position : Vector3.zero;
        }

        // ------------------------------------------------------------------ explosions

        private void PlaySmallExplosion(int index)
        {
            VfxDefinition definition = _smallExplosionVfx != null ? _smallExplosionVfx : _explosionVfx;
            if (_vfx == null || definition == null)
            {
                return;
            }

            // Spread the hits over the body (chest, shoulders, back) instead of stacking them in one spot.
            float angle = index * 2.39996f; // golden angle, so consecutive points never line up
            var offset = new Vector3(
                Mathf.Cos(angle) * _bossSize * 0.28f,
                Mathf.Sin(index * 1.7f) * _bossSize * 0.26f,
                Mathf.Sin(angle) * _bossSize * 0.28f);

            _vfx.Play(definition, _bossCenter + offset, Quaternion.identity, _smallExplosionScale);
            if (_audio != null && _smallExplosionSfx != null)
            {
                _audio.PlaySfx(_smallExplosionSfx);
            }
        }

        private void PlayFinalExplosion()
        {
            if (_vfx != null && _explosionVfx != null)
            {
                _vfx.Play(_explosionVfx, _bossCenter, Quaternion.identity, _finalExplosionScale * (_bossSize / 3f));
            }

            if (_audio != null && _bigExplosionSfx != null)
            {
                _audio.PlaySfx(_bigExplosionSfx);
            }

            _shakeTimeLeft = _screenShakeDuration;
            StartEnergyBurst();
        }

        /// <summary>The boss bursts into a fountain of energy balls that arc out, bounce and settle around the wreck.
        /// Pure decoration built from the pickup's own art: nothing is collectable and nothing is added to any
        /// wallet. The copies are made once, on the one frame the boss explodes, and simulated here in LateUpdate
        /// with plain arrays (no physics, no per-frame allocation).</summary>
        private void StartEnergyBurst()
        {
            if (_energyBurstPrefab == null || _energyBurstCount <= 0 || _burstActive)
            {
                return;
            }

            Transform visualSource = _energyBurstPrefab.transform.Find("Visual");
            GameObject source = visualSource != null ? visualSource.gameObject : _energyBurstPrefab;

            _burstItems = new Transform[_energyBurstCount];
            _burstVelocity = new Vector3[_energyBurstCount];
            _burstSpin = new Vector3[_energyBurstCount];
            _burstGroundY = _bossCenter.y - _bossSize * 0.5f;

            for (int i = 0; i < _energyBurstCount; i++)
            {
                GameObject item = Instantiate(source, transform);
                item.name = "VictoryEnergyBall_" + i;
                item.transform.localScale = source.transform.lossyScale * _energyBurstScale;
                item.transform.position = _bossCenter + UnityEngine.Random.insideUnitSphere * (_bossSize * 0.2f);
                item.transform.rotation = UnityEngine.Random.rotation;

                Vector2 flat = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 direction = new Vector3(flat.x, UnityEngine.Random.Range(0.7f, 1.6f), flat.y).normalized;
                _burstVelocity[i] = direction * UnityEngine.Random.Range(_energyBurstSpeed.x, _energyBurstSpeed.y);
                _burstSpin[i] = UnityEngine.Random.insideUnitSphere * 540f;
                _burstItems[i] = item.transform;
            }

            _burstRadius = 0.25f * _energyBurstScale;
            _burstActive = true;
        }

        private void UpdateEnergyBurst(float deltaTime)
        {
            if (!_burstActive)
            {
                return;
            }

            float floor = _burstGroundY + _burstRadius;
            for (int i = 0; i < _burstItems.Length; i++)
            {
                Transform item = _burstItems[i];
                if (item == null)
                {
                    continue;
                }

                Vector3 velocity = _burstVelocity[i];
                velocity.y -= _energyBurstGravity * deltaTime;
                Vector3 position = item.position + velocity * deltaTime;

                if (position.y < floor)
                {
                    // Bounce, losing most of the height and some of the slide each time, until it rests.
                    position.y = floor;
                    velocity.y = velocity.y < -1.5f ? -velocity.y * 0.42f : 0f;
                    velocity.x *= 0.7f;
                    velocity.z *= 0.7f;
                    _burstSpin[i] *= 0.6f;
                }

                _burstVelocity[i] = velocity;
                item.position = position;
                item.Rotate(_burstSpin[i] * deltaTime, Space.World);
            }
        }

        /// <summary>SetEnabled(false) only fades the beam out and lets its particles finish their lives, so the cone
        /// and sparkles trailed the UFO as it left. Everything the beam draws goes dark at once instead - it is
        /// not brought back: the level is over, and the Victory state keeps the beam off.</summary>
        private void HideBeamVisuals()
        {
            if (_player == null)
            {
                return;
            }

            UFOTractorBeamVisual visual = _player.GetComponentInChildren<UFOTractorBeamVisual>(true);
            HideRenderersUnder(_tractorBeam != null ? _tractorBeam.transform : null);
            HideRenderersUnder(visual != null ? visual.transform : null);
        }

        private static void HideRenderersUnder(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
        }

        private void HideBoss()
        {
            _hiddenBossRendererCount = 0;
            for (int i = 0; i < _bossRenderers.Length; i++)
            {
                Renderer renderer = _bossRenderers[i];
                if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                renderer.enabled = false;
                _bossRenderers[_hiddenBossRendererCount++] = renderer;
            }
        }

        /// <summary>Puts the body back the way it was found before returning it to the pool - a pooled enemy that
        /// came back invisible would spawn invisible next time.</summary>
        private void RestoreBoss()
        {
            for (int i = 0; i < _hiddenBossRendererCount; i++)
            {
                if (_bossRenderers[i] != null)
                {
                    _bossRenderers[i].enabled = true;
                }
            }

            _hiddenBossRendererCount = 0;

            if (_bossHeld && _boss != null)
            {
                _boss.ReleaseCinematicHold();
            }

            _bossHeld = false;
        }

        // ------------------------------------------------------------------ gameplay lock

        private void LockGameplay()
        {
            if (_locked)
            {
                return;
            }

            _locked = true;
            _player?.SetMovementEnabled(false);

            if (_tractorBeam != null)
            {
                _tractorBeam.SetEnabled(false);
            }

            HideBeamVisuals();

            _buildProximity?.SetInputEnabled(false);

            if (_joystickRoot != null)
            {
                _joystickWasActive = _joystickRoot.activeSelf;
                _joystickRoot.SetActive(false);
            }

            FadeHud(0f, blocksRaycasts: false);
        }

        /// <summary>Hands the UI back before the level reports Victory. The HUD CanvasGroup is not just the HUD:
        /// the Victory, Defeat and Pause panels all live under it (Canvas/SafeArea), so leaving it faded out would
        /// open the victory screen at alpha 0 with its buttons unclickable - the win would look like nothing
        /// happened. Gameplay input stays off; LevelCompositionRoot keeps movement, build input and the beam
        /// disabled for the Victory state itself.</summary>
        private void UnlockCinematicState()
        {
            _locked = false;
            FadeHud(1f, blocksRaycasts: true);

            if (_joystickRoot != null && _joystickWasActive)
            {
                _joystickRoot.SetActive(true);
            }
        }

        private void FadeHud(float alpha, bool blocksRaycasts)
        {
            if (_hudCanvasGroup == null)
            {
                return;
            }

            _hudTween?.Kill();
            _hudCanvasGroup.blocksRaycasts = blocksRaycasts;
            _hudTween = DOTween.To(() => _hudCanvasGroup.alpha, a => _hudCanvasGroup.alpha = a, alpha, _hudFadeDuration)
                .SetLink(gameObject);
        }

        // ------------------------------------------------------------------ finish

        private void Finish()
        {
            if (_completedRaised)
            {
                return;
            }

            _completedRaised = true;
            SetPhase(Phase.Finished);
            _ufoActive = false;
            _sequence?.Kill();
            _sequence = null;
            _cameraDriver?.Kill();
            _cameraDriver = null;

            RestoreBoss();
            ShowUfo(); // also covers a watchdog finish before the UFO's entry
            UnlockCinematicState();

            Action callback = _onCompleted;
            _onCompleted = null;
            callback?.Invoke();
        }

        private void Unsubscribe()
        {
            if (_waves != null)
            {
                _waves.BossEncounterSpawned -= HandleBossEncounterSpawned;
                _waves.BossSpawned -= HandleBossSpawned;
            }

            if (_boss != null)
            {
                _boss.Resolved -= HandleBossResolved;
            }
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
            _cameraDriver?.Kill();
            _hudTween?.Kill();
            Unsubscribe();
        }

#if UNITY_EDITOR
        /// <summary>Editor only: plays the cinematic on whatever boss is currently tracked (or on the spot the
        /// camera is looking at) without having to actually finish the level.</summary>
        [ContextMenu("Debug Victory Cinematic")]
        private void DebugPlay()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[VictoryCinematicController] Debug playback only works in Play Mode.");
                return;
            }

            _started = false;
            _completedRaised = false;
            TryPlay(() => Debug.Log("[VictoryCinematicController] Debug cinematic finished."));
        }
#endif
    }
}
