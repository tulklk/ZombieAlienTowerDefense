using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlienDefense.Core
{
    /// <summary>Generic Android-back/Escape listener. Contains no navigation rules itself: each scene's presenter
    /// assigns the single handler that fits that scene, so panels never fight over the back button independently.</summary>
    public sealed class BackNavigationController : MonoBehaviour
    {
        private Action _handler;

        /// <summary>Replaces the current handler. Pass null to stop reacting to the back button.</summary>
        public void SetHandler(Action handler)
        {
            _handler = handler;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _handler?.Invoke();
            }
        }
    }
}
