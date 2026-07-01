using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the LEGACY dev/debug-overlay MASTER TOGGLE (ticket 86cafd6d6 — "assign a
    /// debug-overlay key"; the master key moved F1→F2 by the 86cabeqj9 soak NIT so F1 opens ONLY the dev
    /// console). The deliverable: the always-on LEGACY overlays (the "DEBUG — held weapon" panel that buried
    /// the #158 loot prompt, "AXE SHAFT LENGTH", "POND RECESS / FOAM") + the F7-F10 nudge panels all read ONE
    /// shared flag (DebugOverlays.Visible) and stay HIDDEN by default; F2 reveals the layer.
    ///
    /// F1/F2 DE-CONFLICT (86cabeqj9): the dev CONSOLE (SettingsPanel) keeps F1 and polls it DIRECTLY (no
    /// longer rides DebugOverlays.Visible), so F1 opens ONLY the console; the LEGACY layer here moved to F2.
    /// Before this they shared the flag, so one F1 popped BOTH. This file guards the LEGACY (F2) side.
    ///
    /// These guards pin the load-bearing contracts a HARNESS can verify (the harness can't synthesize a
    /// legacy F2 key-down, so the interactive reveal is Sponsor-driven in the shipped build — but the master
    /// key default + the default-hidden contract + the shared-flag wiring + the verify-capture-path lift ARE
    /// machine-checkable):
    ///
    ///   1. The master toggle defaults to F2 (NOT F1 — the de-conflict), so it can never re-couple with the
    ///      console's F1.
    ///   2. DEFAULT = HIDDEN across frames of normal play (AC2) — DebugOverlayToggle does NOT flip the flag
    ///      without F2, so a normal launch / soak / CI capture shows a clean screen (un-buries the loot prompt).
    ///   3. The flag drives the verify-capture path — FloatDiagnostic.ShowOverlay lifts the SAME master flag
    ///      (so the -verifyFloatDiag headless capture still renders its overlay even though F2 can't be pressed).
    /// </summary>
    public class DebugOverlayTogglePlayModeTests
    {
        private GameObject _bootGo;
        private DebugOverlayToggle _toggle;

        [SetUp]
        public void SetUp()
        {
            DebugOverlays.Hide();            // start from the hidden default
            _bootGo = new GameObject("Boot");
            _toggle = _bootGo.AddComponent<DebugOverlayToggle>();
        }

        // 86cabeqj9 de-conflict — the LEGACY master toggle must default to F2 (moved from F1) so F1 is free for
        // the dev console alone. A regression that re-set it to F1 would re-couple the two layers (the soak bug).
        [Test]
        public void LegacyMasterToggle_DefaultsToF2_NotF1()
        {
            Assert.AreEqual(KeyCode.F2, _toggle.toggleKey,
                "the legacy-overlay master toggle must default to F2 (the 86cabeqj9 F1/F2 de-conflict)");
            Assert.AreNotEqual(KeyCode.F1, _toggle.toggleKey,
                "the legacy-overlay master must NOT share F1 with the dev console (re-coupling = the soak bug)");
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootGo != null) Object.Destroy(_bootGo);
            DebugOverlays.Hide();
        }

        // AC2 — the master flag stays HIDDEN across many frames of normal play (no F2 synthesized). The
        // toggle component does NO gameplay work and does NOT reveal the layer on its own — the clean default
        // (and the #158 loot-prompt un-burial) holds for an entire normal soak.
        [UnityTest]
        public IEnumerator DevOverlays_StayHidden_InNormalPlay_WithoutF2()
        {
            for (int i = 0; i < 30; i++) yield return null; // normal play, no F2 key-down synthesized

            Assert.IsFalse(DebugOverlays.Visible,
                "the legacy dev/debug overlay layer must stay HIDDEN across normal play (AC2) — DebugOverlayToggle " +
                "must not reveal it without F2, so a soak / CI capture stays clean and the #158 loot prompt " +
                "is no longer buried");
        }

        // The shared flag is what reveals the legacy layer — Toggle() (what the F2 handler calls) flips it ON,
        // and every legacy overlay's OnGUI reads exactly this flag. Proves the wiring the F2 press drives.
        [UnityTest]
        public IEnumerator MasterFlag_Toggle_RevealsAndHidesTheLayer()
        {
            Assert.IsFalse(DebugOverlays.Visible, "precondition: hidden");
            DebugOverlays.Toggle();             // == one F2 press
            yield return null;
            Assert.IsTrue(DebugOverlays.Visible, "one F2 press (Toggle) must REVEAL the legacy overlay layer");
            DebugOverlays.Toggle();             // == a second F2 press
            yield return null;
            Assert.IsFalse(DebugOverlays.Visible, "a second F2 press (Toggle) must HIDE the layer again");
        }

        // The verify-capture path must lift the SAME master flag — FloatDiagnostic.ShowOverlay (used by the
        // -verifyFloatDiag headless capture, which can't press F2) reveals the layer so the overlay still
        // renders into the captured frame. Guards the cross-wiring between the gate and the capture path.
        [UnityTest]
        public IEnumerator FloatDiagnosticShowOverlay_LiftsTheMasterFlag_ForTheVerifyCapture()
        {
            Assert.IsFalse(DebugOverlays.Visible, "precondition: hidden (clean default)");
            var diag = _bootGo.AddComponent<FloatDiagnostic>();
            yield return null;

            // Normal play: the overlay is inert AND the master flag is down — no F8, no F2.
            Assert.IsFalse(diag.OverlayActive, "FloatDiagnostic overlay inert by default (its own _active gate)");
            Assert.IsFalse(DebugOverlays.Visible, "master flag still down before ShowOverlay");

            diag.ShowOverlay(); // the -verifyFloatDiag / F8 path (harness can't synth a key-down)
            // NOTE — ShowOverlay lifts DebugOverlays.Visible (the LEGACY F2 flag), unaffected by the F1/F2
            // de-conflict: FloatDiagnostic is a legacy overlay, so it still rides this flag (the console does not).
            yield return null;

            Assert.IsTrue(diag.OverlayActive,
                "ShowOverlay must flip the FloatDiagnostic overlay ON so the verify capture renders the GAP");
            Assert.IsTrue(DebugOverlays.Visible,
                "ShowOverlay must ALSO lift the F1 master gate — else the new master gate would suppress the " +
                "verify-capture frame even though the tool's own _active is on (the gate/capture cross-wire)");
        }
    }
}
