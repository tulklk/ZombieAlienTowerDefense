using System.Collections.Generic;
using AlienDefense.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_03's art pass: stylised water, road markings, lighting/post look and the authored environment.
    ///
    /// Asset sources, in order of preference: Low Poly Tropical Beach (Aquaset: palms, umbrellas, chairs, boats,
    /// rope bridge, beach toys), Quaternius Ultimate Stylized Nature (trees, bushes, flowers, grass, rocks),
    /// Quaternius Farm Buildings (barns, silos, windmills, water tower, fences), Kenney Watercraft (boats, house
    /// boats, buoys, a ship), Low Poly Vehicle Pack Lite (cars), then the project's own Level 2 / Level 3 kits for
    /// what no pack covers (town houses, lighthouse, stilt houses, piers, market stalls, pigs, bus). Third-party
    /// prefabs are only instantiated: scale, rotation, disabled colliders and replacement materials are scene
    /// instance overrides, and the replacement materials are project-owned copies (see OwnedMaterial).
    ///
    /// Nothing is scattered at random. Zones are authored as compositions (a house lot = house + fence + car + tree +
    /// planting; a beach set = umbrella + chairs + toys; ...) laid out by hand along the two routes; the only pattern
    /// helpers are road verges, BuildNode surrounds and ground cover inside the painted lawns, all deterministic.
    /// Every placement goes through Put(), which refuses anything on a road, on a BuildNode clearing, in the base
    /// clearing or (for land objects) in water.</summary>
    public static partial class Level03Builder
    {
        private const string L2 = "Assets/_Game/Prefabs/Level02/";
        private const string L3 = "Assets/_Game/Prefabs/Level03/";
        private const string AQ = "Assets/Aquaset/LowPolyTropicalBeach/Prefabs/";
        private const string NA = "Assets/Ultimate Stylized Nature - May 2022/FBX/";
        private const string NatureTextures = "Assets/Ultimate Stylized Nature - May 2022/Textures/";
        private const string FA = "Assets/Farm Buildings - Sept 2018/FBX/";
        private const string KW = "Assets/kenney_watercraft-pack/Models/FBX format/";
        private const string VL = "Assets/Low poly vehicle pack lite/Prefab/";
        private const string VehicleMaterials = "Assets/Low poly vehicle pack lite/Material/";
        private const string OwnedMaterialFolder = ArtFolder + "/Materials";
        private const string Veg = L2 + "Vegetation/";
        private const string Prop = L2 + "Props/";
        private const string Bld = L2 + "Buildings/";
        private const string Prop3 = L3 + "Props/";
        private const string Bld3 = L3 + "Buildings/";

        private enum Where { Land, Water, Free }

        private static Transform _zones;
        private static int _placed;
        private static readonly List<string> Refused = new List<string>();
        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> OwnedMaterials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();

        // ------------------------------------------------------------------ water

        /// <summary>The water surface: a 1 m grid over the terrain (cells that are entirely land are left out) plus
        /// nothing beyond it - the game camera never sees past the terrain edge), with "distance to shore" baked into vertex colour for the StylizedWater
        /// shader's shallow band and foam line. One mesh, one material, opaque.</summary>
        private static void BuildStylizedWater(Transform environment)
        {
            Transform water = Group(environment, "Water");
            for (int i = water.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(water.GetChild(i).gameObject);
            }

            EnsureFolder(ArtFolder + "/Meshes");
            Material material = EnsureStylizedWaterMaterial();
            int nx = Mathf.RoundToInt(TerrainSize.x);
            int nz = Mathf.RoundToInt(TerrainSize.z);
            var masks = new float[nx + 1, nz + 1];
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iz = 0; iz <= nz; iz++)
                {
                    masks[ix, iz] = WaterMask(new Vector2(TerrainOrigin.x + ix, TerrainOrigin.z + iz));
                }
            }

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var index = new int[nx + 1, nz + 1];
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iz = 0; iz <= nz; iz++)
                {
                    index[ix, iz] = -1;
                }
            }

            int Vertex(int ix, int iz)
            {
                if (index[ix, iz] < 0)
                {
                    index[ix, iz] = vertices.Count;
                    vertices.Add(new Vector3(TerrainOrigin.x + ix, WaterY, TerrainOrigin.z + iz));
                    // The terrain crosses the water plane where the mask is ~0.31; shore = 1 there, 0 in open water.
                    float shore = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.31f, 0.97f, masks[ix, iz]));
                    colors.Add(new Color(shore, 0f, 0f, 1f));
                }

                return index[ix, iz];
            }

            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    float wettest = Mathf.Max(Mathf.Max(masks[ix, iz], masks[ix + 1, iz]), Mathf.Max(masks[ix, iz + 1], masks[ix + 1, iz + 1]));
                    // Land cells, and the strip beyond the top edge that the game camera can never reach.
                    if (wettest < 0.12f || TerrainOrigin.x + ix < MinX - 24f)
                    {
                        continue;
                    }

                    int a = Vertex(ix, iz), b = Vertex(ix + 1, iz), c = Vertex(ix + 1, iz + 1), d = Vertex(ix, iz + 1);
                    triangles.AddRange(new[] { a, d, c, a, c, b });
                }
            }

            string meshPath = ArtFolder + "/Meshes/WaterSurface.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "WaterSurface" };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var surface = new GameObject("WaterSurface");
            surface.transform.SetParent(water, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Report.Add("Water: stylised surface mesh (" + vertices.Count + " vertices, " + triangles.Count / 3 + " triangles, shore distance in vertex colour) with material "
                + (material != null ? material.name : "MISSING") + "; was one opaque URP Lit quad.");
        }

        private static Material EnsureStylizedWaterMaterial()
        {
            Shader shader = Shader.Find("AlienDefense/Level03/StylizedWater");
            if (shader == null)
            {
                Report.Add("WARNING: shader AlienDefense/Level03/StylizedWater not found.");
                return null;
            }

            // Tileable highlight pattern: thin bright curved ridges on black.
            string texturePath = ArtFolder + "/T_Level03_WaterPattern.png";
            if (!System.IO.File.Exists(texturePath))
            {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = x / (float)size * Mathf.PI * 2f;
                        float v = y / (float)size * Mathf.PI * 2f;
                        float r1 = 1f - Mathf.Abs(Mathf.Sin(u * 2f + Mathf.Sin(v * 3f) * 1.2f));
                        float r2 = 1f - Mathf.Abs(Mathf.Sin(v * 3f + Mathf.Sin(u * 2f + 1.3f) * 1.4f));
                        float value = Mathf.Clamp01(Mathf.Pow(Mathf.Max(r1, r2), 5f) * 0.9f + 0.28f * (0.5f + 0.5f * Mathf.Sin(u * 5f + v * 4f)));
                        texture.SetPixel(x, y, new Color(value, value, value, 1f));
                    }
                }

                texture.Apply();
                System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            string materialPath = ArtFolder + "/MAT_Level03_StylizedWater.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = shader;
            material.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_ShallowColor", new Color(0.45f, 0.96f, 0.90f, 1f));
            material.SetColor("_DeepColor", new Color(0.04f, 0.73f, 0.82f, 1f));
            material.SetFloat("_HighlightStrength", 0.3f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ look (lighting + post), scene-local

        /// <summary>Bright sunny daylight for this scene only: a warmer, stronger sun with firmer shadows and
        /// Level 3's own post-processing profile (a copy of Level 2's with more saturation and contrast - Level 2's
        /// asset is not touched). No depth of field, no motion blur.</summary>
        private static void SetupLook(Transform root)
        {
            Transform lighting = root.Find("Lighting");
            if (lighting == null)
            {
                Report.Add("WARNING: no Lighting group - look not applied.");
                return;
            }

            foreach (Light light in lighting.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional)
                {
                    continue;
                }

                light.intensity = 1.32f;
                light.color = new Color(1f, 0.96f, 0.86f);
                light.shadowStrength = 0.68f;
                EditorUtility.SetDirty(light);
                break;
            }

            Volume volume = lighting.GetComponentInChildren<Volume>(true);
            if (volume != null && volume.sharedProfile != null)
            {
                const string profilePath = "Assets/_Game/Settings/Volumes/PP_Level03.asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if (profile == null)
                {
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile), profilePath);
                    profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                }

                if (profile != null)
                {
                    if (profile.TryGet(out ColorAdjustments color))
                    {
                        color.saturation.Override(24f);
                        color.contrast.Override(14f);
                        color.postExposure.Override(0.08f);
                        EditorUtility.SetDirty(color);
                    }

                    EditorUtility.SetDirty(profile);
                    volume.sharedProfile = profile;
                    EditorUtility.SetDirty(volume);
                }
            }

            Report.Add("Look: sun 1.32 warm, shadow strength 0.68; own post profile PP_Level03 (saturation 24, contrast 14); fog 110-320 m.");
        }

        // ------------------------------------------------------------------ road markings

        /// <summary>The faded yellow dashed centre line of both routes: one flat mesh per route that follows the
        /// spline (so it bends with every curve), unlit, no shadows - two draw calls for the whole map.</summary>
        private static void BuildRoadMarkings(Transform environment)
        {
            Transform old = environment.Find("RoadMarkings");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Transform holder = Group(environment, "RoadMarkings");
            string materialPath = ArtFolder + "/MAT_Level03_RoadLine.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetColor("_BaseColor", new Color(0.90f, 0.74f, 0.26f, 1f));
            EditorUtility.SetDirty(material);
            int routeIndex = 0;
            foreach (List<Vector2> route in new[] { _leftSamples, _rightSamples })
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                float total = RouteLength(route);
                float travelled = 0f;
                for (int i = 1; i < route.Count; i++)
                {
                    Vector2 a = route[i - 1];
                    Vector2 b = route[i];
                    float step = Vector2.Distance(a, b);
                    float mid = travelled + step * 0.5f;
                    travelled += step;
                    // 1 m dash, 1.6 m gap; none inside the barn or on the last metres into the base.
                    if (mid < 9f || mid > total - 5f || mid % 2.6f > 1f)
                    {
                        continue;
                    }

                    Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * 0.065f;
                    int v = vertices.Count;
                    const float y = GroundY + 0.04f;
                    vertices.Add(new Vector3(a.x - side.x, y, a.y - side.y));
                    vertices.Add(new Vector3(a.x + side.x, y, a.y + side.y));
                    vertices.Add(new Vector3(b.x + side.x, y, b.y + side.y));
                    vertices.Add(new Vector3(b.x - side.x, y, b.y - side.y));
                    triangles.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2, v, v + 1, v + 2, v, v + 2, v + 3 }); // both windings
                }

                string name = routeIndex == 0 ? "CentreLine_Left" : "CentreLine_Right";
                string meshPath = ArtFolder + "/Meshes/" + name + ".asset";
                EnsureFolder(ArtFolder + "/Meshes");
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null)
                {
                    mesh = new Mesh { name = name };
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }

                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                var go = new GameObject(name);
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                routeIndex++;
            }

            Report.Add("Road markings: thin dashed yellow centre line on both routes (2 meshes, 13 cm wide, 1 m dash / 1.6 m gap).");
        }

        private static float RouteLength(List<Vector2> route)
        {
            float total = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                total += Vector2.Distance(route[i - 1], route[i]);
            }

            return total;
        }

        // ------------------------------------------------------------------ placement

        private static bool IsThirdParty(string path) => !path.StartsWith(L2) && !path.StartsWith(L3);
        /// <summary>World size the packs' models get in this level (their own scale is toy-sized or oversized).</summary>
        private static float BaseScale(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith(AQ))
            {
                if (name.StartsWith("PalmTree3")) return 2f;
                if (name.StartsWith("PalmTree")) return 1.8f;
                if (name.StartsWith("Umbrella")) return 1f;
                if (name.StartsWith("Chair")) return 0.72f;
                if (name.StartsWith("LifeguardChair")) return 1.4f;
                if (name.StartsWith("LifeSaver")) return 0.75f;
                if (name.StartsWith("SurfBoard")) return 1.25f;
                if (name.StartsWith("BeachBall")) return 0.5f;
                if (name.StartsWith("Bucket")) return 0.4f;
                if (name.StartsWith("SailBoat")) return 2.4f;
                if (name.StartsWith("Boat")) return 2f;
                if (name.StartsWith("Bridge")) return 3f;
                if (name.StartsWith("Dock")) return 3f;
                if (name.StartsWith("Fence")) return 1.5f;
                if (name.StartsWith("Bush")) return 1.2f;
                return 1f;
            }

            if (path.StartsWith(NA))
            {
                if (name.StartsWith("PalmTree")) return 1.3f;
                if (name.StartsWith("NormalTree")) return 1.6f;
                if (name.StartsWith("MapleTree")) return 1.4f;
                if (name.StartsWith("PineTree")) return 1.2f;
                if (name.StartsWith("DeadTree")) return 1.6f;
                if (name.StartsWith("Bush")) return 0.8f;
                if (name.StartsWith("Flower")) return 2.2f;
                if (name.StartsWith("Grass")) return 2.1f;
                if (name.StartsWith("Plant")) return 0.9f;
                if (name.StartsWith("Rock")) return 1.2f;
                return 1f;
            }

            if (path.StartsWith(FA))
            {
                if (name.StartsWith("Fence")) return 0.5f;
                if (name == "Windmill") return 0.7f;
                if (name == "TowerWindmill") return 0.62f;
                if (name.StartsWith("Silo")) return 0.72f;
                if (name == "WaterTower") return 0.8f;
                if (name == "Well" || name == "ChickenCoop") return 1f;
                return 0.85f; // barns
            }

            if (path.StartsWith(VL)) return 0.88f;
            return 1f;
        }

        /// <summary>Loose things the tractor beam may take (its size gate decides when); structures never.</summary>
        private static bool IsAbsorbableThirdParty(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith(FA))
            {
                return false;
            }

            if (path.StartsWith(AQ))
            {
                return !(name.StartsWith("Dock") || name.StartsWith("Bridge") || name.StartsWith("Fence") || name.StartsWith("Cloud"));
            }

            if (path.StartsWith(NA))
            {
                return true; // ground cover is absorbable too (the user wants plenty to hoover up for XP)
            }

            if (path.StartsWith(KW))
            {
                return !(name.StartsWith("ship") || name.StartsWith("boat-house"));
            }

            return true;
        }

        /// <summary>Places one prefab instance. <paramref name="radius"/> is its footprint radius for the clearance
        /// checks; <paramref name="variant"/> picks a body colour for the vehicle pack ("Taxi", "School bus", "Red"...).
        /// Returns null when the spot is refused.</summary>
        private static GameObject Put(string zone, string path, float x, float z, float yaw = 0f, float scale = 1f,
            Where where = Where.Land, float radius = 0.5f, float y = float.NaN, string variant = null)
        {
            var p = new Vector2(x, z);
            string label = zone + ": " + System.IO.Path.GetFileNameWithoutExtension(path) + " (" + x.ToString("F1") + ", " + z.ToString("F1") + ")";
            float water = WaterMask(p);
            if (where == Where.Land && water > 0.08f)
            {
                Refused.Add(label + " - in water");
                return null;
            }

            if (where == Where.Water && water < 0.5f)
            {
                Refused.Add(label + " - not in water");
                return null;
            }

            bool tall = path.Contains("Tree") || path.Contains("Palm"); // canopies must not hang over a tower spot
            if (where != Where.Water)
            {
                if (ExactRoadDistance(p) < RoadWidth * 0.5f + 0.3f + radius)
                {
                    Refused.Add(label + " - on a road");
                    return null;
                }

                for (int i = 0; i < BuildNodeXZ.Length; i++)
                {
                    if (Vector2.Distance(p, BuildNodeXZ[i]) < (tall ? 4.6f : 2.9f) + radius)
                    {
                        Refused.Add(label + " - on BuildNode " + (i + 1));
                        return null;
                    }
                }

                // Base model, landing pad and the fight in front of them stay open.
                if (p.x > BaseTargetXZ.x - 3f && p.x < BaseTargetXZ.x + 17f && Mathf.Abs(p.y) < 6.5f + radius)
                {
                    Refused.Add(label + " - in the base clearing");
                    return null;
                }
            }

            if (!PrefabCache.TryGetValue(path, out GameObject prefab))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                PrefabCache[path] = prefab;
            }

            if (prefab == null)
            {
                Refused.Add(label + " - prefab not found");
                return null;
            }

            bool thirdParty = IsThirdParty(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(_zones, zone));
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
            go.transform.localScale = prefab.transform.localScale * (scale * BaseScale(path));
            float groundY = where == Where.Water ? WaterY : HeightAt(p);
            go.transform.position = new Vector3(x, float.IsNaN(y) ? groundY : y, z);
            if (thirdParty)
            {
                // No colliders on scenery in this level (the UFO flies through it, and a stray Default-layer collider
                // near a road would catch the enemies' ground probe). Instance overrides only.
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }

                FixMaterials(go, path, variant);
                if (float.IsNaN(y))
                {
                    Bounds bounds = WorldBounds(go);
                    float targetBottom = where == Where.Water ? WaterY - 0.28f : groundY - 0.03f;
                    go.transform.position += Vector3.up * (targetBottom - bounds.min.y);
                }

                if (IsAbsorbableThirdParty(path))
                {
                    var absorbable = go.AddComponent<TractorAbsorbableProp>();
                    var so = new SerializedObject(absorbable);
                    so.FindProperty("_visualRoot").objectReferenceValue = go.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            string category = Categorise(path);
            Counts.TryGetValue(category, out int count);
            Counts[category] = count + 1;
            _placed++;
            return go;
        }

        private static string Categorise(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (name.Contains("dock") || name.Contains("bridge")) return "docks/bridges";
            if (path.Contains("/Buildings/") || (path.StartsWith(FA) && !name.StartsWith("fence")) || name.StartsWith("boat-house")) return "buildings";
            if (name.Contains("tree") || name.Contains("palm")) return "trees/palms";
            if (path.StartsWith(VL) || name.EndsWith("bus")) return "vehicles";
            if (path.StartsWith(KW) || name.Contains("boat") || name.Contains("buoy") || name.Contains("lifesaver")) return "boats/water props";
            if (name.Contains("fence")) return "fence segments";
            return "small props/plants";
        }

        /// <summary>Third-party materials this URP project cannot use as they are - the nature pack's FBX materials
        /// import without their textures, the vehicle pack's use the Built-in Standard shader - are swapped, on the
        /// scene instance only, for project-owned URP Lit copies under Art/Level03/Materials. The packs' own
        /// material and prefab assets are never edited.</summary>
        private static void FixMaterials(GameObject go, string path, string variant)
        {
            bool nature = path.StartsWith(NA);
            bool vehicle = path.StartsWith(VL);
            if (!nature && !vehicle)
            {
                return;
            }

            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    Material owned = nature ? OwnedNatureMaterial(materials[i].name) : OwnedVehicleMaterial(variant ?? materials[i].name);
                    if (owned != null)
                    {
                        materials[i] = owned;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static Material OwnedNatureMaterial(string sourceName)
        {
            string textureName = sourceName == "Rock" ? "Rocks" : sourceName == "Lit" ? "Grass" : sourceName;
            if (!System.IO.File.Exists(NatureTextures + textureName + ".png"))
            {
                return null;
            }

            bool cutout = textureName.Contains("Leaves") || textureName == "Flowers" || textureName == "Grass";
            return OwnedMaterial("MAT3_Nature_" + textureName, NatureTextures + textureName + ".png", cutout);
        }

        private static Material OwnedVehicleMaterial(string colourName)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(VehicleMaterials + colourName + ".mat");
            if (source == null || source.mainTexture == null)
            {
                return null;
            }

            return OwnedMaterial("MAT3_Vehicle_" + colourName.Replace(" ", ""), AssetDatabase.GetAssetPath(source.mainTexture), false);
        }

        private static Material OwnedMaterial(string name, string texturePath, bool cutout)
        {
            if (OwnedMaterials.TryGetValue(name, out Material cached) && cached != null)
            {
                return cached;
            }

            EnsureFolder(OwnedMaterialFolder);
            string materialPath = OwnedMaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.45f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }

            EditorUtility.SetDirty(material);
            OwnedMaterials[name] = material;
            return material;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        /// <summary>A point given in an object's own frame (right, forward) for an object at (x, z) facing yaw.</summary>
        private static Vector2 Loc(float x, float z, float yaw, float right, float forward)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector2(x + Mathf.Sin(r) * forward + Mathf.Cos(r) * right, z + Mathf.Cos(r) * forward - Mathf.Sin(r) * right);
        }

        private static readonly Dictionary<string, Vector2> FenceInfo = new Dictionary<string, Vector2>();
        /// <summary>Length of one fence segment in this level (x) and whether it runs along local X (y = 1) or Z (0).</summary>
        private static Vector2 MeasureFence(string path)
        {
            if (FenceInfo.TryGetValue(path, out Vector2 info))
            {
                return info;
            }

            info = new Vector2(2f, 1f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                temp.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.rotation);
                temp.transform.localScale = prefab.transform.localScale * BaseScale(path);
                Bounds bounds = WorldBounds(temp);
                Object.DestroyImmediate(temp);
                bool alongX = bounds.size.x >= bounds.size.z;
                info = new Vector2(Mathf.Max(0.5f, alongX ? bounds.size.x : bounds.size.z), alongX ? 1f : 0f);
            }

            FenceInfo[path] = info;
            return info;
        }

        /// <summary>A ring of fence segments laid end to end, with a gate gap toward <paramref name="gateYaw"/>.</summary>
        private static void FenceRing(string zone, string path, float cx, float cz, float r, float gateYaw)
        {
            Vector2 info = MeasureFence(path);
            int count = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * r / info.x));
            float radius = info.x * 0.5f / Mathf.Tan(Mathf.PI / count);
            for (int k = 0; k < count; k++)
            {
                float angle = k * 360f / count;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, gateYaw)) < 185f / count)
                {
                    continue;
                }

                float rad = angle * Mathf.Deg2Rad;
                Put(zone, path, cx + Mathf.Sin(rad) * radius, cz + Mathf.Cos(rad) * radius, angle + 90f - info.y * 90f, 1f, Where.Land, 0.2f);
            }
        }

        /// <summary>A straight run of fence segments from a to b, laid end to end.</summary>
        private static void FenceLine(string zone, string path, Vector2 a, Vector2 b)
        {
            Vector2 info = MeasureFence(path);
            float length = Vector2.Distance(a, b);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / info.x));
            float yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg - info.y * 90f;
            Vector2 direction = (b - a).normalized;
            Vector2 start = (a + b) * 0.5f - direction * (count * info.x * 0.5f);
            for (int k = 0; k < count; k++)
            {
                Vector2 p = start + direction * ((k + 0.5f) * info.x);
                Put(zone, path, p.x, p.y, yaw, 1f, Where.Land, 0.2f);
            }
        }

        // ------------------------------------------------------------------ compositions

        private static readonly string[] SmallPlants =
        {
            NA + "Grass_Large.fbx", NA + "Flower_1_Clump.fbx", NA + "Grass_Small.fbx", NA + "Flower_3_Clump.fbx", NA + "Plant_2.fbx",
            NA + "Flower_4_Clump.fbx", NA + "Grass_Large.fbx", NA + "Flower_2_Clump.fbx", NA + "Flower_5_Clump.fbx",
        };
        private static readonly string[] Bushes =
        {
            NA + "Bush.fbx", NA + "Bush_Flowers.fbx", AQ + "Bush.prefab", NA + "Bush_Small_Flowers.fbx", NA + "Plant_Flowers.fbx", NA + "Bush_Small.fbx",
        };
        private static readonly string[] Palms =
        {
            NA + "PalmTree_1.fbx", NA + "PalmTree_2.fbx", NA + "PalmTree_5.fbx", NA + "PalmTree_3.fbx", NA + "PalmTree_4.fbx", NA + "PalmTree_5.fbx",
        };
        private static readonly string[] Rocks = { NA + "Rock_1.fbx", NA + "Rock_2.fbx", NA + "Rock_5.fbx", NA + "Rock_3.fbx", NA + "Rock_4.fbx" };
        /// <summary>A house on its lot: the house, a picket fence across the front with a gap for the path, planting
        /// at the corners and along the front, a tree behind, a mailbox, and (optionally) a car parked beside it.</summary>
        private static void HouseLot(string zone, string house, float x, float z, float yaw, float scale, float radius,
            int style, string car = null, string carVariant = null)
        {
            if (Put(zone, house, x, z, yaw, scale, Where.Land, radius) == null)
            {
                return;
            }

            float front = radius + 1.5f;
            FenceLine(zone, Prop + "PF_Fence_Picket.prefab", Loc(x, z, yaw, -radius - 0.4f, front), Loc(x, z, yaw, -1.1f, front));
            FenceLine(zone, Prop + "PF_Fence_Picket.prefab", Loc(x, z, yaw, 1.1f, front), Loc(x, z, yaw, radius + 0.4f, front));
            Vector2 q = Loc(x, z, yaw, -radius - 0.2f, radius * 0.75f);
            Put(zone, Bushes[style % Bushes.Length], q.x, q.y, style * 53f);
            q = Loc(x, z, yaw, radius + 0.2f, radius * 0.75f);
            Put(zone, Bushes[(style + 2) % Bushes.Length], q.x, q.y, style * 91f);
            for (int k = 0; k < 4; k++)
            {
                q = Loc(x, z, yaw, -radius + 0.5f + k * (radius * 2f - 1f) / 3f, front - 0.7f);
                Put(zone, SmallPlants[(style + k * 2) % SmallPlants.Length], q.x, q.y, k * 77f, 1f, Where.Land, 0.15f);
            }

            q = Loc(x, z, yaw, (style % 2 == 0 ? -1f : 1f) * radius * 0.8f, -radius - 1.6f);
            Put(zone, style % 3 == 0 ? NA + "NormalTree_5.fbx" : style % 3 == 1 ? Palms[style % Palms.Length] : NA + "MapleTree_4.fbx", q.x, q.y, style * 37f, 1f, Where.Land, 0.6f);
            q = Loc(x, z, yaw, radius + 0.7f, front + 0.5f);
            Put(zone, style % 2 == 0 ? Prop + "PF_Mailbox_01.prefab" : Prop3 + "PF3_TrashBin.prefab", q.x, q.y, yaw);
            q = Loc(x, z, yaw, -radius - 0.9f, front + 0.6f);
            Put(zone, SmallPlants[(style + 5) % SmallPlants.Length], q.x, q.y, 0f, 1.1f, Where.Land, 0.15f);
            if (car != null)
            {
                q = Loc(x, z, yaw, (style % 2 == 0 ? 1f : -1f) * (radius + 2.1f), 0.6f);
                Put(zone, car, q.x, q.y, yaw + (style % 2 == 0 ? 6f : 174f), 1f, Where.Land, 1.3f, float.NaN, carVariant);
            }
        }

        /// <summary>A planted patch: one tree or palm, two bushes, a rock and a handful of small plants.
        /// kind 0 = palm, 1 = broadleaf tree, 2 = dry (dead tree, rocks), 3 = cactus.</summary>
        private static void VegPatch(string zone, float x, float z, int kind, int style)
        {
            float turn = style * 67f;
            string tree = kind == 0 ? Palms[style % Palms.Length]
                : kind == 1 ? (style % 2 == 0 ? NA + "NormalTree_5.fbx" : NA + "MapleTree_4.fbx")
                : kind == 2 ? (style % 2 == 0 ? NA + "DeadTree_5.fbx" : NA + "DeadTree_9.fbx")
                : L3 + "Vegetation/PF3_Cactus_01.prefab";
            Put(zone, tree, x, z, turn, 0.92f + (style % 3) * 0.08f, Where.Land, 0.6f);
            Vector2 q = Loc(x, z, turn, 1.5f, 0.6f);
            Put(zone, kind == 2 ? Veg + "PF_Shrub_Dead_01.prefab" : kind == 3 ? L3 + "Vegetation/PF3_Cactus_02.prefab" : Bushes[style % Bushes.Length], q.x, q.y, turn);
            q = Loc(x, z, turn, -1.3f, 1.1f);
            Put(zone, kind == 2 ? Veg + "PF_Weed_Orange.prefab" : Bushes[(style + 3) % Bushes.Length], q.x, q.y, turn + 90f, 0.9f);
            q = Loc(x, z, turn, -0.9f, -1.4f);
            Put(zone, Rocks[style % Rocks.Length], q.x, q.y, turn * 2f, kind == 2 ? 1.3f : 0.9f);
            float[] rights = { 2.2f, 0.4f, -2.1f, 1.2f, -0.4f, 2.6f };
            float[] forwards = { -0.8f, 2.1f, -0.3f, -2.2f, 2.8f, 1.9f };
            for (int k = 0; k < rights.Length; k++)
            {
                q = Loc(x, z, turn, rights[k], forwards[k]);
                string plant = kind == 2 ? (k % 2 == 0 ? Veg + "PF_Grass_03.prefab" : NA + "Grass_Small.fbx") : SmallPlants[(style + k) % SmallPlants.Length];
                Put(zone, plant, q.x, q.y, k * 59f + turn, 1f, Where.Land, 0.15f);
            }
        }

        /// <summary>Beach set: an umbrella, two deck chairs under it, a ball, a bucket, and a surfboard or sandcastle.</summary>
        private static void BeachSet(string zone, float x, float z, float yaw, int style)
        {
            string[] umbrellas = { AQ + "Umbrella1.prefab", AQ + "Umbrella1 (1).prefab", AQ + "Umbrella1 (2).prefab" };
            Put(zone, umbrellas[style % 3], x, z, yaw, 1f, Where.Land, 0.6f);
            Vector2 q = Loc(x, z, yaw, -1.5f, 0.5f);
            Put(zone, AQ + "Chair.prefab", q.x, q.y, yaw + 8f, 1f, Where.Land, 0.5f);
            q = Loc(x, z, yaw, 1.5f, 0.4f);
            Put(zone, AQ + "Chair (1).prefab", q.x, q.y, yaw - 10f, 1f, Where.Land, 0.5f);
            q = Loc(x, z, yaw, 0.4f, 2.1f);
            Put(zone, AQ + "BeachBall" + (style % 3 + 1) + ".prefab", q.x, q.y, 0f, 1f, Where.Land, 0.2f);
            q = Loc(x, z, yaw, -0.8f, 2.4f);
            Put(zone, AQ + "Bucket.prefab", q.x, q.y, 0f, 1f, Where.Land, 0.2f);
            q = Loc(x, z, yaw, 2.6f, 1.6f);
            Put(zone, style % 2 == 0 ? AQ + "SandCastle.prefab" : AQ + "SurfBoard.prefab", q.x, q.y, yaw + 40f, 1f, Where.Land, 0.4f);
            q = Loc(x, z, yaw, -2.7f, 1.4f);
            Put(zone, style % 2 == 0 ? AQ + "SurfBoard (1).prefab" : AQ + "LifeSaver.prefab", q.x, q.y, yaw - 30f, 1f, Where.Land, 0.4f);
        }

        /// <summary>Market set: a stall, a produce table, an umbrella table, open crates, sacks and loose produce.</summary>
        private static void MarketSet(string zone, float x, float z, float yaw, int style)
        {
            Put(zone, Bld + "PF_MarketStall_0" + (style % 3 + 1) + ".prefab", x, z, yaw, 1f, Where.Land, 1.5f);
            Vector2 q = Loc(x, z, yaw, 0.2f, 3.7f);
            Put(zone, Prop3 + "PF3_MarketTable_0" + (style % 2 + 1) + ".prefab", q.x, q.y, yaw, 1f, Where.Land, 0.9f);
            q = Loc(x, z, yaw, 0f, 7.1f);
            Put(zone, style % 2 == 0 ? Prop3 + "PF3_Umbrella_Market.prefab" : Prop3 + "PF3_Umbrella_Red.prefab", q.x, q.y, 0f, 1f, Where.Land, 0.7f);
            q = Loc(x, z, yaw, 2.1f, 1.6f);
            Put(zone, Prop + "PF_Crate_Open.prefab", q.x, q.y, yaw + 20f);
            q = Loc(x, z, yaw, -2f, 2.1f);
            Put(zone, Prop + "PF_Crate_Open_02.prefab", q.x, q.y, yaw + 70f);
            q = Loc(x, z, yaw, 2.1f, 4.7f);
            Put(zone, style % 2 == 0 ? Prop + "PF_Watermelon.prefab" : Prop + "PF_Pumpkin.prefab", q.x, q.y);
            q = Loc(x, z, yaw, -2f, 5.2f);
            Put(zone, Prop + "PF_Sack_0" + (style % 2 + 1) + ".prefab", q.x, q.y, yaw + 30f);
            q = Loc(x, z, yaw, -1.9f, 0.2f);
            Put(zone, Prop + "PF_Barrel.prefab", q.x, q.y);
            q = Loc(x, z, yaw, 2.2f, 7.4f);
            Put(zone, Prop + "PF_Cabbage.prefab", q.x, q.y);
        }

        /// <summary>Planting and a few props on the side of a BuildNode away from the road, outside its clearing - so
        /// the node reads as a deliberate tower spot instead of a ring on bare sand.</summary>
        private static void DressBuildNodes()
        {
            for (int i = 0; i < BuildNodeXZ.Length; i++)
            {
                Vector2 node = BuildNodeXZ[i];
                Vector2 nearest = node;
                float best = float.MaxValue;
                foreach (List<Vector2> route in new[] { _leftSamples, _rightSamples })
                {
                    foreach (Vector2 s in route)
                    {
                        float d = (s - node).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            nearest = s;
                        }
                    }
                }

                Vector2 away = (node - nearest).sqrMagnitude > 0.01f ? (node - nearest).normalized : Vector2.right;
                float yaw = Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg;
                float[] angles = { -65f, -25f, 20f, 60f, 110f, -110f, 150f, -150f };
                string[] items =
                {
                    Rocks[i % Rocks.Length], Bushes[i % Bushes.Length], i % 2 == 0 ? Prop + "PF_Crate.prefab" : Prop + "PF_Barrel.prefab",
                    SmallPlants[i % SmallPlants.Length], SmallPlants[(i + 3) % SmallPlants.Length], SmallPlants[(i + 5) % SmallPlants.Length],
                    Rocks[(i + 2) % Rocks.Length], SmallPlants[(i + 1) % SmallPlants.Length],
                };
                for (int k = 0; k < angles.Length; k++)
                {
                    float a = (yaw + angles[k]) * Mathf.Deg2Rad;
                    float distance = 3.5f + (k % 3) * 0.45f;
                    Put("BuildNodeSurrounds", items[k], node.x + Mathf.Sin(a) * distance, node.y + Mathf.Cos(a) * distance, k * 83f + i * 31f, 1f, Where.Land, 0.3f);
                }
            }
        }

        /// <summary>Ground cover inside the painted lawns: grass tufts and flower clumps on a 2.3 m lattice, each
        /// nudged and chosen by a fixed hash of its cell - a planted look that rebuilds identically every time.</summary>
        private static void DressLawns()
        {
            for (int l = 0; l < Lawns.Length; l++)
            {
                Vector4 lawn = Lawns[l];
                for (float dx = -lawn.z; dx <= lawn.z; dx += 2.3f)
                {
                    for (float dz = -lawn.w; dz <= lawn.w; dz += 2.3f)
                    {
                        if (new Vector2(dx / lawn.z, dz / lawn.w).magnitude > 0.92f)
                        {
                            continue;
                        }

                        int cx = Mathf.RoundToInt((lawn.x + dx) * 10f);
                        int cz = Mathf.RoundToInt((lawn.y + dz) * 10f);
                        float h = Hash(cx, cz);
                        if (h > 0.62f)
                        {
                            continue;
                        }

                        float x = lawn.x + dx + (Hash(cx + 7, cz) - 0.5f) * 1.3f;
                        float z = lawn.y + dz + (Hash(cx, cz + 13) - 0.5f) * 1.3f;
                        Put("LawnPlanting", SmallPlants[(int)(h * 97f) % SmallPlants.Length], x, z, h * 720f, 0.85f + h * 0.4f, Where.Land, 0.15f);
                    }
                }
            }
        }

        /// <summary>Road shoulders: a small plant every 3.3 m alternating sides, and every third step a bigger one
        /// (bush, rock, palm, cactus) further out on the other side; dry and dead near the barns, tropical through
        /// the middle, flowering toward the base.</summary>
        private static void DressVerges()
        {
            string[] dry = { Veg + "PF_Shrub_Dead_01.prefab", NA + "Grass_Small.fbx", Rocks[0], Veg + "PF_Weed_Orange.prefab", Veg + "PF_Grass_03.prefab" };
            string[] middle = { NA + "Grass_Large.fbx", L3 + "Vegetation/PF3_Cactus_02.prefab", NA + "Plant_2.fbx", Veg + "PF_Weed_Yellow.prefab", NA + "Flower_3_Clump.fbx", NA + "Grass_Small.fbx", NA + "Plant_1.fbx" };
            string[] lush = { NA + "Flower_1_Clump.fbx", NA + "Grass_Large.fbx", NA + "Flower_4_Clump.fbx", NA + "Plant_Flowers.fbx", NA + "Flower_2_Clump.fbx", NA + "Grass_Small.fbx", NA + "Flower_5_Clump.fbx" };
            string[] dryOuter = { Rocks[1], NA + "DeadTree_9.fbx", Rocks[4], Veg + "PF_Shrub_Dead_01.prefab", NA + "DeadTree_5.fbx" };
            string[] middleOuter = { L3 + "Vegetation/PF3_Cactus_01.prefab", Bushes[0], Palms[1], Rocks[2], Bushes[2], Palms[3] };
            string[] lushOuter = { Bushes[1], Palms[0], Bushes[3], NA + "NormalTree_5.fbx", Bushes[4], Palms[2] };
            foreach (List<Vector2> route in new[] { _leftSamples, _rightSamples })
            {
                string zone = route == _leftSamples ? "VergeLeft" : "VergeRight";
                float total = RouteLength(route);
                float travelled = 0f;
                float next = 10f;
                int k = route == _leftSamples ? 0 : 4; // the two routes are mirror images; do not plant them identically
                for (int i = 1; i < route.Count; i++)
                {
                    travelled += Vector2.Distance(route[i - 1], route[i]);
                    if (travelled < next)
                    {
                        continue;
                    }

                    next += 2.4f;
                    Vector2 direction = (route[i] - route[i - 1]).normalized;
                    Vector2 normal = new Vector2(-direction.y, direction.x) * (k % 2 == 0 ? 1f : -1f);
                    float fraction = travelled / total;
                    string[] set = fraction < 0.2f ? dry : fraction < 0.7f ? middle : lush;
                    Vector2 p = route[i] + normal * (RoadWidth * 0.5f + 0.7f + (k % 3) * 0.45f);
                    Put(zone, set[k % set.Length], p.x, p.y, k * 47f, 0.9f + (k % 4) * 0.08f, Where.Land, 0.15f);
                    if (k % 2 == 1)
                    {
                        string[] outer = fraction < 0.2f ? dryOuter : fraction < 0.7f ? middleOuter : lushOuter;
                        Vector2 q = route[i] - normal * (RoadWidth * 0.5f + 2.2f + (k % 2) * 0.8f);
                        Put(zone, outer[(k / 2) % outer.Length], q.x, q.y, k * 71f, 0.85f + (k % 5) * 0.06f, Where.Land, 0.55f);
                    }

                    k++;
                }
            }
        }

        // ------------------------------------------------------------------ zones

        private static void Dress(Transform root)
        {
            Transform old = root.Find("Zones");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            _zones = Group(root, "Zones");
            _placed = 0;
            Refused.Clear();
            PrefabCache.Clear();
            OwnedMaterials.Clear();
            Counts.Clear();
            FenceInfo.Clear();
            SetupLook(root);

            // ================= ZONE A: the two haunted barns (mirrored; the right one is the boss route).
            foreach (float s in new[] { -1f, 1f })
            {
                string zone = s < 0f ? "A_SpawnBarn_Left" : "A_SpawnBarn_Right";
                Put(zone, FA + "Silo.fbx", -46.4f, s * 45.4f, 0f, 1f, Where.Land, 1.4f);
                Put(zone, NA + "DeadTree_5.fbx", -36f, s * 43.8f, 20f, 1.1f);
                Put(zone, NA + "DeadTree_9.fbx", -37.2f, s * 31.2f, 140f, 1.2f);
                Put(zone, Veg + "PF_Tree_Dead_01.prefab", -45.6f, s * 30.6f, 80f, 1.2f);
                Put(zone, NA + "DeadTree_5.fbx", -31f, s * 46.4f, 250f, 0.9f);
                FenceLine(zone, Prop + "PF_Fence_Weathered.prefab", new Vector2(-34f, s * 40.9f), new Vector2(-26.5f, s * 40.9f));
                FenceLine(zone, Prop + "PF_Fence_Weathered.prefab", new Vector2(-34f, s * 34.1f), new Vector2(-28.5f, s * 34.1f));
                Put(zone, Prop + "PF_Rock_03.prefab", -33f, s * 44.6f, 30f, 1.1f, Where.Land, 1f);
                Put(zone, Rocks[4], -31.2f, s * 31.4f, 110f, 1.5f, Where.Land, 0.7f);
                Put(zone, Rocks[1], -29f, s * 43.4f, 200f, 1.3f, Where.Land, 0.6f);
                Put(zone, Rocks[2], -38.6f, s * 44.9f, 20f, 1.4f, Where.Land, 0.6f);
                Put(zone, Prop + "PF_Barrel.prefab", -35.4f, s * 41.8f);
                Put(zone, Prop + "PF_Barrel_Open.prefab", -34.6f, s * 42.5f, 40f);
                Put(zone, Prop + "PF_MetalBarrel.prefab", -35f, s * 33.2f);
                Put(zone, Prop + "PF_Crate.prefab", -33.6f, s * 33f, 25f);
                Put(zone, Prop + "PF_Crate_Open.prefab", -32.4f, s * 32.6f, 70f);
                Put(zone, Prop + "PF_WagonWheel.prefab", -34.2f, s * 41.3f, 15f);
                Put(zone, Prop + "PF_Scarecrow.prefab", -30f, s * 44.6f, s < 0f ? 60f : 120f);
                Put(zone, Prop + "PF_Stones_01.prefab", -27f, s * 42f);
                Put(zone, Prop + "PF_Stones_02.prefab", -31.5f, s * 41.8f, 90f);
                Put(zone, Prop + "PF_LogPile.prefab", -41f, s * 46.4f, 30f);
                Put(zone, Prop + "PF_Stump.prefab", -39.5f, s * 30.2f);
                Put(zone, Prop + "PF_Hay_Small.prefab", -36.6f, s * 32.6f, 20f);
                VegPatch(zone, -27.4f, s * 45.2f, 2, s < 0f ? 0 : 3);
                VegPatch(zone, -42.6f, s * 29.6f, 2, s < 0f ? 1 : 4);
            }

            // Inside the bends beside the barns: an open barn on the left, a small barn yard on the right (its U holds node 02).
            Put("A_SpawnBarn_Left", FA + "OpenBarn.fbx", -25.6f, -30.2f, 90f, 1f, Where.Land, 2.6f);
            Put("A_SpawnBarn_Left", Prop + "PF_Hay_Bale.prefab", -22.6f, -27.8f, 30f, 1f, Where.Land, 0.8f);
            Put("A_SpawnBarn_Left", Prop + "PF_Tool_Shovel.prefab", -22.8f, -32.2f, 40f);
            Put("A_SpawnBarn_Left", Prop + "PF_Barrel_02.prefab", -23.2f, -33.4f);
            Put("A_SpawnBarn_Left", Rocks[3], -28.4f, -33.6f, 70f, 1.3f);
            Put("A_SpawnBarn_Left", FA + "WaterTower.fbx", -31.5f, -15.9f, 20f, 1f, Where.Land, 1.6f);
            Put("A_SpawnBarn_Left", Prop + "PF_Barrel.prefab", -29.2f, -17.6f);
            Put("A_SpawnBarn_Left", Prop + "PF_Crate_02.prefab", -29.4f, -14.2f, 30f);
            Put("A_SpawnBarn_Left", Bushes[0], -33.8f, -13.8f);
            Put("A_SpawnBarn_Left", Bushes[3], -33.6f, -18f, 90f);
            Put("A_SpawnBarn_Right", FA + "Well.fbx", -31.6f, 12f, 0f, 1f, Where.Land, 0.8f);

            // ================= ZONE B: residential town - top-left houses, and the gardens left of the base.
            HouseLot("B_Residential", Bld + "PF_FarmHouse_03.prefab", -30.2f, -45f, 0f, 0.8f, 2.7f, 0, VL + "sedan 1.prefab", "Taxi");
            HouseLot("B_Residential", Bld3 + "PF3_TownHouse_02.prefab", -22.2f, -44.4f, 8f, 0.68f, 2.4f, 1);
            HouseLot("B_Residential", Bld + "PF_FarmHouse_06.prefab", -14.6f, -44.2f, -8f, 0.8f, 2.7f, 2, VL + "hatchback 1.prefab", "Red");
            Put("B_Residential", Prop + "PF_Bench.prefab", -24.8f, -40.6f, 0f);
            VegPatch("B_Residential", -17.6f, -39.6f, 0, 2);
            HouseLot("B_Residential", Bld + "PF_FarmHouse_01.prefab", 40.4f, -40.4f, 270f, 1f, 3.1f, 3, VL + "van 4.prefab", "School bus");
            HouseLot("B_Residential", Bld + "PF_FarmHouse_05.prefab", 45.2f, -29.4f, 270f, 1f, 3.1f, 4, VL + "sedan 1.prefab", "Blue");
            HouseLot("B_Residential", Bld3 + "PF3_TownHouse_03.prefab", 39.6f, -19f, 270f, 0.85f, 2.8f, 5, VL + "pickupTruck 1.prefab", "Green");
            HouseLot("B_Residential", Bld + "PF_FarmHouse_Blue.prefab", 47.6f, -41.8f, 300f, 0.8f, 3.2f, 6);
            Put("B_Residential", Prop + "PF_ClothesLine.prefab", 45.6f, -36.2f, 0f, 1f, Where.Land, 1f);
            Put("B_Residential", Prop + "PF_Bench.prefab", 33.8f, -29.6f, 270f);
            VegPatch("B_Residential", 34.2f, -43.2f, 1, 0);
            VegPatch("B_Residential", 33.6f, -22.6f, 0, 3);
            VegPatch("B_Residential", 49.6f, -22.4f, 1, 1);
            VegPatch("B_Residential", 34.4f, -15.2f, 0, 5);
            VegPatch("B_Residential", 48.6f, -15.6f, 3, 2);

            // ================= ZONE C: lighthouse shore (top centre) and the green round the centre node.
            Put("C_Lighthouse", Bld3 + "PF3_TownHouse_01.prefab", -39.4f, 0f, 90f, 0.9f, Where.Land, 2.8f); // beach house (the lighthouse stands at the fork)
            Put("C_Lighthouse", Rocks[4], -41.8f, 3.6f, 0f, 1.8f, Where.Land, 0.8f);
            Put("C_Lighthouse", Rocks[1], -41.4f, -3.9f, 100f, 1.6f, Where.Land, 0.8f);
            Put("C_Lighthouse", Rocks[2], -42.2f, 0.6f, 40f, 1.3f, Where.Land, 0.6f);
            BeachSet("C_Lighthouse", -35f, -3.6f, 90f, 0);
            BeachSet("C_Lighthouse", -35.4f, 4.6f, 90f, 1);
            Put("C_Lighthouse", AQ + "LifeguardChair.prefab", -37.4f, 6.6f, 270f, 1f, Where.Land, 0.7f);
            Put("C_Lighthouse", Palms[0], -36.2f, -6.6f, 210f, 1.05f);
            Put("C_Lighthouse", Palms[2], -38.6f, 5f, 40f);
            Put("C_Lighthouse", Palms[1], -39.8f, -6.4f, 120f, 0.9f);
            Put("C_Lighthouse", Prop + "PF_Bench.prefab", -32.6f, 0.4f, 90f);
            Put("C_Lighthouse", KW + "boat-sail-a.fbx", -52.6f, -9f, 60f, 1.3f, Where.Water);
            Put("C_Lighthouse", KW + "boat-fishing-small.fbx", -48.6f, 12f, 200f, 1.1f, Where.Water);
            Put("C_Lighthouse", KW + "ship-small.fbx", -57f, 20f, 110f, 0.9f, Where.Water);
            Put("C_Lighthouse", AQ + "Boat.prefab", -47f, -14f, 330f, 1f, Where.Water);
            Put("C_Lighthouse", KW + "buoy-flag.fbx", -49f, 5.4f, 0f, 1f, Where.Water);
            Put("C_Lighthouse", KW + "buoy.fbx", -50.5f, -21f, 0f, 1f, Where.Water);
            Put("C_Lighthouse", KW + "buoy.fbx", -50f, 24f, 0f, 1f, Where.Water);
            Put("C_Lighthouse", AQ + "LifeSaver.prefab", -46.6f, -4.5f, 0f, 1f, Where.Water);
            Put("C_Lighthouse", Bld3 + "PF3_Dock.prefab", -46.4f, 0.4f, 0f, 1f, Where.Free, 0f, 1.5f);
            VegPatch("C_Lighthouse", -26.2f, 4f, 0, 1);
            VegPatch("C_Lighthouse", -26f, -4.2f, 0, 4);
            Put("C_Lighthouse", Prop3 + "PF3_PicnicTable.prefab", -29.4f, 0f, 90f, 1f, Where.Land, 1f);
            Put("C_Lighthouse", L3 + "Vegetation/PF3_Cactus_01.prefab", -17.2f, 3.6f);
            Put("C_Lighthouse", L3 + "Vegetation/PF3_Cactus_02.prefab", -17f, -3.8f);

            // ================= ZONE D: water village - the harbour basin between the routes, and the two canals.
            Put("D_WaterVillage", Bld3 + "PF3_StiltHouse_01.prefab", -6.6f, 0f, 90f, 1f, Where.Free, 0f, 1.2f);
            Put("D_WaterVillage", Bld3 + "PF3_Dock_Long.prefab", 1.1f, 0f, 0f, 1f, Where.Free, 0f, 1.5f);
            Put("D_WaterVillage", KW + "boat-house-a.fbx", -2f, 4.6f, 80f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-row-small.fbx", -1.2f, -3.6f, 200f, 1f, Where.Water);
            Put("D_WaterVillage", AQ + "SailBoat.prefab", -7.6f, -5.2f, 40f, 1f, Where.Water);
            Put("D_WaterVillage", AQ + "LifeSaver.prefab", -0.4f, -1.9f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "buoy.fbx", -8.8f, 5.4f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "cargo-pile-a.fbx", 5.6f, 1.9f, 20f, 0.8f, Where.Land, 0.9f);
            Put("D_WaterVillage", Prop + "PF_Barrel.prefab", 5.4f, -1.6f);
            Put("D_WaterVillage", Prop + "PF_Crate.prefab", 6.3f, -2.4f, 30f);
            Put("D_WaterVillage", Palms[3], 6.4f, 3.6f, 140f, 0.9f);
            Put("D_WaterVillage", Bld3 + "PF3_TallHouse_02.prefab", -14.4f, -3.7f, 90f, 0.8f, Where.Land, 1.8f);
            Put("D_WaterVillage", Bld + "PF_Store.prefab", -14.6f, 3.9f, 90f, 0.7f, Where.Land, 2.2f);
            Put("D_WaterVillage", Bushes[1], -11.8f, -1.2f);
            Put("D_WaterVillage", Bushes[4], -11.6f, 1.4f, 90f);
            foreach (float s in new[] { -1f, 1f })
            {
                const string zone = "D_WaterVillage";
                Put(zone, Bld3 + "PF3_Dock_Long.prefab", 10f, s * 33.6f, 0f, 1f, Where.Free, 0f, 1.62f); // footbridge over the canal
                Put(zone, Bld3 + "PF3_Dock_Long.prefab", 10f, s * 43.4f, 0f, 1f, Where.Free, 0f, 1.5f);
                Put(zone, s < 0f ? KW + "boat-row-small.fbx" : KW + "boat-row-large.fbx", 10f, s * 26.4f, s < 0f ? 20f : 200f, 1f, Where.Water);
                Put(zone, s < 0f ? KW + "boat-speed-a.fbx" : KW + "boat-speed-d.fbx", 9.8f, s * 38.6f, s < 0f ? 175f : 5f, 0.9f, Where.Water);
                Put(zone, s < 0f ? AQ + "Boat.prefab" : KW + "boat-fishing-small.fbx", 10.2f, s * 48.6f, 90f, s < 0f ? 1f : 0.9f, Where.Water);
                Put(zone, KW + "buoy.fbx", 10.6f, s * 30.2f, 0f, 0.8f, Where.Water);
                Put(zone, AQ + "LifeSaver.prefab", 9.2f, s * 29f, 0f, 1f, Where.Water);
                Put(zone, AQ + "LifeSaver.prefab", 10.8f, s * 46f, 0f, 1f, Where.Water);
            }

            // ================= ZONE E: tropical market - left strip between runs 1-2, and the park right of the base.
            MarketSet("E_Market", -3.5f, -29.6f, 0f, 0);
            MarketSet("E_Market", -3.4f, -14.6f, 180f, 1);
            Put("E_Market", Palms[4], -3.8f, -32.6f, 10f, 0.9f);
            Put("E_Market", Palms[5], -5.6f, -21.6f, 190f, 0.85f);
            Put("E_Market", Prop3 + "PF3_TrashBin.prefab", -1.2f, -21f);
            Put("E_Market", Bld + "PF_Store.prefab", 43.4f, 38.4f, 270f, 0.95f, Where.Land, 3.4f);
            MarketSet("E_Market", 37.4f, 20f, 0f, 2);
            MarketSet("E_Market", 44.4f, 17.6f, 0f, 3);
            BeachSet("E_Market", 37.6f, 33.6f, 270f, 2);
            Put("E_Market", Bld + "PF_Well_Stone.prefab", 48.6f, 31.2f, 30f, 1f, Where.Land, 1.3f);
            Put("E_Market", Prop3 + "PF3_PicnicTable.prefab", 41.6f, 31f, 20f, 1f, Where.Land, 1f);
            Put("E_Market", Prop3 + "PF3_Tent_Orange.prefab", 48.4f, 43.4f, 200f, 1f, Where.Land, 1.3f);
            Put("E_Market", VL + "pickupTruck 1.prefab", 48.6f, 36.6f, 85f, 1f, Where.Land, 1.3f, float.NaN, "White");
            VegPatch("E_Market", 34.2f, 14.8f, 0, 0);
            VegPatch("E_Market", 49.6f, 22.6f, 0, 2);
            VegPatch("E_Market", 35.2f, 42.6f, 0, 4);
            VegPatch("E_Market", 41.2f, 45.2f, 3, 1);
            VegPatch("E_Market", 34.6f, 28.4f, 1, 3);
            // Camp / pool strip (right, between runs 1-2).
            Put("E_Market", Bld3 + "PF3_Pool.prefab", -3.5f, 26f, 0f, 1f, Where.Land, 2.3f);
            Put("E_Market", Prop3 + "PF3_Tent_Orange.prefab", -4.4f, 19.8f, 30f, 1f, Where.Land, 1.3f);
            Put("E_Market", Prop3 + "PF3_Tent_Blue.prefab", -2.6f, 15.8f, -20f, 1f, Where.Land, 1.3f);
            Put("E_Market", Prop3 + "PF3_PicnicTable.prefab", -3.5f, 31.2f, 0f, 1f, Where.Land, 1f);
            Put("E_Market", AQ + "Chair.prefab", -0.6f, 23.6f, 80f, 1f, Where.Land, 0.5f);
            Put("E_Market", AQ + "Chair (1).prefab", -0.6f, 28.2f, 100f, 1f, Where.Land, 0.5f);
            Put("E_Market", AQ + "Umbrella1 (1).prefab", -6.2f, 23f, 0f, 1f, Where.Land, 0.6f);
            Put("E_Market", AQ + "BeachBall2.prefab", -6.4f, 28.6f);
            Put("E_Market", Palms[0], -6.2f, 30.4f, 70f, 0.9f);
            Put("E_Market", Palms[3], -1f, 13.4f, 250f, 0.9f);
            Put("E_Market", Prop + "PF_LogPile_02.prefab", -5.8f, 17.4f, 40f);

            // ================= ZONE F: the fork - where the two routes join in front of the base. A lighthouse on the
            // green tip between them with a beach set at its foot, and a lagoon behind it (docks, boats, buoys).
            // The lighthouse takes the spot nearest the base that still clears both roads.
            float forkX = 22f;
            for (float x = 23.5f; x >= 18f; x -= 0.25f)
            {
                var candidate = new Vector2(x, 0f);
                if (ExactRoadDistance(candidate) >= RoadWidth * 0.5f + 0.3f + 2.2f && WaterMask(candidate) < 0.08f)
                {
                    forkX = x;
                    break;
                }
            }

            Put("F_ForkLighthouse", Bld3 + "PF3_Lighthouse.prefab", forkX, 0f, 90f, 0.78f, Where.Land, 2.2f);
            Put("F_ForkLighthouse", AQ + "Umbrella1 (2).prefab", forkX + 3.9f, 0.2f, 20f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", AQ + "Chair.prefab", forkX + 4.3f, -1.5f, 80f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", AQ + "Chair (1).prefab", forkX + 4.3f, 1.8f, 100f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", AQ + "SurfBoard.prefab", forkX + 2.4f, 3.1f, 60f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", AQ + "SurfBoard (1).prefab", forkX + 1.2f, 3.8f, 40f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", AQ + "BeachBall1.prefab", forkX + 5.6f, 0.9f, 0f, 1f, Where.Land, 0.2f);
            Put("F_ForkLighthouse", AQ + "Bucket.prefab", forkX + 5.4f, -0.7f, 0f, 1f, Where.Land, 0.2f);
            Put("F_ForkLighthouse", AQ + "LifeSaver.prefab", forkX + 2.6f, -2.9f, 0f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", Palms[0], forkX - 0.6f, -4.1f, 200f, 0.95f, Where.Land, 0.4f);
            Put("F_ForkLighthouse", Palms[3], forkX - 0.4f, 4.3f, 30f, 1f, Where.Land, 0.4f);
            Put("F_ForkLighthouse", Palms[1], forkX - 3.2f, -5.2f, 120f, 0.9f, Where.Land, 0.4f);
            Put("F_ForkLighthouse", Palms[4], forkX - 3.4f, 5.4f, 300f, 0.9f, Where.Land, 0.4f);
            Put("F_ForkLighthouse", Bushes[1], forkX + 1.2f, -3.4f, 0f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", Bushes[3], forkX + 3.1f, 2.4f, 60f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", Bushes[0], forkX - 2.2f, 3.4f, 90f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", Bushes[4], forkX - 2.4f, -3.2f, 10f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", Rocks[2], forkX - 2.9f, 0.9f, 40f, 1.3f, Where.Land, 0.4f);
            Put("F_ForkLighthouse", Rocks[0], forkX - 2.7f, -1.2f, 130f, 1.1f, Where.Land, 0.4f);
            Put("F_ForkLighthouse", Prop + "PF_Crate.prefab", forkX + 1.6f, -4.6f, 20f, 1f, Where.Land, 0.3f);
            Put("F_ForkLighthouse", Prop + "PF_Barrel.prefab", forkX + 0.6f, -5.2f, 0f, 1f, Where.Land, 0.3f);
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 45f + 20f;
                Put("F_ForkLighthouse", SmallPlants[(k * 2 + 1) % SmallPlants.Length],
                    forkX + 1.2f + Mathf.Cos(angle * Mathf.Deg2Rad) * 4.6f, Mathf.Sin(angle * Mathf.Deg2Rad) * 4.9f, k * 61f, 1f, Where.Land, 0.15f);
            }

            // The lagoon behind the lighthouse.
            Put("F_ForkLighthouse", Bld3 + "PF3_Dock.prefab", forkX - 5.4f, 0f, 0f, 1f, Where.Free, 0f, 1.5f);
            Put("F_ForkLighthouse", KW + "boat-speed-a.fbx", forkX - 7.2f, -2.4f, 250f, 1f, Where.Water);
            Put("F_ForkLighthouse", KW + "boat-row-small.fbx", forkX - 9.6f, 2.6f, 20f, 1f, Where.Water);
            Put("F_ForkLighthouse", KW + "boat-house-b.fbx", forkX - 12.4f, 0.4f, 95f, 0.9f, Where.Water);
            Put("F_ForkLighthouse", AQ + "Boat.prefab", forkX - 10.4f, -5f, 300f, 1f, Where.Water);
            Put("F_ForkLighthouse", KW + "buoy.fbx", forkX - 5.4f, -4.4f, 0f, 0.9f, Where.Water);
            Put("F_ForkLighthouse", KW + "buoy-flag.fbx", forkX - 7.6f, 5f, 0f, 0.9f, Where.Water);
            Put("F_ForkLighthouse", AQ + "LifeSaver.prefab", forkX - 4.8f, 0.8f, 0f, 1f, Where.Water);
            Put("F_ForkLighthouse", AQ + "LifeSaver.prefab", forkX - 8.6f, 0.6f, 0f, 1f, Where.Water);
            Put("F_ForkLighthouse", AQ + "LifeSaver.prefab", forkX - 11f, 3.8f, 0f, 1f, Where.Water);
            Report.Add("Fork: lighthouse at (" + forkX.ToString("F1") + ", 0) with a beach set, lagoon behind it.");

            // ================= ZONE G: farm - top-right barns and cow pen, the pig strip on the left, market strip on the right.
            Put("G_Farm", FA + "BigBarn.fbx", -27.4f, 50.4f, 180f, 1f, Where.Land, 3.6f);
            Put("G_Farm", FA + "Silo_House.fbx", -33.8f, 52f, 180f, 1f, Where.Land, 1.9f);
            Put("G_Farm", FA + "TowerWindmill.fbx", -12.6f, 51f, 200f, 1f, Where.Land, 2.2f);
            Put("G_Farm", FA + "ChickenCoop.fbx", -15.4f, 45.6f, 200f, 1f, Where.Land, 1.2f);
            FenceRing("G_Farm", FA + "Fence.fbx", -19.6f, 49.6f, 3.3f, 180f);
            Put("G_Farm", L2 + "Animals/PF_Cow.prefab", -20.2f, 50.2f, 40f);
            Put("G_Farm", L2 + "Animals/PF_Cow.prefab", -18.8f, 48.8f, 210f);
            Put("G_Farm", Prop + "PF_Hay_Pile.prefab", -23.4f, 45.8f, 0f, 1f, Where.Land, 1.3f);
            Put("G_Farm", Prop + "PF_Hay_Round.prefab", -31f, 46f, 30f, 1f, Where.Land, 0.7f);
            Put("G_Farm", Prop + "PF_Hay_Bale.prefab", -22.6f, 54.6f, 60f, 1f, Where.Land, 0.8f);
            Put("G_Farm", Prop + "PF_Feeder_02.prefab", -16.4f, 53.4f, 0f);
            Put("G_Farm", Prop + "PF_Wheelbarrow.prefab", -30.4f, 47.6f, 100f);
            Put("G_Farm", Prop + "PF_Scarecrow.prefab", -24.4f, 55f, 180f);
            Put("G_Farm", Prop + "PF_Crop_Wheat.prefab", -24.8f, 53.4f);
            Put("G_Farm", Prop + "PF_Crop_Cabbage.prefab", -16.8f, 44.4f);
            Put("G_Farm", NA + "NormalTree_5.fbx", -34.2f, 46.6f, 60f);
            Put("G_Farm", NA + "MapleTree_4.fbx", -14.4f, 54.6f, 0f);
            Put("G_Farm", Bushes[0], -12f, 46.2f);
            FenceRing("G_Farm", Prop + "PF_Fence_Rail.prefab", 22.5f, -27.4f, 2.7f, 90f);
            Put("G_Farm", L3 + "Animals/PF3_Pig.prefab", 21.7f, -28.5f, 40f);
            Put("G_Farm", L3 + "Animals/PF3_Pig.prefab", 23.5f, -27.6f, 200f, 0.9f);
            Put("G_Farm", L3 + "Animals/PF3_Pig.prefab", 22.2f, -26f, 120f, 1.1f);
            Put("G_Farm", Prop + "PF_Hay_Bale.prefab", 22.6f, -21.4f, 20f, 1f, Where.Land, 0.9f);
            Put("G_Farm", Prop + "PF_Hay_Round.prefab", 23.6f, -16.4f, 70f, 1f, Where.Land, 0.7f);
            Put("G_Farm", Prop + "PF_Feeder.prefab", 21.4f, -19.2f, 90f);
            Put("G_Farm", Prop + "PF_Wheelbarrow.prefab", 21.2f, -23f, 30f);
            Put("G_Farm", Prop + "PF_Bucket.prefab", 24.2f, -23.4f);
            Put("G_Farm", Prop + "PF_Tool_Fork.prefab", 24.4f, -19.6f, 15f);
            Put("G_Farm", NA + "NormalTree_5.fbx", 22.4f, -32.4f, 0f, 0.8f);
            Put("G_Farm", Bushes[2], 21.2f, -14.4f);
            MarketSet("E_Market", 22.5f, 14.6f, 0f, 1);
            Put("E_Market", Bld + "PF_MarketStall_03.prefab", 22.5f, 29.4f, 180f, 1f, Where.Land, 1.5f);
            Put("E_Market", Palms[2], 22.6f, 32.6f, 30f, 0.85f);
            Put("E_Market", L3 + "Vegetation/PF3_Cactus_02.prefab", 20.6f, 26.4f);

            // ================= Shores: left beach on the open sea, right bay.
            float[] beachPalmX = { -13.6f, -8.4f, 3.4f, 9.4f, 15.6f, 27.6f, 37.4f, 42.6f, 47.6f };
            for (int i = 0; i < beachPalmX.Length; i++)
            {
                Put("D_WaterVillage", Palms[i % Palms.Length], beachPalmX[i], -46.6f + (i % 2) * 0.5f, i * 67f, 0.92f + (i % 3) * 0.08f);
            }

            BeachSet("D_WaterVillage", 6.2f, -46.4f, 270f, 0);
            BeachSet("D_WaterVillage", 29.4f, -44.6f, 270f, 1);
            Put("D_WaterVillage", Bld3 + "PF3_Dock_Long.prefab", 33f, -53.6f, 90f, 1f, Where.Free, 0f, 1.5f);
            Put("D_WaterVillage", Bld3 + "PF3_Dock.prefab", -11f, -52.4f, 90f, 1f, Where.Free, 0f, 1.5f);
            Put("D_WaterVillage", Bld3 + "PF3_StiltHouse_02.prefab", 15f, -56.6f, 0f, 1f, Where.Free, 0f, 1.2f);
            Put("D_WaterVillage", Bld3 + "PF3_StiltHouse_01.prefab", 42f, -57f, 0f, 1f, Where.Free, 0f, 1.2f);
            Put("D_WaterVillage", KW + "boat-house-c.fbx", 23.6f, -55.6f, 100f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-sail-b.fbx", 1f, -59f, 100f, 1.3f, Where.Water);
            Put("D_WaterVillage", KW + "boat-row-large.fbx", 30.4f, -55f, 80f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-speed-a.fbx", 35.8f, -56.6f, 100f, 1f, Where.Water);
            Put("D_WaterVillage", AQ + "Boat.prefab", -13.6f, -54f, 40f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-tug-a.fbx", -26f, -58f, 70f, 1.1f, Where.Water);
            Put("D_WaterVillage", KW + "buoy-flag.fbx", 24f, -60f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "buoy.fbx", -4f, -56f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "buoy.fbx", -36f, -57.5f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", AQ + "LifeSaver.prefab", 8f, -53f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", AQ + "LifeSaver.prefab", 37f, -52.4f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "cargo-pile-b.fbx", 33f, -47.4f, 10f, 0.7f, Where.Land, 0.9f);
            float[] bayPalmX = { 2.4f, 9.2f, 16f, 27.4f, 36.6f, 41.8f, 47.4f };
            for (int i = 0; i < bayPalmX.Length; i++)
            {
                Put("D_WaterVillage", Palms[(i + 2) % Palms.Length], bayPalmX[i], 46.6f - (i % 2) * 0.4f, i * 83f + 20f, 0.92f + (i % 2) * 0.1f);
            }

            BeachSet("D_WaterVillage", 12.6f, 46.2f, 90f, 2);
            Put("D_WaterVillage", Bld3 + "PF3_StiltHouse_02.prefab", 5f, 57f, 180f, 1f, Where.Free, 0f, 1.2f);
            Put("D_WaterVillage", Bld3 + "PF3_StiltHouse_01.prefab", 19f, 57.6f, 180f, 1f, Where.Free, 0f, 1.2f);
            Put("D_WaterVillage", Bld3 + "PF3_Dock_Long.prefab", 31f, 53.6f, 90f, 1f, Where.Free, 0f, 1.5f);
            Put("D_WaterVillage", KW + "boat-house-d.fbx", 40f, 56f, 260f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-house-b.fbx", 12f, 55f, 80f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-sail-a.fbx", 46f, 61f, 200f, 1.2f, Where.Water);
            Put("D_WaterVillage", KW + "boat-row-small.fbx", 28.4f, 55.4f, 100f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "boat-speed-d.fbx", 34.6f, 57.6f, 280f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "buoy-flag.fbx", 25f, 60f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", KW + "buoy.fbx", 49f, 55f, 0f, 1f, Where.Water);
            Put("D_WaterVillage", AQ + "LifeSaver.prefab", 33.6f, 56.4f, 0f, 1f, Where.Water);

            // ================= ZONE H: base approach - framed low and to the sides, the middle stays open.
            foreach (float s in new[] { -1f, 1f })
            {
                Put("H_Base", Palms[s < 0f ? 0 : 3], 42.6f, s * 10.4f, s * 60f);
                Put("H_Base", Palms[s < 0f ? 2 : 1], 49.4f, s * 11.6f, s * 140f, 0.9f);
                Put("H_Base", Bushes[s < 0f ? 1 : 4], 39.4f, s * 12.6f);
                Put("H_Base", Bushes[s < 0f ? 3 : 0], 46.6f, s * 9.6f, 70f);
                Put("H_Base", Rocks[s < 0f ? 0 : 2], 38f, s * 9.8f, 30f);
                FenceLine("H_Base", Prop + "PF_Fence_Picket.prefab", new Vector2(44f, s * 13.4f), new Vector2(50f, s * 13.4f));
                Put("H_Base", Prop + "PF_Crate.prefab", 40.6f, s * 9.2f, 20f);
                Put("H_Base", Prop + "PF_Barrel.prefab", 41.6f, s * 8.6f);
            }

            DressBuildNodes();
            DressVerges();
            DressLawns();
            DressPickups();
            var counts = new List<string>();
            foreach (KeyValuePair<string, int> pair in Counts)
            {
                counts.Add(pair.Key + " " + pair.Value);
            }

            Report.Add("Environment: " + _placed + " authored objects in " + _zones.childCount + " groups (" + string.Join(", ", counts) + "); "
                + Refused.Count + " placements refused by the clearance rules.");
            int shown = 0;
            foreach (string refused in Refused)
            {
                if (refused.StartsWith("Verge") || refused.StartsWith("LawnPlanting") || refused.StartsWith("BuildNodeSurrounds"))
                {
                    continue; // pattern rows are expected to drop items at roads, nodes and water
                }

                if (shown++ < 40)
                {
                    Report.Add("  refused " + refused);
                }
            }
        }
    }
}
