using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cajb00b (#244 NIT, review comment 4875417363) — regression guard for the capture-drive SAME-VALUE trap.
    ///
    /// The shipped-build settings capture (<see cref="SettingsVerifyCapture"/>) drives a bound control WITH notify
    /// to prove a tweak repaints the frame (#244 un-quarantined verify_settings_gate Check 3 on this). But UI
    /// Toolkit's <c>BaseField&lt;T&gt;.value</c> setter suppresses the ChangeEvent when the target EQUALS the
    /// control's current value — so a FUTURE same-value tweak target would SILENTLY skip the callback + repaint,
    /// leaving <c>settings_tweaked.png</c> pixel-identical to <c>settings_open.png</c> and false-REDding Check 3 with
    /// no clue why. <see cref="SettingsPanel.DriveValueWithNotify{T}"/> converts that silent no-op into a NAMED loud
    /// failure so the author picks a target that actually moves the control.
    ///
    /// This is the EditMode SIBLING of <c>tests/scripts/test_gate_scripts.sh</c> case 5 — case 5 guards the GATE
    /// reds on a pixel-identical tweaked frame (fake-exe seam); this guards the C# helper reds on a same-value drive
    /// (the ROOT cause a same-value target would trigger). Bare UI Toolkit controls construct without a live panel,
    /// so this needs no UIDocument/bootstrap — the guard throws BEFORE any dispatch.
    /// </summary>
    public class SettingsCaptureDriveGuardTests
    {
        [Test]
        public void DriveValueWithNotify_SliderSameValue_FailsLoud_NamingTheTrap()
        {
            var slider = new Slider(0f, 10f) { value = 5f };
            var ex = Assert.Throws<InvalidOperationException>(
                () => SettingsPanel.DriveValueWithNotify(slider, 5f),
                "driving a Slider to its CURRENT value must FAIL LOUD — UI Toolkit suppresses the ChangeEvent for an " +
                "unchanged value, so the capture would not repaint (silent false-RED of verify_settings Check 3)");
            StringAssert.Contains("86cajb00b", ex.Message,
                "the failure must name the trap ticket so a future author immediately knows why (not a mystery red)");
        }

        [Test]
        public void DriveValueWithNotify_SliderDifferentValue_Applies()
        {
            var slider = new Slider(0f, 10f) { value = 5f };
            Assert.DoesNotThrow(() => SettingsPanel.DriveValueWithNotify(slider, 8f),
                "a real (differing) tweak target must apply without failing — this is the live #244 capture path");
            Assert.AreEqual(8f, slider.value, 1e-4f, "the differing target must be applied to the control");
        }

        [Test]
        public void DriveValueWithNotify_RangeSameValue_FailsLoud()
        {
            // MinMaxSlider(minValue, maxValue, lowLimit, highLimit) → value == (6, 26).
            var range = new MinMaxSlider(6f, 26f, 0f, 40f);
            Assert.Throws<InvalidOperationException>(
                () => SettingsPanel.DriveValueWithNotify(range, new Vector2(6f, 26f)),
                "driving a MinMaxSlider to its CURRENT (min,max) must fail loud for the same suppression reason");
        }

        [Test]
        public void DriveValueWithNotify_RangeDifferentValue_Applies()
        {
            var range = new MinMaxSlider(6f, 26f, 0f, 40f); // mirrors the live zoom drive (6,26) -> (6,18)
            Assert.DoesNotThrow(() => SettingsPanel.DriveValueWithNotify(range, new Vector2(6f, 18f)));
            Assert.AreEqual(18f, range.value.y, 1e-4f, "the differing max end must be applied to the control");
        }
    }
}
