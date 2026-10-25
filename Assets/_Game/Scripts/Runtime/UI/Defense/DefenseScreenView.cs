using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Defense
{
    /// <summary>The Defense tab's scrolling page: title, Towers Crit Damage line, the 4-column tower grid, the
    /// upgrade card inventory and the Get Cards button. Holds references only; DefenseScreenPresenter fills it.</summary>
    public sealed class DefenseScreenView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Legacy container of the old unlock list (kept so the old scaffolder still finds it).")]
        private Transform _cardContainer;

        [SerializeField] private GameObject _critPanel;
        [SerializeField] private TMP_Text _critValue;
        [SerializeField] private Button _critInfoButton;
        [SerializeField] private GameObject _critTooltip;
        [SerializeField] private TMP_Text _critTooltipText;
        [SerializeField] private Button _critTooltipDismiss;
        [SerializeField] private Transform _towerGrid;
        [SerializeField] private TowerCollectionCardView _towerCardTemplate;
        [SerializeField] private Transform _upgradeCardGrid;
        [SerializeField] private UpgradeCardView _upgradeCardTemplate;
        [SerializeField] private GameObject _noCardsLabel;
        [SerializeField] private Button _getCardsButton;

        public Transform CardContainer => _cardContainer;
        public GameObject CritPanel => _critPanel;
        public TMP_Text CritValue => _critValue;
        public Button CritInfoButton => _critInfoButton;
        public GameObject CritTooltip => _critTooltip;
        public TMP_Text CritTooltipText => _critTooltipText;
        public Button CritTooltipDismiss => _critTooltipDismiss;
        public Transform TowerGrid => _towerGrid;
        public TowerCollectionCardView TowerCardTemplate => _towerCardTemplate;
        public Transform UpgradeCardGrid => _upgradeCardGrid;
        public UpgradeCardView UpgradeCardTemplate => _upgradeCardTemplate;
        public GameObject NoCardsLabel => _noCardsLabel;
        public Button GetCardsButton => _getCardsButton;
    }
}
