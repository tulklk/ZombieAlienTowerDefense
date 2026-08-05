using AlienDefense.Data;
using AlienDefense.Input;
using AlienDefense.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the UFO_Player prefab with placeholder art and Phase 2 components.</summary>
    internal static class PlayerPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/Player";
        private const string PrefabPath = PrefabFolder + "/UFO_Player.prefab";

        private const string PlayerDataFolder = "Assets/_Game/Data/Player";
        private const string PlayerDefinitionPath = PlayerDataFolder + "/PlayerDefinition.asset";

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

        public static GameObject CreateOrLoadPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                MigrateAddCameraFollowTargetIfMissing(existing);
                MigrateAddPlayerAutoAttackIfMissing(existing);
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

            GameObject root = BuildHierarchy(definition, actions);

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

        /// <summary>Adds PlayerAutoAttack to an existing prefab that predates it (Phase 5).</summary>
        private static void MigrateAddPlayerAutoAttackIfMissing(GameObject prefabAsset)
        {
            if (prefabAsset.GetComponent<PlayerAutoAttack>() != null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(prefabAsset);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents.GetComponent<PlayerAutoAttack>() == null)
            {
                Transform firePoint = contents.transform.Find("FirePoint");
                var autoAttack = contents.AddComponent<PlayerAutoAttack>();
                var autoAttackSerialized = new SerializedObject(autoAttack);
                autoAttackSerialized.FindProperty("_firePoint").objectReferenceValue = firePoint;
                autoAttackSerialized.ApplyModifiedPropertiesWithoutUndo();

                var controller = contents.GetComponent<PlayerController>();
                if (controller != null)
                {
                    var controllerSerialized = new SerializedObject(controller);
                    controllerSerialized.FindProperty("_autoAttack").objectReferenceValue = autoAttack;
                    controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                Debug.Log("[AlienDefense Setup] Migrated " + path + ": added missing PlayerAutoAttack component.");
            }

            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static GameObject BuildHierarchy(PlayerDefinition definition, InputActionAsset actions)
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
            var autoAttack = root.AddComponent<PlayerAutoAttack>();

            GameObject model = BuildModel(root.transform);
            GameObject firePoint = BuildEmptyChild("FirePoint", root.transform, new Vector3(0f, -0.2f, 0.8f));
            BuildEmptyChild("CollectionPoint", root.transform, Vector3.zero);
            BuildEmptyChild("GroundIndicator", root.transform, Vector3.zero);
            BuildEmptyChild(CameraFollowTargetName, root.transform, CameraFollowTargetLocalPosition);
            BuildShadow(root.transform, definition);
            GameObject vfx = BuildEmptyChild("VFX", root.transform, Vector3.zero);
            BuildEmptyChild("HoverEffect", vfx.transform, Vector3.zero);
            BuildEmptyChild("TractorBeam", vfx.transform, Vector3.zero);

            var hoverVisual = model.AddComponent<UFOHoverVisual>();
            var hoverVisualSerialized = new SerializedObject(hoverVisual);
            hoverVisualSerialized.FindProperty("_movementDirectionSource").objectReferenceValue = controller;
            hoverVisualSerialized.ApplyModifiedPropertiesWithoutUndo();

            var inputReaderSerialized = new SerializedObject(inputReader);
            inputReaderSerialized.FindProperty("_actions").objectReferenceValue = actions;
            inputReaderSerialized.ApplyModifiedPropertiesWithoutUndo();

            var autoAttackSerialized = new SerializedObject(autoAttack);
            autoAttackSerialized.FindProperty("_firePoint").objectReferenceValue = firePoint.transform;
            autoAttackSerialized.ApplyModifiedPropertiesWithoutUndo();

            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("_definition").objectReferenceValue = definition;
            controllerSerialized.FindProperty("_movement").objectReferenceValue = movement;
            controllerSerialized.FindProperty("_inputSource").objectReferenceValue = inputReader;
            controllerSerialized.FindProperty("_autoAttack").objectReferenceValue = autoAttack;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
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
