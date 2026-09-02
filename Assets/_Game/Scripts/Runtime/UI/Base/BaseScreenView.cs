using TMPro;
using UnityEngine;

namespace AlienDefense.UI.Base
{
    public sealed class BaseScreenView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _completedLevelsText;

        [SerializeField]
        private TMP_Text _totalStarsText;

        [SerializeField]
        private TMP_Text _unlockedTowersText;

        [SerializeField]
        private TMP_Text _vipTierText;

        [SerializeField]
        private TMP_Text _coinText;

        [SerializeField]
        private TMP_Text _gemText;

        public void SetStats(int completedLevels, int totalStars, int unlockedTowers, int vipTier, string coinDisplay, string gemDisplay)
        {
            if (_completedLevelsText != null)
            {
                _completedLevelsText.text = $"Màn đã hoàn thành: {completedLevels}";
            }

            if (_totalStarsText != null)
            {
                _totalStarsText.text = $"Tổng sao: {totalStars}";
            }

            if (_unlockedTowersText != null)
            {
                _unlockedTowersText.text = $"Tower đã mở: {unlockedTowers}";
            }

            if (_vipTierText != null)
            {
                _vipTierText.text = vipTier > 0 ? $"VIP: {vipTier}" : "VIP: Chưa có";
            }

            if (_coinText != null)
            {
                _coinText.text = $"Coin: {coinDisplay}";
            }

            if (_gemText != null)
            {
                _gemText.text = $"Gem: {gemDisplay}";
            }
        }
    }
}
