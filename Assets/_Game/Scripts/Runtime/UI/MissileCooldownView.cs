using AlienDefense.Player;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>HUD badge for the Missile skill's launcher: the rocket icon with a dark radial sweep that empties
    /// clockwise while the volley cooldown runs, plus the seconds left. Hidden until the skill has been picked;
    /// gives a small punch when the launcher is ready again. Pure view - only reads PlayerMissileController.</summary>
    public sealed class MissileCooldownView : MonoBehaviour
    {
        [SerializeField]
        private PlayerMissileController _missileController;

        [SerializeField]
        [Tooltip("Everything visible. Kept separate from this component's own GameObject so Update keeps running " +
            "while the badge is hidden.")]
        private GameObject _content;

        [SerializeField]
        [Tooltip("Radial-filled overlay (Image Type = Filled, Radial 360) covering the icon during cooldown.")]
        private Image _cooldownOverlay;

        [SerializeField]
        [Tooltip("Optional. Whole seconds left, hidden when ready.")]
        private TMP_Text _secondsText;

        [SerializeField]
        [Tooltip("Optional. Scaled for the 'ready' punch.")]
        private RectTransform _punchTarget;

        private bool _wasReady = true;
        private int _shownSeconds = -1;
        private Tween _punch;

        private void Awake()
        {
            SetContentVisible(false);
        }

        private void Update()
        {
            bool unlocked = _missileController != null && _missileController.IsUnlocked;
            SetContentVisible(unlocked);
            if (!unlocked)
            {
                return;
            }

            float normalized = _missileController.CooldownNormalized;
            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = normalized;
                _cooldownOverlay.enabled = normalized > 0f;
            }

            int seconds = normalized > 0f ? Mathf.CeilToInt(_missileController.CooldownRemaining) : 0;
            if (_secondsText != null && seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _secondsText.text = seconds > 0 ? seconds.ToString() : string.Empty;
            }

            bool ready = normalized <= 0f;
            if (ready && !_wasReady)
            {
                PlayReadyPunch();
            }

            _wasReady = ready;
        }

        private void PlayReadyPunch()
        {
            if (_punchTarget == null)
            {
                return;
            }

            _punch?.Kill();
            _punchTarget.localScale = Vector3.one;
            _punch = _punchTarget.DOPunchScale(Vector3.one * 0.18f, 0.35f, 6, 0.6f).SetLink(_punchTarget.gameObject);
        }

        private void SetContentVisible(bool visible)
        {
            if (_content != null && _content.activeSelf != visible)
            {
                _content.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            _punch?.Kill();
        }
    }
}
