using System;
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

        // Skills the player has actually learned (rank >= 1), in the order they were learned - that order is
        // what fills the "Ability choice" board's slots left to right, so the board reads as a history of this
        // run's picks rather than a fixed enum listing. Never contains a rank-0 skill, which is exactly what
        // makes an un-learned skill show nothing at all (see SkillChoiceView.SetAcquiredSkills).
        private readonly List<SkillType> _acquiredOrder = new List<SkillType>(5);
        private readonly List<Sprite> _acquiredIcons = new List<Sprite>(5);

        public void Initialize(PlayerLevelProgressionService levelProgression, PlayerSkillService skills, GameSpeedController gameSpeed)
        {
            Unsubscribe();

            _levelProgression = levelProgression;
            _skills = skills;
            _gameSpeed = gameSpeed;

            _acquiredOrder.Clear();

            if (_levelProgression != null)
            {
                _levelProgression.LevelChanged += HandleLevelChanged;
            }

            if (_view != null)
            {
                _view.CardClicked += HandleCardClicked;
                _view.RefreshClicked += HandleRefreshClicked;
                _view.Hide();
            }
        }

        /// <summary>Appends anything the service already counts as learned (rank >= 1) that this presenter hasn't
        /// recorded yet, in enum order. Picks made through this popup are already appended in real pick order by
        /// HandleCardClicked, so this only ever catches up on ranks raised somewhere else (or ranks that existed
        /// before Initialize) - meaning the board can never silently miss a learned skill just because the rank
        /// didn't come from a card click.</summary>
        private void SyncAcquiredFromService()
        {
            if (_skills == null)
            {
                return;
            }

            foreach (SkillType type in Enum.GetValues(typeof(SkillType)))
            {
                if (_skills.GetRank(type) >= 1 && !_acquiredOrder.Contains(type))
                {
                    _acquiredOrder.Add(type);
                }
            }
        }

        private void HandleLevelChanged(int newLevel)
        {
            if (!TryBuildOffer())
            {
                return;
            }

            _gameSpeed?.Pause();
        }

        /// <summary>Rolls a fresh 3-skill offer and pushes it (plus the board's learned-skill icons) to the view.
        /// Returns false when there is nothing left to offer, in which case nothing is shown and the game is
        /// deliberately left running.</summary>
        private bool TryBuildOffer()
        {
            if (_skills == null || _view == null)
            {
                return false;
            }

            _currentOffer.Clear();
            _currentOffer.AddRange(_skills.GetRandomOffer(3));

            if (_currentOffer.Count == 0)
            {
                return false; // every skill already maxed - nothing to offer
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
                cards[i] = new SkillCardData(definition.DisplayName, rankData.Description, nextRank, definition.MaxRank, definition.Icon);
            }

            RefreshAcquiredIcons();
            _view.SetAcquiredSkills(_acquiredIcons);
            _view.Show(cards);
            return true;
        }

        private void RefreshAcquiredIcons()
        {
            SyncAcquiredFromService();

            _acquiredIcons.Clear();
            for (int i = 0; i < _acquiredOrder.Count; i++)
            {
                SkillDefinition definition = _skills?.GetDefinition(_acquiredOrder[i]);
                if (definition != null)
                {
                    _acquiredIcons.Add(definition.Icon);
                }
            }
        }

        private void HandleCardClicked(int index)
        {
            if (index < 0 || index >= _currentOffer.Count || _skills == null)
            {
                return;
            }

            SkillType chosen = _currentOffer[index];
            bool isFirstRank = _skills.GetRank(chosen) == 0;

            _skills.Upgrade(chosen);

            if (isFirstRank && _skills.GetRank(chosen) >= 1)
            {
                _acquiredOrder.Add(chosen);
            }

            _currentOffer.Clear();

            _view?.Hide();
            _gameSpeed?.Resume();
        }

        /// <summary>Re-rolls the 3 offered skills in place. Deliberately does NOT resume GameSpeed: the player is
        /// still mid-choice, just looking at a different hand.</summary>
        private void HandleRefreshClicked()
        {
            if (_currentOffer.Count == 0)
            {
                return; // nothing pending - Refresh outside an open offer does nothing
            }

            TryBuildOffer();
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
                _view.RefreshClicked -= HandleRefreshClicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
