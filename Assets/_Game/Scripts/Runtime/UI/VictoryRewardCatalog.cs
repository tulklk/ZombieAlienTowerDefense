using System;
using AlienDefense.Progression;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Presentation for each reward kind - icon, name, the line the detail popup shows, and the popup's
    /// header colour. Keeping it in one asset means adding a reward type is a data edit plus one enum entry, not a
    /// UI change.</summary>
    [CreateAssetMenu(fileName = "VictoryRewardCatalog", menuName = "AlienDefense/UI/Victory Reward Catalog")]
    public sealed class VictoryRewardCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public VictoryRewardType Type;
            public string DisplayName;
            [TextArea(1, 3)]
            public string Description;
            public Sprite Icon;
            [Tooltip("Header colour of the detail popup for this reward.")]
            public Color HeaderColor;

            [Tooltip("The icon art already draws its own card frame, so the grid tile shows no coloured frame behind it.")]
            public bool IconHasOwnFrame;
        }

        [SerializeField]
        private Entry[] _entries = Array.Empty<Entry>();

        [SerializeField]
        [Tooltip("Used when a reward type has no entry yet, so a new type never shows an empty popup.")]
        private Color _fallbackHeaderColor = new Color(0.16f, 0.52f, 0.86f, 1f);

        public bool TryGet(VictoryRewardType type, out Entry entry)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Type == type)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            entry = new Entry
            {
                Type = type,
                DisplayName = type.ToString(),
                Description = string.Empty,
                Icon = null,
                HeaderColor = _fallbackHeaderColor,
            };
            return false;
        }
    }
}
