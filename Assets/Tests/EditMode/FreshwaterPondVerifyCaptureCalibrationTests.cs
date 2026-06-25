using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// CALIBRATION guard for the FreshwaterPondVerifyCapture overhead SHORELINE-ANNULUS gate (ticket 86cadj4g7
    /// #130 ROUND 6). The round-5 gate FALSE-PASSED: its hard-coded 0.30..0.52 frame-radius band sat on OPEN
    /// WATER because the ~2.6–3.0u pond disc OVERFILLED the height-6/fov-40 overhead frame (Tess QA, run
    /// 28155640831 — the waterline + bowl-wall ring fell off the frame edges, so the band could never sample
    /// the shoreline and passed trivially at pale=0.000).
    ///
    /// These tests pin the FRAMING calibration so it can't silently drift back onto open water: the overhead
    /// camera height + FOV must put the pond WATERLINE (and the bowl wall out to PondBowlOuterRadius, where the
    /// raised-collar white ring lived) at a SAMPLABLE mid-frame normalized radius — specifically inside the
    /// gate's self-calibration anchor window [0.15, 0.75] (outside which the gate fails loud as mis-framed) and,
    /// tighter, in a comfortable mid-frame band with corner margin so the green collar is in frame for the
    /// blue→green waterline measurement. Pure math (WorldRadiusToFrameRNorm) — no scene / no swapchain — so it
    /// runs headlessly. This is the "guard the CALIBRATION, not just existence" regression Tess asked for: a
    /// future framing change that pushes the waterline out of the window (the round-5 bug class) goes RED here.
    /// </summary>
    public class FreshwaterPondVerifyCaptureCalibrationTests
    {
        // The pond's world geometry the gate must frame. ROUND 9 (ticket 86cadj4g7 — "STILL a walkable dry slope"):
        // the TWO-SEGMENT wall pushed the VISIBLE waterline OUT to PondWaterlineFillFraction (0.90) × the bowl mouth
        // (5.4u) ≈ 4.86u (the terrain-clip circle where the steep shore lip meets the water surface), NOT the
        // round-8 ~4.0u. The disc overshoots to ~5.0–7.2u but is submerged in the steep lip + terrain-occluded. The
        // waterline is a near-circle (the bowl wall is radially symmetric), so min≈max≈4.86u with only capture-noise
        // spread. PondBowlOuterRadius / PondWaterlineRadius are Editor-asmdef-only, so mirrored here as literals; if
        // the recess/bowl/fill-fraction re-tunes (which moves the waterline), revisit these (intentional coupling).
        private const float WaterlineRadiusMin = 4.70f;  // PondWaterlineRadius (≈4.86u) minus capture-noise margin
        private const float WaterlineRadiusMax = 5.00f;  // PondWaterlineRadius (≈4.86u) plus capture-noise margin
        private const float BowlWallOuterRadius = 5.4f;  // LowPolyZoneGen.PondBowlOuterRadius (the hole rim / collar mouth)
        private const float CollarFadeOuterRadius = 6.8f; // collar paint fade-end (~PondBowlOuterRadius + 1.4)

        // The gate's self-calibration anchor window: outside this the gate declares a framing FAIL (a wrong-
        // framing trivial pass — the round-5 bug). Must match CheckNoShorelineAnnulusRing's [0.15, 0.75] check.
        private const float AnchorLo = 0.15f;
        private const float AnchorHi = 0.75f;

        [Test]
        public void OverheadFraming_PutsWaterline_InsideTheGateAnchorWindow()
        {
            float rNormMin = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                WaterlineRadiusMin, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);
            float rNormMax = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                WaterlineRadiusMax, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);

            Assert.Greater(rNormMin, AnchorLo,
                $"the pond waterline (min {WaterlineRadiusMin}u) maps to rNorm {rNormMin:F3} which is BELOW the gate " +
                $"anchor floor {AnchorLo} — the disc fills almost the whole frame, the gate would read a mis-framing " +
                "FAIL (round-5 overfill bug class). Lower the overhead camera / narrow the FOV.");
            Assert.Less(rNormMax, AnchorHi,
                $"the pond waterline (max {WaterlineRadiusMax}u) maps to rNorm {rNormMax:F3} which is ABOVE the gate " +
                $"anchor ceiling {AnchorHi} — the disc is a tiny speck, the waterline scan would miss it. Raise the " +
                "camera / widen the FOV.");
        }

        [Test]
        public void OverheadFraming_KeepsWaterlineMidFrame_WithCornerMarginForTheCollar()
        {
            // Tighter than the anchor window: the waterline should sit comfortably mid-frame so the green collar
            // (out to the fade-end) is IN frame for the blue→green waterline measurement, AND the corners stay
            // clear of the disc (so the radial coverage scan sees the disc give way to collar/grass). The fade-end
            // collar must land below rNorm ~0.9 (in frame, off the very corners which the scan skips at rMax 0.95).
            float waterlineMid = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                (WaterlineRadiusMin + WaterlineRadiusMax) * 0.5f,
                FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);
            Assert.That(waterlineMid, Is.InRange(0.25f, 0.50f),
                $"the waterline midpoint maps to rNorm {waterlineMid:F3}; it should sit mid-frame (0.25..0.50) so the " +
                "self-calibrating annulus brackets it without reaching the far grass or the disc centre.");

            float collarFadeRNorm = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                CollarFadeOuterRadius, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);
            Assert.Less(collarFadeRNorm, 0.95f,
                $"the collar fade-end ({CollarFadeOuterRadius}u) maps to rNorm {collarFadeRNorm:F3}; the green collar " +
                "must be IN frame (< 0.95, the scan's rMax) so the waterline blue→green transition is measurable.");
        }

        [Test]
        public void OverheadFraming_BowlWall_LandsAroundTheWaterlineRing_WhereTheWhiteRingLived()
        {
            // The raised-collar white ring draped the bowl wall (world ~waterline..PondBowlOuterRadius). With the
            // ±0.10 self-calibration band around the measured waterline, the bowl-wall ring must overlap that band
            // — i.e. the bowl wall's frame-radius span must reach into [waterline-0.10, waterline+0.10] so a real
            // ring WOULD light the sampled band. This is what makes the gate able to FAIL on a white ring (vs the
            // round-5 open-water band that never could).
            float waterlineMin = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                WaterlineRadiusMin, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);
            float bowlWall = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                BowlWallOuterRadius, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);

            // The sampled band is [waterline-0.10, waterline+0.10]. The bowl-wall ring spans [waterlineMin, bowlWall].
            // They must overlap: bowlWall must reach at/above the band's inner edge, and the band's outer edge must
            // reach at/above the waterline. Concretely the bowl wall starts AT the waterline so overlap is given as
            // long as bowlWall > (waterlineMin - 0.10); assert the wall extends meaningfully past the inner edge.
            Assert.Greater(bowlWall, waterlineMin,
                "the bowl wall must extend OUTWARD from the waterline in frame (a ring has radial extent to sample).");
            Assert.Greater(bowlWall, waterlineMin - 0.10f + 0.01f,
                $"the bowl-wall ring (rNorm {waterlineMin:F3}..{bowlWall:F3}) must overlap the gate's sampled band " +
                $"[waterline±0.10] so a white shoreline ring WOULD light the band — otherwise the gate can't FAIL " +
                "on a ring (the round-5 open-water trivial-pass bug class).");
        }

        // === COUPLING GUARD (ticket 86cadj4g7 #130 ROUND 8) — the mirrored bowl-mouth literals track the source ==
        // PondBowlOuterRadius is Editor-asmdef-only, so it is MIRRORED as a literal in TWO Runtime-visible spots:
        // (1) this test's BowlWallOuterRadius, and (2) FreshwaterPondVerifyCapture's `bowlMouthWorld = 5.4f` const
        // (the FILL-TO-RIM gate's denominator). EditMode sees both asmdefs, so pin the test mirror to the real
        // source here — if the bowl mouth re-tunes and someone forgets a mirror, this reds. (The capture's own const
        // is documented to track this same value; this guard is the cheap reminder the mirrors exist.)
        [Test]
        public void MirroredBowlMouthLiteral_MatchesTheEditorSourceConstant()
        {
            Assert.That(BowlWallOuterRadius, Is.EqualTo(FarHorizon.EditorTools.LowPolyZoneGen.PondBowlOuterRadius).Within(1e-4f),
                $"the mirrored bowl-mouth literal ({BowlWallOuterRadius}) must equal the source " +
                $"LowPolyZoneGen.PondBowlOuterRadius ({FarHorizon.EditorTools.LowPolyZoneGen.PondBowlOuterRadius}); " +
                "if it drifted, ALSO update FreshwaterPondVerifyCapture.bowlMouthWorld (the fill-to-rim gate denominator).");
        }

        // The real waterline (Editor source) must land where the calibration constants assume it (the visible
        // shoreline the overhead gate frames). Pins the round-8 fill-to-rim geometry: a recess/bowl re-tune that
        // moved the waterline out of [WaterlineRadiusMin, WaterlineRadiusMax] reds here → revisit the disc radius +
        // these constants together.
        [Test]
        public void RealWaterlineRadius_LandsInTheCalibrationWindow()
        {
            float wl = FarHorizon.EditorTools.LowPolyZoneGen.PondWaterlineRadius;
            Assert.That(wl, Is.InRange(WaterlineRadiusMin, WaterlineRadiusMax),
                $"the solved PondWaterlineRadius ({wl:F3}u) must sit in the calibration window " +
                $"[{WaterlineRadiusMin}, {WaterlineRadiusMax}]; a drift means the recess/bowl re-tuned — re-check " +
                "PondSurfaceRadius (the disc must still fill to this waterline) and these constants.");
        }

        [Test]
        public void WorldRadiusToFrameRNorm_IsCorrectAtTheFrameEdge()
        {
            // Sanity on the pure helper: at rNorm 1.0 the world radius == height*tan(halfFov). For height 18, fov 50
            // (half 25°, tan ≈ 0.46631), that's ≈ 8.394u. A radius of that value must map to ≈ 1.0.
            float edgeWorld = FreshwaterPondVerifyCapture.OverheadHeight *
                              Mathf.Tan(FreshwaterPondVerifyCapture.OverheadFov * 0.5f * Mathf.Deg2Rad);
            float rNorm = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                edgeWorld, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);
            Assert.That(rNorm, Is.EqualTo(1.0f).Within(0.001f),
                "WorldRadiusToFrameRNorm(height*tan(halfFov)) must equal 1.0 (the short-axis frame edge).");

            // Linear: half that radius → rNorm 0.5.
            float half = FreshwaterPondVerifyCapture.WorldRadiusToFrameRNorm(
                edgeWorld * 0.5f, FreshwaterPondVerifyCapture.OverheadHeight, FreshwaterPondVerifyCapture.OverheadFov);
            Assert.That(half, Is.EqualTo(0.5f).Within(0.001f), "the mapping is linear in world radius.");
        }
    }
}
