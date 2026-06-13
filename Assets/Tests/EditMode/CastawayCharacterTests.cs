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
    ///   6. The IDENTITY RECOLOR guard (86ca8ca1m): the atlas PNG's repainted UV cells read as OUR
    ///      young/hopeful castaway — warm khaki shirt (the U2-6 luma >0.6 anchor, re-carried onto the
    ///      chibi), sandy-ginger hair+cap (not the green-cap kid), bare-feet skin (not dark shoes).
    ///
    /// NOTE on guards: the prior base's 6-part recolor / per-part-smoothness asserts are gone (chibi has
    /// its own toon materials). The Quaternius shirt-luminance (>0.6) identity guard is NOW RE-CARRIED
    /// onto the chibi via guard #6 (atlas-cell re-sample) — the chibi's default look had NO identity
    /// guard; the recolor restores one.
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

        // The TOON-ATLAS guard: the chibi's imported materials must bind the flat toon atlas
        // (mini_material_baseColor) on _BaseMap. The identity recolor (86ca8ca1m) repaints cells of THIS
        // atlas — so the binding must survive; this catches the flat toon look silently lost to a
        // missing-texture grey (an importer change dropping the texture). The per-cell identity colours
        // are asserted separately by Atlas_RecoloredToYoungHopefulIdentity_NotGreenCapKid.
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

        // The IDENTITY RECOLOR guard (ticket 86ca8ca1m — restores an identity guard for the chibi,
        // which previously had NONE since its default look was shipped as-is). The atlas was repainted
        // so the chibi reads as OUR young/hopeful castaway — NOT the generic green-cap kid: warm khaki
        // shirt (the U2-6 anchor luma >0.6), sandy-ginger hair+cap, bare-feet skin. This re-samples the
        // recolored UV cells in the bound atlas PNG and fails CI on any drift back toward the kid look
        // (a grey shirt, a green cap, dark shoes). It reads the SAME atlas the materials bind on _BaseMap
        // (probe-verified path), so a regression that drops/replaces the recolored texture is caught.
        [Test]
        public void Atlas_RecoloredToYoungHopefulIdentity_NotGreenCapKid()
        {
            // Read the bound atlas PNG bytes directly (texture isReadable=0, so load into a readable copy).
            string path = CharacterAssetGen.AtlasPngPath;
            Assert.IsTrue(System.IO.File.Exists(path), "the bound atlas PNG must exist at " + path);
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(path)), "atlas PNG must decode");
            int W = tex.width, H = tex.height;

            // Sample the CENTRE of a 16x16 UV cell. UV origin is bottom-left (v up); GetPixel uses the
            // same bottom-up convention, so cell (col,row) centre samples directly.
            Color CellCentre(Vector2Int cell)
            {
                int px = Mathf.Clamp((int)((cell.x + 0.5f) / 16f * W), 0, W - 1);
                int py = Mathf.Clamp((int)((cell.y + 0.5f) / 16f * H), 0, H - 1);
                return tex.GetPixel(px, py);
            }
            float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

            var shirt = CellCentre(CharacterAssetGen.ShirtCell);
            var hair = CellCentre(CharacterAssetGen.HairCell);
            var cap = CellCentre(CharacterAssetGen.CapCell);
            var capDome2 = CellCentre(CharacterAssetGen.CapDome2Cell);
            var feet = CellCentre(CharacterAssetGen.FeetCell);
            Object.DestroyImmediate(tex);

            // SHIRT: warm khaki in a MID-TONE BAND (86ca8ca1m soak-fix). The v1 guard chased luma>0.6,
            // but a bright shirt BLEW OUT to cream under the Zone-D warm post (the Sponsor's "no shirt /
            // all-yellow" soak). The shirt is now a deeper saturated mid-khaki: the guard is a BAND
            // [0.42..0.66] — below 0.42 is grizzled/grey drift (the original anti-dark intent carries),
            // above 0.66 is the blown-bright drift the soak proved washes out. Still WARM (R>G>B).
            Assert.That(Luma(shirt), Is.InRange(0.42f, 0.66f),
                $"shirt must be warm MID-khaki (luma {Luma(shirt):F2}); <0.42 = grizzled drift, " +
                ">0.66 = the blown-bright drift that washed to cream in the soak");
            Assert.Greater(shirt.r, shirt.g,
                $"shirt must read WARM khaki (R>G); got rgb({shirt.r:F2},{shirt.g:F2},{shirt.b:F2})");
            Assert.Greater(shirt.g, shirt.b,
                $"shirt must read WARM khaki (G>B); got rgb({shirt.r:F2},{shirt.g:F2},{shirt.b:F2})");

            // HAIR / CAP-DOME: the kept crown dome (Object_41) must read sandy-ginger HAIR — warm
            // (R>G>B). Its TWO cells are checked: CapCell(1,10) AND CapDome2(1,9). CapDome2 was left
            // GREEN by the v1 recolor (G>R) — the single biggest "still a cap" tell; the soak-fix repaints
            // BOTH sandy-ginger. A green dome fails R>G here.
            Assert.Greater(hair.r, hair.b,
                $"hair must be warm sandy/ginger (R>B); got rgb({hair.r:F2},{hair.g:F2},{hair.b:F2})");
            Assert.Greater(cap.r, cap.g,
                $"cap-dome cell A must be warm hair (R>G — green fails); got rgb({cap.r:F2},{cap.g:F2},{cap.b:F2})");
            Assert.Greater(capDome2.r, capDome2.g,
                $"cap-dome cell B (1,9) — the leftover GREEN cap band — must be recolored warm hair (R>G); " +
                $"got rgb({capDome2.r:F2},{capDome2.g:F2},{capDome2.b:F2})");

            // BARE FEET: skin tan (warm + bright), NOT the kid's dark shoes. Dark shoes fail luma>0.5.
            Assert.Greater(Luma(feet), 0.5f,
                $"feet must be bare skin tan, not dark shoes (luma {Luma(feet):F2} <= 0.5 = shoes); " +
                $"rgb({feet.r:F2},{feet.g:F2},{feet.b:F2})");
            Assert.Greater(feet.r, feet.b,
                $"bare-feet skin must read warm (R>B); got rgb({feet.r:F2},{feet.g:F2},{feet.b:F2})");
        }

        // CAP -> HAIR guard (86ca8ca1m soak-fix). The Sponsor soaked 46f2a9d and the castaway read as
        // wearing a CAP ("still wearing the yellow cap"), not hair. The cap is two skinned-mesh nodes:
        // Object_41 (crown dome) + Object_42 (flat brim/visor). The fix HIDES BOTH (hiding only the brim
        // left the dome's cap-shaped arc sticking up — not hair) and adds a clean procedural sandy-ginger
        // HAIR skull-cap (MovementCameraScene.HairObjectName) on the head bone. This guard reads the
        // SHIPPED scene and fails CI if either cap renderer is re-enabled (the cap returns) OR the hair
        // object is missing (bald crown). Same class as the blob-shadow scene-presence guard: a
        // serialized-state contract a future rebuild could silently break.
        [Test]
        public void Castaway_CapHidden_HairPresent_InShippedScene()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            int capFound = 0, capHidden = 0;
            foreach (var smr in castaway.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var capName in CastawayCharacter.CapMeshNames)
                {
                    if (smr.name == capName)
                    {
                        capFound++;
                        if (!smr.enabled) capHidden++;
                    }
                }
            }
            Assert.AreEqual(CastawayCharacter.CapMeshNames.Length, capFound,
                "both cap mesh nodes must exist in the avatar (hidden, not deleted — deleting risks rig breakage)");
            Assert.AreEqual(CastawayCharacter.CapMeshNames.Length, capHidden,
                "BOTH cap renderers (dome + brim) must be DISABLED in the shipped scene so the castaway " +
                "reads as having hair, not a cap (the Sponsor's 'still wearing the yellow cap' soak failure)");

            // The replacement hair skull-cap must be present under the avatar (the head bone).
            Transform hair = null;
            foreach (var t in castaway.GetComponentsInChildren<Transform>(true))
                if (t.name == MovementCameraScene.HairObjectName) { hair = t; break; }
            Assert.IsNotNull(hair,
                "the '" + MovementCameraScene.HairObjectName + "' skull-cap must be serialized under the " +
                "avatar (the hidden cap leaves a bare crown — this is the sandy-ginger hair)");
            var hmr = hair.GetComponent<MeshRenderer>();
            Assert.IsNotNull(hmr, "the hair must have a MeshRenderer");
            var hmf = hair.GetComponent<MeshFilter>();
            Assert.IsNotNull(hmf, "the hair must have a MeshFilter");
            Assert.IsNotNull(hmf.sharedMesh, "the hair mesh must be serialized");
            Assert.Greater(hmf.sharedMesh.vertexCount, 6, "the hair must be a real dome mesh");
            Assert.IsTrue(hmr.enabled, "the hair renderer must be enabled");
        }

        // Mechanized cap-hide + hair reproducibility: re-running the editor avatar build must re-hide both
        // cap meshes (the fix lives in BuildModel -> HideCap, on every editor build), so a
        // regenerate-on-rebase can't silently ship the cap back. Sibling of
        // EditorRebuild_Reproduces_SameSerializedShape.
        [Test]
        public void EditorRebuild_RehidesCap()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            castaway.BuildInEditor();

            int capFound = 0, capHidden = 0;
            foreach (var smr in castaway.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var capName in CastawayCharacter.CapMeshNames)
                    if (smr.name == capName) { capFound++; if (!smr.enabled) capHidden++; }
            Assert.AreEqual(CastawayCharacter.CapMeshNames.Length, capFound, "rebuild must reproduce both cap nodes");
            Assert.AreEqual(capFound, capHidden, "editor rebuild must RE-HIDE both cap meshes (idempotent cap->hair)");
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

        // HAIR CROWN-SPREAD guard (86ca8ce6y SOAKFIX3). The Sponsor soaked the messy hair and STILL saw a
        // "brown spike" on top at the tilt-to-horizon camera angle; Tess's geometry probe confirmed the top
        // vertex sat 0.138u / relGap 0.121 ABOVE the top-15 crown ring (the apex de-spike didn't pull deep
        // enough — the crown was a tall narrow CONE, not just one proud pole vertex). No test asserted the
        // hair SILHOUETTE, so the spike went green. This guard builds the EXACT shipped MessyHairCap mesh
        // (the same named params the scene build uses) and asserts the crown is flat-ish: the top-15 highest
        // verts must span < 0.05u (Tess's bar) AND no single vertex stands proud above its ring (apex gap
        // small). Catches the bug CLASS — any future param/de-spike change that re-introduces a proud crown
        // fails CI before a Sponsor soak. Pure-geometry (no scene load); deterministic via the fixed seed.
        [Test]
        public void MessyHairCap_CrownIsFlat_NoProudApexSpike()
        {
            var mesh = LowPolyMeshes.MessyHairCap(
                MovementCameraScene.HairCapRadius, MovementCameraScene.HairCapYScale,
                MovementCameraScene.HairCapCut, MovementCameraScene.HairCapSubdiv,
                MovementCameraScene.HairCapJitter, MovementCameraScene.HairCapSeed);
            Assert.IsNotNull(mesh, "MessyHairCap must build a mesh");

            var ys = new System.Collections.Generic.List<float>();
            foreach (var v in mesh.vertices) ys.Add(v.y);
            Assert.GreaterOrEqual(ys.Count, 15, "the cap must have at least 15 verts to measure a crown ring");
            ys.Sort();
            ys.Reverse(); // highest first

            float top1 = ys[0];
            int n = 15;
            float ringMin = ys[n - 1];           // 15th-highest
            float ringMax = ys[1];               // 2nd-highest (the apex's immediate neighbour)
            float spread = top1 - ringMin;       // top1..top15 vertical spread
            float apexGap = top1 - ringMax;      // is the single top vertex proud of its ring?

            // Tess's bar: the crown top-ring spread must be flat-ish (< ~0.05u). Pre-fix this was ~0.106u
            // (probe relGap 0.121); the crown-plateau soft-clamp brings it to ~0.030u.
            Assert.Less(spread, 0.05f,
                $"hair crown top-15 vertex spread {spread:F4}u must be < 0.05u (a taller spread reads as a " +
                $"proud tuft/cone at the tilt-to-horizon cam — the Sponsor's 'brown spike'); top1={top1:F4} " +
                $"ringMin(top15)={ringMin:F4}");
            // No single pole vertex standing proud above its immediate ring (the literal 'spike').
            Assert.Less(apexGap, 0.02f,
                $"no hair vertex may stand proud above the crown ring: apex gap {apexGap:F4}u (top1 {top1:F4} " +
                $"vs 2nd-highest {ringMax:F4}) must be < 0.02u");
        }
    }
}
