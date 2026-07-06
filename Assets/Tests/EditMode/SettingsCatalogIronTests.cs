using NUnit.Framework;
using FarHorizon;
using FarHorizon.Settings;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// I-0 foundation guard (ticket 86cakkmgw) — SettingsCatalog.PopulateIron registers the two Sponsor-locked
    /// difficulty dials (ore rarity + smelt cost, DECISIONS 2026-07-06) as FOUR EXTENSION HOOKS: each present,
    /// Available=false "(soon)", dev-console by default, and reserving the ids for I-2/I-3 to flip LIVE. Also
    /// pins the id strings (§1 vocabulary — downstream imports them verbatim) and the de-collision precedent
    /// (Populate alone must NOT add them). Pure-logic (no scene; the registry is scene-free).
    ///
    /// Guards BITE: a wrong id string, a hook accidentally shipped Available=true (a fake dial with no live
    /// target), a dial leaked into the player panel, or the rows added to the base Populate turn these red.
    /// </summary>
    public class SettingsCatalogIronTests
    {
        private static readonly string[] IronIds =
        {
            SettingsCatalog.IronOreRarityId,
            SettingsCatalog.SmeltOrePerIngotId,
            SettingsCatalog.SmeltFuelCostId,
            SettingsCatalog.SmeltTimeId,
        };

        [Test]
        public void PopulateIron_RegistersFourExtensionHooks_AllGreyed()
        {
            var reg = new SettingsRegistry();
            SettingsCatalog.PopulateIron(reg);

            foreach (var id in IronIds)
            {
                Assert.IsTrue(reg.Has(id), $"iron dial '{id}' is registered (§1)");
                var entry = reg.Get(id);
                Assert.IsNotNull(entry, $"iron dial '{id}' resolves to an entry");
                Assert.IsFalse(entry.Available,
                    $"iron dial '{id}' is an EXTENSION HOOK (Available=false '(soon)') — no live system binds yet (I-2/I-3 flip live)");
            }
        }

        [Test]
        public void IronDialIds_MatchTheVocabularyContract()
        {
            // The §1 table strings are load-bearing — I-2/I-3 flip these EXACT ids live.
            Assert.AreEqual("iron_ore_rarity", SettingsCatalog.IronOreRarityId);
            Assert.AreEqual("smelt_ore_per_ingot", SettingsCatalog.SmeltOrePerIngotId);
            Assert.AreEqual("smelt_fuel_cost", SettingsCatalog.SmeltFuelCostId);
            Assert.AreEqual("smelt_time", SettingsCatalog.SmeltTimeId);
        }

        [Test]
        public void IronDials_AreDevConsole_ByDefault()
        {
            // The ticket constraint: both dials are dev-console (absent from the player allowlist) unless a soak
            // asks otherwise — matches the chop/stone/berry tweakables (dev-tune, then bake presets).
            foreach (var id in IronIds)
                Assert.IsTrue(SettingsCategory.IsDev(id), $"iron dial '{id}' is dev-console by default (not player-facing)");
        }

        [Test]
        public void Populate_Alone_DoesNotAddIronRows_DeCollisionPrecedent()
        {
            // The de-collision precedent: the iron rows live in PopulateIron, NOT the base Populate. A bare
            // Populate must NOT add them; a subsequent PopulateIron adds exactly one of each (no duplicate throw).
            var reg = new SettingsRegistry();
            SettingsCatalog.Populate(reg, null, null);
            foreach (var id in IronIds)
                Assert.IsFalse(reg.Has(id), $"Populate alone must NOT add '{id}' (it lives in PopulateIron)");

            SettingsCatalog.PopulateIron(reg);
            foreach (var id in IronIds)
            {
                int count = 0;
                foreach (var e in reg.Entries) if (e.Id == id) count++;
                Assert.AreEqual(1, count, $"PopulateIron adds exactly one '{id}' row (no duplicate)");
            }
        }

        [Test]
        public void PopulateIron_NullRegistry_IsSafe()
        {
            Assert.DoesNotThrow(() => SettingsCatalog.PopulateIron(null),
                "PopulateIron is null-registry-safe like every other Populate* method");
        }
    }
}
