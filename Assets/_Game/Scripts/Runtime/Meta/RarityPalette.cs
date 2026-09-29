using System;
using UnityEngine;

namespace AlienDefense.Meta
{
    /// <summary>Rarity to colour and frame art, in one asset instead of a colour literal in every card script.
    ///
    /// This exists because the project had no rarity styling table: MetaItemDefinition carries a per-item
    /// HeaderColor, which is the item's own accent and deliberately not the same thing as "what an Epic frame
    /// looks like". Views ask this asset, so re-theming a rarity is one asset edit rather than a code hunt.</summary>
    [CreateAssetMenu(fileName = "RarityPalette", menuName = "AlienDefense/Meta/Rarity Palette")]
    public sealed class RarityPalette : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField]
            private MetaItemRarity _rarity = MetaItemRarity.Common;

            [SerializeField]
            private Color _frameColor = Color.white;

            [SerializeField]
            [Tooltip("Colour for the rarity's NAME text on the craft screen.")]
            private Color _labelColor = Color.white;

            [SerializeField]
            [Tooltip("Optional. Leave empty to tint the shared frame sprite with Frame Color instead.")]
            private Sprite _frameSprite;

            [SerializeField]
            private string _displayName = "Common";

            public MetaItemRarity Rarity => _rarity;
            public Color FrameColor => _frameColor;
            public Color LabelColor => _labelColor;
            public Sprite FrameSprite => _frameSprite;
            public string DisplayName => _displayName;
        }

        [SerializeField]
        private Entry[] _entries = Array.Empty<Entry>();

        [SerializeField]
        [Tooltip("Used when a rarity has no entry, so a newly added rarity degrades to something visible rather " +
            "than to an invisible frame.")]
        private Color _fallbackColor = new Color(0.6f, 0.6f, 0.65f, 1f);

        public Color GetFrameColor(MetaItemRarity rarity)
        {
            Entry entry = Find(rarity);
            return entry != null ? entry.FrameColor : _fallbackColor;
        }

        public Color GetLabelColor(MetaItemRarity rarity)
        {
            Entry entry = Find(rarity);
            return entry != null ? entry.LabelColor : _fallbackColor;
        }

        public Sprite GetFrameSprite(MetaItemRarity rarity)
        {
            Entry entry = Find(rarity);
            return entry != null ? entry.FrameSprite : null;
        }

        public string GetDisplayName(MetaItemRarity rarity)
        {
            Entry entry = Find(rarity);
            return entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.DisplayName : rarity.ToString();
        }

        private Entry Find(MetaItemRarity rarity)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && _entries[i].Rarity == rarity)
                    {
                        return _entries[i];
                    }
                }
            }

            return null;
        }
    }
}
