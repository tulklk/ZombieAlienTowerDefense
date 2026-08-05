using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Ground-plane circle showing a tower's attack range. Hidden by default; diameter = range * 2.</summary>
    public sealed class RangeIndicator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Child mesh whose XZ scale is set to range * 2. Its own local scale.y is left untouched.")]
        private Transform _visualRoot;

        private float _currentRange = -1f;

        private void Awake()
        {
            SetVisible(false);
        }

        public void Show(float range)
        {
            ApplyRange(range);
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void SetRange(float range)
        {
            ApplyRange(range);
        }

        private void ApplyRange(float range)
        {
            if (_visualRoot == null || Mathf.Approximately(range, _currentRange))
            {
                return;
            }

            _currentRange = range;
            float diameter = range * 2f;
            Vector3 scale = _visualRoot.localScale;
            _visualRoot.localScale = new Vector3(diameter, scale.y, diameter);
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null)
            {
                _visualRoot.gameObject.SetActive(visible);
            }
        }
    }
}
