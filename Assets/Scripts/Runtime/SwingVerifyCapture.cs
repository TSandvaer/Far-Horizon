using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the PER-CLASS WEAPON SWINGS (ticket 86caffwv5 — attack
    /// animation per weapon).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving the swings
    /// play in the BUILT exe — not just the editor (the legs-up divergence class). This drives each of the 5
    /// per-class swings and captures each mid-strike so the Sponsor's soak (+ the PR reviewer) judge the real
    /// runtime read of every class's motion.
    ///
    /// HOW: for each WeaponClass 0..4 it calls <see cref="CastawayCharacter.TriggerAttack"/> (the SAME seam the
    /// gameplay left-click + tree-chop/mine verbs use) — which sets the Animator's WeaponClass int + ChopSpeed and
    /// pulses the shared Chop trigger, so the controller plays that class's AttackX one-shot. It then lets REAL
    /// frames advance the Animator (a swing pose only advances with real frames — headless deltaTime≈0 never poses
    /// it, so this MUST run WINDOWED) and captures a frame ~mid-swing from the REAL OrbitCamera (gameplay framing —
    /// an isolated hero rig is the false-green class, unity-conventions.md §"capture must use the GAMEPLAY camera").
    ///
    /// SELF-ASSERTS (LOGIC — auditable in a headless log too):
    ///   • each class routed: after each TriggerAttack, <see cref="CastawayCharacter.LastWeaponClass"/> == the class
    ///     (proves the per-class routing fired for the right class, independent of whether the pose is observable);
    ///   • CONE-EXPLOSION GUARD (the Generic-rig bind, 86ca8rdkp): the skinned mesh bounds stay within a sane radius
    ///     of the player across every swing (a Humanoid-retarget explosion flings it thousands of units off-spawn).
    /// Fails non-zero if any class failed to route OR the mesh ever exploded.
    ///
    /// PICKAXE DEEP-FOLD PASS (86cav8xg9). The 5 shots above fire at a fixed 0.28s after TriggerAttack. For the
    /// pickaxe that is only t≈0.08 of its swing (a 5.20s clip at the 1.5× pickaxe playback ≈ 3.47s), so those shots
    /// land in the WIND-UP and structurally CANNOT show the mid-swing body fold the Sponsor flagged at soak-5 — a
    /// green swing_pickaxe.png said nothing about it (the false-green-capture class, unity-conventions.md
    /// §editor-vs-runtime: here the subject is in frame but at the wrong MOMENT). So after the five, this drives the
    /// pickaxe swing twice more: pass 1 MEASURES the live composed torso tilt (hips→head vs world up, sampled off
    /// the real runtime skeleton every frame — so it includes the Animator playback AND the CastawayArmPose
    /// composition, not just the authored curves) and records when it peaks; pass 2 re-fires and shoots that exact
    /// moment from BOTH the gameplay orbit cam and a dedicated SIDE-PROFILE cam. The side profile is required
    /// because an upright-vs-folded-over read is nearly invisible from the player's over-the-shoulder angle and
    /// obvious side-on (lowpoly-quality.md §0 — the pond lift→mound lesson).
    ///
    /// Inert unless launched with -verifySwings (the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySwings -captureDir &lt;dir&gt;
    /// Captures: swing_axe.png, swing_pickaxe.png, swing_dagger.png, swing_spear.png, swing_sword.png,
    ///           swing_pickaxe_fold.png (gameplay cam at the peak fold), swing_pickaxe_fold_side.png (side profile),
    ///           swing_pickaxe_twohand.png (gameplay cam at the WORST two-hand-grip frame — 86cay4282 round 2),
    ///           swing_pickaxe_twohand_front.png (a CLOSE FRONTAL shot — the plane a hand-on-haft read lives in).
    ///
    /// MINE TWO-HAND GRIP PASS (86cay4282 round 2 — the Sponsor's DIRECTION REVERSAL). The second defect from the
    /// same soak — "he is swinging like he is handing the axe with both hands". Round 1 treated the mine clip's
    /// locked-together hands as the defect and gated on the hands OPENING; the Sponsor then reversed the premise
    /// ("we need to position the axe for a two hand grip"), so the clip is right and the SEAT is the defect. The
    /// anchor is therefore the real-world one: a two-hand grip is ONE HAFT PASSING THROUGH BOTH HANDS. Same two-pass
    /// shape as the fold: pass 1 measures each hand's live distance to the haft LINE every frame and finds the WORST
    /// frame (not the best — shooting the best frame is the false-green class this project has paid for repeatedly),
    /// pass 2 re-fires and shoots it from the gameplay cam and a CLOSE frontal cam. Self-asserts BOTH that the
    /// state-gated seat delta actually engaged in the shipped exe and that BOTH hands sit on the haft within the
    /// shared <see cref="TwoHandGripRead"/> caps.
    ///
    /// ALONG-HAFT REPORT (86cay4282 round 3 — REPORTED, NOT GATED). Round 2 passed this gate with the left hand
    /// clamped at the BUTT end of the haft, because <see cref="TwoHandGripRead.Pass"/> scores only each hand's
    /// PERPENDICULAR distance to the haft line — so a butt-end grip and a mid-haft grip score identically, and the
    /// Sponsor's soak defect ("how can i dial that the left hand is not on the bottom of the axe") was invisible to
    /// every gate. <see cref="TwoHandGripRead.Read.leftU"/>/<c>rightU</c> already carried the answer and were printed
    /// nowhere. They are now logged here (and drawn on the F9 panel), with the 0 = BUTT / 1 = HEAD convention and the
    /// off-the-end cases named. They are deliberately NOT added to the pass criteria this round, at the Sponsor's
    /// explicit call: the right window depends on which grip he settles on at the soak, so gating it now would gate
    /// against an invented threshold.
    /// </summary>
    public class SwingVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // A cone-explosion sends the mesh far off-spawn; a clean Generic bind keeps the mesh bounds within a few
        // units of the player root (the castaway is ~1.8u tall). 8u discriminates explosion-vs-clean unambiguously
        // (a cone blows past by thousands). Mirrors LocomotionHitReactVerifyCapture.ConeExplosionRadiusU.
        private const float ConeExplosionRadiusU = 8f;

        // === PICKAXE DEEP-FOLD PASS (86cav8xg9) ===
        // The pickaxe swing is a 5.20s clip at 1.5x playback ~= 3.47s; the fold peaks around t~0.56 (~1.95s in), so
        // the measure window must span well past it.
        private const float FoldWindowSec = 2.6f;
        // Live composed-pose ceiling for the torso lean off vertical. The authored raw clip measures 66.3deg and the
        // repaired clip 41deg (AttackClipPoseDiag); the axe swing's unflagged peak is 43.3deg. 50deg sits clear of
        // the repaired value (so real frame-timing jitter cannot flap the gate) yet far below the raw fold, so this
        // fires if the build ever plays the RAW clip again.
        private const float LiveFoldCeilingDeg = 50f;
        // Side-profile stand-off. The castaway is ~1.8u tall; 3u at 45deg FOV frames the whole body with headroom.
        private const float SideProfileDistU = 3f;

        // === MINE TWO-HAND GRIP PASS (86cay4282 round 2 — the Sponsor's DIRECTION REVERSAL) ===
        // Round 1 gated on the hands OPENING (min separation >= 1.20 SW), because it read the mine clip's locked-
        // together hands as the defect. The Sponsor reversed that premise — "we need to position the axe for a two
        // hand grip" — so close hands are now CORRECT and that assert is not merely obsolete, it is BACKWARDS: it
        // would red the very build he asked for. It is replaced by the quantity the reversed goal is defined by,
        // each hand's distance to the haft LINE (TwoHandGripRead — the same maths + the same caps the F9 panel
        // draws and the EditMode suite pins, so a gate, a panel and a test cannot disagree about what passes).
        // Separation is still LOGGED (it explains the residual) but is no longer a pass criterion.
        // Stand-off for the two-hand grip close-up. 1.6u at 45deg FOV frames chest-to-hands; the 3u whole-body
        // profile stand-off used for the FOLD read cannot resolve which hand is on the haft (Tess confirmed the
        // default gameplay frame renders the castaway at ~55x95 px — the reason the front-snap key exists too).
        private const float GripShotDistU = 1.6f;
        // Only frames at or above this eased seat weight are SCORED (and shot). See the note at the scoring loop:
        // the transition-paired gate engages before the seat has eased in, so the early frames legitimately show the
        // approved one-handed seat and must not be judged as a failed two-hand grip.
        private const float EngagedWeightFloor = 0.95f;
        // The hero held-tool object name (must match MovementCameraScene.HeroAxeObjectName — kept as a literal so
        // Runtime has no Editor-asm dependency, the same convention AxeNudgeTool follows).
        private const string HeroToolObjectName = "HeroAxe";

        // The 5 per-class swings, in WeaponClass order (mirror CastawayCharacter.WeaponClass*). Names drive the PNG.
        private static readonly (int weaponClass, string name)[] Swings =
        {
            (CastawayCharacter.WeaponClassAxe,     "axe"),
            (CastawayCharacter.WeaponClassPickaxe, "pickaxe"),
            (CastawayCharacter.WeaponClassDagger,  "dagger"),
            (CastawayCharacter.WeaponClassSpear,   "spear"),
            (CastawayCharacter.WeaponClassSword,   "sword"),
        };

        void Start()
        {
            if (HasArg("-verifySwings"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            NavMeshAgent agent = player != null ? player.GetComponent<NavMeshAgent>() : null;
            CastawayCharacter castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            Animator animator = castaway != null && castaway.ModelTransform != null
                ? castaway.ModelTransform.GetComponentInChildren<Animator>()
                : Object.FindAnyObjectByType<Animator>();
            SkinnedMeshRenderer smr = castaway != null && castaway.ModelTransform != null
                ? castaway.ModelTransform.GetComponentInChildren<SkinnedMeshRenderer>()
                : Object.FindAnyObjectByType<SkinnedMeshRenderer>();
            Transform playerRoot = player != null ? player.transform : (castaway != null ? castaway.transform : null);

            float t = 0f;
            while (t < 3f && (agent == null || !agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[SwingVerifyCapture] agent on NavMesh: " + (agent != null && agent.isOnNavMesh) +
                      " castaway=" + (castaway != null) + " animator=" + (animator != null) + " smr=" + (smr != null));
            for (int i = 0; i < 5; i++) yield return null;

            bool allRouted = castaway != null;
            float worstMeshGap = 0f;

            foreach (var (weaponClass, name) in Swings)
            {
                // Fire this class's swing through the production seam. Standing at spawn so the gameplay orbit cam
                // stays framed on the character (the swing frame must SHOW the motion, not empty terrain).
                bool routed = false;
                if (castaway != null)
                {
                    castaway.TriggerAttack(weaponClass, 1f);
                    routed = castaway.LastWeaponClass == weaponClass;
                }
                allRouted &= routed;
                Debug.Log($"[SwingVerifyCapture] fired swing class={weaponClass} ({name}) routed={routed} " +
                          $"(LastWeaponClass={(castaway != null ? castaway.LastWeaponClass : -1)})");

                // Let REAL frames advance the swing to ~mid-strike, then capture. Track the cone guard the whole window.
                float start = Time.time;
                bool shot = false;
                while (Time.time - start < 0.9f)
                {
                    worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                    if (!shot && Time.time - start > 0.28f)
                    {
                        ShotTo(Path.Combine(dir, "swing_" + name + ".png"));
                        shot = true;
                    }
                    yield return null;
                }
                if (!shot) ShotTo(Path.Combine(dir, "swing_" + name + ".png"));
                // Let the one-shot finish + return to idle before the next class (a clean start per swing).
                for (int i = 0; i < 18; i++) yield return null;
            }

            // ===== PICKAXE DEEP-FOLD PASS (86cav8xg9) =====
            float peakTilt = 0f;
            bool foldOk = true;
            // 86cay4282 round 2 — the two-hand-grip pass's readings, hoisted so the final verdict line carries them.
            float worstLeftHaft = -1f, worstRightHaft = -1f, minHandSep = float.MaxValue, peakSeatWeight = 0f;
            // 86cay4282 round 3 — the ALONG-HAFT position of each hand, REPORTED ONLY. See the note at the scoring
            // loop: this is deliberately NOT part of the pass criteria this round.
            float minLeftU = float.MaxValue, maxLeftU = float.MinValue;
            float minRightU = float.MaxValue, maxRightU = float.MinValue;
            bool gripOk = true;
            if (castaway != null && animator != null)
            {
                Transform hips = FindBone(animator.transform, "mixamorig:Hips");
                Transform head = FindBone(animator.transform, "mixamorig:Head");
                if (hips == null || head == null)
                {
                    Debug.LogWarning("[SwingVerifyCapture] fold pass SKIPPED — mixamorig:Hips/Head not found on the " +
                                     "live rig; the mine-pose evidence is MISSING from this run (do not read a PASS " +
                                     "here as proof the fold is fixed).");
                }
                else
                {
                    // pass 1 — MEASURE. Sample the live composed torso tilt every frame; remember when it peaks.
                    castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
                    float start = Time.time, peakAt = 0f;
                    while (Time.time - start < FoldWindowSec)
                    {
                        float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                        if (tilt > peakTilt) { peakTilt = tilt; peakAt = Time.time - start; }
                        worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                        yield return null;
                    }
                    Debug.Log($"[SwingVerifyCapture] pickaxe fold pass 1: LIVE peak torso tilt {peakTilt:F1}deg off " +
                              $"vertical at +{peakAt:F2}s (measured on the runtime skeleton, so it includes the " +
                              "Animator playback AND the CastawayArmPose composition)");
                    for (int i = 0; i < 18; i++) yield return null;

                    // pass 2 — SHOOT that moment, gameplay cam then side profile.
                    castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
                    start = Time.time;
                    while (Time.time - start < peakAt) yield return null;
                    ShotTo(Path.Combine(dir, "swing_pickaxe_fold.png"));
                    yield return null;
                    yield return SideProfileShot(Path.Combine(dir, "swing_pickaxe_fold_side.png"), hips, head,
                                                castaway.ModelTransform);

                    foldOk = peakTilt <= LiveFoldCeilingDeg;
                    Debug.Log($"[SwingVerifyCapture] pickaxe fold: peakTilt={peakTilt:F1}deg <= " +
                              $"{LiveFoldCeilingDeg}deg ceiling => foldOk={foldOk}. The RAW clip measured 66.3deg " +
                              "(authored) — above the ceiling means the repaired .anim is not the clip being played.");
                    for (int i = 0; i < 18; i++) yield return null;

                    // ===== MINE TWO-HAND GRIP PASS (86cay4282 round 2) =====
                    // Same two-pass shape as the fold, on the OTHER defect the Sponsor reported at the same soak
                    // ("swinging like he is handing the axe with both hands") — but from the REVERSED direction he
                    // then chose: the clip is authored two-handed and that is what he wants, so the question is
                    // whether the HAFT runs through both hands. The quantity is each hand's distance to the haft
                    // LINE, shoulder-width-normalised (scale-immune). Measured on the LIVE runtime skeleton so it
                    // includes the Animator playback AND the state-gated seat delta at its real eased weight: an
                    // editor SampleAnimation figure cannot prove the shipped exe engages the gate at all.
                    Transform lArm = FindBone(animator.transform, "mixamorig:LeftArm");
                    Transform rArm = FindBone(animator.transform, "mixamorig:RightArm");
                    Transform lHand = FindBone(animator.transform, "mixamorig:LeftHand");
                    Transform rHand = FindBone(animator.transform, "mixamorig:RightHand");
                    // Resolve the HERO tool rig BY NAME, not by FindAnyObjectByType: the latter returns an
                    // arbitrary instance if the scene ever carries a second HeldToolRig, and measuring a tool that
                    // is not the one in the hand would produce plausible-looking nonsense.
                    Transform heroTool = null;
                    foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                                           FindObjectsSortMode.None))
                        if (tr.name == HeroToolObjectName) { heroTool = tr; break; }
                    var heldRig = heroTool != null ? heroTool.GetComponent<HeldToolRig>() : null;

                    // ⚠ DISPLAY THE PICKAXE BEFORE JUDGING A PICKAXE SWING. The held mesh SYNCS TO THE BELT
                    // SELECTION (HeldWeaponCycleDebug.SelectionIndexFor), and this verify run selects nothing, so
                    // the hand shows the DEFAULT stone AXE while TriggerAttack plays the PICKAXE clip. The seat
                    // delta is fitted to the pickaxe's own haft + per-class holder dial, so scoring it against the
                    // axe mesh measures a combination that CANNOT occur in play (a mine swing requires a pickaxe
                    // selected) — the first run of this gate reported 1.589 SW for exactly that reason before the
                    // mesh was pinned. ShowWeaponForCaptureDebug is the existing capture-only forcing path
                    // (86cam9q5f, the -verifyHeldPickaxe idiom); it is not a gameplay path.
                    var cycle = heroTool != null ? heroTool.GetComponent<HeldWeaponCycleDebug>() : null;
                    if (cycle != null)
                    {
                        cycle.ShowWeaponForCaptureDebug(HeldWeaponCycleDebug.PickaxeStoneFamilyIndex);
                        for (int i = 0; i < 3; i++) yield return null;
                        Debug.Log($"[swing-twohand] displayed held weapon forced to " +
                                  $"'{cycle.CurrentLabel}' (index {cycle.CurrentIndex}, expected " +
                                  $"{HeldWeaponCycleDebug.PickaxeStoneFamilyIndex}) — the mine swing is only " +
                                  "reachable with a pickaxe selected, so this is the combination that ships.");
                    }
                    else
                    {
                        Debug.LogWarning("[swing-twohand] no HeldWeaponCycleDebug on '" + HeroToolObjectName +
                                         "' — cannot force the pickaxe mesh; the grip figures below would be " +
                                         "measured against whatever mesh happens to be displayed. Treat them as " +
                                         "UNRELIABLE rather than as a verdict.");
                    }

                    if (lArm == null || rArm == null || lHand == null || rHand == null || heldRig == null)
                    {
                        Debug.LogWarning("[SwingVerifyCapture] two-hand grip pass SKIPPED — arm/hand bones (" +
                                         (lArm != null) + "/" + (rArm != null) + "/" + (lHand != null) + "/" +
                                         (rHand != null) + ") or the HeldToolRig (" + (heldRig != null) + ") not " +
                                         "found on the live rig; the two-hand-grip evidence is MISSING from this " +
                                         "run (do NOT read a PASS here as proof the haft sits in both hands).");
                    }
                    else
                    {
                        // pass 1 — MEASURE, and find the WORST frame for the two-hand read (the largest left-hand
                        // gap to the haft). The worst frame is what gets shot: a capture of the best frame is the
                        // false-green class this project has paid for seven times.
                        castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
                        start = Time.time;
                        float worstAt = 0f, angAtWorst = 0f, weightAtWorst = 0f, rAtWorst = 0f;
                        int easingFrames = 0;
                        while (Time.time - start < FoldWindowSec)
                        {
                            if (heldRig.TryGetHaftSegment(out Vector3 gripW, out Vector3 headW))
                            {
                                var read = TwoHandGripRead.Measure(lArm.position, rArm.position,
                                                                   lHand.position, rHand.position, gripW, headW);
                                float w = heldRig.MineSeatWeight;
                                peakSeatWeight = Mathf.Max(peakSeatWeight, w);
                                if (read.valid)
                                {
                                    minHandSep = Mathf.Min(minHandSep, read.handSepSW);
                                    // JUDGE ONLY FULLY-ENGAGED FRAMES. The gate is transition-PAIRED, so it goes
                                    // true on the FIRST frame of the AnyState->AttackPickaxe crossfade — where the
                                    // arms are still a BLEND of idle and the mine pose and the eased seat weight is
                                    // still ~0, i.e. the tool is CORRECTLY still at the approved one-handed seat.
                                    // Scoring those frames measured a worst-case ~1.45 SW that has nothing to do
                                    // with the fix (found by instrumenting the PlayMode fixture, which hit exactly
                                    // this and reported 1.451 before the window was corrected). The ~0.25 s ease-in
                                    // is a deliberate hand-over, not a defect to gate on.
                                    if (w < EngagedWeightFloor) { easingFrames++; }
                                    else
                                    {
                                        worstRightHaft = Mathf.Max(worstRightHaft, read.rightHaftSW);
                                        // 86cay4282 ROUND 3 — WHERE ALONG THE HAFT each hand sits, tracked over the
                                        // whole engaged window. ⚠ REPORT ONLY: deliberately NOT folded into gripOk this
                                        // round, at the Sponsor's explicit call — the right pass window depends on which
                                        // grip he settles on at the soak, and a threshold invented for him now would
                                        // gate the build against a guess. Round 2 shipped the inverse mistake (the
                                        // quantity computed but never surfaced at all), so it is surfaced everywhere —
                                        // panel, log, gate — and gated nowhere.
                                        minLeftU = Mathf.Min(minLeftU, read.leftU);
                                        maxLeftU = Mathf.Max(maxLeftU, read.leftU);
                                        minRightU = Mathf.Min(minRightU, read.rightU);
                                        maxRightU = Mathf.Max(maxRightU, read.rightU);
                                        if (read.leftHaftSW > worstLeftHaft)
                                        {
                                            worstLeftHaft = read.leftHaftSW;
                                            worstAt = Time.time - start;
                                            angAtWorst = read.toolVsHandLineDeg;
                                            rAtWorst = read.rightHaftSW;
                                            weightAtWorst = w;
                                        }
                                    }
                                }
                            }
                            worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                            yield return null;
                        }
                        Debug.Log($"[swing-twohand] scored frames at seat weight >= {EngagedWeightFloor:F2}; " +
                                  $"{easingFrames} frames skipped while the seat eased in (the deliberate hand-over " +
                                  "window — the tool is still at the approved one-handed seat there).");
                        // 86cay4282 ROUND 3 — the ALONG-HAFT report. Its own line, with the convention spelled out and
                        // the off-the-end cases called by name, because this is the number the Sponsor's round-2 soak
                        // defect ("the left hand is on the bottom of the axe") actually lives in — and `Pass()` scores
                        // only perpendicular distance, so a butt-end grip and a mid-haft grip are identical to it.
                        string uVerdict =
                            minLeftU == float.MaxValue ? "no engaged frames — nothing measured"
                            : minLeftU < 0f ? "LEFT HAND IS OFF THE BUTT END of the haft (u<0)"
                            : maxRightU > 1f ? "RIGHT HAND IS OFF THE HEAD END of the haft (u>1)"
                            : minLeftU < 0.10f ? "left hand is CLAMPED AT THE BUTT (u<0.10) — the round-2 soak defect"
                            : "both hands are ON the haft between its ends";
                        Debug.Log($"[swing-twohand] ALONG-HAFT (0 = BUTT/grip end, 1 = HEAD end): left hand u " +
                                  $"{(minLeftU == float.MaxValue ? -9f : minLeftU):F2}..{(maxLeftU == float.MinValue ? -9f : maxLeftU):F2}, " +
                                  $"right hand u {(minRightU == float.MaxValue ? -9f : minRightU):F2}.." +
                                  $"{(maxRightU == float.MinValue ? -9f : maxRightU):F2} => {uVerdict}. " +
                                  "REPORT ONLY — deliberately NOT a pass criterion this round (the Sponsor dials the " +
                                  "grip first; a window invented before he picks one would gate against a guess).");
                        Debug.Log($"[swing-twohand] pass 1: WORST left-hand-to-haft {worstLeftHaft:F3} SW at " +
                                  $"+{worstAt:F2}s (right hand {rAtWorst:F3} SW there, tool {angAtWorst:F1}deg off " +
                                  $"the hand line, seat weight {weightAtWorst:F2}); worst right-hand-to-haft over " +
                                  $"the swing {worstRightHaft:F3} SW; peak seat weight {peakSeatWeight:F2}; min hand " +
                                  $"separation {minHandSep:F3} SW (logged for context — NOT a pass criterion since " +
                                  "the Sponsor's reversal made close hands correct).");
                        for (int i = 0; i < 18; i++) yield return null;

                        // pass 2 — SHOOT that moment: gameplay cam (what he actually sees) + a CLOSE FRONTAL cam
                        // (hand-on-haft is a LATERAL read; a sagittal shot hides it because the near hand occludes
                        // the far one, and the gameplay frame is too small to resolve it at all).
                        castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
                        start = Time.time;
                        while (Time.time - start < worstAt) yield return null;
                        ShotTo(Path.Combine(dir, "swing_pickaxe_twohand.png"));
                        yield return null;
                        yield return FrontalShot(Path.Combine(dir, "swing_pickaxe_twohand_front.png"), hips, head,
                                                 castaway.ModelTransform, GripShotDistU);

                        // The gate must have ENGAGED at all — a seat delta that never fires would otherwise pass
                        // silently while the defect ships (the "wired but conditionally inert" family,
                        // procedural-animation-verbs.md §Debug-instrument caveat).
                        bool engaged = peakSeatWeight > 0.5f;
                        bool leftOn = worstLeftHaft >= 0f && worstLeftHaft <= TwoHandGripRead.LeftHaftPassSW;
                        bool rightOn = worstRightHaft >= 0f && worstRightHaft <= TwoHandGripRead.RightHaftPassSW;
                        gripOk = engaged && leftOn && rightOn;
                        Debug.Log($"[swing-twohand] engaged={engaged} (peak seat weight {peakSeatWeight:F2} > 0.50) " +
                                  $"leftOnHaft={leftOn} ({worstLeftHaft:F3} <= {TwoHandGripRead.LeftHaftPassSW:F2} SW) " +
                                  $"rightOnHaft={rightOn} ({worstRightHaft:F3} <= {TwoHandGripRead.RightHaftPassSW:F2} " +
                                  $"SW) => gripOk={gripOk}. A FALSE 'engaged' means the AttackPickaxe gate never " +
                                  "fired in the shipped exe; a false 'leftOnHaft' means the seat delta is reverted, " +
                                  "inverted or too small (pre-fix measured 1.476 SW); a false 'rightOnHaft' means " +
                                  "the delta pulled the haft out of the hand it is actually seated in.");
                    }
                }
            }

            yield return new WaitForSeconds(0.4f);

            bool meshStayed = smr != null && worstMeshGap <= ConeExplosionRadiusU;
            bool pass = allRouted && meshStayed && foldOk && gripOk;
            Debug.Log($"[SwingVerifyCapture] verification complete -> {dir} allRouted={allRouted} " +
                      $"worstMeshGap={worstMeshGap:F2}u (<= {ConeExplosionRadiusU} = mesh stayed at the player, NO " +
                      $"cone-explosion — the Generic-rig bind, 86ca8rdkp) meshStayed={meshStayed} " +
                      $"pickaxePeakTilt={peakTilt:F1}deg foldOk={foldOk} " +
                      $"worstLeftHaft={worstLeftHaft:F3}SW worstRightHaft={worstRightHaft:F3}SW " +
                      $"minHandSep={(minHandSep == float.MaxValue ? -1f : minHandSep):F3}SW " +
                      // 86cay4282 round 3 — the along-haft position rides the ONE-LINE verdict too, so a reviewer
                      // scanning only the summary still sees where on the haft the hands landed. NOT in `pass`.
                      $"leftU={(minLeftU == float.MaxValue ? -9f : minLeftU):F2}..{(maxLeftU == float.MinValue ? -9f : maxLeftU):F2} " +
                      $"rightU={(minRightU == float.MaxValue ? -9f : minRightU):F2}..{(maxRightU == float.MinValue ? -9f : maxRightU):F2} " +
                      $"(u REPORT-ONLY, not gated) " +
                      $"peakSeatWeight={peakSeatWeight:F2} gripOk={gripOk} => PASS={pass}");
            Application.Quit(pass ? 0 : 1);
        }

        /// <summary>
        /// One SIDE-PROFILE frame of the current pose — the angle an upright-vs-folded-over read is actually
        /// visible from (lowpoly-quality.md §0). Follows the #223 camera-race discipline: every other camera is
        /// disabled, this one takes depth 100, and the whole camera roster is logged — two enabled cameras at equal
        /// depth have UNDEFINED render order and the capture then intermittently samples the wrong one.
        /// </summary>
        private IEnumerator SideProfileShot(string file, Transform hips, Transform head, Transform facing)
        {
            // Stand off along the character's own RIGHT so the sagittal plane faces the lens (a true side profile).
            // The axis comes from the FACING-CARRYING model transform (CastawayCharacter yaws the _model child:
            // "the visual owns facing", unity-conventions.md §FBX/rigs) — NOT from a pelvis BONE axis, whose local
            // frame on an imported Mixamo rig is arbitrary and would aim the camera at a guessed angle.
            yield return ProfileShot(file, hips, head, facing != null ? facing.right : Vector3.right,
                                     SideProfileDistU);
        }

        /// <summary>
        /// 86cay4282 — one FRONTAL frame. Deliberately NOT the sagittal profile: the quantity judged here is the
        /// LATERAL SEPARATION of the two hands, which the sagittal plane hides almost entirely (the near hand
        /// occludes the far one). The "shoot a true 90deg sagittal profile" rule (unity-conventions.md, PR #337 QA)
        /// exists for LEAN / FOLD / silhouette; the principle it encodes is "shoot the plane the quantity lives in",
        /// and for a two-handed-vs-one-handed grip read that plane is the FRONTAL one. #337's side-profile fold shot
        /// is untouched and still taken — this is an ADDITIONAL angle, never a replacement.
        /// </summary>
        private IEnumerator FrontalShot(string file, Transform hips, Transform head, Transform facing, float distU)
        {
            yield return ProfileShot(file, hips, head, facing != null ? facing.forward : Vector3.forward, distU);
        }

        /// <summary>Shared implementation: a level, chest-height shot from a stand-off along the given world axis.
        /// A raised/angled shot flattens the very geometry these captures exist to judge (the pond top-down lesson).
        /// The stand-off is a PARAMETER, not a constant: the fold read needs the whole body in frame (3u) while the
        /// two-hand grip read needs the hands legible (1.6u), and using one distance for both makes one of the two
        /// captures unable to show its own subject.</summary>
        private IEnumerator ProfileShot(string file, Transform hips, Transform head, Vector3 axis, float distU)
        {
            var wasEnabled = new System.Collections.Generic.List<Camera>();
            foreach (var c in Camera.allCameras)
                if (c.enabled) { wasEnabled.Add(c); Debug.Log($"[swings-cam-roster] {c.name} depth={c.depth}"); }

            var go = new GameObject("__swingSideCam");
            var cam = go.AddComponent<Camera>();
            cam.depth = 100f;
            cam.fieldOfView = 45f;
            Vector3 centre = (hips.position + head.position) * 0.5f;
            Vector3 side = Vector3.ProjectOnPlane(axis, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            go.transform.position = centre + side.normalized * distU + Vector3.up * 0.1f;
            go.transform.LookAt(centre);
            foreach (var c in wasEnabled) c.enabled = false;
            yield return null;
            ShotTo(file);
            yield return null;
            foreach (var c in wasEnabled) c.enabled = true;
            Object.Destroy(go);
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // The skinned mesh's world-bounds center distance from the player root. A clean bind keeps this small; a
        // cone-explosion makes it huge. Returns 0 when no SMR (degenerate rig — the boot capture covers no-mesh).
        private static float MeshGap(SkinnedMeshRenderer smr, Transform playerRoot)
        {
            if (smr == null || playerRoot == null) return 0f;
            return Vector3.Distance(smr.bounds.center, playerRoot.position);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SwingVerifyCapture] wrote " + file);
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", subDir)
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", subDir);
            return Path.GetFullPath(baseDir);
        }

        private bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
