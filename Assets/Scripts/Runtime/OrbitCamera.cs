using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Mouse-orbit camera with a top-down-ish default angle — the production camera foundation.
    ///
    /// Deliberate clean re-implementation of the engine-eval spike's OrbitCamera
    /// (EmbergraveUnitySlice, READ-ONLY working spec). RMB-drag orbits (yaw + pitch), scroll
    /// wheel zooms the distance, and the rig follows a target (the player) so the world reads
    /// as "small character in a big alive world".
    ///
    /// Carries the spike's hard-won fixes:
    ///  - Default pitch 55 (the Sponsor's preferred top-down-ish framing) — LOCKED, inside the band.
    ///  - Pitch clamp WIDENED to 8-70 (drew/ocean-camera-fix, 2026-06-13). The spike's 35 floor was
    ///    chosen so near-horizontal pitch couldn't graze a side-by-side biome boundary — but in the
    ///    single production play space the Sponsor explicitly wants to tilt DOWN toward the horizon
    ///    for gameplay (and to SEE the beach ocean, which sits seaward of the spawn). The 35 floor
    ///    literally blocked that: a TRACE (OceanCameraDiag, 2026-06-13) showed that at yaw-180 the
    ///    camera CENTRE only reaches the beach (~Z+4) at pitch 35 — the sea (near-edge Z-10.5) never
    ///    enters frame except as a far fogged sliver top-of-frame (the "grey pond" soak report). At
    ///    pitch ~8-15 the centre reaches the coastline and the open sea fills the upper frame to the
    ///    fogged horizon. So the floor drops to 8 (a hair of downward tilt kept so the camera never
    ///    goes fully horizontal/degenerate). maxPitch 70 + defaultPitch 55 are UNCHANGED.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.0f, 0f);

        [Header("Default framing (top-down-ish)")]
        public float defaultYaw = 0f;
        public float defaultPitch = 55f;   // high pitch = looking down; the Sponsor-preferred angle
        public float distance = 14f;

        [Header("Orbit limits")]
        // Pitch band 8-70 (drew/ocean-camera-fix). Default 55 is inside the band and untouched (LOCKED).
        // Floor dropped 35->8 so the Sponsor can tilt down toward the horizon + see the seaward ocean
        // (the 35 floor framed the sea as a far fogged "grey pond" — OceanCameraDiag trace). 8 (not 0)
        // keeps a hair of downward tilt so the view never goes fully horizontal/grazing-degenerate.
        public float minPitch = 8f;
        public float maxPitch = 70f;
        public float yawSpeed = 200f;
        public float pitchSpeed = 120f;

        [Header("Zoom")]
        public float zoomSpeed = 8f;
        public float minDistance = 6f;
        public float maxDistance = 26f;

        [Header("Smoothing")]
        // 86caaqhj5 ATTEMPT 2 — HORIZONTAL follow tightened 12->18. The jump-pull-back A/S/D failure is the
        // HORIZONTAL follow LAGGING the player through a fast move+jump: a pure exponential follower trails a
        // constant-velocity target by v/k (k = followLerp 1/s). At walk 5.5 u/s, k=12 → ~0.46u lag; the diag
        // measured a symmetric ~0.41u horizontal follow-lag across ALL 4 headings — it is NOT direction-specific
        // in the code; perpendicular-to-view motion (A/D strafe) + away-from-view (S) simply READ the constant
        // lag worst (player slides toward/out of frame edge) while toward-view (W) hides it. Raising followLerp
        // alone shrinks but never zeroes the lag; the velocity FEED-FORWARD below (followLeadTime) cancels the
        // steady-state lag outright. Sponsor-dialable live on the F7 CameraFollowNudgeTool.
        public float followLerp = 18f;
        // 86caa83wn fix 3 — JUMP camera-follow lag. The follow point eases toward the target each frame; with a
        // SINGLE rate on ALL axes (the old followLerp 12), the VERTICAL follow lagged the fast jump arc (the
        // avatar rises at jumpVelocity 5.5 u/s for ~0.6s) — the camera trailed the rise + drop, so on jump the
        // player appeared "pulled backwards before landing" (Sponsor soak 2026-06-18). The HORIZONTAL follow is
        // kept smooth (followLerp) — a snappy horizontal follow reads jerky during normal walk/run — but the
        // VERTICAL follow uses a MUCH higher rate so the camera tracks the jump arc tightly (no vertical lag).
        // High enough to effectively pin the vertical to the target within a frame or two at 60fps, so the arc
        // is followed at jump speed. (Walking has negligible vertical motion, so this never affects ground feel.)
        [Tooltip("Vertical follow rate (1/s) — MUCH higher than followLerp so the camera tracks the JUMP ARC " +
                 "vertically without lag (the old single rate trailed the rise/drop → 'pulled back on jump'). " +
                 "Horizontal stays on followLerp for a smooth ground feel; only the Y axis follows fast.")]
        public float verticalFollowLerp = 60f;

        // 86caaqhj5 — the JUMP-PULL-BACK ROOT CAUSE (diagnose-via-trace, EditMode JumpPullBackDiag 2026-06-18).
        // The earlier verticalFollowLerp split (fix above) was tracking a Y THAT NEVER MOVES during a jump: the
        // camera target is the player ROOT (MovementCameraScene: orbit.target = player.transform), but the jump
        // arc is a LOCAL-Y on the avatar CHILD (CastawayCharacter.transform.localPosition.y) — the root's world
        // Y is constant through the whole jump. Diag PROVED it: visual avatar apex 0.795u, camera-follow vertical
        // response 0.0000u → the camera NEVER followed the visual arc in ANY direction. The "W works, A/D/S pull
        // back" report was the SAME constant horizontal follow-lag (0.41u in the travel direction, symmetric
        // across all 4 dirs per the diag) reading differently per heading against a camera whose height never
        // rose with the player. FIX: feed the avatar's actual JumpHeight (exposed by CastawayCharacter, on TOP of
        // the root) into the desired follow-point Y, so the vertical follow has the REAL arc to track — the
        // camera now rises/falls WITH the visual jump in every direction, killing the asymmetric pull-back.
        [Header("Jump-arc vertical follow (86caaqhj5)")]
        [Tooltip("The avatar whose JUMP ARC the camera must follow vertically. The jump is a local-Y on this " +
                 "CHILD avatar, NOT on the camera target (the player root), so without this the camera never " +
                 "tracks the visual jump rise (→ the directional 'pulled back on jump' percept). Its JumpHeight " +
                 "is added to the follow-point Y so the camera rises/falls with the arc. Wired editor-time " +
                 "(MovementCameraScene) so it serializes; null is tolerated (a bare camera test rig — no arc).")]
        public CastawayCharacter jumpHeightSource;

        // 86caaqhj5 ATTEMPT 2 — HORIZONTAL follow VELOCITY FEED-FORWARD (the A/S/D jump-pull-back mechanism fix).
        // A pure exponential follower lags a constant-velocity target by a STEADY amount v/k (v = target speed,
        // k = followLerp). During a fast move+jump the agent keeps driving XZ (the arc is vertical-only), so the
        // camera trails the player by that constant lag in the TRAVEL direction — which reads worst when the
        // travel is perpendicular to / away from the view (A/D strafe, S back: the player slides out of frame),
        // and is hidden when travelling toward the view (W). The fix LEADS the horizontal follow target by the
        // velocity × leadTime: choosing leadTime = 1/followLerp makes the lead EXACTLY cancel the exponential
        // follower's steady-state lag (the follower lags by v/k; leading by v/k zeroes the net error), so the
        // player stays framed through fast move+jump in EVERY direction. It is heading-INDEPENDENT by
        // construction — it consumes the velocity VECTOR, never a per-direction branch.
        [Header("Horizontal follow lead (velocity feed-forward, 86caaqhj5)")]
        [Tooltip("The agent whose HORIZONTAL travel velocity the follow leads by (the player root's NavMeshAgent). " +
                 "Wired editor-time (MovementCameraScene) so it serializes; null is tolerated (the follow falls " +
                 "back to a plain lag-prone exponential ease — a bare camera test rig with no agent).")]
        public UnityEngine.AI.NavMeshAgent followVelocitySource;
        [Tooltip("Seconds of velocity feed-forward the HORIZONTAL follow leads the target by. Defaults to " +
                 "1/followLerp (the exact lag-cancelling value: an exponential follower lags v/k = v·(1/k); " +
                 "leading by velocity·(1/k) zeroes the steady-state lag in ALL directions). Set >0 to override " +
                 "(more = the camera leads further ahead of the player; the F7 nudge tool dials it live). 0 = " +
                 "use the 1/followLerp default. Clamped to a sane ceiling so a runaway value can't fling the cam.")]
        public float followLeadTime = 0f;
        [Tooltip("Upper clamp (s) on the effective lead time so a dialed/serialized value can never overshoot the " +
                 "player wildly. 0.25s at run 9.5 u/s = ~2.4u lead — plenty; beyond reads as the camera racing ahead.")]
        public float maxLeadTime = 0.25f;

        // 86caaqhj5 ATTEMPT 3 — THE CONFIRMED ROOT CAUSE (diagnose-via-trace, EditMode JumpCameraFollowTraceTests
        // 2026-06-18; the prior two fixes' MATH was sound but mis-targeted). The persistent "jump+A/D = OUT of view,
        // jump+S = INTO the camera" is the HORIZONTAL follow's STEADY-STATE LAG (v/k = run 9.5 / followLerp 18 ≈
        // 0.53u) framing the airborne player OFF-CENTRE in the TRAVEL DIRECTION. A LEAD-OFF control trace QUANTIFIED
        // it: at pitch 55° the 0.53u world lag projects to screenY +0.37 (W) / −0.37 (S) and screenX ±0.45 (A/D) —
        // EXACTLY the Sponsor's per-heading percept. The attempt-2 velocity LEAD was meant to cancel that lag, but it
        // only cancels when the agent's REPORTED velocity matches the real travel rate — during a jump the agent
        // velocity is steered + capped by WasdMovement.AirborneVelocity (air-control), so the lead no longer matches
        // and the lag re-frames the player by heading. That is ALSO why F7 does nothing: followLerp/lead tuning can't
        // fix a lag the air-control breaks the lead-cancellation of. FIX: while AIRBORNE, follow the root XZ TIGHTLY
        // (airborneFollowLerp, ~zero lag) with NO lead — the avatar stays CENTRED in every heading (trace: worst
        // screen offset 0.10u, spread 0.006u across W/A/S/D, vs 0.37–0.45u off-centre before). Grounded follow is
        // UNCHANGED (the smooth followLerp + lead) — only the brief airborne phase tracks tight.
        [Header("Airborne horizontal follow (86caaqhj5 attempt 3 — the confirmed jump-pull-back fix)")]
        [Tooltip("Horizontal (X/Z) follow rate (1/s) used ONLY WHILE the avatar is AIRBORNE (jump). High so the " +
                 "follow has ~zero steady-state lag during the jump → the player stays CENTRED regardless of travel " +
                 "heading (the lag framed it OFF-centre: jump+A/D out of view, jump+S into the camera). The velocity " +
                 "LEAD is also skipped while airborne (the air-control steers/caps the agent velocity, so the lead no " +
                 "longer matches the real travel rate — it was the broken cancellation, not a fix). Grounded follow " +
                 "is UNCHANGED (followLerp + lead). Matches verticalFollowLerp so XZ and Y track the arc equally tight.")]
        public float airborneFollowLerp = 60f;

        // ---- TERRAIN COLLISION (BIG ROUND ISLAND N2, 86ca9a7qn — "player disappears under a hill") ----
        // The orbit rig placed the camera at a fixed distance behind the target with NO terrain awareness, so
        // on the hilly island the camera could (a) sink BELOW the terrain surface, or (b) have a hill between
        // it and the character — either way the player vanishes. These keep the camera ABOVE the ground AND
        // pull it IN when a hill occludes the character, so the player stays visible.
        [Header("Terrain collision (hill clip / occlusion)")]
        [Tooltip("Layers treated as solid terrain for the camera collision + above-ground clamp (the Ground " +
                 "layer). Set editor-time so it serializes (no Awake string lookup in the build).")]
        public LayerMask terrainMask = 0;
        [Tooltip("How far above the terrain surface the camera must stay (so it never grazes/sinks into a hill).")]
        public float groundClearance = 0.6f;
        [Tooltip("Keep the camera this far off any hill it would otherwise hit between it and the player.")]
        public float collisionPadding = 0.35f;
        [Tooltip("Don't pull the camera closer than this when a hill occludes the player (avoids a face-zoom).")]
        public float minCollisionDistance = 2.5f;

        private float _yaw;
        private float _pitch;
        private Vector3 _followPos;

        // 86caatv7k — cursor lock during RMB camera-orbit. While the RIGHT mouse button is HELD (orbiting) the
        // cursor is LOCKED to the window centre + hidden, so dragging to orbit never walks the pointer off-screen
        // / out of the window. On RMB RELEASE we restore the free, visible cursor so the Sponsor can click menus /
        // inventory / belt. We only mutate Cursor state on the press/release EDGE (tracked here), never while RMB
        // is up — so UI that wants the cursor free between orbits is left alone.
        private bool _orbiting;

        void Start()
        {
            _yaw = defaultYaw;
            _pitch = Mathf.Clamp(defaultPitch, minPitch, maxPitch);
            if (target != null) _followPos = target.position + targetOffset;
            Apply(instant: true);
        }

        // 86caatv7k — safety net: if the orbit camera is disabled/destroyed mid-orbit (RMB still held), restore
        // the free, visible cursor so it can never be left locked + hidden when no orbit is driving it.
        void OnDisable()
        {
            if (_orbiting)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _orbiting = false;
            }
        }

        void LateUpdate()
        {
            // While a modal gameplay-UI panel is open (settings/inventory), swallow orbit + zoom + the
            // RMB cursor-lock (UI Toolkit does NOT block legacy Input.* polling — research §E1): the Sponsor
            // drags sliders with the cursor FREE, and the camera must not orbit/zoom under the panel. The
            // follow (Apply, below) still runs so the camera keeps framing the player. rmbHeld is forced
            // false so the cursor-lock edge releases to free+visible the frame the panel opens.
            bool rmbHeld = !UiInputGate.CaptureWorldInput && Input.GetMouseButton(1);

            // 86caatv7k — CURSOR LOCK on the RMB press/release EDGE only (the decision lives in the pure-static
            // ResolveCursorForOrbit so the edge contract is unit-testable headlessly without Input/Cursor).
            if (ResolveCursorForOrbit(rmbHeld, ref _orbiting, out CursorLockMode lockState, out bool visible))
            {
                Cursor.lockState = lockState;
                Cursor.visible = visible;
            }

            if (rmbHeld)
            {
                _yaw += Input.GetAxisRaw("Mouse X") * yawSpeed * Time.deltaTime;
                _pitch -= Input.GetAxisRaw("Mouse Y") * pitchSpeed * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            // SCROLL-ZOOM gate (86cabeqj9 soak NIT): swallow the wheel while a modal panel is open (CaptureWorldInput)
            // OR while the mouse hovers the NON-MODAL dev console (PointerOverConsole) — else the camera zooms under
            // the cursor as the Sponsor scrolls the panel. UI Toolkit can't stop legacy Input.* polling, hence the
            // flag (research §E1). ONLY scroll is gated here; orbit/WASD stay live (the intentional passthrough).
            float scroll = (UiInputGate.CaptureWorldInput || UiInputGate.PointerOverConsole) ? 0f : Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            Apply(instant: false);
        }

        private void Apply(bool instant)
        {
            if (target != null)
            {
                // The desired follow point = the root target + offset, PLUS the avatar's live jump-arc height on
                // the Y axis (86caaqhj5). The jump arc is a local-Y on the avatar CHILD, so the root target's Y
                // is constant through a jump — adding JumpHeight here gives the vertical follow the REAL arc to
                // track (the camera rises/falls WITH the visual jump in every direction; without it the camera
                // height never moved and the constant horizontal follow-lag read as a directional 'pull back').
                float jumpY = jumpHeightSource != null ? jumpHeightSource.JumpHeight : 0f;
                bool airborne = jumpHeightSource != null && jumpHeightSource.IsAirborne;
                Vector3 desired = target.position + targetOffset;
                desired.y = DesiredFollowY(target.position.y, targetOffset.y, jumpY);

                // HORIZONTAL follow target. GROUNDED: lead the X/Z by the agent's velocity × leadTime so the
                // exponential follower's steady-state lag (v/k) is cancelled and the player stays framed during a
                // fast move (86caaqhj5 attempt 2). AIRBORNE: SKIP the lead entirely (86caaqhj5 attempt 3 — the
                // CONFIRMED jump fix). While airborne WasdMovement steers/caps the agent velocity (air-control), so
                // the lead no longer matches the real travel rate and re-frames the player off-centre by heading
                // (the trace-proven Sponsor percept: jump+A/D out of view, jump+S into the camera). Following the
                // raw root XZ + a TIGHT airborne rate (below) gives ~zero lag → the avatar stays centred in every
                // direction. Instant snaps (Start / programmatic) always skip the lead — exact head position wanted.
                if (!instant && !airborne)
                {
                    Vector3 vel = followVelocitySource != null ? followVelocitySource.velocity : Vector3.zero;
                    float lead = EffectiveLeadTime(followLeadTime, followLerp, maxLeadTime);
                    Vector3 ledXZ = DesiredFollowXZ(desired, vel, lead);
                    desired.x = ledXZ.x;
                    desired.z = ledXZ.z;   // desired.y (jump-arc head Y) left untouched — lead is horizontal only
                }

                // HORIZONTAL follow RATE: the smooth grounded followLerp normally, but a TIGHT airborneFollowLerp
                // while airborne so the jump has ~zero horizontal lag (the avatar stays centred in all 4 headings —
                // 86caaqhj5 attempt 3). The vertical (jump-arc) follow always uses verticalFollowLerp.
                float horizRate = airborne ? airborneFollowLerp : followLerp;
                _followPos = instant ? desired
                    : FollowStep(_followPos, desired, horizRate, verticalFollowLerp, Time.deltaTime);
            }

            // Guard the pitch every Apply so a serialized/probe value can never escape the band.
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPos = _followPos - rot * Vector3.forward * distance;
            Vector3 pos = ResolveCameraCollision(_followPos, desiredPos);
            transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>
        /// Keep the camera ABOVE the terrain and OUT of any hill between it and the player (BIG ROUND ISLAND
        /// N2). Two steps, in order:
        ///  1. OCCLUSION PULL-IN: raycast from the follow point (the player's head) toward the desired camera
        ///     position; if a hill is hit, pull the camera in to just BEFORE the hill (with padding) so the
        ///     character is never behind terrain. Clamped to minCollisionDistance so it never face-zooms.
        ///  2. ABOVE-GROUND CLAMP: raycast straight DOWN at the resolved camera XZ; if the camera would sit
        ///     below (terrain surface + groundClearance) — e.g. it dropped into a valley behind a ridge — lift
        ///     it to that clearance height so it never sinks under/into the hill.
        /// No terrainMask set (0) → no collision (the camera behaves exactly as before; tests/standalone rigs
        /// without a Ground layer are unaffected). Pure-ish: reads physics, returns the safe position.
        /// </summary>
        public Vector3 ResolveCameraCollision(Vector3 followPoint, Vector3 desiredPos)
        {
            if (terrainMask.value == 0) return desiredPos; // collision disabled (no Ground mask wired)

            Vector3 pos = desiredPos;

            // 1. Occlusion pull-in: is there a hill between the player and the camera?
            Vector3 toCam = desiredPos - followPoint;
            float desiredDist = toCam.magnitude;
            if (desiredDist > 0.001f)
            {
                Vector3 dir = toCam / desiredDist;
                if (Physics.Raycast(followPoint, dir, out RaycastHit hit, desiredDist, terrainMask,
                        QueryTriggerInteraction.Ignore))
                {
                    float pulled = Mathf.Max(minCollisionDistance, hit.distance - collisionPadding);
                    pos = followPoint + dir * pulled;
                }
            }

            // 2. Above-ground clamp: never let the camera sit below the terrain surface beneath it.
            Vector3 high = pos + Vector3.up * 200f;
            if (Physics.Raycast(high, Vector3.down, out RaycastHit down, 400f, terrainMask,
                    QueryTriggerInteraction.Ignore))
            {
                float minY = down.point.y + groundClearance;
                if (pos.y < minY) pos.y = minY;
            }

            return pos;
        }

        /// <summary>
        /// PURE desired-follow-point-Y (the unit-testable core of the jump-pull-back ROOT-CAUSE fix, 86caaqhj5):
        /// the Y the camera follow point should ease toward = the root target Y + the vertical offset + the live
        /// jump-arc height. The jump arc is a local-Y on the avatar CHILD (not the root target), so WITHOUT the
        /// <paramref name="jumpHeight"/> term the desired Y is CONSTANT through a jump and the camera never tracks
        /// the visual rise (the directional 'pulled back on jump' percept — diag-proven the camera vertical
        /// response was 0.0000u while the avatar rose ~0.80u). Static + dependency-free so the EditMode guard can
        /// assert the camera Y tracks the arc identically in every heading with no scene/Time dependency.
        /// </summary>
        public static float DesiredFollowY(float targetY, float offsetY, float jumpHeight)
            => targetY + offsetY + Mathf.Max(0f, jumpHeight);

        /// <summary>
        /// PURE horizontal-follow velocity feed-forward (the A/S/D jump-pull-back MECHANISM fix, 86caaqhj5
        /// attempt 2): lead the X/Z follow target by the horizontal velocity × <paramref name="leadTime"/> so
        /// the exponential follower's STEADY-STATE LAG (v/k) is cancelled and the player stays framed during a
        /// fast move+jump. The Y is passed through UNCHANGED (the jump-arc head Y is owned by DesiredFollowY;
        /// the lead is purely horizontal). Only the X/Z components of <paramref name="velocity"/> are used —
        /// any vertical velocity is ignored (the jump arc is not an agent velocity here). HEADING-INDEPENDENT
        /// by construction: it adds the velocity VECTOR, never a per-direction branch — so W/A/S/D all get the
        /// same lag-cancellation (the bug class: the lag READS worst on strafe/back, but the fix is symmetric).
        /// Static + dependency-free so the EditMode per-direction trace asserts the led target tracks the player
        /// within tolerance for every heading with no scene/Time dependency.
        /// </summary>
        public static Vector3 DesiredFollowXZ(Vector3 desired, Vector3 velocity, float leadTime)
        {
            float lt = Mathf.Max(0f, leadTime);
            return new Vector3(
                desired.x + velocity.x * lt,
                desired.y,                       // Y untouched — jump-arc head Y owned by DesiredFollowY
                desired.z + velocity.z * lt);
        }

        /// <summary>
        /// PURE effective lead-time resolution (86caaqhj5 attempt 2): the seconds of velocity feed-forward the
        /// horizontal follow leads by. A configured value of 0 means "auto" = 1/<paramref name="followLerp"/>,
        /// the EXACT value that cancels the exponential follower's steady-state lag (the follower lags v/k =
        /// v·(1/k); leading by v·(1/k) zeroes the net error). A configured value &gt;0 overrides (the F7 nudge
        /// tool dials this live). The result is clamped to [0, <paramref name="maxLeadTime"/>] so a runaway
        /// serialized/dialed value can never fling the camera ahead of the player. Static + dependency-free so
        /// the auto = 1/k contract is unit-asserted with no scene.
        /// </summary>
        public static float EffectiveLeadTime(float configured, float followLerp, float maxLeadTime)
        {
            float k = Mathf.Max(0.0001f, followLerp);
            float lead = configured > 0.0001f ? configured : 1f / k;   // 0 = auto (1/k = exact lag-cancel)
            float ceiling = Mathf.Max(0f, maxLeadTime);
            return Mathf.Clamp(lead, 0f, ceiling);
        }

        /// <summary>
        /// PURE per-axis follow step (the unit-testable core of the jump-follow-lag fix, ticket 86caa83wn fix
        /// 3): ease <paramref name="current"/> toward <paramref name="desired"/> this frame, using
        /// <paramref name="horizLerp"/> on the X/Z axes (smooth ground feel) and a HIGHER
        /// <paramref name="vertLerp"/> on the Y axis (track the jump arc with no lag). Both are
        /// frame-rate-independent exponential approaches (1 − e^(−rate·dt)). Static + dependency-free so the
        /// EditMode guard can assert "the vertical follow closes the gap to the jump arc much faster than the
        /// horizontal follow" with no scene/Time dependency. The BUG CLASS it pins: a single follow rate on all
        /// axes makes the camera trail the fast vertical jump arc (the 'pulled back on jump' percept).
        /// </summary>
        public static Vector3 FollowStep(Vector3 current, Vector3 desired, float horizLerp, float vertLerp, float dt)
        {
            float ah = 1f - Mathf.Exp(-Mathf.Max(0f, horizLerp) * dt);
            float av = 1f - Mathf.Exp(-Mathf.Max(0f, vertLerp) * dt);
            return new Vector3(
                Mathf.Lerp(current.x, desired.x, ah),
                Mathf.Lerp(current.y, desired.y, av),   // Y follows fast — tracks the jump arc, no lag
                Mathf.Lerp(current.z, desired.z, ah));
        }

        /// <summary>
        /// PURE cursor-lock edge decision (86caatv7k — the unit-testable core of the RMB-orbit cursor lock).
        /// Given whether the right mouse button is held this frame and the prior orbiting state, decide whether
        /// the cursor state must CHANGE this frame and to what. Mutates <paramref name="orbiting"/> to the new
        /// state. Returns TRUE only on a press/release EDGE (the caller then applies <paramref name="lockState"/>
        /// + <paramref name="visible"/> to Cursor); returns FALSE when nothing changed (RMB up-and-was-up, or
        /// held-and-was-held) so the cursor is NEVER touched while RMB isn't held. Contract:
        ///   • rising edge  (held &amp;&amp; !orbiting) → Locked + hidden, orbiting=true,  returns true.
        ///   • falling edge (!held &amp;&amp; orbiting)  → None   + visible, orbiting=false, returns true.
        ///   • no edge → leaves orbiting + cursor untouched, returns false.
        /// Static + dependency-free so the EditMode guard can assert the full edge sequence with no scene/Input.
        /// </summary>
        public static bool ResolveCursorForOrbit(bool rmbHeld, ref bool orbiting,
            out CursorLockMode lockState, out bool visible)
        {
            if (rmbHeld && !orbiting)
            {
                orbiting = true;
                lockState = CursorLockMode.Locked;
                visible = false;
                return true;
            }
            if (!rmbHeld && orbiting)
            {
                orbiting = false;
                lockState = CursorLockMode.None;
                visible = true;
                return true;
            }
            // No edge — RMB isn't being pressed/released this frame; leave the cursor exactly as it is.
            lockState = CursorLockMode.None;
            visible = true;
            return false;
        }

        // ---- Test / programmatic hooks ----
        /// <summary>Set yaw and re-apply instantly (orbit-from-code; used by camera tests).</summary>
        public void SetYaw(float yaw) { _yaw = yaw; Apply(true); }

        /// <summary>
        /// Drive a pitch request (e.g. an orbit gesture) and re-apply instantly. The value is
        /// clamped to [minPitch, maxPitch] — the AC's guarantee that the camera obeys the clamp.
        /// </summary>
        public void SetPitch(float pitch) { _pitch = Mathf.Clamp(pitch, minPitch, maxPitch); Apply(true); }

        /// <summary>Set zoom distance (clamped) and re-apply instantly.</summary>
        public void SetDistance(float d) { distance = Mathf.Clamp(d, minDistance, maxDistance); Apply(true); }

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => distance;

        /// <summary>The effective horizontal velocity-lead time (s) in play THIS frame — the resolved
        /// EffectiveLeadTime(followLeadTime, followLerp, maxLeadTime). Exposed so the F7 CameraFollowNudgeTool
        /// surfaces the auto-resolved 1/followLerp value the Sponsor is actually getting when followLeadTime=0.</summary>
        public float EffectiveLead => EffectiveLeadTime(followLeadTime, followLerp, maxLeadTime);
    }
}
