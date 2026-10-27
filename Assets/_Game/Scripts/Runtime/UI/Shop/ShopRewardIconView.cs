using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Shop
{
    /// <summary>One reward tile: coloured rounded tile, icon, amount in the corner, optional tag on top
    /// ("5m", "10K"). Used by every pack card and by the purchase popup.</summary>
    public sealed class ShopRewardIconView : MonoBehaviour
    {
        [SerializeField]
        private Image _background;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _amount;

        [SerializeField]
        private GameObject _tagRoot;

        [SerializeField]
        private TMP_Text _tag;

        [SerializeField]
        private TMP_Text _name;

        public void Bind(Sprite icon, string amount, string tag, Color background, string label = null)
        {
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_amount != null)
            {
                _amount.text = amount;
            }

            if (_background != null)
            {
                _background.color = background;
            }

            bool hasTag = !string.IsNullOrEmpty(tag);
            if (_tagRoot != null)
            {
                _tagRoot.SetActive(hasTag);
            }

            if (_tag != null && hasTag)
            {
                _tag.text = tag;
            }

            if (_name != null)
            {
                _name.gameObject.SetActive(!string.IsNullOrEmpty(label));
                _name.text = label;
            }
        }

        /// <summary>Pops the tile in (purchase popup), after <paramref name="delay"/> seconds, unscaled.</summary>
        public void PlayPop(float delay)
        {
            transform.DOKill();
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, 0.28f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }
    }
}
