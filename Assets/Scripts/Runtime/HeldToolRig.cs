using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Generalized per-frame driver that seats ANY held tool/weapon (axe, knife, sword, spear, …) at
    /// the castaway's hand bone via ONE shared seat system (ticket 86cabh907, Route A weapon set). This
    /// is the generalization of the soak-locked HELD-AXE rig: the seating math is identical, so any
    /// family item mounts to the hand via the SAME locked seat the axe uses — no per-weapon hold logic.
    ///
    /// <see cref="HeldAxeRig"/> is a thin back-compat subclass (the axe's serialized scene wiring + the
    /// soak-tuning tests reference HeldAxeRig by name); it adds NOTHING — the seat behaviour lives here.
    ///
    /// The seat (carried verbatim from the soak-locked HeldAxeRig — Sponsor soak #5, build 2d90a68):
    ///   - POSITION → a HAND-LOCAL offset, rotated by the RAW hand's rotation every frame:
    ///         transform.position = hand.position + hand.rotation * seatOffsetFromHand   (cm-scale, hand-LOCAL)
    ///     so it TRACKS the hand through every facing AND follows the arm's natural swing (it rides the
    ///     RAW hand bone). It is NOT hand.TransformPoint(offset) — that would re-apply the bone's lossyScale
    ///     and blow the offset up to metres (the §FBX lossy-bone trap); we rotate by hand.rotation ONLY.
    ///   - ROTATION → HAND-RELATIVE, re-applied every frame:
    ///         transform.rotation = hand.rotation * Quaternion.Euler(seatEuler)
    ///     so the haft TURNS WITH the hand through every facing.
    ///
    /// SCALE rides the hierarchy (the tool stays a CHILD of the hand bone); only POSITION + ROTATION are
    /// world-driven here. FOLLOW-THE-ARM: the tool rides the RAW hand, so it swings WITH the arm during
    /// walk/run/jump (the Sponsor's design choice, 86ca9zcjn). The raw hand returns to its pose every walk
    /// cycle → the follow is BOUNDED by construction (no ratchet). An OPTIONAL light low-pass (<see
    /// cref="followDamp"/>, default 0) de-jitters WITHOUT re-locking the swing.
    ///
    /// DefaultExecutionOrder(100): runs AFTER CastawayCharacter (default 0) so the body-yaw + the animated
    /// hand pose for THIS frame are final before we read the hand transform — no one-frame facing lag.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HeldToolRig : MonoBehaviour
    {
        [Tooltip("The hand bone the tool is seated at. Wired editor-time (serialized); Awake searches the " +
                 "parent chain as a fallback.")]
        public Transform hand;

        [Tooltip("POSITION channel — the tool is seated at hand.position + hand.rotation * this offset every " +
                 "frame. HAND-LOCAL (the hand bone's own frame), cm-scale, rotated by the RAW hand each frame " +
                 "so it tracks the hand through every facing. (Field name kept as worldOffsetFromHand on the " +
                 "axe subclass for serialization/F9-tool continuity; the value is HAND-LOCAL end to end.)")]
        public Vector3 seatOffsetFromHand = new Vector3(0.1312f, 0.1409f, 0.0593f);

        [Tooltip("ROTATION channel — the tool's rotation is hand.rotation * Euler(this), HAND-RELATIVE, so " +
                 "the haft turns WITH the hand through every facing.")]
        public Vector3 seatEuler = new Vector3(12.0f, -8.0f, -82.0f);

        [Header("Follow the arm's natural swing (86ca9zcjn — Sponsor design choice)")]
        [Tooltip("OPTIONAL light low-pass on the followed hand POSE (per-second smoothing rate), to DE-JITTER " +
                 "without re-locking the swing. 0 = follow the RAW hand (the full per-step arm-swing is visible " +
                 "— the Sponsor's choice, the default). A SMALL positive value eases toward the LIVE hand per " +
                 "frame (cannot ratchet). Do NOT crank it up to re-lock — 'if it reads wild, damp it, don't lock it'.")]
        public float followDamp = 0f;

        // ==============================================================================================
        // MINE-STATE SEAT DELTA (86cay4282 round 2 — the Sponsor: "we need to position the axe for a two hand grip")
        //
        // THE REVERSAL. Round 1 read the pickaxe MINE clip's locked-together hands as a defect and opened the LEFT
        // ARM off the implied haft. The Sponsor reversed that premise: the clip is authored two-handed and that is
        // what he WANTS, so the animation was right all along and the TOOL is in the wrong place. This is the fix
        // from the other side — leave the clip alone and move the HAFT onto the hands.
        //
        // MEASURED, not guessed (AttackClipPoseDiag MINE-SEAT FIT, live rig, the shipped repaired clip, 61 samples).
        // The seat is RIGID in the hand's own frame (grip/head drift 1e-6 m across the whole swing), so ONE constant
        // delta applies to the entire swing by construction and the required delta closes analytically rather than
        // needing a search: rotate the haft onto the line through both hands, then slide it so the line passes
        // through the (real) right-hand grip. Result, live re-measure:
        //     left hand to haft   mean 1.277 -> 0.445 SW   MAX 1.476 -> 0.615 SW
        //     right hand to haft  mean 0.166 -> 0.000 SW   MAX 0.179 -> 0.000 SW
        //     tool vs hand line   MAX  90.0  -> 32.7 deg
        //     haft-to-torso clearance MIN 0.468 -> 0.557 SW   (it moves AWAY from the body — no traded defect)
        //
        // The fit MUST include CastawayHandPose's order-65 WRIST offset. It is composed onto the hand bones
        // between the arm pose (50) and this seat (100), and this seat reads hand.ROTATION — so a measurement that
        // skips it fits the tool to a right hand a quarter-turn away from the live one. An earlier pass did skip it
        // and the shipped-build gate measured 1.220 SW against a predicted 0.611.
        //
        // WHY IT IS STATE-GATED, not a global re-seat: this delta is only correct for the ONE clip whose hands are
        // locked on a shared haft. Every other state — carry, idle, walk, run, jump, and the other four swings —
        // is one-handed and its seat is Sponsor-approved, so the delta must be identity there. At weight 0
        // Euler(zero) is the identity quaternion and the offset adds Vector3.zero, so those states are BYTE-
        // IDENTICAL to pre-86cay4282: same idiom (and same guarantee) as CastawayArmPose's run-lower / de-grip.
        // ==============================================================================================

        [Header("MINE-state seat delta (86cay4282 — two-hand grip on the pickaxe mine clip)")]
        [Tooltip("The CastawayCharacter whose AttackPickaxe layer-0 state gates the MINE seat delta. Wired " +
                 "editor-time (serialized); Awake resolves it from the parent chain as a fallback. If unresolved " +
                 "the delta is INERT (weight 0) — FAIL-CLOSED toward the Sponsor-approved one-handed seat, so a " +
                 "missing wire can never move the tool in the wrong state.")]
        public CastawayCharacter character;

        [Tooltip("HAND-LOCAL position delta added to seatOffsetFromHand at full MINE weight — slides the haft so " +
                 "its LINE passes through both hands. Zero elsewhere. Baked editor-time from " +
                 "MovementCameraScene.HeldToolMineSeatOffsetDelta (the authoritative ship source); the F9 " +
                 "AxeNudgeTool MINE-SEAT target dials it live and the Sponsor bakes the value back there.")]
        public Vector3 mineSeatOffsetDelta = new Vector3(-0.2491f, -0.3928f, -0.3109f);

        [Tooltip("TOOL-LOCAL rotation delta right-multiplied onto seatEuler at full MINE weight — turns the haft " +
                 "onto the line through both hands (the measured fit: 90.0 deg off -> 32.7 deg). Composed in the " +
                 "tool's OWN frame (unity6-mastery.md §5: right-multiply, never per-component euler accumulation), " +
                 "the same frame the F9 dial nudges in, so dialled == baked == applied.")]
        public Vector3 mineSeatEulerDelta = new Vector3(-24.7f, 70.0f, 23.7f);

        [Tooltip("Per-second ENGAGE rate for the MINE seat weight. Matches CastawayArmPose.mineDeGripBlendRate (12/s " +
                 "~= 0.25 s to 95%) DELIBERATELY: the three mine offsets share one gate and one ease so the haft never " +
                 "moves out of step with the arm — and 0.25 s lands inside the clip's wind-up rather than snapping " +
                 "the tool across the hands under the strike.")]
        public float mineSeatBlendRate = 12f;

        [Tooltip("86cay4282 ROUND 5 — per-second RELEASE rate for the MINE seat weight; matches " +
                 "CastawayArmPose.mineDeGripReleaseRate and CastawayLeftArmHaftIk.releaseBlendRate for the SAME " +
                 "shared-ease reason the engage rate matches. This channel is NOT what the Sponsor's round-4 release " +
                 "defect was measured on (that was the left-arm pin), but the three weights are deliberately one ease: " +
                 "releasing the pin fast while the seat still crawled back at 12/s would let the HAND leave before the " +
                 "HAFT — exactly the out-of-step failure the shared rate exists to prevent. The dialed seat VALUES " +
                 "(mineSeatOffsetDelta / mineSeatEulerDelta) are untouched; only when they hand back changes. The value " +
                 "is DERIVED in MovementCameraScene.MineWeightReleaseRate from the controller's own crossfade out.")]
        public float mineSeatReleaseRate = 42f;

        // ==============================================================================================
        // SWING-AIM SEAT DELTA (86cb6v03j — the Sponsor: "the weapons/tools does not point in the right
        // direction while swinging")
        //
        // THE REAL-WORLD ANCHOR: a swung axe / sword / spear is a LEVER — the head or tip is the FAR end of the
        // stick from the hand, so it sweeps the OUTSIDE of the arc and arrives at the target AHEAD of the hand.
        // At the moment of the strike it must be pointing INTO the direction the character is attacking.
        //
        // WHAT WAS MEASURED (shipped exe, -verifySwings [swing-point] lines; the run is named in the PR body). At
        // each class's OWN strike frame — the frame of peak head speed, found by measurement per run because clip
        // lengths and the per-class SwingSpeed* multipliers are live soak-tuned dials — dot(haftDirection,
        // model.forward) came back:
        //     axe -0.448 | pickaxe -0.432 | spear -0.616 | sword -0.083 | dagger +0.181
        // i.e. on four of the five classes the head points into the BACKWARD half-space at the instant of the
        // strike. The pictures agree: swing_point_<class>_side.png (the character's forward is frame-RIGHT in
        // those shots by construction) shows the axe head trailing behind him and the sword hanging straight down.
        //
        // WHY THE SEAT OWNS IT, ESTABLISHED BY MEASUREMENT RATHER THAN BY REASONING FROM THE SOURCE. Two readings:
        //   (1) The haft's direction expressed in the RIGHT HAND BONE's own frame moved 0.00 deg across the entire
        //       axe / spear / sword swing (dagger 1.16, pickaxe 25.26 — and the pickaxe is exactly the one class
        //       that already HAS a state-gated seat delta easing in, so its spread confirms rather than contradicts
        //       the reading). The clip therefore supplies hand.rotation and NOTHING else; orientation is entirely
        //       the seat dials' output.
        //   (2) The best of the six signed hand-bone axes scored 0.725..0.861 against the forward-and-down strike
        //       direction on every class — so the strike direction IS reachable from the hand pose the clip
        //       produces, which is what says a SEAT dial can fix this and the clip does not need touching.
        // The two together separate the candidate owners cleanly. Neither is an argument from the code.
        //
        // WHY THE FIX IS STATE-GATED AND ADDITIVE rather than a re-dial of seatEuler / WeaponMeshLocalEuler. Those
        // dials are the Sponsor's own, dialled BY EYE at REST across the soak-6/7 in-hand rounds, and the carry
        // read is a standing approved bar. Re-aiming them for the swing would move the carry pose with it. An
        // additive delta at an eased weight, engaged only while that class's attack state HOLDS layer 0, is
        // identity at rest — Euler(zero) is the identity quaternion — so every non-attack state is BYTE-IDENTICAL.
        // Same idiom, same guarantee, and the same shared ease policy as the mine delta above.
        //
        // ⚠ PICKAXE IS EXCLUDED BY NAME, and the exclusion is a scope decision, not an oversight. The pickaxe's
        // swing seat is ALREADY dialled — it is the mineSeatEulerDelta above, the product of five soak rounds of
        // 86cay4282 ending in a Sponsor pass, and the shipped -verifySwings gate currently measures the left PALM
        // on the haft at 0.239 SW against a 0.293 SW mesh-derived touch bound. Rotating the haft would move it off
        // that palm. Ticket 86cb6v03j forbids reworking Sponsor-passed bars in this pass, so the pickaxe entry here
        // is ZERO and the aim gate below does not judge it. That is a stated bound on this fix, not a claim the
        // pickaxe reads correctly.
        // ==============================================================================================

        /// <summary>
        /// THE BAKED PER-CLASS SWING-AIM DELTAS, indexed by <c>CastawayCharacter.WeaponClass*</c>.
        ///
        /// STATIC, NOT A SERIALIZED FIELD, deliberately. Every other seat dial here is serialized because the
        /// SPONSOR dials it by eye through the F9 tool and bakes the number back. These are not dialled — they are
        /// DERIVED per run by the shipped gate — so serializing them would add a field the committed Boot.unity
        /// does not carry, i.e. a value that depends on whether the scene has been re-baked since
        /// ([[unity-procedural-committed-assets-go-stale]]). A static table cannot go stale against a scene.
        ///
        /// HOW EACH NUMBER WAS OBTAINED. The shipped exe's <c>-verifySwings</c> [swing-aim-fit] pass solves, at the
        /// class's OWN measured strike frame (peak head speed, re-found by measurement every run), the MINIMAL
        /// tool-frame rotation carrying the live haft direction onto that verb's aim direction:
        /// <c>D = FromToRotation(hTool, Inverse(toolRot) * aim)</c>, computed in the same tool frame
        /// <see cref="ComposeSeat"/> composes it in — so fitted == baked == applied. MINIMAL matters: it leaves ROLL
        /// about the weapon's own long axis untouched, and roll is what the Sponsor dialled by eye at rest and what
        /// this ticket has no business moving (roll does not change where a weapon POINTS).
        ///
        /// AIM DIRECTION PER VERB, taken from what the verb IS rather than from a tuning preference:
        ///   ARC verbs (axe chop, sword slash) -> forward-and-down, the bisector of "straight ahead" and "straight
        ///     down": at the bottom of an arc the head travels forward and down into the target.
        ///   THRUST verbs (spear thrust, dagger stab) -> straight forward, level: a thrust drives the tip along the
        ///     facing. The clips are named CastawayAxeSwing / CastawaySwordSlash / CastawaySpearThrust /
        ///     CastawayDaggerStab, so the arc-vs-thrust split is the ASSET's own, not an invented taxonomy.
        ///
        /// Re-derive by re-running the gate and reading the [swing-aim-fit] lines; do NOT hand-edit. A correct bake
        /// makes the next run's residual collapse toward 0 and its required-additional delta toward identity, which
        /// is why that line is unconditional — it is a live convergence check, not a one-off fitting aid.
        /// </summary>
        /// ⚠ THIS LOOKUP IS A SWITCH AND NOT A STATIC ARRAY, and that is a bug fix rather than a style choice.
        /// The first cut declared <c>static readonly Vector3[] SwingAimEulerByClass = { SwingAimAxe, ... }</c>
        /// ABOVE the scalars it names. C# runs static field initializers in DECLARATION order, so the array was
        /// built while every scalar was still its default <c>Vector3.zero</c> — the table shipped all-zeros, the
        /// delta applied at weight 1.00 for the correct class every frame, and the shipped gate re-measured
        /// axe fwdDot -0.448 / sword -0.083 BYTE-IDENTICAL to the unfixed run. It presented exactly as the
        /// "wired but conditionally inert" family (procedural-animation-verbs.md §Debug-instrument caveat): the
        /// write succeeded at every layer and the effect was silently zero. A switch has no initialisation order
        /// to get wrong.
        public static Vector3 SwingAimEulerForClass(int weaponClass)
        {
            if (SwingAimForcedZero) return Vector3.zero;
            // 86cb6v03j round 2 — the Sponsor's LIVE per-class dial composes here, and ONLY here, so the rig, the
            // shipped gate and the EditMode suite all read one number. At the dial default (every axis 0)
            // SwingAimNudge.Compose SHORT-CIRCUITS and hands the baked euler back UNTOUCHED — not round-tripped
            // through a quaternion, which would re-decompose the dagger's 163.6 deg yaw into a different (but
            // equal) triple and ship a literal 70583d8 never had. "0 == ships today" is therefore a structural
            // property, not a tolerance claim. The dial sits BELOW the SwingAimForcedZero negative control on
            // purpose: -swingAimFaultZero must keep reproducing the pre-86cb6v03j seat exactly, dialled or not.
            return SwingAimNudge.Compose(SwingAimBakedEulerForClass(weaponClass), SwingAimNudge.Get(weaponClass));
        }

        /// <summary>The BAKED per-class swing-aim euler with NO live dial applied — the committed constants alone.
        /// Split out from <see cref="SwingAimEulerForClass"/> so the readout can print "baked vs effective", and so
        /// a test can assert the dial is neutral at default WITHOUT re-listing the constants beside the rig (the
        /// tautological-assert trap, unity-conventions.md §Editor-vs-runtime). <see cref="SwingAimForcedZero"/> is
        /// honoured here too, so the negative control zeroes the baked term at its source.</summary>
        public static Vector3 SwingAimBakedEulerForClass(int weaponClass)
        {
            if (SwingAimForcedZero) return Vector3.zero;
            switch (weaponClass)
            {
                case CastawayCharacter.WeaponClassAxe:     return SwingAimAxe;
                case CastawayCharacter.WeaponClassPickaxe: return SwingAimPickaxe;
                case CastawayCharacter.WeaponClassDagger:  return SwingAimDagger;
                case CastawayCharacter.WeaponClassSpear:   return SwingAimSpear;
                case CastawayCharacter.WeaponClassSword:   return SwingAimSword;
                default:                                   return Vector3.zero;   // incl. -1 = nothing holds the pose
            }
        }

        /// <summary>
        /// VERIFY-ONLY NEGATIVE CONTROL — forces every swing-aim delta to zero, i.e. reproduces the pre-86cb6v03j
        /// seat exactly, so "the aim gate REDS on the unfixed build" is DEMONSTRABLE from the shipped exe with no
        /// rebuild and no edit to a committed value. Set once from the <c>-swingAimFaultZero</c> command line by
        /// <see cref="SwingVerifyCapture"/>; absent that flag it is false and every byte of behaviour is unchanged.
        ///
        /// It exists because "this gate would have caught the defect" is a claim, and a fail-closed default that has
        /// never been SEEN to red is exactly the claim this project keeps having to retract. It is the direct
        /// sibling of <c>-swingSeatFaultCm</c>, and it is a better control than that one in a specific way: it
        /// injects nothing and invents no magnitude — it simply removes the fix, so the RED it produces is the
        /// genuine pre-fix build rather than an approximation of it.
        /// </summary>
        public static bool SwingAimForcedZero;

        /// <summary>Per-play-entry reset for <see cref="SwingAimForcedZero"/>. Required by the 86ca9a39q discipline
        /// (Configurable Enter Play Mode with domain reload DISABLED leaves statics alive across play-entries) and
        /// enforced mechanically by StaticStateResetTests, which is what caught this one. It matters concretely:
        /// without it, entering play once under the negative-control flag would leave the fix DISABLED for every
        /// later play-entry in that editor session — a debug flag silently becoming the default.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSwingAimStatics() => SwingAimForcedZero = false;

        /// <summary>DERIVED from the closed-form window-mean fit (pre-fix residual 48.9 deg; the fit predicted a
        /// mean fwdDot of 0.187 and the re-measure returned 0.219). ARC verb.</summary>
        public static readonly Vector3 SwingAimAxe = new Vector3(-15.5f, 44.7f, 8.3f);

        /// <summary>ZERO BY DECISION, not by measurement — the fit run reports the pickaxe 126.2 deg off aim and
        /// asks for (37.4, -118.8, -130.4). It is NOT taken, because the pickaxe's swing seat is ALREADY dialled:
        /// it is <see cref="mineSeatEulerDelta"/>, the product of five soak rounds of 86cay4282 ending in a Sponsor
        /// pass, and the shipped gate currently measures the left PALM on the haft at 0.239 SW against a 0.293 SW
        /// mesh-derived touch bound. Applying an aim rotation would move the haft off that palm, i.e. rework a
        /// Sponsor-passed bar — which ticket 86cb6v03j forbids in this pass. The pickaxe is correspondingly EXCLUDED
        /// from the aim gate. This is a stated bound on the fix, NOT a claim that the pickaxe aims correctly.</summary>
        public static readonly Vector3 SwingAimPickaxe = Vector3.zero;

        /// <summary>DERIVED from the closed-form window-mean fit (pre-fix residual 161.4 deg; predicted mean
        /// fwdDot 0.389, re-measured 0.395). THRUST verb.</summary>
        public static readonly Vector3 SwingAimDagger = new Vector3(9.7f, 163.6f, 84.8f);

        /// <summary>DERIVED from the closed-form window-mean fit (pre-fix residual 112.2 deg; predicted mean
        /// fwdDot 0.863, re-measured 0.843). THRUST verb.</summary>
        public static readonly Vector3 SwingAimSpear = new Vector3(4.3f, 108.5f, 40.7f);

        /// <summary>DERIVED from the closed-form window-mean fit (pre-fix residual 53.5 deg; predicted mean
        /// fwdDot 0.254, re-measured 0.113 — the loosest of the four, and the reason the sword's residual is the
        /// largest at 6.6 deg after the bake). ARC verb.</summary>
        public static readonly Vector3 SwingAimSword = new Vector3(36.0f, 42.5f, 11.1f);

        [Tooltip("Per-second ENGAGE / RELEASE rates for the SWING-AIM weight. Deliberately the SAME values the MINE " +
                 "delta uses: the ease lands inside the wind-up rather than snapping the tool round under the " +
                 "strike, and one shared ease policy means a future retune cannot put two seat channels out of step.")]
        public float swingAimBlendRate = 12f;
        public float swingAimReleaseRate = 42f;

        private Vector3 _dampedPos;
        private Quaternion _dampedRot;
        private bool _dampInit;

        // The SMOOTHED swing-aim weight + the class it is easing toward (-1 = none).
        private float _swingAimWeight;
        private int _swingAimClass = -1;

        /// <summary>The current smoothed SWING-AIM weight (0 -&gt; 1 across an attack swing). Exposed for the same
        /// reason <see cref="MineSeatWeight"/> is: this offset is ENGAGEMENT-WEIGHTED, and an engagement-weighted
        /// value with no weight readout is indistinguishable from a broken one (procedural-animation-verbs.md
        /// §Debug-instrument caveat).</summary>
        public float SwingAimWeight => _swingAimWeight;

        /// <summary>The WeaponClass the swing-aim delta is currently applying for, or -1. Part of the shipped-gate
        /// read: "the delta engaged" and "it engaged for the RIGHT class" are different claims.</summary>
        public int SwingAimClass => _swingAimClass;

        /// <summary>The swing-aim euler in effect for a class, safe-zero for anything unmapped (including the -1
        /// "no attack state holds the pose" value). ONE seam for the rig, the shipped gate and the EditMode suite —
        /// a test that mirrors the lookup beside the rig can go green against a broken rig (the tautological-assert
        /// family, unity-conventions.md §Editor-vs-runtime).</summary>
        public static Vector3 SwingAimEulerFor(int weaponClass) => SwingAimEulerForClass(weaponClass);

        /// <summary>Below this weight the swing-aim delta displaces the weapon by less than the largest baked delta
        /// x this fraction (176.2 deg x 0.01 = 1.8 deg) — under any visible rotation — so it is safe to re-latch a
        /// DIFFERENT class's delta at that point without the eye seeing one aim cross-fade into another.
        ///
        /// It replaces a 1e-4 epsilon that was measurably too strict: the shipped run logged the spear swing
        /// engaging while the dagger's weight had only decayed to ~0.004, so the spear never re-latched and ran the
        /// DAGGER's delta ("weight 0.00 class 2" on the [swing-aim-fit] spear line). A release-decay threshold has
        /// to be set from what the EYE can see, not from what a float can reach.</summary>
        public const float SwingAimRelatchWeight = 0.01f;

        // The SMOOTHED mine-seat weight (0 everywhere except while the AttackPickaxe swing owns layer 0).
        private float _mineWeight;

        /// <summary>The current smoothed MINE seat weight (0 -> 1 across the pickaxe swing). Exposed because this
        /// offset is ENGAGEMENT-WEIGHTED: a debug dial targeting it MUST surface the weight or a not-engaged dial is
        /// indistinguishable from a broken handler (procedural-animation-verbs.md §Debug-instrument caveat — the
        /// exact trap that burned the Sponsor twice on run-lower). Also the shipped-gate + regression read.</summary>
        public float MineSeatWeight => _mineWeight;

        // Cached haft-endpoint resolution for TryGetHaftSegment. Re-resolved whenever the displayed mesh changes
        // (HeldWeaponCycleDebug swaps it on [B]), so a cycled weapon can never be measured against a stale haft.
        private Transform _haftHolder;
        private Mesh _haftMesh;
        private Vector3 _haftGripLocal, _haftHeadLocal;

        /// <summary>The current followed hand WORLD position the tool pivot rides (the raw hand, or the lightly
        /// damped hand when followDamp &gt; 0) — exposed so the PlayMode regression can assert the tool follows
        /// the hand's natural swing within tolerance with no cumulative drift.</summary>
        public Vector3 FollowPos { get; private set; }

        protected virtual void Awake()
        {
            // Fallback: if the hand wasn't wired editor-time, the bone is this object's parent (the tool is
            // serialized as a child of the hand bone). Defensive — the authored path always wires it.
            if (hand == null) hand = transform.parent;
            // 86cay4282 — fallback wiring for the MINE-seat gate. Re-resolved LAZILY in LateUpdate too rather than
            // one-shot-cached here: OnEnable/Awake fires synchronously during AddComponent, so a one-shot cache can
            // capture a permanent null purely from test-rig add-order (unity-conventions.md §Editor-vs-runtime,
            // 86cajt6jz). A null character simply leaves the delta inert (fail-closed).
            if (character == null) character = GetComponentInParent<CastawayCharacter>();
        }

        protected virtual void LateUpdate() => ApplySeat(Time.deltaTime);

        /// <summary>
        /// Seat the tool for ONE step. This is exactly what <c>LateUpdate</c> runs — the delta-time is a PARAMETER so
        /// a headless PlayMode test can drive the PRODUCTION seat with a real positive step instead of re-implementing
        /// the maths beside it. That matters here for two documented reasons: a `-batchmode` frame has
        /// <c>Time.deltaTime ≈ 0</c> so the engine clock never advances the weight (unity-conventions.md §Headless),
        /// and a test that mirrors the seat formula rather than calling it can go green against a broken production
        /// path (the tautological-assert family, unity-conventions.md §Editor-vs-runtime).
        /// </summary>
        public virtual void ApplySeat(float dt)
        {
            if (hand == null) return;

            // FOLLOW THE RAW HAND: the tool rides the hand bone's live world pose, so it swings WITH the arm
            // during walk/run/jump. The raw hand returns to its pose every walk cycle → bounded by construction,
            // no ratchet; the facing yaw is already carried by the raw hand, so turning passes through immediately.
            Vector3 followPos = hand.position;
            Quaternion followRot = hand.rotation;

            if (followDamp > 0f)
            {
                if (!_dampInit) { _dampedPos = followPos; _dampedRot = followRot; _dampInit = true; }
                else
                {
                    float a = 1f - Mathf.Exp(-followDamp * dt);
                    _dampedPos = Vector3.Lerp(_dampedPos, followPos, a);
                    _dampedRot = Quaternion.Slerp(_dampedRot, followRot, a);
                }
                followPos = _dampedPos; followRot = _dampedRot;
            }

            // 86cay4282 — MINE-STATE SEAT WEIGHT. Gated on the AttackPickaxe state owning layer 0 (transition-paired
            // — CastawayCharacter.MineSwingOwnsPose), NOT on a gameplay signal: gating an additive offset on a
            // gameplay read instead of the animation state is the documented trap this codebase already paid for
            // once (86caxj30g / 884c611). The weight is stepped by CastawayArmPose.NextMineDeGripWeight — the SAME
            // production policy function the arm-side offset uses, not a mirrored copy, so the haft and the arm can
            // never ease out of step (and no second implementation can drift). Lazy re-resolve keeps a null-at-Awake
            // reference recoverable without ever caching the miss.
            //
            // ROUND 5 — MineSwingHoldsPose (owns MINUS the hand-back window) + the ASYMMETRIC ease, so the seat starts
            // returning on the FIRST frame of the crossfade out at the fast release rate. Engage half unchanged.
            if (character == null) character = GetComponentInParent<CastawayCharacter>();
            bool mineHoldsPose = character != null && character.MineSwingHoldsPose;
            _mineWeight = CastawayArmPose.NextMineDeGripWeight(_mineWeight, mineHoldsPose, mineSeatBlendRate,
                                                              mineSeatReleaseRate, dt);

            // POSITION in HAND-LOCAL space: rotate the cm-scale offset by the hand rotation so it TRACKS the
            // hand through every facing. We rotate by followRot (the SAME hand rotation the ROTATION channel
            // uses) — NOT by the bone's lossyScale (that would re-apply the §FBX lossy scale and blow the
            // offset up to metres). ROTATION hand-relative off the hand rotation.
            //
            // The MINE delta rides BOTH channels, scaled by the weight. At weight 0 the position term adds
            // Vector3.zero and Quaternion.Euler(Vector3.zero) is the identity, so every non-mining state is
            // BYTE-IDENTICAL to pre-86cay4282 — the regression guarantee for the Sponsor's approved one-handed
            // seat. Euler-scaling (rather than a Slerp) matches the established run-lower / de-grip idiom and is
            // exact at both ends of the ease.
            // 86cb6v03j — SWING-AIM WEIGHT. Gated on WHICH attack state holds layer 0 (the general form of the mine
            // gate; CastawayCharacter.AttackClassHoldingPose), never on a gameplay read — the same reason the mine
            // delta is animation-gated. The eased weight uses the SAME production policy function both other
            // engagement-weighted offsets use, so no second implementation can drift out of step.
            //
            // The CLASS is latched while engaged and only re-read once the weight has fully released, so a
            // back-to-back swing of a DIFFERENT class cannot cross-fade one class's delta into another's mid-arc
            // (which would rotate the weapon through an orientation neither dial ever specified).
            int holding = character != null ? character.AttackClassHoldingPose : -1;
            if (holding >= 0 && (_swingAimClass < 0 || _swingAimWeight < SwingAimRelatchWeight))
                _swingAimClass = holding;
            // While a DIFFERENT class holds the pose than the one latched, aimHolds is false, so the weight releases
            // at the fast rate until it drops under the re-latch threshold and the new class takes over. That is the
            // whole cross-fade guard: two aim deltas are never blended, the old one is let go first.
            bool aimHolds = holding >= 0 && holding == _swingAimClass;
            _swingAimWeight = CastawayArmPose.NextMineDeGripWeight(_swingAimWeight, aimHolds, swingAimBlendRate,
                                                                  swingAimReleaseRate, dt);
            if (holding < 0 && _swingAimWeight < SwingAimRelatchWeight) _swingAimClass = -1;

            ComposeSeat(followPos, followRot, seatOffsetFromHand, seatEuler,
                        mineSeatOffsetDelta, mineSeatEulerDelta, _mineWeight,
                        SwingAimEulerFor(_swingAimClass), _swingAimWeight,
                        out Vector3 pos, out Quaternion rot);
            transform.SetPositionAndRotation(pos, rot);
            FollowPos = followPos;
        }

        /// <summary>
        /// THE SEAT COMPOSITION as a pure function — exactly what <see cref="ApplySeat"/> writes to the transform.
        /// Extracted static so a test can drive the SHIPPED maths at a CHOSEN mine weight without needing a live
        /// Animator + gate to produce that weight (and so the A/B of weight 0 vs 1 is measured on one identical
        /// animation frame). LateUpdate calls THIS, so the tests cannot go green against a mirrored re-implementation
        /// — the tautological-assert trap, unity-conventions.md §Editor-vs-runtime.
        ///
        /// At <paramref name="mineWeight"/> 0 the position delta contributes Vector3.zero and
        /// <c>Quaternion.Euler(Vector3.zero)</c> is the identity, so the result is BIT-FOR-BIT the pre-86cay4282
        /// one-handed seat — the regression guarantee for all 15 baked held-weapon poses.
        /// </summary>
        public static void ComposeSeat(Vector3 followPos, Quaternion followRot,
                                       Vector3 seatOffsetFromHand, Vector3 seatEuler,
                                       Vector3 mineOffsetDelta, Vector3 mineEulerDelta, float mineWeight,
                                       Vector3 swingAimEulerDelta, float swingAimWeight,
                                       out Vector3 position, out Quaternion rotation)
        {
            Vector3 offset = seatOffsetFromHand + mineOffsetDelta * mineWeight;
            // The MINE rotation delta is composed in the TOOL's OWN frame (right-multiplied onto the seat euler),
            // the same frame the F9 dial nudges in via ComposeLocalRot — so dialled == baked == applied.
            //
            // 86cb6v03j — the SWING-AIM delta is right-multiplied AFTER it, in the SAME tool frame and for the same
            // reason. Order matters and this one is chosen, not incidental: the two are never both non-zero on the
            // same class (the pickaxe owns the mine delta and its swing-aim entry is zero; the other four own a
            // swing-aim delta and never enter AttackPickaxe), so this composition reduces to exactly one of them
            // being applied — the multiplication order is what makes that reduction EXACT rather than approximate.
            // At either weight 0 the term is the identity quaternion, so every non-attack state is BIT-FOR-BIT the
            // pre-86cb6v03j seat: the regression guarantee for the Sponsor's approved carry pose on all 15 baked
            // held-weapon seats.
            //
            // ⚠ THE SWING-AIM TERM EASES BY SLERP, NOT BY EULER-SCALING — a DELIBERATE divergence from the mine
            // delta beside it, and the reason is the MAGNITUDE, not taste. Scaling an euler TRIPLE is not scaling a
            // ROTATION: the two agree closely for small angles and diverge badly for large ones, because the
            // intermediate triple names a different axis than the endpoint does. The mine delta's largest component
            // is 70 deg and euler-scaling is visually fine there. The swing-aim deltas reach -176.2 deg (spear),
            // where euler-scaling would sweep the weapon through orientations no dial ever specified during the
            // ~0.25 s ease — visible as a tumble at the start of every thrust. Slerp walks the geodesic, is exact at
            // BOTH ends (w=0 -> identity, w=1 -> the baked delta, so the regression guarantee above is untouched),
            // and is the shortest path in between. Do NOT "harmonise" this back to euler-scaling for consistency.
            Quaternion aimD = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(swingAimEulerDelta),
                                               Mathf.Clamp01(swingAimWeight));
            Quaternion preAim = Quaternion.Euler(seatEuler) * Quaternion.Euler(mineEulerDelta * mineWeight);
            Quaternion seat = preAim * aimD;
            rotation = followRot * seat;

            // ===== THE AIM ROTATION PIVOTS ABOUT THE HAND, NOT ABOUT THE TOOL ORIGIN. =====
            //
            // WHY, MEASURED — this cost a build round and the existing gate is what caught it. The tool ORIGIN is
            // the haft's BUTT (the family export contract puts the grip origin at (0,0,0),
            // blender-asset-pipeline.md §6), and the hand is NOT there: the shipped gate measures the wrist at
            // u 0.2004 along the haft. Rotating about the origin therefore swung the stick THROUGH the hand, and
            // the chop-seat along-haft leg reddened in the same run the aim gate first went green — hand at
            // u 0.2350..0.2560 against its 0.1492..0.2516 window, a ~4.4 cm slide up an 87 cm haft. Note WHICH leg
            // caught it: the PERPENDICULAR distance barely moved (0.4027 -> 0.4096 SW), exactly as
            // procedural-animation-verbs.md says it must ("a distance-to-LINE metric leaves the along-line position
            // unscored"). The gate that exists because of that lesson earned its keep here.
            //
            // ⚠ AND THE FIRST PIVOT CHOICE WAS ALSO WRONG, which is worth keeping because the reasoning is seductive:
            // pivoting about the haft point AT u 0.2004 does NOT preserve u. Rotating a LINE about a point on itself
            // still moves the foot of the perpendicular from an off-line point — and the wrist sits 18 cm off the
            // line. Measured, that attempt pushed u FURTHER out, to 0.2697..0.2929. The quantity to hold fixed is not
            // a point on the tool's axis; it is THE HAND ITSELF.
            //
            // Pivoting about the hand makes both seat legs invariant BY CONSTRUCTION rather than by re-measurement:
            // the hand's position expressed in the TOOL's own frame is unchanged by a rotation about the hand, and u
            // and the perpendicular distance are both functions of exactly that. So the along-haft and
            // perpendicular readings are preserved EXACTLY, not approximately — no bound needs re-anchoring, which
            // is the outcome that matters, since re-anchoring MeasuredApprovedChopUMin to whatever the build landed
            // on is the calibrate-against-achievement failure this codebase has already paid for once.
            //
            // It is also the physically honest motion: a hand re-orienting a tool it is gripping turns it about the
            // grip.
            //
            // CLOSED FORM, no transform read and no mesh needed. The tool origin sits at followRot*offset from the
            // hand, so the hand expressed in the pre-aim tool frame is p = -Inverse(preAim)*offset. Holding p fixed
            // gives position' = position + rot_noAim*p - rot*p, and substituting rot_noAim*p = followPos - position
            // collapses the whole thing to the line below. At swingAimWeight 0, aimD is the identity so rot ==
            // rot_noAim and this returns followPos + followRot*offset EXACTLY — the carry-pose regression guarantee
            // is bit-for-bit intact through the pivot.
            Vector3 handInPreAimToolFrame = -(Quaternion.Inverse(preAim) * offset);
            position = followPos - rotation * handInPreAimToolFrame;
        }

        /// <summary>Back-compat overload for callers that predate the 86cb6v03j swing-aim channel (the EditMode seat
        /// suites, AttackClipPoseDiag's fit, the F9 tool's preview). Applies NO swing-aim delta, which is exactly
        /// the pre-86cb6v03j composition — so an existing test that goes green through this seam is still testing
        /// the same thing it always was, rather than silently acquiring a new term.</summary>
        public static void ComposeSeat(Vector3 followPos, Quaternion followRot,
                                       Vector3 seatOffsetFromHand, Vector3 seatEuler,
                                       Vector3 mineOffsetDelta, Vector3 mineEulerDelta, float mineWeight,
                                       out Vector3 position, out Quaternion rotation)
            => ComposeSeat(followPos, followRot, seatOffsetFromHand, seatEuler, mineOffsetDelta, mineEulerDelta,
                           mineWeight, Vector3.zero, 0f, out position, out rotation);

        /// <summary>
        /// The two ends of the held tool's LONG axis in WORLD space — the haft LINE the two-hand grip read is
        /// measured against (<see cref="TwoHandGripRead"/>). Returns false when no displayed mesh can be resolved.
        ///
        /// The long axis is taken FROM THE MESH BOUNDS, never from an assumed convention: weapons are authored
        /// blade-along-+Z in Blender but <c>bakeAxisConversion</c> maps that to Unity +Y on import, so a hard-coded
        /// axis is the documented mis-seat trap (unity-conventions.md §FBX — the AxeSeatProbe round). The endpoint
        /// NEARER the mesh origin is the GRIP, because the family export contract puts the grip origin at (0,0,0)
        /// (blender-asset-pipeline.md §6). This mirrors AttackClipPoseDiag.BuildPropRig exactly, so an editor
        /// measurement and this runtime read resolve the SAME segment.
        ///
        /// The mesh lives on the WeaponMeshHolder CHILD (the rig stomps its own transform every frame, so the mesh
        /// must not be on it — #100 BUG-2), and HeldWeaponCycleDebug swaps that mesh on [B]; the resolution is
        /// therefore cached against the mesh instance and re-derived whenever it changes.
        /// </summary>
        public bool TryGetHaftSegment(out Vector3 gripWorld, out Vector3 headWorld)
        {
            gripWorld = headWorld = Vector3.zero;
            var mf = GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return false;

            if (_haftMesh != mf.sharedMesh || _haftHolder != mf.transform)
            {
                _haftMesh = mf.sharedMesh;
                _haftHolder = mf.transform;
                Bounds b = _haftMesh.bounds;
                int ax = 0;
                if (b.size.y > b.size[ax]) ax = 1;
                if (b.size.z > b.size[ax]) ax = 2;
                Vector3 lo = b.center, hi = b.center;
                lo[ax] = b.min[ax]; hi[ax] = b.max[ax];
                bool loIsGrip = lo.magnitude <= hi.magnitude;
                _haftGripLocal = loIsGrip ? lo : hi;
                _haftHeadLocal = loIsGrip ? hi : lo;
            }

            gripWorld = _haftHolder.TransformPoint(_haftGripLocal);
            headWorld = _haftHolder.TransformPoint(_haftHeadLocal);
            return true;
        }

        /// <summary>
        /// 86cay4282 round 3 — THE HAFT'S OWN LONG AXIS, expressed in the HAND-LOCAL frame that
        /// <see cref="mineSeatOffsetDelta"/> lives in, pointing from the GRIP/butt end toward the HEAD.
        ///
        /// WHY IT EXISTS. Sliding the grip up or down the stick is ONE physical degree of freedom, but
        /// <see cref="mineSeatOffsetDelta"/> is expressed in the hand's frame, so that one motion is a blend of all
        /// three hand-local axes through a large seat rotation. The Sponsor was asked to reach it with arrows (X/Z)
        /// plus PgUp/PgDn (Y) through a ~(-25, 70, 24) seat delta — three coupled dials for one intent, which is what
        /// made the grip position undialable. Adding this axis makes it one key pair.
        ///
        /// EVALUATED AT FULL MINE WEIGHT, DELIBERATELY. The live haft direction depends on the CURRENT eased weight
        /// (the rotation delta is scaled by it), so a slide computed from the live pose would follow a different axis
        /// depending on where in the ease the dial happened to be pressed — and the delta is only ever APPLIED at full
        /// weight. This returns the axis the tool will actually have when engaged, so a nudge pressed at rest and the
        /// same nudge pressed mid-swing move the grip identically. (Same family as the engagement-weighted-dial trap in
        /// procedural-animation-verbs.md §Debug-instrument caveat: a dial whose behaviour silently depends on an
        /// engagement weight is indistinguishable from a broken one.)
        ///
        /// Returns false when no mesh is resolvable — callers must not fall back to a guessed axis (the
        /// bakeAxisConversion +Z-becomes-+Y trap, unity-conventions.md §FBX).
        /// </summary>
        public bool TryGetMineHaftAxisHandLocal(out Vector3 axisHandLocal)
        {
            axisHandLocal = Vector3.zero;
            if (!TryGetHaftSegment(out Vector3 gripWorld, out Vector3 headWorld)) return false;
            return TryMineHaftAxisHandLocal(transform.rotation, headWorld - gripWorld,
                                            seatEuler, mineSeatEulerDelta, out axisHandLocal);
        }

        /// <summary>
        /// The pure form of <see cref="TryGetMineHaftAxisHandLocal"/>, so an EditMode test can pin the frame algebra
        /// without a live rig or a live Animator.
        ///
        /// The step that makes this exact rather than approximate: the haft direction expressed in the TOOL's OWN
        /// frame is FRAME-INVARIANT — the mesh sits on a holder rigidly parented to the tool root, so
        /// <c>Inverse(toolRotation) * haftWorld</c> is the same vector no matter how the rig has the tool oriented this
        /// frame (or whether <c>LateUpdate</c> has run yet). Re-expressing that constant through the seat rotation the
        /// tool WILL have at full mine weight gives the hand-local axis, with no dependence on the hand's own
        /// rotation — which is exactly why the result is facing-invariant, like every other dial on this seat.
        /// </summary>
        public static bool TryMineHaftAxisHandLocal(Quaternion toolRotation, Vector3 haftSegWorld,
                                                    Vector3 seatEuler, Vector3 mineEulerDelta,
                                                    out Vector3 axisHandLocal)
        {
            axisHandLocal = Vector3.zero;
            if (haftSegWorld.sqrMagnitude < 1e-10f) return false;
            Vector3 inToolFrame = Quaternion.Inverse(toolRotation) * haftSegWorld.normalized;
            axisHandLocal = (Quaternion.Euler(seatEuler) * Quaternion.Euler(mineEulerDelta)) * inToolFrame;
            return true;
        }

        /// <summary>
        /// 86cay4282 round 3 — slide the MINE seat along the haft's own long axis by <paramref name="metres"/>, in the
        /// direction the HANDS appear to travel: POSITIVE moves the hands UP the haft toward the HEAD (choking up),
        /// NEGATIVE moves them DOWN toward the BUTT.
        ///
        /// The sign inversion is deliberate and is the whole reason this is a named method rather than a raw add. The
        /// hands are posed by the clip and do not move; the TOOL does. So to make the hands read as HIGHER up the
        /// haft, the tool must translate BUTT-FIRST — the opposite direction to the one the label names. Leaving that
        /// inversion at the call site is how a dial ends up doing the reverse of its own on-screen hint.
        ///
        /// Returns false (and changes nothing) when the haft axis cannot be resolved, so a mis-wired mesh gives no
        /// silent partial slide along a guessed axis.
        /// </summary>
        public bool TrySlideMineSeatAlongHaft(float metres)
        {
            if (!TryGetMineHaftAxisHandLocal(out Vector3 axis)) return false;
            mineSeatOffsetDelta -= axis * metres;
            return true;
        }
    }
}
