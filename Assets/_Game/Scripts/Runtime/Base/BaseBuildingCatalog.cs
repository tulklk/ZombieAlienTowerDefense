using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>Every base building the game knows about, plus the one setting that governs the build queue.
    ///
    /// Mirrors the shape of the project's existing TowerCatalog / MetaItemCatalog so the base does not introduce
    /// a second, different way of looking definitions up.</summary>
    [CreateAssetMenu(fileName = "BaseBuildingCatalog", menuName = "AlienDefense/Base/Building Catalog")]
    public sealed class BaseBuildingCatalog : ScriptableObject
    {
        [SerializeField]
        private BaseBuildingDefinition[] _buildings = Array.Empty<BaseBuildingDefinition>();

        [SerializeField, Min(1)]
        [Tooltip("How many builds may run at once. 1 matches the reference (one builder); raising it is a data " +
            "change, not a code change.")]
        private int _maxConcurrentConstruction = 1;

        [SerializeField]
        [Tooltip("Id of the building every other one gates against. Must match one of the definitions above.")]
        private string _centralBuildingId = "central_building";

        [SerializeField, Min(0f)]
        [Tooltip("Gems charged per remaining MINUTE when finishing early, rounded up. 0 makes Finish Now free, " +
            "which is almost certainly a mistake.")]
        private float _finishNowGemsPerMinute = 1f;

        [SerializeField]
        [Tooltip("Guided progression shown in the quest tracker, in order. The first unmet goal is the one shown.")]
        private BaseBuildingGoal[] _goals = Array.Empty<BaseBuildingGoal>();

        public IReadOnlyList<BaseBuildingDefinition> Buildings => _buildings;
        public IReadOnlyList<BaseBuildingGoal> Goals => _goals;
        public int MaxConcurrentConstruction => Mathf.Max(1, _maxConcurrentConstruction);
        public string CentralBuildingId => _centralBuildingId;
        public float FinishNowGemsPerMinute => Mathf.Max(0f, _finishNowGemsPerMinute);

        public bool TryGet(string buildingId, out BaseBuildingDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(buildingId) && _buildings != null)
            {
                for (int i = 0; i < _buildings.Length; i++)
                {
                    if (_buildings[i] != null && _buildings[i].Id == buildingId)
                    {
                        definition = _buildings[i];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetByPlot(string plotId, out BaseBuildingDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(plotId) && _buildings != null)
            {
                for (int i = 0; i < _buildings.Length; i++)
                {
                    if (_buildings[i] != null && _buildings[i].PlotId == plotId)
                    {
                        definition = _buildings[i];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }
    }
}
