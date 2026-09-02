using UnityEngine;

namespace AlienDefense.UI.Defense
{
    public sealed class DefenseScreenView : MonoBehaviour
    {
        [SerializeField]
        private Transform _cardContainer;

        public Transform CardContainer => _cardContainer;
    }
}
