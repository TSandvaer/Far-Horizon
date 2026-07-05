using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode regression guards for the CASTAWAY v2 (Rodin base — ticket 86cajwp23). These assert the v2
    /// base FBX import CONFIG + the staged-rollout toggle so the bug CLASSES can't recur silently:
    ///
    ///   1. v2 base imports GENERIC + CreateFromThisModel — the anti-Humanoid gate (Humanoid muscle-space
    ///      retarget CONE-EXPLODES the skinned mesh at runtime; 86ca8rdkp). This is THE regression guard: a
    ///      future edit reverting v2 to Humanoid / CopyFromOther reds HERE before it ships a coned mesh.
    ///   2. v2 base produces a VALID avatar (else the 18 clips can't bind → T-pose).
    ///   3. v2 base height-normalizes toward ~1u (the un-normalized-giant guard).
    ///   4. v2's skeleton carries the CORE mixamorig bones the 18 existing clips drive — the CLIP-CARRY guard
    ///      (catches a v2 re-export dropping a core bone → a limb the transform-path clips can't animate).
    ///   5. The staged-rollout toggle DEFAULTS OFF (AC4 — the OLD castaway stays live until the soak passes);
    ///      FbxPath + the v2 material textures track the toggle.
    ///
    /// v2 is configured on EVERY bootstrap (CharacterAssetGen.ConfigureV2BaseFbx, toggle-independent) so these
    /// import guards run against a real import even in the default (toggle-OFF) CI run.
    /// </summary>
    public class CastawayV2BaseTests
    {
        // (1) ANTI-HUMANOID import guard — THE regression guard for the v2 integration.
        [Test]
        public void V2Base_ImportsGeneric_NotHumanoid_CreateFromThisModel()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/v2/castaway_rigged_tpose.fbx",
                CharacterAssetGen.V2RiggedFbxPath, "the v2 base must be the committed Rodin rigged FBX");

            var importer = AssetImporter.GetAtPath(CharacterAssetGen.V2RiggedFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, "the v2 base FBX must be importable at " + CharacterAssetGen.V2RiggedFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType,
                "v2 base MUST import GENERIC — the Mixamo Humanoid muscle-space retarget CONE-EXPLODES the " +
                "skinned mesh at runtime (86ca8rdkp); Generic binds the 18 clips by transform path onto the " +
                "mixamorig skeleton with no retarget");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                "v2 base MUST build its OWN avatar (CreateFromThisModel) — NOT CopyFromOther; the clips bind by " +
                "transform path, no muscle-space retarget");
        }

        // (2) VALID avatar — else the transform-path clips can't bind (T-pose class).
        [Test]
        public void V2Base_ProducesValidAvatar()
        {
            Avatar avatar = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.V2RiggedFbxPath))
                if (obj is Avatar a) avatar = a;
            Assert.IsNotNull(avatar, "v2 base must produce an avatar (CreateFromThisModel)");
            Assert.IsTrue(avatar.isValid, "v2 base avatar must be VALID so the 18 clips bind to the skeleton");
        }

        // (3) HEIGHT NORMALIZE — the v2 base intrinsically imports at ~1.889m; ConfigureV2BaseFbx normalizes to
        // ~1u so the avatar-root scale maps directly onto on-screen height (an un-normalized import = a giant).
        [Test]
        public void V2Base_IntrinsicHeight_NormalizedToAboutOneUnit()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.V2RiggedFbxPath);
            Assert.IsNotNull(fbx, "the imported v2 base must load at " + CharacterAssetGen.V2RiggedFbxPath);

            var inst = Object.Instantiate(fbx);
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            Assert.Greater(rends.Length, 0, "the v2 base must have renderers to measure");
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            Object.DestroyImmediate(inst);

            Assert.That(h, Is.InRange(0.6f, 1.6f),
                $"v2 base height {h:F3}u must normalize to ~{CharacterAssetGen.TargetImportHeightU}u " +
                "(an un-normalized ~1.889m import scales the avatar to a giant)");
        }

        // (4) CLIP-CARRY guard — v2's skeleton must carry the CORE mixamorig bones the 18 existing clips drive
        // by transform path. The task's load-bearing claim ("v2's 41 bones ⊂ the clip skeleton → clips carry")
        // fails silently if a future v2 re-export drops a core bone (e.g. an arm/leg/hand) — that limb then has
        // no target and the clips can't animate it. This reds on such a regression. (Fingers are NOT required —
        // v2 is the no-finger variant; only the locomotion/interaction-critical bones are asserted.)
        [Test]
        public void V2Base_Skeleton_CarriesCoreMixamorigBones_ForClipTransformPathBinding()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.V2RiggedFbxPath);
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

            // The core Mixamo Standard hierarchy the locomotion + jump + chop + hit-react + held-axe systems need.
            string[] core =
            {
                "hips", "spine", "spine1", "spine2", "neck", "head",
                "leftupleg", "leftleg", "leftfoot",
                "rightupleg", "rightleg", "rightfoot",
                "leftshoulder", "leftarm", "leftforearm", "lefthand",
                "rightshoulder", "rightarm", "rightforearm", "righthand",
            };
            var missing = new List<string>();
            foreach (var bone in core)
                if (!present.Contains(bone)) missing.Add(bone);

            Assert.IsEmpty(missing,
                "v2 base is MISSING core mixamorig bones [" + string.Join(", ", missing) + "] — the 18 existing " +
                "clips drive these by transform path; a dropped core bone silently un-animates that limb. " +
                "(righthand is also the held-axe seat bone.)");
        }

        // (5) STAGED-ROLLOUT toggle (AC4) — the committed DEFAULT is OLD-live (v2 gated) so this PR is
        // behavior-neutral on the shipped base until the Sponsor soak passes. FbxPath tracks the toggle.
        [Test]
        public void CastawayV2_Toggle_DefaultsOff_And_FbxPathTracksToggle()
        {
            Assert.IsFalse(CharacterAssetGen.UseCastawayV2Default,
                "the committed default MUST be OLD-live (AC4 Sponsor-lock — v2 stays behind the toggle until the " +
                "soak passes; do NOT flip UseCastawayV2Default to true in this PR)");

            // FbxPath resolves to the toggle-selected mesh (env-driven; asserted against whatever the toggle is
            // in THIS run so it holds both in default CI and in a FARHORIZON_CASTAWAY_V2=1 soak build run).
            string expected = CharacterAssetGen.UseCastawayV2
                ? CharacterAssetGen.V2RiggedFbxPath
                : CharacterAssetGen.IdleFbxPath;
            Assert.AreEqual(expected, CharacterAssetGen.FbxPath,
                "FbxPath must resolve to the toggle-selected mesh (v2 rigged base when UseCastawayV2, else Idle.fbx)");
        }

        // (5b) v2 material sources — the de-lit diffuse + normal the v2 CastawayMat binds must be importable.
        [Test]
        public void V2Base_MaterialTextures_AreImportable()
        {
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterAssetGen.V2DiffusePngPath);
            Assert.IsNotNull(diffuse, "v2 de-lit diffuse must import at " + CharacterAssetGen.V2DiffusePngPath +
                " (the toon _BaseMap albedo — no shirt-recolor for v2)");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterAssetGen.V2NormalPngPath);
            Assert.IsNotNull(normal, "v2 normal map must import at " + CharacterAssetGen.V2NormalPngPath);
        }
    }
}
