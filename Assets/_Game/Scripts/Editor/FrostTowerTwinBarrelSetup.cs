using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>The TD_Sci-Fi turret used by the Frost and Blaster towers has two barrel pods (left and right): this
    /// puts a muzzle at the tip of each so every attack fires one projectile from each pod. FirePoint moves to the
    /// left tip and FirePoint_R is added for the right; TowerAttackController shares the attack's damage between
    /// them, so the tower's DPS is unchanged. The shared orange muzzle flash (untextured squares) is removed too.</summary>
    internal static class FrostTowerTwinBarrelSetup
    {
        private const string FrostTowerPrefabPath = "Assets/_Game/Prefabs/Towers/Tower_Frost.prefab";
        private const string FrostTowerDefinitionPath = "Assets/_Game/Data/Towers/TowerDefinition_Tower_Frost.asset";
        private const string HeadPathSuffix = "/turret_mount1/turret_head1";

        // Barrel tips in turret_head1 space, measured from the barrel meshes: pods at x = +-1.33, tip at z = 4.26,
        // halfway between each pod's upper and lower tube at y = -0.07.
        private const float BarrelX = 1.335f;
        private const float BarrelY = -0.07f;
        private const float BarrelTipZ = 4.2f;

        [MenuItem("AlienDefense/Setup/Towers/Frost Tower Twin Barrels")]
        private static void Run()
        {
            if (Apply(FrostTowerPrefabPath, FrostTowerDefinitionPath))
            {
                Debug.Log("[FrostTowerTwinBarrelSetup] Tower_Frost now fires from both barrel pods.");
            }
        }

        /// <summary>Twin muzzles on a TD_Sci-Fi turret tower prefab and no muzzle flash on its definition.</summary>
        public static bool Apply(string towerPrefabPath, string towerDefinitionPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(towerPrefabPath);
            try
            {
                Transform head = FindHead(root.transform);
                var attack = root.GetComponent<TowerAttackController>();
                if (head == null || attack == null)
                {
                    Debug.LogError("[TwinBarrels] " + towerPrefabPath + " is missing turret_head1 or TowerAttackController.");
                    return false;
                }

                Transform left = EnsureMuzzle(head, "FirePoint", -BarrelX);
                Transform right = EnsureMuzzle(head, "FirePoint_R", BarrelX);

                var so = new SerializedObject(attack);
                so.FindProperty("_firePoint").objectReferenceValue = left;
                SerializedProperty extra = so.FindProperty("_extraFirePoints");
                extra.arraySize = 1;
                extra.GetArrayElementAtIndex(0).objectReferenceValue = right;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, towerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            ClearMuzzleFlash(towerDefinitionPath);
            return true;
        }

        /// <summary>The model root is named per colour variant (Model_turret_1_1, _1_2, ...).</summary>
        private static Transform FindHead(Transform root)
        {
            foreach (Transform child in root)
            {
                Transform head = child.Find(HeadPathSuffix.TrimStart('/'));
                if (head != null)
                {
                    return head;
                }
            }

            return null;
        }

        private static void ClearMuzzleFlash(string towerDefinitionPath)
        {
            var definition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(towerDefinitionPath);
            if (definition == null)
            {
                return;
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_muzzleVfxDefinition").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
        }

        private static Transform EnsureMuzzle(Transform head, string name, float x)
        {
            Transform muzzle = head.Find(name);
            if (muzzle == null)
            {
                muzzle = new GameObject(name).transform;
                muzzle.SetParent(head, false);
            }

            muzzle.localPosition = new Vector3(x, BarrelY, BarrelTipZ);
            muzzle.localRotation = Quaternion.identity;
            muzzle.localScale = Vector3.one;
            return muzzle;
        }
    }
}
