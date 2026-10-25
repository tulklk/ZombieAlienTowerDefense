using System.Collections.Generic;
using AlienDefense.Base.DayNight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Sets up the BaseWorld's real-time day / night (re-runnable):
    ///  - a radial glow texture and two additive glow materials (AlienDefense/WorldGlowAdditive), plus a night copy of
    ///    the street-lamp bulb material with emission on;
    ///  - under every street lamp a NightVisual (bulb halo facing the base camera + warm light pool on the ground,
    ///    off by default) and a BaseWorldNightLight that knows the bulb's material slot;
    ///  - BaseWorld/DayNightSystem with BaseWorldDayNightController, its Day preset read from the scene as authored
    ///    (so day keeps today's look) and a Night preset tuned for a dark-blue, readable town.
    /// No real-time point lights: the mobile URP asset allows one additional light per object and the ground is one
    /// big mesh, so the lamps light the ground with additive quads instead.</summary>
    public static class BaseWorldDayNightBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string ArtDir = "Assets/_Game/Art/Base/DayNight";
        private const string GlowShader = "AlienDefense/WorldGlowAdditive";
        private const string LampMaterialPath = "Assets/_Game/Art/Base/Materials/MAT_Base_StreetLightLamp.mat";
        private const int BaseWorldLayer = 17;

        [MenuItem("AlienDefense/Setup/33. Build Base Day-Night")]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
            {
                return "[BaseWorldDayNightBuilder] Stop Play mode first.";
            }

            EnsureFolder(ArtDir);
            Texture2D glowTexture = PaintGlowTexture();
            Material groundGlow = GlowMaterial("MAT_LampGlowGround", glowTexture, new Color(1f, 0.82f, 0.48f, 0.55f));
            Material bulbGlow = GlowMaterial("MAT_LampGlowBulb", glowTexture, new Color(1f, 0.86f, 0.55f, 0.9f));
            Material nightBulb = NightBulbMaterial();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject world = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "BaseWorld")
                {
                    world = root;
                }
            }

            if (world == null)
            {
                return "[BaseWorldDayNightBuilder] No BaseWorld in MainMenu.";
            }

            var sun = world.transform.Find("BaseSun")?.GetComponent<Light>();
            Camera camera = world.GetComponentInChildren<Camera>(true);

            // Street lamps.
            var lamps = new List<BaseWorldNightLight>();
            Transform lampRoot = world.transform.Find("Environment/StreetLightsRoot");
            if (lampRoot != null)
            {
                foreach (Transform lamp in lampRoot)
                {
                    BaseWorldNightLight nightLamp = BuildLamp(lamp, nightBulb, groundGlow, bulbGlow, camera);
                    if (nightLamp != null)
                    {
                        lamps.Add(nightLamp);
                    }
                }
            }

            // River renderers (the unlit stylised water is recoloured; the rest of the river system is lit).
            var water = new List<Renderer>();
            Material waterMaterial = null;
            Transform river = world.transform.Find("RiverSystem");
            if (river != null)
            {
                foreach (Renderer renderer in river.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = renderer.sharedMaterial;
                    if (m != null && m.shader != null && m.shader.name == "AlienDefense/StylizedRiver")
                    {
                        water.Add(renderer);
                        waterMaterial = m;
                    }
                }
            }

            // Controller.
            Transform old = world.transform.Find("DayNightSystem");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var system = new GameObject("DayNightSystem");
            system.transform.SetParent(world.transform, false);
            system.layer = BaseWorldLayer;
            var controller = system.AddComponent<BaseWorldDayNightController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_sun").objectReferenceValue = sun;
            so.FindProperty("_worldCamera").objectReferenceValue = camera;
            SetArray(so.FindProperty("_waterRenderers"), water.ToArray());
            SetArray(so.FindProperty("_nightLights"), lamps.ToArray());
            so.FindProperty("_groundGlowMaterial").objectReferenceValue = groundGlow;
            so.FindProperty("_bulbGlowMaterial").objectReferenceValue = bulbGlow;
            so.FindProperty("_groundGlowColor").colorValue = new Color(1f, 0.80f, 0.45f, 0.8f);
            so.FindProperty("_bulbGlowColor").colorValue = new Color(1f, 0.86f, 0.55f, 1f);

            // Day = the scene exactly as authored.
            SerializedProperty day = so.FindProperty("_day");
            day.FindPropertyRelative("LightColor").colorValue = sun != null ? sun.color : Color.white;
            day.FindPropertyRelative("LightIntensity").floatValue = sun != null ? sun.intensity : 1.25f;
            day.FindPropertyRelative("LightRotation").vector3Value = sun != null ? sun.transform.eulerAngles : new Vector3(52f, 325f, 0f);
            day.FindPropertyRelative("AmbientTint").colorValue = Color.white;
            day.FindPropertyRelative("AmbientIntensity").floatValue = 1f;
            day.FindPropertyRelative("ReflectionIntensity").floatValue = 1f;
            day.FindPropertyRelative("CameraBackground").colorValue = camera != null ? camera.backgroundColor : new Color(0.624f, 0.847f, 0.961f);
            if (waterMaterial != null)
            {
                day.FindPropertyRelative("WaterShallow").colorValue = waterMaterial.GetColor("_ShallowColor");
                day.FindPropertyRelative("WaterDeep").colorValue = waterMaterial.GetColor("_DeepColor");
                day.FindPropertyRelative("WaterFoam").colorValue = waterMaterial.GetColor("_FoamColor");
                day.FindPropertyRelative("WaterHighlight").floatValue = waterMaterial.GetFloat("_HighlightStrength");
            }

            // Night = cool moonlight from roughly the same side, dark-blue ambient, deep-blue water.
            SerializedProperty night = so.FindProperty("_night");
            night.FindPropertyRelative("LightColor").colorValue = new Color(0.55f, 0.66f, 1f);
            night.FindPropertyRelative("LightIntensity").floatValue = 0.28f;
            Vector3 sunAngles = sun != null ? sun.transform.eulerAngles : new Vector3(52f, 325f, 0f);
            night.FindPropertyRelative("LightRotation").vector3Value = new Vector3(58f, sunAngles.y - 35f, 0f);
            night.FindPropertyRelative("AmbientTint").colorValue = new Color(0.26f, 0.34f, 0.70f);
            night.FindPropertyRelative("AmbientIntensity").floatValue = 0.75f;
            night.FindPropertyRelative("ReflectionIntensity").floatValue = 0.35f;
            night.FindPropertyRelative("CameraBackground").colorValue = new Color(0.03f, 0.05f, 0.12f);
            night.FindPropertyRelative("WaterShallow").colorValue = new Color(0.12f, 0.38f, 0.55f);
            night.FindPropertyRelative("WaterDeep").colorValue = new Color(0.05f, 0.18f, 0.36f);
            night.FindPropertyRelative("WaterFoam").colorValue = new Color(0.50f, 0.66f, 0.78f);
            night.FindPropertyRelative("WaterHighlight").floatValue = 0.22f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"[BaseWorldDayNightBuilder] {lamps.Count} street lamps, {water.Count} water renderer(s), sun={(sun != null ? sun.name : "none")}, camera={(camera != null ? camera.name : "none")}.";
        }

        /// <summary>NightVisual under a lamp: a halo round the bulb (facing the fixed base camera) and a light pool on
        /// the ground. The bulb's height comes from the lamp mesh's bulb sub-mesh.</summary>
        private static BaseWorldNightLight BuildLamp(Transform lamp, Material nightBulb, Material groundGlow, Material bulbGlow, Camera camera)
        {
            var renderer = lamp.GetComponentInChildren<MeshRenderer>();
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            if (renderer == null || filter == null || filter.sharedMesh == null)
            {
                return null;
            }

            Material[] materials = renderer.sharedMaterials;
            int bulbSlot = -1;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && (materials[i].name == "MAT_Base_StreetLightLamp" || materials[i] == nightBulb))
                {
                    bulbSlot = i;
                }
            }

            if (bulbSlot < 0)
            {
                return null;
            }

            // Make sure the scene stores the day bulb (in case a night preview was saved).
            if (materials[bulbSlot] == nightBulb)
            {
                materials[bulbSlot] = AssetDatabase.LoadAssetAtPath<Material>(LampMaterialPath);
                renderer.sharedMaterials = materials;
            }

            Transform old = lamp.Find("NightVisual");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Bounds bulbLocal = filter.sharedMesh.GetSubMesh(Mathf.Min(bulbSlot, filter.sharedMesh.subMeshCount - 1)).bounds;
            Vector3 bulbWorld = filter.transform.TransformPoint(bulbLocal.center);

            var visual = new GameObject("NightVisual");
            visual.transform.SetParent(lamp, false);
            visual.layer = BaseWorldLayer;

            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pool.name = "GroundGlow";
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            pool.transform.SetParent(visual.transform, false);
            pool.transform.position = new Vector3(bulbWorld.x, lamp.position.y + 0.2f, bulbWorld.z);
            pool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            pool.transform.localScale = new Vector3(8.5f, 8.5f, 1f);
            SetupGlowRenderer(pool, groundGlow);

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "BulbGlow";
            Object.DestroyImmediate(halo.GetComponent<Collider>());
            halo.transform.SetParent(visual.transform, false);
            halo.transform.position = bulbWorld;
            halo.transform.rotation = camera != null ? camera.transform.rotation : Quaternion.Euler(50f, 45f, 0f);
            halo.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
            SetupGlowRenderer(halo, bulbGlow);

            visual.SetActive(false);

            var night = lamp.GetComponent<BaseWorldNightLight>();
            if (night == null)
            {
                night = lamp.gameObject.AddComponent<BaseWorldNightLight>();
            }

            night.Configure(renderer, bulbSlot, nightBulb, visual,
                new[] { pool.GetComponent<Renderer>(), halo.GetComponent<Renderer>() });
            EditorUtility.SetDirty(night);
            return night;
        }

        private static void SetupGlowRenderer(GameObject quad, Material material)
        {
            quad.layer = BaseWorldLayer;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material GlowMaterial(string name, Texture2D texture, Color color)
        {
            string path = $"{ArtDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(GlowShader));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find(GlowShader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The bulb material with emission switched on (the day asset already carries a warm emission colour
        /// but has the keyword off). A copy - the day material is left alone.</summary>
        private static Material NightBulbMaterial()
        {
            var day = AssetDatabase.LoadAssetAtPath<Material>(LampMaterialPath);
            string path = $"{ArtDir}/MAT_Base_StreetLightLamp_Night.mat";
            var night = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (night == null)
            {
                night = new Material(day);
                AssetDatabase.CreateAsset(night, path);
            }
            else
            {
                night.CopyPropertiesFromMaterial(day);
            }

            night.SetColor("_BaseColor", new Color(1f, 0.92f, 0.7f));
            night.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.38f) * 2.4f);
            night.EnableKeyword("_EMISSION");
            night.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(night);
            return night;
        }

        /// <summary>Soft round falloff (alpha), white RGB - tinted by the material.</summary>
        private static Texture2D PaintGlowTexture()
        {
            string path = $"{ArtDir}/T_LampGlow.png";
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a); // smooth falloff
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
