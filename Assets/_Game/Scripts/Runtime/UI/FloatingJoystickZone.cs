using AlienDefense.Building;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Makes the movement joystick float: it sits at its authored resting spot until a finger goes down
    /// somewhere in the gameplay area, jumps under that finger, and returns to rest on release.
    ///
    /// This is a transparent full-area Image rather than logic on the joystick itself, because the joystick's own
    /// rect is only 260px wide - it can only receive presses that land on it, which is exactly the "fixed in one
    /// place" behaviour being replaced. The zone is the thing that hears the press, and it forwards the press,
    /// drag and release straight to the existing OnScreenStick, so the actual stick-to-input math (and therefore
    /// the feel of movement) is unchanged.
    ///
    /// It must sit at the BOTTOM of the UI draw order. UGUI gives a press to the topmost graphic under it, so
    /// anything drawn later - buttons, popups, the pause control - still wins its own presses and never reaches
    /// this zone.
    ///
    /// Tap versus drag matters here. Because the zone is a raycast target, WorldSelectionController's own
    /// IsPointerOverUI() check now reports true across the whole area and it stops handling taps itself - without
    /// this component forwarding them back, tapping a tower or a build node would silently stop working. Worse,
    /// if both ran, every attempt to start moving would also select whatever tower was under the thumb. So the
    /// zone owns the gesture and decides at release: a finger that stayed put was a tap, and is handed to
    /// WorldSelectionController; a finger that travelled was movement, and is not.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class FloatingJoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The joystick ring that moves - the PARENT of the stick handle, so the whole control travels " +
            "together rather than the knob sliding out of its ring.")]
        private RectTransform _joystick;

        [SerializeField]
        [Tooltip("The OnScreenStick on the handle. Presses are forwarded to it verbatim; this component never " +
            "computes movement itself.")]
        private OnScreenStick _stick;

        [SerializeField]
        [Tooltip("Optional. The CanvasGroup the rest of the on-screen controls live under - while it is off, " +
            "this zone ignores presses, so cutscenes and pauses that hide the controls also disable the stick.")]
        private CanvasGroup _controlsGroup;

        [SerializeField]
        [Tooltip("Optional. Taps that were not drags are handed to this, which is what keeps tapping a tower or " +
            "a build node working now that the zone covers them.")]
        private WorldSelectionController _worldSelection;

        [Header("Behaviour")]
        [SerializeField, Min(0f)]
        [Tooltip("Keeps the ring this far inside the screen edge, so a press in a corner does not leave half the " +
            "joystick off-screen.")]
        private float _edgePadding = 12f;

        [SerializeField, Min(0f)]
        [Tooltip("How far, in screen pixels, a finger may travel and still count as a tap rather than movement. " +
            "Too low and a slightly shaky tap stops selecting towers; too high and a short nudge of the UFO " +
            "selects whatever was underneath.")]
        private float _tapThresholdPixels = 24f;

        [SerializeField]
        [Tooltip("Return the ring to its authored spot on release. Off leaves it wherever it was last used.")]
        private bool _returnToDefaultOnRelease = true;

        private Canvas _canvas;
        private RectTransform _joystickParent;
        private Vector2 _defaultPosition;
        private Vector2 _pressScreenPosition;
        private float _maxDragDistance;

        // Which finger owns the joystick right now. A second finger landing in the zone is ignored rather than
        // yanking the stick away mid-move.
        private int _activePointerId = int.MinValue;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();

            // A fully transparent full-screen quad still costs a native-resolution blend pass every frame on mobile
            // GPUs; culling it keeps the raycasts (which don't depend on rendering) and drops the draw.
            GetComponent<CanvasRenderer>().cullTransparentMesh = true;

            if (_joystick != null)
            {
                _joystickParent = _joystick.parent as RectTransform;
                _defaultPosition = _joystick.anchoredPosition;
            }
        }

        private void OnDisable()
        {
            // Torn down mid-drag (level ended, panel closed): drop the stick rather than leaving the UFO
            // travelling on the last input it was sent.
            if (_activePointerId == int.MinValue)
            {
                return;
            }

            _activePointerId = int.MinValue;
            ResetJoystickPosition();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != int.MinValue || !ControlsUsable())
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            _pressScreenPosition = eventData.position;
            _maxDragDistance = 0f;

            MoveJoystickTo(eventData);
            _stick?.OnPointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            // Tracked as a maximum, not as the final distance: a finger that swings out and comes back to where
            // it started was still movement, not a tap.
            float distance = Vector2.Distance(eventData.position, _pressScreenPosition);
            if (distance > _maxDragDistance)
            {
                _maxDragDistance = distance;
            }

            _stick?.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId)
            {
                return;
            }

            _activePointerId = int.MinValue;
            _stick?.OnPointerUp(eventData);
            ResetJoystickPosition();

            if (_maxDragDistance <= _tapThresholdPixels)
            {
                // isPointerOverUI is false on purpose: this zone IS the UI the pointer is over, and it has just
                // decided the press was a world tap. Passing true here would make the call a no-op and quietly
                // break tower selection.
                _worldSelection?.HandlePointerDown(eventData.position, false);
            }
        }

        private bool ControlsUsable()
        {
            // activeInHierarchy, not just the CanvasGroup: UFOFlightIntro hides the controls by deactivating
            // their GameObject outright, which a CanvasGroup check would sail straight past - and the zone would
            // then drive a stick that is not even on screen during the intro flight.
            if (_joystick == null || _joystickParent == null || !_joystick.gameObject.activeInHierarchy)
            {
                return false;
            }

            return _controlsGroup == null || (_controlsGroup.interactable && _controlsGroup.alpha > 0.01f);
        }

        /// <summary>Puts the ring under the finger, clamped so it stays fully on screen.</summary>
        private void MoveJoystickTo(PointerEventData eventData)
        {
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _joystickParent, eventData.position, uiCamera, out Vector2 localPoint))
            {
                return;
            }

            Rect bounds = _joystickParent.rect;
            Vector2 half = _joystick.rect.size * 0.5f;

            // Mathf.Clamp needs min <= max; on a parent narrower than the joystick it would otherwise flip and
            // snap the ring to the wrong edge, so fall back to the centre in that case.
            float minX = bounds.xMin + half.x + _edgePadding;
            float maxX = bounds.xMax - half.x - _edgePadding;
            float minY = bounds.yMin + half.y + _edgePadding;
            float maxY = bounds.yMax - half.y - _edgePadding;

            localPoint.x = minX <= maxX ? Mathf.Clamp(localPoint.x, minX, maxX) : bounds.center.x;
            localPoint.y = minY <= maxY ? Mathf.Clamp(localPoint.y, minY, maxY) : bounds.center.y;

            _joystick.anchoredPosition = localPoint - Rect.NormalizedToPoint(bounds, _joystick.anchorMin);
        }

        private void ResetJoystickPosition()
        {
            if (_returnToDefaultOnRelease && _joystick != null)
            {
                _joystick.anchoredPosition = _defaultPosition;
            }
        }
    }
}
