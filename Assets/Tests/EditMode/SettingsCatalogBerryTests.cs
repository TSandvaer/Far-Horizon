using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC guard for the BERRY-REGROWTH settings wiring (ticket 86cabn67w — the 86caa5zz3 AC4 settings-
    /// registration follow-up). Proves SettingsCatalog.PopulateBerry:
    ///   • registers `berry_regrowth_time` as a LIVE RANGE row driving BerryBush.regrowMin/MaxSeconds;
    ///   • FANS OUT across EVERY bush in the list — one slider retunes the wired bush AND the scatter bushes
    ///     (each BerryBush holds its OWN regrow window, unlike the shared ChopTree, so the row must reach all
    ///     of them; a setting that tuned only one of ~32 bushes is a broken instrument);
    ///   • is a NO-OP with a null/empty list OR an all-null list (the row is absent — a bush-less rig / bare
    ///     test never null-refs and never adds a dead knob — the PopulateThirst/Stones de-collision precedent);
    ///   • is registered via the SEPARATE method (NOT by appending to Populate).
    /// Drives the real components (plain public fields) so this proves the bindings hit the actual params.
    ///
    /// The id is DISTINCT from #183's HungerNeed `berry_restore_amount` (BerryRestoreId) — this is the per-bush
    /// REGROW TIMER, not the per-berry hunger satisfaction (the de-collision check below asserts both coexist).
    /// </summary>
    public class SettingsCatalogBerryTests
    {
        private readonly List<GameObject> _gos = new List<GameObject>();
        private BerryBush _bushA, _bushB, _bushC;

        // Build a BerryBush component on a fresh GameObject with the given regrow window.
        private BerryBush MakeBush(string name, float min, float max)
        {
            // BerryBush.Awake does a couple of FindObjectOfType lookups (Inventory/HungerNeed) that log nothing
            // material in a bare rig; tolerate any incidental logs so the test isn't flagged.
            LogAssert.ignoreFailingMessages = true;
            var go = new GameObject(name);
            _gos.Add(go);
            var bush = go.AddComponent<BerryBush>();
            bush.regrowMinSeconds = min;
            bush.regrowMaxSeconds = max;
            return bush;
        }

        [SetUp]
        public void SetUp()
        {
            // Three bushes with DISTINCT regrow windows so the fan-out test can prove every one is rewritten to
            // the SAME dialed window (not that they happened to already match).
            _bushA = MakeBush("BerryBushA", 20f, 40f);
            _bushB = MakeBush("BerryBushB", 55f, 90f);
            _bushC = MakeBush("BerryBushC", 100f, 200f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _gos) if (go != null) Object.DestroyImmediate(go);
            _gos.Clear();
            LogAssert.ignoreFailingMessages = false;
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.BerryRegrowthId + ".min");
            PlayerPrefs.DeleteKey("fh.settings." + SettingsCatalog.BerryRegrowthId + ".max");
        }

        [Test]
        public void PopulateBerry_RegistersBerryRegrowthRange_Live_BoundToRegrowMinMax()
        {
            // Full Build path (every prior target null — those settings are simply skipped) + the berry list.
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null,
                new BerryBush[] { _bushA, _bushB, _bushC });

            Assert.IsTrue(reg.Has(SettingsCatalog.BerryRegrowthId), "berry regrowth time row present");
            var regrow = reg.Get(SettingsCatalog.BerryRegrowthId) as RangeSettingEntry;
            Assert.IsNotNull(regrow, "berry regrowth time is a RANGE row (organic [min,max], like tree/stone)");
            Assert.IsTrue(regrow.Available, "berry regrowth time is LIVE (the settings panel is merged)");
            Assert.AreEqual(SettingsCatalog.BerryRegrowthLower, regrow.LowerLimit, 1e-3f, "lower limit == band floor");
            Assert.AreEqual(SettingsCatalog.BerryRegrowthUpper, regrow.UpperLimit, 1e-3f, "upper limit == band ceiling");

            // Reads the REPRESENTATIVE (first) bush's live min/max...
            Assert.AreEqual(20f, regrow.MinValue, 1e-3f, "regrow row reads the representative bush's regrowMinSeconds");
            Assert.AreEqual(40f, regrow.MaxValue, 1e-3f, "regrow row reads the representative bush's regrowMaxSeconds");
        }

        [Test]
        public void PopulateBerry_RangeRow_FansOutToEVERYBush_NotJustOne()
        {
            // THE fan-out guard (the ticket's core constraint): writing the range must retune EVERY bush, so a
            // single slider drives the wired bush AND the scatter bushes — not just the representative.
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null,
                new BerryBush[] { _bushA, _bushB, _bushC });
            var regrow = reg.Get(SettingsCatalog.BerryRegrowthId) as RangeSettingEntry;
            Assert.IsNotNull(regrow, "berry regrowth time is a RANGE row");

            // WIDEN max-first then min — the RangeSettingEntry invariant is LowerLimit <= min <= max, and SetMin
            // clamps to [Lower, currentMax]. The bushes start at a 20..40 window; raising to 120..240 must widen
            // the max BEFORE the min, or SetMin(120) would clamp against the still-40 max (the entry's own
            // Apply/LoadFromPrefs use this same max-first order, RangeSettingEntry lines 94-96 / 102-104).
            regrow.SetMax(240f);
            regrow.SetMin(120f);

            // EVERY bush — including the ones that started with DIFFERENT windows — now reads the dialed window.
            Assert.AreEqual(120f, _bushA.regrowMinSeconds, 1e-3f, "bush A regrowMin fanned out");
            Assert.AreEqual(240f, _bushA.regrowMaxSeconds, 1e-3f, "bush A regrowMax fanned out");
            Assert.AreEqual(120f, _bushB.regrowMinSeconds, 1e-3f, "bush B regrowMin fanned out (started 55)");
            Assert.AreEqual(240f, _bushB.regrowMaxSeconds, 1e-3f, "bush B regrowMax fanned out (started 90)");
            Assert.AreEqual(120f, _bushC.regrowMinSeconds, 1e-3f, "bush C regrowMin fanned out (started 100)");
            Assert.AreEqual(240f, _bushC.regrowMaxSeconds, 1e-3f, "bush C regrowMax fanned out (started 200)");
        }

        [Test]
        public void PopulateBerry_RangeRow_ClampsBeyondBand_IntoTheLimits()
        {
            // Band-clamp guard (mirrors the Chop range/stepper clamp tests): dialing either end BEYOND the hard
            // band [BerryRegrowthLower, BerryRegrowthUpper] = [0, 1800] must clamp into the band, never escape it.
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null,
                new BerryBush[] { _bushA, _bushB, _bushC });
            var regrow = reg.Get(SettingsCatalog.BerryRegrowthId) as RangeSettingEntry;
            Assert.IsNotNull(regrow, "berry regrowth time is a RANGE row");

            // ABOVE the ceiling: SetMax clamps to UpperLimit (= 1800); the max then carries SetMin's ceiling too.
            float maxRet = regrow.SetMax(999999f);
            Assert.AreEqual(SettingsCatalog.BerryRegrowthUpper, maxRet, 1e-3f,
                "SetMax beyond the ceiling clamps to BerryRegrowthUpper (1800)");
            Assert.AreEqual(SettingsCatalog.BerryRegrowthUpper, regrow.MaxValue, 1e-3f, "max reads the clamped ceiling");

            // A min ABOVE the ceiling clamps to the current max (now 1800), never past it.
            float minHi = regrow.SetMin(999999f);
            Assert.AreEqual(SettingsCatalog.BerryRegrowthUpper, minHi, 1e-3f,
                "SetMin above the ceiling clamps to the current max (1800), never past it");

            // BELOW the floor: SetMin clamps to LowerLimit (= 0); a negative can never escape under the floor.
            float minLo = regrow.SetMin(-50f);
            Assert.AreEqual(SettingsCatalog.BerryRegrowthLower, minLo, 1e-3f,
                "SetMin below the floor clamps to BerryRegrowthLower (0)");
            Assert.AreEqual(SettingsCatalog.BerryRegrowthLower, regrow.MinValue, 1e-3f, "min reads the clamped floor");

            // The clamped band end fans out to EVERY bush (the fan-out + clamp compose — the slider can't push a
            // single bush's regrow window outside the band).
            Assert.AreEqual(SettingsCatalog.BerryRegrowthLower, _bushA.regrowMinSeconds, 1e-3f, "bush A min clamped+fanned");
            Assert.AreEqual(SettingsCatalog.BerryRegrowthUpper, _bushC.regrowMaxSeconds, 1e-3f, "bush C max clamped+fanned");
        }

        [Test]
        public void PopulateBerry_NullList_NoBerryRow()
        {
            // No berry list — the catalog must behave exactly as before berries (no dead knob, no null-ref).
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null, null);

            Assert.IsFalse(reg.Has(SettingsCatalog.BerryRegrowthId),
                "with a null berry list, the berry regrowth row is absent (no dead knob)");
        }

        [Test]
        public void PopulateBerry_EmptyList_NoBerryRow()
        {
            var reg = SettingsCatalog.Build(null, null, null, null, null, null, null, null, null,
                new BerryBush[0]);

            Assert.IsFalse(reg.Has(SettingsCatalog.BerryRegrowthId),
                "with an empty berry list, the berry regrowth row is absent (no dead knob)");
        }

        [Test]
        public void PopulateBerry_AllNullEntries_NoBerryRow()
        {
            // A list of only-null entries (e.g. all bushes destroyed/regenerated) → no live target → no row.
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBerry(reg, new BerryBush[] { null, null });

            Assert.IsFalse(reg.Has(SettingsCatalog.BerryRegrowthId),
                "an all-null bush list yields no live target → no berry regrowth row (no dead knob)");
        }

        [Test]
        public void PopulateBerry_SkipsNullEntries_OnFanOut()
        {
            // A mixed list (a hole where a bush was destroyed) must not null-ref on the fan-out and must still
            // drive the live bushes.
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateBerry(reg, new BerryBush[] { null, _bushA, null, _bushB });
            var regrow = reg.Get(SettingsCatalog.BerryRegrowthId) as RangeSettingEntry;
            Assert.IsNotNull(regrow, "row present (the first non-null bush is the representative)");

            Assert.DoesNotThrow(() => { regrow.SetMin(30f); regrow.SetMax(60f); },
                "the fan-out setter tolerates null holes in the list (no null-ref)");
            Assert.AreEqual(30f, _bushA.regrowMinSeconds, 1e-3f, "live bush A still driven past the null holes");
            Assert.AreEqual(60f, _bushB.regrowMaxSeconds, 1e-3f, "live bush B still driven past the null holes");
        }

        [Test]
        public void PopulateBerry_DirectCall_AddsExactlyOneRow_NotViaPopulate()
        {
            // The de-collision precedent: the row is registered by PopulateBerry, NOT by Populate. A bare
            // Populate must NOT add the berry row; a subsequent PopulateBerry adds exactly one.
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);
            Assert.IsFalse(reg.Has(SettingsCatalog.BerryRegrowthId),
                "Populate alone must NOT add the berry regrowth row (it lives in PopulateBerry)");

            SettingsCatalog.PopulateBerry(reg, new BerryBush[] { _bushA });
            int count = 0;
            foreach (var e in reg.Entries) if (e.Id == SettingsCatalog.BerryRegrowthId) count++;
            Assert.AreEqual(1, count, "PopulateBerry adds exactly one berry regrowth row (no duplicate)");
        }

        [Test]
        public void BerryRegrowthId_IsDistinctFrom_HungerBerryRestoreId()
        {
            // Vocabulary/collision guard (the ticket's ⚠): the per-bush regrow-TIMER id must NOT collide with
            // #183's per-berry hunger-SATISFACTION id. They are different settings on different systems.
            Assert.AreNotEqual(SettingsCatalog.BerryRestoreId, SettingsCatalog.BerryRegrowthId,
                "berry regrowth (per-bush timer) and berry restore (per-berry hunger amount) are distinct ids");
            Assert.AreEqual("berry_regrowth_time", SettingsCatalog.BerryRegrowthId, "the regrowth id is stable");
            Assert.AreEqual("berry_restore_amount", SettingsCatalog.BerryRestoreId, "the restore id is unchanged (#183)");
        }
    }
}
