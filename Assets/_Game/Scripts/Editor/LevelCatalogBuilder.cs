using AlienDefense.Core;
using AlienDefense.Data;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Creates (or re-wires) the LevelCatalog asset with the one campaign level that exists so far.
    /// Extend the entries here as more levels are authored; never add runtime/save state to this asset.</summary>
    internal static class LevelCatalogBuilder
    {
        private const string CatalogFolder = "Assets/_Game/Data/Levels";
        private const string CatalogPath = CatalogFolder + "/LevelCatalog.asset";
        private const string Level01DefinitionPath = CatalogFolder + "/Level_01_Definition.asset";
        private const string Level01SceneName = "Level_01";

        [MenuItem("AlienDefense/Setup/11. Create Level Catalog Asset")]
        public static LevelCatalog CreateOrLoad()
        {
            LevelDefinition level01 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level01DefinitionPath);
            if (level01 == null)
            {
                Debug.LogError("[AlienDefense Setup] No Level_01_Definition asset found at " + Level01DefinitionPath +
                    ". Run 'AlienDefense/Setup/4. Build Level_01 Scene Skeleton' first.");
                return null;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorFolderUtility.EnsureFolder(CatalogFolder);
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entriesProperty = serialized.FindProperty("_entries");
            entriesProperty.arraySize = 1;

            SerializedProperty entry0 = entriesProperty.GetArrayElementAtIndex(0);
            entry0.FindPropertyRelative("_levelDefinition").objectReferenceValue = level01;
            entry0.FindPropertyRelative("_sceneName").stringValue = Level01SceneName;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log("[AlienDefense Setup] LevelCatalog ready at " + CatalogPath + " with 1 entry (Level_01).");
            return AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        }
    }
}
