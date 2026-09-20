using System;
using AlienDefense.Data;
using AlienDefense.Meta;
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

        [SerializeField]
        [Tooltip("Full-color flat map illustration shown as this level's MainMenu center preview " +
            "(LevelSelectionArea/PreviewBackdrop) when the level is unlocked. Config-asset reference only; " +
            "save data never stores it.")]
        private Sprite _menuPreviewSprite;

        [SerializeField]
        [Tooltip("Optional. Grayscale/desaturated variant of MenuPreviewSprite shown instead while the level " +
            "is still locked. Falls back to MenuPreviewSprite itself (rendered full-color) if left empty.")]
        private Sprite _menuPreviewSpriteLocked;

        [SerializeField]
        private ObjectiveRewardBundle _clearRewards = new ObjectiveRewardBundle();

        [SerializeField]
        private ObjectiveRewardBundle _hp50Rewards = new ObjectiveRewardBundle();

        [SerializeField]
        private ObjectiveRewardBundle _perfectRewards = new ObjectiveRewardBundle();

        public LevelDefinition LevelDefinition => _levelDefinition;
        public string SceneName => _sceneName;
        public string LevelId => _levelDefinition != null ? _levelDefinition.LevelId : null;
        public Sprite MenuPreviewSprite => _menuPreviewSprite;
        public Sprite MenuPreviewSpriteLocked => _menuPreviewSpriteLocked != null ? _menuPreviewSpriteLocked : _menuPreviewSprite;
        public ObjectiveRewardBundle ClearRewards => _clearRewards;
        public ObjectiveRewardBundle Hp50Rewards => _hp50Rewards;
        public ObjectiveRewardBundle PerfectRewards => _perfectRewards;

        public ObjectiveRewardBundle GetObjectiveRewards(LevelObjectiveKind kind)
        {
            switch (kind)
            {
                case LevelObjectiveKind.Clear:
                    return _clearRewards;
                case LevelObjectiveKind.Hp50:
                    return _hp50Rewards;
                case LevelObjectiveKind.Perfect:
                    return _perfectRewards;
                default:
                    return null;
            }
        }
    }
}
