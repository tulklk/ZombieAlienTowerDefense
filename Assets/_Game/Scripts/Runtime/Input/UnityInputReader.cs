using UnityEngine;
using UnityEngine.InputSystem;

namespace AlienDefense.Input
{
    /// <summary>Reads player move input from the Input System's "Player/Move" action.</summary>
    public sealed class UnityInputReader : MonoBehaviour, IPlayerInput
    {
        [SerializeField]
        private InputActionAsset _actions;

        [SerializeField]
        private string _actionMapName = "Player";

        [SerializeField]
        private string _moveActionName = "Move";

        private InputAction _moveAction;

        public Vector2 MoveInput => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        private void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError("[UnityInputReader] No InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            InputActionMap map = _actions.FindActionMap(_actionMapName, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError($"[UnityInputReader] Action map '{_actionMapName}' not found.", this);
                enabled = false;
                return;
            }

            _moveAction = map.FindAction(_moveActionName, throwIfNotFound: false);
            if (_moveAction == null)
            {
                Debug.LogError($"[UnityInputReader] Action '{_moveActionName}' not found in map '{_actionMapName}'.", this);
                enabled = false;
                return;
            }

            map.Enable();
        }

        private void OnDestroy()
        {
            _moveAction?.Disable();
        }
    }
}
