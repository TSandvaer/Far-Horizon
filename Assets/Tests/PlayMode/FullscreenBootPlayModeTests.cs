using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Coverage for FullscreenBoot's capture-gate-safety gating (ticket 86ca8ce6y SOAKFIX8 FIX3; window mode
    /// flipped to WINDOWED for BIG ROUND ISLAND N3, 86ca9a7qn).
    ///
    /// FullscreenBoot forces WINDOWED mode on a NORMAL launch (the Sponsor's N3 request — a normal window, NOT
    /// borderless fullscreen — beating any stale persisted Screenmanager fullscreen registry value). The HARD
    /// constraint is unchanged: it must stay INERT on any capture/verify launch — the QA capture path launches
    /// the exe WINDOWED with -screen-fullscreen 0 and drives -captureGate / -verify* / -shot; if FullscreenBoot
    /// re-asserted its own window size there it would override EVERY windowed QA capture (serve_soak /
    /// capture_gate would no longer produce the 1280×720 windowed frames frame_check.py inspects).
    ///
    /// ShouldForceFullscreen is the pure decision the Start() path branches on (its NAME is historic — it now
    /// gates "force the windowed mode"); pinning it deterministically guards the safety contract without
    /// spawning a real player. This is the bug class, not the instance: any new capture/verify flag that
    /// launches the exe windowed must be in the inert set, and a plain normal launch must force the window mode.
    /// </summary>
    public class FullscreenBootPlayModeTests
    {
        // A normal Sponsor double-click (just the exe path, no flags) MUST force the (windowed) mode.
        [Test]
        public void NormalLaunch_ForcesFullscreen()
        {
            Assert.IsTrue(FullscreenBoot.ShouldForceFullscreen(new[] { "FarHorizon.exe" }),
                "a normal launch (no capture/verify flags) must force the windowed mode — the Sponsor's " +
                "double-click case (N3)");
            Assert.IsTrue(FullscreenBoot.ShouldForceFullscreen(new string[0]),
                "an empty arg list must force the window mode (defensive default = the normal case)");
            Assert.IsTrue(FullscreenBoot.ShouldForceFullscreen(null),
                "a null arg list must default to forcing the window mode (never crash the normal path)");
        }

        // The default windowed size must be a sane 16:9 window (not zero / not fullscreen-native). N3.
        [Test]
        public void WindowedDefaultSize_IsSane16x9()
        {
            Assert.AreEqual(1600, FullscreenBoot.WindowedWidth, "default windowed width must be 1600 (N3)");
            Assert.AreEqual(900, FullscreenBoot.WindowedHeight, "default windowed height must be 900 (N3)");
        }

        // EVERY capture/verify launch MUST stay inert (the windowed capture size must be preserved). This is
        // the capture-gate-safety contract — enumerate every flag that launches the exe for a windowed capture.
        [Test]
        public void CaptureAndVerifyLaunches_StayInert()
        {
            string[] inertFlags =
            {
                "-captureGate", "-shot",
                "-verifyMove", "-verifyCraft", "-verifyChop", "-verifyLoop",
                "-verifyAxe", "-verifyAxeFacings", "-verifyCastaway", "-verifyHair", "-verifySea", "-verifyRock",
                "-verifyIsland", "-verifyWorldLook", "-verifyWalkGround", "-verifyFloatDiag",
                "-screen-fullscreen",
            };
            foreach (string flag in inertFlags)
            {
                // The capture gate launches like: FarHorizon.exe -screen-fullscreen 0 -captureGate ...
                Assert.IsFalse(
                    FullscreenBoot.ShouldForceFullscreen(new[] { "FarHorizon.exe", "-screen-fullscreen", "0", flag }),
                    $"a launch carrying '{flag}' must stay INERT (NOT force fullscreen) so the windowed QA " +
                    "capture is preserved — forcing fullscreen here breaks serve_soak / capture_gate frames");
                // And the flag alone (without -screen-fullscreen) must also gate off.
                Assert.IsFalse(FullscreenBoot.ShouldForceFullscreen(new[] { "FarHorizon.exe", flag }),
                    $"a launch carrying '{flag}' must stay inert regardless of other flags");
            }
        }
    }
}
