using AlienDefense.Player;
using AlienDefense.Vfx;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Wraps Assets/_Game/VFX/GoopSpray.prefab (left untouched) into a pooled one-shot VFX and hooks it onto
    /// UFO_Player's tractor beam, so an enemy pulled into the UFO sprays goop. Also removes the old white capture
    /// flash that every absorption (energy, props, enemies) used to emit. Safe to re-run.</summary>
    internal static class EnemyAbsorbGoopSetup
    {
        private const string GoopSourcePath = "Assets/_Game/VFX/GoopSpray.prefab";
        private const string VfxPrefabPath = "Assets/_Game/Prefabs/Vfx/Vfx_EnemyAbsorbGoop.prefab";
        private const string VfxDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_EnemyAbsorbGoop.asset";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/UFO_Player.prefab";
        private const float GoopScale = 2f;

        [MenuItem("AlienDefense/Setup/Player/Setup Enemy Absorb Goop VFX")]
        private static void Run()
        {
            VfxDefinition definition = BuildVfx();
            if (definition == null)
            {
                return;
            }

            WirePlayer(definition);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyAbsorbGoopSetup] Enemy capture now plays GoopSpray; the white capture flash is removed.");
        }

        private static VfxDefinition BuildVfx()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GoopSourcePath);
            if (source == null)
            {
                Debug.LogError("[EnemyAbsorbGoopSetup] Missing " + GoopSourcePath);
                return null;
            }

            var root = new GameObject("Vfx_EnemyAbsorbGoop");
            var goop = (GameObject)PrefabUtility.InstantiatePrefab(source);
            goop.transform.SetParent(root.transform, false);
            // The source was authored somewhere in its demo scene; centre it and let the spawn rotation aim it.
            goop.transform.localPosition = Vector3.zero;
            goop.transform.localRotation = Quaternion.identity;

            // Authored for a close-up demo; from the gameplay camera it needs to be bigger to read on a phone.
            // Hierarchy scaling so the wrapper's scale reaches the systems (they were authored with Local scaling).
            goop.transform.localScale = Vector3.one * GoopScale;
            foreach (ParticleSystem ps in goop.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = false; // one spray per captured enemy - the pool replays it
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            var pooled = root.AddComponent<PooledVfx>();
            var so = new SerializedObject(pooled);
            SerializedProperty systems = so.FindProperty("_particleSystems");
            systems.arraySize = 1;
            systems.GetArrayElementAtIndex(0).objectReferenceValue = goop.GetComponent<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, VfxPrefabPath);
            Object.DestroyImmediate(root);

            var definition = AssetDatabase.LoadAssetAtPath<VfxDefinition>(VfxDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<VfxDefinition>();
                AssetDatabase.CreateAsset(definition, VfxDefinitionPath);
            }

            var dso = new SerializedObject(definition);
            dso.FindProperty("_id").stringValue = "vfx_enemy_absorb_goop";
            dso.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<PooledVfx>();
            dso.FindProperty("_lifetime").floatValue = 2f;
            dso.FindProperty("_poolPrewarmCount").intValue = 3;
            dso.FindProperty("_poolDefaultCapacity").intValue = 6;
            dso.FindProperty("_poolMaximumSize").intValue = 16;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void WirePlayer(VfxDefinition definition)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                UFOTractorBeamVisual visual = contents.GetComponentInChildren<UFOTractorBeamVisual>(true);
                if (visual == null)
                {
                    Debug.LogError("[EnemyAbsorbGoopSetup] UFO_Player has no UFOTractorBeamVisual.");
                    return;
                }

                Transform anchor = FindDeep(contents.transform, "CaptureFlashPoint");
                Transform oldFlash = FindDeep(contents.transform, "CaptureFlashParticles");
                if (oldFlash != null)
                {
                    Object.DestroyImmediate(oldFlash.gameObject);
                }

                var so = new SerializedObject(visual);
                so.FindProperty("_enemyCaptureVfx").objectReferenceValue = definition;
                so.FindProperty("_enemyCaptureVfxAnchor").objectReferenceValue = anchor;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
