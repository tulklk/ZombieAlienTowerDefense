using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Shows the current level's flat map illustration as plain UI Images inside LevelSelectionArea
    /// (map art + padlock icon while locked — no dark tint overlay, just the grayscale art itself plus the
    /// icon, per explicit user feedback that a tint on top read as a "hazy/blurry" smear). On Prev/Next, the
    /// whole PreviewGroup slides out toward the
    /// direction of travel, swaps its content while off-screen, then slides back in from the opposite side —
    /// via DOTween core (DOTween.Sequence/DOTween.To only, never the RectTransform.DOAnchorPos* UI-module
    /// shortcuts: those live in a loose, non-asmdef script invisible to this asmdef-scoped assembly, see
    /// BottomNavTabView's doc comments for the same pitfall). The very first reveal (direction == 0, no prior
    /// content on screen yet) skips the slide-out half and only plays the slide-in.
    ///
    /// This used to live in a separate 3D world (SpriteRenderers on a tilted "diorama" layer behind the UI
    /// Canvas), but that can never actually be seen: the Canvas is Screen Space Overlay, which always composites
    /// on top of every camera's render, and BackgroundOverlay is a full-screen opaque image — so anything behind
    /// the Canvas is permanently hidden regardless of camera/culling-mask setup. Plain UI Images inside the same
    /// Canvas (drawn after BackgroundOverlay in sibling order) render correctly like every other MainMenu widget.
    ///
    /// Dumb view: never reads LevelCatalog itself, only ever shown whatever sprite + unlocked state it's given.</summary>
    public sealed class LevelPreviewView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _previewGroup;

        [SerializeField]
        private Image _mapImage;

        [SerializeField]
        [Tooltip("Padlock glyph drawn centered over the map art while the level is locked.")]
        private Image _lockIconImage;

        [SerializeField, Min(0f)]
        private float _slideDistance = 520f;

        [SerializeField, Min(0f)]
        private float _slideOutDuration = 0.18f;

        [SerializeField, Min(0f)]
        private float _slideInDuration = 0.22f;

        private Vector2 _restPosition;
        private bool _restPositionCaptured;
        private bool _hasShownOnce;
        private Sequence _sequence;

        /// <summary>direction: -1 = arrived via Previous (slides in from the left), +1 = arrived via Next
        /// (slides in from the right), 0 = initial reveal (no prior content to slide out — plays only the
        /// slide-in half, treated like a Next-style entrance).</summary>
        public void ShowPreview(Sprite mapSprite, bool isUnlocked, int direction)
        {
            if (_previewGroup == null)
            {
                ApplyContent(mapSprite, isUnlocked);
                return;
            }

            if (!_restPositionCaptured)
            {
                _restPosition = _previewGroup.anchoredPosition;
                _restPositionCaptured = true;
            }

            _sequence?.Kill();

            float dir = direction < 0 ? -1f : 1f;

            if (!_hasShownOnce)
            {
                _hasShownOnce = true;
                ApplyContent(mapSprite, isUnlocked);
                _previewGroup.anchoredPosition = _restPosition + new Vector2(dir * _slideDistance, 0f);

                _sequence = DOTween.Sequence().SetUpdate(true);
                _sequence.Append(DOTween.To(
                    () => _previewGroup.anchoredPosition,
                    p => _previewGroup.anchoredPosition = p,
                    _restPosition,
                    _slideInDuration).SetEase(Ease.OutCubic));
                return;
            }

            _sequence = DOTween.Sequence().SetUpdate(true);
            _sequence.Append(DOTween.To(
                () => _previewGroup.anchoredPosition,
                p => _previewGroup.anchoredPosition = p,
                _restPosition + new Vector2(-dir * _slideDistance, 0f),
                _slideOutDuration).SetEase(Ease.InCubic));
            _sequence.AppendCallback(() =>
            {
                ApplyContent(mapSprite, isUnlocked);
                _previewGroup.anchoredPosition = _restPosition + new Vector2(dir * _slideDistance, 0f);
            });
            _sequence.Append(DOTween.To(
                () => _previewGroup.anchoredPosition,
                p => _previewGroup.anchoredPosition = p,
                _restPosition,
                _slideInDuration).SetEase(Ease.OutCubic));
        }

        private void ApplyContent(Sprite mapSprite, bool isUnlocked)
        {
            if (_mapImage != null)
            {
                _mapImage.sprite = mapSprite;
            }

            if (_lockIconImage != null)
            {
                _lockIconImage.gameObject.SetActive(!isUnlocked);
            }
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }
    }
}
