using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Renders the saved Mortar assets at real relative scale, without opening or saving a game scene.</summary>
    internal static class MortarVfxPreview
    {
        [MenuItem("AlienDefense/Setup/VFX/Preview Mortar Compact VFX")]
        public static void Render()
        {
            var preview = new PreviewRenderUtility();
            Texture2D image = null;
            try
            {
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = 1.8f;
                preview.camera.transform.position = new Vector3(0f, 1.5f, -12f);
                preview.camera.transform.rotation = Quaternion.identity;
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 30f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.25f, 0.3f, 0.16f);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, 35f, 0f);
                preview.lights[1].intensity = 0.6f;
                preview.ambientColor = Color.gray;

                float[] times = { 0.025f, 0.065f, 0.12f, 0.2f };
                for (int i = 0; i < times.Length; i++)
                {
                    float x = -2.7f + i * 1.8f;
                    GameObject enemy = Clone(preview, "Assets/_Game/Prefabs/Enemies/Enemy_Normal.prefab",
                        new Vector3(x, 0f, 0.3f));
                    foreach (Canvas canvas in enemy.GetComponentsInChildren<Canvas>(true))
                        canvas.gameObject.SetActive(false);
                    GameObject hit = Clone(preview, "Assets/_Game/VFX/Mortar/MortarImpact_Cartoon.prefab",
                        new Vector3(x, 0.9f, -0.35f));
                    Simulate(hit, times[i]);

                    GameObject bullet = Clone(preview, "Assets/_Game/Prefabs/Projectiles/Projectile_Mortar.prefab",
                        new Vector3(x, 2.6f, 0f));
                    bullet.transform.rotation = Quaternion.LookRotation(Vector3.right);
                    Simulate(bullet, 0.02f);
                    TrailRenderer trail = bullet.GetComponentInChildren<TrailRenderer>();
                    trail.Clear();
                    trail.AddPositions(new[] { bullet.transform.position - Vector3.right * 0.45f,
                        bullet.transform.position - Vector3.right * 0.2f, bullet.transform.position });
                }

                preview.BeginStaticPreview(new Rect(0f, 0f, 1200f, 600f));
                preview.Render(true);
                image = preview.EndStaticPreview();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/MortarCompactVfx-Preview.png", image.EncodeToPNG());
                Debug.Log("[MortarCompactVfx] Rendered Logs/MortarCompactVfx-Preview.png; impact columns: 25, 65, 120, 200 ms. Projectile row uses the same scale.");
            }
            finally
            {
                if (image != null) Object.DestroyImmediate(image);
                preview.Cleanup();
            }
        }

        private static GameObject Clone(PreviewRenderUtility preview, string path, Vector3 position)
        {
            // Instantiate beneath an inactive parent so gameplay Awake/OnEnable never run in the preview.
            var holder = new GameObject("MortarPreview");
            holder.SetActive(false);
            preview.AddSingleGO(holder);
            GameObject clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), holder.transform);
            MonoBehaviour[] behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = behaviours.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(behaviours[i]);
            clone.transform.position = position;
            holder.SetActive(true);
            return clone;
        }

        private static void Simulate(GameObject root, float time)
        {
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>())
            {
                system.useAutoRandomSeed = false;
                system.randomSeed = 41;
                system.Simulate(time, false, true, false);
            }
        }
    }
}
