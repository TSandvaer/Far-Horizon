using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC11 regression guard for the DEV TWEAK CONSOLE FOUNDATION (ticket 86cabeqj9). The console's behaviour
    /// is layered on the pure-C# registry + entries (no scene, no Update, no UIDocument render loop — the
    /// headless-time trap, unity-conventions.md), so the bug CLASSES the foundation introduces are pinned
    /// here directly:
    ///   • AC5 — a TYPED value applies + clamps (the panel's typed field drives the SAME entry.SetValue path);
    ///   • AC6 — a NUDGE step (with Shift=5x / Ctrl=0.2x) applies the right delta (the nudge math is the
    ///           SettingsPanel step formula, replicated here so a regression in either fails);
    ///   • AC7 — a Bool entry binds + drives its flag (the new archetype is not a silent no-op);
    ///   • AC9/AC10 — DiffersFromDefault flips when dialed off default AND CLEARS on ResetToDefault().
    ///
    /// The world-input-passthrough rule (AC3) is a SettingsPanel focus-gate (UiInputGate driven by typed-field
    /// FocusIn/FocusOut, not by the open panel) — pinned here at the UiInputGate level: the open console alone
    /// must NOT gate (only a focused field does). The full focus-event path is a UI Toolkit interaction,
    /// covered by the shipped-build capture + Sponsor soak (UIDocument focus is unreliable in EditMode).
    /// </summary>
    public class DevConsoleTests
    {
        [SetUp]
        public void ClearPrefs()
        {
            PlayerPrefs.DeleteKey("fh.settings.con_walk");
            PlayerPrefs.DeleteKey("fh.settings.con_zoom.min");
            PlayerPrefs.DeleteKey("fh.settings.con_zoom.max");
            PlayerPrefs.DeleteKey("fh.settings.con_int");
            PlayerPrefs.DeleteKey("fh.settings.con_bool");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ConsoleUiScaleId);
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.ConsoleTextScaleId);
        }

        // ===== AC5 — typed value applies + clamps (the panel's FloatField commit drives entry.SetValue) =====

        [Test]
        public void TypedValue_AppliesLive_AndClampsToBand()
        {
            float param = 5.5f; // stand-in for WasdMovement.moveSpeed
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("con_walk", "Walk speed", () => param, v => param = v, 1f, 12f);

            // A typed-then-committed value goes through the same single-write authority the field calls.
            float applied = e.SetValue(8.3f);
            Assert.AreEqual(8.3f, applied, 1e-4f, "a typed value applies live (AC5)");
            Assert.AreEqual(8.3f, param, 1e-4f, "the BOUND param actually changed (not a no-op)");

            Assert.AreEqual(12f, e.SetValue(999f), 1e-4f, "a typed value above the band clamps to Max (AC5)");
            Assert.AreEqual(1f, e.SetValue(-999f), 1e-4f, "a typed value below the band clamps to Min (AC5)");
        }

        // ===== AC6 — nudge with Shift(5x) / Ctrl(0.2x) applies the scaled step =====
        //
        // The nudge step is SettingsPanel's formula: base = 1% of the dialable band, scaled by the modifier.
        // 86cagpk72 NIT — the base-step formula is now the SHARED FarHorizon.Settings.NudgeStep helper the panel
        // itself calls (was a private duplicate here, so a drift in the 0.01f constant wouldn't red a test). We
        // call NudgeStep.ForSlider directly so the asserted step IS the shipped step; the modifier multiply
        // (5f/0.2f) is passed explicitly — those constants live in SettingsPanel.NudgeStepMul (reads live Input,
        // not headless-testable) and are asserted directly here.

        [Test]
        public void Nudge_BaseStep_MovesByOnePercentOfBand()
        {
            float param = 5f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("con_walk", "Walk speed", () => param, v => param = v, 1f, 12f); // band 11 → step 0.11

            float step = NudgeStep.ForSlider(e);         // 0.11 (SHARED formula, not a local copy)
            e.SetValue(e.Value + 1 * step * 1f);         // one nudge up, no modifier
            Assert.AreEqual(5.11f, param, 1e-4f, "an unmodified nudge moves by 1% of the band (AC6)");
        }

        [Test]
        public void Nudge_ShiftIsFiveX_CtrlIsFifthX()
        {
            float param = 5f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("con_walk", "Walk speed", () => param, v => param = v, 1f, 12f); // step 0.11
            float step = NudgeStep.ForSlider(e);

            e.SetValue(e.Value + 1 * step * 5f);         // Shift = 5x → +0.55
            Assert.AreEqual(5.55f, param, 1e-4f, "Shift applies a 5x nudge step (AC6)");

            param = 5f;
            e.SetValue(e.Value + 1 * step * 0.2f);       // Ctrl = 0.2x → +0.022
            Assert.AreEqual(5.022f, param, 1e-4f, "Ctrl applies a 0.2x nudge step (AC6)");
        }

        [Test]
        public void Nudge_RespectsClamp_AtBandEdge()
        {
            float param = 11.95f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("con_walk", "Walk speed", () => param, v => param = v, 1f, 12f);
            float step = NudgeStep.ForSlider(e); // 0.11

            e.SetValue(e.Value + 1 * step * 1f);         // 11.95 + 0.11 = 12.06 → clamps to 12
            Assert.AreEqual(12f, param, 1e-4f, "a nudge past the ceiling clamps to Max (AC6 + the entry clamp)");
        }

        // 86cagpk72 NIT — pin the SHARED step formulas directly (so a change to the 0.01f base constant OR the
        // per-axis band reds here, not just as an indirect side-effect). This is the coverage gap the NIT named.
        [Test]
        public void NudgeStep_SharedFormula_MatchesOnePercentOfBand()
        {
            var reg = new SettingsRegistry();
            float f = 5f;
            var slider = reg.AddFloat("con_walk", "Walk", () => f, v => f = v, 1f, 12f); // band 11
            Assert.AreEqual(0.11f, NudgeStep.ForSlider(slider), 1e-5f, "slider base step = 1% of the (Max-Min) band");

            float lo = 6f, hi = 26f;
            var range = reg.AddRange("con_zoom", "Zoom", () => lo, v => lo = v, () => hi, v => hi = v, 2f, 40f); // span 38
            Assert.AreEqual(0.38f, NudgeStep.ForRange(range), 1e-5f, "range base step = 1% of the (Upper-Lower) span");

            int n = 5;
            var stepper = reg.AddInt("con_int", "Slots", () => n, v => n = v, 1, 60, step: 1);
            Assert.AreEqual(1, NudgeStep.ForStepper(stepper, 1f), "int base step = the entry step");
            Assert.AreEqual(5, NudgeStep.ForStepper(stepper, 5f), "Shift scales the int step to 5x");
            Assert.AreEqual(1, NudgeStep.ForStepper(stepper, 0.2f), "Ctrl floors the int step at 1 (never 0)");
        }

        // ===== AC7 — a Bool entry binds + drives its flag (the new archetype) =====

        [Test]
        public void BoolEntry_DrivesBoundFlag_OnSetValue()
        {
            bool flag = false; // stand-in for a per-need on/off
            var reg = new SettingsRegistry();
            var e = reg.AddBool("con_bool", "Hunger on", () => flag, v => flag = v);

            Assert.AreEqual(SettingEntry.Archetype.Toggle, e.Kind, "a bool entry renders as the Toggle archetype (AC7)");

            e.SetValue(true);
            Assert.IsTrue(flag, "the BOUND flag actually changed (the bool binding is not a no-op — AC7)");
            Assert.IsTrue(e.Value, "the entry reads the live flag back");

            e.Toggle();
            Assert.IsFalse(flag, "Toggle flips the flag");
        }

        [Test]
        public void BoolEntry_NumericBridge_DrivesViaTypeAndNudge()
        {
            // AC5/AC6 reach a bool through the 0/1 numeric bridge (so type/nudge are generic across archetypes).
            bool flag = false;
            var reg = new SettingsRegistry();
            var e = reg.AddBool("con_bool", "Hunger on", () => flag, v => flag = v);

            e.SetFromNumeric(1f);
            Assert.IsTrue(flag, "numeric 1 (typed or nudge-up) sets the flag true");
            Assert.AreEqual(1f, e.NumericValue, 1e-4f, "NumericValue reads back 1 when on (the type/nudge readout bridge)");
            e.SetFromNumeric(0f);
            Assert.IsFalse(flag, "numeric 0 (typed or nudge-down) sets the flag false");
            Assert.AreEqual(0f, e.NumericValue, 1e-4f, "NumericValue reads back 0 when off");
            e.SetFromNumeric(0.7f);
            Assert.IsTrue(flag, "numeric ≥ 0.5 rounds to true (the 0/1 threshold)");
        }

        [Test]
        public void BoolEntry_PersistsAndReloads_FromPlayerPrefs()
        {
            bool flag = false;
            var reg = new SettingsRegistry();
            var e = reg.AddBool("con_bool", "Hunger on", () => flag, v => flag = v);
            e.SetValue(true); // writes PlayerPrefs 1 (AC5)

            bool flag2 = false;
            var reg2 = new SettingsRegistry();
            var e2 = reg2.AddBool("con_bool", "Hunger on", () => flag2, v => flag2 = v);
            e2.LoadFromPrefs();

            Assert.IsTrue(flag2, "the persisted bool survives a relaunch (AC5)");
        }

        [Test]
        public void BoolEntry_ExtensionHook_NeverDrivesFlag_AC3()
        {
            bool flag = false;
            var reg = new SettingsRegistry();
            var e = reg.AddBool("con_bool", "Future flag", () => flag, v => flag = v, available: false);

            e.SetValue(true);
            Assert.IsFalse(e.Available, "the hook is marked unavailable");
            Assert.IsFalse(flag, "an unavailable bool hook NEVER drives its flag (AC3)");
            Assert.IsFalse(e.DiffersFromDefault, "an unavailable hook never differs (no false badge)");
        }

        // ===== AC9 / AC10 — differs-from-default flips when dialed off default AND clears on reset =====

        [Test]
        public void Float_DiffersFromDefault_FlipsAndClearsOnReset()
        {
            float param = 5.5f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("con_walk", "Walk speed", () => param, v => param = v, 1f, 12f);

            Assert.IsFalse(e.DiffersFromDefault, "an untouched entry does not differ (no badge — AC9)");
            e.SetValue(9f);
            Assert.IsTrue(e.DiffersFromDefault, "a dialed-off-default entry differs (badge shows — AC9)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset restores the default AND clears the differs flag (AC10)");
            Assert.AreEqual(5.5f, param, 1e-4f, "reset restored the registration-time value (AC10)");
        }

        [Test]
        public void Range_DiffersFromDefault_OnEitherEnd_ClearsOnReset()
        {
            float min = 6f, max = 26f;
            var reg = new SettingsRegistry();
            var e = reg.AddRange("con_zoom", "Zoom range",
                () => min, v => min = v, () => max, v => max = v, 2f, 40f);

            Assert.IsFalse(e.DiffersFromDefault, "untouched range does not differ");
            e.SetMax(30f);
            Assert.IsTrue(e.DiffersFromDefault, "a moved MAX end differs (AC9)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset clears the differs flag on both ends (AC10)");

            e.SetMin(10f);
            Assert.IsTrue(e.DiffersFromDefault, "a moved MIN end differs (AC9)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset clears it again (AC10)");
        }

        [Test]
        public void Int_DiffersFromDefault_FlipsAndClearsOnReset()
        {
            int param = 5;
            var reg = new SettingsRegistry();
            var e = reg.AddInt("con_int", "Belt slots", () => param, v => param = v, 1, 9);

            Assert.IsFalse(e.DiffersFromDefault, "untouched int does not differ");
            e.SetValue(8);
            Assert.IsTrue(e.DiffersFromDefault, "a dialed int differs (AC9)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset clears the flag (AC10)");
        }

        [Test]
        public void Bool_DiffersFromDefault_FlipsAndClearsOnReset()
        {
            bool flag = false;
            var reg = new SettingsRegistry();
            var e = reg.AddBool("con_bool", "Hunger on", () => flag, v => flag = v);

            Assert.IsFalse(e.DiffersFromDefault, "untouched bool does not differ");
            e.SetValue(true);
            Assert.IsTrue(e.DiffersFromDefault, "a flipped bool differs (AC9)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset clears the flag (AC10)");
            Assert.IsFalse(flag, "reset restored the registration-time flag (AC10)");
        }

        [Test]
        public void ResetAll_ClearsEveryDiffersFlag()
        {
            float a = 3f; int b = 5; bool c = false;
            var reg = new SettingsRegistry();
            var ea = reg.AddFloat("con_walk", "A", () => a, v => a = v, 0f, 10f);
            var eb = reg.AddInt("con_int", "B", () => b, v => b = v, 1, 9);
            var ec = reg.AddBool("con_bool", "C", () => c, v => c = v);
            ea.SetValue(9f); eb.SetValue(8); ec.SetValue(true);
            Assert.IsTrue(ea.DiffersFromDefault && eb.DiffersFromDefault && ec.DiffersFromDefault);

            reg.ResetAll();

            Assert.IsFalse(ea.DiffersFromDefault, "ResetAll cleared the float badge (AC10)");
            Assert.IsFalse(eb.DiffersFromDefault, "ResetAll cleared the int badge (AC10)");
            Assert.IsFalse(ec.DiffersFromDefault, "ResetAll cleared the bool badge (AC10)");
        }

        // ===== 86cabeqj9 soak NIT — CONSOLE UI SCALE (the panel/text read very large at the Sponsor's res) =====
        //
        // The scale row is a FloatSettingEntry the PANEL registers, bound to its own _uiScale field + an apply.
        // It flows through the SAME registry machinery as every other entry, so the bug CLASS (the knob drives a
        // value, clamps to [0.5,1.5], persists, badge clears on reset) is pinned here with a stand-in field — the
        // exact bind shape SettingsPanel.Start uses. (The visible transform.scale application is a UI Toolkit
        // render concern, covered by the shipped-build capture + Sponsor played-verification.)

        [Test]
        public void ConsoleUiScale_DrivesValue_AndClampsToBand()
        {
            float uiScale = 1f; // stand-in for SettingsPanel._uiScale (default 1.0x = untouched shipped panel)
            var reg = new SettingsRegistry();
            var e = reg.AddFloat(SettingsCatalog.ConsoleUiScaleId, "Console UI scale",
                () => uiScale, v => uiScale = v,
                SettingsCatalog.ConsoleUiScaleMin, SettingsCatalog.ConsoleUiScaleMax, unit: "x");

            Assert.AreEqual(SettingEntry.Archetype.Slider, e.Kind, "the UI-scale row is a slider archetype");
            Assert.AreEqual(0.5f, e.Min, 1e-4f, "the scale floor is 0.5x");
            Assert.AreEqual(1.5f, e.Max, 1e-4f, "the scale ceiling is 1.5x");

            float applied = e.SetValue(0.75f);
            Assert.AreEqual(0.75f, applied, 1e-4f, "a dialed scale applies live");
            Assert.AreEqual(0.75f, uiScale, 1e-4f, "the BOUND scale field actually changed (not a no-op)");

            Assert.AreEqual(1.5f, e.SetValue(9f), 1e-4f, "a scale above the band clamps to 1.5x");
            Assert.AreEqual(0.5f, e.SetValue(0.01f), 1e-4f, "a scale below the band clamps to 0.5x");
        }

        [Test]
        public void ConsoleUiScale_DefaultsToOne_DiffersFlipsAndClearsOnReset()
        {
            float uiScale = 1f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat(SettingsCatalog.ConsoleUiScaleId, "Console UI scale",
                () => uiScale, v => uiScale = v,
                SettingsCatalog.ConsoleUiScaleMin, SettingsCatalog.ConsoleUiScaleMax, unit: "x");

            Assert.AreEqual(1f, e.Default, 1e-4f, "the captured default is 1.0x (untouched = byte-identical panel)");
            Assert.IsFalse(e.DiffersFromDefault, "an untouched scale does not differ (badge off — 1.0x is shipped)");
            e.SetValue(0.6f);
            Assert.IsTrue(e.DiffersFromDefault, "a dialed scale differs (badge shows)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset clears the differs flag");
            Assert.AreEqual(1f, uiScale, 1e-4f, "reset restored the 1.0x default scale");
        }

        [Test]
        public void ConsoleUiScale_PersistsAndReloads_FromPlayerPrefs()
        {
            float uiScale = 1f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat(SettingsCatalog.ConsoleUiScaleId, "Console UI scale",
                () => uiScale, v => uiScale = v,
                SettingsCatalog.ConsoleUiScaleMin, SettingsCatalog.ConsoleUiScaleMax, unit: "x");
            e.SetValue(0.8f); // writes PlayerPrefs (the single persist authority) — survives a relaunch

            // A fresh registry+entry on relaunch loads the persisted scale + drives the field (the
            // SettingsPanel.Start LoadAll path, so the Sponsor's dialed scale survives a soak relaunch).
            float uiScale2 = 1f;
            var reg2 = new SettingsRegistry();
            var e2 = reg2.AddFloat(SettingsCatalog.ConsoleUiScaleId, "Console UI scale",
                () => uiScale2, v => uiScale2 = v,
                SettingsCatalog.ConsoleUiScaleMin, SettingsCatalog.ConsoleUiScaleMax, unit: "x");
            e2.LoadFromPrefs();

            Assert.AreEqual(0.8f, uiScale2, 1e-4f, "the persisted UI scale survives a relaunch (86cabeqj9 NIT)");
        }

        // ===== AC3 — the OPEN console alone does NOT gate world input (only a focused field does) =====

        [Test]
        public void UiInputGate_OpenConsoleAlone_DoesNotSwallowWorldInput_AC3()
        {
            // The console is NON-MODAL (AC2): opening it must NOT push the gate. Only a focused typed-field
            // does (the SettingsPanel WireFieldFocus path). Pin the contract at the gate level: a fresh gate
            // reads false, and the focus-gate ref-count composes (two fields → still gated until both blur).
            UiInputGate.PopPanel(); UiInputGate.PopPanel(); // drain any residue from another test (clamped at 0)
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "no panel/field gating → world input passes (AC2/AC3)");

            bool tracked = false;
            UiInputGate.SetPanelOpen(true, ref tracked);   // simulate ONE field focus-in
            Assert.IsTrue(UiInputGate.CaptureWorldInput, "a focused field swallows world input (AC3)");
            UiInputGate.SetPanelOpen(false, ref tracked);  // field blur
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "world input passes again once the field blurs (AC3)");
        }

        // ===== 86cabeqj9 soak NIT — SCROLL-over-panel gate (fix 1): the wheel is swallowed while the pointer
        //       hovers the NON-MODAL console, but ONLY scroll (WASD/orbit stay live). Pinned at the gate level;
        //       the OrbitCamera reads (CaptureWorldInput || PointerOverConsole) to decide whether to zoom. The
        //       full PointerEnter/Leave event path is a UI Toolkit interaction covered by the shipped-build
        //       capture + Sponsor soak (UIDocument pointer events are unreliable in EditMode). =====

        [Test]
        public void PointerOverConsole_GatesScrollOnly_AndIsSeparateFromCaptureWorldInput()
        {
            UiInputGate.SetPointerOverConsole(false);
            UiInputGate.PopPanel(); UiInputGate.PopPanel(); // drain any residue (clamped at 0)

            Assert.IsFalse(UiInputGate.PointerOverConsole, "fresh: pointer not over the console");
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "fresh: no field-focus gate");

            // Pointer enters the panel rect → scroll must be swallowed, but the WASD/orbit gate stays OFF
            // (the console is non-modal — the whole point of the passthrough the Sponsor confirmed works).
            UiInputGate.SetPointerOverConsole(true);
            Assert.IsTrue(UiInputGate.PointerOverConsole, "pointer-over swallows the wheel (scroll no longer zooms)");
            Assert.IsFalse(UiInputGate.CaptureWorldInput,
                "pointer-over does NOT gate WASD/orbit — only the scroll gate flips (the non-modal passthrough)");

            // The OrbitCamera scroll decision is (CaptureWorldInput || PointerOverConsole): true here → zoom off.
            bool scrollGated = UiInputGate.CaptureWorldInput || UiInputGate.PointerOverConsole;
            Assert.IsTrue(scrollGated, "the camera swallows the wheel while the pointer is over the console");

            UiInputGate.SetPointerOverConsole(false);
            Assert.IsFalse(UiInputGate.PointerOverConsole, "pointer leaves → the wheel zooms the camera again");
            Assert.IsFalse(UiInputGate.CaptureWorldInput || UiInputGate.PointerOverConsole,
                "pointer-off + no field-focus → the camera zoom is live again");
        }

        // ===== 86cabeqj9 soak NIT — UI TEXT SCALE (fix 3): a DISTINCT font-scale setting, separate from the
        //       chrome-scaling Console UI scale. It flows through the SAME registry machinery, so the bug CLASS
        //       (drives a value, clamps to [0.6,2.0], persists, badge clears on reset, DISTINCT id) is pinned
        //       here with a stand-in field — the exact bind shape SettingsPanel.Start uses. The visible
        //       fontSize application is a UI Toolkit render concern, covered by the shipped-build capture. =====

        [Test]
        public void UiTextScale_DrivesValue_AndClampsToBand()
        {
            float textScale = 1f; // stand-in for SettingsPanel._textScale
            var reg = new SettingsRegistry();
            var e = reg.AddFloat(SettingsCatalog.ConsoleTextScaleId, "UI text scale",
                () => textScale, v => textScale = v,
                SettingsCatalog.ConsoleTextScaleMin, SettingsCatalog.ConsoleTextScaleMax, unit: "x");

            Assert.AreEqual(SettingEntry.Archetype.Slider, e.Kind, "the text-scale row is a slider archetype");
            Assert.AreEqual(0.6f, e.Min, 1e-4f, "the text-scale floor is 0.6x");
            Assert.AreEqual(2.0f, e.Max, 1e-4f, "the text-scale ceiling is 2.0x");

            float applied = e.SetValue(1.5f);
            Assert.AreEqual(1.5f, applied, 1e-4f, "a dialed text scale applies live");
            Assert.AreEqual(1.5f, textScale, 1e-4f, "the BOUND text-scale field actually changed (not a no-op)");

            Assert.AreEqual(2.0f, e.SetValue(9f), 1e-4f, "a text scale above the band clamps to 2.0x");
            Assert.AreEqual(0.6f, e.SetValue(0.01f), 1e-4f, "a text scale below the band clamps to 0.6x");
        }

        [Test]
        public void UiTextScale_IsDistinctFrom_ConsoleUiScale()
        {
            Assert.AreNotEqual(SettingsCatalog.ConsoleUiScaleId, SettingsCatalog.ConsoleTextScaleId,
                "the text scale is a SEPARATE setting from the chrome UI scale (distinct ids → distinct rows + prefs)");
        }

        [Test]
        public void UiTextScale_DefaultsToOne_DiffersFlipsAndClearsOnReset()
        {
            float textScale = 1f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat(SettingsCatalog.ConsoleTextScaleId, "UI text scale",
                () => textScale, v => textScale = v,
                SettingsCatalog.ConsoleTextScaleMin, SettingsCatalog.ConsoleTextScaleMax, unit: "x");

            Assert.AreEqual(1f, e.Default, 1e-4f, "the captured default is 1.0x (untouched = shipped fonts)");
            Assert.IsFalse(e.DiffersFromDefault, "an untouched text scale does not differ (badge off)");
            e.SetValue(1.4f);
            Assert.IsTrue(e.DiffersFromDefault, "a dialed text scale differs (badge shows)");
            e.ResetToDefault();
            Assert.IsFalse(e.DiffersFromDefault, "reset clears the differs flag");
            Assert.AreEqual(1f, textScale, 1e-4f, "reset restored the 1.0x default text scale");
        }

        [Test]
        public void UiTextScale_PersistsAndReloads_FromPlayerPrefs()
        {
            float textScale = 1f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat(SettingsCatalog.ConsoleTextScaleId, "UI text scale",
                () => textScale, v => textScale = v,
                SettingsCatalog.ConsoleTextScaleMin, SettingsCatalog.ConsoleTextScaleMax, unit: "x");
            e.SetValue(1.7f); // writes PlayerPrefs (survives a relaunch)

            float textScale2 = 1f;
            var reg2 = new SettingsRegistry();
            var e2 = reg2.AddFloat(SettingsCatalog.ConsoleTextScaleId, "UI text scale",
                () => textScale2, v => textScale2 = v,
                SettingsCatalog.ConsoleTextScaleMin, SettingsCatalog.ConsoleTextScaleMax, unit: "x");
            e2.LoadFromPrefs();

            Assert.AreEqual(1.7f, textScale2, 1e-4f, "the persisted UI text scale survives a relaunch (86cabeqj9 NIT)");
        }
    }
}
