namespace AlienDefense.UI.Inventory
{
    /// <summary>The three sections of the inventory screen. Separate from MenuTab, which is the bottom
    /// navigation's five destinations - this one only ever selects between the panels inside Inventory.</summary>
    public enum InventoryTab
    {
        Equipment = 0,
        Artifacts = 1,
        Materials = 2
    }
}
