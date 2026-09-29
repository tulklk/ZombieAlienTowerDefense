using UnityEngine;

namespace AlienDefense.Meta
{
    /// <summary>One artifact family. Unlike equipment an artifact has no level and no slot - it is a stack of
    /// copies at a given rarity, and merging trades several copies of one rarity for a single copy of the next.
    ///
    /// The merge size lives here rather than in the merge service so different artifact families can cost
    /// different amounts without touching code.</summary>
    [CreateAssetMenu(fileName = "Artifact_", menuName = "AlienDefense/Meta/Artifact Definition")]
    public sealed class ArtifactDefinition : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        [TextArea(2, 4)]
        private string _description;

        [SerializeField]
        private Sprite _icon;

        [SerializeField]
        private MetaItemRarity _baseRarity = MetaItemRarity.Common;

        [SerializeField]
        [Tooltip("Merging stops here. A stack at this rarity is never offered for merging.")]
        private MetaItemRarity _maxRarity = MetaItemRarity.Legendary;

        [SerializeField, Min(2)]
        [Tooltip("Copies consumed per merge. The design default is 5; set it per family rather than hard-coding.")]
        private int _mergeInputCount = 5;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public MetaItemRarity BaseRarity => _baseRarity;
        public MetaItemRarity MaxRarity => _maxRarity;
        public int MergeInputCount => Mathf.Max(2, _mergeInputCount);

        public bool CanMergeFrom(MetaItemRarity rarity)
        {
            return rarity < _maxRarity;
        }
    }
}
