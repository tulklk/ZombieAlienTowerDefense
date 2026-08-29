using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Scales a centered background image to cover its parent rect while preserving sprite aspect ratio.</summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class LoadingBackgroundCover : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Image _image;
        private RectTransform _parentRectTransform;
        private Vector2 _lastParentSize;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Apply();
        }

        private void Update()
        {
            if (_parentRectTransform == null)
            {
                return;
            }

            Vector2 parentSize = _parentRectTransform.rect.size;
            if (parentSize != _lastParentSize)
            {
                Apply();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void ResolveReferences()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            _parentRectTransform = _rectTransform != null ? _rectTransform.parent as RectTransform : null;
        }

        private void Apply()
        {
            if (_rectTransform == null || _parentRectTransform == null)
            {
                return;
            }

            Vector2 parentSize = _parentRectTransform.rect.size;
            if (parentSize.x <= 0f || parentSize.y <= 0f)
            {
                return;
            }

            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.localScale = Vector3.one;

            Sprite sprite = _image != null ? _image.sprite : null;
            if (sprite == null)
            {
                _rectTransform.anchorMin = Vector2.zero;
                _rectTransform.anchorMax = Vector2.one;
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;
                _lastParentSize = parentSize;
                return;
            }

            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float coverWidth = parentSize.y * spriteAspect;
            float coverHeight = parentSize.y;

            if (coverWidth < parentSize.x)
            {
                coverWidth = parentSize.x;
                coverHeight = parentSize.x / spriteAspect;
            }

            _rectTransform.sizeDelta = new Vector2(coverWidth, coverHeight);
            _lastParentSize = parentSize;
        }
    }
}
