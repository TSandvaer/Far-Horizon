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

        private Vector3 _dampedPos;
        private Quaternion _dampedRot;
        private bool _dampInit;

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
            ComposeSeat(followPos, followRot, seatOffsetFromHand, seatEuler,
                        mineSeatOffsetDelta, mineSeatEulerDelta, _mineWeight,
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
                                       out Vector3 position, out Quaternion rotation)
        {
            Vector3 offset = seatOffsetFromHand + mineOffsetDelta * mineWeight;
            // The MINE rotation delta is composed in the TOOL's OWN frame (right-multiplied onto the seat euler),
            // the same frame the F9 dial nudges in via ComposeLocalRot — so dialled == baked == applied.
            Quaternion seat = Quaternion.Euler(seatEuler) * Quaternion.Euler(mineEulerDelta * mineWeight);
            position = followPos + followRot * offset;
            rotation = followRot * seat;
        }

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
