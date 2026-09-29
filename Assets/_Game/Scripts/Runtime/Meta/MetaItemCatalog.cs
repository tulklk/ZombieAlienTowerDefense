using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienDefense.Meta
{
    [CreateAssetMenu(fileName = "MetaItemCatalog", menuName = "AlienDefense/Meta/Item Catalog")]
    public sealed class MetaItemCatalog : ScriptableObject
    {
        [SerializeField]
        private MetaItemDefinition[] _items = Array.Empty<MetaItemDefinition>();

        [SerializeField]
        [Tooltip("Every equipment piece in the game. The inventory builds its grid and its slots from this, so a " +
            "piece missing here simply does not exist as far as the UI is concerned.")]
        private EquipmentDefinition[] _equipment = Array.Empty<EquipmentDefinition>();

        [SerializeField]
        private ArtifactDefinition[] _artifacts = Array.Empty<ArtifactDefinition>();

        /// <summary>Read-only views, so callers can enumerate without the catalog handing out its arrays.</summary>
        public IReadOnlyList<MetaItemDefinition> Items => _items;
        public IReadOnlyList<EquipmentDefinition> Equipment => _equipment;
        public IReadOnlyList<ArtifactDefinition> Artifacts => _artifacts;

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

        public bool TryGetEquipment(string equipmentId, out EquipmentDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(equipmentId) && _equipment != null)
            {
                for (int i = 0; i < _equipment.Length; i++)
                {
                    EquipmentDefinition item = _equipment[i];
                    if (item != null && item.Id == equipmentId)
                    {
                        definition = item;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetArtifact(string artifactId, out ArtifactDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(artifactId) && _artifacts != null)
            {
                for (int i = 0; i < _artifacts.Length; i++)
                {
                    ArtifactDefinition item = _artifacts[i];
                    if (item != null && item.Id == artifactId)
                    {
                        definition = item;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }
    }
}
