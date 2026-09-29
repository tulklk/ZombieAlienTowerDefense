using System.Collections.Generic;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>One requirement row, already resolved against the player: what it needs, what they have, and
    /// enough context for the popup to draw it and to wire its GO button.</summary>
    public readonly struct RequirementStatus
    {
        public readonly BuildingRequirement Requirement;
        public readonly string DisplayLabel;
        public readonly int Owned;
        public readonly int Required;
        public readonly bool Satisfied;

        /// <summary>Icon for the row, or null when the requirement is not an item.</summary>
        public readonly Sprite Icon;

        public RequirementStatus(BuildingRequirement requirement, string displayLabel, int owned, int required,
            bool satisfied, Sprite icon)
        {
            Requirement = requirement;
            DisplayLabel = displayLabel;
            Owned = owned;
            Required = required;
            Satisfied = satisfied;
            Icon = icon;
        }

        /// <summary>Whether the row should offer a GO button. Only gates the player can go and fix elsewhere get
        /// one - being short of material is fixed by playing, not by navigating.</summary>
        public bool HasNavigation =>
            Requirement != null &&
            (Requirement.Type == BuildingRequirementType.BuildingLevel ||
             Requirement.Type == BuildingRequirementType.CampaignLevel);
    }

    /// <summary>Answers "can the player afford this level?" for every requirement type, and does the spending.
    ///
    /// Everything funnels through here rather than through per-popup logic, so the Start button, the requirement
    /// rows and the actual charge can never disagree. Evaluation is also re-run at spend time - a button that
    /// went stale while another tab drained the same currency must not push a build through.</summary>
    public sealed class BuildingRequirementEvaluator
    {
        private readonly PlayerProfileService _profile;
        private readonly BaseBuildingCatalog _catalog;
        private readonly Meta.MetaItemCatalog _items;

        public BuildingRequirementEvaluator(PlayerProfileService profile, BaseBuildingCatalog catalog,
            Meta.MetaItemCatalog items)
        {
            _profile = profile;
            _catalog = catalog;
            _items = items;
        }

        public List<RequirementStatus> Evaluate(BuildingLevelDefinition level)
        {
            var result = new List<RequirementStatus>();
            if (level == null || _profile == null)
            {
                return result;
            }

            BuildingRequirement[] requirements = level.Requirements;
            for (int i = 0; i < requirements.Length; i++)
            {
                if (requirements[i] != null)
                {
                    result.Add(Evaluate(requirements[i]));
                }
            }

            return result;
        }

        public bool AreAllSatisfied(BuildingLevelDefinition level)
        {
            if (level == null || _profile == null)
            {
                return false;
            }

            BuildingRequirement[] requirements = level.Requirements;
            for (int i = 0; i < requirements.Length; i++)
            {
                if (requirements[i] != null && !Evaluate(requirements[i]).Satisfied)
                {
                    return false;
                }
            }

            return true;
        }

        public RequirementStatus Evaluate(BuildingRequirement requirement)
        {
            int required = requirement.RequiredValue;

            switch (requirement.Type)
            {
                case BuildingRequirementType.BuildingLevel:
                {
                    int owned = _profile.GetBaseBuilding(requirement.TargetId).Level;
                    string name = requirement.TargetId;
                    Sprite icon = null;
                    if (_catalog != null && _catalog.TryGet(requirement.TargetId, out BaseBuildingDefinition def))
                    {
                        name = def.DisplayName;
                        icon = def.Icon;
                    }

                    return new RequirementStatus(requirement, $"{name} Lvl {required}", owned, required,
                        owned >= required, icon);
                }

                case BuildingRequirementType.CampaignLevel:
                {
                    bool completed = _profile.GetLevelProgress(requirement.TargetId).IsCompleted;
                    return new RequirementStatus(requirement, $"Complete campaign level {required}",
                        completed ? 1 : 0, 1, completed, null);
                }

                case BuildingRequirementType.Currency:
                {
                    int owned = _profile.MetaCurrency;
                    return new RequirementStatus(requirement, "Currency", owned, required, owned >= required, null);
                }

                case BuildingRequirementType.Gems:
                {
                    int owned = _profile.Gems;
                    return new RequirementStatus(requirement, "Gems", owned, required, owned >= required, null);
                }

                case BuildingRequirementType.Material:
                {
                    int owned = _profile.GetItemAmount(requirement.TargetId);
                    string name = requirement.TargetId;
                    Sprite icon = null;
                    if (_items != null && _items.TryGet(requirement.TargetId, out Meta.MetaItemDefinition item))
                    {
                        name = item.DisplayName;
                        icon = item.Icon;
                    }

                    return new RequirementStatus(requirement, name, owned, required, owned >= required, icon);
                }

                case BuildingRequirementType.PlayerLevel:
                {
                    int owned = _profile.DisplayLevel;
                    return new RequirementStatus(requirement, $"Player level {required}", owned, required,
                        owned >= required, null);
                }

                default:
                    return new RequirementStatus(requirement, requirement.Type.ToString(), 0, required, false, null);
            }
        }

        /// <summary>Charges every consumable requirement. Re-checks all of them first and returns false without
        /// spending anything if even one falls short, so a build can never half-charge the player.
        ///
        /// The caller is expected to already be inside a profile batch, so the whole purchase lands as one save.</summary>
        public bool TrySpend(BuildingLevelDefinition level)
        {
            if (level == null || _profile == null || !AreAllSatisfied(level))
            {
                return false;
            }

            BuildingRequirement[] requirements = level.Requirements;

            // Remember what actually came out of the player's pocket. AreAllSatisfied passed a moment ago so a
            // failure here should be impossible, but "should be impossible" is not a reason to leave the player
            // charged for a build that never started - so anything already taken is handed straight back.
            var spentSoFar = new List<BuildingRequirement>();

            for (int i = 0; i < requirements.Length; i++)
            {
                BuildingRequirement requirement = requirements[i];
                if (requirement == null || !requirement.IsConsumable)
                {
                    continue;
                }

                bool spent;
                switch (requirement.Type)
                {
                    case BuildingRequirementType.Currency:
                        spent = _profile.TrySpendMetaCurrency(requirement.RequiredValue);
                        break;
                    case BuildingRequirementType.Gems:
                        spent = _profile.TrySpendGems(requirement.RequiredValue);
                        break;
                    case BuildingRequirementType.Material:
                        spent = _profile.TrySpendItem(requirement.TargetId, requirement.RequiredValue);
                        break;
                    default:
                        spent = true;
                        break;
                }

                if (spent)
                {
                    spentSoFar.Add(requirement);
                    continue;
                }

                Debug.LogError($"[BuildingRequirementEvaluator] Spend failed for {requirement.Type} " +
                    $"'{requirement.TargetId}' after validation passed; refunding and aborting the build.");
                Refund(spentSoFar);
                return false;
            }

            return true;
        }

        private void Refund(List<BuildingRequirement> spent)
        {
            for (int i = 0; i < spent.Count; i++)
            {
                BuildingRequirement requirement = spent[i];
                switch (requirement.Type)
                {
                    case BuildingRequirementType.Currency:
                        _profile.AddMetaCurrency(requirement.RequiredValue);
                        break;
                    case BuildingRequirementType.Gems:
                        _profile.AddGems(requirement.RequiredValue);
                        break;
                    case BuildingRequirementType.Material:
                        _profile.AddItem(requirement.TargetId, requirement.RequiredValue);
                        break;
                }
            }
        }
    }
}
