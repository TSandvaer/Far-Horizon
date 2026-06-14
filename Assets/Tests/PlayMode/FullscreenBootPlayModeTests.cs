using NUnit.Framework;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Coverage for FullscreenBoot's capture-gate-safety gating (ticket 86ca8ce6y SOAKFIX8 FIX3).
    ///
    /// FullscreenBoot forces borderless-native fullscreen on a NORMAL launch (so the Sponsor's double-click
    /// fills his widescreen, beating stale persisted Screenmanager registry values). The HARD constraint is
    /// that it must stay INERT on any capture/verify launch — the QA capture path launches the exe WINDOWED
    /// with -screen-fullscreen 0 and drives -captureGate / -verify* / -shot; if FullscreenBoot forced
    /// fullscreen there it would break EVERY windowed QA capture (serve_soak / capture_gate would no longer
    /// produce the 1280×720 windowed frames frame_check.py inspects).
    ///
    /// ShouldForceFullscreen is the pure decision the Start() path branches on; pinning it deterministically
    /// guards the safety contract without spawning a real player (no display/headless-time dependency). This
    /// is the bug class, not the instance: any new capture/verify flag that launches the exe windowed must be
    /// in the inert set, and a plain normal launch must force fullscreen.
    /// </summary>
    public class FullscreenBootPlayModeTests
    {
        // A normal Sponsor double-click (just the exe path, no flags) MUST force fullscreen.
        [Test]
        public void NormalLaunch_ForcesFullscreen()
        {
            Assert.IsTrue(FullscreenBoot.ShouldForceFullscreen(new[] { "FarHorizon.exe" }),
                "a normal launch (no capture/verify flags) must force borderless fullscreen — the Sponsor's " +
                "double-click case");
            Assert.IsTrue(FullscreenBoot.ShouldForceFullscreen(new string[0]),
                "an empty arg list must force fullscreen (defensive default = the normal case)");
            Assert.IsTrue(FullscreenBoot.ShouldForceFullscreen(null),
                "a null arg list must default to forcing fullscreen (never crash the normal path)");
        }

        // EVERY capture/verify launch MUST stay inert (the windowed capture must be preserved). This is the
        // capture-gate-safety contract — enumerate every flag that launches the exe for a windowed capture.
        [Test]
        public void CaptureAndVerifyLaunches_StayInert()
        {
            string[] inertFlags =
            {
                "-captureGate", "-shot",
                "-verifyMove", "-verifyCraft", "-verifyChop", "-verifyLoop",
                "-verifyAxe", "-verifyCastaway", "-verifyHair", "-verifySea", "-verifyRock",
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
