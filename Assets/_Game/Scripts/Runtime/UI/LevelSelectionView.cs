using System;
using UnityEngine;
using UnityEngine.UI;

namespace AlienDefense.UI
{
    /// <summary>Level Selection's dumb view: exposes the card container and forwards Back clicks. Never
    /// instantiates cards or reads the LevelCatalog itself.</summary>
    public sealed class LevelSelectionView : MonoBehaviour
    {
        [SerializeField]
        private Transform _cardContainer;

        [SerializeField]
        [Tooltip("Optional.")]
        private Button _backButton;

        public Transform CardContainer => _cardContainer;

        public event Action BackClicked;

        private void Awake()
        {
            if (_backButton != null)
            {
                _backButton.onClick.AddListener(HandleBackClicked);
            }
        }

        private void HandleBackClicked()
        {
            BackClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(HandleBackClicked);
            }
        }
    }
}
