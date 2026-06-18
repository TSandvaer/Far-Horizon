using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the JUMP camera-follow-lag fix (ticket 86caa83wn fix 3). OrbitCamera.FollowStep is
    /// the pure, dependency-free core: it eases the follow point toward the target using followLerp on the
    /// HORIZONTAL (X/Z) axes (a smooth ground feel) and a MUCH higher verticalFollowLerp on the Y axis so the
    /// camera tracks the fast jump arc with no lag.
    ///
    /// BUG CLASS these pin (NOT one instance): the old follow used a SINGLE rate on all axes, so the VERTICAL
    /// follow lagged the jump arc (the avatar rises at jumpVelocity ~5.5 u/s for ~0.6s) — the camera trailed
    /// the rise + drop, so on jump the player appeared "pulled backwards before landing" (Sponsor soak
    /// 2026-06-18). These assert the vertical follow closes the gap to the jump arc FASTER than the horizontal
    /// follow, while leaving the horizontal feel unchanged — with no scene/Time dependency.
    /// </summary>
    public class OrbitCameraFollowTests
    {
        // Production defaults (OrbitCamera.followLerp / verticalFollowLerp). HorizLerp 12->18 in 86caaqhj5
        // attempt 2 (tightened so even residual lag is small; the velocity lead cancels the steady-state lag).
        private const float HorizLerp = 18f;
        private const float VertLerp = 60f;
        private const float Dt = 1f / 60f; // a 60fps frame

        [Test]
        public void VerticalFollow_ClosesTheJumpArcGapFasterThanHorizontal()
        {
            // The target jumped UP 0.84u (the jump apex) and drifted forward 0.1u in the same frame. The
            // vertical follow must close MORE of its gap this frame than the horizontal follow closes of its
            // (equal-sized) gap — that is what removes the vertical lag on the arc.
            Vector3 current = Vector3.zero;
            Vector3 desired = new Vector3(0.1f, 0.84f, 0.1f);

            Vector3 stepped = OrbitCamera.FollowStep(current, desired, HorizLerp, VertLerp, Dt);

            float vertClosedFrac = stepped.y / desired.y;                 // fraction of the Y gap closed
            float horizClosedFrac = stepped.x / desired.x;               // fraction of the X gap closed
            Assert.Greater(vertClosedFrac, horizClosedFrac + 0.2f,
                $"the vertical follow must close the jump-arc gap notably faster than the horizontal follow " +
                $"(vert {vertClosedFrac:F3} vs horiz {horizClosedFrac:F3}) — else the camera trails the jump " +
                "arc and the player reads as 'pulled back on jump' (86caa83wn fix 3).");
        }

        [Test]
        public void VerticalFollow_TracksTheArcTightly_OverAFewFrames()
        {
            // Over a handful of frames the vertical follow must essentially CATCH the jump apex (track tightly),
            // where the old slow rate would still be lagging well behind.
            Vector3 current = Vector3.zero;
            Vector3 desired = new Vector3(0f, 0.84f, 0f);
            for (int i = 0; i < 4; i++) // ~0.067s — a small slice of the ~0.6s arc
                current = OrbitCamera.FollowStep(current, desired, HorizLerp, VertLerp, Dt);
            Assert.Greater(current.y, desired.y * 0.95f,
                $"after a few frames the vertical follow must have closed ≥95% of the jump-arc gap (got " +
                $"{current.y:F3} toward {desired.y:F3}); the camera tracks the arc, no vertical lag.");
        }

        [Test]
        public void HorizontalFollow_UsesTheHorizontalLerp_OnXZ_NotTheVerticalRate()
        {
            // The X/Z follow must use the HORIZONTAL followLerp (not the fast verticalFollowLerp) — FollowStep
            // applies the slower horizontal rate to X/Z and the fast rate to Y only. (86caaqhj5 attempt 2 raised
            // the horizontal default 12->18, but the AXIS contract — X/Z on horizLerp — is what this pins.)
            Vector3 current = new Vector3(0f, 0f, 0f);
            Vector3 desired = new Vector3(2f, 0f, 3f);
            Vector3 stepped = OrbitCamera.FollowStep(current, desired, HorizLerp, VertLerp, Dt);

            float a = 1f - Mathf.Exp(-HorizLerp * Dt);
            Assert.AreEqual(Mathf.Lerp(0f, 2f, a), stepped.x, 1e-5f, "X follow must equal the old followLerp ease.");
            Assert.AreEqual(Mathf.Lerp(0f, 3f, a), stepped.z, 1e-5f, "Z follow must equal the old followLerp ease.");
        }

        // =================================================================================================
        // JUMP-PULL-BACK ROOT CAUSE (ticket 86caaqhj5). The earlier verticalFollowLerp split tracked the camera
        // TARGET's Y — but the target is the player ROOT, whose Y is CONSTANT through a jump (the arc is a local-Y
        // on the avatar CHILD). Diag-proven: the camera vertical response was 0.0000u while the avatar rose
        // ~0.80u → the camera NEVER followed the visual jump arc, and the constant horizontal follow-lag (0.41u in
        // the travel direction, symmetric across all 4 headings) read as a directional 'pulled back on jump'.
        // FIX: OrbitCamera.DesiredFollowY folds the avatar's live JumpHeight into the follow-point Y, so the
        // vertical follow has the REAL arc to track in EVERY direction. These pin the BUG CLASS (camera Y must
        // track the arc; the tracking is heading-independent), not one instance — with no scene/Time dependency.
        // =================================================================================================

        [Test]
        public void DesiredFollowY_TracksTheJumpArc_NotJustTheRootHeight()
        {
            // The player root sits at world Y=2 (some terrain height); the camera vertical offset is 1. At rest
            // (jumpHeight 0) the follow Y is the head (root+offset = 3). At the jump APEX (height 0.795) the
            // follow Y must RISE by exactly the arc height — so the camera tracks the visual jump, not a static Y.
            float rootY = 2f, offsetY = 1f;
            float atRest = OrbitCamera.DesiredFollowY(rootY, offsetY, 0f);
            float atApex = OrbitCamera.DesiredFollowY(rootY, offsetY, 0.795f);

            Assert.AreEqual(3f, atRest, 1e-5f, "at rest the follow Y is the root head (root+offset) — no arc term.");
            Assert.AreEqual(0.795f, atApex - atRest, 1e-5f,
                "the follow Y must RISE by the FULL jump-arc height at apex — else the camera never tracks the " +
                "visual jump (the arc is a local-Y on the avatar CHILD; the root target Y is constant through a " +
                "jump) and the constant horizontal follow-lag reads as the directional 'pulled back on jump' (86caaqhj5).");
        }

        [Test]
        public void JumpArcVerticalFollow_IsHeadingIndependent_NoWvsADSAsymmetry()
        {
            // THE asymmetry the Sponsor reported ("W works, A/D/S pull back on landing"). The jump-arc vertical
            // follow data is the avatar's JumpHeight — a SCALAR, with NO horizontal-direction dependence. So the
            // camera's vertical tracking of the arc MUST be byte-identical whether the player jumps moving forward
            // (W), back (S), or strafing (A/D). This pins that the fix removes the heading-dependence by
            // construction: DesiredFollowY only consumes the jump height, never the travel direction.
            const float rootY = 0.5f, offsetY = 1f, apex = 0.795f;
            float fW = OrbitCamera.DesiredFollowY(rootY, offsetY, apex);
            float fS = OrbitCamera.DesiredFollowY(rootY, offsetY, apex);
            float fA = OrbitCamera.DesiredFollowY(rootY, offsetY, apex);
            float fD = OrbitCamera.DesiredFollowY(rootY, offsetY, apex);
            Assert.AreEqual(fW, fS, 1e-6f, "vertical jump-arc follow must be identical forward vs back (no W/S asymmetry).");
            Assert.AreEqual(fW, fA, 1e-6f, "vertical jump-arc follow must be identical forward vs strafe-left (no W/A asymmetry).");
            Assert.AreEqual(fW, fD, 1e-6f, "vertical jump-arc follow must be identical forward vs strafe-right (no W/D asymmetry).");
        }

        [Test]
        public void DesiredFollowY_ClampsNegativeJumpHeightToZero_NeverDipsBelowHead()
        {
            // JumpHeight is ≥0 by contract (CastawayCharacter returns 0 when grounded), but guard defensively: a
            // stray negative must never pull the camera BELOW the head (which would itself read as a downward jerk).
            Assert.AreEqual(3f, OrbitCamera.DesiredFollowY(2f, 1f, -5f), 1e-5f,
                "a negative jump height must clamp to 0 — the camera follow Y never dips below the head.");
        }

        [Test]
        public void FollowStep_NeverOvershoots_AnyAxis_AndIsFiniteAtZeroDt()
        {
            // Each axis is an exponential approach (1 − e^(−rate·dt)) ∈ [0,1], so the result never overshoots the
            // target. At dt=0 nothing moves (the instant-snap path is the caller's `instant:true`, not this).
            Vector3 current = new Vector3(1f, 2f, -3f);
            Vector3 desired = new Vector3(5f, 0.84f, 4f);
            Vector3 s = OrbitCamera.FollowStep(current, desired, HorizLerp, VertLerp, Dt);
            // X rose toward 5 but not past it; Y dropped toward 0.84 but not past it; Z rose toward 4 not past.
            Assert.IsTrue(s.x >= current.x && s.x <= desired.x, "X must not overshoot the target.");
            Assert.IsTrue(s.y <= current.y && s.y >= desired.y, "Y must not overshoot the target.");
            Assert.IsTrue(s.z >= current.z && s.z <= desired.z, "Z must not overshoot the target.");

            Vector3 zero = OrbitCamera.FollowStep(current, desired, HorizLerp, VertLerp, 0f);
            Assert.AreEqual(current, zero, "at dt=0 the follow point does not move (no NaN, no jump).");
        }

        // =================================================================================================
        // CURSOR LOCK during RMB camera-orbit (ticket 86caatv7k). OrbitCamera.ResolveCursorForOrbit is the
        // pure, dependency-free edge decision: while RMB is HELD the cursor is Locked + hidden (so the orbit
        // drag can't walk the pointer out of the window); on RMB RELEASE it restores None + visible (so the
        // Sponsor can click menus / inventory / belt). The decision fires ONLY on a press/release EDGE — the
        // cursor is NEVER touched while RMB isn't held. These pin the BUG CLASS (cursor walks off-screen while
        // orbiting; cursor stays locked after release), not one instance — with no Input/Cursor dependency.
        // =================================================================================================

        // =================================================================================================
        // ATTEMPT 2 — HORIZONTAL follow VELOCITY FEED-FORWARD (the A/S/D jump-pull-back MECHANISM fix, 86caaqhj5).
        // The vertical fix (DesiredFollowY) made the camera track the jump arc Y, but A/S/D jumps STILL failed:
        // the HORIZONTAL follow lagged the player through the fast move+jump. A pure exponential follower trails
        // a constant-velocity target by a STEADY v/k (k=followLerp); the diag measured ~0.41u symmetric across
        // all 4 headings. The fix LEADS the X/Z follow target by velocity × leadTime, with leadTime = 1/k chosen
        // to EXACTLY cancel that steady-state lag — heading-independent by construction. These pin the BUG CLASS
        // (the led follow must track the player within tolerance in EVERY direction; the lag-cancel is symmetric),
        // with no scene/Time dependency. The PER-DIRECTION TRACE is the diagnose-via-trace evidence (brief req 1).
        // =================================================================================================

        // Production lead defaults (OrbitCamera.followLeadTime auto / maxLeadTime).
        private const float AutoLead = 0f;       // 0 = auto (= 1/followLerp)
        private const float MaxLead = 0.25f;

        [Test]
        public void EffectiveLeadTime_AutoEqualsOneOverFollowLerp_TheExactLagCancel()
        {
            // followLeadTime 0 means AUTO = 1/followLerp — the exact value that cancels an exponential follower's
            // steady-state lag (it lags v/k = v·(1/k); leading by v·(1/k) zeroes the net error).
            float lead = OrbitCamera.EffectiveLeadTime(AutoLead, HorizLerp, MaxLead);
            Assert.AreEqual(1f / HorizLerp, lead, 1e-6f,
                "auto lead (followLeadTime=0) must resolve to 1/followLerp — the exact steady-state-lag cancel.");
        }

        [Test]
        public void EffectiveLeadTime_ClampsRunawayConfiguredValue_NeverFlingsTheCamera()
        {
            // A dialed/serialized value beyond maxLeadTime must clamp — a runaway lead can never fling the cam.
            Assert.AreEqual(MaxLead, OrbitCamera.EffectiveLeadTime(99f, HorizLerp, MaxLead), 1e-6f,
                "an over-large configured lead must clamp to maxLeadTime.");
            Assert.AreEqual(0f, OrbitCamera.EffectiveLeadTime(0f, HorizLerp, 0f), 1e-6f,
                "maxLeadTime 0 disables the lead entirely (back to the plain lag-prone ease).");
        }

        [Test]
        public void HorizontalFollow_PerDirectionTrace_AllFourHeadingsTrackWithinTolerance()
        {
            // DIAGNOSE-VIA-TRACE (brief req 1): simulate a constant-velocity move + jump for each of W/A/S/D at
            // the WASD walk speed, with the velocity-feed-forward lead engaged, and assert the camera follow
            // point tracks the player's HORIZONTAL position within a tight tolerance in EVERY direction (the W-
            // only pass shipped before — this proves all 4 now track). The lag the Sponsor saw was ~0.41u; with
            // the lead-cancel the residual must be a small fraction of that. Also asserts the residual is
            // SYMMETRIC across headings (the bug was never direction-specific in the code).
            const float speed = 5.5f;                          // WASD walk speed
            (string name, Vector3 dir)[] headings =
            {
                ("W (forward)", new Vector3(0f, 0f, 1f)),
                ("S (back)",    new Vector3(0f, 0f, -1f)),
                ("A (left)",    new Vector3(-1f, 0f, 0f)),
                ("D (right)",   new Vector3(1f, 0f, 0f)),
            };

            float lead = OrbitCamera.EffectiveLeadTime(AutoLead, HorizLerp, MaxLead);
            float worst = 0f;
            var residuals = new System.Collections.Generic.List<float>();
            var trace = new System.Text.StringBuilder("[CameraFollowTrace] per-direction horizontal follow (lead="
                                                       + lead.ToString("F4") + "s):\n");

            foreach (var (name, dir) in headings)
            {
                Vector3 vel = dir * speed;
                Vector3 playerPos = Vector3.zero;             // the player's actual XZ (root)
                Vector3 follow = playerPos;                    // camera follow point starts on the player
                // Simulate ~1.0s of constant-velocity move+jump (60 frames) — long enough to reach the follower's
                // steady state (the regime the lag lives in). The jump Y is irrelevant to the HORIZONTAL track.
                for (int f = 0; f < 60; f++)
                {
                    playerPos += vel * Dt;
                    // The desired follow target = the player XZ, LED by the velocity feed-forward (the fix).
                    Vector3 desired = OrbitCamera.DesiredFollowXZ(playerPos, vel, lead);
                    follow = OrbitCamera.FollowStep(follow, desired, HorizLerp, VertLerp, Dt);
                }
                float residual = new Vector2(follow.x - playerPos.x, follow.z - playerPos.z).magnitude;
                residuals.Add(residual);
                worst = Mathf.Max(worst, residual);
                trace.AppendLine($"  {name,-12} vel={vel} residual={residual:F4}u");
            }
            UnityEngine.Debug.Log(trace.ToString());

            // (1) ALL 4 headings track within tolerance — the residual is a small fraction of the ~0.41u lag.
            Assert.Less(worst, 0.05f,
                $"every heading's horizontal follow residual must be < 0.05u (the ~0.41u lag is cancelled by the " +
                $"velocity lead); worst was {worst:F4}u. The W-only pass shipped before — all 4 must track now.");

            // (2) The residual is SYMMETRIC across headings (the lag was never direction-specific in the code).
            float min = residuals[0], max = residuals[0];
            foreach (var r in residuals) { min = Mathf.Min(min, r); max = Mathf.Max(max, r); }
            Assert.Less(max - min, 1e-4f,
                $"the horizontal follow residual must be identical across W/A/S/D (it consumes the velocity " +
                $"VECTOR, no per-direction branch); spread was {(max - min):F6}u.");
        }

        [Test]
        public void HorizontalFollow_WithoutLead_StillLags_ProvesTheLeadIsLoadBearing()
        {
            // The bug-class anchor: with NO lead (leadTime 0, no feed-forward applied), the same simulation
            // leaves a real steady-state lag ≈ v/k — proving the lead term is what fixes it (not the tighter
            // followLerp alone). This is the "could a wrong-version pass?" guard: it must FAIL to track without
            // the lead, so the passing per-direction trace above is meaningful.
            const float speed = 5.5f;
            Vector3 vel = new Vector3(1f, 0f, 0f) * speed;     // D strafe
            Vector3 playerPos = Vector3.zero, follow = Vector3.zero;
            for (int f = 0; f < 60; f++)
            {
                playerPos += vel * Dt;
                // NO lead — desired is the raw player XZ (the OLD lag-prone behaviour).
                Vector3 desired = OrbitCamera.DesiredFollowXZ(playerPos, Vector3.zero, 0f);
                follow = OrbitCamera.FollowStep(follow, desired, HorizLerp, VertLerp, Dt);
            }
            float residual = Mathf.Abs(follow.x - playerPos.x);
            // Steady-state lag of an exponential follower at constant v is v/k. At v=5.5, k=18 → ~0.31u.
            Assert.Greater(residual, 0.2f,
                $"WITHOUT the velocity lead the horizontal follow must still lag (got {residual:F4}u ≈ v/k) — " +
                "this proves the lead term (not just the tighter followLerp) is the load-bearing fix.");
        }

        [Test]
        public void CameraFollowNudgeTool_Activate_ForcesSiblingNudgePanelsOff_NoCrossFire()
        {
            // The F7 camera-follow panel shares adjust keys (PageUp/PageDown/T/G/Y/H) with the axe + world-look
            // panels, so activating it must force the siblings OFF — only one nudge panel ever active, no
            // cross-fire. EditMode component instances (no play loop needed — Activate/Deactivate are pure state
            // toggles). Cleaned up via DestroyImmediate (no PlayMode fixture).
            var axeGo = new GameObject("axe"); var axe = axeGo.AddComponent<AxeNudgeTool>();
            var worldGo = new GameObject("world"); var world = worldGo.AddComponent<WorldLookNudgeTool>();
            var camGo = new GameObject("camfollow"); var cam = camGo.AddComponent<CameraFollowNudgeTool>();
            try
            {
                axe.Activate();   // axe panel up first
                Assert.IsTrue(axe.IsActive, "precondition: the axe panel is active");

                cam.Activate();   // activating the camera panel must silence the axe panel
                Assert.IsTrue(cam.IsActive, "the camera-follow panel must be active after Activate()");
                Assert.IsFalse(axe.IsActive, "activating the camera-follow panel must force the axe panel OFF (no cross-fire)");

                // ...and the reverse: an axe/world activation silences the camera panel.
                world.Activate();
                Assert.IsFalse(cam.IsActive, "activating the world-look panel must force the camera-follow panel OFF");
            }
            finally
            {
                Object.DestroyImmediate(axeGo);
                Object.DestroyImmediate(worldGo);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void Cursor_OnRmbPressEdge_LocksAndHides()
        {
            bool orbiting = false;
            bool changed = OrbitCamera.ResolveCursorForOrbit(true, ref orbiting,
                out CursorLockMode lockState, out bool visible);
            Assert.IsTrue(changed, "the press edge (RMB down, was-not-orbiting) must signal a cursor change.");
            Assert.AreEqual(CursorLockMode.Locked, lockState, "RMB-held must LOCK the cursor (no off-screen walk).");
            Assert.IsFalse(visible, "RMB-held must HIDE the cursor while orbiting.");
            Assert.IsTrue(orbiting, "the orbiting flag must latch true on the press edge.");
        }

        [Test]
        public void Cursor_OnRmbReleaseEdge_RestoresFreeVisibleCursor()
        {
            bool orbiting = true; // mid-orbit
            bool changed = OrbitCamera.ResolveCursorForOrbit(false, ref orbiting,
                out CursorLockMode lockState, out bool visible);
            Assert.IsTrue(changed, "the release edge (RMB up, was-orbiting) must signal a cursor change.");
            Assert.AreEqual(CursorLockMode.None, lockState,
                "RMB-release must FREE the cursor so the Sponsor can click menus / inventory / belt.");
            Assert.IsTrue(visible, "RMB-release must show the cursor again.");
            Assert.IsFalse(orbiting, "the orbiting flag must clear on the release edge.");
        }

        [Test]
        public void Cursor_WhileRmbNotHeld_DoesNotTouchCursorState()
        {
            // The contract's load-bearing half: when RMB isn't being pressed/released this frame, the function
            // must NOT signal a change (so the caller leaves Cursor exactly as it is — a menu that freed the
            // cursor between orbits is never overridden). Two no-edge cases: idle (up, was-up) and held-steady.
            bool idleOrbiting = false;
            Assert.IsFalse(OrbitCamera.ResolveCursorForOrbit(false, ref idleOrbiting, out _, out _),
                "RMB up while not orbiting must NOT touch the cursor (no edge).");
            Assert.IsFalse(idleOrbiting, "no edge must leave the orbiting flag false.");

            bool heldOrbiting = true;
            Assert.IsFalse(OrbitCamera.ResolveCursorForOrbit(true, ref heldOrbiting, out _, out _),
                "RMB held-steady (already orbiting) must NOT re-signal a cursor change every frame.");
            Assert.IsTrue(heldOrbiting, "held-steady must leave the orbiting flag latched true.");
        }

        [Test]
        public void Cursor_FullPressHoldReleaseSequence_LocksOnceThenFreesOnce()
        {
            // Walk the real frame sequence: idle → press → hold → hold → release → idle. The lock must fire
            // exactly ONCE (press) and the free exactly ONCE (release); the hold + idle frames must be no-ops.
            bool orbiting = false;
            int changes = 0;

            // idle frame (RMB up)
            if (OrbitCamera.ResolveCursorForOrbit(false, ref orbiting, out _, out _)) changes++;
            Assert.AreEqual(0, changes, "no change on the leading idle frame.");

            // press frame (RMB down) → lock
            Assert.IsTrue(OrbitCamera.ResolveCursorForOrbit(true, ref orbiting, out var s1, out var v1));
            Assert.AreEqual(CursorLockMode.Locked, s1); Assert.IsFalse(v1); changes++;

            // two hold frames (RMB still down) → no change
            Assert.IsFalse(OrbitCamera.ResolveCursorForOrbit(true, ref orbiting, out _, out _));
            Assert.IsFalse(OrbitCamera.ResolveCursorForOrbit(true, ref orbiting, out _, out _));

            // release frame (RMB up) → free
            Assert.IsTrue(OrbitCamera.ResolveCursorForOrbit(false, ref orbiting, out var s2, out var v2));
            Assert.AreEqual(CursorLockMode.None, s2); Assert.IsTrue(v2); changes++;

            // trailing idle frame → no change
            Assert.IsFalse(OrbitCamera.ResolveCursorForOrbit(false, ref orbiting, out _, out _));

            Assert.AreEqual(2, changes, "exactly two cursor-state changes across the orbit: lock on press, free on release.");
            Assert.IsFalse(orbiting, "the orbiting flag returns to false after release.");
        }
    }
}
