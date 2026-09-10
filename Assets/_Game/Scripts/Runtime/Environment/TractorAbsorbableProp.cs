using System;
using AlienDefense.Common;
using DG.Tweening;
using UnityEngine;

namespace AlienDefense.Environment
{
    /// <summary>Opt-in marker + behavior for one decorative Environment Prop (tree, flower, mushroom, rock,
    /// barrel...) the UFO tractor beam may Pull/Lift/Absorb. Absence of this component is what makes an object
    /// immune — Ground/Plane/Terrain/Road/Bridge/gameplay-critical objects simply never get it, so there is no
    /// name-string or tag check anywhere in this system. This class itself never grants a reward - it only
    /// fires Absorbed; LevelCompositionRoot's HandlePropAbsorbedForExperience is what turns that into Experience
    /// (see its doc comment for why props grant only XP, never Energy, unlike EnergyPickups).
    ///
    /// Disabled (Update stopped) whenever Idle, per the "no per-frame cost for objects sitting still"
    /// performance requirement — only enabled while actually Pulling/Lifting.
    ///
    /// Absorption is one-way and permanent: on completion the GameObject is actually Destroyed (not just
    /// deactivated) to free its Transform/Renderer/Collider — with potentially hundreds of these on a level,
    /// keeping them alive-but-inactive is memory/traversal overhead this game gets nothing from carrying. Scene
    /// reload restores everything anyway (see LevelRestartService), so nothing needs to be reversible mid-session,
    /// and no persistent "destroyed prop" state is ever saved.</summary>
    [DisallowMultipleComponent]
    public sealed class TractorAbsorbableProp : MonoBehaviour
    {
        [Header("Absorption")]
        [SerializeField]
        private bool _canBeAbsorbed = true;

        [SerializeField, Min(0.01f)]
        [Tooltip("Higher = pulled/lifted more slowly. Applied as a divisor, same convention as EnemyDefinition.TractorResistance.")]
        private float _tractorResistance = 1f;

        [SerializeField]
        [Tooltip("Shrunk/spun during Lift. Falls back to this Transform if unassigned.")]
        private Transform _visualRoot;

        [SerializeField, Range(1, 2)]
        [Tooltip("XP granted to PlayerLevelProgressionService when this prop is absorbed - deliberately capped " +
            "low (max 2), same convention as EnemyDefinition.ExperienceReward, so decorative clutter can never " +
            "rush a level-up any faster than an actual kill.")]
        private int _experienceReward = 1;

        [Header("Speed Multipliers (relative to the beam's base Pull/Lift speed)")]
        [SerializeField, Min(0.01f)]
        private float _pullSpeedMultiplier = 1f;

        [SerializeField, Min(0.01f)]
        private float _liftSpeedMultiplier = 1f;

        [Header("Visual")]
        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("A quick, unmistakable size-down tween played the instant absorption begins, from full size to " +
            "this fraction, over Capture Shrink Duration - independent of Pull/Lift speed or distance (those " +
            "finish in well under a second at the beam's default speed, too fast for a distance-tied shrink to " +
            "read as anything but an instant snap). The normal shrink-during-Lift below then continues on from " +
            "here down to Minimum Visual Scale. 1 = no change (every ordinary prop). Meant for oversized " +
            "decoration like live animals (Cow/Horse/Sheep).")]
        private float _captureStartScale = 1f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds the Capture Start Scale tween above takes, timed independently of how fast Pull/Lift " +
            "physically move this object - see Capture Start Scale's tooltip.")]
        private float _captureShrinkDuration = 0.35f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Degrees this object leans over as the beam takes hold, around a random horizontal axis - what " +
            "makes it read as being yanked off the ground rather than sliding along it. Eased in over Capture " +
            "Shrink Duration alongside the shrink; the Lift spin then keeps turning it around its (now tilted) " +
            "own axis, so it tumbles. 0 disables the lean.")]
        private float _captureTiltDegrees = 30f;

        [Header("Absorption Acceleration")]
        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Fraction of the beam's Pull/Lift speed this object starts at - it then accelerates to full " +
            "speed over Acceleration Time, so it peels away slowly and gets whipped up at the end instead of " +
            "moving at one flat speed the whole way. 1 = no acceleration (the old constant-speed behaviour).")]
        private float _absorbStartSpeedFactor = 0.35f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds to reach full Pull/Lift speed from Start Speed Factor.")]
        private float _absorbAccelerationTime = 0.5f;

        [SerializeField]
        private bool _shrinkDuringLift = true;

        [SerializeField, Range(0.01f, 1f)]
        private float _minimumVisualScale = 0.15f;

        private TractorAbsorbablePropRegistry _registry;
        private TractorPropAbsorptionRequest _request;
        private Vector3 _initialVisualScale = Vector3.one;
        private Collider[] _colliders;
        private float _groundY;
        private Tweener _captureShrinkTween;
        private Tweener _captureTiltTween;
        private float _absorbElapsed;
        private float _liftStartDistance = 1f;
        private int _generation;

        // Optional. Present on live decorative animals (Cow/Horse/Sheep) that are also marked absorbable - both
        // this component's Pull/Lift and AnimalWanderer's own wander loop drive transform.position every frame,
        // so whichever runs last each Update would fight the other and the animal would jitter/teleport instead
        // of flying smoothly into the beam. Silently stopped for the rest of this absorption in TryBeginAbsorption
        // (see AnimalWanderer.enabled) - safe because absorption is one-way (the GameObject is Destroyed at the
        // end, see CompleteAbsorption), so there is never a "resume wandering" case to handle.
        private AnimalWanderer _animalWanderer;

        public TractorAbsorbablePropState State { get; private set; } = TractorAbsorbablePropState.Idle;
        public bool CanBeAbsorbed => _canBeAbsorbed;
        public float TractorResistance => Mathf.Max(0.01f, _tractorResistance);
        public int ExperienceReward => _experienceReward;
        public float PullSpeedMultiplier => _pullSpeedMultiplier;
        public float LiftSpeedMultiplier => _liftSpeedMultiplier;
        public bool ShrinkDuringLift => _shrinkDuringLift;
        public float MinimumVisualScale => _minimumVisualScale;
        public float CaptureStartScale => Mathf.Clamp(_captureStartScale, 0.1f, 1f);
        public int Generation => _generation;

        /// <summary>World-space point the beam should measure distance to. Falls back to this Transform if no
        /// VisualRoot was assigned.</summary>
        public Transform AimPoint => _visualRoot != null ? _visualRoot : transform;

        /// <summary>True only while this prop is eligible to start a fresh tractor beam absorption.</summary>
        public bool IsAbsorbable => _canBeAbsorbed && State == TractorAbsorbablePropState.Idle;

        /// <summary>Fired exactly once, when this prop finishes its Lift phase. No payload beyond "which prop" —
        /// there is no reward to report.</summary>
        public event Action<TractorAbsorbableProp> Absorbed;

        private void Awake()
        {
            ResolveVisualRoot();
            _initialVisualScale = _visualRoot.localScale;

            _colliders = GetComponentsInChildren<Collider>(true);
            _animalWanderer = GetComponent<AnimalWanderer>();
            enabled = false;
        }

        /// <summary>Settles which Transform the capture shrink/tilt may actually write to, once, at Awake.
        ///
        /// The rule is simply "the visual root has to actually move everything this prop draws with". Whatever
        /// VisualRoot was authored as, if anything this prop renders would be left behind by tweening it, we fall
        /// back to the prop's own Transform - the one Transform guaranteed to contain the whole object.
        ///
        /// Two authoring patterns break the assumption, and between them they covered most of the scene:
        ///
        /// - LOD children. Most environment art is a LODGroup whose LOD0/LOD1/LOD2 renderers are siblings, and
        ///   VisualRoot was pointed at the LOD0 child. LODGroup picks which one to draw from screen size, so the
        ///   shrink/lean was visible only while LOD0 happened to be the active level and silently did nothing at
        ///   LOD1/LOD2 - which is why tall trees and rock piles (large, drawn at a coarser LOD) looked completely
        ///   inert while the small props beside them worked.
        ///
        /// - Skinned meshes. A SkinnedMeshRenderer is drawn purely from its bone Transforms; the Transform the
        ///   renderer sits on contributes nothing. The animals (Cow/Horse/Sheep) put the renderer and the
        ///   "Armature" bone hierarchy side by side under the prop root, so tweening the renderer child changed
        ///   every number and moved no pixels.
        ///
        /// Position is driven separately (see TickPull/TickLift) and never touches localRotation/localScale, so
        /// promoting the visual root to the prop root has nothing to conflict with.</summary>
        private void ResolveVisualRoot()
        {
            if (_visualRoot == null)
            {
                _visualRoot = transform; // what the VisualRoot tooltip has always promised
                return;
            }

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                // Drawn from somewhere the visual root doesn't reach - it would stay full-size and upright.
                if (!renderer.transform.IsChildOf(_visualRoot))
                {
                    _visualRoot = transform;
                    return;
                }

                // Inside the visual root, but posed by bones that are not.
                if (renderer is SkinnedMeshRenderer skinned &&
                    skinned.rootBone != null && !skinned.rootBone.IsChildOf(_visualRoot))
                {
                    _visualRoot = transform;
                    return;
                }
            }
        }

        /// <summary>Called once by LevelCompositionRoot's environment-prop bootstrap collection (see
        /// TractorAbsorbablePropRegistry's doc comment for why this isn't OnEnable/OnDisable self-registration).</summary>
        public void Register(TractorAbsorbablePropRegistry registry)
        {
            _registry = registry;
            _registry?.Register(this);
        }

        /// <summary>Admission into a tractor beam. Fails silently if not currently Idle, or if CanBeAbsorbed is off.</summary>
        public bool TryBeginAbsorption(in TractorPropAbsorptionRequest request)
        {
            if (!_canBeAbsorbed || State != TractorAbsorbablePropState.Idle || !request.IsValid)
            {
                return false;
            }

            _request = request;
            _groundY = transform.position.y;
            SetCollidersEnabled(false);

            if (_animalWanderer != null)
            {
                _animalWanderer.enabled = false;
            }

            _absorbElapsed = 0f;
            StartCaptureShrink();
            StartCaptureTilt();

            State = TractorAbsorbablePropState.Pulling;
            enabled = true;
            return true;
        }

        private void SetCollidersEnabled(bool value)
        {
            if (_colliders == null)
            {
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = value;
                }
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances Pull/Lift by deltaTime. Separated from Update() so tests can drive it with an
        /// explicit deltaTime instead of relying on Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            // Counts across Pull AND Lift, so the acceleration ramp doesn't restart when the phase changes.
            _absorbElapsed += deltaTime;

            switch (State)
            {
                case TractorAbsorbablePropState.Pulling:
                    TickPull(deltaTime);
                    break;
                case TractorAbsorbablePropState.Lifting:
                    TickLift(deltaTime);
                    break;
            }
        }

        private void TickPull(float deltaTime)
        {
            if (!_request.IsValid)
            {
                return;
            }

            float boost = (_request.SpeedBoost?.PullSpeedMultiplier ?? 1f) * AbsorbSpeedFactor;
            transform.position = TractorPullLiftMotion.TickPull(
                transform.position, _request.BeamGroundAnchor.position, _groundY,
                _request.PullSpeed * boost, _request.CenterThreshold, deltaTime, out bool reachedCenter);

            // Nothing to drive per-frame during Pull any more: the capture shrink is its own tween, started in
            // TryBeginAbsorption and left to run across both phases.
            if (reachedCenter)
            {
                BeginLift();
            }
        }

        /// <summary>Starts the quick eased shrink from full size down to CaptureStartScale - timed by seconds
        /// rather than Pull distance (see CaptureStartScale's tooltip: Pull/Lift alone finish in well under a
        /// second at the beam's default speed, too fast for a distance-tied shrink to read as anything but an
        /// instant snap). No-op at CaptureStartScale's default of 1, which is every ordinary prop.</summary>
        private void StartCaptureShrink()
        {
            _captureShrinkTween?.Kill();
            _captureShrinkTween = null;
            _captureTiltTween?.Kill();
            _captureTiltTween = null;

            if (_visualRoot == null || _captureStartScale >= 1f)
            {
                return;
            }

            _captureShrinkTween = _visualRoot
                .DOScale(_initialVisualScale * CaptureStartScale, _captureShrinkDuration)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>True while the capture shrink above is still running - Lift's own distance-based shrink has to
        /// hold off until then, or the two would fight over the same frame's scale.</summary>
        private bool IsCaptureShrinkRunning => _captureShrinkTween != null && _captureShrinkTween.IsActive() && _captureShrinkTween.IsPlaying();

        /// <summary>Leans the object over around a random horizontal axis as the beam grabs it, so it reads as
        /// being yanked off the ground rather than sliding along it (see CaptureTiltDegrees' tooltip). Rotates
        /// from wherever the prop was already facing - scene decoration is randomly rotated for variety, and
        /// that base orientation has to be preserved, so the lean is applied as a quaternion offset rather than
        /// by overwriting euler angles.</summary>
        private void StartCaptureTilt()
        {
            _captureTiltTween?.Kill();
            _captureTiltTween = null;

            if (_visualRoot == null || _captureTiltDegrees <= 0f)
            {
                return;
            }

            // Fully qualified: this file has `using System`, which makes a bare `Random` ambiguous.
            Vector2 randomHorizontal = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomHorizontal.sqrMagnitude < 0.0001f)
            {
                randomHorizontal = Vector2.right;
            }

            var tiltAxis = new Vector3(randomHorizontal.x, 0f, randomHorizontal.y);

            // PRE-multiplied, so the lean is measured around a horizontal axis in the PARENT's space rather than
            // the model's own. Imported models bake wildly different local axes (many FBXs sit -90° on X), so a
            // local-axis lean would tip a tree 30° but roll a cow onto its back - this keeps every object leaning
            // by the same visible amount.
            Quaternion tilted = Quaternion.AngleAxis(_captureTiltDegrees, tiltAxis) * _visualRoot.localRotation;

            _captureTiltTween = _visualRoot.DOLocalRotateQuaternion(tilted, _captureShrinkDuration)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>True while the lean is still easing in - the Lift spin has to hold off until then, since both
        /// drive the same rotation.</summary>
        private bool IsCaptureTiltRunning => _captureTiltTween != null && _captureTiltTween.IsActive() && _captureTiltTween.IsPlaying();

        /// <summary>Ramps from AbsorbStartSpeedFactor up to full speed over AbsorbAccelerationTime - the beam
        /// peels this object away slowly and then whips it up, instead of dragging it at one flat speed. Shared
        /// by Pull and Lift so the two phases read as one continuous motion.</summary>
        private float AbsorbSpeedFactor
        {
            get
            {
                if (_absorbStartSpeedFactor >= 1f)
                {
                    return 1f;
                }

                float t = Mathf.Clamp01(_absorbElapsed / _absorbAccelerationTime);
                return Mathf.Lerp(_absorbStartSpeedFactor, 1f, Mathf.SmoothStep(0f, 1f, t));
            }
        }

        private void BeginLift()
        {
            State = TractorAbsorbablePropState.Lifting;
            _liftStartDistance = _request.IsValid
                ? Mathf.Max(0.01f, Vector3.Distance(transform.position, _request.CaptureSocket.position))
                : 1f;
        }

        private void TickLift(float deltaTime)
        {
            if (!_request.IsValid)
            {
                return;
            }

            float boost = (_request.SpeedBoost?.PullSpeedMultiplier ?? 1f) * AbsorbSpeedFactor;
            transform.position = TractorPullLiftMotion.TickLift(
                transform.position, _request.CaptureSocket.position,
                _request.LiftSpeed * boost, _request.SocketThreshold, deltaTime,
                out float remainingDistance, out bool reachedSocket);

            UpdateLiftVisual(remainingDistance, deltaTime);

            if (reachedSocket)
            {
                CompleteAbsorption();
            }
        }

        private void UpdateLiftVisual(float remainingDistance, float deltaTime)
        {
            if (_visualRoot == null)
            {
                return;
            }

            // The quick capture-shrink tween (1 -> CaptureStartScale) keeps running here too, in case Pull
            // finished before it did - only once IT'S done does Lift's own distance-based shrink (CaptureStartScale
            // -> MinimumVisualScale) take over, so the two never fight over the same frame's scale value.
            bool captureShrinkStillRunning = IsCaptureShrinkRunning;

            if (!captureShrinkStillRunning && _request.ShrinkDuringLift)
            {
                float progress = TractorPullLiftMotion.ComputeLiftProgress(remainingDistance, _liftStartDistance);
                float scale = Mathf.Lerp(CaptureStartScale, _request.MinimumVisualScale, progress);
                _visualRoot.localScale = _initialVisualScale * scale;
            }

            // Turns around WORLD up, not the object's own (already leaned, and differently baked per model) axis:
            // that keeps the lean pointing the same way the whole ride up and makes every object rotate at the
            // same visible rate, instead of some spinning flat and others tumbling end over end. Held off while
            // the tilt tween is still easing in, since both write the same rotation.
            if (_request.SpinSpeedDegreesPerSecond > 0f && !IsCaptureTiltRunning)
            {
                _visualRoot.Rotate(Vector3.up, _request.SpinSpeedDegreesPerSecond * deltaTime, Space.World);
            }
        }

        private void CompleteAbsorption()
        {
            State = TractorAbsorbablePropState.Absorbed;
            enabled = false;

            // This object is about to be destroyed - a live tween still holding its Transform would throw on its
            // next tick.
            _captureShrinkTween?.Kill();
            _captureShrinkTween = null;
            _captureTiltTween?.Kill();
            _captureTiltTween = null;

            _generation++;
            _registry?.Unregister(this);
            Absorbed?.Invoke(this);

            // Fire the event (and let subscribers read `this`) BEFORE destroying — DestroyImmediate (the edit-mode/
            // test path) invalidates the object the instant it runs, unlike Destroy's end-of-frame Play Mode path.
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy()
        {
            _captureShrinkTween?.Kill();
            _captureTiltTween?.Kill();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _experienceReward = Mathf.Clamp(_experienceReward, 1, 2);

            if (_tractorResistance <= 0f)
            {
                Debug.LogWarning($"[TractorAbsorbableProp] '{name}': Tractor Resistance must be > 0.", this);
            }

            if (_visualRoot == null)
            {
                Debug.LogWarning($"[TractorAbsorbableProp] '{name}': no Visual Root assigned — shrink/spin during Lift will be skipped.", this);
            }

            if (gameObject.isStatic)
            {
                Debug.LogWarning($"[TractorAbsorbableProp] '{name}' is marked Static but the tractor beam moves it at " +
                    "runtime — uncheck Static (and any static batching) or this prop will not visually move when absorbed.", this);
            }

            // Safety net for hand-adding this component in the Inspector (bypassing Tools/Alien Defense/Tractor
            // Beam/Mark Selected As Absorbable, which already refuses to add it to these). Editor-only: compiled
            // out of player builds, so it adds no runtime dependency on Building/Towers/Core/Enemies.
            if (GetComponent<Terrain>() != null || GetComponent<TerrainCollider>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is Terrain — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponent<AlienDefense.Towers.TowerController>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is a Tower — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponent<AlienDefense.Building.BuildNode>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is a BuildNode — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponent<AlienDefense.Enemies.EnemyController>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is an Enemy — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponent<AlienDefense.Player.PlayerController>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is the Player — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponent<Camera>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' has a Camera component — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponent<AlienDefense.Core.LevelCompositionRoot>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is the CompositionRoot — this must never be absorbable. Remove this component.", this);
            }

            if (GetComponentInParent<AlienDefense.Enemies.EnemyPath3D>() != null)
            {
                Debug.LogError($"[TractorAbsorbableProp] '{name}' is the EnemyPath or one of its Waypoints — this must never be absorbable. Remove this component.", this);
            }
        }
#endif
    }
}
