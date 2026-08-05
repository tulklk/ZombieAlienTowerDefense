namespace AlienDefense.Building
{
    /// <summary>Outcome of one TowerSellService.TrySell call.</summary>
    public enum SellResult
    {
        Success = 0,
        InvalidTower = 1,
        GameNotPlaying = 2,
        InvalidNode = 3,
        FactoryUnavailable = 4
    }
}
