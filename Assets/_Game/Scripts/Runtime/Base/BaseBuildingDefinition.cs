using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Base
{
    /// <summary>One base building, described entirely as data: its identity, which plot it belongs on, and its
    /// level ladder.
    ///
    /// The whole point is that adding a building means authoring one of these and dropping it in the catalog -
    /// no switch on a building id anywhere in code.</summary>
    [CreateAssetMenu(fileName = "BaseBuilding_", menuName = "AlienDefense/Base/Building Definition")]
    public sealed class BaseBuildingDefinition : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        [TextArea(2, 4)]
        [Tooltip("Shown in the popup's Details section.")]
        private string _description;

        [SerializeField]
        private Sprite _icon;

        [SerializeField]
        private BaseBuildingType _type = BaseBuildingType.Support;

        [SerializeField]
        [Tooltip("Which plot in the world this building occupies. One building per plot.")]
        private string _plotId;

        [SerializeField]
        [Tooltip("Level 1 first. Index + 1 is NOT assumed to be the level - each entry carries its own.")]
        private BuildingLevelDefinition[] _levels = Array.Empty<BuildingLevelDefinition>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public BaseBuildingType Type => _type;
        public string PlotId => _plotId;
        public IReadOnlyList<BuildingLevelDefinition> Levels => _levels;

        public int MaxLevel
        {
            get
            {
                int max = 0;
                if (_levels != null)
                {
                    for (int i = 0; i < _levels.Length; i++)
                    {
                        if (_levels[i] != null && _levels[i].Level > max)
                        {
                            max = _levels[i].Level;
                        }
                    }
                }

                return max;
            }
        }

        public bool TryGetLevel(int level, out BuildingLevelDefinition definition)
        {
            if (_levels != null)
            {
                for (int i = 0; i < _levels.Length; i++)
                {
                    if (_levels[i] != null && _levels[i].Level == level)
                    {
                        definition = _levels[i];
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        /// <summary>The level the player would build or upgrade INTO from their current level. Level 0 (not built)
        /// targets level 1. Returns false at max level, which is what makes the popup show MAX instead of Start.</summary>
        public bool TryGetNextLevel(int currentLevel, out BuildingLevelDefinition definition)
        {
            return TryGetLevel(currentLevel + 1, out definition);
        }
    }
}
