using UnityEngine;
using UnityEngine.EventSystems;

namespace AlienDefense.UI.Shop
{
    /// <summary>Keeps the Shop's cards at their designed width on every phone. The canvas matches height, so a tall
    /// 20:9 / 21:9 screen is narrower than the 1080 design: the content column is then scaled down uniformly (never
    /// squashed); a wide 16:9 screen just centres it. Runs only when the viewport's size changes.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ShopContentWidthFitter : UIBehaviour
    {
        [SerializeField]
        private RectTransform _content;

        [SerializeField, Min(100f)]
        private float _designWidth = 1080f;

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void Apply()
        {
            if (_content == null)
            {
                return;
            }

            float width = ((RectTransform)transform).rect.width;
            if (width <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(1f, width / _designWidth);
            _content.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
