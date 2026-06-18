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
    }
}
