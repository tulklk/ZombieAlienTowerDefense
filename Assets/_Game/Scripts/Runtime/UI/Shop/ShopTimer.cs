using System;
using System.Collections;
using UnityEngine;

namespace AlienDefense.UI.Shop
{
    /// <summary>The Shop's single clock: one tick per real second while the Shop is open, shared by every countdown
    /// (cards subscribe through the presenter; nothing runs an Update of its own). Unscaled, allocation-free.</summary>
    public sealed class ShopTimer : MonoBehaviour
    {
        private static readonly WaitForSecondsRealtime OneSecond = new WaitForSecondsRealtime(1f);
        private Coroutine _routine;

        public event Action<DateTime> Tick;

        private void OnEnable()
        {
            _routine = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator Run()
        {
            while (true)
            {
                yield return OneSecond;
                Tick?.Invoke(DateTime.UtcNow);
            }
        }
    }
}
