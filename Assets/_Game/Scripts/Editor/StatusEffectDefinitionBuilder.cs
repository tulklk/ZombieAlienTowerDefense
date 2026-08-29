using AlienDefense.Combat;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds (or reuses) the Phase 15 status effect definitions: Slow and Burn.</summary>
    internal static class StatusEffectDefinitionBuilder
    {
        private const string DataFolder = "Assets/_Game/Data/StatusEffects";
        private const string SlowPath = DataFolder + "/StatusEffect_Slow.asset";
        private const string BurnPath = DataFolder + "/StatusEffect_Burn.asset";

        public static StatusEffectDefinition Slow => AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(SlowPath);
        public static StatusEffectDefinition Burn => AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(BurnPath);

        [MenuItem("AlienDefense/Setup/17. Create Status Effect Definitions")]
        public static void CreateAll()
        {
            EditorFolderUtility.EnsureFolder(DataFolder);

            CreateOrLoad(SlowPath, "status_slow", "Slow", StatusEffectType.Slow, duration: 3f, magnitude: 0.5f, tickInterval: 1f,
                stackingRule: StatusStackingRule.RefreshDurationOnly, maxStacks: 1, tint: new Color(0.4f, 0.75f, 1f));

            CreateOrLoad(BurnPath, "status_burn", "Burn", StatusEffectType.Burn, duration: 4f, magnitude: 4f, tickInterval: 1f,
                stackingRule: StatusStackingRule.StackMagnitude, maxStacks: 3, tint: new Color(1f, 0.45f, 0.15f));

            Debug.Log("[AlienDefense Setup] Status effect definitions ready.");
        }

        private static void CreateOrLoad(
            string path, string id, string displayName, StatusEffectType type,
            float duration, float magnitude, float tickInterval, StatusStackingRule stackingRule, int maxStacks, Color tint)
        {
            var definition = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<StatusEffectDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_type").enumValueIndex = (int)type;
            serialized.FindProperty("_duration").floatValue = duration;
            serialized.FindProperty("_magnitude").floatValue = magnitude;
            serialized.FindProperty("_tickInterval").floatValue = tickInterval;
            serialized.FindProperty("_stackingRule").enumValueIndex = (int)stackingRule;
            serialized.FindProperty("_maxStacks").intValue = maxStacks;
            serialized.FindProperty("_tintColor").colorValue = tint;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
        }
    }
}
