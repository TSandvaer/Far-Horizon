using NUnit.Framework;
using UnityEngine;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC6 regression guard for the EXTENSIBLE settings registry (ticket 86caa4bqp). The registry + the
    /// typed entries are pure C# (no scene, no Update, no UIDocument), so the WHOLE contract — register,
    /// bind, drive-the-param, clamp, range-both-ends, persist, reset, extension-hook — is fully driveable
    /// here in EditMode (where the UIDocument render loop is unreliable + Time.deltaTime≈0).
    ///
    /// THE BUG CLASS these pin (not one instance): "a registered setting actually DRIVES its bound param"
    /// (a binding that silently no-ops is the silent-killer), "a range setting CLAMPS the live system to
    /// both ends" (AC4), and "an unavailable extension hook never fakes a param" (AC3). A future setting
    /// added to the registry rides the SAME entry types, so these guards cover it too.
    /// </summary>
    public class SettingsRegistryTests
    {
        [SetUp]
        public void ClearPrefs()
        {
            // PlayerPrefs persistence (AC5) is exercised below — start clean so a prior run can't leak.
            PlayerPrefs.DeleteKey("fh.settings.sample_walk");
            PlayerPrefs.DeleteKey("fh.settings.sample_zoom.min");
            PlayerPrefs.DeleteKey("fh.settings.sample_zoom.max");
            PlayerPrefs.DeleteKey("fh.settings.sample_int");
        }

        // ===== ARCHETYPE A — FloatSettingEntry binds + drives + clamps + persists =====

        [Test]
        public void FloatEntry_DrivesBoundParam_OnSetValue()
        {
            float param = 5.5f; // stand-in for WasdMovement.moveSpeed
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("sample_walk", "Walk speed", () => param, v => param = v, 1f, 12f);

            float applied = e.SetValue(8f);

            Assert.AreEqual(8f, applied, 1e-4f, "SetValue returns the applied value");
            Assert.AreEqual(8f, param, 1e-4f, "the BOUND param actually changed (the binding is not a no-op — AC2)");
            Assert.AreEqual(8f, e.Value, 1e-4f, "the entry reads the live param back");
        }

        [Test]
        public void FloatEntry_ClampsToSliderBand()
        {
            float param = 5.5f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("sample_walk", "Walk speed", () => param, v => param = v, 1f, 12f);

            Assert.AreEqual(12f, e.SetValue(999f), 1e-4f, "value clamps to Max");
            Assert.AreEqual(1f, e.SetValue(-999f), 1e-4f, "value clamps to Min");
            Assert.AreEqual(1f, param, 1e-4f, "the clamped value is what reaches the param");
        }

        [Test]
        public void FloatEntry_PersistsAndReloads_FromPlayerPrefs()
        {
            float param = 5.5f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("sample_walk", "Walk speed", () => param, v => param = v, 1f, 12f);
            e.SetValue(9f); // writes PlayerPrefs (AC5)

            // Simulate a relaunch: a fresh param + entry, then LoadFromPrefs drives it back.
            float param2 = 5.5f;
            var reg2 = new SettingsRegistry();
            var e2 = reg2.AddFloat("sample_walk", "Walk speed", () => param2, v => param2 = v, 1f, 12f);
            e2.LoadFromPrefs();

            Assert.AreEqual(9f, param2, 1e-4f, "the persisted soak tweak survives a relaunch (AC5)");
        }

        [Test]
        public void FloatEntry_ResetToDefault_RestoresRegistrationValue()
        {
            float param = 5.5f;
            var reg = new SettingsRegistry();
            var e = reg.AddFloat("sample_walk", "Walk speed", () => param, v => param = v, 1f, 12f);
            e.SetValue(11f);

            e.ResetToDefault();

            Assert.AreEqual(5.5f, param, 1e-4f, "reset restores the value captured at registration (AC5)");
        }

        [Test]
        public void FloatEntry_ExtensionHook_NeverDrivesParam_AC3()
        {
            float param = 0f;
            var reg = new SettingsRegistry();
            // Available=false — a not-yet-built extension hook (run/jump/tool-use). Must NOT fake a param.
            var e = reg.AddFloat("sample_hook", "Run speed", () => param, v => param = v, 0f, 18f, available: false);

            float applied = e.SetValue(9f);

            Assert.IsFalse(e.Available, "the hook is marked unavailable (greyed + '(soon)' in the panel)");
            Assert.AreEqual(0f, param, 1e-4f, "an unavailable hook NEVER drives its (placeholder) param — AC3");
            Assert.AreEqual(0f, applied, 1e-4f, "SetValue on a hook is inert");
        }

        // ===== ARCHETYPE B — RangeSettingEntry drives BOTH ends + the live system clamps (AC4) =====

        [Test]
        public void RangeEntry_DrivesBothEnds_AndClampsLiveSystem_AC4()
        {
            // Stand-in for an OrbitCamera that clamps a runtime value to [min, max] every frame.
            float min = 6f, max = 26f, live = 14f;
            void Clamp() => live = Mathf.Clamp(live, min, max);

            var reg = new SettingsRegistry();
            var e = reg.AddRange("sample_zoom", "Zoom range",
                () => min, v => { min = v; Clamp(); },
                () => max, v => { max = v; Clamp(); },
                2f, 40f);

            // Tighten BOTH ends; the live system re-clamps to the new range immediately (AC4).
            e.SetMin(10f);
            e.SetMax(12f);

            Assert.AreEqual(10f, e.MinValue, 1e-4f, "the MIN end drove the live min");
            Assert.AreEqual(12f, e.MaxValue, 1e-4f, "the MAX end drove the live max");
            Assert.AreEqual(12f, live, 1e-4f, "the live runtime value clamped DOWN into the tightened range (AC4)");
            Assert.That(live, Is.InRange(10f, 12f), "the live value sits within both registered ends");
        }

        [Test]
        public void RangeEntry_MinCannotExceedMax_AndHardLimitsHold()
        {
            float min = 6f, max = 26f;
            var reg = new SettingsRegistry();
            var e = reg.AddRange("sample_zoom", "Zoom range",
                () => min, v => min = v, () => max, v => max = v, 2f, 40f);

            e.SetMin(999f); // drag min past max → snaps to max (the clamp-hit feedback case)
            Assert.LessOrEqual(min, max, "min can never exceed max (the dual-thumb invariant)");
            Assert.AreEqual(max, e.MinValue, 1e-4f, "min snaps to max when dragged past it");

            e.SetMax(-999f); // drag max below the hard lower-limit
            Assert.GreaterOrEqual(max, 2f, "max can never drop below the hard lower-limit");

            e.SetMax(999f);
            Assert.LessOrEqual(max, 40f, "max can never exceed the hard upper-limit");
        }

        [Test]
        public void RangeEntry_PersistsBothEnds_AndReloads()
        {
            float min = 6f, max = 26f;
            var reg = new SettingsRegistry();
            var e = reg.AddRange("sample_zoom", "Zoom range",
                () => min, v => min = v, () => max, v => max = v, 2f, 40f);
            e.SetMin(8f);
            e.SetMax(20f);

            float min2 = 6f, max2 = 26f;
            var reg2 = new SettingsRegistry();
            var e2 = reg2.AddRange("sample_zoom", "Zoom range",
                () => min2, v => min2 = v, () => max2, v => max2 = v, 2f, 40f);
            e2.LoadFromPrefs();

            Assert.AreEqual(8f, min2, 1e-4f, "the min end persists across a relaunch (AC5)");
            Assert.AreEqual(20f, max2, 1e-4f, "the max end persists across a relaunch (AC5)");
        }

        // ===== ARCHETYPE C — IntSettingEntry (downstream tickets slot in; the type EXISTS now) =====

        [Test]
        public void IntEntry_StepperDrives_AndClamps()
        {
            int param = 5; // stand-in for a future belt-slot count
            var reg = new SettingsRegistry();
            var e = reg.AddInt("sample_int", "Belt slots", () => param, v => param = v, 1, 9);

            e.Increment();
            Assert.AreEqual(6, param, "one [+] press increments the bound int");
            e.SetValue(99);
            Assert.AreEqual(9, param, "value clamps to Max");
            e.SetValue(-5);
            Assert.AreEqual(1, param, "value clamps to Min");
        }

        // ===== REGISTRY semantics =====

        [Test]
        public void Registry_KeepsOrder_LookupById_AndRejectsDuplicateIds()
        {
            float a = 1f, b = 2f;
            var reg = new SettingsRegistry();
            reg.AddFloat("first", "First", () => a, v => a = v, 0f, 10f);
            reg.AddFloat("second", "Second", () => b, v => b = v, 0f, 10f);

            Assert.AreEqual(2, reg.Count, "both registered");
            Assert.AreEqual("first", reg.Entries[0].Id, "registration order preserved (panel renders rows in order)");
            Assert.IsNotNull(reg.Get("second"), "lookup by id works");
            Assert.IsTrue(reg.Has("first"));
            Assert.Throws<System.ArgumentException>(
                () => reg.AddFloat("first", "Dup", () => a, v => a = v, 0f, 10f),
                "a duplicate id is a registration bug — it throws, never silently shadows");
        }

        [Test]
        public void Registry_ResetAll_RestoresEveryEntry()
        {
            float a = 3f, b = 7f;
            var reg = new SettingsRegistry();
            var ea = reg.AddFloat("a", "A", () => a, v => a = v, 0f, 10f);
            var eb = reg.AddFloat("b", "B", () => b, v => b = v, 0f, 10f);
            ea.SetValue(9f);
            eb.SetValue(1f);

            reg.ResetAll();

            Assert.AreEqual(3f, a, 1e-4f, "entry A reset to its default");
            Assert.AreEqual(7f, b, 1e-4f, "entry B reset to its default");
        }
    }
}
