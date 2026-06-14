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

        // PANTS cell (86ca8ce6y SOAKFIX4 — clothes-tatter). The Sponsor said "pants/shorts (blue)". The
        // PANTS are on a DIFFERENT skinned object than the head/body (Object_44, the body+clothing mesh —
        // the chibi is SIX skinned objects sharing the atlas; a single-SMR trace MISSED them, the original
        // SOAKFIX4 blue-pants miss). DIAGNOSE-VIA-TRACE (UvCellTrace, per-object) CONFIRMS the Sponsor: the
        // pants are cell (7,15) = rgb(0.14,0.22,0.33), a BLUE denim cell (Object_44 uses it for 101 verts).
        // (My first pass wrongly weathered (15,5)/(15,6) — those are Object_36/38 SKIN-SHADOW cells, not the
        // pants; reverted.) The weathering PRESERVES the blue (darken/streak/fray only, no recolour).
        public static readonly Vector2Int PantsCell = new Vector2Int(7, 15); // blue denim pants (Object_44)

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
        // SOAKFIX5: lifted a hair (0.50,0.44,0.26 -> 0.54,0.47,0.28, luma 0.43 -> 0.46) so the BOLDER SOAKFIX5
        // weathering (punchy dirt/tears/hem) leaves the cell MEAN comfortably ABOVE the young/hopeful identity
        // floor (luma >0.34) — worn-but-warm khaki, not grizzled. Still olive-khaki (R>G>B), sub-1.0 HDR-safe,
        // inside the [0.42,0.66] guard band, and separates from the skin tan under the warm post.
        public static readonly Color ShirtTopColor = new Color(0.54f, 0.47f, 0.28f); // olive-khaki, lifted for the bolder weather
        public static readonly Color SkinTopColor  = new Color(0.80f, 0.62f, 0.48f); // toned warm tan (was 0.94,0.79,0.65 — blew white under the warm post)
        public static readonly Color FeetTopColor  = new Color(0.78f, 0.60f, 0.46f); // bare-feet skin, matches the toned skin
        // The cap-dome cells are still recolored sandy-ginger as a defensive base, but the dome+brim are
        // BOTH hidden now (the dome's cap-shaped arc read as a floating loop, not hair) and a clean
        // procedural hair skull-cap is added instead — so this colour mainly anchors the guard (R>G>B,
        // not green). The visible hair colour is HairMeshColor (MovementCameraScene), tuned from the soak.
        public static readonly Color HairTopColor  = new Color(0.70f, 0.45f, 0.22f); // sandy-ginger (guard anchor; dome hidden)
        // PANTS anchor (86ca8ce6y SOAKFIX4). FIXED constant (NOT read-from-atlas) so the weather pass is
        // IDEMPOTENT — a bootstrap re-run must converge, not darken progressively. Value = the chibi's
        // shipped BLUE denim top-of-gradient (per-object UvCellTrace: cell (7,15) ≈ rgb(0.14,0.22,0.33); the
        // cell is a gradient, this is a representative mid). Slightly lifted from the cell bottom so the toon
        // ramp re-applies cleanly. Weathering darkens/streaks/frays THIS blue — preserving the blue read.
        public static readonly Color PantsTopColor = new Color(0.20f, 0.30f, 0.42f); // blue denim pants (trace-measured)

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

            // WEATHER one garment cell (86ca8ce6y SOAKFIX5 — torn/tattered castaway clothes that READ at
            // gameplay distance). SOAKFIX4 weathered the cells but the Sponsor said "clothes seems UNCHANGED":
            // the overlay was too SUBTLE (mostly 0.78-0.92x wear, 0.045-wide tears) and the atlas cell is only
            // 16×16 px stretched over the body at the 14u orbit — fine detail simply does not read. FIX: make
            // the weathering BOLD + COARSE so it survives the orbit distance + the warm Zone-D post:
            //   - DIRT/WEAR PATCHES: bigger + darker grime blotches (down to ~0.55x), more of them, so dirt
            //     reads as real smudges not faint mottling;
            //   - TEARS: WIDER + DARKER rips (~0.40x core) with a torn-open look (a darker slit flanked by a
            //     slightly-lighter frayed lip) so a rip reads as fabric pulled apart, not a hairline;
            //   - FRAYED/RAGGED HEM: a deeper bottom band (~30%) with a stronger irregular notch so the hem
            //     reads visibly ragged (torn-off edge), the castaway-fiction tell.
            // STILL LUMA-PRESERVING per the brief: every overlay only DARKENS (multiplies by <=1), never
            // recolours — the shirt stays olive-khaki (R>G>B) + the pants stay blue denim (B>R). "Tattered",
            // not re-skinned. Absolute writes (idempotent) — a deterministic per-cell seed converges on a
            // bootstrap re-run. Bounded floor (wear never below 0.30x) so the garment never reads as holes.
            void PaintWeatheredCell(Vector2Int cell, Color anchor, int seed)
            {
                int x0 = cell.x * cw, y0 = cell.y * ch;
                var rnd = new System.Random(seed);
                // Pre-roll dirt-patch centres + tear lines so the overlay is stable per cell. MORE + BIGGER +
                // DARKER than SOAKFIX4 so the wear reads at gameplay distance.
                // READS-AT-DISTANCE STRATEGY (SOAKFIX5): at the 14u orbit a 16x16 cell stretches over the small
                // torso, so FINE detail (thin tears) blurs away — what reads is OVERALL TONAL BREAKUP: a few
                // LARGE dark grime zones with clean cloth between, plus a clearly-darker ragged hem. So use a
                // few BIG, HIGH-CONTRAST dirt zones (not many small ones that merge into flat darkening) +
                // bold wide tears + a strong hem. CONTRAST not just darkness: the clean cloth stays bright (the
                // toon ramp top) so the dark zones POP — which both reads at distance AND holds the cell MEAN
                // above the young/hopeful identity floor (luma >0.34) better than uniform darkening would.
                int patchN = 3;
                var px = new float[patchN]; var py = new float[patchN]; var pr = new float[patchN]; var pd = new float[patchN];
                for (int i = 0; i < patchN; i++)
                { px[i] = (float)rnd.NextDouble(); py[i] = (float)rnd.NextDouble(); pr[i] = 0.28f + (float)rnd.NextDouble() * 0.20f; pd[i] = 0.46f + (float)rnd.NextDouble() * 0.14f; }
                int tearN = 2;
                var tx = new float[tearN]; var tslope = new float[tearN]; var tlen = new float[tearN]; var tylo = new float[tearN];
                for (int i = 0; i < tearN; i++)
                { tx[i] = 0.2f + (float)rnd.NextDouble() * 0.6f; tslope[i] = ((float)rnd.NextDouble() - 0.5f) * 0.9f; tlen[i] = 0.4f + (float)rnd.NextDouble() * 0.35f; tylo[i] = (float)rnd.NextDouble() * 0.45f; }

                for (int yy = 0; yy < ch; yy++)
                {
                    float vf = ch > 1 ? (float)yy / (ch - 1) : 0.5f; // 0 bottom -> 1 top
                    float ramp = Mathf.Lerp(0.82f, 1.12f, vf);
                    for (int xx = 0; xx < cw; xx++)
                    {
                        float uf = cw > 1 ? (float)xx / (cw - 1) : 0.5f; // 0..1 across the cell
                        float wear = 1f; // luma-multiplier (<=1 only — darken, never brighten)

                        // dirt/wear patches — bigger + darker grime so the smudge reads at orbit distance.
                        for (int i = 0; i < patchN; i++)
                        {
                            float d = Mathf.Sqrt((uf - px[i]) * (uf - px[i]) + (vf - py[i]) * (vf - py[i]));
                            if (d < pr[i]) wear *= Mathf.Lerp(pd[i], 1f, d / pr[i]); // centre darkest, fades out
                        }
                        // tears — WIDER, DARKER rips with a torn-open look: a dark core slit (~0.40x) flanked
                        // by a slightly-lighter frayed lip, so a rip reads as fabric pulled apart, not a line.
                        for (int i = 0; i < tearN; i++)
                        {
                            if (vf >= tylo[i] && vf <= tylo[i] + tlen[i])
                            {
                                float lineU = tx[i] + tslope[i] * (vf - tylo[i]);
                                float du = Mathf.Abs(uf - lineU);
                                if (du < 0.06f) wear *= 0.38f;        // the rip core — a wide dark slit
                                else if (du < 0.12f) wear *= 0.70f;   // the frayed lip either side of the rip
                            }
                        }
                        // frayed/ragged hem — a deeper bottom band with a STRONGER irregular notch so the hem
                        // reads visibly torn-off (the castaway tell). Bottom ~26% of the cell. The notch makes
                        // SOME teeth dark (torn) + others near-full (intact) — a ragged edge, not a flat dark
                        // band (which would just drag the mean down without reading as "frayed").
                        if (vf < 0.24f)
                        {
                            float notch = 0.5f + 0.5f * Mathf.Sin((uf * 6.1f + cell.x * 1.7f) * Mathf.PI);
                            float hem = Mathf.Lerp(0.55f, 1f, vf / 0.24f); // very bottom darkest
                            wear *= Mathf.Lerp(hem, 1f, notch * 0.6f);     // notches: ragged torn-off teeth
                        }

                        // Floor — tattered but the cell MEAN must stay above the young/hopeful identity floor
                        // (the shirt-mean guard floors luma >0.34; the dark patches/tears are punchy but the
                        // floor keeps the average a worn-but-warm khaki, not grizzled — both garment cells clear
                        // the floor with margin).
                        wear = Mathf.Max(wear, 0.46f);

                        var c = new Color(
                            Mathf.Clamp01(anchor.r * ramp * wear),
                            Mathf.Clamp01(anchor.g * ramp * wear),
                            Mathf.Clamp01(anchor.b * ramp * wear), 1f);
                        tex.SetPixel(x0 + xx, y0 + yy, c);
                    }
                }
            }

            PaintWeatheredCell(ShirtCell, ShirtTopColor, 41011);    // torso shirt — olive-khaki, tattered
            PaintWeatheredCell(SleeveCell, ShirtTopColor, 41012);   // rolled sleeve — same khaki, tattered
            PaintWeatheredCell(PantsCell, PantsTopColor, 41013);    // blue denim pants — tattered (idempotent fixed anchor)
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
            Debug.Log("[CharacterAssetGen] identity atlas recolored (shirt/sleeve khaki + blue-denim pants " +
                      "WEATHERED/tattered, cap-dome->sandy hair, skin+feet toned) — reproducible-from-code, idempotent");
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

        // ===== UV-CELL REGION TRACE (throwaway; never on the ship path) — SOAKFIX4 clothes-tatter.
        // The Sponsor wants the SHIRT + PANTS/SHORTS weathered (torn/dirty). The shirt cells are already
        // mapped (ShirtCell/SleeveCell); the PANTS/SHORTS cell is NOT — the legs still ship the kid's default
        // blue. This trace MEASURES which atlas cell(s) the LOWER-BODY (leg/shorts) verts map to, so the
        // tatter repaint targets the right cell (diagnose-via-trace, not guess). For each vert below the hip
        // line it reads the vert's UV -> 16x16 cell and tallies; dumps the dominant lower-body cells + their
        // current atlas colour (to confirm the kid's blue), plus the upper-body (shirt) cells for sanity.
        // Run via:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CharacterAssetGen.UvCellTrace
        public static void UvCellTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[uv-trace] ===== UV-CELL REGION TRACE (POSED SCENE) =====");
            // Open the POSED Boot scene avatar (height-normalized, real world Y range) — the bind-pose FBX
            // instance bakes to a near-degenerate Y range that collapses the leg/torso split (SOAKFIX4 miss:
            // the shipped pants are BLUE but the bind-pose trace surfaced the wrong cells). The scene avatar
            // gives a real Y range so the LEG band is correctly isolated to its true (blue) cell.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            // Dump ALL renderers (skinned AND mesh) under the castaway — the pants might be a SEPARATE mesh /
            // material (the single-SMR assumption could be the blue-pants miss). Find which renderer's
            // material actually carries a BLUE base colour (the pants).
            var castaway = Object.FindAnyObjectByType<FarHorizon.CastawayCharacter>();
            if (castaway != null)
            {
                sb.AppendLine("[uv-trace] ALL renderers under castaway:");
                foreach (var r in castaway.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    var baseMap = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    var baseCol = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.clear;
                    string smrTag = r is SkinnedMeshRenderer ? "SMR" : r.GetType().Name;
                    sb.AppendLine($"[uv-trace]   {smrTag} '{r.name}' mat='{(m != null ? m.name : "<none>")}' baseMap='{(baseMap != null ? baseMap.name : "<none>")}' baseColor=({baseCol.r:F2},{baseCol.g:F2},{baseCol.b:F2})");
                }
            }
            // Per-OBJECT UV-cell dump — the chibi is SIX skinned objects sharing the atlas; the pants are on
            // a different object than Object_36 (head+body). Dump each object's dominant cells + blue flag.
            {
                var atlasTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                atlasTex.LoadImage(System.IO.File.ReadAllBytes(AtlasPngPath));
                int AW = atlasTex.width, AH = atlasTex.height;
                Color ACell(Vector2Int cl) => atlasTex.GetPixel(Mathf.Clamp((int)((cl.x + 0.5f) / 16f * AW), 0, AW - 1), Mathf.Clamp((int)((cl.y + 0.5f) / 16f * AH), 0, AH - 1));
                foreach (var rr in castaway.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mm = rr.sharedMesh; if (mm == null) continue;
                    var uu = mm.uv;
                    var t2 = new Dictionary<Vector2Int, int>();
                    for (int i = 0; i < uu.Length; i++)
                    {
                        var cell = new Vector2Int(Mathf.Clamp((int)(uu[i].x * 16f), 0, 15), Mathf.Clamp((int)(uu[i].y * 16f), 0, 15));
                        t2[cell] = t2.TryGetValue(cell, out int c) ? c + 1 : 1;
                    }
                    var l2 = new List<KeyValuePair<Vector2Int, int>>(t2); l2.Sort((a, c) => c.Value.CompareTo(a.Value));
                    var top = l2.Count > 0 ? l2[0] : default;
                    var tc = ACell(top.Key);
                    string blue = (tc.b > tc.r && tc.b > tc.g) ? " <== BLUE (PANTS?)" : "";
                    sb.AppendLine($"[uv-trace] OBJECT '{rr.name}' verts={mm.vertexCount} topCell=({top.Key.x},{top.Key.y}) cnt={top.Value} colour=rgb({tc.r:F2},{tc.g:F2},{tc.b:F2}){blue}");
                    for (int i = 1; i < l2.Count && i < 4; i++)
                    {
                        var cc = ACell(l2[i].Key);
                        string b2 = (cc.b > cc.r && cc.b > cc.g) ? " <== BLUE (PANTS?)" : "";
                        sb.AppendLine($"[uv-trace]     cell=({l2[i].Key.x},{l2[i].Key.y}) cnt={l2[i].Value} colour=rgb({cc.r:F2},{cc.g:F2},{cc.b:F2}){b2}");
                    }
                }
                Object.DestroyImmediate(atlasTex);
            }

            var smr = castaway != null ? castaway.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (smr == null) { sb.AppendLine("[uv-trace] no posed avatar in Boot.unity"); Debug.Log(sb.ToString()); if (Application.isBatchMode) EditorApplication.Exit(0); return; }
            var mesh = smr.sharedMesh;
            if (mesh == null) { sb.AppendLine("[uv-trace] no skinned mesh"); Debug.Log(sb.ToString()); if (Application.isBatchMode) EditorApplication.Exit(0); return; }

            // BONE-WEIGHT REGION SPLIT (pose-INDEPENDENT — the reliable map). A BakeMesh in EditMode returns
            // the bind/rest pose whose leg verts don't extend (Y range collapsed to 0.71..1.64, no feet), so a
            // Y-band split keeps MISSING the leg cell (the SOAKFIX4 blue-pants miss). Instead, classify each
            // vert by the BONE it is primarily skinned to: a vert weighted to a LEG/SHIN/FOOT bone IS leg/
            // shorts geometry regardless of pose. Read THOSE verts' UVs -> the shorts cell, deterministically.
            var bones = smr.bones;
            var uvs = mesh.uv;
            // The legacy mesh.boneWeights accessor returns a DEGENERATE array on this asset (every vert reads
            // boneIndex0=Head, weight 1.0 — proven by the dominant-bone diagnostic). This mesh stores skin
            // weights in the MODERN variable-influence API (GetBonesPerVertex + GetAllBoneWeights); read the
            // dominant bone per vertex from THAT (the first weight per vertex is the highest — sorted desc).
            var bonesPerVertex = mesh.GetBonesPerVertex(); // byte count per vert
            var allWeights = mesh.GetAllBoneWeights();      // flattened BoneWeight1 (sorted desc per vert)
            int vc = mesh.vertexCount;
            var domBone = new int[vc];
            int cursor = 0;
            for (int v = 0; v < vc; v++)
            {
                int cnt = v < bonesPerVertex.Length ? bonesPerVertex[v] : 0;
                domBone[v] = cnt > 0 && cursor < allWeights.Length ? allWeights[cursor].boneIndex : -1;
                cursor += cnt;
            }
            bool BoneIsLeg(int bi)
            {
                if (bi < 0 || bones == null || bi >= bones.Length || bones[bi] == null) return false;
                string n = bones[bi].name.ToLowerInvariant();
                return n.Contains("leg") || n.Contains("shin") || n.Contains("calf") || n.Contains("foot") ||
                       n.Contains("knee") || n.Contains("thigh");
            }
            bool BoneIsTorso(int bi)
            {
                if (bi < 0 || bones == null || bi >= bones.Length || bones[bi] == null) return false;
                string n = bones[bi].name.ToLowerInvariant();
                return n.Contains("spine") || n.Contains("chest") || n.Contains("torso") || n.Contains("hips");
            }
            // dump the bone roster so the leg-token match is auditable.
            sb.AppendLine($"[uv-trace] SMR bone count={bones?.Length} allWeights.Length={allWeights.Length} vertCount={mesh.vertexCount}");
            // SUBMESH + MATERIAL dump — the chibi ships TWO materials (mini_material + _secondary). The pants
            // may live on a SECOND submesh bound to a different material/atlas region (the first-submesh-only
            // UV read would MISS them — a candidate root cause of the blue-pants trace miss).
            sb.AppendLine($"[uv-trace] mesh subMeshCount={mesh.subMeshCount} sharedMaterials={smr.sharedMaterials.Length}");
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var sm = mesh.GetSubMesh(s);
                var matName = s < smr.sharedMaterials.Length && smr.sharedMaterials[s] != null ? smr.sharedMaterials[s].name : "?";
                var baseMap = s < smr.sharedMaterials.Length && smr.sharedMaterials[s] != null && smr.sharedMaterials[s].HasProperty("_BaseMap") ? smr.sharedMaterials[s].GetTexture("_BaseMap") : null;
                sb.AppendLine($"[uv-trace]   submesh[{s}] indexStart={sm.indexStart} count={sm.indexCount} firstVertex={sm.firstVertex} vertexCount={sm.vertexCount} mat='{matName}' baseMap='{(baseMap != null ? baseMap.name : "<none>")}'");
            }
            if (bones != null) for (int bi = 0; bi < bones.Length; bi++)
                sb.AppendLine($"[uv-trace]   bone[{bi}]='{(bones[bi] != null ? bones[bi].name : "<null>")}' isLeg={BoneIsLeg(bi)} isTorso={BoneIsTorso(bi)}");

            // Load the current atlas to dump cell colours.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(AtlasPngPath));
            int W = tex.width, H = tex.height;
            Color CellColorC(Vector2Int cell)
            {
                int px = Mathf.Clamp((int)((cell.x + 0.5f) / 16f * W), 0, W - 1);
                int py = Mathf.Clamp((int)((cell.y + 0.5f) / 16f * H), 0, H - 1);
                return tex.GetPixel(px, py);
            }
            string CellColor(Vector2Int cell) { var c = CellColorC(cell); return $"rgb({c.r:F2},{c.g:F2},{c.b:F2})"; }

            // Tally cells by BONE-WEIGHT region: a vert whose dominant bone is a LEG bone is shorts/leg
            // geometry; a torso-bone vert is shirt geometry. This is pose-independent.
            var legTally = new Dictionary<Vector2Int, int>();
            var torsoTally = new Dictionary<Vector2Int, int>();
            var allTally = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < uvs.Length && i < vc; i++)
            {
                var uv = uvs[i];
                int cx = Mathf.Clamp((int)(uv.x * 16f), 0, 15);
                int cy = Mathf.Clamp((int)(uv.y * 16f), 0, 15);
                var cell = new Vector2Int(cx, cy);
                allTally[cell] = allTally.TryGetValue(cell, out int ac) ? ac + 1 : 1;
                int dom = domBone[i]; // highest-weight bone (modern API)
                if (BoneIsLeg(dom)) legTally[cell] = legTally.TryGetValue(cell, out int lc) ? lc + 1 : 1;
                else if (BoneIsTorso(dom)) torsoTally[cell] = torsoTally.TryGetValue(cell, out int tc) ? tc + 1 : 1;
            }

            void DumpTally(string label, Dictionary<Vector2Int, int> tally)
            {
                var list = new List<KeyValuePair<Vector2Int, int>>(tally);
                list.Sort((a, c) => c.Value.CompareTo(a.Value));
                sb.AppendLine($"[uv-trace] {label} cells (by count):");
                for (int i = 0; i < list.Count && i < 8; i++)
                {
                    var c = CellColorC(list[i].Key);
                    string tag = (c.b > c.r && c.b > c.g) ? " <-- BLUE" : "";
                    sb.AppendLine($"[uv-trace]   cell({list[i].Key.x},{list[i].Key.y}) count={list[i].Value} colour={CellColor(list[i].Key)}{tag}");
                }
            }
            // Diagnostic: distribution of dominant-bone indices (modern API).
            var domDist = new Dictionary<int, int>();
            for (int i = 0; i < vc; i++) { int d = domBone[i]; domDist[d] = domDist.TryGetValue(d, out int dc) ? dc + 1 : 1; }
            var dd = new List<KeyValuePair<int, int>>(domDist); dd.Sort((a, c) => c.Value.CompareTo(a.Value));
            sb.AppendLine("[uv-trace] dominant-bone distribution:");
            foreach (var kv in dd)
                sb.AppendLine($"[uv-trace]   bone[{kv.Key}]='{(kv.Key >= 0 && kv.Key < bones.Length && bones[kv.Key] != null ? bones[kv.Key].name : "?")}' count={kv.Value}");
            DumpTally("LEG/SHORTS-bone", legTally);
            DumpTally("TORSO/SHIRT-bone", torsoTally);
            sb.AppendLine($"[uv-trace] known ShirtCell={ShirtCell} colour={CellColor(ShirtCell)}");
            sb.AppendLine($"[uv-trace] known SkinCell={SkinCell} colour={CellColor(SkinCell)}");

            // (3) FULL ATLAS DUMP: every cell the mesh USES, with colour — so the shorts cell is identifiable
            // by colour even when the bind-pose Y-band split is degenerate. Marks blue-ish + dark cells.
            sb.AppendLine("[uv-trace] ALL mesh-used cells (count>=3), colour, tags:");
            var used = new List<KeyValuePair<Vector2Int, int>>(allTally);
            used.Sort((a, c) => c.Value.CompareTo(a.Value));
            foreach (var kv in used)
            {
                if (kv.Value < 3) continue;
                var c = CellColorC(kv.Key);
                string tag = "";
                if (c.b > c.r && c.b >= c.g) tag += " BLUE";
                if (c.r > c.g && c.g > c.b) tag += " WARM";
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                if (lum < 0.3f) tag += " DARK";
                sb.AppendLine($"[uv-trace]   cell({kv.Key.x},{kv.Key.y}) count={kv.Value} colour={CellColor(kv.Key)} lum={lum:F2}{tag}");
            }
            Object.DestroyImmediate(tex);
            sb.AppendLine("[uv-trace] ===== END TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
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
