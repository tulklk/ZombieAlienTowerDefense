using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Stretches a dim/blocker/background to the whole screen even when it lives inside a SafeArea, so
    /// popups also cover the notch and rounded-corner strips instead of leaving the game visible there. Layout of
    /// siblings is unaffected; only this RectTransform bleeds past the safe area.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class FullScreenBackdrop : MonoBehaviour
    {
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rectTransform;
        private RectTransform _canvasRect;
        private Rect _lastParentRect;
        private Vector2 _lastCanvasSize;
        private Vector3 _lastParentPosition;

        private void OnEnable()
        {
            Apply();
        }

        private void CacheReferences()
        {
            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            if (_canvasRect == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                _canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            }
        }

        // LateUpdate so it runs after SafeAreaFitter.Update has moved the parent for this frame.
        private void LateUpdate()
        {
            CacheReferences();
            var parent = _rectTransform.parent as RectTransform;
            if (_canvasRect == null || parent == null)
            {
                return;
            }

            if (parent.rect != _lastParentRect || _canvasRect.rect.size != _lastCanvasSize || parent.position != _lastParentPosition)
            {
                Apply();
            }
        }

        private void Apply()
        {
            CacheReferences();
            var parent = _rectTransform.parent as RectTransform;
            if (_canvasRect == null || parent == null)
            {
                return;
            }

            _canvasRect.GetWorldCorners(_corners);
            Vector2 min = parent.InverseTransformPoint(_corners[0]);
            Vector2 max = parent.InverseTransformPoint(_corners[2]);
            Rect parentRect = parent.rect;

            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = min - parentRect.min;
            _rectTransform.offsetMax = max - parentRect.max;

            _lastParentRect = parentRect;
            _lastCanvasSize = _canvasRect.rect.size;
            _lastParentPosition = parent.position;
        }
    }
}
