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
        public float followLerp = 12f;
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
            // RMB-drag orbit (legacy input, ported from the spike).
            bool rmbHeld = Input.GetMouseButton(1);

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

            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
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
                Vector3 desired = target.position + targetOffset;
                _followPos = instant ? desired
                    : FollowStep(_followPos, desired, followLerp, verticalFollowLerp, Time.deltaTime);
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
    }
}
