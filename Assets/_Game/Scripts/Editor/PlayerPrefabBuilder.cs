using AlienDefense.Data;
using AlienDefense.Input;
using AlienDefense.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the UFO_Player prefab: placeholder art, movement, and the continuous
    /// multi-enemy Tractor Beam (Controller + Visual under TractorBeamRoot, a sibling of VisualRoot).</summary>
    internal static class PlayerPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/Player";
        private const string PrefabPath = PrefabFolder + "/UFO_Player.prefab";

        private const string PlayerDataFolder = "Assets/_Game/Data/Player";
        private const string PlayerDefinitionPath = PlayerDataFolder + "/PlayerDefinition.asset";
        private const string TractorBeamDefinitionPath = PlayerDataFolder + "/UFO_TractorBeam.asset";

        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerLayerName = "Player";

        private const string CameraFollowTargetName = "CameraFollowTarget";
        private static readonly Vector3 CameraFollowTargetLocalPosition = new Vector3(0f, 0.4f, 0f);

        public static PlayerDefinition CreateOrLoadPlayerDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerDefinition>(PlayerDefinitionPath);
            if (existing != null)
            {
                return existing;
            }

            EditorFolderUtility.EnsureFolder(PlayerDataFolder);
            var definition = ScriptableObject.CreateInstance<PlayerDefinition>();
            AssetDatabase.CreateAsset(definition, PlayerDefinitionPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AlienDefense Setup] Created " + PlayerDefinitionPath + ".");
            return definition;
        }

        public static UFOTractorBeamDefinition CreateOrLoadTractorBeamDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UFOTractorBeamDefinition>(TractorBeamDefinitionPath);
            if (existing != null)
            {
                return existing;
            }

            EditorFolderUtility.EnsureFolder(PlayerDataFolder);
            var definition = ScriptableObject.CreateInstance<UFOTractorBeamDefinition>();
            AssetDatabase.CreateAsset(definition, TractorBeamDefinitionPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AlienDefense Setup] Created " + TractorBeamDefinitionPath + ".");
            return definition;
        }

        /// <summary>Standalone entry point: migrates/builds only the UFO prefab (strip PlayerAutoAttack, add the
        /// Tractor Beam) without touching Level_01 or any other scene. Safe to run on its own at any time.</summary>
        [MenuItem("AlienDefense/Setup/10. Create Player Definition And Prefab")]
        public static void CreatePlayerDefinitionAndPrefab()
        {
            CreateOrLoadPrefab();
            Debug.Log("[AlienDefense Setup] UFO_Player prefab and PlayerDefinition/UFO_TractorBeam assets ready.");
        }

        public static GameObject CreateOrLoadPrefab()
        {
            UFOTractorBeamDefinition tractorBeamDefinition = CreateOrLoadTractorBeamDefinition();

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                MigrateAddCameraFollowTargetIfMissing(existing);
                MigrateRemovePlayerAutoAttack(existing);
                MigrateAddTractorBeamIfMissing(existing, tractorBeamDefinition);
                MigrateUpgradeTractorBeamVisualLayers(existing);
                MigrateRemoveGroundGlow(existing);
                MigrateRemoveGroundRing(existing);
                MigrateRepairBrokenShadowMaterial(existing);
                return existing;
            }

            PlayerDefinition definition = CreateOrLoadPlayerDefinition();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                Debug.LogError("[AlienDefense Setup] Could not find " + InputActionsPath + ". UFO_Player will have no input source.");
            }

            EditorFolderUtility.EnsureFolder(PrefabFolder);

            GameObject root = BuildHierarchy(definition, actions, tractorBeamDefinition);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log("[AlienDefense Setup] Created " + PrefabPath + ".");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>Adds CameraFollowTarget to an existing prefab that predates it.</summary>
        private static void MigrateAddCameraFollowTargetIfMissing(GameObject prefabAsset)
        {
            if (prefabAsset.transform.Find(CameraFollowTargetName) != null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents.transform.Find(CameraFollowTargetName) == null)
            {
                BuildEmptyChild(CameraFollowTargetName, contents.transform, CameraFollowTargetLocalPosition);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[AlienDefense Setup] Migrated " + path + ": added missing CameraFollowTarget child.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>The UFO no longer shoots: strips the now-scriptless PlayerAutoAttack component (its .cs file is
        /// deleted, so it only exists as a "Missing Script" slot on old prefabs) and its unused FirePoint child.</summary>
        private static void MigrateRemovePlayerAutoAttack(GameObject prefabAsset)
        {
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
            if (removedCount > 0)
            {
                changed = true;
            }

            Transform firePoint = contents.transform.Find("FirePoint");
            if (firePoint != null)
            {
                Object.DestroyImmediate(firePoint.gameObject);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[AlienDefense Setup] Migrated " + path + $": removed {removedCount} missing-script component(s) (old PlayerAutoAttack) and FirePoint (UFO no longer shoots).");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Adds TractorBeamRoot (Controller + Visual + anchors) to a prefab built before the Tractor Beam existed.</summary>
        private static void MigrateAddTractorBeamIfMissing(GameObject prefabAsset, UFOTractorBeamDefinition tractorBeamDefinition)
        {
            if (prefabAsset.GetComponentInChildren<UFOTractorBeamController>(true) != null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents.GetComponentInChildren<UFOTractorBeamController>(true) == null)
            {
                PlayerDefinition definition = AssetDatabase.LoadAssetAtPath<PlayerDefinition>(PlayerDefinitionPath);
                BuildTractorBeamRoot(contents.transform, definition, tractorBeamDefinition);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[AlienDefense Setup] Migrated " + path + ": added TractorBeamRoot (UFOTractorBeamController + Visual).");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Rebuilds the Beam's visual layers (Outer/Inner cone, Ground Glow/Ring, Top Glow) on a prefab
        /// that still has the earlier single-cylinder placeholder Beam visual. Detected by the absence of
        /// "BeamConeOuter", which only exists on the current layer set. Re-wires UFOTractorBeamVisual afterward;
        /// UFOTractorBeamController/its anchors are untouched.</summary>
        private static void MigrateUpgradeTractorBeamVisualLayers(GameObject prefabAsset)
        {
            Transform existingBeamRoot = prefabAsset.transform.Find("TractorBeamRoot");
            if (existingBeamRoot == null || existingBeamRoot.Find("BeamConeOuter") != null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            Transform beamRoot = contents.transform.Find("TractorBeamRoot");
            var beamVisual = beamRoot != null ? beamRoot.GetComponent<UFOTractorBeamVisual>() : null;
            if (beamRoot == null || beamVisual == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            foreach (string legacyName in new[] { "BeamAreaVisual", "BeamConeVisual" })
            {
                Transform legacy = beamRoot.Find(legacyName);
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy.gameObject);
                }
            }

            ParticleSystem legacyParticles = beamRoot.Find("BeamParticles")?.GetComponent<ParticleSystem>();
            if (legacyParticles != null)
            {
                Object.DestroyImmediate(legacyParticles.gameObject);
            }

            Transform captureSocket = beamRoot.Find("CaptureSocket");
            Transform captureFlashPoint = beamRoot.Find("CaptureFlashPoint");
            PlayerDefinition definition = AssetDatabase.LoadAssetAtPath<PlayerDefinition>(PlayerDefinitionPath);
            UFOTractorBeamDefinition tractorBeamDefinition = AssetDatabase.LoadAssetAtPath<UFOTractorBeamDefinition>(TractorBeamDefinitionPath);
            float hoverHeight = definition != null ? definition.HoverHeight : 1.5f;
            float attractionRadius = tractorBeamDefinition != null ? tractorBeamDefinition.AttractionRadius : 3.5f;

            Transform existingFlash = captureFlashPoint != null ? captureFlashPoint.Find("CaptureFlashParticles") : null;
            if (existingFlash != null)
            {
                Object.DestroyImmediate(existingFlash.gameObject);
            }

            BuildBeamVisualLayers(beamRoot, captureSocket, captureFlashPoint, beamVisual, hoverHeight, attractionRadius);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            Debug.Log("[AlienDefense Setup] Migrated " + path + ": upgraded Tractor Beam to the layered VFX (Outer/Inner cone, Ground Glow/Ring, Top Glow).");

            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>GroundGlow (the large filled disc covering the whole attraction radius) read as an oversized,
        /// near-opaque circle dominating the screen. Removed per feedback.</summary>
        private static void MigrateRemoveGroundGlow(GameObject prefabAsset)
        {
            MigrateRemoveBeamLayer(prefabAsset, "GroundGlow", "_groundGlow");
        }

        /// <summary>GroundRing was kept as a dimmer fallback after GroundGlow's removal, but per feedback the
        /// ground-level ring should go too — only the cone (Outer/Inner) should remain visible.</summary>
        private static void MigrateRemoveGroundRing(GameObject prefabAsset)
        {
            MigrateRemoveBeamLayer(prefabAsset, "GroundRing", "_groundRing");
        }

        private static void MigrateRemoveBeamLayer(GameObject prefabAsset, string childName, string visualFieldName)
        {
            Transform layer = prefabAsset.transform.Find("TractorBeamRoot/" + childName);
            if (layer == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            Transform contentsLayer = contents.transform.Find("TractorBeamRoot/" + childName);
            if (contentsLayer != null)
            {
                Object.DestroyImmediate(contentsLayer.gameObject);

                var beamVisual = contents.transform.Find("TractorBeamRoot")?.GetComponent<UFOTractorBeamVisual>();
                if (beamVisual != null)
                {
                    var serialized = new SerializedObject(beamVisual);
                    serialized.FindProperty(visualFieldName).objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[AlienDefense Setup] Migrated " + path + ": removed " + childName + ".");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static GameObject BuildHierarchy(PlayerDefinition definition, InputActionAsset actions, UFOTractorBeamDefinition tractorBeamDefinition)
        {
            var root = new GameObject("UFO_Player");
            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            if (playerLayer >= 0)
            {
                root.layer = playerLayer;
            }

            var characterController = root.AddComponent<CharacterController>();
            characterController.radius = 0.6f;
            characterController.height = 0.8f;
            characterController.center = new Vector3(0f, 0.3f, 0f);

            var inputReader = root.AddComponent<UnityInputReader>();
            var movement = root.AddComponent<PlayerMovement>();
            var controller = root.AddComponent<PlayerController>();

            GameObject model = BuildModel(root.transform);
            BuildEmptyChild("CollectionPoint", root.transform, Vector3.zero);
            BuildEmptyChild("GroundIndicator", root.transform, Vector3.zero);
            BuildEmptyChild(CameraFollowTargetName, root.transform, CameraFollowTargetLocalPosition);
            BuildShadow(root.transform, definition);
            GameObject vfx = BuildEmptyChild("VFX", root.transform, Vector3.zero);
            BuildEmptyChild("HoverEffect", vfx.transform, Vector3.zero);

            BuildTractorBeamRoot(root.transform, definition, tractorBeamDefinition);

            var hoverVisual = model.AddComponent<UFOHoverVisual>();
            var hoverVisualSerialized = new SerializedObject(hoverVisual);
            hoverVisualSerialized.FindProperty("_movementDirectionSource").objectReferenceValue = controller;
            hoverVisualSerialized.ApplyModifiedPropertiesWithoutUndo();

            var inputReaderSerialized = new SerializedObject(inputReader);
            inputReaderSerialized.FindProperty("_actions").objectReferenceValue = actions;
            inputReaderSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_definition").objectReferenceValue = definition;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.FindProperty("_inputSource").objectReferenceValue = inputReader;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Builds TractorBeamRoot as a sibling of VisualRoot/Model (never nested under it, so the beam's
        /// gameplay anchors never bob/tilt/spin with the hover model) with BeamGroundAnchor, CaptureSocket, and the
        /// visual pieces UFOTractorBeamVisual reacts to.</summary>
        private static void BuildTractorBeamRoot(Transform parent, PlayerDefinition definition, UFOTractorBeamDefinition tractorBeamDefinition)
        {
            float hoverHeight = definition != null ? definition.HoverHeight : 1.5f;
            float attractionRadius = tractorBeamDefinition != null ? tractorBeamDefinition.AttractionRadius : 3.5f;

            var beamRoot = BuildEmptyChild("TractorBeamRoot", parent, Vector3.zero);

            Transform beamGroundAnchor = BuildEmptyChild("BeamGroundAnchor", beamRoot.transform, new Vector3(0f, -hoverHeight, 0f)).transform;
            Transform captureSocket = BuildEmptyChild("CaptureSocket", beamRoot.transform, new Vector3(0f, 0.15f, 0f)).transform;
            Transform captureFlashPoint = BuildEmptyChild("CaptureFlashPoint", beamRoot.transform, new Vector3(0f, 0.15f, 0f)).transform;

            var beamController = beamRoot.AddComponent<UFOTractorBeamController>();
            var beamControllerSerialized = new SerializedObject(beamController);
            beamControllerSerialized.FindProperty("_definition").objectReferenceValue = tractorBeamDefinition;
            beamControllerSerialized.FindProperty("_beamGroundAnchor").objectReferenceValue = beamGroundAnchor;
            beamControllerSerialized.FindProperty("_captureSocket").objectReferenceValue = captureSocket;
            beamControllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var beamVisual = beamRoot.AddComponent<UFOTractorBeamVisual>();
            BuildBeamVisualLayers(beamRoot.transform, captureSocket, captureFlashPoint, beamVisual, hoverHeight, attractionRadius);
        }

        /// <summary>Builds (or, on migration, rebuilds) the 7 VFX layers and wires them onto an existing
        /// UFOTractorBeamVisual. Shared by fresh prefab creation and the visual-upgrade migration.</summary>
        private static void BuildBeamVisualLayers(Transform beamRoot, Transform captureSocket, Transform captureFlashPoint, UFOTractorBeamVisual beamVisual, float hoverHeight, float attractionRadius)
        {
            MeshRenderer beamConeOuter = BuildConeLayer(beamRoot, "BeamConeOuter", hoverHeight, attractionRadius, TractorBeamMaterialBuilder.CreateOrLoadOuter());
            MeshRenderer beamConeInner = BuildConeLayer(beamRoot, "BeamConeInner", hoverHeight, attractionRadius * 0.6f, TractorBeamMaterialBuilder.CreateOrLoadInner());
            // GroundGlow and GroundRing were removed per feedback: only the cone (Outer/Inner) should mark the beam.
            GameObject beamTopGlow = BuildTopGlow(captureSocket, TractorBeamMaterialBuilder.CreateOrLoadGlow());
            ParticleSystem beamParticles = BuildBeamParticles(beamRoot, attractionRadius, hoverHeight);
            ParticleSystem captureFlashParticles = BuildCaptureFlashParticles(captureFlashPoint);

            var beamVisualSerialized = new SerializedObject(beamVisual);
            beamVisualSerialized.FindProperty("_beamConeOuter").objectReferenceValue = beamConeOuter;
            beamVisualSerialized.FindProperty("_beamConeInner").objectReferenceValue = beamConeInner;
            beamVisualSerialized.FindProperty("_groundGlow").objectReferenceValue = null;
            beamVisualSerialized.FindProperty("_groundRing").objectReferenceValue = null;
            beamVisualSerialized.FindProperty("_beamTopGlow").objectReferenceValue = beamTopGlow;
            beamVisualSerialized.FindProperty("_beamParticles").objectReferenceValue = beamParticles;
            beamVisualSerialized.FindProperty("_captureFlashParticles").objectReferenceValue = captureFlashParticles;
            beamVisualSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>One truncated-cone layer (Outer or Inner). Positioned once at ground level; UFOTractorBeamVisual
        /// only ever changes localScale afterward (height via Y, radius via X/Z) — the mesh itself is never touched.</summary>
        private static MeshRenderer BuildConeLayer(Transform parent, string name, float hoverHeight, float bottomRadius, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -hoverHeight, 0f);
            go.transform.localScale = new Vector3(bottomRadius, hoverHeight, bottomRadius);

            go.GetComponent<MeshFilter>().sharedMesh = TractorBeamMeshGenerator.CreateOrLoadConeMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return renderer;
        }

        /// <summary>Small bright disc right under the UFO body, billboard-free (top-down camera never sees it edge-on).</summary>
        private static GameObject BuildTopGlow(Transform captureSocket, Material material)
        {
            var go = new GameObject("BeamTopGlow", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(captureSocket, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(0.7f, 1f, 0.7f);

            go.GetComponent<MeshFilter>().sharedMesh = TractorBeamMeshGenerator.CreateOrLoadDiscMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go;
        }

        /// <summary>Small cyan/white particles rising from the ground area toward the UFO, converging toward center
        /// as they climb — a wide Cone shape emitting upward gives that "pulled toward center" read cheaply,
        /// without a custom simulation.</summary>
        private static ParticleSystem BuildBeamParticles(Transform parent, float attractionRadius, float hoverHeight)
        {
            var particleObject = new GameObject("BeamParticles");
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, -hoverHeight, 0f);

            var particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.startColor = new Color(0.4f, 0.85f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 100;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 3f; // idle rate; UFOTractorBeamVisual scales this up while capturing

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = Mathf.Max(0.1f, attractionRadius * 0.9f);
            shape.rotation = new Vector3(180f, 0f, 0f); // cone points up, toward the UFO

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.3f, 0.7f, 1f), 0f),
                    new GradientColorKey(new Color(0.85f, 0.98f, 1f), 0.6f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.6f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = TractorBeamMaterialBuilder.CreateOrLoadGlow();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return particles;
        }

        private static ParticleSystem BuildCaptureFlashParticles(Transform parent)
        {
            var particleObject = new GameObject("CaptureFlashParticles");
            particleObject.transform.SetParent(parent, false);

            var particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = Color.white;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = TractorBeamMaterialBuilder.CreateOrLoadGlow();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return particles;
        }

        private static GameObject BuildModel(Transform parent)
        {
            var model = new GameObject("Model");
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(model.transform, false);
            disc.transform.localScale = new Vector3(1.1f, 0.15f, 1.1f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());

            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Dome";
            dome.transform.SetParent(model.transform, false);
            dome.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            dome.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
            Object.DestroyImmediate(dome.GetComponent<Collider>());

            return model;
        }

        private static void BuildShadow(Transform parent, PlayerDefinition definition)
        {
            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "Shadow";
            shadow.transform.SetParent(parent, false);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            float hoverHeight = definition != null ? definition.HoverHeight : 1.5f;
            shadow.transform.localPosition = new Vector3(0f, -hoverHeight + 0.02f, 0f);
            Object.DestroyImmediate(shadow.GetComponent<Collider>());

            var renderer = shadow.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_PlayerShadow", "Universal Render Pipeline/Unlit", new Color(0.05f, 0.05f, 0.05f, 1f));
        }

        /// <summary>Re-links an existing prefab's Shadow renderer to the persistent shadow material if it points elsewhere or nowhere.</summary>
        private static void MigrateRepairBrokenShadowMaterial(GameObject prefabAsset)
        {
            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_PlayerShadow", "Universal Render Pipeline/Unlit", new Color(0.05f, 0.05f, 0.05f, 1f));

            Transform shadowTransform = prefabAsset.transform.Find("Shadow");
            Renderer existingRenderer = shadowTransform != null ? shadowTransform.GetComponent<MeshRenderer>() : null;
            if (existingRenderer == null || existingRenderer.sharedMaterial == material)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            Renderer renderer = contents.transform.Find("Shadow")?.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[AlienDefense Setup] Migrated " + path + ": re-linked Shadow material to the persistent asset.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static GameObject BuildEmptyChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }
    }
}
