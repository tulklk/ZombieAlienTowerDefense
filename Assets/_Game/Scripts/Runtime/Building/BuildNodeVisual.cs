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
        [Tooltip("Optional. Transient highlight shown while a tower type is selected for build.")]
        private GameObject _selectedIndicator;

        public void SetState(BuildNodeState state)
        {
            SetActiveIfAssigned(_availableIndicator, state == BuildNodeState.Available);
            SetActiveIfAssigned(_occupiedIndicator, state == BuildNodeState.Occupied);
            SetActiveIfAssigned(_disabledIndicator, state == BuildNodeState.Disabled);
        }

        public void SetHighlighted(bool highlighted)
        {
            SetActiveIfAssigned(_selectedIndicator, highlighted);
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
