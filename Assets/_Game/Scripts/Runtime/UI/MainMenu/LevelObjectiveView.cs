using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One reward-chest slot. Dumb view: displays whatever LevelObjectivePresentation it's given,
    /// never computes completion itself. The chest icon itself never changes (same "?" chest art always) —
    /// only a green tick badge toggles on top of it once the objective is completed, plus a slight dim while
    /// locked (no state at all yet, e.g. the level hasn't been unlocked).</summary>
    public sealed class LevelObjectiveView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        [Tooltip("Green checkmark shown over the chest once this objective is completed.")]
        private GameObject _completedBadge;

        [SerializeField]
        private TMP_Text _requirementText;

        [SerializeField]
        private Color _lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField]
        private Color _unlockedColor = Color.white;

        public void Configure(LevelObjectivePresentation presentation)
        {
            if (_requirementText != null)
            {
                _requirementText.text = presentation.RequirementText;
            }

            if (_icon != null)
            {
                _icon.color = presentation.State == LevelObjectiveState.Locked ? _lockedColor : _unlockedColor;
            }

            if (_completedBadge != null)
            {
                _completedBadge.SetActive(presentation.State == LevelObjectiveState.Completed);
            }
        }
    }
}
