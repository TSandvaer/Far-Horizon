using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Prepares the "Mini Chibi Kid" sourced CC0-Attribution rigged low-poly character (Sketchfab,
    /// joaobaltieri) for the player — the chunky-cartoon castaway base (ticket 86ca8ca1m). This
    /// SUPERSEDES the Quaternius Animated-Men character: the realistic Quaternius head could not be
    /// cartoon-ified, so the Sponsor swapped to a purpose-built chunky base whose big-head toy
    /// proportions are INTRINSIC to the mesh — no bone-scale dials needed (the PR #25 bone-scale path
    /// is dropped for this base; the mesh is already chunky).
    ///
    /// Runs from batchmode (no GUI) so the whole project stays reproducible-from-code (the project
    /// invariant; CI re-runs BootstrapProject.Run which calls this):
    ///
    ///   1. Configure the FBX ModelImporter: Generic rig + CreateFromThisModel avatar (the FBX imports
    ///      as NoAvatar — without an avatar the clips cannot bind and the model freezes in its bind
    ///      pose), import its bundled animation clips, normalize the intrinsic ~1.82u import height to
    ///      ~1u, and mark the locomotion clips (Idle/Walk) as looping. The clip names carry the
    ///      "Object_5|" armature prefix (e.g. "Object_5|Idle 01") — match by .Contains, NOT exact
    ///      equality (exact-match loops ZERO clips — the T-pose-mid-walk failure class, see the guard).
    ///   2. The material atlas binds out of the box: ImportStandard imports two URP/Lit materials
    ///      (mini_material / mini_material_secondary) with _BaseMap = mini_material_baseColor (the 256²
    ///      toon atlas) — verified by probe. We KEEP the FBX's own flat toon materials + atlas; the
    ///      IDENTITY RECOLOR (ticket 86ca8ca1m) is done by REPAINTING specific UV cells of the atlas PNG
    ///      itself (shirt -> warm khaki, hair/cap -> sandy-ginger, shoes -> bare-feet skin) — NOT by
    ///      replacing the materials (a per-material tint would WIPE the gradient toon-shade to a flat
    ///      colour). So the chibi reads as OUR young/hopeful castaway, not the generic green-cap kid,
    ///      while the flat toon look survives. We assert the atlas binds (missing-texture grey fails the
    ///      import) AND the identity guard re-samples the recolored cells (see CastawayCharacterTests).
    ///   3. Build an AnimatorController asset with an Idle&lt;-&gt;Walk blend driven by a "Moving" bool,
    ///      cross-faded so the gait reads (no hard pop). CastawayCharacter.SetBool("Moving", ...) flips it.
    ///
    /// RIG CHOICE — GENERIC, not Humanoid (same rationale as the prior base): the character ships its
    /// OWN Idle/Walk clips on its OWN 29-bone armature (Hips_00..Head_05, Right/LeftFoot_0xx), so NO
    /// cross-FBX retarget is needed — a Generic rig that creates an avatar from this model
    /// (CreateFromThisModel) binds the clips directly. A Humanoid retarget adds an avatar-build step that
    /// risks failing in batchmode on a single-model port for ZERO benefit here. unity-conventions.md
    /// §FBX documents the avatarSetup T-pose trap + the armature clip-prefix finding, both honored below.
    ///
    /// Source: "Mini Chibi Kid" by joaobaltieri (Sketchfab, CC-Attribution). ~1442 faces, 29-bone
    /// Mixamo-style rig, big-head toy proportions, 2 flat toon materials on a 256² atlas. License/
    /// attribution committed alongside the FBX. Imports UPRIGHT (probe-verified: head world-Y 0.785
    /// above feet world-Y 0.019, feet at origin) — no -90° X bake required for this asset.
    /// </summary>
    public static class CharacterAssetGen
    {
        public const string FbxPath = "Assets/Art/Character/MiniChibiKid/MiniChibiKid.fbx";
        public const string ControllerPath = "Assets/Art/Character/CastawayAnimator.controller";

        // Clip name TOKENS inside the FBX we wire. The chibi ships an alt-set ("Idle 01"/"Walk 01"/
        // "Run 01") AND the FBX clip names carry the "Object_5|" armature prefix (e.g.
        // "Object_5|Idle 01") — match by Contains on these tokens, NOT exact equality. (Empirically the
        // shipped clip names are the " 01" alt-set, NOT "Man_Idle"/"Man_Walk" — verified by probe;
        // matching the real names is load-bearing.)
        public const string IdleClip = "Idle 01";
        public const string WalkClip = "Walk 01";

        // The toon atlas the imported materials bind. Asserted present so a missing-texture grey
        // regression (the flat toon look silently lost) fails the import.
        public const string AtlasTextureName = "mini_material_baseColor";

        // The standalone atlas PNG path the imported materials actually bind on _BaseMap (probe-verified:
        // Unity remaps the FBX materials to this asset, NOT the .fbm/ embedded extract — so this is the
        // file the recolor edits). Identity recolor (ticket 86ca8ca1m) repaints specific UV cells here.
        public const string AtlasPngPath = "Assets/Art/Character/MiniChibiKid/mini_material_baseColor.png";

        // IDENTITY RECOLOR — atlas UV cells (16x16 grid of vertical-gradient toon-shade cells), mapped by
        // a per-region bone-influence probe (86ca8ca1m). UV origin bottom-left; cell (colX,rowY) covers
        // UV x in [colX/16,(colX+1)/16], v in [rowY/16,(rowY+1)/16]. These are the recolor TARGETS the
        // identity guard re-samples — keeping the SHIPPED look and the guard reading the SAME source.
        //   Shirt (torso + rolled sleeve): warm khaki, the U2-6 anchor (0.72,0.60,0.42), luma >0.6.
        //   Hair + cap: sandy/ginger (the green-cap kid restyled toward warm hair).
        //   Bare feet: skin tan (vs the kid's dark shoes).
        public static readonly Vector2Int ShirtCell = new Vector2Int(15, 11); // torso shirt
        public static readonly Vector2Int HairCell = new Vector2Int(12, 11);  // hair mass
        public static readonly Vector2Int CapCell = new Vector2Int(1, 10);    // former green cap -> warm hair
        public static readonly Vector2Int FeetCell = new Vector2Int(12, 12);  // bare feet (was shoes)

        // SOAK-FIX cells (86ca8ca1m soak-fix). The Sponsor soaked 46f2a9d and reported "yellow cap / no
        // shirt / all-yellow blob" — the GAMEPLAY orbit view (warm Zone-D post × warm key × the BRIGHT
        // atlas) washed the castaway to a uniform yellow-cream, the cap still read as a cap, and the
        // khaki shirt had no contrast against the skin. Empirically mapped by CastawayDiagnoseTrace:
        //   - The kept CAP DOME (Object_41, now the hair mass) uses TWO cells: CapCell(1,10) AND
        //     CapDome2(1,9) — and (1,9) was left GREEN by the v1 recolor. BOTH are repainted sandy-ginger
        //     so the dome reads as HAIR, not a green/yellow cap.
        //   - The SkinCell(12,2) (Object_36 head+body) was very bright (0.93,0.76,0.62) and BLEW OUT to
        //     white under the post lift — toned down so the face/body holds tone instead of washing.
        //   - Shirt + feet are DEEPENED (below) so they survive the post lift without blowing to cream
        //     and the shirt SEPARATES from the skin (the "no shirt" read).
        public static readonly Vector2Int CapDome2Cell = new Vector2Int(1, 9); // 2nd cap-dome cell (was green)
        public static readonly Vector2Int SkinCell = new Vector2Int(12, 2);    // head+body skin (was blown bright)
        public static readonly Vector2Int SleeveCell = new Vector2Int(15, 10); // shirt rolled-sleeve cell

        // ---- SOAK-FIX target colours (86ca8ca1m soak-fix). Each is the cell's TOP-of-gradient anchor;
        // RecolorIdentityAtlas paints a vertical toon ramp from this anchor (top brighter -> bottom
        // darker) so the flat toon-shade gradient survives. All sub-1.0 (HDR-safe) and TONED so the
        // warm post no longer blows them to yellow. Verified against the post stack by a shipped capture. ----
        // SOAKFIX2 (Sponsor: shirt "renders pale-cream under the warm key" → deepen for shirt/skin
        // separation). The prior (0.62,0.50,0.30) still lifted toward cream under the warm Zone-D post +
        // bloom and read too close to the warm skin tan (0.80,0.62,0.48). DEEPENED + made more OLIVE-khaki:
        // a darker, more saturated field-khaki (luma ~0.44, a ~0.21 luma gap below the skin's 0.65) that
        // holds tone under the warm lift and reads CLEARLY as a shirt against the skin. Still WARM (R>G>B)
        // and sub-1.0 HDR-safe; sits comfortably inside the guard band's lower half (deliberately deeper).
        public static readonly Color ShirtTopColor = new Color(0.50f, 0.44f, 0.26f); // deeper olive-khaki — separates from skin under the warm post
        public static readonly Color SkinTopColor  = new Color(0.80f, 0.62f, 0.48f); // toned warm tan (was 0.94,0.79,0.65 — blew white under the warm post)
        public static readonly Color FeetTopColor  = new Color(0.78f, 0.60f, 0.46f); // bare-feet skin, matches the toned skin
        // The cap-dome cells are still recolored sandy-ginger as a defensive base, but the dome+brim are
        // BOTH hidden now (the dome's cap-shaped arc read as a floating loop, not hair) and a clean
        // procedural hair skull-cap is added instead — so this colour mainly anchors the guard (R>G>B,
        // not green). The visible hair colour is HairMeshColor (MovementCameraScene), tuned from the soak.
        public static readonly Color HairTopColor  = new Color(0.70f, 0.45f, 0.22f); // sandy-ginger (guard anchor; dome hidden)

        // Normalize the FBX's intrinsic import height to ~1 world-unit so the avatar-root scale maps
        // directly onto on-screen height (the camera/NavMesh/grounding are calibrated to ~1u). The chibi
        // FBX imports at ~1.82u intrinsic (measured) — un-normalized it ships ~1.8× tall on the 1.8u
        // avatar root → a giant. Re-derived from the LIVE measured bounds so a future swap self-corrects.
        public const float TargetImportHeightU = 1.0f;

        public static void PrepareCharacter()
        {
            ConfigureFbxImporter();
            BuildAnimatorController();
            // IDENTITY RECOLOR (86ca8ca1m + soak-fix) — make the recolor REPRODUCIBLE-FROM-CODE (the
            // project invariant: CI re-runs bootstrap). The v1 recolor was a hand-run PIL script (the
            // atlas PNG was committed with no code path to regenerate it). This bakes the per-cell
            // identity colours from CODE, deterministically + idempotently, so a bootstrap re-run always
            // reproduces the SAME shipped atlas — and the soak-fix toning lives in source, not a mystery
            // hand-edit. Runs AFTER the FBX import (the atlas PNG is a standalone asset the materials
            // bind on _BaseMap — repainting it does not need the FBX re-imported).
            RecolorIdentityAtlas();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterAssetGen] character assets prepared: " + FbxPath + " + " + ControllerPath);
        }

        // Repaint the identity UV cells of the bound toon atlas (86ca8ca1m soak-fix), reproducibly +
        // idempotently. Each target cell is painted with a deterministic VERTICAL TOON RAMP from its
        // anchor colour (top ~1.12x brighter -> bottom ~0.82x darker over the cell's v span), preserving
        // the flat toon-shade gradient the chibi ships. Painting ABSOLUTE values (not a multiplicative
        // tint of the prior pixels) makes a bootstrap re-run converge to the exact same atlas — the
        // idempotency the reproducible-from-code invariant needs. Cells repainted: shirt + sleeve (deeper
        // mid-khaki, separates from skin), the kept cap-dome's TWO cells (sandy-ginger hair — kills the
        // leftover green), skin (toned so the post lift doesn't blow it white), feet (bare-skin tan).
        public static void RecolorIdentityAtlas()
        {
            string path = AtlasPngPath;
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[CharacterAssetGen] atlas PNG not found at " + path + " — cannot recolor");
                return;
            }

            // Decode the committed PNG into a writable Texture2D (the imported asset has isReadable=0, so
            // read the file bytes directly rather than GetPixels on the import).
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Debug.LogError("[CharacterAssetGen] atlas PNG failed to decode — recolor aborted");
                Object.DestroyImmediate(tex);
                return;
            }
            int W = tex.width, H = tex.height;
            int cw = W / 16, ch = H / 16;

            // Paint one 16x16 UV cell with a deterministic vertical toon ramp from anchor (top bright ->
            // bottom darker). UV v is up; the Texture2D's y is also up (GetPixel/SetPixel bottom-left),
            // so cell row*ch..(row+1)*ch maps directly. Idempotent: absolute writes.
            void PaintCell(Vector2Int cell, Color anchor)
            {
                int x0 = cell.x * cw, y0 = cell.y * ch;
                for (int yy = 0; yy < ch; yy++)
                {
                    // v fraction within the cell (0 at bottom -> 1 at top). Toon ramp: top brightest.
                    float vf = ch > 1 ? (float)yy / (ch - 1) : 0.5f;
                    float ramp = Mathf.Lerp(0.82f, 1.12f, vf); // bottom 0.82x -> top 1.12x
                    var c = new Color(
                        Mathf.Clamp01(anchor.r * ramp),
                        Mathf.Clamp01(anchor.g * ramp),
                        Mathf.Clamp01(anchor.b * ramp), 1f);
                    for (int xx = 0; xx < cw; xx++)
                        tex.SetPixel(x0 + xx, y0 + yy, c);
                }
            }

            PaintCell(ShirtCell, ShirtTopColor);     // torso shirt — deeper mid-khaki
            PaintCell(SleeveCell, ShirtTopColor);     // rolled sleeve — same khaki
            PaintCell(CapCell, HairTopColor);         // cap-dome cell A -> sandy-ginger hair
            PaintCell(CapDome2Cell, HairTopColor);    // cap-dome cell B (was GREEN) -> sandy-ginger hair
            PaintCell(HairCell, HairTopColor);         // the brim's hair cell (kept consistent)
            PaintCell(SkinCell, SkinTopColor);         // head+body skin — toned so it holds under the post lift
            PaintCell(FeetCell, FeetTopColor);         // bare feet — toned skin tan
            tex.Apply();

            // Write the repainted atlas back to disk as the committed source (sRGB PNG). The import
            // settings (sRGB on, readable off) are unchanged — only the pixels change.
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log("[CharacterAssetGen] identity atlas recolored (shirt/sleeve khaki, cap-dome->sandy hair, " +
                      "skin+feet toned) — reproducible-from-code, idempotent");
        }

        private static void ConfigureFbxImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[CharacterAssetGen] FBX not found at " + FbxPath);
                return;
            }

            // Generic rig (the character's own skeleton). A Generic rig must CREATE AN AVATAR from this
            // model, or the imported Animator has no avatar + the clips cannot bind -> the model renders
            // frozen in its bind/T-pose. CreateFromThisModel builds the avatar from the FBX's own bone
            // hierarchy so Idle/Walk animate the skeleton. (The chibi imports as NoAvatar by default.)
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            // Keep the FBX's own flat toon materials + the 256² atlas (ImportStandard binds
            // _BaseMap = mini_material_baseColor — verified by probe). The IDENTITY RECOLOR (86ca8ca1m)
            // lives in the atlas PNG's repainted cells (warm khaki shirt / sandy-ginger hair+cap /
            // bare-feet skin), so the toon-shade gradient survives — we do NOT replace the materials.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            // HEIGHT NORMALIZATION: measure the imported model's intrinsic height + set globalScale so
            // it imports at ~TargetImportHeightU (1u). Done BEFORE the clip-loop reimport so a single
            // SaveAndReimport applies scale + loop flags together. Re-derived from live bounds.
            float measured = MeasureImportedModelHeight();
            if (measured > 0.01f)
            {
                float factor = importer.globalScale * (TargetImportHeightU / measured);
                importer.globalScale = factor;
                Debug.Log($"[CharacterAssetGen] height-normalize: measured={measured:F3}u -> globalScale={factor:F5} " +
                          $"(target {TargetImportHeightU}u)");
            }
            else
            {
                Debug.LogWarning("[CharacterAssetGen] could not measure model height — skipping normalize");
            }

            // Mark the locomotion clips as looping so the Animator doesn't freeze mid-cycle. The FBX
            // clip names carry the armature prefix ("Object_5|Idle 01"), NOT bare "Idle 01" — match by
            // Contains, not exact equality. An exact-match silently sets the loop flag on ZERO clips
            // (loopTime stays 0 in the .meta) and the Animator plays once + freezes (the T-pose-mid-walk
            // failure class). Pinned by the EditMode test that asserts isLooping on both.
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            var edited = new List<ModelImporterClipAnimation>();
            int looped = 0;
            foreach (var c in clips)
            {
                var cc = c;
                if (cc.name.Contains(IdleClip) || cc.name.Contains(WalkClip))
                {
                    cc.loopTime = true;
                    cc.loop = true;
                    looped++;
                }
                edited.Add(cc);
            }
            importer.clipAnimations = edited.ToArray();

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] FBX reimported: rig=Generic, {edited.Count} clips, " +
                      $"{looped} set to loop (matched {IdleClip}/{WalkClip} by Contains)");
            if (looped < 2)
                Debug.LogError($"[CharacterAssetGen] expected to loop 2 clips ({IdleClip}+{WalkClip}) " +
                               $"but matched {looped} — locomotion clips may freeze mid-cycle (T-pose risk)");

            // Verify the toon atlas binds (the flat toon look must ship — a missing-texture grey is a
            // silent identity loss). Checks the imported materials carry the atlas on _BaseMap.
            int atlasBound = 0;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is Material m && m.HasProperty("_BaseMap"))
                {
                    var tex = m.GetTexture("_BaseMap");
                    if (tex != null && tex.name.Contains(AtlasTextureName)) atlasBound++;
                }
            }
            if (atlasBound == 0)
                Debug.LogError("[CharacterAssetGen] no imported material binds the toon atlas '" +
                               AtlasTextureName + "' on _BaseMap — the flat toon look would ship as grey");
            else
                Debug.Log($"[CharacterAssetGen] toon atlas bound on {atlasBound} material(s)");
        }

        // Instantiate the currently-imported FBX, measure its renderer-bounds height (world Y extent),
        // and clean up. Used to derive the import-scale normalization factor. Returns 0 on failure.
        private static float MeasureImportedModelHeight()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) return 0f;
            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            var rends = inst.GetComponentsInChildren<Renderer>();
            float h = 0f;
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                h = b.size.y;
            }
            Object.DestroyImmediate(inst);
            return h;
        }

        private static AnimationClip FindClip(string name)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is AnimationClip clip && clip.name == name) return clip;
            }
            // Fallback: a clip whose name CONTAINS the token (the armature-prefix case, e.g.
            // "Object_5|Idle 01"). Skip Unity's "__preview__" mirror clips.
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (obj is AnimationClip clip && clip.name.Contains(name) &&
                    !clip.name.StartsWith("__preview__")) return clip;
            }
            return null;
        }

        private static void BuildAnimatorController()
        {
            AnimationClip idle = FindClip(IdleClip);
            AnimationClip walk = FindClip(WalkClip);
            if (idle == null || walk == null)
            {
                Debug.LogError($"[CharacterAssetGen] missing clips (idle={idle != null}, walk={walk != null}); " +
                               "controller not built");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            var walkState = sm.AddState("Walk");
            walkState.motion = walk;
            sm.defaultState = idleState;

            // Idle -> Walk when Moving, Walk -> Idle when !Moving; short blends for a smooth gait.
            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;

            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            Debug.Log("[CharacterAssetGen] AnimatorController built: Idle<->Walk on Moving bool");
        }
    }
}
