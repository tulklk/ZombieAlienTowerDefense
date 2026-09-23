using Unity.Profiling;
using System;
using System.Collections.Generic;
using AlienDefense.Combat;
using AlienDefense.Enemies;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Floating damage numbers over enemies. Listens to EnemyHealth.DamageLanded - the one place damage is
    /// final (after defense/shield, clamped to the health that was left) - so an AoE blast shows one number per enemy
    /// it actually hurt, with that enemy's own amount, and nothing is shown for immune/dead targets or 0 damage. Only
    /// hits whose DamageInfo.PopupStyle has a Style entry show anything.
    ///
    /// Numbers live on this object's screen-space overlay canvas (sorted under the gameplay HUD/panels so pause and
    /// win screens cover them), are pooled, animate with DOTween and are projected from their world anchor each
    /// frame, so they never hide behind a zombie.</summary>
    public sealed class DamagePopupService : MonoBehaviour
    {
        [Serializable]
        public struct Style
        {
            public DamagePopupStyle Type;
            public Sprite Icon;
            public Color TopColor;
            public Color BottomColor;
            public Color OutlineColor;
            [Range(0f, 1f)] public float OutlineWidth;
            public float FontSize;
        }

        [SerializeField]
        private DamagePopupView _popupPrefab;

        [SerializeField]
        private RectTransform _container;

        [SerializeField]
        private Style[] _styles = Array.Empty<Style>();

        [Header("Motion")]
        [SerializeField, Min(0.2f)]
        private float _lifetime = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("World units the number drifts up over its lifetime.")]
        private float _riseHeight = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Random sideways offset (world units) so repeated hits on one enemy do not stack exactly.")]
        private float _horizontalJitter = 0.15f;

        [SerializeField, Min(0f)]
        private float _verticalJitter = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("Another number on the same enemy within this many seconds is lifted above the previous one.")]
        private float _stackWindow = 0.35f;

        [SerializeField, Min(0f)]
        private float _stackStep = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Height above the enemy's HealthBarAnchor (or its root when it has none).")]
        private float _anchorLift = 0.25f;

        [Header("Pool")]
        [SerializeField, Min(0)]
        private int _prewarm = 12;

        [SerializeField, Min(1)]
        private int _maxActive = 40;

        private readonly Stack<DamagePopupView> _pool = new Stack<DamagePopupView>();
        private readonly List<DamagePopupView> _active = new List<DamagePopupView>();
        private readonly Dictionary<EnemyHealth, Transform> _anchors = new Dictionary<EnemyHealth, Transform>();
        private readonly Dictionary<EnemyHealth, StackState> _stacks = new Dictionary<EnemyHealth, StackState>();
        private Action<DamagePopupView> _release;
        private Camera _camera;

        private struct StackState
        {
            public float LastTime;
            public int Count;
        }

        private void Awake()
        {
            _release = Release;
            for (int i = 0; i < _prewarm; i++)
            {
                _pool.Push(Create());
            }
        }

        private void OnEnable()
        {
            EnemyHealth.DamageLanded += HandleDamageLanded;
        }

        private void OnDisable()
        {
            EnemyHealth.DamageLanded -= HandleDamageLanded;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Stop();
                _pool.Push(_active[i]);
            }

            _active.Clear();
            _stacks.Clear();
        }

        /// <summary>Shows a number at a world point directly (e.g. for damage that does not go through EnemyHealth).</summary>
        public void Show(Vector3 worldPosition, float amount, DamagePopupStyle style)
        {
            int rounded = Mathf.RoundToInt(amount);
            if (rounded <= 0 || !TryGetStyle(style, out Style entry))
            {
                return;
            }

            Spawn(worldPosition, rounded, entry);
        }

        private void HandleDamageLanded(EnemyHealth health, DamageInfo info, float amount)
        {
            if (health == null || info.PopupStyle == DamagePopupStyle.None)
            {
                return;
            }

            int rounded = Mathf.RoundToInt(amount);
            if (rounded <= 0 || !TryGetStyle(info.PopupStyle, out Style entry))
            {
                return;
            }

            Vector3 position = AnchorOf(health).position + Vector3.up * _anchorLift;

            // Several hits on one enemy in quick succession climb instead of overlapping.
            float now = Time.time;
            _stacks.TryGetValue(health, out StackState stack);
            stack.Count = now - stack.LastTime <= _stackWindow ? Mathf.Min(stack.Count + 1, 4) : 0;
            stack.LastTime = now;
            _stacks[health] = stack;

            Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
            position += right * UnityEngine.Random.Range(-_horizontalJitter, _horizontalJitter)
                + Vector3.up * (UnityEngine.Random.Range(0f, _verticalJitter) + stack.Count * _stackStep);

            Spawn(position, rounded, entry);
        }

        private void Spawn(Vector3 worldPosition, int amount, Style style)
        {
            DamagePopupView view;
            if (_active.Count >= _maxActive)
            {
                // Recycle the oldest rather than allocate during a big AoE.
                view = _active[0];
                _active.RemoveAt(0);
                view.Stop();
            }
            else
            {
                view = _pool.Count > 0 ? _pool.Pop() : Create();
            }

            _active.Add(view);
            view.Play(worldPosition, amount, style, _riseHeight, _lifetime, _release);
            Place(view);
        }

        private static readonly ProfilerMarker LateUpdateMarker = new ProfilerMarker("AlienDefense.DamagePopups.LateUpdate");

        private void LateUpdate()
        {
            using (LateUpdateMarker.Auto())
            {
                LateUpdateCore();
            }
        }

        private void LateUpdateCore()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Place(_active[i]);
            }
        }

        private void Place(DamagePopupView view)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Vector3 world = view.WorldPosition + Vector3.up * view.Rise;
            Vector3 screen = _camera.WorldToScreenPoint(world);
            var rect = (RectTransform)view.transform;
            if (screen.z <= 0f)
            {
                rect.anchoredPosition = new Vector2(-100000f, -100000f);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, null, out Vector2 local);
            rect.anchoredPosition = local;
        }

        private void Release(DamagePopupView view)
        {
            _active.Remove(view);
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }

        private DamagePopupView Create()
        {
            DamagePopupView view = Instantiate(_popupPrefab, _container);
            view.gameObject.SetActive(false);
            return view;
        }

        private Transform AnchorOf(EnemyHealth health)
        {
            if (_anchors.TryGetValue(health, out Transform anchor) && anchor != null)
            {
                return anchor;
            }

            anchor = health.transform.Find("HealthBarAnchor");
            if (anchor == null)
            {
                anchor = health.transform;
            }

            _anchors[health] = anchor;
            return anchor;
        }

        private bool TryGetStyle(DamagePopupStyle type, out Style style)
        {
            for (int i = 0; i < _styles.Length; i++)
            {
                if (_styles[i].Type == type)
                {
                    style = _styles[i];
                    return true;
                }
            }

            // Rapid ballistic hits use a neutral number; the fire icon belongs to fire attacks.
            // A default keeps existing scenes compatible without changing their authored fire style.
            if (type == DamagePopupStyle.Kinetic)
            {
                style = new Style
                {
                    Type = type,
                    TopColor = Color.white,
                    BottomColor = new Color(0.9f, 0.93f, 0.96f, 1f),
                    OutlineColor = new Color(0.1f, 0.12f, 0.15f, 1f),
                    OutlineWidth = 0.22f,
                    FontSize = 30f,
                };
                return true;
            }

            style = default;
            return false;
        }
    }
}
