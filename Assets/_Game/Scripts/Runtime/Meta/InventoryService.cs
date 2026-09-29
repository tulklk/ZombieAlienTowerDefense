using System;
using System.Collections.Generic;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Meta
{
    /// <summary>The inventory's rules layer: joins MetaItemCatalog definitions to the player's saved rows, decides
    /// what can be equipped, crafted or merged, and raises Changed so the UI never has to poll.
    ///
    /// Deliberately split from PlayerProfileService: that class owns the save document and validates individual
    /// mutations, but knows nothing about slots, rarity ladders or merge sizes. Those live here, next to the
    /// catalog that defines them.
    ///
    /// Every craft/merge runs inside a profile batch so a multi-step transaction hits the disk once, and each
    /// one validates before it spends - there is no path that charges the player for a failed operation.</summary>
    public sealed class InventoryService
    {
        private readonly PlayerProfileService _profile;
        private readonly MetaItemCatalog _catalog;

        /// <summary>Raised after any successful mutation. Views subscribe on enable and unsubscribe on disable.</summary>
        public event Action Changed;

        /// <summary>Raised on a successful rarity craft, for the success animation. The UI still refreshes from
        /// Changed - this only says which piece to punch.</summary>
        public event Action<EquipmentDefinition> EquipmentCrafted;

        /// <summary>Raised on a successful merge, with the artifact and the rarity it became.</summary>
        public event Action<ArtifactDefinition, MetaItemRarity> ArtifactMerged;

        public InventoryService(PlayerProfileService profile, MetaItemCatalog catalog)
        {
            _profile = profile;
            _catalog = catalog;
        }

        public bool IsReady => _profile != null && _catalog != null;

        // ------------------------------------------------------------------ Materials and containers

        public List<MaterialEntry> GetMaterials()
        {
            return CollectItems(MetaItemKind.Material);
        }

        public List<MaterialEntry> GetContainers()
        {
            return CollectItems(MetaItemKind.Container);
        }

        /// <summary>Walks the player's stacks rather than the catalog, so the grid only ever shows what is
        /// actually owned - an item defined but never earned simply does not appear.</summary>
        private List<MaterialEntry> CollectItems(MetaItemKind kind)
        {
            var result = new List<MaterialEntry>();
            if (!IsReady)
            {
                return result;
            }

            List<MetaItemStackSnapshot> stacks = _profile.GetInventoryStacks();
            for (int i = 0; i < stacks.Count; i++)
            {
                if (_catalog.TryGet(stacks[i].ItemId, out MetaItemDefinition definition) && definition.Kind == kind)
                {
                    result.Add(new MaterialEntry(definition, stacks[i].Amount));
                }
            }

            return result;
        }

        // ------------------------------------------------------------------ Equipment

        /// <summary>Owned pieces, optionally narrowed to one slot for the category bar. Null slot = All.</summary>
        public List<EquipmentEntry> GetEquipment(EquipmentSlotType? slotFilter = null)
        {
            var result = new List<EquipmentEntry>();
            if (!IsReady)
            {
                return result;
            }

            List<EquipmentSnapshot> owned = _profile.GetEquipmentEntries();
            for (int i = 0; i < owned.Count; i++)
            {
                if (!_catalog.TryGetEquipment(owned[i].ItemId, out EquipmentDefinition definition))
                {
                    continue;
                }

                if (slotFilter.HasValue && definition.Slot != slotFilter.Value)
                {
                    continue;
                }

                result.Add(ToEntry(definition, owned[i]));
            }

            return result;
        }

        /// <summary>The piece worn in that slot, or a default entry (Exists == false) when the slot is empty -
        /// which is what draws the dimmed placeholder.</summary>
        public EquipmentEntry GetEquippedInSlot(EquipmentSlotType slot)
        {
            if (!IsReady)
            {
                return default;
            }

            List<EquipmentSnapshot> owned = _profile.GetEquipmentEntries();
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].Equipped &&
                    _catalog.TryGetEquipment(owned[i].ItemId, out EquipmentDefinition definition) &&
                    definition.Slot == slot)
                {
                    return ToEntry(definition, owned[i]);
                }
            }

            return default;
        }

        /// <summary>Wears a piece, taking off whatever shared its slot. Both writes happen in one batch so the
        /// save can never land with two pieces equipped in the same slot.</summary>
        public void Equip(string equipmentId)
        {
            if (!IsReady || !_catalog.TryGetEquipment(equipmentId, out EquipmentDefinition definition))
            {
                return;
            }

            _profile.BeginBatch();
            try
            {
                List<EquipmentSnapshot> owned = _profile.GetEquipmentEntries();
                for (int i = 0; i < owned.Count; i++)
                {
                    if (owned[i].ItemId == equipmentId || !owned[i].Equipped)
                    {
                        continue;
                    }

                    if (_catalog.TryGetEquipment(owned[i].ItemId, out EquipmentDefinition other) &&
                        other.Slot == definition.Slot)
                    {
                        _profile.SetEquipmentEquipped(owned[i].ItemId, false);
                    }
                }

                _profile.SetEquipmentEquipped(equipmentId, true);
            }
            finally
            {
                _profile.EndBatch();
            }

            Changed?.Invoke();
        }

        /// <summary>What the next rarity costs and whether the player can pay. Returns a default plan (Definition
        /// null) for a piece that is not owned.</summary>
        public EquipmentCraftPlan GetCraftPlan(string equipmentId)
        {
            if (!IsReady || !_catalog.TryGetEquipment(equipmentId, out EquipmentDefinition definition))
            {
                return default;
            }

            EquipmentSnapshot owned = _profile.GetEquipment(equipmentId);
            if (!owned.Exists)
            {
                return default;
            }

            definition.TryGetTier(owned.Rarity, out EquipmentDefinition.Tier current);
            bool hasNext = definition.TryGetNextTier(owned.Rarity, out EquipmentDefinition.Tier next);

            int duplicatesRequired = hasNext && current != null ? current.UpgradeDuplicates : 0;
            int currencyRequired = hasNext && current != null ? current.UpgradeCurrency : 0;

            return new EquipmentCraftPlan(
                definition,
                current,
                hasNext ? next : null,
                owned.Duplicates,
                duplicatesRequired,
                _profile.MetaCurrency,
                currencyRequired);
        }

        /// <summary>Spends the duplicates and the currency, then raises the rarity. Re-reads the plan first, so a
        /// stale button press (the player spent the currency elsewhere in another tab) cannot push it through.</summary>
        public bool TryCraftEquipment(string equipmentId)
        {
            EquipmentCraftPlan plan = GetCraftPlan(equipmentId);
            if (!plan.CanCraft)
            {
                return false;
            }

            bool success = false;
            _profile.BeginBatch();
            try
            {
                // Currency first: it is the only step that can fail for a reason the plan could not see.
                if (plan.CurrencyRequired > 0 && !_profile.TrySpendMetaCurrency(plan.CurrencyRequired))
                {
                    return false;
                }

                if (!_profile.TryUpgradeEquipmentRarity(equipmentId, plan.Next.Rarity, plan.DuplicatesRequired))
                {
                    // Hand the currency back rather than leaving the player short for nothing.
                    if (plan.CurrencyRequired > 0)
                    {
                        _profile.AddMetaCurrency(plan.CurrencyRequired);
                    }

                    return false;
                }

                success = true;
            }
            finally
            {
                _profile.EndBatch();
            }

            if (success)
            {
                EquipmentCrafted?.Invoke(plan.Definition);
                Changed?.Invoke();
            }

            return success;
        }

        private EquipmentEntry ToEntry(EquipmentDefinition definition, EquipmentSnapshot owned)
        {
            bool canUpgrade = false;
            if (definition.TryGetNextTier(owned.Rarity, out _) &&
                definition.TryGetTier(owned.Rarity, out EquipmentDefinition.Tier current))
            {
                canUpgrade = owned.Duplicates >= current.UpgradeDuplicates &&
                             _profile.MetaCurrency >= current.UpgradeCurrency;
            }

            return new EquipmentEntry(definition, owned.Rarity, owned.Level, owned.Duplicates, owned.Equipped, canUpgrade);
        }

        // ------------------------------------------------------------------ Artifacts

        public List<ArtifactEntry> GetArtifacts()
        {
            var result = new List<ArtifactEntry>();
            if (!IsReady)
            {
                return result;
            }

            List<ArtifactSnapshot> owned = _profile.GetArtifactEntries();
            for (int i = 0; i < owned.Count; i++)
            {
                if (_catalog.TryGetArtifact(owned[i].ItemId, out ArtifactDefinition definition))
                {
                    result.Add(new ArtifactEntry(definition, owned[i].Rarity, owned[i].Amount));
                }
            }

            return result;
        }

        /// <summary>Every stack that currently has enough copies to merge. The merge screen shows exactly these
        /// rows, so "no artifacts available for merging" is simply this list coming back empty.</summary>
        public List<ArtifactMergePlan> GetMergePlans()
        {
            var result = new List<ArtifactMergePlan>();
            if (!IsReady)
            {
                return result;
            }

            List<ArtifactSnapshot> owned = _profile.GetArtifactEntries();
            for (int i = 0; i < owned.Count; i++)
            {
                if (!_catalog.TryGetArtifact(owned[i].ItemId, out ArtifactDefinition definition))
                {
                    continue;
                }

                if (!definition.CanMergeFrom(owned[i].Rarity) || owned[i].Amount < definition.MergeInputCount)
                {
                    continue;
                }

                result.Add(new ArtifactMergePlan(
                    definition,
                    owned[i].Rarity,
                    owned[i].Rarity + 1,
                    definition.MergeInputCount));
            }

            return result;
        }

        public bool TryMergeArtifact(ArtifactMergePlan plan)
        {
            if (!IsReady || !plan.Exists)
            {
                return false;
            }

            bool success = false;
            _profile.BeginBatch();
            try
            {
                if (!_profile.TrySpendArtifact(plan.Definition.Id, plan.SourceRarity, plan.InputCount))
                {
                    return false;
                }

                _profile.AddArtifact(plan.Definition.Id, plan.ResultRarity, 1);
                success = true;
            }
            finally
            {
                _profile.EndBatch();
            }

            if (success)
            {
                ArtifactMerged?.Invoke(plan.Definition, plan.ResultRarity);
                Changed?.Invoke();
            }

            return success;
        }

        /// <summary>For debug tooling and reward grants that want the UI to refresh without knowing the details.</summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
