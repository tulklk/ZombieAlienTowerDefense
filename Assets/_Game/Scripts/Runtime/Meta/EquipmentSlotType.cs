namespace AlienDefense.Meta
{
    /// <summary>The six sockets around the UFO preview, one row of the equipment category bar each.
    ///
    /// The player wears at most one piece per slot. Values are serialized into definition assets and into the
    /// save file, so they must never be renumbered.</summary>
    public enum EquipmentSlotType
    {
        Controls = 0,
        Seat = 1,
        Core = 2,
        CargoBay = 3,
        AntiGravity = 4,
        Engine = 5
    }
}
