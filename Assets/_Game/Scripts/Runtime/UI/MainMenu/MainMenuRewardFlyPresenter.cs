using System.Collections;
using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Progression;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>The reward shower on MainMenu: after a level is won, a few icons burst out over the level preview
    /// and curve into the TopHUD widget that owns each resource, which then counts up to the value it already has.
    ///
    /// Strictly presentation. The amounts were granted and saved by LevelCompositionRoot before the victory panel
    /// appeared; this reads them from <see cref="PendingRewardPresentation"/>, which empties on read - so coming
    /// back to MainMenu, or reloading it, never replays the shower and can never add a coin. If this component is
    /// missing or half-wired, nothing here runs and the HUD simply shows the correct totals straight away.
    ///
    /// Icons are pooled and the whole run is a single coroutine, so a big reward list costs a fixed handful of
    /// objects and no per-frame allocation.</summary>
    public sealed class MainMenuRewardFlyPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Full-screen RectTransform the flying icons live under - put it above the HUD in the hierarchy.")]
        private RectTransform _flyLayer;

        [SerializeField]
        [Tooltip("Pooled icon prefab.")]
        private RewardFlyIconView _iconPrefab;

        [SerializeField]
        [Tooltip("Where the icons burst from - the level preview card works well. Falls back to the layer centre.")]
        private RectTransform _originAnchor;

        [SerializeField]
        [Tooltip("Icons and colours per reward type - the same asset the victory panel uses.")]
        private VictoryRewardCatalog _catalog;

        [SerializeField]
        [Tooltip("TopHUD, so each reward knows which widget to fly into and which number to count up.")]
        private MainMenuResourcePresenter _resourcePresenter;

        [Header("Amounts")]
        [SerializeField, Range(1, 12)]
        [Tooltip("Icons for a small reward. A 5,800-coin reward is still only a handful of icons.")]
        private int _minIconsPerReward = 3;

        [SerializeField, Range(1, 16)]
        private int _maxIconsPerReward = 6;

        [SerializeField, Min(1)]
        [Tooltip("Amount at which a reward uses the maximum icon count.")]
        private int _amountForMaxIcons = 1000;

        [Header("Motion")]
        [SerializeField, Range(0f, 0.8f)]
        private float _startDelay = 0.35f;

        [SerializeField, Range(0f, 300f)]
        [Tooltip("How far the icons scatter from the origin, in canvas units.")]
        private float _scatterRadius = 90f;

        [SerializeField, Range(0.05f, 0.6f)]
        private float _burstDuration = 0.22f;

        [SerializeField, Range(0f, 0.5f)]
        private float _holdAfterBurst = 0.1f;

        [SerializeField, Range(0.2f, 2f)]
        private float _flyDuration = 0.8f;

        [SerializeField, Range(0.01f, 0.4f)]
        private float _itemStagger = 0.08f;

        [SerializeField, Range(0f, 400f)]
        [Tooltip("Sideways bulge of the curve on the way to the HUD.")]
        private float _curveOffset = 160f;

        [SerializeField, Range(0.1f, 2f)]
        private float _counterDuration = 0.6f;

        private readonly List<VictoryReward> _pending = new List<VictoryReward>(4);
        private readonly Stack<RewardFlyIconView> _pool = new Stack<RewardFlyIconView>();
        private readonly List<RewardFlyIconView> _active = new List<RewardFlyIconView>();
        private ApplicationServices _services;
        private Coroutine _routine;

        /// <summary>Called by MainMenuPresenter once the scene has its services. Starts nothing when the level did
        /// not queue anything (a cold boot into MainMenu, or a second visit).</summary>
        public void Initialize(ApplicationServices services, MainMenuResourcePresenter resourcePresenter)
        {
            _services = services;
            if (resourcePresenter != null)
            {
                _resourcePresenter = resourcePresenter;
            }

            if (_services?.PendingRewards == null || _flyLayer == null || _iconPrefab == null)
            {
                return;
            }

            if (!_services.PendingRewards.TryConsume(_pending) || _pending.Count == 0)
            {
                return;
            }

            _routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // Start from the totals as they were before this level, so the counters visibly climb to what the
            // profile already holds - the value itself is never changed here.
            PrimeCountersToPreRewardValues();

            yield return new WaitForSeconds(_startDelay);

            for (int i = 0; i < _pending.Count; i++)
            {
                VictoryReward reward = _pending[i];
                int iconCount = ResolveIconCount(reward.Amount);
                RectTransform target = _resourcePresenter != null
                    ? _resourcePresenter.GetFlyTarget(reward.Type)
                    : null;

                for (int icon = 0; icon < iconCount; icon++)
                {
                    bool isLast = icon == iconCount - 1;
                    StartCoroutine(FlyOne(reward, target, isLast));
                    yield return new WaitForSeconds(_itemStagger);
                }
            }

            _pending.Clear();
            _routine = null;
        }

        /// <summary>Winds each affected widget back by the amount that was granted, so the count-up starts from the
        /// player's old total. Nothing is spent or re-added: only the label is touched.</summary>
        private void PrimeCountersToPreRewardValues()
        {
            if (_resourcePresenter == null)
            {
                return;
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                _resourcePresenter.PrimeForIncoming(_pending[i].Type, _pending[i].Amount);
            }
        }

        private IEnumerator FlyOne(VictoryReward reward, RectTransform target, bool isLastOfReward)
        {
            RewardFlyIconView icon = Rent(reward);
            RectTransform rect = icon.RectTransform;

            Vector2 origin = LocalPointOf(_originAnchor != null ? _originAnchor : _flyLayer);
            Vector2 scatter = origin + Random.insideUnitCircle * _scatterRadius;

            // Stage 1: pop outwards from the preview.
            rect.anchoredPosition = origin;
            rect.localScale = Vector3.zero;
            icon.SetAlpha(1f);

            float elapsed = 0f;
            while (elapsed < _burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _burstDuration);
                rect.anchoredPosition = Vector2.Lerp(origin, scatter, EaseOutCubic(t));
                rect.localScale = Vector3.one * Mathf.Lerp(0f, 1f, EaseOutBack(t));
                yield return null;
            }

            // Stage 2: a beat where the reward is just readable.
            yield return new WaitForSeconds(_holdAfterBurst);

            // Stage 3: curve into the HUD. The target is read every frame, so a HUD that is still laying out (or a
            // safe-area shift on a notched phone) never leaves an icon stranded.
            Vector2 start = rect.anchoredPosition;
            Vector2 control1 = start + (Vector2)(Random.insideUnitCircle * _curveOffset * 0.5f);
            Vector2 controlBias = new Vector2(Random.Range(-_curveOffset, _curveOffset), _curveOffset * 0.6f);

            elapsed = 0f;
            while (elapsed < _flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _flyDuration);
                Vector2 end = target != null ? LocalPointOf(target) : start;
                Vector2 control2 = end + controlBias;
                rect.anchoredPosition = CubicBezier(start, control1, control2, end, EaseInOutCubic(t));
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
                icon.SetAlpha(Mathf.Lerp(1f, 0.85f, t));
                yield return null;
            }

            Release(icon);

            if (isLastOfReward)
            {
                _resourcePresenter?.PlayArrival(reward.Type, _counterDuration);
            }
            else
            {
                _resourcePresenter?.PulseOnly(reward.Type);
            }
        }

        private int ResolveIconCount(int amount)
        {
            int min = Mathf.Min(_minIconsPerReward, _maxIconsPerReward);
            int max = Mathf.Max(_minIconsPerReward, _maxIconsPerReward);
            float weight = Mathf.Clamp01(amount / (float)Mathf.Max(1, _amountForMaxIcons));
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, weight)), 1, max);
        }

        private RewardFlyIconView Rent(VictoryReward reward)
        {
            RewardFlyIconView icon = _pool.Count > 0 ? _pool.Pop() : Instantiate(_iconPrefab, _flyLayer);
            icon.RectTransform.SetParent(_flyLayer, false);
            icon.RectTransform.SetAsLastSibling();
            icon.gameObject.SetActive(true);
            _active.Add(icon);

            Sprite sprite = null;
            if (_catalog != null)
            {
                // TryGet always fills entry - it returns false only to say the type had no authored row yet.
                _catalog.TryGet(reward.Type, out VictoryRewardCatalog.Entry entry);
                sprite = entry.Icon;
            }

            icon.SetIcon(sprite, Color.white);
            return icon;
        }

        private void Release(RewardFlyIconView icon)
        {
            _active.Remove(icon);
            icon.gameObject.SetActive(false);
            _pool.Push(icon);
        }

        /// <summary>A world-space point of one RectTransform expressed in the fly layer's local space, so origins
        /// and targets can live anywhere in the canvas hierarchy.</summary>
        private Vector2 LocalPointOf(RectTransform target)
        {
            if (target == null || _flyLayer == null)
            {
                return Vector2.zero;
            }

            Vector3 world = target.TransformPoint(target.rect.center);
            Vector3 local = _flyLayer.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * p0
                + 3f * inverse * inverse * t * p1
                + 3f * inverse * t * t * p2
                + t * t * t * p3;
        }

        private static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float inverse = t - 1f;
            return 1f + (overshoot + 1f) * inverse * inverse * inverse + overshoot * inverse * inverse;
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] != null)
                {
                    _active[i].gameObject.SetActive(false);
                    _pool.Push(_active[i]);
                }
            }

            _active.Clear();
        }
    }
}
