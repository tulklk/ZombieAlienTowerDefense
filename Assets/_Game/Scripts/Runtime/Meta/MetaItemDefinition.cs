using UnityEngine;

namespace AlienDefense.Meta
{
    [CreateAssetMenu(fileName = "MetaItem_", menuName = "AlienDefense/Meta/Item Definition")]
    public sealed class MetaItemDefinition : ScriptableObject
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
        private MetaItemRarity _rarity = MetaItemRarity.Common;

        [SerializeField]
        private Color _headerColor = new Color(0.16f, 0.72f, 0.95f, 1f);

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public MetaItemRarity Rarity => _rarity;
        public Color HeaderColor => _headerColor;
    }
}
