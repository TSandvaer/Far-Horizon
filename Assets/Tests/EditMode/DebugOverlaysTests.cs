using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode coverage for the shared LEGACY dev/debug overlay visibility flag (ticket 86cafd6d6 — "assign
    /// a debug-overlay key"; the master key moved F1→F2 by the 86cabeqj9 soak NIT so F1 opens ONLY the dev
    /// console). The flag is the ONE thing every LEGACY dev overlay reads to decide whether to draw; these
    /// guards pin its load-bearing contracts (the flag's Show/Hide/Toggle semantics are key-agnostic — only
    /// the master KEY moved, in DebugOverlayToggle):
    ///
    ///   1. DEFAULT = HIDDEN (AC2): a clean screen for normal play / soak / CI captures (also un-buries the
    ///      #158 loot prompt). A regression that flips the default to visible reds here.
    ///   2. Show / Hide / Toggle semantics: the F2 handler + the verify-capture path drive these.
    ///   3. The mandatory SubsystemRegistration reset re-seeds the HIDDEN default each play-entry (domain-
    ///      reload-disabled discipline — a stale "overlays on" must not survive a re-Play). This is also
    ///      enforced asmdef-wide by StaticStateResetTests; this asserts the concrete behaviour.
    /// </summary>
    public class DebugOverlaysTests
    {
        [SetUp]
        public void Reset() => DebugOverlays.Hide(); // each test starts from the hidden default

        // AC2: the layer is HIDDEN by default. The verify (a fresh SubsystemRegistration reset) re-seeds false,
        // so a clean launch shows NO dev overlays. This is the test the #158-collision fix rests on.
        [Test]
        public void Visible_DefaultsHidden()
        {
            InvokeReset();
            Assert.IsFalse(DebugOverlays.Visible,
                "the dev/debug overlay layer must default HIDDEN (AC2) — a normal launch / soak / CI capture " +
                "shows a clean screen; this also un-buries the #158 loot prompt the always-on overlay was burying");
        }

        // Show reveals, Hide conceals — the verify-capture path (FloatDiagnostic.ShowOverlay) drives Show.
        [Test]
        public void Show_RevealsLayer_Hide_ConcealsIt()
        {
            DebugOverlays.Show();
            Assert.IsTrue(DebugOverlays.Visible, "Show() must reveal the dev-overlay layer");
            DebugOverlays.Hide();
            Assert.IsFalse(DebugOverlays.Visible, "Hide() must conceal the dev-overlay layer");
        }

        // Toggle flips the layer — this is exactly what the F2 handler (DebugOverlayToggle) does each press.
        [Test]
        public void Toggle_FlipsVisibility()
        {
            Assert.IsFalse(DebugOverlays.Visible, "precondition: hidden");
            DebugOverlays.Toggle();
            Assert.IsTrue(DebugOverlays.Visible, "first F2 press reveals the legacy layer");
            DebugOverlays.Toggle();
            Assert.IsFalse(DebugOverlays.Visible, "second F2 press hides the legacy layer again");
        }

        // The mandatory per-play-entry reset must re-seed the HIDDEN default — a previous session's "overlays
        // on" must not leak into the next editor Play (domain-reload-disabled discipline, unity-conventions.md).
        [Test]
        public void SubsystemRegistrationReset_ReSeedsHiddenDefault()
        {
            DebugOverlays.Show();
            Assert.IsTrue(DebugOverlays.Visible, "precondition: visible before reset");
            InvokeReset();
            Assert.IsFalse(DebugOverlays.Visible,
                "the SubsystemRegistration reset must re-seed Visible=false so a stale 'overlays on' can't " +
                "survive a play-entry (the static-reset discipline; StaticStateResetTests audits this too)");
        }

        // Prove the reset method is actually wired with [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]
        // (the asmdef-wide StaticStateResetTests would also catch a missing one; this names the concrete method).
        [Test]
        public void DebugOverlays_HasSubsystemRegistrationReset()
        {
            var reset = typeof(DebugOverlays)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    return attr != null && attr.loadType == RuntimeInitializeLoadType.SubsystemRegistration;
                });
            Assert.IsNotNull(reset,
                "DebugOverlays must carry a [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] reset for its " +
                "mutable static Visible (domain-reload-disabled discipline)");
        }

        // Invoke the private SubsystemRegistration reset via reflection (it's the mechanism the editor fires
        // each play-entry; tests run with a fresh domain so we call it explicitly to assert its effect).
        private static void InvokeReset()
        {
            var reset = typeof(DebugOverlays)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .First(m =>
                {
                    var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    return attr != null && attr.loadType == RuntimeInitializeLoadType.SubsystemRegistration;
                });
            reset.Invoke(null, null);
        }
    }
}
