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
        [Tooltip("Walk speed (u/s) the WASD input drives. Defaults to the agent's own speed if left 0. " +
                 "OOS: run-on-Shift is a downstream ticket (86ca9yq34) — this is the single walk speed.")]
        public float moveSpeed = 5.5f;

        [Header("Camera-relative basis (AC1)")]
        [Tooltip("The orbit camera transform whose facing defines 'forward'. Auto-resolves to Camera.main " +
                 "if unset (the OrbitCamera rig is the main camera). Wired editor-time so it serializes.")]
        public Transform cameraTransform;

        [Header("Click-to-move handoff (AC3)")]
        [Tooltip("The ClickToMove this WASD locomotion REPLACES. Wired editor-time; on Start the click " +
                 "handling is disabled (clickEnabled=false) and any leftover click path is cleared so a " +
                 "stale destination can't fight the manual velocity. Null is tolerated (tests without it).")]
        public ClickToMove clickToMove;

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

        /// <summary>Drive WASD programmatically (the input-independent seam — the verify capture's analog of
        /// ClickToMove.MoveTo). Pass an (x=strafe, y=forward) vector; pass null / <see cref="ClearInputOverride"/>
        /// to return to the real keyboard. Used by WasdVerifyCapture to exercise WASD in the shipped exe.</summary>
        public void SetInputOverride(Vector2 input) => _inputOverride = input;

        /// <summary>Stop overriding input — return to reading the real keyboard.</summary>
        public void ClearInputOverride() => _inputOverride = null;

        /// <summary>The camera-relative planar move direction the WASD input resolved to LAST frame
        /// (unit-length while moving, zero at rest). Exposed so the PlayMode regression can assert the
        /// direction is camera-relative without depending on the agent having physically moved (headless
        /// Time.deltaTime≈0 — the documented headless-time trap).</summary>
        public Vector3 LastMoveDir { get; private set; } = Vector3.zero;

        /// <summary>Whether WASD input is being held this frame (planar input above the deadzone).
        /// Exposed for tests / later systems.</summary>
        public bool HasInput { get; private set; }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        void Start()
        {
            // Take over from click-to-move (AC3): disable its click handling + clear any path it set so a
            // leftover destination can't fight the manual velocity. The component stays (MoveTo seam used by
            // the verify captures + harness); only its gameplay click-locomotion is turned off.
            DisableClickToMove();
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

            // Read WASD (legacy Input Manager — the project is activeInputHandler=0; Horizontal/Vertical
            // already bind a/d + w/s + arrows). GetAxisRaw = crisp digital response (no smoothing ramp),
            // the right feel for keyboard locomotion. A programmatic override (the verify-capture seam)
            // takes precedence so the shipped build can exercise the SAME path without real keystrokes.
            Vector2 input;
            if (_inputOverride.HasValue)
            {
                input = _inputOverride.Value;
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                input = new Vector2(h, v);
            }

            ResolveCameraBasis(out Vector3 camFwd, out Vector3 camRight);
            Vector3 dir = CameraRelativeDirection(camFwd, camRight, input);

            HasInput = dir.sqrMagnitude > InputDeadzone * InputDeadzone;
            LastMoveDir = HasInput ? dir : Vector3.zero;

            // Drive the EXISTING agent's velocity (AC3): the agent simulation keeps the player on the
            // NavMesh (grounding + obstacle handling intact) and exposes this as agent.velocity — exactly
            // what CastawayCharacter reads for the Idle<->Walk blend (AC4) + facing yaw (AC2). Zero velocity
            // when no input → the agent settles → CastawayCharacter flips to Idle.
            float speed = moveSpeed > 0.001f ? moveSpeed : _agent.speed;
            if (_agent.isOnNavMesh)
                _agent.velocity = LastMoveDir * speed;
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
    }
}
