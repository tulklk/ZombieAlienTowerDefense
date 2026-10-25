using System.Collections.Generic;
using System.IO;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds Level 3's boss from the imported model in Models/Zombie/Prefabs/BossLevel3 the same way the
    /// Level 2 boss is put together: its own material, its Walking/Attacking clips in a copy of the Level 2 boss
    /// Animator Controller (Walk state + "Attack" trigger), a copy of the Enemy_Boss_Level2 prefab with the model
    /// swapped (same height, same components, same behaviour asset), an EnemyDefinition, a BossEncounterDefinition,
    /// and the reference on Level_03_Definition. Safe to run again: every asset is updated in place.</summary>
    public static class Level03BossSetup
    {
        private const string ModelFolder = "Assets/_Game/Models/Zombie/Prefabs/BossLevel3";
        private const string WalkFbx = ModelFolder + "/Walking.fbx";
        private const string AttackFbx = ModelFolder + "/Attacking.fbx";
        private const string MaterialPath = ModelFolder + "/Materials/ZombieBossLevel3_Mat.mat";
        private const string ControllerPath = ModelFolder + "/BossLevel3_Walk.controller";

        private const string SourceMaterialPath = "Assets/_Game/Models/Zombie/Prefabs/BossLevel2/Materials/ZombieBossLevel2_Mat.mat";
        private const string SourceControllerPath = "Assets/_Game/Models/Zombie/Prefabs/BossLevel2/BossLevel2_Walk.controller";
        private const string SourcePrefabPath = "Assets/_Game/Prefabs/Enemies/Enemy_Boss_Level2.prefab";
        private const string SourceDefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Boss_Level2.asset";
        private const string SourceEncounterPath = "Assets/_Game/Data/Waves/Level02/Level02_BossEncounter.asset";

        private const string PrefabPath = "Assets/_Game/Prefabs/Enemies/Enemy_Boss_Level3.prefab";
        private const string DefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Boss_Level3.asset";
        private const string EncounterPath = "Assets/_Game/Data/Waves/Level03/Level03_BossEncounter.asset";
        private const string LevelDefinitionPath = "Assets/_Game/Data/Levels/Level_03_Definition.asset";

        [MenuItem("Tools/Tower Defense/Level 03/Setup Boss (Level 3)")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        public static string Setup()
        {
            var report = new List<string>();
            Material material = EnsureMaterial(report);
            AnimationClip walk = PrepareClip(WalkFbx, material, report);
            AnimationClip attack = PrepareClip(AttackFbx, material, report);
            if (walk == null || attack == null)
            {
                return "[Level03BossSetup] FAILED: Walking.fbx / Attacking.fbx have no animation clip.";
            }

            AnimationClip walkInPlace = MakeInPlace(walk, out float strideSpeed, report);
            AnimatorController controller = EnsureController(walkInPlace, attack, report);
            GameObject prefab = BuildPrefab(material, controller, walkInPlace, strideSpeed, attack.length, report);
            Object definition = EnsureDefinition(prefab, report);
            Object encounter = EnsureEncounter(definition, report);

            var level = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(LevelDefinitionPath));
            level.FindProperty("_bossEncounter").objectReferenceValue = encounter;
            level.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level.targetObject);
            report.Add("Level_03_Definition: Boss Encounter = " + EncounterPath + " (after the normal waves).");

            AssetDatabase.SaveAssets();
            return "[Level03BossSetup] Done.\n" + string.Join("\n", report);
        }

        private static Material EnsureMaterial(List<string> report)
        {
            EnsureFolder(ModelFolder + "/Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                AssetDatabase.CopyAsset(SourceMaterialPath, MaterialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }

            Texture2D baseColor = FindTexture("BaseColor");
            Texture2D normal = FindTexture("Normal_Bake");
            if (normal != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }

            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", null);
            EditorUtility.SetDirty(material);
            report.Add("Material: " + MaterialPath + " (copy of the Level 2 boss material; base colour "
                + (baseColor != null ? baseColor.name : "MISSING") + ", normal " + (normal != null ? normal.name : "MISSING") + ").");
            return material;
        }

        private static Texture2D FindTexture(string suffix)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ModelFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).EndsWith(suffix))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }

        /// <summary>Loop the clip (as on the Level 2 boss) and point the model's embedded material at ours.</summary>
        private static AnimationClip PrepareClip(string fbxPath, Material material, List<string> report)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = true;
            }

            importer.clipAnimations = clips;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material source in renderer.sharedMaterials)
                {
                    if (source != null && source != material)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
                    }
                }
            }

            importer.SaveAndReimport();

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    report.Add(Path.GetFileName(fbxPath) + ": clip '" + clip.name + "' " + clip.length.ToString("F2") + " s, loop on, material remapped.");
                    return clip;
                }
            }

            return null;
        }

        private static AnimatorController EnsureController(AnimationClip walk, AnimationClip attack, List<string> report)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            {
                AssetDatabase.CopyAsset(SourceControllerPath, ControllerPath);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                child.state.motion = child.state.name == "Attack" ? attack : walk;
                EditorUtility.SetDirty(child.state);
            }

            EditorUtility.SetDirty(controller);
            report.Add("Animator: " + ControllerPath + " (Walk default, 'Attack' trigger - same graph as the Level 2 boss).");
            return controller;
        }

        private static GameObject BuildPrefab(Material material, AnimatorController controller, AnimationClip walk, float strideSpeed,
            float attackLength, List<string> report)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            try
            {
                root.name = "Enemy_Boss_Level3";
                Transform model = root.transform.Find("VisualRoot/Model");
                float targetHeight = Bounds(model).size.y;

                for (int i = model.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(model.GetChild(i).gameObject);
                }

                // The model's own hierarchy sits directly under Model, so the clips' bone paths resolve from its Animator.
                var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WalkFbx));
                model.localScale = Vector3.one;
                model.localPosition = Vector3.zero;
                while (source.transform.childCount > 0)
                {
                    source.transform.GetChild(0).SetParent(model, false);
                }

                Object.DestroyImmediate(source);

                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = material;
                    }

                    renderer.sharedMaterials = materials;
                }

                float height = Mathf.Max(0.0001f, Bounds(model).size.y);
                model.localScale = Vector3.one * (targetHeight / height);
                model.localPosition = new Vector3(0f, root.transform.position.y - Bounds(model).min.y, 0f);

                // The bind pose is not the walk pose: plant the feet on the ground using the walk clip itself (the
                // lowest point of the skinned mesh over the cycle sits at the enemy's root).
                var animatorObject = model.gameObject;
                float lowest = LowestPointOverClip(model, walk);
                model.localPosition += Vector3.up * (root.transform.position.y - lowest);
                walk.SampleAnimation(animatorObject, 0f);

                var animator = model.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = null;

                var flash = new SerializedObject(root.GetComponent<EnemyHitFlash>());
                flash.FindProperty("_renderer").objectReferenceValue = model.GetComponentInChildren<Renderer>(true);
                flash.ApplyModifiedPropertiesWithoutUndo();

                var attack = new SerializedObject(root.GetComponent<EnemyBaseAttackVisual>());
                attack.FindProperty("_animator").objectReferenceValue = animator;
                attack.FindProperty("_attackHoldDuration").floatValue = attackLength;
                attack.ApplyModifiedPropertiesWithoutUndo();

                // The legs cycle at the pace that matches the move speed (no foot sliding, no "running" stride):
                // the clip's own stride speed (scaled model) vs the boss's MoveSpeed.
                var speed = root.GetComponent<EnemyAnimatorSpeed>();
                if (speed == null)
                {
                    speed = root.AddComponent<EnemyAnimatorSpeed>();
                }

                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
                float moveSpeed = definition != null ? definition.MoveSpeed : 1f;
                float animSpeed = Mathf.Clamp(moveSpeed / Mathf.Max(0.01f, strideSpeed * model.localScale.x), 0.2f, 1.5f);
                var speedSo = new SerializedObject(speed);
                speedSo.FindProperty("_animator").objectReferenceValue = animator;
                speedSo.FindProperty("_speedMultiplier").floatValue = animSpeed;
                speedSo.ApplyModifiedPropertiesWithoutUndo();
                report.Add("Walk: feet planted (model y " + model.localPosition.y.ToString("F2") + "), animator speed " + animSpeed.ToString("F2")
                    + " (stride " + (strideSpeed * model.localScale.x).ToString("F1") + " m/s at this scale vs MoveSpeed " + moveSpeed.ToString("F1") + ").");

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                report.Add("Prefab: " + PrefabPath + " (Enemy_Boss_Level2 with the model swapped; height " + targetHeight.ToString("F2")
                    + " m, model scale " + model.localScale.x.ToString("F2") + ", attack hold " + attackLength.ToString("F2") + " s).");
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The Mixamo walk moves the hips forward (about one stride per loop) and the loop snaps them back
        /// - with the movement already driven by EnemyMovement that reads as a fast, jerky walk. This writes a
        /// project-owned copy with the hips' forward/sideways drift removed (vertical bob kept), and reports the
        /// stride speed in model units per second.</summary>
        private static AnimationClip MakeInPlace(AnimationClip source, out float strideSpeed, List<string> report)
        {
            string path = ModelFolder + "/Walking_InPlace.anim";
            var clip = new AnimationClip { name = "Walking_InPlace", frameRate = source.frameRate };
            strideSpeed = 0f;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                bool hips = binding.path.EndsWith("Hips") && binding.type == typeof(Transform);
                if (hips && (binding.propertyName == "m_LocalPosition.z" || binding.propertyName == "m_LocalPosition.x") && curve.length > 1)
                {
                    float start = curve.keys[0].value;
                    float drift = curve.keys[curve.length - 1].value - start;
                    float length = Mathf.Max(0.0001f, curve.keys[curve.length - 1].time);
                    if (binding.propertyName == "m_LocalPosition.z")
                    {
                        strideSpeed = Mathf.Abs(drift) / length;
                    }

                    Keyframe[] keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        keys[k].value -= drift * (keys[k].time / length); // remove the linear travel, keep the sway
                        keys[k].inTangent -= drift / length;
                        keys[k].outTangent -= drift / length;
                    }

                    curve.keys = keys;
                }

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                existing.name = "Walking_InPlace";
                EditorUtility.SetDirty(existing);
                clip = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(clip, path);
            }

            report.Add("Walk clip: " + path + " (hips drift removed - the source moved " + (strideSpeed * source.length).ToString("F2")
                + " model units per loop and snapped back).");
            return clip;
        }

        private static float LowestPointOverClip(Transform model, AnimationClip clip)
        {
            var mesh = new Mesh();
            float lowest = float.MaxValue;
            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i <= 8; i++)
            {
                clip.SampleAnimation(model.gameObject, clip.length * i / 8f);
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    skin.BakeMesh(mesh, true);
                    Vector3[] vertices = mesh.vertices;
                    for (int v = 0; v < vertices.Length; v += 3)
                    {
                        lowest = Mathf.Min(lowest, skin.transform.TransformPoint(vertices[v]).y);
                    }
                }
            }

            Object.DestroyImmediate(mesh);
            return lowest;
        }

        private static Bounds Bounds(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(model.position, Vector3.zero);
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Object EnsureDefinition(GameObject prefab, List<string> report)
        {
            if (AssetDatabase.LoadMainAssetAtPath(DefinitionPath) == null)
            {
                AssetDatabase.CopyAsset(SourceDefinitionPath, DefinitionPath);
            }

            Object definition = AssetDatabase.LoadMainAssetAtPath(DefinitionPath);
            var so = new SerializedObject(definition);
            so.FindProperty("_id").stringValue = "enemy_boss_level3";
            so.FindProperty("_displayName").stringValue = "Shark Warlord";
            so.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<EnemyController>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            report.Add("EnemyDefinition: " + DefinitionPath + " (id enemy_boss_level3; stats copied unchanged from the Level 2 boss: "
                + so.FindProperty("_maxHealth").floatValue + " HP, base damage " + so.FindProperty("_baseDamage").intValue + ").");
            return definition;
        }

        private static Object EnsureEncounter(Object definition, List<string> report)
        {
            EnsureFolder("Assets/_Game/Data/Waves/Level03");
            if (AssetDatabase.LoadMainAssetAtPath(EncounterPath) == null)
            {
                AssetDatabase.CopyAsset(SourceEncounterPath, EncounterPath);
            }

            Object encounter = AssetDatabase.LoadMainAssetAtPath(EncounterPath);
            var so = new SerializedObject(encounter);
            so.FindProperty("_bossDefinition").objectReferenceValue = definition;
            so.FindProperty("_delayAfterNormalWaves").floatValue = 0.75f; // Level 3 has normal waves (Level 1's value)
            so.FindProperty("_bossCountdown").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);
            report.Add("BossEncounter: " + EncounterPath + " (boss only, no escorts, appears 0.75 s after the last wave is cleared).");
            return encounter;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
