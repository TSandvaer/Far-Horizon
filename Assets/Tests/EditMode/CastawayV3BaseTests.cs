using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode regression guards for the CASTAWAY v3 (Rodin Smart-Low-poly base — ticket 86cak41d4). These
    /// assert the v3 base FBX import CONFIG + the DORMANT staged-rollout toggle so the bug CLASSES can't recur
    /// silently:
    ///
    ///   1. v3 base imports GENERIC + CreateFromThisModel — the anti-Humanoid gate (Humanoid muscle-space
    ///      retarget CONE-EXPLODES the skinned mesh at runtime; 86ca8rdkp). THE regression guard: a future edit
    ///      reverting v3 to Humanoid / CopyFromOther reds HERE before it ships a coned mesh.
    ///   2. v3 base produces a VALID avatar (else the 18 clips can't bind → T-pose) — also proves the FBX-7700
    ///      stray empty `Armature` node did NOT break avatar construction.
    ///   3. v3 base height-normalizes toward ~1u (the un-normalized-giant guard).
    ///   4. v3's skeleton carries the CORE mixamorig bones the 18 existing clips drive — the CLIP-CARRY guard.
    ///   5. THE ACTIVATED-TOGGLE guard (86cak9kau): the rollout toggle DEFAULTS ON (v3 is now the LIVE hero after
    ///      the Sponsor soak); FbxPath resolves v3-first. v2 stays reachable behind the toggle for rollback.
    ///   6. v3's posterized diffuse (the URP/Unlit Base Map) is importable.
    ///
    /// v3 is configured on EVERY bootstrap (CharacterAssetGen.ConfigureV3BaseFbx) so these import guards run
    /// against a real import; with the default now ON, the default CI run also renders v3.
    /// </summary>
    public class CastawayV3BaseTests
    {
        // (1) ANTI-HUMANOID import guard — THE regression guard for the v3 integration.
        [Test]
        public void V3Base_ImportsGeneric_NotHumanoid_CreateFromThisModel()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/v3/castaway_v3_rigged.fbx",
                CharacterAssetGen.V3RiggedFbxPath, "the v3 base must be the committed Rodin rigged FBX (mixamo/Idle.fbx)");

            var importer = AssetImporter.GetAtPath(CharacterAssetGen.V3RiggedFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, "the v3 base FBX must be importable at " + CharacterAssetGen.V3RiggedFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                "v3 base MUST import GENERIC — the Mixamo Humanoid muscle-space retarget CONE-EXPLODES the " +
                "skinned mesh at runtime (86ca8rdkp); Generic binds the 18 clips by transform path onto the " +
                "mixamorig skeleton with no retarget");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                "v3 base MUST build its OWN avatar (CreateFromThisModel) — NOT CopyFromOther");
        }

        // (2) VALID avatar — else the transform-path clips can't bind (T-pose class). Also proves the FBX-7700
        // stray empty `Armature` node did not break avatar construction from the mixamorig skeleton.
        [Test]
        public void V3Base_ProducesValidAvatar()
        {
            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.V3RiggedFbxPath))
                if (obj is Avatar a) avatar = a;
            Assert.IsNotNull(avatar, "v3 base must produce an avatar (CreateFromThisModel)");
            Assert.IsTrue(avatar.isValid, "v3 base avatar must be VALID so the 18 clips bind to the skeleton " +
                "(the FBX-7700 stray Armature node must not break this)");
        }

        // (3) HEIGHT NORMALIZE — ConfigureV3BaseFbx normalizes the intrinsic import to ~1u so the avatar-root
        // scale maps directly onto on-screen height (an un-normalized import = a giant).
        [Test]
        public void V3Base_IntrinsicHeight_NormalizedToAboutOneUnit()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.V3RiggedFbxPath);
            Assert.IsNotNull(fbx, "the imported v3 base must load at " + CharacterAssetGen.V3RiggedFbxPath);

            var inst = Object.Instantiate(fbx);
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            Assert.Greater(rends.Length, 0, "the v3 base must have renderers to measure");
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            Object.DestroyImmediate(inst);

            Assert.That(h, Is.InRange(0.6f, 1.6f),
                $"v3 base height {h:F3}u must normalize to ~{CharacterAssetGen.TargetImportHeightU}u");
        }

        // (4) CLIP-CARRY guard — v3's skeleton must carry the CORE mixamorig bones the 18 existing clips drive
        // by transform path. Reds if a future v3 re-export drops a core bone (that limb then can't animate).
        [Test]
        public void V3Base_Skeleton_CarriesCoreMixamorigBones_ForClipTransformPathBinding()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.V3RiggedFbxPath);
            Assert.IsNotNull(fbx);
            var inst = Object.Instantiate(fbx);

            var present = new HashSet<string>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                int colon = n.LastIndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                present.Add(n);
            }
            Object.DestroyImmediate(inst);

            // Toe bones are core, NOT optional: the CrouchWalk (Sneak Walk) clip drives LeftToeBase by transform
            // path (SneakGaitCurveFix smooths its mid-cycle LeftToeBase quaternion spike — that fix is a no-op if
            // the rig drops the toe bone), so a v3 re-export dropping *toebase silently un-animates the toes.
            string[] core =
            {
                "hips", "spine", "spine1", "spine2", "neck", "head",
                "leftupleg", "leftleg", "leftfoot", "lefttoebase",
                "rightupleg", "rightleg", "rightfoot", "righttoebase",
                "leftshoulder", "leftarm", "leftforearm", "lefthand",
                "rightshoulder", "rightarm", "rightforearm", "righthand",
            };
            var missing = new List<string>();
            foreach (var bone in core)
                if (!present.Contains(bone)) missing.Add(bone);

            Assert.IsEmpty(missing,
                "v3 base is MISSING core mixamorig bones [" + string.Join(", ", missing) + "] — the 18 existing " +
                "clips drive these by transform path; a dropped core bone silently un-animates that limb. " +
                "(righthand is also the held-axe seat bone.)");
        }

        // (5) ROLLBACK-TARGET toggle (86cak9kau activated v3 as LIVE; superseded as LIVE by v4 activation 86catvb6u)
        // — v3's default stays ON so it remains the reachable ROLLBACK target: with v4 live, flipping
        // UseCastawayV4Default back to false makes UseCastawayV3 (still true) re-select v3. So FbxPath no longer
        // resolves to v3 while v4 is live — v4 takes precedence. This test now guards that v3's default is intact
        // (rollback available) AND that FbxPath resolves to the v4 base directly (v4-first).
        [Test]
        public void CastawayV3_DefaultStaysOn_AsRollbackTarget_UnderV4()
        {
            Assert.IsTrue(CharacterAssetGen.UseCastawayV3Default,
                "v3's default must stay ON as the ROLLBACK target under v4 (86catvb6u): with v4 live, a one-line " +
                "flip of UseCastawayV4Default back to false re-selects v3. Flipping v3's default off would remove the " +
                "rollback target (rollback would land on the v2 base instead of v3).");

            // DIRECT resolved-value assertion (the #263 NIT — don't re-derive FbxPath's own ternary): with v4 live,
            // FbxPath must be the v4 rigged base. Bites if the ternary is reordered so v3/v2 silently steals the path.
            Assert.AreEqual(CharacterAssetGen.V4RiggedFbxPath, CharacterAssetGen.FbxPath,
                "with v4 live, FbxPath must resolve to the v4 rigged base — v4 takes precedence over the v3 rollback " +
                "target, v2, and the old Idle base");
            Assert.AreNotEqual(CharacterAssetGen.V3RiggedFbxPath, CharacterAssetGen.FbxPath,
                "FbxPath must NOT resolve to v3 now that v4 is live (v3 is the rollback target, not the resolved path)");
        }

        // (6) v3 material source — the posterized diffuse the v3 URP/Unlit CastawayMat binds must be importable.
        [Test]
        public void V3Base_PosterizedDiffuse_IsImportable()
        {
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterAssetGen.V3DiffusePosterizedPngPath);
            Assert.IsNotNull(diffuse, "v3 posterized diffuse must import at " +
                CharacterAssetGen.V3DiffusePosterizedPngPath + " (the URP/Unlit _BaseMap albedo)");
        }
    }
}
