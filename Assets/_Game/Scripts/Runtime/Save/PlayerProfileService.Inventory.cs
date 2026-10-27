using UnityEngine;

namespace AlienDefense.Save
{
    /// <summary>Keys of the inventory "new" flags, shared by the grant paths and the inventory UI.</summary>
    public static class InventoryNewKeys
    {
        public const string EquipmentPrefix = "eq:";
        public const string ArtifactPrefix = "art:";
        public const string ItemPrefix = "item:";

        public static string Equipment(string id) => EquipmentPrefix + id;
        public static string Artifact(string id, AlienDefense.Meta.MetaItemRarity rarity) => ArtifactPrefix + id + ":" + (int)rarity;
        public static string Item(string id) => ItemPrefix + id;
    }

    /// <summary>Inventory additions: equipment levels and the "new item" flags behind the red badges. Grants mark
    /// their entry new (GrantEquipment / AddArtifact / AddItem); the inventory clears a flag once it is seen.</summary>
    public sealed partial class PlayerProfileService
    {
        private const int MaxInventoryNewFlags = 128;

        /// <summary>Sets a piece's level (the caller checks the tier's max and pays first).</summary>
        public bool TrySetEquipmentLevel(string itemId, int level)
        {
            EquipmentSaveData entry = FindEquipment(itemId);
            if (entry == null || level < 1)
            {
                return false;
            }

            entry.Level = level;
            RequestImmediateSave();
            return true;
        }

        public bool HasInventoryNew(string key)
        {
            return !string.IsNullOrEmpty(key) && _data.InventoryNew != null && _data.InventoryNew.Contains(key);
        }

        public bool HasAnyInventoryNew(string prefix)
        {
            if (_data.InventoryNew == null)
            {
                return false;
            }

            for (int i = 0; i < _data.InventoryNew.Count; i++)
            {
                if (_data.InventoryNew[i] != null && _data.InventoryNew[i].StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void MarkInventorySeen(string key)
        {
            if (_data.InventoryNew != null && _data.InventoryNew.Remove(key))
            {
                RequestImmediateSave();
            }
        }

        /// <summary>Clears every flag with a prefix (a tab the player has looked through).</summary>
        public void MarkInventorySeenByPrefix(string prefix)
        {
            if (_data.InventoryNew != null && _data.InventoryNew.RemoveAll(k => k != null && k.StartsWith(prefix, System.StringComparison.Ordinal)) > 0)
            {
                RequestImmediateSave();
            }
        }

        /// <summary>Flags an entry new without its own save (the grant that calls it saves).</summary>
        private void MarkInventoryNewSilently(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            _data.InventoryNew ??= new System.Collections.Generic.List<string>();
            if (_data.InventoryNew.Contains(key))
            {
                return;
            }

            _data.InventoryNew.Add(key);
            if (_data.InventoryNew.Count > MaxInventoryNewFlags)
            {
                _data.InventoryNew.RemoveAt(0);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugClearEquipmentAndArtifacts()
        {
            _data.Equipment?.Clear();
            _data.Artifacts?.Clear();
            _data.InventoryNew?.Clear();
            RequestImmediateSave();
        }
#endif
    }
}
