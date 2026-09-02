using UnityEngine;

namespace AlienDefense.UI.Shop
{
    /// <summary>Shop tab view: exchange pack container only (HUD/back live on the menu shell).</summary>
    public sealed class ShopScreenView : MonoBehaviour
    {
        [SerializeField]
        private Transform _exchangeCardContainer;

        public Transform ExchangeCardContainer => _exchangeCardContainer;
    }
}
