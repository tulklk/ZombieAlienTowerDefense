using AlienDefense.Core;
using AlienDefense.Meta;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Editor-only Inventory test controls (Play mode): grant every equipment piece, a mergeable stack of
    /// artifacts, containers and crafting materials, or wipe equipment / artifacts. Never in a build.</summary>
    public static class InventoryDebugMenu
    {
        private const string Root = "AlienDefense/Debug/Inventory/";

        [MenuItem(Root + "Grant All Equipment")]
        private static void GrantEquipment() => With(inv =>
        {
            foreach (EquipmentDefinition d in inv.Catalog.Equipment)
            {
                if (d != null) inv.DebugGrantEquipment(d.Id);
            }
        });

        [MenuItem(Root + "Grant 5 Common Artifacts Each")]
        private static void GrantArtifacts() => With(inv =>
        {
            foreach (ArtifactDefinition a in inv.Catalog.Artifacts)
            {
                if (a != null) inv.DebugGrantArtifacts(a.Id, a.BaseRarity, 5);
            }
        });

        [MenuItem(Root + "Grant 10 Resource Containers")]
        private static void GrantContainers() => With(inv => inv.DebugGrantItem("resource_container", 10));

        [MenuItem(Root + "Grant Crafting Materials")]
        private static void GrantMaterials() => With(inv =>
        {
            inv.DebugGrantItem("microcircuit", 200);
            inv.DebugGrantItem("volt_drip", 1000);
            inv.DebugGrantItem("reactor_blueprint", 50);
            inv.DebugGrantItem("anti_gravity_blueprint", 50);
            inv.DebugGrantItem("rare_atom", 50);
        });

        [MenuItem(Root + "Clear Equipment And Artifacts")]
        private static void Clear()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[InventoryDebugMenu] Enter Play mode first.");
                return;
            }

            var root = Object.FindFirstObjectByType<ApplicationCompositionRoot>();
            root?.Services?.PlayerProfileService?.DebugClearEquipmentAndArtifacts();
            root?.Services?.Inventory?.NotifyChanged();
        }

        private static void With(System.Action<InventoryService> action)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[InventoryDebugMenu] Enter Play mode first.");
                return;
            }

            InventoryService inventory = Object.FindFirstObjectByType<ApplicationCompositionRoot>()?.Services?.Inventory;
            if (inventory == null || !inventory.IsReady)
            {
                Debug.LogWarning("[InventoryDebugMenu] Inventory not ready.");
                return;
            }

            action(inventory);
            Debug.Log("[InventoryDebugMenu] Done.");
        }
    }
}
