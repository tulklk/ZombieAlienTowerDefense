namespace AlienDefense.Building
{
    /// <summary>Domain state of a BuildNode. "Selected/highlighted" is a transient visual concept, not a state here.</summary>
    public enum BuildNodeState
    {
        Available = 0,
        Occupied = 1,
        Disabled = 2
    }
}
