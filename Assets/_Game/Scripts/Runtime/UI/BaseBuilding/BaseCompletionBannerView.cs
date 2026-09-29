using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>The "Force 20669 ^100 / Patrol Post Lvl 1 / Upgrade done!" celebration after a build completes.
    ///
    /// Timing follows the design: dim in, gold glow burst at 0.1s, the Force number punches 0.8 -> 1.1 -> 1 at
    /// 0.2s, the building line lands at 0.6s, everything fades out around 1.8s. Unscaled throughout, and
    /// lightweight on purpose - one glow sprite and text, no particle system.
    ///
    /// Several builds can finish at once (coming back after a long time away), so completions are queued and
    /// shown one after another instead of overwriting each other.</summary>
    public sealed class BaseCompletionBannerView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _group;

        [SerializeField]
        private Image _glow;

        [SerializeField]
        private RectTransform _forceBlock;

        [SerializeField]
        private TMP_Text _forceLabel;

        [SerializeField]
        private TMP_Text _forceValue;

        [SerializeField]
        private TMP_Text _forceDelta;

        [SerializeField]
        private CanvasGroup _messageGroup;

        [SerializeField]
        private TMP_Text _buildingText;

        [SerializeField]
        private TMP_Text _doneText;

        [SerializeField, Min(0.5f)]
        private float _holdSeconds = 1.2f;

        private readonly System.Collections.Generic.Queue<(int force, int delta, string building)> _queue =
            new System.Collections.Generic.Queue<(int, int, string)>();

        private Sequence _sequence;

        private void Awake()
        {
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
        }

        private void OnDisable()
        {
            _sequence?.Kill();
            _queue.Clear();
            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        public void Enqueue(int totalForce, int delta, string buildingLine)
        {
            _queue.Enqueue((totalForce, delta, buildingLine));
            if (_sequence == null || !_sequence.IsActive())
            {
                PlayNext();
            }
        }

        private void PlayNext()
        {
            if (_queue.Count == 0 || _group == null)
            {
                return;
            }

            (int force, int delta, string building) = _queue.Dequeue();

            if (_forceLabel != null) _forceLabel.text = "Force";
            if (_forceValue != null) _forceValue.text = force.ToString();
            if (_forceDelta != null)
            {
                _forceDelta.gameObject.SetActive(delta > 0);
                _forceDelta.text = delta > 0 ? "↑" + delta : string.Empty;
            }

            if (_buildingText != null) _buildingText.text = building;
            if (_doneText != null) _doneText.text = "Upgrade done!";

            _group.alpha = 0f;
            if (_messageGroup != null) _messageGroup.alpha = 0f;
            if (_glow != null)
            {
                _glow.transform.localScale = Vector3.one * 0.6f;
                SetAlpha(_glow, 0f);
            }

            if (_forceBlock != null) _forceBlock.localScale = Vector3.one * 0.8f;

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Append(DOTween.To(() => _group.alpha, a => _group.alpha = a, 1f, 0.12f));

            if (_glow != null)
            {
                _sequence.Insert(0.1f, _glow.transform.DOScale(1.25f, 0.5f).SetEase(Ease.OutQuad));
                _sequence.Insert(0.1f, DOTween.To(() => _glow.color.a, a => SetAlpha(_glow, a), 0.9f, 0.15f));
                _sequence.Insert(0.45f, DOTween.To(() => _glow.color.a, a => SetAlpha(_glow, a), 0.35f, 0.4f));
            }

            if (_forceBlock != null)
            {
                _sequence.Insert(0.2f, _forceBlock.DOScale(1.1f, 0.14f).SetEase(Ease.OutQuad));
                _sequence.Insert(0.34f, _forceBlock.DOScale(1f, 0.12f).SetEase(Ease.InQuad));
            }

            if (_messageGroup != null)
            {
                _sequence.Insert(0.6f, DOTween.To(() => _messageGroup.alpha, a => _messageGroup.alpha = a, 1f, 0.2f));
            }

            float fadeAt = 0.6f + _holdSeconds;
            _sequence.Insert(fadeAt, DOTween.To(() => _group.alpha, a => _group.alpha = a, 0f, 0.3f));
            _sequence.OnComplete(PlayNext);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
