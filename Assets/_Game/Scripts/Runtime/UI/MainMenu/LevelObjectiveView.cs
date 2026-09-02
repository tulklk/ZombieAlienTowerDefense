using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>One objective/chest slot. Dumb view: displays whatever LevelObjectivePresentation it's given,
    /// never computes completion itself.</summary>
    public sealed class LevelObjectiveView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _requirementText;

        [SerializeField]
        private Color _lockedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        [SerializeField]
        private Color _incompleteColor = new Color(0.7f, 0.7f, 0.8f, 1f);

        [SerializeField]
        private Color _completedColor = new Color(0.4f, 0.85f, 0.5f, 1f);

        public void Configure(LevelObjectivePresentation presentation)
        {
            if (_requirementText != null)
            {
                _requirementText.text = presentation.RequirementText;
            }

            if (_icon != null)
            {
                _icon.color = presentation.State switch
                {
                    LevelObjectiveState.Completed => _completedColor,
                    LevelObjectiveState.Locked => _lockedColor,
                    _ => _incompleteColor
                };
            }
        }
    }
}
