using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// The player's visual avatar: the HYPER3D + MIXAMO generated castaway — a chunky low-poly young/hopeful
    /// castaway (ticket 86ca8rdkp). This SUPERSEDES the Sketchfab "Mini Chibi Kid": the chibi's realistic-
    /// leaning face couldn't carry the identity, and the Sponsor APPROVED the swap (2026-06-15) after the
    /// viability spike (86ca8r72j / PR #45) proved this asset animates clean + on-style in the shipped URP exe.
    /// The character ships sculpted hair + clothing IN THE MESH (no procedural hair-cap / cap-hide needed —
    /// those were chibi-specific). Its big-head toy proportions are INTRINSIC to the mesh — no bone-scale dials.
    ///
    /// WHY A BAKED SKINNED MESH (the durable fix for a bug CLASS): the spike's earlier PROCEDURAL humanoid
    /// shipped BROKEN in the exe — "legs pointing upwards" — because an Awake-assembled hierarchy serialized
    /// differently than the editor-time build (unity-conventions.md §editor-vs-runtime). A skinned mesh with a
    /// baked bone skeleton + imported clips CANNOT exhibit that failure by construction: the skeleton + skin
    /// live in the FBX, not assembled at runtime. The model child is built EDITOR-TIME (BuildInEditor, called
    /// by MovementCameraScene) so it SERIALIZES into Boot.unity; runtime code only animates what's serialized.
    ///
    /// SELF-DRIVING (the U5/U3 seam): this component lives on a CHILD avatar root under the player (so its
    /// avatar-height scale doesn't scale the NavMeshAgent), and reads the player's <see cref="NavMeshAgent"/>
    /// velocity (resolved from the parent) itself each frame to flip the Idle&lt;-&gt;Walk Animator blend and
    /// yaw the model toward travel. It does NOT modify ClickToMove. The agent has updateRotation=false
    /// (ClickToMove owns that contract), so the visual owns facing.
    ///
    /// MATERIALS — a single flat DE-LIT URP/Lit material (CastawayMat) from texture_diffuse, authored by
    /// CharacterAssetGen + bound onto the SkinnedMeshRenderer editor-time by MovementCameraScene. The de-lit
    /// baked albedo reads flat/toon (the project's URP/Lit toon idiom). The IDENTITY RECOLOR (warm the
    /// generated mustard shirt toward tan) lives in the texture_diffuse PNG's repainted pixels
    /// (CharacterAssetGen.RecolorShirtToTan, a luma-preserving HSV remap) — NOT a per-material tint (which
    /// would flatten the toon gradient — the trap this class always warned of).
    /// </summary>
    public class CastawayCharacter : MonoBehaviour
    {
        [Header("Model source (wired by MovementCameraScene at author time)")]
        // The imported with-skin Idle FBX prefab (rig + Idle clip + Humanoid avatar) and the Idle<->Walk
        // controller. CharacterAssetGen produces both; MovementCameraScene also binds the de-lit material.
        public GameObject modelPrefab;
        public RuntimeAnimatorController animatorController;

        [Header("Facing")]
        [Tooltip("How fast the body yaws toward the travel direction (higher = snappier).")]
        public float turnLerp = 12f;

        [Header("Locomotion thresholds")]
        [Tooltip("Planar speed (u/s) above which the character is considered walking (drives the " +
                 "Idle<->locomotion blend). Squared internally.")]
        public float walkSpeedThreshold = 0.15f;

        [Header("Locomotion transition smoothing (86caay44r)")]
        [Tooltip("Damp time (s) for the Speed param fed to the locomotion blend tree. The Sponsor reported " +
                 "idle<->walk<->run transitions as too ABRUPT — most noticeable stopping a walk (release W), " +
                 "where the agent velocity drops to 0 in one frame and the blend SNAPS Walk->Idle. Feeding Speed " +
                 "through Animator.SetFloat(param, value, dampTime, dt) instead of a raw set EASES the parameter " +
                 "toward its target over ~this many seconds, so starts/stops/speed-changes glide rather than snap. " +
                 "~0.18s reads smooth without feeling mushy (the Sponsor judges in soak; tunable from the build). " +
                 "0 = the old raw, instant set. Keeps the Walk<->Run blend (#68) intact — it just eases the SAME " +
                 "Speed param the blend tree already consumes (a smoother crossfade, never a different clip).")]
        public float speedDampTime = 0.18f;

        [Tooltip("Planar speed (u/s) at/above which the WALK<->RUN blend (86ca9yq34) reads as a full RUN. " +
                 "The blend tree is 1D on the Speed param: <= walk speed plays Walk, >= this plays Run, and " +
                 "between them blends smoothly (AC1's smooth Walk<->Run blend). Set to the WASD run speed " +
                 "(WasdMovement.runSpeed) so holding Shift reaches a full run; the WASD walk speed lands in the " +
                 "Walk band. Exposed so the run state (IsRunning) reads consistently with the blend.")]
        public float runSpeedThreshold = 9.5f;

        // 86caa83wn fix 2 — IsRunning engages at this FRACTION of runSpeedThreshold (not strictly at it), so a
        // NavMeshAgent simulated velocity that lags/dips just below the COMMANDED run speed still reads running
        // and the held-axe RUN clamp stays engaged through the whole cycle (the strict ">= threshold" compare
        // flickered false on accel/decel ramps → the clamp popped off → axe into the head). 0.85·9.5 ≈ 8.1 is
        // safely above the 5.5 walk speed (a walk never reads running) and below the 9.5 run speed (a run
        // reliably does). NOT a serialized field — a fixed engage margin, audited from source.
        public const float RunEngageFraction = 0.85f;

        [Header("Walk-float model-sole grounding (86ca8rdkp attempt-9 — the WALK-clip body-lift fix)")]
        [Tooltip("Ground the VISIBLE rendered SOLE (scale-immune) by offsetting the MODEL CHILD's local-Y, on " +
                 "top of the root snap. The Mixamo WALK clip authors the body ~0.66u higher than IDLE (clip-trace: " +
                 "Idle sole at root-relative -0.003, Walk at +0.63..+0.69), so the rendered mesh floats only while " +
                 "walking even though the root is grounded. This cancels that per-clip lift so the feet plant in " +
                 "both states. Default ON; the PlayMode walk-grounding regression toggles it OFF to prove the fix " +
                 "is load-bearing.")]
        public bool modelSoleGround = true;
        [Tooltip("Convergence rate (1/s) for the model-sole grounding. HIGHER than the root snapRate because " +
                 "this cancels a MEASURED per-clip-frame lift (deterministic, not noisy terrain) — it should " +
                 "track tightly so the sole stays planted every stride frame and the Idle→Walk transition " +
                 "(a +0.66 step) converges in a few frames, not lagging ~10cm as a transient. 90 = ~78% " +
                 "convergence per 60fps frame; smooth enough to avoid a pop, tight enough to plant the walk.")]
        public float modelSoleGroundRate = 90f;

        [Header("Ground snap (86ca8rdkp soak-fix #1 — 'walking in the air')")]
        [Tooltip("Snap the avatar feet to the VISIBLE terrain each frame. The NavMeshAgent grounds the " +
                 "player ROOT on the flat NavMesh collider, which sits ABOVE the dipping Zone-D visual " +
                 "terrain (ground-trace 2026-06-15: feet at 0.081 vs visible sand at 0.020 = a 6cm float). " +
                 "A downward raycast onto the Ground layer plants the feet on the VISIBLE-terrain collider " +
                 "(the renderer-ENABLED Ground hit) — skipping the flat NavMesh slab whose renderer is off.")]
        public bool groundSnap = true;
        [Tooltip("Layer mask the ground-snap raycast tests (the VISIBLE terrain — the Ground layer). " +
                 "Wired editor-time; defaults to the Ground layer at runtime if unset.")]
        public LayerMask groundMask;
        [Tooltip("How far above the player root the ground ray starts (must clear the avatar's own height " +
                 "so the ray doesn't originate inside the mesh).")]
        public float groundRayUp = 3f;
        [Tooltip("Max ground-ray distance below the start point.")]
        public float groundRayDown = 12f;

        [Header("Ground snap convergence (86ca8rdkp EXTENSIVE-DEBUG round — unified rate, kills the arrival BOB)")]
        [Tooltip("Snap convergence rate (1/s) — UNIFIED across rest AND walk. The prior speed-adaptive split " +
                 "(60 moving / 18 rest) made the rate JUMP at the moving→rest transition, so the still-" +
                 "converging error visibly settled the instant the agent stopped = the arrival BOB the Sponsor " +
                 "reported. With the snap target now the STABLE baked sole (no animation-envelope wobble), one " +
                 "rate tracks the descending foreshore tightly while walking AND doesn't pop at rest — no rate " +
                 "discontinuity at arrival, no bob. 40 = ~49% convergence per 60fps frame.")]
        public float snapRate = 40f;

        // RETAINED (serialized) for back-compat with scenes baked before the unify; no longer read by the snap.
        // The during-walk speed-adaptive split was superseded by the single snapRate above (kills the bob).
        [HideInInspector] public float snapRateRest = 18f;
        [HideInInspector] public float snapRateMove = 60f;

        [Header("Ground Y-OFFSET — Sponsor-dialable (86ca8rdkp 4th-attempt; F9 nudge target)")]
        [Tooltip("A constant world-Y offset added to the snapped feet (and the shadow). 4 attempts on the " +
                 "feet-on-ground placement → give the Sponsor the KNOB: he dials this in-game on the F9 nudge " +
                 "tool's GROUND-Y target (PageUp/Down) until the feet sit EXACTLY on the visible sand, reads " +
                 "the value off the HUD/log, and reports it to bake here. What-you-dial-is-what-you-get. " +
                 "Default 0 = plant exactly on the raycast hit (the geometric ground).")]
        public float groundYOffset = 0f;

        [Header("Contact shadow (86ca8rdkp re-soak #2 — 'he STILL seems elevated')")]
        [Tooltip("The blob/contact shadow under the feet. The shadow is a child of the PLAYER ROOT and does " +
                 "NOT inherit the avatar ground-snap, so on the dipping foreshore it was STRANDED ~9cm ABOVE " +
                 "the snapped feet — the body read as floating above its own shadow = 'elevated' (foot-trace " +
                 "2026-06-15: feet planted at the sand, but BlobShadow 9cm above them). Driving the shadow's " +
                 "world-Y down onto the snapped feet each frame grounds the contact read. Wired editor-time.")]
        public Transform blobShadow;
        [Tooltip("Small lift (world units) of the shadow above the snapped feet so it doesn't z-fight the sand.")]
        public float blobShadowLift = 0.02f;

        [Header("Jump (ticket 86ca9yq3q — Space → vertical impulse + arc + land)")]
        [Tooltip("Initial upward velocity (u/s) the jump imparts. The arc apex height ≈ jumpVelocity^2 / " +
                 "(2·gravity). 5.5 u/s with gravity 18 → ~0.84u apex (a clear hop on the 1.8u castaway, reads " +
                 "from the gameplay orbit). Sponsor-tunable from the build.")]
        public float jumpVelocity = 5.5f;
        [Tooltip("Downward acceleration (u/s^2) applied to the jump arc while airborne. Higher = a snappier, " +
                 "less floaty hop. 18 gives a brisk ~0.62s round-trip at jumpVelocity 5.5.")]
        public float jumpGravity = 18f;

        // Animator parameters the controller blends on (set each frame from the agent's velocity).
        // Moving (bool): flips the Idle<->locomotion (Walk/Run blend tree) transition.
        // Speed (float): the planar agent speed (u/s) the locomotion 1D blend tree blends Walk<->Run on
        // (86ca9yq34). Walk clip <= walkSpeed, Run clip >= runSpeed, smooth blend between — driven from the
        // SAME agent.velocity magnitude WasdMovement commands (walkSpeed walking, runSpeed sprinting).
        public const string MovingParam = "Moving";
        public const string SpeedParam = "Speed";
        // The one-shot Jump trigger (86ca9yq3q) — pulsed on the rising edge of a jump so the Animator plays a
        // Jump clip once (the AnyState→JumpIdle/JumpRunning transitions the controller wires, selected by the
        // Moving bool) and returns. Mirrors CharacterAssetGen.JumpParam (kept in sync so the trigger fires the
        // state the controller built).
        public const string JumpParam = "Jump";
        // The GROUNDED bool (86ca9yq3q rework — THE floating-bug fix) — driven = !IsAirborne each frame. The jump
        // states transition back to the LOCOMOTION blend tree (Grounded && Moving) or Idle (Grounded && !Moving)
        // the MOMENT this flips true on landing, so if W is still held Walk/Run resumes on the SAME frame instead
        // of stalling in the finished jump pose (which translated while non-locomotion → the "floating" percept).
        // Mirrors CharacterAssetGen.GroundedParam.
        public const string GroundedParam = "Grounded";

        private NavMeshAgent _agent;
        private Animator _animator;
        private Transform _model;       // the instantiated FBX root, yaw-rotated toward facing
        private float _bodyYaw;
        private Vector3 _lastFacing = Vector3.forward;
        private bool _built;

        // Exposed for tests / later systems: current Idle/Walk state read off the agent.
        public bool IsWalking { get; private set; }

        /// <summary>The planar agent speed (u/s) fed to the Walk<->Run blend tree's Speed param LAST frame
        /// (86ca9yq34). Exposed so the PlayMode AC5 regression can assert the run drives a FASTER speed (→ the
        /// Run clip blends in) than walk, off the agent velocity — without depending on the Animator having
        /// physically advanced the clip (headless Time.deltaTime≈0, the documented headless-time trap).</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>Whether the character is RUNNING this frame: the planar agent speed is at/above
        /// runSpeedThreshold (86ca9yq34). The Walk<->Run blend reads as a full Run here. Exposed so the AC5
        /// regression asserts the run state flips when Shift drives the faster speed (and stays false at walk
        /// speed). Reads off the same Speed value the blend tree consumes — consistent with what renders.</summary>
        public bool IsRunning { get; private set; }

        // === JUMP (ticket 86ca9yq3q). A ballistic VERTICAL arc on the avatar root, on TOP of the ground snap.
        // The NavMeshAgent owns world XZ (it keeps the player on the NavMesh — a jump does NOT change that; you
        // can jump while idle AND while moving, AC1); the agent has NO vertical channel, so the jump arc is a
        // pure local-Y offset added to the avatar root here, where the grounded local-Y is already owned. While
        // airborne the per-frame GROUND SNAP is SUSPENDED (AC3 — else modelSoleGround / the root snap would PIN
        // the feet back to the terrain mid-jump); it RE-ENGAGES on landing with the grounded behavior UNCHANGED. ===
        private bool _airborne;
        private float _jumpVelY;   // current vertical velocity of the arc (u/s); +up
        private float _jumpY;      // current arc height above the grounded position (world-up units); 0 when grounded
        // The grounded avatar-root local-Y captured at lift-off (the snap's last settled value). The arc is added
        // ON TOP of this; on landing we restore the snap from this baseline so grounding resumes seamlessly.
        private float _jumpBaseLocalY;

        // === RUNTIME JUMP-TRACE (ticket 86caaqhj5 — the "pulled back on landing" RE-DIAGNOSIS instrument).
        // AUTO-fires on EVERY jump (NO toggle/launch-arg) so the Sponsor just PLAYS (W/A/S/D + Space) and the
        // ground truth lands in Player.log; the orchestrator reads it after. Cheap + build-safe: one greppable
        // Debug.Log per frame ONLY inside the per-jump window (launch → 0.5s post-landing), silent otherwise.
        //
        // WHY a RUNTIME trace (not EditMode): EditMode anim-grounding/jump traces have PASSED 3× while the soak
        // FAILED (dispatch note; the headless Time.deltaTime≈0 trap means the Animator/arc never ticks the way
        // live play does — unity-conventions.md §Headless). The runtime trace captures the LIVE per-frame world
        // motion the Sponsor actually sees.
        //
        // WHAT IT MEASURES (the candidates the dispatch enumerates — root XZ pull-back vs avatar-child snap vs
        // air-control decel): the PLAYER ROOT world X/Z/Y (the NavMeshAgent owns this — updatePosition=true), the
        // AVATAR-CHILD world X/Z/Y (this transform — the jump arc is a local-Y here), agent.velocity (what
        // WasdMovement commands), agent.desiredVelocity + hasPath/pathPending (the agent's OWN steering target —
        // 0/none under WASD, so autoBraking/acceleration decelerate the body), agent.nextPosition (where the
        // agent's internal sim wants the root NEXT), the airborne flag + the exact LANDING frame, _jumpY (arc),
        // and which camera-follow path is active (airborne tight-follow vs grounded lead). The root-XZ delta
        // launch→land is THE discriminator: if the root is pulled BACK in the world, it's the agent sim (a/c);
        // if the root advances but the child snaps, it's (b).
        private int _jumpTraceFrame;            // frame counter within the current trace window
        private float _jumpTracePostLandT;      // seconds remaining to keep tracing after landing (0 = window closed)
        private bool _jumpTraceActive;          // a trace window is open this jump
        private Vector3 _jumpTraceLaunchRootXZ; // player-root world XZ captured at lift-off (the pull-back baseline)
        private const float JumpTracePostLandSeconds = 0.5f; // keep tracing 0.5s past touch-down (catch the snap)

        /// <summary>Whether the castaway is mid-jump (airborne) this frame (86ca9yq3q). While true the ground
        /// snap is suspended and the avatar rides the jump arc. Exposed so the PlayMode AC5 regression asserts
        /// the airborne phase suspends grounding, and so later systems (jump SFX, fall damage) can read it.</summary>
        public bool IsAirborne => _airborne;

        /// <summary>The current jump-arc height above the grounded position (world-up units); 0 when grounded
        /// (86ca9yq3q). Exposed so the AC5 regression asserts the root Y RISES then RETURNS to grounded across
        /// the arc without depending on physical agent traversal (the head/apex is &gt; 0, landing is ~0).</summary>
        public float JumpHeight => _airborne ? _jumpY : 0f;

        /// <summary>Whether the RUNTIME jump-trace window is OPEN this frame (86caaqhj5 — the "pulled back on
        /// landing" instrument). True from a jump's lift-off until ~0.5s past touch-down, while the per-frame
        /// [JumpTrace] line auto-logs to Player.log; false otherwise (silent). Exposed READ-ONLY so the EditMode
        /// guard can assert TryJump OPENS the window (the silent-instrument bug class — a trace that never fires
        /// is the CaptureGate/FloatTrace silent-killer family, unity-conventions.md §Component-not-serialized).
        /// No PlayMode fixture needed: TryJump (grounded-only) opens the window synchronously.</summary>
        public bool JumpTraceActive => _jumpTraceActive;

        /// <summary>
        /// Begin a jump (ticket 86ca9yq3q) — Space's rising edge calls this (WasdMovement). Imparts the upward
        /// impulse + pulses the Jump animator trigger, but ONLY when grounded (a mid-air re-press is ignored — no
        /// double-jump; control returns on landing). Idempotent while airborne. Returns true iff a jump started
        /// (so a caller can gate a jump SFX on the actual lift-off). Works identically idle or moving — the agent
        /// keeps driving XZ; this only adds the vertical arc.
        /// </summary>
        public bool TryJump()
        {
            if (_airborne) return false;          // already mid-jump — no double-jump
            if (!groundSnap) return false;        // a rig with grounding off has no settled baseline to launch from
            _airborne = true;
            _jumpVelY = Mathf.Max(0.01f, jumpVelocity);
            _jumpY = 0f;
            _jumpBaseLocalY = _snapInit ? _snapLocalY : transform.localPosition.y; // launch from the grounded local-Y
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetTrigger(JumpParam);  // fire the one-shot Jump state (AnyState→Jump on the trigger)

            // RUNTIME JUMP-TRACE (86caaqhj5): open the per-jump trace window at lift-off. Capture the player-root
            // world XZ NOW — the launch→land delta of this baseline is the "pulled back" discriminator.
            Transform jr = transform.parent != null ? transform.parent : transform;
            _jumpTraceLaunchRootXZ = new Vector3(jr.position.x, 0f, jr.position.z);
            _jumpTraceFrame = 0;
            _jumpTracePostLandT = JumpTracePostLandSeconds;
            _jumpTraceActive = true;
            return true;
        }

        /// <summary>
        /// Force the model to a KNOWN body yaw immediately (verification-only determinism hook). The verify
        /// capture cannot rely on the rest-state facing being a fixed axis, so it pins the facing to a known
        /// value before framing. Inert for normal gameplay (LateUpdate keeps driving facing off velocity).
        /// </summary>
        /// <param name="yawDeg">World Y yaw (0 = the model's rest/intrinsic facing).</param>
        public void FaceWorldYawInstant(float yawDeg)
        {
            _bodyYaw = yawDeg;
            float rad = yawDeg * Mathf.Deg2Rad;
            _lastFacing = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            if (_model != null)
                _model.localRotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }

        /// <summary>Current body yaw (degrees) the model is rotated to — exposed so the verify capture can
        /// derive the camera position from the SAME facing it pinned (no axis assumption).</summary>
        public float BodyYaw => _bodyYaw;

        /// <summary>
        /// The instantiated FBX MODEL child — the transform that gets yaw-rotated toward facing
        /// (<c>_model.localRotation = Euler(0, _bodyYaw, 0)</c>, see <see cref="LateUpdate"/>). Exposed
        /// (86ca9xz00) as the FACING-CARRYING frame — still consumed by CastawayArmPose + the AxeWalkTrace
        /// instrument. (HISTORY: 86ca9xz00 wired a held-axe swing-stabilizer's grip anchor to this frame; that
        /// stabilizer was REMOVED in 86ca9zcjn when the Sponsor chose for the held axe to FOLLOW the arm's
        /// natural swing — the axe now rides the RAW hand bone directly. The model child still owns facing.)
        ///
        /// WHY THIS, NOT the CastawayCharacter root: facing lives on the MODEL CHILD, not on this component's
        /// transform (the agent has updateRotation=false; "the visual owns facing", :25). Resolves editor-time
        /// after BuildInEditor (the wiring path) and at runtime after RebindFromHierarchy/BuildModel.
        ///
        /// STATIC-LOAD FALLBACK (the editor-vs-runtime serialization trap): _model is a private runtime field,
        /// NOT serialized, so it is null on a freshly-deserialized scene where Awake hasn't run (an EditMode
        /// scene-presence test, or any editor-static load). The MODEL CHILD itself IS serialized in the
        /// hierarchy (built editor-time by BuildInEditor → the first child, named "Model"), so fall back to the
        /// first child when _model is unresolved — RebindFromHierarchy uses the same GetChild(0) contract. This
        /// lets the editor-time wiring + the scene-presence guard read the model child without a play loop.
        /// </summary>
        public Transform ModelTransform => _model != null ? _model
            : (transform.childCount > 0 ? transform.GetChild(0) : null);

        void Awake()
        {
            // The agent lives on the player ROOT (this component is on a child avatar root so its
            // height-scale doesn't scale the agent). Resolve it from the parent chain.
            _agent = GetComponentInParent<NavMeshAgent>();
            if (_agent == null)
                Debug.LogWarning("[CastawayCharacter] no NavMeshAgent found in parents — the " +
                                 "Idle<->Walk anim + facing won't drive (avatar must be a child of the " +
                                 "player root that carries the agent)");
            // If the author-time build already serialized the Model child into the scene (the ship path),
            // re-bind to it rather than re-instantiate. Otherwise build at runtime (defensive fallback).
            if (transform.childCount > 0 && _model == null) RebindFromHierarchy();
            if (!_built) BuildModel();
        }

        /// <summary>
        /// Editor build entry: MovementCameraScene calls this so the Model child + the Animator controller
        /// reference SERIALIZE into Boot.unity (the editor-vs-runtime serialization lesson). Idempotent:
        /// clears prior children first.
        /// </summary>
        public void BuildInEditor()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _model = null; _animator = null; _built = false;
            BuildModel();
        }

        private void RebindFromHierarchy()
        {
            _model = transform.childCount > 0 ? transform.GetChild(0) : null;
            _animator = GetComponentInChildren<Animator>();
            _built = _model != null;
        }

        private void BuildModel()
        {
            if (modelPrefab == null)
            {
                Debug.LogError("[CastawayCharacter] modelPrefab not wired — cannot build avatar");
                return;
            }

            // Instantiate the imported FBX under the avatar root. In the editor-build path the resulting
            // SkinnedMeshRenderer + bone hierarchy SERIALIZE into Boot.unity, so the shipped exe loads the
            // SAME baked skeleton the editor sees (no Awake-assembled hierarchy to diverge — the legs-up
            // lesson). Works for both editor-build + runtime-fallback with no UnityEditor dependency.
            GameObject go = Instantiate(modelPrefab, transform, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;   // FBX origin is at the feet -> grounded feet
            go.transform.localRotation = Quaternion.identity;
            _model = go.transform;

            _animator = go.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = go.AddComponent<Animator>();
            // Preserve the FBX-imported avatar across the controller assignment: capture it FIRST, assign the
            // controller, then RE-ASSERT — an Animator with a missing/cleared avatar at runtime can't bind the
            // clips to the skeleton and renders a frozen/collapsed skin. 86ca8rdkp.
            Avatar imported = _animator.avatar;
            if (imported == null)
                Debug.LogWarning("[CastawayCharacter] Animator has NO avatar — clips will not bind " +
                                 "(Idle.fbx must import Generic with avatarSetup=CreateFromThisModel)");
            if (animatorController != null) _animator.runtimeAnimatorController = animatorController;
            if (imported != null) _animator.avatar = imported; // re-assert post-controller
            _animator.applyRootMotion = false; // NavMeshAgent drives position; anim is in-place

            // No cap-hide / procedural hair here (chibi-specific, removed for the Hyper3D castaway — it ships
            // sculpted hair in the mesh). The de-lit material is bound editor-time by MovementCameraScene.
            _built = true;
        }


        // The avatar root's local Y is normally 0 (feet on the player root). The ground-snap drives it to
        // a (usually small NEGATIVE) value so the feet plant on the VISIBLE terrain that dips below the
        // agent's NavMesh ground point. Smoothed so terrain undulation doesn't pop the avatar.
        private float _snapLocalY;
        private bool _snapInit;

        // The WALK-FLOAT model-sole grounding (86ca8rdkp attempt-9). The model CHILD's local-Y is driven so the
        // scale-immune rendered sole plants on the grounded root, cancelling the per-clip body-lift baseline
        // (Idle ~0, Walk ~+0.66). Smoothed at snapRate so a clip blend doesn't pop. _modelGroundInit snaps the
        // first frame to the residual (no startup slide).
        private float _modelLocalY;
        private bool _modelGroundInit;

        /// <summary>The current ground-snap local-Y applied to the avatar root (0 = no snap). Exposed for
        /// the PlayMode grounding regression so it can assert the feet are planted on the visible surface.</summary>
        public float GroundSnapLocalY => _snapLocalY;

        /// <summary>The MODEL CHILD's current local-Y — the per-frame modelSoleGround offset that cancels the
        /// Mixamo WALK-clip body-lift (Idle ~0, Walk ~+0.66). Exposed READ-ONLY for the -axeWalkTrace instrument
        /// (86ca9ykp0): this is the Y-oscillation a HeldAxeRig stabilizeFrame=_model rides, so the trace can
        /// PIN it as the bounce/ratchet source. NaN until the model is built.</summary>
        public float ModelLocalY => _model != null ? _model.localPosition.y : float.NaN;

        /// <summary>The WORLD Y of the surface the LAST ground-snap raycast SELECTED to plant the feet on
        /// (NaN if no ground was hit). This is the snap TARGET before smoothing — exposed so the PlayMode
        /// regression can assert the snap picks the VISIBLE terrain (not the proxy slab) WITHOUT depending on
        /// the smoothed lerp converging, which it can't in headless runs (Time.deltaTime≈0, the documented
        /// headless-time trap — unity-conventions.md §Headless).</summary>
        public float LastSnapTargetWorldY { get; private set; } = float.NaN;

        // === LIVE FLOAT-DIAGNOSTIC readouts (86ca8rdkp — the instrument, F8 overlay + F9 GROUND-Y panel +
        // ~1Hz log). The Sponsor + orchestrator measure the residual feet-vs-sand float from ground truth
        // instead of arguing "is it fixed". Updated every frame inside ApplyGroundSnap. ===

        /// <summary>The avatar feet WORLD-Y this frame (the avatar root's world position Y — the FBX origin
        /// sits at the feet, so the avatar-root Y IS the feet Y). NaN until the first snap frame.</summary>
        public float FeetWorldY { get; private set; } = float.NaN;

        /// <summary>The RAW visible-ground raycast-hit WORLD-Y directly under the player this frame (the
        /// selected renderer-ENABLED Ground surface, BEFORE the groundYOffset is added). NaN if no visible
        /// ground was hit. This is the surface the player SEES under his feet — the GAP measures against it.</summary>
        public float GroundHitWorldY { get; private set; } = float.NaN;

        /// <summary>The live FLOAT GAP = feet world-Y − ground-hit world-Y. ~0 = feet planted on the visible
        /// sand; &gt;~0.01 (1 cm) = the avatar is floating (the bug). NaN until both readings are valid. This
        /// is THE number the Sponsor dials groundYOffset to drive to ~0.</summary>
        public float FloatGap { get; private set; } = float.NaN;

        // === THE BREAKTHROUGH READOUT (86ca8rdkp final — Sponsor + instrument, 2026-06-15). The Sponsor dialed
        // GROUND-Y until FloatGap reads 0.0000 "✓ planted" — YET the character STILL visibly floated above its
        // (grounded) shadow. So FloatGap's "feet" (the avatar ROOT world-Y, == the snap reference) is a PROXY
        // point DECOUPLED from the actual RENDERED mesh bottom: the proxy grounds (gap 0) while the visible mesh
        // sits a FIXED OFFSET above it. The TRUE visible sole is SkinnedMeshRenderer.bounds.min.y. We expose
        // BOTH the rendered-sole world-Y and the TRUE gap (rendered sole − ground) so the gauge can flip to the
        // honest number (GAP=0 ⟺ soles on the sand), and the snap can ground the SOLE, not the proxy root. ===

        /// <summary>The TRUE rendered mesh bottom WORLD-Y this frame — the lowest visible vertex (the visible
        /// SOLE), measured from the union of every child SkinnedMeshRenderer's world-space bounds. This is what
        /// the player SEES touch the sand. NaN until a renderer is resolved. (Read from the renderer's runtime
        /// world bounds, which DO reflect the avatar-root scale + the snapped position — NOT the stale
        /// import-baked .bounds the proportion guards must use BakeMesh for; here the renderer is live + rendered
        /// every frame, so its world .bounds are current.)</summary>
        public float MeshBottomWorldY { get; private set; } = float.NaN;

        /// <summary>The CONSERVATIVE SMR.bounds.min.y (the OLD proxy / animation-max AABB floor) this frame —
        /// DUMP-ONLY. Exposed so the [FloatTrace] line and the regression guard can show it BELOW the baked
        /// actual sole (MeshBottomWorldY), proving the bounds floor is the deeper false-green (grounding to it
        /// floats the real soles while a bounds-reading gauge agrees). NOT used for snapping or the gauge.</summary>
        public float SmrBoundsMinWorldY { get; private set; } = float.NaN;

        /// <summary>The TRUE float gap = rendered-mesh-bottom world-Y − ground-hit world-Y. ~0 ⟺ the visible
        /// SOLES sit on the visible sand (the HONEST "planted" the Sponsor judges). Identical to
        /// <see cref="FloatGap"/> now that the gauge reads the rendered sole; kept as the explicit-name alias
        /// the [FloatTrace] dump + regression guard reference. NaN until both valid.</summary>
        public float MeshFloatGap { get; private set; } = float.NaN;

        /// <summary>The OLD proxy gap = avatar-ROOT world-Y − ground-hit world-Y (the misleading reading that
        /// went to 0 while the rendered mesh floated — the false "✓ planted" the Sponsor caught). Kept exposed
        /// so the regression guard can prove the gauge flipped from this proxy to the rendered sole, and so the
        /// [FloatTrace] dump always carries both. NaN until valid.</summary>
        public float ProxyRootFloatGap { get; private set; } = float.NaN;

        /// <summary>Whether the agent is moving (above walkSpeedThreshold) this frame — surfaced so the
        /// diagnostic shows rest-vs-move (the during-walk float was state-dependent).</summary>
        public bool IsMovingForSnap { get; private set; }

        /// <summary>The snap convergence rate (1/s) active this frame (snapRateRest at rest, snapRateMove
        /// while moving) — surfaced so the Sponsor/orchestrator see which smoothing rate is in play.</summary>
        public float ActiveSnapRate { get; private set; }

        /// <summary>
        /// PURE GAP math (the unit-testable core of the diagnostic): the float gap between the avatar feet
        /// and the visible ground directly under them. Both args are WORLD-Y. Returns feet − ground; ~0 means
        /// planted, positive means floating ABOVE the sand. Static + dependency-free so the PlayMode test can
        /// assert "feet at a known Y over ground at a known Y yields the expected gap" with no scene rig.
        /// </summary>
        public static float ComputeFloatGap(float feetWorldY, float groundHitWorldY)
            => feetWorldY - groundHitWorldY;

        /// <summary>
        /// PURE model-sole grounding math (the unit-testable core of the WALK-FLOAT fix, 86ca8rdkp attempt-9):
        /// the local-Y the MODEL CHILD must take so its SCALE-IMMUNE rendered sole (currently at
        /// <paramref name="renderedSoleWorldY"/>) lands on <paramref name="plantWorldY"/>. The model child lives
        /// under the avatar root whose world Y-scale is <paramref name="rootYScale"/> (= PlayerVisualHeight), so a
        /// model local-Y delta moves the world sole by rootYScale × delta — divide the world residual by the
        /// scale. Static + dependency-free so the EditMode guard can assert "a WALK clip lifting the sole +0.66
        /// yields a model local-Y that cancels it" against a real SampleAnimation'd clip with no play loop.
        /// </summary>
        public static float ComputeModelGroundLocalY(float renderedSoleWorldY, float plantWorldY,
                                                     float currentModelLocalY, float rootYScale)
        {
            if (Mathf.Abs(rootYScale) < 1e-4f) rootYScale = 1f;
            float worldResidual = renderedSoleWorldY - plantWorldY;   // >0 ⟺ sole floats ABOVE the plant target
            return currentModelLocalY - worldResidual / rootYScale;
        }

        // Reusable RaycastAll buffer (no per-frame GC). Sized for the handful of Ground colliders a single
        // down-ray can ever cross (the visible terrain + the flat NavMesh slab + a little headroom).
        private readonly RaycastHit[] _snapHits = new RaycastHit[8];

        // Cached child SkinnedMeshRenderer(s) — the TRUE rendered-sole reference (86ca8rdkp). We bake the LIVE
        // skinned mesh each frame and take the actual lowest world-Y VERTEX — NOT SMR.bounds.min.y.
        private SkinnedMeshRenderer[] _smrs;

        // Reusable bake target per SMR (no per-frame Mesh alloc — BakeMesh reuses the buffer; allocating a Mesh
        // every frame leaks until GC). Sized lazily to match _smrs.
        private Mesh[] _bakeMeshes;
        // Reusable vertex scratch — BakeMesh writes into the mesh; mesh.vertices allocates a fresh array each
        // call, so we use GetVertices(List<>) into a reused List to avoid the per-frame array alloc.
        private readonly System.Collections.Generic.List<Vector3> _bakeVerts =
            new System.Collections.Generic.List<Vector3>(4096);

        /// <summary>
        /// The TRUE rendered SOLE world-Y this frame: the actual lowest skinned VERTEX across all child
        /// SkinnedMeshRenderers, computed by BAKING the live skinned mesh (current bone poses) and taking the
        /// min world-Y over its real vertices. NaN if no renderer is resolved.
        ///
        /// WHY NOT SMR.bounds.min.y (the deeper FALSE-GREEN the Sponsor's F8 gauge exposed, 86ca8rdkp final):
        /// Unity's SkinnedMeshRenderer.bounds is the CONSERVATIVE animation-MAX AABB — a single box sized to
        /// contain the mesh across the WHOLE animation range, NOT the current frame's mesh. So `bounds.min.y`
        /// sits BELOW the real visible soles (by the per-clip foot-lift envelope), and it WOBBLES as the idle
        /// anim plays (the −0.003↔+0.0003 standing-still GAP jitter the Sponsor reported). Grounding to
        /// bounds.min.y floats the character (the box floor is below the soles) while a gauge ALSO reading
        /// bounds.min.y agrees with the snap → GAP=0 "✓ planted" yet visibly floating. Baking the live mesh
        /// gives the ACTUAL current-frame lowest vertex — the surface the player sees touch the sand —
        /// deterministic and stable per pose (no animation-envelope wobble).
        ///
        /// TRANSFORM (the cm→m / lossy-scale trap, unity-conventions/character-pipeline): verts bake in the
        /// SMR transform's LOCAL space; we transform to WORLD via the SMR's own `localToWorldMatrix`, which
        /// carries the full parent chain INCLUDING the SMR node's own (large, cm→m-compensating) scale. Using
        /// BakeMesh(useScale:FALSE) here so the node scale is applied ONCE — by the matrix — not double-applied
        /// (useScale:true would bake the renderer's lossyScale into the verts, then the matrix scales again →
        /// a hugely-wrong Y, the 56u-tall trap CastawayVerifyCapture documents).
        /// </summary>
        private float MeasureRenderedSoleWorldY()
        {
            if (_smrs == null || _smrs.Length == 0)
                _smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_smrs == null || _smrs.Length == 0) return float.NaN;
            if (_bakeMeshes == null || _bakeMeshes.Length != _smrs.Length)
                _bakeMeshes = new Mesh[_smrs.Length];

            float minY = float.PositiveInfinity;
            bool any = false;
            for (int i = 0; i < _smrs.Length; i++)
            {
                var smr = _smrs[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (_bakeMeshes[i] == null) _bakeMeshes[i] = new Mesh { name = "CastawaySoleBake" + i };
                // useScale:false — verts come back in the SMR's authored mesh space (no renderer-node scale).
                smr.BakeMesh(_bakeMeshes[i], false);
                _bakeMeshes[i].GetVertices(_bakeVerts);
                if (_bakeVerts.Count == 0) continue;
                // SCALE-IMMUNE world matrix (86ca8rdkp attempt-8 ROOT-CAUSE fix): use the SMR's world POSITION
                // + ROTATION with UNIT scale — NOT smr.transform.localToWorldMatrix. This Hyper3D/Mixamo FBX
                // bakes a 100× cm→m node scale onto the SkinnedMeshRenderer's OWN transform ("model" node,
                // probe-verified localScale=(100,100,100)). smr.localToWorldMatrix carries that 100× (×1.8 root
                // = 180×), and BakeMesh(false) verts are already in the mesh's authored metres space, so
                // multiplying by l2w DOUBLE-APPLIES the 100× and blows the world Y to ~±280 (the ±68 runaway
                // that drove the snap divergence — sole-probe.log: formula (1) bake(false)+smr.l2w gave 4.45
                // vs GT 4.85 at root-Y 5, and exploded to 282 at root-Y 0). A unit-scale TRS matches the
                // rendered bounds floor (sole-probe formula 4 = 4.9970 ≈ GT). character-pipeline.md §cm→m trap.
                Matrix4x4 l2w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int v = 0; v < _bakeVerts.Count; v++)
                {
                    float y = l2w.MultiplyPoint3x4(_bakeVerts[v]).y;
                    if (y < minY) minY = y;
                }
                any = true;
            }
            return any ? minY : float.NaN;
        }

        /// <summary>The CONSERVATIVE SMR.bounds.min.y across all child SMRs (the OLD proxy — the
        /// animation-max AABB floor). Kept ONLY so the [FloatTrace] dump can show it side-by-side with the
        /// baked actual sole, proving the bounds floor sits below the real soles (the deeper false-green).
        /// NOT used for snapping or the gauge anymore.</summary>
        private float MeasureSmrBoundsMinY()
        {
            if (_smrs == null || _smrs.Length == 0)
                _smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_smrs == null || _smrs.Length == 0) return float.NaN;
            float minY = float.PositiveInfinity;
            bool any = false;
            for (int i = 0; i < _smrs.Length; i++)
            {
                if (_smrs[i] == null) continue;
                float y = _smrs[i].bounds.min.y;
                if (y < minY) minY = y;
                any = true;
            }
            return any ? minY : float.NaN;
        }

        // Snap the avatar feet to the VISIBLE terrain (86ca8rdkp soak-fix #1; RE-SOAK fix — "he STILL walks
        // elevated"). The NavMeshAgent grounds the player ROOT on the flat NavMesh collider; the FBX-origin
        // feet ride that root. We raycast the Ground layer down and plant the feet on the VISIBLE-terrain hit.
        //
        // RE-SOAK ROOT CAUSE (ground-trace 2026-06-15, -groundTrace; OVERTURNED the "snap is enough" assumption):
        // the PR #47 snap used a SINGLE Physics.Raycast and took the TOPMOST/closest Ground hit, assuming the
        // visible terrain is always the highest surface. That holds INLAND (the meadow rises above the slab) —
        // but on the SEAWARD FORESHORE the Zone-D terrain DIPS BELOW the flat NavMesh slab (Y=0): trace measured
        // the visible sand at −0.02 … −0.43 while TestGround stayed at 0.000, and the single ray returned the
        // SLAB (the higher surface). So walking shoreward the feet stayed pinned at Y≈0 while the visible sand
        // dropped away — a growing ~0.43u float = the Sponsor's "he still walks elevated" (worst toward the
        // shore/campfire). Idle-at-spawn looked fine because there the sand IS the topmost hit.
        //
        // FIX: RaycastAll and select the VISIBLE terrain — the hit whose collider carries a renderer-ENABLED
        // MeshRenderer. The TestGround slab is a collision/NavMesh PROXY with its renderer DISABLED (the grey-
        // slab soak-fix, unity-conventions.md §NavMesh) — so "renderer enabled" cleanly distinguishes the sand
        // the player SEES from the invisible slab. Among visible Ground hits, take the HIGHEST (defensive — a
        // single visible terrain is expected). Runs in both Idle and Walk (the float is in BOTH; motion just
        // makes it obvious). Smoothed to avoid popping on slopes.
        private void ApplyGroundSnap()
        {
            if (!groundSnap) return;
            Transform root = transform.parent != null ? transform.parent : transform; // the player root
            int mask = groundMask.value != 0 ? groundMask.value : (1 << LayerMask.NameToLayer("Ground"));
            Vector3 origin = root.position + Vector3.up * groundRayUp;
            float maxDist = groundRayUp + groundRayDown;

            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _snapHits, maxDist, mask,
                                            QueryTriggerInteraction.Ignore);
            // Pick the VISIBLE terrain: the highest hit whose collider has a renderer-ENABLED MeshRenderer
            // (skips the renderer-disabled flat NavMesh slab — the bug the re-soak trace exposed).
            bool found = false;
            float bestY = 0f;
            for (int i = 0; i < n; i++)
            {
                var mr = _snapHits[i].collider.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;          // skip the invisible collision-proxy slab
                float y = _snapHits[i].point.y;
                if (!found || y > bestY) { bestY = y; found = true; }
            }
            // Fallback: if NO visible Ground was hit (e.g. a scene with only the proxy slab — the PlayMode
            // rigs, or an inland spot where the proxy is the only collider), use the topmost hit so the snap
            // still grounds rather than going inert.
            if (!found && n > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    float y = _snapHits[i].point.y;
                    if (!found || y > bestY) { bestY = y; found = true; }
                }
            }
            if (!found)
            {
                // No visible ground under the feet — diagnostic readouts go invalid (the overlay shows N/A).
                LastSnapTargetWorldY = float.NaN;
                GroundHitWorldY = float.NaN;
                FloatGap = float.NaN;
                FeetWorldY = transform.position.y;
                return;
            }
            // The RAW visible-ground hit (pre-offset) — the surface the player SEES under his feet. The live
            // FLOAT GAP is measured against THIS, so the Sponsor watches the gap to the actual sand (not the
            // offset-shifted plant target). 86ca8rdkp diagnostic.
            GroundHitWorldY = bestY;
            // The plant Y = the visible-terrain hit + the Sponsor-dialable ground offset (default 0 = plant
            // exactly on the geometric ground). The OFFSET is the F9-dialable knob (4th-attempt — give the
            // Sponsor the handle instead of the team guessing the exact feet-on-ground value).
            float plantY = bestY + groundYOffset;
            LastSnapTargetWorldY = plantY; // the SELECTED plant surface (pre-smoothing) — the snap-target the test reads

            // ===== THE SOURCE FIX (86ca8rdkp attempt-8 — ROOT-GROUND with a FIXED K; abandon per-frame mesh
            // chasing). The ~6-iteration float saga + the ±68 runaway all came from CHASING the rendered sole
            // each frame: snapping to SMR.bounds.min.y (anim-max AABB, wobbles), then to a per-frame BakeMesh
            // sole whose world matrix DOUBLE-APPLIED the FBX's intrinsic 100× cm→m node scale (scale-trace.log:
            // the "model" SMR node is localScale=100; BakeMesh+smr.l2w blew world Y to 282 → the snap drove
            // avatarRootY to ~−68 in a false-green equilibrium while the character left frame).
            //
            // ROOT CAUSE (probe-MEASURED, not hypothesized): the 100× node is INTRINSIC to the FBX hierarchy —
            // no importer flag removes it (useFileScale=false → 218u tall + still 100× node; bakeAxisConversion
            // → still 100× node; both refuted by fix-probe). The mesh RENDERS correct (Unity skinning handles
            // the node scale via bone matrices) — ONLY world-space BakeMesh measurements explode. So we STOP
            // measuring the sole to ground it: the FBX origin is AT THE FEET (BuildModel sets the model child
            // localPos=0; the bind sole sits ≈ the avatar root — sole-probe: bind sole 4.997 ≈ root 5.000), and
            // the Idle/Walk clips are imported IN-PLACE (lockRootPositionXZ, root-motion off), so the root-to-
            // sole offset is a FIXED CONSTANT, not pose-dependent. Ground the ROOT directly:
            //     avatarRoot.worldY = groundHit + groundYOffset(K)
            // K is the small Sponsor-dialable constant (default 0 = feet on the geometric ground). NO BakeMesh
            // in the snap path → no 100× exposure → no runaway. The BakeMesh sole is now read ONLY for the F8
            // gauge (a sane, scale-immune honest readout — MeasureRenderedSoleWorldY uses a unit-scale matrix).
            float bakedSoleNow = MeasureRenderedSoleWorldY(); // gauge-only (scale-immune); NOT used to snap
            float boundsMinNow = MeasureSmrBoundsMinY();      // dump-only (prove bounds floor diverges)

            // Desired avatar-root WORLD Y = the plant target (groundHit + K). The avatar root is a direct child
            // of the player root, so localY = worldY − root.position.y. NO pose-offset subtraction — grounding
            // the root, not the chased sole, is what kills the divergence.
            float desiredLocalY = plantY - root.position.y;

            bool moving = _agent != null &&
                          new Vector2(_agent.velocity.x, _agent.velocity.z).sqrMagnitude >
                          (walkSpeedThreshold * walkSpeedThreshold);
            IsMovingForSnap = moving;          // diagnostic: rest-vs-move
            ActiveSnapRate = snapRate;         // diagnostic: the single unified rate now in play

            // KILL-THE-BOB (86ca8rdkp — one UNIFIED rate). A speed-adaptive split (fast moving / slow at rest)
            // made the convergence rate JUMP at the moving↔rest transition → the still-converging error visibly
            // settled the instant the agent stopped = the arrival BOB. The snap target is now the STABLE plant
            // target (groundHit + K — a smooth function of terrain only, no animation-envelope wobble, no mesh
            // chasing), so a SINGLE unified rate tracks the descending foreshore tightly while walking AND
            // doesn't pop at rest — no rate discontinuity at arrival, no bob. (snapRateMove/snapRateRest retained
            // as [HideInInspector] serialized fields for scene back-compat; snapRate is the live one.)
            if (!_snapInit) { _snapLocalY = desiredLocalY; _snapInit = true; }
            else _snapLocalY = Mathf.Lerp(_snapLocalY, desiredLocalY, 1f - Mathf.Exp(-snapRate * Time.deltaTime));
            Vector3 lp = transform.localPosition;
            lp.y = _snapLocalY;
            transform.localPosition = lp;

            // ===== THE WALK-FLOAT FIX (86ca8rdkp attempt-9 — diagnose-via-trace OVERTURNED e1289ef's premise).
            // e1289ef grounded the avatar ROOT to plantY (proxyRootGap≈0 — root correctly grounded) AND assumed
            // the rendered sole rides the root because "the clips are in-place". The ClipBaselineDiagnose (scale-
            // immune baked sole, avatarRoot@0) DISPROVED that: the IDLE clip plants the sole at root-relative
            // -0.003 (feet on the root, good), but the WALK clip plants it at +0.63..+0.69 — the Mixamo Walk
            // clip's HIPS/body are authored ~0.66u HIGHER than Idle's, lifting the WHOLE rendered mesh while
            // walking even though the root is grounded. That is the Sponsor's "hovering above the sand while
            // mid-stride". It is NOT a scale/snap/shadow bug, and it is NOT fixable via clip import flags
            // (lockRootHeightY/heightFromFeet govern ROOT-MOTION extraction; applyRootMotion=false samples the
            // mesh IN-PLACE from the raw bone curves — PROVEN: re-importing with those flags left WALK sole at
            // +0.66 unchanged). The lift lives in the BONE pose, per-clip.
            //
            // FIX: ground the VISIBLE rendered SOLE (not the proxy root) by offsetting the MODEL CHILD's local-Y
            // so the scale-immune baked sole sits at the grounded plant level. This works for BOTH clips with no
            // per-clip constant: Idle's residual is ~0 (no offset), Walk's residual is ~+0.66 (model pushed down).
            // It is the brief's prescribed "ground the actual VISIBLE mesh-bottom measured SCALE-IMMUNELY"
            // approach — and it is NOT the ±68 runaway: MeasureRenderedSoleWorldY uses a UNIT-SCALE TRS (never
            // smr.localToWorldMatrix), so the FBX 100× node is never double-applied (shipped [FloatTrace] read a
            // sane +0.66, not ±68). The model child carries only a yaw (Y-axis) rotation, so a local-Y offset is
            // orientation-independent. Convergence uses modelSoleGroundRate (HIGHER than the root snapRate) —
            // this cancels a deterministic MEASURED per-clip-frame lift, so it must track tightly (plant every
            // stride frame + converge the Idle→Walk +0.66 step in a few frames), unlike the root snap which
            // smooths noisy terrain.
            if (_model != null && modelSoleGround)
            {
                float soleBeforeModelOffset = MeasureRenderedSoleWorldY();
                if (!float.IsNaN(soleBeforeModelOffset))
                {
                    // The local-Y the model child needs so its scale-immune rendered sole lands on plantY. Pure
                    // static (ComputeModelGroundLocalY) so the EditMode walk-grounding guard asserts THIS math
                    // against a real SampleAnimation'd WALK clip (the bug class) without a play loop.
                    float desiredModelLocalY = ComputeModelGroundLocalY(
                        soleBeforeModelOffset, plantY, _model.localPosition.y, transform.lossyScale.y);
                    if (!_modelGroundInit) { _modelLocalY = desiredModelLocalY; _modelGroundInit = true; }
                    else _modelLocalY = Mathf.Lerp(_modelLocalY, desiredModelLocalY,
                                                   1f - Mathf.Exp(-modelSoleGroundRate * Time.deltaTime));
                    Vector3 mlp = _model.localPosition;
                    mlp.y = _modelLocalY;
                    _model.localPosition = mlp;
                }
            }

            // LIVE FLOAT-DIAGNOSTIC — the gauge reads the HONEST gap: SCALE-IMMUNE baked sole − ground. GAP≈0 ⟺
            // the visible SOLES are on the visible sand. The sole is now measured with a UNIT-SCALE world matrix
            // (MeasureRenderedSoleWorldY) so the FBX 100× node never blows it up — the gauge reads a sane sole-Y,
            // not the ±68 garbage the old smr.l2w path produced. Re-measure AFTER applying this frame's snap so
            // the gauge reflects the corrected position. (Snap grounds the ROOT to groundHit+K; since the bind
            // sole ≈ the root and the clips are in-place, the sole lands on the sand too — verified by the WALK
            // [FloatTrace]: GAP stays bounded standing + walking + at arrival.)
            MeshBottomWorldY = MeasureRenderedSoleWorldY();
            SmrBoundsMinWorldY = boundsMinNow;       // dump-only proxy for the [FloatTrace] discrepancy line
            MeshFloatGap = float.IsNaN(MeshBottomWorldY) ? float.NaN
                                                         : ComputeFloatGap(MeshBottomWorldY, GroundHitWorldY);
            // FeetWorldY is the gauge's "feet" line — the TRUE baked sole (falls back to the avatar-root world Y
            // only if no SMR is resolvable). FloatGap is the HONEST sole-vs-sand gap the overlay shows.
            FeetWorldY = float.IsNaN(MeshBottomWorldY) ? transform.position.y : MeshBottomWorldY;
            FloatGap = float.IsNaN(MeshFloatGap) ? ComputeFloatGap(transform.position.y, GroundHitWorldY)
                                                 : MeshFloatGap;
            // The proxy avatar-root gap (kept for the regression guard + dump — proves the gauge reads the sole).
            ProxyRootFloatGap = ComputeFloatGap(transform.position.y, GroundHitWorldY);

            // PER-FRAME [FloatTrace] (86ca8rdkp EXTENSIVE-DEBUG round). The Sponsor demanded extensive logging;
            // this dumps EVERY Y-reference EACH FRAME (gated on _frameTrace = -floatTrace OR the F8 overlay) so
            // the orchestrator reads the discrepancy (bounds.min vs baked-actual vs shadow vs ground) standing,
            // walking, AND at arrival from the player log. Throttled to once per render frame (not per physics).
            if (_frameTrace) EmitFrameTrace(root, bakedSoleNow, boundsMinNow, plantY, moving);

            // CONTACT SHADOW — ground it to the VISIBLE-SAND CONTACT LEVEL (the plant target). The snap grounds
            // the avatar ROOT to plantY (= groundHit + K), and the bind feet sit at the root, so plantY IS the
            // contact level the feet touch. Planting the shadow at plantY tracks the sand the feet touch in
            // motion AND at rest — the shadow is a child of the player root and would otherwise stay at a fixed
            // Y and strand above/below the snapped feet on the dipping foreshore (the 'floats above its shadow'
            // percept from earlier soaks). No pose coupling, no mesh chasing.
            if (blobShadow != null)
            {
                Vector3 sp = blobShadow.position;
                sp.y = plantY + blobShadowLift;
                blobShadow.position = sp;
            }
        }

        // Per-frame [FloatTrace] gate (86ca8rdkp EXTENSIVE-DEBUG round). The Sponsor EXPLICITLY demanded
        // extensive per-frame logging. Driven ON by either the -floatTrace launch arg (an orchestrator-driven
        // log-only capture) OR FloatDiagnostic toggling the F8 overlay (so the interactive soak also logs).
        // FloatDiagnostic sets this via SetFrameTrace when its overlay/-floatTrace activates.
        private bool _frameTrace;
        private int _traceFrame;

        /// <summary>FloatDiagnostic (or the verify capture) turns per-frame [FloatTrace] on/off — so the
        /// extensive log fires every frame while the overlay is up or -floatTrace is set, and is silent in a
        /// normal soak (zero log spam). Idempotent.</summary>
        public void SetFrameTrace(bool on) => _frameTrace = on;

        // Cache the foot bones + shadow once (avoids a per-frame bone scan inside the trace).
        private Transform _leftFootBone, _rightFootBone, _hipsBone;
        private bool _bonesResolved;

        private void ResolveBones()
        {
            _bonesResolved = true;
            if (_smrs == null || _smrs.Length == 0) _smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_smrs == null) return;
            foreach (var smr in _smrs)
            {
                if (smr == null || smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b == null) continue;
                    string n = b.name.ToLowerInvariant();
                    if (_leftFootBone == null && (n.EndsWith("leftfoot") || n.EndsWith("lefttoebase") ||
                                                  n.EndsWith("lefttoe_end"))) _leftFootBone = b;
                    else if (_rightFootBone == null && (n.EndsWith("rightfoot") || n.EndsWith("righttoebase") ||
                                                        n.EndsWith("righttoe_end"))) _rightFootBone = b;
                    else if (_hipsBone == null && (n.EndsWith("hips") || n == "mixamorig:hips")) _hipsBone = b;
                }
                if (_leftFootBone != null && _rightFootBone != null) break;
            }
        }

        // The EXTENSIVE per-frame [FloatTrace] line (86ca8rdkp). Dumps ALL the references the Sponsor + the
        // orchestrator asked for in ONE greppable line each frame: root.y / SMR.bounds.min.y (the OLD proxy) /
        // the BAKED actual-lowest-vertex (the TRUE sole) / L+R foot-bone world-Y / ground raycast hit-Y /
        // blob-shadow world-Y / the snap delta applied / moving-state / snap-rate. The DISCREPANCY the whole
        // saga turned on (bounds.min BELOW the baked sole while the gauge agreed) is the load-bearing field.
        private void EmitFrameTrace(Transform root, float bakedSole, float boundsMin, float plantY, bool moving)
        {
            if (!_bonesResolved) ResolveBones();
            float lFootY = _leftFootBone != null ? _leftFootBone.position.y : float.NaN;
            float rFootY = _rightFootBone != null ? _rightFootBone.position.y : float.NaN;
            float shadowY = blobShadow != null ? blobShadow.position.y : float.NaN;
            float boundsVsBaked = (float.IsNaN(boundsMin) || float.IsNaN(bakedSole)) ? float.NaN : (bakedSole - boundsMin);
            _traceFrame++;
            Debug.Log(
                $"[FloatTrace] f={_traceFrame} rootY={root.position.y:F4} avatarRootY={transform.position.y:F4} " +
                $"boundsMinY(OLD proxy)={Fmt4(boundsMin)} BAKED_SOLE(TRUE)={Fmt4(bakedSole)} " +
                $"boundsBelowSoleBy={Fmt4(boundsVsBaked)} lFootBoneY={Fmt4(lFootY)} rFootBoneY={Fmt4(rFootY)} " +
                $"groundHitY={Fmt4(GroundHitWorldY)} plantY={Fmt4(plantY)} shadowY={Fmt4(shadowY)} " +
                $"snapLocalY={_snapLocalY:F4} GAP(sole-ground)={Fmt4(FloatGap)} " +
                $"proxyRootGap={Fmt4(ProxyRootFloatGap)} offset={groundYOffset:F4} moving={moving} rate={ActiveSnapRate:F0}");
        }

        private static string Fmt4(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");

        /// <summary>
        /// ONE-FRAME GROUND-TRUTH DUMP (86ca8rdkp diagnostic — diagnostic-traces-before-fixes). Dumps EVERY
        /// Y-reference in a single frame so the hidden offset between the OLD bounds.min.y proxy and the BAKED
        /// actual-lowest-vertex TRUE sole is MEASURED, not guessed. Called by FloatDiagnosticVerifyCapture at
        /// spawn + shore so the [FloatTrace] log carries the full picture. Pure read-only (never mutates state).
        /// </summary>
        public string DumpGroundTruth(string where)
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            float bakedSole = MeasureRenderedSoleWorldY();
            float boundsMin = MeasureSmrBoundsMinY();
            if (!_bonesResolved) ResolveBones();
            string hips = _hipsBone != null ? _hipsBone.position.y.ToString("F4") : "N/A";
            string lFoot = _leftFootBone != null ? _leftFootBone.position.y.ToString("F4") : "N/A";
            string rFoot = _rightFootBone != null ? _rightFootBone.position.y.ToString("F4") : "N/A";
            string modelLocalY = _model != null ? _model.localPosition.y.ToString("F4") : "N/A";
            string shadowY = blobShadow != null ? blobShadow.position.y.ToString("F4") : "N/A";
            float boundsBelowSoleBy = (float.IsNaN(boundsMin) || float.IsNaN(bakedSole)) ? float.NaN
                                                                                         : (bakedSole - boundsMin);
            string msg =
                $"[FloatTrace] DUMP ({where}): rootY={root.position.y:F4} avatarRootY(gauge feetY)={transform.position.y:F4} " +
                $"hipsY={hips} leftFootBoneY={lFoot} rightFootBoneY={rFoot} " +
                $"BOUNDS_MIN_Y(OLD proxy=anim-max AABB floor)={Fmt4(boundsMin)} " +
                $"BAKED_SOLE_Y(TRUE current-frame lowest vertex)={Fmt4(bakedSole)} " +
                $"boundsBelowTrueSoleBy={Fmt4(boundsBelowSoleBy)} " +
                $"modelChildLocalY={modelLocalY} groundHitY={Fmt4(GroundHitWorldY)} " +
                $"shadowY={shadowY} | PROXY_ROOT_GAP(avatarRoot-ground)={Fmt4(ProxyRootFloatGap)} " +
                $"TRUE_GAP(bakedSole-ground)={Fmt4(MeshFloatGap)} " +
                $"poseOffset(bakedSole-avatarRoot)={Fmt4(float.IsNaN(bakedSole) ? float.NaN : bakedSole - transform.position.y)} " +
                $"snapLocalY={_snapLocalY:F4} groundYOffset={groundYOffset:F4} moving={IsMovingForSnap}";
            Debug.Log(msg);
            return msg;
        }

        // Advance the ballistic jump arc one frame (ticket 86ca9yq3q). Integrates v -= g·dt; y += v·dt, writes
        // the avatar-root local-Y = the lift-off grounded baseline + the arc height (so the WHOLE character —
        // and the held axe riding the hand, which inherits this transform — rises + falls), and LANDS when the
        // arc returns to (or below) the baseline. On landing the snap baseline is restored from _jumpBaseLocalY
        // so the next grounded frame resumes the snap with NO pop. The XZ stays owned by the agent throughout
        // (the player keeps moving while airborne — AC1's "jump while moving"). NO ground raycast / modelSoleGround
        // runs here — that's the AC3 suspension; the grounded path (ApplyGroundSnap) is untouched.
        private void AdvanceJump()
        {
            float dt = Time.deltaTime;
            // Headless guard: Time.deltaTime≈0 in -batchmode (the documented headless-time trap) would freeze the
            // arc forever (never landing) → a hung PlayMode test. Use a nominal step so the arc still advances
            // deterministically in headless runs; live play uses the real dt.
            if (dt < 1e-5f) dt = 1f / 60f;

            _jumpVelY -= jumpGravity * dt;
            _jumpY += _jumpVelY * dt;

            if (_jumpY <= 0f)
            {
                // LANDED — clamp to the ground, re-engage the snap from the grounded baseline, return control.
                _jumpY = 0f;
                _airborne = false;
                _jumpVelY = 0f;
                _snapLocalY = _jumpBaseLocalY;     // resume the snap exactly where it lifted off (no pop)
                Vector3 landed = transform.localPosition;
                landed.y = _jumpBaseLocalY;
                transform.localPosition = landed;
                return;
            }

            Vector3 lp = transform.localPosition;
            lp.y = _jumpBaseLocalY + _jumpY;       // grounded baseline + the arc height
            transform.localPosition = lp;
        }

        void LateUpdate()
        {
            // JUMP ↔ GROUND-SNAP GATE (ticket 86ca9yq3q, AC3 — load-bearing). While AIRBORNE the per-frame
            // ground snap is SUSPENDED: ApplyGroundSnap drives the avatar root (and modelSoleGround the model
            // child) onto the terrain every frame, which would PIN the feet back to the ground mid-jump (the
            // 9-attempt float fix is doing its job — grounding the feet — and that's exactly wrong while jumping).
            // So while airborne we run the arc instead; on landing we re-engage the snap with the grounded
            // behavior UNCHANGED (the gate is an "only-when-grounded" wrapper — it does NOT alter ApplyGroundSnap).
            if (_airborne)
                AdvanceJump();
            else
                // Plant the feet on the VISIBLE terrain FIRST (before facing), so the grounded position is
                // settled this frame (soak-fix #1 — 'walking in the air'). The avatar root's local Y is driven
                // down onto the visible sand the agent's NavMesh ground point floats above. (UNCHANGED grounded
                // behavior — the AC5 regression guard asserts this path is byte-identical to pre-jump.)
                ApplyGroundSnap();

            // Read the agent's planar velocity each frame and drive the Idle<->locomotion blend + the
            // Walk<->Run blend tree (86ca9yq34) + facing. Self-driving keeps this PR's surface to the visual
            // only (no ClickToMove/WasdMovement edit beyond reading the velocity they already command).
            Vector3 vel = _agent != null ? _agent.velocity : Vector3.zero;
            vel.y = 0f;
            float planarSpeed = vel.magnitude;
            bool walking = vel.sqrMagnitude > (walkSpeedThreshold * walkSpeedThreshold);
            IsWalking = walking;
            // The Speed param fed to the 1D Walk<->Run blend tree = the planar agent speed (u/s). WASD drives
            // the agent at moveSpeed walking / runSpeed sprinting, so this magnitude lands in the blend tree's
            // Walk band or Run band accordingly — the Run clip blends in only while actually running fast.
            CurrentSpeed = planarSpeed;
            // RUNNING: at/above the run threshold (the blend reads as a full Run here). 86caa83wn fix 2 —
            // the held-axe RUN clamp gates on IsRunning, and it "wasn't biting": the NavMeshAgent's SIMULATED
            // velocity (what we read) LAGS the COMMANDED run speed (== runSpeedThreshold) and dips just below
            // it on accel/decel ramps + obstacle steering, so a strict ">= threshold" compare FLICKERED false
            // mid-run → the clamp disengaged for those frames → the axe popped into the head. So engage running
            // at a MARGIN below the threshold (RunEngageFraction · threshold). The margin stays well ABOVE the
            // walk speed (5.5 vs ~8.1 at 0.85·9.5), so a WALK never reads as running (the AC5 walk-band test +
            // the blend tree are unaffected — the blend tree reads the Speed param, not this flag), while a RUN
            // reads running through the whole cycle so the clamp stays engaged. The visual Walk<->Run blend is
            // unchanged (driven by SpeedParam above, not by IsRunning).
            IsRunning = walking && planarSpeed >= runSpeedThreshold * RunEngageFraction;
            if (walking) _lastFacing = vel.normalized;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetBool(MovingParam, walking);
                // 86caay44r — DAMP the Speed param so the blend tree EASES rather than snaps on start/stop/
                // speed-change (the Sponsor's "transitions too abrupt, most on releasing W"). SetFloat's built-in
                // damp smooths the value toward planarSpeed over ~speedDampTime, frame-rate-independent. A 0
                // dampTime falls back to a raw instant set (the old behavior) — so the feel is fully tunable from
                // the build. The Walk<->Run blend (#68) is UNTOUCHED: it still reads this same Speed param; the
                // damp only makes its crossfade smoother, never selects a different clip.
                if (speedDampTime > 0.0001f)
                    _animator.SetFloat(SpeedParam, planarSpeed, speedDampTime, Time.deltaTime);
                else
                    _animator.SetFloat(SpeedParam, planarSpeed);
                // GROUNDED (86ca9yq3q rework — THE floating-bug fix). The airborne gate above already updated
                // _airborne for THIS frame (AdvanceJump flips it false on the landing frame). Driving Grounded =
                // !_airborne here lets the controller's jump→{Locomotion if Moving | Idle} transition fire on the
                // SAME landing frame: if W is still held (walking true) the Walk/Run blend resumes immediately
                // instead of the character stalling in the finished jump pose while still translating (the Sponsor's
                // "floating" report). Moving (set just above) is current, so a moving landing routes to Locomotion.
                _animator.SetBool(GroundedParam, !_airborne);
            }

            // Yaw the model smoothly toward the travel facing (frame-rate-independent lerp).
            Vector3 face = _lastFacing; face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                float target = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                _bodyYaw = Mathf.LerpAngle(_bodyYaw, target, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));
            }
            if (_model != null)
                _model.localRotation = Quaternion.Euler(0f, _bodyYaw, 0f);

            // RUNTIME JUMP-TRACE (86caaqhj5) — emit AFTER this frame's arc/snap + anim update so the trace
            // reflects the final per-frame positions the player sees. Auto-fires on every jump, silent otherwise.
            if (_jumpTraceActive) EmitJumpTrace();
        }

        // The per-frame RUNTIME jump-trace line (86caaqhj5 — the "pulled back on landing" RE-DIAGNOSIS). Fires
        // every frame from lift-off (TryJump opened the window) until JumpTracePostLandSeconds past touch-down.
        // ONE greppable [JumpTrace] Debug.Log per frame inside the window — no toggle, no launch-arg: the Sponsor
        // just plays + jumps W/A/S/D and the orchestrator reads Player.log. Silent in every non-jump frame.
        //
        // The LANDING frame is marked explicitly (LANDED=YES on the touch-down frame). The load-bearing fields:
        //   rootΔXZ      — player-root world XZ moved since lift-off; a NEGATIVE component along travel = the
        //                  "pulled BACK in the world" the Sponsor reports (the agent sim displaced the root).
        //   agentVel     — what WasdMovement commands (coast/steer airborne; full speed grounded).
        //   desiredVel   — the agent's OWN steering target (≈0 under WASD — no path; autoBraking decelerates the
        //                  body toward THIS, fighting the commanded velocity — the prime suspect for the decel).
        //   nextPos      — where the agent's internal sim wants the root NEXT frame (updatePosition=true owns it).
        //   childΔ vs root — the avatar child rides root XZ + a local-Y arc; if root advances but child snaps
        //                  back, the displacement is in the child (candidate b); if root itself retreats, it's the
        //                  agent sim (candidate a/c).
        private void EmitJumpTrace()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            Vector3 rootPos = root.position;
            Vector3 rootXZ = new Vector3(rootPos.x, 0f, rootPos.z);
            Vector3 dXZ = rootXZ - _jumpTraceLaunchRootXZ;        // root world XZ moved since lift-off
            Vector3 childPos = transform.position;                // the avatar child (jump arc local-Y lives here)

            Vector3 agentVel = _agent != null ? _agent.velocity : Vector3.zero;
            Vector3 desiredVel = _agent != null ? _agent.desiredVelocity : Vector3.zero;
            Vector3 nextPos = _agent != null ? _agent.nextPosition : Vector3.zero;
            bool hasPath = _agent != null && _agent.hasPath;
            bool pathPending = _agent != null && _agent.pathPending;
            bool onMesh = _agent != null && _agent.isOnNavMesh;

            // VISUAL-vs-ENTITY divergence (Sponsor root-motion hypothesis, 86caaqhj5): the HIPS BONE world pos is
            // the VISUAL avatar's body — a Mixamo "jump forward" clip lunges the hips FORWARD off the root pivot
            // even though the clip is imported lockRootPositionXZ=true + applyRootMotion=false (those neutralize
            // ROOT-NODE motion, NOT the bone-pose lunge). If hipsXZ diverges FORWARD from the root during the air
            // and SNAPS BACK to the root on landing, the "pulled back" is a bone-pose forward-lunge (visual ≠
            // entity), confirming the Sponsor's hypothesis at the BONE level. We also log applyRootMotion to prove
            // root motion is OFF (so any divergence is bone-pose, not extracted root motion).
            if (!_bonesResolved) ResolveBones();
            Vector3 hipsPos = _hipsBone != null ? _hipsBone.position : new Vector3(float.NaN, float.NaN, float.NaN);
            // hips world XZ relative to the root XZ — the VISUAL forward-lunge magnitude (the load-bearing number).
            Vector3 hipsRelRootXZ = _hipsBone != null
                ? new Vector3(hipsPos.x - rootPos.x, 0f, hipsPos.z - rootPos.z)
                : new Vector3(float.NaN, float.NaN, float.NaN);
            bool applyRootMotion = _animator != null && _animator.applyRootMotion;
            string animState = "n/a";
            if (_animator != null && _animator.runtimeAnimatorController != null && _animator.layerCount > 0)
            {
                var st = _animator.GetCurrentAnimatorStateInfo(0);
                animState = $"hash={st.shortNameHash} t={st.normalizedTime:F2}";
            }

            // Detect + mark the exact LANDING frame: _airborne is false this frame but a post-land countdown is
            // still running. (TryJump set the window; AdvanceJump flipped _airborne false on touch-down.)
            bool landedThisFrame = !_airborne && _jumpTracePostLandT == JumpTracePostLandSeconds;
            string camPath = _airborne ? "AIRBORNE(tight-follow,no-lead)" : "GROUNDED(lead)";

            _jumpTraceFrame++;
            Debug.Log(
                $"[JumpTrace] f={_jumpTraceFrame} airborne={_airborne} LANDED={(landedThisFrame ? "YES" : "no")} " +
                $"jumpY={Fmt4(JumpHeight)} jumpVelY={_jumpVelY:F3} " +
                $"rootXZ=({rootPos.x:F3},{rootPos.z:F3}) rootY={rootPos.y:F3} " +
                $"rootDeltaXZ=({dXZ.x:F4},{dXZ.z:F4}) rootDeltaMag={new Vector2(dXZ.x, dXZ.z).magnitude:F4} " +
                $"childXZ=({childPos.x:F3},{childPos.z:F3}) childY={childPos.y:F3} childLocalY={transform.localPosition.y:F4} " +
                $"hipsXZ=({Fmt4(hipsPos.x)},{Fmt4(hipsPos.z)}) hipsY={Fmt4(hipsPos.y)} " +
                $"hipsRelRootXZ=({Fmt4(hipsRelRootXZ.x)},{Fmt4(hipsRelRootXZ.z)}) " +
                $"hipsLungeMag={Fmt4(new Vector2(hipsRelRootXZ.x, hipsRelRootXZ.z).magnitude)} " +
                $"applyRootMotion={applyRootMotion} animState={animState} " +
                $"agentVel=({agentVel.x:F3},{agentVel.z:F3}) agentSpeed={new Vector2(agentVel.x, agentVel.z).magnitude:F3} " +
                $"desiredVel=({desiredVel.x:F3},{desiredVel.z:F3}) desiredSpeed={new Vector2(desiredVel.x, desiredVel.z).magnitude:F3} " +
                $"nextPos=({nextPos.x:F3},{nextPos.z:F3}) hasPath={hasPath} pathPending={pathPending} onMesh={onMesh} " +
                $"cam={camPath}");

            // Keep the window open through the post-landing tail, then close it (silent again until the next jump).
            if (!_airborne)
            {
                _jumpTracePostLandT -= Mathf.Max(Time.deltaTime, 1f / 240f);
                if (_jumpTracePostLandT <= 0f) _jumpTraceActive = false;
            }
        }
    }
}
