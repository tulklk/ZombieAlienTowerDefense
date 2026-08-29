using AlienDefense.CameraSystem;
using AlienDefense.Common;
using AlienDefense.Data;
using AlienDefense.Input;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>Validates the UFO's dependencies and wires them into PlayerMovement.</summary>
    public sealed class PlayerController : MonoBehaviour, IMovementDirectionSource
    {
        [SerializeField]
        private PlayerDefinition _definition;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private LevelBounds _levelBounds;

        [SerializeField]
        [Tooltip("Optional. The follow camera's Transform — when assigned, joystick input is remapped onto its " +
            "current forward/right so movement always matches what's visually up/right on screen.")]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Must implement IPlayerInput.")]
        private MonoBehaviour _inputSource;

        public Vector2 MovementDirection => _movement != null ? _movement.CurrentWorldMoveDirection : Vector2.zero;
        public PlayerDefinition Definition => _definition;

        private void Awake()
        {
            if (_definition == null)
            {
                Debug.LogError("[PlayerController] No PlayerDefinition assigned.", this);
                enabled = false;
                return;
            }

            if (_movement == null)
            {
                Debug.LogError("[PlayerController] No PlayerMovement assigned.", this);
                enabled = false;
                return;
            }

            if (_inputSource is not IPlayerInput playerInput)
            {
                Debug.LogError("[PlayerController] Input source is not assigned or does not implement IPlayerInput.", this);
                enabled = false;
                return;
            }

            _movement.Initialize(_definition, playerInput, _levelBounds, _cameraTransform);
        }

        /// <summary>Enables or disables player movement.</summary>
        public void SetMovementEnabled(bool value)
        {
            if (_movement != null)
            {
                _movement.SetMovementEnabled(value);
            }
        }
    }
}
