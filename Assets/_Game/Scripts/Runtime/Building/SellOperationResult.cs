namespace AlienDefense.Building
{
    /// <summary>Immutable outcome of a sell attempt: status, sell value paid out, and resource left.</summary>
    public readonly struct SellOperationResult
    {
        public readonly SellResult Status;
        public readonly int SellValue;
        public readonly int RemainingResource;

        public SellOperationResult(SellResult status, int sellValue, int remainingResource)
        {
            Status = status;
            SellValue = sellValue;
            RemainingResource = remainingResource;
        }

        public bool IsSuccess => Status == SellResult.Success;
    }
}
