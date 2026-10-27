using System;
using System.Collections.Generic;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Meta
{
    /// <summary>What a level-up of one piece costs and whether the player can pay.</summary>
    public readonly struct EquipmentLevelPlan
    {
        public readonly EquipmentDefinition Definition;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly int CoinCost;
        public readonly int CoinsOwned;
        public readonly string MaterialId;
        public readonly int MaterialCost;
        public readonly int MaterialOwned;

        public EquipmentLevelPlan(EquipmentDefinition definition, int level, int maxLevel, int coinCost, int coinsOwned,
            string materialId, int materialCost, int materialOwned)
        {
            Definition = definition;
            Level = level;
            MaxLevel = maxLevel;
            CoinCost = coinCost;
            CoinsOwned = coinsOwned;
            MaterialId = materialId;
            MaterialCost = materialCost;
            MaterialOwned = materialOwned;
        }

        public bool IsMaxLevel => Definition == null || Level >= MaxLevel;
        public bool HasCoins => CoinsOwned >= CoinCost;
        public bool HasMaterial => MaterialCost <= 0 || MaterialOwned >= MaterialCost;
        public bool CanUpgrade => Definition != null && !IsMaxLevel && HasCoins && HasMaterial;
    }

    /// <summary>One line paid out by a container (for the reward popup).</summary>
    public readonly struct InventoryRewardLine
    {
        public readonly ContainerRewardKind Kind;
        public readonly string Id;
        public readonly int Amount;

        public InventoryRewardLine(ContainerRewardKind kind, string id, int amount)
        {
            Kind = kind;
            Id = id;
            Amount = amount;
        }
    }

    /// <summary>Inventory additions (2026-10-28): unequip, level upgrades, material recipes, container opening, the
    /// equipment bonus / power totals and the notification rules. Same contract as the rest of the service: validate,
    /// spend, grant inside one profile batch, then raise Changed.</summary>
    public sealed partial class InventoryService
    {
        public MetaItemCatalog Catalog => _catalog;
        public int Coins => _profile != null ? _profile.MetaCurrency : 0;
        public int GetOwnedAmount(string itemId) => _profile != null ? _profile.GetItemAmount(itemId) : 0;

        // ------------------------------------------------------------------ equipment

        public bool TryGetOwnedEquipment(string equipmentId, out EquipmentEntry entry)
        {
            entry = default;
            if (!IsReady || !_catalog.TryGetEquipment(equipmentId, out EquipmentDefinition definition))
            {
                return false;
            }

            EquipmentSnapshot owned = _profile.GetEquipment(equipmentId);
            if (!owned.Exists)
            {
                return false;
            }

            entry = ToEntry(definition, owned);
            return true;
        }

        public void Unequip(string equipmentId)
        {
            if (!IsReady)
            {
                return;
            }

            _profile.SetEquipmentEquipped(equipmentId, false);
            Changed?.Invoke();
        }

        public EquipmentLevelPlan GetLevelPlan(string equipmentId)
        {
            if (!TryGetOwnedEquipment(equipmentId, out EquipmentEntry entry))
            {
                return default;
            }

            EquipmentDefinition d = entry.Definition;
            int maxLevel = d.TryGetTier(entry.Rarity, out EquipmentDefinition.Tier tier) ? tier.MaxLevel : 1;
            string material = d.LevelMaterialId;
            return new EquipmentLevelPlan(d, entry.Level, maxLevel, d.GetLevelCoinCost(entry.Level), _profile.MetaCurrency,
                material, d.GetLevelMaterialCost(entry.Level), string.IsNullOrEmpty(material) ? 0 : _profile.GetItemAmount(material));
        }

        public bool TryUpgradeLevel(string equipmentId)
        {
            EquipmentLevelPlan plan = GetLevelPlan(equipmentId);
            if (!plan.CanUpgrade)
            {
                return false;
            }

            _profile.BeginBatch();
            try
            {
                if (!_profile.TrySpendMetaCurrency(plan.CoinCost))
                {
                    return false;
                }

                if (plan.MaterialCost > 0 && !_profile.TrySpendItem(plan.MaterialId, plan.MaterialCost))
                {
                    _profile.AddMetaCurrency(plan.CoinCost);
                    return false;
                }

                _profile.TrySetEquipmentLevel(equipmentId, plan.Level + 1);
            }
            finally
            {
                _profile.EndBatch();
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Rarity craft available or a level-up affordable.</summary>
        public bool CanImprove(in EquipmentEntry entry)
        {
            return entry.Exists && (entry.CanUpgrade || GetLevelPlan(entry.Definition.Id).CanUpgrade);
        }

        /// <summary>Bonus percent a piece adds to the UFO (its ability + flight speed, grown by level).</summary>
        public float GetPieceBonusPercent(in EquipmentEntry entry)
        {
            if (!entry.Exists || !entry.Definition.TryGetTier(entry.Rarity, out EquipmentDefinition.Tier tier))
            {
                return 0f;
            }

            return (tier.AbilityPercent + tier.FlightSpeedPercent) * entry.Definition.LevelMultiplier(entry.Level);
        }

        public float GetPieceWeaponPower(in EquipmentEntry entry)
        {
            if (!entry.Exists || !entry.Definition.TryGetTier(entry.Rarity, out EquipmentDefinition.Tier tier))
            {
                return 0f;
            }

            return tier.WeaponPower * entry.Definition.LevelMultiplier(entry.Level);
        }

        /// <summary>The "600%" above the UFO: every equipped piece plus the best artifact of each part.</summary>
        public float GetTotalBonusPercent()
        {
            float total = 0f;
            for (int s = 0; s < EquipmentSlotNames.Count; s++)
            {
                total += GetPieceBonusPercent(GetEquippedInSlot((EquipmentSlotType)s));
            }

            List<ArtifactEntry> artifacts = GetArtifacts();
            var best = new float[EquipmentSlotNames.Count];
            foreach (ArtifactEntry a in artifacts)
            {
                int slot = (int)a.Definition.Slot;
                if (slot >= 0 && slot < best.Length)
                {
                    best[slot] = Mathf.Max(best[slot], a.Definition.GetBonusPercent(a.Rarity));
                }
            }

            for (int i = 0; i < best.Length; i++)
            {
                total += best[i];
            }

            return total;
        }

        /// <summary>Power the equipped pieces add to the player's Power (PlayerPowerCalculator).</summary>
        public int GetEquipmentPower()
        {
            float power = 0f;
            for (int s = 0; s < EquipmentSlotNames.Count; s++)
            {
                power += GetPieceWeaponPower(GetEquippedInSlot((EquipmentSlotType)s));
            }

            return Mathf.RoundToInt(power);
        }

        // ------------------------------------------------------------------ craft (materials -> new piece)

        public void GetRecipes(EquipmentSlotType? filter, List<EquipmentDefinition> into)
        {
            into.Clear();
            if (!IsReady)
            {
                return;
            }

            foreach (EquipmentDefinition d in _catalog.Equipment)
            {
                if (d != null && d.HasRecipe && (!filter.HasValue || d.Slot == filter.Value))
                {
                    into.Add(d);
                }
            }
        }

        public bool CanCraft(EquipmentDefinition definition)
        {
            if (!IsReady || definition == null || !definition.HasRecipe || _profile.MetaCurrency < definition.RecipeCoins)
            {
                return false;
            }

            foreach (MetaItemAmount cost in definition.Recipe)
            {
                if (_profile.GetItemAmount(cost.ItemId) < cost.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Spends the recipe and grants the piece (a spare copy when already owned). All-or-nothing.</summary>
        public bool TryCraft(EquipmentDefinition definition)
        {
            if (!CanCraft(definition))
            {
                return false;
            }

            _profile.BeginBatch();
            try
            {
                if (definition.RecipeCoins > 0 && !_profile.TrySpendMetaCurrency(definition.RecipeCoins))
                {
                    return false;
                }

                foreach (MetaItemAmount cost in definition.Recipe)
                {
                    _profile.TrySpendItem(cost.ItemId, cost.Amount); // checked by CanCraft inside the same batch
                }

                _profile.GrantEquipment(definition.Id, definition.BaseRarity);
            }
            finally
            {
                _profile.EndBatch();
            }

            EquipmentCrafted?.Invoke(definition);
            Changed?.Invoke();
            return true;
        }

        /// <summary>A recipe for a piece not owned yet is affordable (the Craft button's badge).</summary>
        public bool HasNewCraftAvailable()
        {
            if (!IsReady)
            {
                return false;
            }

            foreach (EquipmentDefinition d in _catalog.Equipment)
            {
                if (d != null && d.HasRecipe && !_profile.GetEquipment(d.Id).Exists && CanCraft(d))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ containers

        /// <summary>Opens <paramref name="count"/> containers (clamped to what is owned): spends them and grants
        /// each roll. Returns the summed payout for the reward popup.</summary>
        public bool TryOpenContainer(string containerId, int count, List<InventoryRewardLine> granted)
        {
            granted?.Clear();
            if (!IsReady || !_catalog.TryGet(containerId, out MetaItemDefinition container) || container.Kind != MetaItemKind.Container)
            {
                return false;
            }

            count = Mathf.Min(count, _profile.GetItemAmount(containerId));
            if (count <= 0 || !_profile.TrySpendItem(containerId, count))
            {
                return false;
            }

            var totals = new Dictionary<string, int>();
            var kinds = new Dictionary<string, ContainerRewardKind>();
            var random = new System.Random(System.Environment.TickCount);
            for (int n = 0; n < count; n++)
            {
                foreach (ContainerReward reward in container.ContainerRewards)
                {
                    if (random.NextDouble() > reward.Chance)
                    {
                        continue;
                    }

                    int amount = random.Next(Mathf.Max(1, reward.Min), Mathf.Max(reward.Min, reward.Max) + 1);
                    string key = reward.Kind + "|" + reward.Id;
                    totals[key] = (totals.TryGetValue(key, out int t) ? t : 0) + amount;
                    kinds[key] = reward.Kind;
                }
            }

            _profile.BeginBatch();
            try
            {
                foreach (KeyValuePair<string, int> pair in totals)
                {
                    ContainerRewardKind kind = kinds[pair.Key];
                    string id = pair.Key.Substring(pair.Key.IndexOf('|') + 1);
                    switch (kind)
                    {
                        case ContainerRewardKind.Coins: _profile.AddMetaCurrency(pair.Value); break;
                        case ContainerRewardKind.Gems: _profile.AddGems(pair.Value); break;
                        case ContainerRewardKind.Item: _profile.AddItem(id, pair.Value); break;
                        case ContainerRewardKind.Artifact:
                            if (_catalog.TryGetArtifact(id, out ArtifactDefinition artifact))
                            {
                                _profile.AddArtifact(id, artifact.BaseRarity, pair.Value);
                            }

                            break;
                    }

                    granted?.Add(new InventoryRewardLine(kind, id, pair.Value));
                }
            }
            finally
            {
                _profile.EndBatch();
            }

            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ artifacts progress

        /// <summary>For the progression overview: per part, which rarities the player owns (index = rarity).</summary>
        public bool[,] GetArtifactProgress()
        {
            var owned = new bool[EquipmentSlotNames.Count, 5];
            foreach (ArtifactEntry a in GetArtifacts())
            {
                int slot = (int)a.Definition.Slot;
                int rarity = Mathf.Clamp((int)a.Rarity, 0, 4);
                if (slot >= 0 && slot < EquipmentSlotNames.Count)
                {
                    owned[slot, rarity] = true;
                }
            }

            return owned;
        }

        // ------------------------------------------------------------------ notifications

        public bool IsNewEquipment(string id) => _profile != null && _profile.HasInventoryNew(InventoryNewKeys.Equipment(id));
        public bool IsNewArtifact(string id, MetaItemRarity rarity) => _profile != null && _profile.HasInventoryNew(InventoryNewKeys.Artifact(id, rarity));
        public bool IsNewItem(string id) => _profile != null && _profile.HasInventoryNew(InventoryNewKeys.Item(id));

        public void MarkSeen(string key)
        {
            _profile?.MarkInventorySeen(key);
        }

        public void MarkAllSeen(string prefix)
        {
            _profile?.MarkInventorySeenByPrefix(prefix);
        }

        public bool HasSlotBadge(EquipmentSlotType slot)
        {
            foreach (EquipmentEntry e in GetEquipment(slot))
            {
                if (IsNewEquipment(e.Definition.Id) || CanImprove(e))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasEquipmentBadge()
        {
            for (int s = 0; s < EquipmentSlotNames.Count; s++)
            {
                if (HasSlotBadge((EquipmentSlotType)s))
                {
                    return true;
                }
            }

            return HasNewCraftAvailable();
        }

        public bool HasArtifactBadge()
        {
            return GetMergePlans().Count > 0 || (_profile != null && _profile.HasAnyInventoryNew(InventoryNewKeys.ArtifactPrefix));
        }

        public bool HasMaterialsBadge()
        {
            return GetContainers().Count > 0 || (_profile != null && _profile.HasAnyInventoryNew(InventoryNewKeys.ItemPrefix));
        }

        public bool HasAnyBadge() => IsReady && (HasEquipmentBadge() || HasArtifactBadge() || HasMaterialsBadge());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugGrantEquipment(string id)
        {
            if (_catalog.TryGetEquipment(id, out EquipmentDefinition d))
            {
                _profile.GrantEquipment(id, d.BaseRarity);
                Changed?.Invoke();
            }
        }

        public void DebugGrantArtifacts(string id, MetaItemRarity rarity, int amount)
        {
            _profile.AddArtifact(id, rarity, amount);
            Changed?.Invoke();
        }

        public void DebugGrantItem(string id, int amount)
        {
            _profile.AddItem(id, amount);
            Changed?.Invoke();
        }
#endif
    }
}
