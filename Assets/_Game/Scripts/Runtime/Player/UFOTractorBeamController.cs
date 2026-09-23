using Unity.Profiling;
using System;
using System.Collections.Generic;
using AlienDefense.CameraSystem;
using AlienDefense.Common;
using AlienDefense.Economy;
using AlienDefense.Enemies;
using AlienDefense.Environment;
using AlienDefense.Pickups;
using AlienDefense.Settings;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Continuous area-admission tractor beam covering three independent target categories — Enemy
    /// capture, Energy Pickup absorption, Environment Prop absorption — each with its own registry, its own
    /// concurrent-slot limit, and its own admission scan, sharing only the beam geometry (BeamGroundAnchor/
    /// CaptureSocket) and the scan interval. A full Enemy beam never blocks Energy or Prop admission and vice
    /// versa. Never moves anything itself (each target type owns its own Pull/Lift), never grants reward itself
    /// (Enemy capture grants none at all; Energy reward is granted by EnergyPickupFactory when a pickup finishes;
    /// Prop absorption grants none). Reads EnergyWalletService.IsFull only to refuse new Energy admissions when
    /// cargo is at capacity — never calls Add/Spend itself.</summary>
    public sealed class UFOTractorBeamController : MonoBehaviour, IPullSpeedBoostSource
    {
        [SerializeField]
        private UFOTractorBeamDefinition _definition;

        [SerializeField]
        [Tooltip("Gameplay center of the beam on the ground, directly under the UFO. Not the visual model.")]
        private Transform _beamGroundAnchor;

        [SerializeField]
        [Tooltip("Where a captured/absorbed object ends its Lift phase, inside/under the UFO body.")]
        private Transform _captureSocket;

        [SerializeField]
        [Tooltip("Optional. Must implement IMovementDirectionSource (e.g. PlayerController). While the Player is " +
            "actively moving, every object currently being pulled/lifted gets a temporary speed boost (see Moving " +
            "Pull Speed Multiplier) so it keeps pace with the beam instead of trailing behind as the UFO flies " +
            "away. Leave empty to disable the boost entirely — Pull/Lift then always run at their base speed.")]
        private MonoBehaviour _movementDirectionSource;

        [SerializeField, Min(1f)]
        [Tooltip("Extra multiplier stacked on top of every active capture/absorption's PullSpeed and LiftSpeed " +
            "while the Player is moving (see Movement Direction Source above). 1 = no boost.")]
        private float _movingPullSpeedMultiplier = 1.6f;

        private IMovementDirectionSource _resolvedMovementSource;

        // Reused by ScanForImmuneObjects so its per-scan overlap query never allocates. Overflow is harmless:
        // anything past the buffer just misses one wobble and gets it on the next scan.
        private readonly Collider[] _immuneOverlapBuffer = new Collider[32];

        private EnemyRegistry _enemyRegistry;
        private EnergyPickupRegistry _energyRegistry;
        private TractorAbsorbablePropRegistry _propRegistry;
        private EnergyWalletService _energyWallet;
        private SettingsService _settingsService;
        private float _cargoFullFeedbackCooldown;

        private readonly HashSet<CaptureHandle> _activeCaptures = new HashSet<CaptureHandle>();
        private readonly HashSet<EnergyHandle> _activeEnergyAbsorptions = new HashSet<EnergyHandle>();
        private readonly HashSet<PropHandle> _activePropAbsorptions = new HashSet<PropHandle>();

        private float _scanTimer;
        private bool _isEnabled = true;
        private float _radiusMultiplier = 1f;
        private float _pullSpeedMultiplier = 1f;
        private float _broadcastRadius = -1f; // last AttractionRadius sent through BeamGeometryChanged

        /// <summary>Runtime-only multipliers stacked on top of the definition's own AttractionRadius/PullSpeed -
        /// never mutates the asset. Intended caller: PlayerSkillEffectApplier, combining the Radius skill's own
        /// multiplier with the Magnet skill's additional bonus into these two final numbers.</summary>
        public void SetSkillMultipliers(float radiusMultiplier, float pullSpeedMultiplier)
        {
            _radiusMultiplier = Mathf.Max(0.01f, radiusMultiplier);
            _pullSpeedMultiplier = Mathf.Max(0.01f, pullSpeedMultiplier);
            UpdateBeamGeometry(); // re-broadcasts the new AttractionRadius so the beam and its ring resize with it
        }

        public bool IsEnabled => _isEnabled;

        public int ActiveCaptureCount => _activeCaptures.Count;
        public int ActiveEnergyAbsorptionCount => _activeEnergyAbsorptions.Count;
        public int ActivePropAbsorptionCount => _activePropAbsorptions.Count;

        /// <summary>Sum across all three categories — what the beam VISUAL's intensity should scale with (see
        /// UFOTractorBeamVisual), since a viewer never needs to know which category is currently active.</summary>
        public int TotalActiveAbsorptionCount => ActiveCaptureCount + ActiveEnergyAbsorptionCount + ActivePropAbsorptionCount;

        public int MaxConcurrentCaptures => _definition != null ? _definition.MaxConcurrentCaptures : 0;
        public int MaxConcurrentEnergyAbsorptions => _definition != null ? _definition.MaxConcurrentEnergyAbsorptions : 0;
        public int MaxConcurrentPropAbsorptions => _definition != null ? _definition.MaxConcurrentPropAbsorptions : 0;

        public float AttractionRadius => _definition != null ? _definition.AttractionRadius * _radiusMultiplier : 0f;

        /// <summary>IPullSpeedBoostSource implementation — read live every frame by every currently Pulling/
        /// Lifting object (see TractorCaptureRequest/TractorEnergyAbsorptionRequest/TractorPropAbsorptionRequest's
        /// SpeedBoost field), not just the ones admitted while the Player happened to already be moving.</summary>
        public float PullSpeedMultiplier =>
            _resolvedMovementSource != null && _resolvedMovementSource.MovementDirection.sqrMagnitude > 0.0001f
                ? _movingPullSpeedMultiplier
                : 1f;

        public bool HasAvailableSlot => MaxConcurrentCaptures <= 0 || ActiveCaptureCount < MaxConcurrentCaptures;
        public bool HasAvailableEnergySlot => MaxConcurrentEnergyAbsorptions <= 0 || ActiveEnergyAbsorptionCount < MaxConcurrentEnergyAbsorptions;
        public bool HasAvailablePropSlot => MaxConcurrentPropAbsorptions <= 0 || ActivePropAbsorptionCount < MaxConcurrentPropAbsorptions;

        /// <summary>World-space distance between CaptureSocket and BeamGroundAnchor. Purely geometric, for visual sizing only.</summary>
        public float BeamLength { get; private set; }

        public event Action<int> ActiveCaptureCountChanged;
        public event Action<int> TotalActiveAbsorptionCountChanged;
        public event Action<EnemyController> EnemyCaptureStarted;
        public event Action<EnemyController> EnemyCaptureCompleted;
        public event Action<EnergyPickupController> EnergyAbsorptionStarted;
        public event Action<EnergyPickupController> EnergyPickupCollected;
        /// <summary>Fired (throttled) when the beam is over an Idle EnergyPickup but cargo is full.</summary>
        public event Action EnergyCargoFullRefused;
        public event Action<TractorAbsorbableProp> PropAbsorptionStarted;
        public event Action<TractorAbsorbableProp> PropAbsorbed;
        public event Action<bool> BeamEnabledChanged;

        /// <summary>Fired once at Initialize (and again if the anchors ever move relative to each other) with
        /// (length, radius) so visual pieces can size themselves without reading anchors or the Definition directly.</summary>
        public event Action<float, float> BeamGeometryChanged;

        private void Awake()
        {
            _resolvedMovementSource = _movementDirectionSource as IMovementDirectionSource;
        }

        /// <summary>energyRegistry/propRegistry/energyWallet/settings are optional so existing callers (tests that
        /// only exercise Enemy capture) keep compiling unchanged; Energy cargo-full refuse no-ops while wallet is null.</summary>
        public void Initialize(
            EnemyRegistry enemyRegistry,
            EnergyPickupRegistry energyRegistry = null,
            TractorAbsorbablePropRegistry propRegistry = null,
            EnergyWalletService energyWallet = null,
            SettingsService settingsService = null)
        {
            _enemyRegistry = enemyRegistry;
            _energyRegistry = energyRegistry;
            _propRegistry = propRegistry;
            _energyWallet = energyWallet;
            _settingsService = settingsService;
            _scanTimer = 0f;
            _cargoFullFeedbackCooldown = 0f;
            UpdateBeamGeometry();
        }

        /// <summary>UFO hover height is fixed in this game, so beam geometry is effectively constant after the first
        /// push. Kept as a cheap distance check (no allocation) rather than continuous per-frame push, so it still
        /// self-corrects if hover height/anchors ever become dynamic without paying for it every frame today.</summary>
        private void UpdateBeamGeometry()
        {
            if (_beamGroundAnchor == null || _captureSocket == null)
            {
                return;
            }

            float length = Vector3.Distance(_captureSocket.position, _beamGroundAnchor.position);
            float radius = AttractionRadius;

            // Radius counts too, not just length: the Radius/Magnet skills change AttractionRadius at runtime, and
            // when only length was compared the visuals never heard about it - the beam and its ground ring kept
            // their level-start size while the real capture area grew around them, so enemies well outside the
            // ring were being pulled in.
            if (Mathf.Approximately(length, BeamLength) && Mathf.Approximately(radius, _broadcastRadius))
            {
                return;
            }

            BeamLength = length;
            _broadcastRadius = radius;
            BeamGeometryChanged?.Invoke(length, radius);
        }

        /// <summary>Gates admission of new captures/absorptions only. Objects already being pulled/lifted keep
        /// going (correct for a transient Pause); level cleanup aborts them separately.</summary>
        public void SetEnabled(bool value)
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            BeamEnabledChanged?.Invoke(value);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the shared scan timer by deltaTime and admits new captures/absorptions across all
        /// three categories once it elapses. Separated from Update() so tests can drive it with an explicit
        /// deltaTime instead of relying on Time.deltaTime.</summary>
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("AlienDefense.TractorBeam.Scan");

        public void Tick(float deltaTime)
        {
            using (TickMarker.Auto())
            {
                TickCore(deltaTime);
            }
        }

        private void TickCore(float deltaTime)
        {
            if (_cargoFullFeedbackCooldown > 0f)
            {
                _cargoFullFeedbackCooldown -= deltaTime;
            }

            if (!_isEnabled || _definition == null || _beamGroundAnchor == null)
            {
                return;
            }

            _scanTimer -= deltaTime;
            if (_scanTimer > 0f)
            {
                return;
            }

            _scanTimer = _definition.ScanInterval;
            ScanForNewCaptures();
            ScanForNewEnergyAbsorptions();
            ScanForNewPropAbsorptions();
            ScanForImmuneObjects();
        }

        /// <summary>Cosmetic fourth pass: rocks any TractorImmuneShake standing in the beam, so a building the
        /// beam can't take reads as "tugged at but too heavy" instead of silently ignoring the beam. Nothing is
        /// admitted, no slot is consumed and no state changes - which is why this runs regardless of whether the
        /// capture/absorption slots are full.
        ///
        /// A physics overlap rather than a registry because these are ordinary scene decoration with no
        /// lifecycle worth tracking; at one query per ScanInterval against a radius this small that's cheaper
        /// than maintaining registration for every building on the level.</summary>
        private void ScanForImmuneObjects()
        {
            int count = Physics.OverlapSphereNonAlloc(
                _beamGroundAnchor.position, AttractionRadius, _immuneOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider candidate = _immuneOverlapBuffer[i];
                if (candidate == null)
                {
                    continue;
                }

                // GetComponentInParent because the collider usually sits on a mesh child, not the marked root.
                var immune = candidate.GetComponentInParent<TractorImmuneShake>();
                if (immune != null && !immune.IsBusy)
                {
                    immune.Nudge();
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Enemy capture — unchanged from before the multi-type refactor.
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Linear EnemyRegistry scan, XZ squared distance only, no allocation. Admission order follows
        /// registry order (roughly spawn order) rather than a true nearest-first sort: with a small attraction
        /// radius and a fast scan interval this reads as "nearest fills first" in practice, without the cost of a
        /// per-scan candidate buffer/partial-sort for a slot count this small (MaxConcurrentCaptures ~8).</summary>
        private void ScanForNewCaptures()
        {
            if (_enemyRegistry == null || !HasAvailableSlot)
            {
                return;
            }

            Vector3 beamPosition = _beamGroundAnchor.position;
            float radiusSquared = AttractionRadius * AttractionRadius;

            for (int i = 0; i < _enemyRegistry.Count; i++)
            {
                if (!HasAvailableSlot)
                {
                    break;
                }

                EnemyController candidate = _enemyRegistry.GetAt(i);
                if (candidate == null || !candidate.IsCapturable)
                {
                    continue;
                }

                Vector3 position = candidate.AimPoint.position;
                float dx = position.x - beamPosition.x;
                float dz = position.z - beamPosition.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                TryBeginCapture(candidate);
            }
        }

        private void TryBeginCapture(EnemyController enemy)
        {
            float resistance = enemy.Definition != null ? Mathf.Max(0.01f, enemy.Definition.TractorResistance) : 1f;
            float effectivePullSpeed = _definition.PullSpeed * _pullSpeedMultiplier / resistance;

            var request = new TractorCaptureRequest(
                _beamGroundAnchor,
                _captureSocket,
                effectivePullSpeed,
                _definition.LiftSpeed,
                _definition.BeamCenterThreshold,
                _definition.CaptureSocketThreshold,
                _definition.ShrinkDuringLift,
                _definition.MinimumVisualScale,
                _definition.LiftSpinSpeedDegreesPerSecond,
                this);

            if (!enemy.TryBeginTractorCapture(request))
            {
                return;
            }

            _activeCaptures.Add(new CaptureHandle(enemy));
            enemy.Resolved += HandleCapturedEnemyResolved;

            ActiveCaptureCountChanged?.Invoke(_activeCaptures.Count);
            TotalActiveAbsorptionCountChanged?.Invoke(TotalActiveAbsorptionCount);
            EnemyCaptureStarted?.Invoke(enemy);
        }

        private void HandleCapturedEnemyResolved(EnemyController enemy, EnemyResolveReason reason)
        {
            enemy.Resolved -= HandleCapturedEnemyResolved;
            _activeCaptures.Remove(new CaptureHandle(enemy));
            ActiveCaptureCountChanged?.Invoke(_activeCaptures.Count);
            TotalActiveAbsorptionCountChanged?.Invoke(TotalActiveAbsorptionCount);

            if (reason == EnemyResolveReason.Captured)
            {
                EnemyCaptureCompleted?.Invoke(enemy);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Energy Pickup absorption — same scan/admission shape as Enemy capture, separate slot pool. Grants
        // no reward itself: EnergyPickupFactory (not this controller) turns "Collected" into Energy/XP.
        // ---------------------------------------------------------------------------------------------------

        private void ScanForNewEnergyAbsorptions()
        {
            if (_energyRegistry == null)
            {
                return;
            }

            bool cargoFull = _energyWallet != null && _energyWallet.IsFull;
            if (!cargoFull && !HasAvailableEnergySlot)
            {
                return;
            }

            Vector3 beamPosition = _beamGroundAnchor.position;
            float radiusSquared = AttractionRadius * AttractionRadius;

            for (int i = 0; i < _energyRegistry.Count; i++)
            {
                EnergyPickupController candidate = _energyRegistry.GetAt(i);
                if (candidate == null || !candidate.IsAbsorbable)
                {
                    continue;
                }

                Vector3 position = candidate.transform.position;
                float dx = position.x - beamPosition.x;
                float dz = position.z - beamPosition.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                if (cargoFull)
                {
                    HandleCargoFullRefuse(candidate);
                    continue;
                }

                if (!HasAvailableEnergySlot)
                {
                    break;
                }

                TryBeginEnergyAbsorption(candidate);
            }
        }

        private void HandleCargoFullRefuse(EnergyPickupController pickup)
        {
            // Per-pickup shake — TractorImmuneShake self-throttles via IsBusy. Must run before any global cooldown
            // so every Idle ball under the beam can rock when cargo is full.
            TractorImmuneShake shake = pickup.GetComponent<TractorImmuneShake>();
            if (shake != null && !shake.IsBusy)
            {
                shake.Nudge();
            }

            // Presence UI every refuse (banner hold); haptic stays throttled.
            EnergyCargoFullRefused?.Invoke();

            if (_cargoFullFeedbackCooldown > 0f)
            {
                return;
            }

            _cargoFullFeedbackCooldown = 0.9f;

            bool hapticsEnabled = _settingsService == null || _settingsService.Current.HapticsEnabled;
            if (hapticsEnabled)
            {
                Handheld.Vibrate();
            }
        }

        private void TryBeginEnergyAbsorption(EnergyPickupController pickup)
        {
            var request = new TractorEnergyAbsorptionRequest(
                _beamGroundAnchor,
                _captureSocket,
                _definition.PullSpeed * _pullSpeedMultiplier,
                _definition.LiftSpeed,
                _definition.BeamCenterThreshold,
                _definition.CaptureSocketThreshold,
                _definition.ShrinkDuringLift,
                _definition.MinimumVisualScale,
                _definition.LiftSpinSpeedDegreesPerSecond,
                this);

            if (!pickup.TryBeginAbsorption(request))
            {
                return;
            }

            // A ball still rocking from an earlier cargo-full refuse must settle before the lift spins/shrinks it.
            TractorImmuneShake shake = pickup.GetComponent<TractorImmuneShake>();
            if (shake != null)
            {
                shake.StopShake();
            }

            _activeEnergyAbsorptions.Add(new EnergyHandle(pickup));
            pickup.Collected += HandleEnergyPickupCollected;

            TotalActiveAbsorptionCountChanged?.Invoke(TotalActiveAbsorptionCount);
            EnergyAbsorptionStarted?.Invoke(pickup);
        }

        private void HandleEnergyPickupCollected(EnergyPickupController pickup)
        {
            pickup.Collected -= HandleEnergyPickupCollected;
            _activeEnergyAbsorptions.Remove(new EnergyHandle(pickup));
            TotalActiveAbsorptionCountChanged?.Invoke(TotalActiveAbsorptionCount);
            EnergyPickupCollected?.Invoke(pickup);
        }

        // ---------------------------------------------------------------------------------------------------
        // Environment Prop absorption — same scan/admission shape again, its own slot pool. Grants no reward.
        // ---------------------------------------------------------------------------------------------------

        private void ScanForNewPropAbsorptions()
        {
            if (_propRegistry == null || !HasAvailablePropSlot)
            {
                return;
            }

            Vector3 beamPosition = _beamGroundAnchor.position;
            float radiusSquared = AttractionRadius * AttractionRadius;

            for (int i = 0; i < _propRegistry.Count; i++)
            {
                if (!HasAvailablePropSlot)
                {
                    break;
                }

                TractorAbsorbableProp candidate = _propRegistry.GetAt(i);
                if (candidate == null || !candidate.IsAbsorbable)
                {
                    continue;
                }

                Vector3 position = candidate.AimPoint.position;
                float dx = position.x - beamPosition.x;
                float dz = position.z - beamPosition.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                TryBeginPropAbsorption(candidate);
            }
        }

        private void TryBeginPropAbsorption(TractorAbsorbableProp prop)
        {
            float resistance = prop.TractorResistance;
            float effectivePullSpeed = _definition.PullSpeed * prop.PullSpeedMultiplier * _pullSpeedMultiplier / resistance;
            float effectiveLiftSpeed = _definition.LiftSpeed * prop.LiftSpeedMultiplier / resistance;

            var request = new TractorPropAbsorptionRequest(
                _beamGroundAnchor,
                _captureSocket,
                effectivePullSpeed,
                effectiveLiftSpeed,
                _definition.BeamCenterThreshold,
                _definition.CaptureSocketThreshold,
                prop.ShrinkDuringLift,
                prop.MinimumVisualScale,
                _definition.LiftSpinSpeedDegreesPerSecond,
                this);

            if (!prop.TryBeginAbsorption(request))
            {
                return;
            }

            _activePropAbsorptions.Add(new PropHandle(prop));
            prop.Absorbed += HandlePropAbsorbed;

            TotalActiveAbsorptionCountChanged?.Invoke(TotalActiveAbsorptionCount);
            PropAbsorptionStarted?.Invoke(prop);
        }

        private void HandlePropAbsorbed(TractorAbsorbableProp prop)
        {
            prop.Absorbed -= HandlePropAbsorbed;
            _activePropAbsorptions.Remove(new PropHandle(prop));
            TotalActiveAbsorptionCountChanged?.Invoke(TotalActiveAbsorptionCount);
            PropAbsorbed?.Invoke(prop);
        }

        private void OnDestroy()
        {
            foreach (CaptureHandle handle in _activeCaptures)
            {
                if (handle.IsValid)
                {
                    handle.Enemy.Resolved -= HandleCapturedEnemyResolved;
                }
            }

            _activeCaptures.Clear();

            foreach (EnergyHandle handle in _activeEnergyAbsorptions)
            {
                if (handle.IsValid)
                {
                    handle.Pickup.Collected -= HandleEnergyPickupCollected;
                }
            }

            _activeEnergyAbsorptions.Clear();

            foreach (PropHandle handle in _activePropAbsorptions)
            {
                if (handle.IsValid)
                {
                    handle.Prop.Absorbed -= HandlePropAbsorbed;
                }
            }

            _activePropAbsorptions.Clear();
        }

        /// <summary>Safe identity for a tracked capture: reuses EnemyController's existing pooled-instance
        /// generation token instead of a second staleness system.</summary>
        private readonly struct CaptureHandle : IEquatable<CaptureHandle>
        {
            public readonly EnemyController Enemy;
            public readonly int Generation;

            public CaptureHandle(EnemyController enemy)
            {
                Enemy = enemy;
                Generation = enemy != null ? enemy.Generation : -1;
            }

            public bool IsValid => Enemy != null && Enemy.Generation == Generation;

            public bool Equals(CaptureHandle other) => ReferenceEquals(Enemy, other.Enemy) && Generation == other.Generation;
            public override bool Equals(object obj) => obj is CaptureHandle other && Equals(other);

            public override int GetHashCode()
            {
                int enemyHash = Enemy != null ? Enemy.GetHashCode() : 0;
                return (enemyHash * 397) ^ Generation;
            }
        }

        /// <summary>Same generation-token safety as CaptureHandle, for pooled EnergyPickupController instances.</summary>
        private readonly struct EnergyHandle : IEquatable<EnergyHandle>
        {
            public readonly EnergyPickupController Pickup;
            public readonly int Generation;

            public EnergyHandle(EnergyPickupController pickup)
            {
                Pickup = pickup;
                Generation = pickup != null ? pickup.Generation : -1;
            }

            public bool IsValid => Pickup != null && Pickup.Generation == Generation;

            public bool Equals(EnergyHandle other) => ReferenceEquals(Pickup, other.Pickup) && Generation == other.Generation;
            public override bool Equals(object obj) => obj is EnergyHandle other && Equals(other);

            public override int GetHashCode()
            {
                int hash = Pickup != null ? Pickup.GetHashCode() : 0;
                return (hash * 397) ^ Generation;
            }
        }

        /// <summary>Same generation-token safety as CaptureHandle. Scene-authored props are never pooled, but the
        /// token still guards against the (currently theoretical) case of a stale reference outliving a Destroy.</summary>
        private readonly struct PropHandle : IEquatable<PropHandle>
        {
            public readonly TractorAbsorbableProp Prop;
            public readonly int Generation;

            public PropHandle(TractorAbsorbableProp prop)
            {
                Prop = prop;
                Generation = prop != null ? prop.Generation : -1;
            }

            public bool IsValid => Prop != null && Prop.Generation == Generation;

            public bool Equals(PropHandle other) => ReferenceEquals(Prop, other.Prop) && Generation == other.Generation;
            public override bool Equals(object obj) => obj is PropHandle other && Equals(other);

            public override int GetHashCode()
            {
                int hash = Prop != null ? Prop.GetHashCode() : 0;
                return (hash * 397) ^ Generation;
            }
        }
    }
}
