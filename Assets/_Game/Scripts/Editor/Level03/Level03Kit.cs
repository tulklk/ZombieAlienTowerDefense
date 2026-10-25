using AlienDefense.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>The generated Level_03 kit - the coastal-town pieces the project has no art for: palms, cacti, a
    /// lighthouse, beach and market umbrellas, sun loungers, docks, wooden stilt houses, two-storey town houses, a
    /// narrow tall house, buoys, lifebuoys, a bus, pigs, a pool, tents, a sailboat, picnic tables, bins, market
    /// tables and surfboards. Same recipe as the Level_02 kit (and deliberately part of the same partial class, so it
    /// reuses KitMesh, the vertex-colour material and the roof helper instead of copying them): flat-shaded low-poly,
    /// one colour per face, one shared material, local origin on the ground, front toward +Z, no colliders.
    /// Prefabs go to Assets/_Game/Prefabs/Level03, meshes to Assets/_Game/Art/Level03/Meshes.</summary>
    public static partial class Level02Builder
    {
        public const string Level03PrefabRoot = "Assets/_Game/Prefabs/Level03";
        private const string Level03MeshFolder = "Assets/_Game/Art/Level03/Meshes";

        private static readonly Color Wood = Hex("#B9824F");
        private static readonly Color WoodDark = Hex("#8C5E38");
        private static readonly Color WoodLight = Hex("#D3A26B");
        private static readonly Color Thatch = Hex("#E2B84A");
        private static readonly Color ThatchAlt = Hex("#CFA23B");
        private static readonly Color PalmGreen = Hex("#58B947");
        private static readonly Color PalmGreenAlt = Hex("#3F9E3C");
        private static readonly Color PalmLime = Hex("#9AD04A");

        [MenuItem("Tools/Tower Defense/Level 03/Build Level 03 Kit")]
        public static void BuildLevel03KitFromMenu()
        {
            BuildLevel03Kit();
            AssetDatabase.SaveAssets();
        }

        public static void BuildLevel03Kit()
        {
            _vertexLit = EnsureVertexLitMaterial();

            for (int v = 0; v < 3; v++)
            {
                K3("Vegetation", "PF3_Palm_0" + (v + 1), DesignPalm(v), true);
            }

            K3("Vegetation", "PF3_Cactus_01", DesignCactus(0), true);
            K3("Vegetation", "PF3_Cactus_02", DesignCactus(1), true);

            K3("Buildings", "PF3_Lighthouse", DesignLighthouse(), false);
            for (int v = 0; v < 3; v++)
            {
                K3("Buildings", "PF3_TownHouse_0" + (v + 1), DesignTownHouse(v), false);
            }

            K3("Buildings", "PF3_TallHouse_01", DesignTallHouse(0), false);
            K3("Buildings", "PF3_TallHouse_02", DesignTallHouse(1), false);
            K3("Buildings", "PF3_StiltHouse_01", DesignStiltHouse(0), false);
            K3("Buildings", "PF3_StiltHouse_02", DesignStiltHouse(1), false);
            K3("Buildings", "PF3_Dock", DesignDock(6f), false);
            K3("Buildings", "PF3_Dock_Long", DesignDock(10f), false);
            K3("Buildings", "PF3_Pool", DesignPool(), false);

            K3("Props", "PF3_Umbrella_Beach", DesignUmbrella(Hex("#2F6FC4"), Hex("#F4F6F8"), false), true);
            K3("Props", "PF3_Umbrella_Market", DesignUmbrella(Hex("#F39A2B"), Hex("#FFF3D6"), true), true);
            K3("Props", "PF3_Umbrella_Red", DesignUmbrella(Hex("#E0483B"), Hex("#FFF3D6"), true), true);
            K3("Props", "PF3_Lounger", DesignLounger(), true);
            K3("Props", "PF3_Buoy", DesignBuoy(), true);
            K3("Props", "PF3_Lifebuoy", DesignLifebuoy(), true);
            K3("Props", "PF3_Tent_Orange", DesignTent(Hex("#F28C28"), Hex("#D8741C")), true);
            K3("Props", "PF3_Tent_Blue", DesignTent(Hex("#3B7FD0"), Hex("#2C66AD")), true);
            K3("Props", "PF3_PicnicTable", DesignPicnicTable(), true);
            K3("Props", "PF3_TrashBin", DesignTrashBin(), true);
            K3("Props", "PF3_MarketTable_01", DesignMarketTable(0), true);
            K3("Props", "PF3_MarketTable_02", DesignMarketTable(1), true);
            K3("Props", "PF3_Surfboard_Yellow", DesignSurfboard(Hex("#F6D34A"), Hex("#F28C28")), true);
            K3("Props", "PF3_Surfboard_Blue", DesignSurfboard(Hex("#4AB6E8"), Hex("#F4F6F8")), true);
            K3("Animals", "PF3_Pig", DesignPig(), true);
            K3("Vehicles", "PF3_Bus", DesignBus(), true);
            K3("Vehicles", "PF3_Sailboat", DesignSailboat(), true);
            K3("Vehicles", "PF3_RowBoat_Wood", DesignRowBoat(Hex("#B9824F"), Hex("#8C5E38")), true);
            K3("Vehicles", "PF3_RowBoat_Blue", DesignRowBoat(Hex("#3F7FC0"), Hex("#F4F6F8")), true);
            K3("Vehicles", "PF3_RowBoat_Red", DesignRowBoat(Hex("#D9483B"), Hex("#F6D34A")), true);

            Report.Add("Level 3 kit: prefabs under " + Level03PrefabRoot + " (generated, vertex-colour material shared with the Level 2 kit).");
        }

        private static void K3(string folder, string name, Mesh mesh, bool absorbable)
        {
            EnsureFolder(Level03PrefabRoot + "/" + folder);
            mesh.name = name.Replace("PF3_", "M3_");
            Mesh saved = SaveMesh(mesh, Level03MeshFolder + "/" + mesh.name + ".asset");
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _vertexLit;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (absorbable)
            {
                // Loose decoration can be pulled in by the tractor beam (its size gate decides when); structures cannot.
                AddAbsorbable(root, root.transform, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, Level03PrefabRoot + "/" + folder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ vegetation

        private static Mesh DesignPalm(int variant)
        {
            var m = new KitMesh();
            Color bark = Hex("#A9794A");
            Color barkAlt = Hex("#8F6239");
            float lean = 7f + variant * 4f;
            int segments = 5 + variant % 2;
            Vector3 p = Vector3.zero;
            Quaternion r = Quaternion.identity;
            for (int i = 0; i < segments; i++)
            {
                r = r * Quaternion.Euler(lean * (i < 3 ? 1f : -0.6f), 0f, 0f);
                float r0 = 0.30f - i * 0.025f;
                m.Push(p, r);
                int band = i;
                m.Cylinder(Vector3.zero, r0, r0 - 0.03f, 1.02f, 6, s => (s + band) % 2 == 0 ? bark : barkAlt, null);
                m.Cylinder(new Vector3(0f, 0.95f, 0f), r0 + 0.03f, r0 - 0.02f, 0.12f, 6, _ => barkAlt, null);
                m.Pop();
                p += r * Vector3.up * 1.0f;
            }

            int fronds = 7 + variant % 2;
            for (int k = 0; k < fronds; k++)
            {
                float yaw = k * 360f / fronds + variant * 17f;
                float droop = k % 2 == 0 ? 0f : 0.22f;
                m.Push(p, Quaternion.Euler(0f, yaw, 0f));
                Vector3[] spine =
                {
                    new Vector3(0f, 0.05f, 0.05f), new Vector3(0f, 0.55f - droop, 1.0f),
                    new Vector3(0f, 0.38f - droop * 1.6f, 2.0f), new Vector3(0f, -0.45f - droop * 2f, 2.95f),
                };
                float[] width = { 0.34f, 0.66f, 0.5f, 0.06f };
                for (int s = 0; s < 3; s++)
                {
                    Color c = (k + s) % 3 == 0 ? PalmLime : (k + s) % 2 == 0 ? PalmGreen : PalmGreenAlt;
                    Vector3 a = spine[s] + Vector3.left * width[s];
                    Vector3 b = spine[s] + Vector3.right * width[s];
                    Vector3 c1 = spine[s + 1] + Vector3.right * width[s + 1];
                    Vector3 d = spine[s + 1] + Vector3.left * width[s + 1];
                    m.Quad(a, b, c1, d, c, Vector3.up);
                    m.Quad(a, b, c1, d, Shade(c, 0.72f), Vector3.down); // seen from below on tall palms near the camera
                }

                m.Pop();
            }

            for (int k = 0; k < 3; k++)
            {
                float a = k * 2.1f;
                m.Push(p + new Vector3(Mathf.Cos(a) * 0.26f, -0.18f, Mathf.Sin(a) * 0.26f), Quaternion.identity);
                m.Cylinder(Vector3.zero, 0.17f, 0.11f, 0.26f, 5, _ => Hex("#6B4A2B"), Hex("#7A5733"), Hex("#5A3D22"));
                m.Pop();
            }

            return m.ToMesh("palm" + variant);
        }

        private static Mesh DesignCactus(int variant)
        {
            var m = new KitMesh();
            Color green = Hex("#4FA65A");
            Color greenAlt = Hex("#3F8E4C");
            float h = variant == 0 ? 1.7f : 1.25f;
            m.Cylinder(Vector3.zero, 0.3f, 0.27f, h, 6, s => s % 2 == 0 ? green : greenAlt, null);
            m.Cylinder(new Vector3(0f, h, 0f), 0.27f, 0.1f, 0.24f, 6, s => s % 2 == 0 ? green : greenAlt, green);
            foreach (float side in variant == 0 ? new[] { -1f, 1f } : new[] { 1f })
            {
                float y = side > 0f ? h * 0.42f : h * 0.6f;
                m.Beam(new Vector3(0f, y, 0f), new Vector3(side * 0.62f, y, 0f), 0.26f, greenAlt);
                m.Cylinder(new Vector3(side * 0.62f, y - 0.13f, 0f), 0.17f, 0.13f, 0.72f, 5, s => s % 2 == 0 ? green : greenAlt, green);
            }

            m.Box(new Vector3(0f, h + 0.3f, 0f), new Vector3(0.24f, 0.14f, 0.24f), Quaternion.Euler(0f, 45f, 0f), Hex("#F06CA0"), Hex("#FFC2DA"));
            return m.ToMesh("cactus" + variant);
        }

        // ------------------------------------------------------------------ buildings

        private static Mesh DesignLighthouse()
        {
            var m = new KitMesh();
            Color red = Hex("#E2573B");
            Color white = Hex("#F6F3EA");
            m.Cylinder(Vector3.zero, 2.6f, 2.4f, 1.2f, 10, s => s % 2 == 0 ? Stone : StoneLight, StoneLight);
            float y = 1.2f;
            float radius = 1.75f;
            for (int band = 0; band < 5; band++)
            {
                float next = radius - 0.13f;
                Color c = band % 2 == 0 ? red : white;
                m.Cylinder(new Vector3(0f, y, 0f), radius, next, 1.8f, 10, s => s % 2 == 0 ? c : Shade(c, 0.93f), null);
                radius = next;
                y += 1.8f;
            }

            m.Cylinder(new Vector3(0f, y, 0f), 1.75f, 1.75f, 0.24f, 10, _ => Hex("#4B4F5C"), Hex("#5C6170"), Hex("#3F434E"));
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI * 2f / 10f;
                m.Beam(new Vector3(Mathf.Cos(a) * 1.68f, y + 0.24f, Mathf.Sin(a) * 1.68f), new Vector3(Mathf.Cos(a) * 1.68f, y + 0.85f, Mathf.Sin(a) * 1.68f), 0.07f, Hex("#3F434E"));
            }

            m.Cylinder(new Vector3(0f, y + 0.24f, 0f), 0.85f, 0.85f, 1.35f, 8, s => s % 2 == 0 ? Hex("#FFE9A6") : Hex("#9FD4F0"), null);
            m.Cylinder(new Vector3(0f, y + 1.59f, 0f), 1.15f, 0f, 1.15f, 8, s => s % 2 == 0 ? red : Shade(red, 0.9f), null);
            m.Box(new Vector3(0f, 1.2f + 1.0f, 1.72f), new Vector3(0.9f, 1.9f, 0.16f), Hex("#6B4A2B"));
            m.Box(new Vector3(0f, 5.4f, 1.48f), new Vector3(0.55f, 0.75f, 0.14f), Hex("#3F6FA8"));
            m.Box(new Vector3(1.4f, 7.2f, 0f), new Vector3(0.14f, 0.75f, 0.55f), Hex("#3F6FA8"));
            return m.ToMesh("lighthouse");
        }

        private static Mesh DesignTownHouse(int variant)
        {
            Color wall, wallUpper, roof, roofAlt, trim, shutter;
            switch (variant)
            {
                case 0: wall = Hex("#F2A23A"); wallUpper = Hex("#F7C86B"); roof = Hex("#3F7FC0"); roofAlt = Hex("#356DA8"); trim = Hex("#FFF6E3"); shutter = Hex("#2E8B8B"); break;
                case 1: wall = Hex("#F3EDE0"); wallUpper = Hex("#F6C04A"); roof = Hex("#D0623A"); roofAlt = Hex("#B9542F"); trim = Hex("#8A5B36"); shutter = Hex("#3F6FA8"); break;
                default: wall = Hex("#59B8B0"); wallUpper = Hex("#8FD6CE"); roof = Hex("#E07A3C"); roofAlt = Hex("#C96A31"); trim = Hex("#FFF6E3"); shutter = Hex("#E2573B"); break;
            }

            var m = new KitMesh();
            const float w = 5.6f, d = 4.8f, h1 = 2.7f, h2 = 2.5f, found = 0.35f;
            m.Box(new Vector3(0f, found * 0.5f, 0f), new Vector3(w + 0.9f, found, d + 0.9f), Stone, StoneLight);
            m.Box(new Vector3(0f, found + h1 * 0.5f, 0f), new Vector3(w, h1, d), wall);
            m.Box(new Vector3(0f, found + h1 + 0.09f, 0f), new Vector3(w + 0.2f, 0.18f, d + 0.2f), trim);
            m.Box(new Vector3(0f, found + h1 + 0.18f + h2 * 0.5f, 0f), new Vector3(w, h2, d), wallUpper);
            float top = found + h1 + 0.18f + h2;
            GableRoof(m, w, d, top, 0.42f, roof, roofAlt, wallUpper, false);

            // Door, windows with shutters on the front, plain windows on the sides.
            m.Box(new Vector3(-1.5f, found + 1.0f, d * 0.5f + 0.05f), new Vector3(1.0f, 2.0f, 0.12f), Hex("#7A4A2A"));
            foreach (float y in new[] { found + 1.45f, found + h1 + 0.18f + 1.25f })
            {
                foreach (float x in y < 2.5f ? new[] { 0.5f, 2.0f } : new[] { -1.8f, 0.1f, 1.9f })
                {
                    m.Box(new Vector3(x, y, d * 0.5f + 0.05f), new Vector3(0.8f, 1.0f, 0.1f), Glass);
                    m.Box(new Vector3(x - 0.52f, y, d * 0.5f + 0.07f), new Vector3(0.2f, 1.0f, 0.08f), shutter);
                    m.Box(new Vector3(x + 0.52f, y, d * 0.5f + 0.07f), new Vector3(0.2f, 1.0f, 0.08f), shutter);
                }

                foreach (float side in new[] { -1f, 1f })
                {
                    m.Box(new Vector3(side * (w * 0.5f + 0.05f), y, -0.6f), new Vector3(0.1f, 1.0f, 0.8f), Glass);
                    m.Box(new Vector3(side * (w * 0.5f + 0.05f), y, 1.0f), new Vector3(0.1f, 1.0f, 0.8f), Glass);
                }
            }

            // Balcony with railing and a striped awning over the door.
            float by = found + h1 + 0.18f;
            m.Box(new Vector3(0.6f, by + 0.06f, d * 0.5f + 0.55f), new Vector3(3.4f, 0.12f, 1.1f), trim);
            for (int k = 0; k <= 6; k++)
            {
                float x = 0.6f - 1.65f + k * 0.55f;
                m.Beam(new Vector3(x, by + 0.12f, d * 0.5f + 1.05f), new Vector3(x, by + 0.8f, d * 0.5f + 1.05f), 0.07f, WoodDark);
            }

            m.Beam(new Vector3(-1.05f, by + 0.8f, d * 0.5f + 1.05f), new Vector3(2.25f, by + 0.8f, d * 0.5f + 1.05f), 0.09f, WoodDark);
            for (int k = 0; k < 4; k++)
            {
                m.Box(new Vector3(-2.1f + k * 0.4f, found + 2.25f, d * 0.5f + 0.5f), new Vector3(0.4f, 0.08f, 1.0f), Quaternion.Euler(18f, 0f, 0f),
                    k % 2 == 0 ? shutter : trim);
            }

            return m.ToMesh("townhouse" + variant);
        }

        private static Mesh DesignTallHouse(int variant)
        {
            Color wall = variant == 0 ? Hex("#D9603F") : Hex("#F0C24B");
            Color roof = variant == 0 ? Hex("#5878B8") : Hex("#C8563A");
            Color trim = Hex("#F6EFE0");
            var m = new KitMesh();
            const float w = 3.4f, d = 3.4f, h = 7.2f, found = 0.4f;
            m.Box(new Vector3(0f, found * 0.5f, 0f), new Vector3(w + 0.6f, found, d + 0.6f), Stone, StoneLight);
            m.Box(new Vector3(0f, found + h * 0.5f, 0f), new Vector3(w, h, d), wall);
            foreach (float y in new[] { found + 2.5f, found + 4.9f })
            {
                m.Box(new Vector3(0f, y, 0f), new Vector3(w + 0.16f, 0.16f, d + 0.16f), trim);
            }

            GableRoof(m, w, d, found + h, 0.7f, roof, Shade(roof, 0.88f), wall, false);
            m.Box(new Vector3(0f, found + 1.0f, d * 0.5f + 0.05f), new Vector3(0.9f, 2.0f, 0.12f), Hex("#7A4A2A"));
            foreach (float y in new[] { found + 3.6f, found + 6.0f })
            {
                foreach (float x in new[] { -0.75f, 0.75f })
                {
                    m.Box(new Vector3(x, y, d * 0.5f + 0.05f), new Vector3(0.7f, 1.1f, 0.1f), Glass);
                    m.Box(new Vector3(x, y + 0.62f, d * 0.5f + 0.07f), new Vector3(0.86f, 0.14f, 0.12f), trim);
                }

                m.Box(new Vector3(w * 0.5f + 0.05f, y, 0f), new Vector3(0.1f, 1.1f, 0.7f), Glass);
                m.Box(new Vector3(-w * 0.5f - 0.05f, y, 0f), new Vector3(0.1f, 1.1f, 0.7f), Glass);
            }

            m.Box(new Vector3(0f, found + 3.0f, d * 0.5f + 0.45f), new Vector3(2.4f, 0.1f, 0.8f), trim);
            m.Beam(new Vector3(-1.2f, found + 3.6f, d * 0.5f + 0.82f), new Vector3(1.2f, found + 3.6f, d * 0.5f + 0.82f), 0.08f, WoodDark);
            return m.ToMesh("tallhouse" + variant);
        }

        private static Mesh DesignStiltHouse(int variant)
        {
            var m = new KitMesh();
            const float deckW = 6.2f, deckD = 5.4f, deckY = 1.25f;
            float w = variant == 0 ? 4.2f : 3.6f;
            float d = variant == 0 ? 3.4f : 3.6f;
            foreach (float x in new[] { -deckW * 0.5f + 0.3f, 0f, deckW * 0.5f - 0.3f })
            {
                foreach (float z in new[] { -deckD * 0.5f + 0.3f, deckD * 0.5f - 0.3f })
                {
                    m.Cylinder(new Vector3(x, -0.9f, z), 0.16f, 0.16f, deckY + 0.9f, 6, _ => WoodDark, null);
                }
            }

            for (int k = 0; k < 8; k++)
            {
                float x = -deckW * 0.5f + (k + 0.5f) * deckW / 8f;
                m.Box(new Vector3(x, deckY, 0f), new Vector3(deckW / 8f - 0.03f, 0.18f, deckD), k % 2 == 0 ? Wood : WoodLight);
            }

            // Hut at the back of the deck, railing along the front, steps on the right.
            float hutZ = -deckD * 0.5f + d * 0.5f + 0.25f;
            m.Push(new Vector3(0f, 0f, hutZ), Quaternion.identity);
            Color plank = variant == 0 ? Hex("#C68A52") : Hex("#4FA3A8");
            m.Box(new Vector3(0f, deckY + 0.09f + 1.15f, 0f), new Vector3(w, 2.3f, d), plank);
            m.Box(new Vector3(0f, deckY + 0.09f + 1.0f, d * 0.5f + 0.05f), new Vector3(0.9f, 1.9f, 0.1f), WoodDark);
            m.Box(new Vector3(-1.25f, deckY + 1.5f, d * 0.5f + 0.05f), new Vector3(0.7f, 0.7f, 0.1f), Glass);
            m.Box(new Vector3(1.25f, deckY + 1.5f, d * 0.5f + 0.05f), new Vector3(0.7f, 0.7f, 0.1f), Glass);
            GableRoof(m, w, d, deckY + 0.09f + 2.3f, 0.62f, variant == 0 ? Thatch : Hex("#B56A4A"), variant == 0 ? ThatchAlt : Hex("#9F5B3E"), plank, variant == 0);
            m.Pop();

            float railZ = deckD * 0.5f - 0.1f;
            for (int k = 0; k <= 6; k++)
            {
                float x = -deckW * 0.5f + 0.1f + k * (deckW - 2.0f) / 6f;
                m.Beam(new Vector3(x, deckY, railZ), new Vector3(x, deckY + 0.95f, railZ), 0.1f, WoodDark);
            }

            m.Beam(new Vector3(-deckW * 0.5f + 0.1f, deckY + 0.95f, railZ), new Vector3(deckW * 0.5f - 1.9f, deckY + 0.95f, railZ), 0.1f, WoodLight);
            for (int k = 0; k < 4; k++)
            {
                m.Box(new Vector3(deckW * 0.5f - 0.95f, deckY - 0.16f - k * 0.3f, deckD * 0.5f + 0.25f + k * 0.38f), new Vector3(1.5f, 0.14f, 0.4f), k % 2 == 0 ? Wood : WoodLight);
            }

            return m.ToMesh("stilthouse" + variant);
        }

        /// <summary>A pier running along local X (length <paramref name="length"/>), 2.4 m wide, deck 0.55 m above its origin.</summary>
        private static Mesh DesignDock(float length)
        {
            var m = new KitMesh();
            int planks = Mathf.RoundToInt(length / 0.75f);
            for (int k = 0; k < planks; k++)
            {
                float x = -length * 0.5f + (k + 0.5f) * length / planks;
                m.Box(new Vector3(x, 0.55f, 0f), new Vector3(length / planks - 0.04f, 0.16f, 2.4f), k % 2 == 0 ? Wood : WoodLight);
            }

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.5f) + 1);
            for (int k = 0; k < posts; k++)
            {
                float x = -length * 0.5f + 0.2f + k * (length - 0.4f) / (posts - 1);
                foreach (float z in new[] { -1.08f, 1.08f })
                {
                    m.Cylinder(new Vector3(x, -1.1f, z), 0.14f, 0.14f, 1.95f, 6, _ => WoodDark, Hex("#6E4A2C"));
                }
            }

            return m.ToMesh("dock" + Mathf.RoundToInt(length));
        }

        private static Mesh DesignPool()
        {
            var m = new KitMesh();
            m.Cylinder(Vector3.zero, 2.3f, 2.3f, 0.85f, 14, s => s % 2 == 0 ? Hex("#3E86C8") : Hex("#F1F5F8"), Hex("#DDE6EE"));
            m.Cylinder(new Vector3(0f, 0.1f, 0f), 2.1f, 2.1f, 0.68f, 14, _ => Hex("#3E86C8"), Hex("#3FD0E8"));
            m.Beam(new Vector3(2.35f, 0f, 0.3f), new Vector3(2.35f, 1.25f, 0.3f), 0.08f, Hex("#C9CED6"));
            m.Beam(new Vector3(2.35f, 0f, -0.3f), new Vector3(2.35f, 1.25f, -0.3f), 0.08f, Hex("#C9CED6"));
            for (int k = 0; k < 3; k++)
            {
                m.Beam(new Vector3(2.35f, 0.3f + k * 0.32f, -0.3f), new Vector3(2.35f, 0.3f + k * 0.32f, 0.3f), 0.07f, Hex("#C9CED6"));
            }

            return m.ToMesh("pool");
        }

        // ------------------------------------------------------------------ props

        private static Mesh DesignUmbrella(Color a, Color b, bool table)
        {
            var m = new KitMesh();
            m.Cylinder(Vector3.zero, 0.06f, 0.06f, 2.35f, 5, _ => Hex("#D9DDE3"), null);
            m.Push(new Vector3(0f, 2.05f, 0f), Quaternion.Euler(table ? 0f : 9f, 0f, table ? 0f : 6f));
            m.Cylinder(Vector3.zero, 1.65f, 0.06f, 0.6f, 8, s => s % 2 == 0 ? a : b, null);
            m.Cylinder(new Vector3(0f, -0.02f, 0f), 1.65f, 1.65f, 0.02f, 8, s => Shade(s % 2 == 0 ? a : b, 0.8f), null, Shade(a, 0.75f));
            m.Pop();
            if (table)
            {
                m.Cylinder(new Vector3(0f, 0.72f, 0f), 0.75f, 0.75f, 0.08f, 8, _ => WoodLight, Wood);
                m.Cylinder(Vector3.zero, 0.3f, 0.12f, 0.08f, 6, _ => Hex("#8A8F99"), Hex("#8A8F99"));
            }

            return m.ToMesh("umbrella");
        }

        private static Mesh DesignLounger()
        {
            var m = new KitMesh();
            Color frame = Hex("#F4F6F8");
            Color cloth = Hex("#3B8FD8");
            for (int k = 0; k < 5; k++)
            {
                m.Box(new Vector3(0f, 0.36f, -0.55f + k * 0.28f), new Vector3(0.66f, 0.07f, 0.25f), k % 2 == 0 ? cloth : Shade(cloth, 0.88f));
            }

            m.Box(new Vector3(0f, 0.62f, -0.95f), new Vector3(0.66f, 0.07f, 0.7f), Quaternion.Euler(-38f, 0f, 0f), cloth);
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                m.Beam(new Vector3(x, 0.3f, -0.75f), new Vector3(x, 0.3f, 0.75f), 0.07f, frame);
                m.Beam(new Vector3(x, 0f, -0.6f), new Vector3(x, 0.3f, -0.6f), 0.07f, frame);
                m.Beam(new Vector3(x, 0f, 0.65f), new Vector3(x, 0.3f, 0.65f), 0.07f, frame);
            }

            return m.ToMesh("lounger");
        }

        private static Mesh DesignBuoy()
        {
            var m = new KitMesh();
            m.Cylinder(new Vector3(0f, -0.25f, 0f), 0.55f, 0.55f, 0.5f, 8, s => s % 2 == 0 ? Hex("#F2952B") : Hex("#E2573B"), Hex("#F6B04A"), Hex("#C9742A"));
            m.Cylinder(new Vector3(0f, 0.25f, 0f), 0.42f, 0.14f, 1.0f, 6, s => s % 2 == 0 ? Hex("#E2573B") : Hex("#F6D34A"), null);
            m.Box(new Vector3(0f, 1.36f, 0f), new Vector3(0.22f, 0.22f, 0.22f), Hex("#FFE9A6"), Hex("#FFF6D0"));
            return m.ToMesh("buoy");
        }

        private static Mesh DesignLifebuoy()
        {
            var m = new KitMesh();
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                m.Push(Vector3.zero, Quaternion.Euler(0f, a, 0f));
                m.Box(new Vector3(0f, 0.09f, 0.42f), new Vector3(0.36f, 0.18f, 0.2f), k % 2 == 0 ? Hex("#E2483B") : Hex("#F7F4EC"));
                m.Pop();
            }

            return m.ToMesh("lifebuoy");
        }

        private static Mesh DesignTent(Color a, Color b)
        {
            var m = new KitMesh();
            const float w = 1.3f, h = 1.55f, d = 1.5f;
            m.Quad(new Vector3(-w, 0f, -d), new Vector3(-w, 0f, d), new Vector3(0f, h, d), new Vector3(0f, h, -d), a, Vector3.left + Vector3.up);
            m.Quad(new Vector3(w, 0f, -d), new Vector3(w, 0f, d), new Vector3(0f, h, d), new Vector3(0f, h, -d), b, Vector3.right + Vector3.up);
            m.Tri(new Vector3(-w, 0f, -d), new Vector3(w, 0f, -d), new Vector3(0f, h, -d), Shade(a, 0.8f), Vector3.back);
            m.Tri(new Vector3(-w, 0f, d), new Vector3(-0.25f, 0f, d), new Vector3(0f, h, d), Shade(a, 0.9f), Vector3.forward);
            m.Tri(new Vector3(w, 0f, d), new Vector3(0.25f, 0f, d), new Vector3(0f, h, d), Shade(b, 0.9f), Vector3.forward);
            return m.ToMesh("tent");
        }

        private static Mesh DesignPicnicTable()
        {
            var m = new KitMesh();
            Color red = Hex("#D9483B");
            m.Box(new Vector3(0f, 0.78f, 0f), new Vector3(1.9f, 0.09f, 0.9f), red, Hex("#E86A5C"));
            foreach (float z in new[] { -0.78f, 0.78f })
            {
                m.Box(new Vector3(0f, 0.45f, z), new Vector3(1.9f, 0.08f, 0.32f), Hex("#3F7FC0"), Hex("#5A97D6"));
            }

            foreach (float x in new[] { -0.75f, 0.75f })
            {
                m.Beam(new Vector3(x, 0f, -0.8f), new Vector3(x, 0.76f, -0.2f), 0.09f, WoodDark);
                m.Beam(new Vector3(x, 0f, 0.8f), new Vector3(x, 0.76f, 0.2f), 0.09f, WoodDark);
            }

            return m.ToMesh("picnictable");
        }

        private static Mesh DesignTrashBin()
        {
            var m = new KitMesh();
            m.Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), Hex("#3F8E4C"));
            m.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.68f, 0.1f, 0.68f), Hex("#2F6E3B"), Hex("#3A7E46"));
            return m.ToMesh("trashbin");
        }

        private static Mesh DesignMarketTable(int variant)
        {
            var m = new KitMesh();
            m.Box(new Vector3(0f, 0.82f, 0f), new Vector3(2.3f, 0.09f, 1.0f), WoodLight, Wood);
            foreach (float x in new[] { -1.0f, 1.0f })
            {
                foreach (float z in new[] { -0.38f, 0.38f })
                {
                    m.Beam(new Vector3(x, 0f, z), new Vector3(x, 0.8f, z), 0.09f, WoodDark);
                }
            }

            Color[] produce = variant == 0
                ? new[] { Hex("#E2483B"), Hex("#5DB046"), Hex("#F2952B") }
                : new[] { Hex("#F6D34A"), Hex("#9B59B6"), Hex("#5DB046") };
            for (int k = 0; k < 3; k++)
            {
                float x = -0.75f + k * 0.75f;
                m.Box(new Vector3(x, 0.96f, 0f), new Vector3(0.62f, 0.2f, 0.72f), WoodDark);
                for (int i = 0; i < 4; i++)
                {
                    m.Box(new Vector3(x - 0.15f + (i % 2) * 0.3f, 1.12f, -0.17f + (i / 2) * 0.34f), new Vector3(0.24f, 0.2f, 0.26f),
                        Quaternion.Euler(0f, 20f * i, 0f), produce[k], Shade(produce[k], 1.15f));
                }
            }

            return m.ToMesh("markettable" + variant);
        }

        private static Mesh DesignSurfboard(Color a, Color b)
        {
            var m = new KitMesh();
            m.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.56f, 0.09f, 1.6f), a);
            m.Box(new Vector3(0f, 0.06f, 0.98f), new Vector3(0.4f, 0.09f, 0.42f), a);
            m.Box(new Vector3(0f, 0.06f, -0.98f), new Vector3(0.44f, 0.09f, 0.4f), a);
            m.Box(new Vector3(0f, 0.11f, 0f), new Vector3(0.12f, 0.02f, 2.3f), b);
            return m.ToMesh("surfboard");
        }

        private static Mesh DesignPig()
        {
            var m = new KitMesh();
            Color pink = Hex("#F2A7A0");
            Color dark = Hex("#D98880");
            m.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.62f, 1.2f), pink);
            m.Box(new Vector3(0f, 0.62f, 0.78f), new Vector3(0.58f, 0.52f, 0.45f), pink);
            m.Box(new Vector3(0f, 0.55f, 1.05f), new Vector3(0.3f, 0.22f, 0.14f), dark);
            m.Box(new Vector3(-0.2f, 0.95f, 0.7f), new Vector3(0.16f, 0.18f, 0.1f), dark);
            m.Box(new Vector3(0.2f, 0.95f, 0.7f), new Vector3(0.16f, 0.18f, 0.1f), dark);
            foreach (float x in new[] { -0.23f, 0.23f })
            {
                foreach (float z in new[] { -0.42f, 0.42f })
                {
                    m.Box(new Vector3(x, 0.13f, z), new Vector3(0.18f, 0.26f, 0.18f), dark);
                }
            }

            return m.ToMesh("pig");
        }

        private static Mesh DesignBus()
        {
            var m = new KitMesh();
            const float l = 7.4f, w = 2.3f;
            m.Box(new Vector3(0f, 1.5f, 0f), new Vector3(w, 2.2f, l), Hex("#F4F6F4"), Hex("#E3E8E4"));
            m.Box(new Vector3(0f, 0.95f, 0f), new Vector3(w + 0.04f, 0.32f, l + 0.04f), Hex("#2E9A54"));
            m.Box(new Vector3(0f, 0.58f, 0f), new Vector3(w + 0.02f, 0.3f, l + 0.02f), Hex("#3A3F47"));
            foreach (float side in new[] { -1f, 1f })
            {
                for (int k = 0; k < 5; k++)
                {
                    m.Box(new Vector3(side * (w * 0.5f + 0.02f), 1.85f, -2.7f + k * 1.3f), new Vector3(0.06f, 0.8f, 1.05f), Hex("#2B3A4A"));
                }

                foreach (float z in new[] { -2.3f, 2.3f })
                {
                    m.Box(new Vector3(side * (w * 0.5f - 0.1f), 0.42f, z), new Vector3(0.34f, 0.84f, 0.84f), Quaternion.Euler(45f, 0f, 0f), Hex("#23262B"));
                }
            }

            m.Box(new Vector3(0f, 1.8f, l * 0.5f + 0.02f), new Vector3(w - 0.3f, 1.0f, 0.06f), Hex("#2B3A4A"));
            m.Box(new Vector3(-0.75f, 0.75f, l * 0.5f + 0.03f), new Vector3(0.36f, 0.2f, 0.06f), Hex("#FFE9A6"));
            m.Box(new Vector3(0.75f, 0.75f, l * 0.5f + 0.03f), new Vector3(0.36f, 0.2f, 0.06f), Hex("#FFE9A6"));
            return m.ToMesh("bus");
        }

        /// <summary>A small open row boat, bow toward +Z, waterline at its origin.</summary>
        private static Mesh DesignRowBoat(Color hull, Color trim)
        {
            var m = new KitMesh();
            const float w = 0.62f, l = 1.35f, top = 0.42f, keel = -0.22f;
            Vector3 bow = new Vector3(0f, top, l + 0.85f);
            m.Quad(new Vector3(-w, top, -l), new Vector3(-w, top, l), new Vector3(-w * 0.55f, keel, l * 0.9f), new Vector3(-w * 0.55f, keel, -l), hull, Vector3.left);
            m.Quad(new Vector3(w, top, -l), new Vector3(w, top, l), new Vector3(w * 0.55f, keel, l * 0.9f), new Vector3(w * 0.55f, keel, -l), Shade(hull, 0.88f), Vector3.right);
            m.Quad(new Vector3(-w, top, -l), new Vector3(w, top, -l), new Vector3(w * 0.55f, keel, -l), new Vector3(-w * 0.55f, keel, -l), Shade(hull, 0.8f), Vector3.back);
            m.Tri(new Vector3(-w, top, l), bow, new Vector3(-w * 0.55f, keel, l * 0.9f), hull, Vector3.left + Vector3.forward);
            m.Tri(new Vector3(w, top, l), bow, new Vector3(w * 0.55f, keel, l * 0.9f), Shade(hull, 0.88f), Vector3.right + Vector3.forward);
            m.Tri(new Vector3(-w * 0.55f, keel, l * 0.9f), new Vector3(w * 0.55f, keel, l * 0.9f), bow, Shade(hull, 0.75f), Vector3.down + Vector3.forward);
            m.Quad(new Vector3(-w * 0.55f, keel + 0.06f, -l), new Vector3(w * 0.55f, keel + 0.06f, -l), new Vector3(w * 0.55f, keel + 0.06f, l * 0.9f), new Vector3(-w * 0.55f, keel + 0.06f, l * 0.9f), WoodLight, Vector3.up);
            m.Beam(new Vector3(-w, top, -l), new Vector3(-w, top, l), 0.09f, trim);
            m.Beam(new Vector3(w, top, -l), new Vector3(w, top, l), 0.09f, trim);
            m.Beam(new Vector3(-w, top, l), bow, 0.09f, trim);
            m.Beam(new Vector3(w, top, l), bow, 0.09f, trim);
            m.Box(new Vector3(0f, top - 0.14f, -0.5f), new Vector3(w * 2f - 0.06f, 0.06f, 0.3f), WoodLight);
            m.Box(new Vector3(0f, top - 0.14f, 0.55f), new Vector3(w * 2f - 0.06f, 0.06f, 0.3f), WoodLight);
            m.Beam(new Vector3(-w, top + 0.05f, 0f), new Vector3(-w - 1.1f, top - 0.25f, -0.5f), 0.06f, WoodDark);
            m.Beam(new Vector3(w, top + 0.05f, 0f), new Vector3(w + 1.1f, top - 0.25f, -0.5f), 0.06f, WoodDark);
            return m.ToMesh("rowboat");
        }

        private static Mesh DesignSailboat()
        {
            var m = new KitMesh();
            Color hull = Hex("#B9824F");
            Color sail = Hex("#F3EAD3");
            m.Box(new Vector3(0f, 0.25f, 0f), new Vector3(1.9f, 0.95f, 4.4f), hull, WoodLight);
            m.Tri(new Vector3(-0.95f, 0.72f, 2.2f), new Vector3(0.95f, 0.72f, 2.2f), new Vector3(0f, 0.72f, 3.7f), WoodLight, Vector3.up);
            m.Quad(new Vector3(-0.95f, -0.22f, 2.2f), new Vector3(-0.95f, 0.72f, 2.2f), new Vector3(0f, 0.72f, 3.7f), new Vector3(0f, -0.1f, 3.3f), hull, Vector3.left + Vector3.forward);
            m.Quad(new Vector3(0.95f, -0.22f, 2.2f), new Vector3(0.95f, 0.72f, 2.2f), new Vector3(0f, 0.72f, 3.7f), new Vector3(0f, -0.1f, 3.3f), Shade(hull, 0.9f), Vector3.right + Vector3.forward);
            m.Box(new Vector3(0f, 0.86f, 0f), new Vector3(2.06f, 0.14f, 4.5f), WoodDark);
            m.Box(new Vector3(0f, 1.2f, -0.9f), new Vector3(1.3f, 0.7f, 1.5f), Hex("#F4F6F8"), Hex("#3F7FC0"));
            m.Cylinder(new Vector3(0f, 0.9f, 0.5f), 0.09f, 0.06f, 6.2f, 5, _ => WoodDark, null);
            m.Beam(new Vector3(0f, 1.9f, 0.5f), new Vector3(0f, 1.9f, -2.3f), 0.08f, WoodDark);
            Vector3 a = new Vector3(0f, 6.9f, 0.45f), b = new Vector3(0f, 2.0f, 0.45f), c = new Vector3(0f, 2.0f, -2.25f);
            m.Tri(a, b, c, sail, Vector3.right);
            m.Tri(a, b, c, Shade(sail, 0.9f), Vector3.left);
            Vector3 d = new Vector3(0f, 6.4f, 0.6f), e = new Vector3(0f, 1.3f, 0.6f), f = new Vector3(0f, 1.3f, 3.4f);
            m.Tri(d, e, f, Shade(sail, 0.96f), Vector3.right);
            m.Tri(d, e, f, Shade(sail, 0.86f), Vector3.left);
            return m.ToMesh("sailboat");
        }
    }
}
