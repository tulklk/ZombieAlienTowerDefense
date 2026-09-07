using System.Collections.Generic;
using System.Linq;
using AlienDefense.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Builds 13 reusable zombie prefabs (mesh + skeleton clone, each bone-remapped to its own copied
    /// rig) out of the single "Zombies stylized concatenated.fbx" source, plus one shared Walk/Die AnimatorController
    /// (all 13 skeletons share an identical CC_Base_* bone hierarchy, so one Walk clip + one Die clip — authored
    /// procedurally below, since the pack ships no animation of its own beyond a store-preview camera take — drive
    /// every zombie). Also wires 6 of the 13 onto the existing Enemy_Normal/Runner/Tank/Armored/Shield/Boss
    /// prefabs, replacing their placeholder Capsule visual with a zombie + EnemyDeathVisual, so Level_01 (and
    /// every level reusing those EnemyDefinition assets) spawns zombies instead of capsules.</summary>
    public static class ZombiePrefabBuilder
    {
        private const string FbxPath = "Assets/_Game/Models/Zombie/source/Zombies stylized concatenated.fbx";
        private const string ZombieFolder = "Assets/_Game/Models/Zombie";
        private const string PrefabFolder = ZombieFolder + "/Prefabs";
        private const string AnimFolder = ZombieFolder + "/Animations";
        private const string EnemyPrefabFolder = "Assets/_Game/Prefabs/Enemies";

        /// <summary>The source rig faces local -Z; EnemyMovement rotates the prefab root to face local +Z toward
        /// the path. Baked onto the "Skeleton" wrapper in every clone (see BuildOnePrefab) since skinned mesh
        /// deformation is driven by the bones' world transforms, not the mesh renderer's own Transform.</summary>
        private static readonly Quaternion SkeletonBaseRotation = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>meshName -> boneRootName, established from each SkinnedMeshRenderer's actual rootBone/bones[]
        /// references in the source FBX (NOT a naming-order guess — see conversation notes).</summary>
        private static readonly (string Mesh, string BoneRoot)[] ZombiePairs =
        {
            ("base_basic_shaded", "RL_BoneRoot"),
            ("Cartoon_zombie1", "RL_BoneRoot 4"),
            ("Cartoon_zombie2", "RL_BoneRoot 2"),
            ("Cartoon_zombie3", "RL_BoneRoot 1"),
            ("Cartoon_zombie4", "RL_BoneRoot 12"),
            ("Cartoon_zombie5_female", "RL_BoneRoot 11"),
            ("Cartoon_zombie6_female", "RL_BoneRoot 10"),
            ("Cartoon_zombie7_female", "RL_BoneRoot 9"),
            ("Cartoon_Zombie8_obese_man", "RL_BoneRoot 6"),
            ("Cartoon_zombie_new10", "RL_BoneRoot 3"),
            ("Obese_Police_Zombie1", "RL_BoneRoot 8"),
            ("Obese_Police_Zombie2", "RL_BoneRoot 7"),
            ("Obese_Zombie1", "RL_BoneRoot 5"),
        };

        /// <summary>Which zombie visual replaces which existing Capsule enemy's Model. The 4 "Obese_*"/obese_man
        /// meshes were tried here first but rejected after visual + numeric testing (see conversation notes):
        /// their belly/robe geometry hangs low enough in the BIND pose to visually cover the thighs from almost
        /// every camera angle regardless of animation pose — confirmed it's not a bone/foot-height bug (the
        /// obese rig's feet actually sit slightly LESS sunk than the working slim rig at the same spot; all 13
        /// meshes share one identical skeleton/bone-length set) but pure mesh-geometry occlusion, which no
        /// animation-curve tuning can fix. Swapped to 4 of the slimmer unused meshes, each spot-checked in a
        /// live side-on screenshot to confirm the legs stay visible through a full walk cycle.</summary>
        private static readonly (string EnemyPrefab, string ZombieMesh)[] EnemyAssignments =
        {
            ("Enemy_Normal", "base_basic_shaded"),
            ("Enemy_Runner", "Cartoon_zombie5_female"),
            ("Enemy_Tank", "Cartoon_zombie1"),
            ("Enemy_Armored", "Cartoon_zombie3"),
            ("Enemy_Shield", "Cartoon_zombie6_female"),
            ("Enemy_Boss", "Cartoon_zombie7_female"),
        };

        /// <summary>Shared neutral standing pose (one bone name -> local rotation), computed from the mesh's own
        /// bindposes (SkinnedMeshRenderer.sharedMesh.bindposes, decomposed into each bone's PARENT-relative
        /// rotation by chaining consecutive inverse-bind matrices) — NOT a hand-picked scene pose. An earlier
        /// version of this table was hand-picked from Obese_Police_Zombie1's scene rotations on the assumption
        /// that character looked "standing" — it did not; every one of the 13 is frozen in some store-preview
        /// action pose (lunging, reaching, etc.), none neutral, so ANY of their scene rotations produces a
        /// collapsed-looking rig once shared across all 13 (confirmed by spawning the reference character alone
        /// and seeing the same sprawled pose). The bind pose is the only rotation set guaranteed to be a plain
        /// standing pose for every character, since it's what the mesh was actually skinned against. CC_Base_Pelvis
        /// carries no skin weights in this mesh (absent from bones[]/bindposes) and is left at identity.</summary>
        private static readonly Dictionary<string, Quaternion> NeutralPose = new Dictionary<string, Quaternion>
        {
            ["CC_Base_Hip"] = new Quaternion(0.036242f, 0.944709f, -0.154347f, 0.287034f),
            ["CC_Base_Pelvis"] = Quaternion.identity,
            ["CC_Base_L_Thigh"] = new Quaternion(0.943662f, 0.120265f, 0.284003f, 0.119924f),
            ["CC_Base_L_Calf"] = new Quaternion(0.033912f, -0.000600f, 0.050735f, 0.998136f),
            ["CC_Base_L_Foot"] = new Quaternion(0.567019f, 0.134015f, 0.019268f, 0.812501f),
            ["CC_Base_R_Thigh"] = new Quaternion(0.940295f, -0.185769f, 0.282983f, 0.035443f),
            ["CC_Base_R_Calf"] = new Quaternion(0.045830f, 0.003260f, -0.109389f, 0.992937f),
            ["CC_Base_R_Foot"] = new Quaternion(0.541361f, -0.154774f, 0.066671f, 0.823729f),
            ["CC_Base_Waist"] = new Quaternion(-0.131570f, 0.000002f, -0.000003f, 0.991307f),
            ["CC_Base_Spine01"] = new Quaternion(0.000165f, 0.000000f, -0.000002f, 1.000000f),
            ["CC_Base_Spine02"] = new Quaternion(-0.166049f, -0.001386f, 0.000229f, 0.986117f),
            ["CC_Base_L_Clavicle"] = new Quaternion(0.017084f, -0.180057f, 0.697260f, 0.693625f),
            ["CC_Base_L_Upperarm"] = new Quaternion(0.115213f, -0.000050f, -0.008972f, 0.993300f),
            ["CC_Base_L_Forearm"] = new Quaternion(0.001206f, 0.000000f, 0.000000f, 0.999999f),
            ["CC_Base_L_Hand"] = new Quaternion(-0.020290f, 0.000129f, -0.007131f, 0.999769f),
            ["CC_Base_NeckTwist01"] = new Quaternion(0.183207f, 0.004529f, -0.002196f, 0.983062f),
            ["CC_Base_Head"] = new Quaternion(-0.117606f, 0.000026f, 0.000008f, 0.993060f),
            ["CC_Base_R_Clavicle"] = new Quaternion(0.014126f, 0.179735f, -0.683307f, 0.707522f),
            ["CC_Base_R_Upperarm"] = new Quaternion(0.115210f, 0.000051f, 0.008972f, 0.993301f),
            ["CC_Base_R_Forearm"] = new Quaternion(0.001208f, 0.000000f, 0.000000f, 0.999999f),
            ["CC_Base_R_Hand"] = new Quaternion(-0.020295f, -0.000128f, 0.007126f, 0.999769f),
        };

        [MenuItem("AlienDefense/Setup/6. Build Zombie Prefabs, Animations And Enemy Visuals")]
        public static void BuildAll()
        {
            EditorFolderUtility.EnsureFolder(PrefabFolder);
            EditorFolderUtility.EnsureFolder(AnimFolder);

            GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxRoot == null)
            {
                Debug.LogError("[ZombiePrefabBuilder] Could not load FBX at " + FbxPath);
                return;
            }

            GameObject sourceInstance = (GameObject)Object.Instantiate(fbxRoot);
            sourceInstance.name = "__ZombieBuilderSource__";

            try
            {
                AnimationClip walkClip = BuildWalkClip();
                AnimationClip dieClip = BuildDieClip();
                AnimatorController controller = BuildAnimatorController(walkClip, dieClip);

                var builtPrefabs = new Dictionary<string, GameObject>();
                foreach (var pair in ZombiePairs)
                {
                    GameObject prefab = BuildOnePrefab(sourceInstance.transform, pair.Mesh, pair.BoneRoot, controller);
                    if (prefab != null)
                    {
                        builtPrefabs[pair.Mesh] = prefab;
                    }
                }

                foreach (var assignment in EnemyAssignments)
                {
                    if (!builtPrefabs.TryGetValue(assignment.ZombieMesh, out GameObject zombiePrefab))
                    {
                        Debug.LogWarning("[ZombiePrefabBuilder] Zombie prefab for '" + assignment.ZombieMesh + "' was not built; skipping " + assignment.EnemyPrefab + ".");
                        continue;
                    }

                    ApplyZombieVisualToEnemy(assignment.EnemyPrefab, zombiePrefab, controller);
                }

                Debug.Log("[ZombiePrefabBuilder] Built " + builtPrefabs.Count + " zombie prefabs in " + PrefabFolder +
                    ", wired " + EnemyAssignments.Length + " enemy prefabs to zombie visuals.");
            }
            finally
            {
                Object.DestroyImmediate(sourceInstance);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Prefab assembly
        // ------------------------------------------------------------------------------------------------

        private static GameObject BuildOnePrefab(Transform sourceRoot, string meshName, string boneRootName, AnimatorController controller)
        {
            Transform meshSource = FindDeep(sourceRoot, meshName);
            Transform boneRootSource = FindDeep(sourceRoot, boneRootName);
            if (meshSource == null || boneRootSource == null)
            {
                Debug.LogWarning("[ZombiePrefabBuilder] Could not find '" + meshName + "' or '" + boneRootName + "' in the source FBX instance.");
                return null;
            }

            var smrSource = meshSource.GetComponent<SkinnedMeshRenderer>();
            if (smrSource == null)
            {
                Debug.LogWarning("[ZombiePrefabBuilder] '" + meshName + "' has no SkinnedMeshRenderer.");
                return null;
            }

            string prefabName = "Zombie_" + meshName;
            var root = new GameObject(prefabName);

            // Clone the skeleton under a fresh, identity-transformed "Skeleton" wrapper (its own local space is
            // then guaranteed clean/world-aligned, independent of the FBX's own RL_BoneRoot correction node —
            // see BuildWalkClip/BuildDieClip, which animate this wrapper's position/rotation directly).
            // 180 degrees on Y: the source rig's own face-forward axis is local -Z (confirmed by comparing
            // CC_Base_L_Hand/R_Hand world positions against root.forward on a spawned instance — the character's
            // own left hand landed on root's +X side, which only happens facing -Z), but EnemyMovement rotates
            // the prefab ROOT to face local +Z toward the path. Skinned mesh deformation is driven entirely by
            // the BONES' world transforms (the SkinnedMeshRenderer's own Transform is not used for vertex
            // positions), so the correction has to live on the Skeleton wrapper, not on the Model/mesh clone.
            var skeletonWrapper = new GameObject("Skeleton");
            skeletonWrapper.transform.SetParent(root.transform, false);
            skeletonWrapper.transform.localRotation = SkeletonBaseRotation;

            GameObject boneRootClone = (GameObject)Object.Instantiate(boneRootSource.GetChild(0).gameObject);
            boneRootClone.name = boneRootSource.GetChild(0).name; // strip the "(Clone)" suffix
            boneRootClone.transform.SetParent(skeletonWrapper.transform, false);
            boneRootClone.transform.localPosition = Vector3.zero;
            boneRootClone.transform.localRotation = Quaternion.identity;

            ApplyNeutralPose(boneRootClone.transform);
            ApplyRestingStanceOffsets(boneRootClone.transform);

            // Ground calibration: leaving Hip at local Y=0 puts the PELVIS at "ground level", not the FEET — for
            // a real leg chain (Hip -> Thigh -> Calf -> Foot, ~0.33 units long in this rig) that leaves the feet
            // floating ~0.33+ units *below* where EnemyMovement/the CapsuleCollider convention expects ground
            // contact (root.y — see EnemyMovement's ground-snap fix, which corrects the same convention on the
            // movement side). Confirmed directly: after fixing EnemyMovement to snap root.y to real terrain, the
            // feet were STILL sinking ~0.55-0.68 units below root — this Hip offset, not the animation or the
            // path, is that remaining gap. Shift Hip up so the neutral-pose feet land at local Y=0 instead.
            Transform lFootNeutral = FindDeep(boneRootClone.transform, "CC_Base_L_Foot");
            Transform rFootNeutral = FindDeep(boneRootClone.transform, "CC_Base_R_Foot");
            if (lFootNeutral != null && rFootNeutral != null)
            {
                float footY = Mathf.Min(lFootNeutral.position.y, rFootNeutral.position.y);
                boneRootClone.transform.localPosition = new Vector3(0f, -footY, 0f);
            }

            // Mesh clone, sibling of Skeleton, bones remapped by NAME onto the freshly cloned skeleton.
            GameObject meshClone = (GameObject)Object.Instantiate(meshSource.gameObject);
            meshClone.name = "Model";
            meshClone.transform.SetParent(root.transform, false);
            meshClone.transform.localPosition = Vector3.zero;
            meshClone.transform.localRotation = Quaternion.identity;

            var boneMap = new Dictionary<string, Transform>();
            foreach (var t in boneRootClone.GetComponentsInChildren<Transform>(true))
            {
                boneMap[t.name] = t;
            }

            var smrClone = meshClone.GetComponent<SkinnedMeshRenderer>();
            Transform[] oldBones = smrClone.bones;
            var newBones = new Transform[oldBones.Length];
            for (int i = 0; i < oldBones.Length; i++)
            {
                newBones[i] = oldBones[i] != null && boneMap.TryGetValue(oldBones[i].name, out Transform mapped) ? mapped : null;
            }
            smrClone.bones = newBones;
            if (smrClone.rootBone != null && boneMap.TryGetValue(smrClone.rootBone.name, out Transform newRoot))
            {
                smrClone.rootBone = newRoot;
            }

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            string prefabPath = PrefabFolder + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // SaveAsPrefabAsset can drop a RuntimeAnimatorController reference assigned in the same pass as the
            // Animator component itself (observed directly — the asset saves fine but Animator.runtimeAnimatorController
            // reads back null). Re-open and re-assign explicitly so it reliably sticks.
            GameObject savedContents = PrefabUtility.LoadPrefabContents(prefabPath);
            var savedAnimator = savedContents.GetComponent<Animator>();
            if (savedAnimator != null && savedAnimator.runtimeAnimatorController != controller)
            {
                savedAnimator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(savedAnimator);
                PrefabUtility.SaveAsPrefabAsset(savedContents, prefabPath);
            }
            PrefabUtility.UnloadPrefabContents(savedContents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void ApplyNeutralPose(Transform boneRoot)
        {
            foreach (var t in boneRoot.GetComponentsInChildren<Transform>(true))
            {
                if (NeutralPose.TryGetValue(t.name, out Quaternion rot))
                {
                    t.localRotation = rot;
                }
            }
        }

        /// <summary>The prefab's default/un-animated appearance should match the source asset's own reference
        /// "Static Pose" (arms out to the sides, legs straight and close together — see the Sketchfab listing
        /// this pack came from), i.e. pure NeutralPose plus only the corrections below.
        ///
        /// Several earlier passes here used an approximate/swept angle for leg straightness (e.g. "the local-Z
        /// angle that scores best in a coarse sweep") and kept getting reported back as still visibly bent —
        /// correctly: a 5-10 degree residual, while small-sounding, is very visible over the length of a leg
        /// from a 3/4-front camera angle, which the pure "screenshot tool is broken" workaround (measuring only
        /// numbers, never actually looking) let slide for several rounds. Once screenshots were finally working
        /// (via opening the standalone Zombie prefab directly in Prefab Mode — the main Scene view's SceneView
        /// camera stayed stuck/cached all session, but the Prefab Stage's own SceneView rendered correctly) the
        /// residual bend was immediately visible and confirmed the numbers were the problem, not a rendering
        /// fluke. Fixed by computing the EXACT rotation via Quaternion.FromToRotation on the live Thigh->Calf /
        /// Calf->Foot segment vector (not a stepped sweep), verified afterward at a mathematically exact 0.00
        /// degrees for all four segments, then confirmed visually straight from a true front view AND the 3/4
        /// angle that had shown the bend.
        ///
        /// The feet needed a separate fix: even with the leg segments straightened, NeutralPose's own
        /// L_Foot/R_Foot rotation points the toes mostly SIDEWAYS in world space (not forward, not exactly
        /// backward either — measured via the actual mesh) instead of forward like the reference, which read as
        /// "feet facing the wrong way". Since Foot has no child bone to sight down, "which way is the toe" was
        /// measured directly from the skinned mesh itself: found the vertex most-weighted (>50%) to each Foot
        /// bone that sits farthest from the ankle in the bone's own bind-local space — that's the toe tip —
        /// then used Quaternion.FromToRotation to point that same vertex forward (root.forward, with a slight
        /// natural downward cant) instead of sideways. Verified two ways: toe direction dots ~0.97 with
        /// root.forward, and the foot mesh's vertical extent still sits mostly below the ankle (the correction
        /// didn't flip the sole to point sideways or up while fixing the toe).
        ///
        /// All four leg bones (Thigh/Calf, both sides) also needed a TWIST correction around their own length
        /// axis on top of the straightening above: the pointing direction being exactly vertical does not
        /// prevent the bone from being rolled around that same axis, which reads as diagonally-creased pant
        /// fabric. Measured the same way as the foot's roll fix: the mesh vertex farthest from the length axis
        /// (widest point of the pant leg, perpendicular to direction-to-child-bone) was swept through a full
        /// 360-degree roll to find the angle bringing it closest to purely horizontal — using the EXACT
        /// bone-to-child-bone vector as the roll axis (not any mesh-derived approximation), since only that
        /// exact axis is mathematically guaranteed to leave the child bone's position — and therefore the
        /// straightness numbers above — completely unchanged (re-verified at 0.00 degrees after adding this).
        ///
        /// All six leg-bone rotations below are baked as absolute replacements (not additive angle offsets)
        /// since Thigh/Calf/Foot each depend on their already-corrected parent's new world orientation. Order
        /// matters: Thigh, then Calf (Thigh's child), then Foot (Calf's child).
        ///
        /// BuildWalkClip/BuildDieClip are untouched by any of this — both read NeutralPose directly, not this
        /// method's result, and keep using CalfBendBaseDeg/etc. as before.</summary>
        private static void ApplyRestingStanceOffsets(Transform boneRoot)
        {
            SetAbsoluteLocalRotation(boneRoot, "CC_Base_L_Thigh", ThighRotL);
            SetAbsoluteLocalRotation(boneRoot, "CC_Base_R_Thigh", ThighRotR);
            SetAbsoluteLocalRotation(boneRoot, "CC_Base_L_Calf", CalfRotL);
            SetAbsoluteLocalRotation(boneRoot, "CC_Base_R_Calf", CalfRotR);
            SetAbsoluteLocalRotation(boneRoot, "CC_Base_L_Foot", FootForwardRotL);
            SetAbsoluteLocalRotation(boneRoot, "CC_Base_R_Foot", FootForwardRotR);
        }

        private static void SetAbsoluteLocalRotation(Transform boneRoot, string boneName, Quaternion rotation)
        {
            Transform t = FindDeep(boneRoot, boneName);
            if (t != null)
            {
                t.localRotation = rotation;
            }
        }

        /// <summary>Replaces an existing enemy prefab's VisualRoot/Model (the Capsule) with an instance of the
        /// zombie prefab (mesh + skeleton + Animator), and wires an EnemyDeathVisual so the Die animation is
        /// actually visible before the enemy returns to the pool. EnemyHitFlash keeps working unmodified since
        /// its Renderer field is generic (SkinnedMeshRenderer included).</summary>
        private static void ApplyZombieVisualToEnemy(string enemyPrefabName, GameObject zombiePrefab, AnimatorController controller)
        {
            string prefabPath = EnemyPrefabFolder + "/" + enemyPrefabName + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null)
            {
                Debug.LogWarning("[ZombiePrefabBuilder] Enemy prefab not found: " + prefabPath);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            Transform visualRoot = contents.transform.Find("VisualRoot");
            Transform oldModel = visualRoot != null ? visualRoot.Find("Model") : null;
            if (visualRoot == null || oldModel == null)
            {
                Debug.LogWarning("[ZombiePrefabBuilder] " + enemyPrefabName + " has no VisualRoot/Model to replace.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            // Preserve the capsule's footprint (scale/ground offset) so the collider/health bar/target point
            // (all sized off the old capsule scale) still line up; only the renderer underneath changes.
            Vector3 oldScale = oldModel.localScale;
            Object.DestroyImmediate(oldModel.gameObject);

            GameObject zombieInstance = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab, contents.scene);
            zombieInstance.name = "Model";
            zombieInstance.transform.SetParent(visualRoot, false);
            zombieInstance.transform.localPosition = Vector3.zero;
            zombieInstance.transform.localRotation = Quaternion.identity;
            zombieInstance.transform.localScale = Vector3.one;

            var animator = zombieInstance.GetComponent<Animator>();

            var deathVisual = contents.GetComponent<EnemyDeathVisual>();
            if (deathVisual == null)
            {
                deathVisual = contents.AddComponent<EnemyDeathVisual>();
            }
            var deathVisualSerialized = new SerializedObject(deathVisual);
            deathVisualSerialized.FindProperty("_animator").objectReferenceValue = animator;
            deathVisualSerialized.ApplyModifiedPropertiesWithoutUndo();

            var enemyController = contents.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                var controllerSerialized = new SerializedObject(enemyController);
                var deathVisualProp = controllerSerialized.FindProperty("_deathVisual");
                if (deathVisualProp != null)
                {
                    deathVisualProp.objectReferenceValue = deathVisual;
                    controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var hitFlash = contents.GetComponent<EnemyHitFlash>();
            if (hitFlash != null)
            {
                var renderer = zombieInstance.transform.Find("Model")?.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var hitFlashSerialized = new SerializedObject(hitFlash);
                    hitFlashSerialized.FindProperty("_renderer").objectReferenceValue = renderer;
                    hitFlashSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            // Same RuntimeAnimatorController-drops-on-save quirk as BuildOnePrefab — verify and repair.
            GameObject verifyContents = PrefabUtility.LoadPrefabContents(prefabPath);
            Transform verifyModel = verifyContents.transform.Find("VisualRoot/Model");
            var verifyAnimator = verifyModel != null ? verifyModel.GetComponent<Animator>() : null;
            if (verifyAnimator != null && verifyAnimator.runtimeAnimatorController != controller)
            {
                verifyAnimator.runtimeAnimatorController = controller;
                verifyAnimator.applyRootMotion = false;
                EditorUtility.SetDirty(verifyAnimator);
                PrefabUtility.SaveAsPrefabAsset(verifyContents, prefabPath);
            }
            PrefabUtility.UnloadPrefabContents(verifyContents);

            Debug.Log("[ZombiePrefabBuilder] " + enemyPrefabName + " now uses " + zombiePrefab.name + " (was scale " + oldScale + ").");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------------------------------------
        // Animation authoring
        //
        // The pack ships zero Walk/Die clips (its only embedded take is a 30s store-preview camera flythrough),
        // so both clips below are authored procedurally from the verified bone-swing data gathered by testing
        // each candidate axis against the actual rig (see conversation notes) rather than guessed: for every
        // CC_Base_* limb/spine bone in this rig, local Y is the twist/bone-length axis (near-zero end-effector
        // movement) and local Z is the cleanest single-axis swing/bend (the largest, most sagittal-plane-aligned
        // end-effector displacement of the three axes) — so all posing below rotates around each bone's own
        // local Z only. The overall fall/bob for Die/Walk is done on the "Skeleton" wrapper instead of on
        // CC_Base_Hip directly, since Skeleton is a fresh identity-rotated GameObject added by this builder
        // (so its local axes are just world axes) and is IDENTICAL across all 13 clones, whereas CC_Base_Hip's
        // own local orientation is rig-specific and only its ROTATION (not position/proportions) was unified
        // across clones by ApplyNeutralPose.
        // ------------------------------------------------------------------------------------------------

        private const float WalkCycleDuration = 0.8f;
        private const float ThighSwingDeg = 30f;
        // Permanent (not just swinging) bend so the stance is a slightly-crouched shuffle the whole cycle.
        // Numeric check (world-position sampling, see conversation notes) showed the legs are already SHORT
        // relative to this pack's fat/obese-bodied zombies and were reading as basically fully extended even
        // with the previous offsets — the "legs missing" complaint was actually the torso/belly (tipped forward
        // by SpineHunchDeg) visually occluding the thighs from the game's top-down camera, not a leg-bend bug.
        // Kept mild instead of removed, since some downward hip offset is still what makes it read as a shuffle.
        private const float ThighBaseOffsetDeg = 6f;
        private const float CalfBendBaseDeg = 10f;
        private const float CalfBendExtraDeg = 26f;
        // Resting-stance-only (see ApplyRestingStanceOffsets — NOT used by BuildWalkClip, which reads NeutralPose
        // directly). All four are absolute replacements, each built in two EXACT (not swept/approximate) steps:
        // (1) Quaternion.FromToRotation on the live Thigh->Calf / Calf->Foot segment vector, bringing it to a
        // mathematically exact 0.00 degrees from straight-down; then (2) a roll around the EXACT bone-to-child-
        // bone axis (not any mesh-derived approximation — only this axis is guaranteed not to move the child
        // bone, which is why straightness and the foot's toe direction both re-verified identical after adding
        // this) to un-twist the fabric, found by sweeping the mesh's own widest-perpendicular-to-length vertex
        // until it reads as horizontal.
        private static readonly Quaternion ThighRotL = new Quaternion(0.79477f, 0.10210f, 0.58583f, 0.12131f);
        private static readonly Quaternion ThighRotR = new Quaternion(0.77812f, 0.10542f, 0.60779f, 0.11839f);
        private static readonly Quaternion CalfRotL = new Quaternion(0.00006f, 0.19021f, 0.00002f, 0.98174f);
        private static readonly Quaternion CalfRotR = new Quaternion(0.00001f, 0.99880f, -0.00010f, 0.04905f);
        // Absolute replacement for CC_Base_L_Foot/R_Foot's local rotation. Built in two measured steps — see
        // ApplyRestingStanceOffsets' doc comment for the full method:
        // 1) Quaternion.FromToRotation aligning the foot mesh's own measured toe-tip vertex direction to point
        //    forward (this alone got the toe pointing the right way, but left an unconstrained "roll" around
        //    that forward axis, which visibly turned the shoe mesh inward until the two feet overlapped).
        // 2) An additional roll around that same forward axis chosen by sweeping the angle and re-measuring the
        //    mesh's own widest-perpendicular-to-toe vertex (the outer edge of the shoe) until it points cleanly
        //    sideways instead of inward. Re-derived from scratch after ThighRotL/R and CalfRotL/R above were
        //    switched to the exact FromToRotation values, to keep hitting the same validated world-space
        //    toe/width directions with the new Calf orientation underneath (Foot's parent is Calf).
        // R_Foot correction found directly in Prefab Mode (not from a new mesh-vertex formula): the widest-
        // perpendicular-vertex roll sweep above gave a numerically "symmetric" result for L and R alike (both
        // read as pure sideways, matching), yet only the right shoe rendered as a crumpled/bladed mesh — proof
        // the vertex-based roll metric doesn't fully predict the visual result. Root cause turned out to be a
        // stale SkinnedMeshRenderer skin (Prefab Stage wasn't re-skinning on bone-only edits unless
        // forceMatrixRecalculationPerRender was set / the renderer toggled off-on), which made earlier "fixes"
        // to this bone look like no-ops. With live rendering actually working, a plain +90 deg roll of the
        // already-fixed R_Foot around its own toe-forward axis (Quaternion.AngleAxis(90, toeAxisWorld) * current
        // rotation, verified visually against the reference and against the L shoe) resolved it; baked back to
        // this local-space constant so BuildAll() reproduces it without needing the live editor session.
        private static readonly Quaternion FootForwardRotL = new Quaternion(-0.25951f, -0.62123f, -0.56414f, 0.47798f);
        private static readonly Quaternion FootForwardRotR = new Quaternion(-0.00744f, 0.21155f, -0.55381f, -0.80528f);
        // Widened from 14 -> 30: near this arm's ArmForwardOffsetDeg baseline the hand's world position is
        // fairly flat vs. small angle changes (measured), so the old +-14 swing barely separated "arm reaching
        // further" from "arm reaching less" frame to frame — the user's reference needs one arm CLEARLY leading
        // the other each step, not a subtle wobble.
        private const float ArmSwingDeg = 30f;
        // The bind pose has arms out to the sides at roughly HEAD height (measured: hand.y sits above head.y at
        // rest — see conversation notes for the exact delta test), and rotating around local Z sweeps the hand
        // *down* as the angle grows (also measured directly, not assumed) — 65 degrees only brought it from
        // "above head" to "just above head", which is why the previous pass still looked like arms raised in the
        // air. 135 degrees is what the measured sweep needs to bring the hand down to roughly chest height.
        private const float ArmForwardOffsetDeg = 135f;
        // CC_Base_R_Upperarm's forward-reach-vs-angle relationship is NOT a mirror of the left side's (measured
        // directly, multiple times, in the actual animated multi-bone context, not just a guess): the right hand
        // never once got more forward than the left hand's typical range using the same 135-degree offset, even
        // after widening ArmSwingDeg — the gap is a fixed baseline offset, not an amplitude problem. Sweeping an
        // extra rotation on top of the animated R_Upperarm pose found the right hand only starts exceeding the
        // left hand's reach past roughly +90 degrees more than the left side's offset. This is specific to how
        // this rig's right arm bones were authored, not a general formula — re-measure with the same fwd-dot
        // sampling (see conversation notes) if NeutralPose or this rig ever changes.
        private const float ArmForwardOffsetDegR = ArmForwardOffsetDeg + 90f;
        private const float ForearmBendDeg = 35f;
        private const float SpineSwayDeg = 8f;
        // Lowered from 22 -> 11: at 22 degrees the torso (and this pack's oversized zombie bellies with it) tips
        // forward enough to visually cover the thighs from directly above/behind (the game's actual camera
        // angle), which read as "the zombie has no legs" even though the legs and feet were correctly posed and
        // grounded. 11 keeps a readable hunch without burying the legs behind the belly.
        private const float SpineHunchDeg = 11f;
        private const float BobAmplitude = 0.03f;

        private static AnimationClip BuildWalkClip()
        {
            const string path = AnimFolder + "/Zombie_Walk.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var clip = new AnimationClip { name = "Zombie_Walk", frameRate = 30f };

            int samples = 13; // 12 intervals across the loop, last sample == first for a clean loop
            float[] times = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                times[i] = WalkCycleDuration * i / (samples - 1);
            }

            // Zombie shuffle gait (per the user's Mixamo "Zombie Walk" reference, NOT a normal contralateral human
            // walk): the arm and leg on the SAME side swing forward together — left arm reaches farthest exactly
            // when the left leg is the forward/planted one, then the pair swaps to the right side next step.
            SetSwingCurve(clip, PelvisPath, "CC_Base_L_Thigh", times, ThighSwingDeg, 0f, WalkCycleDuration, baseOffsetDeg: ThighBaseOffsetDeg);
            SetSwingCurve(clip, PelvisPath, "CC_Base_R_Thigh", times, ThighSwingDeg, 0.5f, WalkCycleDuration, baseOffsetDeg: ThighBaseOffsetDeg);
            SetBendCurve(clip, LThighPath, "CC_Base_L_Calf", times, CalfBendBaseDeg, CalfBendExtraDeg, 0.25f, WalkCycleDuration);
            SetBendCurve(clip, RThighPath, "CC_Base_R_Calf", times, CalfBendBaseDeg, CalfBendExtraDeg, 0.75f, WalkCycleDuration);
            // Empirically measured (world-position fwd-dot on L/R Hand vs L/R Foot, sampled straight off the
            // Animator, not judged from a screenshot): the arm chain's own local-Z "positive angle" sense is
            // inverted relative to the leg chain's, so giving an arm the SAME numeric phase as its same-side
            // thigh actually produces CONTRALATERAL timing (confirmed: L_Thigh phase 0f paired with L_Upperarm
            // phase 0f put the RIGHT foot and LEFT hand forward together, not the left/left pair the reference
            // needs). Half a cycle off from the thigh is what actually lands the same-side arm and leg forward
            // together — re-confirmed by measurement after this change.
            SetSwingCurve(clip, LClaviclePath, "CC_Base_L_Upperarm", times, ArmSwingDeg, 0.5f, WalkCycleDuration, baseOffsetDeg: ArmForwardOffsetDeg);
            SetSwingCurve(clip, RClaviclePath, "CC_Base_R_Upperarm", times, ArmSwingDeg, 0f, WalkCycleDuration, baseOffsetDeg: ArmForwardOffsetDegR);
            SetSwingCurve(clip, LUpperarmPath, "CC_Base_L_Forearm", times, ArmSwingDeg * 0.6f, 0.5f, WalkCycleDuration, baseOffsetDeg: ForearmBendDeg);
            SetSwingCurve(clip, RUpperarmPath, "CC_Base_R_Forearm", times, ArmSwingDeg * 0.6f, 0f, WalkCycleDuration, baseOffsetDeg: ForearmBendDeg);
            SetSwingCurve(clip, WaistPath, "CC_Base_Spine01", times, SpineSwayDeg, 0.25f, WalkCycleDuration, halfFreq: true, baseOffsetDeg: SpineHunchDeg);

            SetBobCurve(clip, times, BobAmplitude, WalkCycleDuration);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationClip BuildDieClip()
        {
            const string path = AnimFolder + "/Zombie_Die.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var clip = new AnimationClip { name = "Zombie_Die", frameRate = 30f };

            // Skeleton wrapper: topple backward and sink slightly, settle. Composed on top of SkeletonBaseRotation
            // (the 180-degree facing correction baked into the prefab's Skeleton wrapper) since this curve sets
            // ABSOLUTE rotation values each frame — starting it from plain identity would snap the character to
            // face the wrong way the instant the Die state becomes active.
            AddQuatCurve(clip, "Skeleton", "localRotation",
                new float[] { 0f, 0.45f, 0.7f, 1.0f },
                new[]
                {
                    SkeletonBaseRotation,
                    SkeletonBaseRotation * Quaternion.AngleAxis(-55f, Vector3.right),
                    SkeletonBaseRotation * Quaternion.AngleAxis(-88f, Vector3.right),
                    SkeletonBaseRotation * Quaternion.AngleAxis(-88f, Vector3.right),
                });
            AddFloatCurve(clip, "Skeleton", "localPosition.y",
                new float[] { 0f, 0.5f, 0.8f, 1.0f },
                new float[] { 0f, -0.05f, -0.32f, -0.32f });

            // Limbs relax/splay into a collapsed heap, reaching their final pose by ~0.6s and holding.
            AddRelaxedLimb(clip, "CC_Base_L_Thigh", 14f);
            AddRelaxedLimb(clip, "CC_Base_R_Thigh", -10f);
            AddRelaxedLimb(clip, "CC_Base_L_Calf", 55f);
            AddRelaxedLimb(clip, "CC_Base_R_Calf", 70f);
            AddRelaxedLimb(clip, "CC_Base_L_Upperarm", -28f);
            AddRelaxedLimb(clip, "CC_Base_R_Upperarm", 32f);
            AddRelaxedLimb(clip, "CC_Base_Spine01", 18f);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>Full path (from the prefab-relative "Skeleton" wrapper) to each bone's PARENT — i.e. exactly
        /// what SetSwingCurve/SetBendCurve/AddRelaxedLimb need as "parentPath", matching the real hierarchy
        /// (Hip/Pelvis/Thigh/Calf/Foot and Hip/Waist/Spine01/Spine02/Clavicle/Upperarm/Forearm/Hand) rather than
        /// the flattened "everything is a direct child of Hip" paths an earlier version of this table used —
        /// those never matched any real Transform, so every limb curve silently failed to bind at runtime (the
        /// clip still reported the right binding *count*, since SetCurve doesn't validate the path against a
        /// live hierarchy, so this went unnoticed until the animation visibly did nothing in a real build).</summary>
        private const string HipPath = "Skeleton/CC_Base_Hip";
        private const string PelvisPath = HipPath + "/CC_Base_Pelvis";
        private const string WaistPath = HipPath + "/CC_Base_Waist";
        private const string Spine01Path = WaistPath + "/CC_Base_Spine01";
        private const string Spine02Path = Spine01Path + "/CC_Base_Spine02";
        private const string LClaviclePath = Spine02Path + "/CC_Base_L_Clavicle";
        private const string RClaviclePath = Spine02Path + "/CC_Base_R_Clavicle";
        private const string LUpperarmPath = LClaviclePath + "/CC_Base_L_Upperarm";
        private const string RUpperarmPath = RClaviclePath + "/CC_Base_R_Upperarm";
        private const string LThighPath = PelvisPath + "/CC_Base_L_Thigh";
        private const string RThighPath = PelvisPath + "/CC_Base_R_Thigh";

        private static readonly Dictionary<string, string> BoneParentPath = new Dictionary<string, string>
        {
            ["CC_Base_L_Thigh"] = PelvisPath,
            ["CC_Base_R_Thigh"] = PelvisPath,
            ["CC_Base_L_Calf"] = LThighPath,
            ["CC_Base_R_Calf"] = RThighPath,
            ["CC_Base_L_Upperarm"] = LClaviclePath,
            ["CC_Base_R_Upperarm"] = RClaviclePath,
            ["CC_Base_L_Forearm"] = LUpperarmPath,
            ["CC_Base_R_Forearm"] = RUpperarmPath,
            ["CC_Base_Spine01"] = WaistPath,
        };

        private static void AddRelaxedLimb(AnimationClip clip, string boneName, float endAngleDeg)
        {
            Quaternion rest = NeutralPose[boneName];
            string parentPath = BoneParentPath[boneName];
            AddBoneRotationCurve(clip, parentPath, boneName,
                new float[] { 0f, 0.6f, 1.0f },
                new[]
                {
                    rest,
                    rest * Quaternion.AngleAxis(endAngleDeg, Vector3.forward),
                    rest * Quaternion.AngleAxis(endAngleDeg, Vector3.forward),
                });
        }

        private static void SetSwingCurve(AnimationClip clip, string parentPath, string boneName, float[] times, float amplitudeDeg, float phase01, float cycle, bool halfFreq = false, float baseOffsetDeg = 0f)
        {
            Quaternion rest = NeutralPose[boneName];
            var values = new Quaternion[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                float t01 = times[i] / cycle;
                float freq = halfFreq ? 1f : 1f;
                float angle = baseOffsetDeg + amplitudeDeg * Mathf.Sin((t01 + phase01) * freq * Mathf.PI * 2f);
                values[i] = rest * Quaternion.AngleAxis(angle, Vector3.forward);
            }

            AddBoneRotationCurve(clip, parentPath, boneName, times, values);
        }

        private static void SetBendCurve(AnimationClip clip, string parentPath, string boneName, float[] times, float baseDeg, float extraDeg, float phase01, float cycle)
        {
            Quaternion rest = NeutralPose[boneName];
            var values = new Quaternion[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                float t01 = times[i] / cycle;
                float raise = Mathf.Max(0f, Mathf.Sin((t01 + phase01) * Mathf.PI * 2f));
                float angle = baseDeg + extraDeg * raise;
                values[i] = rest * Quaternion.AngleAxis(angle, Vector3.forward);
            }

            AddBoneRotationCurve(clip, parentPath, boneName, times, values);
        }

        private static void SetBobCurve(AnimationClip clip, float[] times, float amplitude, float cycle)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                float t01 = times[i] / cycle;
                float y = amplitude * Mathf.Abs(Mathf.Sin(t01 * 2f * Mathf.PI * 2f));
                keys[i] = new Keyframe(times[i], y);
            }

            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            clip.SetCurve("Skeleton", typeof(Transform), "localPosition.y", curve);
        }

        private static void AddBoneRotationCurve(AnimationClip clip, string parentPath, string boneName, float[] times, Quaternion[] values)
        {
            string path = string.IsNullOrEmpty(parentPath) ? boneName : parentPath + "/" + boneName;
            AddQuatCurve(clip, path, "localRotation", times, values);
        }

        private static void AddQuatCurve(AnimationClip clip, string path, string property, float[] times, Quaternion[] values)
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            var cw = new AnimationCurve();

            for (int i = 0; i < times.Length; i++)
            {
                cx.AddKey(times[i], values[i].x);
                cy.AddKey(times[i], values[i].y);
                cz.AddKey(times[i], values[i].z);
                cw.AddKey(times[i], values[i].w);
            }

            foreach (var c in new[] { cx, cy, cz, cw })
            {
                for (int i = 0; i < c.length; i++)
                {
                    c.SmoothTangents(i, 0f);
                }
            }

            clip.SetCurve(path, typeof(Transform), property + ".x", cx);
            clip.SetCurve(path, typeof(Transform), property + ".y", cy);
            clip.SetCurve(path, typeof(Transform), property + ".z", cz);
            clip.SetCurve(path, typeof(Transform), property + ".w", cw);
        }

        private static void AddFloatCurve(AnimationClip clip, string path, string property, float[] times, float[] values)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                keys[i] = new Keyframe(times[i], values[i]);
            }

            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            clip.SetCurve(path, typeof(Transform), property, curve);
        }

        // ------------------------------------------------------------------------------------------------
        // AnimatorController
        // ------------------------------------------------------------------------------------------------

        private static AnimatorController BuildAnimatorController(AnimationClip walkClip, AnimationClip dieClip)
        {
            string path = AnimFolder + "/Zombie.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            AnimatorState walkState = rootStateMachine.AddState("Walk");
            walkState.motion = walkClip;
            rootStateMachine.defaultState = walkState;

            AnimatorState dieState = rootStateMachine.AddState("Die");
            dieState.motion = dieClip;

            AnimatorStateTransition transition = walkState.AddTransition(dieState);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
