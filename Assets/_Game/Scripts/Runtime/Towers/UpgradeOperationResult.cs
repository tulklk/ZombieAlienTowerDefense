namespace AlienDefense.Towers
{
    /// <summary>Immutable outcome of an upgrade attempt: status, resulting level index, and resource left.</summary>
    public readonly struct UpgradeOperationResult
    {
        public readonly UpgradeResult Status;
        public readonly int NewLevelIndex;
        public readonly int RemainingResource;

        public UpgradeOperationResult(UpgradeResult status, int newLevelIndex, int remainingResource)
        {
            Status = status;
            NewLevelIndex = newLevelIndex;
            RemainingResource = remainingResource;
        }

        public bool IsSuccess => Status == UpgradeResult.Success;
    }
}
