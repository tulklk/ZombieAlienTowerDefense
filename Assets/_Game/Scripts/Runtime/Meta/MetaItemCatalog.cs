using System;
using UnityEngine;

namespace AlienDefense.Meta
{
    [CreateAssetMenu(fileName = "MetaItemCatalog", menuName = "AlienDefense/Meta/Item Catalog")]
    public sealed class MetaItemCatalog : ScriptableObject
    {
        [SerializeField]
        private MetaItemDefinition[] _items = Array.Empty<MetaItemDefinition>();

        public bool TryGet(string itemId, out MetaItemDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(itemId) || _items == null)
            {
                definition = null;
                return false;
            }

            for (int i = 0; i < _items.Length; i++)
            {
                MetaItemDefinition item = _items[i];
                if (item != null && item.Id == itemId)
                {
                    definition = item;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
