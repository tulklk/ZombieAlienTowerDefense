using System.Reflection;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Turns the zombie model in Models/Zombie/Prefabs/2 (ZombieVariant2 - the only variant no enemy type
    /// used) into its own enemy type: Enemy_Zombie2 (a copy of Enemy_Normal with the model swapped through
    /// ZombieVariantBuilder's own visual-swap step, so it is wired exactly like the other zombies) and
    /// EnemyDefinition_Enemy_Zombie2 (a copy of the Normal definition - same health, speed, damage and reward;
    /// it is a second look for the basic zombie, not a new balance tier). Safe to run again.</summary>
    public static class Zombie2EnemySetup
    {
        private const string SourcePrefab = "Assets/_Game/Prefabs/Enemies/Enemy_Normal.prefab";
        private const string PrefabPath = "Assets/_Game/Prefabs/Enemies/Enemy_Zombie2.prefab";
        private const string VariantPrefab = "Assets/_Game/Models/Zombie/Prefabs/2/ZombieVariant2.prefab";
        private const string SourceDefinition = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Normal.asset";
        public const string DefinitionPath = "Assets/_Game/Data/Enemies/EnemyDefinition_Enemy_Zombie2.asset";

        [MenuItem("Tools/Tower Defense/Enemies/Setup Zombie 2 Enemy")]
        public static void SetupFromMenu()
        {
            Debug.Log(Setup());
        }

        public static string Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                AssetDatabase.CopyAsset(SourcePrefab, PrefabPath);
            }

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefab);
            MethodInfo apply = typeof(ZombieVariantBuilder).GetMethod("ApplyVariantVisualToEnemy", BindingFlags.NonPublic | BindingFlags.Static);
            apply.Invoke(null, new object[] { "Enemy_Zombie2", variant });

            // The attack visual still points at the Animator of the model that was swapped out.
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                contents.name = "Enemy_Zombie2";
                var attack = contents.GetComponent<EnemyBaseAttackVisual>();
                Transform model = contents.transform.Find("VisualRoot/Model");
                if (attack != null && model != null)
                {
                    var so = new SerializedObject(attack);
                    so.FindProperty("_animator").objectReferenceValue = model.GetComponent<Animator>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (AssetDatabase.LoadMainAssetAtPath(DefinitionPath) == null)
            {
                AssetDatabase.CopyAsset(SourceDefinition, DefinitionPath);
            }

            Object definition = AssetDatabase.LoadMainAssetAtPath(DefinitionPath);
            var definitionSo = new SerializedObject(definition);
            definitionSo.FindProperty("_id").stringValue = "enemy_zombie2";
            definitionSo.FindProperty("_displayName").stringValue = "Ghoul Enemy";
            definitionSo.FindProperty("_prefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<EnemyController>();
            definitionSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            var model2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.Find("VisualRoot/Model");
            return "[Zombie2EnemySetup] Enemy_Zombie2 (model " + (model2 != null ? model2.GetComponentInChildren<Renderer>()?.sharedMesh() : "MISSING")
                + ") + EnemyDefinition_Enemy_Zombie2 (Normal stats).";
        }

        private static string sharedMesh(this Renderer renderer)
        {
            var skinned = renderer as SkinnedMeshRenderer;
            return skinned != null && skinned.sharedMesh != null ? skinned.sharedMesh.name : renderer.name;
        }
    }
}
