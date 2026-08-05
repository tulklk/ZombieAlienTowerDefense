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
            collider.center = new Vector3(0f, 0.15f, 0f);
            collider.size = new Vector3(1.2f, 0.3f, 1.2f);

            GameObject baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseVisual.name = "BaseVisual";
            baseVisual.transform.SetParent(root.transform, false);
            baseVisual.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            baseVisual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            Object.DestroyImmediate(baseVisual.GetComponent<Collider>());
            baseVisual.GetComponent<MeshRenderer>().sharedMaterial = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_BuildNode_Base", "Universal Render Pipeline/Lit", new Color(0.4f, 0.4f, 0.45f));

            GameObject availableIndicator = BuildRingIndicator("AvailableIndicator", root.transform, new Color(0.3f, 0.85f, 0.9f, 0.6f));
            GameObject occupiedIndicator = BuildRingIndicator("OccupiedIndicator", root.transform, new Color(0.9f, 0.3f, 0.3f, 0.5f));
            GameObject disabledIndicator = BuildRingIndicator("DisabledIndicator", root.transform, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            GameObject selectedIndicator = BuildRingIndicator("SelectedIndicator", root.transform, new Color(0.95f, 0.85f, 0.2f, 0.7f));
            occupiedIndicator.SetActive(false);
            disabledIndicator.SetActive(false);
            selectedIndicator.SetActive(false);

            var buildPoint = new GameObject("BuildPoint");
            buildPoint.transform.SetParent(root.transform, false);
            buildPoint.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            var visual = root.AddComponent<BuildNodeVisual>();
            var visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("_availableIndicator").objectReferenceValue = availableIndicator;
            visualSerialized.FindProperty("_occupiedIndicator").objectReferenceValue = occupiedIndicator;
            visualSerialized.FindProperty("_disabledIndicator").objectReferenceValue = disabledIndicator;
            visualSerialized.FindProperty("_selectedIndicator").objectReferenceValue = selectedIndicator;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();

            var node = root.AddComponent<BuildNode>();
            var nodeSerialized = new SerializedObject(node);
            nodeSerialized.FindProperty("_buildPoint").objectReferenceValue = buildPoint.transform;
            nodeSerialized.FindProperty("_visual").objectReferenceValue = visual;
            nodeSerialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject BuildRingIndicator(string name, Transform parent, Color color)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = name;
            ring.transform.SetParent(parent, false);
            ring.transform.localScale = new Vector3(0.65f, 0.02f, 0.65f);
            ring.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_BuildNode_" + name, "Universal Render Pipeline/Unlit", color);
            ConfigureTransparent(material);
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
