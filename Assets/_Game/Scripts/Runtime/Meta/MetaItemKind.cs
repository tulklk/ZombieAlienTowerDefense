namespace AlienDefense.Meta
{
    /// <summary>Which inventory section a MetaItemDefinition belongs to.
    ///
    /// Material is 0 on purpose: every item asset authored before this field existed deserializes to 0, which is
    /// exactly what those assets are (upgrade cards, blueprints, research resources). Nothing needs re-authoring.</summary>
    public enum MetaItemKind
    {
        Material = 0,

        /// <summary>Loot boxes and chests - shown in the Containers strip above the material grid.</summary>
        Container = 1
    }
}
