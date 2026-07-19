using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard for the BUILD MENU (ticket 86catpvpa) — the C build menu that fronts the ① ghost-
    /// placement flow for ALL placeable structures. Covers the AC success-test set:
    ///   • the pure row-state truth-table (Buildable / Unaffordable / Built);
    ///   • the menu lists exactly the registered placeable set (registration seam, idempotent + null-safe);
    ///   • REGRESSION GUARD — selecting a BUILDABLE row enters the ① ghost flow (IsPlacing true) + closes the
    ///     menu; selecting an UNAFFORDABLE / already-BUILT row is NON-interactive (no placement, no close);
    ///   • the shared IBuildPlaceable seam adapters on the real CraftingTablePlacement + ForgePlacement.
    /// The menu logic is layout-independent (unresolved UIDocument only skips the repaint), so no scene / no
    /// laid-out panel is needed here; the VISUAL is proven by the shipped-build capture (-verifyBuildMenu).
    /// </summary>
    public class BuildMenuTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private T New<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        [TearDown]
        public void TearDown()
        {
            // Hermetic: drain any leaked UiInputGate push (EnterPlacement pushes it; the static does NOT
            // auto-reset in EditMode, so keep it balanced for sibling test classes).
            int guard = 0;
            while (UiInputGate.CaptureWorldInput && guard++ < 16) UiInputGate.PopPanel();
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // ---- pure row-state truth-table ----

        [Test]
        public void RowState_Buildable_WhenAffordable_AndNotBuilt()
            => Assert.AreEqual(BuildRowState.Buildable,
                BuildMenuUI.ResolveRowState(canAfford: true, isBuilt: false),
                "affordable + not built → Buildable (the row is active + selectable)");

        [Test]
        public void RowState_Unaffordable_WhenNotAffordable_AndNotBuilt()
            => Assert.AreEqual(BuildRowState.Unaffordable,
                BuildMenuUI.ResolveRowState(canAfford: false, isBuilt: false),
                "unbuilt but can't afford → Unaffordable (greyed, cost shown, non-interactive)");

        [Test]
        public void RowState_Built_TakesPrecedence_EvenIfAffordable()
            => Assert.AreEqual(BuildRowState.Built,
                BuildMenuUI.ResolveRowState(canAfford: true, isBuilt: true),
                "already built → Built (hidden; one-per-world) regardless of affordability");

        // ---- registration seam (③/⑤ consume this) ----

        [Test]
        public void Register_ListsExactlyTheRegisteredSet_Idempotent_NullSafe()
        {
            var menu = New<BuildMenuUI>("BuildMenu");
            var a = new FakePlaceable { BuildDisplayName = "Crafting Table" };
            var b = new FakePlaceable { BuildDisplayName = "Forge" };

            menu.RegisterPlaceable(a);
            menu.RegisterPlaceable(b);
            menu.RegisterPlaceable(a);   // idempotent — already registered
            menu.RegisterPlaceable(null); // null-safe — no-op

            Assert.AreEqual(2, menu.Placeables.Count, "the menu lists exactly the registered placeable set");
            Assert.AreSame(a, menu.Placeables[0]);
            Assert.AreSame(b, menu.Placeables[1]);
        }

        // ---- REGRESSION GUARD: select → enters the ① ghost flow (fake seam, deterministic) ----

        [Test]
        public void Select_Buildable_EntersGhostFlow_ReturnsTrue()
        {
            var menu = New<BuildMenuUI>("BuildMenu");
            var p = new FakePlaceable { CanAffordBuild = true, IsBuildComplete = false };
            menu.RegisterPlaceable(p);

            bool selected = menu.SelectPlaceable(p);

            Assert.IsTrue(selected, "an affordable, unbuilt row selects");
            Assert.AreEqual(1, p.BeginCalls,
                "REGRESSION GUARD: selecting a buildable row MUST enter the ① ghost flow (BeginBuildPlacement)");
        }

        [Test]
        public void Select_Unaffordable_IsNonInteractive()
        {
            var menu = New<BuildMenuUI>("BuildMenu");
            var p = new FakePlaceable { CanAffordBuild = false, IsBuildComplete = false };
            menu.RegisterPlaceable(p);

            bool selected = menu.SelectPlaceable(p);

            Assert.IsFalse(selected, "an unaffordable (greyed) row is NON-interactive — selection refused");
            Assert.AreEqual(0, p.BeginCalls, "an unaffordable row must NOT enter placement");
        }

        [Test]
        public void Select_AlreadyBuilt_IsNonInteractive()
        {
            var menu = New<BuildMenuUI>("BuildMenu");
            var p = new FakePlaceable { CanAffordBuild = true, IsBuildComplete = true };
            menu.RegisterPlaceable(p);

            Assert.IsFalse(menu.SelectPlaceable(p), "an already-built structure's row is non-interactive");
            Assert.AreEqual(0, p.BeginCalls, "a built row must NOT re-enter placement");
        }

        // ---- REGRESSION GUARD: full chain on the REAL CraftingTablePlacement (menu → ① ghost) ----

        [Test]
        public void Select_RealTablePlacement_UnaffordableRefused_ThenAffordableEntersGhost()
        {
            var inv = New<Inventory>("Inv");
            var place = New<CraftingTablePlacement>("TablePlacement");
            place.inventory = inv;
            place.woodCost = 5; place.stoneCost = 3; // the §5 defaults (explicit)
            // table + ghost left null — EnterPlacement is null-safe (no camera / no ghost needed here).

            var menu = New<BuildMenuUI>("BuildMenu");
            menu.RegisterPlaceable(place);

            // Empty pack → the row is Unaffordable + selection is refused + NO ghost flow entered.
            Assert.AreEqual(BuildRowState.Unaffordable, menu.RowStateOf(place));
            Assert.IsFalse(menu.SelectPlaceable(place), "greyed row refused");
            Assert.IsFalse(place.IsPlacing, "an unaffordable selection must NOT enter the ghost flow");

            // Grant the table's materials → the row becomes Buildable → selecting it ENTERS the ① ghost flow.
            inv.AddWood(5);
            inv.Model.AddItem(inv.Catalog.ById(ItemCatalog.StoneId), 3);
            Assert.AreEqual(BuildRowState.Buildable, menu.RowStateOf(place));

            bool selected = menu.SelectPlaceable(place);
            Assert.IsTrue(selected, "an affordable table row selects");
            Assert.IsTrue(place.IsPlacing,
                "REGRESSION GUARD: selecting the table row MUST enter the ① free-cursor ghost placement flow");

            place.Cancel(); // release the world-input gate (hermetic)
        }

        // ---- the shared seam adapters on the real placement components ----

        [Test]
        public void TablePlacement_ImplementsSeam_WithStableIdentity_AndCosts()
        {
            var place = New<CraftingTablePlacement>("TablePlacement");
            place.woodCost = 5; place.stoneCost = 3;
            IBuildPlaceable seam = place;
            Assert.AreEqual("Crafting Table", seam.BuildDisplayName, "stable per-structure identifier");
            Assert.AreEqual(5, seam.BuildWoodCost);
            Assert.AreEqual(3, seam.BuildStoneCost);
            Assert.IsFalse(seam.IsBuildComplete, "unbuilt (no table wired / not revealed)");
        }

        [Test]
        public void ForgePlacement_ImplementsSeam_WithStableIdentity_AndCosts()
        {
            var place = New<ForgePlacement>("ForgePlacement");
            place.woodCost = ForgePlacement.ForgeWoodCostDefault;   // 6
            place.stoneCost = ForgePlacement.ForgeStoneCostDefault; // 12
            IBuildPlaceable seam = place;
            Assert.AreEqual("Forge", seam.BuildDisplayName, "stable per-structure identifier");
            Assert.AreEqual(6, seam.BuildWoodCost);
            Assert.AreEqual(12, seam.BuildStoneCost);
            Assert.IsFalse(seam.IsBuildComplete, "unbuilt (forge not revealed)");
        }

        [Test]
        public void ForgePlacement_BeginBuildPlacement_EntersGhostFlow()
        {
            var place = New<ForgePlacement>("ForgePlacement");
            IBuildPlaceable seam = place;
            Assert.IsFalse(place.IsPlacing);
            seam.BeginBuildPlacement();
            Assert.IsTrue(place.IsPlacing, "the forge seam adapter enters the ① ghost flow (EnterPlacement)");
            place.Cancel(); // release the gate (hermetic)
        }

        // A controllable IBuildPlaceable test double — proves the menu-layer select/paint contract without
        // a scene or a real placement component (deterministic).
        private sealed class FakePlaceable : IBuildPlaceable
        {
            public string BuildDisplayName { get; set; } = "Fake";
            public int BuildWoodCost { get; set; } = 1;
            public int BuildStoneCost { get; set; } = 1;
            public bool CanAffordBuild { get; set; }
            public bool IsBuildComplete { get; set; }
            public int BeginCalls { get; private set; }
            public void BeginBuildPlacement() => BeginCalls++;
        }
    }
}
