using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Attaches one of the TD_Sci-Fi_Turret1_Example art prefabs (turret_1_1/2/3) as a Tower's visual
    /// model: instantiates it as a prefab-linked child, exposes the model's own rotating "turret_mount1" as the
    /// yaw pivot TowerVisual rotates, and adds a FirePoint marker near the barrel tips for projectile spawn/VFX.</summary>
    internal static class TowerModelAttacher
    {
        private const string TurretRootFolder = "Assets/TD_Sci-Fi_Turret1_Example/Prefabs/Turret1";
        public const string Turret1Path = TurretRootFolder + "/turret_1_1.prefab";
        public const string Turret2Path = TurretRootFolder + "/turret_1_2.prefab";
        public const string Turret3Path = TurretRootFolder + "/turret_1_3.prefab";

        // The source art asset's own units run ~5x this game's 1-unit tower footprint; 0.2 matches how it was
        // hand-tested in the Hierarchy (Inspector Scale 0.2/0.2/0.2) before this was automated.
        private const float ModelScale = 0.2f;

        // Approximate barrel-tip offset in turret_head1's local space. The source asset ships no muzzle socket,
        // so this is an estimate — nudge the "FirePoint" child in the Inspector if a muzzle flash/projectile
        // spawn looks off-center for a given tower.
        private static readonly Vector3 MuzzleLocalOffset = new Vector3(0f, -0.07f, 2.2f);

        /// <summary>Instantiates turretModelPrefab under root (named "Model_&lt;prefabName&gt;" so callers/migrations
        /// can detect which model is already attached) and returns (turretPivot, firePoint) for wiring into
        /// TowerVisual._turretPivot and TowerAttackController._firePoint.</summary>
        public static (Transform turretPivot, Transform firePoint) Attach(Transform root, GameObject turretModelPrefab)
        {
            if (turretModelPrefab == null)
            {
                Debug.LogError("[AlienDefense Setup] TowerModelAttacher.Attach called with a null turret model prefab; " +
                    "check the TD_Sci-Fi_Turret1_Example import. Falling back to an empty pivot so the tower still functions.");
                var fallbackFirePoint = new GameObject("FirePoint");
                fallbackFirePoint.transform.SetParent(root, false);
                fallbackFirePoint.transform.localPosition = new Vector3(0f, 0.4f, 0.8f);
                return (root, fallbackFirePoint.transform);
            }

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(turretModelPrefab);
            modelInstance.name = ModelChildName(turretModelPrefab);
            modelInstance.transform.SetParent(root, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * ModelScale;

            Transform mount = modelInstance.transform.Find("turret_mount1");
            Transform pivot = mount != null ? mount : modelInstance.transform;
            Transform head = mount != null ? mount.Find("turret_head1") : null;
            Transform muzzleParent = head != null ? head : pivot;

            var firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(muzzleParent, false);
            firePointObject.transform.localPosition = MuzzleLocalOffset;

            return (pivot, firePointObject.transform);
        }

        /// <summary>True if `root` already has the correct model attached (used by migrations to stay idempotent).</summary>
        public static bool HasModelAttached(Transform root, GameObject turretModelPrefab)
        {
            return root.Find(ModelChildName(turretModelPrefab)) != null;
        }

        /// <summary>Destroys every direct child of root except ones named in keepNames (e.g. "RangeIndicator").</summary>
        public static void DestroyAllChildrenExcept(Transform root, params string[] keepNames)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (System.Array.IndexOf(keepNames, child.name) >= 0)
                {
                    continue;
                }

                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static string ModelChildName(GameObject turretModelPrefab) => "Model_" + turretModelPrefab.name;
    }
}
