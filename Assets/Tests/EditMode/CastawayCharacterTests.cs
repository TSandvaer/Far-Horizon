using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode regression guards for the HYPER3D + MIXAMO castaway player avatar (ticket 86ca8rdkp — the
    /// generated chunky low-poly castaway that SUPERSEDES the Sketchfab "Mini Chibi Kid"). These assert the
    /// REAL serialized Boot scene (the bytes the exe loads) + the two-FBX Humanoid import config, so the bug
    /// CLASSES can't recur silently in headless CI before a Sponsor soak:
    ///
    ///   1. The player carries a serialized, SKINNED, BONED avatar — NOT a runtime-assembled hierarchy and
    ///      NOT the retired capsule placeholder (the legs-up serialization guard).
    ///   2. Idle.fbx is the with-skin character (GENERIC rig) with a looping Idle clip + a VALID avatar;
    ///      Walking.fbx carries a looping Walk clip (Generic, binds by transform path) — the T-pose-mid-walk
    ///      guard. (Mixamo clip takes are "mixamo.com" → renamed to CastawayIdle/CastawayWalk on import. The
    ///      GENERIC rig is the runtime-explosion fix — Humanoid retarget coned the mesh; 86ca8rdkp.)
    ///   3. The intrinsic-height normalization is configured (the un-normalized-giant guard).
    ///   4. The de-lit material binds texture_diffuse on _BaseMap (the flat toon look — catches a missing
    ///      texture grey).
    ///   5. The shipped scene's castaway reads CHUNKY (heads-tall in the toy band) — catches a swap back to
    ///      a realistic ~7-8-heads base.
    ///   6. The IDENTITY RECOLOR guard: the shirt region of texture_diffuse is warmed toward TAN (the
    ///      mustard-yellow generated shirt no longer dominates) WITHOUT flattening the gradient.
    /// </summary>
    public class CastawayCharacterTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private GameObject _player;

        private Scene OpenBootAndFindPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            _player = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var ctm = root.GetComponentInChildren<ClickToMove>(true);
                if (ctm != null) { _player = ctm.gameObject; break; }
            }
            Assert.IsNotNull(_player, "Boot scene must contain a player with ClickToMove");
            return scene;
        }

        // The CORE serialization guard: the shipped scene must already hold a SKINNED, BONED avatar. A
        // skinned mesh with a real bone hierarchy is baked into the FBX + serialized into the scene, so it
        // cannot diverge between editor capture and the standalone exe (the legs-up bug).
        [Test]
        public void Player_HasSerializedSkinnedBonedCastaway_NotPlaceholder()
        {
            OpenBootAndFindPlayer();

            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway, "the player must carry a CastawayCharacter avatar");

            var placeholder = _player.transform.Find("PlaceholderVisual");
            Assert.IsNull(placeholder, "the U3 capsule placeholder must be replaced by the castaway avatar");

            var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the castaway avatar must have a SkinnedMeshRenderer serialized in the scene");
            Assert.IsNotNull(smr.sharedMesh, "the skinned mesh must be present (FBX imported)");
            Assert.Greater(smr.sharedMesh.vertexCount, 100,
                "the skinned mesh must be a real model, not a placeholder primitive");

            Assert.IsNotNull(smr.bones, "the skinned mesh must reference a bone array");
            Assert.Greater(smr.bones.Length, 10,
                "a real rigged humanoid has a non-trivial bone hierarchy (the legs-up bug cannot occur on a " +
                "baked skeleton — this is the serialization guard)");

            var animator = castaway.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, "the avatar must have an Animator serialized in the scene");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "the Animator must reference the serialized Idle/Walk controller (not assembled in Awake)");
        }

        // Mechanized "editor build == shipped build": re-running the editor avatar build must reproduce the
        // SAME serialized hierarchy shape (skinned mesh + same bone count + one Model child).
        [Test]
        public void EditorRebuild_Reproduces_SameSerializedShape()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            var smrBefore = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int bonesBefore = smrBefore != null && smrBefore.bones != null ? smrBefore.bones.Length : 0;

            castaway.BuildInEditor();

            var smrAfter = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int bonesAfter = smrAfter != null && smrAfter.bones != null ? smrAfter.bones.Length : 0;

            Assert.IsNotNull(smrAfter, "rebuild must reproduce a SkinnedMeshRenderer");
            Assert.AreEqual(bonesBefore, bonesAfter,
                "editor rebuild must reproduce the same bone count (no editor-vs-shipped divergence)");
            Assert.AreEqual(1, castaway.transform.childCount,
                "rebuild must be idempotent — exactly one Model child (no duplication)");
        }

        // The clip-binding guard: Idle.fbx must carry a looping Idle clip + a VALID HUMANOID avatar, and
        // Walking.fbx must carry a looping Walk clip. A missing / non-looping clip is the T-pose / freeze-
        // mid-walk failure mode; a null/invalid avatar freezes the model in its bind pose. The Mixamo clips
        // export as the take "mixamo.com" — renamed to CastawayIdle/CastawayWalk on import.
        [Test]
        public void Fbx_IsHyper3DCastaway_WithLoopingIdleAndWalk_AndHumanoidAvatar()
        {
            Assert.AreEqual("Assets/Art/Character/Castaway/Idle.fbx", CharacterAssetGen.IdleFbxPath,
                "the player FBX must be the Hyper3D castaway Idle FBX (with skin)");
            Assert.AreEqual("Assets/Art/Character/Castaway/Walking.fbx", CharacterAssetGen.WalkFbxPath,
                "the Walk clip must come from the Hyper3D castaway Walking FBX (without skin)");

            var idleImporter = AssetImporter.GetAtPath(CharacterAssetGen.IdleFbxPath) as ModelImporter;
            Assert.IsNotNull(idleImporter, "Idle.fbx must be importable at " + CharacterAssetGen.IdleFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, idleImporter.animationType,
                "Idle.fbx must import as a GENERIC rig — the Humanoid muscle-space retarget EXPLODED the skinned " +
                "mesh into a cone at runtime in the production scene (86ca8rdkp); Generic binds the clip by " +
                "transform path onto the mixamorig skeleton, no retarget, and renders clean (spike captures 04/05)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, idleImporter.avatarSetup,
                "Idle.fbx must build its OWN avatar (CreateFromThisModel) or clips will not bind (T-pose)");

            // Idle.fbx must produce a VALID avatar.
            Avatar idleAvatar = null;
            AnimationClip idle = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.IdleFbxPath))
            {
                if (obj is Avatar a) idleAvatar = a;
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    c.name.Contains(CharacterAssetGen.IdleClip)) idle = c;
            }
            Assert.IsNotNull(idleAvatar, "Idle.fbx must produce an avatar");
            Assert.IsTrue(idleAvatar.isValid,
                "Idle.fbx avatar must be VALID (so the clips bind to the skeleton)");
            Assert.IsNotNull(idle, "Idle.fbx must contain a clip matching '" + CharacterAssetGen.IdleClip + "'");
            Assert.IsTrue(idle.isLooping, CharacterAssetGen.IdleClip + " must loop (no freeze-on-idle)");

            // Walking.fbx must carry a looping Walk clip (Generic, CreateFromThisModel — binds by path).
            var walkImporter = AssetImporter.GetAtPath(CharacterAssetGen.WalkFbxPath) as ModelImporter;
            Assert.IsNotNull(walkImporter, "Walking.fbx must be importable at " + CharacterAssetGen.WalkFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, walkImporter.animationType,
                "Walking.fbx must import as a GENERIC rig (the Walk clip binds by transform path, no retarget)");
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, walkImporter.avatarSetup,
                "Walking.fbx must create its own avatar (Generic path binds by transform path onto Idle's mesh)");
            AnimationClip walk = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.WalkFbxPath))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") &&
                    c.name.Contains(CharacterAssetGen.WalkClip)) walk = c;
            Assert.IsNotNull(walk, "Walking.fbx must contain a clip matching '" + CharacterAssetGen.WalkClip + "'");
            Assert.IsTrue(walk.isLooping, CharacterAssetGen.WalkClip + " must loop (no freeze-mid-walk)");
        }

        // The intrinsic-height-normalization guard: the imported Idle FBX's bounds height must normalize
        // toward ~1u so the avatar-root scale maps directly onto on-screen height. An un-normalized import
        // scales the character to a giant whose head clips the frame.
        [Test]
        public void Fbx_IntrinsicHeight_IsNormalizedToAboutOneUnit()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            Assert.IsNotNull(fbx, "the imported FBX must load at " + CharacterAssetGen.IdleFbxPath);

            var inst = Object.Instantiate(fbx);
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            Assert.Greater(rends.Length, 0, "the imported model must have renderers to measure");
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            Object.DestroyImmediate(inst);

            Assert.That(h, Is.InRange(0.6f, 1.6f),
                $"imported model height {h:F3}u must be normalized to ~{CharacterAssetGen.TargetImportHeightU}u " +
                "(an un-normalized import scales the avatar to a giant — the framing defect)");
        }

        // The UPRIGHT guard: the castaway imports standing upright (head world-Y above feet, feet near
        // origin) so localPosition zero grounds the feet. A future import-config change that lays it on its
        // back (the Y-up trap) would fail here before it ships face-down.
        [Test]
        public void Fbx_ImportsUpright_HeadAboveFeet_FeetNearOrigin()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.IdleFbxPath);
            Assert.IsNotNull(fbx);
            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var rends = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Transform head = CastawayProportions.FindHeadBone(inst.transform);
            Assert.IsNotNull(head, "the castaway must carry a Head bone");
            float headY = head.position.y;
            float feetY = b.min.y;
            Object.DestroyImmediate(inst);

            Assert.Greater(headY, feetY + 0.2f,
                "the head must sit well ABOVE the feet (upright — not laid on its back / Y-up trap)");
            Assert.That(feetY, Is.InRange(-0.25f, 0.35f),
                $"the feet must sit near the model origin (feetY={feetY:F3}) so localPosition zero grounds them");
        }

        // The DE-LIT MATERIAL guard: the authored CastawayMat must bind texture_diffuse on _BaseMap. The
        // flat toon look (de-lit albedo) must ship — a missing-texture grey is a silent identity loss. The
        // shirt recolor repaints THIS diffuse, so the binding must survive.
        [Test]
        public void Material_BindsTheDiffuseToonAtlas()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CharacterAssetGen.MaterialPath);
            Assert.IsNotNull(mat, "the de-lit CastawayMat must exist at " + CharacterAssetGen.MaterialPath);
            Assert.IsTrue(mat.HasProperty("_BaseMap"), "the material must have a _BaseMap");
            var tex = mat.GetTexture("_BaseMap");
            Assert.IsNotNull(tex, "the material must bind a diffuse texture on _BaseMap (else it ships grey)");
            Assert.IsTrue(tex.name.Contains(CharacterAssetGen.DiffuseTextureName),
                "the bound _BaseMap must be the toon diffuse '" + CharacterAssetGen.DiffuseTextureName +
                "' — else the flat toon look ships as grey/wrong");
        }

        // The CHUNKY guard: the shipped scene's castaway must read in the toy proportion band. This catches a
        // REGRESSION to a realistic many-heads base. Measured via BakeMesh (render-state-independent — the
        // deserialized-SMR stale-bounds trap, unity-conventions.md §Editor-vs-runtime).
        [Test]
        public void Castaway_ReadsChunky_HeadsTallInToyBand()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            Assert.IsNotNull(CastawayProportions.FindHeadBone(castaway.transform),
                "the avatar must carry a Head bone for the proportion measure");

            float heads = CastawayProportions.MeasureHeadsTall(castaway);
            Assert.IsFalse(float.IsNaN(heads), "heads-tall must measure (skinned mesh + head bone present)");
            Assert.That(heads, Is.InRange(CastawayProportions.MinHeadsTall, CastawayProportions.MaxHeadsTall),
                $"the shipped castaway must read CHUNKY (heads-tall {heads:F2} in the toy band " +
                $"{CastawayProportions.MinHeadsTall}-{CastawayProportions.MaxHeadsTall}) — a realistic " +
                "~7-8-heads value means a regression to a non-chunky base");
        }

        // ARM-POSE WIRING guard (86ca8rdkp soak-fixes #2 + #3): the shipped scene's avatar must carry a
        // serialized CastawayArmPose with BOTH upper-arm bones resolved (mixamorig:RightArm / LeftArm) and a
        // non-trivial relax + right-carry offset. This is the component-in-source-but-not-serialized-into-scene
        // guard (unity-conventions.md): the arm-pose driver could compile + pass script tests while the scene
        // never carries it (the arms ship pinched). The bones must be the UPPER arms (not the clavicle /
        // forearm), so a future bone-resolution regression reds here.
        [Test]
        public void Avatar_HasSerializedArmPose_WithUpperArmBonesAndNonTrivialOffsets()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            var pose = castaway.GetComponent<CastawayArmPose>();
            Assert.IsNotNull(pose, "the avatar must carry a serialized CastawayArmPose (the #2/#3 soak-fix " +
                "driver) — a component in source but absent from the scene ships the arms pinched");

            Assert.IsNotNull(pose.rightUpperArm, "CastawayArmPose must have the RIGHT upper-arm bone wired");
            Assert.IsNotNull(pose.leftUpperArm, "CastawayArmPose must have the LEFT upper-arm bone wired");
            // The resolved bones must be the UPPER arms (mixamorig:RightArm/LeftArm) — exact token, NOT the
            // clavicle (RightShoulder) or the forearm (RightForeArm).
            Assert.AreEqual("mixamorig:RightArm", pose.rightUpperArm.name,
                "the right arm-pose bone must be the UPPER arm 'mixamorig:RightArm' (not shoulder/forearm)");
            Assert.AreEqual("mixamorig:LeftArm", pose.leftUpperArm.name,
                "the left arm-pose bone must be the UPPER arm 'mixamorig:LeftArm' (not shoulder/forearm)");

            // The offsets must be NON-TRIVIAL (a zeroed pose = the arms ship pinched again — #3 regresses).
            Assert.Greater(pose.relaxSpreadDeg, 1f,
                "the idle-relax spread must be non-trivial (>1°) or the arms ship pinched (#3 regression)");
            // The right arm must get an EXTRA away-from-body + raised carry (#2).
            Assert.Greater(pose.rightCarryExtraSpreadDeg + pose.rightCarryRaiseDeg, 1f,
                "the RIGHT-arm carry (extra spread + raise) must be non-trivial (#2 regression)");
        }

        // GROUND-SNAP wiring guard (86ca8rdkp soak-fix #1 — 'walking in the air'): the shipped avatar must
        // ship with the ground-snap ENABLED and its raycast mask wired to a real layer (not 0/Nothing, which
        // would make the snap a no-op and the feet float above the visible terrain). The behavioral proof is
        // the PlayMode CastawayGroundSnapPlayModeTests; this pins the serialized scene config.
        [Test]
        public void Avatar_GroundSnapEnabled_WithRealMask()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);
            Assert.IsTrue(castaway.groundSnap,
                "the avatar must ship with groundSnap ENABLED (else the feet float above the visible terrain " +
                "— the 'walking in the air' soak bug)");
            Assert.AreNotEqual(0, castaway.groundMask.value,
                "the ground-snap raycast mask must be wired to a real layer (0/Nothing = the snap is a no-op)");
        }

        // The IDENTITY RECOLOR guard (86ca8rdkp AC4): the generated MUSTARD-YELLOW shirt is warmed toward
        // TAN (the concept read) WITHOUT flattening the toon gradient. We read the SAME diffuse the material
        // binds and assert: (1) the saturated-yellow shirt band is now SMALL (the bulk was remapped), and
        // (2) the remapped region reads warm-tan (R>G>B, hue below the yellow band) with REAL value variation
        // (the gradient survived — not a flat tint). Catches a regression that drops the recolor (mustard
        // returns) OR flattens it (gradient lost).
        [Test]
        public void Diffuse_ShirtWarmedToTan_NotMustardYellow_GradientPreserved()
        {
            string path = CharacterAssetGen.DiffusePngPath;
            Assert.IsTrue(System.IO.File.Exists(path), "the bound diffuse PNG must exist at " + path);
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(path)), "diffuse PNG must decode");

            var pixels = tex.GetPixels();
            int saturatedYellow = 0;   // pixels still in the mustard-yellow band (should be ~0 after recolor)
            int warmTan = 0;           // pixels in the post-recolor warm-tan band
            var tanValues = new System.Collections.Generic.List<float>();
            foreach (var c in pixels)
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float hueDeg = h * 360f;
                bool inYellowBand = hueDeg >= CharacterAssetGen.ShirtHueMinDeg &&
                                    hueDeg <= CharacterAssetGen.ShirtHueMaxDeg &&
                                    s >= CharacterAssetGen.ShirtSatMin && v >= CharacterAssetGen.ShirtValMin;
                if (inYellowBand) saturatedYellow++;
                // warm tan: hue near the target (28-38°), warm (R>G>B), reasonably lit.
                if (hueDeg >= 26f && hueDeg <= 40f && c.r > c.g && c.g > c.b && v >= CharacterAssetGen.ShirtValMin)
                {
                    warmTan++;
                    tanValues.Add(v);
                }
            }
            Object.DestroyImmediate(tex);

            float yellowFrac = (float)saturatedYellow / pixels.Length;
            float tanFrac = (float)warmTan / pixels.Length;

            // (1) The mustard-yellow shirt band must be largely GONE (the source band measured ~12.9% of the
            // atlas; after the remap the saturated-yellow shirt is recolored, so this must drop well below 2%).
            Assert.Less(yellowFrac, 0.02f,
                $"the saturated mustard-yellow shirt band must be remapped away (still {yellowFrac:P1} of the " +
                "atlas in the yellow band — the recolor did not run / regressed)");

            // (2) A real warm-tan region must now exist (the shirt landed in the tan band).
            Assert.Greater(tanFrac, 0.04f,
                $"a real warm-tan shirt region must exist after the recolor (only {tanFrac:P1} in the tan band — " +
                "the shirt did not land warm-tan)");

            // (3) The gradient survived: the tan region carries real VALUE variation (a flat tint would have
            // ~zero std-dev). Std-dev of HSV value over the tan pixels must be non-trivial.
            float mean = 0f; foreach (var v in tanValues) mean += v; mean /= Mathf.Max(1, tanValues.Count);
            float var2 = 0f; foreach (var v in tanValues) var2 += (v - mean) * (v - mean);
            var2 /= Mathf.Max(1, tanValues.Count);
            float std = Mathf.Sqrt(var2);
            Assert.Greater(std, 0.03f,
                $"the tan shirt must keep its toon light→dark gradient (value std {std:F3} > 0.03); a near-zero " +
                "std means the recolor flattened the gradient (the per-material-tint trap)");
        }
    }
}
