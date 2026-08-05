namespace AlienDefense.Towers
{
    /// <summary>Outcome of one TowerUpgradeService.TryUpgrade call.</summary>
    public enum UpgradeResult
    {
        Success = 0,
        InvalidTower = 1,
        GameNotPlaying = 2,
        AlreadyMaxLevel = 3,
        InvalidConfiguration = 4,
        NotEnoughResource = 5,
        PaymentFailed = 6,
        ApplyFailed = 7
    }
}
