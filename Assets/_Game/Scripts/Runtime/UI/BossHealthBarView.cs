using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Screen-space top health bar shown only while a Boss is alive.
    /// The red fill snaps to the new health; the white layer behind it trails down via DOTween so the chunk just
    /// lost stays readable, and the bar gives a small punch on each hit.</summary>
    public sealed class BossHealthBarView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _visualRoot;

        [SerializeField]
        private Image _fillImage;

        [SerializeField]
        [Tooltip("White layer under the red fill that catches up after a short delay.")]
        private Image _ghostFillImage;

        [SerializeField]
        private float _ghostDelay = 0.2f;

        [SerializeField]
        private float _ghostDuration = 0.45f;

        [SerializeField]
        [Tooltip("Optional - scaled briefly on each hit.")]
        private RectTransform _punchTarget;

        [SerializeField]
        private float _punchStrength = 0.04f;

        [SerializeField]
        private float _punchDuration = 0.18f;

        private Tween _ghostTween;
        private Tween _punchTween;

        public void Show()
        {
            SetVisible(true);
        }

        public void Hide()
        {
            KillTweens();
            SetVisible(false);
        }

        public void SetHealth(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            float previous = _fillImage != null ? _fillImage.fillAmount : ratio;

            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;
            }

            if (ratio < previous - 0.0001f)
            {
                Punch();
            }

            if (_ghostFillImage == null)
            {
                return;
            }

            _ghostTween?.Kill();
            float ghost = _ghostFillImage.fillAmount;
            if (ratio >= ghost - 0.0001f)
            {
                // Healing or a fresh boss: nothing to trail.
                _ghostFillImage.fillAmount = ratio;
                return;
            }

            // Restarting from the ghost's current value keeps rapid hits smooth: it pauses, then drains once.
            _ghostTween = DOTween.To(() => _ghostFillImage.fillAmount, value => _ghostFillImage.fillAmount = value,
                    ratio, _ghostDuration)
                .SetDelay(_ghostDelay)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void Punch()
        {
            if (_punchTarget == null || _punchStrength <= 0f)
            {
                return;
            }

            _punchTween?.Kill();
            _punchTarget.localScale = Vector3.one;
            _punchTween = _punchTarget
                .DOPunchScale(new Vector3(_punchStrength, _punchStrength * 1.5f, 0f), _punchDuration, 6, 0.5f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void KillTweens()
        {
            _ghostTween?.Kill();
            _ghostTween = null;
            _punchTween?.Kill();
            _punchTween = null;
            if (_punchTarget != null)
            {
                _punchTarget.localScale = Vector3.one;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            _ghostTween?.Kill();
            _punchTween?.Kill();
        }
    }
}
