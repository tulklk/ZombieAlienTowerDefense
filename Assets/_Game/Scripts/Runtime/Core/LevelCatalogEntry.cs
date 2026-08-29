using System;
using AlienDefense.Data;
using UnityEngine;

namespace AlienDefense.Core
{
    /// <summary>One campaign entry: which LevelDefinition to run and which gameplay scene to load for it.</summary>
    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField]
        private LevelDefinition _levelDefinition;

        [SerializeField]
        [Tooltip("Scene name as it appears in Build Settings.")]
        private string _sceneName;

        public LevelDefinition LevelDefinition => _levelDefinition;
        public string SceneName => _sceneName;
        public string LevelId => _levelDefinition != null ? _levelDefinition.LevelId : null;
    }
}
