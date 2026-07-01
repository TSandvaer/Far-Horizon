using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode tests for the CAMERA-RELATIVE direction math of WASD locomotion (ticket 86ca9yq2x, AC1).
    ///
    /// These pin the BUG CLASS, not one instance: WasdMovement.CameraRelativeDirection is the pure,
    /// dependency-free core that maps a WASD input into the camera's GROUND-PLANE basis. The classic WASD
    /// bugs this guards are (a) "W moves world-forward, not camera-forward" (camera basis ignored), (b)
    /// "the character flies/sinks when the camera is pitched down" (the camera's −Y forward leaks into the
    /// move vector), and (c) A/D inverted. Asserting the pure function from multiple camera headings + a
    /// steep pitch catches all three with no scene rig (deterministic, no Animator/NavMesh/headless-time).
    /// </summary>
    public class WasdMovementTests
    {
        // The orbit camera at the Sponsor-locked default pitch 55° facing +Z (yaw 0): forward points
        // down-and-forward, right points +X. This is the real gameplay basis the move math must flatten.
        private static void DefaultOrbitBasis(float yawDeg, out Vector3 fwd, out Vector3 right)
        {
            // Pitch 55° down, yaw about Y. Quaternion.Euler(pitch, yaw, 0) is exactly how OrbitCamera builds it.
            Quaternion rot = Quaternion.Euler(55f, yawDeg, 0f);
            fwd = rot * Vector3.forward;   // has a large −Y component (camera looks down)
            right = rot * Vector3.right;
        }

        [Test]
        public void ForwardW_WithCameraFacingPlusZ_MovesPlusZ_AndIsPlanar()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            // W = (x:0, y:+1).
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));

            Assert.AreEqual(0f, dir.y, 1e-4f,
                "the move direction must be PLANAR — the camera's steep −Y forward must NOT leak into the " +
                "move vector (else the character flies/sinks when the orbit cam pitches down). dir.y was " + dir.y);
            Assert.Greater(dir.z, 0.9f,
                "W with the camera facing +Z must move the character +Z (camera-relative forward), not some " +
                "other axis — dir was " + dir);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f, "the move direction must be unit-length while moving");
        }

        [Test]
        public void BackS_IsOppositeForward()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            Vector3 w = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));
            Vector3 s = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, -1f));
            Assert.Less(Vector3.Dot(w, s), -0.99f, "S must move opposite to W (back vs forward).");
        }

        [Test]
        public void StrafeAD_IsPerpendicularToForward_AndCorrectHandedness()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            Vector3 d = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(1f, 0f)); // D = +strafe
            Vector3 a = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(-1f, 0f)); // A = −strafe
            Vector3 w = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));

            Assert.AreEqual(0f, Vector3.Dot(d, w), 1e-3f, "strafe must be perpendicular to forward");
            Assert.Less(Vector3.Dot(d, a), -0.99f, "A and D must be opposite strafe directions");
            // With the camera facing +Z, D (right strafe) must be +X (camera-right projected to the plane).
            Assert.Greater(d.x, 0.9f, "D (right strafe) with the camera facing +Z must move +X (camera-right)");
        }

        [Test]
        public void Rotated45Camera_ForwardFollowsCameraHeading()
        {
            // Orbit the camera 45° about Y; forward must now point into the +X/+Z quadrant (diagonal), not +Z.
            DefaultOrbitBasis(45f, out var fwd, out var right);
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));
            Assert.AreEqual(0f, dir.y, 1e-4f, "still planar at a rotated heading");
            Assert.Greater(dir.x, 0.5f, "forward at yaw 45 must carry +X — the move tracks the camera heading");
            Assert.Greater(dir.z, 0.5f, "forward at yaw 45 must carry +Z — the move tracks the camera heading");
            // The heading must equal the camera's PLANAR forward (the camera-relative guarantee).
            Vector3 camPlanar = new Vector3(fwd.x, 0f, fwd.z).normalized;
            Assert.Greater(Vector3.Dot(dir, camPlanar), 0.999f,
                "W must move along the camera's planar forward at every heading (the camera-relative AC).");
        }

        [Test]
        public void ZeroInput_YieldsZeroDirection()
        {
            DefaultOrbitBasis(0f, out var fwd, out var right);
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, Vector2.zero);
            Assert.AreEqual(Vector3.zero, dir, "no input → no move direction (the character holds Idle)");
        }

        [Test]
        public void NearlyTopDownCamera_StillProducesAPlanarForward_NoNaN()
        {
            // A near-vertical camera (pitch ~89°) has an almost-zero planar forward; the degenerate guard must
            // still produce a finite, planar, unit direction (never NaN / never a vertical move).
            Quaternion rot = Quaternion.Euler(89f, 0f, 0f);
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right;
            Vector3 dir = WasdMovement.CameraRelativeDirection(fwd, right, new Vector2(0f, 1f));
            Assert.IsFalse(float.IsNaN(dir.x) || float.IsNaN(dir.y) || float.IsNaN(dir.z),
                "the degenerate near-top-down camera must not yield a NaN move direction");
            Assert.AreEqual(0f, dir.y, 1e-4f, "even a near-top-down camera yields a planar move (no vertical creep)");
            Assert.AreEqual(1f, dir.magnitude, 1e-3f, "still unit-length");
        }

        // =================================================================================================
        // AIRBORNE AIR-CONTROL (ticket 86caac81y — the "thrown violently to the sides" fix).
        //
        // BUG CLASS these pin (NOT one instance): while airborne, WasdMovement used to drive
        // _agent.velocity = LastMoveDir * fullSpeed every frame — IDENTICAL to grounded — so a single A/D
        // press in flight SNAPPED the lateral velocity to ±moveSpeed instantly = the violent throw. The fix
        // steers via a capped acceleration that PRESERVES carried-in momentum. These guard the pure
        // WasdMovement.AirborneVelocity core: a single-frame A/D press stays a SUBTLE nudge (well under the
        // full move speed), forward momentum is preserved, the vertical (jump-arc) channel is untouched, and
        // no-input coasts. They are scene-rig-free (no Animator/NavMesh/headless-time).
        // =================================================================================================

        private const float Accel = 9f;       // production default airControlAccel (u/s²) — 86caambxh: Sponsor soak 2026-07-01 raised 5→9 (snappier mid-air sideways air-steer)
        private const float Cap = 5.5f;       // production default airControlMaxSpeed (u/s) — the walk speed
        private const float MoveSpeed = 5.5f; // grounded walk speed (the OLD airborne snap magnitude)
        private const float RunSpeed = 9.5f;  // grounded run speed
        private const float Dt = 1f / 60f;    // a 60fps frame

        [Test]
        public void Airborne_SingleFrameStrafe_IsASubtleNudge_NotAFullSpeedSnap()
        {
            // Carried into the jump moving FORWARD at the run speed along +Z (a run-jump). Press D (camera-right
            // = +X) for ONE frame. The OLD code would set velocity = (+X)*moveSpeed = 5.5 u/s lateral INSTANTLY.
            Vector3 carried = new Vector3(0f, 2f, RunSpeed);       // +Y arc velocity present (must be preserved)
            Vector3 dPress = new Vector3(1f, 0f, 0f);             // D resolves to +X (one frame)

            Vector3 v = WasdMovement.AirborneVelocity(carried, dPress, Accel, Cap, Dt);

            // The per-frame lateral (X) gain must be tiny — bounded by accel·dt (~0.133 u/s), NOWHERE NEAR the
            // 5.5 u/s the old full-speed snap produced. THIS is the regression guard for the "violent throw".
            Assert.LessOrEqual(Mathf.Abs(v.x), Accel * Dt + 1e-4f,
                $"a SINGLE-FRAME airborne A/D press must nudge lateral velocity by at most accel·dt " +
                $"({Accel * Dt:F4} u/s), not snap to the full move speed (the old bug threw it to {MoveSpeed} u/s). " +
                $"Got vx={v.x:F4}");
            Assert.Less(Mathf.Abs(v.x), MoveSpeed * 0.1f,
                "the lateral nudge must be a small fraction of the grounded move speed (subtle, not a throw).");
        }

        [Test]
        public void Airborne_PreservesVerticalArcVelocity_Untouched()
        {
            // The jump arc owns vertical (CastawayCharacter local-Y); air-control must NEVER touch velocity.y.
            Vector3 carried = new Vector3(0f, 3.27f, 4f);
            Vector3 v = WasdMovement.AirborneVelocity(carried, new Vector3(1f, 0f, 0f), Accel, Cap, Dt);
            Assert.AreEqual(3.27f, v.y, 1e-5f, "air-control must preserve the jump arc's vertical velocity exactly.");
        }

        [Test]
        public void Airborne_NoInput_CoastsHorizontalMomentum_Unchanged()
        {
            // Forward momentum carried into the jump (PR #69 — Sponsor approved): with no A/D/W/S held in flight,
            // the horizontal velocity must COAST unchanged (we steer, we never brake the carried-in momentum).
            Vector3 carried = new Vector3(1.2f, 1f, RunSpeed);
            Vector3 v = WasdMovement.AirborneVelocity(carried, Vector3.zero, Accel, Cap, Dt);
            Assert.AreEqual(carried.x, v.x, 1e-5f, "no-input airborne must not change horizontal X (coast momentum).");
            Assert.AreEqual(carried.z, v.z, 1e-5f, "no-input airborne must not change horizontal Z (coast momentum).");
            Assert.AreEqual(carried.y, v.y, 1e-5f, "no-input airborne must not change vertical.");
        }

        [Test]
        public void Airborne_StrafeGentlySteers_WithoutKillingForwardMomentum()
        {
            // Held D for a handful of frames while carrying forward (+Z) momentum: the velocity should gain a
            // SMALL +X component while KEEPING most of its forward +Z — a gentle steer, not a hard redirect.
            Vector3 v = new Vector3(0f, 1.5f, MoveSpeed);
            for (int i = 0; i < 6; i++) // ~0.1s of held D
                v = WasdMovement.AirborneVelocity(v, new Vector3(1f, 0f, 0f), Accel, Cap, Dt);

            Assert.Greater(v.x, 0f, "held D must build SOME +X lateral velocity (it does steer).");
            Assert.Less(v.x, MoveSpeed * 0.5f,
                "after ~0.1s the lateral velocity must still be well under the move speed (subtle, gradual steer).");
            Assert.Greater(v.z, MoveSpeed * 0.7f,
                "forward (+Z) momentum carried into the jump must be largely PRESERVED while strafing (PR #69 AC).");
        }

        [Test]
        public void Airborne_RunJumpCarriedMomentum_IsNotBrakedDownToTheWalkCap()
        {
            // A RUN jump carries +Z momentum at runSpeed (9.5) which EXCEEDS the walk-speed cap (5.5). The nudge
            // must NOT brake that carried-in momentum down to the cap — the cap only clamps speed the nudge BUILDS
            // past it. Pressing W (continue forward) should keep ~the run speed, not drop to 5.5.
            Vector3 v = new Vector3(0f, 2f, RunSpeed);
            for (int i = 0; i < 10; i++)
                v = WasdMovement.AirborneVelocity(v, new Vector3(0f, 0f, 1f), Accel, Cap, Dt); // hold W (+Z)
            float horizSpeed = new Vector2(v.x, v.z).magnitude;
            Assert.Greater(horizSpeed, Cap + 0.5f,
                $"a run-jump's carried-in {RunSpeed} u/s forward momentum must not be braked to the walk cap " +
                $"({Cap}); horizontal speed was {horizSpeed:F3}. The cap clamps the NUDGE, never the carried momentum.");
        }

        [Test]
        public void Airborne_NudgeAccumulationIsCappedOverALongArc()
        {
            // Holding A/D for a long arc must not accumulate into a fast sideways slide: from REST, held D for
            // many frames converges toward — and is clamped at — the cap, never blowing past it.
            Vector3 v = Vector3.zero;
            for (int i = 0; i < 600; i++) // 10s of held D (far longer than any real arc) — must stay clamped
                v = WasdMovement.AirborneVelocity(v, new Vector3(1f, 0f, 0f), Accel, Cap, Dt);
            Assert.LessOrEqual(new Vector2(v.x, v.z).magnitude, Cap + 1e-3f,
                "the air-control nudge must clamp at airControlMaxSpeed — it can't accumulate into a fast slide.");
        }

        // =================================================================================================
        // WASD-ONLY (arrow keys excluded) — ticket 86caa83wn fix 1.
        //
        // BUG CLASS these pin (NOT one instance): the old movement read GetAxisRaw("Horizontal"/"Vertical"),
        // whose legacy Input-Manager axes ALSO bind the ARROW keys — so the arrows drove the character, which
        // HIJACKED the F9 AxeNudgeTool (it dials the axe/clamp with the arrow keys + PageUp/PageDown). The fix
        // reads ONLY the W/A/S/D KeyCodes. WasdAxesFromKeys is the pure mapping; the arrow-exclusion is
        // STRUCTURAL — there is no arrow parameter, so an arrow press cannot reach the move vector by
        // construction. These guard the mapping equals the old GetAxisRaw digital response for the WASD letters.
        // =================================================================================================

        [Test]
        public void WasdKeys_MapToExpectedAxes_MatchingTheOldDigitalResponse()
        {
            // W → forward +1; S → −1; D → strafe +1; A → −1 (the GetAxisRaw digital values, arrow-free).
            Assert.AreEqual(new Vector2(0f, 1f), WasdMovement.WasdAxesFromKeys(true, false, false, false), "W → forward");
            Assert.AreEqual(new Vector2(0f, -1f), WasdMovement.WasdAxesFromKeys(false, false, true, false), "S → back");
            Assert.AreEqual(new Vector2(1f, 0f), WasdMovement.WasdAxesFromKeys(false, false, false, true), "D → strafe right");
            Assert.AreEqual(new Vector2(-1f, 0f), WasdMovement.WasdAxesFromKeys(false, true, false, false), "A → strafe left");
        }

        [Test]
        public void WasdKeys_OppositeKeysCancel_AndNoKeysIsZero()
        {
            Assert.AreEqual(Vector2.zero, WasdMovement.WasdAxesFromKeys(false, false, false, false), "no keys → no move");
            Assert.AreEqual(Vector2.zero, WasdMovement.WasdAxesFromKeys(true, false, true, false), "W+S cancel");
            Assert.AreEqual(Vector2.zero, WasdMovement.WasdAxesFromKeys(false, true, false, true), "A+D cancel");
            Assert.AreEqual(new Vector2(1f, 1f), WasdMovement.WasdAxesFromKeys(true, false, false, true), "W+D → diagonal");
        }

        [Test]
        public void Movement_ReadsOnlyWasd_NotArrows_StructurallyArrowFree()
        {
            // The arrow-exclusion is enforced BY CONSTRUCTION: the only key-reading move path is
            // WasdAxesFromKeys(w,a,s,d) — there is no arrow input parameter, so an arrow key CANNOT contribute
            // to the move vector. This is the regression guard for "arrows drive movement" — if a future edit
            // re-introduces an arrow source it must add a parameter here, which breaks this signature contract.
            // (The old GetAxisRaw("Horizontal"/"Vertical") path — which DID bind arrows — is gone.)
            var method = typeof(WasdMovement).GetMethod(nameof(WasdMovement.WasdAxesFromKeys));
            Assert.IsNotNull(method, "the pure WASD-only key mapping must exist (replaces the arrow-binding GetAxisRaw path)");
            var ps = method.GetParameters();
            Assert.AreEqual(4, ps.Length, "the WASD mapping takes exactly the four W/A/S/D booleans — no arrow source");
            foreach (var p in ps)
                Assert.AreEqual(typeof(bool), p.ParameterType, "each WASD-key parameter is a bool key-state (W/A/S/D only)");
        }

        // =================================================================================================
        // CROUCH speed precedence (ticket 86caa3kur — crouch-on-Ctrl-hold) — WasdMovement.ResolveSpeed.
        //
        // BUG CLASS these pin (NOT one instance): the Ctrl+Shift precedence (AC2 — CROUCH WINS) + the reduced
        // SNEAK speed (AC1) + the defensive clamps. A regression where crouch+sprint runs at run speed (crouch
        // didn't win), or where a mis-tuned sneakSpeed makes a crouched move FASTER than a stand-walk, fails
        // here. Pure + scene-rig-free (no Animator/NavMesh/headless-time).
        // =================================================================================================
        private const float Walk = 5.5f, Run = 9.5f, Sneak = 3f; // production defaults

        [Test]
        public void ResolveSpeed_Walking_PlainWalkSpeed()
        {
            Assert.AreEqual(Walk, WasdMovement.ResolveSpeed(Walk, Run, Sneak, false, false), 1e-5f,
                "no sprint, no crouch → the plain walk speed.");
        }

        [Test]
        public void ResolveSpeed_Sprinting_RunSpeed()
        {
            Assert.AreEqual(Run, WasdMovement.ResolveSpeed(Walk, Run, Sneak, true, false), 1e-5f,
                "sprint (Shift) + not crouching → the run speed (86ca9yq34).");
        }

        [Test]
        public void ResolveSpeed_Crouching_SneakSpeed_SlowerThanWalk()
        {
            float s = WasdMovement.ResolveSpeed(Walk, Run, Sneak, false, true);
            Assert.AreEqual(Sneak, s, 1e-5f, "crouch (Ctrl) → the reduced sneak speed (86caa3kur AC1).");
            Assert.Less(s, Walk, "a crouched (sneak) move must be SLOWER than a stand-walk (it's a sneak).");
        }

        [Test]
        public void ResolveSpeed_CrouchWinsOverSprint_AC2()
        {
            // THE AC2 precedence guard: Ctrl WHILE Shift is held drops to the SNEAK, not the run (crouch wins).
            float s = WasdMovement.ResolveSpeed(Walk, Run, Sneak, /*isSprinting*/ true, /*isCrouching*/ true);
            Assert.AreEqual(Sneak, s, 1e-5f,
                "CROUCH WINS (AC2): holding Ctrl while running must drop to the SNEAK speed, NOT keep the run " +
                "speed. A regression that lets sprint override crouch fails here.");
            Assert.Less(s, Run, "crouch+sprint must not run.");
        }

        [Test]
        public void ResolveSpeed_DefensiveClamps_RunNeverSlowerThanWalk_SneakNeverFasterThanWalk()
        {
            // Mis-tuned run (slower than walk) → clamped up to walk (run never slower than walk).
            Assert.AreEqual(Walk, WasdMovement.ResolveSpeed(Walk, /*run*/ 2f, Sneak, true, false), 1e-5f,
                "a run speed mis-set BELOW the walk speed must clamp to the walk (run never slower than walk).");
            // Mis-tuned sneak (faster than walk) → clamped down to walk (a crouch can't be FASTER than a walk).
            Assert.AreEqual(Walk, WasdMovement.ResolveSpeed(Walk, Run, /*sneak*/ 8f, false, true), 1e-5f,
                "a sneak speed mis-set ABOVE the walk speed must clamp to the walk — a crouched move can never " +
                "be FASTER than a stand-walk (the defensive clamp).");
            // Zero/unset sneak falls back to walk (not a freeze).
            Assert.AreEqual(Walk, WasdMovement.ResolveSpeed(Walk, Run, /*sneak*/ 0f, false, true), 1e-5f,
                "an unset (0) sneak speed must fall back to the walk speed — a crouch must never freeze the player.");
        }

        // =================================================================================================
        // SNEAK-SPEED-SNAP ISOLATION instrument (86caa3kur re-soak attempt-3 /unstick) — WasdMovement.EffectiveSneakSpeed.
        //
        // BUG CLASS these pin: the DEBUG isolation toggle must be INERT at its shipped default (a debug handle
        // that changes shipped behavior when OFF is the silent-regression class). Default (snapToWalk=false) →
        // the reduced sneak speed is UNCHANGED; ON (true) → the crouch commands the walk speed (the disconfirming
        // control). Pure + scene-rig-free.
        // =================================================================================================
        [Test]
        public void EffectiveSneakSpeed_DefaultOff_ReturnsReducedSneak_ShippedBehaviorUnchanged()
        {
            Assert.AreEqual(Sneak, WasdMovement.EffectiveSneakSpeed(Sneak, Walk, /*snapToWalk*/ false), 1e-5f,
                "DEFAULT (snap OFF) must leave the SHIPPED reduced sneak speed untouched — the instrument is " +
                "inert at its default (no silent regression of crouch behavior).");
        }

        [Test]
        public void EffectiveSneakSpeed_SnapOn_ReturnsWalkSpeed_TheIsolationControl()
        {
            Assert.AreEqual(Walk, WasdMovement.EffectiveSneakSpeed(Sneak, Walk, /*snapToWalk*/ true), 1e-5f,
                "snap ON must command the NORMAL walk speed for a crouched move — the disconfirming control that " +
                "rules out the slow-sneak-speed-specific jerk path (86caa3kur attempt-3).");
        }

        [Test]
        public void EffectiveSneakSpeed_ComposesWithResolveSpeed_CrouchAtWalkSpeed_WhenSnapped()
        {
            // The full path the Update runs: snap ON feeds the walk speed into ResolveSpeed for a crouched move,
            // so a crouched (Ctrl) move commands the WALK speed (not the reduced sneak) — what the Sponsor A/Bs.
            float effSneak = WasdMovement.EffectiveSneakSpeed(Sneak, Walk, /*snapToWalk*/ true);
            float speed = WasdMovement.ResolveSpeed(Walk, Run, effSneak, /*sprint*/ false, /*crouch*/ true);
            Assert.AreEqual(Walk, speed, 1e-5f,
                "with snap ON, a crouched move must resolve to the WALK speed end-to-end (EffectiveSneakSpeed → " +
                "ResolveSpeed). Default OFF still resolves to the reduced sneak (the inert-default guard above).");
        }

        // SNEAK-ISOLATION runtime toggle DEFAULT — the WasdMovement component's snap flag starts OFF (shipped).
        // This is the component-state half of the inert-default guard (the pure-math half is above): a freshly
        // built WasdMovement must report the snap OFF so a normal soak/CI build runs the reduced sneak speed.
        [Test]
        public void SneakSpeedSnap_RuntimeDefault_IsOff()
        {
            var go = new GameObject("wasd-snap-default");
            try
            {
                go.AddComponent<UnityEngine.AI.NavMeshAgent>(); // WasdMovement [RequireComponent]
                var w = go.AddComponent<WasdMovement>();
                Assert.IsFalse(w.SneakSpeedSnappedToWalk,
                    "the sneak-speed-snap isolation toggle must DEFAULT OFF on a fresh component — a normal " +
                    "soak/CI build must run the shipped reduced sneak speed, not the walk-speed isolation control.");
                w.SetSneakSpeedSnapToWalk(true);
                Assert.IsTrue(w.SneakSpeedSnappedToWalk, "SetSneakSpeedSnapToWalk(true) must flip the flag ON.");
                w.SetSneakSpeedSnapToWalk(false);
                Assert.IsFalse(w.SneakSpeedSnappedToWalk, "SetSneakSpeedSnapToWalk(false) must flip it back OFF.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Airborne_VsOldSnap_QuantifiesTheFix_DiagnoseBeforeFix()
        {
            // DIAGNOSE-BEFORE-FIX, quantified in a guard: the OLD airborne path was velocity = LastMoveDir*speed,
            // so ONE frame of D from a standing-still-in-air state produced 5.5 u/s lateral INSTANTLY. The NEW
            // path produces ≤ accel·dt (at the shipped 9 u/s² accel ≈ 0.15 u/s) that frame — a ~37× reduction in
            // the single-frame lateral impulse (86caambxh: Sponsor soak 2026-07-01 raised the accel 5→9 for a
            // snappier mid-air sideways air-steer; still nowhere near the old full-speed snap).
            // This pins the magnitude of the fix so a regression that re-snaps is caught loudly.
            Vector3 dPress = new Vector3(1f, 0f, 0f);
            float oldFrameLateral = (dPress * MoveSpeed).x;                              // the old full-speed snap
            float newFrameLateral = WasdMovement.AirborneVelocity(Vector3.zero, dPress, Accel, Cap, Dt).x;
            Assert.AreEqual(MoveSpeed, oldFrameLateral, 1e-4f, "sanity: old path snapped to the full move speed.");
            Assert.Less(newFrameLateral, oldFrameLateral / 10f,
                $"the new single-frame lateral ({newFrameLateral:F4}) must be <1/10 the old snap ({oldFrameLateral:F4}) " +
                "— the subtle-nudge fix (86caac81y).");
        }

        // =================================================================================================
        // SMOOTH DIRECT-DRIVE agent config (ticket 86caa3kur RE-SOAK — the SNEAK-WALK STUTTER fix) —
        // WasdMovement.SmoothDirectDriveConfig. WasdMovement commands agent.velocity directly each frame; with the
        // agent's default autoBraking=true + a modest acceleration + NO path (desiredVelocity≈0 under WASD), the
        // agent's internal simulation DECELERATES the root toward zero AND only RAMPS toward the new velocity at
        // `acceleration` u/s² — so the simulated velocity LAGS + oscillates against the command. At walk/run the
        // large per-frame step swamps it; at the slow SNEAK speed the braking/ramp noise is a large FRACTION of
        // the step → the visible hitching. These pin the BUG CLASS: the fix turns autoBraking OFF (no decel-to-zero
        // fight) and sets a HIGH acceleration (velocity snaps to the command, no slow ramp). A regression that
        // re-enables autoBraking or drops the acceleration back reintroduces the slow-speed stutter — caught here.
        // Pure + scene-rig-free (no Animator/NavMesh/headless-time).
        // =================================================================================================
        [Test]
        public void SmoothDirectDriveConfig_TurnsAutoBrakingOff_NoDecelTowardZeroFight()
        {
            WasdMovement.SmoothDirectDriveConfig(out _, out bool autoBraking, out _);
            Assert.IsFalse(autoBraking,
                "the smooth direct-drive config must turn autoBraking OFF — with it ON the agent decelerates the " +
                "root toward the zero desiredVelocity (no path under WASD), fighting the directly-commanded " +
                "velocity each frame. That braking fight is the slow-speed sneak STUTTER the re-soak fixes.");
        }

        [Test]
        public void SmoothDirectDriveConfig_UsesHighAcceleration_VelocitySnapsToCommand_NoSlowRamp()
        {
            WasdMovement.SmoothDirectDriveConfig(out float acceleration, out _, out _);
            // A high acceleration makes the simulated velocity converge to the directly-set command within ~one
            // frame instead of RAMPING over many frames (the ramp lag is disproportionately large at the slow
            // sneak speed → hitching). Far above the production walk/run speeds so the snap is effectively instant.
            Assert.AreEqual(WasdMovement.SmoothDriveAcceleration, acceleration, 1e-3f,
                "the config must apply the high SmoothDriveAcceleration (the velocity snaps to the command, no " +
                "slow ramp).");
            Assert.Greater(acceleration, Run * 10f,
                $"the smooth-drive acceleration ({acceleration}) must be FAR above the run speed ({Run}) so the " +
                "simulated velocity snaps to the commanded velocity within a frame (no slow-speed ramp jitter). A " +
                "regression to the old modest acceleration (~30) reintroduces the sneak stutter.");
        }

        // SNEAK-WALK HITCH fix (86caa3kur re-soak ATTEMPT 2 — the CONFIRMED cause). The CI Animator+motion trace
        // (run 28432489421) REFUTED all three animation candidates (clip plays smoothly) and isolated the hitch to
        // the NavMeshAgent-owned root XZ translation at the slow sneak speed. Per the NavMeshAgent.velocity docs,
        // READING velocity "returns the simulation's current value, which may differ from what you set due to
        // COLLISION AVOIDANCE" — the per-frame RVO avoidance pass perturbs the sim's integration of the commanded
        // velocity. At the slow 3 u/s sneak that perturbation is a large FRACTION of the step (sneak step CoV 21x
        // the walk baseline) = the hitch. The fix turns local RVO avoidance OFF (a single-player castaway has no
        // agents to avoid; static geometry is handled by the baked NavMesh). This pins the BUG CLASS: a regression
        // that re-enables avoidance reintroduces the slow-speed translation jitter. Pure + scene-rig-free.
        [Test]
        public void SmoothDirectDriveConfig_TurnsObstacleAvoidanceOff_NoPerFrameVelocityPerturbation()
        {
            WasdMovement.SmoothDirectDriveConfig(out _, out _, out var avoidance);
            Assert.AreEqual(UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance, avoidance,
                "the smooth direct-drive config must turn local RVO obstacle avoidance OFF — the avoidance pass " +
                "perturbs the sim's per-frame velocity (NavMeshAgent.velocity docs), and at the slow sneak speed " +
                "that perturbation is a large fraction of the step = the CONFIRMED translation jitter (run 28432489421).");
            Assert.AreEqual(UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance, WasdMovement.SmoothDriveAvoidance,
                "the SmoothDriveAvoidance constant the config returns must be NoObstacleAvoidance.");
        }
    }
}
