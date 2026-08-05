using AlienDefense.Common;
using AlienDefense.Data;
using AlienDefense.Input;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>
    /// Moves the UFO root on the XZ plane using a CharacterController and holds it at a fixed
    /// hover height. CharacterController was chosen over Rigidbody because the UFO needs stable,
    /// predictable mobile-friendly collision with static geometry and never needs forces/impulses —
    /// a kinematic Rigidbody would add FixedUpdate/interpolation bookkeeping for no benefit here.
    /// Reads input through <see cref="IPlayerInput"/> only; never touches a UI widget directly.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private CharacterController _characterController;
        private PlayerDefinition _definition;
        private IPlayerInput _input;
        private LevelBounds _levelBounds;

        private Vector3 _currentVelocity;
        private bool _isInitialized;
        private bool _movementEnabled = true;

        /// <summary>Move input after dead-zone filtering; consumed by hover visual and camera look-ahead.</summary>
        public Vector2 CurrentMoveInput { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        public void Initialize(PlayerDefinition definition, IPlayerInput input, LevelBounds levelBounds)
        {
            _definition = definition;
            _input = input;
            _levelBounds = levelBounds;

            Vector3 position = transform.position;
            position.y = _definition.HoverHeight;
            transform.position = position;

            _isInitialized = true;
        }

        public void SetMovementEnabled(bool value)
        {
            _movementEnabled = value;
            if (!value)
            {
                _currentVelocity = Vector3.zero;
                CurrentMoveInput = Vector2.zero;
            }
        }

        private void Update()
        {
            if (!_isInitialized || !_movementEnabled)
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

            Vector3 desiredDirection = new Vector3(rawInput.x, 0f, rawInput.y);
            Vector3 targetVelocity = desiredDirection * _definition.MoveSpeed;

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

            float heightCorrection = _definition.HoverHeight - currentPosition.y;
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
