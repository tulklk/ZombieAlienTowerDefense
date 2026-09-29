namespace AlienDefense.Meta
{
    /// <summary>Shared rarity ladder for every meta item - materials, containers, equipment and artifacts.
    ///
    /// The numbers are serialized into save files and into MetaItemDefinition assets, so existing values must
    /// never be renumbered. Legendary was appended at the top for the equipment craft ladder (Epic -> Legendary);
    /// every asset authored before it keeps the rarity it already had.</summary>
    public enum MetaItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }
}
