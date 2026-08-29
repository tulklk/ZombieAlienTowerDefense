using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Cosmetic idle "floating" animation for a UI icon — sine-wave vertical bob plus a gentle side-to-
    /// side rock, e.g. the loading screen's UFO progress handle. Only ever touches this RectTransform's Y (never
    /// X) and its own local rotation, so it composes safely with anything else driving X every frame (like
    /// LoadingOverlayView.SetProgress sliding the handle along the bar) — each script reads the current
    /// anchoredPosition and writes back only its own axis, so neither fights the other regardless of update order.
    /// Uses unscaled time so it keeps floating even if Time.timeScale is ever 0 during a loading screen.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIHoverBobAnimation : MonoBehaviour
    {
        [Header("Vertical Bob")]
        [SerializeField, Min(0f)]
        private float _bobAmplitude = 10f;

        [SerializeField, Min(0f)]
        private float _bobFrequency = 1f;

        [Header("Rock (gentle Z rotation, optional)")]
        [SerializeField, Range(0f, 20f)]
        private float _rockDegrees = 6f;

        [SerializeField, Min(0f)]
        private float _rockFrequency = 0.6f;

        private RectTransform _rectTransform;
        private float _baseAnchoredY;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _baseAnchoredY = _rectTransform.anchoredPosition.y;
        }

        private void Update()
        {
            float bob = Mathf.Sin(Time.unscaledTime * _bobFrequency * Mathf.PI * 2f) * _bobAmplitude;
            Vector2 anchoredPosition = _rectTransform.anchoredPosition;
            anchoredPosition.y = _baseAnchoredY + bob;
            _rectTransform.anchoredPosition = anchoredPosition;

            if (_rockDegrees > 0f)
            {
                float rock = Mathf.Sin(Time.unscaledTime * _rockFrequency * Mathf.PI * 2f) * _rockDegrees;
                _rectTransform.localRotation = Quaternion.Euler(0f, 0f, rock);
            }
        }
    }
}
