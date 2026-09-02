using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Row of 3 objective slots with connectors between them. Dumb view: SetObjectives(null) hides the
    /// whole panel (e.g. no objective data for this level) instead of leaving an empty gap; SetObjectives(array)
    /// configures each slot and colors connectors by whether the objective before them is completed.</summary>
    public sealed class LevelObjectivePanelView : MonoBehaviour
    {
        [SerializeField]
        private LevelObjectiveView[] _objectiveSlots = System.Array.Empty<LevelObjectiveView>();

        [SerializeField]
        [Tooltip("Optional. One fewer than _objectiveSlots.")]
        private Image[] _connectors = System.Array.Empty<Image>();

        [SerializeField]
        private Color _connectorIncompleteColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        [SerializeField]
        private Color _connectorCompletedColor = new Color(0.4f, 0.85f, 0.5f, 1f);

        public void SetObjectives(LevelObjectivePresentation[] objectives)
        {
            bool hasObjectives = objectives != null && objectives.Length > 0;
            gameObject.SetActive(hasObjectives);

            if (!hasObjectives)
            {
                return;
            }

            int slotCount = Mathf.Min(_objectiveSlots.Length, objectives.Length);
            for (int i = 0; i < slotCount; i++)
            {
                _objectiveSlots[i]?.Configure(objectives[i]);
            }

            int connectorCount = Mathf.Min(_connectors.Length, slotCount - 1);
            for (int i = 0; i < connectorCount; i++)
            {
                if (_connectors[i] == null)
                {
                    continue;
                }

                bool completed = objectives[i].State == LevelObjectiveState.Completed;
                _connectors[i].color = completed ? _connectorCompletedColor : _connectorIncompleteColor;
            }
        }
    }
}
