using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the F1 dev/debug-overlay MASTER TOGGLE (ticket 86cafd6d6 — "assign a F1 key
    /// for debug overlays"). The deliverable: the always-on dev overlays (the "DEBUG — held weapon" panel
    /// that buried the #158 loot prompt, "AXE SHAFT LENGTH", "POND RECESS / FOAM") + the F7-F10 nudge panels
    /// all read ONE shared flag (DebugOverlays.Visible) and stay HIDDEN by default; F1 reveals the layer.
    ///
    /// These guards pin the load-bearing contracts a HARNESS can verify (the harness can't synthesize a
    /// legacy F1 key-down, so the interactive reveal is Sponsor-driven in the shipped build — but the
    /// default-hidden contract + the shared-flag wiring + the verify-capture-path lift ARE machine-checkable):
    ///
    ///   1. DEFAULT = HIDDEN across frames of normal play (AC2) — DebugOverlayToggle does NOT flip the flag
    ///      without F1, so a normal launch / soak / CI capture shows a clean screen (un-buries the loot prompt).
    ///   2. The flag drives the verify-capture path — FloatDiagnostic.ShowOverlay lifts the SAME master flag
    ///      (so the -verifyFloatDiag headless capture still renders its overlay even though F1 can't be pressed).
    /// </summary>
    public class DebugOverlayTogglePlayModeTests
    {
        private GameObject _bootGo;

        [SetUp]
        public void SetUp()
        {
            DebugOverlays.Hide();            // start from the hidden default
            _bootGo = new GameObject("Boot");
            _bootGo.AddComponent<DebugOverlayToggle>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootGo != null) Object.Destroy(_bootGo);
            DebugOverlays.Hide();
        }

        // AC2 — the master flag stays HIDDEN across many frames of normal play (no F1 synthesized). The
        // toggle component does NO gameplay work and does NOT reveal the layer on its own — the clean default
        // (and the #158 loot-prompt un-burial) holds for an entire normal soak.
        [UnityTest]
        public IEnumerator DevOverlays_StayHidden_InNormalPlay_WithoutF1()
        {
            for (int i = 0; i < 30; i++) yield return null; // normal play, no F1 key-down synthesized

            Assert.IsFalse(DebugOverlays.Visible,
                "the dev/debug overlay layer must stay HIDDEN across normal play (AC2) — DebugOverlayToggle " +
                "must not reveal it without F1, so a soak / CI capture stays clean and the #158 loot prompt " +
                "is no longer buried");
        }

        // The shared flag is what reveals the layer — Toggle() (what the F1 handler calls) flips it ON, and
        // every dev overlay's OnGUI reads exactly this flag. Proves the wiring the F1 press drives.
        [UnityTest]
        public IEnumerator MasterFlag_Toggle_RevealsAndHidesTheLayer()
        {
            Assert.IsFalse(DebugOverlays.Visible, "precondition: hidden");
            DebugOverlays.Toggle();             // == one F1 press
            yield return null;
            Assert.IsTrue(DebugOverlays.Visible, "one F1 press (Toggle) must REVEAL the dev-overlay layer");
            DebugOverlays.Toggle();             // == a second F1 press
            yield return null;
            Assert.IsFalse(DebugOverlays.Visible, "a second F1 press (Toggle) must HIDE the layer again");
        }

        // The verify-capture path must lift the SAME master flag — FloatDiagnostic.ShowOverlay (used by the
        // -verifyFloatDiag headless capture, which can't press F1) reveals the layer so the overlay still
        // renders into the captured frame. Guards the cross-wiring between the gate and the capture path.
        [UnityTest]
        public IEnumerator FloatDiagnosticShowOverlay_LiftsTheMasterFlag_ForTheVerifyCapture()
        {
            Assert.IsFalse(DebugOverlays.Visible, "precondition: hidden (clean default)");
            var diag = _bootGo.AddComponent<FloatDiagnostic>();
            yield return null;

            // Normal play: the overlay is inert AND the master flag is down — no F8, no F1.
            Assert.IsFalse(diag.OverlayActive, "FloatDiagnostic overlay inert by default (its own _active gate)");
            Assert.IsFalse(DebugOverlays.Visible, "master flag still down before ShowOverlay");

            diag.ShowOverlay(); // the -verifyFloatDiag / F8 path (harness can't synth a key-down)
            yield return null;

            Assert.IsTrue(diag.OverlayActive,
                "ShowOverlay must flip the FloatDiagnostic overlay ON so the verify capture renders the GAP");
            Assert.IsTrue(DebugOverlays.Visible,
                "ShowOverlay must ALSO lift the F1 master gate — else the new master gate would suppress the " +
                "verify-capture frame even though the tool's own _active is on (the gate/capture cross-wire)");
        }
    }
}
