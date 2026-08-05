using UnityEngine;

namespace AlienDefense.Enemies
{
    /// <summary>Briefly tints an enemy's renderer via MaterialPropertyBlock when it takes damage. Never clones the shared material.</summary>
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        private EnemyHealth _health;

        [SerializeField]
        private Renderer _renderer;

        [SerializeField]
        private Color _flashColor = Color.white;

        [SerializeField, Min(0.01f)]
        private float _flashDuration = 0.1f;

        private MaterialPropertyBlock _propertyBlock;
        private Color _baseColor;
        private float _remainingFlashTime;

        private void Awake()
        {
            if (_health == null || _renderer == null)
            {
                Debug.LogError("[EnemyHitFlash] EnemyHealth and Renderer must both be assigned.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            _baseColor = _renderer.sharedMaterial != null ? _renderer.sharedMaterial.color : Color.white;
            _health.HealthChanged += HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (current >= max)
            {
                return;
            }

            _remainingFlashTime = _flashDuration;
            ApplyColor(_flashColor);
        }

        private void Update()
        {
            if (_remainingFlashTime <= 0f)
            {
                return;
            }

            _remainingFlashTime -= Time.deltaTime;
            if (_remainingFlashTime <= 0f)
            {
                ApplyColor(_baseColor);
            }
        }

        private void ApplyColor(Color color)
        {
            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.HealthChanged -= HandleHealthChanged;
            }
        }
    }
}
