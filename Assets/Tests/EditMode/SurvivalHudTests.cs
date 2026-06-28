using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the U2-5 survival HUD's pure presentation math (ticket 86ca8bdge).
    ///
    /// The HUD's only non-trivial logic is the 0..1 warmth -> filled-segment-count mapping and the
    /// gold->coal-red band selection — both are STATIC + deterministic so they're proven fast in
    /// headless CI with no scene/OnGUI. The PINNED rounding rule is FLOOR for the LOWER segments
    /// (Tess PR #9 nit (a)) — a segment lights only when its full 1/10th share is earned — with a
    /// deliberate TOP-segment near-full policy (86cafc6ty): the 10th segment lights at Current01 >= 0.95
    /// so a continuously-decaying full need shows + HOLDS 10/10 instead of capping at 9/10. Boundary
    /// asserts are placed at non-half points so the floor decision is exact and unambiguous. The COMPLEMENTARY
    /// PlayMode test (SurvivalHudPlayModeTests) proves the HUD reflects LIVE warmth/inventory through
    /// a real scene. Together they catch the bug CLASS: a segment-fill that drifts off the pinned
    /// rule, or a HUD that paints the wrong band / stops tracking the model.
    /// </summary>
    public class SurvivalHudTests
    {
        // === Segment count is pinned at EXACTLY 10 (Uma spec §3 "~10" -> integer; Tess PR #9 nit (c)) ===
        [Test]
        public void SegmentCount_IsExactlyTen()
        {
            Assert.AreEqual(10, SurvivalHud.SegmentCount,
                "the warmth bar is pinned to exactly 10 segments (spec §3 '~10 segments' resolved to 10)");
        }

        [Test]
        public void PlateAlpha_MatchesBootHudFamily()
        {
            Assert.AreEqual(0.55f, SurvivalHud.PlateAlpha, 0.0001f,
                "plate alpha is pinned to 0.55 — BootHud's build-stamp-plate family (amends spec's 0.45)");
        }

        // === FLOOR rounding rule for the LOWER segments (pinned, Tess PR #9 nit (a)) ================
        // filled = Floor(Current01 * 10), clamped 0..10, for every segment BELOW the top. A lower segment
        // lights only when fully earned — a 3.4/10 need MUST read 3/10, never 4/10 (86cafc6ty AC2). The TOP
        // segment is the deliberate exception, covered by the near-full-policy test below.
        [TestCase(0.00f, 0)]   // freezing -> all charcoal
        [TestCase(0.04f, 0)]   // below 1/10th -> still 0 lit (FLOOR, not round-up)
        [TestCase(0.09f, 0)]   // just under one segment -> 0 (the floor distinction vs round)
        [TestCase(0.10f, 1)]   // exactly 1/10th earned -> 1 lit
        [TestCase(0.15f, 1)]   // mid-segment -> FLOOR keeps it at 1 (round would give 2 — proves floor)
        [TestCase(0.19f, 1)]   // just under 2/10ths -> still 1
        [TestCase(0.34f, 3)]   // 0.34*10 = 3.4 -> floor 3 (the AC2 "must not read 4/10" case)
        [TestCase(0.55f, 5)]   // 0.55*10 = 5.5 -> floor 5 (round-half would ambiguate; floor is exact)
        [TestCase(0.89f, 8)]   // 0.89*10 = 8.9 -> floor 8 (well below the top threshold — pure floor)
        [TestCase(0.94f, 9)]   // 86cafc6ty AC4: just under the 0.95 top threshold -> 9 (NOT promoted)
        public void FilledSegments_LowerSegments_FollowThePinnedFloorRule(float current01, int expectedLit)
        {
            Assert.AreEqual(expectedLit, SurvivalHud.FilledSegments(current01),
                $"FilledSegments({current01}) must FLOOR to {expectedLit} lit segments (pinned lower-band rule)");
        }

        // === Top-segment near-full policy (86cafc6ty) — the 9/10-cap fix ============================
        // ROOT CAUSE: every SurvivalNeed decays continuously, so a JUST-satisfied full need drops to ~0.996
        // within a frame; a pure FLOOR(9.96)=9 meant the 10th segment was essentially NEVER visible (the
        // Sponsor-soaked "hunger never reaches 10/10, always caps at 9/10"). FIX: light the top segment at
        // Current01 >= 0.95 (TopSegmentThreshold). The threshold is strictly ABOVE 0.94 (so 0.94 -> 9, AC4)
        // and well above the post-satisfy decay value (so a full need HOLDS 10/10 across the window, AC1 —
        // at the shipped 0.0055/sec drop the need stays >= 0.95 for ~9s, not a one-frame flash).
        [TestCase(0.949f, 9)]  // a hair under the threshold -> still 9 (lower-band FLOOR governs)
        [TestCase(0.95f, 10)]  // AT the threshold -> the top segment lights: 10/10
        [TestCase(0.96f, 10)]  // above the threshold -> 10
        [TestCase(0.99f, 10)]  // the post-satisfy decay band (was 9 under pure FLOOR) -> now 10 (the fix)
        [TestCase(0.996f, 10)] // the exact ~one-frame-after-satisfy value -> 10 (holds, no flash)
        [TestCase(1.00f, 10)]  // full -> all 10 lit
        public void FilledSegments_TopSegment_LightsAtNearFull(float current01, int expectedLit)
        {
            Assert.AreEqual(expectedLit, SurvivalHud.FilledSegments(current01),
                $"FilledSegments({current01}) must light the top segment at/above 0.95 (near-full policy) = {expectedLit}");
        }

        [Test]
        public void TopSegmentThreshold_IsAbovePointNineFour_SoPointNineFourStillReadsNine()
        {
            // The threshold MUST be strictly above 0.94 (AC4: a 0.94 need reads 9/10) AND at/below the
            // post-satisfy decay value (AC1: a just-full need holds 10/10). 0.95 satisfies both.
            Assert.Greater(SurvivalHud.TopSegmentThreshold, 0.94f,
                "the top-segment threshold must be ABOVE 0.94 so a 0.94 need still reads 9/10 (86cafc6ty AC4)");
            Assert.LessOrEqual(SurvivalHud.TopSegmentThreshold, 0.99f,
                "the threshold must be at/below the post-satisfy decay band so a full need HOLDS 10/10 (AC1)");
        }

        [Test]
        public void FilledSegments_ClampsOutOfRangeInput()
        {
            Assert.AreEqual(0, SurvivalHud.FilledSegments(-0.5f), "negative warmth clamps to 0 lit segments");
            Assert.AreEqual(10, SurvivalHud.FilledSegments(1.5f), "over-full warmth clamps to 10 lit segments");
        }

        // === Empties RIGHT-TO-LEFT (spec §3): more warmth => more lit segments, monotonic ==========
        [Test]
        public void FilledSegments_IsMonotonicInWarmth()
        {
            int prev = -1;
            for (int i = 0; i <= 20; i++)
            {
                float c = i / 20f;
                int lit = SurvivalHud.FilledSegments(c);
                Assert.GreaterOrEqual(lit, prev,
                    "filled-segment count must never DECREASE as warmth rises (bar empties right-to-left)");
                prev = lit;
            }
        }

        // === Band color mapping (spec §3): warm gold >=0.60, dusk orange 0.30..0.60, coal red <0.30 =
        private static readonly Color WarmGold   = new Color(0.91f, 0.70f, 0.36f);
        private static readonly Color DuskOrange = new Color(0.85f, 0.54f, 0.31f);
        private static readonly Color CoalRed    = new Color(0.71f, 0.34f, 0.24f);

        [TestCase(1.00f)]
        [TestCase(0.60f)]   // band boundary: >= 0.60 is warm
        public void BandColor_WarmBand_IsEmberGold(float c)
        {
            AssertColor(WarmGold, SurvivalHud.BandColor(c), $"warmth {c} must read warm ember gold");
        }

        [TestCase(0.59f)]
        [TestCase(0.30f)]   // band boundary: >= 0.30 (and < 0.60) is cooling
        public void BandColor_CoolingBand_IsDuskOrange(float c)
        {
            AssertColor(DuskOrange, SurvivalHud.BandColor(c), $"warmth {c} must read dusk orange (cooling)");
        }

        [TestCase(0.29f)]
        [TestCase(0.00f)]   // cold band: < 0.30 is coal red
        public void BandColor_ColdBand_IsCoalRed(float c)
        {
            AssertColor(CoalRed, SurvivalHud.BandColor(c), $"warmth {c} must read coal red (cold, NOT alarm red)");
        }

        [Test]
        public void BandColors_AreAllSubOne_HdrClampSafe()
        {
            foreach (float c in new[] { 1f, 0.5f, 0.1f })
            {
                Color col = SurvivalHud.BandColor(c);
                Assert.LessOrEqual(col.r, 1f, "R channel must be sub-1.0 (HDR-clamp-safe, spec §5)");
                Assert.LessOrEqual(col.g, 1f, "G channel must be sub-1.0 (HDR-clamp-safe, spec §5)");
                Assert.LessOrEqual(col.b, 1f, "B channel must be sub-1.0 (HDR-clamp-safe, spec §5)");
                // No pure-saturated alarm red: the cold band is a muted coal red, never #FF0000.
                Assert.Less(col.r, 0.95f, "no pure-saturated alarm red — the cold state is a dying-ember red");
            }
        }

        private static void AssertColor(Color expected, Color actual, string msg)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, msg + " (R)");
            Assert.AreEqual(expected.g, actual.g, 0.001f, msg + " (G)");
            Assert.AreEqual(expected.b, actual.b, 0.001f, msg + " (B)");
        }
    }
}
