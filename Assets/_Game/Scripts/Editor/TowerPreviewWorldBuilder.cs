using System.IO;
using AlienDefense.UI.Defense;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Defense popup's 3D showcase in MainMenu ("DefensePreviewWorld"): a round display pad on a
    /// patch of grass with a few rocks, plants and workshop props, a soft painted backdrop (the cheap way to get the
    /// blurred-background look - no depth of field), two lights, one camera and the TowerPreviewRenderer that drives
    /// them. Everything sits on its own "TowerPreview" layer, far from the menu's other worlds; every other camera
    /// and light in the scene is told to ignore that layer, and the showcase's own lights light nothing else.
    ///
    /// Scenery is reused from the project (Level 2 kit rocks / grass / bush / trees, Low Poly Construction toolbox and
    /// fuel can) as unpacked, script-free, collider-free copies - the source prefabs are never edited. Re-runnable:
    /// the previous DefensePreviewWorld is deleted first. Called by DefenseTabBuilder.</summary>
    public static class TowerPreviewWorldBuilder
    {
        public const string LayerName = "TowerPreview";
        private const string RootName = "DefensePreviewWorld";
        private const string ArtFolder = "Assets/_Game/Art/DefensePreview";
        private const string Kit = "Assets/_Game/Prefabs/Level02/";
        private const string Construction = "Assets/Low Poly Construction/Low Poly Construction/";
        private const float CameraYaw = 28f;

        private static int _layer;

        public static TowerPreviewRenderer Build(Scene scene)
        {
            _layer = EnsureLayer();
            EditorFolderUtility.EnsureFolder(ArtFolder);

            foreach (GameObject existing in scene.GetRootGameObjects())
            {
                if (existing.name == RootName)
                {
                    Object.DestroyImmediate(existing);
                }
            }

            // Nothing else in the menu may see or light the showcase.
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Camera other in sceneRoot.GetComponentsInChildren<Camera>(true))
                {
                    other.cullingMask &= ~(1 << _layer);
                    EditorUtility.SetDirty(other);
                }

                foreach (Light other in sceneRoot.GetComponentsInChildren<Light>(true))
                {
                    other.cullingMask &= ~(1 << _layer);
                    EditorUtility.SetDirty(other);
                }
            }

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(600f, -300f, 600f); // far from the menu's base world and profile preview
            root.layer = _layer;

            // Screen axes on the ground for the fixed camera yaw: "right" and "away from the camera".
            Quaternion yaw = Quaternion.Euler(0f, 180f + CameraYaw, 0f);
            Vector3 far = yaw * Vector3.forward;
            Vector3 right = yaw * Vector3.right;
            Vector3 At(float screenRight, float away, float height = 0f) => right * screenRight + far * away + Vector3.up * height;

            Transform environment = Child(root.transform, "PreviewEnvironment");

            // Ground: one unlit disc, lighter in the middle, the backdrop's bottom colour at the rim.
            GameObject ground = Primitive(PrimitiveType.Cylinder, "Ground", environment, Vector3.down * 0.02f, new Vector3(30f, 0.02f, 30f));
            ground.GetComponent<Renderer>().sharedMaterial = UnlitMaterial("MAT_Preview_Ground", PaintGround());

            // Backdrop: a big quad facing the camera with soft green countryside painted on it.
            GameObject backdrop = Primitive(PrimitiveType.Quad, "Backdrop", environment, At(0f, 15f, 3.4f), new Vector3(30f, 8f, 1f));
            backdrop.transform.rotation = Quaternion.LookRotation(far, Vector3.up);
            backdrop.GetComponent<Renderer>().sharedMaterial = UnlitMaterial("MAT_Preview_Backdrop", PaintBackdrop());

            // Display pad: dark outer ring, mid step, light top plate. The renderer scales it to the model's footprint.
            Transform pad = Child(root.transform, "DisplayPlatform");
            Disc("OuterRing", pad, 1.08f, 0.07f, 0f, LitMaterial("MAT_Preview_PadDark", Hex("#2E3644"), 0.35f, 0.45f));
            Disc("Step", pad, 0.95f, 0.1f, 0.05f, LitMaterial("MAT_Preview_PadMid", Hex("#566172"), 0.45f, 0.5f));
            Disc("Top", pad, 0.82f, 0.08f, 0.13f, LitMaterial("MAT_Preview_PadTop", Hex("#8590A2"), 0.4f, 0.55f));
            const float padTop = 0.21f;
            GameObject blob = Primitive(PrimitiveType.Quad, "BlobShadow", pad, Vector3.up * (padTop + 0.004f), new Vector3(1.75f, 1.75f, 1f));
            blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            blob.GetComponent<Renderer>().sharedMaterial = BlobMaterial();
            GameObject groundShadow = Primitive(PrimitiveType.Quad, "PadShadow", environment, Vector3.up * 0.004f + At(0.25f, 0.2f), new Vector3(3.3f, 3.3f, 1f));
            groundShadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            groundShadow.GetComponent<Renderer>().sharedMaterial = BlobMaterial();

            Transform anchor = Child(root.transform, "PreviewTowerAnchor");
            anchor.localPosition = Vector3.up * padTop;

            // A little life around the pad - the model stays the focus.
            Prop(Construction + "Toolbox.fbx", environment, At(-1.75f, 1.15f), 35f, 0.8f);
            Prop(Construction + "Fuel Can.fbx", environment, At(-2.35f, 1.9f), -20f, 0.62f);
            Prop(Kit + "Props/PF_Rock_01.prefab", environment, At(1.95f, 1.5f), 40f, 0.6f);
            Prop(Kit + "Props/PF_Rock_02.prefab", environment, At(-3.1f, 3.4f), 120f, 0.8f);
            Prop(Kit + "Vegetation/PF_Grass_01.prefab", environment, At(1.9f, -0.5f), 0f, 1f);
            Prop(Kit + "Vegetation/PF_Grass_02.prefab", environment, At(-1.35f, -0.35f), 70f, 1f);
            Prop(Kit + "Vegetation/PF_Grass_01.prefab", environment, At(-2.2f, 1.5f), 200f, 1.1f);
            Prop(Kit + "Vegetation/PF_Weed_Yellow.prefab", environment, At(1.45f, -0.75f), 0f, 0.75f);
            Prop(Kit + "Vegetation/PF_Bush_Round_01.prefab", environment, At(2.9f, 3.6f), 0f, 0.85f);
            // Lights: warm key from the upper left front (casts the soft shadow), cool low fill from the right.
            Transform lighting = Child(root.transform, "PreviewLighting");
            Light key = AddLight(lighting, "KeyLight", (far * 0.55f + right * 0.6f + Vector3.down * 0.75f).normalized, Hex("#FFF3DC"), 1.25f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;
            AddLight(lighting, "FillLight", (far * 0.3f - right * 0.85f + Vector3.down * 0.35f).normalized, Hex("#BCD6FF"), 0.45f);
            AddLight(lighting, "TopLight", Vector3.down, Hex("#EAF6FF"), 0.3f); // stands in for ambient: nothing goes flat grey

            // The one camera. The renderer positions it per model; it only draws the showcase layer.
            var cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("#D6F0F2");
            camera.cullingMask = 1 << _layer;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.allowHDR = false;
            camera.enabled = false;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = true;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraObject.transform.SetPositionAndRotation(root.transform.position + At(0f, -7f, 2.6f), Quaternion.Euler(16f, 180f + CameraYaw, 0f));

            SetLayer(root.transform);

            var renderer = root.AddComponent<TowerPreviewRenderer>();
            var so = new SerializedObject(renderer);
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_anchor").objectReferenceValue = anchor;
            so.FindProperty("_pad").objectReferenceValue = pad;
            so.FindProperty("_cameraYaw").floatValue = CameraYaw;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false); // switched on only while the popup's Core Stats page is showing
            return renderer;
        }

        // ------------------------------------------------------------------ pieces

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>A cylinder of the given radius and height whose bottom sits at <paramref name="bottom"/>.</summary>
        private static void Disc(string name, Transform parent, float radius, float height, float bottom, Material material)
        {
            GameObject go = Primitive(PrimitiveType.Cylinder, name, parent, Vector3.up * (bottom + height * 0.5f), new Vector3(radius * 2f, height * 0.5f, radius * 2f));
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
        }

        /// <summary>An unpacked copy of a project prop with every script and collider removed (visual only).</summary>
        private static void Prop(string path, Transform parent, Vector3 localPosition, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[TowerPreviewWorldBuilder] Missing " + path);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
            go.transform.localPosition = localPosition;

            // Stand it on the ground whatever its pivot is.
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                    r.shadowCastingMode = ShadowCastingMode.Off; // the pad's blob shadow is the only shadow needed
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    FixMaterials(r);
                }

                go.transform.position += Vector3.up * (parent.position.y - bounds.min.y);
            }
        }

        /// <summary>Third-party props whose materials are not URP (they would render magenta) get a project-owned
        /// URP Lit copy carrying the same colour and texture, on this instance only.</summary>
        private static void FixMaterials(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || source.shader == null || source.shader.name.StartsWith("Universal Render Pipeline")
                    || source.shader.name.StartsWith("AlienDefense") || source.shader.name.StartsWith("Shader Graphs"))
                {
                    continue;
                }

                string path = ArtFolder + "/MAT_Preview_" + source.name.Replace(' ', '_') + ".mat";
                var owned = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (owned == null)
                {
                    owned = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    Texture texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    owned.SetColor("_BaseColor", color);
                    owned.SetTexture("_BaseMap", texture);
                    owned.SetFloat("_Smoothness", 0.2f);
                    AssetDatabase.CreateAsset(owned, path);
                }

                materials[i] = owned;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        private static Light AddLight(Transform parent, string name, Vector3 direction, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << _layer; // lights the showcase and nothing else in the menu
            return light;
        }

        private static void SetLayer(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = _layer;
            }
        }

        // ------------------------------------------------------------------ materials + painted textures

        private static Material LitMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = ArtFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material UnlitMaterial(string name, Texture2D texture)
        {
            string path = ArtFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BlobMaterial()
        {
            string path = ArtFolder + "/MAT_Preview_BlobShadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default")); // plain alpha blend, no depth write
                AssetDatabase.CreateAsset(material, path);
            }

            material.mainTexture = PaintBlob();
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static readonly Color GroundEdge = Hex("#6DB548");

        private static Texture2D PaintGround()
        {
            const int size = 512;
            var pixels = new Color32[size * size];
            Color centre = Hex("#93D154"), dirt = Hex("#C9B77C"), dark = Hex("#5FA83F"), light = Hex("#A9DE63");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = Color.Lerp(centre, GroundEdge, Mathf.SmoothStep(0f, 1f, r / 0.5f));
                    // Soft mottling so the lawn is not one flat colour, and a worn patch of earth round the pad.
                    float mottle = Mathf.PerlinNoise(u * 22f + 3.1f, v * 22f + 7.7f) - 0.5f;
                    c = Color.Lerp(c, mottle > 0f ? light : dark, Mathf.Abs(mottle) * 0.55f);
                    float worn = (1f - Mathf.SmoothStep(0f, 1f, (r - 0.085f) / 0.065f)) * (0.55f + 0.45f * Mathf.PerlinNoise(u * 40f, v * 40f));
                    c = Color.Lerp(c, dirt, worn * 0.6f);
                    pixels[y * size + x] = c;
                }
            }

            return WriteTexture("T_Preview_Ground", pixels, size, size, false);
        }

        /// <summary>Soft, low-contrast countryside: pale sky, hazy hills, blurred foliage clumps and a couple of rocks,
        /// melting into the ground colour at the bottom. Painted blurry, so the model in front reads sharp.</summary>
        private static Texture2D PaintBackdrop()
        {
            const int width = 512, height = 256;
            var pixels = new Color32[width * height];
            Color sky = Hex("#D6F0F2"), haze = Hex("#B4E09C"), meadow = GroundEdge;
            Color foliage = Hex("#4F9E48"), foliageLight = Hex("#86C95A"), rock = Hex("#9EB3A8");
            // x, y, radius x, radius y, kind (0 dark foliage, 1 light foliage, 2 rock)
            float[,] blobs =
            {
                { 0.06f, 0.27f, 0.09f, 0.13f, 0 }, { 0.20f, 0.31f, 0.08f, 0.15f, 1 }, { 0.33f, 0.22f, 0.05f, 0.06f, 2 },
                { 0.45f, 0.30f, 0.09f, 0.14f, 0 }, { 0.58f, 0.25f, 0.07f, 0.10f, 1 }, { 0.72f, 0.31f, 0.09f, 0.15f, 0 },
                { 0.86f, 0.27f, 0.08f, 0.12f, 1 }, { 0.96f, 0.30f, 0.07f, 0.14f, 0 }, { 0.66f, 0.17f, 0.04f, 0.04f, 2 },
            };
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    Color c = v > 0.3f ? Color.Lerp(haze, sky, Mathf.SmoothStep(0f, 1f, (v - 0.3f) / 0.2f))
                        : Color.Lerp(meadow, haze, Mathf.SmoothStep(0f, 1f, (v - 0.08f) / 0.22f));
                    for (int b = 0; b < blobs.GetLength(0); b++)
                    {
                        float dx = (u - blobs[b, 0]) / blobs[b, 2], dy = (v - blobs[b, 1]) / blobs[b, 3];
                        float amount = Mathf.Exp(-(dx * dx + dy * dy) * 2.2f) * 0.9f;
                        Color tint = blobs[b, 4] < 0.5f ? foliage : blobs[b, 4] < 1.5f ? foliageLight : rock;
                        c = Color.Lerp(c, tint, amount);
                    }

                    if (v < 0.16f)
                    {
                        c = Color.Lerp(meadow, c, Mathf.SmoothStep(0f, 1f, (v - 0.07f) / 0.09f)); // bottom = ground colour
                    }

                    pixels[y * width + x] = c;
                }
            }

            return WriteTexture("T_Preview_Backdrop", pixels, width, height, false);
        }

        private static Texture2D PaintBlob()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    pixels[y * size + x] = new Color(0.03f, 0.08f, 0.05f, a * a * 0.5f);
                }
            }

            return WriteTexture("T_Preview_BlobShadow", pixels, size, size, true);
        }

        private static Texture2D WriteTexture(string name, Color32[] pixels, int width, int height, bool alpha)
        {
            string path = ArtFolder + "/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = alpha;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static int EnsureLayer()
        {
            int existing = LayerMask.NameToLayer(LayerName);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = LayerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }

            Debug.LogError("[TowerPreviewWorldBuilder] No free layer for " + LayerName + ".");
            return 0;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }
    }
}
