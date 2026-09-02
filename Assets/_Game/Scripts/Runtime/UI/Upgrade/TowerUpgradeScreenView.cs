using UnityEngine;

namespace AlienDefense.UI.Upgrade
{
    public sealed class TowerUpgradeScreenView : MonoBehaviour
    {
        [SerializeField]
        private Transform _cardContainer;

        public Transform CardContainer => _cardContainer;
    }
}
