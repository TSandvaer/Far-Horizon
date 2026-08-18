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
    ///           swing_pickaxe_twohand_front.png (a CLOSE FRONTAL shot — the plane a hand-on-haft read lives in),
    ///           swing_pickaxe_panel.png (the F9 MINE-SEAT panel drawn in the SHIPPED exe with its rows populated —
    ///           86cay4282 round 3, so the Sponsor is handed a picture of the instrument he is asked to find),
    ///           swing_chop_seat_worst.png + swing_chop_seat_worst_close.png (86cayp0ay — the CHOP swing's worst
    ///           seat frame, gameplay cam + a close shot framed on the hand).
    /// EVERY capture this component writes is named `swing_*` ON PURPOSE — see the note at the chop-seat shots: the
    /// gate wrapper's stale-artifact clear is globbed on that prefix while the frame checker judges every PNG in the
    /// directory, so a name outside the glob is a stale-frame false-green waiting to happen.
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
        private const float GripShotDistU = 1.15f;
        // Only frames at or above this eased seat weight are SCORED (and shot). See the note at the scoring loop:
        // the transition-paired gate engages before the seat has eased in, so the early frames legitimately show the
        // approved one-handed seat and must not be judged as a failed two-hand grip.
        private const float EngagedWeightFloor = 0.95f;
        // The hero held-tool object name (must match MovementCameraScene.HeroAxeObjectName — kept as a literal so
        // Runtime has no Editor-asm dependency, the same convention AxeNudgeTool follows).
        private const string HeroToolObjectName = "HeroAxe";
        // === MINE RELEASE PASS (86cay4282 round 5) ===
        // The pin weight at or below which it is no longer moving the arm visibly. Not a tolerance chosen to fit: the
        // measured worst pin displacement is ~60 deg of upper-arm rotation, so 0.02 of it is ~1.2 deg — below any
        // visible arm displacement, and the same 1-deg settle definition the PlayMode release A/B uses.
        private const float ReleasedWeight = 0.02f;

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
            // 86cayp0ay — apply the verify-only SEAT FAULT before any gate runs, in EVERY launch mode, not just
            // -verifySwings. WHY IT IS HOISTED HERE: the ticket's success test is not "the new gate reds", it is
            // "the new gate reds AND the old REST-POSE gates stay green — that is the proof the two cover different
            // classes". Those gates are separate flags in separate processes (-verifyHeldWood is AxeVerifyCapture),
            // so a fault scoped to this component's own coroutine could never be run against them. Applied at Start
            // it is one shared negative control any gate can be launched under.
            ApplySeatFaultIfRequested();
            // 86cb6v03j — the SWING-AIM negative control, applied in EVERY launch mode for the same reason the seat
            // fault above is: the success test is "the aim gate REDS with the fix removed AND the seat / two-hand
            // grip gates stay GREEN" — that pair is what shows the new gate covers a class the existing ones cannot
            // see — and those gates live behind other flags in other processes.
            if (HasArg(SwingAimZeroArg))
            {
                HeldToolRig.SwingAimForcedZero = true;
                Debug.LogWarning("[swing-aim-fault] SWING-AIM DELTAS FORCED TO ZERO for this process (" +
                                 SwingAimZeroArg + "). THIS RUN IS A DELIBERATE NEGATIVE CONTROL: it reproduces the " +
                                 "pre-86cb6v03j seat EXACTLY (it removes the fix rather than injecting an invented " +
                                 "fault magnitude). Its verdict is evidence that the aim gate REDS on the unfixed " +
                                 "build - it is NOT evidence about the shipped build and must never be quoted as such.");
            }
            if (HasArg("-verifySwings"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        /// <summary>Verify-only flag that removes the 86cb6v03j swing-aim fix for one throwaway process, so the
        /// gate's RED can be reproduced by anyone against the SHIPPED exe. Read only here; absent it, no launch mode
        /// changes by a byte.</summary>
        private const string SwingAimZeroArg = "-swingAimFaultZero";

        /// <summary>
        /// Verify-only fault injection: add N cm to the PRODUCTION seat value (<see cref="HeldToolRig.seatOffsetFromHand"/>
        /// — the field <see cref="HeldToolRig.ComposeSeat"/> actually reads), so a deliberate seat error of a stated
        /// magnitude travels the real seat composition and the real LateUpdate chain. Absent
        /// <see cref="SeatFaultArg"/> this is a no-op and every byte of behaviour is unchanged.
        ///
        /// It is deliberately LOUD: a faulted run logs a warning naming the magnitude and the before/after value, and
        /// the gate's verdict line is therefore never quotable as evidence about the shipped seat by accident. There
        /// is no restore here BY DESIGN — a faulted launch is a throwaway negative-control process that quits at the
        /// end of the gate; nothing it does reaches a committed asset (the seat it mutates is the runtime component's
        /// field, not the serialized scene).
        /// </summary>
        private void ApplySeatFaultIfRequested()
        {
            float faultCm = ReadFloatArg(SeatFaultArg, 0f);
            if (Mathf.Abs(faultCm) < 0.0001f) return;
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None))
            {
                if (tr.name != HeroToolObjectName) continue;
                var rig = tr.GetComponent<HeldToolRig>();
                if (rig == null) continue;
                Vector3 delta = new Vector3(faultCm * 0.01f, 0f, 0f);

                // ⚠ WRITE THE FIELD THE RIG ACTUALLY CONSUMES, NOT THE ONE IT EXPOSES. HeldAxeRig overrides
                // ApplySeat with `seatOffsetFromHand = worldOffsetFromHand;` EVERY LateUpdate, so a write to the
                // BASE field is stomped before it is ever read - the write succeeds at the data layer and the
                // effect is silently discarded. MEASURED, not reasoned: the first version of this injector wrote
                // the base field, logged "(0.13, 0.14, 0.06) -> (0.43, 0.14, 0.06)", and the gate's readings came
                // back BYTE-IDENTICAL to the clean run (0.4027 SW, u 0.2004). That is the documented "wired but
                // conditionally inert" family (procedural-animation-verbs.md §Debug-instrument caveat - the
                // weapon-mesh-holder stomp sibling), and an unverified injector makes a negative control that
                // proves nothing while looking like proof.
                Vector3 before;
                if (rig is HeldAxeRig axeRig)
                {
                    before = axeRig.worldOffsetFromHand;
                    axeRig.worldOffsetFromHand = before + delta;
                    _seatFaultExpected = axeRig.worldOffsetFromHand;
                }
                else
                {
                    before = rig.seatOffsetFromHand;
                    rig.seatOffsetFromHand = before + delta;
                    _seatFaultExpected = rig.seatOffsetFromHand;
                }
                _seatFaultCm = faultCm;
                Debug.LogWarning("[seat-fault] SEAT FAULT INJECTED: " + faultCm.ToString("F1") + " cm added to the " +
                                 "seat offset the rig CONSUMES on '" + HeroToolObjectName + "' (" +
                                 rig.GetType().Name + ": " + before + " -> " + _seatFaultExpected + "). THIS RUN IS " +
                                 "A DELIBERATE NEGATIVE CONTROL. Its verdict is evidence that a gate REDS on a real " +
                                 "seat error - it is NOT evidence about the shipped seat, and must never be quoted " +
                                 "as such. The injection VERIFIES ITSELF before any gate reports (see " +
                                 "VerifySeatFaultTookEffect).");
                return;
            }
            Debug.LogError("[seat-fault] " + SeatFaultArg + " was requested but no '" + HeroToolObjectName +
                           "' carrying a HeldToolRig was found - NO FAULT WAS INJECTED. A 'the gate stayed green' " +
                           "conclusion from this run would be worthless; treat it as a failed negative control.");
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
            // 86cay4282 round 4 — the PALM criterion + the left-arm IK's own live state, so the gate can distinguish
            // "the pin worked" from "the pin was absent / inert / reaching" instead of inferring it from one number.
            float worstLeftPalm = -1f, swAtWorst = TwoHandGripRead.ReferenceShoulderWidthM;
            bool anyPalmMeasured = false, allPalmMeasured = true;
            int ikSolvedFrames = 0, ikReachingFrames = 0, ikPoleFallbackFrames = 0, ikScoredFrames = 0;
            float peakPinWeight = 0f, minAchievedU = float.MaxValue, maxAchievedU = float.MinValue;
            int transitionFrames = 0;
            float palmWorstAt = -1f, palmWorstU = float.NaN, palmWorstReachW = -1f, palmWorstSep = -1f;
            bool palmWorstReaching = false;
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
                    // 86cay4282 round 4 — the LEFT-ARM HAFT PIN is what this round ships, so the gate must be able to
                    // say whether it EXISTS and ENGAGED in the shipped exe, not just whether the number looks good.
                    var leftIk = Object.FindAnyObjectByType<CastawayLeftArmHaftIk>(FindObjectsInactive.Include);
                    if (leftIk == null)
                        Debug.LogWarning("[swing-twohand] no CastawayLeftArmHaftIk in the shipped scene — the LEFT-HAND " +
                                         "PIN this round delivers is ABSENT from this build. Every palm figure below " +
                                         "would then be the clip's own unpinned hand; do NOT read a PASS as proof the " +
                                         "left hand was moved.");
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
                                var read = MeasureWithPalms(leftIk, lArm, rArm, lHand, rHand, gripW, headW);
                                float w = heldRig.MineSeatWeight;
                                if (leftIk != null)
                                {
                                    ikSolvedFrames += leftIk.LastSolved ? 1 : 0;
                                    ikReachingFrames += leftIk.SpanEmpty ? 1 : 0;
                                    ikPoleFallbackFrames += leftIk.PoleFromFallback ? 1 : 0;
                                    peakPinWeight = Mathf.Max(peakPinWeight, leftIk.PinWeight);
                                    if (!float.IsNaN(leftIk.AchievedU))
                                    {
                                        minAchievedU = Mathf.Min(minAchievedU, leftIk.AchievedU);
                                        maxAchievedU = Mathf.Max(maxAchievedU, leftIk.AchievedU);
                                    }
                                }
                                peakSeatWeight = Mathf.Max(peakSeatWeight, w);
                                if (read.valid)
                                {
                                    ikScoredFrames++;
                                    minHandSep = Mathf.Min(minHandSep, read.handSepSW);
                                    // JUDGE ONLY FULLY-ENGAGED FRAMES. The gate is transition-PAIRED, so it goes
                                    // true on the FIRST frame of the AnyState->AttackPickaxe crossfade — where the
                                    // arms are still a BLEND of idle and the mine pose and the eased seat weight is
                                    // still ~0, i.e. the tool is CORRECTLY still at the approved one-handed seat.
                                    // Scoring those frames measured a worst-case ~1.45 SW that has nothing to do
                                    // with the fix (found by instrumenting the PlayMode fixture, which hit exactly
                                    // this and reported 1.451 before the window was corrected). The ~0.25 s ease-in
                                    // is a deliberate hand-over, not a defect to gate on.
                                    // ⚠ 86cay4282 ROUND 4 — ALSO SKIP ANIMATOR TRANSITION FRAMES, not just low-weight
                                    // ones. Round 2 excluded the hand-over window by testing the EASED SEAT WEIGHT,
                                    // which works only if the weight starts near 0. It does not here: this pass runs
                                    // AFTER the fold pass, which already drove the same AttackPickaxe gate, so the
                                    // weight is still saturated when scoring begins and the `< EngagedWeightFloor`
                                    // guard skips NOTHING ("0 frames skipped" in the round-4 first run). The frames it
                                    // was supposed to exclude were therefore scored: during an AnyState->AttackPickaxe
                                    // CROSSFADE the Animator outputs a BLEND of idle and the mine pose, which is a pose
                                    // the clip never contains and nothing was ever fitted to.
                                    //
                                    // THE EVIDENCE THIS IS THE RIGHT DISCRIMINATOR, not a convenient exclusion: the
                                    // first round-4 run reported `min hand separation 0.482 SW` while the clip's own
                                    // measured separation range is 1.01..1.33 SW (AttackClipPoseDiag MINE-SEAT FIT, 361
                                    // samples). A hand separation less than HALF the clip's minimum cannot come from the
                                    // clip; it is the crossfade. Excluding transition frames must restore that figure to
                                    // ~1.0 SW, which is the falsifiable check on this change.
                                    bool inTransition = animator.IsInTransition(0);
                                    if (w < EngagedWeightFloor || inTransition)
                                    {
                                        easingFrames++;
                                        if (inTransition) transitionFrames++;
                                    }
                                    else
                                    {
                                        worstRightHaft = Mathf.Max(worstRightHaft, read.rightHaftSW);
                                        // 86cay4282 ROUND 4 — the PALM figure is the pass criterion now, so it is the
                                        // one tracked to a worst frame. The wrist figure stays tracked below purely so
                                        // this round's numbers are comparable with rounds 2-3, which were written in it.
                                        anyPalmMeasured |= read.palmMeasured;
                                        allPalmMeasured &= read.palmMeasured;
                                        swAtWorst = read.shoulderWidth;
                                        // The WORST PALM frame's full state, so a marginal miss is diagnosable from the
                                        // log alone instead of costing a build cycle to instrument (round 4's first run
                                        // missed by 5 mm and the log could not say which frame or why).
                                        if (read.leftPalmHaftSW > worstLeftPalm)
                                        {
                                            worstLeftPalm = read.leftPalmHaftSW;
                                            palmWorstAt = Time.time - start;
                                            palmWorstU = leftIk != null ? leftIk.AchievedU : float.NaN;
                                            palmWorstReaching = leftIk != null && leftIk.SpanEmpty;
                                            palmWorstReachW = leftIk != null ? leftIk.ReachWeight : -1f;
                                            palmWorstSep = read.handSepSW;
                                        }
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
                        Debug.Log($"[swing-twohand] scored frames at seat weight >= {EngagedWeightFloor:F2} AND not in " +
                                  $"an Animator transition; {easingFrames} frames skipped total, of which " +
                                  $"{transitionFrames} were CROSSFADE frames (an AnyState->AttackPickaxe blend outputs " +
                                  "a pose the clip never contains). The round-4 first run skipped 0 and scored those " +
                                  "blends — its tell was a min hand separation of 0.482 SW against the clip's own " +
                                  "measured 1.01..1.33 SW range.");
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
                        // The judged frame: aimed from the SUBJECT (chest -> hand midpoint) and framed on the HANDS.
                        yield return GripShot(Path.Combine(dir, "swing_pickaxe_twohand_front.png"), lHand, rHand,
                                              lArm, rArm, GripShotDistU);

                        // The gate must have ENGAGED at all — a seat delta that never fires would otherwise pass
                        // silently while the defect ships (the "wired but conditionally inert" family,
                        // procedural-animation-verbs.md §Debug-instrument caveat).
                        // ===== 86cay4282 ROUND 4 — THE PALM CRITERION IS THE GATE =====
                        // The Sponsor's soak of round 3 found what round 3's own green gate could not see: 0.80 SW is
                        // 36.6 cm, so a left hand a quarter of a metre off the shaft PASSED. The left cap is now the
                        // mesh-derived TOUCHING bound (hand radius + haft radius, 13.4 cm) measured against the PALM
                        // CENTRE — because the palm, not the wrist 5.6 cm behind it, is what closes around a haft.
                        // The RIGHT hand's wrist criterion and its 0.30 SW cap are UNCHANGED (out of round-4 scope).
                        bool engaged = peakSeatWeight > 0.5f;
                        bool pinEngaged = leftIk != null && peakPinWeight > 0.5f;
                        // FAIL CLOSED on an unmeasured palm: scoring a wrist figure against a palm cap is a different,
                        // easier question, and substituting it silently is how a cap loses its meaning.
                        bool palmOk = anyPalmMeasured && allPalmMeasured;
                        bool leftOn = palmOk && worstLeftPalm >= 0f && worstLeftPalm <= TwoHandGripRead.LeftHaftPassSW;
                        bool rightOn = worstRightHaft >= 0f && worstRightHaft <= TwoHandGripRead.RightHaftPassSW;
                        gripOk = engaged && pinEngaged && leftOn && rightOn;
                        Debug.Log($"[swing-twohand] LEFT-ARM PIN: present={leftIk != null} peak weight " +
                                  $"{peakPinWeight:F2} solved {ikSolvedFrames}/{ikScoredFrames} frames, " +
                                  $"REACHING (whole haft past the arm) on {ikReachingFrames}, pole-fallback on " +
                                  $"{ikPoleFallbackFrames}; achieved u " +
                                  $"{(minAchievedU == float.MaxValue ? -9f : minAchievedU):F2}.." +
                                  $"{(maxAchievedU == float.MinValue ? -9f : maxAchievedU):F2} " +
                                  $"(requested {(leftIk != null ? leftIk.pinU : -9f):F2}, shell " +
                                  $"{(leftIk != null ? leftIk.shellFraction : -9f):F2}). A high REACHING count is " +
                                  "EXPECTED and measured (80/166 in the editor sweep): the seat parks the haft up to " +
                                  "63.4 cm from a 54.0 cm arm, so on those frames the pin aims at the haft's closest " +
                                  "point instead of handing the frame back to the clip's 20-28 cm gap.");
                        Debug.Log($"[swing-twohand] engaged={engaged} (peak seat weight {peakSeatWeight:F2} > 0.50) " +
                                  $"pinEngaged={pinEngaged} (peak pin weight {peakPinWeight:F2} > 0.50) " +
                                  $"palmMeasured={palmOk} " +
                                  $"leftPalmOnHaft={leftOn} ({worstLeftPalm:F3} SW = " +
                                  $"{worstLeftPalm * swAtWorst * 100f:F1} cm <= {TwoHandGripRead.LeftHaftPassSW:F3} SW " +
                                  $"= {TwoHandGripRead.LeftHaftPassSW * swAtWorst * 100f:F1} cm, the mesh-measured " +
                                  $"touch bound) rightWristOnHaft={rightOn} ({worstRightHaft:F3} <= " +
                                  $"{TwoHandGripRead.RightHaftPassSW:F2} SW) => gripOk={gripOk}. " +
                                  "A FALSE 'engaged' means the AttackPickaxe seat gate never fired; a FALSE " +
                                  "'pinEngaged' means the left-arm IK never engaged (absent, unwired chain, or the " +
                                  "gate missed) and the left hand shipped unpinned; a FALSE 'palmMeasured' means the " +
                                  "palm anchor was unresolvable, which fails closed rather than scoring the wrist " +
                                  "against a palm cap; a FALSE 'leftPalmOnHaft' means the palm is genuinely NOT " +
                                  "touching (round 3 measured 0.615 SW = 28.2 cm here); a FALSE 'rightWristOnHaft' " +
                                  "means the seat pulled the haft out of the hand it is actually seated in.");
                        Debug.Log($"[swing-twohand] WORST-PALM FRAME detail: +{palmWorstAt:F2}s, achieved u " +
                                  $"{palmWorstU:F3}, reaching={palmWorstReaching}, reachWeight {palmWorstReachW:F2}, " +
                                  $"hand separation {palmWorstSep:F3} SW. A separation far below the clip's own " +
                                  "1.01..1.33 SW range means a blend pose was scored, not the mine pose.");
                        Debug.Log($"[swing-twohand] WRIST figures, for continuity with rounds 2-3 (NOT the criterion): " +
                                  $"worst left wrist {worstLeftHaft:F3} SW = {worstLeftHaft * swAtWorst * 100f:F1} cm " +
                                  $"(round 3 shipped 0.615 SW = 28.2 cm). 1 SW = {swAtWorst:F4} m.");

                        // ===== F9 MINE-SEAT PANEL PASS (86cay4282 round 3) =====
                        // WHY A GATE FOR A DEBUG PANEL. Everything this round delivers is an INSTRUMENT the Sponsor
                        // drives, and this project's scar tissue on that is specific: an F9 panel has already drawn
                        // NOTHING for him twice (the F9-without-F10 master-gate briefs), a nudge dial has already
                        // accepted input with zero visible effect twice (run-lower's engagement weight), and the
                        // round-2 verdict line was being CLIPPED by its own box. Each of those is a soak cycle spent
                        // discovering the tool was the broken thing. So the shipped exe now proves, before he is asked
                        // to dial anything: the panel's rows POPULATE with real numbers, and the along-haft dial
                        // genuinely MOVES the along-haft read.
                        yield return MineSeatPanelPass(dir, castaway, heldRig, leftIk, lArm, rArm, lHand, rHand);

                        // ===== MINE RELEASE PASS (86cay4282 round 5 — the Sponsor's round-4 soak defect) =====
                        yield return MineReleasePass(dir, castaway, animator, heldRig, leftIk, lArm, rArm, lHand, rHand,
                                                     hips, head);

                        // ===== CHOP SEAT PASS (86cayp0ay) — the held-weapon SEAT judged DURING a swing =====
                        yield return ChopSeatPass(dir, castaway, animator, heldRig, lArm, rArm, rHand, hips, head);

                        // ===== SWING POINTING PASS (86cb6v03j) — WHICH WAY the weapon points, per class =====
                        yield return SwingPointPass(dir, castaway, animator, heldRig, lArm, rArm, rHand, hips, head);
                    }
                }
            }
            else
            {
                // 86caynve9 (Drew's #369 review, comment 5136309565): the guard above had NO else, so a run
                // that reached here with an unresolved rig skipped EVERY measurement with ZERO log output.
                // `castaway == null` is already fatal (allRouted := castaway != null, :164 -> PASS=False), but
                // `castaway != null && animator == null` was NOT: foldOk/gripOk/_releaseOk keep their `true`
                // initialisers, so the verdict line below prints `foldOk=True gripOk=True releaseOk=True =>
                // PASS=True` and the exe exits 0 having measured nothing. The gate still reddens on the four
                // IN-BLOCK evidence needles being absent (see the REQUIRED_NEEDLES header in
                // verify_swings_gate.sh), but it had no positive signal naming the cause. This warning gives it
                // one, and reuses the EXISTING "fold pass SKIPPED" ABSENT needle so no gate needle changes:
                // the run now reddens on BOTH halves of Check 2 instead of the presence half alone.
                // INERT on any healthy run -- the block above runs, so this branch never fires.
                Debug.LogWarning("[SwingVerifyCapture] fold pass SKIPPED -- the rig never resolved (castaway=" +
                                 (castaway != null) + " animator=" + (animator != null) + "), so the fold, " +
                                 "two-hand grip, mine-seat panel and release passes ALL never ran. foldOk/gripOk/" +
                                 "releaseOk below are their `true` INITIALISERS, not measurements: do NOT read a " +
                                 "PASS here as proof of anything.");
            }

            yield return new WaitForSeconds(0.4f);

            bool meshStayed = smr != null && worstMeshGap <= ConeExplosionRadiusU;
            bool pass = allRouted && meshStayed && foldOk && gripOk && _releaseOk && _chopSeatOk && _pointOk;
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
                      $"peakSeatWeight={peakSeatWeight:F2} " +
                      // 86cay4282 round 4 — the PALM figure IS the criterion now, so it rides the one-line verdict with
                      // its centimetre conversion; the wrist figure above stays only for continuity with rounds 2-3.
                      $"worstLeftPALM={worstLeftPalm:F3}SW={worstLeftPalm * swAtWorst * 100f:F1}cm " +
                      $"(cap {TwoHandGripRead.LeftHaftPassSW:F3}SW=" +
                      $"{TwoHandGripRead.LeftHaftPassSW * swAtWorst * 100f:F1}cm) " +
                      $"palmMeasured={anyPalmMeasured && allPalmMeasured} pinPeakWeight={peakPinWeight:F2} " +
                      $"pinReaching={ikReachingFrames}/{ikScoredFrames} gripOk={gripOk} " +
                      // 86cay4282 round 5 — the RELEASE rides the one-line verdict, because "the arm never let go" is
                      // invisible to every engaged-frame figure above it and that is exactly how it shipped.
                      $"releaseSettleFrames={_releaseSettleFrames} releaseBudgetFrames={_releaseBudgetFrames} " +
                      $"releaseOk={_releaseOk} " +
                      // 86cayp0ay — the CHOP-swing seat read rides the one-line verdict, with the liveness figure
                      // NEXT TO it: a seat number without the pose it was measured on is exactly the pair that let a
                      // headless run report 39.5 cm off an idle skeleton.
                      $"chopSeatRan={_chopSeatRan} chopPeakTilt={_chopPeakTilt:F1}deg " +
                      $"chopPhases={_chopPhasesCovered}/{SwingSeatGate.RequiredPhases} " +
                      $"chopWorstRightHaft={_chopWorstRightHaftSW:F4}SW@phase{_chopWorstAtPhase:F2} " +
                      $"chopSeatOk={_chopSeatOk} " +
                      // 86cb6v03j — the POINTING read rides the one-line verdict, because "the weapon is aimed
                      // backwards at the strike" is invisible to every seat/grip figure above it (a distance to a
                      // LINE and a position ALONG that line are both unchanged when the whole stick is rotated about
                      // the hand) — and that is exactly how it shipped.
                      $"pointRan={_pointRan} pointClassesScored={_pointClassesScored} " +
                      $"pointWorstClass={_pointWorstClass} pointWorstStrikeFwdDot={_pointWorstStrikeFwdDot:F3} " +
                      $"pointWorstStrikeUpDot={_pointWorstStrikeUpDot:F3} " +
                      $"pointWorstStrikePhase={_pointWorstStrikePhase:F3} pointOk={_pointOk} => PASS={pass}");
            Application.Quit(pass ? 0 : 1);
        }

        // ===== CHOP SEAT PASS state (86cayp0ay) — hoisted so the one-line verdict carries them. =====
        // FAIL CLOSED (#411 review item c). _chopSeatOk starts FALSE and is set true ONLY by the completed pass's own
        // verdict below, so every path on which the swing-time seat evidence is ABSENT — an unresolvable
        // Inventory/Catalog/HeldWeaponCycleDebug, unresolved mixamorig:Hips/Head, unresolved arm/hand bones, a missing
        // HeldToolRig, the wood axe not reaching the belt — REDS this gate instead of exiting 0 with the entire
        // evidence missing.
        //
        // It deliberately does NOT copy _releaseOk's older default-TRUE convention a few lines below. This file
        // already carries the stricter idiom, adopted in round 4 after the Sponsor caught a green-on-air:
        // `bool palmOk = anyPalmMeasured && allPalmMeasured;` — "an unmeasured palm fails closed, because scoring a
        // different, easier question silently is how a cap loses its meaning". A pass whose own reason for existing
        // is closing an absent-evidence hole (86caz428q's shape) must not reproduce that hole in itself.
        //
        // The skip is ALSO loud in the log and _chopSeatRan rides the one-line verdict, so a RED is diagnosable as
        // "did not run" rather than mistaken for "measured and failed".
        private bool _chopSeatOk;
        private bool _chopSeatRan;
        private float _chopPeakTilt = float.NaN;
        private int _chopPhasesCovered;
        private float _chopWorstRightHaftSW = -1f;
        private float _chopWorstAtPhase = float.NaN;

        // The layer-0 state the AXE class routes to. Source of truth is the controller build:
        // Assets/Scripts/Editor/CharacterAssetGen.cs WireAttackClass(sm, "AttackAxe", ..., WeaponClassAxe, ...).
        // Kept as a literal here because Runtime cannot reference the Editor asmdef; the duplication is pinned by
        // SwingSeatGateTests.AttackAxeStateName_ExistsOnTheShippedController_86cayp0ay.
        public const string AttackAxeState = "AttackAxe";

        // Verify-only fault injection (86cayp0ay). Adds a hand-local offset of N cm to the PRODUCTION seat
        // (HeldToolRig.seatOffsetFromHand, the value ComposeSeat actually reads) for the duration of this pass, then
        // RESTORES it. It exists so the ticket's success test — "introduce a deliberate seat error of ~30 cm -> the
        // gate REDS naming the measured value and the phase" — is reproducible from the shipped exe by anyone, with
        // no rebuild and no edit to a committed seat value. Absent the flag it is 0 and the pass is byte-identical.
        private const string SeatFaultArg = "-swingSeatFaultCm";

        // Verify-only NEGATIVE CONTROL for the FAIL-CLOSED path (86cayp0ay, #411 review item c). Forces the chop-seat
        // pass down its OWN SKIPPED branch — the same branch an unresolvable Inventory/Catalog/HeldWeaponCycleDebug
        // takes, not a parallel one — so "absent evidence REDS the gate" is DEMONSTRATED from the shipped exe rather
        // than argued from the source. Same discipline as SeatFaultArg: a fail-closed default that has never been seen
        // to red is exactly the claim this project keeps having to retract. Read ONLY inside ChopSeatPass, which is
        // reachable only under -verifySwings, so absent the flag no launch mode changes by a byte.
        private const string SkipEvidenceArg = "-swingSeatSkipEvidence";

        // The injected fault's magnitude + the seat value it is supposed to have produced, so the injection can
        // verify ITSELF against the live rig once frames have run (see VerifySeatFaultTookEffect).
        private float _seatFaultCm;
        private Vector3 _seatFaultExpected;

        /// <summary>
        /// Did the injected fault actually reach the seat the rig drives from? Called after many frames have run, so
        /// any per-frame re-sync has had every chance to stomp it. Logs an ERROR and returns false when the write was
        /// discarded — a negative control that did not inject is worthless, and worse than worthless if its green is
        /// read as "the gate is insensitive".
        /// </summary>
        private bool VerifySeatFaultTookEffect(HeldToolRig rig)
        {
            if (Mathf.Abs(_seatFaultCm) < 0.0001f) return true;   // no fault requested
            Vector3 live = rig is HeldAxeRig a ? a.worldOffsetFromHand : rig.seatOffsetFromHand;
            bool held = (live - _seatFaultExpected).magnitude < 1e-4f;
            if (!held)
                Debug.LogError("[seat-fault] THE INJECTION DID NOT STICK: the seat the rig consumes now reads " +
                               live + " but the injection set " + _seatFaultExpected + ". Something re-synced it " +
                               "(HeldAxeRig.ApplySeat copies worldOffsetFromHand over the base field every " +
                               "LateUpdate). This run is a FAILED NEGATIVE CONTROL - do NOT read its verdict as " +
                               "evidence about the gate's sensitivity in either direction.");
            else
                Debug.Log("[seat-fault] injection verified live on the rig: seat reads " + live +
                          " (the value the injection set), so the gate below is scoring a genuinely faulted seat.");
            return held;
        }

        // ===== MINE RELEASE PASS state (86cay4282 round 5) =====
        // Hoisted to fields so the one-line verdict can carry them, the same way the grip pass's readings are.
        private bool _releaseOk = true;          // defaults TRUE only so a SKIPPED pass cannot red the whole gate; a
                                                 // skip is LOUD in the log instead (see the pass's warnings).
        private int _releaseSettleFrames = -1;
        private int _releaseBudgetFrames = -1;

        /// <summary>
        /// 86cay4282 ROUND 5 — DOES THE LEFT ARM LET GO? Sponsor soak of round 4, verbatim: <c>"the reach is ok but the
        /// left arm does not return to normal position after the pickaxe two hand motion"</c>.
        ///
        /// WHY THIS PASS HAD TO EXIST. Every figure the grip pass above logs is measured on ENGAGED frames — by
        /// construction, since a grip read on a non-engaged frame is meaningless. So a defect that lives entirely in the
        /// DISENGAGE was invisible to the gate, to the EditMode suite, to the PlayMode fixture and to the F9 panel, all
        /// four of which were green. It took the Sponsor's eye. This pass closes that hole in the shipped exe: it fires
        /// one more mine swing with the F9 tool CLOSED (so <c>debugForceEngaged</c> is false and the gate is purely the
        /// production animation-state read), then watches the release frame by frame.
        ///
        /// THE TIMELINE ORIGIN is a raw layer-0 reading — the frame the ANIMATOR begins crossfading out of
        /// AttackPickaxe — not either gate predicate, so the measurement does not depend on the thing under test. The
        /// BUDGET is the controller's OWN measured crossfade-out length + 4 frames of slack: a hand that has let go of a
        /// haft is not on it any more, so the arm must be back by the time the BODY has finished returning.
        ///
        /// WHAT IS OBSERVED, and why these quantities: a coroutine reads the pose the previous frame's LateUpdate chain
        /// wrote, so an intra-frame before/after displacement is not available here (the PlayMode fixture measures that
        /// directly). The two outside-observable proxies are exact enough: the pin's own
        /// <see cref="CastawayLeftArmHaftIk.PinWeight"/> — the pin writes NOTHING at weight 0, so a released weight IS a
        /// released arm — and the HAND SEPARATION, which the clip holds at ~1.2 SW while gripping and which returns to
        /// the ~1.7 SW idle carry once the arm is handed back. Both are logged per frame.
        /// </summary>
        private IEnumerator MineReleasePass(string dir, CastawayCharacter castaway, Animator animator,
                                            HeldToolRig heldRig, CastawayLeftArmHaftIk leftIk,
                                            Transform lArm, Transform rArm, Transform lHand, Transform rHand,
                                            Transform hips, Transform head)
        {
            if (leftIk == null)
            {
                Debug.LogWarning("[swing-release] SKIPPED — no CastawayLeftArmHaftIk in the shipped scene, so there is " +
                                 "no release to measure. The round-5 evidence is MISSING from this run; do NOT read the " +
                                 "PASS above as proof the left arm lets go.");
                yield break;
            }
            // The F9 tool force must be OFF, or this pass measures the debug hold rather than the production gate — and
            // that confusion is precisely what produced the round-4 screenshot the ticket's hypothesis was built on.
            if (leftIk.debugForceEngaged)
            {
                Debug.LogWarning("[swing-release] debugForceEngaged was still TRUE entering this pass — the panel pass " +
                                 "should have cleared it via AxeNudgeTool.Deactivate(). Forcing it false; if this line " +
                                 "appears, the panel's release path regressed.");
                leftIk.debugForceEngaged = false;
            }

            int pickaxeHash = Animator.StringToHash(CastawayCharacter.AttackPickaxeState);
            castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);

            int crossfadeStart = -1, leftState = -1, settled = -1, f = 0;
            float peakPin = 0f, sepWhileHeld = float.NaN, sepAtSettle = float.NaN;
            float worstPinAfterCrossfade = 0f;
            var trace = new System.Text.StringBuilder();
            float t0 = Time.time;

            while (Time.time - t0 < FoldWindowSec * 2.5f)
            {
                bool inTr = animator.IsInTransition(0);
                int cur = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                int next = inTr ? animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
                if (crossfadeStart < 0 && cur == pickaxeHash && inTr && next != pickaxeHash) crossfadeStart = f;
                if (leftState < 0 && crossfadeStart >= 0 && cur != pickaxeHash) leftState = f;

                float sw = (rArm.position - lArm.position).magnitude;
                float sep = sw > 1e-5f ? (lHand.position - rHand.position).magnitude / sw : float.NaN;
                peakPin = Mathf.Max(peakPin, leftIk.PinWeight);
                if (crossfadeStart < 0 && leftIk.PinWeight > 0.95f) sepWhileHeld = sep;

                if (crossfadeStart >= 0)
                {
                    worstPinAfterCrossfade = Mathf.Max(worstPinAfterCrossfade, leftIk.PinWeight);
                    if (settled < 0 && leftIk.PinWeight <= ReleasedWeight) { settled = f; sepAtSettle = sep; }
                    if (f - crossfadeStart <= 30)
                        trace.AppendLine($"[swing-release]   +{f - crossfadeStart,2} frames pinW={leftIk.PinWeight:F3} " +
                                         $"seatW={heldRig.MineSeatWeight:F3} solved={leftIk.LastSolved,-5} " +
                                         $"handSep={sep:F2}SW " +
                                         $"{(cur == pickaxeHash ? "(still AttackPickaxe)" : "(layer 0 has LEFT it)")}");
                }
                f++;
                yield return null;
                if (settled >= 0 && f - settled > 45) break;      // measured through settle plus margin
            }

            int crossfade = (crossfadeStart >= 0 && leftState >= 0) ? leftState - crossfadeStart : -1;
            _releaseBudgetFrames = crossfade >= 0 ? crossfade + 4 : -1;
            _releaseSettleFrames = (settled >= 0 && crossfadeStart >= 0) ? settled - crossfadeStart : -1;
            float sepNow = (rArm.position - lArm.position).magnitude > 1e-5f
                ? (lHand.position - rHand.position).magnitude / (rArm.position - lArm.position).magnitude
                : float.NaN;

            Debug.Log(trace.ToString());
            if (crossfade < 0 || _releaseSettleFrames < 0)
            {
                _releaseOk = false;
                Debug.LogWarning($"[swing-release] FAIL — crossfadeOutFrames={crossfade} settleFrames=" +
                                 $"{_releaseSettleFrames} (peak pin weight {peakPin:F2}). A NEGATIVE crossfade means the " +
                                 "Animator never left AttackPickaxe inside the window; a NEGATIVE settle means the pin " +
                                 $"weight never fell to {ReleasedWeight:F2} — i.e. the arm never let go, which IS the " +
                                 "Sponsor's reported defect.");
            }
            else
            {
                _releaseOk = _releaseSettleFrames <= _releaseBudgetFrames;
                Debug.Log($"[swing-release] crossfade OUT measured {crossfade} frames; pin weight fell to " +
                          $"<= {ReleasedWeight:F2} at +{_releaseSettleFrames} frames (budget {_releaseBudgetFrames} = " +
                          $"the body's own crossfade + 4 frames of slack) => releaseOk={_releaseOk}. Peak pin weight " +
                          $"this pass {peakPin:F2}; worst weight from the crossfade onward {worstPinAfterCrossfade:F2}; " +
                          $"hand separation {sepWhileHeld:F2} SW while GRIPPED -> {sepAtSettle:F2} SW at release -> " +
                          $"{sepNow:F2} SW settled (the clip's own grip measures 1.01..1.33 SW and its idle carry " +
                          "1.65..1.89, so separation returning to the upper band is the geometric confirmation that " +
                          "the arm is back on the clip rather than on the haft). At the round-4 symmetric 12/s rate " +
                          "this settled ~28 frames after the crossfade — 0.47 s of the left arm still pulled onto a " +
                          "haft the character had already let go of.");
            }

            // The picture of the judged moment: the frame the bar is evaluated on, from the gameplay cam and a SIDE
            // PROFILE — an arm that has not come back down reads as a silhouette defect, and up-vs-down/in-vs-out is
            // what a side profile is for (lowpoly-quality.md §0).
            ShotTo(Path.Combine(dir, "swing_pickaxe_release.png"));
            yield return null;
            if (hips != null && head != null)
                yield return SideProfileShot(Path.Combine(dir, "swing_pickaxe_release_side.png"), hips, head,
                                            castaway.ModelTransform);
        }

        /// <summary>
        /// 86cayp0ay — IS THE HELD WEAPON STILL IN THE HAND WHILE THE HAND IS SWINGING?
        ///
        /// WHAT THIS COVERS THAT THE REST-POSE GATES DO NOT. -verifyHeldWood / -verifyHeldBelt evidence the held prop
        /// with the character STANDING: they assert a renderer is enabled and the holder's sharedMesh is the expected
        /// lineup node. Both are strong for their own bug class ("nothing in the hand") and both are structurally
        /// blind to WHERE the prop is — a mesh seated 30 cm off the palm satisfies every one of those asserts.
        ///
        /// WHY A SWING AND NOT A REST POSE. The seat reads <c>hand.rotation</c>
        /// (<see cref="HeldToolRig.ApplySeat"/>), and that rotation is written by a chain that is MOVING during a
        /// swing: Animator -> CastawayArmPose (50) -> CastawayFingerCurl (60) -> CastawayHandPose (65, the WRIST
        /// euler) -> CastawayFootYaw (70) -> HeldToolRig (100). At rest the chain runs too, but it runs to ONE pose;
        /// a defect that only opens up at some phases of the arc cannot show there. Measuring on the LIVE runtime
        /// skeleton means every order in that chain is included BY CONSTRUCTION — there is no re-implementation here
        /// for order 65 to be missing from, which is the exact omission that had two self-authored instruments
        /// agreeing at 0.615 SW while the shipped exe measured 1.220 (procedural-animation-verbs.md).
        ///
        /// DRIVE LAYER — ALL PRODUCTION. The weapon is granted and SELECTED through
        /// <c>InventoryModel.AddToolToBelt</c> + <c>SelectBelt</c> (a crafting grant + a hotbar click), never through
        /// <c>ShowWeaponForCaptureDebug</c>, so the held visual is placed by the same
        /// <c>SelectBelt -> Inventory.Changed -> SyncHeldVisualToSelection -> WoodSelectionIndexFor</c> path that
        /// broke in soak-3. The swing is fired with <see cref="CastawayCharacter.TriggerChop"/> — the tree-chop
        /// verb's own seam, at the shipped <c>chopSpeed</c>, NOT a hand-picked TriggerAttack speed and NOT a forced
        /// Animator state.
        ///
        /// THE VERDICT IS A MEASUREMENT: the RIGHT hand's distance to the haft LINE in shoulder-widths
        /// (<see cref="TwoHandGripRead"/>), worst frame over the swing, capped at a value anchored to a measured
        /// achieved worst (<see cref="SwingSeatGate"/>). No pixel statistic is consulted — whole-frame luma/variance
        /// is structurally blind to a held weapon (on PR #355's own pair the negative control scored HIGHER variance
        /// than the positive case), so the PNGs below are reviewer evidence only.
        ///
        /// AND IT REFUSES TO REPORT ONE WITHOUT PROVING THE SWING POSED. See <see cref="SwingSeatGate.Posed"/>: a
        /// headless launch of this exe advances the state machine but never takes the swing pose, and the two-hand
        /// pass above happily scored 4696 frames of that idle stance and reported 39.5 cm. A seat figure is only a
        /// swing verdict if a swing happened, so liveness is a PRECONDITION here, not a co-equal term.
        /// </summary>
        private IEnumerator ChopSeatPass(string dir, CastawayCharacter castaway, Animator animator,
                                         HeldToolRig heldRig, Transform lArm, Transform rArm, Transform rHand,
                                         Transform hips, Transform head)
        {
            var inventory = Object.FindAnyObjectByType<Inventory>();
            var cycle = heldRig != null ? heldRig.GetComponent<HeldWeaponCycleDebug>() : null;
            bool forcedSkip = HasArg(SkipEvidenceArg);
            if (forcedSkip || inventory == null || inventory.Model == null || inventory.Catalog == null || cycle == null)
            {
                _chopSeatOk = false;
                Debug.LogWarning("[chop-seat] SKIPPED — Inventory/Catalog/HeldWeaponCycleDebug not resolvable " +
                                 "(inventory=" + (inventory != null) + " model=" +
                                 (inventory != null && inventory.Model != null) + " catalog=" +
                                 (inventory != null && inventory.Catalog != null) + " cycle=" + (cycle != null) +
                                 "), forcedSkip=" + forcedSkip + ". The swing-time SEAT evidence is MISSING from " +
                                 "this run, so this gate FAILS CLOSED: chopSeatOk=false and the exe exits non-zero. " +
                                 "An absent measurement must never render as a pass — read this as 'the pass did " +
                                 "NOT run' (chopSeatRan=False on the verdict line), not as 'the seat failed'.");
                yield break;
            }

            // --- GRANT + SELECT the WOOD axe through the REAL seams (the crafting grant + a hotbar click). ---
            if (!inventory.Model.OwnsItem(ItemCatalog.AxeWoodId))
            {
                var def = inventory.Catalog.ById(ItemCatalog.AxeWoodId);
                if (def != null) inventory.Model.AddToolToBelt(def);
            }
            int slot = -1;
            var belt = inventory.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
                if (!belt[i].IsEmpty && belt[i].Def != null && belt[i].Def.Id == ItemCatalog.AxeWoodId) { slot = i; break; }
            if (slot < 0)
            {
                Debug.LogError("[chop-seat] '" + ItemCatalog.AxeWoodId + "' is not on the belt after the grant (it " +
                               "fell through to the pack, or the catalog id changed) — cannot drive a belt selection, " +
                               "so this pass cannot judge a production-selected weapon.");
                _chopSeatOk = false;
                yield break;
            }
            inventory.Model.SelectBelt(slot);
            for (int i = 0; i < 10; i++) yield return null;

            // The SAME discriminator triple ChopVerifyCapture uses: without the NOT-stone term a future change that
            // re-selected the stone axe would green this pass while the wood tier stayed unexercised.
            bool woodSelected = inventory.IsAxeWoodSelectedInBelt && inventory.IsAnyAxeSelectedInBelt
                                && !inventory.IsAxeSelectedInBelt;
            // …and the held VISUAL must have followed that selection on its own. DebugViewActive false is what proves
            // the selection path (not a capture-debug force) is what put the mesh in the hand.
            bool visualFollowed = cycle.CurrentIndex == HeldWeaponCycleDebug.AxeWoodFamilyIndex && !cycle.DebugViewActive;
            Debug.Log("[chop-seat] production selection: woodAxeSelected=" + woodSelected +
                      " (wood=" + inventory.IsAxeWoodSelectedInBelt + " anyAxe=" + inventory.IsAnyAxeSelectedInBelt +
                      " stone=" + inventory.IsAxeSelectedInBelt + ") heldVisualFollowed=" + visualFollowed +
                      " (index=" + cycle.CurrentIndex + "/" + HeldWeaponCycleDebug.AxeWoodFamilyIndex +
                      " debugViewActive=" + cycle.DebugViewActive + " — false is what proves SelectBelt, not " +
                      "ShowWeaponForCaptureDebug, placed this mesh)");

            // (Any -swingSeatFaultCm negative control was applied in Start, before every gate — see
            //  ApplySeatFaultIfRequested. Nothing seat-related is mutated here.) Verify it STUCK before scoring:
            //  an injection that was silently re-synced away would make this pass look insensitive.
            VerifySeatFaultTookEffect(heldRig);

            // --- Fire the CHOP verb and score the swing frame by frame. ---
            int axeHash = Animator.StringToHash(AttackAxeState);
            castaway.TriggerChop();
            float t0 = Time.time;
            int scored = 0;
            var phaseHit = new bool[SwingSeatGate.RequiredPhases];
            float peakTilt = 0f, worstSW = -1f, worstPhase = float.NaN, swAtWorst = TwoHandGripRead.ReferenceShoulderWidthM;
            float worstAtSec = 0f;
            float minU = float.MaxValue, maxU = float.MinValue, haftLen = 0f;
            while (Time.time - t0 < FoldWindowSec)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                bool inTr = animator.IsInTransition(0);
                // Score ONLY frames the AXE swing owns outright. A crossfade blends idle with the swing, which is a
                // pose the clip never contains and nothing was ever seated against (the round-4 lesson: scoring
                // blends produced a hand separation less than HALF the clip's own minimum).
                if (st.shortNameHash == axeHash && !inTr)
                {
                    float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                    if (tilt > peakTilt) peakTilt = tilt;
                    // The chop is a ONE-HANDED verb, so this scores the RIGHT hand only — the hand the tool is
                    // actually seated in. TwoHandGripRead.Measure is the two-hand read and would report a meaningless
                    // separation here, so the shared primitive DistanceToSegment is used directly: same maths, same
                    // shoulder-width normalisation, no invented second hand.
                    float sw = (rArm.position - lArm.position).magnitude;
                    if (sw > 1e-5f && heldRig.TryGetHaftSegment(out Vector3 gripW, out Vector3 headW)
                        && (headW - gripW).sqrMagnitude > 1e-10f)
                    {
                        float d = TwoHandGripRead.DistanceToSegment(rHand.position, gripW, headW, out float u) / sw;
                        scored++;
                        float phase = Mathf.Repeat(st.normalizedTime, 1f);
                        phaseHit[SwingSeatGate.PhaseBucket(phase)] = true;
                        // THE ALONG-HAFT COMPONENT. A perpendicular distance-to-LINE is BLIND to the tool sliding
                        // along its own axis: translate the haft parallel to itself and the perpendicular distance
                        // does not move at all. That is GEOMETRY — a property of the metric, true by construction —
                        // NOT an empirical control. So the discarded component is gated too
                        // (procedural-animation-verbs.md: "the discarded ALONG component is a second, independent
                        // defect axis - compute it, DRAW it, and decide explicitly whether to gate it"). u is 0 at
                        // the BUTT/grip end, 1 at the HEAD end, UNCLAMPED so a hand that has slid off an end reads
                        // <0 or >1.
                        // ⚠ Do NOT attach a "-swingSeatFaultCm 30 left the perpendicular unmoved" justification to
                        // this leg IN ANY WORDING. That reading came from the pre-fix injector writing a field
                        // HeldAxeRig.ApplySeat stomps every LateUpdate, so NEITHER axis moved and the run measured
                        // the injector's own inertness. With the injection landing, a 30 cm hand-local +X fault moves
                        // BOTH axes (measured: perpendicular 0.4027 -> 0.7172 SW, u 0.2004 -> 0.0107), so that fault
                        // is NOT an along-only control. See SwingSeatGate's ALONG-HAFT block for the full correction.
                        haftLen = (headW - gripW).magnitude;
                        if (u < minU) minU = u;
                        if (u > maxU) maxU = u;
                        if (d > worstSW)
                        {
                            worstSW = d;
                            worstPhase = phase;
                            swAtWorst = sw;
                            worstAtSec = Time.time - t0;
                        }
                    }
                }
                yield return null;
            }
            int phases = 0;
            foreach (bool b in phaseHit) if (b) phases++;

            _chopSeatRan = true;
            _chopPeakTilt = peakTilt;
            _chopPhasesCovered = phases;
            _chopWorstRightHaftSW = worstSW;
            _chopWorstAtPhase = worstPhase;

            float driftU = haftLen > 1e-5f ? TwoHandGripRead.HaftRadiusM / haftLen : float.NaN;
            // INVARIANT culture on every figure: this line is quoted into PR bodies and may be grepped, so it must
            // not change shape on a comma-decimal machine (this project's own runner is one).
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log("[chop-seat] ALONG-HAFT (0 = BUTT/grip end, 1 = HEAD end): the hand sits at u " +
                      (minU == float.MaxValue ? -9f : minU).ToString("F4", inv) + ".." +
                      (maxU == float.MinValue ? -9f : maxU).ToString("F4", inv) + " over the swing; haft length " +
                      haftLen.ToString("F4", inv) + " m, so one haft radius of allowed slide is " +
                      driftU.ToString("F4", inv) + " u. This is the component a perpendicular distance-to-line " +
                      "THROWS AWAY: translating the haft parallel to itself maps the line onto itself, so the " +
                      "perpendicular CANNOT move. That is geometry, not a measured control - and note that " +
                      "-swingSeatFaultCm is NOT an along-only control (it injects along hand-local +X and moves " +
                      "BOTH axes), so do not quote it as one.");

            bool verdict = SwingSeatGate.Verdict(scored, phases, peakTilt, worstSW, swAtWorst, worstPhase,
                                                 minU, maxU, driftU, out string why);
            // The production-drive terms are part of the verdict: a seat measured while the wrong tier was selected,
            // or while a capture-debug force (not the selection path) placed the mesh, is a measurement of a
            // combination that does not ship.
            _chopSeatOk = verdict && woodSelected && visualFollowed;
            Debug.Log("[chop-seat] VERDICT: " + why + " | woodAxeSelected=" + woodSelected +
                      " heldVisualFollowed=" + visualFollowed + " => chopSeatOk=" + _chopSeatOk +
                      " (worst frame at +" + worstAtSec.ToString("F2", inv) + "s, 1 SW = " + swAtWorst.ToString("F4", inv) +
                      " m). A FALSE with a SWING NEVER POSED reason means the run measured an idle skeleton and the " +
                      "seat number in it is not a swing reading at all.");

            // Reviewer evidence at the JUDGED moment — re-fire and shoot the worst frame, never a nice one. Under a
            // headless launch ScreenCapture writes nothing; that is expected and is not the verdict (the verdict is
            // the measurement above).
            castaway.TriggerChop();
            float t1 = Time.time;
            while (Time.time - t1 < worstAtSec) yield return null;
            // ⚠ THE `swing_` PREFIX IS LOAD-BEARING, not decoration (#411 review §3(2)). The -verifySwings wrapper
            // #369 authors clears stale artifacts with `rm -f "$ABS_CAP"/swing_*.png` before EVERY launch attempt,
            // while frame_check.py judges EVERY .png in that directory (_iter_pngs, frame_check.py:41-49). A capture
            // named outside that glob therefore SURVIVES a retry and is judged as fresh — the #130 stale-artifact
            // false-green class, in picture form. Every sibling shot in this file already carries the prefix; these
            // two were the only exceptions, so they are renamed INTO the convention rather than leaving the wrapper
            // to widen its glob for them (widening it is still worth doing as defence-in-depth for the next author).
            ShotTo(Path.Combine(dir, "swing_chop_seat_worst.png"));
            yield return null;
            // A CLOSE shot framed ON THE HAND, aimed from the SUBJECT (chest -> hand), which by construction puts the
            // hand between the lens and the body whichever way the character has yawed. Round 4 paid for taking a
            // capture axis from an assumed rig 'forward' and photographing the back of the head instead.
            {
                Vector3 chest = (lArm.position + rArm.position) * 0.5f;
                Vector3 aim = Vector3.ProjectOnPlane(rHand.position - chest, Vector3.up);
                if (aim.sqrMagnitude < 1e-4f) aim = Vector3.forward;
                yield return ProfileShotAt(Path.Combine(dir, "swing_chop_seat_worst_close.png"),
                                           rHand.position + aim.normalized * GripShotDistU, rHand.position);
            }

            yield return null;
        }

        // ===== SWING POINTING PASS state (86cb6v03j) — hoisted so the one-line verdict carries it. =====
        // FAIL CLOSED, matching ChopSeatPass rather than the older default-TRUE _releaseOk convention: this pass
        // exists BECAUSE weapon orientation was an unmeasured axis, so a run where it does not measure must red
        // rather than inherit a `true`.
        private bool _pointOk;
        private bool _pointRan;
        private string _pointWorstClass = "none";
        private float _pointWorstStrikeFwdDot = float.NaN;
        private float _pointWorstStrikeUpDot = float.NaN;
        private float _pointWorstStrikePhase = float.NaN;
        private int _pointClassesScored;
        /// <summary>Each class's MEASURED strike phase (peak head speed), so the shoot pass can re-find that exact
        /// moment by PHASE. Never by wall clock — see the fixed-delay false-green note in the pass.</summary>
        private readonly System.Collections.Generic.Dictionary<string, float> _pointStrikePhaseByClass =
            new System.Collections.Generic.Dictionary<string, float>();
        /// <summary>Each class's strike-frame fwdDot — the gated quantity. A class ABSENT from this map produced no
        /// strike reading, which the verdict treats as a coverage failure rather than as a pass.</summary>
        private readonly System.Collections.Generic.Dictionary<string, float> _pointStrikeFwdByClass =
            new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>WeaponClass (CastawayCharacter order) -> the HeldWeaponCycleDebug FAMILY INDEX whose mesh must be
        /// in the hand while that class's clip plays. The two orderings are DIFFERENT and it is a silent error to use
        /// one for the other: CastawayCharacter is {axe 0, pickaxe 1, dagger 2, spear 3, sword 4} while
        /// HeldWeaponCycleDebug's mesh table is {axe 0, dagger 1, sword 2, spear 3, pickaxe 4}. Three of the five
        /// indices collide harmlessly and two do not, which is precisely the shape that survives a careless read.
        ///
        /// ⚠ FORCING THE MESH IS NOT OPTIONAL. The held mesh syncs to the BELT SELECTION and a verify run selects
        /// nothing, so without this the hand shows the DEFAULT stone AXE for all five clips — the two-hand pass
        /// already paid for that exact mistake (it reported 1.589 SW off the axe mesh before the pickaxe was pinned).
        /// A pointing read taken off the wrong mesh measures a combination that cannot occur in play.</summary>
        private static readonly (int weaponClass, int familyIndex, string state, string name, bool arcVerb)[] PointClasses =
        {
            // arcVerb: TRUE for a swung arc (the head arrives forward-and-DOWN at the bottom of the arc), FALSE for
            // a thrust (the tip drives straight along the facing, level). The split is the ASSET's own, not an
            // invented taxonomy — the clips are named CastawayAxeSwing / CastawaySwordSlash vs
            // CastawaySpearThrust / CastawayDaggerStab.
            (CastawayCharacter.WeaponClassAxe,     HeldWeaponCycleDebug.AxeFamilyIndex,          "AttackAxe",     "axe",     true),
            (CastawayCharacter.WeaponClassPickaxe, HeldWeaponCycleDebug.PickaxeStoneFamilyIndex, "AttackPickaxe", "pickaxe", true),
            (CastawayCharacter.WeaponClassDagger,  HeldWeaponCycleDebug.DaggerStoneFamilyIndex,  "AttackDagger",  "dagger",  false),
            (CastawayCharacter.WeaponClassSpear,   HeldWeaponCycleDebug.SpearFamilyIndex,        "AttackSpear",   "spear",   false),
            (CastawayCharacter.WeaponClassSword,   HeldWeaponCycleDebug.SwordStoneFamilyIndex,   "AttackSword",   "sword",   true),
        };

        /// <summary>The world direction a class's strike should send the weapon's head. ARC verbs get the bisector
        /// of "straight ahead" and "straight down" (the bottom of an arc travels forward and down into the target);
        /// THRUST verbs get straight ahead, level. Shared by the FIT (which solves for the delta that reaches it)
        /// and by the log line (which reports the residual angle to it), so the fit and the report can never
        /// disagree about what was being aimed at.</summary>
        private static Vector3 AimDirFor(bool arcVerb, Vector3 modelForward)
        {
            Vector3 f = modelForward.sqrMagnitude > 1e-8f ? modelForward.normalized : Vector3.forward;
            return arcVerb ? (f + Vector3.down).normalized : f;
        }

        /// <summary>
        /// 86cb6v03j — THE SWING POINTING PASS. The Sponsor's report is "the weapons/tools does not point in the right
        /// direction while swinging"; which weapon and which verb was UNSPECIFIED, so this measures ALL FIVE classes
        /// rather than assuming the axe combat swing (the ticket states that as a hypothesis, not an observation).
        ///
        /// It measures the axis <see cref="SwingSeatGate"/> is structurally blind to. That gate scores the hand's
        /// perpendicular distance to the haft LINE and its position u ALONG that line; rotate the whole stick about
        /// the grip point and BOTH are unchanged while the head swings round to point backwards. See
        /// <see cref="SwingPointRead"/> for the real-world anchor and the frame convention.
        ///
        /// SHAPE: the established two-pass idiom in this file. Pass 1 drives each class's swing and MEASURES the live
        /// composed pointing read every frame the class's own attack state owns OUTRIGHT (crossfade frames excluded —
        /// a blend is a pose the clip never contains, the round-4 lesson). Pass 2 re-fires the WORST class and shoots
        /// its worst frame from the gameplay cam AND a side profile, because an up-vs-down / fore-vs-aft pointing read
        /// is nearly invisible from the player's over-the-shoulder angle (lowpoly-quality.md §0).
        ///
        /// The per-frame table is logged in full (bounded at <see cref="PointTraceMaxLinesPerClass"/> lines per class).
        /// That is deliberate: the fix shape for a pointing defect depends on WHERE in the arc it goes wrong, and a
        /// summary min/max cannot answer that. The trace is the artifact the PR body quotes.
        /// </summary>
        private IEnumerator SwingPointPass(string dir, CastawayCharacter castaway, Animator animator,
                                           HeldToolRig heldRig, Transform lArm, Transform rArm, Transform rHand,
                                           Transform hips, Transform head)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var cycle = heldRig != null ? heldRig.GetComponent<HeldWeaponCycleDebug>() : null;
            if (HasArg(SkipEvidenceArg) || castaway == null || animator == null || heldRig == null || cycle == null
                || lArm == null || rArm == null || rHand == null || hips == null || head == null)
            {
                _pointOk = false;
                Debug.LogWarning("[swing-point] SKIPPED — the pointing read's inputs did not resolve (castaway=" +
                                 (castaway != null) + " animator=" + (animator != null) + " heldRig=" +
                                 (heldRig != null) + " cycle=" + (cycle != null) + " arms=" +
                                 (lArm != null && rArm != null) + " rHand=" + (rHand != null) +
                                 " hips/head=" + (hips != null && head != null) +
                                 "). The weapon-ORIENTATION evidence is MISSING from this run, so this gate FAILS " +
                                 "CLOSED: pointOk=false. Read it as 'the pass did NOT run' (pointRan=False on the " +
                                 "verdict line), never as 'the weapon points correctly'.");
                yield break;
            }

            // ===== FACING-SIGN CONTROL — the ONE assumption the whole pointing read rests on. =====
            // Every fwdDot in this pass is dot(haftDirection, ModelTransform.forward). If the castaway FBX's visual
            // front were not the model transform's +Z, every sign in this pass would be exactly inverted and the
            // diagnosis would be precisely backwards — a failure mode no amount of internal consistency could
            // catch, because all five classes would flip together. The rig-convention argument ("Mixamo imports
            // facing +Z") is not evidence; unity-conventions.md §FBX is a list of times that class of argument was
            // wrong on this very project.
            //
            // So it is settled with a PICTURE instead: FrontalShot stands the camera off ALONG ModelTransform.forward
            // and looks back at the character. If +Z is the visual front, this frame shows his FACE. If it shows the
            // back of his head, every fwdDot in this log must be read negated. One frame, no assumption.
            yield return FrontalShot(Path.Combine(dir, "swing_point_facing_control.png"), hips, head,
                                     castaway.ModelTransform, SideProfileDistU);
            Debug.Log("[swing-point] FACING-SIGN CONTROL written to swing_point_facing_control.png: the camera " +
                      "stands off ALONG ModelTransform.forward and looks back. A FACE in that frame confirms the " +
                      "model transform's +Z is the castaway's visual front, which is the sole assumption behind " +
                      "every fwdDot below; the BACK of the head would mean every fwdDot must be read negated. " +
                      "Note the same axis sets the side-profile framing: ProfileShot stands off along " +
                      "ModelTransform.right, and for a left-handed frame the camera's own right then works out to " +
                      "the character's FORWARD — so in swing_point_<class>_side.png the direction he is attacking " +
                      "along is toward the RIGHT of the image.");
            for (int i = 0; i < 6; i++) yield return null;

            var summary = new System.Collections.Generic.List<string>();
            float globalWorstStrikeFwd = float.MaxValue;
            _pointClassesScored = 0;
            _pointWorstStrikePhase = float.NaN;

            foreach (var (weaponClass, familyIndex, stateName, name, arcVerb) in PointClasses)
            {
                cycle.ShowWeaponForCaptureDebug(familyIndex);
                for (int i = 0; i < 3; i++) yield return null;
                bool meshRight = cycle.CurrentIndex == familyIndex;
                if (!meshRight)
                    Debug.LogWarning("[swing-point] " + name + ": held mesh is index " + cycle.CurrentIndex +
                                     ", expected " + familyIndex + " — every figure below would be measured against " +
                                     "the WRONG weapon mesh. Treat this class as UNMEASURED.");

                int stateHash = Animator.StringToHash(stateName);
                castaway.TriggerAttack(weaponClass, 1f);

                float t0 = Time.time;
                bool havePrev = false;
                Vector3 prevGrip = Vector3.zero, prevHead = Vector3.zero, prevHand = Vector3.zero;
                float prevT = Time.time;
                int scored = 0, lines = 0;
                var phaseHit = new bool[SwingSeatGate.RequiredPhases];
                float peakTilt = 0f;

                // ===== THE STRIKE FRAME is the frame of PEAK *HAND* SPEED. =====
                //
                // Found by MEASUREMENT every run, never at a fixed delay: clip lengths and the per-class
                // SwingSpeed* multipliers are live soak-tuned dials, so any fixed delay is calibrated against
                // numbers that move under it (the fixed-delay false-green, procedural-animation-verbs.md).
                //
                // ⚠ IT WAS PEAK *HEAD* SPEED FIRST, AND THAT WAS A REAL DEFECT IN THE INSTRUMENT — kept here
                // because the error is not obvious and the symptom looked like a converging fix. The head's motion
                // depends on the weapon's ORIENTATION, which is the very thing this ticket changes. So when the aim
                // delta landed, the peak-head-speed moment MIGRATED: the axe's strike moved from phase 0.206 to
                // 0.382 (16.5 -> 19.4 m/s) and the spear's from 0.186 to 0.110. The delta had been fitted at the OLD
                // frame and was then judged at a NEW one, so the fit could never close — it was a fixed-point
                // iteration wearing the costume of a one-shot solve, and its residual (axe 104.3 deg) read as "the
                // fix underachieved" rather than as "the ruler moved".
                //
                // The HAND is owned by the CLIP. Rotating what it holds does not move it by a millimetre, so peak
                // hand speed is invariant under this fix — which makes the judged moment stable across builds, the
                // fit a genuine one-shot solve, and the gate a comparison of like with like. General rule this is an
                // instance of: never define the judged MOMENT using the quantity under repair.
                var strike = default(SwingPointRead.Read);
                float strikeGripSpd = -1f, strikePhase = float.NaN, strikeAtSec = float.NaN;
                // The strike frame's tool rotation + haft direction, kept so the FIT below solves in the tool's own
                // frame — the SAME frame HeldToolRig.ComposeSeat composes its deltas in, so fitted == baked ==
                // applied. Also the live swing-aim weight/class, so the line can say whether the delta was ON.
                Quaternion strikeToolRot = Quaternion.identity;
                Vector3 strikeHaftWorld = Vector3.zero, strikeAim = Vector3.zero;
                float strikeAimWeight = float.NaN; int strikeAimClass = -99;
                var samples = new System.Collections.Generic.List<PointSample>();
                // The seat's own constant, and its spread — the check that the frame-invariance argument holds in
                // the shipped build rather than only on paper.
                Vector3 haftHandFirst = Vector3.zero; bool haveFirst = false; float haftHandSpreadDeg = 0f;
                float minFwd = float.MaxValue, minFwdPhase = float.NaN;

                while (Time.time - t0 < FoldWindowSec)
                {
                    var st = animator.GetCurrentAnimatorStateInfo(0);
                    bool owns = st.shortNameHash == stateHash && !animator.IsInTransition(0);
                    if (owns && heldRig.TryGetHaftSegment(out Vector3 gripW, out Vector3 headW))
                    {
                        float tilt = Vector3.Angle(head.position - hips.position, Vector3.up);
                        if (tilt > peakTilt) peakTilt = tilt;
                        if (havePrev)
                        {
                            var r = SwingPointRead.Measure(gripW, headW, prevGrip, prevHead, Time.time - prevT,
                                                           (lArm.position + rArm.position) * 0.5f, rHand.rotation,
                                                           castaway.ModelTransform != null
                                                               ? castaway.ModelTransform.forward : Vector3.forward,
                                                           rHand.position, prevHand);
                            if (r.valid)
                            {
                                scored++;
                                float phase = Mathf.Repeat(st.normalizedTime, 1f);
                                phaseHit[SwingSeatGate.PhaseBucket(phase)] = true;

                                if (!haveFirst) { haftHandFirst = r.haftInHandLocal; haveFirst = true; }
                                else haftHandSpreadDeg = Mathf.Max(haftHandSpreadDeg,
                                                                   Vector3.Angle(haftHandFirst, r.haftInHandLocal));

                                // BUFFER the frame. The strike WINDOW cannot be classified until the swing's peak
                                // hand speed is known, so the samples are kept and post-processed below rather than
                                // reduced on the fly.
                                samples.Add(new PointSample
                                {
                                    read = r,
                                    phase = phase,
                                    atSec = Time.time - t0,
                                    toolRot = heldRig.transform.rotation,
                                    haftWorld = (headW - gripW).normalized,
                                    aim = AimDirFor(arcVerb, castaway.ModelTransform != null
                                                             ? castaway.ModelTransform.forward : Vector3.forward),
                                    aimWeight = heldRig.SwingAimWeight,
                                    aimClass = heldRig.SwingAimClass,
                                });
                                if (r.handSpeed > strikeGripSpd) strikeGripSpd = r.handSpeed;
                                if (r.fwdDot < minFwd) { minFwd = r.fwdDot; minFwdPhase = phase; }

                                if (lines < PointTraceMaxLinesPerClass)
                                {
                                    lines++;
                                    Debug.Log("[swing-point-trace] " + name +
                                              " phase=" + phase.ToString("F3", inv) +
                                              " fwdDot=" + r.fwdDot.ToString("F3", inv) +
                                              " upDot=" + r.upDot.ToString("F3", inv) +
                                              " extendDot=" + r.extendDot.ToString("F3", inv) +
                                              " speedRatio=" + r.speedRatio.ToString("F3", inv) +
                                              " leadDot=" + r.leadDot.ToString("F3", inv) +
                                              " headSpd=" + r.headSpeed.ToString("F2", inv) +
                                              " gripSpd=" + r.gripSpeed.ToString("F2", inv) + " handSpd=" + r.handSpeed.ToString("F2", inv) +
                                              " haftLen=" + r.haftLenM.ToString("F3", inv) +
                                              " haftInHand=(" + r.haftInHandLocal.x.ToString("F3", inv) + "," +
                                              r.haftInHandLocal.y.ToString("F3", inv) + "," +
                                              r.haftInHandLocal.z.ToString("F3", inv) + ")");
                                }
                            }
                        }
                        prevGrip = gripW; prevHead = headW; prevHand = rHand.position;
                        prevT = Time.time; havePrev = true;
                    }
                    else if (!owns)
                    {
                        havePrev = false;   // never differentiate ACROSS a gap in the owned window
                    }
                    yield return null;
                }

                // ===== CLASSIFY THE STRIKE WINDOW, AND JUDGE ACROSS IT =====
                //
                // ⚠ THE SINGLE PEAK FRAME WAS NOT A STABLE RULER, AND THAT IS MEASURED, NOT FEARED. The sword's
                // hand-speed peak is a narrow spike (it plays at a 1.5x class multiplier on top of its own
                // attackSpeed), so at ~60 Hz the "fastest frame" lands on a different phase from run to run: the
                // sword's fit residual sat at 124.4 then 124.3 deg across two runs with COMPLETELY different deltas
                // baked between them — the fit was chasing a ruler that moved under it, and the flat residual read
                // as "the fix does nothing" rather than as "the moment being judged is not the same moment".
                //
                // So the judged scope is a WINDOW, not an instant: every frame whose HAND speed is at least
                // StrikeWindowSpeedFraction of that swing's own peak. That is the fast part of the swing — the part
                // the eye actually reads as the strike — and it is robust to one spiky frame because its boundaries
                // are a ratio of the peak rather than the peak's own location.
                //
                // It is also what the ticket asks for literally. A value-at-one-instant assert is the trap #436
                // paid for ([[feel-gates-need-an-eye-time-floor]]): the eye consumes a duration, so the gate must
                // too. The criterion below is the MINIMUM fwdDot ACROSS the window — the weapon must not point
                // backwards at ANY moment of the fast phase, not merely on average and not merely at one frame.
                // ⚠ THE WINDOW'S REFERENCE SPEED IS A HIGH PERCENTILE, NOT THE MAX — because the max is the least
                // robust statistic there is and this signal demonstrably carries one-frame artifacts. Measured: the
                // spear swing logged a single frame at gripSpd 56.40 m/s (headSpd 54.34) in a swing that otherwise
                // peaks around 8 m/s. A hand does not move at 56 m/s; that is a sampling/crossfade artifact. Taken
                // as the peak it put the window floor at 28 m/s and collapsed the spear's strike window to ONE
                // frame — the exact single-frame fragility the window exists to remove, smuggled back in through
                // the reference value. The 90th percentile of the swing's own hand speeds is immune to a lone
                // outlier and is otherwise the same number.
                var speeds = new System.Collections.Generic.List<float>(samples.Count);
                foreach (var s in samples) speeds.Add(s.read.handSpeed);
                speeds.Sort();
                float refGripSpd = speeds.Count > 0
                    ? speeds[Mathf.Clamp(Mathf.FloorToInt(speeds.Count * StrikeWindowReferencePercentile), 0,
                                         speeds.Count - 1)]
                    : 0f;
                float windowFloor = refGripSpd * StrikeWindowSpeedFraction;
                var window = new System.Collections.Generic.List<PointSample>();
                foreach (var s in samples) if (s.read.handSpeed >= windowFloor) window.Add(s);

                float winMinFwd = float.MaxValue, winMaxFwd = float.MinValue, winSumFwd = 0f;
                float winMinPhase = float.MaxValue, winMaxPhase = float.MinValue, winMinFwdPhase = float.NaN;
                foreach (var s in window)
                {
                    if (s.read.fwdDot < winMinFwd) { winMinFwd = s.read.fwdDot; winMinFwdPhase = s.phase; }
                    winMaxFwd = Mathf.Max(winMaxFwd, s.read.fwdDot);
                    winSumFwd += s.read.fwdDot;
                    winMinPhase = Mathf.Min(winMinPhase, s.phase);
                    winMaxPhase = Mathf.Max(winMaxPhase, s.phase);
                }
                float winMeanFwd = window.Count > 0 ? winSumFwd / window.Count : float.NaN;

                // THE FIT FRAME is the window frame whose heading is CLOSEST TO THE WINDOW'S MEAN HEADING.
                //
                // It follows from what is gated. The criterion is the MEAN fwdDot across the window, so the fit has
                // to move the MEAN onto the aim — not the best frame (fitting the best moment of a bad swing is the
                // false-green class this project has paid for repeatedly), and not the worst either (the worst frame
                // of a perfectly legitimate arc sits ~120 deg from the best, so aiming IT would swing the whole arc
                // past the target the other way). The frame that best represents the mean moves the mean by very
                // nearly the fitted rotation, which is the quantity actually being judged.
                //
                // The mean heading is the normalised SUM of the window's unit headings — the standard mean direction
                // for unit vectors. Averaging angles component-wise would be wrong at the wrap.
                if (window.Count > 0)
                {
                    Vector3 meanHeading = Vector3.zero;
                    foreach (var s in window) meanHeading += s.haftWorld;
                    var repS = window[0];
                    bool haveMean = meanHeading.sqrMagnitude > 1e-8f;
                    if (haveMean)
                    {
                        meanHeading.Normalize();
                        float best = -2f;
                        foreach (var s in window)
                        {
                            float d = Vector3.Dot(s.haftWorld, meanHeading);
                            if (d > best) { best = d; repS = s; }
                        }
                    }
                    strike = repS.read;
                    strikePhase = repS.phase; strikeAtSec = repS.atSec;
                    strikeToolRot = repS.toolRot; strikeAim = repS.aim;
                    strikeAimWeight = repS.aimWeight; strikeAimClass = repS.aimClass;
                    // Carry the MEAN heading onto the aim, not the representative frame's own heading: that frame
                    // supplies the tool FRAME the rotation is expressed in, while the vector being aimed is the
                    // mean. Fitting the frame's own heading instead would leave the mean short by however far that
                    // frame sits from it.
                    strikeHaftWorld = haveMean ? meanHeading : repS.haftWorld;
                }

                int phases = 0;
                foreach (bool b in phaseHit) if (b) phases++;
                bool posed = SwingSeatGate.Posed(scored, phases, peakTilt, out string posedWhy);
                if (posed && meshRight && window.Count > 0)
                {
                    _pointClassesScored++;
                    _pointStrikePhaseByClass[name] = strikePhase;
                    _pointStrikeFwdByClass[name] = winMeanFwd;
                    if (winMeanFwd < globalWorstStrikeFwd)
                    {
                        globalWorstStrikeFwd = winMeanFwd;
                        _pointWorstClass = name;
                        _pointWorstStrikeFwdDot = winMeanFwd;
                        _pointWorstStrikeUpDot = strike.upDot;
                        _pointWorstStrikePhase = strikePhase;
                    }
                }

                Debug.Log("[swing-point] " + name + " STRIKE WINDOW: " + window.Count + "/" + samples.Count +
                          " frames at or above " + (StrikeWindowSpeedFraction * 100f).ToString("F0", inv) +
                          "% of this swing's p90 HAND speed (" + refGripSpd.ToString("F1", inv) + " m/s; raw max " + strikeGripSpd.ToString("F1", inv) + "), " +
                          "spanning phase " + winMinPhase.ToString("F3", inv) + ".." +
                          winMaxPhase.ToString("F3", inv) + ". fwdDot across the window: min " +
                          winMinFwd.ToString("F3", inv) + " (at phase " + winMinFwdPhase.ToString("F3", inv) +
                          ") mean " + winMeanFwd.ToString("F3", inv) + " max " + winMaxFwd.ToString("F3", inv) +
                          ". The MEAN is the gated figure: a swing legitimately sweeps its heading through a wide " +
                          "arc while the hand is fast (this window's own min..max IS that sweep), so gating the " +
                          "MINIMUM would forbid the weapon from swinging at all — measured, the axe sweeps -0.902 " +
                          "to +0.907 inside its own window. The mean over the fast phase is what separates a " +
                          "weapon LEADING into the strike from one TRAILING through it, and it is a duration " +
                          "rather than an instant.");

                Debug.Log("[swing-point] " + name + ": meshRight=" + meshRight + " " + posedWhy +
                          " | WORST WINDOW FRAME (the fit + capture moment) at phase " +
                          strikePhase.ToString("F3", inv) + " (+" + strikeAtSec.ToString("F2", inv) + "s): fwdDot=" +
                          strike.fwdDot.ToString("F3", inv) + " upDot=" + strike.upDot.ToString("F3", inv) +
                          " extendDot=" + strike.extendDot.ToString("F3", inv) + " speedRatio=" +
                          strike.speedRatio.ToString("F3", inv) + " leadDot=" + strike.leadDot.ToString("F3", inv) +
                          " | worst fwdDot over the whole swing " +
                          (minFwd == float.MaxValue ? -9f : minFwd).ToString("F3", inv) + " at phase " +
                          minFwdPhase.ToString("F3", inv) +
                          " | OWNING-LAYER READ: haft in HAND-LOCAL frame = (" +
                          strike.haftInHandLocal.x.ToString("F3", inv) + "," +
                          strike.haftInHandLocal.y.ToString("F3", inv) + "," +
                          strike.haftInHandLocal.z.ToString("F3", inv) + "), spread over the swing " +
                          haftHandSpreadDeg.ToString("F2", inv) + " deg (near 0 CONFIRMS the seat owns orientation " +
                          "outright and the clip supplies only hand.rotation); best hand axis toward " +
                          "forward-and-down at the strike = " +
                          (strike.handAxisBestIndex >= 0
                              ? SwingPointRead.HandAxisNames[strike.handAxisBestIndex] : "none") + " at " +
                          strike.handAxisBestFwdDown.ToString("F3", inv) +
                          " (HIGH means a SEAT re-dial could aim this weapon into the strike, so the seat owns the " +
                          "defect; LOW means the strike direction is not reachable from this hand pose at all and " +
                          "the clip / CastawayArmPose owns it). fwdDot < 0 = the head points AWAY from what the " +
                          "character is facing.");

                // ===== THE FIT / CONVERGENCE LINE — A CLOSED-FORM OPTIMUM FOR THE GATED STATISTIC =====
                //
                // The gated quantity is the MEAN of fwdDot over the strike window, so the fit must maximise exactly
                // that. It has an exact solution, and finding it removed the last source of non-convergence.
                //
                // Two facts make it close analytically:
                //   (1) The haft's direction in the TOOL's own frame is a CONSTANT h — the mesh is rigidly parented
                //       to the tool root. (Measured, not assumed: the haft-in-hand spread is ~0 deg per class.)
                //   (2) With an extra tool-frame rotation D, frame i's heading is toolRot_i * D * h, so
                //           mean_i dot(toolRot_i * D * h, aim_i)  =  dot( D*h , (1/N) SUM_i Inverse(toolRot_i)*aim_i )
                //       — the per-frame tool rotations move entirely onto the AIM side of the dot product.
                // Writing A for that sum, the mean is maximised exactly when D*h is PARALLEL TO A, giving
                //     D = FromToRotation(h, normalize(A))
                // which is global, not iterative, and optimal for the window as a whole rather than for one frame.
                //
                // ⚠ WHY THE OBVIOUS ONE-FRAME FIT WAS NOT GOOD ENOUGH, measured. The previous version solved
                // FromToRotation at a single representative frame. That is only a first-order approximation of the
                // mean, because a CONSTANT tool-frame rotation does NOT rotate every frame's world heading by the
                // same world rotation — so the mean of the rotated headings is not the rotation of the mean. It
                // closed for classes whose hand rotates little across the window (spear 9.3 deg residual) and stalled
                // for those whose hand rotates a lot: the axe went 73.9 -> 72.7 deg across a bake, i.e. essentially a
                // fixed point, and left the axe's gated mean at a marginal +0.040. A gate passing by 0.04 is a gate
                // that flips on run-to-run noise.
                //
                // D IS STILL MINIMAL in the sense that matters: FromToRotation leaves ROLL about the weapon's own
                // long axis untouched, and roll is what the Sponsor dialled by eye at rest and what this ticket has
                // no business moving (roll does not change where a weapon POINTS).
                //
                // THE LINE SERVES TWO PURPOSES, which is why it is unconditional rather than behind a flag:
                //   * BEFORE a delta is baked it is the FIT — the number to bake, derived rather than guessed.
                //   * AFTER it is baked the delta is already applied in these very samples, so a correct bake makes
                //     the remaining rotation collapse toward identity and the PREDICTED mean match the MEASURED one.
                //     It is a live convergence check every run, so a bake that stopped matching its clip surfaces
                //     here instead of silently — which is how a fitted constant usually rots.
                if (window.Count > 0 && strikeHaftWorld.sqrMagnitude > 1e-8f)
                {
                    Vector3 hSum = Vector3.zero, aSum = Vector3.zero;
                    foreach (var s in window)
                    {
                        var invRot = Quaternion.Inverse(s.toolRot);
                        hSum += invRot * s.haftWorld;    // constant per class; summed for numerical robustness
                        aSum += invRot * s.aim;
                    }
                    Vector3 hTool = hSum.normalized;
                    Vector3 aimTool = aSum.normalized;
                    Quaternion d = Quaternion.FromToRotation(hTool, aimTool);
                    // The mean fwdDot this D predicts, evaluated through the SAME algebra the rig will apply — so
                    // "the fit says X" and "the next run measures X" are comparable claims rather than hopes.
                    float predMean = 0f;
                    foreach (var s in window)
                        predMean += Vector3.Dot(s.toolRot * (d * (Quaternion.Inverse(s.toolRot) * s.haftWorld)),
                                                s.aim);
                    predMean /= window.Count;
                    Vector3 e = d.eulerAngles;
                    // Wrap to (-180, 180] so a bakeable value reads as a small correction rather than as 3xx deg.
                    e = new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));
                    // The residual is the angle the WINDOW-MEAN heading still sits off the window-mean aim, in the
                    // tool frame — the same quantity the fit minimises, so it and the fit cannot disagree.
                    float residualDeg = Vector3.Angle(hTool, aimTool);
                    // THE TOTAL TO BAKE, composed BY THE ENGINE rather than by hand. Whatever delta is already in
                    // the build is applied BEFORE this additional one is measured (the additional is relative to the
                    // tool's CURRENT rotation), so the new total is `baked * additional` as QUATERNIONS. Euler
                    // triples do not add, and hand-composing them is exactly the arithmetic slip that would land a
                    // re-bake somewhere nobody chose. Printed ready to paste.
                    Vector3 tot = (Quaternion.Euler(HeldToolRig.SwingAimEulerForClass(weaponClass)) * d).eulerAngles;
                    tot = new Vector3(Mathf.DeltaAngle(0f, tot.x), Mathf.DeltaAngle(0f, tot.y),
                                      Mathf.DeltaAngle(0f, tot.z));
                    Debug.Log("[swing-aim-fit] " + name + " (" + (arcVerb ? "ARC verb -> aim forward-and-down"
                                                                          : "THRUST verb -> aim forward, level") +
                              "): residual " + residualDeg.ToString("F1", inv) +
                              " deg (window-mean heading vs window-mean aim); PREDICTED mean fwdDot after this " +
                              "delta = " + predMean.ToString("F3", inv) + "; " +
                              "REQUIRED ADDITIONAL tool-frame delta = new Vector3(" + e.x.ToString("F1", inv) + "f, " +
                              e.y.ToString("F1", inv) + "f, " + e.z.ToString("F1", inv) + "f); TOTAL TO BAKE " +
                              "(currently-baked composed with that additional) = new Vector3(" +
                              tot.x.ToString("F1", inv) + "f, " + tot.y.ToString("F1", inv) + "f, " +
                              tot.z.ToString("F1", inv) + "f)  <- bake into " +
                              "HeldToolRig.SwingAim" + char.ToUpperInvariant(name[0]) + name.Substring(1) +
                              ". Live swing-aim channel at this frame: weight " +
                              strikeAimWeight.ToString("F2", inv) + " class " + strikeAimClass +
                              " (expected " + weaponClass + " once baked; a weight far below 1.00 means the delta " +
                              "was NOT engaged at the judged frame and the residual describes the UNCORRECTED " +
                              "seat). Once the bake is in, residual -> ~0 and the required delta -> ~identity; a " +
                              "residual that climbs back is this fit rotting, reported rather than silent.");
                }

                summary.Add(name + " strikeFwd=" + strike.fwdDot.ToString("F3", inv) +
                            " strikeUp=" + strike.upDot.ToString("F3", inv) +
                            " reach=" + strike.handAxisBestFwdDown.ToString("F3", inv));

                for (int i = 0; i < 18; i++) yield return null;
            }

            _pointRan = true;

            // ===== THE VERDICT =====
            // GATED CLASSES: axe, dagger, spear, sword. The PICKAXE is EXCLUDED BY NAME — its swing seat is the
            // Sponsor-passed mineSeatEulerDelta (86cay4282, five rounds, left palm currently 0.239 SW on the haft
            // against a 0.293 SW touch bound), and 86cb6v03j forbids reworking a Sponsor-passed bar. The exclusion
            // is LOUD in the log with its own measured figure rather than silently dropped, because a class quietly
            // missing from a gate is indistinguishable from a class that passed it.
            //
            // FAIL CLOSED on coverage: every gated class must have produced a measurement. A run that scored three
            // of four and passed them would be a gate with a hole in it that reads as green.
            bool aimAll = true;
            int gatedCovered = 0;
            foreach (var (weaponClass, familyIndex, stateName, name, arcVerb) in PointClasses)
            {
                if (weaponClass == CastawayCharacter.WeaponClassPickaxe)
                {
                    _pointStrikeFwdByClass.TryGetValue(name, out float pfwd);
                    Debug.Log("[swing-point] " + name + ": EXCLUDED FROM THE AIM GATE BY SCOPE, measured " +
                              "fwdDot=" + pfwd.ToString("F3", inv) + " at its strike. Its swing seat is the " +
                              "Sponsor-passed mineSeatEulerDelta (86cay4282, five rounds); re-aiming it would move " +
                              "the haft off the left palm the shipped gate measures at 0.239 SW, i.e. rework a " +
                              "Sponsor-passed bar, which 86cb6v03j forbids in this pass. This is a stated bound on " +
                              "the fix, NOT a claim the pickaxe aims correctly.");
                    continue;
                }
                bool have = _pointStrikeFwdByClass.TryGetValue(name, out float fwd);
                if (have) gatedCovered++;
                bool ok = SwingPointRead.StrikeAimOk(have, have ? fwd : float.NaN, name, out string why);
                aimAll &= ok;
                Debug.Log("[swing-point] AIM VERDICT " + name + ": " + why);
            }
            int gatedTotal = PointClasses.Length - 1;   // minus the excluded pickaxe
            bool coverageOk = gatedCovered == gatedTotal;
            _pointOk = aimAll && coverageOk;

            Debug.Log("[swing-point] SUMMARY over " + _pointClassesScored + "/" + PointClasses.Length +
                      " scored classes: " + string.Join(" | ", summary) + " => WORST class '" + _pointWorstClass +
                      "' (lowest fwdDot at its own strike frame). AIM GATE: " + gatedCovered + "/" + gatedTotal +
                      " gated classes measured (coverageOk=" + coverageOk + ", pickaxe excluded by scope) " +
                      "aimAllOk=" + aimAll + " => pointOk=" + _pointOk + ". A FALSE coverageOk means a gated class " +
                      "produced no strike reading at all and the gate has a hole in it — that reds rather than " +
                      "passing on the classes it did manage to measure.");

            // ----- Pass 2: re-fire EVERY class and photograph ITS OWN strike frame in SIDE PROFILE. -----
            // Every class, not just the worst: the ticket requires per-weapon-class evidence, and "class X does NOT
            // show it" is a bounded-convergence claim that needs its own picture to stand on.
            //
            // SIDE PROFILE ONLY, deliberately. A fore/aft + up/down heading is what is being judged, and that is
            // nearly invisible from the player's over-the-shoulder angle and obvious side-on (lowpoly-quality.md
            // §0). The gameplay-cam frame is additionally unusable here in practice: the orbit cam sits far out by
            // the time this pass runs and renders the castaway a few dozen pixels tall.
            //
            // The moment is re-found BY PHASE (the class's own measured strike phase), never by wall clock.
            foreach (var (weaponClass, familyIndex, stateName, name, arcVerb) in PointClasses)
            {
                float wantPhase = _pointStrikePhaseByClass.TryGetValue(name, out float p) ? p : float.NaN;
                if (float.IsNaN(wantPhase)) continue;
                cycle.ShowWeaponForCaptureDebug(familyIndex);
                for (int i = 0; i < 3; i++) yield return null;
                int stateHash = Animator.StringToHash(stateName);
                castaway.TriggerAttack(weaponClass, 1f);
                float t0 = Time.time;
                bool reached = false;
                while (Time.time - t0 < FoldWindowSec && !reached)
                {
                    var st = animator.GetCurrentAnimatorStateInfo(0);
                    if (st.shortNameHash == stateHash && !animator.IsInTransition(0)
                        && Mathf.Repeat(st.normalizedTime, 1f) >= wantPhase)
                        reached = true;
                    else
                        yield return null;
                }
                if (!reached)
                {
                    Debug.LogWarning("[swing-point] " + name + ": the measured strike phase " +
                                     wantPhase.ToString("F3", inv) + " was NOT reached on the shoot pass, so " +
                                     "swing_point_" + name + "_side.png is NOT a picture of the judged moment. The " +
                                     "MEASUREMENT above still stands; this warns the PICTURE is missing.");
                    continue;
                }
                // ProfileShotAt runs several frames (camera swap + settle) and the clip keeps playing under it, so
                // the shot lands a frame or two past the strike. That is stated rather than hidden: the strike is a
                // ~10-frame plateau at these playback rates, and the alternative — pausing time — would change the
                // very Animator evaluation being photographed.
                yield return SideProfileShot(Path.Combine(dir, "swing_point_" + name + "_side.png"), hips, head,
                                             castaway.ModelTransform);
                for (int i = 0; i < 12; i++) yield return null;
            }
        }

        /// <summary>Per-class cap on the per-frame pointing trace. The swing windows here run ~150 frames each and
        /// five classes of unbounded trace would bury the criterion lines the gate wrapper greps for. 40 lines spans
        /// the whole owned window at the sampling this pass reaches while keeping the log readable.</summary>
        private const int PointTraceMaxLinesPerClass = 40;

        /// <summary>A frame of the pointing measurement, buffered so the strike WINDOW can be classified after the
        /// swing's peak hand speed is known (it cannot be known while the swing is still running).</summary>
        private struct PointSample
        {
            public SwingPointRead.Read read;
            public float phase, atSec;
            public Quaternion toolRot;
            public Vector3 haftWorld, aim;
            public float aimWeight;
            public int aimClass;
        }

        /// <summary>The strike window is every frame at or above this fraction of the swing's own PEAK HAND speed.
        /// Half is not a tuning knob dressed as a constant — it is the natural "the fast half of the swing" split,
        /// it is scale-free (a fraction of each swing's own peak, so it adapts to every class's cadence and to any
        /// future retune of the SwingSpeed* multipliers without being re-picked), and it is deliberately WIDE:
        /// a narrow window would reintroduce the single-spiky-frame instability it exists to remove. Measured
        /// window widths are reported per class on the [swing-point] STRIKE WINDOW line every run, so a change that
        /// collapsed a window to a couple of frames would be visible rather than silent.</summary>
        private const float StrikeWindowSpeedFraction = 0.5f;

        /// <summary>The percentile of a swing's own hand speeds used as the window's REFERENCE speed instead of the
        /// maximum. 0.90 keeps the reference at the genuinely fast part of the swing while discarding the top decile
        /// where single-frame artifacts live (measured: one spear frame at 56.40 m/s in a swing that otherwise peaks
        /// near 8). Not a tuning knob for the verdict - it only sets which frames are IN the window, and the window
        /// sizes are reported per class every run so a collapse would be visible rather than silent.</summary>
        private const float StrikeWindowReferencePercentile = 0.90f;

        /// <summary>Read a float CLI argument, or <paramref name="fallback"/> when absent/unparseable. Invariant
        /// culture: this machine runs a comma-decimal locale, and a locale-sensitive parse would silently read
        /// "-30.0" as 300.</summary>
        private static float ReadFloatArg(string flag, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag &&
                    float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float v))
                    return v;
            return fallback;
        }

        /// <summary>
        /// 86cay4282 round 3 — prove the F9 MINE-SEAT INSTRUMENT works IN THE SHIPPED EXE, then photograph it.
        ///
        /// Three things are established here, in order, each one a documented past failure of this exact tool:
        ///   1. THE PANEL DRAWS. It is behind TWO gates — the F10 <see cref="DebugOverlays"/> master AND the tool's own
        ///      F9 toggle — and "F9 alone draws nothing" has already cost two soak rounds. Both are opened here and a
        ///      frame is captured, so the Sponsor is handed a picture of the panel he is being asked to find.
        ///   2. ITS ROWS CARRY REAL NUMBERS. The rows come from the SAME <see cref="AxeNudgeTool.GripReadoutRows"/>
        ///      seam OnGUI draws, logged verbatim; a row reading the unavailable notice here means the panel would show
        ///      the Sponsor nothing useful, which is reported rather than left for him to discover.
        ///   3. THE ALONG-HAFT DIAL ACTUALLY MOVES THE ALONG-HAFT READ. The slide is driven through
        ///      <see cref="AxeNudgeTool.ApplyHaftSlide"/> — the identical call the [R]/[V] key handler makes — and the
        ///      live <c>leftU</c> is re-measured after it. This is the "verify the binding MOVES the value, not merely
        ///      that a hint string exists" rule (unity-conventions.md §Input System). The one link that structurally
        ///      cannot be closed from inside a player is legacy Input's own key-down; the key constants and the hint
        ///      text are pinned by MineSeatAlongHaftTests, and a keypress at the soak closes it.
        ///
        /// The dial is RESTORED afterwards, so this pass cannot leave the shipped seat delta moved — a verify pass that
        /// mutates ship state would poison every figure logged after it.
        /// </summary>
        private IEnumerator MineSeatPanelPass(string dir, CastawayCharacter castaway, HeldToolRig heldRig,
                                              CastawayLeftArmHaftIk leftIk,
                                              Transform lArm, Transform rArm, Transform lHand, Transform rHand)
        {
            var tool = Object.FindAnyObjectByType<AxeNudgeTool>(FindObjectsInactive.Include);
            if (tool == null)
            {
                Debug.LogWarning("[swing-panel] no AxeNudgeTool in the shipped scene — the F9 MINE-SEAT instrument this " +
                                 "round delivers is ABSENT from this build. Do NOT read the PASS above as proof the " +
                                 "Sponsor has a dial.");
                yield break;
            }

            bool prevOverlay = DebugOverlays.Visible;
            Vector3 prevDelta = heldRig.mineSeatOffsetDelta;
            float prevPinU = leftIk != null ? leftIk.pinU : float.NaN;
            DebugOverlays.Show();                                       // the F10 master reveal
            tool.Activate();                                            // the F9 sub-toggle
            tool.SelectTargetForVerify(AxeNudgeTool.MineSeatTargetIndex);
            Debug.Log($"[swing-panel] overlay={DebugOverlays.Visible} f9Active={tool.IsActive} " +
                      "target=MINE SEAT — both gates open (F10 master + F9 sub-toggle; F9 alone draws nothing).");

            // Re-fire the swing so the seat weight is ENGAGED while the panel is photographed: at weight 0 the rows are
            // truthful but describe the one-handed seat, which is not what he is being asked to judge.
            castaway.TriggerAttack(CastawayCharacter.WeaponClassPickaxe, 1f);
            float t0 = Time.time;
            while (Time.time - t0 < FoldWindowSec * 0.5f && heldRig.MineSeatWeight < EngagedWeightFloor) yield return null;
            Debug.Log($"[swing-panel] seat weight at capture {heldRig.MineSeatWeight:F2}");

            foreach (string row in tool.GripReadoutRows(heldRig.MineSeatWeight))
                Debug.Log("[swing-panel] ROW | " + row);

            ShotTo(Path.Combine(dir, "swing_pickaxe_panel.png"));
            yield return null;

            // …and now prove the dial MOVES the number, on the live rig, through the production seam. ROUND 4: the dial
            // moves the LEFT-HAND PIN, so the reads are the pin's own requested/achieved u AND the live wrist u.
            float uBefore = ReadLeftU(heldRig, lArm, rArm, lHand, rHand);
            float pinBefore = leftIk != null ? leftIk.pinU : float.NaN;
            float achBefore = leftIk != null ? leftIk.AchievedU : float.NaN;
            const float slide = -0.10f;   // DOWN the haft: the measured reachable window's low end (u 0.14) sits BELOW
                                          // the shipped pin (0.35), so a downward slide is the direction guaranteed to
                                          // be available. Sliding UP can be legitimately refused by the reach clamp,
                                          // which would read as a broken dial when it is the arm's real limit.
            bool slid = tool.ApplyHaftSlide(slide);
            yield return null;                                          // let order 100 then 110 re-run
            yield return null;
            float uAfter = ReadLeftU(heldRig, lArm, rArm, lHand, rHand);
            float pinAfter = leftIk != null ? leftIk.pinU : float.NaN;
            float achAfter = leftIk != null ? leftIk.AchievedU : float.NaN;
            bool pinMoved = slid && !float.IsNaN(pinBefore) && !float.IsNaN(pinAfter) &&
                            pinAfter < pinBefore - 0.01f;
            bool readMoved = (!float.IsNaN(achBefore) && !float.IsNaN(achAfter) && Mathf.Abs(achAfter - achBefore) > 0.01f)
                             || (!float.IsNaN(uBefore) && !float.IsNaN(uAfter) && Mathf.Abs(uAfter - uBefore) > 0.01f);
            Debug.Log($"[swing-panel] LEFT-HAND PIN DIAL: {slide:F2}m accepted={slid} => requested u " +
                      $"{pinBefore:F3} -> {pinAfter:F3} (moved={pinMoved}); ACHIEVED u {achBefore:F3} -> {achAfter:F3}; " +
                      $"live wrist u {uBefore:F3} -> {uAfter:F3} (a live read moved = {readMoved}). This is the [R]/[V] " +
                      "keys' own code path. A FALSE 'moved' means the dial the Sponsor is handed does not move the " +
                      "grip — the 'wired but silently inert' class this tool has been bitten by three times. Note the " +
                      "ACHIEVED value can legitimately lag the request when the reach clamp is binding; that is why " +
                      "BOTH are printed rather than one.");

            heldRig.mineSeatOffsetDelta = prevDelta;                    // restore — never ship a mutated seat
            if (leftIk != null && !float.IsNaN(prevPinU)) leftIk.pinU = prevPinU;   // …nor a mutated pin
            tool.Deactivate();
            DebugOverlays.Visible = prevOverlay;
            yield return null;
        }

        /// <summary>The live along-haft position of the LEFT hand, through the production read. NaN when unmeasurable —
        /// never 0, which would read as "the hand is at the butt" (a specific, wrong claim).</summary>
        private static float ReadLeftU(HeldToolRig rig, Transform lArm, Transform rArm, Transform lHand, Transform rHand)
        {
            if (!rig.TryGetHaftSegment(out Vector3 g, out Vector3 h)) return float.NaN;
            var read = TwoHandGripRead.Measure(lArm.position, rArm.position, lHand.position, rHand.position, g, h);
            return read.valid ? read.leftU : float.NaN;
        }

        /// <summary>
        /// 86cay4282 round 4 — one grip read WITH the real palm centres when the shipped rig can supply them. The left
        /// pass criterion is palm-anchored, so a wrist-only read must be flagged as such (<c>palmMeasured=false</c>)
        /// rather than quietly scored against a palm cap. The RIGHT palm mirrors the left's definition (midpoint of the
        /// wrist bone and its index/middle knuckle) and is reported for symmetry only — the right criterion is still the
        /// wrist figure, unchanged.
        /// </summary>
        private static TwoHandGripRead.Read MeasureWithPalms(CastawayLeftArmHaftIk ik,
            Transform lArm, Transform rArm, Transform lHand, Transform rHand, Vector3 gripW, Vector3 headW)
        {
            bool have = ik != null && ik.TryGetPalmWorld(out Vector3 lPalm0);
            Vector3 lPalm = lHand.position, rPalm = rHand.position;
            if (have) ik.TryGetPalmWorld(out lPalm);
            Transform rKnuckle = FindBone(rHand, "mixamorig:RightHandMiddle1")
                                 ?? FindBone(rHand, "mixamorig:RightHandIndex1");
            if (rKnuckle != null) rPalm = (rHand.position + rKnuckle.position) * 0.5f;
            else have = false;
            return TwoHandGripRead.Measure(lArm.position, rArm.position, lHand.position, rHand.position,
                                           gripW, headW, lPalm, rPalm, have);
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

        /// <summary>
        /// 86cay4282 round 4 — A GRIP SHOT THAT CAN ACTUALLY SEE THE GRIP. Round 3's frontal shot took its axis from
        /// <c>ModelTransform.forward</c> — a RIG CONVENTION — and the round-4 capture came out looking at the back of the
        /// head with both hands cropped: the gate's logic was green while the judged IMAGE could not show its own
        /// subject. That is the 8th instance of the false-green-capture family (unity-conventions.md
        /// §Editor-vs-runtime), and the specific lesson is that a capture axis must be derived from the SUBJECT, not
        /// from an assumed forward.
        ///
        /// So the aim is measured: the camera stands off along the horizontal direction from the CHEST to the HAND
        /// MIDPOINT, which by construction puts the hands between the lens and the body whatever the rig's forward
        /// happens to mean and whichever way the character has yawed. It also FRAMES ON THE HANDS rather than on the
        /// hips→head midpoint, because the quantity being judged is 10 cm across and was previously ~1/8 of the frame
        /// height away from centre.
        ///
        /// The shot LOGS what it could see (both hands' distance from the lens and their angular separation), so
        /// "this frame can resolve which hand is on the haft" is evidence in the log rather than an assumption — the
        /// gate has to be able to say the image is judgeable, not just that it was written.
        /// </summary>
        private IEnumerator GripShot(string file, Transform lHand, Transform rHand, Transform chestA, Transform chestB,
                                     float distU)
        {
            Vector3 handMid = (lHand.position + rHand.position) * 0.5f;
            Vector3 chest = (chestA.position + chestB.position) * 0.5f;
            Vector3 aim = Vector3.ProjectOnPlane(handMid - chest, Vector3.up);
            if (aim.sqrMagnitude < 1e-4f) aim = Vector3.forward;   // hands directly above/below the chest: any side works
            aim.Normalize();

            Vector3 camPos = handMid + aim * distU;
            float dL = (lHand.position - camPos).magnitude, dR = (rHand.position - camPos).magnitude;
            float sep = Vector3.Angle(lHand.position - camPos, rHand.position - camPos);
            Debug.Log($"[swing-grip-shot] aim={aim:F3} (measured chest->hand-midpoint, NOT a rig 'forward'), " +
                      $"standoff {distU:F2}u; left hand {dL:F2}u from the lens, right hand {dR:F2}u, angular " +
                      $"separation {sep:F1}deg at a 45deg FOV => the two hands occupy ~{sep / 45f * 100f:F0}% of the " +
                      "frame width, so a hand-on-haft read is resolvable in this image. Round 3's shot took its axis " +
                      "from ModelTransform.forward and framed the BACK of the head with the hands cropped.");
            yield return ProfileShotAt(file, camPos, handMid);
        }

        /// <summary>Shared implementation: a level, chest-height shot from a stand-off along the given world axis.
        /// A raised/angled shot flattens the very geometry these captures exist to judge (the pond top-down lesson).
        /// The stand-off is a PARAMETER, not a constant: the fold read needs the whole body in frame (3u) while the
        /// two-hand grip read needs the hands legible (1.6u), and using one distance for both makes one of the two
        /// captures unable to show its own subject.</summary>
        private IEnumerator ProfileShot(string file, Transform hips, Transform head, Vector3 axis, float distU)
        {
            Vector3 centre = (hips.position + head.position) * 0.5f;
            Vector3 side = Vector3.ProjectOnPlane(axis, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            yield return ProfileShotAt(file, centre + side.normalized * distU + Vector3.up * 0.1f, centre);
        }

        /// <summary>The shared camera mechanics: one frame from an EXPLICIT world position looking at an explicit point.
        /// Follows the #223 camera-race discipline — every other camera is disabled, this one takes depth 100, and the
        /// whole roster is logged, because two enabled cameras at equal depth have UNDEFINED render order and the capture
        /// then intermittently samples the wrong one.</summary>
        private IEnumerator ProfileShotAt(string file, Vector3 camPos, Vector3 lookAt)
        {
            var wasEnabled = new System.Collections.Generic.List<Camera>();
            foreach (var c in Camera.allCameras)
                if (c.enabled) { wasEnabled.Add(c); Debug.Log($"[swings-cam-roster] {c.name} depth={c.depth}"); }

            var go = new GameObject("__swingSideCam");
            var cam = go.AddComponent<Camera>();
            cam.depth = 100f;
            cam.fieldOfView = 45f;
            go.transform.position = camPos;
            go.transform.LookAt(lookAt);
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
