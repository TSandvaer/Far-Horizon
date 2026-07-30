using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// 86cay4282 round 4 — PIN THE LEFT HAND TO THE HAFT, PER FRAME. The Sponsor, soaking round 3, verbatim:
    /// <c>"R/V only manipulates the right hand, which is great, but what about the left hand? its not even touching
    /// the shaft"</c>. He is right: at the measured mean shoulder width 0.4580 m the shipped left hand sits 0.445 SW =
    /// 20.4 cm mean / 0.615 SW = 28.2 cm worst off the haft, while <see cref="TwoHandGripRead.LeftHaftPassSW"/> was
    /// 0.80 SW = 36.6 cm — a cap calibrated from what a static seat could ACHIEVE, not from what "one haft passing
    /// through both hands" MEANS. So the gate was green on a hand gripping air by a quarter of a metre.
    ///
    /// WHY A PER-FRAME SOLVE. Two round-3 measurements settle it and are not re-derived here: the haft is NOT too short
    /// ("fits-on-the-haft? max separation 0.72" — under 1 means both hands fit), and no single CONSTANT seat can close
    /// the gap, because the clip's own hand-line direction wanders 21.0deg mean / 36.6deg MAX about its mean and ONE
    /// constant can only match the mean — the residual IS the wander. Removing a per-frame residual needs a per-frame
    /// mechanism.
    ///
    /// ORDER — <see cref="DefaultExecutionOrder"/> 110, AFTER the seat. The chain this joins is non-negotiable
    /// (procedural-animation-verbs.md):
    ///
    ///     Animator → CastawayArmPose (50) → CastawayFingerCurl (60) → CastawayHandPose (65)
    ///              → CastawayFootYaw (70) → HeldToolRig (100) → ** THIS (110) **
    ///
    /// The target is a point ON THE HAFT, and the haft is only placed once <see cref="HeldToolRig"/> has seated it off
    /// the RIGHT hand at order 100. Running earlier would aim at last frame's haft and then be overwritten by orders
    /// 50/65 — round 2's ordering failure in a new place. This writes ONLY left-arm bones, so it cannot feed back into
    /// the seat (which reads the RIGHT hand) and the approved right-hand grip is untouched.
    ///
    /// ⚠ THE MEASURED CONSTRAINT THAT SHAPED THIS (the design assumption that died to a measurement — `[left-ik]` /
    /// `[left-span]` in `AttackClipPoseDiag`). The obvious design — pin at a fixed u along the haft and blend out when
    /// unreachable — is REFUTED: the left arm's full extension is 0.5401 m (28.19 shoulder→elbow + 25.82 elbow→palm)
    /// and against the shipped seat the pin at ANY fixed u is beyond that on ~64% of judged frames (worst-frame reach
    /// 1.18–1.49x extension across u 0.00..0.80). A blend-out-on-over-reach would leave the IK INERT for most of the
    /// swing and the Sponsor would see the same defect. So the pin is CLAMPED into what the arm can actually hold:
    ///
    ///   • Intersect the haft SEGMENT with the arm's usable shell SPHERE about the left shoulder. Non-empty (86/166
    ///     judged frames) ⇒ pin at the reachable point NEAREST the Sponsor's preferred <see cref="pinU"/> — the palm
    ///     lands EXACTLY on the haft (measured 0.0000 m) and the elbow only extends as far as it must.
    ///   • Empty (80/166 frames — the WHOLE haft is beyond the arm, worst closest-approach 0.6340 m vs a 0.5293 m
    ///     shell) ⇒ pin at the CLOSEST point of the haft and let the solver's shell clamp hold the arm aimed at it.
    ///     Blending OUT there would hand the frame back to the clip pose — the 20–28 cm defect — on half the swing,
    ///     which is worse than a reach. Measured result at the shipped shell: palm→haft 2.2 cm mean / 10.7 cm worst,
    ///     inside the 11.1 cm mesh-measured touch tolerance at EVERY frame.
    ///
    /// ROOT CAUSE OF THE RESIDUAL, NAMED (fix-the-cause discipline — this PR does not fix it, it names the lever): the
    /// 10.7 cm worst case is a SEAT-DISTANCE consequence, not slack in the solve. The shipped seat was fitted for the
    /// right hand's grip and the head-driving-down read, and it parks the haft up to 63.4 cm from the left shoulder
    /// against a 54.0 cm arm. Bringing the tool ~10 cm closer to the body would make the whole swing comfortably
    /// reachable and drive the residual to ~0. That is a seat re-fit with a left-arm-reach objective — a separate
    /// ticket, and explicitly out of scope here (the chop/mine seat is OOS on this one).
    ///
    /// IDIOM. No new Animator clip / state / layer / AvatarMask; a `LateUpdate` write on two bones, weighted by the
    /// SAME production gate policy (<see cref="CastawayArmPose.NextMineDeGripWeight"/>) the arm offset and the seat
    /// delta use, so the three can never ease out of step. At weight 0 nothing is written at all, so every non-mining
    /// state — carry, idle, walk, run, jump and the other four swings — is BYTE-IDENTICAL.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class CastawayLeftArmHaftIk : MonoBehaviour
    {
        [Header("Left-arm chain (wired editor-time from the SkinnedMeshRenderer.bones skeleton)")]
        [Tooltip("mixamorig:LeftArm — the shoulder joint (the IK root).")]
        public Transform leftUpperArm;
        [Tooltip("mixamorig:LeftForeArm — the elbow joint (the IK mid).")]
        public Transform leftForeArm;
        [Tooltip("mixamorig:LeftHand — the wrist bone. NOT the IK tip: the tip is the PALM CENTRE, because the anchor " +
                 "is 'the haft passes through the closed hand' and the palm sits 5.6 cm in front of this bone.")]
        public Transform leftHand;
        [Tooltip("The KNUCKLE bone the palm centre is measured against — palm = midpoint(leftHand, this). Resolved " +
                 "editor-time from a CANDIDATE LIST, because the v4 hero is a fist-hand variant carrying only index + " +
                 "thumb bones: 'mixamorig:LeftHandMiddle1' (the obvious proxy on a full Mixamo hand) does NOT exist " +
                 "here and 'mixamorig:LeftHandIndex1' is what resolves. If unwired the whole driver is INERT (fail-" +
                 "closed) rather than silently falling back to the wrist, which would pin the haft through the back " +
                 "of the hand by 5.6 cm.")]
        public Transform leftPalmKnuckle;

        [Header("What it pins to")]
        [Tooltip("The held-tool rig whose seated haft this pins to. Read at order 100, so the segment is final by the " +
                 "time this runs. Unwired => INERT.")]
        public HeldToolRig heldRig;
        [Tooltip("The CastawayCharacter whose AttackPickaxe layer-0 state gates the pin. Wired editor-time; a runtime " +
                 "fallback resolves it from the parent chain. Unresolved => weight 0 => nothing written (fail-closed " +
                 "toward the clip's own authored left arm).")]
        public CastawayCharacter character;
        [Tooltip("The FACING-CARRYING model transform the pole fallback direction is expressed in (CastawayCharacter " +
                 "yaws the _model child — 'the visual owns facing', unity-conventions.md §FBX). Using the character " +
                 "ROOT instead would make the fallback pole rotate with nothing.")]
        public Transform modelFrame;

        [Header("THE DIAL — where along the haft the left hand is pinned")]
        [Tooltip("PREFERRED position of the left palm along the haft: 0 = BUTT end, 1 = HEAD end. Clamped per frame " +
                 "into the part of the haft the arm can actually reach, so this is a preference the solve honours " +
                 "wherever it can — the F9 MINE-SEAT panel draws both this and the ACHIEVED value. Dialed live with " +
                 "[R]/[V]; the measured reachable window against the shipped seat is u 0.14..0.61 (12..52 cm up an " +
                 "85 cm haft), and it is bounded by ARM REACH rather than by the clip's hand spacing — which is what " +
                 "re-opens the mid-haft choked-up grip the Sponsor asked for. Bake into " +
                 "MovementCameraScene.LeftArmHaftPinU.")]
        [Range(0f, 1f)] public float pinU = 0.35f;

        [Tooltip("Upper limit on the pin, from the MESH: above it the palm is inside the tool HEAD mass, which reads " +
                 "worse than the defect being fixed. Measured 0.80 for the stone pickaxe (AttackClipPoseDiag " +
                 "[haft-profile]: bare-stick radius baseline 0.0526 of the haft length, head geometry starts at " +
                 "u=0.80). Baked from MovementCameraScene.LeftArmHaftPinUCeiling.")]
        [Range(0.1f, 1f)] public float pinUCeiling = 0.80f;

        [Header("Reach guards (the 'never snap the arm straight' pair)")]
        [Tooltip("Fraction of FULL extension the target is clamped to — the one knob trading 'how close does the palm " +
                 "get' against 'how straight does the arm go'. MEASURED trade curve over 166 judged frames of the " +
                 "shipped mine clip: 0.90 => palm 4.8 cm mean / 15.0 cm worst, elbow 36..128deg | 0.94 => 3.4 / 12.9 " +
                 "cm, elbow 36..140deg | 0.98 => 2.2 / 10.7 cm, elbow 36..157deg. 0.98 ships because it is the ONLY " +
                 "value whose WORST frame (10.7 cm) is inside the mesh-measured 11.1 cm touch tolerance — i.e. the " +
                 "only one that satisfies the anchor at every frame. 157deg is extended but NOT locked (180 would " +
                 "be); if it reads too straight at the soak this is the value to lower, and the cost is priced above. " +
                 "Dialed live with [Z]/[X]. Bake into MovementCameraScene.LeftArmHaftShellFraction.")]
        [Range(0.5f, 0.98f)] public float shellFraction = 0.98f;

        [Tooltip("Metres of over-reach HELD AT FULL STRENGTH before the blend-out begins. ⚠ THIS FIELD EXISTS BECAUSE " +
                 "THE SHIPPED GATE CAUGHT ITS ABSENCE. Round 4's first build had no hold band, so the ease began at the " +
                 "shell edge and the frames with the LARGEST over-reach — exactly the ones needing the reach most — got " +
                 "the pin at partial strength: the worst frame ran at reachWeight 0.65 and measured a 13.5 cm palm gap " +
                 "against a 13.0 cm touching bound, i.e. the blend-out itself caused the FAIL. A clamped solve is " +
                 "already safe (the arm cannot pass the shell), so full strength across the real working range is " +
                 "correct. 0.25 m sits clear above the measured worst over-reach of 10.5 cm.")]
        public float reachHoldMetres = 0.25f;

        [Tooltip("Metres of over-reach BEYOND the hold band across which the pin blends out entirely. The guard for an " +
                 "absurd target (a re-seated tool parked a metre away), not normal behaviour: with the hold band above " +
                 "it measures 0 frames of partial weight across the shipped swing.")]
        public float reachFalloff = 0.30f;

        [Tooltip("Pole FALLBACK direction, in the model frame, used only when the clip's own elbow projects too close " +
                 "to the shoulder->target axis to define a bend plane. MEASURED, not guessed: the mean clip elbow " +
                 "direction off the left shoulder in the model frame is (0.269, -0.963, -0.024) — i.e. essentially " +
                 "straight DOWN with a slight outward lean. Baked from MovementCameraScene.LeftArmHaftPoleFallback.")]
        public Vector3 poleFallbackLocal = new Vector3(0.269f, -0.963f, -0.024f);

        [Tooltip("DEBUG/TEST: force the gate OPEN so the pin engages without an actual mine swing. Exactly the " +
                 "CastawayFingerCurl.alwaysCurl idiom, and it exists for the same reason that one does: an " +
                 "engagement-weighted effect that only appears mid-swing is indistinguishable from a broken one, and " +
                 "the Sponsor has been burned by that twice (run-lower). The F9 MINE-SEAT target sets this WHILE " +
                 "SELECTED and clears it on cycle-away/close, so he can see the pin the moment he opens the panel " +
                 "instead of having to hold a swing; normal play never touches it. Ships FALSE, so gameplay is gated " +
                 "purely on the AttackPickaxe state as designed.")]
        public bool debugForceEngaged;

        [Tooltip("Per-second ENGAGE rate for the pin weight. Matches CastawayArmPose.mineDeGripBlendRate and " +
                 "HeldToolRig.mineSeatBlendRate (12/s ~= 0.25 s to 95%) DELIBERATELY: the arm offset, the seat delta " +
                 "and this pin share ONE gate and ONE ease, so the hand can never arrive before the haft does. " +
                 "Measured: reaches 0.95 at ~0.26 s after the trigger (asserted, round 5).")]
        public float blendRate = 12f;

        [Tooltip("86cay4282 ROUND 5 — per-second RELEASE rate. ⚠ THIS FIELD EXISTS BECAUSE THE SPONSOR'S SOAK FOUND " +
                 "WHAT FOUR GREEN GATES COULD NOT: rounds 1-4 only ever measured this pin ENGAGED. Nothing measured it " +
                 "DISENGAGING, and at the shipped symmetric 12/s the live trace shows that on the FIRST frame layer 0 " +
                 "had left AttackPickaxe — body already in Idle — the pin was still writing at weight 0.819, pulling " +
                 "the palm 58.4 cm and the upper arm 60.1deg off the idle pose, and it took 0.350 s to settle. " +
                 "Verbatim: 'the left arm does not return to normal position after the pickaxe two hand motion'. " +
                 "DERIVED from the deadline rather than picked: 60deg -> 1deg takes ln(60)/rate and the binding window " +
                 "is the SHORTER crossfade out the controller authors (0.10 s -> Locomotion), so rate >= 40.9/s; 42/s " +
                 "clears it at 0.097 s and the arm returns WITH the body. Shared with " +
                 "CastawayArmPose.mineDeGripReleaseRate + HeldToolRig.mineSeatReleaseRate so the three cannot ease out " +
                 "of step. Ship source: MovementCameraScene.MineWeightReleaseRate.")]
        public float releaseBlendRate = 42f;

        // ---- live state, all instance fields (no mutable runtime statics — StaticStateResetTests stays green) ----
        private float _weight;
        private float _achievedU = float.NaN;
        private float _palmToHaft = float.NaN;
        private float _reachWeight;
        private bool _spanEmpty;
        private bool _poleFallback;
        private bool _lastSolved;

        /// <summary>The smoothed pin weight (0 everywhere except while the AttackPickaxe swing owns layer 0). Exposed
        /// because this driver is ENGAGEMENT-WEIGHTED, and a debug dial targeting an engagement-weighted value MUST
        /// surface the weight or a not-engaged dial cannot be told apart from a broken handler
        /// (procedural-animation-verbs.md §Debug-instrument caveat — the trap that burned the Sponsor twice on
        /// run-lower). Also the shipped gate's engaged read.</summary>
        public float PinWeight => _weight;

        /// <summary>The position along the haft the pin ACTUALLY landed on last frame after the reach clamp, or NaN
        /// when nothing was solved. Distinct from <see cref="pinU"/> — a dial whose requested and achieved values can
        /// differ must show BOTH, or it reads as ignoring input.</summary>
        public float AchievedU => _achievedU;

        /// <summary>Last frame's PALM-CENTRE to haft-segment distance in metres, measured off the live bones AFTER the
        /// write, or NaN when unmeasurable. This is the quantity the anchor is defined by.</summary>
        public float PalmToHaftMetres => _palmToHaft;

        /// <summary>Last frame's reach weight (1 = inside the shell). Multiplied into <see cref="PinWeight"/>.</summary>
        public float ReachWeight => _reachWeight;

        /// <summary>True when the WHOLE haft was beyond the arm's shell last frame, so the pin fell back to the haft's
        /// closest point. Surfaced because "the arm is reaching" is a legitimate state a reviewer/Sponsor should be
        /// able to see, not infer.</summary>
        public bool SpanEmpty => _spanEmpty;

        /// <summary>True when the clip's own elbow could not define a bend plane and the measured fallback pole was
        /// used. A build silently running on the fallback all swing is a real finding, so it is readable.</summary>
        public bool PoleFromFallback => _poleFallback;

        /// <summary>Did last frame actually write a pose? False = the driver was inert (gate closed, unwired, or the
        /// solve refused). Never read a false as "the grip is fine".</summary>
        public bool LastSolved => _lastSolved;

        /// <summary>The PALM CENTRE in world space — midpoint of the wrist bone and the knuckle bone. This, not the
        /// wrist, is the point the haft must pass through: the knuckle is 11.1 cm from the wrist on this rig, so the
        /// palm sits 5.6 cm in front of it and pinning the WRIST to the axis would drive the haft through the back of
        /// the hand by that much. NaN-free: returns <c>false</c> rather than a plausible-looking wrong point.</summary>
        public bool TryGetPalmWorld(out Vector3 palm)
        {
            palm = Vector3.zero;
            if (leftHand == null || leftPalmKnuckle == null) return false;
            palm = (leftHand.position + leftPalmKnuckle.position) * 0.5f;
            return true;
        }

        void Awake()
        {
            // Lazy-resolved rather than one-shot-cached: OnEnable/Awake fires synchronously during AddComponent, so a
            // one-shot cache can capture a permanent null purely from test-rig add-order (unity-conventions.md
            // §Editor-vs-runtime, 86cajt6jz). A null character simply leaves the pin inert.
            if (character == null) character = GetComponentInParent<CastawayCharacter>();
            if (heldRig == null) heldRig = GetComponentInChildren<HeldToolRig>(true);
        }

        void LateUpdate() => ApplyPin(Time.deltaTime);

        /// <summary>
        /// Pin the left hand for ONE step. Exactly what <c>LateUpdate</c> runs, with the delta-time as a PARAMETER so a
        /// headless PlayMode test can drive the PRODUCTION path with a real positive step: a `-batchmode` frame has
        /// <c>Time.deltaTime ≈ 0</c> so the engine clock never advances the weight (unity-conventions.md §Headless), and
        /// a test that mirrors the maths beside the driver can go green against a broken production path (the
        /// tautological-assert family).
        /// </summary>
        public void ApplyPin(float dt)
        {
            _lastSolved = false;
            _achievedU = float.NaN;
            _palmToHaft = float.NaN;
            _reachWeight = 0f;
            _spanEmpty = false;
            _poleFallback = false;

            // THE GATE. Animation-state-driven (AttackPickaxe owning layer 0, transition-paired), never a gameplay
            // read — gating an additive offset on a gameplay signal is the trap this codebase already paid for once
            // (86caxj30g / 884c611). Stepped by the SAME production policy function the arm offset and the seat delta
            // use, so all three ease together by construction.
            //
            // ROUND 5 — the gate is MineSwingHoldsPose (owns MINUS the hand-back window) and the ease is ASYMMETRIC.
            // Both are release-side only; the crossfade-IN engagement at 12/s is byte-unchanged. This pin is the
            // channel the Sponsor's release defect was MEASURED on — see releaseBlendRate.
            if (character == null) character = GetComponentInParent<CastawayCharacter>();
            bool mineHoldsPose = debugForceEngaged || (character != null && character.MineSwingHoldsPose);
            _weight = CastawayArmPose.NextMineDeGripWeight(_weight, mineHoldsPose, blendRate, releaseBlendRate, dt);
            if (_weight <= 0.0001f) return;                 // byte-identical to pre-86cay4282 at rest

            if (leftUpperArm == null || leftForeArm == null || heldRig == null) return;
            if (!TryGetPalmWorld(out Vector3 palm)) return;
            if (!heldRig.TryGetHaftSegment(out Vector3 gripW, out Vector3 headW)) return;

            Vector3 shoulder = leftUpperArm.position;
            Vector3 elbow = leftForeArm.position;
            float aLen = (elbow - shoulder).magnitude;
            float bLen = (palm - elbow).magnitude;
            if (aLen < 1e-4f || bLen < 1e-4f) return;

            float u = ResolvePinU(shoulder, gripW, headW, aLen, bLen,
                                  Mathf.Clamp01(pinU), Mathf.Clamp(pinUCeiling, 0.1f, 1f),
                                  Mathf.Clamp(shellFraction, 0.5f, TwoBoneIkSolver.StraightArmFraction),
                                  out _spanEmpty);
            _achievedU = u;
            Vector3 target = Vector3.Lerp(gripW, headW, u);

            Quaternion upper0 = leftUpperArm.rotation, lower0 = leftForeArm.rotation;
            Vector3 poleFallbackWorld = (modelFrame != null ? modelFrame.rotation : transform.rotation)
                                        * poleFallbackLocal;
            var res = TwoBoneIkSolver.Solve(shoulder, upper0, elbow, lower0, palm, target,
                                            poleHint: elbow,
                                            poleFallbackDir: poleFallbackWorld,
                                            reachFalloff: reachFalloff,
                                            straightArmFraction: Mathf.Clamp(shellFraction, 0.5f,
                                                                             TwoBoneIkSolver.StraightArmFraction),
                                            reachHold: reachHoldMetres);
            _reachWeight = res.reachWeight;
            _poleFallback = res.poleFromFallback;
            if (!res.solved) return;                        // refused: leave the clip pose alone, write NOTHING

            float w = Mathf.Clamp01(_weight * res.reachWeight);
            if (w <= 0.0001f) return;

            // Write UPPER then LOWER. Setting the upper carries the lower rigidly, so the lower's explicit write must
            // come second; both are slerped from the captured CLIP pose so a partial weight is a real blend rather
            // than a pop.
            leftUpperArm.rotation = Quaternion.Slerp(upper0, res.upperRotation, w);
            leftForeArm.rotation = Quaternion.Slerp(lower0, res.lowerRotation, w);
            _lastSolved = true;

            // RE-MEASURE off the live bones, never off the solver's own prediction — two instruments sharing one model
            // agree with each other and disagree with the build (this ticket's round-2 lesson).
            if (TryGetPalmWorld(out Vector3 palmAfter))
                _palmToHaft = TwoHandGripRead.DistanceToSegment(palmAfter, gripW, headW, out _);
        }

        /// <summary>
        /// WHERE TO PIN, as a pure function — the strategy the measurements forced, so an EditMode test can pin it with
        /// no rig, no Animator and no clock.
        ///
        /// Intersect the haft segment with the arm's usable shell sphere about the shoulder, restricted to the bare
        /// haft (<paramref name="uCeiling"/>). Non-empty ⇒ the reachable point NEAREST <paramref name="preferredU"/>,
        /// so the Sponsor's dial is honoured wherever the arm allows. Empty ⇒ the haft's CLOSEST point to the shoulder,
        /// which is the best a reaching arm can do and is measurably far better than handing the frame back to the clip
        /// (2.2 cm mean palm gap vs the clip's 20.4 cm).
        /// </summary>
        public static float ResolvePinU(Vector3 shoulder, Vector3 gripW, Vector3 headW,
                                        float aLen, float bLen, float preferredU, float uCeiling,
                                        float shellFraction, out bool spanEmpty)
        {
            spanEmpty = true;
            Vector3 seg = headW - gripW;
            float len2 = seg.sqrMagnitude;
            if (len2 < 1e-10f) return 0f;

            float shell = (aLen + bLen) * shellFraction;
            Vector3 f = gripW - shoulder;
            float A = len2;
            float B = 2f * Vector3.Dot(f, seg);
            float C = f.sqrMagnitude - shell * shell;
            float disc = B * B - 4f * A * C;
            if (disc >= 0f)
            {
                float sq = Mathf.Sqrt(disc);
                float t0 = Mathf.Max((-B - sq) / (2f * A), 0f);
                float t1 = Mathf.Min((-B + sq) / (2f * A), uCeiling);
                if (t1 >= t0) { spanEmpty = false; return Mathf.Clamp(preferredU, t0, t1); }
            }
            // No part of the bare haft is inside the shell — pin its CLOSEST point and let the solver's clamp hold the
            // arm aimed at it.
            float uClosest = Mathf.Clamp(Vector3.Dot(shoulder - gripW, seg) / len2, 0f, uCeiling);
            return uClosest;
        }

        /// <summary>
        /// Slide the pin along the haft by <paramref name="metres"/>: POSITIVE moves the hand UP toward the HEAD
        /// (choking up), NEGATIVE down toward the BUTT. Metres rather than raw u so the F9 dial's step keeps its
        /// physical meaning across weapons with different haft lengths — the Sponsor's 2 cm step is 2 cm of stick.
        ///
        /// Returns false (and changes nothing) when the haft cannot be measured, so a mis-wired mesh gives no silent
        /// partial slide along a guessed axis.
        /// </summary>
        public bool TrySlidePinAlongHaft(float metres)
        {
            if (heldRig == null || !heldRig.TryGetHaftSegment(out Vector3 g, out Vector3 h)) return false;
            float len = (h - g).magnitude;
            if (len < 1e-5f) return false;
            pinU = Mathf.Clamp(pinU + metres / len, 0f, Mathf.Clamp(pinUCeiling, 0.1f, 1f));
            return true;
        }

        /// <summary>Nudge the reach-shell fraction — the "how straight may the arm go" knob whose trade is priced in
        /// the <see cref="shellFraction"/> tooltip. Clamped to the solver's own ceiling so no dial can ever request a
        /// fully-straight arm.</summary>
        public void NudgeShellFraction(float delta)
        {
            shellFraction = Mathf.Clamp(shellFraction + delta, 0.5f, TwoBoneIkSolver.StraightArmFraction);
        }
    }
}
