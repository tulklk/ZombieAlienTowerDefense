using AlienDefense.Save;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Creates the PlayerProfileDefaults asset used to seed a brand-new save profile. All 3 current
    /// towers start unlocked since Phase 13 only lays the persistence foundation — actual unlock gating in the
    /// Build UI is Phase 14 scope.</summary>
    internal static class PlayerProfileDefaultsBuilder
    {
        private const string AssetFolder = "Assets/_Game/Data/Save";
        private const string AssetPath = AssetFolder + "/PlayerProfileDefaults.asset";

        private static readonly string[] DefaultUnlockedTowerIds = { "tower_blaster", "tower_rapid", "tower_heavy" };

        [MenuItem("AlienDefense/Setup/17. Create Player Profile Defaults Asset")]
        public static PlayerProfileDefaults CreateOrLoad()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerProfileDefaults>(AssetPath);
            if (existing != null)
            {
                Debug.Log("[AlienDefense Setup] PlayerProfileDefaults already exists at " + AssetPath + ", reusing it.");
                return existing;
            }

            EditorFolderUtility.EnsureFolder(AssetFolder);

            var defaults = ScriptableObject.CreateInstance<PlayerProfileDefaults>();

            var serialized = new SerializedObject(defaults);
            SerializedProperty towerIdsProperty = serialized.FindProperty("_defaultUnlockedTowerIds");
            towerIdsProperty.arraySize = DefaultUnlockedTowerIds.Length;
            for (int i = 0; i < DefaultUnlockedTowerIds.Length; i++)
            {
                towerIdsProperty.GetArrayElementAtIndex(i).stringValue = DefaultUnlockedTowerIds[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(defaults, AssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[AlienDefense Setup] Created " + AssetPath + ".");
            return AssetDatabase.LoadAssetAtPath<PlayerProfileDefaults>(AssetPath);
        }
    }
}
