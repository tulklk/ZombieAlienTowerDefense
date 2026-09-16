using AlienDefense.Combat;
using AlienDefense.Towers;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Turns the Blaster Tower into a twin rocket launcher: both barrel pods fire the player's rocket
    /// (Projectile_Rocket's prefab, exhaust and explosion), lobbed - it climbs out of the barrel and falls onto the
    /// target. The player's own Projectile_Rocket stays a straight shot; the tower gets its own definition so the
    /// two keep separate pools and flight settings. Damage, range and fire rate on the tower levels are unchanged.</summary>
    internal static class BlasterTowerRocketSetup
    {
        private const string BlasterPrefabPath = "Assets/_Game/Prefabs/Towers/Tower_Blaster.prefab";
        private const string BlasterDefinitionPath = "Assets/_Game/Data/Towers/TowerDefinition_Tower_Blaster.asset";
        private const string PlayerRocketDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_Rocket.asset";
        private const string TowerRocketDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_BlasterRocket.asset";

        private const float RocketSpeed = 11f;       // along the line to the target; the arc adds a little on top
        private const float RocketArcHeight = 3f;    // metres above the straight line at mid-flight
        private const float RocketLifetime = 4f;

        [MenuItem("AlienDefense/Setup/Towers/Blaster Tower Twin Rockets")]
        private static void Run()
        {
            var playerRocket = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(PlayerRocketDefinitionPath);
            var tower = AssetDatabase.LoadAssetAtPath<TowerDefinition>(BlasterDefinitionPath);
            if (playerRocket == null || tower == null)
            {
                Debug.LogError("[BlasterTowerRocketSetup] Missing Projectile_Rocket or the Blaster tower definition.");
                return;
            }

            ProjectileDefinition rocket = BuildTowerRocket(playerRocket);

            var towerSo = new SerializedObject(tower);
            towerSo.FindProperty("_projectileDefinition").objectReferenceValue = rocket;
            towerSo.FindProperty("_description").stringValue = "Twin rockets that arc up and crash down on the target";
            towerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tower);
            AssetDatabase.SaveAssets();

            if (FrostTowerTwinBarrelSetup.Apply(BlasterPrefabPath, BlasterDefinitionPath))
            {
                Debug.Log("[BlasterTowerRocketSetup] Blaster Tower now lobs a rocket from each barrel pod.");
            }
        }

        private static ProjectileDefinition BuildTowerRocket(ProjectileDefinition playerRocket)
        {
            var rocket = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(TowerRocketDefinitionPath);
            if (rocket == null)
            {
                rocket = ScriptableObject.CreateInstance<ProjectileDefinition>();
                AssetDatabase.CreateAsset(rocket, TowerRocketDefinitionPath);
            }

            var source = new SerializedObject(playerRocket);
            var so = new SerializedObject(rocket);
            so.FindProperty("_id").stringValue = "projectile_blaster_rocket";
            so.FindProperty("_displayName").stringValue = "Blaster Rocket";
            so.FindProperty("_prefab").objectReferenceValue = source.FindProperty("_prefab").objectReferenceValue;
            so.FindProperty("_hitVfxDefinition").objectReferenceValue = source.FindProperty("_hitVfxDefinition").objectReferenceValue;
            so.FindProperty("_speed").floatValue = RocketSpeed;
            so.FindProperty("_maximumLifetime").floatValue = RocketLifetime;
            so.FindProperty("_hitDistance").floatValue = source.FindProperty("_hitDistance").floatValue;
            so.FindProperty("_arcHeight").floatValue = RocketArcHeight;
            // Two rockets per shot, several towers: a bigger pool than the player's launcher needs.
            so.FindProperty("_poolPrewarmCount").intValue = 8;
            so.FindProperty("_poolDefaultCapacity").intValue = 24;
            so.FindProperty("_poolMaximumSize").intValue = 80;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rocket);
            AssetDatabase.SaveAssets();
            return rocket;
        }
    }
}
