using AlienDefense.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>World-space shield bar bound to a fixed sibling EnemyShield for its whole pooled lifetime.</summary>
    public sealed class EnemyShieldBarView : MonoBehaviour
    {
        [SerializeField]
        private EnemyShield _shield;

        [SerializeField]
        private Image _fillImage;

        [SerializeField]
        private GameObject _visualRoot;

        [SerializeField]
        private WorldSpaceBillboard _billboard;

        private void Awake()
        {
            if (_shield == null)
            {
                Debug.LogError("[EnemyShieldBarView] No EnemyShield assigned.", this);
                enabled = false;
                return;
            }

            _shield.ShieldChanged += HandleShieldChanged;
        }

        public void Initialize(Transform cameraTransform)
        {
            if (_billboard != null)
            {
                _billboard.Initialize(cameraTransform);
            }
        }

        private void HandleShieldChanged(float current, float max)
        {
            float ratio = max > 0f ? current / max : 0f;
            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;
            }

            SetVisible(current > 0f);
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
