using TMPro;
using UnityEngine;

namespace AlienDefense.UI.MainMenu
{
    /// <summary>Reusable small red badge (dot, "!" or a count) used on feature/rail/nav buttons. Purely display —
    /// never decides on its own whether something is new/claimable, a Presenter always calls Hide/ShowDot/ShowCount.</summary>
    public sealed class NotificationBadgeView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        [Tooltip("Optional. Left empty (dot-only badge) or filled with a count/'!' string.")]
        private TMP_Text _countText;

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void ShowDot()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_countText != null)
            {
                _countText.text = string.Empty;
                _countText.gameObject.SetActive(false);
            }
        }

        public void ShowCount(int count)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_countText != null)
            {
                _countText.gameObject.SetActive(true);
                _countText.text = count > 99 ? "99+" : count.ToString();
            }
        }
    }
}
