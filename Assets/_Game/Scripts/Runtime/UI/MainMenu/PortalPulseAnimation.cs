using DG.Tweening;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Very slow scale pulse for the portal ring behind the level diorama preview — cheap placeholder
    /// VFX (see section 24/60 of the MainMenu spec: "very slow pulse/rotation... not too bright", "avoid heavy
    /// shader transitions"). Only ever touches its own localScale. A looping eased DOTween yoyo rather than a
    /// per-frame sine, on unscaled time so it keeps pulsing even if Time.timeScale is 0.</summary>
    public sealed class PortalPulseAnimation : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.2f)]
        private float _amplitude = 0.06f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Full in-out cycles per second.")]
        private float _frequency = 0.25f;

        private Vector3 _baseScale;
        private Tweener _pulseTween;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _pulseTween?.Kill();

            if (_amplitude <= 0f)
            {
                return;
            }

            // One yoyo loop is half a cycle, hence the /2 on the period.
            float halfCycle = 1f / (_frequency * 2f);
            transform.localScale = _baseScale * (1f - _amplitude);
            _pulseTween = transform.DOScale(_baseScale * (1f + _amplitude), halfCycle)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
            transform.localScale = _baseScale;
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }
    }
}
