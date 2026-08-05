using UnityEngine;

namespace AlienDefense.Towers
{
    /// <summary>Computes a tower's sell value: floor(total invested resource x sell percentage).</summary>
    public static class SellValueCalculator
    {
        public static int Calculate(int totalInvestedResource, float sellPercentage)
        {
            if (totalInvestedResource <= 0 || sellPercentage <= 0f)
            {
                return 0;
            }

            return Mathf.FloorToInt(totalInvestedResource * sellPercentage);
        }
    }
}
