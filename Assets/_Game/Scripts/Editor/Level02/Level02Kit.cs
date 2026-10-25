using System.Collections.Generic;
using System.IO;
using AlienDefense.Environment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Level_02 prefab kit under Assets/_Game/Prefabs/Level02: thin PF_* wrappers around the project's own
    /// prefabs (the sources stay untouched - scale, colliders and material swaps are overrides on the wrapper), the
    /// generated buildings/props from Level02KitDesigns, palette-remapped copies of the Pandazole atlas for autumn,
    /// yellow-green and golden foliage, purple flowers and the dark enemy area, plus small FX (chimney smoke, a capped
    /// small fire).</summary>
    public static partial class Level02Builder
    {
        private const string PrefabRoot = "Assets/_Game/Prefabs/Level02";
        private const string PZ = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
        private const string PE = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";
        private const string PV = "Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/";
        private const string GR = "Assets/Gridness Studios/Lite Farm Pack/Prefabs/";
        private const string LF = "Assets/LowPolyFarmLite/Prefabs/";
        private const string CARS = "Assets/Pack_FREE_Cars/Prefabs/";
        private const string LPV = "Assets/LowPolyVehicleStarterPack/Prefabs/";
        private const string PandaMatPath = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Materials/PandaMat.mat";

        private static readonly Dictionary<string, GameObject> Kit = new Dictionary<string, GameObject>();
        private static Material _vertexLit;

        static partial void BuildKit()
        {
            Kit.Clear();
            _vertexLit = EnsureVertexLitMaterial();
            Material autumn = PandaVariant("Autumn", c => RemapGreens(c, 0.075f, 1.08f, 1.12f));
            Material yellowGreen = PandaVariant("YellowGreen", c => RemapGreens(c, 0.2f, 0.92f, 1.1f));
            Material golden = PandaVariant("Golden", c => RemapGreens(c, 0.125f, 1f, 1.16f));
            Material purple = PandaVariant("Purple", RemapRedsToPurple);
            Material dark = PandaVariant("Weathered", c => { Color.RGBToHSV(c, out float h, out float s, out float v); return Color.HSVToRGB(h, s * 0.5f, v * 0.62f); });
            Material rockWarm = TintedCopy("MAT_Level02_RockWarm", "Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Rocks_mat.mat", Hex("#D8CDB6"));

            // Trees and shrubs (six groups: pine, broadleaf, yellow-green, autumn/golden, shrub, round bush).
            Wrap("Vegetation", "PF_Tree_Pine_01", PZ + "Env_Tree_07.prefab", 1.05f);
            Wrap("Vegetation", "PF_Tree_Pine_02", PE + "Trees/PT_Pine_Tree_03_green.prefab", 0.58f);
            Wrap("Vegetation", "PF_Tree_Broadleaf_01", PZ + "Env_Tree_01.prefab", 1.1f);
            Wrap("Vegetation", "PF_Tree_Broadleaf_02", PZ + "Env_Tree_06.prefab", 1.15f);
            Wrap("Vegetation", "PF_Tree_Broadleaf_03", PZ + "Env_Tree_02.prefab", 1.05f);
            Wrap("Vegetation", "PF_Tree_YellowGreen_01", PZ + "Env_Tree_03.prefab", 1.15f, yellowGreen);
            Wrap("Vegetation", "PF_Tree_YellowGreen_02", PZ + "Env_Tree_04.prefab", 1.05f, yellowGreen);
            Wrap("Vegetation", "PF_Tree_Autumn_01", PZ + "Env_Tree_06.prefab", 1.15f, autumn);
            Wrap("Vegetation", "PF_Tree_Autumn_02", PZ + "Env_Tree_01.prefab", 1.1f, autumn);
            Wrap("Vegetation", "PF_Tree_Golden_01", PZ + "Env_Tree_05.prefab", 1.05f, golden);
            Wrap("Vegetation", "PF_Tree_Golden_02", PZ + "Env_Tree_03.prefab", 1.1f, golden);
            Wrap("Vegetation", "PF_Tree_Fruit_01", PE + "Trees/PT_Fruit_Tree_01_apples.prefab", 0.55f);
            Wrap("Vegetation", "PF_Tree_Dead_01", PE + "Trees/PT_Pine_Tree_03_dead.prefab", 0.55f);
            Wrap("Vegetation", "PF_Tree_Dead_02", PE + "Trees/PT_Fruit_Tree_01_dead.prefab", 0.7f);
            Wrap("Vegetation", "PF_Shrub_01", PE + "Shrubs/PT_Generic_Shrub_01_green.prefab", 0.75f);
            Wrap("Vegetation", "PF_Shrub_Dead_01", PE + "Shrubs/PT_Generic_Shrub_01_dead.prefab", 0.8f);
            Wrap("Vegetation", "PF_Bush_Round_01", PZ + "Env_Bush_02.prefab", 1.15f);
            Wrap("Vegetation", "PF_Bush_02", PZ + "Env_Bush_01.prefab", 1.15f);
            Wrap("Vegetation", "PF_Bush_Autumn_01", PZ + "Env_Bush_02.prefab", 1.05f, autumn);
            Wrap("Vegetation", "PF_Bush_YellowGreen_01", PZ + "Env_Bush_01.prefab", 1.05f, yellowGreen);

            // Flowers, weeds and grass.
            Wrap("Vegetation", "PF_Flower_Red", PZ + "Env_GrassPlant_02.prefab", 1.5f);
            Wrap("Vegetation", "PF_Flower_Purple", PZ + "Env_GrassPlant_02.prefab", 1.5f, purple);
            Wrap("Vegetation", "PF_Flower_Poppy", PE + "Flowers/PT_Poppy_02.prefab", 0.9f);
            Wrap("Vegetation", "PF_Flower_Daisy", GR + "Flower.prefab", 0.75f);
            Wrap("Vegetation", "PF_Weed_Orange", PZ + "Env_GrassPlant_07.prefab", 1.35f, autumn);
            Wrap("Vegetation", "PF_Weed_Yellow", PZ + "Env_GrassPlant_06.prefab", 1.35f, golden);
            Wrap("Vegetation", "PF_Grass_01", PZ + "Env_GrassPlant_05.prefab", 1.5f);
            Wrap("Vegetation", "PF_Grass_02", PZ + "Env_GrassPlant_01.prefab", 1.7f);
            Wrap("Vegetation", "PF_Grass_03", PE + "Plants/PT_Grass_02.prefab", 0.75f);
            Wrap("Vegetation", "PF_LeafPlant_01", PZ + "Env_GrassPlant_03.prefab", 1.7f);
            Wrap("Vegetation", "PF_Mushroom_01", PE + "Mushrooms/PT_Caesars_Mushroom_01.prefab", 1.7f);

            // Rocks (warm beige, matching the cliffs).
            Wrap("Props", "PF_Rock_01", "Assets/_Game/Prefabs/Base/Environment/BP_Boulder_A.prefab", 0.55f);
            Wrap("Props", "PF_Rock_02", "Assets/_Game/Prefabs/Base/Environment/BP_Boulder_B.prefab", 0.55f);
            Wrap("Props", "PF_Rock_03", "Assets/_Game/Prefabs/Base/Environment/BP_Boulder_C.prefab", 0.55f);
            Wrap("Props", "PF_Rock_Tall_01", PE + "Rocks/PT_Menhir_Rock_02.prefab", 0.9f, rockWarm);
            Wrap("Props", "PF_Stones_01", PE + "Rocks/PT_River_Rock_Pile_02.prefab", 1.3f, rockWarm);
            Wrap("Props", "PF_Stones_02", PE + "Rocks/PT_Generic_Rock_01.prefab", 2.2f, rockWarm);

            // Farm props.
            Wrap("Props", "PF_Barrel", PZ + "Prop_Berrel_02_A.prefab", 0.85f);
            Wrap("Props", "PF_Barrel_02", PZ + "Prop_Berrel_04.prefab", 0.85f);
            Wrap("Props", "PF_Barrel_Open", PZ + "Prop_Berrel_03.prefab", 0.85f);
            Wrap("Props", "PF_MetalBarrel", PZ + "Prop_MetalBerrel_01.prefab", 0.8f);
            Wrap("Props", "PF_Crate", PZ + "Prop_WoodenBox_01.prefab", 0.75f);
            Wrap("Props", "PF_Crate_02", PZ + "Prop_WoodenBox_03.prefab", 0.75f);
            Wrap("Props", "PF_Crate_Long", PZ + "Prop_WoodenBox_04.prefab", 0.75f);
            Wrap("Props", "PF_Crate_Open", PZ + "Prop_WoodenCrates_01.prefab", 0.8f);
            Wrap("Props", "PF_Crate_Open_02", PZ + "Prop_WoodenCrates_02.prefab", 0.8f);
            Wrap("Props", "PF_Sack_01", PZ + "Prop_FoodSack_01.prefab", 0.8f);
            Wrap("Props", "PF_Sack_02", PZ + "Prop_FoodSack_03.prefab", 0.8f);
            Wrap("Props", "PF_Hay_Round", PZ + "Prop_Haystack_02.prefab", 1.2f);
            Wrap("Props", "PF_Hay_Bale", PZ + "Prop_Haystack_04.prefab", 1.05f);
            Wrap("Props", "PF_Hay_Pile", PZ + "Prop_Haystack_05.prefab", 1.3f);
            Wrap("Props", "PF_Hay_Small", PZ + "Prop_Haystack_03.prefab", 1.1f);
            Wrap("Props", "PF_Pallet", PZ + "Prop_Pallete_01.prefab", 0.8f);
            Wrap("Props", "PF_Bucket", PZ + "Prop_Bucket_04.prefab", 0.9f);
            Wrap("Props", "PF_Bucket_Metal", PZ + "Prop_Bucket_01.prefab", 0.7f);
            Wrap("Props", "PF_WateringCan_01", PZ + "Prop_WateringCan_01.prefab", 0.9f);
            Wrap("Props", "PF_WateringCan_02", PZ + "Prop_WateringCan_02.prefab", 0.9f);
            Wrap("Props", "PF_WateringCan_03", PZ + "Prop_WateringCan_03.prefab", 0.9f);
            Wrap("Props", "PF_Wheelbarrow", PZ + "Prop_Wheelbarrow.prefab", 0.55f);
            Wrap("Props", "PF_Tool_Shovel", PZ + "Prop_BigFarmingTool_01.prefab", 0.85f);
            Wrap("Props", "PF_Tool_Rake", PZ + "Prop_BigFarmingTool_05.prefab", 0.85f);
            Wrap("Props", "PF_Tool_Fork", PZ + "Prop_BigFarmingTool_04.prefab", 0.85f);
            Wrap("Props", "PF_Feeder", PZ + "Prop_AnimalFeeder_01.prefab", 0.8f);
            Wrap("Props", "PF_Feeder_02", PZ + "Prop_AnimalFeeder_02.prefab", 0.8f);
            Wrap("Props", "PF_Pumpkin", PZ + "food_Pumpkin.prefab", 1.7f);
            Wrap("Props", "PF_Watermelon", PZ + "food_Watermelon.prefab", 1.3f);
            Wrap("Props", "PF_Cabbage", LF + "Cabbage_01.prefab", 1f);
            Wrap("Props", "PF_LogPile", PE + "Trees/PT_Fruit_Tree_01_logs.prefab", 0.75f);
            Wrap("Props", "PF_LogPile_02", PE + "Trees/PT_Pine_Tree_03_logs.prefab", 0.6f);
            Wrap("Props", "PF_Stump", PE + "Trees/PT_Pine_Tree_03_stump.prefab", 0.6f);
            Wrap("Props", "PF_FarmPlot_01", PZ + "Env_FarmLand_03.prefab", 0.9f);
            Wrap("Props", "PF_FarmPlot_02", PZ + "Env_FarmLand_05.prefab", 0.9f);
            Wrap("Props", "PF_FarmPlot_03", PZ + "Env_FarmLand_05_Watered.prefab", 0.9f);
            Wrap("Props", "PF_Crop_Wheat", PZ + "Env_Wheat.prefab", 1f);
            Wrap("Props", "PF_Crop_Tomato", GR + "Plant_Tomato_Large.prefab", 0.8f);
            Wrap("Props", "PF_Crop_Cabbage", LF + "Cabbage_01.prefab", 0.9f);
            Wrap("Props", "PF_Fence_Rail", PZ + "Env_WoodFence_04.prefab", 1f);
            Wrap("Props", "PF_Fence_Rail_02", PZ + "Env_WoodFence_02.prefab", 1f);
            Wrap("Props", "PF_Fence_Picket", PV + "Fence/PT_Modular_Fence_Wood_01.prefab", 1f);
            Wrap("Props", "PF_Fence_Weathered", PZ + "Env_WoodFence_04.prefab", 1f, dark);

            // Generated kit props.
            KitPrefab("Props", "PF_Mailbox_01", DesignMailbox(0));
            KitPrefab("Props", "PF_Mailbox_02", DesignMailbox(1));
            KitPrefab("Props", "PF_Signpost", DesignSignpost());
            KitPrefab("Props", "PF_Bench", DesignBench());
            KitPrefab("Props", "PF_ClothesLine", DesignClothesLine());
            KitPrefab("Props", "PF_WagonWheel", DesignWagonWheel());
            KitPrefab("Props", "PF_Scarecrow", DesignScarecrow());

            // Buildings.
            for (int v = 0; v < 6; v++)
            {
                KitPrefab("Buildings", "PF_FarmHouse_0" + (v + 1), DesignHouse(v), go => AddChimneySmoke(go, v));
            }

            KitPrefab("Buildings", "PF_Church", DesignChurch());
            KitPrefab("Buildings", "PF_WaterTower", DesignWaterTower());
            KitPrefab("Buildings", "PF_EnemyBarn", DesignEnemyBarn());
            for (int v = 0; v < 3; v++)
            {
                KitPrefab("Buildings", "PF_MarketStall_0" + (v + 1), DesignMarketStall(v));
            }

            Wrap("Buildings", "PF_FarmHouse_Blue", PZ + "Bld_FarmerHouse.prefab", 1f);
            Wrap("Buildings", "PF_Barn_Red", PZ + "Bld_Barn_02.prefab", 1f);
            Wrap("Buildings", "PF_Barn_White", PZ + "Bld_Barn_01.prefab", 1f);
            Wrap("Buildings", "PF_Silo", PZ + "Bld_Silo_01.prefab", 1f);
            Wrap("Buildings", "PF_ChickenCoop", PZ + "Bld_ChickenCoop.prefab", 1f);
            Wrap("Buildings", "PF_Store", PZ + "Bld_StoreBuilding_01.prefab", 1f);
            Wrap("Buildings", "PF_OldShed", PZ + "Bld_Outhouses.prefab", 1f, dark);
            Wrap("Buildings", "PF_Well", PZ + "Env_Well_02.prefab", 1f);
            Wrap("Buildings", "PF_Well_Stone", PZ + "Env_Well_01.prefab", 1f, null, go =>
            {
                Mesh canopy = SaveMesh(DesignWellCanopy(true), MeshesFolder + "/SM_Level02_WellCanopy.asset");
                var child = new GameObject("Canopy");
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = canopy;
                child.AddComponent<MeshRenderer>().sharedMaterial = _vertexLit;
            });
            Wrap("Buildings", "PF_Windmill", PZ + "Bld_FarmMill_02.prefab", 1f, null, go => AddSpinner(go, "FRP_Bld_FarmMill_02_p2", 22f));
            Wrap("Buildings", "PF_WindPump", PZ + "Bld_FarmMill_01.prefab", 1f, null, go => AddSpinner(go, "FRP_Bld_FarmMill_01_p2", 40f));

            // Animals.
            Wrap("Animals", "PF_Cow", "Assets/_Game/Models/Animals/Cow.fbx", 0.3f, null, go => SetupAnimal(go, "Cow"));
            Wrap("Animals", "PF_Sheep", "Assets/_Game/Models/Animals/Sheep.fbx", 0.3f, null, go => SetupAnimal(go, "Sheep"));
            Wrap("Animals", "PF_Horse", "Assets/_Game/Models/Animals/Horse.fbx", 0.28f, null, go => SetupAnimal(go, "Horse"));

            // Vehicles.
            Wrap("Vehicles", "PF_Vehicle_PickupBlue", CARS + "Pickup.prefab", 0.9f);
            Wrap("Vehicles", "PF_Vehicle_Van", CARS + "Van.prefab", 0.9f);
            Wrap("Vehicles", "PF_Vehicle_TowTruck", CARS + "Towtruck.prefab", 0.9f);
            Wrap("Vehicles", "PF_Vehicle_PickupOld", LPV + "Pickup.prefab", 1.1f);
            Wrap("Vehicles", "PF_Vehicle_Scooter", LPV + "Scooter.prefab", 1.05f);
            Wrap("Vehicles", "PF_FireTruck", CARS + "Truck.prefab", 0.9f, null, AddFireTruckKit);

            // FX.
            Wrap("FX", "PF_Fire_Small", "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small_Simple.prefab", 0.8f, null, CapParticles);

            Report.Add("Kit: " + Kit.Count + " prefabs under " + PrefabRoot + ".");

            // The prefabs were just re-saved: put the player X-ray fade back on the tall ones.
            Report.Add(OcclusionSetup.ApplyToKitPrefabs());
            foreach (string key in new List<string>(Kit.Keys))
            {
                Kit[key] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(Kit[key]));
            }
        }

        // ------------------------------------------------------------------ prefab helpers

        private static GameObject Wrap(string folder, string name, string sourcePath, float scale, Material material = null, System.Action<GameObject> extra = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Report.Add("MISSING ASSET: " + sourcePath + " (" + name + " not created)");
                return null;
            }

            EnsureFolder(PrefabRoot + "/" + folder);
            var root = new GameObject(name);
            var child = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = child.transform.localScale * scale;
            foreach (Collider collider in child.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            if (material != null)
            {
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = material;
                    }

                    renderer.sharedMaterials = materials;
                }
            }

            extra?.Invoke(root);
            if (IsAbsorbableKit(folder, name))
            {
                AddAbsorbable(root, child.transform, folder == "Animals" || folder == "Vehicles");
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/" + folder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            Kit[name] = saved;
            return saved;
        }

        private static GameObject KitPrefab(string folder, string name, Mesh mesh, System.Action<GameObject> extra = null)
        {
            EnsureFolder(PrefabRoot + "/" + folder);
            Mesh saved = SaveMesh(mesh, MeshesFolder + "/" + mesh.name + ".asset");
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _vertexLit;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            extra?.Invoke(root);
            if (IsAbsorbableKit(folder, name))
            {
                AddAbsorbable(root, root.transform, false);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/" + folder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            Kit[name] = prefab;
            return prefab;
        }

        /// <summary>Level_01 convention: loose decoration (vegetation, props, animals, parked vehicles) can be pulled
        /// in by the tractor beam; structures (buildings, fences, field plots) cannot.</summary>
        private static bool IsAbsorbableKit(string folder, string name) =>
            (folder == "Vegetation" || folder == "Props" || folder == "Animals" || folder == "Vehicles")
            && !name.StartsWith("PF_Fence") && !name.StartsWith("PF_FarmPlot") && name != "PF_Vehicle_Scooter";

        private static void AddAbsorbable(GameObject root, Transform visual, bool oversized)
        {
            var absorbable = root.AddComponent<TractorAbsorbableProp>();
            var so = new SerializedObject(absorbable);
            so.FindProperty("_visualRoot").objectReferenceValue = visual;
            if (oversized)
            {
                so.FindProperty("_captureStartScale").floatValue = 0.5f; // same as Level_01's animals
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSpinner(GameObject root, string childName, float speed)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                {
                    var spinner = t.gameObject.AddComponent<AmbientSpinner>();
                    spinner.Configure(Vector3.forward, speed, t.GetComponent<Renderer>());
                    return;
                }
            }

            Report.Add("WARNING: spinner child " + childName + " not found.");
        }

        private static void SetupAnimal(GameObject root, string species)
        {
            Transform model = root.transform.GetChild(0);
            var animator = model.GetComponent<Animator>() ?? model.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Game/Models/Animals/Animations/" + species + ".controller");
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Models/Animals/" + species + ".fbx"))
            {
                if (o is Avatar avatar)
                {
                    animator.avatar = avatar;
                }
            }

            animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        private static void AddFireTruckKit(GameObject root)
        {
            Transform truck = root.transform.GetChild(0);
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in truck.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            Mesh ladder = SaveMesh(DesignFireLadder(b.size.z * 0.62f), MeshesFolder + "/SM_Level02_FireLadder.asset");
            var ladderGo = new GameObject("Ladder");
            ladderGo.transform.SetParent(root.transform, false);
            ladderGo.transform.position = new Vector3(b.center.x, b.max.y - 0.35f, b.center.z - b.size.z * 0.12f);
            ladderGo.AddComponent<MeshFilter>().sharedMesh = ladder;
            ladderGo.AddComponent<MeshRenderer>().sharedMaterial = _vertexLit;

            Mesh lights = SaveMesh(DesignLightBar(), MeshesFolder + "/SM_Level02_LightBar.asset");
            var lightGo = new GameObject("LightBar");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.position = new Vector3(b.center.x, b.max.y + 0.08f, b.max.z - b.size.z * 0.2f);
            lightGo.AddComponent<MeshFilter>().sharedMesh = lights;
            lightGo.AddComponent<MeshRenderer>().sharedMaterial = _vertexLit;
        }

        /// <summary>Mobile budget for the borrowed fire effect: every system capped to a few particles.</summary>
        private static void CapParticles(GameObject root)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.maxParticles = Mathf.Min(main.maxParticles, 16);
            }

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(light); // additional lights are expensive on mobile
            }
        }

        /// <summary>A thin wisp of chimney smoke: one small alpha-blended particle system, 6 particles max.</summary>
        private static void AddChimneySmoke(GameObject house, int variant)
        {
            if (variant == 3 || variant == 5)
            {
                return; // thatch / no chimney variants keep still
            }

            Bounds b = house.GetComponent<MeshFilter>().sharedMesh.bounds;
            float w = 5.0f + (variant % 3) * 0.35f;
            float d = 4.3f + (variant % 2) * 0.45f;
            var go = new GameObject("ChimneySmoke");
            go.transform.SetParent(house.transform, false);
            go.transform.localPosition = new Vector3(w * 0.27f, b.max.y + 0.1f, -d * 0.17f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 3.2f;
            main.startSpeed = 0.55f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startColor = new Color(0.93f, 0.93f, 0.92f, 0.55f);
            main.maxParticles = 6;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 1.8f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.08f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, -0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.1f, 0.1f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EnsureSmokeMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material EnsureSmokeMaterial()
        {
            string path = MaterialsFolder + "/MAT_Level02_SoftSmoke.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            string texturePath = TexturesFolder + "/T_Level02_SoftPuff.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            }

            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "MAT_Level02_SoftSmoke" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ palette variants

        private static Color RemapGreens(Color c, float hue, float saturation, float value)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (h > 0.18f && h < 0.48f && s > 0.22f && v > 0.18f)
            {
                return Color.HSVToRGB(hue, Mathf.Clamp01(s * saturation), Mathf.Clamp01(v * value + 0.03f));
            }

            return c;
        }

        private static Color RemapRedsToPurple(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if ((h < 0.05f || h > 0.92f) && s > 0.4f)
            {
                return Color.HSVToRGB(0.78f, s * 0.75f, Mathf.Clamp01(v * 1.05f));
            }

            return c;
        }

        /// <summary>A copy of the Pandazole atlas material whose palette texture has been recoloured - the source
        /// material and texture are never modified.</summary>
        private static Material PandaVariant(string suffix, System.Func<Color, Color> remap)
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);
            string materialPath = MaterialsFolder + "/MAT_Level02_Panda_" + suffix + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null && !_regenerateTextures)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Material>(PandaMatPath);
            var sourceTexture = (Texture2D)source.GetTexture("_BaseMap");
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(sourcePath));
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = remap(pixels[i]);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            string texturePath = TexturesFolder + "/T_Level02_Panda_" + suffix + ".png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.filterMode = sourceImporter.filterMode;
            importer.mipmapEnabled = sourceImporter.mipmapEnabled;
            importer.maxTextureSize = sourceImporter.maxTextureSize;
            importer.textureCompression = sourceImporter.textureCompression;
            importer.wrapMode = sourceImporter.wrapMode;
            importer.SaveAndReimport();
            var remapped = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            Material material = existing != null ? existing : new Material(source);
            material.name = "MAT_Level02_Panda_" + suffix;
            material.SetTexture("_BaseMap", remapped);
            material.SetTexture("_MainTex", remapped);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, materialPath);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TintedCopy(string name, string sourcePath, Color tint)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
            {
                Report.Add("MISSING ASSET: " + sourcePath);
                return null;
            }

            var material = new Material(source) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            // Polytope rock shader: vertical gradient between a top and a ground colour.
            foreach (string suffix in new[] { "", "1" })
            {
                if (material.HasProperty("_TopColor" + suffix))
                {
                    material.SetColor("_TopColor" + suffix, tint);
                }

                if (material.HasProperty("_GroundColor" + suffix))
                {
                    material.SetColor("_GroundColor" + suffix, Shade(tint, 0.62f));
                }
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
