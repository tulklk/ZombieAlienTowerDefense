using AlienDefense.CameraSystem;
using AlienDefense.Common;
using AlienDefense.Data;
using AlienDefense.Input;
using UnityEngine;

namespace AlienDefense.Player
{
    /// <summary>
    /// Thin orchestrator for the UFO: validates its fixed dependencies, wires them into
    /// <see cref="PlayerMovement"/>, and exposes the one thing outside systems (the camera) are
    /// allowed to read — current movement direction, via <see cref="IMovementDirectionSource"/>.
    /// Does not implement movement/visual/attack logic itself.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour, IMovementDirectionSource
    {
        [SerializeField]
        private PlayerDefinition _definition;

        [SerializeField]
        private PlayerMovement _movement;

        [SerializeField]
        private LevelBounds _levelBounds;

        [SerializeField]
        [Tooltip("Must implement IPlayerInput.")]
        private MonoBehaviour _inputSource;

        public Vector2 MovementDirection => _movement != null ? _movement.CurrentMoveInput : Vector2.zero;

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

            _movement.Initialize(_definition, playerInput, _levelBounds);
        }

        /// <summary>Called by the level composition root in reaction to GameState changes.</summary>
        public void SetMovementEnabled(bool value)
        {
            if (_movement != null)
            {
                _movement.SetMovementEnabled(value);
            }
        }
    }
}
