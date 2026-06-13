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
    /// EditMode regression guards for the U6 castaway player avatar (ticket 86ca86fz9) — the rigged
    /// CC0 low-poly clothed character that replaces U3's capsule placeholder on the merged movement
    /// rig. These assert the REAL serialized Boot scene (the bytes the exe loads) + the FBX import
    /// config, so the bug CLASSES the spike hit (legs-up serialization divergence, T-pose-mid-walk
    /// non-looping clips, the featureless single-tone recolor, the un-normalized giant) can't recur
    /// silently in headless CI before a Sponsor soak:
    ///
    ///   1. The player carries a serialized, SKINNED, BONED avatar — NOT a runtime-assembled
    ///      hierarchy and NOT the retired capsule placeholder (the legs-up serialization guard).
    ///   2. The FBX is the clothed Animated-Men character with looping Man_Idle + Man_Walk clips and
    ///      a CreateFromThisModel avatar (the T-pose-mid-walk guard).
    ///   3. The intrinsic-height normalization is configured (the un-normalized-giant guard).
    ///   4. The per-part castaway recolor maps all six materials to a DISTINCT palette (the
    ///      featureless / erased-face guard).
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

        // The CORE serialization guard: the shipped scene must already hold a SKINNED, BONED avatar.
        // A skinned mesh with a real bone hierarchy is baked into the FBX + serialized into the scene,
        // so it cannot diverge between editor capture and the standalone exe the way the spike's
        // Awake-assembled procedural hierarchy did (the legs-up bug). If a future change reverts to a
        // runtime-assembled avatar OR keeps the capsule placeholder, this fails.
        [Test]
        public void Player_HasSerializedSkinnedBonedCastaway_NotPlaceholder()
        {
            OpenBootAndFindPlayer();

            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway, "the player must carry a CastawayCharacter avatar (U6 port)");

            // The retired U3 capsule placeholder must be gone.
            var placeholder = _player.transform.Find("PlaceholderVisual");
            Assert.IsNull(placeholder, "the U3 capsule placeholder must be replaced by the castaway avatar");

            var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the castaway avatar must have a SkinnedMeshRenderer serialized in the scene");
            Assert.IsNotNull(smr.sharedMesh, "the skinned mesh must be present (FBX imported)");
            Assert.Greater(smr.sharedMesh.vertexCount, 100,
                "the skinned mesh must be a real model, not a placeholder primitive");

            Assert.IsNotNull(smr.bones, "the skinned mesh must reference a bone array");
            Assert.Greater(smr.bones.Length, 10,
                "a real rigged humanoid has a non-trivial bone hierarchy (the legs-up bug cannot occur " +
                "on a baked skeleton — this is the serialization guard)");

            var animator = castaway.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, "the avatar must have an Animator serialized in the scene");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "the Animator must reference the serialized Idle/Walk controller (not assembled in Awake)");
        }

        // Mechanized "editor build == shipped build": re-running the editor avatar build must
        // reproduce the SAME serialized hierarchy shape (skinned mesh + same bone count + one Model
        // child). The legs-up bug was exactly an editor-vs-shipped MISMATCH; idempotent rebuild proves
        // there is no divergence to ship.
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

        // The clip-binding guard: the FBX must be the clothed Animated-Men character AND carry the
        // Man_Idle/Man_Walk locomotion clips, both LOOPING, with a CreateFromThisModel avatar. A
        // missing / non-looping clip is the T-pose / freeze-mid-walk failure mode; a null avatar
        // freezes the model in its bind pose. Pinning these catches an importer-config regression in
        // headless CI before it ships as a frozen avatar.
        [Test]
        public void Fbx_IsClothedCharacter_WithLoopingIdleAndWalkClips_AndAvatar()
        {
            Assert.AreEqual("Assets/Art/Character/Quaternius_AnimatedMan_SmoothCasual.fbx",
                CharacterAssetGen.FbxPath, "the player FBX must be the clothed Animated-Men character");

            var importer = AssetImporter.GetAtPath(CharacterAssetGen.FbxPath) as ModelImporter;
            Assert.IsNotNull(importer, "the character FBX must be importable at " + CharacterAssetGen.FbxPath);

            // CreateFromThisModel avatar so the Generic rig binds its own clips (the T-pose fix).
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                "the Generic rig must create its own avatar or clips will not bind (T-pose)");

            AnimationClip idle = null, walk = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.FbxPath))
            {
                if (obj is AnimationClip c)
                {
                    if (c.name.Contains(CharacterAssetGen.IdleClip)) idle = c;
                    if (c.name.Contains(CharacterAssetGen.WalkClip)) walk = c;
                }
            }
            Assert.IsNotNull(idle, "FBX must contain a " + CharacterAssetGen.IdleClip + " clip");
            Assert.IsNotNull(walk, "FBX must contain a " + CharacterAssetGen.WalkClip + " clip");
            Assert.IsTrue(idle.isLooping, CharacterAssetGen.IdleClip + " must loop (no freeze-on-idle)");
            Assert.IsTrue(walk.isLooping, CharacterAssetGen.WalkClip + " must loop (no freeze-mid-walk)");
        }

        // The intrinsic-height-normalization guard: the FBX importer's globalScale must normalize the
        // (~4.96u intrinsic) model down toward ~1u, so the avatar-root scale maps directly onto
        // on-screen height. An un-normalized import scales the character to a giant whose head clips
        // the frame (the spike's iter-7 v1 framing defect). We assert the imported model's actual
        // bounds height is in a sane normalized band, which is robust to small importer-scale drift.
        [Test]
        public void Fbx_IntrinsicHeight_IsNormalizedToAboutOneUnit()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            Assert.IsNotNull(fbx, "the imported FBX must load at " + CharacterAssetGen.FbxPath);

            var inst = Object.Instantiate(fbx);
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            Assert.Greater(rends.Length, 0, "the imported model must have renderers to measure");
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            Object.DestroyImmediate(inst);

            // Target is 1u; allow a generous band so the test guards against the un-normalized ~4.96u
            // giant (or a zero-scale collapse) without being brittle to ±0.2u importer rounding.
            Assert.That(h, Is.InRange(0.6f, 1.6f),
                $"imported model height {h:F3}u must be normalized to ~{CharacterAssetGen.TargetImportHeightU}u " +
                "(an un-normalized ~4.96u import scales the avatar to a giant — the framing defect)");
        }

        // The recolor guard: the castaway recolor must map the SIX per-part materials (Shirt / Skin /
        // Pants / Eyes / Socks / Hair) to DISTINCT palette colors. Catches (a) the recolor collapsing
        // every part to one tone (the featureless "not appealing" read), (b) dropping out to the
        // grey/magenta default, or (c) Eyes falling through to skin (which ERASES the face read).
        // Pure mapping function — no instantiation.
        [Test]
        public void CastawayRecolor_MapsSixPartMaterialsToDistinctPalette()
        {
            // U2-6 polish palette — brighter/higher-key per the v4 design reference (young + hopeful).
            var skin    = new Color(0.86f, 0.64f, 0.47f);
            var shirt   = new Color(0.72f, 0.60f, 0.42f);  // U2-6 Uma tune: warmer mid-khaki for torso/ground separation; luma 0.615 (still > 0.6 guard)
            var pants   = new Color(0.34f, 0.46f, 0.50f);
            var hair    = new Color(0.84f, 0.50f, 0.22f);
            var eyes    = new Color(0.18f, 0.13f, 0.11f);
            var leather = new Color(0.45f, 0.30f, 0.18f);

            Assert.AreEqual(shirt, CastawayCharacter.CastawayColorFor("Shirt", skin, shirt, pants, hair, eyes, leather),
                "a 'Shirt' material must recolor to the warm mid-khaki shirt tone");
            Assert.AreEqual(pants, CastawayCharacter.CastawayColorFor("Pants", skin, shirt, pants, hair, eyes, leather),
                "a 'Pants' material must recolor to the muted teal-blue rolled-trousers tone");
            Assert.AreEqual(hair, CastawayCharacter.CastawayColorFor("Hair", skin, shirt, pants, hair, eyes, leather),
                "a 'Hair' material must recolor to the copper/ginger hair tone");
            Assert.AreEqual(eyes, CastawayCharacter.CastawayColorFor("Eyes", skin, shirt, pants, hair, eyes, leather),
                "an 'Eyes' material must recolor to the dark eye tone (the face must read) — NOT skin");
            Assert.AreEqual(skin, CastawayCharacter.CastawayColorFor("Skin", skin, shirt, pants, hair, eyes, leather),
                "a 'Skin' material must recolor to the warm tan skin tone");
            Assert.AreEqual(skin, CastawayCharacter.CastawayColorFor("Socks", skin, shirt, pants, hair, eyes, leather),
                "a 'Socks' material reads as bare tan feet (castaway is barefoot) -> skin tone");
            Assert.AreEqual(skin, CastawayCharacter.CastawayColorFor("Body", skin, shirt, pants, hair, eyes, leather),
                "an unknown/'Body' material must fall back to skin (never the grey/magenta default)");

            // U2-6 leather accent: a belt / strap / satchel material maps to the leather tone, NOT to
            // pants or skin — this is the new detail-pass slot (regression guard for the fall-through
            // order: leather is tested before pants/skin).
            Assert.AreEqual(leather, CastawayCharacter.CastawayColorFor("Belt", skin, shirt, pants, hair, eyes, leather),
                "a 'Belt' material must read as warm leather (the U2-6 detail accent), not pants/skin");
            Assert.AreEqual(leather, CastawayCharacter.CastawayColorFor("Strap", skin, shirt, pants, hair, eyes, leather),
                "a 'Strap' material must read as warm leather (the crossbody-satchel accent)");

            Assert.AreNotEqual(shirt, pants, "shirt and pants must read distinctly");
            Assert.AreNotEqual(shirt, hair, "shirt and hair must read distinctly");
            Assert.AreNotEqual(pants, skin, "pants and skin must read distinctly");
            Assert.AreNotEqual(eyes, skin, "eyes and skin must read distinctly (the face must read)");
            Assert.AreNotEqual(leather, pants, "leather accent and pants must read distinctly");
            Assert.AreNotEqual(leather, skin, "leather accent and skin must read distinctly");

            // IDENTITY GUARD (U2-6): the shirt must read LIGHT / high-key, not a dark grizzled-survivor
            // tone. The iter-8 palette had drifted to a dark rust shirt (~0.42 green); the approved v4
            // identity is a light warm khaki. Pin the shirt's luminance high so a future dark-drift
            // regression (the exact failure this ticket corrects) fails CI before a Sponsor soak.
            float shirtLuma = 0.299f * shirt.r + 0.587f * shirt.g + 0.114f * shirt.b;
            Assert.Greater(shirtLuma, 0.6f,
                "the castaway shirt must be a LIGHT warm tone (young/hopeful identity), not a dark " +
                "grizzled rust — the iter-8 dark-drift is the regression this guard catches");
        }

        // THE CARTOONISH-STYLIZATION PROPORTION GUARD (ticket 86ca8ca1m, Uma castaway-style-v2 §2/§4).
        // The shipped scene's castaway must read TOY-CHUNKY: a head:total-height ratio in the loose
        // chunky band (2.5-3.3 heads), NOT the realistic ~7-8 heads. This is the single biggest
        // readability lever. Loose band so the Sponsor soak can tune "cuter" (toward 2.5) without
        // redding CI; a REGRESSION to the realistic head ratio (or a swap to a non-stylized mesh, or a
        // bone-scale dial reset to 1.0 against the un-stylized base mesh) fails here — the proportion
        // sibling of the luma guard. Measures the REAL serialized avatar via CastawayProportions.
        [Test]
        public void Castaway_ReadsChunky_HeadRatioInToyBand()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway, "the player must carry a CastawayCharacter avatar");

            Assert.IsNotNull(CastawayProportions.FindHeadBone(castaway.transform),
                "the avatar must carry a Head bone for the proportion measurement (rig intact)");

            float headsTall = CastawayProportions.MeasureHeadsTall(castaway);
            Assert.IsFalse(float.IsNaN(headsTall),
                "the heads-tall ratio must be measurable (skinned mesh + Head bone present)");
            Assert.That(headsTall,
                Is.InRange(CastawayProportions.MinHeadsTall, CastawayProportions.MaxHeadsTall),
                $"the castaway must read TOY-CHUNKY ({CastawayProportions.MinHeadsTall}-" +
                $"{CastawayProportions.MaxHeadsTall} heads tall), not realistic (~7-8). measured=" +
                headsTall.ToString("0.00") + " heads — the cartoonish-stylization proportion guard");
        }

        // The bone-scale dial must actually CHUNK the proportions: applying the stylization scale must
        // lower the heads-tall ratio (bigger head = fewer heads tall) relative to the un-scaled base
        // mesh. Catches the dial silently no-op'ing (wrong bone name match, scale not composing) — the
        // failure where the scene LOOKS configured but the stylization never lands. Builds two avatars
        // (scaled vs base) and asserts the scaled one is meaningfully chunkier. Pure build — no scene.
        [Test]
        public void StylizationDial_ActuallyChunksTheHead_VsBaseMesh()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            Assert.IsNotNull(fbx, "the castaway FBX must load to build the proportion-dial comparison");

            // Base mesh (no stylization) — the realistic ratio.
            var baseGo = new GameObject("BaseAvatar");
            var baseC = baseGo.AddComponent<CastawayCharacter>();
            baseC.modelPrefab = fbx;
            baseC.headScale = 1f; baseC.handScale = 1f; baseC.footScale = 1f;
            baseC.BuildInEditor();
            float baseHeads = CastawayProportions.MeasureHeadsTall(baseC);

            // Stylized (the shipped dial) — the chunky ratio.
            var styGo = new GameObject("StylizedAvatar");
            var styC = styGo.AddComponent<CastawayCharacter>();
            styC.modelPrefab = fbx;
            styC.headScale = 3.5f; styC.handScale = 1.4f; styC.footScale = 1.5f;
            styC.BuildInEditor();
            float styHeads = CastawayProportions.MeasureHeadsTall(styC);

            Object.DestroyImmediate(baseGo);
            Object.DestroyImmediate(styGo);

            Assert.IsFalse(float.IsNaN(baseHeads), "base heads-tall must measure");
            Assert.IsFalse(float.IsNaN(styHeads), "stylized heads-tall must measure");
            Assert.Greater(baseHeads, CastawayProportions.MaxHeadsTall,
                "the BASE (un-stylized) mesh must read realistic (>" + CastawayProportions.MaxHeadsTall +
                " heads) so the dial has real work to do — base=" + baseHeads.ToString("0.00"));
            Assert.Less(styHeads, baseHeads - 2f,
                "the stylization dial must CHUNK the head meaningfully (>=2 heads-tall reduction) — " +
                "base=" + baseHeads.ToString("0.00") + " stylized=" + styHeads.ToString("0.00") +
                " (a no-op dial — wrong bone match / scale not composing — fails here)");
            Assert.That(styHeads, Is.InRange(CastawayProportions.MinHeadsTall, CastawayProportions.MaxHeadsTall),
                "the shipped dial must land in the toy band — stylized=" + styHeads.ToString("0.00"));
        }

        // RIG-SURVIVAL-WITH-STYLIZATION guard (ticket 86ca8ca1m — the binding "Idle/Walk must survive"
        // AC, headless half). The stylization is a BONE-BASELINE SCALE that must compose with the
        // imported clips: building the stylized avatar must STILL bind the Animator's avatar + Idle/Walk
        // controller (clips bind = no T-pose freeze), AND the Head bone must carry the ~3.5 baseline
        // scale AFTER the build (the scale landed on the same rig the clips drive — so animation
        // multiplies rotation/translation onto the chunked bone rather than overwriting it). The
        // runtime/shipped half (the clips actually TICK the scaled rig without a legs-up regression) is
        // proven by the existing -verifyMove/-verifyLoop shipped-build captures (CastawayAnimationTests
        // pins the velocity-state switch separately). This is the editor-side binding check.
        [Test]
        public void StylizedAvatar_BindsAnimator_AndHeadBoneKeepsBaselineScale()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetGen.ControllerPath);
            Assert.IsNotNull(fbx, "the castaway FBX must load");
            Assert.IsNotNull(controller, "the Idle/Walk controller must load");

            var go = new GameObject("StylizedRigAvatar");
            var castaway = go.AddComponent<CastawayCharacter>();
            castaway.modelPrefab = fbx;
            castaway.animatorController = controller;
            castaway.headScale = 3.5f; castaway.handScale = 1.4f; castaway.footScale = 1.5f;
            castaway.BuildInEditor();

            // Clips bind: a non-null avatar (no NoAvatar freeze) + the Idle/Walk controller assigned.
            var animator = castaway.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, "the stylized avatar must carry an Animator");
            Assert.IsNotNull(animator.avatar,
                "the Animator must keep its FBX avatar after stylization (null avatar = bind/T-pose freeze)");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "the Idle/Walk controller must bind on the stylized avatar (clips drive the scaled rig)");

            // The Head bone carries the baseline scale on the rig the clips animate (the dial landed).
            var head = CastawayProportions.FindHeadBone(castaway.transform);
            Assert.IsNotNull(head, "the stylized avatar must keep its Head bone (rig intact)");
            Assert.That(head.localScale.x, Is.InRange(3.4f, 3.6f),
                "the Head bone must carry the ~3.5 stylization baseline scale after build (the dial " +
                "landed on the animated rig) — actual=" + head.localScale.x.ToString("0.00"));

            Object.DestroyImmediate(go);
        }

        // ---- ROUND-2 toy-chunky dials (86ca8ca1m bone-scale-only direction): SHORTER arm/leg/torso
        // LENGTH + CHUNKIER limb/torso GIRTH. These guard the bug CLASSES found via RigProbe trace, not
        // just the instance: (a) the girth-vs-length AXIS SWAP, (b) limb shortening not landing, (c) the
        // foot detaching/floating after leg-shorten, (d) the hand getting squashed (geometry must stay
        // intact — the hand is the ORIGINAL Quaternius mesh, NO vertex edits this round). ----

        // Helper: build a fully-stylized avatar with the round-2 dials for the trait guards below.
        private static CastawayCharacter BuildRound2Stylized(out GameObject go)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            Assert.IsNotNull(fbx, "the castaway FBX must load");
            go = new GameObject("Round2StylizedAvatar");
            var c = go.AddComponent<CastawayCharacter>();
            c.modelPrefab = fbx;
            c.headScale = 4.0f; c.handScale = 1.5f; c.footScale = 1.6f;
            c.armLengthScale = 0.70f; c.legLengthScale = 0.78f; c.torsoLengthScale = 0.80f;
            c.limbGirthScale = 1.50f; c.torsoGirthScale = 1.35f;
            c.BuildInEditor();
            return c;
        }

        private static Transform FindBone(CastawayCharacter c, string token)
        {
            foreach (var t in c.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains(token.ToLowerInvariant())) return t;
            return null;
        }

        // GIRTH-AXIS guard (THE bug found via trace): limb/torso GIRTH must scale the NON-LENGTH axes
        // (local X/Z) and leave LENGTH (local Y) for the length dials. The first implementation scaled
        // (X,Y) by girth — which stretched limbs LONGER instead of fatter (UpperLeg.y ended 1.17, not
        // 0.78). Pin the leg-segment bone's local scale: X/Z carry girth (1.5), Y carries ONLY the length
        // dial (0.78). A future axis-swap regression fails here.
        [Test]
        public void StylizationGirth_FattensXZ_NotLength()
        {
            var c = BuildRound2Stylized(out var go);
            var upperLeg = FindBone(c, "UpperLeg.L");
            Assert.IsNotNull(upperLeg, "the rig must carry UpperLeg.L for the girth-axis guard");
            var s = upperLeg.localScale;
            Assert.That(s.x, Is.InRange(1.45f, 1.55f),
                "limb girth must fatten local-X (~1.50) — actual " + s.x.ToString("0.00"));
            Assert.That(s.z, Is.InRange(1.45f, 1.55f),
                "limb girth must fatten local-Z (~1.50) — actual " + s.z.ToString("0.00"));
            Assert.That(s.y, Is.InRange(0.73f, 0.83f),
                "leg LENGTH (local-Y) must carry ONLY the length dial (~0.78), NOT the girth — a girth/" +
                "length axis swap (the trace-found bug) lands here. actual " + s.y.ToString("0.00"));
            Object.DestroyImmediate(go);
        }

        // LENGTH-SHORTENS guard: the arm/leg/torso length dials (<1) must actually SHORTEN the segment
        // bone's local-Y vs the FBX baseline (1.0). Catches the dial silently no-op'ing (wrong bone name)
        // or being overwritten. Asserts the segment bones carry their length factor.
        [Test]
        public void StylizationLength_ShortensArmLegTorsoSegments()
        {
            var c = BuildRound2Stylized(out var go);
            Assert.That(FindBone(c, "LowerArm.L").localScale.y, Is.InRange(0.65f, 0.75f),
                "arm length dial must shorten LowerArm local-Y (~0.70)");
            Assert.That(FindBone(c, "LowerLeg.L").localScale.y, Is.InRange(0.73f, 0.83f),
                "leg length dial must shorten LowerLeg local-Y (~0.78)");
            Assert.That(FindBone(c, "Torso").localScale.y, Is.InRange(0.75f, 0.85f),
                "torso length dial must shorten Torso local-Y (~0.80)");
            Object.DestroyImmediate(go);
        }

        // FOOT-GROUNDED-NOT-FLOATING guard (the leg-shorten trap): shortening the legs raises the ankle;
        // feet are a SIBLING chain (not children of the legs — RigProbe), so without re-seat + re-ground
        // the figure FLOATS (baked minY > 0) or the foot detaches from the shin. Assert the post-stylize
        // baked mesh grounds at ~0 AND the foot bone tracks within a small distance of the ankle end
        // (attached, no gap). Measured deterministically via BakeMesh (render-state-independent — the
        // CastawayProportions trap). A re-seat/re-ground regression (the float-off-ground class) fails here.
        [Test]
        public void StylizationLegShorten_FeetStayGroundedAndAttached()
        {
            var c = BuildRound2Stylized(out var go);
            var smr = c.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "the stylized avatar must carry a SkinnedMeshRenderer");

            // Bake in the SMR's local space; the Model child was re-grounded so the lowest vertex sits
            // at the Model's local y ~= the avatar-root origin (the player root grounds that to the floor).
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var verts = baked.vertices;
            Assert.Greater(verts.Length, 0, "the baked mesh must have verts");
            var smrToRoot = c.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            float minY = float.PositiveInfinity;
            for (int i = 0; i < verts.Length; i++)
            {
                float y = smrToRoot.MultiplyPoint3x4(verts[i]).y;
                if (y < minY) minY = y;
            }
            Object.DestroyImmediate(baked);
            Assert.That(minY, Is.InRange(-0.03f, 0.03f),
                "the stylized figure's lowest vertex must GROUND at the avatar origin (~0) — a positive " +
                "minY means the figure FLOATS (leg-shorten not re-grounded); negative means sunk. minY=" +
                minY.ToString("0.000"));

            // Foot stays attached to the shortened shin: foot bone within ~0.06u (local-1u space) of the
            // ankle end. A detach (re-seat regression) opens a gap larger than this.
            var ankle = FindBone(c, "LowerLeg.L_end");
            var foot = FindBone(c, "Foot.L");
            Assert.IsNotNull(ankle, "rig must carry the ankle end LowerLeg.L_end");
            Assert.IsNotNull(foot, "rig must carry Foot.L");
            float gap = Mathf.Abs(foot.position.y - ankle.position.y);
            Assert.Less(gap, 0.06f,
                "the foot must re-seat to the shortened ankle (no shin/foot detach) — gap=" + gap.ToString("0.000"));
            Object.DestroyImmediate(go);
        }

        // HAND-GEOMETRY-INTACT guard (Sponsor-clicked HARD constraint: arms/hands stay the ORIGINAL
        // Quaternius mesh, NO vertex edits; the orchestrator's vertex-sculpt mangled them). The bone-scale
        // route must NOT squash the hand: the arm-LENGTH shorten propagates a Y-squash to the Palm (its
        // child), so the Palm is counter-compensated in Y. Assert the Palm's WORLD Y-span (Palm -> finger
        // tip) is NOT collapsed by the arm shorten — i.e. the hand keeps a real, un-squashed extent. A
        // regression that drops the Palm counter-compensation collapses this span and fails here.
        [Test]
        public void StylizationArmShorten_DoesNotSquashTheHand()
        {
            var c = BuildRound2Stylized(out var go);
            var palm = FindBone(c, "Palm.L");
            var fingerTip = FindBone(c, "Fingers.L_end");
            Assert.IsNotNull(palm, "rig must carry Palm.L");
            Assert.IsNotNull(fingerTip, "rig must carry Fingers.L_end (the hand tip)");
            // The hand's vertical span (palm root to finger tip). On the un-stylized base this is ~0.087u
            // (Palm wY 0.455 - Fingers.L_end wY 0.368). The arm shorten must NOT crush it; the Palm
            // counter-compensation keeps it at a real fraction of the base. Assert it stays well above a
            // collapsed value (a squash drops it toward ~0).
            float handSpan = Mathf.Abs(palm.position.y - fingerTip.position.y);
            Assert.Greater(handSpan, 0.05f,
                "the hand must keep a real (un-squashed) vertical span — the arm-shorten Y-squash must be " +
                "counter-compensated on the Palm so the ORIGINAL hand geometry reads intact. span=" +
                handSpan.ToString("0.000"));
            Object.DestroyImmediate(go);
        }

        // Per-part smoothness must DIFFERENTIATE (cloth matte vs skin/hair/eyes glossier) so the
        // figure reads "detailed/polished" rather than uniformly flat-matte (the spike's iter7 polish
        // gap). A regression that flattens every part to one smoothness fails here.
        [Test]
        public void CastawayRecolor_PerPartSmoothnessIsDifferentiated()
        {
            float shirtS   = CastawayCharacter.SmoothnessFor("Shirt");
            float pantsS   = CastawayCharacter.SmoothnessFor("Pants");
            float skinS    = CastawayCharacter.SmoothnessFor("Skin");
            float hairS    = CastawayCharacter.SmoothnessFor("Hair");
            float eyesS    = CastawayCharacter.SmoothnessFor("Eyes");
            float leatherS = CastawayCharacter.SmoothnessFor("Belt");

            Assert.AreEqual(shirtS, pantsS, 0.001f, "cloth parts (shirt/pants) share the matte smoothness");
            Assert.Greater(skinS, shirtS, "skin must catch more key light than matte cloth");
            Assert.Greater(eyesS, hairS, "eyes are the glossiest (a small specular dot for life)");
            Assert.Greater(leatherS, skinS, "leather reads with a soft worn sheen above bare skin");
            var levels = new System.Collections.Generic.HashSet<float> { shirtS, skinS, hairS, eyesS, leatherS };
            Assert.GreaterOrEqual(levels.Count, 4,
                "per-part smoothness must span >=4 levels (a uniformly-matte figure reads undetailed)");
        }
    }
}
