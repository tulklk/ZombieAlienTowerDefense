using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds the Missile skill's rocket: its own projectile (RMB_20 model + VFX_Fire_01_Small_Smoke exhaust
    /// at the tail) and a SmallExplosion hit effect, then points every level's LevelCompositionRoot at it. The
    /// towers keep Projectile_Blaster. Source assets in Assets/_Game/Models/Rocket are only nested, never edited -
    /// every tweak (scale, exhaust density, muted fire loop) is an override inside the generated prefabs.
    /// Safe to re-run: generated assets are rebuilt in place (GUIDs kept).</summary>
    internal static class RocketMissileSetup
    {
        private const string RocketModelPath = "Assets/_Game/Models/Rocket/RMB_20.prefab";
        private const string ExhaustPath = "Assets/_Game/Models/Rocket/VFX_Fire_01_Small_Smoke.prefab";
        private const string ExplosionPath = "Assets/_Game/Models/Rocket/SmallExplosion.prefab";

        private const string ProjectilePrefabPath = "Assets/_Game/Prefabs/Projectiles/Projectile_Rocket.prefab";
        private const string ProjectileDefinitionPath = "Assets/_Game/Data/Projectiles/Projectile_Rocket.asset";
        private const string ExplosionPrefabPath = "Assets/_Game/Prefabs/Vfx/Vfx_RocketExplosion.prefab";
        private const string ExplosionDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_RocketExplosion.asset";
        private const string LevelScenesFolder = "Assets/_Game/Scenes/Levels";

        // RMB_20 is ~10.2 units long, nose along +Y, its mesh centre 0.73 above the pivot.
        private const float RocketScale = 0.1f;
        private const float RocketModelLength = 10.19f;
        private const float RocketModelCentreY = 0.73f;
        private const float ExhaustScale = 0.6f;
        private const float ExplosionScale = 0.55f;
        private const float ShockwaveScale = 0.2f;

        [MenuItem("AlienDefense/Setup/Player/Setup Rocket Missile")]
        private static void Run()
        {
            VfxDefinition explosion = BuildExplosion();
            ProjectileDefinition rocket = BuildProjectile(explosion);
            int scenes = AssignToLevels(rocket);
            bool hud = BuildCooldownHudInActiveScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[RocketMissileSetup] Rocket missile ready ({ProjectileDefinitionPath}); assigned in {scenes} level scene(s); cooldown HUD {(hud ? "built" : "skipped (no minimap/launcher in the open scene)")}.");
        }

        private const string CooldownIconPath = "Assets/_Game/Art/Sprite/Play/rocketloading.png";

        /// <summary>Rocket cooldown badge right under the minimap (Canvas/SafeArea/MissileCooldown) in the open
        /// level scene, wired to its PlayerMissileController. Rebuilt from scratch on every run.</summary>
        private static bool BuildCooldownHudInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            var launcher = Object.FindFirstObjectByType<AlienDefense.Player.PlayerMissileController>(FindObjectsInactive.Include);
            var minimap = Object.FindFirstObjectByType<AlienDefense.UI.Minimap.MinimapController>(FindObjectsInactive.Include);
            if (launcher == null || minimap == null || minimap.transform.parent == null)
            {
                return false;
            }

            var minimapRoot = (RectTransform)minimap.transform.parent;
            Transform safeArea = minimapRoot.parent;
            Transform existing = safeArea.Find("MissileCooldown");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CooldownIconPath);
            const float size = 124f;

            var root = new GameObject("MissileCooldown", typeof(RectTransform));
            root.transform.SetParent(safeArea, false);
            root.transform.SetSiblingIndex(minimapRoot.GetSiblingIndex() + 1);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            // Centred under the minimap, a small gap below it.
            float minimapCentreX = minimapRoot.anchoredPosition.x - minimapRoot.sizeDelta.x * minimapRoot.pivot.x + minimapRoot.sizeDelta.x * 0.5f;
            float minimapBottom = minimapRoot.anchoredPosition.y - minimapRoot.sizeDelta.y * minimapRoot.pivot.y;
            rootRect.anchoredPosition = new Vector2(minimapCentreX, minimapBottom - 18f);
            rootRect.sizeDelta = new Vector2(size, size);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var contentRect = Stretch((RectTransform)content.transform);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            icon.transform.SetParent(content.transform, false);
            Stretch((RectTransform)icon.transform);
            var iconImage = icon.GetComponent<UnityEngine.UI.Image>();
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // Same sprite as the icon so the sweep is exactly the badge's round shape.
            var overlay = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            overlay.transform.SetParent(content.transform, false);
            Stretch((RectTransform)overlay.transform);
            var overlayImage = overlay.GetComponent<UnityEngine.UI.Image>();
            overlayImage.sprite = sprite;
            overlayImage.preserveAspect = true;
            overlayImage.raycastTarget = false;
            overlayImage.color = new Color(0.02f, 0.05f, 0.12f, 0.68f);
            overlayImage.type = UnityEngine.UI.Image.Type.Filled;
            overlayImage.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            overlayImage.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            overlayImage.fillClockwise = false; // the dark part recedes clockwise as the cooldown runs out
            overlayImage.fillAmount = 0f;

            var text = new GameObject("SecondsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            text.transform.SetParent(content.transform, false);
            Stretch((RectTransform)text.transform);
            var tmp = text.GetComponent<TMPro.TextMeshProUGUI>();
            TMPro.TMP_FontAsset font = null;
            foreach (string guid in AssetDatabase.FindAssets("Fredoka-Bold SDF t:TMP_FontAsset"))
            {
                font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                break;
            }

            if (font != null)
            {
                tmp.font = font;
            }

            tmp.text = string.Empty;
            tmp.fontSize = 50f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(10, 20, 45, 255);

            content.SetActive(false);

            var view = root.AddComponent<AlienDefense.UI.MissileCooldownView>();
            var so = new SerializedObject(view);
            so.FindProperty("_missileController").objectReferenceValue = launcher;
            so.FindProperty("_content").objectReferenceValue = content;
            so.FindProperty("_cooldownOverlay").objectReferenceValue = overlayImage;
            so.FindProperty("_secondsText").objectReferenceValue = tmp;
            so.FindProperty("_punchTarget").objectReferenceValue = contentRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static VfxDefinition BuildExplosion()
        {
            var root = new GameObject("Vfx_RocketExplosion");
            var explosionSource = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
            var explosion = (GameObject)PrefabUtility.InstantiatePrefab(explosionSource);
            explosion.transform.SetParent(root.transform, false);
            // The source prefab's root sits ~20 m off its origin; centre it on the hit point.
            explosion.transform.localPosition = Vector3.zero;

            // Scale every system itself: the explosion's systems use Local scaling, which ignores parent scale.
            foreach (ParticleSystem ps in explosion.GetComponentsInChildren<ParticleSystem>(true))
            {
                // The shockwave sphere is authored 10 m wide - far bigger than an enemy - so it shrinks further.
                ps.transform.localScale = Vector3.one * (ps.name == "Shockwave" ? ShockwaveScale : ExplosionScale);
                var main = ps.main;
                main.loop = false; // one burst per hit - the pool replays it
                main.playOnAwake = false;
            }

            var pooled = root.AddComponent<PooledVfx>();
            var so = new SerializedObject(pooled);
            SerializedProperty systems = so.FindProperty("_particleSystems");
            systems.arraySize = 1;
            systems.GetArrayElementAtIndex(0).objectReferenceValue = explosion.GetComponent<ParticleSystem>();
            so.FindProperty("_keepWorldUpright").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPrefabPath);
            Object.DestroyImmediate(root);

            var definition = LoadOrCreate<VfxDefinition>(ExplosionDefinitionPath);
            var dso = new SerializedObject(definition);
            dso.FindProperty("_id").stringValue = "vfx_rocket_explosion";
            dso.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<PooledVfx>();
            dso.FindProperty("_lifetime").floatValue = 1.6f;
            dso.FindProperty("_poolPrewarmCount").intValue = 4;
            dso.FindProperty("_poolDefaultCapacity").intValue = 8;
            dso.FindProperty("_poolMaximumSize").intValue = 32;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static ProjectileDefinition BuildProjectile(VfxDefinition hitVfx)
        {
            var root = new GameObject("Projectile_Rocket");
            var controller = root.AddComponent<ProjectileController>();

            // ProjectileController faces +Z toward its target, so the nose (+Y on the model) is turned onto +Z.
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RocketModelPath));
            model.transform.SetParent(visualRoot.transform, false);
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            model.transform.localScale = Vector3.one * RocketScale;
            model.transform.localPosition = new Vector3(0f, 0f, -RocketModelCentreY * RocketScale); // centred on the pivot

            // Exhaust at the tail, flames (which rise along +Y) turned to stream out backwards along -Z.
            float tailZ = -RocketModelLength * 0.5f * RocketScale;
            var exhaust = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ExhaustPath));
            exhaust.name = "Exhaust";
            exhaust.transform.SetParent(root.transform, false);
            exhaust.transform.localPosition = new Vector3(0f, 0f, tailZ);
            exhaust.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            exhaust.transform.localScale = Vector3.one * ExhaustScale;
            ConfigureExhaust(exhaust);

            var so = new SerializedObject(controller);
            SerializedProperty attached = so.FindProperty("_attachedParticles");
            attached.arraySize = 1;
            attached.GetArrayElementAtIndex(0).objectReferenceValue = exhaust.GetComponent<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);

            var definition = LoadOrCreate<ProjectileDefinition>(ProjectileDefinitionPath);
            var dso = new SerializedObject(definition);
            dso.FindProperty("_id").stringValue = "projectile_rocket";
            dso.FindProperty("_displayName").stringValue = "Rocket";
            dso.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<ProjectileController>();
            dso.FindProperty("_speed").floatValue = 12f;
            dso.FindProperty("_maximumLifetime").floatValue = 4f;
            dso.FindProperty("_hitDistance").floatValue = 0.35f;
            dso.FindProperty("_hitVfxDefinition").objectReferenceValue = hitVfx;
            dso.FindProperty("_poolPrewarmCount").intValue = 6;
            dso.FindProperty("_poolDefaultCapacity").intValue = 12;
            dso.FindProperty("_poolMaximumSize").intValue = 48;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>The fire effect is authored as a stationary campfire: particles emitted per second and left in
        /// world space. On a rocket moving 12 m/s that reads as scattered blobs, so the flame/smoke layers also
        /// emit per metre travelled and live shorter, giving a tight tail. The looping fire sound and the heat haze
        /// (needs the opaque texture, which this renderer has off) are switched off.</summary>
        private static void ConfigureExhaust(GameObject exhaust)
        {
            var audio = exhaust.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.enabled = false;
            }

            foreach (ParticleSystem ps in exhaust.GetComponentsInChildren<ParticleSystem>(true))
            {
                switch (ps.name)
                {
                    case "Heat Distortion":
                        ps.gameObject.SetActive(false);
                        break;
                    case "Flames":
                    case "Flames Secondary":
                        Tune(ps, lifetimeScale: 0.35f, perMetre: 9f);
                        break;
                    case "Dark Background":
                        Tune(ps, lifetimeScale: 0.6f, perMetre: 5f);
                        break;
                    case "Ashes":
                        Tune(ps, lifetimeScale: 0.5f, perMetre: 3f);
                        break;
                }
            }
        }

        private static void Tune(ParticleSystem ps, float lifetimeScale, float perMetre)
        {
            var main = ps.main;
            main.startLifetimeMultiplier *= lifetimeScale;
            var emission = ps.emission;
            emission.rateOverDistanceMultiplier = perMetre;
        }

        private static int AssignToLevels(ProjectileDefinition rocket)
        {
            int assigned = 0;
            string rocketGuid = AssetDatabase.AssetPathToGUID(ProjectileDefinitionPath);
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { LevelScenesFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = SceneManager.GetSceneByPath(path);
                bool openedHere = !scene.isLoaded;

                // Already pointing at the rocket on disk - no need to open (and re-save) the scene again.
                if (openedHere && System.IO.File.ReadAllText(path).Contains("_missileProjectileDefinition: {fileID: 11400000, guid: " + rocketGuid))
                {
                    assigned++;
                    continue;
                }

                if (openedHere)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                bool changed = false;
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    foreach (LevelCompositionRoot compositionRoot in go.GetComponentsInChildren<LevelCompositionRoot>(true))
                    {
                        var so = new SerializedObject(compositionRoot);
                        SerializedProperty property = so.FindProperty("_missileProjectileDefinition");
                        if (property != null && property.objectReferenceValue != rocket)
                        {
                            property.objectReferenceValue = rocket;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            changed = true;
                        }

                        assigned++;
                    }
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return assigned;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }
    }
}
