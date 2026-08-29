namespace AlienDefense.Pickups
{
    /// <summary>Lifecycle of one EnergyPickup instance while it may be tractor-beamed.</summary>
    public enum EnergyPickupState
    {
        Idle = 0,
        Pulling = 1,
        Lifting = 2,
        Collected = 3
    }
}
