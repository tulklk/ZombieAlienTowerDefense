using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AlienDefense.UI
{
    /// <summary>Squeezes a button slightly while it is held, so a tap on a big UI card feels like a press. Event
    /// driven (no Update), unscaled so it still animates while the game clock is paused, and it does nothing else -
    /// the Button's own onClick still carries the behaviour.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Range(0.8f, 1f)]
        private float _pressedScale = 0.96f;

        [SerializeField, Range(0.02f, 0.3f)]
        private float _duration = 0.1f;

        private Tween _tween;

        private void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ScaleTo(_pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ScaleTo(1f);
        }

        private void ScaleTo(float target)
        {
            _tween?.Kill();
            _tween = transform.DOScale(target, _duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
        }

        private void OnDestroy()
        {
            _tween?.Kill();
        }
    }
}
