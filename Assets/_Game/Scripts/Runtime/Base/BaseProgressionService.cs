using System;
using System.Collections.Generic;
using AlienDefense.Save;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>The base's rules layer: what state each building is in, what it would cost to advance it, and the
    /// transactions that advance it.
    ///
    /// Time is always absolute UTC. Nothing here counts down, and nothing calls Time.time - a build knows only
    /// when it will be finished, so a player who closes the game mid-build finds it done when they return
    /// (ResolveFinishedConstructions). That is also why there is no Update in this class at all: the view polls
    /// the remaining time a few times a second for its bar, but the state machine only moves on real events.
    ///
    /// Deliberately separate from the in-level AlienDefense.Building namespace, which is tower placement.</summary>
    public sealed class BaseProgressionService
    {
        private readonly PlayerProfileService _profile;
        private readonly BaseBuildingCatalog _catalog;
        private readonly BuildingRequirementEvaluator _requirements;

        /// <summary>Force earned from base buildings, recomputed from the saved levels rather than accumulated -
        /// an accumulated total would drift the first time a save was edited or a definition rebalanced.</summary>
        public event Action<int> ForceChanged;

        public event Action<BaseBuildingDefinition, int> BuildingStarted;
        public event Action<BaseBuildingDefinition, int> BuildingCompleted;
        public event Action<BaseBuildingDefinition, string, int> ResourceCollected;

        /// <summary>Any state change at all - views refresh on this instead of polling.</summary>
        public event Action Changed;

        public BaseProgressionService(PlayerProfileService profile, BaseBuildingCatalog catalog,
            Meta.MetaItemCatalog items)
        {
            _profile = profile;
            _catalog = catalog;
            _requirements = new BuildingRequirementEvaluator(profile, catalog, items);
        }

        public bool IsReady => _profile != null && _catalog != null;
        public BaseBuildingCatalog Catalog => _catalog;
        public BuildingRequirementEvaluator Requirements => _requirements;

        // ------------------------------------------------------------------ Queries

        public int GetLevel(string buildingId)
        {
            return _profile != null ? _profile.GetBaseBuilding(buildingId).Level : 0;
        }

        public BaseBuildingSnapshot GetState(string buildingId)
        {
            return _profile != null ? _profile.GetBaseBuilding(buildingId) : default;
        }

        /// <summary>The state to draw for a plot, including the Locked/Available distinction that an unsaved
        /// building has no row for.</summary>
        public BaseBuildingState GetDisplayState(BaseBuildingDefinition definition, DateTime nowUtc)
        {
            if (definition == null || _profile == null)
            {
                return BaseBuildingState.Locked;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(definition.Id);
            if (saved.Exists && saved.Level > 0)
            {
                return saved.IsBuilding ? saved.State : BaseBuildingState.Built;
            }

            if (saved.Exists && saved.IsBuilding)
            {
                return BaseBuildingState.Constructing;
            }

            // Never built: it is buildable only once its level-1 gates are met.
            return definition.TryGetLevel(1, out BuildingLevelDefinition first) && _requirements.AreAllSatisfied(first)
                ? BaseBuildingState.Available
                : BaseBuildingState.Locked;
        }

        public bool IsMaxLevel(BaseBuildingDefinition definition)
        {
            return definition != null && GetLevel(definition.Id) >= definition.MaxLevel;
        }

        /// <summary>How many builds are running. The catalog's MaxConcurrentConstruction is compared against this
        /// so a second Start is refused while the builder is busy.</summary>
        public int GetActiveConstructionCount(DateTime nowUtc)
        {
            if (_profile == null)
            {
                return 0;
            }

            int count = 0;
            List<BaseBuildingSnapshot> all = _profile.GetBaseBuildings();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsBuilding && !all[i].IsConstructionFinished(nowUtc))
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsBuilderBusy(DateTime nowUtc)
        {
            return _catalog != null && GetActiveConstructionCount(nowUtc) >= _catalog.MaxConcurrentConstruction;
        }

        /// <summary>True when pressing Start on this building right now would succeed. Drives the green arrow over
        /// the building, so it answers the same question TryStartConstruction does, minus the side effects.</summary>
        public bool CanStartNow(BaseBuildingDefinition definition, DateTime nowUtc)
        {
            if (!IsReady || definition == null)
            {
                return false;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(definition.Id);
            if (saved.IsBuilding && !saved.IsConstructionFinished(nowUtc))
            {
                return false;
            }

            return definition.TryGetNextLevel(saved.Level, out BuildingLevelDefinition next)
                   && !IsBuilderBusy(nowUtc)
                   && _requirements.AreAllSatisfied(next);
        }

        /// <summary>The first guided-progression goal the player has not reached yet. False when every goal is
        /// done, which hides the tracker.</summary>
        public bool TryGetCurrentGoal(out BaseBuildingGoal goal, out BaseBuildingDefinition definition)
        {
            goal = null;
            definition = null;
            if (!IsReady)
            {
                return false;
            }

            IReadOnlyList<BaseBuildingGoal> goals = _catalog.Goals;
            for (int i = 0; i < goals.Count; i++)
            {
                BaseBuildingGoal candidate = goals[i];
                if (candidate == null || !_catalog.TryGet(candidate.BuildingId, out BaseBuildingDefinition def))
                {
                    continue;
                }

                if (GetLevel(def.Id) < candidate.Level)
                {
                    goal = candidate;
                    definition = def;
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ Construction

        /// <summary>Why a Start press was refused. The popup turns this into its button label, so the player is
        /// told which of the three very different blockers they hit.</summary>
        public enum StartResult
        {
            Started = 0,
            AlreadyMaxLevel = 1,
            RequirementsNotMet = 2,
            BuilderBusy = 3,
            AlreadyBuilding = 4,
            InvalidBuilding = 5
        }

        public StartResult TryStartConstruction(string buildingId, DateTime nowUtc)
        {
            if (!IsReady || !_catalog.TryGet(buildingId, out BaseBuildingDefinition definition))
            {
                return StartResult.InvalidBuilding;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(buildingId);
            if (saved.IsBuilding && !saved.IsConstructionFinished(nowUtc))
            {
                return StartResult.AlreadyBuilding;
            }

            int currentLevel = saved.Level;
            if (!definition.TryGetNextLevel(currentLevel, out BuildingLevelDefinition next))
            {
                return StartResult.AlreadyMaxLevel;
            }

            if (IsBuilderBusy(nowUtc))
            {
                return StartResult.BuilderBusy;
            }

            if (!_requirements.AreAllSatisfied(next))
            {
                return StartResult.RequirementsNotMet;
            }

            _profile.BeginBatch();
            try
            {
                if (!_requirements.TrySpend(next))
                {
                    return StartResult.RequirementsNotMet;
                }

                long completeTicks = nowUtc.Ticks + (long)(next.ConstructionSeconds * TimeSpan.TicksPerSecond);
                _profile.SetBaseBuilding(
                    buildingId,
                    currentLevel,
                    currentLevel > 0 ? BaseBuildingState.Upgrading : BaseBuildingState.Constructing,
                    nowUtc.Ticks,
                    completeTicks,
                    saved.LastCollectUtcTicks);
            }
            finally
            {
                _profile.EndBatch();
            }

            BuildingStarted?.Invoke(definition, currentLevel + 1);
            Changed?.Invoke();
            return StartResult.Started;
        }

        /// <summary>Gems needed to skip the rest of a build, rounded up per minute remaining. Returns 0 when
        /// nothing is running, which the popup reads as "hide the Finish button".</summary>
        public int GetFinishNowGemCost(string buildingId, DateTime nowUtc)
        {
            if (!IsReady)
            {
                return 0;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(buildingId);
            if (!saved.IsBuilding)
            {
                return 0;
            }

            TimeSpan remaining = saved.GetRemaining(nowUtc);
            if (remaining <= TimeSpan.Zero)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.CeilToInt((float)remaining.TotalMinutes * _catalog.FinishNowGemsPerMinute));
        }

        /// <summary>Gems for skipping a build of the given length. Used for the popup's "Finish" button before a build
        /// has started, where the whole duration is being skipped. Same formula as a running build's cost.</summary>
        public int GetGemCostForSeconds(double seconds)
        {
            if (!IsReady || seconds <= 0d)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.CeilToInt((float)(seconds / 60d) * _catalog.FinishNowGemsPerMinute));
        }

        /// <summary>Pays requirements AND gems, then completes immediately - the reference's "Finish" offered next to
        /// "Start". Gems are checked before anything is spent, so a player short of gems loses nothing.</summary>
        public bool TryBuildInstantly(string buildingId, DateTime nowUtc)
        {
            if (!IsReady || !_catalog.TryGet(buildingId, out BaseBuildingDefinition definition))
            {
                return false;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(buildingId);
            if (!definition.TryGetNextLevel(saved.Level, out BuildingLevelDefinition next))
            {
                return false;
            }

            int gems = GetGemCostForSeconds(next.ConstructionSeconds);
            if (_profile.Gems < gems)
            {
                return false;
            }

            if (TryStartConstruction(buildingId, nowUtc) != StartResult.Started)
            {
                return false;
            }

            // The build is now running; skipping it charges exactly the gems checked above.
            return TryFinishNow(buildingId, nowUtc);
        }

        public bool TryFinishNow(string buildingId, DateTime nowUtc)
        {
            if (!IsReady || !_catalog.TryGet(buildingId, out BaseBuildingDefinition definition))
            {
                return false;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(buildingId);
            if (!saved.IsBuilding)
            {
                return false;
            }

            int cost = GetFinishNowGemCost(buildingId, nowUtc);
            if (cost > 0 && !_profile.TrySpendGems(cost))
            {
                return false;
            }

            CompleteConstruction(definition, saved, nowUtc);
            return true;
        }

        /// <summary>Completes every build whose stored instant has passed. Call once when the base is opened and
        /// whenever the app regains focus - this is the whole of the offline-completion mechanism.
        ///
        /// Returns how many finished, so the caller can decide whether to play the completion sequence.</summary>
        public int ResolveFinishedConstructions(DateTime nowUtc)
        {
            if (!IsReady)
            {
                return 0;
            }

            List<BaseBuildingSnapshot> all = _profile.GetBaseBuildings();
            int completed = 0;

            _profile.BeginBatch();
            try
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (!all[i].IsConstructionFinished(nowUtc))
                    {
                        continue;
                    }

                    if (_catalog.TryGet(all[i].BuildingId, out BaseBuildingDefinition definition))
                    {
                        CompleteConstruction(definition, all[i], nowUtc, raiseChanged: false);
                        completed++;
                    }
                }
            }
            finally
            {
                _profile.EndBatch();
            }

            if (completed > 0)
            {
                RaiseForceChanged();
                Changed?.Invoke();
            }

            return completed;
        }

        private void CompleteConstruction(BaseBuildingDefinition definition, BaseBuildingSnapshot saved,
            DateTime nowUtc, bool raiseChanged = true)
        {
            int newLevel = saved.Level + 1;

            // The production anchor starts now, not at the build's start: a building cannot have been producing
            // while it was still scaffolding.
            _profile.SetBaseBuilding(definition.Id, newLevel, BaseBuildingState.Built, 0L, 0L, nowUtc.Ticks);

            BuildingCompleted?.Invoke(definition, newLevel);

            if (raiseChanged)
            {
                RaiseForceChanged();
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------------ Force

        /// <summary>Force contributed by the base, summed over every completed level of every building. Recomputed
        /// rather than stored so it always matches the current definitions.</summary>
        public int ComputeBaseForce()
        {
            if (!IsReady)
            {
                return 0;
            }

            int total = 0;
            IReadOnlyList<BaseBuildingDefinition> buildings = _catalog.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                BaseBuildingDefinition definition = buildings[i];
                if (definition == null)
                {
                    continue;
                }

                int level = GetLevel(definition.Id);
                for (int l = 1; l <= level; l++)
                {
                    if (definition.TryGetLevel(l, out BuildingLevelDefinition levelDefinition))
                    {
                        total += levelDefinition.ForceReward;
                    }
                }
            }

            return total;
        }

        private void RaiseForceChanged()
        {
            ForceChanged?.Invoke(ComputeBaseForce());
        }

        // ------------------------------------------------------------------ Production

        /// <summary>Material owed since the last collect, capped by the level's storage. Pure read - calling it
        /// repeatedly (for the world bubble) never grants anything.</summary>
        public int GetPendingProduction(BaseBuildingDefinition definition, DateTime nowUtc, out string materialId)
        {
            materialId = null;
            if (!IsReady || definition == null)
            {
                return 0;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(definition.Id);
            if (saved.Level <= 0 || saved.IsBuilding)
            {
                return 0;
            }

            if (!definition.TryGetLevel(saved.Level, out BuildingLevelDefinition level))
            {
                return 0;
            }

            BuildingProductionConfig production = level.Production;
            if (production == null || !production.IsActive)
            {
                return 0;
            }

            materialId = production.MaterialId;

            long anchor = saved.LastCollectUtcTicks > 0 ? saved.LastCollectUtcTicks : nowUtc.Ticks;
            long elapsed = nowUtc.Ticks - anchor;
            if (elapsed <= 0)
            {
                // Clock moved backwards. Produce nothing rather than a negative or a windfall.
                return 0;
            }

            long intervalTicks = (long)(production.IntervalSeconds * TimeSpan.TicksPerSecond);
            long ticks = elapsed / Math.Max(1L, intervalTicks);
            long amount = ticks * production.AmountPerInterval;

            if (production.StorageCapacity > 0 && amount > production.StorageCapacity)
            {
                amount = production.StorageCapacity;
            }

            return (int)Math.Min(int.MaxValue, Math.Max(0L, amount));
        }

        public bool TryCollectProduction(string buildingId, DateTime nowUtc)
        {
            if (!IsReady || !_catalog.TryGet(buildingId, out BaseBuildingDefinition definition))
            {
                return false;
            }

            int amount = GetPendingProduction(definition, nowUtc, out string materialId);
            if (amount <= 0 || string.IsNullOrWhiteSpace(materialId))
            {
                return false;
            }

            BaseBuildingSnapshot saved = _profile.GetBaseBuilding(buildingId);

            _profile.BeginBatch();
            try
            {
                _profile.AddItem(materialId, amount);
                _profile.SetBaseBuilding(buildingId, saved.Level, saved.State,
                    saved.ConstructionStartUtcTicks, saved.ConstructionCompleteUtcTicks, nowUtc.Ticks);
            }
            finally
            {
                _profile.EndBatch();
            }

            ResourceCollected?.Invoke(definition, materialId, amount);
            Changed?.Invoke();
            return true;
        }
    }
}
