using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Prepares the HYPER3D + MIXAMO castaway for the player — the generated, on-style chunky low-poly
    /// young/hopeful castaway (ticket 86ca8rdkp). This ADOPTS the viability-spike asset (spike 86ca8r72j /
    /// PR #45) into the live game, SUPERSEDING the Sketchfab "Mini Chibi Kid" (the chibi's realistic-leaning
    /// face + 6-SMR-atlas weathering grind is dropped). The Sponsor APPROVED the swap 2026-06-15 after the
    /// spike proved this asset animates clean + on-style in the shipped URP exe.
    ///
    /// PIPELINE — TWO Mixamo FBX, GENERIC rig:
    ///   1. Idle.fbx  (WITH skin = mesh + rig + Idle clip) → rig=Generic, avatarSetup=CreateFromThisModel
    ///      (builds an avatar from the Mixamo skeleton), import the clip, loop it, height-normalize the
    ///      intrinsic import to ~1u (the camera/NavMesh/grounding are ~1u-calibrated).
    ///   2. Walking.fbx (WITHOUT skin = Walk clip only) → rig=Generic, avatarSetup=CreateFromThisModel; the
    ///      Generic Walk clip binds by TRANSFORM PATH onto Idle's mesh (same mixamorig bone names) — no
    ///      Humanoid muscle-space retarget.
    ///
    /// RIG = GENERIC (86ca8rdkp — the runtime-explosion fix). The ticket's prescribed Humanoid path EXPLODED
    /// the skinned mesh into a cone / stretched arm at RUNTIME in the production scene (the Humanoid retarget
    /// reconstructs the pose in muscle space, which mismatched this FBX's internal 100× mesh node + the scene's
    /// avatar root). The spike's Humanoid captures only LOOKED clean because the spike capture camera FOLLOWED
    /// the displaced mesh (fixed scene camera would have shown empty). GENERIC binds clips by transform path
    /// onto the FBX's own mixamorig skeleton — NO muscle retarget — and renders clean at the player with sane
    /// bone world positions (rig-trace-verified: body at spawn, RightHand at the hand). The spike ALSO proved
    /// Generic clean (captures 04/05); it is the choice the live chibi made too.
    ///   3. A flat de-lit URP/Lit material from texture_diffuse as _BaseMap (smoothness ~0, no metallic) so
    ///      the baked toon-shaded albedo reads de-lit/toon (the project's URP/Lit toon idiom). Bound onto the
    ///      avatar's SkinnedMeshRenderer(s) editor-time by MovementCameraScene (the FBX imports its own
    ///      ImportStandard material, but we author a single shared de-lit mat for the toon look + the recolor).
    ///   4. IDENTITY RECOLOR (86ca8rdkp AC4) — warm the mustard-yellow generated shirt toward TAN toward the
    ///      concept (concepts/concept_01_front.png) WITHOUT flattening the toon gradient. A per-material tint
    ///      would WIPE the baked gradient to a flat colour (the trap CastawayCharacter always warned of); the
    ///      texture_diffuse atlas is NOT a 16x16 grid (it is an organic UV unwrap), so the chibi's per-cell
    ///      repaint does not apply. Instead a LUMA-PRESERVING HUE REMAP of the saturated YELLOW shirt pixels
    ///      shifts hue yellow->warm-tan + tones saturation while KEEPING each texel's value (so the toon
    ///      light->dark gradient survives). Reproducible-from-code + idempotent (absolute HSV remap keyed on
    ///      the SOURCE yellow band, NOT the prior pixels), so a bootstrap re-run converges. A REASONABLE pass
    ///      the Sponsor judges from the build — NOT a grind (per the ticket).
    ///
    /// unity-conventions.md §FBX documents the avatarSetup T-pose trap (honored by CreateFromThisModel) and
    /// the clip-take-name finding (the Mixamo take is "mixamo.com", NOT "Idle"/"Walk" — match by Contains +
    /// rename, or zero clips loop = the T-pose-mid-walk failure class).
    ///
    /// Source: Hyper3D Rodin Image-to-3D (Gen-2.5 Quad ~8000) + Mixamo auto-rig (Standard 65 skeleton),
    /// generated from an openai-image A-pose concept (see .claude/docs/character-pipeline.md). License is
    /// CC-style generated content; attribution committed alongside the FBX.
    /// </summary>
    public static class CharacterAssetGen
    {
        // PRODUCTION location (NOT _Spike). The two Mixamo FBX + the de-lit material + the controller.
        private const string CharDir = "Assets/Art/Character/Castaway";
        public const string IdleFbxPath = CharDir + "/Idle.fbx";   // WITH skin (mesh+rig+Idle clip)
        // BREATHING IDLE (86cackb3j re-soak — "calm but clearly alive"). The Sponsor sourced a Mixamo Breathing
        // Idle clip (WITHOUT skin — gentle chest/shoulder breathing + subtle weight shift) to REPLACE the static
        // Idle.fbx clip as the at-rest pose. WITHOUT skin → binds by TRANSFORM PATH onto Idle's mesh (same
        // mixamorig skeleton — the proven Walk/Run idiom). The Idle.fbx still ships the SKIN (mesh+rig+avatar);
        // only the IDLE STATE's clip is swapped to this breathing take. LOOPING (a sustained at-rest cycle).
        public const string BreathingIdleFbxPath = CharDir + "/Breathing Idle.fbx"; // WITHOUT skin (breathing idle, LOOP)
        public const string WalkFbxPath = CharDir + "/Walking.fbx"; // WITHOUT skin (Walk clip only)
        public const string RunFbxPath = CharDir + "/Running.fbx";  // WITHOUT skin (Run clip only — 86ca9yq34)
        // TWO jump clips by movement state (86ca9yq3q rework — Sponsor soak): an idle/standing jump and a
        // walk/run jump. The single Jump.fbx is REPLACED — the controller plays Jump_idle when standing and
        // Jump_running when moving, so a jump initiated mid-locomotion reads as a running jump and lands back
        // into Walk/Run. Both WITHOUT skin (bind by transform path onto Idle's mesh, same as Walk/Run).
        public const string JumpIdleFbxPath = CharDir + "/Jump_idle.fbx";       // WITHOUT skin (idle/standing jump)
        public const string JumpRunningFbxPath = CharDir + "/Jump_running.fbx"; // WITHOUT skin (walk/run jump)
        // The CHOP swing clip (86caa4c5c change-(b) — the Sponsor's Mixamo "Standing Melee Attack Downward",
        // WITH skin, same castaway rig as the other clips → binds by transform path; chop-swing-mixamo-clip-not-
        // procedural). REPLACES the procedural ChopPoseDriver swing the Sponsor rejected. Imported NON-looping
        // (a one-shot strike), Generic — identical idiom to the jump one-shots.
        public const string MeleeFbxPath = CharDir + "/Melee_Attack.fbx";       // WITH skin (Melee one-shot clip)
        // ===== CROUCH + HIT-REACT clips (86cackb3j — locomotion/hit-react integration). All WITHOUT skin, GENERIC,
        // bind by transform path onto Idle's mesh (same mixamorig rig — the proven Walk/Run/jump idiom). Looping
        // for the sustained states (crouch idle/move, stunned hold), one-shot for the reactions/recovery/interaction.
        public const string SneakWalkFbxPath = CharDir + "/Sneak Walk.fbx";       // crouch-move (LOOP)
        public const string CrouchIdleFbxPath = CharDir + "/Crouching Idle.fbx";  // crouch stand (LOOP)
        public const string GettingUpFbxPath = CharDir + "/Getting Up.fbx";       // stun recovery (one-shot)
        public const string PickingUpFbxPath = CharDir + "/Picking Up.fbx";       // ground-pick interaction (one-shot)
        public const string StunnedFbxPath = CharDir + "/Stunned.fbx";            // knocked-down hold (LOOP)
        public const string HeadHitFbxPath = CharDir + "/Head Hit.fbx";           // hit-react (one-shot)
        public const string BigStomachHitFbxPath = CharDir + "/Big Stomach Hit.fbx"; // hit-react (one-shot)
        public const string StomachHitFbxPath = CharDir + "/Stomach Hit.fbx";     // hit-react (one-shot)
        public const string RibHitFbxPath = CharDir + "/Rib Hit.fbx";             // hit-react (one-shot)
        public const string HitToBodyFbxPath = CharDir + "/Hit To Body.fbx";      // hit-react (one-shot, default region)
        public const string DiffusePngPath = CharDir + "/texture_diffuse.png";
        public const string NormalPngPath = CharDir + "/texture_normal.png";
        public const string MaterialPath = CharDir + "/CastawayMat.mat";
        public const string ControllerPath = CharDir + "/CastawayAnimator.controller";

        // ===== CASTAWAY v2 (Rodin base — ticket 86cajwp23). A NEW hero base (bearded rugged adult survivor,
        // Sponsor-approved 2026-07-05), generated via the SAME Hyper3D-Rodin → Mixamo → Unity route as the
        // original castaway (character-pipeline.md). Imported GENERIC + transform-path (CreateFromThisModel),
        // the SAME shipping recipe as the current castaway — NOT Humanoid, NOT CopyFromOther (Humanoid
        // cone-explodes the skinned mesh at runtime; 86ca8rdkp / live anti-Humanoid gate). v2's 41 Mixamo
        // bones are a SUBSET of the clip skeleton (only middle/ring fingers missing), so the existing 18
        // WITHOUT-skin clips (BreathingIdle/Walk/Run/Jump*/Melee/Crouch*/hit-reacts/Stunned/…) bind onto v2's
        // mesh by TRANSFORM PATH with NO retarget — the same way they already bind onto the old Idle.fbx mesh.
        // Source-of-truth files live under art-src/castaway-rodin-export/; the integration-consumed subset
        // (rigged mesh + de-lit diffuse + normal) is committed here under v2/ so the .meta is deterministic.
        public const string V2Dir = CharDir + "/v2";
        public const string V2RiggedFbxPath = V2Dir + "/castaway_rigged_tpose.fbx"; // WITH skin (mesh+rig; T-pose take unused)
        public const string V2DiffusePngPath = V2Dir + "/texture_diffuse.png"; // de-lit toon albedo (URP _BaseMap; NO shirt-recolor — v2 has no shirt)
        public const string V2NormalPngPath = V2Dir + "/texture_normal.png";   // normal map (low strength)

        // AC4 STAGED-ROLLOUT TOGGLE (SPONSOR-LOCKED 2026-07-05). The OLD castaway stays LIVE by DEFAULT; v2
        // (Rodin base) is gated behind this flag UNTIL it passes the Sponsor soak in a shipped build — the OLD
        // base is NOT deleted. Resolved at BOOTSTRAP time: CI re-runs BootstrapProject.Run before EVERY build
        // (ci.yml), so the env var is honored WITHOUT committing a regenerated Boot.unity (which is re-authored
        // each bootstrap anyway). To produce a v2 SOAK build set the env var before the bootstrap+build:
        //   FARHORIZON_CASTAWAY_V2=1  Unity … -executeMethod …BootstrapProject.Run   (then …BuildWindows)
        // Default (env unset) => the old castaway ships UNCHANGED (this PR is behavior-neutral on the live base).
        // When v2 passes soak it is promoted to the default + the old base is removed in a follow-up.
        public const bool UseCastawayV2Default = false;
        public const string CastawayV2EnvVar = "FARHORIZON_CASTAWAY_V2";
        public static bool UseCastawayV2 =>
            System.Environment.GetEnvironmentVariable(CastawayV2EnvVar) == "1" || UseCastawayV2Default;

        // The model prefab MovementCameraScene instantiates IS the with-skin mesh FBX (it carries the skin +
        // rig). Toggle-aware (was a const alias of IdleFbxPath): resolves to the v2 rigged base when
        // UseCastawayV2 is set, else the old Idle.fbx. Callers (MovementCameraScene.BuildModel, diagnostics)
        // read CharacterAssetGen.FbxPath unchanged — the SAME accessor now returns the toggle-selected mesh.
        public static string FbxPath => UseCastawayV2 ? V2RiggedFbxPath : IdleFbxPath;

        // Mixamo clip-take finding (EMPIRICAL, spike Hyper3DSpikeDiag 2026-06-15): BOTH FBX export their
        // single clip as the take name "mixamo.com" (NOT "Idle"/"Walk"). An exact/Contains "Idle"/"Walk"
        // match loops ZERO clips → the T-pose-mid-walk failure class (unity-conventions.md §FBX). So we match
        // the SOURCE take by "mixamo.com" and RENAME it on import to a stable per-FBX name, so the two clips
        // are distinct in the controller.
        public const string SourceTake = "mixamo.com";
        public const string IdleClip = "CastawayIdle"; // renamed-on-import (the with-skin Idle.fbx clip — UNUSED by the controller now; the Idle STATE plays BreathingIdleClip below)
        public const string BreathingIdleClip = "CastawayBreathingIdle"; // renamed-on-import (86cackb3j — the at-rest IDLE state clip, LOOP)
        public const string WalkClip = "CastawayWalk"; // renamed-on-import
        public const string RunClip = "CastawayRun";   // renamed-on-import (86ca9yq34 — the Run clip)
        // TWO jump clips by movement state (86ca9yq3q rework) — renamed-on-import, distinct in the controller.
        public const string JumpIdleClip = "CastawayJumpIdle";       // idle/standing jump (Jump_idle.fbx)
        public const string JumpRunningClip = "CastawayJumpRunning"; // walk/run jump (Jump_running.fbx)
        // The CHOP swing clip — renamed-on-import (the Mixamo take is "mixamo.com"; an exact "Melee" match loops
        // ZERO clips, the T-pose-mid-swing failure class). Distinct in the controller as the Attack state's clip.
        public const string MeleeClip = "CastawayMelee"; // chop swing (Melee_Attack.fbx, NON-looping one-shot)
        // CROUCH + HIT-REACT clip names (86cackb3j) — renamed-on-import from the Mixamo "mixamo.com" take (an exact
        // "Sneak Walk"/"Head Hit"/… match loops/binds ZERO clips, the T-pose class). Distinct per-FBX in the controller.
        public const string CrouchWalkClip = "CastawayCrouchWalk"; // Sneak Walk.fbx (LOOP)
        public const string CrouchIdleClip = "CastawayCrouchIdle"; // Crouching Idle.fbx (LOOP)
        public const string GettingUpClip = "CastawayGettingUp";   // Getting Up.fbx (one-shot recovery)
        public const string PickingUpClip = "CastawayPickingUp";   // Picking Up.fbx (one-shot interaction)
        public const string StunnedClip = "CastawayStunned";       // Stunned.fbx (LOOP — knocked-down hold)
        public const string HeadHitClip = "CastawayHeadHit";       // Head Hit.fbx (one-shot)
        public const string BigStomachHitClip = "CastawayBigStomachHit"; // Big Stomach Hit.fbx (one-shot)
        public const string StomachHitClip = "CastawayStomachHit"; // Stomach Hit.fbx (one-shot)
        public const string RibHitClip = "CastawayRibHit";         // Rib Hit.fbx (one-shot)
        public const string HitToBodyClip = "CastawayHitToBody";   // Hit To Body.fbx (one-shot, default region)

        // The Animator TRIGGER param that fires the one-shot Jump state (86ca9yq3q). CastawayCharacter pulses
        // it on the rising edge of a jump (SetTrigger). The controller routes the trigger to JumpIdle (Moving
        // false) or JumpRunning (Moving true) — clip choice keys off the SAME Moving bool the locomotion graph
        // uses (86ca9yq3q rework). Neither jump state is a Walk<->Run blend-tree child (the blend stays {Idle,
        // Walk, Run} — AC5 OOS protection).
        public const string JumpParam = "Jump";
        // The GROUNDED bool (86ca9yq3q rework — THE floating-bug fix). CastawayCharacter drives it = !IsAirborne
        // each frame. The jump states transition back to the LOCOMOTION blend tree (Grounded && Moving) or to
        // Idle (Grounded && !Moving) the MOMENT the character grounds — so if W is still held on landing,
        // Walk/Run resumes on the same frame instead of stalling in the finished jump pose (which translated
        // while non-locomotion → the "floating" percept the Sponsor reported).
        public const string GroundedParam = "Grounded";

        // The LOCOMOTION PLAYBACK-SPEED MULTIPLIER float (86cackb3j re-soak Part 2 — FOOT-SYNC). The Locomotion
        // blend-tree state's speedParameter reads this, so setting it scales the WALK+RUN clip PLAYBACK RATE to
        // match the actual agent move-speed (the legs cadence tracks translation → feet don't skate). The Sponsor
        // reported the WALK legs too slow vs move-speed (feet skate). CastawayCharacter computes it per-frame =
        // currentSpeed / blendedStrideRef (the clip's natural ground speed), clamped, so a faster body strides
        // faster legs. Default 1 = the authored cadence (an unbound rig / at the reference speed plays unchanged).
        public const string LocoSpeedMulParam = "LocoSpeedMul";

        // The one-shot CHOP TRIGGER (86caa4c5c change-(b)). CastawayCharacter.TriggerChop() pulses it on each
        // landed chop so the Animator plays the Attack (melee swing) state ONCE and returns to locomotion. Mirrors
        // CastawayCharacter.ChopParam (kept in sync). The Attack state is an AnyState→Attack on this trigger so a
        // chop can fire from Idle OR mid-locomotion; it returns to Locomotion (Moving) / Idle (!Moving) on exit so
        // a held-movement chop resumes walking on the clip's end (the no-stall-locomotion lesson, jump rework).
        public const string ChopParam = "Chop";
        // The Attack-state SPEED MULTIPLIER float (86caa4c5c AC1 — tool-use speed). The Attack state's
        // speedParameter reads this, so setting it scales the melee clip's PLAYBACK RATE (a fast/slow chop). The
        // settings-panel `tool-use speed` row drives CastawayCharacter.chopSpeed, which sets this param live (V1).
        // 1x = the authored clip duration. Default 1 so an unbound rig plays the clip at its authored speed.
        public const string ChopSpeedParam = "ChopSpeed";

        // ===== CROUCH + HIT-REACT params (86cackb3j) — mirror CastawayCharacter.* (kept in sync; the runtime can't
        // reference the editor asmdef, and a ControllerParamNamesMatch test pins the duplication). The GAMEPLAY
        // systems that DRIVE these are SEPARATE tickets (this ticket's OOS); the controller only WIRES the clips to them.
        public const string CrouchParam = "Crouch";       // bool — upright<->crouch lane select
        public const string HitParam = "Hit";             // trigger — fire a body-region hit-react
        public const string HitRegionParam = "HitRegion"; // int — which region clip (see HitRegion* below)
        public const string StunnedParam = "Stunned";     // bool — knocked-down hold (loops) -> Getting Up on release
        public const string PickUpParam = "PickUp";       // trigger — the one-shot ground-pick interaction
        // HitRegion int values (mirror CastawayCharacter.HitRegion*). 0 = the default Hit To Body reaction.
        public const int HitRegionBody = 0;
        public const int HitRegionHead = 1;
        public const int HitRegionBigStomach = 2;
        public const int HitRegionStomach = 3;
        public const int HitRegionRib = 4;

        // The 1D Walk<->Run blend-tree thresholds on the Speed param (86ca9yq34). Idle@0, Walk@WalkBlendSpeed,
        // Run@RunBlendSpeed — so the planar agent speed WasdMovement commands (moveSpeed walking, runSpeed
        // sprinting) lands the blend in the Walk band or the Run band, blending smoothly between. These mirror
        // MovementCameraScene's WASD walk/run speeds (kept in sync so the Speed param maps onto the right clip).
        public const float IdleBlendSpeed = 0f;
        public const float WalkBlendSpeed = 5.5f;  // == MovementCameraScene WASD walk speed (agent.speed)
        public const float RunBlendSpeed = 9.5f;   // == WasdMovement.runSpeed

        // The de-lit material binds texture_diffuse as the toon albedo. Asserted present so a missing-texture
        // grey regression (the flat toon look silently lost) fails the build/test.
        public const string DiffuseTextureName = "texture_diffuse";

        // Height normalization — the FBX imports at ~2.18u intrinsic; normalize the IMPORT directly to the
        // on-screen height (1.8u, the agent height) so the avatar root sits UNSCALED (scale 1) — a SINGLE
        // scale chain (the FBX globalScale only), matching the spike's clean single-instantiate. Wrapping the
        // 100×-internal-node FBX in a SECOND scaled ancestor (the old 1.8× root) made the Humanoid Animator's
        // retarget displace the mesh off-spawn at runtime (86ca8rdkp / rig-trace). useFileUnits/useFileScale
        // stay at import DEFAULTS (the spike's known-clean values). Re-derived from live bounds (self-corrects).
        public const float TargetImportHeightU = 1.0f;

        // ---- IDENTITY RECOLOR (86ca8rdkp AC4) — warm the mustard-yellow generated shirt toward TAN.
        // The shirt is the big saturated-yellow region of texture_diffuse (probe: ~12.9% of the atlas, mean
        // hue ~44°, sat ~0.92). The concept (concept_01_front.png) reads a warm tan/wheat shirt. We REMAP the
        // saturated-yellow band's hue toward warm-tan + tone its saturation, PRESERVING each pixel's VALUE
        // (HSV V) so the baked toon light→dark gradient survives (a flat tint would wipe it — the per-material
        // trap). Reasonable pass, Sponsor-judged from the build; NOT a grind. ----
        // The yellow shirt SOURCE band (HSV). Pixels inside this band are remapped; everything else (skin,
        // hair, shorts, face) is untouched. Derived empirically (the yellow shirt mean hue ~44°, sat ~0.92).
        public const float ShirtHueMinDeg = 38f;   // lower yellow edge (deg)
        public const float ShirtHueMaxDeg = 75f;   // upper yellow edge (deg) — excludes orange skin (~25-32°)
        public const float ShirtSatMin = 0.45f;    // only SATURATED yellow (skin/hair are lower-sat warm browns)
        public const float ShirtValMin = 0.40f;    // only reasonably-lit yellow (dark shadow stays)
        // The TARGET: warm tan/wheat. Hue shifted from yellow (~44°) down to warm-tan (~34°), saturation
        // pulled toward a muted tan. VALUE is preserved per-pixel (gradient kept). These are the post-remap
        // hue + a saturation SCALE applied to the in-band pixels.
        public const float ShirtTargetHueDeg = 34f; // warm tan/wheat (was ~44° mustard yellow)
        public const float ShirtSatScale = 0.62f;    // tone the saturation toward muted tan (keeps warmth)

        public static void PrepareCharacter()
        {
            // CASTAWAY v2 (86cajwp23) — ALWAYS configure the v2 base FBX importer (Generic + CreateFromThisModel
            // + height-normalize), even when the toggle is OFF, so its .meta is deterministic + the EditMode
            // import guards (CastawayV2BaseTests) always have a real import to assert. The WIRING (which mesh →
            // Boot.unity, which textures → CastawayMat, whether RecolorShirtToTan runs, which axe seat) is what
            // the UseCastawayV2 toggle gates below — importing the base is cheap + side-effect-free on the old path.
            ConfigureV2BaseFbx();
            ConfigureIdleFbx();   // Generic CreateFromThisModel + loop+rename Idle + height-normalize (the WITH-skin mesh/rig)
            // BREATHING IDLE (86cackb3j re-soak) — the at-rest clip the Idle STATE plays. WITHOUT-skin Generic,
            // binds by transform path onto Idle's mesh (the Walk/Run idiom). LOOP (a sustained breathing cycle).
            ConfigureGenericClipFbx(BreathingIdleFbxPath, BreathingIdleClip, loop: true);
            ConfigureWalkFbx();   // Generic CreateFromThisModel + loop+rename Walk (binds by transform path)
            ConfigureRunFbx();    // Generic CreateFromThisModel + loop+rename Run (binds by transform path; 86ca9yq34)
            // TWO jump clips by movement state (86ca9yq3q rework): idle/standing + walk/run, both NON-looping one-shots.
            ConfigureJumpFbx(JumpIdleFbxPath, JumpIdleClip);
            ConfigureJumpFbx(JumpRunningFbxPath, JumpRunningClip);
            // CHOP swing clip (86caa4c5c change-(b)) — the Mixamo melee one-shot, Generic + NON-looping; binds by
            // transform path onto Idle's mesh (same mixamorig rig). Replaces the procedural ChopPoseDriver swing.
            ConfigureMeleeFbx();
            // CROUCH + HIT-REACT clips (86cackb3j) — all WITHOUT-skin Generic, bind by transform path onto Idle's
            // mesh (the proven Walk/Run/jump idiom). LOOP the sustained states (crouch idle/move, stunned hold);
            // ONE-SHOT the reactions/recovery/interaction. ConfigureGenericClipFbx is the parameterised import
            // (same body as ConfigureWalkFbx/ConfigureJumpFbx, differing only in loop + rename target).
            ConfigureGenericClipFbx(SneakWalkFbxPath, CrouchWalkClip, loop: true);
            ConfigureGenericClipFbx(CrouchIdleFbxPath, CrouchIdleClip, loop: true);
            // GAIT-CURVE SMOOTHING (86caa3kur / #197 — the toe-pop fix). Generate an editable smoothed .anim from
            // the raw Sneak Walk clip with ONLY the mid-cycle foot/toe quaternion spike (LeftToeBase ~80deg/frame @
            // normT~=0.907, live-probe confirmed) surgically slerp-smoothed; every other curve copied verbatim.
            // BuildAnimatorController points CrouchWalk at this .anim. Runs AFTER the FBX import (it reads the imported
            // raw clip) and BEFORE BuildAnimatorController (which binds the smoothed clip). Committed .anim ships it.
            {
                var gaitSb = new System.Text.StringBuilder();
                SneakGaitCurveFix.Generate(gaitSb);
                Debug.Log(gaitSb.ToString());
            }
            ConfigureGenericClipFbx(StunnedFbxPath, StunnedClip, loop: true);   // knocked-down HOLD loops until recovery
            ConfigureGenericClipFbx(GettingUpFbxPath, GettingUpClip, loop: false);
            ConfigureGenericClipFbx(PickingUpFbxPath, PickingUpClip, loop: false);
            ConfigureGenericClipFbx(HeadHitFbxPath, HeadHitClip, loop: false);
            ConfigureGenericClipFbx(BigStomachHitFbxPath, BigStomachHitClip, loop: false);
            ConfigureGenericClipFbx(StomachHitFbxPath, StomachHitClip, loop: false);
            ConfigureGenericClipFbx(RibHitFbxPath, RibHitClip, loop: false);
            ConfigureGenericClipFbx(HitToBodyFbxPath, HitToBodyClip, loop: false);
            // IDENTITY RECOLOR (86ca8rdkp) — REPRODUCIBLE-FROM-CODE (the project invariant: CI re-runs
            // bootstrap). Repaints the shirt region of texture_diffuse, idempotently. Runs AFTER the FBX
            // import (the material binds the diffuse PNG; repainting it does not need the FBX re-imported).
            // AC3 (86cajwp23) — RETIRED for v2: the Rodin base has NO shirt, so the yellow→tan shirt remap is
            // OLD-castaway-only. Gated OFF when UseCastawayV2 (which also swaps BuildMaterial to v2's already-
            // de-lit textures). When v2 is promoted to the default, RecolorShirtToTan is deleted outright.
            if (!UseCastawayV2)
                RecolorShirtToTan();
            BuildMaterial();      // flat de-lit URP/Lit from texture_diffuse (v2's de-lit albedo, or the old recolored shirt)
            BuildAnimatorController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CharacterAssetGen] Hyper3D castaway prepared: " + IdleFbxPath + " + " + WalkFbxPath +
                      " + " + MaterialPath + " + " + ControllerPath);
        }

        // CASTAWAY v2 base (86cajwp23) — the Rodin rigged mesh FBX (WITH skin: mesh + mixamorig skeleton +
        // an unused T-pose take). Import config is IDENTICAL to ConfigureIdleFbx's mesh path: GENERIC +
        // CreateFromThisModel (its OWN avatar from its OWN mixamorig skeleton — the anti-Humanoid recipe;
        // 86ca8rdkp Humanoid cone-explodes the mesh at runtime) + height-normalize to TargetImportHeightU.
        // The 18 existing WITHOUT-skin clips bind onto THIS mesh by transform path (matching mixamorig bone
        // names) with NO retarget — exactly how they already bind onto the old Idle.fbx. importAnimation=false:
        // v2's own T-pose take is unused (the controller drives BreathingIdle/Walk/Run/… from the clip FBX),
        // so we import the mesh + rig ONLY (no stray clip). materialImportMode=None: the shared de-lit
        // CastawayMat is authored by BuildMaterial + bound editor-time by MovementCameraScene (no stray FBX mat).
        // Configured on EVERY bootstrap (toggle-independent) so the .meta is deterministic + the EditMode import
        // guards always assert a real import; only the SCENE WIRING is gated on UseCastawayV2.
        private static void ConfigureV2BaseFbx()
        {
            var importer = AssetImporter.GetAtPath(V2RiggedFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[CharacterAssetGen] castaway v2 base FBX not found at " + V2RiggedFbxPath +
                               " — v2 integration (86cajwp23) cannot import; the old castaway is unaffected");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic; // NOT Humanoid (86ca8rdkp runtime-explosion)
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = false; // mesh+rig only — clips come from the WITHOUT-skin clip FBX by transform path
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            // HEIGHT NORMALIZE the intrinsic import to ~1u (v2 imports at ~1.889m; TargetImportHeightU=1.0).
            // Self-correcting: reads the current globalScale, measures at that scale, re-scales to hit target —
            // convergent regardless of the committed .meta's starting globalScale (the old Idle path idiom).
            float measured = MeasureHeight(V2RiggedFbxPath);
            if (measured > 0.01f)
            {
                float factor = importer.globalScale * (TargetImportHeightU / measured);
                importer.globalScale = factor;
                Debug.Log($"[CharacterAssetGen] v2 base height-normalize: measured={measured:F3}u -> globalScale={factor:F5} " +
                          $"(target {TargetImportHeightU}u)");
            }
            else
            {
                Debug.LogWarning("[CharacterAssetGen] could not measure v2 base height — skipping normalize");
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            var avatar = LoadAvatar(V2RiggedFbxPath);
            bool ok = avatar != null && avatar.isValid;
            if (!ok)
                Debug.LogError("[CharacterAssetGen] castaway v2 base did NOT produce a VALID avatar (avatar=" +
                               (avatar != null) + " valid=" + (avatar != null && avatar.isValid) +
                               ") — clips will not bind (the T-pose class)");
            else
                Debug.Log("[CharacterAssetGen] castaway v2 base reimported: rig=Generic CreateFromThisModel, avatar valid" +
                          (UseCastawayV2 ? " [WIRED — UseCastawayV2 ON]" : " [imported only — toggle OFF, old castaway live]"));
        }

        // Idle.fbx carries the skin (mesh+rig) + the Idle clip. Humanoid rig, avatar created from THIS model
        // (CreateFromThisModel) — the Mixamo Standard-65 skeleton maps to the Humanoid avatar so the clips
        // bind. Loop the Idle clip; height-normalize the mesh to ~1u.
        private static void ConfigureIdleFbx()
        {
            var importer = AssetImporter.GetAtPath(IdleFbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] Idle.fbx not found at " + IdleFbxPath); return; }

            // GENERIC rig (86ca8rdkp — the runtime-explosion fix). The Humanoid retarget reconstructed the
            // pose in MUSCLE space and, combined with this Mixamo FBX's internal 100× mesh node, EXPLODED the
            // skinned mesh into a cone/stretched-arm at runtime in the production scene (verify + gameplay
            // captures; the spike's Humanoid mode only looked clean because its capture camera FOLLOWED the
            // displaced mesh). GENERIC binds the clip by TRANSFORM PATH onto the FBX's own mixamorig skeleton
            // — NO muscle-space retarget — which the spike ALSO proved animates clean (captures 04/05) and
            // which the live chibi used. Both FBX CreateFromThisModel (own avatar from own identical skeleton);
            // the Walk clip binds by path onto the shared bone names — no CopyFromOther retarget needed.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            // HEIGHT NORMALIZATION before the clip reimport so one SaveAndReimport applies scale + loop flags.
            float measured = MeasureHeight(IdleFbxPath);
            if (measured > 0.01f)
            {
                float factor = importer.globalScale * (TargetImportHeightU / measured);
                importer.globalScale = factor;
                Debug.Log($"[CharacterAssetGen] Idle height-normalize: measured={measured:F3}u -> globalScale={factor:F5} " +
                          $"(target {TargetImportHeightU}u)");
            }
            else
            {
                Debug.LogWarning("[CharacterAssetGen] could not measure Idle height — skipping normalize");
            }

            importer.clipAnimations = LoopAndRename(importer, IdleClip, out int looped);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] Idle.fbx reimported: rig=Humanoid CreateFromThisModel, " +
                      $"looped+renamed {looped} clip(s) -> {IdleClip}");

            var avatar = LoadAvatar(IdleFbxPath);
            bool ok = avatar != null && avatar.isValid;
            if (!ok)
                Debug.LogError("[CharacterAssetGen] Idle.fbx did not produce a VALID avatar. avatar=" +
                               (avatar != null) + " valid=" + (avatar != null && avatar.isValid) +
                               " — clips will not bind (the T-pose-mid-walk class)");
            else
                Debug.Log("[CharacterAssetGen] Idle.fbx Generic avatar valid");
        }

        // Walking.fbx is the Walk clip WITHOUT skin. Humanoid rig, avatar COPIED FROM Idle's avatar
        // (CopyFromOther) so the Walk clip RETARGETS onto Idle's mesh (the Mixamo Standard-65 skeletons
        // match). Loop the Walk clip. No skin → no material import (avoid stray materials).
        private static void ConfigureWalkFbx()
        {
            var importer = AssetImporter.GetAtPath(WalkFbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] Walking.fbx not found at " + WalkFbxPath); return; }

            // GENERIC: Walking.fbx creates its OWN avatar from its own (identical mixamorig) skeleton; the
            // Generic Walk clip binds by TRANSFORM PATH onto Idle's mesh (same bone names) — no Humanoid
            // muscle-space retarget (the runtime-explosion cause). The spike proved this path clean (capture
            // 05). No CopyFromOther / sourceAvatar needed.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            importer.clipAnimations = LoopAndRename(importer, WalkClip, out int looped);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] Walking.fbx reimported: rig=Generic CreateFromThisModel, " +
                      $"looped+renamed {looped} clip(s) -> {WalkClip}");
        }

        // Running.fbx is the RUN clip WITHOUT skin (86ca9yq34). IDENTICAL import config to Walking.fbx: GENERIC
        // rig, avatar created from its OWN (identical mixamorig) skeleton (CreateFromThisModel); the Generic Run
        // clip binds by TRANSFORM PATH onto Idle's mesh (same bone names) — NO Humanoid muscle-space retarget
        // (the runtime-explosion cause, 86ca8rdkp). Loop the Run clip. No skin → no material import (avoid stray
        // materials). The Mixamo take is "mixamo.com" → renamed to CastawayRun on import (the same clip-take
        // finding the Idle/Walk imports honor — an exact "Run" match loops ZERO clips, the T-pose-mid-walk class).
        private static void ConfigureRunFbx()
        {
            var importer = AssetImporter.GetAtPath(RunFbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] Running.fbx not found at " + RunFbxPath); return; }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            importer.clipAnimations = LoopAndRename(importer, RunClip, out int looped);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] Running.fbx reimported: rig=Generic CreateFromThisModel, " +
                      $"looped+renamed {looped} clip(s) -> {RunClip}");
        }

        // Jump.fbx is the JUMP clip WITHOUT skin (86ca9yq3q — In-Place, Without-Skin per the brief). IDENTICAL
        // import config to Walking/Running.fbx (GENERIC rig, avatar from its OWN identical mixamorig skeleton —
        // binds by TRANSFORM PATH onto Idle's mesh, NO Humanoid muscle retarget = the 86ca8rdkp runtime-explosion
        // cause) EXCEPT the clip is NON-LOOPING — a jump is a ONE-SHOT (crouch→push→arc→land), not a cycle, so it
        // plays once and the controller transitions back to Idle/Locomotion on exit. The Mixamo take is
        // "mixamo.com" → renamed to CastawayJump (the same clip-take finding the Idle/Walk/Run imports honor — an
        // exact "Jump" match loops ZERO clips, the T-pose-mid-walk class).
        // (86ca9yq3q rework) Parameterised on the FBX path + rename target — used for BOTH Jump_idle.fbx
        // (idle/standing jump) and Jump_running.fbx (walk/run jump). Same import config for each.
        private static void ConfigureJumpFbx(string fbxPath, string clipName)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] jump FBX not found at " + fbxPath); return; }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            importer.clipAnimations = RenameNonLooping(importer, clipName, out int renamed);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] {fbxPath} reimported: rig=Generic CreateFromThisModel, " +
                      $"renamed {renamed} NON-looping clip(s) -> {clipName}");
        }

        // Build a flat de-lit URP/Lit material from texture_diffuse: _BaseMap = diffuse, smoothness ~0, no
        // metallic, so the baked toon-shaded albedo reads toon (the project's URP/Lit toon idiom). Normal map
        // at a LOW strength (the baked albedo already carries shading; a strong normal would double up).
        // MovementCameraScene binds this onto the avatar's SkinnedMeshRenderer(s) editor-time.
        private static void BuildMaterial()
        {
            // AC1 (86cajwp23) — v2 binds its OWN de-lit diffuse + normal (URP toon albedo, no shirt-recolor);
            // the old path binds the recolored old texture_diffuse. Same material path (CastawayMat.mat) + same
            // toon idiom either way, so MovementCameraScene binds it unchanged; only the source textures switch.
            string diffusePath = UseCastawayV2 ? V2DiffusePngPath : DiffusePngPath;
            string normalPath = UseCastawayV2 ? V2NormalPngPath : NormalPngPath;

            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuse == null) { Debug.LogError("[CharacterAssetGen] texture_diffuse not found at " + diffusePath); return; }

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) { Debug.LogError("[CharacterAssetGen] URP/Lit shader not found"); return; }

            var mat = new Material(litShader) { name = "CastawayMat" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.03f); // matte/toon — no gloss
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normalTex != null)
            {
                var ni = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap)
                {
                    ni.textureType = TextureImporterType.NormalMap;
                    ni.SaveAndReimport();
                }
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap", normalTex);
                    if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.4f);
                }
            }

            EnsureShaderAlwaysIncluded(litShader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterAssetGen] de-lit URP/Lit material built from texture_diffuse -> " + MaterialPath);
        }

        // IDENTITY RECOLOR (86ca8rdkp AC4) — warm the saturated-yellow SHIRT region of texture_diffuse toward
        // TAN, LUMA-PRESERVING (HSV value kept per-pixel so the toon gradient survives — NOT a flat material
        // tint). Reproducible-from-code + IDEMPOTENT: the remap is keyed on the SOURCE yellow band (a hue/sat/
        // value window), and a re-run sees the ALREADY-TANNED pixels OUTSIDE that band (tan hue ~34° is below
        // ShirtHueMin 38°), so a bootstrap re-run does NOT re-shift them — it converges. (Defensive: even if a
        // re-run caught an edge pixel still in-band, the absolute target hue makes it converge, not drift.)
        //
        // AC3 (86cajwp23) — OLD-CASTAWAY-ONLY, RETIRED for v2. The Rodin base (v2) has NO shirt, and its albedo
        // is already de-lit from Rodin's De-light pass, so v2 needs no recolor at all. PrepareCharacter no longer
        // calls this when UseCastawayV2 (see the gated call). It is kept (not deleted) ONLY because the old base
        // stays live behind the toggle (AC4); when v2 is promoted to the default this method + its Shirt* constants
        // are deleted outright.
        public static void RecolorShirtToTan()
        {
            string path = DiffusePngPath;
            if (!File.Exists(path))
            {
                Debug.LogError("[CharacterAssetGen] diffuse PNG not found at " + path + " — cannot recolor");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Debug.LogError("[CharacterAssetGen] diffuse PNG failed to decode — recolor aborted");
                Object.DestroyImmediate(tex);
                return;
            }

            var pixels = tex.GetPixels();
            int remapped = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float hueDeg = h * 360f;
                // Only the SATURATED YELLOW SHIRT band — skin (orange ~25-32°, lower sat), hair/shorts
                // (brown, lower value/sat), and the face are all outside this window and stay untouched.
                bool inShirt = hueDeg >= ShirtHueMinDeg && hueDeg <= ShirtHueMaxDeg &&
                               s >= ShirtSatMin && v >= ShirtValMin;
                if (!inShirt) continue;

                // HUE → warm tan; SAT toned; VALUE PRESERVED (the toon gradient lives in V). Absolute target
                // hue (not a relative shift) so the remap is idempotent + converges on a re-run.
                float newH = ShirtTargetHueDeg / 360f;
                float newS = Mathf.Clamp01(s * ShirtSatScale);
                Color nc = Color.HSVToRGB(newH, newS, v);
                nc.a = c.a;
                pixels[i] = nc;
                remapped++;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            float frac = pixels.Length > 0 ? (float)remapped / pixels.Length : 0f;
            Debug.Log($"[CharacterAssetGen] shirt recolored yellow->tan (luma-preserving HSV remap, {remapped} px " +
                      $"= {frac:P1} of the atlas) — reproducible-from-code, idempotent");
            if (remapped == 0)
                Debug.LogWarning("[CharacterAssetGen] recolor remapped ZERO pixels — the shirt band may have " +
                                 "already been tanned (idempotent re-run) OR the source band is mis-tuned");
        }

        // ----- helpers (mirror the spike's proven Hyper3DSpikeGen idioms) -----

        // Match the Mixamo source take ("mixamo.com"), set it to loop, and RENAME it to a stable per-FBX name
        // so the controller binds distinct Idle/Walk clips. Each FBX carries exactly one take; we loop+rename
        // every non-preview clip whose name/take matches the source take. Guarded by the looped>0 assert.
        private static ModelImporterClipAnimation[] LoopAndRename(ModelImporter importer, string newName, out int looped)
        {
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            var edited = new List<ModelImporterClipAnimation>();
            looped = 0;
            foreach (var c in clips)
            {
                var cc = c;
                if (cc.name.Contains(SourceTake) || cc.takeName.Contains(SourceTake))
                {
                    cc.name = newName;
                    cc.loopTime = true;
                    cc.loop = true;
                    // LOOP-POSE blend (86caa3kur — #197 crouch-jerk fix). The C# property `loopPose` serializes
                    // to the .meta field `loopBlend` (Unity API↔YAML naming differs). With loopBlend=0 the pose
                    // SNAPS at the frame-N→frame-0 wrap once per clip cycle — for Sneak Walk that's once per
                    // ~28-frame gait cycle = the Sponsor's "left, right, JERK" (LIVE-CONFIRMED via the v4 F2/F3
                    // isolation build; foot-sync + speed both exonerated). loopPose=true blends the cycle ends so
                    // the pose wraps seamlessly. INVISIBLE to a normalizedTime trace: a clean TIME-wrap is not a
                    // clean POSE-wrap. The orientation/XZ/Y loop-blend fields (loopBlendOrientation:1,
                    // loopBlendPositionXZ:1, loopBlendPositionY:0) are ALREADY at the desired values via the
                    // lockRoot*/keepOriginal* lines below — left UNCHANGED (do not touch the spike's pinned
                    // float-fix values). Net improvement / low risk across ALL looped clips (Idle/Walk/Run/
                    // CrouchIdle/CrouchWalk/BreathingIdle/Stunned), all of which share loopBlend:0 today.
                    cc.loopPose = true;
                    // ROOT-TRANSFORM settings matching the spike's known-clean import EXACTLY (the spike's
                    // shipped meta: keepOriginalOrientation=0, keepOriginalPositionY=1, keepOriginalPositionXZ=0).
                    // In-place loco (NavMeshAgent owns world position; applyRootMotion=false).
                    //
                    // NOTE (86ca8rdkp attempt-9 — diagnose-via-trace): the WALK-clip float is NOT fixable here.
                    // These root-transform flags govern ROOT-MOTION EXTRACTION; with applyRootMotion=false the
                    // baked skinned mesh is sampled IN-PLACE from the raw bone curves, so lockRootHeightY /
                    // heightFromFeet / keepOriginalPositionY do NOT move the rendered feet (PROVEN: re-importing
                    // the Walk clip with lockRootHeightY=true + keepOriginalPositionY=false + heightFromFeet=true
                    // left the scale-immune baked WALK sole at +0.63..+0.69 — unchanged from the +0.66 baseline).
                    // The Mixamo Walk clip's HIPS are authored ~0.66u higher than Idle's; that lift lives in the
                    // BONE pose, not the root node. The fix is at the MODEL level (CastawayCharacter grounds the
                    // scale-immune rendered sole), not in these import flags — kept at the spike's clean values.
                    cc.lockRootRotation = true;
                    cc.keepOriginalOrientation = false;
                    cc.lockRootPositionXZ = true;
                    cc.keepOriginalPositionXZ = false;
                    cc.lockRootHeightY = false;
                    cc.keepOriginalPositionY = true;
                    cc.heightFromFeet = false;
                    looped++;
                }
                edited.Add(cc);
            }
            if (looped == 0)
                Debug.LogError($"[CharacterAssetGen] no clip matched source take '{SourceTake}' to loop+rename " +
                               $"to '{newName}' — clip will freeze mid-cycle (T-pose risk). Re-run CharacterDiagnoseTrace.");
            return edited.ToArray();
        }

        // Match the Mixamo source take ("mixamo.com") + RENAME it to a stable name, but DO NOT loop it — the
        // Jump clip is a ONE-SHOT (86ca9yq3q), so loopTime=false so the controller's Jump state plays the arc
        // once and transitions back on exit (a looped jump would replay the push-off forever). Same root-transform
        // settings as LoopAndRename (in-place loco — NavMeshAgent owns world XZ; applyRootMotion=false). Guarded
        // by the renamed>0 assert (zero matched = the T-pose-mid-jump failure class, same as LoopAndRename).
        private static ModelImporterClipAnimation[] RenameNonLooping(ModelImporter importer, string newName, out int renamed)
        {
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            var edited = new List<ModelImporterClipAnimation>();
            renamed = 0;
            foreach (var c in clips)
            {
                var cc = c;
                if (cc.name.Contains(SourceTake) || cc.takeName.Contains(SourceTake))
                {
                    cc.name = newName;
                    cc.loopTime = false; // ONE-SHOT jump — play the arc once, transition back on exit
                    cc.loop = false;
                    cc.lockRootRotation = true;
                    cc.keepOriginalOrientation = false;
                    cc.lockRootPositionXZ = true;
                    cc.keepOriginalPositionXZ = false;
                    cc.lockRootHeightY = false;
                    cc.keepOriginalPositionY = true;
                    cc.heightFromFeet = false;
                    renamed++;
                }
                edited.Add(cc);
            }
            if (renamed == 0)
                Debug.LogError($"[CharacterAssetGen] no clip matched source take '{SourceTake}' to rename to " +
                               $"'{newName}' — the Jump state will be empty (T-pose-mid-jump risk). Re-run CharacterDiagnoseTrace.");
            return edited.ToArray();
        }

        // Melee_Attack.fbx is the CHOP swing clip (86caa4c5c change-(b) — the Sponsor's Mixamo "Standing Melee
        // Attack Downward"). It ships WITH skin (mesh+rig+clip), but we use ONLY its CLIP: like the jump one-shots,
        // it imports GENERIC + CreateFromThisModel (its own identical mixamorig skeleton) so the Generic Melee clip
        // binds by TRANSFORM PATH onto Idle's mesh (same bone names) — NO Humanoid muscle retarget (the 86ca8rdkp
        // runtime-explosion cause). materialImportMode=None so the with-skin FBX does NOT spill a stray material
        // into the project (we never render its mesh — only the clip drives Idle's avatar). The clip is NON-LOOPING
        // (RenameNonLooping — a chop is a ONE-SHOT strike, like the jump): it plays once and the Attack state
        // transitions back on exit. The Mixamo take is "mixamo.com" → renamed to CastawayMelee (the clip-take
        // finding the other imports honor — an exact "Melee" match loops ZERO clips, the T-pose-mid-swing class).
        private static void ConfigureMeleeFbx()
        {
            var importer = AssetImporter.GetAtPath(MeleeFbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] Melee_Attack.fbx not found at " + MeleeFbxPath); return; }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // with-skin → suppress stray materials
            importer.useFileUnits = true;
            importer.useFileScale = true;

            importer.clipAnimations = RenameNonLooping(importer, MeleeClip, out int renamed);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] Melee_Attack.fbx reimported: rig=Generic CreateFromThisModel, " +
                      $"renamed {renamed} NON-looping clip(s) -> {MeleeClip} (the chop swing)");
        }

        // CROUCH + HIT-REACT clip import (86cackb3j) — the parameterised WITHOUT-skin Generic import shared by the
        // crouch (Sneak Walk / Crouching Idle), stun (Stunned / Getting Up), pick-up (Picking Up), and the five
        // hit-react clips. IDENTICAL import config to ConfigureWalkFbx/ConfigureJumpFbx (GENERIC rig, avatar from its
        // OWN identical mixamorig skeleton — binds by TRANSFORM PATH onto Idle's mesh, NO Humanoid muscle retarget =
        // the 86ca8rdkp runtime-explosion cause), differing only in the LOOP flag + rename target:
        //   loop=true  → LoopAndRename   (crouch idle/move + the stunned HOLD — sustained cyclic states)
        //   loop=false → RenameNonLooping (hit reactions / Getting Up recovery / Picking Up — ONE-SHOTs)
        // materialImportMode=None so a with-skin source (none here — all are without-skin) never spills a stray
        // material; harmless for without-skin. The Mixamo take is "mixamo.com" → renamed to clipName on import (the
        // clip-take finding the Idle/Walk/Run/Jump/Melee imports honor — an exact name match loops ZERO clips).
        private static void ConfigureGenericClipFbx(string fbxPath, string clipName, bool loop)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) { Debug.LogError("[CharacterAssetGen] clip FBX not found at " + fbxPath); return; }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.useFileUnits = true;
            importer.useFileScale = true;

            int n;
            importer.clipAnimations = loop
                ? LoopAndRename(importer, clipName, out n)
                : RenameNonLooping(importer, clipName, out n);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[CharacterAssetGen] {fbxPath} reimported: rig=Generic CreateFromThisModel, " +
                      $"{(loop ? "looped" : "renamed NON-looping")} {n} clip(s) -> {clipName}");
        }

        private static Avatar LoadAvatar(string fbxPath)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is Avatar a) return a;
            return null;
        }

        private static AnimationClip FindClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip clip && clip.name.Contains(token) && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }

        private static float MeasureHeight(string fbxPath)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
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

        // Build the locomotion controller (86ca9yq34 — Walk<->Run blend on Shift). The state machine is:
        //   Idle  <--Moving bool-->  Locomotion (a 1D BLEND TREE on the Speed float: Walk@WalkBlendSpeed,
        //                                        Run@RunBlendSpeed — smooth Walk<->Run blend, AC1).
        // WHY A BLEND TREE (not a Walk state + a Run state with transitions): a 1D blend tree interpolates the
        // Walk and Run clips CONTINUOUSLY on the Speed param, so accelerating from walk to run (Shift held) and
        // back is a smooth crossfade with NO transition pops or exit-time stalls — exactly AC1's "smooth
        // Walk<->Run blend". CastawayCharacter feeds Speed = the planar agent speed every frame (moveSpeed
        // walking, runSpeed sprinting), so the blend reads the right clip by construction.
        // The Idle<->Locomotion split stays on the Moving bool (keeps IsWalking + the existing Idle transition
        // behavior; the Speed=0 floor of the blend tree is Idle too, but the explicit bool transition keeps the
        // at-rest Idle crisp + back-compatible with the prior Moving-bool contract).
        private static void BuildAnimatorController()
        {
            AnimationClip idle = FindClip(IdleFbxPath, IdleClip);
            // BREATHING IDLE (86cackb3j re-soak) — the at-rest clip the Idle state + the blend-tree Idle floor play
            // ("calm but clearly alive"). Falls back to the static Idle clip ONLY if the breathing FBX is missing
            // (defensive — the controller never silently ships a T-pose; a missing breathing clip degrades to the
            // prior static idle rather than a broken state).
            AnimationClip breathingIdle = FindClip(BreathingIdleFbxPath, BreathingIdleClip);
            AnimationClip restIdle = breathingIdle != null ? breathingIdle : idle;
            AnimationClip walk = FindClip(WalkFbxPath, WalkClip);
            AnimationClip run = FindClip(RunFbxPath, RunClip);
            AnimationClip jumpIdle = FindClip(JumpIdleFbxPath, JumpIdleClip);
            AnimationClip jumpRunning = FindClip(JumpRunningFbxPath, JumpRunningClip);
            AnimationClip melee = FindClip(MeleeFbxPath, MeleeClip); // the chop swing (86caa4c5c change-(b))
            // CROUCH + HIT-REACT clips (86cackb3j).
            // CrouchWalk binds the SMOOTHED .anim (86caa3kur / #197 — the toe-pop fix), falling back to the raw FBX
            // clip only if the smoothed asset is missing (defensive — never silently ship a T-pose; a missing
            // smoothed clip degrades to the raw clip's known-visible-pop rather than a broken state).
            AnimationClip crouchWalkSmoothed = AssetDatabase.LoadAssetAtPath<AnimationClip>(SneakGaitCurveFix.SmoothedClipPath);
            AnimationClip crouchWalk = crouchWalkSmoothed != null ? crouchWalkSmoothed : FindClip(SneakWalkFbxPath, CrouchWalkClip);
            if (crouchWalkSmoothed == null)
                Debug.LogWarning("[CharacterAssetGen] smoothed CrouchWalk .anim NOT found at " +
                                 SneakGaitCurveFix.SmoothedClipPath + " — falling back to the RAW Sneak Walk clip " +
                                 "(the #197 toe-pop will be VISIBLE). Re-run PrepareCharacter to regenerate it.");
            AnimationClip crouchIdle = FindClip(CrouchIdleFbxPath, CrouchIdleClip);
            AnimationClip stunned = FindClip(StunnedFbxPath, StunnedClip);
            AnimationClip gettingUp = FindClip(GettingUpFbxPath, GettingUpClip);
            AnimationClip pickingUp = FindClip(PickingUpFbxPath, PickingUpClip);
            AnimationClip headHit = FindClip(HeadHitFbxPath, HeadHitClip);
            AnimationClip bigStomachHit = FindClip(BigStomachHitFbxPath, BigStomachHitClip);
            AnimationClip stomachHit = FindClip(StomachHitFbxPath, StomachHitClip);
            AnimationClip ribHit = FindClip(RibHitFbxPath, RibHitClip);
            AnimationClip hitToBody = FindClip(HitToBodyFbxPath, HitToBodyClip);
            if (idle == null || walk == null || run == null || jumpIdle == null || jumpRunning == null || melee == null)
            {
                Debug.LogError($"[CharacterAssetGen] missing clips (idle={idle != null}, walk={walk != null}, " +
                               $"run={run != null}, jumpIdle={jumpIdle != null}, jumpRunning={jumpRunning != null}, " +
                               $"melee={melee != null}); controller not built");
                return;
            }
            // BREATHING IDLE absence is a LOUD warning (not fatal — restIdle falls back to the static idle), so a
            // dropped breathing FBX doesn't silently ship a frozen T-pose AND doesn't block the locomotion fix.
            if (breathingIdle == null)
                Debug.LogWarning("[CharacterAssetGen] Breathing Idle clip NOT found at " + BreathingIdleFbxPath +
                                 " — the Idle state will FALL BACK to the static Idle clip (the 'too static' soak " +
                                 "complaint returns). Verify the FBX is committed + imports a 'mixamo.com' take.");
            if (crouchWalk == null || crouchIdle == null || stunned == null || gettingUp == null || pickingUp == null ||
                headHit == null || bigStomachHit == null || stomachHit == null || ribHit == null || hitToBody == null)
            {
                Debug.LogError($"[CharacterAssetGen] missing crouch/hit-react clips (86cackb3j) — crouchWalk=" +
                               $"{crouchWalk != null}, crouchIdle={crouchIdle != null}, stunned={stunned != null}, " +
                               $"gettingUp={gettingUp != null}, pickingUp={pickingUp != null}, headHit={headHit != null}, " +
                               $"bigStomachHit={bigStomachHit != null}, stomachHit={stomachHit != null}, ribHit=" +
                               $"{ribHit != null}, hitToBody={hitToBody != null}); controller not built");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(JumpParam, AnimatorControllerParameterType.Trigger);  // 86ca9yq3q — one-shot Jump
            controller.AddParameter(GroundedParam, AnimatorControllerParameterType.Bool); // 86ca9yq3q rework — land→loco
            controller.AddParameter(ChopParam, AnimatorControllerParameterType.Trigger);  // 86caa4c5c — one-shot chop swing
            // ChopSpeed default 1 (the authored melee clip speed); the Attack state's speedParameter reads it so
            // tool-use speed scales the swing playback rate live (CastawayCharacter.chopSpeed → SetFloat).
            AddFloatParam(controller, ChopSpeedParam, 1f);
            // LocoSpeedMul default 1 (86cackb3j re-soak Part 2 — FOOT-SYNC). The Locomotion state's speedParameter
            // reads it; CastawayCharacter drives it = actualSpeed / strideRef each frame so the legs cadence tracks
            // move-speed (no foot-skate). Default 1 so an unbound rig plays the authored cadence.
            AddFloatParam(controller, LocoSpeedMulParam, 1f);
            // CROUCH + HIT-REACT params (86cackb3j). Crouch (bool) selects the crouch lane; Hit (trigger) + HitRegion
            // (int) fire a body-region reaction; Stunned (bool) holds the knocked-down loop -> Getting Up on release;
            // PickUp (trigger) fires the one-shot ground-pick. The gameplay systems driving these are OOS (this ticket
            // only WIRES the clips); the params exist so the clips are reachable + the contract is test-pinned.
            controller.AddParameter(CrouchParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(HitParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(HitRegionParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(StunnedParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(PickUpParam, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            // 86cackb3j re-soak — the at-rest Idle state plays the BREATHING idle clip ("calm but clearly alive"),
            // replacing the static Idle clip. restIdle = breathingIdle (or the static idle as a defensive fallback).
            idleState.motion = restIdle;

            // The Locomotion state = a 1D blend tree on Speed (Idle floor + Walk + Run). CreateBlendTreeInController
            // creates the tree as an asset child of the controller AND the hosting state in one call.
            var locoState = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            // FOOT-SYNC (86cackb3j re-soak Part 2) — the Locomotion state's PLAYBACK speed reads LocoSpeedMul, so
            // CastawayCharacter scales the walk/run clip cadence to the actual move-speed (no foot-skate). This is
            // the SAME idiom the Attack state uses for ChopSpeed (speedParameterActive + speedParameter). The Speed
            // BLEND param (which clip) is untouched; this only scales HOW FAST the chosen blend plays.
            locoState.speedParameterActive = true;
            locoState.speedParameter = LocoSpeedMulParam;
            // Idle floor @0 so a tiny residual speed reads as standing (no foot-slide); Walk @WalkBlendSpeed;
            // Run @RunBlendSpeed. The Speed param above WalkBlendSpeed blends Walk->Run; below it blends Walk->Idle.
            // 86cackb3j re-soak — the @0 floor uses the BREATHING idle (restIdle) too, so a tiny residual speed at
            // the edge of motion still reads "alive" rather than the old static idle.
            tree.AddChild(restIdle, IdleBlendSpeed);
            tree.AddChild(walk, WalkBlendSpeed);
            tree.AddChild(run, RunBlendSpeed);

            sm.defaultState = idleState;

            // 86caay44r — SOFTEN the Idle<->Locomotion crossfades. The Sponsor reported idle<->walk<->run
            // transitions as too ABRUPT, most on releasing W (the walk->stop). The Moving bool flips in one frame
            // at walkSpeedThreshold, so a SHORT transition duration snaps the state crossfade even with the Speed
            // param damped. Lengthening the crossfade (0.12->0.22 start, 0.15->0.30 stop — the stop is the
            // most-noticed case, so it eases out a touch longer) makes the Idle<->Locomotion blend glide. Pairs
            // with the damped Speed param (CastawayCharacter.speedDampTime) for the full smoothing. Feel-tuned;
            // the Sponsor judges in soak.
            var toLoco = idleState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toLoco.hasExitTime = false;
            toLoco.duration = 0.22f;

            var toIdle = locoState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.30f;

            // JUMP (86ca9yq3q rework — Sponsor soak) — TWO one-shot overlay states by movement state:
            //   JumpIdle    = the idle/standing jump (Jump_idle.fbx);    AnyState→JumpIdle    on (Jump && !Moving)
            //   JumpRunning = the walk/run jump      (Jump_running.fbx); AnyState→JumpRunning on (Jump &&  Moving)
            // Clip choice keys off the SAME Moving bool the locomotion graph uses, so a jump initiated mid-walk/run
            // plays Jump_running and a standing jump plays Jump_idle (AC: two clips by movement state).
            //
            // THE FLOATING-BUG FIX (the rework's core): each jump state transitions BACK on the GROUNDED bool
            // (driven = !IsAirborne by CastawayCharacter), NOT on clip exit-time. The MOMENT the character grounds:
            //   Grounded && Moving  → Locomotion (the Walk/Run blend tree)  — W still held → Walk/Run resumes SAME frame
            //   Grounded && !Moving → Idle
            // The prior exit-time→Idle path stalled the character in the FINISHED jump pose after landing while it
            // kept translating (W held) until exit-time elapsed → the "floating" percept. Routing land→locomotion
            // on the grounded edge resumes the locomotion blend on the landing frame. Neither jump state is a
            // blend-tree child — the Walk<->Run blend tree is UNTOUCHED (AC5 OOS protection).
            var jumpIdleState = sm.AddState("JumpIdle");
            jumpIdleState.motion = jumpIdle;
            var jumpRunningState = sm.AddState("JumpRunning");
            jumpRunningState.motion = jumpRunning;

            // AnyState → JumpIdle (idle/standing jump): Jump trigger AND NOT Moving.
            var anyToJumpIdle = sm.AddAnyStateTransition(jumpIdleState);
            anyToJumpIdle.AddCondition(AnimatorConditionMode.If, 0f, JumpParam);
            anyToJumpIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            anyToJumpIdle.hasExitTime = false;
            anyToJumpIdle.duration = 0.06f;            // a quick crossfade into the push-off
            anyToJumpIdle.canTransitionToSelf = false; // a re-trigger mid-jump won't restart the clip from 0

            // AnyState → JumpRunning (walk/run jump): Jump trigger AND Moving.
            var anyToJumpRunning = sm.AddAnyStateTransition(jumpRunningState);
            anyToJumpRunning.AddCondition(AnimatorConditionMode.If, 0f, JumpParam);
            anyToJumpRunning.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            anyToJumpRunning.hasExitTime = false;
            anyToJumpRunning.duration = 0.06f;
            anyToJumpRunning.canTransitionToSelf = false;

            // Both jump states return on the GROUNDED edge — to Locomotion if still moving, else Idle. No exit-time
            // (the physical arc, not the clip length, owns landing — AdvanceJump flips Grounded on touch-down).
            WireJumpReturn(jumpIdleState, locoState, idleState);
            WireJumpReturn(jumpRunningState, locoState, idleState);

            // CHOP swing (86caa4c5c change-(b)) — a ONE-SHOT Attack state playing the Mixamo melee clip, replacing
            // the rejected procedural ChopPoseDriver swing. AnyState→Attack on the Chop trigger (so a chop fires
            // from Idle OR mid-locomotion — like the jump). The state's speedParameter = ChopSpeed scales the clip
            // playback rate live (tool-use speed). The clip is non-looping; Attack returns on its OWN exit-time
            // (the swing's natural end — unlike the jump, whose landing is a physics edge) to Locomotion (Moving)
            // or Idle (!Moving), so a chop while walking resumes locomotion on the swing's end (no-stall lesson).
            var attackState = sm.AddState("Attack");
            attackState.motion = melee;
            attackState.speedParameterActive = true;
            attackState.speedParameter = ChopSpeedParam;

            var anyToAttack = sm.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, ChopParam);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.06f;            // a quick crossfade into the windup
            anyToAttack.canTransitionToSelf = true;  // a re-chop mid-swing restarts the strike (mash = repeated chops)

            WireAttackReturn(attackState, locoState, idleState);

            // ===== CROUCH LANE (86cackb3j) — a SECOND locomotion lane, NOT folded into the upright Walk<->Run
            // blend tree (it stays exactly {Idle, Walk, Run} — the Attack/Jump OOS-protection idiom). Two states:
            //   CrouchIdle = Crouching Idle.fbx (LOOP);  CrouchWalk = Sneak Walk.fbx (LOOP, the crouch-move).
            // Reached from the upright graph on the Crouch bool, selected by Moving inside the crouch lane, and
            // released back to the upright graph when Crouch flips false. AnyState→ on (Crouch [&& Moving]) so a
            // crouch can engage from Idle OR mid-walk; the lane itself flips CrouchIdle<->CrouchWalk on Moving.
            var crouchIdleState = sm.AddState("CrouchIdle");
            crouchIdleState.motion = crouchIdle;
            var crouchWalkState = sm.AddState("CrouchWalk");
            crouchWalkState.motion = crouchWalk;

            // AnyState → CrouchWalk (Crouch && Moving) ; AnyState → CrouchIdle (Crouch && !Moving). The moving lane
            // is added first so a moving crouch-engage prefers CrouchWalk. canTransitionToSelf=false so re-evaluating
            // the same lane doesn't restart the loop from 0.
            var anyToCrouchWalk = sm.AddAnyStateTransition(crouchWalkState);
            anyToCrouchWalk.AddCondition(AnimatorConditionMode.If, 0f, CrouchParam);
            anyToCrouchWalk.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            anyToCrouchWalk.hasExitTime = false;
            anyToCrouchWalk.duration = 0.18f;
            anyToCrouchWalk.canTransitionToSelf = false;

            var anyToCrouchIdle = sm.AddAnyStateTransition(crouchIdleState);
            anyToCrouchIdle.AddCondition(AnimatorConditionMode.If, 0f, CrouchParam);
            anyToCrouchIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            anyToCrouchIdle.hasExitTime = false;
            anyToCrouchIdle.duration = 0.18f;
            anyToCrouchIdle.canTransitionToSelf = false;

            // Release the crouch lane back to the upright graph on (!Crouch): → Locomotion if Moving else Idle (the
            // no-stall idiom — standing up while still moving resumes Walk/Run on the same frame).
            WireCrouchRelease(crouchIdleState, locoState, idleState);
            WireCrouchRelease(crouchWalkState, locoState, idleState);

            // ===== HIT-REACT (86cackb3j) — five body-region reactions fired by the Hit trigger, the clip selected by
            // the HitRegion int (0=Body default, 1=Head, 2=BigStomach, 3=Stomach, 4=Rib). Each is a ONE-SHOT overlay
            // state (the Attack idiom): AnyState→<region> on (Hit && HitRegion==value), returning on exit-time to
            // Locomotion (Moving) / Idle (!Moving) so a hit taken mid-walk resumes locomotion when the flinch ends.
            // NOT folded into the blend tree. A hit can fire from any state (AnyState) — you can be hit while idle,
            // walking, running, crouched, or recovering.
            WireHitReact(sm, "HitToBody", hitToBody, HitRegionBody, locoState, idleState);
            WireHitReact(sm, "HeadHit", headHit, HitRegionHead, locoState, idleState);
            WireHitReact(sm, "BigStomachHit", bigStomachHit, HitRegionBigStomach, locoState, idleState);
            WireHitReact(sm, "StomachHit", stomachHit, HitRegionStomach, locoState, idleState);
            WireHitReact(sm, "RibHit", ribHit, HitRegionRib, locoState, idleState);

            // ===== STUNNED + recovery (86cackb3j) — Stunned (bool) holds a LOOPING knocked-down state; when it flips
            // false the character plays the one-shot Getting Up recovery, then returns to Locomotion/Idle. AnyState→
            // Stunned on (Stunned) so a stun can land from any state. Stunned→GettingUp on (!Stunned). The recovery
            // is a dedicated transition chain (NOT AnyState) so Getting Up only plays as the END of a stun, never
            // spuriously. GettingUp returns on exit-time → Locomotion (Moving) / Idle.
            var stunnedState = sm.AddState("Stunned");
            stunnedState.motion = stunned;
            var gettingUpState = sm.AddState("GettingUp");
            gettingUpState.motion = gettingUp;

            var anyToStunned = sm.AddAnyStateTransition(stunnedState);
            anyToStunned.AddCondition(AnimatorConditionMode.If, 0f, StunnedParam);
            anyToStunned.hasExitTime = false;
            anyToStunned.duration = 0.08f;
            anyToStunned.canTransitionToSelf = false; // re-asserting Stunned won't restart the knocked-down loop

            // Stunned → GettingUp the moment Stunned releases (the recovery). No exit-time (the stun ends when the
            // gameplay flag clears, not on a clip cycle).
            var stunnedToRecover = stunnedState.AddTransition(gettingUpState);
            stunnedToRecover.AddCondition(AnimatorConditionMode.IfNot, 0f, StunnedParam);
            stunnedToRecover.hasExitTime = false;
            stunnedToRecover.duration = 0.12f;
            stunnedToRecover.hasFixedDuration = true;

            // GettingUp plays once, then returns to Locomotion (Moving) / Idle on its exit-time (the recovery ends).
            WireOneShotReturn(gettingUpState, locoState, idleState);

            // ===== PICK-UP interaction (86cackb3j) — a ONE-SHOT ground-pick: AnyState→PickingUp on the PickUp trigger,
            // returning on exit-time to Locomotion (Moving) / Idle (!Moving). The Attack/Getting-Up idiom.
            var pickingUpState = sm.AddState("PickingUp");
            pickingUpState.motion = pickingUp;
            var anyToPickUp = sm.AddAnyStateTransition(pickingUpState);
            anyToPickUp.AddCondition(AnimatorConditionMode.If, 0f, PickUpParam);
            anyToPickUp.hasExitTime = false;
            anyToPickUp.duration = 0.08f;
            anyToPickUp.canTransitionToSelf = false;
            WireOneShotReturn(pickingUpState, locoState, idleState);

            EditorUtility.SetDirty(controller);
            Debug.Log("[CharacterAssetGen] AnimatorController built: Idle<->Locomotion(Moving) + Walk<->Run 1D " +
                      $"blend tree on Speed (Idle@{IdleBlendSpeed} Walk@{WalkBlendSpeed} Run@{RunBlendSpeed}) + " +
                      $"JumpIdle/JumpRunning one-shots (AnyState on '{JumpParam}'+Moving; return on '{GroundedParam}' " +
                      $"edge → Locomotion if Moving else Idle) + Attack chop swing (AnyState on '{ChopParam}'; speed " +
                      $"'{ChopSpeedParam}'; return on exit → Locomotion if Moving else Idle) + 86cackb3j: CrouchIdle/" +
                      $"CrouchWalk lane (AnyState on '{CrouchParam}'[+Moving]; release on !Crouch) + 5 hit-reacts " +
                      $"(AnyState on '{HitParam}'+'{HitRegionParam}'==0..4) + Stunned loop→GettingUp (on '{StunnedParam}') " +
                      $"+ PickingUp one-shot (on '{PickUpParam}') -> " + ControllerPath);
        }

        // (86cackb3j) Release the crouch lane back to the upright graph on (!Crouch): → Locomotion if Moving else
        // Idle. The no-stall idiom (standing up while moving resumes Walk/Run on the same frame). No exit-time — the
        // crouch ends when the gameplay flag clears, not on a clip cycle. Moving transition added first.
        private static void WireCrouchRelease(AnimatorState crouchState, AnimatorState locoState, AnimatorState idleState)
        {
            var toLoco = crouchState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, CrouchParam);
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toLoco.hasExitTime = false;
            toLoco.duration = 0.18f;
            toLoco.hasFixedDuration = true;

            var toIdle = crouchState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, CrouchParam);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.18f;
            toIdle.hasFixedDuration = true;
        }

        // (86cackb3j) Wire one body-region hit-react: an AnyState→<state> one-shot fired by (Hit && HitRegion==region),
        // returning on exit-time to Locomotion (Moving) / Idle (!Moving) — the Attack idiom so a mid-walk hit resumes
        // locomotion when the flinch ends. canTransitionToSelf=true so a rapid repeated hit re-triggers the flinch.
        private static void WireHitReact(AnimatorStateMachine sm, string stateName, AnimationClip clip, int region,
                                         AnimatorState locoState, AnimatorState idleState)
        {
            var state = sm.AddState(stateName);
            state.motion = clip;

            var any = sm.AddAnyStateTransition(state);
            any.AddCondition(AnimatorConditionMode.If, 0f, HitParam);
            any.AddCondition(AnimatorConditionMode.Equals, region, HitRegionParam);
            any.hasExitTime = false;
            any.duration = 0.06f;            // a quick crossfade into the flinch
            any.canTransitionToSelf = true;  // a rapid repeated hit re-triggers the flinch

            WireOneShotReturn(state, locoState, idleState);
        }

        // (86cackb3j) Wire a one-shot overlay state's return on its OWN exit-time (the clip's natural end — a flinch /
        // pick-up / recovery has no physics edge like the jump's landing): → Locomotion if Moving else Idle, so a
        // held-movement one-shot resumes locomotion on the clip's end (the no-stall lesson, same as WireAttackReturn).
        private static void WireOneShotReturn(AnimatorState state, AnimatorState locoState, AnimatorState idleState)
        {
            var toLoco = state.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toLoco.hasExitTime = true;
            toLoco.exitTime = 0.9f;        // play ~90% of the clip before returning
            toLoco.duration = 0.12f;
            toLoco.hasFixedDuration = true;

            var toIdle = state.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.9f;
            toIdle.duration = 0.12f;
            toIdle.hasFixedDuration = true;
        }

        // (86ca9yq3q rework — THE floating-bug fix) Wire a jump state's return transitions on the GROUNDED edge:
        //   Grounded && Moving  → Locomotion (Walk/Run blend resumes the SAME frame W is still held on landing)
        //   Grounded && !Moving → Idle
        // NO exit-time — the physical ballistic arc owns landing (CastawayCharacter.AdvanceJump flips Grounded on
        // touch-down), not the clip length. A short crossfade keeps the land→loco transition smooth. Ordering: the
        // Moving transition is added first so a moving landing prefers Locomotion over Idle.
        private static void WireJumpReturn(AnimatorState jumpState, AnimatorState locoState, AnimatorState idleState)
        {
            var toLoco = jumpState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, GroundedParam);
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toLoco.hasExitTime = false;
            toLoco.duration = 0.10f;
            toLoco.hasFixedDuration = true;

            var toIdle = jumpState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.If, 0f, GroundedParam);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.12f;
            toIdle.hasFixedDuration = true;
        }

        // (86caa4c5c change-(b)) Wire the chop Attack state's return on the clip's OWN EXIT-TIME (the swing's
        // natural end — a chop has no physics edge like the jump's landing). At exit it returns to:
        //   Moving  → Locomotion (Walk/Run blend resumes the SAME instant the swing ends if W is still held)
        //   !Moving → Idle
        // Routing the held-movement chop back to Locomotion (not Idle-only) avoids the post-overlay locomotion
        // stall the jump rework fixed (a finished one-shot pose translating in place reads as "floating"/gliding).
        // exitTime near 1 so the full strike plays; a short fixed crossfade keeps the return smooth.
        private static void WireAttackReturn(AnimatorState attackState, AnimatorState locoState, AnimatorState idleState)
        {
            var toLoco = attackState.AddTransition(locoState);
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            toLoco.hasExitTime = true;
            toLoco.exitTime = 0.9f;        // play ~90% of the swing before returning (the strike has landed)
            toLoco.duration = 0.10f;
            toLoco.hasFixedDuration = true;

            var toIdle = attackState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.9f;
            toIdle.duration = 0.12f;
            toIdle.hasFixedDuration = true;
        }

        // Add a float Animator parameter WITH a default value (AnimatorController.AddParameter alone leaves the
        // default at 0). Used for ChopSpeed (default 1 = the authored melee clip speed) so an unbound rig plays the
        // swing at authored speed rather than frozen (speed 0).
        private static void AddFloatParam(AnimatorController controller, string name, float defaultValue)
        {
            var p = new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue
            };
            controller.AddParameter(p);
        }

        // Mirror MovementCameraScene.EnsureShaderAlwaysIncluded: GraphicsSettings.asset's
        // m_AlwaysIncludedShaders is editable via SerializedObject so the URP/Lit shader never strips.
        private static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        // ===== DIAGNOSE-VIA-TRACE (throwaway entry; never on the ship path) — dump the imported asset's
        // ground truth so the held-axe bone + the recolor + the proportion guards are tuned from EVIDENCE,
        // not guesses (diagnostic-traces-before-hypothesized-fixes). Dumps: every sub-asset (clips/avatar);
        // the SMR roster + the FULL bone list flagging hand/head bones; intrinsic + normalized height. Run:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CharacterAssetGen.CharacterDiagnoseTrace
        public static void CharacterDiagnoseTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[char-trace] ===== CHARACTER DIAGNOSE TRACE =====");
            foreach (var fbx in new[] { IdleFbxPath, WalkFbxPath, RunFbxPath, JumpIdleFbxPath, JumpRunningFbxPath, MeleeFbxPath })
            {
                sb.AppendLine("[char-trace] ===== " + fbx + " =====");
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    string extra = "";
                    if (o is AnimationClip c) extra = $" len={c.length:F2}s looping={c.isLooping}";
                    if (o is Avatar a) extra = $" valid={a.isValid} human={a.isHuman}";
                    sb.AppendLine($"[char-trace]   {o.GetType().Name}: '{o.name}'{extra}");
                }
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer != null)
                    foreach (var dc in importer.defaultClipAnimations)
                        sb.AppendLine($"[char-trace]     defaultClip '{dc.name}' take='{dc.takeName}'");
            }

            var idle = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (idle != null)
            {
                var inst = Object.Instantiate(idle);
                inst.transform.localScale = Vector3.one;
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                sb.AppendLine($"[char-trace] Idle instantiated: SMR count={smrs.Length}");
                var rends = inst.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    sb.AppendLine($"[char-trace] normalized height={b.size.y:F3}u bounds={b.size}");
                }
                if (smrs.Length > 0 && smrs[0].bones != null)
                {
                    sb.AppendLine($"[char-trace] bone count={smrs[0].bones.Length}:");
                    foreach (var bone in smrs[0].bones)
                    {
                        if (bone == null) { sb.AppendLine("[char-trace]   <null bone>"); continue; }
                        string n = bone.name.ToLowerInvariant();
                        string tag = n.Contains("righthand") || (n.Contains("right") && n.Contains("hand")) ? "  <-- RIGHT HAND"
                                   : n.Contains("hand") ? "  <-- hand"
                                   : n.Contains("head") ? "  <-- head" : "";
                        sb.AppendLine($"[char-trace]   bone='{bone.name}' lossyScale={bone.lossyScale}{tag}");
                    }
                }
                Object.DestroyImmediate(inst);
            }
            sb.AppendLine("[char-trace] ===== END TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ===== CASTAWAY v2 HAND-AXIS TRACE (86cajwp23 AC2 — the held-axe RE-MEASURE instrument). Durable
        // read-only diagnostic (procedural-animation-verbs.md "measure bone axes FIRST"): the HeldAxeRig seat
        // (HeldAxeRelEuler / HeldAxeLocalOffsetFromHand in MovementCameraScene) was dialed against the OLD rig's
        // mixamorig:RightHand LOCAL FRAME; v2's rigged bind pose may orient that frame differently, so the seat
        // must be re-derived from v2's ACTUAL hand-bone axes rather than guessed. Dumps, for v2's
        // mixamorig:RightHand: local rotation (Euler), lossyScale (the §FBX lossy-bone trap check), and each
        // LOCAL axis (+X/+Y/+Z) expressed as a WORLD direction at the T-pose bind — so the grip/forearm axis is
        // identifiable. Run on the runner (has a warm Library) to get MEASURED values before the soak locks:
        //   Unity -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.CharacterAssetGen.CastawayV2HandAxisTrace
        public static void CastawayV2HandAxisTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[v2-hand] ===== CASTAWAY v2 HAND-AXIS TRACE (86cajwp23 AC2) =====");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(V2RiggedFbxPath);
            if (fbx == null)
            {
                sb.AppendLine("[v2-hand] v2 base FBX NOT FOUND at " + V2RiggedFbxPath);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            var inst = Object.Instantiate(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // Overall height (confirms the height-normalize landed ~1u) + the full bone list, flagging the hands.
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                sb.AppendLine($"[v2-hand] normalized height={b.size.y:F3}u (target {TargetImportHeightU}u)");
            }

            Transform rightHand = null, leftHand = null;
            int boneCount = 0;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                boneCount++;
                string tok = ExactTokenLocal(t.name);
                if (tok == "righthand") rightHand = t;
                if (tok == "lefthand") leftHand = t;
            }
            sb.AppendLine($"[v2-hand] transforms={boneCount}  rightHand={(rightHand != null ? rightHand.name : "<MISSING>")}" +
                          $"  leftHand={(leftHand != null ? leftHand.name : "<MISSING>")}");

            if (rightHand != null)
            {
                sb.AppendLine($"[v2-hand] RightHand localRotation(euler)={NormEuler(rightHand.localRotation.eulerAngles)}");
                sb.AppendLine($"[v2-hand] RightHand worldRotation(euler)={NormEuler(rightHand.rotation.eulerAngles)}");
                sb.AppendLine($"[v2-hand] RightHand lossyScale={rightHand.lossyScale} (expect ~1,1,1 — Mixamo has NO 267x trap)");
                // The +X/+Y/+Z LOCAL axes as WORLD directions at bind — identifies which local axis points along
                // the grip/forearm (the held-axe relEuler is dialed against these).
                sb.AppendLine($"[v2-hand] RightHand local+X in world={(rightHand.rotation * Vector3.right).ToString("F3")}");
                sb.AppendLine($"[v2-hand] RightHand local+Y in world={(rightHand.rotation * Vector3.up).ToString("F3")}");
                sb.AppendLine($"[v2-hand] RightHand local+Z in world={(rightHand.rotation * Vector3.forward).ToString("F3")}");
                sb.AppendLine("[v2-hand] SEAT-DERIVE: compare these axes to the OLD rig's (run CharacterDiagnoseTrace on the old " +
                              "Idle.fbx) — if the local frame matches, the OLD HeldAxeRelEuler carries; if it differs, rotate " +
                              "HeldAxeV2RelEuler by the frame delta, then the Sponsor F9-dials the final seat in the soak.");
            }
            else
            {
                sb.AppendLine("[v2-hand] mixamorig:RightHand NOT resolved — the held axe cannot seat; the v2 export is missing the hand bone");
            }

            Object.DestroyImmediate(inst);
            sb.AppendLine("[v2-hand] ===== END TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ===== (attempt-8 throwaway import-flag fix-probes REMOVED post-diagnosis: useFileScale=false made it
        // WORSE — 218u tall + the 100× node SURVIVED; bakeAxisConversion=true ALSO kept the 100× node. Both
        // REFUTED the "an importer flag collapses the cm→m node" hypothesis — the 100× is intrinsic to the FBX
        // hierarchy. The remaining read-only ScaleChainDiagnose stays as the durable instrument that found it.) =====

        // ===== SCALE-CHAIN DIAGNOSE (86ca8rdkp attempt-8 — ROOT-CAUSE the 68u intrinsic offset). Read-only
        // diagnostic, KEPT as the durable instrument that found the root cause (re-runnable on any future
        // character swap to catch a cm→m node). Reproduces the SCENE setup (avatar root scaled PlayerVisualHeight under a
        // player root, the FBX instantiated as a child, BakeMesh the live mesh) and dumps EVERY node's
        // localScale + lossyScale + the SMR.transform scale + the BakeMesh actual extents in BOTH local AND
        // world space — so we MEASURE where the ~68 lives (the cm→m 100× node, or a bone, or the bake) instead
        // of guessing. Run:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CharacterAssetGen.ScaleChainDiagnose
        public static void ScaleChainDiagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[scale-trace] ===== SCALE-CHAIN DIAGNOSE (68u root-cause) =====");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (fbx == null)
            {
                sb.AppendLine("[scale-trace] FBX NOT FOUND at " + IdleFbxPath);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            // ---- (A) the importer's effective globalScale (the height-normalize result) ----
            var importer = AssetImporter.GetAtPath(IdleFbxPath) as ModelImporter;
            if (importer != null)
                sb.AppendLine($"[scale-trace] importer.globalScale={importer.globalScale:F6} " +
                              $"useFileScale={importer.useFileScale} useFileUnits={importer.useFileUnits} " +
                              $"animationType={importer.animationType}");

            // ---- (B) reproduce the SCENE: playerRoot -> avatarRoot(scale 1.8) -> FBX(scale 1) ----
            const float PlayerVisualHeight = 1.8f; // mirror MovementCameraScene
            var playerRoot = new GameObject("__diagPlayer");
            playerRoot.transform.position = Vector3.zero;
            var avatarRoot = new GameObject("__diagAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * PlayerVisualHeight;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one; // matches BuildModel

            sb.AppendLine($"[scale-trace] avatarRoot lossyScale={avatarRoot.transform.lossyScale}");
            sb.AppendLine($"[scale-trace] modelChild '{model.name}' localScale={model.transform.localScale} " +
                          $"lossyScale={model.transform.lossyScale}");

            // ---- (C) walk EVERY transform under the model, dump local+lossy scale (find the 100× node) ----
            sb.AppendLine("[scale-trace] --- full transform scale chain ---");
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                Vector3 ls = t.localScale, lossy = t.lossyScale;
                // FLAG any node whose local OR lossy scale is far from ~1 (the 100× / cm→m suspect).
                bool suspect = Mathf.Abs(ls.x) > 5f || Mathf.Abs(ls.x) < 0.2f ||
                               Mathf.Abs(lossy.x) > 5f || Mathf.Abs(lossy.x) < 0.02f;
                string flag = suspect ? "  <== SCALE SUSPECT" : "";
                sb.AppendLine($"[scale-trace]   '{t.name}' local={ls.ToString("F4")} lossy={lossy.ToString("F5")}{flag}");
            }

            // ---- (D) the SMR(s): transform scale + BakeMesh extents in LOCAL and WORLD ----
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            sb.AppendLine($"[scale-trace] --- {smrs.Length} SMR(s) ---");
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                sb.AppendLine($"[scale-trace] SMR '{smr.name}' transform.localScale={smr.transform.localScale} " +
                              $"lossyScale={smr.transform.lossyScale}");
                sb.AppendLine($"[scale-trace]   sharedMesh.bounds.size={smr.sharedMesh.bounds.size} (import-baked)");
                sb.AppendLine($"[scale-trace]   SMR.bounds(world AABB).size={smr.bounds.size} min.y={smr.bounds.min.y:F4}");

                var baked = new Mesh();
                // useScale:FALSE — node scale applied via the matrix below (apply ONCE; matches runtime path).
                smr.BakeMesh(baked, false);
                var verts = baked.vertices;
                if (verts.Length > 0)
                {
                    float lMinY = float.PositiveInfinity, lMaxY = float.NegativeInfinity;
                    float wMinY = float.PositiveInfinity, wMaxY = float.NegativeInfinity;
                    Matrix4x4 l2w = smr.transform.localToWorldMatrix;
                    foreach (var v in verts)
                    {
                        if (v.y < lMinY) lMinY = v.y; if (v.y > lMaxY) lMaxY = v.y;
                        float wy = l2w.MultiplyPoint3x4(v).y;
                        if (wy < wMinY) wMinY = wy; if (wy > wMaxY) wMaxY = wy;
                    }
                    sb.AppendLine($"[scale-trace]   BakeMesh(useScale:false) LOCAL y=[{lMinY:F4}..{lMaxY:F4}] " +
                                  $"height={lMaxY - lMinY:F4}");
                    sb.AppendLine($"[scale-trace]   BakeMesh->WORLD via l2w  y=[{wMinY:F4}..{wMaxY:F4}] " +
                                  $"height={wMaxY - wMinY:F4}  <== THE SNAP READS THIS world-Y");
                }
                Object.DestroyImmediate(baked);
            }

            // ---- (E) overall rendered bounds (what MeasureHeight reads) ----
            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                sb.AppendLine($"[scale-trace] OVERALL Renderer.bounds (scene, scaled 1.8): size={b.size} " +
                              $"center.y={b.center.y:F4} min.y={b.min.y:F4} max.y={b.max.y:F4}");
            }

            Object.DestroyImmediate(playerRoot);
            sb.AppendLine("[scale-trace] ===== END SCALE-CHAIN DIAGNOSE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ===== CLIP-BASELINE DIAGNOSE (86ca8rdkp attempt-9 — THROWAWAY; removed before PR). Bakes the IDLE and
        // WALK clips across their full cycle and reports the SCALE-IMMUNE baked sole-Y (lowest vertex, unit-scale
        // TRS) at each sample — so we MEASURE whether the WALK clip lifts the whole mesh off the feet relative to
        // IDLE (the [FloatTrace] showed GAP≈0 at rest but +0.69 walking). Reproduces the SCENE rig (avatarRoot
        // scale 1.8 under a player root), samples each clip via AnimationClip.SampleAnimation. Run:
        //   Unity -batchmode -quit -executeMethod FarHorizon.EditorTools.CharacterAssetGen.ClipBaselineDiagnose
        public static void ClipBaselineDiagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[clip-trace] ===== CLIP-BASELINE DIAGNOSE (walk-lift root cause) =====");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (fbx == null) { sb.AppendLine("[clip-trace] FBX NOT FOUND " + IdleFbxPath); Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0); return; }

            AnimationClip idle = FindClip(IdleFbxPath, IdleClip);
            AnimationClip walk = FindClip(WalkFbxPath, WalkClip);
            AnimationClip run = FindClip(RunFbxPath, RunClip);
            sb.AppendLine($"[clip-trace] idle={(idle != null ? idle.name : "<null>")} walk={(walk != null ? walk.name : "<null>")} run={(run != null ? run.name : "<null>")}");

            const float PlayerVisualHeight = 1.8f;
            var playerRoot = new GameObject("__clipPlayer");
            var avatarRoot = new GameObject("__clipAvatar");
            avatarRoot.transform.SetParent(playerRoot.transform, false);
            avatarRoot.transform.localScale = Vector3.one * PlayerVisualHeight;
            var model = Object.Instantiate(fbx, avatarRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);

            void SampleClip(string label, AnimationClip clip)
            {
                if (clip == null) { sb.AppendLine($"[clip-trace] {label}: <null clip>"); return; }
                float minSole = float.PositiveInfinity, maxSole = float.NegativeInfinity;
                int N = 12;
                for (int i = 0; i <= N; i++)
                {
                    float t = clip.length * i / N;
                    clip.SampleAnimation(model, t);
                    float sole = BakeSoleScaleImmune(smr);
                    if (sole < minSole) minSole = sole;
                    if (sole > maxSole) maxSole = sole;
                    sb.AppendLine($"[clip-trace] {label} t={t:F3}s soleY(scale-immune,root@0)={sole:F4}");
                }
                sb.AppendLine($"[clip-trace] {label} SUMMARY soleY min={minSole:F4} max={maxSole:F4} " +
                              $"range={maxSole - minSole:F4}  <== a clip whose min soleY != ~0 lifts the feet off the root");
            }

            SampleClip("IDLE", idle);
            SampleClip("WALK", walk);
            SampleClip("RUN", run);

            Object.DestroyImmediate(playerRoot);
            sb.AppendLine("[clip-trace] ===== END CLIP-BASELINE DIAGNOSE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // Scale-immune baked sole-Y for the diagnose (unit-scale TRS world matrix — the FBX 100× node never blows
        // it up; matches CastawayCharacter.MeasureRenderedSoleWorldY). avatarRoot is at world 0 here, so the
        // returned value is the sole-Y RELATIVE to the (grounded) root — ~0 means feet on the root.
        private static float BakeSoleScaleImmune(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return float.NaN;
            var baked = new Mesh();
            smr.BakeMesh(baked, false);
            var verts = baked.vertices;
            Matrix4x4 l2w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            float minY = float.PositiveInfinity;
            foreach (var v in verts) { float y = l2w.MultiplyPoint3x4(v).y; if (y < minY) minY = y; }
            Object.DestroyImmediate(baked);
            return minY;
        }

        // ===== FINGER-DEFORM TRACE (86cackb3j re-soak Part 4 — "clean in T-pose, mangles UNDER ANIMATION, empty
        // hands no axe"). DIAGNOSE-VIA-TRACE (diagnostic-traces-before-hypothesized-fixes). The earlier -fingerTrace
        // measured the BIND-POSE skinning (uniform 1.8 lossy, verts tight) and ruled out a static weight defect —
        // but the new symptom is UNDER ANIMATION. CastawayFingerCurl early-returns when not gripping (line 111), so
        // with empty hands it is NOT the cause; the candidates are: (1) a CLIP that poses the finger bones into a
        // broken shape (POSE artifact — fix in CastawayFingerCurl angles/gating, code-only — though the empty-hand
        // gate already rules the curl out, so a pose artifact here means the IMPORTED CLIP itself mangles), or
        // (2) a genuine WEIGHT defect that only SHOWS when bones rotate (a finger vert dominantly weighted to the
        // WRONG bone STRETCHES as that bone moves). This trace DISCRIMINATES them: it samples the BREATHING-IDLE +
        // WALK clips across their cycle and, per right-hand finger bone, measures the dominant-weighted verts'
        // distance-from-bone — a value that BALLOONS across the animation = a weight defect (verts torn off the
        // bone); a value that stays ~bind-pose-tight while the bone rotates = clean skinning (the clip's bone POSE
        // is what reads, not torn weights). Run:
        //   Unity -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.CharacterAssetGen.FingerDeformTrace
        public static void FingerDeformTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[finger-trace] ===== FINGER-DEFORM TRACE (86cackb3j — mangle-under-animation, empty hands) =====");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (fbx == null) { sb.AppendLine("[finger-trace] FBX NOT FOUND " + IdleFbxPath); Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0); return; }

            // The right-hand finger bone tokens (mirror MovementCameraScene.RightFingerCurlTokens + thumb).
            string[] fingerTokens =
            {
                "righthandindex1","righthandindex2","righthandindex3",
                "righthandmiddle1","righthandmiddle2","righthandmiddle3",
                "righthandring1","righthandring2","righthandring3",
                "righthandthumb1","righthandthumb2","righthandthumb3",
            };

            var breathing = FindClip(BreathingIdleFbxPath, BreathingIdleClip);
            var walk = FindClip(WalkFbxPath, WalkClip);
            sb.AppendLine($"[finger-trace] breathingIdle={(breathing != null ? breathing.name : "<null>")} " +
                          $"walk={(walk != null ? walk.name : "<null>")}");

            var model = Object.Instantiate(fbx);
            model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null)
            {
                sb.AppendLine("[finger-trace] no SkinnedMeshRenderer/sharedMesh"); Object.DestroyImmediate(model);
                Debug.Log(sb.ToString()); if (Application.isBatchMode) EditorApplication.Exit(0); return;
            }

            var bones = smr.bones;
            var mesh = smr.sharedMesh;
            var boneWeights = mesh.boneWeights;
            var bindPoses = mesh.bindposes;

            // For each finger bone: the index in the bones[] array + its lossyScale (a degenerate bone tag).
            var fingerBoneIdx = new System.Collections.Generic.Dictionary<int, string>();
            for (int b = 0; b < bones.Length; b++)
            {
                if (bones[b] == null) continue;
                string tok = ExactTokenLocal(bones[b].name);
                foreach (var ft in fingerTokens)
                    if (tok == ft) { fingerBoneIdx[b] = ft; sb.AppendLine(
                        $"[finger-trace] bone[{b}]='{bones[b].name}' tok={ft} lossyScale={bones[b].lossyScale}"); }
            }
            if (fingerBoneIdx.Count == 0) sb.AppendLine("[finger-trace] WARNING: NO finger bones resolved from the SMR bone array");

            // Per finger bone, collect the vertices whose DOMINANT weight is that bone (the verts the bone owns).
            var ownedVerts = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
            foreach (var kv in fingerBoneIdx) ownedVerts[kv.Key] = new System.Collections.Generic.List<int>();
            for (int v = 0; v < boneWeights.Length; v++)
            {
                var w = boneWeights[v];
                int dom = w.boneIndex0; float dw = w.weight0;
                if (w.weight1 > dw) { dom = w.boneIndex1; dw = w.weight1; }
                if (w.weight2 > dw) { dom = w.boneIndex2; dw = w.weight2; }
                if (w.weight3 > dw) { dom = w.boneIndex3; dw = w.weight3; }
                if (ownedVerts.ContainsKey(dom)) ownedVerts[dom].Add(v);
            }

            var baked = new Mesh();
            // Measure, per sampled clip-time, each finger bone's owned-vert MAX distance from the bone origin in
            // BONE-LOCAL space. A clean skin keeps this ~constant across the animation (verts ride the bone); a
            // weight defect makes it BALLOON when a different bone rotates (verts torn off the owning bone).
            void SampleClip(string label, AnimationClip clip)
            {
                if (clip == null) { sb.AppendLine($"[finger-trace] {label}: <null clip>"); return; }
                // bind-pose reference distance per bone (the clean baseline).
                var maxDistOverClip = new System.Collections.Generic.Dictionary<int, float>();
                var minDistOverClip = new System.Collections.Generic.Dictionary<int, float>();
                foreach (var k in ownedVerts.Keys) { maxDistOverClip[k] = 0f; minDistOverClip[k] = float.PositiveInfinity; }
                int N = 10;
                for (int i = 0; i <= N; i++)
                {
                    float t = clip.length * i / N;
                    clip.SampleAnimation(model, t);
                    smr.BakeMesh(baked, false);
                    var verts = baked.vertices;
                    foreach (var kv in ownedVerts)
                    {
                        int boneI = kv.Key; var bone = bones[boneI];
                        if (bone == null || kv.Value.Count == 0) continue;
                        // verts bake in SMR-local space; map to bone-local via the bone's world matrix.
                        Matrix4x4 w2bone = bone.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                        float maxD = 0f;
                        foreach (int vi in kv.Value)
                        {
                            float d = w2bone.MultiplyPoint3x4(verts[vi]).magnitude;
                            if (d > maxD) maxD = d;
                        }
                        if (maxD > maxDistOverClip[boneI]) maxDistOverClip[boneI] = maxD;
                        if (maxD < minDistOverClip[boneI]) minDistOverClip[boneI] = maxD;
                    }
                }
                foreach (var kv in fingerBoneIdx)
                {
                    int boneI = kv.Key;
                    if (!maxDistOverClip.ContainsKey(boneI)) continue;
                    float mn = minDistOverClip[boneI], mx = maxDistOverClip[boneI];
                    float ratio = mn > 1e-4f ? mx / mn : float.PositiveInfinity;
                    string flag = ratio > 1.6f ? "  <== WEIGHT-DEFECT SUSPECT (verts stretch >1.6x under anim)" : "";
                    sb.AppendLine($"[finger-trace] {label} {kv.Value} owned={ownedVerts[boneI].Count} " +
                                  $"ownedVertMaxDist min={mn:F4} max={mx:F4} ratio={ratio:F2}{flag}");
                }
            }

            SampleClip("BREATHING-IDLE", breathing);
            SampleClip("WALK", walk);
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(model);
            sb.AppendLine("[finger-trace] VERDICT GUIDE: every finger bone ratio ~1.0 (verts ride the bone) => CLEAN " +
                          "skinning; the mangle is the CLIP's finger-bone POSE (fix: CastawayFingerCurl/gating or " +
                          "import). A bone ratio >1.6 => genuine WEIGHT defect (repaint that finger's weights).");
            sb.AppendLine("[finger-trace] ===== END FINGER-DEFORM TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static string ExactTokenLocal(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return "";
            string n = boneName.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            return n;
        }

        // ===== FINGER-POSE ROTATION TRACE (PR #186 FINGER re-open — "MANGLES under the arms-down idle pose").
        // The stretch-RATIO trace above (FingerDeformTrace) measures owned-vert distance-from-bone; that ONLY
        // catches a STRETCHED/torn finger (a weight defect) — it CANNOT see a BENT / ROTATED / COLLAPSED finger,
        // which is exactly what a "mangle" looks like (the verts ride a bone that the clip rotates to a BAD ANGLE,
        // so distance-from-bone stays ~1.0 = "CLEAN" while the finger visibly bends wrong). So the earlier
        // "CLEAN SKINNING" verdict was correct-but-irrelevant to a POSE mangle.
        //
        // This trace measures the MISSING dimension: each finger bone's LOCAL ROTATION under the BREATHING-IDLE
        // pose (sampled across the cycle) vs the FBX BIND/REST local rotation, for BOTH hands. A finger bone whose
        // clip pose rotates it far from its rest angle (esp. a big angle on a non-curl axis, or an asymmetric L-vs-R
        // delta) is the cause-(a) signature: the imported Breathing-Idle clip poses that finger to a broken angle on
        // THIS rig. A finger bone that stays near its rest angle under the clip = the clip is NOT mangling it
        // (look elsewhere — weights / bind orient). Run:
        //   Unity -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.CharacterAssetGen.FingerPoseRotationTrace
        public static void FingerPoseRotationTrace()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[finger-pose] ===== FINGER-POSE ROTATION TRACE (PR #186 — mangle under arms-down idle, BOTH hands) =====");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            if (fbx == null) { sb.AppendLine("[finger-pose] FBX NOT FOUND " + IdleFbxPath); Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0); return; }

            // BOTH hands' finger + thumb proximal..distal tokens.
            string[] fingerTokens =
            {
                "righthandindex1","righthandindex2","righthandindex3",
                "righthandmiddle1","righthandmiddle2","righthandmiddle3",
                "righthandring1","righthandring2","righthandring3",
                "righthandpinky1","righthandpinky2","righthandpinky3",
                "righthandthumb1","righthandthumb2","righthandthumb3",
                "lefthandindex1","lefthandindex2","lefthandindex3",
                "lefthandmiddle1","lefthandmiddle2","lefthandmiddle3",
                "lefthandring1","lefthandring2","lefthandring3",
                "lefthandpinky1","lefthandpinky2","lefthandpinky3",
                "lefthandthumb1","lefthandthumb2","lefthandthumb3",
            };

            var breathing = FindClip(BreathingIdleFbxPath, BreathingIdleClip);
            var walk = FindClip(WalkFbxPath, WalkClip);
            sb.AppendLine($"[finger-pose] breathingIdle={(breathing != null ? breathing.name : "<null>")} " +
                          $"walk={(walk != null ? walk.name : "<null>")}");

            var model = Object.Instantiate(fbx);
            model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null) { sb.AppendLine("[finger-pose] no SkinnedMeshRenderer"); Object.DestroyImmediate(model);
                Debug.Log(sb.ToString()); if (Application.isBatchMode) EditorApplication.Exit(0); return; }

            // Resolve each finger bone from the SMR bone array (the real skeleton).
            var fingerBones = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var bone in smr.bones)
            {
                if (bone == null) continue;
                string tok = ExactTokenLocal(bone.name);
                foreach (var ft in fingerTokens) if (tok == ft) fingerBones[ft] = bone;
            }
            sb.AppendLine($"[finger-pose] resolved {fingerBones.Count} finger/thumb bones (of {fingerTokens.Length} tokens)");

            // BIND/REST local rotation: SampleAnimation at clip-time 0 of a clip is the clip's pose, NOT the
            // bind — so capture the FBX REST pose first (the imported skeleton's default localRotation, before
            // any clip is sampled). This is the bone's authored rest angle.
            var restEuler = new System.Collections.Generic.Dictionary<string, Vector3>();
            foreach (var kv in fingerBones) restEuler[kv.Key] = NormEuler(kv.Value.localRotation.eulerAngles);

            // Per clip, per bone: max angular delta of the bone's LOCAL rotation from its REST rotation across
            // the cycle (Quaternion.Angle is axis-agnostic — catches a bend on ANY axis), plus the worst-frame
            // local-Euler so we can read WHICH axis bends.
            void SampleClip(string label, AnimationClip clip)
            {
                if (clip == null) { sb.AppendLine($"[finger-pose] {label}: <null clip>"); return; }
                sb.AppendLine($"[finger-pose] --- {label} (rest-rel local-rotation delta across {clip.length:F2}s cycle) ---");
                var worstAng = new System.Collections.Generic.Dictionary<string, float>();
                var worstEuler = new System.Collections.Generic.Dictionary<string, Vector3>();
                foreach (var k in fingerBones.Keys) worstAng[k] = 0f;
                int N = 12;
                for (int i = 0; i <= N; i++)
                {
                    float t = clip.length * i / N;
                    clip.SampleAnimation(model, t);
                    foreach (var kv in fingerBones)
                    {
                        Quaternion restQ = Quaternion.Euler(restEuler[kv.Key]);
                        float ang = Quaternion.Angle(restQ, kv.Value.localRotation);
                        if (ang > worstAng[kv.Key])
                        {
                            worstAng[kv.Key] = ang;
                            worstEuler[kv.Key] = NormEuler(kv.Value.localRotation.eulerAngles);
                        }
                    }
                }
                // Print in a stable order so L-vs-R asymmetry is eyeball-readable.
                foreach (var ft in fingerTokens)
                {
                    if (!fingerBones.ContainsKey(ft)) continue;
                    float ang = worstAng[ft];
                    string flag = ang > 35f ? "  <== LARGE clip-pose rotation (bad-angle SUSPECT)" : "";
                    sb.AppendLine($"[finger-pose] {label} {ft,-18} restEuler={Fmt(restEuler[ft])} " +
                                  $"worstDeltaAng={ang,6:F1}deg worstEuler={Fmt(worstEuler[ft])}{flag}");
                }
            }

            SampleClip("BREATHING-IDLE", breathing);
            SampleClip("WALK", walk);
            Object.DestroyImmediate(model);
            sb.AppendLine("[finger-pose] VERDICT GUIDE: a finger bone with a small (<35deg) rest-rel delta is NOT " +
                          "posed wrong by the clip. A LARGE delta (esp. asymmetric L-vs-R, or on a non-curl axis) " +
                          "= the imported clip poses that finger to a broken angle on THIS rig (cause (a)); the fix " +
                          "is to MASK/ZERO the finger-bone curves on the idle/locomotion clips so the fingers hold " +
                          "the rig's relaxed rest pose.");
            sb.AppendLine("[finger-pose] ===== END FINGER-POSE ROTATION TRACE =====");
            Debug.Log(sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // Normalize Euler components to (-180,180] so a 359deg rest reads as -1, not a fake 359 delta.
        private static Vector3 NormEuler(Vector3 e)
        {
            return new Vector3(NormDeg(e.x), NormDeg(e.y), NormDeg(e.z));
        }
        private static float NormDeg(float d)
        {
            d %= 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }
        private static string Fmt(Vector3 e) => $"({e.x,6:F1},{e.y,6:F1},{e.z,6:F1})";
    }
}
