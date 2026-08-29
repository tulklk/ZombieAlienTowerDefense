using AlienDefense.Enemies;
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

        private void Awake()
        {
            if (_health == null)
            {
                Debug.LogError("[EnemyHealthBarView] No EnemyHealth assigned.", this);
                enabled = false;
                return;
            }

            _health.HealthChanged += HandleHealthChanged;
            SetVisible(false);
        }

        public void Initialize(Transform cameraTransform)
        {
            if (_billboard != null)
            {
                _billboard.Initialize(cameraTransform);
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
            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;
            }

            SetVisible(current > 0f && current < max);
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
