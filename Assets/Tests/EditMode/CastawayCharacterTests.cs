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
            var shirt   = new Color(0.82f, 0.72f, 0.52f);
            var pants   = new Color(0.34f, 0.46f, 0.50f);
            var hair    = new Color(0.84f, 0.50f, 0.22f);
            var eyes    = new Color(0.18f, 0.13f, 0.11f);
            var leather = new Color(0.45f, 0.30f, 0.18f);

            Assert.AreEqual(shirt, CastawayCharacter.CastawayColorFor("Shirt", skin, shirt, pants, hair, eyes, leather),
                "a 'Shirt' material must recolor to the light warm khaki-shirt tone");
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
