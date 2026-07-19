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

        // CONSERVATIVE-DEFAULT + SEED-LOGIC guard (RE-SOAK — the Sponsor's "the auto pose made it even WORSE,
        // axe held too high/forward"). Two contracts the re-soak default rests on:
        //   (1) the named deg fields SEED the per-arm eulers correctly: RebuildCached() with seed=true derives
        //       rightArmEuler/leftArmEuler from the deg fields, the RIGHT arm gets MORE total offset than the
        //       left (the carry), and the default is CONSERVATIVE (bounded — not the prior too-high/forward pose);
        //   (2) the F9-nudge contract: once seedEulersFromDegFields is cleared (what the nudge tool does), a
        //       RebuildCached must NOT clobber a dialed euler. A regression that re-seeds on every rebuild would
        //       wipe the Sponsor's live dial.
        [Test]
        public void ArmPose_ConservativeDefaultSeed_RightHasMoreThanLeft_AndNudgeDialSurvivesRebuild()
        {
            var go = new GameObject("ArmPoseSeedProbe");
            var pose = go.AddComponent<CastawayArmPose>();

            // (1) Seed from the deg fields (the authored default path).
            pose.seedEulersFromDegFields = true;
            pose.RebuildCached();
            Assert.That(pose.leftArmEuler.x, Is.EqualTo(pose.relaxSpreadDeg).Within(1e-3f),
                "the LEFT arm euler X must seed to the relax spread");
            Assert.That(pose.rightArmEuler.x, Is.EqualTo(pose.relaxSpreadDeg + pose.rightCarryExtraSpreadDeg).Within(1e-3f),
                "the RIGHT arm euler X must seed to relax + extra carry spread");
            Assert.That(pose.rightArmEuler.z, Is.EqualTo(pose.rightCarryRaiseDeg).Within(1e-3f),
                "the RIGHT arm euler Z must seed to the carry raise");
            // RIGHT total offset > LEFT (the carry adds spread + raise on top of the shared relax).
            Assert.Greater(pose.rightArmEuler.magnitude, pose.leftArmEuler.magnitude + 0.5f,
                "the RIGHT arm must carry MORE total offset than the LEFT (the held-axe carry, #2)");
            // CONSERVATIVE: the default must be a small nudge, NOT the prior 16°+20° "too high/forward" pose.
            // Cap every component well under the rejected magnitude so a regression back to it reds here.
            Assert.Less(pose.rightArmEuler.magnitude, 18f,
                $"the RIGHT-arm default must be CONSERVATIVE (got {pose.rightArmEuler.magnitude:F1}° total) — the " +
                "Sponsor rejected the prior too-high/forward pose; the in-game F9 dial finalizes it");
            Assert.Less(pose.relaxSpreadDeg, 14f,
                "the relax spread default must be conservative (arms only SLIGHTLY off the torso)");

            // (2) The F9-nudge contract: clear the seed flag (what the nudge tool does), dial an euler, then
            // RebuildCached must KEEP the dialed value (not re-seed over it).
            pose.seedEulersFromDegFields = false;
            pose.rightArmEuler = new Vector3(5f, 0f, 3f); // a Sponsor "dial"
            pose.RebuildCached();
            Assert.AreEqual(new Vector3(5f, 0f, 3f), pose.rightArmEuler,
                "with seedEulersFromDegFields cleared (the nudge tool's state), RebuildCached must NOT clobber " +
                "the dialed euler — else the Sponsor's live F9 dial would be wiped");

            Object.DestroyImmediate(go);
        }

        // RE-SOAK #1 BAKED-DIAL guard (86ca8rdkp re-soak): the Sponsor dialed the held axe + the arm pose
        // in-game via F9 and reported the values; they must SHIP as the baked defaults (what-he-dialed-is-what-
        // ships). The held-axe values live as MovementCameraScene constants; the arm-pose eulers live as the
        // CastawayArmPose serialized defaults with seedEulersFromDegFields FALSE (so a RebuildCached can't re-
        // derive over the dialed values). A regression that reverts to the "reasonable" pre-dial defaults — or
        // that flips the seed flag back on (which would clobber the dial) — reds here.
        [Test]
        public void ReSoak_HeldAxeAndArmPose_ShipTheSponsorDialedValues()
        {
            // Held axe — HAND-LOCAL offset + hand-relative euler (the F9 nudge fields). 86caa83wn soak #4 (the
            // seat-doesn't-stick fix): the offset is now HAND-LOCAL END TO END (dialed/displayed/baked in the
            // hand frame, NO WORLD-frame round-trip — that round-trip is what made the dialed seat facing-
            // specific). The default is the soak-#3 APPROVED spawn seat (old world (0.0707,-0.1988,-0.0111))
            // expressed hand-local. soak #5 (build 2d90a68): the Sponsor LOCKED the FINAL hand-local seat via the
            // F9 panel. 86cabh907 soak ROUND 2 (PR #100): the Sponsor re-dialed the seat in the shipped build;
            // recovered from Player.log (Danish-locale decimals) and set as the NEW STARTING POINT for the
            // re-soak — offset (0.1712,0.1209,-0.0007), euler (-186,-168,-84), SUPERSEDING the soak-#5 lock
            // (0.1312,0.1409,0.0593)/(12,-8,-82). NOT a final bake (re-confirmed in the re-soak).
            Assert.That(MovementCameraScene.HeldAxeLocalOffsetFromHand.x, Is.EqualTo(0.1712f).Within(1e-4f));
            Assert.That(MovementCameraScene.HeldAxeLocalOffsetFromHand.y, Is.EqualTo(0.1209f).Within(1e-4f));
            Assert.That(MovementCameraScene.HeldAxeLocalOffsetFromHand.z, Is.EqualTo(-0.0007f).Within(1e-4f));
            Assert.That(MovementCameraScene.HeldAxeRelEuler.x, Is.EqualTo(-186.0f).Within(1e-3f));
            Assert.That(MovementCameraScene.HeldAxeRelEuler.y, Is.EqualTo(-168.0f).Within(1e-3f));
            Assert.That(MovementCameraScene.HeldAxeRelEuler.z, Is.EqualTo(-84.0f).Within(1e-3f));

            // Arm pose — the shipped scene's CastawayArmPose must carry the dialed eulers, seed flag OFF.
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            var pose = castaway.GetComponent<CastawayArmPose>();
            Assert.IsNotNull(pose, "the avatar must carry CastawayArmPose");
            Assert.IsFalse(pose.seedEulersFromDegFields,
                "the shipped arm pose must NOT re-seed from the deg fields (else a RebuildCached clobbers the " +
                "Sponsor's baked F9 dial — re-soak #1)");
            Assert.That(pose.rightArmEuler, Is.EqualTo(new Vector3(-4.0f, -50.0f, -3.0f)),
                "the RIGHT arm euler must ship the Sponsor's dialed value (-4,-50,-3)");
            Assert.That(pose.leftArmEuler, Is.EqualTo(new Vector3(-5.0f, 22.0f, 0.0f)),
                "the LEFT arm euler must ship the Sponsor's dialed value (-5,22,0)");
        }

        // RE-SOAK #2 contact-shadow wiring guard (86ca8rdkp re-soak — 'he STILL seems elevated'): the foot-
        // trace OVERTURNED the feet-float hypothesis (feet planted to ~3mm) — the real cause was the BlobShadow
        // stranded ~9cm ABOVE the snapped feet (body floats above its own shadow). The fix wires the shadow
        // onto CastawayCharacter so it grounds to the snapped feet. This pins the serialized wiring; the
        // behavioral proof is CastawayGroundSnapPlayModeTests.BlobShadow_GroundsToTheSnappedFeet.
        [Test]
        public void Avatar_BlobShadowWiredToCastaway_ForContactGrounding()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway.blobShadow,
                "CastawayCharacter.blobShadow must be wired (else the contact shadow strands above the snapped " +
                "feet on the dipping foreshore — the 'elevated' re-soak #2 percept)");
            Assert.AreEqual(MovementCameraScene.BlobShadowObjectName, castaway.blobShadow.name,
                "the wired shadow must be the BlobShadow");
        }

        // RE-SOAK #4 finger-curl wiring guard (86ca8rdkp re-soak — 'his right finger is mangled'): the finger-
        // trace OVERTURNED the skinning hypothesis (the weights/bones are clean) — the 'mangled' read was the
        // OPEN clip hand around a held haft. The fix is a HasAxe-gated finger-curl driver. The shipped avatar
        // must carry CastawayFingerCurl with the right-hand finger bones resolved (the component-in-source-but-
        // -not-in-scene guard). Behavioral proof: CastawayFingerCurlPlayModeTests.
        [Test]
        public void Avatar_HasSerializedFingerCurl_WithRightHandFingerBones()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            var curl = castaway.GetComponent<CastawayFingerCurl>();
            Assert.IsNotNull(curl, "the avatar must carry a serialized CastawayFingerCurl (the #4 grip fix) — " +
                "a component in source but absent from the scene ships the open 'mangled' hand");
            Assert.IsNotNull(curl.fingerBones, "the finger-curl must have its finger bones wired");
            // Context: v2 (86cajx050) AND v3 (86cak9kau) are 41-bone Mixamo variants whose RIGHT hand carries ONLY
            // index 1-3 + thumb 1-3 (CastawayV2/V3HandAxisTrace: index/middle/ring=3/9, thumb=3/3, pinky=0/3 for
            // BOTH — v3's "16 finger bones" do NOT surface as separated mixamorig middle/ring/pinky curl targets),
            // so the fist-hand variants resolve 3 index bones today. The prior hardcoded `fistHandVariant ? 3 : 6`
            // floor could NOT re-tighten if a future rig gained middle/ring chains.
            // 86cakbe2v item 3 (Tess PR #264 coverage note 4): DERIVE the expected floor from the rig's ACTUAL
            // curl-resolvable finger bones instead of a hardcoded 3, so a future v3 re-export that ADDS separated
            // middle/ring curl chains RE-TIGHTENS this guard automatically. The curl resolves exactly the
            // MovementCameraScene.RightFingerCurlTokens the rig provides; count how many that rig carries (matched
            // by the SAME exact-token discipline the curl uses) and require the serialized curl to resolve every
            // one. A resolution FAILURE (curl wired to fewer bones than the rig actually provides) reds; and if a
            // richer rig ships later, the floor rises with it — no stale hardcoded 3 to maintain.
            var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the castaway must have a SkinnedMeshRenderer to read the rig's finger bones from");
            int rigCurlBones = 0;
            foreach (var tok in MovementCameraScene.RightFingerCurlTokens)
                foreach (var bone in smr.bones)
                    if (bone != null && ExactBoneToken(bone.name) == tok) { rigCurlBones++; break; }
            Assert.Greater(rigCurlBones, 0,
                "the rig must provide at least the right-hand index curl chain (else the grip has nothing to curl)");
            Assert.GreaterOrEqual(curl.fingerBones.Length, rigCurlBones,
                $"the finger-curl must resolve EVERY curl-resolvable right-hand finger bone the rig provides (rig " +
                $"carries {rigCurlBones} of the index/middle/ring curl tokens; curl resolved {curl.fingerBones.Length}) " +
                "— a partial resolve ships an incomplete grip. This floor is DERIVED from the rig so a future " +
                "re-export that adds middle/ring chains re-tightens it automatically (86cakbe2v item 3).");
            foreach (var b in curl.fingerBones)
            {
                Assert.IsNotNull(b, "every wired finger bone must be non-null");
                string n = b.name.ToLowerInvariant();
                Assert.IsTrue(n.Contains("righthand") && (n.Contains("index") || n.Contains("middle") || n.Contains("ring")),
                    $"finger-curl bone '{b.name}' must be a right-hand Index/Middle/Ring bone (not some other bone)");
            }
            Assert.Greater(curl.fingerCurlDeg, 5f,
                "the finger curl must be non-trivial (>5°) — a zeroed curl ships the open 'mangled' hand (#4 regression)");
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

        // UNIFIED-RATE / KILL-THE-BOB config guard (86ca8rdkp EXTENSIVE-DEBUG round — 'reaching a destination
        // causes a BOB'). The prior speed-adaptive split (60 moving / 18 rest) made the convergence rate JUMP
        // at the moving→rest transition, so the still-converging error visibly settled the instant the agent
        // stopped = the arrival bob. With the snap target now the STABLE BAKED sole (no animation-envelope
        // wobble), a SINGLE unified rate tracks the descending foreshore tightly while walking AND doesn't pop
        // at rest — no rate discontinuity at arrival, no bob. This pins: (1) the live snapRate is high enough to
        // keep the during-walk steady-state lag sub-cm, and (2) the snap uses ONE rate (no rest/move split) so
        // there is no rate jump at arrival. (snapRateRest/Move are retained as dead serialized fields.)
        [Test]
        public void Avatar_GroundSnap_UnifiedRate_HighEnoughForWalk_NoArrivalRateJump()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);
            // At 5.5u/s into the ~0.047u/u worst foreshore slope, dY/dt≈0.256u/s; steady-state lag = dY/dt /
            // snapRate must stay sub-cm. 0.256/rate < 0.01 → rate > ~26. Pin a comfortable floor.
            Assert.GreaterOrEqual(castaway.snapRate, 40f,
                "the unified snap rate must be high enough that the during-walk steady-state lag stays sub-cm " +
                "(0.256u/s worst descent / rate < ~0.6cm needs rate ≥ ~40)");
            // ActiveSnapRate is set to snapRate (the unified rate) every snap frame — proving the snap no longer
            // branches on moving-state for its rate (the rate jump at arrival = the bob). Verified end-to-end in
            // CastawayGroundSnapPlayModeTests.Snap_UsesUnifiedRate_NoMoveRestRateDiscontinuity.
        }

        // GROUND-Y-OFFSET knob guard (86ca8rdkp 4th-attempt — give the Sponsor the dial). The offset must SHIP
        // at a sane default (0 = plant on the geometric ground) so a fresh build is grounded out of the box,
        // and the field must exist for the F9 nudge tool's GROUND-Y target to drive. (The Sponsor bakes his
        // dialed value here after the soak; until then 0 is correct — the snap plants exactly on the sand.)
        [Test]
        public void Avatar_GroundYOffset_ShipsAtSaneDefault()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);
            Assert.That(castaway.groundYOffset, Is.EqualTo(0f).Within(0.5f),
                "the Sponsor-dialable groundYOffset must ship near 0 (plant on the geometric ground) — a large " +
                "baked-in offset means the feet plant off the visible sand unless the Sponsor re-dials");
        }

        // V3-ACTIVATION SERIALIZED-IDENTITY guard (86cakbe2v item 1 — Tess PR #264 coverage note 1). The existing
        // character guards (Player_HasSerializedSkinnedBonedCastaway / Castaway_ReadsChunky /
        // Material_BindsTheDiffuseToonAtlas) ALL pass for v2 AND v3 (verts>100, heads-tall band spans both,
        // "texture_diffuse" is a substring of both diffuse names) — so a STALE-committed Boot baked with v2 while
        // the const says v3 (the [[unity-procedural-committed-assets-go-stale]] class) would ship v2 and pass 100%
        // green; NOTHING asserts the serialized bytes against the configured default. This asserts the baked
        // skinned mesh's SOURCE FBX == the configured-default hero's FBX (CharacterAssetGen.FbxPath, which is what
        // BuildModel wires castaway.modelPrefab from). It BITES specifically when the baked character != the
        // configured default: a stale v2 Boot under a v3 const → the mesh's source path is v2's FBX != FbxPath (v3)
        // → RED. (CI is green because BootstrapProject.Run re-bakes from the const before EditMode; this is the
        // guard that reds if that re-bake is ever skipped/broken and the stale committed scene ships.)
        [Test]
        public void Boot_SerializedSkinnedMeshIdentity_MatchesConfiguredDefaultHero()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway, "the player must carry a CastawayCharacter avatar");

            var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the castaway must have a serialized SkinnedMeshRenderer");
            Assert.IsNotNull(smr.sharedMesh, "the skinned mesh must be present (FBX imported + baked into Boot)");

            string bakedMeshFbx = AssetDatabase.GetAssetPath(smr.sharedMesh);
            Assert.IsFalse(string.IsNullOrEmpty(bakedMeshFbx),
                "the baked skinned mesh must resolve to its source FBX asset (an orphan scene-local mesh means the " +
                "avatar was not instantiated from the imported hero FBX)");
            Assert.AreEqual(CharacterAssetGen.FbxPath, bakedMeshFbx,
                "the BAKED Boot skinned-mesh identity must be the CONFIGURED-DEFAULT hero's FBX (" +
                CharacterAssetGen.FbxPath + ") — a stale-committed Boot baked with a DIFFERENT hero than the const " +
                "selects (the unity-procedural-committed-assets-go-stale class) reds here even though the " +
                "verts/heads-tall/diffuse-substring guards pass for BOTH heroes");
        }

        // HELD-AXE SEAT-SELECTION guard (86cakbe2v item 2 — Tess PR #264 coverage note 2; extended to v4-first for
        // the 86catvb6u activation). The held-axe seat is chosen by a per-hero ternary in MovementCameraScene.
        // BuildModel (UseCastawayV4 ? HeldAxeV4* : UseCastawayV3 ? HeldAxeV3* : UseCastawayV2 ? HeldAxeV2* :
        // HeldAxe*). HeldToolRigTests / HeldAxeSeatFacingIndependentTests pin the OLD-rig constants (rollback
        // guards that hold regardless of the live hero), so DROPPING the v4 ternary branch (falling to the v3/v2/old
        // seat) passes every EditMode test AND the held-belt capture gate (which asserts axe SHOWN + is-axe, not the
        // specific offset). This asserts the SERIALIZED Boot HeldAxeRig carries the seat the CONFIGURED-DEFAULT hero
        // selects: under v4, BuildModel must have baked HeldAxeV4* into the rig. Drop the v4 branch → the rig bakes
        // v3/v2/old values → RED. Mirrors the source precedence so a rollback build stays guarded too. Hand-parented + active
        // (visibility is gated by HeldAxe on renderer.enabled, not GameObject active), so it deserializes here.
        [Test]
        public void Boot_SerializedHeldAxeSeat_MatchesConfiguredDefaultHeroSeat()
        {
            OpenBootAndFindPlayer();
            var rig = _player.GetComponentInChildren<HeldAxeRig>(true);
            Assert.IsNotNull(rig, "the Boot scene must carry the serialized held-axe rig (the axe is hand-parented, " +
                "visibility-gated on HasAxe — the GameObject is present + active, only its renderers are disabled)");

            Vector3 expectedOffset =
                CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.HeldAxeV4LocalOffsetFromHand :
                CharacterAssetGen.UseCastawayV3 ? MovementCameraScene.HeldAxeV3LocalOffsetFromHand :
                CharacterAssetGen.UseCastawayV2 ? MovementCameraScene.HeldAxeV2LocalOffsetFromHand :
                                                  MovementCameraScene.HeldAxeLocalOffsetFromHand;
            Vector3 expectedEuler =
                CharacterAssetGen.UseCastawayV4 ? MovementCameraScene.HeldAxeV4RelEuler :
                CharacterAssetGen.UseCastawayV3 ? MovementCameraScene.HeldAxeV3RelEuler :
                CharacterAssetGen.UseCastawayV2 ? MovementCameraScene.HeldAxeV2RelEuler :
                                                  MovementCameraScene.HeldAxeRelEuler;
            string hero = CharacterAssetGen.UseCastawayV4 ? "v4" : CharacterAssetGen.UseCastawayV3 ? "v3"
                        : CharacterAssetGen.UseCastawayV2 ? "v2" : "old";

            Assert.That((rig.worldOffsetFromHand - expectedOffset).magnitude, Is.LessThan(1e-3f),
                $"under the configured-default hero ({hero}) BuildModel must seat the axe with THAT hero's HAND-LOCAL " +
                $"offset (expected {expectedOffset:F4}, serialized {rig.worldOffsetFromHand:F4}) — dropping the v3 " +
                "ternary branch bakes the v2/old offset here (the seat-selection regression the capture gate can't see)");
            Assert.That((rig.relEuler - expectedEuler).magnitude, Is.LessThan(1e-2f),
                $"under the configured-default hero ({hero}) BuildModel must seat the axe with THAT hero's " +
                $"hand-relative euler (expected {expectedEuler:F1}, serialized {rig.relEuler:F1}) — dropping the v3 " +
                "ternary branch bakes the v2/old euler here");
        }

        // (86cakbe2v item 4 — RETIRED) The old IDENTITY-RECOLOR guard `Diffuse_ShirtWarmedToTan_...` was removed
        // here. It read CharacterAssetGen.DiffusePngPath (the OLD base's texture_diffuse.png) off disk directly and
        // asserted the mustard-yellow shirt was warmed to tan — a v1/OLD-base operation. The live hero is now v3
        // (UseCastawayV3Default; v2 is the shallow-rollback target), and the shirt-recolor path is OLD-castaway-only
        // and DORMANT (BuildMaterial gates it behind `!UseCastawayV2` — CharacterAssetGen.cs), so this test was dead
        // coverage for the live hero: it passed on a STALE committed PNG regardless of which base actually ships
        // (Tess PR #264 coverage note 5). Retired rather than repointed — v3's posterized diffuse has no
        // mustard-shirt recolor to assert. If a future DEEP-rollback re-activates the old base, re-introduce a
        // recolor guard gated on that base being live (not an unconditional off-disk read).

        // Mirror of MovementCameraScene.ExactBoneToken (private there): strip the "mixamorig:" namespace + lower-case
        // for an exact bone-token compare (86cakbe2v item 3 — matches the curl's own exact-token resolution).
        private static string ExactBoneToken(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return "";
            string n = boneName.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            return n;
        }
    }
}
