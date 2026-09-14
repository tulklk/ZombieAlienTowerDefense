using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds standalone, ready-to-use prefabs out of the 4 new zombie variants dropped into
    /// Assets/_Game/Models/Zombie/Prefabs/1..4 (each folder: a static T-pose OBJ + textures from "Tripo AI",
    /// and a separately Mixamo-auto-rigged "Walk*.fbx" with the SAME mesh, a "mixamorig:*" skeleton, and one
    /// embedded "mixamo.com" walk clip). Unlike the old CC3 pipeline (ZombiePrefabBuilder), each variant here
    /// is already fully and correctly self-rigged/skinned/animated by Mixamo — no bone remapping or procedural
    /// animation authoring needed, just three real import bugs to fix (see BuildOneVariant) plus assembling the
    /// prefab and wiring it onto the existing Enemy_* prefabs (whose old CC3 zombie visuals were deleted by the
    /// user alongside the old Prefabs/*.prefab files, leaving Enemy_Normal/Runner/Tank/Armored/Shield/Boss with
    /// a dangling "Missing Prefab" Model — see ApplyVariantVisualToEnemy, which self-heals that same as
    /// ZombiePrefabBuilder's fixed lookup does).</summary>
    public static class ZombieVariantBuilder
    {
        private const string RootFolder = "Assets/_Game/Models/Zombie/Prefabs";
        private const string EnemyPrefabFolder = "Assets/_Game/Prefabs/Enemies";

        /// <summary>Uniform size bump applied to every variant's standalone prefab (see BuildStandalonePrefab) —
        /// the raw ~1-unit-tall import read as too small once actually seen in Level_01.</summary>
        private const float CharacterScale = 1.8f;

        private struct Variant
        {
            public int Number;
            /// <summary>Folder under Prefabs/ — defaults to Number.ToString(); use "Boss" for the boss pack.</summary>
            public string FolderKey;
            public string FbxFileName;
            public string ObjFolderName;
            public string ObjBaseName;
            public string DisplayName;
            public string AttackFbxFileName;
            /// <summary>When set, remap the walk FBX to this existing Materials/{name}.mat instead of OBJ textures.</summary>
            public string ExistingMaterialName;

            public string Folder => string.IsNullOrEmpty(FolderKey) ? Number.ToString() : FolderKey;

            public Variant(int number, string fbxFileName, string objFolderName, string objBaseName, string displayName, string attackFbxFileName,
                string folderKey = null, string existingMaterialName = null)
            {
                Number = number;
                FolderKey = folderKey;
                FbxFileName = fbxFileName;
                ObjFolderName = objFolderName;
                ObjBaseName = objBaseName;
                DisplayName = displayName;
                AttackFbxFileName = attackFbxFileName;
                ExistingMaterialName = existingMaterialName;
            }
        }

        private static readonly Variant[] Variants =
        {
            new Variant(1, "Walk1.fbx", "zombie character 3d model", "zombie+character+3d+model", "ZombieVariant1", "Attack1.fbx"),
            new Variant(2, "Walking2.fbx", "zom2", "zom2", "ZombieVariant2", "Attack2.fbx"),
            new Variant(3, "Walking3.fbx", "zomb3", "zomb3", "ZombieVariant3", "Attack3.fbx"),
            new Variant(4, "Walking5.fbx", "zomb5", "zomb5", "ZombieVariant4", "Attack4.fbx"),
        };

        /// <summary>Folder 5 / 7 / Boss — replace duplicate Enemy_Shield / Armored / Boss visuals.</summary>
        private static readonly Variant[] ExtendedVariants =
        {
            new Variant(5, "Walking5.fbx", "zom5", null, "ZombieVariant5", "Attack5.fbx",
                existingMaterialName: "ZombieVariant5_Mat"),
            new Variant(7, "Walking7.fbx", "zomb7", "zomb7", "ZombieVariant7", "Attack7.fbx"),
            new Variant(0, "BossWalk.fbx", "bossmodel", null, "BossVariant", "BossAttack.fbx",
                folderKey: "Boss", existingMaterialName: "BossModel_Mat"),
        };

        private static IEnumerable<Variant> AllVariants => Variants.Concat(ExtendedVariants);

        /// <summary>Convenience wrapper for the menu item — runs both phases back to back. When driving this
        /// from an external script-execution tool (RunCommand-style, one Editor tick per call), call
        /// BuildImportsAndControllers() and BuildPrefabsAndWireEnemies() as two SEPARATE invocations instead —
        /// see BuildPrefabsAndWireEnemies' doc comment for why that separation is what actually fixes the
        /// animator-drops-null bug, not just the retry loops below (which help, but weren't sufficient alone,
        /// confirmed directly by re-running this exact combined method and still reading nulls back).</summary>
        [MenuItem("AlienDefense/Setup/7. Build New Zombie Variants (Tripo+Mixamo)")]
        public static void BuildAll()
        {
            BuildImportsAndControllers();
            BuildPrefabsAndWireEnemies();
        }

        /// <summary>Phase A: fixes each variant's FBX import settings (scale, walk-clip loop) and material,
        /// and builds/saves its walk-only AnimatorController. Deliberately does NOT touch prefabs yet — see
        /// BuildPrefabsAndWireEnemies.</summary>
        [MenuItem("AlienDefense/Setup/7a. Build New Zombie Variants - Step 1 (Imports+Controllers)")]
        public static void BuildImportsAndControllers()
        {
            foreach (var variant in Variants)
            {
                BuildOneVariantImportsAndController(variant);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZombieVariantBuilder] Step 1 done: fixed imports/materials and built " + Variants.Length + " AnimatorControllers.");
        }

        private static void BuildOneVariantImportsAndController(Variant variant)
        {
            string variantFolder = RootFolder + "/" + variant.Folder;
            string fbxPath = variantFolder + "/" + variant.FbxFileName;
            string objFolder = string.IsNullOrEmpty(variant.ObjFolderName)
                ? null
                : variantFolder + "/" + variant.ObjFolderName;

            var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Missing FBX: " + fbxPath);
                return;
            }

            // Bug 1: the FBX reports fileScale=0.01 ("1 unit = 1cm") and Unity applies it on import
            // (useFileScale=true by default), shrinking the character to ~0.01 units tall — confirmed
            // directly (SkinnedMeshRenderer.bounds.size was (0.01, 0.01, 0.01) before this fix) and confirmed
            // wrong by comparing against the sibling OBJ in the same folder (same mesh, same "tripo_node_*"
            // GUID, no unit metadata so Unity imports its raw vertex coordinates 1:1): the OBJ comes in at
            // ~1 unit tall, matching this project's existing enemy scale convention (Enemy_Normal's
            // CapsuleCollider is 0.9 units tall). Ignoring the FBX's (wrong, for our use) declared scale
            // brings it back to the same ~1-unit range as the OBJ and the rest of the game's enemies.
            modelImporter.useFileScale = false;

            // Bug 2: the walk clip imports with looping OFF by default (Mixamo's export convention), which
            // would freeze on its last frame instead of cycling. Only one clip ("mixamo.com") exists per file.
            var clips = modelImporter.defaultClipAnimations;
            if (clips.Length > 0)
            {
                ModelImporterClipAnimation clip = clips[0];
                clip.loopTime = true;
                clip.loopPose = true;
                modelImporter.clipAnimations = new[] { clip };
            }

            AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            // Bug 3: the FBX's embedded material has no textures (Mixamo's re-export drops the original PBR
            // textures entirely — confirmed directly, _BaseMap/_BumpMap both read back null before this fix).
            // Folder 5/Boss already have extracted URP mats from Fix Tripo Textures; folder 7 uses OBJ textures.
            if (!string.IsNullOrEmpty(variant.ExistingMaterialName))
            {
                RemapToExistingMaterial(fbxPath, variantFolder, variant.ExistingMaterialName);
            }
            else
            {
                FixMaterial(fbxPath, objFolder, variant.ObjBaseName, variant.DisplayName, variantFolder);
            }

            // Picking the walk clip by "first AnimationClip sub-asset found" was the actual bug behind the
            // zombies standing frozen instead of looping: Unity's ModelImporter also leaves a SEPARATE,
            // non-looping "__preview__mixamo.com" clip sub-asset alongside the real "mixamo.com" one (an
            // internal preview-panel artifact, confirmed directly — it reports isLooping=False/length=1s
            // regardless of the real clip's own loopTime=True set above), and LoadAllAssetsAtPath's ordering
            // isn't guaranteed to put the real clip first. The AnimatorController ended up playing that dead
            // preview clip once and holding on its last frame. Filtering it out by name gets the real clip.
            AnimationClip walkClip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (walkClip == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] No non-preview animation clip found in " + fbxPath);
                return;
            }

            BuildWalkOnlyController(variantFolder, variant.DisplayName, walkClip);
        }

        private static void RemapToExistingMaterial(string fbxPath, string variantFolder, string materialAssetName)
        {
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            Material embedded = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().FirstOrDefault();
            if (embedded == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] No embedded material in " + fbxPath);
                return;
            }

            string matPath = variantFolder + "/Materials/" + materialAssetName + ".mat";
            Material extracted = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (extracted == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Existing material not found: " + matPath +
                                 " (run Fix Tripo Textures first).");
                return;
            }

            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), extracted);
            modelImporter.SaveAndReimport();
        }

        /// <summary>Phase B: builds the standalone prefab for each variant and wires it onto its assigned
        /// Enemy_* prefab. Must run as a genuinely separate top-level call from BuildImportsAndControllers
        /// (a fresh Editor tick later, e.g. a second RunCommand invocation) — NOT merely later in the same
        /// method/frame: PrefabUtility.SaveAsPrefabAsset silently drops a RuntimeAnimatorController assigned
        /// to a controller asset created moments earlier in that same call (confirmed directly and repeatedly —
        /// AssetDatabase.Refresh() + a same-call retry loop were NOT enough to reliably avoid it, unlike the
        /// working CC3 pipeline in ZombiePrefabBuilder, which builds its shared controller once, long before
        /// any prefab references it). Loading each controller fresh from disk here, in its own call, is what
        /// actually fixes it — this method still keeps a defensive retry loop per prefab on top of that.</summary>
        [MenuItem("AlienDefense/Setup/7b. Build New Zombie Variants - Step 2 (Prefabs)")]
        public static void BuildPrefabsAndWireEnemies()
        {
            foreach (var variant in Variants)
            {
                string variantFolder = RootFolder + "/" + variant.Folder;
                string fbxPath = variantFolder + "/" + variant.FbxFileName;
                string controllerPath = variantFolder + "/" + variant.DisplayName + "_Walk.controller";

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    Debug.LogWarning("[ZombieVariantBuilder] Controller not found: " + controllerPath + " (run Step 1 first).");
                    continue;
                }

                BuildStandalonePrefab(fbxPath, variantFolder, variant.DisplayName, controller);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZombieVariantBuilder] Step 2 done: built " + Variants.Length + " standalone variant prefabs.");
        }

        private static void FixMaterial(string fbxPath, string objFolder, string objBaseName, string displayName, string variantFolder)
        {
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            Material embedded = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().FirstOrDefault();
            if (embedded == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] No embedded material in " + fbxPath);
                return;
            }

            string baseColorPath = objFolder + "/" + objBaseName + "_basecolor.jpg";
            string normalPath = objFolder + "/" + objBaseName + "_normal.jpg";

            // Normal maps import as a plain color texture by default (textureType=Default), which reads back
            // washed-out/wrong once fed into a shader's normal-map sampler — must be flagged NormalMap so
            // Unity applies the correct tangent-space unpacking.
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            Texture baseColorTex = AssetDatabase.LoadAssetAtPath<Texture>(baseColorPath);
            Texture normalTex = AssetDatabase.LoadAssetAtPath<Texture>(normalPath);

            // Extract to a real standalone .mat (matching how Assets/ArtStore3D/Zombie/Materials/Zombie_Mat.mat
            // was already fixed in this project) instead of editing the FBX's embedded material in place: an
            // embedded material gets silently regenerated from the FBX's own material description on the next
            // reimport, which would wipe these texture assignments.
            string matFolder = variantFolder + "/Materials";
            EditorFolderUtility.EnsureFolder(matFolder);
            string matPath = matFolder + "/" + displayName + "_Mat.mat";

            Material extracted = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (extracted == null)
            {
                extracted = new Material(embedded);
                AssetDatabase.CreateAsset(extracted, matPath);
            }
            else
            {
                extracted.shader = embedded.shader;
            }

            extracted.SetTexture("_BaseMap", baseColorTex);
            if (normalTex != null)
            {
                extracted.SetTexture("_BumpMap", normalTex);
                extracted.EnableKeyword("_NORMALMAP");
            }
            // Mild, non-shiny default — the source Standard-shader-style "_rm" (roughness/metallic) texture's
            // channel packing wasn't verified against URP's expected R=metallic/A=smoothness layout, so it's
            // deliberately not wired up here (risk of a wrong-channel shiny/patchy look outweighs the benefit
            // for this stylized/cartoon art). 0.35 keeps URP/Lit's plasticky default (0.5) from standing out.
            if (extracted.HasProperty("_Smoothness"))
            {
                extracted.SetFloat("_Smoothness", 0.35f);
            }

            EditorUtility.SetDirty(extracted);
            AssetDatabase.SaveAssets();

            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), extracted);
            modelImporter.SaveAndReimport();
        }

        private const string Zom5TripoFbxPath =
            RootFolder + "/5/zom5/tripo_convert_2d217258-1158-420c-953b-0a278db197cf.fbx";
        private const string BossTripoFbxPath =
            RootFolder + "/Boss/bossmodel/tripo_convert_2b99b395-9234-465e-9cf3-2dd32c4de928.fbx";
        private const string FixTripoTexturesRequestPath = "Assets/_Game/EditorReports/FixTripoTextures.request";

        [InitializeOnLoadMethod]
        private static void AutoRunFixTripoIfRequested()
        {
            if (!File.Exists(FixTripoTexturesRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(FixTripoTexturesRequestPath))
                {
                    return;
                }

                try
                {
                    File.Delete(FixTripoTexturesRequestPath);
                    string meta = FixTripoTexturesRequestPath + ".meta";
                    if (File.Exists(meta))
                    {
                        File.Delete(meta);
                    }
                }
                catch
                {
                    // ignore
                }

                FixTripoTexturesZombie5AndBoss();
            };
        }

        /// <summary>Fixes Tripo FBX models whose PBR textures live in a sibling .fbm folder.
        /// Note: folder 5's Walking5 Mixamo mesh must NOT use the static Tripo BaseColor (UV mismatch) —
        /// use FixZombieVariant5MixamoTextures for that. This only remaps the static zom5 / bossmodel FBXs.</summary>
        [MenuItem("AlienDefense/Setup/Fix Tripo Textures (Zombie 5 + Boss)")]
        public static void FixTripoTexturesZombie5AndBoss()
        {
            // Static Tripo FBX only — do not overwrite ZombieVariant5_Mat (used by Walking5 Mixamo mesh).
            FixTripoFbmMaterial(Zom5TripoFbxPath, "Zom5Static", RootFolder + "/5");
            FixTripoFbmMaterial(BossTripoFbxPath, "BossModel", RootFolder + "/Boss");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZombieVariantBuilder] Tripo texture fix applied to static zom5 FBX and Boss (bossmodel).");
        }

        /// <summary>Fixes ZombieVariant5 Walk mesh: Walking5.fbx expects Mixamo companion textures
        /// (zomb6_basecolor.jpg), not the static Tripo NewUVMap BaseColor.png.
        /// If ExtractTextures cannot produce zomb6_*, remaps Mixamo mesh UVs to the static Tripo NewUVMap
        /// so the existing .fbm BaseColor/Normal can be used correctly.</summary>
        [MenuItem("AlienDefense/Setup/Fix ZombieVariant5 Mixamo Textures")]
        public static void FixZombieVariant5MixamoTextures()
        {
            const string variantFolder = RootFolder + "/5";
            const string walkFbxPath = variantFolder + "/Walking5.fbx";
            const string attackFbxPath = variantFolder + "/Attack5.fbx";
            const string texturesFolder = variantFolder + "/Textures";
            const string matName = "ZombieVariant5_Mat";
            const string fbmFolder = variantFolder + "/zom5/tripo_convert_2d217258-1158-420c-953b-0a278db197cf.fbm";

            var walkImporter = AssetImporter.GetAtPath(walkFbxPath) as ModelImporter;
            if (walkImporter == null)
            {
                Debug.LogError("[ZombieVariantBuilder] Missing Walking5.fbx at " + walkFbxPath);
                return;
            }

            EditorFolderUtility.EnsureFolder(texturesFolder);

            // Extract embedded / referenced textures next to the Mixamo FBX.
            walkImporter.ExtractTextures(texturesFolder);
            AssetDatabase.Refresh();

            // Only accept Mixamo companion textures — never the Tripo .fbm BaseColor (wrong UV layout).
            string baseColorPath = FindTextureInFolderFlexible(texturesFolder,
                new[] { "zomb6_basecolor", "basecolor", "BaseColor", "diffuse", "Diffuse", "albedo", "Albedo" });
            string normalPath = FindTextureInFolderFlexible(texturesFolder,
                new[] { "normal", "Normal", "Normal_Bake", "nrm" });

            bool usedUvRemapFallback = false;
            if (string.IsNullOrEmpty(baseColorPath))
            {
                // External Mixamo jpg was never shipped with the FBX — align Walking/Attack UVs to Tripo NewUVMap.
                string uvReport;
                if (!TryRemapVariant5MixamoUvsToTripo(walkFbxPath, attackFbxPath, variantFolder, out uvReport))
                {
                    string msg =
                        "[ZombieVariantBuilder] Could not find Mixamo basecolor for Walking5 and UV remap failed. " +
                        "Drop zomb6_basecolor.jpg (from the Mixamo download) into " + texturesFolder +
                        " then re-run this menu.\n" + uvReport;
                    Debug.LogError(msg);
                    WriteVariant5TextureReport("missing_basecolor_and_uv_remap_failed\n" + uvReport, texturesFolder);
                    return;
                }

                usedUvRemapFallback = true;
                baseColorPath = FindTextureInFolderFlexible(fbmFolder, new[] { "BaseColor", "basecolor" });
                normalPath = FindTextureInFolderFlexible(fbmFolder, new[] { "Normal_Bake", "Normal", "normal" });
                WriteVariant5TextureReport("uv_remap_fallback\n" + uvReport, texturesFolder);
                Debug.Log("[ZombieVariantBuilder] Mixamo zomb6 texture missing — applied Tripo NewUVMap onto Walking5/Attack5. " + uvReport);
            }

            if (string.IsNullOrEmpty(baseColorPath))
            {
                Debug.LogError("[ZombieVariantBuilder] No basecolor path resolved for ZombieVariant5.");
                WriteVariant5TextureReport("missing_basecolor_after_fallback", texturesFolder);
                return;
            }

            EnsureNormalMapImport(normalPath, isNormalMap: true);
            EnsureNormalMapImport(baseColorPath, isNormalMap: false);

            Texture baseColorTex = AssetDatabase.LoadAssetAtPath<Texture>(baseColorPath);
            Texture normalTex = string.IsNullOrEmpty(normalPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(normalPath);

            if (baseColorTex == null)
            {
                Debug.LogError("[ZombieVariantBuilder] Failed to load basecolor at " + baseColorPath);
                return;
            }

            string matFolder = variantFolder + "/Materials";
            EditorFolderUtility.EnsureFolder(matFolder);
            string matPath = matFolder + "/" + matName + ".mat";

            Material embedded = AssetDatabase.LoadAllAssetsAtPath(walkFbxPath).OfType<Material>().FirstOrDefault();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = embedded != null ? new Material(embedded) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (embedded != null)
            {
                mat.shader = embedded.shader != null
                    ? embedded.shader
                    : Shader.Find("Universal Render Pipeline/Lit");
            }

            mat.SetTexture("_BaseMap", baseColorTex);
            mat.SetTexture("_MainTex", baseColorTex);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.35f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            RemapFbxToMaterial(walkFbxPath, mat);
            if (AssetImporter.GetAtPath(attackFbxPath) != null)
            {
                RemapFbxToMaterial(attackFbxPath, mat);
            }

            // Refresh standalone prefab: materials + remapped mesh assets (if UV fallback created them).
            string prefabPath = variantFolder + "/ZombieVariant5.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                Mesh remappedWalk = AssetDatabase.LoadAssetAtPath<Mesh>(variantFolder + "/ZombieVariant5_RemappedMesh.asset");
                foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = mat;
                    }

                    renderer.sharedMaterials = mats;

                    if (usedUvRemapFallback && remappedWalk != null)
                    {
                        if (renderer is SkinnedMeshRenderer smr)
                        {
                            smr.sharedMesh = remappedWalk;
                        }
                        else if (renderer is MeshRenderer)
                        {
                            var filter = renderer.GetComponent<MeshFilter>();
                            if (filter != null)
                            {
                                filter.sharedMesh = remappedWalk;
                            }
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteVariant5TextureReport(
                "ok\nmode=" + (usedUvRemapFallback ? "uv_remap_to_tripo" : "mixamo_extract") +
                "\nbaseColor=" + baseColorPath + "\nnormal=" + (normalPath ?? "none") + "\nmat=" + matPath,
                texturesFolder);
            Debug.Log("[ZombieVariantBuilder] ZombieVariant5 textures fixed. BaseColor=" + baseColorPath +
                      " mode=" + (usedUvRemapFallback ? "uv_remap_to_tripo" : "mixamo_extract"));
        }

        /// <summary>Copies Tripo static NewUVMap onto Mixamo Walking5/Attack5 meshes so .fbm BaseColor matches.</summary>
        private static bool TryRemapVariant5MixamoUvsToTripo(
            string walkFbxPath, string attackFbxPath, string variantFolder, out string report)
        {
            report = "";
            EnsureModelReadable(walkFbxPath, true);
            EnsureModelReadable(Zom5TripoFbxPath, true);
            if (AssetImporter.GetAtPath(attackFbxPath) != null)
            {
                EnsureModelReadable(attackFbxPath, true);
            }

            Mesh staticMesh = AssetDatabase.LoadAllAssetsAtPath(Zom5TripoFbxPath).OfType<Mesh>()
                .OrderByDescending(m => m.vertexCount)
                .FirstOrDefault();
            Mesh walkMesh = AssetDatabase.LoadAllAssetsAtPath(walkFbxPath).OfType<Mesh>()
                .OrderByDescending(m => m.vertexCount)
                .FirstOrDefault();

            if (staticMesh == null || walkMesh == null)
            {
                report = "staticMesh=" + (staticMesh != null) + " walkMesh=" + (walkMesh != null);
                return false;
            }

            Vector2[] staticUv = staticMesh.uv;
            if (staticUv == null || staticUv.Length != staticMesh.vertexCount)
            {
                report = "static mesh missing uv0 (count=" + (staticUv != null ? staticUv.Length : 0) +
                         ", verts=" + staticMesh.vertexCount + ")";
                return false;
            }

            string mode;
            Vector2[] walkUv = BuildTransferredUvs(walkMesh, staticMesh, out mode);
            if (walkUv == null)
            {
                report = "uv transfer failed walkVerts=" + walkMesh.vertexCount +
                         " staticVerts=" + staticMesh.vertexCount;
                return false;
            }

            string walkMeshPath = variantFolder + "/ZombieVariant5_RemappedMesh.asset";
            SaveRemappedSkinnedMesh(walkMesh, walkUv, walkMeshPath);

            string attackNote = "attack=skipped";
            if (AssetImporter.GetAtPath(attackFbxPath) != null)
            {
                Mesh attackMesh = AssetDatabase.LoadAllAssetsAtPath(attackFbxPath).OfType<Mesh>()
                    .OrderByDescending(m => m.vertexCount)
                    .FirstOrDefault();
                if (attackMesh != null)
                {
                    string attackMode;
                    Vector2[] attackUv = BuildTransferredUvs(attackMesh, staticMesh, out attackMode);
                    if (attackUv != null)
                    {
                        SaveRemappedSkinnedMesh(attackMesh, attackUv, variantFolder + "/ZombieVariant5_Attack_RemappedMesh.asset");
                        attackNote = "attack=" + attackMode + " verts=" + attackMesh.vertexCount;
                    }
                    else
                    {
                        attackNote = "attack=transfer_failed verts=" + attackMesh.vertexCount;
                    }
                }
            }

            report = "mode=" + mode +
                     " walkVerts=" + walkMesh.vertexCount +
                     " staticVerts=" + staticMesh.vertexCount +
                     " " + attackNote +
                     " meshAsset=" + walkMeshPath;
            return true;
        }

        private static void EnsureModelReadable(string fbxPath, bool readable)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            if (importer.isReadable == readable)
            {
                return;
            }

            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        private static Vector2[] BuildTransferredUvs(Mesh dst, Mesh src, out string mode)
        {
            mode = null;
            if (dst.vertexCount == src.vertexCount)
            {
                mode = "exact_vertex_order";
                return (Vector2[])src.uv.Clone();
            }

            // Spatial nearest-neighbour in normalized local bounds (Mixamo vs Tripo scale/orientation).
            Vector3[] srcPos = NormalizePositions(src.vertices);
            Vector3[] dstPos = NormalizePositions(dst.vertices);
            Vector2[] srcUv = src.uv;
            var result = new Vector2[dstPos.Length];

            const int grid = 48;
            var buckets = new List<int>[grid * grid * grid];
            for (int j = 0; j < srcPos.Length; j++)
            {
                int key = GridKey(srcPos[j], grid);
                if (buckets[key] == null)
                {
                    buckets[key] = new List<int>(4);
                }

                buckets[key].Add(j);
            }

            for (int i = 0; i < dstPos.Length; i++)
            {
                Vector3 p = dstPos[i];
                int best = 0;
                float bestDist = float.MaxValue;

                int cx = Mathf.Clamp(Mathf.FloorToInt(p.x * (grid - 1e-4f)), 0, grid - 1);
                int cy = Mathf.Clamp(Mathf.FloorToInt(p.y * (grid - 1e-4f)), 0, grid - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt(p.z * (grid - 1e-4f)), 0, grid - 1);

                for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                for (int oz = -1; oz <= 1; oz++)
                {
                    int x = cx + ox;
                    int y = cy + oy;
                    int z = cz + oz;
                    if (x < 0 || y < 0 || z < 0 || x >= grid || y >= grid || z >= grid)
                    {
                        continue;
                    }

                    List<int> bucket = buckets[x + y * grid + z * grid * grid];
                    if (bucket == null)
                    {
                        continue;
                    }

                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int j = bucket[b];
                        float d = (srcPos[j] - p).sqrMagnitude;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = j;
                        }
                    }
                }

                // Rare empty-neighbourhood fallback: scan all (should be uncommon).
                if (bestDist == float.MaxValue)
                {
                    for (int j = 0; j < srcPos.Length; j++)
                    {
                        float d = (srcPos[j] - p).sqrMagnitude;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = j;
                        }
                    }
                }

                result[i] = srcUv[best];
            }

            mode = "spatial_nn_grid";
            return result;
        }

        private static int GridKey(Vector3 p, int grid)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(p.x * (grid - 1e-4f)), 0, grid - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(p.y * (grid - 1e-4f)), 0, grid - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(p.z * (grid - 1e-4f)), 0, grid - 1);
            return x + y * grid + z * grid * grid;
        }

        private static Vector3[] NormalizePositions(Vector3[] raw)
        {
            if (raw == null || raw.Length == 0)
            {
                return raw;
            }

            Bounds b = new Bounds(raw[0], Vector3.zero);
            for (int i = 1; i < raw.Length; i++)
            {
                b.Encapsulate(raw[i]);
            }

            Vector3 size = b.size;
            float maxAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (maxAxis < 1e-5f)
            {
                maxAxis = 1f;
            }

            var normalized = new Vector3[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                Vector3 t = raw[i] - b.min;
                normalized[i] = new Vector3(t.x / maxAxis, t.y / maxAxis, t.z / maxAxis);
            }

            return normalized;
        }

        private static void SaveRemappedSkinnedMesh(Mesh source, Vector2[] uv, string assetPath)
        {
            Mesh copy = UnityEngine.Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(assetPath);
            copy.uv = uv;
            copy.UploadMeshData(false);

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                UnityEngine.Object.DestroyImmediate(copy);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(copy, assetPath);
            }

            AssetDatabase.SaveAssets();
        }

        private static void RemapFbxToMaterial(string fbxPath, Material mat)
        {
            var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter == null)
            {
                return;
            }

            modelImporter.useFileScale = false;
            Material embedded = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().FirstOrDefault();
            if (embedded == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] No embedded material to remap on " + fbxPath);
                modelImporter.SaveAndReimport();
                return;
            }

            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
            modelImporter.SaveAndReimport();
        }

        private static string FindTextureInFolderFlexible(string folder, string[] nameHints)
        {
            string absoluteFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(absoluteFolder))
            {
                return null;
            }

            string[] files = Directory.GetFiles(absoluteFolder, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
                })
                .ToArray();

            foreach (string hint in nameHints)
            {
                string match = files.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(match))
                {
                    return ToAssetsPath(match);
                }
            }

            return null;
        }

        private static string ToAssetsPath(string absoluteOrAssetsPath)
        {
            string match = absoluteOrAssetsPath.Replace('\\', '/');
            const string assetsPrefix = "/Assets/";
            int assetsIndex = match.IndexOf(assetsPrefix, System.StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                return match.Substring(assetsIndex + 1);
            }

            if (match.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                return match;
            }

            return match;
        }

        private static void WriteVariant5TextureReport(string body, string texturesFolder)
        {
            string reportDir = "Assets/_Game/EditorReports";
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            File.WriteAllText(
                Path.Combine(reportDir, "FixZombieVariant5Textures.done"),
                body + "\ntexturesFolder=" + texturesFolder + "\n");
        }

        private static void FixTripoFbmMaterial(string fbxPath, string displayName, string variantFolder)
        {
            var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Missing FBX: " + fbxPath);
                return;
            }

            Material embedded = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().FirstOrDefault();
            if (embedded == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] No embedded material in " + fbxPath);
                return;
            }

            string fbmFolder = Path.ChangeExtension(fbxPath, ".fbm");
            if (!AssetDatabase.IsValidFolder(fbmFolder))
            {
                Debug.LogWarning("[ZombieVariantBuilder] Missing .fbm folder: " + fbmFolder);
                return;
            }

            string baseColorPath = FindTextureInFolder(fbmFolder, "_BaseColor.png");
            string normalPath = FindTextureInFolder(fbmFolder, "_Normal_Bake.png")
                                ?? FindTextureInFolder(fbmFolder, "_Normal.png");

            if (string.IsNullOrEmpty(baseColorPath))
            {
                Debug.LogWarning("[ZombieVariantBuilder] No BaseColor texture in " + fbmFolder);
                return;
            }

            EnsureNormalMapImport(baseColorPath, isNormalMap: false);
            EnsureNormalMapImport(normalPath, isNormalMap: true);

            Texture baseColorTex = AssetDatabase.LoadAssetAtPath<Texture>(baseColorPath);
            Texture normalTex = string.IsNullOrEmpty(normalPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(normalPath);

            string matFolder = variantFolder + "/Materials";
            EditorFolderUtility.EnsureFolder(matFolder);
            string matPath = matFolder + "/" + displayName + "_Mat.mat";

            Material extracted = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (extracted == null)
            {
                extracted = new Material(embedded);
                AssetDatabase.CreateAsset(extracted, matPath);
            }
            else
            {
                extracted.shader = embedded.shader;
            }

            extracted.SetTexture("_BaseMap", baseColorTex);
            if (normalTex != null)
            {
                extracted.SetTexture("_BumpMap", normalTex);
                extracted.EnableKeyword("_NORMALMAP");
            }

            if (extracted.HasProperty("_Smoothness"))
            {
                extracted.SetFloat("_Smoothness", 0.35f);
            }

            EditorUtility.SetDirty(extracted);
            AssetDatabase.SaveAssets();

            modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), extracted);
            modelImporter.SaveAndReimport();

            Debug.Log("[ZombieVariantBuilder] Fixed " + displayName + " material at " + matPath + ".");
        }

        private static string FindTextureInFolder(string folder, string suffix)
        {
            string absoluteFolder = Path.GetFullPath(folder);
            if (!Directory.Exists(absoluteFolder))
            {
                return null;
            }

            string match = Directory.GetFiles(absoluteFolder, "*" + suffix, SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(match))
            {
                return null;
            }

            match = match.Replace('\\', '/');
            const string assetsPrefix = "/Assets/";
            int assetsIndex = match.IndexOf(assetsPrefix, System.StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                return match.Substring(assetsIndex + 1);
            }

            return match;
        }

        private static void EnsureNormalMapImport(string texturePath, bool isNormalMap)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var desired = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == desired)
            {
                return;
            }

            importer.textureType = desired;
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildWalkOnlyController(string variantFolder, string displayName, AnimationClip walkClip)
        {
            string path = variantFolder + "/" + displayName + "_Walk.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walkClip;
            stateMachine.defaultState = walkState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // Force a full flush + reimport and hand back a freshly-disk-loaded reference (not the just-created
            // in-memory one): referencing a controller created moments earlier in the very same call, with far
            // less "settling" than ZombiePrefabBuilder's shared-controller-built-once pattern, turned out to be
            // exactly when SaveAsPrefabAsset's RuntimeAnimatorController-drops-on-save quirk actually reproduces
            // (confirmed directly — the standalone prefab's Animator.runtimeAnimatorController read back null
            // even after BuildStandalonePrefab's own verify-and-repair pass). A settled, canonical reference
            // here is the belt half of the belt-and-suspenders fix; BuildStandalonePrefab's retry loop is the
            // suspenders half.
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        private static GameObject BuildStandalonePrefab(string fbxPath, string variantFolder, string displayName, AnimatorController controller)
        {
            // Object.Instantiate, NOT PrefabUtility.InstantiatePrefab: the latter keeps this instance LINKED
            // to the FBX as its source prefab (an intentional feature for scene use, but wrong for "use this
            // as a one-off template to build a different prefab from"). That link is the actual root cause of
            // the Animator-drops-null bug — confirmed by comparing against ZombiePrefabBuilder's own working
            // pattern, which only ever uses Object.Instantiate on its source FBX for exactly this reason.
            // Adding a new component to a nested-prefab-instance root and immediately saving IT as a new
            // top-level prefab is what was silently losing the reference field, not any timing/settling issue
            // (a same-call vs. separate-call retry loop and AssetDatabase.Refresh() calls were both tried
            // first and neither fixed it reliably).
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            GameObject instance = (GameObject)Object.Instantiate(fbxAsset);
            instance.name = displayName;

            var animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // Ground calibration: unlike the CC3 rig (bind pose, a genuine rest stance), this Generic rig's
            // "default" appearance outside Play mode already reflects live frame 0 of its only clip (Mixamo
            // FBX imports have no separate neutral bind pose), a mid-stride walk pose whose planted foot's
            // sole sits BELOW this instance's own root Y — measured directly per variant (varies per mesh),
            // not assumed. Lifting the root by that amount keeps the lowest point of the stride at Y=0
            // instead of sinking into the ground once EnemyMovement snaps the root to the terrain surface
            // (same class of bug as ZombiePrefabBuilder's Hip-offset fix, smaller magnitude here).
            var smrForGrounding = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smrForGrounding != null)
            {
                float lowestY = smrForGrounding.bounds.min.y - instance.transform.position.y;
                instance.transform.position = new Vector3(0f, Mathf.Max(0f, -lowestY), 0f);
            }

            // Overall character scale: the raw Tripo/Mixamo import lands at ~1 unit tall (matching
            // Enemy_Normal's old CapsuleCollider height of 0.9 as a rough convention), which read as too small
            // once actually seen in Level_01 next to the environment. Scaled up uniformly — the grounding
            // offset above must scale by the same factor too (it's a distance measured in the SAME local space
            // that localScale then stretches), or the feet would drift off the ground once the whole character
            // gets bigger.
            instance.transform.localScale = Vector3.one * CharacterScale;
            instance.transform.position = new Vector3(0f, instance.transform.position.y * CharacterScale, 0f);

            string prefabPath = variantFolder + "/" + displayName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            // Same RuntimeAnimatorController-drops-on-save quirk noted in ZombiePrefabBuilder, but a single
            // verify-and-repair pass was NOT enough here (confirmed directly — re-checking after WireEnemies
            // still read back null): loop it instead of trusting one retry to stick.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                GameObject saved = PrefabUtility.LoadPrefabContents(prefabPath);
                var savedAnimator = saved.GetComponent<Animator>();
                bool needsRepair = savedAnimator != null && savedAnimator.runtimeAnimatorController != controller;
                if (needsRepair)
                {
                    savedAnimator.runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(savedAnimator);
                    PrefabUtility.SaveAsPrefabAsset(saved, prefabPath);
                }
                PrefabUtility.UnloadPrefabContents(saved);

                if (!needsRepair)
                {
                    break;
                }

                AssetDatabase.Refresh();
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // ------------------------------------------------------------------------------------------------
        // Wiring onto the existing Enemy_* prefabs
        // ------------------------------------------------------------------------------------------------

        /// <summary>Which Enemy_* prefab gets which variant. Normal/Runner/Tank keep variants 1/3/4;
        /// Shield/Armored/Boss use folder 5 / 7 / Boss so no two enemy types share the same mesh.</summary>
        public static readonly (string EnemyPrefab, string VariantDisplayName)[] EnemyAssignments =
        {
            ("Enemy_Normal", "ZombieVariant1"),
            ("Enemy_Runner", "ZombieVariant3"),
            ("Enemy_Tank", "ZombieVariant4"),
            ("Enemy_Armored", "ZombieVariant7"),
            ("Enemy_Shield", "ZombieVariant5"),
            ("Enemy_Boss", "BossVariant"),
        };

        [MenuItem("AlienDefense/Setup/8. Wire New Zombie Variants Onto Enemy Prefabs")]
        public static void WireEnemies()
        {
            foreach (var assignment in EnemyAssignments)
            {
                Variant variant = AllVariants.First(v => v.DisplayName == assignment.VariantDisplayName);
                string variantPrefabPath = RootFolder + "/" + variant.Folder + "/" + assignment.VariantDisplayName + ".prefab";
                GameObject variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPrefabPath);
                if (variantPrefab == null)
                {
                    Debug.LogWarning("[ZombieVariantBuilder] Variant prefab not found: " + variantPrefabPath + " (run BuildAll first).");
                    continue;
                }

                ApplyVariantVisualToEnemy(assignment.EnemyPrefab, variantPrefab);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ZombieVariantBuilder] Wired " + EnemyAssignments.Length + " enemy prefabs to the new zombie variants.");
        }

        // ------------------------------------------------------------------------------------------------
        // Attack animations (added after the walk-only pipeline above already shipped) - each variant folder
        // got its own "Attack{N}.fbx" (same Tripo+Mixamo pipeline, same three import bugs as the walk clips).
        // Kept as the same two-separate-calls shape as Steps 7a/7b for the same documented reason: writing a
        // freshly-created AnimatorController change and reading it back as part of building/saving something
        // else in the very same call is where Unity's serialization has repeatedly dropped references in this
        // pipeline. Step 9a only touches the controllers; Step 9b (a later call) reads them back off disk and
        // wires the result onto the Enemy_* prefabs' EnemyController/EnemyBaseAttackVisual.
        // ------------------------------------------------------------------------------------------------

        private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

        /// <summary>Step 1 of 2: fixes each variant's Attack FBX import (same Bug 1 as the walk clips - Mixamo's
        /// declared 1cm-per-unit file scale would otherwise shrink the retargeted attack pose relative to the
        /// walk clip on the same rig) and adds an "Attack" state + trigger parameter to that variant's existing
        /// Walk controller (Any State -&gt; Attack on the trigger, Attack -&gt; Walk once the clip finishes
        /// playing - exit time, no condition needed since the clip doesn't loop). Idempotent: re-running this
        /// after the state/parameter already exist updates the motion/import fix in place instead of
        /// duplicating them.</summary>
        [MenuItem("AlienDefense/Setup/9a. Add Zombie Attack Animations - Step 1 (Imports+Controller)")]
        public static void BuildAttackImportsAndControllers()
        {
            foreach (var variant in Variants)
            {
                BuildOneVariantAttackImportAndController(variant);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZombieVariantBuilder] Step 9a done: fixed Attack imports and extended " + Variants.Length + " AnimatorControllers.");
        }

        private static void BuildOneVariantAttackImportAndController(Variant variant)
        {
            string variantFolder = RootFolder + "/" + variant.Folder;
            string attackFbxPath = variantFolder + "/" + variant.AttackFbxFileName;

            var modelImporter = AssetImporter.GetAtPath(attackFbxPath) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Missing Attack FBX: " + attackFbxPath);
                return;
            }

            // Bug 1 (see BuildOneVariantImportsAndController): same Mixamo fileScale=0.01 issue.
            modelImporter.useFileScale = false;

            // The zombie never walks again once it reaches the base - it stays parked there attacking
            // repeatedly (see EnemyController.AttackBaseRepeatedly) until killed or the base is destroyed - so
            // the Attack state loops this clip forever instead of playing once and returning to Walk. Mixamo
            // exports it non-looping by default; override that explicitly.
            var clips = modelImporter.defaultClipAnimations;
            if (clips.Length > 0)
            {
                ModelImporterClipAnimation clip = clips[0];
                clip.loopTime = true;
                clip.loopPose = true;
                modelImporter.clipAnimations = new[] { clip };
            }

            AssetDatabase.WriteImportSettingsIfDirty(attackFbxPath);
            AssetDatabase.ImportAsset(attackFbxPath, ImportAssetOptions.ForceUpdate);

            // Same "__preview__" decoy clip as the walk import (see BuildOneVariantImportsAndController) - the
            // real clip is the one NOT prefixed that way.
            AnimationClip attackClip = AssetDatabase.LoadAllAssetsAtPath(attackFbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (attackClip == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] No non-preview animation clip found in " + attackFbxPath);
                return;
            }

            string controllerPath = variantFolder + "/" + variant.DisplayName + "_Walk.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Controller not found: " + controllerPath + " (run Step 7a first).");
                return;
            }

            AddOrUpdateAttackState(controller, attackClip);
            EditorUtility.SetDirty(controller);
        }

        /// <summary>Attack is a terminal, looping state once entered: Any State -&gt; Attack on the trigger, and
        /// NO transition back to Walk - the zombie never walks again after reaching the base (see
        /// EnemyController.AttackBaseRepeatedly), it just loops this clip forever. Fully idempotent: re-running
        /// this also strips any old Attack -&gt; Walk transition an earlier (one-shot-attack) version of this
        /// builder left behind, so existing controllers self-correct instead of needing to be deleted by hand.</summary>
        private static void AddOrUpdateAttackState(AnimatorController controller, AnimationClip attackClip)
        {
            bool hasParam = controller.parameters.Any(p => p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger);
            if (!hasParam)
            {
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            ChildAnimatorState[] existing = stateMachine.states;
            AnimatorState attackState = existing.FirstOrDefault(s => s.state.name == "Attack").state;

            if (attackState == null)
            {
                attackState = stateMachine.AddState("Attack");
            }
            else
            {
                // Strip any leftover Attack -> Walk transition from an earlier version of this builder.
                attackState.transitions = System.Array.Empty<AnimatorStateTransition>();
            }

            bool hasAnyStateTransition = stateMachine.anyStateTransitions.Any(t => t.destinationState == attackState);
            if (!hasAnyStateTransition)
            {
                AnimatorStateTransition toAttack = stateMachine.AddAnyStateTransition(attackState);
                toAttack.hasExitTime = false;
                toAttack.duration = 0.1f;
                toAttack.canTransitionToSelf = false;
                toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            }

            attackState.motion = attackClip;
        }

        /// <summary>Step 2 of 2 (a separate later call - see the class-level doc comment on why): adds
        /// EnemyBaseAttackVisual to each Enemy_* prefab's root (the same spot EnemyDeathVisual/EnemyHitFlash
        /// already live), points it at the variant's Animator (VisualRoot/Model) and sets its hold duration to
        /// that variant's actual Attack clip length (read fresh off disk, not assumed), and wires the result
        /// into EnemyController's own _baseAttackVisual field.</summary>
        [MenuItem("AlienDefense/Setup/9b. Add Zombie Attack Animations - Step 2 (Wire Enemies)")]
        public static void WireBaseAttackVisualOntoEnemies()
        {
            foreach (var assignment in EnemyAssignments)
            {
                Variant variant = AllVariants.First(v => v.DisplayName == assignment.VariantDisplayName);
                string attackFbxPath = RootFolder + "/" + variant.Folder + "/" + variant.AttackFbxFileName;
                AnimationClip attackClip = AssetDatabase.LoadAllAssetsAtPath(attackFbxPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                if (attackClip == null)
                {
                    Debug.LogWarning("[ZombieVariantBuilder] No Attack clip found for " + assignment.EnemyPrefab + " (run Step 9a first).");
                    continue;
                }

                WireBaseAttackVisualOntoEnemy(assignment.EnemyPrefab, attackClip.length);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ZombieVariantBuilder] Step 9b done: wired EnemyBaseAttackVisual onto " + EnemyAssignments.Length + " enemy prefabs.");
        }

        private const string BuildExtendedRequestPath = "Assets/_Game/EditorReports/BuildExtendedVariants.request";

        private static string ResolveProjectRelativePath(string assetsRelativePath)
        {
            // InitializeOnLoad may not have CWD = project root; resolve via Application.dataPath.
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? "", assetsRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        [InitializeOnLoadMethod]
        private static void AutoRunBuildExtendedIfRequested()
        {
            string absoluteRequest = ResolveProjectRelativePath(BuildExtendedRequestPath);
            if (!File.Exists(absoluteRequest) && !File.Exists(BuildExtendedRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                string path = File.Exists(absoluteRequest) ? absoluteRequest : BuildExtendedRequestPath;
                if (!File.Exists(path))
                {
                    return;
                }

                try
                {
                    File.Delete(path);
                    string meta = path + ".meta";
                    if (File.Exists(meta))
                    {
                        File.Delete(meta);
                    }

                    string assetsMeta = BuildExtendedRequestPath + ".meta";
                    if (File.Exists(assetsMeta))
                    {
                        File.Delete(assetsMeta);
                    }
                }
                catch
                {
                    // ignore
                }

                Debug.Log("[ZombieVariantBuilder] Auto-running Build Variants 5+7+Boss from request file.");
                BuildExtendedVariantsPhaseA();
                EditorApplication.delayCall += BuildExtendedVariantsPhaseB;
            };
        }

        /// <summary>Builds Walk+Attack for folder 5 / 7 / Boss, then wires Shield / Armored / Boss enemies.
        /// Phase A (imports+controllers) then Phase B (prefabs+wire) run on consecutive delayCalls.</summary>
        [MenuItem("AlienDefense/Setup/10. Build Variants 5+7+Boss And Wire Enemies")]
        public static void BuildExtendedVariantsAndWireEnemies()
        {
            BuildExtendedVariantsPhaseA();
            EditorApplication.delayCall += BuildExtendedVariantsPhaseB;
        }

        [MenuItem("AlienDefense/Setup/10a. Build Variants 5+7+Boss - Step 1 (Imports+Controllers)")]
        public static void BuildExtendedVariantsPhaseA()
        {
            foreach (var variant in ExtendedVariants)
            {
                BuildOneVariantImportsAndController(variant);
                BuildOneVariantAttackImportAndController(variant);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZombieVariantBuilder] Step 10a done: imports+Walk/Attack controllers for folder 5, 7, Boss.");
        }

        [MenuItem("AlienDefense/Setup/10b. Build Variants 5+7+Boss - Step 2 (Prefabs+Wire)")]
        public static void BuildExtendedVariantsPhaseB()
        {
            foreach (var variant in ExtendedVariants)
            {
                string variantFolder = RootFolder + "/" + variant.Folder;
                string fbxPath = variantFolder + "/" + variant.FbxFileName;
                string controllerPath = variantFolder + "/" + variant.DisplayName + "_Walk.controller";

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                {
                    Debug.LogWarning("[ZombieVariantBuilder] Controller not found: " + controllerPath + " (run Step 10a first).");
                    continue;
                }

                BuildStandalonePrefab(fbxPath, variantFolder, variant.DisplayName, controller);
            }

            // Only re-wire the three enemies that use extended variants (leave Normal/Runner/Tank alone).
            string[] extendedEnemyNames = { "Enemy_Shield", "Enemy_Armored", "Enemy_Boss" };
            foreach (var assignment in EnemyAssignments)
            {
                if (!extendedEnemyNames.Contains(assignment.EnemyPrefab))
                {
                    continue;
                }

                Variant variant = AllVariants.First(v => v.DisplayName == assignment.VariantDisplayName);
                string variantPrefabPath = RootFolder + "/" + variant.Folder + "/" + assignment.VariantDisplayName + ".prefab";
                GameObject variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPrefabPath);
                if (variantPrefab == null)
                {
                    Debug.LogWarning("[ZombieVariantBuilder] Variant prefab not found: " + variantPrefabPath);
                    continue;
                }

                ApplyVariantVisualToEnemy(assignment.EnemyPrefab, variantPrefab);

                string attackFbxPath = RootFolder + "/" + variant.Folder + "/" + variant.AttackFbxFileName;
                AnimationClip attackClip = AssetDatabase.LoadAllAssetsAtPath(attackFbxPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (attackClip != null)
                {
                    WireBaseAttackVisualOntoEnemy(assignment.EnemyPrefab, attackClip.length);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string reportDir = "Assets/_Game/EditorReports";
            if (!Directory.Exists(reportDir))
            {
                Directory.CreateDirectory(reportDir);
            }

            File.WriteAllText(
                Path.Combine(reportDir, "BuildExtendedVariants.done"),
                "ok\nShield=ZombieVariant5\nArmored=ZombieVariant7\nBoss=BossVariant\n");

            Debug.Log("[ZombieVariantBuilder] Step 10b done: prefabs built and Shield/Armored/Boss wired.");
        }

        private static void WireBaseAttackVisualOntoEnemy(string enemyPrefabName, float attackClipLength)
        {
            string prefabPath = EnemyPrefabFolder + "/" + enemyPrefabName + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Enemy prefab not found: " + prefabPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            Transform model = contents.transform.Find("VisualRoot/Model");
            Animator animator = model != null ? model.GetComponent<Animator>() : null;
            if (animator == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] " + enemyPrefabName + " has no VisualRoot/Model Animator (run Step 8 first).");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            var baseAttackVisual = contents.GetComponent<EnemyBaseAttackVisual>();
            if (baseAttackVisual == null)
            {
                baseAttackVisual = contents.AddComponent<EnemyBaseAttackVisual>();
            }

            var visualSerialized = new SerializedObject(baseAttackVisual);
            visualSerialized.FindProperty("_animator").objectReferenceValue = animator;
            visualSerialized.FindProperty("_attackHoldDuration").floatValue = attackClipLength;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();

            var enemyController = contents.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                var controllerSerialized = new SerializedObject(enemyController);
                var baseAttackVisualProp = controllerSerialized.FindProperty("_baseAttackVisual");
                if (baseAttackVisualProp != null)
                {
                    baseAttackVisualProp.objectReferenceValue = baseAttackVisual;
                    controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            Debug.Log("[ZombieVariantBuilder] " + enemyPrefabName + " now attacks PlayerBase (hold=" + attackClipLength.ToString("0.00") + "s) instead of vanishing on arrival.");
        }

        /// <summary>Same "replace VisualRoot's one child" approach as ZombiePrefabBuilder.ApplyZombieVisualToEnemy
        /// (fixed there to look up by position, not the literal name "Model", since a deleted Model prefab
        /// leaves that child renamed to "Model (Missing Prefab with guid: ...)"), but deliberately does NOT
        /// wire/keep an EnemyDeathVisual: none of these 4 variants has a death clip (only "mixamo.com" walk),
        /// and holding the corpse for EnemyDeathVisual's hold-duration with the Walk clip still looping (its
        /// PlayDeath() sets a "Die" Animator trigger that silently no-ops with no such parameter defined) would
        /// read as "the zombie keeps walking in place after dying" — worse than EnemyController's own documented
        /// fallback of releasing straight to the pool when _deathVisual is left unassigned.</summary>
        private static void ApplyVariantVisualToEnemy(string enemyPrefabName, GameObject variantPrefab)
        {
            string prefabPath = EnemyPrefabFolder + "/" + enemyPrefabName + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] Enemy prefab not found: " + prefabPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            Transform visualRoot = contents.transform.Find("VisualRoot");
            Transform oldModel = visualRoot != null && visualRoot.childCount > 0 ? visualRoot.GetChild(0) : null;
            if (visualRoot == null)
            {
                Debug.LogWarning("[ZombieVariantBuilder] " + enemyPrefabName + " has no VisualRoot.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(variantPrefab, contents.scene);
            instance.name = "Model";
            // Deliberately NOT resetting localPosition to zero: SetParent(_, false) keeps the instantiated
            // prefab's own local position/rotation/scale numbers as-is (VisualRoot itself sits at identity, so
            // that's also its resulting world position), preserving the per-variant grounding offset baked
            // into the standalone prefab's root by BuildStandalonePrefab. Rotation still gets normalized
            // explicitly (should always be identity regardless of instantiation quirks) but scale is NOT reset
            // to one anymore — CharacterScale (see BuildStandalonePrefab) needs to carry through here too, or
            // every Enemy_* would render back at the tiny ~1-unit raw import size despite the standalone
            // prefab itself being correctly sized.
            instance.transform.SetParent(visualRoot, false);
            instance.transform.localRotation = Quaternion.identity;

            RuntimeAnimatorController animatorController = instance.GetComponent<Animator>()?.runtimeAnimatorController;

            // Reverted per request: play the walk clip at its own natural speed (Animator.speed's default of 1)
            // instead of a computed multiplier matching it to MoveSpeed. That multiplier fixed foot-sliding but
            // read as an unnaturally fast walk cycle on some variants — removing EnemyAnimatorSpeed entirely
            // (rather than just setting its multiplier to 1) so nothing overrides Animator.speed at all.
            var staleSpeedSync = contents.GetComponent<EnemyAnimatorSpeed>();
            if (staleSpeedSync != null)
            {
                Object.DestroyImmediate(staleSpeedSync);
            }

            var deathVisual = contents.GetComponent<EnemyDeathVisual>();
            if (deathVisual != null)
            {
                Object.DestroyImmediate(deathVisual);
            }

            var enemyController = contents.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                var controllerSerialized = new SerializedObject(enemyController);
                var deathVisualProp = controllerSerialized.FindProperty("_deathVisual");
                if (deathVisualProp != null)
                {
                    deathVisualProp.objectReferenceValue = null;
                    controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var hitFlash = contents.GetComponent<EnemyHitFlash>();
            if (hitFlash != null)
            {
                var renderer = instance.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    var hitFlashSerialized = new SerializedObject(hitFlash);
                    hitFlashSerialized.FindProperty("_renderer").objectReferenceValue = renderer;
                    hitFlashSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            // Same RuntimeAnimatorController-drops-on-save quirk noted in ZombiePrefabBuilder — verify and
            // repair, looped (a single retry wasn't reliable — see BuildStandalonePrefab's own version of this).
            for (int attempt = 0; attempt < 3; attempt++)
            {
                GameObject verify = PrefabUtility.LoadPrefabContents(prefabPath);
                Transform verifyModel = verify.transform.Find("VisualRoot/Model");
                var verifyAnimator = verifyModel != null ? verifyModel.GetComponent<Animator>() : null;
                bool needsRepair = verifyAnimator != null && verifyAnimator.runtimeAnimatorController != animatorController;
                if (needsRepair)
                {
                    verifyAnimator.runtimeAnimatorController = animatorController;
                    verifyAnimator.applyRootMotion = false;
                    EditorUtility.SetDirty(verifyAnimator);
                    PrefabUtility.SaveAsPrefabAsset(verify, prefabPath);
                }
                PrefabUtility.UnloadPrefabContents(verify);

                if (!needsRepair)
                {
                    break;
                }

                AssetDatabase.Refresh();
            }

            Debug.Log("[ZombieVariantBuilder] " + enemyPrefabName + " now uses " + variantPrefab.name + ".");
        }
    }
}

