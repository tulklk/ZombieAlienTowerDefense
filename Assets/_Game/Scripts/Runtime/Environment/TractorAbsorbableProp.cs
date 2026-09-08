using System;
using AlienDefense.Common;
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
        [SerializeField]
        private bool _shrinkDuringLift = true;

        [SerializeField, Range(0.01f, 1f)]
        private float _minimumVisualScale = 0.15f;

        private TractorAbsorbablePropRegistry _registry;
        private TractorPropAbsorptionRequest _request;
        private Vector3 _initialVisualScale = Vector3.one;
        private Collider[] _colliders;
        private float _groundY;
        private float _liftStartDistance = 1f;
        private int _generation;

        public TractorAbsorbablePropState State { get; private set; } = TractorAbsorbablePropState.Idle;
        public bool CanBeAbsorbed => _canBeAbsorbed;
        public float TractorResistance => Mathf.Max(0.01f, _tractorResistance);
        public int ExperienceReward => _experienceReward;
        public float PullSpeedMultiplier => _pullSpeedMultiplier;
        public float LiftSpeedMultiplier => _liftSpeedMultiplier;
        public bool ShrinkDuringLift => _shrinkDuringLift;
        public float MinimumVisualScale => _minimumVisualScale;
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
            if (_visualRoot != null)
            {
                _initialVisualScale = _visualRoot.localScale;
            }

            _colliders = GetComponentsInChildren<Collider>(true);
            enabled = false;
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

            float boost = _request.SpeedBoost?.PullSpeedMultiplier ?? 1f;
            transform.position = TractorPullLiftMotion.TickPull(
                transform.position, _request.BeamGroundAnchor.position, _groundY,
                _request.PullSpeed * boost, _request.CenterThreshold, deltaTime, out bool reachedCenter);

            if (reachedCenter)
            {
                BeginLift();
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

            float boost = _request.SpeedBoost?.PullSpeedMultiplier ?? 1f;
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

            if (_request.ShrinkDuringLift)
            {
                float progress = TractorPullLiftMotion.ComputeLiftProgress(remainingDistance, _liftStartDistance);
                float scale = Mathf.Lerp(1f, _request.MinimumVisualScale, progress);
                _visualRoot.localScale = _initialVisualScale * scale;
            }

            if (_request.SpinSpeedDegreesPerSecond > 0f)
            {
                _visualRoot.Rotate(Vector3.up, _request.SpinSpeedDegreesPerSecond * deltaTime, Space.Self);
            }
        }

        private void CompleteAbsorption()
        {
            State = TractorAbsorbablePropState.Absorbed;
            enabled = false;
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
