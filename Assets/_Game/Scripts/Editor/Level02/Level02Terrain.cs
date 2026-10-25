using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_02 ground: one Unity Terrain (the UFO hover, tractor beam ring, animals and energy scatter all
    /// follow Terrain.SampleHeight, so the ground has to be a Terrain), four generated stylised layers - grass, dry
    /// grass, light road dirt, dark trampled dirt - in a single mobile pass, a gently rolling height field that is
    /// flattened under roads, plazas and buildings, and a splat map painted from the art-directed layout with
    /// noise-broken soft edges (road centre -> darker worn edge -> dirt specks -> grass).</summary>
    public static partial class Level02Builder
    {
        private const string ArtRoot = "Assets/_Game/Art/Level02";
        private const string TexturesFolder = ArtRoot + "/Textures";
        private const string MaterialsFolder = ArtRoot + "/Materials";
        private const string MeshesFolder = ArtRoot + "/Meshes";
        private const string TerrainFolder = "Assets/_Game/Data/Terrain/Level02";
        private const string TerrainLitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";

        private static readonly Vector3 TerrainOrigin = new Vector3(-80f, 0f, -64f); // was Z -48..48 before the 2026-10-11 widening
        private static readonly Vector3 TerrainSize = new Vector3(160f, 30f, 128f);
        private const int HeightmapResolution = 257;
        private const int AlphamapResolution = 512;

        // Distance fields over the terrain rectangle (0.4 m cells), built once per run.
        private const float FieldCell = 0.4f;
        private const float FieldMax = 12f;
        private static int _fieldW;
        private static int _fieldH;
        private static float[] _roadEdgeField;
        private static float[] _mainRoadField;

        // ------------------------------------------------------------------ distance fields

        private static void BuildDistanceFields()
        {
            _fieldW = Mathf.CeilToInt(TerrainSize.x / FieldCell) + 1;
            _fieldH = Mathf.CeilToInt(TerrainSize.z / FieldCell) + 1;
            _roadEdgeField = new float[_fieldW * _fieldH];
            _mainRoadField = new float[_fieldW * _fieldH];
            for (int i = 0; i < _roadEdgeField.Length; i++)
            {
                _roadEdgeField[i] = FieldMax;
                _mainRoadField[i] = FieldMax;
            }

            StampPolyline(_mainRoadSamples, MainRoadWidth * 0.5f, true);
            foreach (List<Vector2> road in _sideRoadSamples)
            {
                StampPolyline(road, SideRoadWidth * 0.5f, false);
            }
        }

        private static void StampPolyline(List<Vector2> line, float halfWidth, bool main)
        {
            for (int s = 0; s < line.Count - 1; s++)
            {
                Vector2 a = line[s];
                Vector2 b = line[s + 1];
                float reach = FieldMax + halfWidth;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - TerrainOrigin.x) / FieldCell));
                int x1 = Mathf.Min(_fieldW - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - TerrainOrigin.x) / FieldCell));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach - TerrainOrigin.z) / FieldCell));
                int z1 = Mathf.Min(_fieldH - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach - TerrainOrigin.z) / FieldCell));
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(TerrainOrigin.x + x * FieldCell, TerrainOrigin.z + z * FieldCell);
                        float d = DistanceToSegment(p, a, b);
                        int i = z * _fieldW + x;
                        float edge = d - halfWidth;
                        if (edge < _roadEdgeField[i])
                        {
                            _roadEdgeField[i] = edge;
                        }

                        if (main && d < _mainRoadField[i])
                        {
                            _mainRoadField[i] = d;
                        }
                    }
                }
            }
        }

        private static float SampleField(float[] field, Vector2 p)
        {
            float fx = (p.x - TerrainOrigin.x) / FieldCell;
            float fz = (p.y - TerrainOrigin.z) / FieldCell;
            if (fx < 0f || fz < 0f || fx >= _fieldW - 1 || fz >= _fieldH - 1)
            {
                return FieldMax;
            }

            int x = (int)fx;
            int z = (int)fz;
            float tx = fx - x;
            float tz = fz - z;
            int i = z * _fieldW + x;
            float a = Mathf.Lerp(field[i], field[i + 1], tx);
            float b = Mathf.Lerp(field[i + _fieldW], field[i + _fieldW + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        /// <summary>Distance to the nearest road edge in metres (negative on the road).</summary>
        private static float RoadEdge(Vector2 p) => SampleField(_roadEdgeField, p);

        /// <summary>Distance to the main road (= enemy path) centre line.</summary>
        private static float MainRoadDistance(Vector2 p) => SampleField(_mainRoadField, p);

        // ------------------------------------------------------------------ yards under buildings

        private struct Yard
        {
            public Vector2 Center;
            public Vector2 Radii;
            public float Yaw;
            public GroundKind Kind;
        }

        private static List<Yard> _yards;

        private static void BuildYards()
        {
            _yards = new List<Yard>();
            foreach (BuildingSpot b in Buildings)
            {
                Vector2 r;
                GroundKind kind = GroundKind.LightDirt;
                switch (b.Kind)
                {
                    case Landmark.House: r = new Vector2(4.4f, 4f); break;
                    case Landmark.FarmHouseBlue: r = new Vector2(5.2f, 4.8f); break;
                    case Landmark.StoreYellow: r = new Vector2(6.2f, 4f); break;
                    case Landmark.Church: r = new Vector2(4.8f, 7.2f); break;
                    case Landmark.RedBarn: r = new Vector2(7.5f, 6.5f); kind = GroundKind.DarkDirt; break;
                    case Landmark.BarnWhite: r = new Vector2(5.8f, 6.8f); kind = GroundKind.DarkDirt; break;
                    case Landmark.EnemyBarn: r = new Vector2(7.5f, 8.5f); kind = GroundKind.DarkDirt; break;
                    case Landmark.SiloRed: r = new Vector2(2.4f, 2.4f); kind = GroundKind.DarkDirt; break;
                    case Landmark.ChickenCoop: r = new Vector2(3f, 2.6f); kind = GroundKind.DarkDirt; break;
                    case Landmark.WindPump: r = new Vector2(3f, 3f); kind = GroundKind.DarkDirt; break;
                    case Landmark.OldShed: r = new Vector2(2.4f, 2.2f); kind = GroundKind.DarkDirt; break;
                    case Landmark.Well:
                    case Landmark.WellStone: r = new Vector2(1.9f, 1.7f); break;
                    default: continue; // market stalls, windmill and water tower sit in their own clearings
                }

                _yards.Add(new Yard { Center = b.Position, Radii = r * b.Scale, Yaw = b.Yaw, Kind = kind });
            }
        }

        private static float YardDistance(Yard y, Vector2 p)
        {
            Vector2 local = Rotate(p - y.Center, y.Yaw);
            return new Vector2(local.x / y.Radii.x, local.y / y.Radii.y).magnitude;
        }

        private static float PastureCoverage(Vector2 p, float jitter)
        {
            float dx = Mathf.Max(CowPasture.xMin - p.x, p.x - CowPasture.xMax);
            float dz = Mathf.Max(CowPasture.yMin - p.y, p.y - CowPasture.yMax);
            float outside = Mathf.Max(dx, dz);
            return 1f - SmoothStep(-1.2f, 0.4f, outside + jitter);
        }

        // ------------------------------------------------------------------ heights

        private static float GroundHeightAt(Vector2 p)
        {
            float rolling = (Fbm(p.x * 0.055f, p.y * 0.055f, 11, 3) - 0.5f) * 0.55f
                + (ValueNoise(p.x * 0.2f, p.y * 0.2f, 12) - 0.5f) * 0.12f;

            float flat = 1f - SmoothStep(0.4f, 3.2f, RoadEdge(p));
            foreach (Clearing c in Clearings)
            {
                if (c.Movement || c.Kind != GroundKind.DryGrass)
                {
                    flat = Mathf.Max(flat, 1f - SmoothStep(0.9f, 1.35f, ClearingDistance(c, p)));
                }
            }

            foreach (Yard y in _yards)
            {
                flat = Mathf.Max(flat, 1f - SmoothStep(0.8f, 1.3f, YardDistance(y, p)));
            }

            flat = Mathf.Max(flat, PastureCoverage(p, 0f) * 0.7f);
            float worn = 1f - SmoothStep(-0.6f, 0.4f, RoadEdge(p));
            return GroundY + rolling * (1f - flat * 0.88f) - worn * 0.05f;
        }

        // ------------------------------------------------------------------ build

        private static Terrain BuildTerrain(Transform parent)
        {
            EnsureFolder(TerrainFolder);
            TerrainLayer[] layers = EnsureTerrainLayers();

            string dataPath = TerrainFolder + "/TD_Level02.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, dataPath);
            }

            data.heightmapResolution = HeightmapResolution;
            data.alphamapResolution = AlphamapResolution;
            data.baseMapResolution = 512;
            data.SetDetailResolution(32, 16);
            data.size = TerrainSize;
            data.terrainLayers = layers;

            var heights = new float[HeightmapResolution, HeightmapResolution];
            for (int z = 0; z < HeightmapResolution; z++)
            {
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    var p = new Vector2(TerrainOrigin.x + x / (float)(HeightmapResolution - 1) * TerrainSize.x,
                        TerrainOrigin.z + z / (float)(HeightmapResolution - 1) * TerrainSize.z);
                    heights[z, x] = GroundHeightAt(p) / TerrainSize.y;
                }
            }

            data.SetHeights(0, 0, heights);
            data.SetAlphamaps(0, 0, PaintSplat());
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_Level02";
            go.transform.SetParent(parent, false);
            go.transform.position = TerrainOrigin;
            var terrain = go.GetComponent<Terrain>();
            var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainLitMaterialPath);
            if (terrainMaterial != null)
            {
                terrain.materialTemplate = terrainMaterial;
            }

            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 1000f;
            terrain.drawInstanced = true;
            terrain.drawTreesAndFoliage = false;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return terrain;
        }

        private static float[,,] PaintSplat()
        {
            int n = AlphamapResolution;
            var alphas = new float[n, n, 4];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(TerrainOrigin.x + (x + 0.5f) / n * TerrainSize.x, TerrainOrigin.z + (z + 0.5f) / n * TerrainSize.z);
                    Vector4 w = SplatWeights(p);
                    alphas[z, x, 0] = w.x;
                    alphas[z, x, 1] = w.y;
                    alphas[z, x, 2] = w.z;
                    alphas[z, x, 3] = w.w;
                }
            }

            return alphas;
        }

        /// <summary>(grass, dry grass, light dirt, dark dirt) for a world point.</summary>
        private static Vector4 SplatWeights(Vector2 p)
        {
            float jitterLow = (ValueNoise(p.x * 0.33f, p.y * 0.33f, 21) - 0.5f) * 1.1f;
            float jitterHigh = (ValueNoise(p.x * 1.4f, p.y * 1.4f, 22) - 0.5f) * 0.45f;
            float edge = RoadEdge(p) + jitterLow + jitterHigh;

            // Roads: light dirt in the middle, a darker worn band toward the edge, soft noisy border.
            float road = 1f - SmoothStep(-0.35f, 0.45f, edge);
            float wornBand = SmoothStep(-1.2f, -0.2f, edge) * road * (0.16f + 0.22f * ValueNoise(p.x * 0.8f, p.y * 0.8f, 23));

            // Dirt specks scattered just outside the road edge.
            float speck = 0f;
            if (edge > 0.2f && edge < 2.4f)
            {
                float s = ValueNoise(p.x * 2.6f, p.y * 2.6f, 24);
                speck = SmoothStep(0.74f, 0.86f, s) * (1f - SmoothStep(0.6f, 2.4f, edge));
            }

            float light = Mathf.Max(road - wornBand, speck * 0.7f);
            float dark = wornBand + speck * 0.3f;
            float dry = 0f;

            float clearingJitter = (ValueNoise(p.x * 0.45f, p.y * 0.45f, 25) - 0.5f) * 0.32f
                + (ValueNoise(p.x * 1.7f, p.y * 1.7f, 26) - 0.5f) * 0.12f;
            foreach (Clearing c in Clearings)
            {
                float cover = 1f - SmoothStep(0.82f, 1.05f, ClearingDistance(c, p) + clearingJitter);
                if (cover <= 0f)
                {
                    continue;
                }

                switch (c.Kind)
                {
                    case GroundKind.LightDirt:
                        light = Mathf.Max(light, cover * (0.88f + 0.12f * ValueNoise(p.x * 0.9f, p.y * 0.9f, 27)));
                        dark = Mathf.Max(dark, cover * 0.18f * SmoothStep(0.55f, 1f, ClearingDistance(c, p)));
                        break;
                    case GroundKind.DarkDirt:
                        dark = Mathf.Max(dark, cover * (0.7f + 0.3f * ValueNoise(p.x * 0.7f, p.y * 0.7f, 28)));
                        light = Mathf.Max(light, cover * 0.25f);
                        break;
                    default:
                        dry = Mathf.Max(dry, cover * 0.85f);
                        break;
                }
            }

            foreach (Yard y in _yards)
            {
                float cover = 1f - SmoothStep(0.8f, 1.05f, YardDistance(y, p) + clearingJitter);
                if (cover <= 0f)
                {
                    continue;
                }

                if (y.Kind == GroundKind.DarkDirt)
                {
                    dark = Mathf.Max(dark, cover * 0.75f);
                    light = Mathf.Max(light, cover * 0.2f);
                }
                else
                {
                    light = Mathf.Max(light, cover * 0.7f);
                    dark = Mathf.Max(dark, cover * 0.15f);
                }
            }

            // Cow pasture: trampled, patchy - dark dirt broken by surviving grass.
            float pasture = PastureCoverage(p, clearingJitter * 2f);
            if (pasture > 0f)
            {
                float trampled = SmoothStep(0.35f, 0.65f, Fbm(p.x * 0.25f, p.y * 0.25f, 29, 2));
                dark = Mathf.Max(dark, pasture * (0.35f + 0.5f * trampled));
                dry = Mathf.Max(dry, pasture * 0.3f);
            }

            // Grass variation: large soft dry/light patches and small spots.
            float large = Fbm(p.x * 0.04f, p.y * 0.04f, 30, 3);
            float spots = ValueNoise(p.x * 0.3f, p.y * 0.3f, 31);
            dry = Mathf.Max(dry, SmoothStep(0.56f, 0.7f, large) * 0.75f + SmoothStep(0.8f, 0.9f, spots) * 0.45f);

            light = Mathf.Clamp01(light);
            dark = Mathf.Clamp01(dark) * (1f - light * 0.5f);
            float dirt = Mathf.Min(1f, light + dark);
            if (light + dark > 1f)
            {
                float k = 1f / (light + dark);
                light *= k;
                dark *= k;
            }

            dry = Mathf.Clamp01(dry) * (1f - dirt);
            float grass = Mathf.Max(0f, 1f - dirt - dry);
            return new Vector4(grass, dry, light, dark);
        }

        // ------------------------------------------------------------------ layers and textures

        private static TerrainLayer[] EnsureTerrainLayers()
        {
            EnsureFolder(TerrainFolder);
            return new[]
            {
                EnsureLayer("TL_Level02_Grass", EnsureGroundTexture("T_Level02_Grass", 0), 9f),
                EnsureLayer("TL_Level02_GrassDry", EnsureGroundTexture("T_Level02_GrassDry", 1), 9f),
                EnsureLayer("TL_Level02_Dirt", EnsureGroundTexture("T_Level02_Dirt", 2), 7f),
                EnsureLayer("TL_Level02_DirtDark", EnsureGroundTexture("T_Level02_DirtDark", 3), 7f),
            };
        }

        private static TerrainLayer EnsureLayer(string name, Texture2D texture, float tile)
        {
            string path = TerrainFolder + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;
            layer.smoothness = 0f;
            layer.metallic = 0f;
            layer.specular = Color.black;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>Tileable 512 px stylised ground: 0 grass, 1 dry grass, 2 road dirt, 3 dark dirt. Painted-looking:
        /// soft colour blotches, short blade strokes / specks, and small pebbles on the dirt.</summary>
        private static Texture2D EnsureGroundTexture(string name, int kind)
        {
            EnsureFolder(TexturesFolder);
            string path = TexturesFolder + "/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && !_regenerateTextures)
            {
                return existing;
            }

            const int size = 512;
            Color baseColor, darkColor, lightColor;
            switch (kind)
            {
                case 0: baseColor = Hex("#93C052"); darkColor = Hex("#84B449"); lightColor = Hex("#A2C95C"); break;
                case 1: baseColor = Hex("#AFC35C"); darkColor = Hex("#A2B853"); lightColor = Hex("#BCCB69"); break;
                case 2: baseColor = Hex("#D3B677"); darkColor = Hex("#C9AA6D"); lightColor = Hex("#DBC287"); break;
                default: baseColor = Hex("#AE8F62"); darkColor = Hex("#A38559"); lightColor = Hex("#B99B6C"); break;
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float blotch = Fbm(u * 4f, v * 4f, 40 + kind, 3, 4);
                    float mid = ValueNoise(u * 16f, v * 16f, 50 + kind, 16);
                    float t = Mathf.Clamp01((blotch - 0.5f) * 1.7f + (mid - 0.5f) * 0.5f + 0.5f);
                    Color c = t < 0.5f ? Color.Lerp(darkColor, baseColor, t * 2f) : Color.Lerp(baseColor, lightColor, (t - 0.5f) * 2f);
                    pixels[y * size + x] = c;
                }
            }

            var random = new System.Random(900 + kind);
            if (kind <= 1)
            {
                // Short grass blade strokes, dark and light, leaning a little.
                int strokes = kind == 0 ? 5200 : 4200;
                for (int i = 0; i < strokes; i++)
                {
                    int x = random.Next(size);
                    int y = random.Next(size);
                    int length = 3 + random.Next(5);
                    int lean = random.Next(3) - 1;
                    bool dark = random.NextDouble() < 0.6;
                    float amount = dark ? 0.86f : 1.1f;
                    for (int k = 0; k < length; k++)
                    {
                        int px = (x + (k * lean) / 3 + size) % size;
                        int py = (y + k) % size;
                        Color c = pixels[py * size + px];
                        pixels[py * size + px] = Shade(c, Mathf.Lerp(amount, 1f, k / (float)length));
                    }
                }

                // Rare tiny pale specks (seed heads).
                for (int i = 0; i < 160; i++)
                {
                    int x = random.Next(size);
                    int y = random.Next(size);
                    pixels[y * size + x] = Color.Lerp(pixels[y * size + x], Hex("#E8E6A0"), 0.7f);
                }
            }
            else
            {
                // Fine darker grit.
                for (int i = 0; i < 9000; i++)
                {
                    int x = random.Next(size);
                    int y = random.Next(size);
                    pixels[y * size + x] = Shade(pixels[y * size + x], 0.84f + (float)random.NextDouble() * 0.06f);
                }

                // Small pebbles: light top, darker lower rim.
                int pebbles = kind == 2 ? 16 : 12;
                Color pebble = kind == 2 ? Hex("#CDB47C") : Hex("#B69A6E");
                for (int i = 0; i < pebbles; i++)
                {
                    int cx = random.Next(size);
                    int cy = random.Next(size);
                    float rx = 2.5f + (float)random.NextDouble() * 3f;
                    float ry = rx * (0.6f + (float)random.NextDouble() * 0.3f);
                    for (int dy = -7; dy <= 7; dy++)
                    {
                        for (int dx = -7; dx <= 7; dx++)
                        {
                            float e = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
                            int px = (cx + dx + size) % size;
                            int py = (cy + dy + size) % size;
                            if (e <= 1f)
                            {
                                pixels[py * size + px] = dy < -ry * 0.35f ? Shade(pebble, 0.82f) : pebble;
                            }
                            else if (e <= 1.6f && dy < 0)
                            {
                                pixels[py * size + px] = Shade(pixels[py * size + px], 0.88f); // soft contact shadow
                            }
                        }
                    }
                }

                if (kind == 3)
                {
                    // A few surviving grass bits in trampled ground.
                    for (int i = 0; i < 900; i++)
                    {
                        int x = random.Next(size);
                        int y = random.Next(size);
                        for (int k = 0; k < 3; k++)
                        {
                            int py = (y + k) % size;
                            pixels[py * size + x] = Color.Lerp(pixels[py * size + x], Hex("#7FA048"), 0.65f);
                        }
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 2;
            importer.maxTextureSize = 512;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static bool _regenerateTextures;

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
