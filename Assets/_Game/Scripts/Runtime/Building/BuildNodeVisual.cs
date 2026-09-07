using UnityEngine;

namespace AlienDefense.Building
{
    /// <summary>Toggles a BuildNode's child indicator objects to reflect its state. No business logic.</summary>
    public sealed class BuildNodeVisual : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional.")]
        private GameObject _availableIndicator;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameObject _occupiedIndicator;

        [SerializeField]
        [Tooltip("Optional.")]
        private GameObject _disabledIndicator;

        [SerializeField]
        [Tooltip("Optional. Highlight shown while this Available node has a tower ghost previewed on it.")]
        private GameObject _selectedIndicator;

        private BuildNodeState _state = BuildNodeState.Available;
        private bool _isHighlighted;

        public void SetState(BuildNodeState state)
        {
            _state = state;
            Refresh();
        }

        public void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;
            Refresh();
        }

        /// <summary>Exactly one ring is ever active at a time. _availableIndicator and _selectedIndicator both
        /// only make sense while Available, so highlighted (a tower ghost is previewed here) takes over from the
        /// plain "you can build here" ring instead of drawing both at once — two transparent rings stacked at the
        /// same spot have no stable draw order and visibly fight each other.</summary>
        private void Refresh()
        {
            bool isAvailable = _state == BuildNodeState.Available;
            SetActiveIfAssigned(_selectedIndicator, isAvailable && _isHighlighted);
            SetActiveIfAssigned(_availableIndicator, isAvailable && !_isHighlighted);
            SetActiveIfAssigned(_occupiedIndicator, _state == BuildNodeState.Occupied);
            SetActiveIfAssigned(_disabledIndicator, _state == BuildNodeState.Disabled);
        }

        private static void SetActiveIfAssigned(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
