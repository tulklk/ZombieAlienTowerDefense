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
