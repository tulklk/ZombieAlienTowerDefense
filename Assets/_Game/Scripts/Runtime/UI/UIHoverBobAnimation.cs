using DG.Tweening;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Cosmetic idle "floating" animation for a UI icon — vertical bob plus a gentle side-to-side rock,
    /// e.g. the loading screen's UFO progress handle. Only ever touches this RectTransform's Y (never X) and its
    /// own local rotation, so it composes safely with anything else driving X every frame (like
    /// LoadingOverlayView.SetProgress sliding the handle along the bar) — the bob tween writes back only the Y
    /// component, so neither fights the other regardless of update order.
    ///
    /// Driven by looping DOTween yoyos rather than a per-frame sine so the motion is eased (InOutSine) and costs
    /// nothing on frames where nothing else changes. Unscaled time, so it keeps floating even if Time.timeScale
    /// is 0 during a loading screen.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIHoverBobAnimation : MonoBehaviour
    {
        [Header("Vertical Bob")]
        [SerializeField, Min(0f)]
        private float _bobAmplitude = 10f;

        [SerializeField, Min(0f)]
        [Tooltip("Full up-down cycles per second.")]
        private float _bobFrequency = 1f;

        [Header("Rock (gentle Z rotation, optional)")]
        [SerializeField, Range(0f, 20f)]
        private float _rockDegrees = 6f;

        [SerializeField, Min(0f)]
        [Tooltip("Full left-right cycles per second.")]
        private float _rockFrequency = 0.6f;

        private RectTransform _rectTransform;
        private float _baseAnchoredY;
        private Tweener _bobTween;
        private Tweener _rockTween;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _baseAnchoredY = _rectTransform.anchoredPosition.y;
        }

        private void OnEnable()
        {
            StartTweens();
        }

        private void OnDisable()
        {
            KillTweens();

            // Leave the icon exactly where it started rather than frozen mid-bob.
            SetAnchoredY(_baseAnchoredY);
            _rectTransform.localRotation = Quaternion.identity;
        }

        private void StartTweens()
        {
            KillTweens();

            if (_bobAmplitude > 0f && _bobFrequency > 0f)
            {
                // One yoyo loop is half a cycle, hence the /2 on the period.
                float halfCycle = 1f / (_bobFrequency * 2f);
                SetAnchoredY(_baseAnchoredY - _bobAmplitude);
                _bobTween = DOTween.To(() => _rectTransform.anchoredPosition.y, SetAnchoredY, _baseAnchoredY + _bobAmplitude, halfCycle)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }

            if (_rockDegrees > 0f && _rockFrequency > 0f)
            {
                float halfCycle = 1f / (_rockFrequency * 2f);
                _rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_rockDegrees);
                _rockTween = _rectTransform.DOLocalRotate(new Vector3(0f, 0f, _rockDegrees), halfCycle)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        /// <summary>Writes ONLY Y back - see the class doc for why X must be left alone.</summary>
        private void SetAnchoredY(float y)
        {
            Vector2 anchoredPosition = _rectTransform.anchoredPosition;
            anchoredPosition.y = y;
            _rectTransform.anchoredPosition = anchoredPosition;
        }

        private void KillTweens()
        {
            _bobTween?.Kill();
            _rockTween?.Kill();
            _bobTween = null;
            _rockTween = null;
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
