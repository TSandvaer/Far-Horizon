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
    /// EditMode regression guards for the chunky-cartoon castaway player avatar (ticket 86ca8ca1m —
    /// the "Mini Chibi Kid" sourced base that SUPERSEDES the Quaternius Animated-Men character). These
    /// assert the REAL serialized Boot scene (the bytes the exe loads) + the FBX import config, so the
    /// bug CLASSES (legs-up serialization divergence, T-pose-mid-walk non-looping clips, the
    /// un-normalized giant, a missing toon atlas, a swap back to a realistic non-chunky base) can't
    /// recur silently in headless CI before a Sponsor soak:
    ///
    ///   1. The player carries a serialized, SKINNED, BONED avatar — NOT a runtime-assembled
    ///      hierarchy and NOT the retired capsule placeholder (the legs-up serialization guard).
    ///   2. The FBX is the chibi character with looping Idle/Walk clips and a CreateFromThisModel
    ///      avatar (the T-pose-mid-walk guard).
    ///   3. The intrinsic-height normalization is configured (the un-normalized-giant guard).
    ///   4. The chibi's flat TOON ATLAS binds on the imported materials (replaces the prior base's
    ///      6-part recolor guard — the chibi ships its own toon materials; identity/recolor is OUT OF
    ///      SCOPE. The guard catches the flat toon look silently lost to a missing-texture grey).
    ///   5. The shipped scene's castaway reads CHUNKY (heads-tall in the toy band) — catches a swap
    ///      back to a realistic ~7-8-heads base (replaces the base-specific shirt-luma identity guard,
    ///      which was for the Quaternius recolor and does NOT apply to the chibi's default look).
    ///
    /// NOTE on dropped guards: the prior base's shirt-luminance (>0.6) identity guard + the 6-part
    /// recolor / per-part-smoothness asserts are REMOVED — they were specific to the Quaternius mesh's
    /// 6 material slots + the recolor we no longer apply (we ship the chibi's default toon look).
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
            Assert.IsNotNull(castaway, "the player must carry a CastawayCharacter avatar");

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

        // The clip-binding guard: the FBX must be the chibi character AND carry the Idle/Walk
        // locomotion clips, both LOOPING, with a CreateFromThisModel avatar. A missing / non-looping
        // clip is the T-pose / freeze-mid-walk failure mode; a null avatar freezes the model in its
        // bind pose. The chibi's clip names carry the "Object_5|" armature prefix (e.g.
        // "Object_5|Idle 01") — matched by Contains. Pinning these catches an importer-config
        // regression in headless CI before it ships as a frozen avatar.
        [Test]
        public void Fbx_IsChibiCharacter_WithLoopingIdleAndWalkClips_AndAvatar()
        {
            Assert.AreEqual("Assets/Art/Character/MiniChibiKid/MiniChibiKid.fbx",
                CharacterAssetGen.FbxPath, "the player FBX must be the Mini Chibi Kid character");

            var importer = AssetImporter.GetAtPath(CharacterAssetGen.FbxPath) as ModelImporter;
            Assert.IsNotNull(importer, "the character FBX must be importable at " + CharacterAssetGen.FbxPath);

            // CreateFromThisModel avatar so the Generic rig binds its own clips (the T-pose fix; the
            // chibi imports as NoAvatar by default).
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, importer.avatarSetup,
                "the Generic rig must create its own avatar or clips will not bind (T-pose)");

            AnimationClip idle = null, walk = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.FbxPath))
            {
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    if (c.name.Contains(CharacterAssetGen.IdleClip)) idle = c;
                    if (c.name.Contains(CharacterAssetGen.WalkClip)) walk = c;
                }
            }
            Assert.IsNotNull(idle, "FBX must contain a clip matching '" + CharacterAssetGen.IdleClip + "'");
            Assert.IsNotNull(walk, "FBX must contain a clip matching '" + CharacterAssetGen.WalkClip + "'");
            Assert.IsTrue(idle.isLooping, CharacterAssetGen.IdleClip + " must loop (no freeze-on-idle)");
            Assert.IsTrue(walk.isLooping, CharacterAssetGen.WalkClip + " must loop (no freeze-mid-walk)");
        }

        // The intrinsic-height-normalization guard: the FBX importer's globalScale must normalize the
        // (~1.82u intrinsic) model down toward ~1u, so the avatar-root scale maps directly onto
        // on-screen height. An un-normalized import scales the character ~1.8x tall on the 1.8u avatar
        // root (a giant whose head clips the frame). We assert the imported model's actual bounds
        // height is in a sane normalized band, robust to small importer-scale drift.
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

            // Target is 1u; allow a generous band so the test guards against the un-normalized ~1.82u
            // import (or a zero-scale collapse) without being brittle to ±0.2u importer rounding.
            Assert.That(h, Is.InRange(0.6f, 1.6f),
                $"imported model height {h:F3}u must be normalized to ~{CharacterAssetGen.TargetImportHeightU}u " +
                "(an un-normalized ~1.82u import scales the avatar to a giant — the framing defect)");
        }

        // The UPRIGHT guard: the chibi imports standing upright (probe-verified — head world-Y above
        // feet, feet near origin), no -90 X bake needed. A future import-config change that lays it on
        // its back (the Sketchfab Y-up trap) would fail here before it ships face-down.
        [Test]
        public void Fbx_ImportsUpright_HeadAboveFeet_FeetNearOrigin()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            Assert.IsNotNull(fbx);
            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var rends = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Transform head = CastawayProportions.FindHeadBone(inst.transform);
            Assert.IsNotNull(head, "the chibi must carry a Head bone (Head_05)");
            float headY = head.position.y;
            float feetY = b.min.y;
            Object.DestroyImmediate(inst);

            Assert.Greater(headY, feetY + 0.2f,
                "the head must sit well ABOVE the feet (upright — not laid on its back / Y-up trap)");
            Assert.That(feetY, Is.InRange(-0.25f, 0.35f),
                $"the feet must sit near the model origin (feetY={feetY:F3}) so localPosition zero grounds them");
        }

        // The TOON-ATLAS guard (replaces the prior base's recolor guard): the chibi's imported
        // materials must bind the flat toon atlas (mini_material_baseColor) on _BaseMap. The chibi
        // ships its OWN toon materials (we do NOT recolor — identity is out of scope); this catches the
        // flat toon look silently lost to a missing-texture grey (a recolor regression or an importer
        // change dropping the embedded texture).
        [Test]
        public void Fbx_ImportedMaterials_BindTheToonAtlas()
        {
            int atlasBound = 0, matCount = 0;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(CharacterAssetGen.FbxPath))
            {
                if (obj is Material m)
                {
                    matCount++;
                    if (m.HasProperty("_BaseMap"))
                    {
                        var tex = m.GetTexture("_BaseMap");
                        if (tex != null && tex.name.Contains(CharacterAssetGen.AtlasTextureName)) atlasBound++;
                    }
                }
            }
            Assert.Greater(matCount, 0, "the chibi FBX must import its toon materials");
            Assert.Greater(atlasBound, 0,
                "at least one imported material must bind the toon atlas '" +
                CharacterAssetGen.AtlasTextureName + "' on _BaseMap — else the flat toon look ships as grey");
        }

        // The CHUNKY guard (replaces the base-specific shirt-luma identity guard): the shipped scene's
        // castaway must read in the toy proportion band. The chibi base measures ~1.07 on this BakeMesh
        // fingerprint (a stable big-head toy ratio) vs a realistic base far higher; this catches a
        // REGRESSION to a realistic base (e.g. an accidental swap back to the Quaternius character).
        // Measured via BakeMesh (render-state-independent — the deserialized-SMR stale-bounds trap,
        // unity-conventions.md §Editor-vs-runtime). See CastawayProportions for the fingerprint rationale.
        [Test]
        public void Castaway_ReadsChunky_HeadsTallInToyBand()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            Assert.IsNotNull(CastawayProportions.FindHeadBone(castaway.transform),
                "the avatar must carry a Head bone for the proportion measure");

            float heads = CastawayProportions.MeasureHeadsTall(castaway);
            Assert.IsFalse(float.IsNaN(heads),
                "heads-tall must measure (skinned mesh + head bone present)");
            Assert.That(heads, Is.InRange(CastawayProportions.MinHeadsTall, CastawayProportions.MaxHeadsTall),
                $"the shipped castaway must read CHUNKY (heads-tall {heads:F2} in the toy band " +
                $"{CastawayProportions.MinHeadsTall}-{CastawayProportions.MaxHeadsTall}) — a realistic " +
                "~7-8-heads value means a regression to a non-chunky base");
        }

        // Blob-shadow SCENE-PRESENCE guard (the component-not-serialized-into-scene failure class,
        // unity-conventions.md §Editor-vs-runtime — a component can exist in source while the scene
        // never carries it, shipping silently inert). The shipped scene must hold the BlobShadow under
        // the player, with a MeshFilter/Renderer, NO collider (must not block the click raycast or the
        // NavMesh bake), and casting no real shadow.
        [Test]
        public void BootScene_HasBlobShadow_UnderPlayer_NoColliderNoCast()
        {
            OpenBootAndFindPlayer();
            var blob = _player.transform.Find(MovementCameraScene.BlobShadowObjectName);
            Assert.IsNotNull(blob,
                "the shipped scene must carry a '" + MovementCameraScene.BlobShadowObjectName +
                "' under the player (the contact shadow — serialized, not Awake-built)");

            var mf = blob.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "the blob shadow must have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "the blob shadow mesh must be serialized");
            Assert.Greater(mf.sharedMesh.vertexCount, 6, "the blob shadow disc must be a real fan mesh");

            var mr = blob.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mr, "the blob shadow must have a MeshRenderer");
            Assert.IsNotNull(mr.sharedMaterial, "the blob shadow material must be serialized inline");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, mr.shadowCastingMode,
                "the fake contact shadow must cast NO real shadow");

            Assert.IsNull(blob.GetComponent<Collider>(),
                "the blob shadow must have NO collider (it must not block the click raycast / NavMesh bake)");
        }
    }
}
