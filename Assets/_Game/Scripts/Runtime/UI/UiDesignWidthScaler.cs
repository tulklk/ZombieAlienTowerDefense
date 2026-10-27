using UnityEngine;
using UnityEngine.EventSystems;

namespace AlienDefense.UI
{
    /// <summary>Keeps a screen authored for a fixed width (1080) intact on narrower phones. Sits on a stretched rect
    /// and drives one child column: the canvas matches height, so a tall 20:9 / 21:9 screen is narrower than the
    /// design - the column then keeps the design width, is scaled down uniformly and made correspondingly taller so it
    /// still fills this rect top to bottom. Wider screens get the column at scale 1, stretched to the full width.
    /// Event-driven (this rect's size changes), no Update.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiDesignWidthScaler : UIBehaviour
    {
        [SerializeField]
        private RectTransform _column;

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

        public void Apply()
        {
            if (_column == null)
            {
                return;
            }

            Rect area = ((RectTransform)transform).rect;
            if (area.width <= 0f || area.height <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(1f, area.width / _designWidth);
            var size = new Vector2(Mathf.Max(_designWidth, area.width / scale), area.height / scale);
            _column.anchorMin = _column.anchorMax = _column.pivot = new Vector2(0.5f, 1f);
            _column.anchoredPosition = Vector2.zero;
            _column.sizeDelta = size;
            _column.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
