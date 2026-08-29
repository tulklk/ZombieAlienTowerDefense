using AlienDefense.Pickups;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the single EnergyPickup prefab: a small glowing sphere, procedural material,
    /// no external asset — matching this project's established "no unauthorized package" convention for VFX-ish
    /// placeholders (see TractorBeamMaterialBuilder).</summary>
    internal static class EnergyPickupPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Game/Prefabs/Pickups";
        private const string PrefabPath = PrefabFolder + "/EnergyPickup.prefab";

        private static readonly Color PickupColor = new Color(0.4f, 0.95f, 1f, 1f);

        [MenuItem("AlienDefense/Setup/32. Create Energy Pickup Prefab")]
        public static EnergyPickupController CreateOrLoadPrefab()
        {
            EditorFolderUtility.EnsureFolder(PrefabFolder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<EnergyPickupController>();
            }

            GameObject root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[AlienDefense Setup] Energy Pickup prefab ready at " + PrefabPath + ".");
            return saved != null ? saved.GetComponent<EnergyPickupController>() : null;
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("EnergyPickup");
            var controller = root.AddComponent<EnergyPickupController>();

            Material material = EditorMaterialUtility.CreateOrLoadMaterial(
                "Mat_EnergyPickup", "Universal Render Pipeline/Unlit", PickupColor);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.35f;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_visualRoot").objectReferenceValue = visual.transform;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
