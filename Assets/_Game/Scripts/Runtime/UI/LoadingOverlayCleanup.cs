using System;
using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Destroys stray LoadingOverlay instances left in loaded scenes (e.g. after Bootstrap boot).</summary>
    public static class LoadingOverlayCleanup
    {
        public static void DestroyAllRuntimeInstances(LoadingOverlayView exclude = null)
        {
            LoadingOverlayView[] views = UnityEngine.Object.FindObjectsByType<LoadingOverlayView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                LoadingOverlayView view = views[i];
                if (view == null || ReferenceEquals(view, exclude))
                {
                    continue;
                }

                DestroyInstance(view);
            }
        }

        public static void DestroyInstance(LoadingOverlayView view)
        {
            if (view == null)
            {
                return;
            }

            GameObject gameObject = view.gameObject;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(gameObject))
            {
                return;
            }
#endif
            view.Dismiss();
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
