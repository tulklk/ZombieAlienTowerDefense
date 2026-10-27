using System;

namespace AlienDefense.Shop
{
    /// <summary>Hook for systems that should react to paid privileges (ads, a future Boss Path, battle perks).
    /// None of those systems exist yet, so nothing consumes this today; ShopService implements it, and
    /// PlayerProfileService.IsVipActive / IsShopPrivilegeActive answer the same questions from any scene.</summary>
    public interface IVipBenefitProvider
    {
        bool IsVipActive(DateTime nowUtc);

        /// <summary>Whether a privilege card (by product id, e.g. "alien.monthly_combat") is running.</summary>
        bool IsPrivilegeActive(string productId, DateTime nowUtc);
    }
}
