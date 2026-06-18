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
        // Production defaults (OrbitCamera.followLerp / verticalFollowLerp).
        private const float HorizLerp = 12f;
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
        public void HorizontalFollow_FeelIsUnchanged_MatchesTheOldSmoothRate()
        {
            // The X/Z follow must be EXACTLY the old single-rate smooth ease (followLerp) — the fix only changes
            // the vertical axis, so the ground walk/run camera feel is byte-identical to before.
            Vector3 current = new Vector3(0f, 0f, 0f);
            Vector3 desired = new Vector3(2f, 0f, 3f);
            Vector3 stepped = OrbitCamera.FollowStep(current, desired, HorizLerp, VertLerp, Dt);

            float a = 1f - Mathf.Exp(-HorizLerp * Dt);
            Assert.AreEqual(Mathf.Lerp(0f, 2f, a), stepped.x, 1e-5f, "X follow must equal the old followLerp ease.");
            Assert.AreEqual(Mathf.Lerp(0f, 3f, a), stepped.z, 1e-5f, "Z follow must equal the old followLerp ease.");
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
