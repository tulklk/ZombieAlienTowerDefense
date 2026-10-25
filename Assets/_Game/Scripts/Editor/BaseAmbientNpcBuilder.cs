using System.Collections.Generic;
using AlienDefense.Base.Ambient;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the BaseWorld's ambient alien residents (decoration only) and re-runs safely:
    ///
    /// 1. Import settings of the Mixamo-rigged alien FBXs (Generic rig with its own avatar, root node mixamorig:Hips,
    ///    loop flags). Only the .meta changes. The walk's forward travel still sits in the hips on these rigs, so the
    ///    prefabs carry AmbientInPlaceAnimation, which pins it - every clip plays in place.
    /// 2. Project-owned URP materials that use the textures shipped next to each model (the FBX materials have none).
    /// 3. Animator controllers (a shared walk controller + an override for Alien 2, the worker, Alien 4's
    ///    BreathingIdle -> StartWalking -> Walking -> BreathingIdle machine).
    /// 4. Prefabs Alien1..6 (root = the NPC script that moves, VisualRoot = scale, model = the FBX). Aliens 1, 2, 5 and
    ///    6 walk the roads (5 and 6 added 2026-10-25), 3 works at the research center, 4 lives in the central building.
    /// 5. MainMenu scene: the road-grid waypoint network, NpcAnchors under the two plot roots (never under the
    ///    building model BaseBuildingView swaps on upgrade), and BaseWorld/AmbientNPCs wired to them.
    ///
    /// No NavMesh in BaseWorld, so walkers follow linked waypoints laid on the road centre lines.</summary>
    public static class BaseAmbientNpcBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/Menu/MainMenu.unity";
        private const string ModelDir = "Assets/_Game/Models/Alien/";
        private const string ArtDir = "Assets/_Game/Art/Base/AmbientNpc";
        private const string PrefabDir = "Assets/_Game/Prefabs/Base/AmbientNpc";
        private const string HipsName = "mixamorig:Hips";
        private const int BaseWorldLayer = 17;

        /// <summary>Alien model height is ~1.15 m at scale 1; buildings are ~9 m and street lights ~5 m, so the
        /// residents are shown at twice that (~2.3 m): the central building's door is ~2.35 m tall and Alien 4 walks
        /// through it. The base camera zooms in (BaseCameraController) to see them closer.</summary>
        private const float VisualScale = 2f;

        private const float RoadTop = 0.15f;

        private sealed class AlienAssets
        {
            public string Folder;
            public string TexturePrefix;
            public string[] Clips; // FBX file names (without .fbx) holding the clips
            public string ModelFile; // the FBX whose skinned mesh + skeleton is shown
        }

        private static readonly AlienAssets[] Aliens =
        {
            new AlienAssets { Folder = "1/alien1", Clips = new[] { "Walking" }, ModelFile = "Walking" },
            new AlienAssets { Folder = "2/alien2", Clips = new[] { "Walking" }, ModelFile = "Walking" },
            new AlienAssets { Folder = "3/alien3", Clips = new[] { "Working" }, ModelFile = "Working" },
            new AlienAssets { Folder = "4/alien4", Clips = new[] { "Breathing Idle", "Start Walking", "Walking" }, ModelFile = "Breathing Idle" },
            new AlienAssets { Folder = "5/alien5", Clips = new[] { "Brutal To Happy Walking" }, ModelFile = "Brutal To Happy Walking" },
            new AlienAssets { Folder = "6/alien6", Clips = new[] { "Walking" }, ModelFile = "Walking" },
        };

        [MenuItem("AlienDefense/Setup/32. Build Base Ambient NPCs")]
        public static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
            {
                return "[BaseAmbientNpcBuilder] Stop Play mode first.";
            }

            var report = new System.Text.StringBuilder("[BaseAmbientNpcBuilder] ");
            EnsureFolder(ArtDir + "/Materials");
            EnsureFolder(ArtDir + "/Animators");
            EnsureFolder(PrefabDir);

            // 1. Import settings.
            ConfigureClip(Aliens[0], "Walking", true);
            ConfigureClip(Aliens[1], "Walking", true);
            ConfigureClip(Aliens[2], "Working", true);
            ConfigureClip(Aliens[3], "Breathing Idle", true);
            ConfigureClip(Aliens[3], "Start Walking", false);
            ConfigureClip(Aliens[3], "Walking", true);
            ConfigureClip(Aliens[4], "Brutal To Happy Walking", true);
            ConfigureClip(Aliens[5], "Walking", true);

            // 2. Materials, 3. controllers.
            var materials = new Material[Aliens.Length];
            for (int i = 0; i < Aliens.Length; i++)
            {
                materials[i] = BuildMaterial(i);
            }

            AnimationClip walk1 = LoadClip(Aliens[0], "Walking");
            AnimationClip walk2 = LoadClip(Aliens[1], "Walking");
            AnimationClip work3 = LoadClip(Aliens[2], "Working");
            AnimationClip idle4 = LoadClip(Aliens[3], "Breathing Idle");
            AnimationClip start4 = LoadClip(Aliens[3], "Start Walking");
            AnimationClip walk4 = LoadClip(Aliens[3], "Walking");
            AnimationClip walk5 = LoadClip(Aliens[4], "Brutal To Happy Walking");
            AnimationClip walk6 = LoadClip(Aliens[5], "Walking");

            AnimatorController walkController = BuildSingleStateController("AlienWalk", "Walking", walk1);
            RuntimeAnimatorController walk2Controller = BuildOverride("Alien2Walk", walkController, walk1, walk2);
            RuntimeAnimatorController walk5Controller = BuildOverride("Alien5Walk", walkController, walk1, walk5);
            RuntimeAnimatorController walk6Controller = BuildOverride("Alien6Walk", walkController, walk1, walk6);
            AnimatorController workController = BuildSingleStateController("AlienWorker", "Working", work3);
            AnimatorController strollController = BuildStrollController(idle4, start4, walk4);

            // 4. Prefabs. Walking speed = the clip's own stride at this scale, so the feet do not slide.
            float speed1 = StrideSpeed(walk1);
            float speed2 = StrideSpeed(walk2);
            float speed4 = StrideSpeed(walk4);
            float speed5 = StrideSpeed(walk5);
            float speed6 = StrideSpeed(walk6);
            GameObject alien1 = BuildPrefab("Alien1", Aliens[0], walkController, materials[0], typeof(AlienPatrolNpc));
            GameObject alien2 = BuildPrefab("Alien2", Aliens[1], walk2Controller, materials[1], typeof(AlienPatrolNpc));
            GameObject alien3 = BuildPrefab("Alien3", Aliens[2], workController, materials[2], typeof(AlienWorkerNpc));
            GameObject alien4 = BuildPrefab("Alien4", Aliens[3], strollController, materials[3], typeof(AlienStrollNpc));
            GameObject alien5 = BuildPrefab("Alien5", Aliens[4], walk5Controller, materials[4], typeof(AlienPatrolNpc));
            GameObject alien6 = BuildPrefab("Alien6", Aliens[5], walk6Controller, materials[5], typeof(AlienPatrolNpc));
            report.Append($"Walk speeds (m/s at scale {VisualScale}): Alien1 {speed1:0.00}, Alien2 {speed2:0.00}, Alien4 {speed4:0.00}, Alien5 {speed5:0.00}, Alien6 {speed6:0.00}. ");

            // 5. Scene.
            report.Append(BuildScene(new[] { alien1, alien2, alien5, alien6 }, new[] { speed1, speed2, speed5, speed6 }, alien3, alien4, speed4, start4 != null ? start4.length : 2.9f));
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // ------------------------------------------------------------------ import

        private static string ModelPath(AlienAssets alien, string file)
        {
            return ModelDir + alien.Folder + "/" + file + ".fbx";
        }

        /// <summary>Generic rig, avatar from the model, mixamorig:Hips as root node, rotation and height baked into the
        /// pose, loop flag per clip. (The Animator still leaves the forward travel in the hips here - measured -
        /// which AmbientInPlaceAnimation removes.) Re-imports only when something differs.</summary>
        private static void ConfigureClip(AlienAssets alien, string file, bool loop)
        {
            string path = ModelPath(alien, file);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[BaseAmbientNpcBuilder] Missing " + path);
                return;
            }

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (importer.motionNodeName != HipsName)
            {
                importer.motionNodeName = HipsName;
                changed = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
                changed = true;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                if (clip.loopTime != loop || !clip.lockRootRotation || !clip.keepOriginalOrientation || !clip.lockRootHeightY
                    || !clip.keepOriginalPositionY || clip.lockRootPositionXZ || !clip.keepOriginalPositionXZ)
                {
                    clip.loopTime = loop;
                    clip.lockRootRotation = true;      // bake rotation into the pose: no drifting heading
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;       // bake the bob into the pose
                    clip.keepOriginalPositionY = true;
                    clip.lockRootPositionXZ = false;   // forward travel: pinned at runtime by AmbientInPlaceAnimation
                    clip.keepOriginalPositionXZ = true;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static AnimationClip LoadClip(AlienAssets alien, string file)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath(alien, file)))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                {
                    return clip;
                }
            }

            Debug.LogWarning("[BaseAmbientNpcBuilder] No clip in " + ModelPath(alien, file));
            return null;
        }

        /// <summary>Forward travel of the hips over one cycle, per second, at VisualScale.</summary>
        private static float StrideSpeed(AnimationClip clip)
        {
            if (clip == null || clip.length <= 0f)
            {
                return 1.2f;
            }

            // Raw hips curves, or - once the importer extracts them as root motion - the motion curves.
            float hipsTravel = 0f, motionTravel = 0f;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                bool hips = binding.path == HipsName && (binding.propertyName == "m_LocalPosition.z" || binding.propertyName == "m_LocalPosition.x");
                bool motion = binding.path.Length == 0 && (binding.propertyName == "MotionT.z" || binding.propertyName == "MotionT.x"
                    || binding.propertyName == "RootT.z" || binding.propertyName == "RootT.x");
                if (!hips && !motion)
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                float delta = curve.keys[curve.length - 1].value - curve.keys[0].value;
                if (hips)
                {
                    hipsTravel += delta * delta;
                }
                else
                {
                    motionTravel += delta * delta;
                }
            }

            float travel = Mathf.Sqrt(hipsTravel > 0.0001f ? hipsTravel : motionTravel);
            return travel > 0.01f ? travel * VisualScale / clip.length : 1.2f;
        }

        // ------------------------------------------------------------------ materials

        private static Material BuildMaterial(int index)
        {
            AlienAssets alien = Aliens[index];
            string folder = ModelDir + alien.Folder;
            Texture2D baseMap = null, normalMap = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("_BaseColor.png"))
                {
                    baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                else if (path.EndsWith("_Normal_Bake.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }

                    normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            string materialPath = $"{ArtDir}/Materials/MAT_Alien{index + 1}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.3f);
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ animator controllers

        private static AnimatorController LoadOrCreateController(string name)
        {
            string path = $"{ArtDir}/Animators/{name}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            // Rebuild the contents in place (keeps the GUID every prefab points at).
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states)
            {
                machine.RemoveState(child.state);
            }

            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                machine.RemoveAnyStateTransition(transition);
            }

            while (controller.parameters.Length > 0)
            {
                controller.RemoveParameter(0);
            }

            return controller;
        }

        private static AnimatorController BuildSingleStateController(string name, string stateName, AnimationClip clip)
        {
            AnimatorController controller = LoadOrCreateController(name);
            AnimatorState state = controller.layers[0].stateMachine.AddState(stateName);
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static RuntimeAnimatorController BuildOverride(string name, AnimatorController source, AnimationClip from, AnimationClip to)
        {
            string path = $"{ArtDir}/Animators/{name}.overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(source);
                AssetDatabase.CreateAsset(controller, path);
            }

            controller.runtimeAnimatorController = source;
            controller[from] = to;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>BreathingIdle (default) -(StartMove)-> StartWalking -(exit 0.92)-> Walking -(IsWalking false)->
        /// BreathingIdle. No Any State transitions.</summary>
        private static AnimatorController BuildStrollController(AnimationClip idle, AnimationClip start, AnimationClip walk)
        {
            AnimatorController controller = LoadOrCreateController("Alien4Ambient");
            controller.AddParameter("StartMove", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("BreathingIdle", new Vector3(260f, 0f));
            idleState.motion = idle;
            AnimatorState startState = machine.AddState("StartWalking", new Vector3(520f, 100f));
            startState.motion = start;
            AnimatorState walkState = machine.AddState("Walking", new Vector3(260f, 200f));
            walkState.motion = walk;
            machine.defaultState = idleState;

            AnimatorStateTransition toStart = idleState.AddTransition(startState);
            toStart.hasExitTime = false;
            toStart.duration = 0.1f;
            toStart.hasFixedDuration = true;
            toStart.AddCondition(AnimatorConditionMode.If, 0f, "StartMove");

            AnimatorStateTransition toWalk = startState.AddTransition(walkState);
            toWalk.hasExitTime = true;
            toWalk.exitTime = 0.92f;
            toWalk.duration = 0.12f;
            toWalk.hasFixedDuration = true;

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.hasFixedDuration = true;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWalking");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>Alien_X (NPC script, moves) > VisualRoot (scale) > the FBX model (Animator + skinned mesh).</summary>
        private static GameObject BuildPrefab(string name, AlienAssets alien, RuntimeAnimatorController controller, Material material, System.Type npcType)
        {
            string modelPath = ModelPath(alien, alien.ModelFile);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is Avatar a)
                {
                    avatar = a;
                }
            }

            var root = new GameObject(name);
            var visual = new GameObject("VisualRoot");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * VisualScale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, visual.transform);
            model.name = "AlienModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // off-screen residents cost nothing
            animator.updateMode = AnimatorUpdateMode.Normal;
            Transform hips = model.transform.Find(HipsName);
            var inPlace = model.GetComponent<AmbientInPlaceAnimation>();
            if (inPlace == null)
            {
                inPlace = model.AddComponent<AmbientInPlaceAnimation>();
            }

            // The freshly instantiated model is in its rest pose: that is the forward offset the hips stay at.
            inPlace.Configure(hips, hips != null ? model.transform.InverseTransformPoint(hips.position).z : 0f);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.updateWhenOffscreen = false;
                    skinned.quality = SkinQuality.Bone2; // mobile: two bones per vertex is plenty at this size
                }
            }

            var npc = root.AddComponent(npcType);
            var so = new SerializedObject(npc);
            so.FindProperty("_animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetLayer(root.transform, BaseWorldLayer);
            string prefabPath = $"{PrefabDir}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ scene

        /// <param name="walkers">The road walkers, in order Alien1, Alien2, Alien5, Alien6; <paramref name="walkerSpeeds"/>
        /// matches.</param>
        private static string BuildScene(GameObject[] walkers, float[] walkerSpeeds, GameObject alien3, GameObject alien4,
            float speed4, float startWalkingLength)
        {
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
                return "No BaseWorld in MainMenu.";
            }

            Transform plots = world.transform.Find("Plots");
            Transform central = plots != null ? plots.Find("Plot_central_building") : null;
            Transform research = plots != null ? plots.Find("Plot_research_center") : null;
            if (central == null || research == null)
            {
                return "Plot_central_building / Plot_research_center missing.";
            }

            DestroyChild(world.transform, "AmbientNPCs");
            DestroyChild(world.transform, "AmbientNpcWaypoints");
            DestroyChild(central, "NpcAnchors");
            DestroyChild(research, "NpcAnchors");

            // Road grid: centre lines x = -48..48 (every 24), z = -24/0/24, with the four curved corners.
            Transform waypointRoot = NewChild(world.transform, "AmbientNpcWaypoints");
            Transform globalRoot = NewChild(waypointRoot, "Routes_Global");
            var grid = new Dictionary<Vector2Int, AmbientWaypoint>();
            var all = new List<AmbientWaypoint>();
            int[] xs = { -48, -24, 0, 24, 48 };
            int[] zs = { -24, 0, 24 };
            foreach (int z in zs)
            {
                foreach (int x in xs)
                {
                    if (Mathf.Abs(x) == 48 && Mathf.Abs(z) == 24)
                    {
                        continue; // corners are curves, laid below
                    }

                    grid[new Vector2Int(x, z)] = AddWaypoint(globalRoot, all, new Vector3(x, RoadTop, z));
                }
            }

            // Each curved corner: end of the straight, the middle of the arc, start of the side street.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AmbientWaypoint a = AddWaypoint(globalRoot, all, new Vector3(40.8f * sx, RoadTop, 24f * sz));
                    AmbientWaypoint b = AddWaypoint(globalRoot, all, new Vector3(45.9f * sx, RoadTop, 21.9f * sz));
                    AmbientWaypoint c = AddWaypoint(globalRoot, all, new Vector3(48f * sx, RoadTop, 16.8f * sz));
                    Link(a, b);
                    Link(b, c);
                    Link(grid[new Vector2Int(24 * sx, 24 * sz)], a);
                    Link(c, grid[new Vector2Int(48 * sx, 0)]);
                }
            }

            foreach (int z in zs)
            {
                for (int i = 0; i + 1 < xs.Length; i++)
                {
                    if (grid.TryGetValue(new Vector2Int(xs[i], z), out AmbientWaypoint left)
                        && grid.TryGetValue(new Vector2Int(xs[i + 1], z), out AmbientWaypoint right))
                    {
                        Link(left, right);
                    }
                }
            }

            foreach (int x in xs)
            {
                for (int i = 0; i + 1 < zs.Length; i++)
                {
                    if (grid.TryGetValue(new Vector2Int(x, zs[i]), out AmbientWaypoint near)
                        && grid.TryGetValue(new Vector2Int(x, zs[i + 1]), out AmbientWaypoint far))
                    {
                        Link(near, far);
                    }
                }
            }

            for (int i = 0; i < all.Count; i++)
            {
                all[i].name = "WP_" + i.ToString("00");
            }

            var globalNetwork = globalRoot.gameObject.AddComponent<AmbientWaypointNetwork>();
            globalNetwork.SetWaypoints(all.ToArray());

            // Alien 4: a ring round the central building's yard (building ~9.5 x 7.8 m at (11, 11), door to -z;
            // the well sits at (3.5, 5), street lights at (3.5, 20.5) and (20.5, 3.5)).
            Transform centralAnchors = NewChild(central, "NpcAnchors");
            // Ring order matters (AlienStrollNpc walks home the short way round): element 0 is the door step.
            Vector3[] ring =
            {
                new Vector3(11.1f, 0f, 5.6f), new Vector3(5.9f, 0f, 4.5f), new Vector3(4.6f, 0f, 11f), new Vector3(5.3f, 0f, 17.4f),
                new Vector3(11f, 0f, 18.2f), new Vector3(16.8f, 0f, 17.4f), new Vector3(17.6f, 0f, 11f), new Vector3(16.6f, 0f, 4.6f),
            };
            var ringPoints = new List<AmbientWaypoint>();
            for (int i = 0; i < ring.Length; i++)
            {
                var waypoint = AddWaypoint(centralAnchors, ringPoints, ring[i]);
                waypoint.name = i == 0 ? "Alien4_DoorStep" : "Alien4_WP_" + i.ToString("00");
            }

            for (int i = 0; i < ringPoints.Count; i++)
            {
                Link(ringPoints[i], ringPoints[(i + 1) % ringPoints.Count]);
            }

            var centralNetwork = centralAnchors.gameObject.AddComponent<AmbientWaypointNetwork>();
            centralNetwork.SetWaypoints(ringPoints.ToArray());

            // The way in, measured on BP_central_building: door centred at x 11.1; step 1 (0.64 m) at z 7.0-7.4,
            // step 2 (1.0 m) at z 7.4-7.8, porch floor ~1.05 m to z 8.8, door at z ~9. The last point is inside the
            // house, where the walker is hidden. Not linked into the ring: only the "go home" walk uses it.
            var homePoints = new List<AmbientWaypoint> { ringPoints[0] };
            Vector3[] way = { new Vector3(11.1f, 0f, 6.8f), new Vector3(11.1f, 0.64f, 7.2f), new Vector3(11.1f, 1.0f, 7.6f), new Vector3(11.1f, 1.05f, 8.4f), new Vector3(11.1f, 1.05f, 10.4f) };
            string[] wayNames = { "Alien4_StepBase", "Alien4_Step1", "Alien4_Step2", "Alien4_Porch", "Alien4_Inside" };
            var unused = new List<AmbientWaypoint>();
            for (int i = 0; i < way.Length; i++)
            {
                AmbientWaypoint waypoint = AddWaypoint(centralAnchors, unused, way[i]);
                waypoint.name = wayNames[i];
                homePoints.Add(waypoint);
            }

            // Alien 3: in the research plot's front yard, beside the door, turned to the building / construction.
            Transform researchAnchors = NewChild(research, "NpcAnchors");
            Transform workAnchor = NewChild(researchAnchors, "Alien3_WorkAnchor");
            workAnchor.position = new Vector3(-8.6f, 0f, 4.2f);
            Transform lookTarget = NewChild(researchAnchors, "Alien3_LookTarget");
            lookTarget.position = new Vector3(-11f, 1.2f, 8.5f);

            // Residents.
            Transform npcRoot = NewChild(world.transform, "AmbientNPCs");
            // Road walkers, each starting on a different street, each on its own lane offset and seed:
            // Alien 1 above the central building heading east, Alien 2 between research and patrol post heading east,
            // Alien 5 west of the patrol post heading north, Alien 6 between the workshop and the sawmill heading south.
            Vector2Int[,] starts =
            {
                { new Vector2Int(0, 24), new Vector2Int(24, 24) },
                { new Vector2Int(-24, 0), new Vector2Int(0, 0) },
                { new Vector2Int(-24, -24), new Vector2Int(-24, 0) },
                { new Vector2Int(24, 0), new Vector2Int(24, -24) },
            };
            float[] lanes = { 0.9f, 1.0f, 0.95f, 1.05f };
            var patrols = new AlienPatrolNpc[walkers.Length];
            for (int i = 0; i < walkers.Length; i++)
            {
                patrols[i] = ((GameObject)PrefabUtility.InstantiatePrefab(walkers[i], npcRoot)).GetComponent<AlienPatrolNpc>();
            }

            for (int i = 0; i < patrols.Length; i++)
            {
                AmbientWaypoint from = grid[starts[i, 0]], to = grid[starts[i, 1]];
                WirePatrol(patrols[i], globalNetwork, from, to, walkerSpeeds[i], lanes[i], patrols);
                patrols[i].transform.position = Vector3.Lerp(from.Position, to.Position, 0.5f);
            }

            var a3 = (GameObject)PrefabUtility.InstantiatePrefab(alien3, npcRoot);
            var a4 = (GameObject)PrefabUtility.InstantiatePrefab(alien4, npcRoot);

            var worker = new SerializedObject(a3.GetComponent<AlienWorkerNpc>());
            worker.FindProperty("_workAnchor").objectReferenceValue = workAnchor;
            worker.FindProperty("_lookTarget").objectReferenceValue = lookTarget;
            // Alien 3 only works there once the research center is built (user, 2026-10-25).
            worker.FindProperty("_requiredBuilding").objectReferenceValue = research.GetComponent<AlienDefense.Base.BaseBuildingView>();
            worker.FindProperty("_visual").objectReferenceValue = a3.transform.Find("VisualRoot").gameObject;
            worker.ApplyModifiedPropertiesWithoutUndo();
            a3.transform.position = workAnchor.position;

            // Alien 4 lives in the central building: out of the door, round the yard, back in, and again.
            var stroll = new SerializedObject(a4.GetComponent<AlienStrollNpc>());
            stroll.FindProperty("_localRoute").objectReferenceValue = centralNetwork;
            stroll.FindProperty("_spawn").objectReferenceValue = ringPoints[0];
            SerializedProperty home = stroll.FindProperty("_homePath");
            home.arraySize = homePoints.Count;
            for (int i = 0; i < homePoints.Count; i++)
            {
                home.GetArrayElementAtIndex(i).objectReferenceValue = homePoints[i];
            }

            Renderer[] renderers = a4.GetComponentsInChildren<Renderer>(true);
            SerializedProperty hideList = stroll.FindProperty("_renderers");
            hideList.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                hideList.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }

            stroll.FindProperty("_moveSpeed").floatValue = speed4;
            stroll.FindProperty("_startWalkingDuration").floatValue = startWalkingLength * 0.92f; // Walking takes over at exit time
            stroll.ApplyModifiedPropertiesWithoutUndo();
            a4.transform.position = homePoints[homePoints.Count - 1].Position;

            SetLayer(waypointRoot, BaseWorldLayer);
            SetLayer(centralAnchors, BaseWorldLayer);
            SetLayer(researchAnchors, BaseWorldLayer);
            SetLayer(npcRoot, BaseWorldLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Scene: {all.Count} road waypoints, {ringPoints.Count} central-yard waypoints + {homePoints.Count - 1} door waypoints, {patrols.Length + 2} residents.";
        }

        private static void WirePatrol(AlienPatrolNpc npc, AmbientWaypointNetwork network, AmbientWaypoint start, AmbientWaypoint first,
            float speed, float lane, AlienPatrolNpc[] all)
        {
            var so = new SerializedObject(npc);
            so.FindProperty("_network").objectReferenceValue = network;
            so.FindProperty("_startWaypoint").objectReferenceValue = start;
            so.FindProperty("_firstTarget").objectReferenceValue = first;
            so.FindProperty("_moveSpeed").floatValue = speed;
            so.FindProperty("_laneOffset").floatValue = lane;
            SerializedProperty keepClear = so.FindProperty("_keepClearOf");
            keepClear.arraySize = 0;
            foreach (AlienPatrolNpc other in all)
            {
                if (other != null && other != npc)
                {
                    keepClear.arraySize++;
                    keepClear.GetArrayElementAtIndex(keepClear.arraySize - 1).objectReferenceValue = other;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AmbientWaypoint AddWaypoint(Transform parent, List<AmbientWaypoint> list, Vector3 position)
        {
            var go = new GameObject("WP");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var waypoint = go.AddComponent<AmbientWaypoint>();
            list.Add(waypoint);
            return waypoint;
        }

        private static void Link(AmbientWaypoint a, AmbientWaypoint b)
        {
            a.AddNeighbor(b);
            b.AddNeighbor(a);
            EditorUtility.SetDirty(a);
            EditorUtility.SetDirty(b);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
        }

        private static void SetLayer(Transform root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
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
