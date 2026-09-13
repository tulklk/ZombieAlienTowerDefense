using AlienDefense.Common;
using AlienDefense.Data;
using AlienDefense.Input;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Moves the UFO on the XZ plane via CharacterController and holds it at hover height above the
    /// actual terrain surface (not a fixed world-Y), so hovering stays correct over hills.
    ///
    /// Deliberately follows the Terrain only, never the buildings standing on it: the UFO is meant to sail
    /// straight over/through them at a constant height rather than climbing onto their roofs. Structures it
    /// cannot absorb announce themselves by shaking instead - see TractorImmuneShake.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private CharacterController _characterController;
        private PlayerDefinition _definition;
        private IPlayerInput _input;
        private LevelBounds _levelBounds;

        // Optional: when assigned, joystick input is remapped onto this camera's current forward/right (projected
        // onto the XZ plane) instead of raw world X/Z, so "up" on the stick always matches "up" on screen even
        // after the follow camera's angle changes. Null falls back to raw world-axis input (old behavior).
        private Transform _cameraTransform;

        // Cached once at Initialize (not queried per-frame): Terrain.activeTerrains allocates a fresh array on
        // every call, which is unacceptable in Update.
        private Terrain[] _terrains;

        private Vector3 _currentVelocity;
        private bool _isInitialized;
        private bool _movementEnabled = true;
        private float _speedMultiplier = 1f;

        /// <summary>Runtime-only multiplier on top of PlayerDefinition.MoveSpeed - never mutates the asset
        /// itself. Intended caller: PlayerSkillEffectApplier, driven by the player's current Speed skill rank.</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>Raw stick input after dead-zone filtering (screen-relative, not world-relative).</summary>
        public Vector2 CurrentMoveInput { get; private set; }

        /// <summary>This frame's actual world-space XZ movement direction (after camera-relative remapping);
        /// consumed by hover visual tilt and camera look-ahead, both of which need a world-space direction.</summary>
        public Vector2 CurrentWorldMoveDirection { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        public void Initialize(PlayerDefinition definition, IPlayerInput input, LevelBounds levelBounds, Transform cameraTransform = null)
        {
            _definition = definition;
            _input = input;
            _levelBounds = levelBounds;
            _cameraTransform = cameraTransform;
            _terrains = Terrain.activeTerrains;

            Vector3 position = transform.position;
            position.y = SampleHoverY(position);
            transform.position = position;

            _isInitialized = true;
        }

        /// <summary>The world-space Y this component holds the UFO at over <paramref name="worldPosition"/> - ground
        /// plus hover offset, the same value every movement frame snaps to. False until Initialize has run.
        ///
        /// For scripted flights (UFOFlightIntro) that hand the UFO back to this component: ending the flight at
        /// exactly this height means the first movement frame has nothing to correct, instead of teleporting the
        /// UFO from wherever the flight's authored end point happened to sit.</summary>
        public bool TryGetHoverY(Vector3 worldPosition, out float hoverY)
        {
            if (_definition == null)
            {
                hoverY = 0f;
                return false;
            }

            hoverY = SampleHoverY(worldPosition);
            return true;
        }

        /// <summary>Ground height (from Terrain.SampleHeight, world space) plus the definition's hover offset.
        /// Falls back to the definition's hover height as an absolute world-Y if worldPosition isn't over any
        /// active Terrain tile (e.g. a non-terrain floor area).</summary>
        private float SampleHoverY(Vector3 worldPosition)
        {
            return TryGetGroundHeight(worldPosition, out float groundHeight)
                ? groundHeight + _definition.HoverHeight
                : _definition.HoverHeight;
        }

        private bool TryGetGroundHeight(Vector3 worldPosition, out float groundHeight)
        {
            if (_terrains != null)
            {
                for (int i = 0; i < _terrains.Length; i++)
                {
                    Terrain terrain = _terrains[i];
                    if (terrain == null || terrain.terrainData == null)
                    {
                        continue;
                    }

                    Vector3 terrainOrigin = terrain.transform.position;
                    Vector3 terrainSize = terrain.terrainData.size;

                    bool insideX = worldPosition.x >= terrainOrigin.x && worldPosition.x <= terrainOrigin.x + terrainSize.x;
                    bool insideZ = worldPosition.z >= terrainOrigin.z && worldPosition.z <= terrainOrigin.z + terrainSize.z;
                    if (!insideX || !insideZ)
                    {
                        continue;
                    }

                    groundHeight = terrainOrigin.y + terrain.SampleHeight(worldPosition);
                    return true;
                }
            }

            groundHeight = 0f;
            return false;
        }

        /// <summary>Maps raw joystick input (x = screen-right, y = screen-up) onto world space using the follow
        /// camera's current yaw, so the mapping stays correct after the camera's angle is changed (e.g. via
        /// "Rotate Camera Follow Angle"). Falls back to raw world X/Z if no camera was assigned.</summary>
        private Vector3 ComputeWorldDirection(Vector2 input)
        {
            if (_cameraTransform == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            Vector3 forward = _cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 right = _cameraTransform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }
            else
            {
                right.Normalize();
            }

            return right * input.x + forward * input.y;
        }

        public void SetMovementEnabled(bool value)
        {
            _movementEnabled = value;
            if (!value)
            {
                _currentVelocity = Vector3.zero;
                CurrentMoveInput = Vector2.zero;
                CurrentWorldMoveDirection = Vector2.zero;
            }
        }

        private void Update()
        {
            // The null checks are for Play Mode teardown, where this Update can still run one more time after
            // the input source / definition the composition root injected have already gone away.
            if (!_isInitialized || !_movementEnabled || _input == null || _definition == null)
            {
                return;
            }

            Vector2 rawInput = _input.MoveInput;
            if (rawInput.magnitude < _definition.InputDeadZone)
            {
                rawInput = Vector2.zero;
            }
            else
            {
                rawInput = Vector2.ClampMagnitude(rawInput, 1f);
            }

            CurrentMoveInput = rawInput;

            Vector3 desiredDirection = ComputeWorldDirection(rawInput);
            CurrentWorldMoveDirection = new Vector2(desiredDirection.x, desiredDirection.z);
            Vector3 targetVelocity = desiredDirection * (_definition.MoveSpeed * _speedMultiplier);

            float rate = targetVelocity.sqrMagnitude > _currentVelocity.sqrMagnitude
                ? _definition.Acceleration
                : _definition.Deceleration;
            _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, rate * Time.deltaTime);

            ApplyMovement(desiredDirection);
        }

        private void ApplyMovement(Vector3 desiredDirection)
        {
            Vector3 horizontalDelta = _currentVelocity * Time.deltaTime;
            Vector3 currentPosition = transform.position;
            Vector3 intendedPosition = currentPosition + horizontalDelta;

            if (_levelBounds != null)
            {
                intendedPosition = _levelBounds.ClampXZ(intendedPosition);
            }

            float targetY = SampleHoverY(intendedPosition);
            float heightCorrection = targetY - currentPosition.y;
            Vector3 finalDelta = new Vector3(
                intendedPosition.x - currentPosition.x,
                heightCorrection,
                intendedPosition.z - currentPosition.z);

            _characterController.Move(finalDelta);

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _definition.RotationSpeed * Time.deltaTime);
            }
        }
    }
}
