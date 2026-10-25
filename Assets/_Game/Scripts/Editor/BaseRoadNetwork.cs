using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>The Base road grid and street lights.
    ///
    /// Roads use the Low Poly Road Pack "SingleLane" pieces (straight, T-junction, cross, curve), wrapped in project
    /// prefabs under Assets/_Game/Prefabs/Base/Roads with a recoloured copy of the pack's palette: sandy beige road,
    /// cream kerbs - the pack's own asphalt/grey (and its Built-in Standard material, pink in URP) do not fit the map.
    /// The pack files are never modified.
    ///
    /// Layout: grid lines at x = -48, -24, 0, 24, 48 and z = -24, 0, 24, so every plot sits alone in a block ringed by
    /// road; crosses inside, T-junctions on the edges, curved corners, and a spur from the centre down to the bridge.
    /// Street lights (a small low-poly lamp built here, since the project has none) stand at two opposite corners of
    /// each junction, and inside each curved corner - off the road, clear of plots and doors.</summary>
    public static partial class BaseSceneSetupTool
    {
        private const string RoadPackFolder = "Assets/Low Poly Road Pack/Models/Models/";
        private const string RoadPackPalette = "Assets/Low Poly Road Pack/Textures/Textures/Road Colorscheme.png";
        private const string RoadPrefabFolder = "Assets/_Game/Prefabs/Base/Roads";
        private const string LampPrefabPath = "Assets/_Game/Prefabs/Base/Environment/BP_StreetLight.prefab";

        /// <summary>Pack pieces are 10 units per tile; at this scale a tile (full road width, kerbs included) is 4.8 m.</summary>
        private const float RoadPieceScale = RoadWidth / 10f;
        /// <summary>Kerb top. The pack's lane sits 0.12 m below it, so this keeps the lane (the paved surface) 5 cm above
        /// the painted ground instead of buried under it.</summary>
        private const float RoadTopY = 0.17f;
        private const float LampCornerOffset = 3.5f;

        [MenuItem("Tools/Tower Defense/Base/Rebuild Roads And Street Lights")]
        public static void RebuildRoadsAndStreetLights()
        {
            Missing.Clear();
            Scene scene = SceneManager.GetSceneByPath(MainMenuPath);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            }

            GameObject world = FindRoot(scene, WorldRootName);
            Transform environment = world != null ? world.transform.Find("Environment") : null;
            if (environment == null)
            {
                Debug.LogError("[BaseRoads] MainMenu has no BaseWorld/Environment. Run Setup Base Scene first.");
                return;
            }

            foreach (string name in new[] { "Roads", "RoadsRoot", "StreetLightsRoot", "ZoneProps" })
            {
                Transform old = environment.Find(name);
                while (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                    old = environment.Find(name);
                }
            }

            BuildRoadNetwork(environment);
            // District props were placed against the old road layout; rebuilt so none sits on the new roads.
            BuildZoneProps(environment);
            foreach (string name in new[] { "RoadsRoot", "StreetLightsRoot", "ZoneProps" })
            {
                Transform built = environment.Find(name);
                SetLayerRecursively(built.gameObject, BaseLayer);
                MarkEnvironmentStatic(built.gameObject);
                DisableDecorColliders(built.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Ground paint, roadside grass and nature placement all follow the road rectangles.
            RebuildEnvironmentDressing();
            Debug.Log("[BaseRoads] Roads and street lights rebuilt.");
        }

        private enum RoadPiece { Straight, Cross, T, Curve }

        private static void BuildRoadNetwork(Transform environment)
        {
            Material roadMaterial = EnsureRoadMaterial();
            var pieces = new Dictionary<RoadPiece, GameObject>
            {
                [RoadPiece.Straight] = EnsureRoadPrefab("Road_Straight", "SingleLane Straight 2", new Vector2(-5f, -5f), roadMaterial),
                [RoadPiece.Cross] = EnsureRoadPrefab("Road_Cross", "SingleLane Cross SingleLane", new Vector2(5f, -5f), roadMaterial),
                [RoadPiece.T] = EnsureRoadPrefab("Road_T", "SingleLane Join SingleLane", new Vector2(5f, -5f), roadMaterial),
                // The curve's pivot is the virtual sharp corner its arc rounds off (arms north and west).
                [RoadPiece.Curve] = EnsureRoadPrefab("Road_Corner", "SingleLane Curve", new Vector2(15f, -15f), roadMaterial)
            };

            var roadsRoot = new GameObject("RoadsRoot").transform;
            roadsRoot.SetParent(environment, false);
            var lightsRoot = new GameObject("StreetLightsRoot").transform;
            lightsRoot.SetParent(environment, false);

            float minX = RoadGridX[0], maxX = RoadGridX[RoadGridX.Length - 1];
            float minZ = RoadGridZ[0], maxZ = RoadGridZ[RoadGridZ.Length - 1];
            var extent = new Dictionary<Vector2, float>();
            var counters = new Dictionary<string, int>();
            GameObject lamp = EnsureStreetLightPrefab();
            int lampIndex = 0;

            // Junctions.
            foreach (float x in RoadGridX)
            {
                foreach (float z in RoadGridZ)
                {
                    bool north = z < maxZ;
                    bool south = z > minZ || Mathf.Approximately(x, 0f);
                    bool east = x < maxX;
                    bool west = x > minX;
                    int arms = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
                    var node = new Vector3(x, 0f, z);

                    if (arms == 4)
                    {
                        PlaceRoad(pieces[RoadPiece.Cross], roadsRoot, "Road_Cross", counters, node, 0f, 1f);
                        extent[new Vector2(x, z)] = RoadWidth * 0.5f;
                    }
                    else if (arms == 3)
                    {
                        float yaw = !west ? 0f : !north ? 90f : !east ? 180f : 270f;
                        PlaceRoad(pieces[RoadPiece.T], roadsRoot, "Road_T", counters, node, yaw, 1f);
                        extent[new Vector2(x, z)] = RoadWidth * 0.5f;
                    }
                    else
                    {
                        float yaw = north && west ? 0f : north && east ? 90f : south && east ? 180f : 270f;
                        PlaceRoad(pieces[RoadPiece.Curve], roadsRoot, "Road_Corner", counters, node, yaw, 1f);
                        extent[new Vector2(x, z)] = RoadWidth * 1.5f;

                        // One lamp inside the bend, facing the rounded corner.
                        Vector3 inward = new Vector3(east ? 1f : -1f, 0f, north ? 1f : -1f);
                        Vector3 arcCentre = node + inward * (RoadWidth * 1.5f);
                        PlaceLamp(lamp, lightsRoot, ref lampIndex, arcCentre - inward.normalized * 3.2f);
                        continue;
                    }

                    // Lamps at two opposite corners of the junction (north-west and south-east), inside the grid.
                    var corners = new[] { new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, -1f) };
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 p = node + corner * LampCornerOffset;
                        if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ)
                        {
                            continue;
                        }

                        PlaceLamp(lamp, lightsRoot, ref lampIndex, p);
                    }
                }
            }

            // Straights between neighbouring junctions.
            for (int zi = 0; zi < RoadGridZ.Length; zi++)
            {
                for (int xi = 0; xi < RoadGridX.Length - 1; xi++)
                {
                    float z = RoadGridZ[zi];
                    float a = RoadGridX[xi] + extent[new Vector2(RoadGridX[xi], z)];
                    float b = RoadGridX[xi + 1] - extent[new Vector2(RoadGridX[xi + 1], z)];
                    PlaceRoad(pieces[RoadPiece.Straight], roadsRoot, "Road_Straight", counters, new Vector3((a + b) * 0.5f, 0f, z),
                        90f, (b - a) / RoadWidth);
                }
            }

            for (int xi = 0; xi < RoadGridX.Length; xi++)
            {
                for (int zi = 0; zi < RoadGridZ.Length - 1; zi++)
                {
                    float x = RoadGridX[xi];
                    float a = RoadGridZ[zi] + extent[new Vector2(x, RoadGridZ[zi])];
                    float b = RoadGridZ[zi + 1] - extent[new Vector2(x, RoadGridZ[zi + 1])];
                    PlaceRoad(pieces[RoadPiece.Straight], roadsRoot, "Road_Straight", counters, new Vector3(x, 0f, (a + b) * 0.5f),
                        0f, (b - a) / RoadWidth);
                }
            }

            // Spur from the centre of the south edge down to the bridge.
            {
                float a = RoadToBridgeZ;
                float b = minZ - extent[new Vector2(0f, minZ)];
                PlaceRoad(pieces[RoadPiece.Straight], roadsRoot, "Road_Straight", counters, new Vector3(0f, 0f, (a + b) * 0.5f),
                    0f, (b - a) / RoadWidth);
            }
        }

        /// <summary>Places a road prefab at a grid point. The prefab pivot is the tile centre (the corner point for
        /// curves), so only yaw and - for straights - the length along local Z vary.</summary>
        private static void PlaceRoad(GameObject prefab, Transform parent, string baseName, Dictionary<string, int> counters,
            Vector3 position, float yaw, float lengthScale)
        {
            if (prefab == null)
            {
                return;
            }

            counters.TryGetValue(baseName, out int count);
            counters[baseName] = count + 1;
            var road = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            road.name = baseName + "_" + (count + 1).ToString("00");
            road.transform.position = position;
            road.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            road.transform.localScale = new Vector3(1f, 1f, lengthScale);
        }

        private static void PlaceLamp(GameObject prefab, Transform parent, ref int index, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            index++;
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            lamp.name = "StreetLight_" + index.ToString("00");
            lamp.transform.position = position;
            // Lanterns face the camera side a little differently each, so the row does not look stamped.
            lamp.transform.rotation = Quaternion.Euler(0f, 45f + (index % 3 - 1) * 12f, 0f);
        }

        // ------------------------------------------------------------------ Prefabs

        /// <summary>Wrapper around one pack piece: scaled to the grid, pivot at the tile centre, road top at
        /// RoadTopY, recoloured material, no colliders.</summary>
        private static GameObject EnsureRoadPrefab(string name, string modelName, Vector2 localPivot, Material material)
        {
            string path = RoadPrefabFolder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RoadPackFolder + modelName + ".dae");
            if (model == null)
            {
                Missing.Add("MISSING ASSET: " + RoadPackFolder + modelName + ".dae - " + name + " not created.");
                return null;
            }

            EnsureFolder(RoadPrefabFolder);
            var root = new GameObject(name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.transform.localScale = Vector3.one * RoadPieceScale;
            // Pack pieces have their top at y = 0.25 (units) and skirts below; sit the top just above the ground.
            instance.transform.localPosition = new Vector3(-localPivot.x * RoadPieceScale,
                RoadTopY - 0.25f * RoadPieceScale, -localPivot.y * RoadPieceScale);

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Material EnsureRoadMaterial()
        {
            return EnsurePaverMaterial();
        }

        // ------------------------------------------------------------------ Paved surface

        private const float PaverWorldSize = 3.2f;

        [MenuItem("Tools/Tower Defense/Base/Apply Road Paver Material")]
        public static void ApplyRoadPavers()
        {
            Material material = EnsurePaverMaterial();
            if (material == null)
            {
                return;
            }

            foreach (string name in new[] { "Road_Straight", "Road_Cross", "Road_T", "Road_Corner" })
            {
                string path = RoadPrefabFolder + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                // Older prefabs sat the kerb at 0.05 m, which buried the lane under the ground: lift the model child.
                foreach (Transform child in contents.transform)
                {
                    Vector3 p = child.localPosition;
                    child.localPosition = new Vector3(p.x, RoadTopY - 0.25f * RoadPieceScale, p.z);
                }

                foreach (MeshRenderer renderer in contents.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[BaseRoads] Road prefabs now use " + material.name);
        }

        /// <summary>Warm beige paved road: the pieces' palette still separates road from kerb (softened colours), and
        /// a world-projected paver pattern covers every piece at one constant stone size.</summary>
        private static Material EnsurePaverMaterial()
        {
            string path = MaterialsFolder + "/MAT_BaseRoad_Pavers.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("AlienDefense/BaseRoadPavers");
                if (shader == null)
                {
                    Debug.LogError("[BaseRoads] Shader AlienDefense/BaseRoadPavers is not compiled yet - re-run.");
                    return null;
                }

                material = new Material(shader) { name = "MAT_BaseRoad_Pavers", enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_Palette", EnsureSoftRoadPalette());
            material.SetTexture("_PaverTex", EnsurePaverTexture());
            material.SetFloat("_PaverWorldSize", PaverWorldSize);
            material.SetFloat("_PaverStrength", 0.55f);
            material.SetFloat("_AmbientBoost", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Palette with the pack's greys remapped to road beige and a kerb only a little lighter - the old
        /// near-white kerb pulled the eye away from the buildings.</summary>
        private static Texture2D EnsureSoftRoadPalette()
        {
            string path = TexturesFolder + "/T_BaseRoad_Palette.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            var source = new Texture2D(2, 2);
            source.LoadImage(System.IO.File.ReadAllBytes(RoadPackPalette));
            Color road = Hex("#D5B978");
            Color kerb = Hex("#DFC993");
            Color kerbLight = Hex("#E5D2A2");
            var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    bool grey = Mathf.Abs(c.r - c.g) < 0.03f && Mathf.Abs(c.g - c.b) < 0.03f;
                    if (grey)
                    {
                        c = c.r < 0.4f ? road : c.r < 0.6f ? kerb : kerbLight;
                    }

                    output.SetPixel(x, y, c);
                }
            }

            Object.DestroyImmediate(source);
            return SaveRoadTexture(output, path, FilterMode.Point, false, TextureWrapMode.Clamp);
        }

        /// <summary>Seamless 512 px paving: 4 x 8 rectangular stones (2:1) per repeat, i.e. 0.8 x 0.4 m each at a
        /// 3.2 m repeat. Grout is only mildly darker, stones get a soft bevel and a +/-3% brightness variation, so the
        /// pattern reads up close and stays calm from the gameplay camera. Greyscale: the material tints it.</summary>
        private static Texture2D EnsurePaverTexture()
        {
            string path = TexturesFolder + "/T_BaseRoad_Pavers_512.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 512;
            const int columns = 4;
            const int rows = 8;
            const float grout = 0.78f;
            const float groutHalfWidth = 2.5f;
            const float bevel = 5f;
            int stoneW = size / columns;
            int stoneH = size / rows;
            var random = new System.Random(9);
            var shade = new float[columns * rows];
            for (int i = 0; i < shade.Length; i++) shade[i] = 0.97f + (float)(random.NextDouble() - 0.5) * 0.06f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x / stoneW;
                    int cy = y / stoneH;
                    float lx = x % stoneW + 0.5f;
                    float ly = y % stoneH + 0.5f;
                    // Distance to the nearest stone edge (edges shared with the neighbour, so the tile is seamless).
                    float edge = Mathf.Min(Mathf.Min(lx, stoneW - lx), Mathf.Min(ly, stoneH - ly));
                    float value = shade[cy * columns + cx];
                    // Soft bevel toward the edge, then the grout line itself.
                    value *= Mathf.Lerp(0.93f, 1f, Mathf.Clamp01((edge - groutHalfWidth) / bevel));
                    float g = Mathf.Clamp01((edge - groutHalfWidth + 0.8f) / 1.6f);
                    value = Mathf.Lerp(grout, value, g);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            return SaveRoadTexture(texture, path, FilterMode.Trilinear, true, TextureWrapMode.Repeat);
        }

        private static Texture2D SaveRoadTexture(Texture2D texture, string path, FilterMode filter, bool mipmaps, TextureWrapMode wrap)
        {
            EnsureFolder(TexturesFolder);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = filter;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = wrap;
            importer.anisoLevel = mipmaps ? 2 : 1;
            importer.maxTextureSize = 512;
            importer.textureCompression = mipmaps ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The pack palette with its greys remapped to the map's sandy road tones: dark asphalt -> beige
        /// #D8BC7C, grey kerbs -> cream, light grey -> pale cream. Every other swatch is kept.</summary>
        private static Texture2D EnsureRoadPalette()
        {
            string path = TexturesFolder + "/T_Base_RoadPalette.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(TexturesFolder);
            var source = new Texture2D(2, 2);
            if (!System.IO.File.Exists(RoadPackPalette) || !source.LoadImage(System.IO.File.ReadAllBytes(RoadPackPalette)))
            {
                Missing.Add("MISSING ASSET: " + RoadPackPalette + " - roads use a flat beige instead.");
                source = new Texture2D(1, 1);
                source.SetPixel(0, 0, Hex("#D8BC7C"));
            }

            Color road = Hex("#D8BC7C");
            Color kerb = Hex("#EAD8AC");
            Color kerbLight = Hex("#F3E7C8");
            var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    bool grey = Mathf.Abs(c.r - c.g) < 0.03f && Mathf.Abs(c.g - c.b) < 0.03f && c.r < 0.98f;
                    if (grey)
                    {
                        c = c.r < 0.4f ? road : c.r < 0.6f ? kerb : kerbLight;
                    }

                    output.SetPixel(x, y, c);
                }
            }

            output.Apply();
            System.IO.File.WriteAllBytes(path, output.EncodeToPNG());
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Warm self-lit lantern glass. The emission keyword is enabled through the LocalKeyword API and the
        /// asset saved straight away - a plain EnableKeyword on a freshly created material did not survive.</summary>
        private static Material EnsureStreetLightGlass()
        {
            Material glass = EnsureMaterial("MAT_Base_StreetLightLamp", Hex("#FFE3A3"), 0.4f);
            glass.SetColor("_BaseColor", Hex("#FFE3A3"));
            glass.SetColor("_EmissionColor", Hex("#FFC857") * 1.6f);
            glass.SetKeyword(new UnityEngine.Rendering.LocalKeyword(glass.shader, "_EMISSION"), true);
            glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(glass);
            AssetDatabase.SaveAssetIfDirty(glass);
            return glass;
        }

        /// <summary>Small low-poly street light: stone foot, slim dark pole, warm lantern with a little roof. Built
        /// from primitives and baked to one mesh (two materials), so each lamp is a single renderer. The lantern is
        /// emissive - no real light, which would cost a pass per lamp on mobile.</summary>
        private static GameObject EnsureStreetLightPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Material metal = EnsureMaterial("MAT_Base_StreetLightMetal", Hex("#3E434C"), 0.25f);
            Material glass = EnsureStreetLightGlass();

            var root = new GameObject("BP_StreetLight");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 1.25f;

            GameObject Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
            {
                GameObject part = GameObject.CreatePrimitive(type);
                part.name = name;
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(body.transform, false);
                part.transform.localPosition = position;
                part.transform.localScale = scale;
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
                return part;
            }

            Part(PrimitiveType.Cylinder, "Foot", new Vector3(0f, 0.15f, 0f), new Vector3(0.46f, 0.15f, 0.46f), metal);
            Part(PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.6f, 0f), new Vector3(0.18f, 1.45f, 0.18f), metal);
            Part(PrimitiveType.Cube, "LanternBase", new Vector3(0f, 3.08f, 0f), new Vector3(0.46f, 0.08f, 0.46f), metal);
            Part(PrimitiveType.Cube, "Lantern", new Vector3(0f, 3.38f, 0f), new Vector3(0.42f, 0.52f, 0.42f), glass);
            Part(PrimitiveType.Cube, "Roof", new Vector3(0f, 3.7f, 0f), new Vector3(0.62f, 0.12f, 0.62f), metal);
            GameObject cap = Part(PrimitiveType.Cube, "RoofCap", new Vector3(0f, 3.83f, 0f), new Vector3(0.32f, 0.16f, 0.32f), metal);
            cap.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            EnsureFolder(MeshesFolder);
            ConstructionPlotVisualSetupEditor.BakeToSingleRenderer(root, MeshesFolder + "/SM_StreetLight.asset", castShadows: false);

            EnsureFolder(System.IO.Path.GetDirectoryName(LampPrefabPath).Replace('\\', '/'));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, LampPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }
    }
}
