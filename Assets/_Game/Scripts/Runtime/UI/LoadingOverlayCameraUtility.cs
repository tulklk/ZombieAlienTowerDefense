using UnityEngine;

namespace AlienDefense.UI
{
    /// <summary>Creates cameras used by LoadingOverlay canvases (Screen Space - Camera).</summary>
    public static class LoadingOverlayCameraUtility
    {
        public const string BootstrapUICameraName = "BootstrapUICamera";
        public const string TransitionCameraName = "LoadingOverlayCamera";

        private const float OverlayCameraPlaneDistance = 100f;
        private const float OverlayCameraDepth = 100f;
        private static readonly Color OverlayClearColor = new Color(0.05f, 0.12f, 0.15f, 1f);

        public static Camera CreateOverlayCamera(Transform parent, string cameraObjectName)
        {
            var cameraObject = new GameObject(cameraObjectName);
            if (parent != null)
            {
                cameraObject.transform.SetParent(parent, false);
            }

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = OverlayClearColor;
            camera.depth = OverlayCameraDepth;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.useOcclusionCulling = false;
            return camera;
        }

        public static void BindCanvas(Canvas canvas, Camera camera)
        {
            if (canvas == null || camera == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = OverlayCameraPlaneDistance;
        }
    }
}
