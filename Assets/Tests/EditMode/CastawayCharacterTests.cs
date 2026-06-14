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
            // MEAN over a whole 16x16 cell — robust to the SOAKFIX4 per-pixel TATTER (dirt/tears/fray) which
            // makes any single centre sample noisy. The identity colour still holds in the average.
            Color CellMean(Vector2Int cell)
            {
                int cw = W / 16, ch = H / 16, x0 = cell.x * cw, y0 = cell.y * ch;
                float r = 0, g = 0, b = 0; int n = 0;
                for (int yy = 0; yy < ch; yy++)
                    for (int xx = 0; xx < cw; xx++) { var c = tex.GetPixel(x0 + xx, y0 + yy); r += c.r; g += c.g; b += c.b; n++; }
                return new Color(r / n, g / n, b / n, 1f);
            }
            float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

            var shirt = CellMean(CharacterAssetGen.ShirtCell); // MEAN (tatter-robust)
            var hair = CellCentre(CharacterAssetGen.HairCell);
            var cap = CellCentre(CharacterAssetGen.CapCell);
            var capDome2 = CellCentre(CharacterAssetGen.CapDome2Cell);
            var feet = CellCentre(CharacterAssetGen.FeetCell);
            Object.DestroyImmediate(tex);

            // SHIRT: warm khaki in a MID-TONE BAND (86ca8ca1m soak-fix; band floor lowered SOAKFIX4 for the
            // tatter). The v1 guard chased luma>0.6, but a bright shirt BLEW OUT to cream under the Zone-D
            // warm post (the Sponsor's "no shirt / all-yellow" soak). The shirt is a deeper saturated mid-
            // khaki; SOAKFIX4 also WEATHERS it (dirt/tears/fray), which DARKENS the cell MEAN a touch. Band
            // is now [0.34..0.66] on the MEAN — below 0.34 is grizzled/grey drift (anti-dark intent carries),
            // above 0.66 is the blown-bright drift the soak proved washes out. Still WARM (R>G>B in the mean).
            Assert.That(Luma(shirt), Is.InRange(0.34f, 0.66f),
                $"shirt mean must be warm MID-khaki (luma {Luma(shirt):F2}); <0.34 = grizzled/over-tattered drift, " +
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

        // CLOTHES-TATTER guard (86ca8ce6y SOAKFIX4). The Sponsor asked for the shirt + pants to read TORN /
        // WEATHERED (castaway fiction). RecolorIdentityAtlas now WEATHERS the garment cells (dirt/tears/fray)
        // instead of painting a flat fill. This guard catches the bug CLASS — a regression that flattens the
        // garment back to a clean solid colour (the prior PaintCell). It asserts the garment cells carry real
        // pixel VARIATION (a std-dev over the cell well above a flat ramp's), AND that the PANTS stay BLUE
        // denim (luma-preserved: the weather darkens/streaks, it does not recolour). The pants cell is
        // (7,15) on Object_44 — confirmed by the per-object UvCellTrace (the chibi is SIX skinned objects;
        // the head/body Object_36 does NOT carry the pants — the original single-SMR trace miss). Reads the
        // SAME atlas the materials bind.
        [Test]
        public void Atlas_ShirtAndPants_AreWeatheredTattered_NotFlat()
        {
            string path = CharacterAssetGen.AtlasPngPath;
            Assert.IsTrue(System.IO.File.Exists(path), "the bound atlas PNG must exist at " + path);
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(path)), "atlas PNG must decode");
            int W = tex.width, H = tex.height, cw = W / 16, ch = H / 16;

            // Mean + luma-std-dev over a whole cell. A flat solid (or pure vertical ramp) has a LOW std-dev
            // across the FULL cell area; the dirt/tears/fray push it up. We measure deviation from the cell
            // MEAN (so a smooth gradient alone reads low, but blotches/rips read high).
            void CellStats(Vector2Int cell, out Color mean, out float lumaStd)
            {
                int x0 = cell.x * cw, y0 = cell.y * ch;
                float r = 0, g = 0, b = 0; int n = 0;
                var lumas = new System.Collections.Generic.List<float>();
                for (int yy = 0; yy < ch; yy++)
                    for (int xx = 0; xx < cw; xx++)
                    {
                        var c = tex.GetPixel(x0 + xx, y0 + yy);
                        r += c.r; g += c.g; b += c.b; n++;
                        lumas.Add(0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b);
                    }
                mean = new Color(r / n, g / n, b / n, 1f);
                float lm = 0; foreach (var l in lumas) lm += l; lm /= lumas.Count;
                float v = 0; foreach (var l in lumas) v += (l - lm) * (l - lm); v /= lumas.Count;
                lumaStd = Mathf.Sqrt(v);
            }
            float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

            // Fraction of pixels in a cell DARKENED notably below the cell's own bright top-of-ramp (a
            // strength-of-weathering metric — a subtle wear leaves most pixels near the ramp; bold dirt/tears/
            // hem push a real fraction well below it). SOAKFIX5: the Sponsor said the SOAKFIX4 weather read as
            // "unchanged" (too subtle), so the guard now asserts STRENGTH, not just presence-of-variation.
            float DarkenedFraction(Vector2Int cell)
            {
                int x0 = cell.x * cw, y0 = cell.y * ch;
                // the un-weathered toon ramp peaks at anchor*1.12 (top); use the cell's own max luma as the
                // clean reference and count pixels dimmed to < 0.72x of it (a clearly-weathered pixel).
                float maxL = 0f;
                for (int yy = 0; yy < ch; yy++)
                    for (int xx = 0; xx < cw; xx++)
                    {
                        var c = tex.GetPixel(x0 + xx, y0 + yy);
                        float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        if (l > maxL) maxL = l;
                    }
                int dimmed = 0, n = 0;
                for (int yy = 0; yy < ch; yy++)
                    for (int xx = 0; xx < cw; xx++)
                    {
                        var c = tex.GetPixel(x0 + xx, y0 + yy);
                        float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        if (l < maxL * 0.72f) dimmed++;
                        n++;
                    }
                return n > 0 ? (float)dimmed / n : 0f;
            }

            // SHIRT: BOLDLY weathered (strong variation + a real darkened fraction) AND still warm khaki.
            CellStats(CharacterAssetGen.ShirtCell, out var shirtMean, out var shirtStd);
            float shirtDark = DarkenedFraction(CharacterAssetGen.ShirtCell);
            Assert.Greater(shirtStd, 0.05f,
                $"shirt cell must carry STRONG tatter variation that READS at gameplay distance (luma std " +
                $"{shirtStd:F4} > 0.05); the SOAKFIX4 0.02 bar passed a too-subtle weather the Sponsor called " +
                "'unchanged'");
            Assert.Greater(shirtDark, 0.12f,
                $"shirt must have a real DARKENED fraction (dirt/tears/hem) to read worn: {shirtDark:P0} of " +
                "pixels dimmed <0.72x must be > 12%");
            Assert.Greater(shirtMean.r, shirtMean.b,
                $"shirt must stay warm khaki even weathered (R>B mean); got {shirtMean}");

            // PANTS: BOLDLY weathered AND still BLUE denim (luma-PRESERVED — darken/streak, no recolour).
            CellStats(CharacterAssetGen.PantsCell, out var pMean, out var pStd);
            float pDark = DarkenedFraction(CharacterAssetGen.PantsCell);
            Assert.Greater(pStd, 0.04f,
                $"pants cell {CharacterAssetGen.PantsCell} must carry STRONG tatter variation (luma std " +
                $"{pStd:F4} > 0.04), not the too-subtle SOAKFIX4 weather");
            Assert.Greater(pDark, 0.12f,
                $"pants must have a real DARKENED fraction to read worn: {pDark:P0} of pixels dimmed <0.72x " +
                "must be > 12%");
            Assert.Greater(pMean.b, pMean.r,
                $"pants must stay BLUE denim (B>R mean — the Sponsor's 'blue pants', confirmed by the per-object trace); {pMean}");
            Assert.Greater(pMean.b, pMean.g,
                $"pants must stay BLUE denim (B>G mean), not greened/recoloured; {pMean}");
            Object.DestroyImmediate(tex);
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

        // HAIR CROWN-FLAT guard (86ca8ce6y SOAKFIX4 — TIGHTENED). SOAKFIX3 asserted the top-15 RING spread
        // < 0.05u, and that PASSED while a spike remained: the Sponsor sees the spike from the DEFAULT over-
        // the-shoulder GAMEPLAY cam (looking DOWN at the crown, pitch 55-70°), where a vertex standing proud
        // of its NEIGHBOURS pokes the top-down silhouette — a defect the ring-spread metric is blind to (it
        // measures the band height, not whether one vert exceeds the plateau). This guard is the bug-CLASS
        // catch: it builds the EXACT shipped MessyHairCap mesh and asserts the crown is a FLAT PLATEAU viewed
        // FROM ABOVE — (1) the highest vert sits no more than epsilon above the next-highest (no single proud
        // apex), and (2) the top plateau is genuinely FLAT (many verts share the max height — a hard-clamped
        // plateau, not a tapering cone tip). Any future param/de-spike regression that re-introduces a proud
        // crown vertex fails CI before a Sponsor soak. Pure-geometry (no scene load); deterministic via seed.
        // (SOAKFIX7 restores this flat-dome guard after the soakfix6 TuftedHair redo was reverted to FLAT —
        // the Sponsor preferred the clean flat hair over the spiky lobes.)
        [Test]
        public void MessyHairCap_CrownIsFlatPlateau_NoProudApexFromAbove()
        {
            var mesh = LowPolyMeshes.MessyHairCap(
                MovementCameraScene.HairCapRadius, MovementCameraScene.HairCapYScale,
                MovementCameraScene.HairCapCut, MovementCameraScene.HairCapSubdiv,
                MovementCameraScene.HairCapJitter, MovementCameraScene.HairCapSeed);
            Assert.IsNotNull(mesh, "MessyHairCap must build a mesh");

            var ys = new System.Collections.Generic.List<float>();
            foreach (var v in mesh.vertices) ys.Add(v.y);
            Assert.GreaterOrEqual(ys.Count, 15, "the cap must have at least 15 verts to measure a crown plateau");
            ys.Sort();
            ys.Reverse(); // highest first

            float top1 = ys[0];
            float top2 = ys[1];
            float apexGap = top1 - top2; // does the single highest vertex stand proud of the next-highest?

            // (1) NO PROUD APEX FROM ABOVE: the highest vertex must be (essentially) level with the next —
            // a proud apex IS the spike the over-shoulder cam sees. Hard-clamp lands all crown verts on one
            // ceiling, so this gap is ~0. Tight 0.01u bar (the prior 0.02u ring-gap bar passed a real spike).
            Assert.Less(apexGap, 0.01f,
                $"no hair vertex may stand proud of the crown plateau (the over-shoulder 'brown spike'): " +
                $"apex gap {apexGap:F4}u (top1 {top1:F4} vs top2 {top2:F4}) must be < 0.01u");

            // (2) THE PLATEAU IS FLAT, NOT A CONE TIP: a hard-clamped crown has MANY verts sharing the max
            // height. A tapering cone would have a few verts near the top and a steep falloff — fail that.
            // Count verts within 0.01u of the top; a flat plateau has several, a spike/cone has ~1.
            int onPlateau = 0;
            foreach (var y in ys) if (top1 - y < 0.01f) onPlateau++;
            Assert.GreaterOrEqual(onPlateau, 4,
                $"the crown must be a FLAT plateau (>=4 verts within 0.01u of the top), not a cone/spike tip; " +
                $"got {onPlateau} (top1={top1:F4})");

            // (3) Belt-and-suspenders: the top-15 ring spread bar from SOAKFIX3 still holds (a coarse sanity
            // floor — the plateau makes this comfortably small now).
            float ringMin = ys[14];
            Assert.Less(top1 - ringMin, 0.05f,
                $"hair crown top-15 spread {(top1 - ringMin):F4}u must stay < 0.05u (coarse flat-crown floor)");
        }

        // HAIR FRONT-FRINGE guard (86ca8ce6y SOAKFIX5 — the 4th-attempt ORANGE TUFT). The crown-plateau guard
        // above proved the crown is FLAT (clamp lands all crown verts on one ceiling) and PASSED — yet the
        // Sponsor still saw a small ORANGE tuft poking ABOVE the crown at the top-FRONT from the over-the-
        // shoulder DOWN-looking gameplay cam (pitch 55-70°). DIAGNOSE-VIA-TRACE (HairMeshTrace) proved the
        // cause was NOT a proud crown vertex but the FRONT FRINGE jutting FORWARD (local z to -1.216, ~0.22u
        // beyond the nominal radius) at a height near the crown: a forward-jutting lobe projects ABOVE the
        // brow silhouette from a camera looking down the -Z face axis (closer + higher in screen-space than
        // the crown behind it) and catches the key light -> the bright "orange" tuft. The plateau guard is
        // BLIND to this (it only measures world-Y top verts; a forward lobe can sit BELOW the crown in Y yet
        // still project over the brow). THIS guard is the bug-CLASS catch: build the EXACT shipped mesh and
        // assert the FRINGE band (local z < -0.2, the face side) sits a real margin BELOW the crown ceiling
        // AND does not jut forward past a sane radius — so no front lobe can project above the crown from the
        // down-looking cam. Any future param/fringe regression that re-grows the forward tuft fails CI before
        // a Sponsor soak. Pure-geometry, deterministic via seed. (KEPT through the SOAKFIX7 flat revert — the
        // soakfix5 front-fringe fix is part of the clean flat hair the Sponsor asked to keep.)
        [Test]
        public void MessyHairCap_FrontFringe_SitsBelowCrown_NoForwardTuft()
        {
            var mesh = LowPolyMeshes.MessyHairCap(
                MovementCameraScene.HairCapRadius, MovementCameraScene.HairCapYScale,
                MovementCameraScene.HairCapCut, MovementCameraScene.HairCapSubdiv,
                MovementCameraScene.HairCapJitter, MovementCameraScene.HairCapSeed);
            Assert.IsNotNull(mesh, "MessyHairCap must build a mesh");

            float crownCeil = MovementCameraScene.HairCapRadius * MovementCameraScene.HairCapYScale * 0.705f;
            float radius = MovementCameraScene.HairCapRadius;

            float crownMaxY = float.MinValue, fringeMaxY = float.MinValue, fringeMinZ = float.MaxValue;
            int fringeVerts = 0;
            foreach (var v in mesh.vertices)
            {
                if (v.y > crownMaxY) crownMaxY = v.y;
                if (v.z < -0.2f) // the front FRINGE band (face side is -Z)
                {
                    fringeVerts++;
                    if (v.y > fringeMaxY) fringeMaxY = v.y;
                    if (v.z < fringeMinZ) fringeMinZ = v.z;
                }
            }
            Assert.Greater(fringeVerts, 4, "the cap must have a real front fringe band to measure");

            // (1) THE FRINGE SITS BELOW THE CROWN: the highest fringe vert must be a clear margin under the
            // crown ceiling, so a forward fringe lobe cannot project above the brow from the down-looking cam.
            // Trace post-fix: fringe maxY 0.5496 vs crownCeil 0.6204 -> 0.0708u gap. Require >= 0.04u.
            float fringeGap = crownCeil - fringeMaxY;
            Assert.GreaterOrEqual(fringeGap, 0.04f,
                $"the front fringe must sit a clear margin BELOW the crown (the over-shoulder orange tuft): " +
                $"fringe maxY {fringeMaxY:F4} vs crownCeil {crownCeil:F4} -> gap {fringeGap:F4}u must be >= 0.04u");

            // (2) THE FRINGE DOES NOT JUT FORWARD past a sane radius: a far-forward lobe (the pre-fix z=-1.216)
            // projects over the brow even at moderate height. Cap the forward jut. Post-fix minZ -1.030;
            // require >= -1.10u (a touch of forward fringe is fine; a jutting lobe is not).
            Assert.GreaterOrEqual(fringeMinZ, -1.10f * radius,
                $"the front fringe must not jut FORWARD past ~1.10r (the forward-lobe tuft): " +
                $"fringe minZ {fringeMinZ:F4} must be >= {-1.10f * radius:F4}u");
        }

        // HAIR SEATS-FLUSH guard (folds in 86ca8m3t2 — the hero close-up SEAM NIT). The close-up seam was the
        // hair mesh floating slightly HIGH/detached above the SKULL SURFACE (a visible gap between the hair
        // underside and the head at hero magnification). The TuftedHair base cap drops its skirt below the
        // crown (cut -0.30) AND the hair localPos was lowered so the cap OVERLAPS the skull crown — this guard
        // pins that seating so a future offset/cut regression can't re-open the seam.
        //
        // The reference is the SKULL CROWN (the castaway SkinnedMeshRenderer's world max.y — the top of the
        // head mesh), NOT the head BONE origin: the bone pivot sits at the neck base, ~0.6u below the crown,
        // so a bone-relative test is geometrically wrong (an earlier version of this guard mismeasured against
        // the bone). The seam is real iff the hair UNDERSIDE floats ABOVE the skull crown (a gap); a flush
        // seat has the base-cap skirt DIP BELOW the crown, wrapping the upper skull. This asserts the hair's
        // lowest vert sits at/below the skull crown (overlap), so there is no air gap at the close-up. The
        // geometry-only MessyHairCap guards were blind to this (no skull-relative placement check). Reads the
        // SHIPPED scene (the bytes the exe loads). Trace-measured post-fix: skull crown 1.649, hair skirt
        // 1.375 -> the skirt dips 0.274u below the crown (solid overlap, no seam).
        [Test]
        public void CastawayHair_SeatsFlushToHeadBone_NoCloseUpSeam()
        {
            OpenBootAndFindPlayer();
            var castaway = _player.GetComponentInChildren<CastawayCharacter>(true);
            Assert.IsNotNull(castaway);

            Transform hair = null;
            foreach (var t in castaway.GetComponentsInChildren<Transform>(true))
                if (t.name == MovementCameraScene.HairObjectName) { hair = t; break; }
            Assert.IsNotNull(hair, "the CastawayHair must be present under the avatar");

            var mf = hair.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "the hair must have a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "the hair mesh must be serialized");

            // The SKULL CROWN: the top of the castaway head mesh. Force-show the SMRs so bounds are valid in a
            // static editor load (the cap meshes are disabled, but the head mesh Object_36 is on).
            var smrs = castaway.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.Greater(smrs.Length, 0, "the castaway must carry skinned meshes to measure the skull crown");
            foreach (var smr in smrs) smr.enabled = true;
            float skullCrownY = float.MinValue;
            foreach (var smr in smrs)
            {
                if (smr.name == "CastawayHair") continue;
                if (smr.sharedMesh == null) continue;
                float top = smr.bounds.max.y;
                if (top > skullCrownY) skullCrownY = top;
            }
            Assert.Greater(skullCrownY, float.MinValue, "must resolve a skull crown world-Y");

            // The hair's LOWEST vert (the base-cap skirt) in world space.
            float minHairY = float.MaxValue, maxHairY = float.MinValue;
            foreach (var v in mf.sharedMesh.vertices)
            {
                float wy = hair.TransformPoint(v).y;
                if (wy < minHairY) minHairY = wy;
                if (wy > maxHairY) maxHairY = wy;
            }
            Assert.Greater(maxHairY - minHairY, 0.0001f, "the hair must have real world vertical extent");

            // FLUSH SEAT: the base-cap skirt must DIP BELOW the skull crown (overlap), not float above it. A
            // seam = the hair underside sits a gap ABOVE the crown. Require the skirt bottom at/below the crown.
            float skirtAboveCrown = minHairY - skullCrownY; // >0 means the hair underside floats above the skull (the seam)
            Assert.LessOrEqual(skirtAboveCrown, 0.0f,
                $"the hair base-cap skirt must seat FLUSH on the skull (close-up seam guard, 86ca8m3t2): the " +
                $"lowest hair vert (worldY {minHairY:F3}) sits {skirtAboveCrown:F3}u above the skull crown " +
                $"(worldY {skullCrownY:F3}); it must DIP BELOW the crown (overlap the skull), not float above it.");
        }
    }
}
