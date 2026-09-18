using DG.Tweening;
using TMPro;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Short-lived floating "+N XP" label, one instance spawned per XP-granting absorption (an Energy
    /// Pickup collected, or an Environment Prop/animal absorbed) - see UFOTractorBeamVisual.HandleEnergyPickupCollected
    /// / HandlePropAbsorbed, its only two callers. Rises and fades out over Duration, then destroys itself.
    ///
    /// Screen Space - Overlay by design (its own Canvas, not the world-space kind EnemyHealthBarView uses): a
    /// World Space version of this was tried first and reliably got hidden behind the tractor beam's own
    /// semi-transparent cone/particles (both draw in the same Transparent queue, and relative sort order between
    /// two transparent things is never guaranteed) - Overlay sidesteps that entirely by never compositing against
    /// 3D world geometry at all, at the cost of tracking its world position manually every frame via
    /// Camera.WorldToScreenPoint.
    ///
    /// The rise and the fade are one DOTween on a 0-1 progress value (eased, so it drifts up and thins out
    /// instead of moving linearly), but the screen position is still recomputed every frame from that value:
    /// the camera follows the UFO, so a tween straight to a fixed screen point would visibly slide sideways
    /// whenever the player moves mid-popup.</summary>
    public sealed class XpPopupView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _rectTransform;

        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        [Tooltip("Optional. Number + XP badge, scaled up on spawn.")]
        private RectTransform _content;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds from spawn to fully faded/destroyed.")]
        private float _duration = 0.9f;

        [SerializeField, Min(0f)]
        [Tooltip("World-space distance risen over Duration - reprojected to screen space every frame, not a " +
            "fixed screen-pixel drift, so it still reads as \"coming from that point in the world\".")]
        private float _riseDistance = 0.6f;

        private float _progress;
        private Vector3 _worldPosition;
        private Camera _camera;
        private Tweener _progressTween;
        private Tweener _popTween;

        /// <summary>camera may be null (e.g. Camera.main not resolved yet) - the popup then just sits at
        /// whatever screen position WorldToScreenPoint last produced (or the RectTransform's own placement) and
        /// still fades/destroys itself on schedule; it simply won't track position without a camera.</summary>
        public void Show(int amount, Vector3 worldPosition, Camera camera)
        {
            _worldPosition = worldPosition;
            _camera = camera;
            _progress = 0f;

            if (_label != null)
            {
                // "XP" is the badge next to the number, not part of the label.
                _label.text = $"+{amount}";
            }

            if (_content != null)
            {
                _content.localScale = Vector3.one * 0.6f;
                _popTween?.Kill();
                _popTween = _content.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            UpdateScreenPosition();

            _progressTween?.Kill();
            _progressTween = DOTween.To(() => _progress, p => _progress = p, 1f, _duration)
                .SetEase(Ease.OutCubic)
                .OnUpdate(ApplyProgress)
                .OnComplete(() => Destroy(gameObject));
        }

        private void LateUpdate()
        {
            // Re-projected every frame (not just on tween ticks) so the label stays pinned to its world point
            // even while the follow camera is moving.
            UpdateScreenPosition();
        }

        private void ApplyProgress()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f - _progress;
            }
        }

        private void UpdateScreenPosition()
        {
            if (_camera == null || _rectTransform == null)
            {
                return;
            }

            Vector3 worldPos = _worldPosition + Vector3.up * (_riseDistance * _progress);
            _rectTransform.position = _camera.WorldToScreenPoint(worldPos);
        }

        private void OnDestroy()
        {
            _progressTween?.Kill();
            _popTween?.Kill();
        }
    }
}
