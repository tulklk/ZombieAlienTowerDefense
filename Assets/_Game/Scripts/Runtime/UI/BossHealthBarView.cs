using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Screen-space top health bar shown only while a Boss is alive. Pure view, no gameplay knowledge.</summary>
    public sealed class BossHealthBarView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _visualRoot;

        [SerializeField]
        private Image _fillImage;

        [SerializeField]
        private TMP_Text _nameText;

        public void Show(string bossName)
        {
            if (_nameText != null)
            {
                _nameText.text = bossName;
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void SetFill(float ratio)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = Mathf.Clamp01(ratio);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(visible);
            }
        }
    }
}
