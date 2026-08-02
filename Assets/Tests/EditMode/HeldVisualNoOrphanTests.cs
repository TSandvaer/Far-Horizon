using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;
using FarHorizon.Combat;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86caxjx26 AC2/AC3 — THE NO-ORPHAN GUARD for the belt-selection → held-visual map, in BOTH directions.
    ///
    /// WHY THIS EXISTS (it is the load-bearing half of the ticket). "A new weapon is just new data" has been
    /// quietly UNTRUE four times now: adding a <see cref="WeaponCatalog"/> id + an <c>ItemCatalog</c> ItemDef +
    /// a recipe makes the weapon craftable and belt-selectable, but the HELD VISUAL additionally needs
    /// (a) an <see cref="Inventory"/> <c>Is…SelectedInBelt</c> predicate and (b) a row in one of the selection
    /// → family-index maps that <see cref="HeldWeaponCycleDebug.IsHeldVisualWeaponSelected"/> composes.
    /// Nothing forced an author to write either, so the weapon shipped and rendered EMPTY HANDS:
    ///   • 86cakkmr0 (I-2)   — the stone/iron PICKAXE.
    ///   • 86caffwv5 (soak-3) — the whole WOOD tier; the Sponsor's soak blocker.
    ///   • 86cah7y5b (#351)  — the four IRON blades.
    ///   • 86caxjx26 (this)  — `dagger_stone` + `sword_stone`, the last two.
    /// Four occurrences of ONE failure class, each found by a human noticing nothing in the castaway's hand.
    /// This guard makes the fifth occurrence impossible to merge.
    ///
    /// THE TWO DESIGN CONSTRAINTS THAT MAKE IT WORK (do not "simplify" either away):
    ///   1. DATA-DRIVEN off <see cref="WeaponCatalog.BuildDefaults"/>. A hand-written id list reproduces the
    ///      bug exactly — the author who forgets the map row also forgets the list row.
    ///   2. ASSERTED THROUGH <see cref="HeldWeaponCycleDebug.IsHeldVisualWeaponSelected"/>, the single predicate
    ///      <see cref="HeldAxe.ShouldShow"/> (mesh visibility) AND <see cref="CastawayFingerCurl"/> (the grip)
    ///      both read. A guard written against whichever MAP was added re-passes when a future author adds a
    ///      map row and forgets to compose it into the predicate — the same one-step-short miss this guards.
    ///
    /// Both directions are covered, because a seated mesh nothing can select is the same class of hole:
    ///   FORWARD — every obtainable weapon id resolves to a held-visual family index (no orphan IDS).
    ///   REVERSE — every family index in <see cref="HeldWeaponCycleDebug.WeaponNodeNames"/> is reachable from
    ///             some obtainable id (no orphan INDICES).
    /// </summary>
    public class HeldVisualNoOrphanTests
    {
        /// <summary>
        /// Ids that are deliberately NOT held-visual. EMPTY today, and that is the correct state: the weapon
        /// roster is CLOSED at 5 types × 3 tiers (DECISIONS 2026-07-27) and every one of the 15 is a physical
        /// tool the castaway holds. A future thrown/consumable/placed weapon would go here WITH ITS REASON on
        /// the same line — never as a silent omission, and never as a way to quiet this test.
        /// </summary>
        private static readonly Dictionary<string, string> NotHeldVisualById = new Dictionary<string, string>
        {
            // (id, reason) — intentionally empty. Example of the required shape if one is ever added:
            // { WeaponCatalog.SomeThrownId, "thrown on use — it never seats in the hand" },
        };

        private static Inventory NewInventory(out GameObject go)
        {
            go = new GameObject("Inventory");
            return go.AddComponent<Inventory>();
        }

        /// <summary>The canonical shipped weapon id set, read from the catalog itself (never hand-listed).</summary>
        private static List<string> AllWeaponIds()
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            try
            {
                catalog.BuildDefaults();
                var ids = new List<string>();
                foreach (var def in catalog.All)
                    if (def != null && !string.IsNullOrEmpty(def.Id)) ids.Add(def.Id);
                return ids;
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        // ============================================================================================
        // FORWARD — no orphan IDS. The four-time-repeated defect, as an executable gate.
        // ============================================================================================

        [Test]
        public void EveryObtainableWeaponId_ShowsInTheHandWhenSelected_NoOrphanIds()
        {
            var ids = AllWeaponIds();
            Assert.IsNotEmpty(ids, "staleness guard: the catalog must yield ids, or this test asserts nothing");

            var unmapped = new List<string>();
            var unobtainable = new List<string>();

            foreach (string id in ids)
            {
                if (NotHeldVisualById.ContainsKey(id)) continue; // declared non-held-visual, with a reason

                var inv = NewInventory(out var go);
                try
                {
                    var def = inv.Catalog.ById(id);
                    if (def == null) { unobtainable.Add(id + " (no ItemDef)"); continue; }

                    var slot = inv.Model.AddToolToBelt(def);
                    if (!slot.HasValue) { unobtainable.Add(id + " (not belt-eligible)"); continue; }
                    inv.Model.SelectBelt(slot.Value.Index);

                    // THE assert: the SHARED predicate, not whichever map happens to carry this id.
                    if (!HeldWeaponCycleDebug.IsHeldVisualWeaponSelected(inv)) unmapped.Add(id);
                }
                finally { Object.DestroyImmediate(go); }
            }

            Assert.IsEmpty(unobtainable,
                "these WeaponCatalog ids cannot be put on the belt at all: " + string.Join(", ", unobtainable) +
                " — register an ItemDef in ItemCatalog, or declare the id in NotHeldVisualById with a reason.");

            Assert.IsEmpty(unmapped, Explain(unmapped));
        }

        // ============================================================================================
        // REVERSE — no orphan INDICES. AC2: a seated mesh nothing can select is the same hole, mirrored.
        // ============================================================================================

        [Test]
        public void EveryHeldVisualFamilyIndex_IsReachableFromSomeObtainableId_NoOrphanIndices()
        {
            var reached = new Dictionary<int, string>();
            var collisions = new List<string>();

            foreach (string id in AllWeaponIds())
            {
                if (NotHeldVisualById.ContainsKey(id)) continue;

                var inv = NewInventory(out var go);
                try
                {
                    var def = inv.Catalog.ById(id);
                    if (def == null) continue; // the forward test owns "unobtainable"
                    var slot = inv.Model.AddToolToBelt(def);
                    if (!slot.HasValue) continue;
                    inv.Model.SelectBelt(slot.Value.Index);

                    int index = HeldWeaponCycleDebug.HeldVisualIndexFor(inv);
                    if (index < 0) continue; // the forward test owns "unmapped"

                    if (reached.TryGetValue(index, out string owner)) collisions.Add(
                        "index " + index + " (" + HeldWeaponCycleDebug.WeaponNodeNames[index] + ") is claimed by BOTH `" +
                        owner + "` and `" + id + "`");
                    else reached[index] = id;
                }
                finally { Object.DestroyImmediate(go); }
            }

            var orphans = new List<string>();
            for (int i = 0; i < HeldWeaponCycleDebug.WeaponNodeNames.Length; i++)
                if (!reached.ContainsKey(i))
                    orphans.Add("index " + i + " (" + HeldWeaponCycleDebug.WeaponNodeNames[i] + ")");

            Assert.IsEmpty(orphans,
                "ORPHAN HELD-VISUAL MESH — " + orphans.Count + " family index(es) are seated in WeaponNodeNames but " +
                "NO obtainable weapon id resolves to them: " + string.Join(", ", orphans) + ". Either a weapon id + " +
                "ItemDef + recipe is missing for that mesh (the player can never hold it), or the mesh row is dead " +
                "and should be removed. This is the mirror of the empty-hands defect: there, an id had no mesh; " +
                "here, a mesh has no id.");

            // A second id silently stealing an existing index would render the WRONG weapon — the crossed-visual
            // class (soak-224) rather than empty hands, which a visibility-only assert passes straight through.
            Assert.IsEmpty(collisions,
                "TWO ids map to ONE held-visual mesh, so at least one renders the WRONG weapon: " +
                string.Join("; ", collisions));

            // Staleness guard: if the catalog or the lineup is ever gutted, the loops above go vacuous and every
            // assert above passes on an empty set. Pin the shipped count so that cannot happen quietly.
            Assert.AreEqual(HeldWeaponCycleDebug.WeaponNodeNames.Length, reached.Count,
                "every seated mesh is claimed by exactly one obtainable id (5 weapon types x 3 tiers, roster " +
                "CLOSED per DECISIONS 2026-07-27) — a mismatch here means this test measured a truncated set");
        }

        private static string Explain(List<string> unmapped)
        {
            if (unmapped.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            sb.Append("EMPTY HANDS — ").Append(unmapped.Count)
              .Append(" weapon id(s) can be crafted/found and selected on the belt but render NOTHING in the hand: ");
            for (int i = 0; i < unmapped.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('`').Append(unmapped[i]).Append('`');
            }
            sb.Append(".\nEach of these has no held-visual selection predicate / family index. To fix ONE id:\n")
              .Append("  1. Inventory.cs — add `public bool Is<X>SelectedInBelt => Model.IsSelectedBeltItem(ItemCatalog.<X>Id);`\n")
              .Append("  2. HeldWeaponCycleDebug.cs — name its family index const (the WeaponNodeNames slot for its mesh)\n")
              .Append("     and map the predicate to that index in a selection→index map.\n")
              .Append("  3. HeldWeaponCycleDebug.HeldVisualIndexFor — compose that map in, so BOTH\n")
              .Append("     HeldAxe.ShouldShow (the mesh) and CastawayFingerCurl (the grip) see it. Step 3 is the one\n")
              .Append("     that is always forgotten; steps 1-2 alone still render empty hands.\n")
              .Append("If the id is deliberately not held in the hand, declare it in NotHeldVisualById with a reason.");
            return sb.ToString();
        }
    }
}
