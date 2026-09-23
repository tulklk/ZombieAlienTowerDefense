using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One flying reward icon. Deliberately dumb and allocation-free: the presenter pools these and drives
    /// the path itself, so a "5,800 coins" reward is a handful of icons being reused, never thousands of
    /// Instantiate calls.</summary>
    public sealed class RewardFlyIconView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        [Tooltip("Optional. The presenter parents this to the fly layer and moves it.")]
        private RectTransform _rectTransform;

        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = (RectTransform)transform;
                }

                return _rectTransform;
            }
        }

        public void SetIcon(Sprite sprite, Color tint)
        {
            if (_icon == null)
            {
                return;
            }

            _icon.sprite = sprite;
            _icon.color = tint;
            _icon.enabled = sprite != null;
            _icon.preserveAspect = true;
        }

        public void SetAlpha(float alpha)
        {
            if (_icon == null)
            {
                return;
            }

            Color color = _icon.color;
            color.a = alpha;
            _icon.color = color;
        }
    }
}
