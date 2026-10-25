using AlienDefense.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>World-space health bar bound to a fixed sibling EnemyHealth for its whole pooled lifetime.</summary>
    public sealed class EnemyHealthBarView : MonoBehaviour
    {
        [SerializeField]
        private EnemyHealth _health;

        [SerializeField]
        private Image _fillImage;

        [SerializeField]
        private GameObject _visualRoot;

        [SerializeField]
        private WorldSpaceBillboard _billboard;

        [SerializeField]
        [Tooltip("Optional skull icon shown in boss style (mini bosses). Hidden for regular enemies.")]
        private GameObject _bossIcon;

        [SerializeField]
        [Tooltip("Fill colour in boss style.")]
        private Color _bossFillColor = new Color(0.9f, 0.16f, 0.16f, 1f);

        [SerializeField]
        [Tooltip("Optional \"x5\" label: in boss style with several health layers it shows the layers still left.")]
        private TMP_Text _layerLabel;

        private static readonly string[] LayerTexts = { "", "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };
        private int _layers = 1;
        private int _shownLayer = -1;

        private Color _normalFillColor = Color.white;
        private bool _normalColorCaptured;
        private bool _bossStyle;

        private void Awake()
        {
            if (_health == null)
            {
                Debug.LogError("[EnemyHealthBarView] No EnemyHealth assigned.", this);
                enabled = false;
                return;
            }

            _health.HealthChanged += HandleHealthChanged;
            if (_fillImage != null)
            {
                _normalFillColor = _fillImage.color;
                _normalColorCaptured = true;
            }

            ApplyIcon();
            SetVisible(false);
        }

        public void Initialize(Transform cameraTransform)
        {
            if (_billboard != null)
            {
                _billboard.Initialize(cameraTransform);
            }
        }

        /// <summary>Boss style (mini bosses): the bar is always shown while alive, red, with the skull icon - like
        /// the boss's own bar. Off = the regular bar that only appears once the enemy is hurt. Pooled enemies are
        /// switched back to off on every spawn.</summary>
        public void SetBossStyle(bool bossStyle)
        {
            SetBossStyle(bossStyle, 1);
        }

        /// <summary>Boss style with the health split into <paramref name="layers"/> bars (the "x5" of a mini boss):
        /// the fill empties once per layer and the label counts the layers left.</summary>
        public void SetBossStyle(bool bossStyle, int layers)
        {
            _bossStyle = bossStyle;
            _layers = bossStyle ? Mathf.Clamp(layers, 1, LayerTexts.Length - 1) : 1;
            _shownLayer = -1;
            if (_fillImage != null && _normalColorCaptured)
            {
                _fillImage.color = bossStyle ? _bossFillColor : _normalFillColor;
            }

            ApplyIcon();
            if (_health != null)
            {
                HandleHealthChanged(_health.CurrentHealth, _health.MaximumHealth);
            }
        }

        private void UpdateLayerLabel(int layersLeft)
        {
            if (_layerLabel == null)
            {
                return;
            }

            bool show = _bossStyle && _layers > 1;
            if (_layerLabel.gameObject.activeSelf != show)
            {
                _layerLabel.gameObject.SetActive(show);
            }

            if (show && layersLeft != _shownLayer)
            {
                _shownLayer = layersLeft;
                _layerLabel.text = LayerTexts[layersLeft];
            }
        }

        private void ApplyIcon()
        {
            if (_bossIcon != null)
            {
                _bossIcon.SetActive(_bossStyle);
            }
        }

        /// <summary>Force-hides the bar (e.g. while this enemy is being tractor-beam captured). The next real
        /// HealthChanged event (including the one ResetState fires on pool reuse) recomputes normal visibility.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        private void HandleHealthChanged(float current, float max)
        {
            float ratio = max > 0f ? current / max : 0f;
            float fill = ratio;
            int layersLeft = 1;
            if (_bossStyle && _layers > 1)
            {
                float scaled = ratio * _layers;
                layersLeft = Mathf.Clamp(Mathf.CeilToInt(scaled - 0.0001f), 1, _layers);
                fill = Mathf.Clamp01(scaled - (layersLeft - 1));
            }

            if (_fillImage != null)
            {
                _fillImage.fillAmount = fill;
            }

            UpdateLayerLabel(layersLeft);

            SetVisible(current > 0f && (_bossStyle || current < max));
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(visible);
            }
        }
    }
}
