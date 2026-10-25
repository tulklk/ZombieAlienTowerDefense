using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AlienDefense.Base
{
    /// <summary>Drag-to-pan camera for the base, clamped to the map, with pinch / mouse-wheel zoom that slides the
    /// camera along its own line of sight (user, 2026-10-25: up to ~3x closer, to see the residents).
    ///
    /// Pans the RIG, not the camera: the camera keeps its authored isometric tilt, and panning is a pure
    /// translation on the ground plane. Rotating or moving the camera itself would make the clamp depend on the
    /// tilt, which is exactly the kind of thing that breaks the first time someone adjusts the angle.
    ///
    /// Movement is computed from where the finger STARTED on the ground, not from a per-frame delta multiplied by
    /// a speed constant. That is what makes the ground stay stuck to the finger at any zoom level and any screen
    /// DPI, with no tuning and no drift.
    ///
    /// Reads the new Input System directly rather than through the project's IPlayerInput, which is the UFO's
    /// movement abstraction and has nothing to say about a menu camera.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BaseCameraController : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField]
        [Tooltip("The transform that actually moves. Leave empty to use this object's parent, or this object " +
            "itself when it has no parent.")]
        private Transform _rig;

        [Header("Bounds (world XZ)")]
        [SerializeField]
        private float _minX = -40f;

        [SerializeField]
        private float _maxX = 40f;

        [SerializeField]
        private float _minZ = -40f;

        [SerializeField]
        private float _maxZ = 40f;

        [Header("Zoom")]
        [SerializeField]
        [Tooltip("Two-finger pinch on a phone, mouse wheel in the Editor / on desktop.")]
        private bool _allowPinchZoom = true;

        [SerializeField, Range(0.15f, 1f)]
        [Tooltip("Closest zoom, as a share of the authored camera distance (0.35 = the camera goes ~3x closer).")]
        private float _closestZoom = 0.35f;

        [SerializeField, Range(1f, 2f)]
        [Tooltip("Farthest zoom, as a share of the authored camera distance.")]
        private float _farthestZoom = 1.1f;

        [SerializeField, Range(0.02f, 0.4f)]
        [Tooltip("Zoom change per mouse-wheel notch.")]
        private float _wheelStep = 0.12f;

        [SerializeField, Range(0f, 30f)]
        [Tooltip("How fast the camera catches up with the zoom. 0 = instant.")]
        private float _zoomSharpness = 14f;

        [Header("Feel")]
        [SerializeField, Range(0f, 30f)]
        [Tooltip("How fast the rig catches up to the drag target. 0 = rigid (no smoothing).")]
        private float _followSharpness = 18f;

        [SerializeField, Min(0f)]
        [Tooltip("Screen pixels the finger must travel before a press counts as a drag rather than a tap. Below " +
            "this, the press is left alone so a building tap still registers.")]
        private float _dragThresholdPixels = 12f;

        private Camera _camera;
        private Plane _groundPlane;

        private bool _dragging;
        private bool _pressActive;
        private Vector2 _pressScreenPosition;

        /// <summary>The camera's authored offset from the rig, and the ground point (rig space) it looks at. Zooming
        /// slides the camera along that line - same angle, same centre of the screen - instead of narrowing the
        /// field of view, so close up still looks like the authored isometric view.</summary>
        private Vector3 _cameraBaseLocal;
        private Vector3 _focusLocal;
        private bool _zoomReady;
        private float _zoom = 1f;
        private float _zoomTarget = 1f;
        private bool _pinching;

        /// <summary>Where on the ground the finger grabbed. The rig moves so that this point stays under it.</summary>
        private Vector3 _grabWorldPoint;
        private Vector3 _targetPosition;

        /// <summary>True while the player is actually dragging the map, so other systems can ignore the tap that
        /// ends the drag.</summary>
        public bool IsDragging => _dragging;

        /// <summary>A press that started on the world (not on UI) and was released without becoming a drag. The
        /// world presenter raycasts this into a building.</summary>
        public event System.Action<Vector2> Tapped;

        public Camera Camera => _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (_rig == null)
            {
                _rig = transform.parent != null ? transform.parent : transform;
            }

            _groundPlane = new Plane(Vector3.up, Vector3.zero);
            _targetPosition = _rig.position;
            CacheZoomFrame();
        }

        private void OnEnable()
        {
            // Re-entering the tab must not resume a drag that was in progress when it was left.
            _dragging = false;
            _pressActive = false;
            _targetPosition = _rig != null ? _rig.position : Vector3.zero;
            _pinching = false;
        }

        private void Update()
        {
            if (_rig == null || _camera == null)
            {
                return;
            }

            if (_allowPinchZoom)
            {
                HandlePinch();
                HandleWheel();
            }

            if (!_pinching)
            {
                HandlePointer();
            }

            ApplyMovement();
            ApplyZoom();
        }

        private void HandlePointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            Vector2 screenPosition = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                // A press that lands on UI belongs to the UI. Bailing out here is what stops a drag on the
                // building popup or the bottom navigation from sliding the map underneath it.
                if (IsPointerOverUI())
                {
                    _pressActive = false;
                    return;
                }

                _pressActive = true;
                _dragging = false;
                _pressScreenPosition = screenPosition;
                return;
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                // A press that began on the world and never became a drag is a tap on the world. Raised here,
                // not on press, so starting a pan on top of a building does not also open its popup.
                bool wasTap = _pressActive && !_dragging;
                _pressActive = false;
                _dragging = false;

                if (wasTap)
                {
                    Tapped?.Invoke(screenPosition);
                }

                return;
            }

            if (!_pressActive || !pointer.press.isPressed)
            {
                return;
            }

            if (!_dragging)
            {
                // Wait for real movement so a tap on a building is not eaten as a one-pixel drag.
                if (Vector2.Distance(screenPosition, _pressScreenPosition) < _dragThresholdPixels)
                {
                    return;
                }

                if (!TryGetGroundPointFrom(_targetPosition, screenPosition, out _grabWorldPoint))
                {
                    return;
                }

                _dragging = true;
                return;
            }

            // The ray is cast from where the camera WILL be (the target), not where the smoothed rig currently is.
            // Casting from the lagging rig and adding the difference every frame would keep re-adding the lag and
            // send the map sliding away; from the target, the grabbed point lands exactly under the finger after
            // one step and the difference is zero from then on.
            if (TryGetGroundPointFrom(_targetPosition, screenPosition, out Vector3 current))
            {
                _targetPosition = ClampToBounds(_targetPosition + (_grabWorldPoint - current));
            }
        }

        /// <summary>Two fingers: the camera distance follows the finger spread (spread apart = closer). While two
        /// fingers are down the one-finger drag is suspended and the press is dropped, so lifting them never pans
        /// the map or registers as a tap on a building.</summary>
        private void HandlePinch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null || touchscreen.touches.Count < 2)
            {
                _pinching = false;
                return;
            }

            TouchControl a = touchscreen.touches[0];
            TouchControl b = touchscreen.touches[1];
            if (!a.press.isPressed || !b.press.isPressed)
            {
                _pinching = false;
                return;
            }

            if (!_pinching)
            {
                _pinching = true;
                _dragging = false;
                _pressActive = false;
            }

            Vector2 currentA = a.position.ReadValue();
            Vector2 currentB = b.position.ReadValue();
            Vector2 previousA = currentA - a.delta.ReadValue();
            Vector2 previousB = currentB - b.delta.ReadValue();

            float previousDistance = Vector2.Distance(previousA, previousB);
            float currentDistance = Vector2.Distance(currentA, currentB);
            if (previousDistance > 1f && currentDistance > 1f)
            {
                SetZoomTarget(_zoomTarget * previousDistance / currentDistance);
            }
        }

        /// <summary>Mouse wheel (Editor, desktop): one notch = Wheel Step closer or farther. Ignored over UI.</summary>
        private void HandleWheel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f || IsPointerOverUI())
            {
                return;
            }

            SetZoomTarget(_zoomTarget * (scroll > 0f ? 1f - _wheelStep : 1f + _wheelStep));
        }

        private void SetZoomTarget(float zoom)
        {
            _zoomTarget = Mathf.Clamp(zoom, _closestZoom, Mathf.Max(_closestZoom, _farthestZoom));
        }

        /// <summary>The camera's authored offset and the ground point it looks at, in rig space.</summary>
        private void CacheZoomFrame()
        {
            if (transform == _rig)
            {
                return; // no separate rig: nothing to slide the camera along
            }

            _cameraBaseLocal = _rig.InverseTransformPoint(transform.position);
            var ray = new Ray(transform.position, transform.forward);
            _focusLocal = _groundPlane.Raycast(ray, out float distance)
                ? _rig.InverseTransformPoint(ray.GetPoint(distance))
                : _cameraBaseLocal + _rig.InverseTransformDirection(transform.forward) * 50f;
            _zoomReady = true;
        }

        private void ApplyZoom()
        {
            if (!_zoomReady || Mathf.Approximately(_zoom, _zoomTarget))
            {
                return;
            }

            _zoom = _zoomSharpness <= 0f
                ? _zoomTarget
                : Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-_zoomSharpness * Time.unscaledDeltaTime));
            if (Mathf.Abs(_zoom - _zoomTarget) < 0.0005f)
            {
                _zoom = _zoomTarget;
            }

            transform.position = _rig.TransformPoint(_focusLocal + (_cameraBaseLocal - _focusLocal) * _zoom);
        }

        private void ApplyMovement()
        {
            if (_followSharpness <= 0f)
            {
                _rig.position = _targetPosition;
                return;
            }

            // Frame-rate independent smoothing: the same feel at 30 and at 60 fps.
            float t = 1f - Mathf.Exp(-_followSharpness * Time.unscaledDeltaTime);
            _rig.position = Vector3.Lerp(_rig.position, _targetPosition, t);
        }

        /// <summary>Ground point under a screen position, as seen by a camera whose rig sits at rigPosition. The camera
        /// is a child of the rig and only translates with it, so the ray direction is unchanged and only its origin
        /// shifts by the rig offset.</summary>
        private bool TryGetGroundPointFrom(Vector3 rigPosition, Vector2 screenPosition, out Vector3 worldPoint)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            ray.origin += rigPosition - _rig.position;
            if (_groundPlane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            worldPoint = default;
            return false;
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, _minX, _maxX);
            position.z = Mathf.Clamp(position.z, _minZ, _maxZ);
            return position;
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            // Touch needs its own finger id; the mouse path takes the parameterless overload.
            if (Touchscreen.current != null && Pointer.current == Touchscreen.current)
            {
                return EventSystem.current.IsPointerOverGameObject(
                    Touchscreen.current.primaryTouch.touchId.ReadValue());
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>Snaps the rig to a world position, used by the popup's GO button to focus a building.</summary>
        public void FocusOn(Vector3 worldPosition, bool instant = false)
        {
            if (_rig == null)
            {
                return;
            }

            _dragging = false;
            _pressActive = false;
            _targetPosition = ClampToBounds(new Vector3(worldPosition.x, _rig.position.y, worldPosition.z));

            if (instant)
            {
                _rig.position = _targetPosition;
            }
        }

        public void SetBounds(float minX, float maxX, float minZ, float maxZ)
        {
            _minX = minX;
            _maxX = maxX;
            _minZ = minZ;
            _maxZ = maxZ;
            _targetPosition = ClampToBounds(_targetPosition);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
            var centre = new Vector3((_minX + _maxX) * 0.5f, 0f, (_minZ + _maxZ) * 0.5f);
            var size = new Vector3(Mathf.Abs(_maxX - _minX), 0.1f, Mathf.Abs(_maxZ - _minZ));
            Gizmos.DrawWireCube(centre, size);
        }
#endif
    }
}
