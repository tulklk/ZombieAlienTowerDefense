using AlienDefense.Building;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the Phase 7 BuildNode prototype prefab.</summary>
    internal static class BuildNodePrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/Building";
        private const string PrefabPath = PrefabFolder + "/BuildNode.prefab";
        private const string BuildNodeLayerName = "BuildNode";

        /// <summary>Tripo-generated tower base platform (helipad-style octagon with an X marker), used for
        /// BaseVisual in place of the original placeholder cylinder. Its FBX root carries a baked (270,0,0)
        /// axis-conversion rotation to lie flat — preserved as-authored via InstantiatePrefab, never touched.</summary>
        private const string BaseVisualModelPath =
            "Assets/_Game/Models/Base/tripo_convert_7c586f8b-a725-4d3b-96d1-87e86cf80088.fbx";

        /// <summary>BaseVisual's world-space diameter. Must stay clearly larger than a built Tower's own base
        /// footprint (Tower_Blaster's turret_base1 is ~0.93 diameter) so the platform reads as visibly bigger
        /// than whatever gets built on it, per request.</summary>
        private const float BaseVisualScale = 2.3f;

        private const float RingScale = 3.4f;
        private const float SelectedRingScale = 4.2f;
        private const float SelectedRingInnerRadius = 0.9f;
        private const float RingHeight = 0.32f;
        private const float BuildPointHeight = 0.4f;
        private const float ColliderHeight = 0.35f;
        private const float ColliderSize = 2.8f;
        private const float ColliderSizeY = 0.7f;

        private const string HologramMaterialPath = "Assets/_Game/Materials/TowerHologram.mat";

        [MenuItem("AlienDefense/Setup/9. Create BuildNode Prefab")]
        public static GameObject CreateOrLoad()
        {
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log("[AlienDefense Setup] BuildNode prefab ready.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("BuildNode");
            int buildNodeLayer = LayerMask.NameToLayer(BuildNodeLayerName);
            if (buildNodeLayer >= 0)
            {
                root.layer = buildNodeLayer;
            }
            else
            {
                Debug.LogWarning($"[AlienDefense Setup] Layer '{BuildNodeLayerName}' not found; leaving BuildNode on Default layer.");
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, ColliderHeight, 0f);
            collider.size = new Vector3(ColliderSize, ColliderSizeY, ColliderSize);

            GameObject baseVisual = BuildBaseVisual(root.transform);

            // OccupiedIndicator turns green once a real Tower is built here (was removed, then brought back
            // green per a later request — see BuildNodeVisual.SetState).
            GameObject availableIndicator = BuildRingIndicator("AvailableIndicator", root.transform, new Color(0.3f, 0.85f, 0.9f, 0.6f));
            GameObject occupiedIndicator = BuildRingIndicator("OccupiedIndicator", root.transform, new Color(0.25f, 0.9f, 0.35f, 0.75f));
            GameObject disabledIndicator = BuildRingIndicator("DisabledIndicator", root.transform, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            GameObject selectedIndicator = BuildRingIndicator("SelectedIndicator", root.transform, new Color(0.95f, 0.85f, 0.2f, 0.7f));
            occupiedIndicator.SetActive(false);
            disabledIndicator.SetActive(false);
            selectedIndicator.SetActive(false);

            // Selected (yellow) is the one shown by default on every Available node now (see
            // BuildNodeVisualCoordinator's _defaultPreviewTower), so it gets its own bigger/thinner treatment
            // instead of sharing the other rings' sizing.
            selectedIndicator.transform.localScale = new Vector3(SelectedRingScale, 0.02f, SelectedRingScale);
            var selectedMaterial = selectedIndicator.GetComponent<MeshRenderer>().sharedMaterial;
            selectedMaterial.SetFloat("_InnerRadius", SelectedRingInnerRadius);

            var buildPoint = new GameObject("BuildPoint");
            buildPoint.transform.SetParent(root.transform, false);
            buildPoint.transform.localPosition = new Vector3(0f, BuildPointHeight, 0f);

            var visual = root.AddComponent<BuildNodeVisual>();
            var visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("_availableIndicator").objectReferenceValue = availableIndicator;
            visualSerialized.FindProperty("_occupiedIndicator").objectReferenceValue = occupiedIndicator;
            visualSerialized.FindProperty("_disabledIndicator").objectReferenceValue = disabledIndicator;
            visualSerialized.FindProperty("_selectedIndicator").objectReferenceValue = selectedIndicator;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();

            // TowerPreviewRoot: dedicated child solely for the hologram ghost, positioned exactly where the real
            // Tower would spawn (same as BuildPoint) so the preview reads as "this is what will be built here".
            var previewRoot = new GameObject("TowerPreviewRoot");
            previewRoot.transform.SetParent(root.transform, false);
            previewRoot.transform.localPosition = buildPoint.transform.localPosition;
            previewRoot.transform.localRotation = buildPoint.transform.localRotation;

            var hologramPreview = root.AddComponent<TowerHologramPreview>();
            var hologramMaterial = AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath);
            if (hologramMaterial == null)
            {
                Debug.LogWarning($"[AlienDefense Setup] Hologram material not found at '{HologramMaterialPath}'; TowerHologramPreview will have no material assigned.");
            }

            var hologramSerialized = new SerializedObject(hologramPreview);
            hologramSerialized.FindProperty("_previewRoot").objectReferenceValue = previewRoot.transform;
            hologramSerialized.FindProperty("_hologramMaterial").objectReferenceValue = hologramMaterial;
            hologramSerialized.ApplyModifiedPropertiesWithoutUndo();

            var node = root.AddComponent<BuildNode>();
            var nodeSerialized = new SerializedObject(node);
            nodeSerialized.FindProperty("_buildPoint").objectReferenceValue = buildPoint.transform;
            nodeSerialized.FindProperty("_visual").objectReferenceValue = visual;
            nodeSerialized.FindProperty("_hologramPreview").objectReferenceValue = hologramPreview;
            nodeSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Nests the Tripo tower-base model as BaseVisual, falling back to the original placeholder
        /// cylinder if the model asset is missing (e.g. not yet imported on a fresh clone).</summary>
        private static GameObject BuildBaseVisual(Transform parent)
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseVisualModelPath);
            if (modelPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                instance.name = "BaseVisual";
                instance.transform.SetParent(parent, false); // keeps the model's own authored local pos/rot
                instance.transform.localScale = Vector3.one * BaseVisualScale;
                return instance;
            }

            Debug.LogWarning($"[AlienDefense Setup] BaseVisual model not found at '{BaseVisualModelPath}'; using a placeholder cylinder instead.");
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placeholder.name = "BaseVisual";
            placeholder.transform.SetParent(parent, false);
            placeholder.transform.localScale = new Vector3(BaseVisualScale, BaseVisualScale * 0.1f, BaseVisualScale);
            placeholder.transform.localPosition = new Vector3(0f, BaseVisualScale * 0.05f, 0f);
            Object.DestroyImmediate(placeholder.GetComponent<Collider>());
            placeholder.GetComponent<MeshRenderer>().sharedMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_BuildNode_Base", "Universal Render Pipeline/Lit", new Color(0.4f, 0.4f, 0.45f));
            return placeholder;
        }

        private static GameObject BuildRingIndicator(string name, Transform parent, Color color)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = name;
            ring.transform.SetParent(parent, false);
            ring.transform.localScale = new Vector3(RingScale, 0.02f, RingScale);
            ring.transform.localPosition = new Vector3(0f, RingHeight, 0f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            // AlienDefense/RingIndicator renders a hollow border (see the shader file) instead of a filled disc
            // — already Transparent/ZWrite-off by design, so no separate ConfigureTransparent() pass is needed.
            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_BuildNode_" + name, "AlienDefense/RingIndicator", color);
            material.SetFloat("_InnerRadius", 0.82f);
            material.SetFloat("_EdgeSoftness", 0.04f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = material;

            return ring;
        }

        /// <summary>Switches a URP Lit/Unlit material to alpha-blended Transparent so its color alpha is visible.</summary>
        private static void ConfigureTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
