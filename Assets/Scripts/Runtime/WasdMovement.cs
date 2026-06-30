using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// WASD keyboard locomotion (ticket 86ca9yq2x) — REPLACES the PoE-style click-to-move as the
    /// player's locomotion (a Sponsor-directed pivot; CLAUDE.md core-feel + DECISIONS record it).
    ///
    /// CAMERA-RELATIVE (AC1): W moves the character in the direction the orbit camera FACES (its
    /// forward projected onto the ground plane), S back, A/D strafe left/right — standard
    /// third-person. The input vector is rotated into the camera's planar basis each frame, so
    /// "forward" tracks wherever the Sponsor has orbited the camera to.
    ///
    /// DRIVES THE EXISTING NavMeshAgent (AC3 — least-disruption): rather than swapping to a raw
    /// CharacterController, this sets the player's <see cref="NavMeshAgent.velocity"/> to the
    /// camera-relative desired velocity each frame. The agent's own simulation keeps the player ON
    /// the baked NavMesh (terrain grounding + obstacle handling + the float fix all intact —
    /// CastawayCharacter.modelSoleGround is UNTOUCHED), and <see cref="NavMeshAgent.velocity"/> is
    /// exactly the quantity CastawayCharacter already reads each frame to (a) flip the Idle&lt;-&gt;Walk
    /// Animator blend (AC4) and (b) yaw the model toward travel (AC2). So the anim blend + facing
    /// fall out of driving the agent velocity — no new coupling to CastawayCharacter.
    ///
    /// CLICK-TO-MOVE OFF (AC3): the scene author (MovementCameraScene) sets ClickToMove.clickEnabled
    /// = false when this component is wired, so a left-click no longer sets a destination. ClickToMove
    /// stays in the scene (its programmatic MoveTo seam is still used by the verify captures + the
    /// PlayMode harness), but it no longer drives gameplay locomotion. On the first frame WASD takes
    /// over, any leftover click path is cleared (ResetPath) so it can't fight the manual velocity.
    ///
    /// NO mutable runtime statics here (only instance + serialized fields), so the
    /// Configurable-Enter-Play-Mode static-reset audit (StaticStateResetTests) stays green with no
    /// [RuntimeInitializeOnLoadMethod] reset needed (the #61 discipline — applies only if a static is added).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class WasdMovement : MonoBehaviour
    {
        [Header("Speed")]
        [Tooltip("Walk speed (u/s) the WASD input drives. Defaults to the agent's own speed if left 0.")]
        public float moveSpeed = 5.5f;

        [Tooltip("RUN speed (u/s) while Sprint (Shift) is held AND moving (ticket 86ca9yq34). Faster than " +
                 "moveSpeed so holding Shift visibly runs. CastawayCharacter reads the resulting agent.velocity " +
                 "MAGNITUDE to drive the Walk<->Run blend tree, so a faster run speed → the Run clip blends in. " +
                 "Defaults ~1.8× the walk speed (a clear run). The Sponsor can re-tune from the build.")]
        public float runSpeed = 9.5f;

        [Tooltip("SNEAK speed (u/s) while Crouch (Ctrl) is held AND moving (ticket 86caa3kur). SLOWER than " +
                 "moveSpeed so a crouched move visibly sneaks (the Sneak Walk crouch-move clip). CastawayCharacter " +
                 "reads the resulting agent.velocity MAGNITUDE — at this reduced speed it lands in the upright " +
                 "blend tree's WALK band, but the Animator's CROUCH lane (driven by the Crouch bool) plays the " +
                 "Sneak Walk clip instead. Defaults ~0.55× the walk speed (a clear sneak). Sponsor-tunable.")]
        public float sneakSpeed = 3f;

        [Header("Camera-relative basis (AC1)")]
        [Tooltip("The orbit camera transform whose facing defines 'forward'. Auto-resolves to Camera.main " +
                 "if unset (the OrbitCamera rig is the main camera). Wired editor-time so it serializes.")]
        public Transform cameraTransform;

        [Header("Click-to-move handoff (AC3)")]
        [Tooltip("The ClickToMove this WASD locomotion REPLACES. Wired editor-time; on Start the click " +
                 "handling is disabled (clickEnabled=false) and any leftover click path is cleared so a " +
                 "stale destination can't fight the manual velocity. Null is tolerated (tests without it).")]
        public ClickToMove clickToMove;

        [Header("Jump (ticket 86ca9yq3q — Space → jump)")]
        [Tooltip("The avatar that owns the jump arc + the airborne ground-snap gate. Space's rising edge calls " +
                 "CastawayCharacter.TryJump(). Wired editor-time (MovementCameraScene); Awake resolves it from " +
                 "the children as a fallback. Null is tolerated (a bare movement test with no avatar).")]
        public CastawayCharacter castaway;

        [Header("Airborne air-control (ticket 86caac81y — subtle lateral nudge in flight)")]
        [Tooltip("How strongly A/D steers the player WHILE AIRBORNE (jump), as a per-second ACCELERATION (u/s²) " +
                 "applied to the existing horizontal velocity — NOT a full-speed snap. GROUNDED movement is " +
                 "unchanged (it still commands full moveSpeed/runSpeed directly). The Sponsor reported the OLD " +
                 "behavior (full-speed snap each frame) as 'thrown violently to the sides'; this gentle accel " +
                 "PRESERVES the forward momentum carried into the jump and nudges it slightly sideways. Default " +
                 "8 u/s² ≈ a soft drift over the ~0.6s arc, not a whip. Sponsor-tunable from the build.")]
        public float airControlAccel = 8f;
        [Tooltip("Cap (u/s) on the airborne HORIZONTAL speed the air-control nudge can build to. Prevents the " +
                 "subtle nudge from accumulating into a fast sideways slide over a long arc. Defaults to the " +
                 "WALK speed so airborne horizontal speed never exceeds a walk — momentum carried in from a RUN " +
                 "jump is preserved (the cap only clamps when the nudge would PUSH past it, it never brakes an " +
                 "already-faster carried velocity below itself).")]
        public float airControlMaxSpeed = 5.5f;

        // Deadzone below which input is treated as zero (the axes have a small dead band already, but a
        // squared-magnitude floor keeps a near-zero diagonal from creeping the agent).
        private const float InputDeadzone = 0.01f;

        private NavMeshAgent _agent;
        private Camera _mainCam;
        private bool _clickDisabled;

        // PROGRAMMATIC INPUT SEAM (the input-independent equivalent of ClickToMove.MoveTo). The shipped-
        // build verify capture + any scripted driver feeds a WASD vector here so the EXACT same Update path
        // runs in a build where real keystrokes can't be injected. null = read real keyboard (normal play).
        private Vector2? _inputOverride;

        // SPRINT (Shift) PROGRAMMATIC SEAM (86ca9yq34 — run-on-Shift). A headless PlayMode run / the shipped-
        // build verify capture can't inject a real Shift keystroke, so the sprint state can be overridden the
        // same way the move vector is. null = read the real keyboard (Input.GetKey(LeftShift|RightShift)).
        private bool? _sprintOverride;

        // JUMP (Space) PROGRAMMATIC SEAM (86ca9yq3q). A headless PlayMode run / the shipped-build verify capture
        // can't inject a real Space keystroke, so a jump can be REQUESTED via this latch (consumed on the next
        // Update, mirroring the keyboard's rising edge — one jump per request). null/false = read the real
        // keyboard (Input.GetKeyDown(Space)).
        private bool _jumpRequested;

        // CROUCH (Ctrl) PROGRAMMATIC SEAM (86caa3kur — crouch-on-Ctrl-hold). A headless PlayMode run / the
        // shipped-build verify capture can't inject a real Ctrl keystroke, so the crouch HOLD state can be
        // overridden the same way sprint is. null = read the real keyboard (Input.GetKey(Left|RightControl)).
        private bool? _crouchOverride;

        /// <summary>Drive WASD programmatically (the input-independent seam — the verify capture's analog of
        /// ClickToMove.MoveTo). Pass an (x=strafe, y=forward) vector; pass null / <see cref="ClearInputOverride"/>
        /// to return to the real keyboard. Used by WasdVerifyCapture to exercise WASD in the shipped exe.</summary>
        public void SetInputOverride(Vector2 input) => _inputOverride = input;

        /// <summary>Stop overriding input — return to reading the real keyboard.</summary>
        public void ClearInputOverride() => _inputOverride = null;

        /// <summary>Drive the SPRINT (run-on-Shift) state programmatically — the input-independent analog of the
        /// Shift key (ticket 86ca9yq34). Pass true to run, false to walk; <see cref="ClearSprintOverride"/> /
        /// null returns to the real keyboard. Used by the PlayMode AC5 test + the shipped-build run capture
        /// (headless / built-exe runs can't inject a Shift keystroke).</summary>
        public void SetSprintOverride(bool sprint) => _sprintOverride = sprint;

        /// <summary>Stop overriding sprint — return to reading the real LeftShift/RightShift key.</summary>
        public void ClearSprintOverride() => _sprintOverride = null;

        /// <summary>Drive the CROUCH (crouch-on-Ctrl-hold) state programmatically — the input-independent analog
        /// of the Ctrl key (ticket 86caa3kur). Pass true to crouch, false to stand; <see cref="ClearCrouchOverride"/>
        /// / null returns to the real keyboard. Used by the PlayMode AC7 test + the shipped-build crouch capture
        /// (headless / built-exe runs can't inject a Ctrl keystroke).</summary>
        public void SetCrouchOverride(bool crouch) => _crouchOverride = crouch;

        /// <summary>Stop overriding crouch — return to reading the real LeftControl/RightControl key.</summary>
        public void ClearCrouchOverride() => _crouchOverride = null;

        /// <summary>Request a JUMP programmatically — the input-independent analog of pressing Space (ticket
        /// 86ca9yq3q). Latched + consumed on the next Update (mirrors the keyboard's rising edge — one jump per
        /// call), so the PlayMode AC5 test + the shipped-build jump capture can trigger a jump where a real Space
        /// keystroke can't be injected. CastawayCharacter.TryJump() still enforces grounded-only (no double-jump),
        /// so a request while airborne is harmlessly ignored.</summary>
        public void RequestJump() => _jumpRequested = true;

        /// <summary>The camera-relative planar move direction the WASD input resolved to LAST frame
        /// (unit-length while moving, zero at rest). Exposed so the PlayMode regression can assert the
        /// direction is camera-relative without depending on the agent having physically moved (headless
        /// Time.deltaTime≈0 — the documented headless-time trap).</summary>
        public Vector3 LastMoveDir { get; private set; } = Vector3.zero;

        /// <summary>Whether WASD input is being held this frame (planar input above the deadzone).
        /// Exposed for tests / later systems.</summary>
        public bool HasInput { get; private set; }

        /// <summary>Whether the player is RUNNING this frame: Sprint (Shift) held AND moving (86ca9yq34). At
        /// rest, holding Shift does NOT run (no move → walk/run is moot). Exposed so the PlayMode AC5 regression
        /// can assert the run state flips with Shift, and for any later system (stamina, FOV kick).</summary>
        public bool IsSprinting { get; private set; }

        /// <summary>Whether the player is CROUCHING this frame: Crouch (Ctrl) held (86caa3kur). UNLIKE sprint,
        /// crouch does NOT gate on movement — holding Ctrl at a standstill IS a crouch (the Crouching Idle lowered
        /// stance); moving while crouched is the Sneak Walk. Exposed so the PlayMode AC7 regression asserts the
        /// crouch stance flips with Ctrl + drives the reduced sneak speed while moving, and so CastawayCharacter
        /// drives the Animator Crouch bool off it. Suppressed while AIRBORNE (you don't crouch mid-jump).</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>The move speed (u/s) the WASD input drove the agent at LAST frame — runSpeed while sprinting
        /// + moving, moveSpeed while walking, 0 at rest. Exposed so a regression can assert run drives a FASTER
        /// speed than walk WITHOUT depending on the agent having physically traversed (headless Time.deltaTime≈0,
        /// the documented headless-time trap — assert the commanded speed, not displacement).</summary>
        public float CurrentSpeed { get; private set; }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // Resolve the avatar that owns the jump arc (86ca9yq3q) if not wired editor-time — it's a child
            // avatar root under the player. Null is tolerated (a bare movement test with no avatar — jump is
            // simply inert there).
            if (castaway == null) castaway = GetComponentInChildren<CastawayCharacter>(true);
        }

        void Start()
        {
            // Take over from click-to-move (AC3): disable its click handling + clear any path it set so a
            // leftover destination can't fight the manual velocity. The component stays (MoveTo seam used by
            // the verify captures + harness); only its gameplay click-locomotion is turned off.
            DisableClickToMove();

            // SNEAK-WALK STUTTER fix (86caa3kur re-soak): configure the agent so its internal simulation TRACKS
            // the directly-commanded velocity instead of FIGHTING it (the slow-speed hitching). See
            // SmoothDirectDriveConfig for the CONFIRMED root cause + mechanism. Applied once here (the agent is
            // resolved in Awake). Null-tolerant (a bare test rig with no agent).
            EnsureSmoothDirectDrive();
        }

        /// <summary>Configure the NavMeshAgent for SMOOTH direct velocity-drive (86caa3kur re-soak — the sneak-walk
        /// stutter fix). WasdMovement commands agent.velocity directly each frame; this turns OFF the agent's own
        /// braking/acceleration dynamics so the simulated velocity tracks the command (no slow-speed sim-vs-command
        /// oscillation). The values come from the pure <see cref="SmoothDirectDriveConfig"/> so an EditMode guard
        /// pins them. Idempotent + null-tolerant.</summary>
        private void EnsureSmoothDirectDrive()
        {
            if (_agent == null) return;
            SmoothDirectDriveConfig(out float acceleration, out bool autoBraking);
            _agent.acceleration = acceleration;
            _agent.autoBraking = autoBraking;
        }

        private void DisableClickToMove()
        {
            if (_clickDisabled) return;
            if (clickToMove == null) clickToMove = GetComponent<ClickToMove>();
            if (clickToMove != null)
            {
                clickToMove.clickEnabled = false;
                clickToMove.Stop(); // clear any leftover click path
            }
            _clickDisabled = true;
        }

        void Update()
        {
            if (_agent == null) return;
            // Defensive: if Start ran before ClickToMove resolved (init order), keep trying to disable it.
            if (!_clickDisabled) DisableClickToMove();

            // Read WASD ONLY (86caa83wn fix 1 — WASD-only locomotion). The legacy Input Manager's
            // "Horizontal"/"Vertical" axes ALSO bind the arrow keys, so the prior GetAxisRaw path let the
            // ARROW keys drive the character too — which HIJACKED the F9 AxeNudgeTool (it uses the arrow keys
            // + PageUp/PageDown to dial the axe/clamp): pressing an arrow to nudge ALSO walked the player, so
            // the Sponsor couldn't dial. Read the four W/A/S/D KeyCodes directly instead (crisp digital
            // response, the right feel for keyboard locomotion — equivalent to GetAxisRaw but arrow-free).
            // A programmatic override (the verify-capture seam) still takes precedence so the shipped build
            // can exercise the SAME path without real keystrokes.
            // Swallow keyboard locomotion while a modal gameplay-UI panel is open (settings/inventory) —
            // UI Toolkit does NOT block legacy Input.* polling (research §E1), so without this the player
            // would walk while the Sponsor types/drags in the settings panel. A programmatic override (the
            // verify-capture seam) still drives so the shipped-build capture can exercise the path. The
            // agent simply holds position (zero commanded velocity) → Idle while the panel is up.
            Vector2 input = _inputOverride
                ?? (UiInputGate.CaptureWorldInput ? Vector2.zero : ReadWasdKeys());

            // SPRINT (run-on-Shift, 86ca9yq34): the real LeftShift/RightShift key, or the programmatic override
            // (the headless / shipped-build seam). Legacy Input (the project is activeInputHandler=0 — driving
            // input via legacy Input avoids the project-wide NEW-Input-System flip that would BREAK the
            // OrbitCamera mouse-orbit/zoom + the F8/F9 tools; the WASD base 86ca9yq2x chose legacy for exactly
            // this — see unity-conventions.md §Input System).
            bool sprint;
            if (_sprintOverride.HasValue)
                sprint = _sprintOverride.Value;
            else
                sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // CROUCH on Ctrl-HOLD (86caa3kur): the real Left/RightControl key (legacy Input — layout-agnostic, the
            // same reason WASD/run/jump chose legacy: it avoids the project-wide NEW-Input-System flip that would
            // BREAK the OrbitCamera mouse-orbit/zoom + the F8/F9 tools; and Ctrl is a layout-stable KeyCode, NOT a
            // US-position punctuation key that shifts on the Sponsor's Danish keyboard — [[sponsor-danish-keyboard-layout]])
            // OR the programmatic override (the headless / shipped-build seam). Swallowed while a modal gameplay-UI
            // panel is open (Ctrl is a common UI modifier) so the player doesn't crouch while the Sponsor works in
            // the settings/inventory panel — the same gate the move read uses. A programmatic override still drives
            // so the shipped-build crouch capture can exercise the path with the panel state irrelevant.
            bool crouchHeld;
            if (_crouchOverride.HasValue)
                crouchHeld = _crouchOverride.Value;
            else
                crouchHeld = !UiInputGate.CaptureWorldInput &&
                             (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

            // JUMP on Space (86ca9yq3q): the rising edge of the real Space key (legacy Input — the project is
            // activeInputHandler=0; legacy avoids the project-wide NEW-Input-System flip that would BREAK the
            // OrbitCamera mouse-orbit/zoom + the F8/F9 tools, the same reason WASD/run chose legacy) OR the
            // programmatic latch (the headless / shipped-build seam, consumed once here). CastawayCharacter
            // enforces grounded-only (no double-jump) + owns the arc + the airborne ground-snap gate; this only
            // detects the press. Jumping does NOT touch the XZ velocity below, so the player keeps moving while
            // airborne (AC1 — jump while idle AND while moving).
            bool jumpPressed = _jumpRequested
                || (!UiInputGate.CaptureWorldInput && Input.GetKeyDown(KeyCode.Space));
            _jumpRequested = false;
            if (jumpPressed && castaway != null) castaway.TryJump();

            ResolveCameraBasis(out Vector3 camFwd, out Vector3 camRight);
            Vector3 dir = CameraRelativeDirection(camFwd, camRight, input);

            HasInput = dir.sqrMagnitude > InputDeadzone * InputDeadzone;
            LastMoveDir = HasInput ? dir : Vector3.zero;

            // CROUCH stance (86caa3kur). UNLIKE sprint, crouch does NOT gate on movement — Ctrl at a standstill
            // IS a crouch (the lowered Crouching Idle stance). Suppress crouch while AIRBORNE (you don't crouch
            // mid-jump — the jump arc owns the body); on landing it re-reads the held Ctrl immediately. The
            // Ctrl+Shift RULE (AC2): CROUCH WINS — holding Ctrl while running drops to a sneak (sprint is
            // suppressed below), the explicit precedence the Sponsor judges in the soak.
            bool airborne = castaway != null && castaway.IsAirborne;
            IsCrouching = crouchHeld && !airborne;

            // RUN only while MOVING and NOT crouching (AC2 — crouch wins): holding Shift at a standstill is not
            // "running" (no travel to speed up), and Ctrl+Shift drops to the sneak rather than the run. So sprint
            // gates on HasInput AND !IsCrouching — release Shift OR stop moving OR crouch → no run.
            IsSprinting = HasInput && sprint && !IsCrouching;

            // Drive the EXISTING agent's velocity (AC3): the agent simulation keeps the player on the
            // NavMesh (grounding + obstacle handling intact) and exposes this as agent.velocity — exactly
            // what CastawayCharacter reads for the Walk<->Run blend tree (AC1) + the Idle<->locomotion flip
            // (AC4) + facing yaw (AC2). Zero velocity when no input → the agent settles → Idle.
            float walk = moveSpeed > 0.001f ? moveSpeed : _agent.speed;
            // Speed precedence (AC1/AC2): crouch → reduced SNEAK speed (crouch wins over sprint); else sprint →
            // run; else walk. Defensive clamps (run never slower than walk; sneak never faster than walk) live in
            // the pure ResolveSpeed so the EditMode guard pins the precedence + the clamps without a scene rig.
            float speed = ResolveSpeed(walk, runSpeed, sneakSpeed, IsSprinting, IsCrouching);
            CurrentSpeed = HasInput ? speed : 0f;

            // Drive the Animator's CROUCH lane (86caa3kur): the avatar's CastawayCharacter sets the controller's
            // Crouch bool from this each frame (Idle→CrouchIdle when !Moving, Locomotion→CrouchWalk when Moving —
            // PR #186's wired crouch lane). Null-tolerant (a bare movement test with no avatar). The Moving bool
            // CastawayCharacter already drives off agent.velocity selects CrouchIdle vs CrouchWalk for us.
            if (castaway != null) castaway.SetCrouch(IsCrouching);

            if (!_agent.isOnNavMesh) return;

            // AIRBORNE AIR-CONTROL (ticket 86caac81y). GROUNDED, the WASD direction commands FULL move speed
            // each frame (crisp keyboard locomotion — UNCHANGED). But applying that same full-speed snap WHILE
            // AIRBORNE instantly redirects the horizontal velocity to ±moveSpeed sideways on an A/D press — the
            // Sponsor's "thrown violently to the sides" (PR #69 soak). In flight we instead ACCELERATE the
            // existing horizontal velocity gently toward the input direction (airControlAccel u/s², capped), so
            // A/D is a SUBTLE lateral nudge that PRESERVES the forward momentum carried into the jump. The jump's
            // vertical arc is owned by CastawayCharacter (local-Y); this only governs the agent's horizontal XZ.
            // (airborne resolved above for the crouch suppression.)
            if (airborne)
            {
                // AIRBORNE: set the agent velocity directly — the jump arc + air-control are a physics nudge that
                // genuinely wants the velocity channel (coast/steer), and the brief airborne phase is fast enough
                // that the agent-sim braking fight isn't visible. UNCHANGED (the air-control fix #69 lives here).
                _agent.velocity = AirborneVelocity(_agent.velocity, LastMoveDir, airControlAccel,
                                                   airControlMaxSpeed, Time.deltaTime);
            }
            else
            {
                // GROUNDED: command the agent velocity directly (UNCHANGED — keeps agent.velocity readable for the
                // camera-follow lead + CastawayCharacter's Walk<->Run blend + facing, all of which read it). The
                // SNEAK-WALK STUTTER fix (86caa3kur re-soak) is NOT here — it's the EnsureSmoothDirectDrive() agent
                // config below, applied once. See its comment for the CONFIRMED root cause + mechanism.
                _agent.velocity = LastMoveDir * speed;
            }
        }

        // Read the four W/A/S/D keys off the real keyboard into a (strafe, forward) axis pair — the
        // ARROW-FREE replacement for GetAxisRaw("Horizontal"/"Vertical") (86caa83wn fix 1). Legacy Input
        // (the project is activeInputHandler=0). Delegates the boolean→axis mapping to the pure static
        // WasdAxesFromKeys so the EditMode guard can assert the mapping (incl. that arrows are NOT a source).
        private static Vector2 ReadWasdKeys()
            => WasdAxesFromKeys(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.A),
                                Input.GetKey(KeyCode.S), Input.GetKey(KeyCode.D));

        /// <summary>
        /// PURE WASD-key→axis mapping (the unit-testable core of fix 1, ticket 86caa83wn): map the four
        /// W/A/S/D key states to a (x = strafe A/D, y = forward W/S) axis pair, matching the old GetAxisRaw
        /// digital response (−1 / 0 / +1 per axis; opposite keys cancel). This deliberately reads ONLY the
        /// WASD letters — the arrow keys are NOT a movement source (they belong to the F9 AxeNudgeTool), which
        /// is exactly the bug this fix removes (arrows were driving movement via the Input-Manager axes).
        /// Static + dependency-free so the EditMode guard can assert the mapping (and the arrow-exclusion by
        /// construction — there is no arrow parameter to pass) with no scene rig.
        /// </summary>
        public static Vector2 WasdAxesFromKeys(bool w, bool a, bool s, bool d)
        {
            float x = (d ? 1f : 0f) - (a ? 1f : 0f);   // strafe: D = +, A = −
            float y = (w ? 1f : 0f) - (s ? 1f : 0f);   // forward: W = +, S = −
            return new Vector2(x, y);
        }

        /// <summary>
        /// PURE move-speed precedence (the unit-testable core of the crouch/run speed selection, tickets
        /// 86ca9yq34 + 86caa3kur): pick the commanded move speed from the walk/run/sneak speeds + the sprint +
        /// crouch states. The PRECEDENCE the Sponsor judges (AC2): CROUCH WINS over sprint — crouching commands
        /// the reduced <paramref name="sneak"/> speed even when sprint is also requested; otherwise sprinting
        /// commands <paramref name="run"/>; otherwise <paramref name="walk"/>. Defensive clamps: the run is never
        /// slower than the walk, and the sneak is never faster than the walk (a mis-tuned sneakSpeed can't make
        /// crouch FASTER than a stand-walk). Static + dependency-free so the EditMode guard asserts the precedence
        /// + the clamps with no Animator/NavMesh/headless-time dependency.
        /// </summary>
        public static float ResolveSpeed(float walk, float run, float sneak, bool isSprinting, bool isCrouching)
        {
            float clampedRun = run > walk ? run : walk;                          // run never slower than walk
            float clampedSneak = sneak > 0.001f && sneak < walk ? sneak : walk;  // sneak never faster than walk
            if (isCrouching) return clampedSneak;                                // crouch wins over sprint (AC2)
            if (isSprinting) return clampedRun;
            return walk;
        }

        // The agent acceleration (u/s²) the smooth-direct-drive config applies (86caa3kur re-soak). HIGH so the
        // agent's simulated velocity tracks the directly-commanded velocity within ~one frame instead of ramping —
        // the ramp is the slow-speed stutter source (see EnsureSmoothDirectDrive). Audited from source (not
        // serialized) so the config is deterministic + the EditMode guard pins the exact contract.
        public const float SmoothDriveAcceleration = 1000f;

        /// <summary>
        /// PURE smooth-direct-drive agent config (the unit-testable core of the sneak-walk STUTTER fix, ticket
        /// 86caa3kur re-soak): given the agent's current (acceleration, autoBraking), return the values that make
        /// the agent's internal simulation TRACK a directly-commanded velocity instead of FIGHTING it. The fix is
        /// (a) autoBraking = FALSE and (b) acceleration = a high value. WHY (the CONFIRMED root cause): WasdMovement
        /// sets agent.velocity each frame, but with updatePosition=true + autoBraking=true + NO path (desiredVelocity
        /// ≈0 under WASD) the agent continuously DECELERATES the root toward zero (braking) AND only ramps toward a
        /// new velocity at `acceleration` u/s² — so the simulated velocity LAGS + oscillates against the commanded
        /// velocity. At walk/run the large per-frame step swamps it; at the slow SNEAK speed (3 u/s) the braking/
        /// ramp noise is a large FRACTION of the step → the visible hitching the Sponsor reported. Disabling
        /// autoBraking removes the decel-toward-zero; a high acceleration makes the velocity snap to the command in
        /// ~one frame. Returns the corrected (acceleration, autoBraking) so the EditMode guard pins the contract
        /// with no scene rig. Static + dependency-free.
        /// </summary>
        public static void SmoothDirectDriveConfig(out float acceleration, out bool autoBraking)
        {
            acceleration = SmoothDriveAcceleration; // velocity snaps to the command (no slow ramp = no slow-speed jitter)
            autoBraking = false;                    // no decel toward the zero desiredVelocity (no braking fight)
        }

        // Resolve the camera's planar forward/right basis (the orbit camera's facing projected onto the
        // ground plane). Cached Camera.main fallback (no per-frame tag search — the GC rule).
        private void ResolveCameraBasis(out Vector3 camFwd, out Vector3 camRight)
        {
            Transform camT = cameraTransform;
            if (camT == null)
            {
                if (_mainCam == null) _mainCam = Camera.main;
                camT = _mainCam != null ? _mainCam.transform : null;
            }
            if (camT != null)
            {
                camFwd = camT.forward;
                camRight = camT.right;
            }
            else
            {
                // No camera (a bare test rig) — fall back to world axes so movement is still well-defined.
                camFwd = Vector3.forward;
                camRight = Vector3.right;
            }
        }

        /// <summary>
        /// PURE camera-relative direction (the unit-testable core of AC1): map a WASD <paramref name="input"/>
        /// (x = strafe A/D, y = forward W/S) into the camera's GROUND-PLANE basis. The camera forward/right
        /// are projected onto the XZ plane (y stripped) and re-normalized, so the result is always a planar
        /// unit direction (or zero for zero input) — the character never gets a vertical velocity component
        /// no matter how steeply the orbit camera is pitched down. Static + dependency-free so the PlayMode
        /// test can assert "W with the camera facing +X moves the player +X" with no scene rig.
        /// </summary>
        public static Vector3 CameraRelativeDirection(Vector3 camForward, Vector3 camRight, Vector2 input)
        {
            // Flatten the camera basis onto the ground plane (strip Y; the orbit cam pitches down ~55°, so
            // its raw forward has a big −Y — we want only the heading).
            Vector3 fwd = camForward; fwd.y = 0f;
            Vector3 right = camRight; right.y = 0f;
            // Degenerate guard: a perfectly top-down camera has ~zero planar forward — fall back to its
            // up-projected heading (camForward's negation of the down-tilt) so "forward" is still defined.
            if (fwd.sqrMagnitude < 1e-6f) { fwd = -Vector3.up; fwd.y = 0f; }
            fwd.Normalize();
            if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.up, fwd);
            right.Normalize();

            Vector3 dir = fwd * input.y + right * input.x;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
        }

        /// <summary>
        /// PURE airborne air-control velocity (the unit-testable core of the subtle-nudge fix, ticket 86caac81y).
        /// While airborne, the horizontal velocity is STEERED by a gentle acceleration toward the input direction
        /// instead of being SNAPPED to full move-speed — so A/D in flight is a subtle lateral nudge that preserves
        /// the momentum carried into the jump, not a violent sideways throw.
        ///
        /// Contract (what the regression guards pin — the BUG CLASS, not one instance):
        ///  - <paramref name="currentVel"/> is the agent's current XZ velocity (Y is preserved untouched — the
        ///    jump arc owns vertical). Output Y == input Y.
        ///  - With input held, the horizontal velocity moves toward <paramref name="moveDir"/> by at most
        ///    <paramref name="accel"/>·<paramref name="dt"/> u/s this frame — so a SINGLE-FRAME A/D press can
        ///    never produce a full-speed lateral snap (the bug). The per-frame lateral delta is bounded.
        ///  - With NO input (<paramref name="moveDir"/> ≈ zero) the horizontal velocity is UNCHANGED — momentum
        ///    coasts (we never brake the carried-in velocity, only steer it).
        ///  - The resulting horizontal SPEED is capped at <paramref name="maxSpeed"/>, BUT a velocity already
        ///    faster than the cap (momentum carried in from a run-jump) is never braked DOWN to the cap — the cap
        ///    only clamps the speed the nudge itself would BUILD past it. This keeps the nudge subtle without
        ///    stealing the Sponsor-approved carried-in forward momentum.
        /// Static + dependency-free so the EditMode guard can assert "one frame of A/D at the grounded full speed
        /// stays well under the move speed" with no scene rig (no Animator/NavMesh/headless-time dependency).
        /// </summary>
        public static Vector3 AirborneVelocity(Vector3 currentVel, Vector3 moveDir, float accel,
                                               float maxSpeed, float dt)
        {
            float vy = currentVel.y;                       // the jump arc owns vertical — never touch it
            Vector3 horiz = new Vector3(currentVel.x, 0f, currentVel.z);

            // No input → coast: keep the carried-in horizontal momentum unchanged (don't brake it).
            if (moveDir.sqrMagnitude > 1e-6f)
            {
                Vector3 dir = moveDir; dir.y = 0f;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                    float startSpeed = horiz.magnitude;        // remember the carried-in speed (for the cap rule)
                    // GENTLE STEER: nudge the horizontal velocity toward the input direction by accel·dt this
                    // frame (a small step), NOT a snap to dir*moveSpeed. MoveTowards bounds the per-frame change.
                    Vector3 target = dir * Mathf.Max(maxSpeed, startSpeed); // aim no slower than what we carry in
                    horiz = Vector3.MoveTowards(horiz, target, Mathf.Max(0f, accel) * Mathf.Max(0f, dt));

                    // Cap the speed the NUDGE built — but never brake a faster carried-in momentum below the cap.
                    float cap = Mathf.Max(maxSpeed, startSpeed);
                    if (horiz.magnitude > cap) horiz = horiz.normalized * cap;
                }
            }

            return new Vector3(horiz.x, vy, horiz.z);
        }
    }
}
