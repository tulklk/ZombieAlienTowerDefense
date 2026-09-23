using System;
using System.Collections.Generic;
using System.IO;
using AlienDefense.Combat;
using AlienDefense.Core;
using AlienDefense.UI;
using AlienDefense.Vfx;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlienDefense.EditorTools
{
    /// <summary>Cartoon rocket VFX and fire damage numbers, matching the stylised mobile TD reference:
    ///
    /// Exhaust (RocketFire_Cartoon, nested in Projectile_Rocket at the nozzle, facing backwards): NozzleCore (a bright
    /// blob glued to the tail) + MainExhaust (thick white-yellow blobs left behind the flight, ~1 rocket length) +
    /// ExhaustEmbers (3-6 small fire dots).
    ///
    /// Explosion (RocketExplosion_Cartoon, swapped into VfxDefinition_Vfx_RocketExplosion, pooled): ImpactFlash
    /// (white/yellow starburst) -> YellowBlast (big round blast) -> FirePlume L/R (a V of chunky orange-red fire) ->
    /// SmokePlume L/R (dark brown cartoon puffs riding above the fire) + Embers. ~35 particles, gone in 0.85 s.
    ///
    /// Damage numbers: both rocket definitions get DamagePopupStyle.Fire; the open level scene gets a
    /// DamagePopupCanvas with DamagePopupService (pooled DamagePopup prefab, generated flame icon, Fredoka font).
    ///
    /// All textures are generated here, drawn with URP Particles/Unlit, no lights. Gameplay values are untouched.
    /// Safe to re-run.</summary>
    internal static class RocketCartoonVfxSetup
    {
        private const string Folder = "Assets/_Game/VFX/Rocket";
        private const string MaterialFolder = Folder + "/Materials";
        private const string TextureFolder = Folder + "/Textures";

        private const string RocketPrefabPath = "Assets/_Game/Prefabs/Projectiles/Projectile_Rocket.prefab";
        private const string FirePrefabPath = Folder + "/RocketFire_Cartoon.prefab";
        private const string ExplosionPrefabPath = Folder + "/RocketExplosion_Cartoon.prefab";
        private const string ExplosionDefinitionPath = "Assets/_Game/Data/Vfx/VfxDefinition_Vfx_RocketExplosion.asset";
        private static readonly string[] RocketDefinitionPaths =
        {
            "Assets/_Game/Data/Projectiles/Projectile_Rocket.asset",
            "Assets/_Game/Data/Projectiles/Projectile_BlasterRocket.asset",
        };

        private const string PopupPrefabPath = "Assets/_Game/Prefabs/UI/DamagePopup.prefab";
        private const string FireIconPath = "Assets/_Game/Art/Sprite/Play/HUD/icon_damage_fire.png";
        private const string FontPath = "Assets/_Game/Font/Fredoka-Bold SDF.asset";

        // Rocket model (RMB_20 at 0.1 scale): ~1.02 m long, ~0.3 m wide, tail at z = -0.51.
        private const float NozzleZ = -0.5f;
        private const float ExplosionLifetime = 0.85f;

        // Exhaust palette.
        private static readonly Color ExhaustWhite = Hex(0xFFF8D7);
        private static readonly Color ExhaustLightYellow = Hex(0xFFE77A);
        private static readonly Color ExhaustGold = Hex(0xFFC845);
        private static readonly Color ExhaustOrange = Hex(0xFF9C32);
        private static readonly Color EmberPaleOrange = Hex(0xFFB45A);

        // Explosion palette.
        private static readonly Color FlashCenter = Hex(0xFFFCE0);
        private static readonly Color FlashOuter = Hex(0xFFD43B);
        private static readonly Color BlastCenter = Hex(0xFFF17A);
        private static readonly Color BlastOuter = Hex(0xFFC928);
        private static readonly Color FireCore = Hex(0xFFD33D);
        private static readonly Color FireMid = Hex(0xFF8426);
        private static readonly Color FireOuter = Hex(0xF34B22);
        private static readonly Color FireDark = Hex(0xD9361F);
        private static readonly Color SmokeBrown = Hex(0x5A3028);
        private static readonly Color SmokeNearBlack = Hex(0x2E2220);

        // Damage number palette.
        private static readonly Color DamageTop = Hex(0xFFB13A);
        private static readonly Color DamageBottom = Hex(0xFF5A2D);
        private static readonly Color DamageOutline = Hex(0x2A120A);
        private static readonly Color KineticBottom = Hex(0xC9D6E2);
        private static readonly Color KineticOutline = Hex(0x1B2430);

        [MenuItem("AlienDefense/Setup/VFX/Rocket Cartoon VFX + Damage Numbers")]
        private static void Run()
        {
            Apply();
            Debug.Log("[RocketCartoonVfxSetup] Rocket exhaust, explosion and fire damage numbers rebuilt.");
        }

        public static void Apply()
        {
            EditorFolderUtility.EnsureFolder(MaterialFolder);
            EditorFolderUtility.EnsureFolder(TextureFolder);

            Texture2D blob = EnsureTexture("T_VFX_Blob", 96, 96, BlobPixel, false);
            Texture2D starburst = EnsureTexture("T_VFX_Starburst", 128, 128, StarburstPixel, false);
            Texture2D blast = EnsureTexture("T_VFX_YellowBlast", 128, 128, BlastPixel, false);

            var materials = new Materials
            {
                Blob = EnsureMaterial("MAT_VFX_Cartoon_Blob", blob),
                Starburst = EnsureMaterial("MAT_VFX_Cartoon_Starburst", starburst),
                Blast = EnsureMaterial("MAT_VFX_Cartoon_Blast", blast),
            };

            GameObject fire = BuildFirePrefab(materials);
            ApplyToRocket(fire);
            BuildExplosion(materials);
            SetRocketPopupStyle();

            DamagePopupView popup = BuildPopupPrefab();
            SetupPopupServiceInOpenScene(popup);
            AssetDatabase.SaveAssets();
        }

        private sealed class Materials
        {
            public Material Blob;
            public Material Starburst;
            public Material Blast;
        }

        // ------------------------------------------------------------------------------------------ exhaust

        private static GameObject BuildFirePrefab(Materials m)
        {
            var root = new GameObject("RocketFire_Cartoon");

            // A bright blob that stays glued to the nozzle, so the flame always starts at the rocket.
            ParticleSystem core = NewSystem(root.transform, "NozzleCore");
            var coreMain = core.main;
            coreMain.loop = true;
            coreMain.duration = 1f;
            coreMain.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            coreMain.startSpeed = 0f;
            coreMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.36f);
            coreMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            coreMain.maxParticles = 4;
            SetRate(core, 30f, 0f);
            DisableShape(core);
            SetColorOverLifetime(core, new[] { CK(ExhaustWhite, 0f), CK(ExhaustLightYellow, 1f) }, new[] { AK(1f, 0f), AK(0.6f, 1f) });
            SetBillboard(core, m.Blob, sortingOrder: 3);

            // The thick exhaust: big white -> yellow -> gold -> orange blobs left behind in world space, living ~0.12 s
            // (about one rocket length at flight speed), swelling a touch before snapping away.
            ParticleSystem exhaust = NewSystem(root.transform, "MainExhaust");
            var exhaustMain = exhaust.main;
            exhaustMain.loop = true;
            exhaustMain.duration = 1f;
            exhaustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            exhaustMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            exhaustMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.42f);
            exhaustMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            exhaustMain.simulationSpace = ParticleSystemSimulationSpace.World;
            exhaustMain.maxParticles = 16;
            SetRate(exhaust, 12f, 12f);
            var exhaustShape = exhaust.shape;
            exhaustShape.enabled = true;
            exhaustShape.shapeType = ParticleSystemShapeType.Cone;
            exhaustShape.angle = 8f;
            exhaustShape.radius = 0.05f;
            SetSizeCurve(exhaust, Key(0f, 0.9f), Key(0.35f, 1.1f), Key(1f, 0.15f));
            SetColorOverLifetime(exhaust,
                new[] { CK(ExhaustWhite, 0f), CK(ExhaustLightYellow, 0.35f), CK(ExhaustGold, 0.7f), CK(ExhaustOrange, 1f) },
                new[] { AK(1f, 0f), AK(0.85f, 0.35f), AK(0.45f, 0.7f), AK(0f, 1f) });
            SetBillboard(exhaust, m.Blob, sortingOrder: 2);

            // A few small fire dots dropping off behind.
            ParticleSystem embers = NewSystem(root.transform, "ExhaustEmbers");
            var emberMain = embers.main;
            emberMain.loop = true;
            emberMain.duration = 1f;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            emberMain.startColor = new ParticleSystem.MinMaxGradient(ExhaustLightYellow, EmberPaleOrange);
            emberMain.gravityModifier = 0.4f;
            emberMain.simulationSpace = ParticleSystemSimulationSpace.World;
            emberMain.maxParticles = 8;
            SetRate(embers, 16f, 0f);
            var emberShape = embers.shape;
            emberShape.enabled = true;
            emberShape.shapeType = ParticleSystemShapeType.Cone;
            emberShape.angle = 35f;
            emberShape.radius = 0.05f;
            SetSizeCurve(embers, Key(0f, 1f), Key(1f, 0.2f));
            SetBillboard(embers, m.Blob, sortingOrder: 1);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FirePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ApplyToRocket(GameObject firePrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RocketPrefabPath);
            try
            {
                foreach (string oldName in new[] { "Exhaust", "RocketFire_Cartoon" })
                {
                    Transform old = root.transform.Find(oldName);
                    if (old != null)
                    {
                        Object.DestroyImmediate(old.gameObject);
                    }
                }

                var fire = (GameObject)PrefabUtility.InstantiatePrefab(firePrefab, root.transform);
                fire.name = "RocketFire_Cartoon";
                fire.transform.localPosition = new Vector3(0f, 0f, NozzleZ);
                fire.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // +Z of the effect points out of the tail
                fire.transform.localScale = Vector3.one;

                var controller = root.GetComponent<ProjectileController>();
                var so = new SerializedObject(controller);
                so.FindProperty("_trail").objectReferenceValue = null; // the blob exhaust replaces the thin trail
                SerializedProperty attached = so.FindProperty("_attachedParticles");
                ParticleSystem[] systems = fire.GetComponentsInChildren<ParticleSystem>(true);
                attached.arraySize = systems.Length;
                for (int i = 0; i < systems.Length; i++)
                {
                    attached.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, RocketPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------------------------------ explosion

        private static void BuildExplosion(Materials m)
        {
            var root = new GameObject("RocketExplosion_Cartoon");
            var systems = new List<ParticleSystem>();

            // 0.00 - white/yellow starburst.
            ParticleSystem flash = Burst(root.transform, "ImpactFlash", 1, 0f, systems);
            var flashMain = flash.main;
            flashMain.startLifetime = 0.1f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = 2.2f;
            flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            DisableShape(flash);
            SetSizeCurve(flash, Key(0f, 0.5f), Key(1f, 1.15f));
            SetColorOverLifetime(flash, White(), new[] { AK(1f, 0f), AK(1f, 0.4f), AK(0f, 1f) });
            SetBillboard(flash, m.Starburst, sortingOrder: 6);

            // 0.02 - big round yellow blast, 0.2 -> 1.0 -> 1.15.
            ParticleSystem blast = Burst(root.transform, "YellowBlast", 1, 0.02f, systems);
            var blastMain = blast.main;
            blastMain.startLifetime = 0.3f;
            blastMain.startSpeed = 0f;
            blastMain.startSize = 2f;
            DisableShape(blast);
            SetSizeCurve(blast, Key(0f, 0.2f), Key(0.35f, 1f), Key(1f, 1.15f));
            SetColorOverLifetime(blast, White(), new[] { AK(0.8f, 0f), AK(0.6f, 0.5f), AK(0f, 1f) });
            SetBillboard(blast, m.Blast, sortingOrder: 1);

            // 0.04 - two fire plumes in a V, then dark smoke puffs flying further along the same paths. Gameplay
            // camera looks along -X, so the V opens along Z (screen left/right) and leans away from the camera to
            // read taller on screen.
            var left = new Vector3(-0.25f, 0.95f, -0.45f).normalized;
            var right = new Vector3(-0.25f, 0.95f, 0.45f).normalized;
            BuildFirePlume(root.transform, "FirePlume_L", left, m, systems);
            BuildFirePlume(root.transform, "FirePlume_R", right, m, systems);
            BuildSmokePlume(root.transform, "SmokePlume_L", left, m, systems);
            BuildSmokePlume(root.transform, "SmokePlume_R", right, m, systems);

            // 0.08 - a few embers.
            ParticleSystem embers = Burst(root.transform, "Embers", 10, 0.08f, systems);
            var emberMain = embers.main;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            emberMain.gravityModifier = 0.8f;
            emberMain.startColor = new ParticleSystem.MinMaxGradient(FireCore, FireMid);
            var emberShape = embers.shape;
            emberShape.enabled = true;
            emberShape.shapeType = ParticleSystemShapeType.Sphere;
            emberShape.radius = 0.2f;
            SetSizeCurve(embers, Key(0f, 1f), Key(1f, 0.1f));
            SetBillboard(embers, m.Blob, sortingOrder: 5);

            var pooled = root.AddComponent<PooledVfx>();
            var pso = new SerializedObject(pooled);
            SerializedProperty array = pso.FindProperty("_particleSystems");
            array.arraySize = systems.Count;
            for (int i = 0; i < systems.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
            }

            pso.FindProperty("_keepWorldUpright").boolValue = true;
            pso.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPrefabPath);
            Object.DestroyImmediate(root);

            var definition = AssetDatabase.LoadAssetAtPath<VfxDefinition>(ExplosionDefinitionPath);
            if (definition == null)
            {
                Debug.LogError("[RocketCartoonVfxSetup] Missing " + ExplosionDefinitionPath);
                return;
            }

            var dso = new SerializedObject(definition);
            dso.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<PooledVfx>();
            dso.FindProperty("_lifetime").floatValue = ExplosionLifetime;
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        /// <summary>5-6 chunky fire blobs shot along one direction over 0.12 s: the earliest fly furthest, so they
        /// string out into a column that turns yellow -> orange -> red and burns out in ~0.4 s.</summary>
        private static void BuildFirePlume(Transform parent, string name, Vector3 direction, Materials m, List<ParticleSystem> collect)
        {
            ParticleSystem ps = Stream(parent, name, direction, 0.04f, 0.12f, 45f, 6, collect);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.3f;
            SetDrag(ps, 4f);
            SetSizeCurve(ps, Key(0f, 1f), Key(1f, 0.35f));
            SetColorOverLifetime(ps,
                new[] { CK(FireCore, 0f), CK(FireMid, 0.3f), CK(FireOuter, 0.6f), CK(FireDark, 1f) },
                new[] { AK(1f, 0f), AK(1f, 0.6f), AK(0f, 1f) });
            SetBillboard(ps, m.Blob, sortingOrder: 4);
        }

        /// <summary>4-5 big dark brown puffs launched a little later and faster along the same path, so they end
        /// up above the fire as a smoke column, swell 1 -> 1.3 and fade from 0.65 in under 0.8 s. Drawn under the
        /// fire so they never hide it.</summary>
        private static void BuildSmokePlume(Transform parent, string name, Vector3 direction, Materials m, List<ParticleSystem> collect)
        {
            ParticleSystem ps = Stream(parent, name, direction, 0.1f, 0.14f, 35f, 5, collect);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(SmokeBrown, SmokeNearBlack);
            main.gravityModifier = -0.4f;
            SetDrag(ps, 4f);
            SetSizeCurve(ps, Key(0f, 1f), Key(1f, 1.3f));
            SetColorOverLifetime(ps, White(), new[] { AK(0.65f, 0f), AK(0.5f, 0.5f), AK(0f, 1f) });
            SetBillboard(ps, m.Blob, sortingOrder: 2);
        }

        private static ParticleSystem Stream(Transform parent, string name, Vector3 direction, float delay, float duration,
            float rate, int max, List<ParticleSystem> collect)
        {
            ParticleSystem ps = NewSystem(parent, name);
            ps.transform.localRotation = Quaternion.LookRotation(direction, Vector3.right);
            var main = ps.main;
            main.loop = false;
            main.duration = duration;
            main.startDelay = delay;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            SetRate(ps, rate, 0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.15f;
            collect.Add(ps);
            return ps;
        }

        // ------------------------------------------------------------------------------------------ damage numbers

        private static void SetRocketPopupStyle()
        {
            foreach (string path in RocketDefinitionPaths)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(path);
                if (definition == null)
                {
                    continue;
                }

                var so = new SerializedObject(definition);
                so.FindProperty("_damagePopupStyle").enumValueIndex = (int)DamagePopupStyle.Fire;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }
        }

        private static DamagePopupView BuildPopupPrefab()
        {
            EditorFolderUtility.EnsureFolder(Path.GetDirectoryName(PopupPrefabPath).Replace('\\', '/'));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = new GameObject("DamagePopup", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.sizeDelta = Vector2.zero;

            var iconGo = new GameObject("FireIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(content.transform, false);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            var textGo = new GameObject("DamageText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(content.transform, false);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = "0";

            var view = root.AddComponent<DamagePopupView>();
            var so = new SerializedObject(view);
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_content").objectReferenceValue = contentRect;
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PopupPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<DamagePopupView>();
        }

        /// <summary>DamagePopupCanvas at the open level scene's root: its own overlay canvas one step below the gameplay
        /// Canvas (so HUD, pause and win panels draw over the numbers), same scaler, no raycaster.</summary>
        private static void SetupPopupServiceInOpenScene(DamagePopupView popupPrefab)
        {
            if (Object.FindFirstObjectByType<LevelCompositionRoot>() == null)
            {
                Debug.LogWarning("[RocketCartoonVfxSetup] Open scene is not a level; damage numbers not added to a scene.");
                return;
            }

            GameObject mainCanvasGo = GameObject.Find("Canvas");
            var mainCanvas = mainCanvasGo != null ? mainCanvasGo.GetComponent<Canvas>() : null;

            GameObject existing = GameObject.Find("DamagePopupCanvas");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var go = new GameObject("DamagePopupCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = mainCanvas != null ? mainCanvas.sortingOrder - 1 : -1;
            if (mainCanvas != null)
            {
                EditorUtility.CopySerialized(mainCanvas.GetComponent<CanvasScaler>(), go.GetComponent<CanvasScaler>());
                go.transform.SetSiblingIndex(mainCanvas.transform.GetSiblingIndex());
            }

            var service = go.AddComponent<DamagePopupService>();
            var so = new SerializedObject(service);
            so.FindProperty("_popupPrefab").objectReferenceValue = popupPrefab;
            so.FindProperty("_container").objectReferenceValue = go.transform;
            SerializedProperty styles = so.FindProperty("_styles");
            styles.arraySize = 2;

            // Rockets: orange number with the flame badge.
            SerializedProperty fire = styles.GetArrayElementAtIndex(0);
            fire.FindPropertyRelative("Type").enumValueIndex = (int)DamagePopupStyle.Fire;
            fire.FindPropertyRelative("Icon").objectReferenceValue = EnsureFireIcon();
            fire.FindPropertyRelative("TopColor").colorValue = DamageTop;
            fire.FindPropertyRelative("BottomColor").colorValue = DamageBottom;
            fire.FindPropertyRelative("OutlineColor").colorValue = DamageOutline;
            fire.FindPropertyRelative("OutlineWidth").floatValue = 0.3f;
            fire.FindPropertyRelative("FontSize").floatValue = 46f;

            // Plain shells (Mortar): a smaller white number, no icon, so fire damage still stands out.
            SerializedProperty kinetic = styles.GetArrayElementAtIndex(1);
            kinetic.FindPropertyRelative("Type").enumValueIndex = (int)DamagePopupStyle.Kinetic;
            kinetic.FindPropertyRelative("Icon").objectReferenceValue = null;
            kinetic.FindPropertyRelative("TopColor").colorValue = Color.white;
            kinetic.FindPropertyRelative("BottomColor").colorValue = KineticBottom;
            kinetic.FindPropertyRelative("OutlineColor").colorValue = KineticOutline;
            kinetic.FindPropertyRelative("OutlineWidth").floatValue = 0.3f;
            kinetic.FindPropertyRelative("FontSize").floatValue = 40f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorSceneManager.SaveScene(go.scene);
        }

        // ------------------------------------------------------------------------------------------ particle helpers

        private static ParticleSystem NewSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.prewarm = false;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = Color.white;
            return ps;
        }

        private static ParticleSystem Burst(Transform parent, string name, short count, float delay, List<ParticleSystem> collect)
        {
            ParticleSystem ps = NewSystem(parent, name);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.5f;
            main.startDelay = delay;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            collect.Add(ps);
            return ps;
        }

        private static void SetRate(ParticleSystem ps, float perSecond, float perMetre)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = perSecond;
            emission.rateOverDistance = perMetre;
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        private static void DisableShape(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = false;
        }

        private static void SetDrag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 50f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;     // plain linear drag: big puffs must not stop dead
            limit.multiplyDragByParticleVelocity = false;
        }

        private static void SetSizeCurve(ParticleSystem ps, params Keyframe[] keys)
        {
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            float max = 0f;
            foreach (Keyframe k in keys)
            {
                max = Mathf.Max(max, k.value);
            }

            // MinMaxCurve scales a 0..1 curve by its multiplier: normalise so values above 1 work.
            var normalised = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                normalised[i] = new Keyframe(keys[i].time, keys[i].value / max);
            }

            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(max, new AnimationCurve(normalised));
        }

        private static void SetColorOverLifetime(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetBillboard(ParticleSystem ps, Material material, int sortingOrder)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
        }

        private static GradientColorKey[] White() => new[] { CK(Color.white, 0f), CK(Color.white, 1f) };
        private static Keyframe Key(float time, float value) => new Keyframe(time, value);
        private static GradientColorKey CK(Color color, float time) => new GradientColorKey(color, time);
        private static GradientAlphaKey AK(float alpha, float time) => new GradientAlphaKey(alpha, time);

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        // ------------------------------------------------------------------------------------------ assets

        /// <summary>URP Particles/Unlit, alpha blended, soft particles off - particle colour does the rest.</summary>
        private static Material EnsureMaterial(string name, Texture2D texture)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_SOFTPARTICLES_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Texture2D EnsureTexture(string name, int width, int height, Func<float, float, Color> pixel, bool sprite)
        {
            return EnsureTextureAt(TextureFolder + "/" + name + ".png", width, height, pixel, sprite);
        }

        private static Texture2D EnsureTextureAt(string path, int width, int height, Func<float, float, Color> pixel, bool sprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, pixel((x + 0.5f) / width, (y + 0.5f) / height));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            // The project's texture preset imports PNGs as multi-sprites; set exactly what each one is.
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite EnsureFireIcon()
        {
            EnsureTextureAt(FireIconPath, 128, 128, FireIconPixel, sprite: true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(FireIconPath);
        }

        private static float Polar(float u, float v, out float angle)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            angle = Mathf.Atan2(dy, dx);
            return Mathf.Sqrt(dx * dx + dy * dy) * 2f;
        }

        /// <summary>A slightly lumpy solid disc with a thin soft rim - reads as a cartoon puff, not fog.</summary>
        private static Color BlobPixel(float u, float v)
        {
            float r = Polar(u, v, out float angle);
            float edge = 0.9f + 0.05f * Mathf.Sin(angle * 5f) + 0.03f * Mathf.Sin(angle * 3f + 1.3f);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.1f, edge, r));
            return new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>Radial starburst: white core, eight long and eight short spikes fading to yellow.</summary>
        private static Color StarburstPixel(float u, float v)
        {
            float r = Polar(u, v, out float angle);
            float longSpikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 18f);
            float shortSpikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f + Mathf.PI * 0.25f)), 18f) * 0.6f;
            float reach = 0.32f + Mathf.Max(longSpikes, shortSpikes) * 0.66f;
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach - 0.12f, reach, r));
            Color color = Color.Lerp(FlashCenter, FlashOuter, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, r)));
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>Round cartoon blast with a wavy edge, pale yellow centre to golden rim.</summary>
        private static Color BlastPixel(float u, float v)
        {
            float r = Polar(u, v, out float angle);
            float edge = 0.92f + 0.05f * Mathf.Sin(angle * 7f);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.06f, edge, r));
            Color color = Color.Lerp(BlastCenter, BlastOuter, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, r)));
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>Stylised flame icon: dark outline, red-orange body, yellow inner flame.</summary>
        private static Color FireIconPixel(float u, float v)
        {
            float outer = FlameField(u, v, 0.5f, 0.12f, 1f);
            float inner = FlameField(u, v, 0.5f, 0.2f, 0.55f);
            const float outline = 0.09f;

            if (outer < -outline)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            if (outer < 0f)
            {
                return DamageOutline;
            }

            Color body = Color.Lerp(DamageBottom, Hex(0xFF8A2A), Mathf.InverseLerp(0.12f, 0.8f, v));
            if (inner >= 0f)
            {
                body = Color.Lerp(Hex(0xFFE27A), DamageTop, Mathf.InverseLerp(0.2f, 0.6f, v));
            }

            return body;
        }

        /// <summary>Positive inside a teardrop flame: a round base at (cx, baseY + 0.28 s) tapering to a tip bent
        /// slightly sideways at the top. Units are roughly texture UV.</summary>
        private static float FlameField(float u, float v, float cx, float baseY, float scale)
        {
            float radius = 0.28f * scale;
            float centreY = baseY + radius;
            float tipY = baseY + 0.78f * scale;
            if (v < baseY - 0.04f || v > tipY + 0.04f)
            {
                return -1f; // above the tip / below the base: outside, not a sliver of outline running off
            }

            float t = Mathf.Clamp01((v - centreY) / Mathf.Max(0.001f, tipY - centreY));
            float width = v < centreY
                ? Mathf.Sqrt(Mathf.Max(0f, radius * radius - (v - centreY) * (v - centreY)))
                : radius * (1f - t) * (1f - 0.35f * t);
            float bend = Mathf.Sin(t * Mathf.PI * 0.9f) * 0.06f * scale;
            float dx = Mathf.Abs(u - (cx + bend));
            return (width - dx) * 2f;
        }
    }
}
