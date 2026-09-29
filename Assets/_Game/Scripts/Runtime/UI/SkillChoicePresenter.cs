using System;
using System.Collections;
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

        // Parallel to _acquiredIcons: which of those learned skills have hit their last rank. Drives the boost
        // frame on the board slots.
        private readonly List<bool> _acquiredMaxed = new List<bool>(5);

        // Set the instant a pick is committed and cleared when the popup closes. Swallows repeat taps and
        // Refresh presses for the length of the celebration.
        private bool _isPlayingSelection;
        private Coroutine _selectionRoutine;

        public void Initialize(PlayerLevelProgressionService levelProgression, PlayerSkillService skills, GameSpeedController gameSpeed)
        {
            Unsubscribe();

            _levelProgression = levelProgression;
            _skills = skills;
            _gameSpeed = gameSpeed;

            _acquiredOrder.Clear();

            if (_selectionRoutine != null && isActiveAndEnabled)
            {
                StopCoroutine(_selectionRoutine);
            }

            _selectionRoutine = null;
            _isPlayingSelection = false;

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
            if (_isPlayingSelection)
            {
                return; // a pick is still celebrating; re-rolling the cards underneath it would look broken
            }

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

            if (_currentOffer.Count == 0 || !HasUpgradableOffer())
            {
                // Nothing here can actually be upgraded. GetRandomOffer falls back to handing out maxed skills
                // once nothing else is left, and picking one of those is a no-op in PlayerSkillService - which
                // would leave the popup open on a choice that can never resolve, with the game paused behind it.
                _currentOffer.Clear();
                return false;
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
            _view.SetAcquiredSkills(_acquiredIcons, _acquiredMaxed);
            _view.Show(cards, FindRecommendedIndex());
            return true;
        }

        /// <summary>True when at least one card in the current offer would actually raise a rank.</summary>
        private bool HasUpgradableOffer()
        {
            for (int i = 0; i < _currentOffer.Count; i++)
            {
                if (!_skills.IsMaxed(_currentOffer[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Which of the three offered cards to badge as the suggested pick: the one the player has already
        /// put the most ranks into, so the badge points at finishing a skill rather than starting a new one. A
        /// maxed skill is never offered in the first place (see PlayerSkillService.GetRandomOffer), so no extra
        /// guard is needed here. Ties go to the leftmost card, which keeps the badge from jumping around between
        /// re-rolls that happen to produce the same ranks.</summary>
        private int FindRecommendedIndex()
        {
            if (_skills == null || _currentOffer.Count == 0)
            {
                return -1;
            }

            int bestIndex = 0;
            int bestRank = _skills.GetRank(_currentOffer[0]);

            for (int i = 1; i < _currentOffer.Count; i++)
            {
                int rank = _skills.GetRank(_currentOffer[i]);
                if (rank > bestRank)
                {
                    bestRank = rank;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void RefreshAcquiredIcons()
        {
            SyncAcquiredFromService();

            _acquiredIcons.Clear();
            _acquiredMaxed.Clear();
            for (int i = 0; i < _acquiredOrder.Count; i++)
            {
                SkillType type = _acquiredOrder[i];
                SkillDefinition definition = _skills?.GetDefinition(type);
                if (definition != null)
                {
                    _acquiredIcons.Add(definition.Icon);
                    _acquiredMaxed.Add(_skills.IsMaxed(type));
                }
            }
        }

        private void HandleCardClicked(int index)
        {
            if (_isPlayingSelection || index < 0 || index >= _currentOffer.Count || _skills == null)
            {
                return; // a second tap while the pick is animating must not upgrade again
            }

            SkillType chosen = _currentOffer[index];

            // PlayerSkillService.Upgrade returns void and is a no-op past MaxRank (or for a skill it doesn't
            // know), so "did it work?" is the rank actually moving. Reading it back rather than assuming keeps
            // the celebration from firing on an upgrade that never happened, and gives the view the committed
            // star count instead of whatever the card was showing.
            int rankBefore = _skills.GetRank(chosen);
            _skills.Upgrade(chosen);
            int rankAfter = _skills.GetRank(chosen);

            if (rankAfter <= rankBefore)
            {
                return; // upgrade rejected - leave the popup exactly as it was, game still paused
            }

            if (rankBefore == 0)
            {
                _acquiredOrder.Add(chosen);
            }

            _currentOffer.Clear();

            SkillDefinition definition = _skills.GetDefinition(chosen);
            int maxRank = definition != null ? definition.MaxRank : 0;

            if (_view == null)
            {
                _gameSpeed?.Resume();
                return;
            }

            _isPlayingSelection = true;
            float hold = _view.PlaySelection(index, rankAfter, maxRank);

            if (!isActiveAndEnabled)
            {
                // No coroutine host available - close immediately rather than leaving the game paused forever.
                CompleteSelection();
                return;
            }

            _selectionRoutine = StartCoroutine(CloseAfterSelection(hold));
        }

        /// <summary>Waits out the pick animation, then runs the same close-and-resume this presenter always did.
        /// Realtime, not scaled: GameSpeed is paused while the popup is open, so a scaled wait would never
        /// finish and the popup would hang on screen.</summary>
        private IEnumerator CloseAfterSelection(float seconds)
        {
            if (seconds > 0f)
            {
                yield return new WaitForSecondsRealtime(seconds);
            }

            _selectionRoutine = null;
            CompleteSelection();
        }

        private void CompleteSelection()
        {
            _isPlayingSelection = false;
            _view?.Hide();
            _gameSpeed?.Resume();
        }

        /// <summary>Re-rolls the 3 offered skills in place. Deliberately does NOT resume GameSpeed: the player is
        /// still mid-choice, just looking at a different hand.</summary>
        private void HandleRefreshClicked()
        {
            if (_isPlayingSelection || _currentOffer.Count == 0)
            {
                return; // nothing pending, or a pick is already animating - Refresh does nothing
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
