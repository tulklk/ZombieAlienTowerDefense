using AlienDefense.Meta;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.Inventory
{
    /// <summary>The craft screen for one equipment piece: current rarity vs next, four stat rows, the requirement
    /// slots, and the Craft button.
    ///
    /// Every number comes from the EquipmentCraftPlan the service computed - this view never decides whether a
    /// craft is affordable, it only draws the answer. That is what stops the button and the requirement slots
    /// from ever disagreeing.
    ///
    /// The success punch runs on unscaled time: the menu can be sitting at timeScale 0 behind a popup.</summary>
    public sealed class EquipmentDetailView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField]
        private TMP_Text _currentRarityText;

        [SerializeField]
        private TMP_Text _nextRarityText;

        [SerializeField]
        [Tooltip("The '>' between the two rarity labels. Hidden at max rarity.")]
        private GameObject _rarityArrow;

        [Header("Stat rows - in display order")]
        [SerializeField]
        private StatComparisonRowView _weaponPowerRow;

        [SerializeField]
        private StatComparisonRowView _abilityRow;

        [SerializeField]
        private StatComparisonRowView _flightSpeedRow;

        [SerializeField]
        private StatComparisonRowView _maxLevelRow;

        [Header("Current item")]
        [SerializeField]
        private EquipmentCardView _currentCard;

        [SerializeField]
        private RectTransform _punchTarget;

        [Header("Requirements")]
        [SerializeField]
        [Tooltip("Duplicate copies of this same piece.")]
        private CraftRequirementView _duplicateRequirement;

        [SerializeField]
        [Tooltip("Crafting currency (the profile's MetaCurrency).")]
        private CraftRequirementView _currencyRequirement;

        [SerializeField]
        private Sprite _currencyIcon;

        [Header("Craft")]
        [SerializeField]
        private Button _craftButton;

        [SerializeField]
        private TMP_Text _craftLabel;

        [SerializeField]
        private string _craftText = "Craft";

        [SerializeField]
        private string _maxText = "MAX";

        [SerializeField]
        private Button _backButton;

        [Header("Shared")]
        [SerializeField]
        private RarityPalette _rarityPalette;

        private string _equipmentId;
        private System.Action<string> _craftRequested;
        private System.Action _backRequested;
        private Tweener _punchTween;

        public string EquipmentId => _equipmentId;

        private void Awake()
        {
            if (_craftButton != null)
            {
                _craftButton.onClick.AddListener(HandleCraft);
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(HandleBack);
            }
        }

        private void OnDestroy()
        {
            _punchTween?.Kill();

            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveListener(HandleCraft);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBack);
            }
        }

        public void Configure(System.Action<string> craftRequested, System.Action backRequested)
        {
            _craftRequested = craftRequested;
            _backRequested = backRequested;
        }

        public void Bind(EquipmentEntry entry, EquipmentCraftPlan plan)
        {
            _equipmentId = entry.Definition != null ? entry.Definition.Id : null;

            if (_currentCard != null)
            {
                _currentCard.Bind(entry, _rarityPalette, null);
            }

            bool hasNext = !plan.IsMaxRarity && plan.Next != null;

            if (_currentRarityText != null)
            {
                _currentRarityText.text = _rarityPalette != null
                    ? _rarityPalette.GetDisplayName(entry.Rarity)
                    : entry.Rarity.ToString();
                if (_rarityPalette != null)
                {
                    _currentRarityText.color = _rarityPalette.GetLabelColor(entry.Rarity);
                }
            }

            if (_nextRarityText != null)
            {
                _nextRarityText.gameObject.SetActive(hasNext);
                if (hasNext)
                {
                    _nextRarityText.text = _rarityPalette != null
                        ? _rarityPalette.GetDisplayName(plan.Next.Rarity)
                        : plan.Next.Rarity.ToString();
                    if (_rarityPalette != null)
                    {
                        _nextRarityText.color = _rarityPalette.GetLabelColor(plan.Next.Rarity);
                    }
                }
            }

            if (_rarityArrow != null)
            {
                _rarityArrow.SetActive(hasNext);
            }

            BindStats(entry.Definition, plan, hasNext);
            BindRequirements(entry, plan, hasNext);

            if (_craftLabel != null)
            {
                _craftLabel.text = hasNext ? _craftText : _maxText;
            }

            if (_craftButton != null)
            {
                _craftButton.interactable = plan.CanCraft;
            }
        }

        private void BindStats(EquipmentDefinition definition, EquipmentCraftPlan plan, bool hasNext)
        {
            EquipmentDefinition.Tier current = plan.Current;
            EquipmentDefinition.Tier next = plan.Next;
            if (current == null)
            {
                return;
            }

            _weaponPowerRow?.Bind("Weapon Power", current.WeaponPower,
                hasNext ? next.WeaponPower : current.WeaponPower, hasNext, false);

            // The ability's LABEL comes from the definition, so a Cargo Bay piece and an Engine piece read
            // differently without this view knowing either name.
            string abilityName = definition != null && !string.IsNullOrWhiteSpace(definition.AbilityDisplayName)
                ? $"Ability: {definition.AbilityDisplayName}"
                : "Ability";
            _abilityRow?.Bind(abilityName, current.AbilityPercent,
                hasNext ? next.AbilityPercent : current.AbilityPercent, hasNext, true);

            _flightSpeedRow?.Bind("Flight Speed", current.FlightSpeedPercent,
                hasNext ? next.FlightSpeedPercent : current.FlightSpeedPercent, hasNext, true);

            _maxLevelRow?.Bind("Max level", current.MaxLevel,
                hasNext ? next.MaxLevel : current.MaxLevel, hasNext, false);
        }

        private void BindRequirements(EquipmentEntry entry, EquipmentCraftPlan plan, bool hasNext)
        {
            if (_duplicateRequirement != null)
            {
                _duplicateRequirement.gameObject.SetActive(hasNext);
                if (hasNext)
                {
                    _duplicateRequirement.Bind(
                        entry.Definition != null ? entry.Definition.Icon : null,
                        plan.DuplicatesOwned,
                        plan.DuplicatesRequired);
                }
            }

            if (_currencyRequirement != null)
            {
                _currencyRequirement.gameObject.SetActive(hasNext && plan.CurrencyRequired > 0);
                if (hasNext && plan.CurrencyRequired > 0)
                {
                    _currencyRequirement.Bind(_currencyIcon, plan.CurrencyOwned, plan.CurrencyRequired);
                }
            }
        }

        /// <summary>Small punch on the crafted card. Unscaled so it still plays with the game paused.</summary>
        public void PlayCraftSuccess()
        {
            if (_punchTarget == null)
            {
                return;
            }

            _punchTween?.Kill();
            _punchTarget.localScale = Vector3.one;
            _punchTween = _punchTarget.DOScale(1.08f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void HandleCraft()
        {
            if (!string.IsNullOrEmpty(_equipmentId))
            {
                _craftRequested?.Invoke(_equipmentId);
            }
        }

        private void HandleBack()
        {
            _backRequested?.Invoke();
        }
    }
}
