using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Dumb view for the center level-selection area: title/status text, Previous/Next/More buttons,
    /// and the objectives panel. Never decides which level is selected or what's unlocked — only displays
    /// whatever MainMenuLevelSelectionPresenter configures and forwards clicks as events.</summary>
    public sealed class MainMenuLevelSelectionView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _titleText;

        [SerializeField]
        private TMP_Text _statusText;

        [SerializeField]
        private Button _previousButton;

        [SerializeField]
        private Button _nextButton;

        [SerializeField]
        [Tooltip("Optional. Opens MainMenuMorePanel once one exists (see MainMenuPresenter).")]
        private Button _moreButton;

        [SerializeField]
        private LevelObjectivePanelView _objectivesPanel;

        public event Action PreviousClicked;
        public event Action NextClicked;
        public event Action MoreClicked;
        public event Action<AlienDefense.Meta.LevelObjectiveKind> ObjectiveClaimClicked;

        private void Awake()
        {
            if (_previousButton != null)
            {
                _previousButton.onClick.AddListener(HandlePreviousClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.AddListener(HandleNextClicked);
            }

            if (_moreButton != null)
            {
                _moreButton.onClick.AddListener(HandleMoreClicked);
            }

            if (_objectivesPanel != null)
            {
                _objectivesPanel.ClaimClicked += HandleObjectiveClaimClicked;
            }
        }

        public void SetTitle(string title)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }
        }

        public void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.richText = true;
                _statusText.text = status;
            }
        }

        public void SetNavigationAvailable(bool hasPrevious, bool hasNext)
        {
            if (_previousButton != null)
            {
                _previousButton.gameObject.SetActive(hasPrevious);
            }

            if (_nextButton != null)
            {
                _nextButton.gameObject.SetActive(hasNext);
            }
        }

        public void SetObjectives(LevelObjectivePresentation[] objectives)
        {
            _objectivesPanel?.SetObjectives(objectives);
        }

        private void HandlePreviousClicked()
        {
            PreviousClicked?.Invoke();
        }

        private void HandleNextClicked()
        {
            NextClicked?.Invoke();
        }

        private void HandleMoreClicked()
        {
            MoreClicked?.Invoke();
        }

        private void HandleObjectiveClaimClicked(AlienDefense.Meta.LevelObjectiveKind kind)
        {
            ObjectiveClaimClicked?.Invoke(kind);
        }

        private void OnDestroy()
        {
            if (_previousButton != null)
            {
                _previousButton.onClick.RemoveListener(HandlePreviousClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveListener(HandleNextClicked);
            }

            if (_moreButton != null)
            {
                _moreButton.onClick.RemoveListener(HandleMoreClicked);
            }

            if (_objectivesPanel != null)
            {
                _objectivesPanel.ClaimClicked -= HandleObjectiveClaimClicked;
            }
        }
    }
}
