using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode regression guards for the CASTAWAY v4 (chamfered-blocky "wooden toy" hand-model — ticket
    /// 86catpwc4 dormant integration, ACTIVATED 86catvb6u). v4 is now the LIVE default hero; v3 is the ROLLBACK
    /// target (its default stays ON — a one-line flip re-selects it). These assert the v4 base FBX import CONFIG +
    /// the ACTIVATION toggle so the bug CLASSES can't recur silently:
    ///
    ///   1. v4 base imports GENERIC + CreateFromThisModel — the anti-Humanoid gate (Humanoid muscle-space
    ///      retarget CONE-EXPLODES the skinned mesh at runtime; 86ca8rdkp). THE regression guard: a future edit
    ///      reverting v4 to Humanoid / CopyFromOther reds HERE before it ships a coned mesh.
    ///   2. v4 base produces a VALID avatar (else the 18 clips can't bind → T-pose) — also proves the FBX-7700
    ///      stray empty `Armature` node did NOT break avatar construction.
    ///   3. v4 base height-normalizes toward ~1u (the un-normalized-giant guard).
    ///   4. v4's skeleton carries the CORE mixamorig bones the 18 existing clips drive — the CLIP-CARRY guard.
    ///   5. THE ACTIVATION-TOGGLE guard (86catvb6u): the rollout toggle DEFAULTS ON — v4 is the live hero. DIRECT
    ///      coverage (the #307 NIT): FbxPath resolves to v4 (not merely 'not v3'); v3's default stays ON so a
    ///      one-line flip rolls back to v3. A future edit that flips the default off (rollback) reds this.
    ///   6. v4's flat palette (the URP/Lit Base Map) is importable.
    ///
    /// v4 is configured on EVERY bootstrap (CharacterAssetGen.ConfigureV4BaseFbx) so these import guards run
    /// against a real import regardless of the toggle state (they held while v4 was dormant; they hold now it is live).
    /// </summary>
    public class CastawayV4BaseTests
    {
        // (1) ANTI-HUMANOID import guard — THE regression guard for the v4 integration.
        [Test]
        public void V4Base_ImportsGeneric_NotHumanoid_CreateFromThisModel()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx",
                CharacterAssetGen.V4RiggedFbxPath, "the v4 base must be the committed hand-model rigged FBX (Mixamo Idle.fbx)");

            var importer = AssetImporter.GetAtPath(CharacterAssetGen.V4RiggedFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, "the v4 base FBX must be importable at " + CharacterAssetGen.V4RiggedFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                "v4 base MUST import GENERIC — the Mixamo Humanoid muscle-space retarget CONE-EXPLODES the " +
                "skinned mesh at runtime (86ca8rdkp); Generic binds the 18 clips by transform path onto the " +
                "mixamorig skeleton with no retarget");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                "v4 base MUST build its OWN avatar (CreateFromThisModel) — NOT CopyFromOther");
        }

        // (2) VALID avatar — else the transform-path clips can't bind (T-pose class). Also proves the FBX-7700
        // stray empty `Armature` node did not break avatar construction from the mixamorig skeleton.
        [Test]
        public void V4Base_ProducesValidAvatar()
        {
            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.V4RiggedFbxPath))
                if (obj is Avatar a) avatar = a;
            Assert.IsNotNull(avatar, "v4 base must produce an avatar (CreateFromThisModel)");
            Assert.IsTrue(avatar.isValid, "v4 base avatar must be VALID so the 18 clips bind to the skeleton " +
                "(the FBX-7700 stray Armature node must not break this)");
        }

        // (3) HEIGHT NORMALIZE — ConfigureV4BaseFbx normalizes the intrinsic import to ~1u so the avatar-root
        // scale maps directly onto on-screen height (an un-normalized import = a giant; v4's raw mesh is 1.90 m
        // tall with the Mixamo 100x mesh-node scale).
        [Test]
        public void V4Base_IntrinsicHeight_NormalizedToAboutOneUnit()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.V4RiggedFbxPath);
            Assert.IsNotNull(fbx, "the imported v4 base must load at " + CharacterAssetGen.V4RiggedFbxPath);

            var inst = Object.Instantiate(fbx);
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            Assert.Greater(rends.Length, 0, "the v4 base must have renderers to measure");
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            Object.DestroyImmediate(inst);

            Assert.That(h, Is.InRange(0.6f, 1.6f),
                $"v4 base height {h:F3}u must normalize to ~{CharacterAssetGen.TargetImportHeightU}u");
        }

        // (4) CLIP-CARRY guard — v4's skeleton must carry the CORE mixamorig bones the 18 existing clips drive
        // by transform path. Reds if a future v4 re-export drops a core bone (that limb then can't animate).
        // v4 is the 33-bone rig (Index-chain fingers only, NO thumb bones) but ALL core locomotion/arm/toe bones
        // are present (phase-B raw parse), so the WITHOUT-skin clip set binds with no retarget — same as v2/v3.
        [Test]
        public void V4Base_Skeleton_CarriesCoreMixamorigBones_ForClipTransformPathBinding()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.V4RiggedFbxPath);
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
            // path (SneakGaitCurveFix smooths its mid-cycle LeftToeBase quaternion spike), so a v4 re-export
            // dropping *toebase silently un-animates the toes. righthand is also the held-axe seat bone.
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
                "v4 base is MISSING core mixamorig bones [" + string.Join(", ", missing) + "] — the 18 existing " +
                "clips drive these by transform path; a dropped core bone silently un-animates that limb. " +
                "(righthand is also the held-axe seat bone.)");
        }

        // (5) ACTIVATION toggle (86catvb6u — the v4 DEFAULT FLIP). v4 is now the LIVE default hero; v3 is the
        // ROLLBACK target. THE activation guard, with DIRECT coverage (86catvb6u NIT #307 — the dormant-phase test
        // gave only TRANSITIVE coverage of the resolved hero via the "stays v3" ternary; this asserts the RESOLVED
        // path IS v4 directly): (a) UseCastawayV4Default is true; (b) UseCastawayV4 resolves true; (c) FbxPath IS
        // the v4 rigged base (v4-first ternary now steals the path — the whole point of activation); (d) v3's
        // default STAYS true so a one-line rollback (flip UseCastawayV4Default back to false) cleanly re-selects v3.
        [Test]
        public void CastawayV4_Toggle_Activated_And_FbxPathResolvesToV4()
        {
            Assert.IsTrue(CharacterAssetGen.UseCastawayV4Default,
                "v4 is ACTIVATED (86catvb6u) — the default MUST be ON so v4 is the shipped hero. A revert to false " +
                "is the explicit ROLLBACK to v3, not the shipped state.");

            // DIRECT coverage of the resolved hero (the NIT): assert FbxPath IS v4, not merely 'not v3'.
            Assert.IsTrue(CharacterAssetGen.UseCastawayV4,
                "v4 default ON ⇒ UseCastawayV4 true regardless of the FARHORIZON_CASTAWAY_V4 env override");
            Assert.AreEqual(CharacterAssetGen.V4RiggedFbxPath, CharacterAssetGen.FbxPath,
                "with v4 ACTIVATED, FbxPath must resolve to the v4 rigged base — v4 is the live hero; the v4-first " +
                "ternary branch now owns the path");
            Assert.AreNotEqual(CharacterAssetGen.V3RiggedFbxPath, CharacterAssetGen.FbxPath,
                "FbxPath must NOT resolve to v3 now that v4 is live (v3 is the rollback target, not the resolved path)");

            // ROLLBACK reachability (direct): v3's own default stays ON, so flipping UseCastawayV4Default back to
            // false makes UseCastawayV3 (still true) re-select the v3 base — a one-line rollback, no other edit.
            Assert.IsTrue(CharacterAssetGen.UseCastawayV3Default,
                "v3 must stay reachable as the ROLLBACK target — its default stays ON so a single flip of " +
                "UseCastawayV4Default back to false re-selects v3 (mirrors how #264 kept v2 behind the v3 toggle)");
        }

        // (6) v4 material source — the flat palette the v4 URP/Lit CastawayMat binds must be importable.
        [Test]
        public void V4Base_Palette_IsImportable()
        {
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterAssetGen.V4PalettePngPath);
            Assert.IsNotNull(palette, "v4 palette must import at " +
                CharacterAssetGen.V4PalettePngPath + " (the URP/Lit _BaseMap)");
        }
    }
}
