using System.Collections.Generic;
using AlienDefense.Core;
using AlienDefense.Data;
using AlienDefense.Economy;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Whenever PlayerLevelProgressionService.LevelChanged fires, asks PlayerSkillService for 3 random
    /// skills and shows them via SkillChoiceView; picking a card calls PlayerSkillService.Upgrade for that one
    /// and hides the popup. Pauses GameSpeed (not GameFlow - this is a mid-gameplay choice, not the manual Pause
    /// menu) while a choice is pending so waves/enemies don't advance under the player while they decide.</summary>
    public sealed class SkillChoicePresenter : MonoBehaviour
    {
        [SerializeField]
        private SkillChoiceView _view;

        private PlayerLevelProgressionService _levelProgression;
        private PlayerSkillService _skills;
        private GameSpeedController _gameSpeed;
        private readonly List<SkillType> _currentOffer = new List<SkillType>(3);

        public void Initialize(PlayerLevelProgressionService levelProgression, PlayerSkillService skills, GameSpeedController gameSpeed)
        {
            Unsubscribe();

            _levelProgression = levelProgression;
            _skills = skills;
            _gameSpeed = gameSpeed;

            if (_levelProgression != null)
            {
                _levelProgression.LevelChanged += HandleLevelChanged;
            }

            if (_view != null)
            {
                _view.CardClicked += HandleCardClicked;
                _view.Hide();
            }
        }

        private void HandleLevelChanged(int newLevel)
        {
            if (_skills == null || _view == null)
            {
                return;
            }

            _currentOffer.Clear();
            _currentOffer.AddRange(_skills.GetRandomOffer(3));

            if (_currentOffer.Count == 0)
            {
                return; // every skill already maxed - nothing to offer
            }

            var cards = new SkillCardData[_currentOffer.Count];
            for (int i = 0; i < _currentOffer.Count; i++)
            {
                SkillType type = _currentOffer[i];
                SkillDefinition definition = _skills.GetDefinition(type);
                if (definition == null)
                {
                    continue;
                }

                int nextRank = Mathf.Min(_skills.GetRank(type) + 1, definition.MaxRank);
                SkillRankData rankData = definition.GetRank(nextRank);
                // "★" isn't in this project's LiberationSans SDF font asset (renders as a fallback box) - "Sao"
                // (Vietnamese for "star") instead, matching how every other skill/rank label in this project is
                // plain text, not a symbol.
                cards[i] = new SkillCardData(definition.DisplayName, rankData.Description, $"Sao {nextRank}/{definition.MaxRank}", definition.Icon);
            }

            _view.Show(cards);
            _gameSpeed?.Pause();
        }

        private void HandleCardClicked(int index)
        {
            if (index < 0 || index >= _currentOffer.Count || _skills == null)
            {
                return;
            }

            SkillType chosen = _currentOffer[index];
            _skills.Upgrade(chosen);
            _currentOffer.Clear();

            _view?.Hide();
            _gameSpeed?.Resume();
        }

        private void Unsubscribe()
        {
            if (_levelProgression != null)
            {
                _levelProgression.LevelChanged -= HandleLevelChanged;
            }

            if (_view != null)
            {
                _view.CardClicked -= HandleCardClicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
