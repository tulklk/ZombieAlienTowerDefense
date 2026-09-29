using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI.BaseBuilding
{
    /// <summary>"[icon] Central Building Lvl 3     X  [Go]" - one requirement. Green tick when met, red cross when
    /// not, and a Go button only for gates the player can travel to fix.</summary>
    public sealed class BaseRequirementRowView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private GameObject _check;

        [SerializeField]
        private GameObject _cross;

        [SerializeField]
        private Button _goButton;

        [SerializeField]
        private Color _satisfiedColor = new Color(0.13f, 0.2f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Colour of the part of the label that is still missing, e.g. \"Lvl 3\".")]
        private Color _missingColor = new Color(0.9f, 0.15f, 0.15f, 1f);

        private System.Action _go;

        private void Awake()
        {
            if (_goButton != null)
            {
                _goButton.onClick.AddListener(HandleGo);
            }
        }

        private void OnDestroy()
        {
            if (_goButton != null)
            {
                _goButton.onClick.RemoveListener(HandleGo);
            }
        }

        public void Bind(Sprite icon, string label, bool satisfied, System.Action go)
        {
            _go = go;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.gameObject.SetActive(icon != null);
            }

            if (_label != null)
            {
                _label.text = label;
                _label.color = satisfied ? _satisfiedColor : _missingColor;
            }

            if (_check != null)
            {
                _check.SetActive(satisfied);
            }

            if (_cross != null)
            {
                _cross.SetActive(!satisfied);
            }

            if (_goButton != null)
            {
                _goButton.gameObject.SetActive(!satisfied && go != null);
            }
        }

        private void HandleGo()
        {
            _go?.Invoke();
        }
    }
}
