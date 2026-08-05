using AlienDefense.Towers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AlienDefense.Building
{
    /// <summary>Turns a mouse/touch tap into a world-space raycast against BuildNodes and Towers, forwarding to
    /// BuildService or TowerSelectionService accordingly. A tap that hits neither clears the tower selection.</summary>
    public sealed class WorldSelectionController : MonoBehaviour
    {
        [SerializeField]
        private Camera _worldCamera;

        [SerializeField]
        [Tooltip("Should include both the BuildNode and Tower layers.")]
        private LayerMask _interactableLayerMask;

        private BuildService _buildService;
        private TowerSelectionService _towerSelectionService;
        private bool _isInputEnabled = true;

        public void Initialize(BuildService buildService, TowerSelectionService towerSelectionService)
        {
            _buildService = buildService;
            _towerSelectionService = towerSelectionService;
        }

        public void SetInputEnabled(bool value)
        {
            _isInputEnabled = value;
        }

        /// <summary>Core tap-handling logic, exposed directly for tests so real Input System hardware isn't required.</summary>
        public bool HandlePointerDown(Vector2 screenPosition, bool isPointerOverUI)
        {
            if (!_isInputEnabled || _worldCamera == null || isPointerOverUI)
            {
                return false;
            }

            Ray ray = _worldCamera.ScreenPointToRay(screenPosition);
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, float.PositiveInfinity, _interactableLayerMask);

            BuildNode node = hitSomething ? hit.collider.GetComponentInParent<BuildNode>() : null;
            if (node != null)
            {
                _buildService?.TryBuild(node);
                return true;
            }

            TowerController tower = hitSomething ? hit.collider.GetComponentInParent<TowerController>() : null;
            if (tower != null)
            {
                _towerSelectionService?.Select(tower);
                return true;
            }

            _towerSelectionService?.Clear();
            return false;
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            HandlePointerDown(pointer.position.ReadValue(), IsPointerOverUI());
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (Touchscreen.current != null && Pointer.current == Touchscreen.current)
            {
                TouchControl touch = Touchscreen.current.primaryTouch;
                return EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue());
            }

            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
