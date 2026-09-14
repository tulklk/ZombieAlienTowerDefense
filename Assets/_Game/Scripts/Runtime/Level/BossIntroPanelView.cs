using DG.Tweening;
using TMPro;
using UnityEngine;

namespace AlienDefense.Level
{
    /// <summary>"Dangerous boss appeared" banner shown during the boss intro cinematic: a tilted warning band with
    /// BOSS + the boss's name that sweeps in, holds, and fades out. Pure view - BossIntroController decides when.</summary>
    public sealed class BossIntroPanelView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        [Tooltip("The band that sweeps in from the side.")]
        private RectTransform _band;

        [SerializeField]
        private TMP_Text _bossNameText;

        [SerializeField]
        [Tooltip("Optional. Scrolling warning ticker rows.")]
        private RectTransform[] _tickers = new RectTransform[0];

        [SerializeField, Min(0.05f)]
        private float _appearDuration = 0.35f;

        [SerializeField, Min(0.05f)]
        private float _hideDuration = 0.25f;

        [SerializeField]
        private float _tickerSpeed = 60f;

        private Sequence _sequence;
        private Vector2 _bandRestPosition;
        private Vector2[] _tickerRestPositions;
        private bool _cached;

        // The panel object is authored inactive; it is first activated by Show, which is where its Awake runs.
        private void Awake()
        {
            CacheRest();
        }

        public void Show(string bossName)
        {
            CacheRest();
            gameObject.SetActive(true);
            if (_bossNameText != null)
            {
                _bossNameText.text = bossName;
            }

            _sequence?.Kill();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }

            if (_band != null)
            {
                _band.anchoredPosition = _bandRestPosition + new Vector2(900f, 0f);
                _band.localScale = Vector3.one * 1.08f;
            }

            _sequence = DOTween.Sequence().SetLink(gameObject);
            if (_canvasGroup != null)
            {
                _sequence.Append(Fade(1f, _appearDuration * 0.6f));
            }

            if (_band != null)
            {
                _sequence.Join(MoveBand(_bandRestPosition, _appearDuration).SetEase(Ease.OutCubic));
                _sequence.Append(_band.DOScale(1f, 0.18f).SetEase(Ease.OutQuad));
            }
        }

        public void Hide()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetLink(gameObject);
            if (_canvasGroup != null)
            {
                _sequence.Append(Fade(0f, _hideDuration));
            }

            if (_band != null)
            {
                _sequence.Join(MoveBand(_bandRestPosition - new Vector2(250f, 0f), _hideDuration).SetEase(Ease.InQuad));
            }

            _sequence.OnComplete(() => gameObject.SetActive(false));
        }

        private Tweener Fade(float to, float duration)
        {
            return DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, to, duration);
        }

        private Tweener MoveBand(Vector2 to, float duration)
        {
            return DOTween.To(() => _band.anchoredPosition, p => _band.anchoredPosition = p, to, duration);
        }

        private void Update()
        {
            // Warning ticker rows drift sideways and wrap - cheap, only while the panel is up.
            if (_tickers == null)
            {
                return;
            }

            for (int i = 0; i < _tickers.Length; i++)
            {
                RectTransform ticker = _tickers[i];
                if (ticker == null)
                {
                    continue;
                }

                float direction = i % 2 == 0 ? -1f : 1f;
                Vector2 p = ticker.anchoredPosition;
                p.x += direction * _tickerSpeed * Time.deltaTime;
                float wrap = ticker.rect.width * 0.25f;
                if (Mathf.Abs(p.x - _tickerRestPositions[i].x) > wrap)
                {
                    p.x = _tickerRestPositions[i].x;
                }

                ticker.anchoredPosition = p;
            }
        }

        private void CacheRest()
        {
            if (_cached)
            {
                return;
            }

            _cached = true;
            _bandRestPosition = _band != null ? _band.anchoredPosition : Vector2.zero;
            _tickerRestPositions = new Vector2[_tickers?.Length ?? 0];
            for (int i = 0; i < _tickerRestPositions.Length; i++)
            {
                _tickerRestPositions[i] = _tickers[i] != null ? _tickers[i].anchoredPosition : Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }
    }
}
