using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Daily-login-streak popup. Dumb view: shows whatever streak/reward state it's given and forwards
    /// Claim/Close clicks — never reads/writes save data itself.</summary>
    public sealed class DailyRewardPanelView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _streakDayText;

        [SerializeField]
        private TMP_Text _rewardPreviewText;

        [SerializeField]
        private Button _claimButton;

        [SerializeField]
        private TMP_Text _claimButtonLabel;

        [SerializeField]
        private Button _closeButton;

        public event Action ClaimClicked;
        public event Action CloseClicked;

        private void Awake()
        {
            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(HandleClaimClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetAvailable(int nextStreakDay, int coinReward, int gemReward)
        {
            if (_streakDayText != null)
            {
                _streakDayText.text = $"Day {nextStreakDay}/7";
            }

            if (_rewardPreviewText != null)
            {
                _rewardPreviewText.text = gemReward > 0 ? $"+{coinReward} Coin, +{gemReward} Gem" : $"+{coinReward} Coin";
            }

            if (_claimButtonLabel != null)
            {
                _claimButtonLabel.text = "Claim";
            }

            if (_claimButton != null)
            {
                _claimButton.interactable = true;
            }
        }

        public void SetAlreadyClaimedToday(int currentStreakDay)
        {
            if (_streakDayText != null)
            {
                _streakDayText.text = $"Day {currentStreakDay}/7";
            }

            if (_rewardPreviewText != null)
            {
                _rewardPreviewText.text = "Already claimed today, come back tomorrow.";
            }

            if (_claimButtonLabel != null)
            {
                _claimButtonLabel.text = "Claimed";
            }

            if (_claimButton != null)
            {
                _claimButton.interactable = false;
            }
        }

        private void HandleClaimClicked()
        {
            ClaimClicked?.Invoke();
        }

        private void HandleCloseClicked()
        {
            CloseClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveListener(HandleClaimClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }
    }
}
