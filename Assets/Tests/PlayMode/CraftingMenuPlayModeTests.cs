using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode gate for the crafting-table place-to-build + recipe-menu flow (ticket 86camz9uz /
    /// crafting-redesign ①) — the end-to-end interaction the isolated EditMode logic tests can't cover:
    ///   • place the table (all-or-nothing material debit) → the menu opens → craft a WOOD axe → it lands
    ///     on the belt (the strip-test spine);
    ///   • opening the menu PUSHES UiInputGate (modal) so a menu click can never leak to a world verb (the
    ///     ① click-guard constraint);
    ///   • the table-cost gate refuses placement when materials are short (no build, no debit).
    /// Drives the REAL public seams (EnterPlacement/TryConfirm/Open/CraftRecipe) — no synthetic UI pointer,
    /// the InventoryUI BeginDrag/EndDrag test-seam precedent. The VISUAL read is the shipped-build capture.
    /// </summary>
    public class CraftingMenuPlayModeTests
    {
        private GameObject _invGo, _playerGo, _tableGo, _ghostGo, _menuGo, _placeGo;
        private Inventory _inv;
        private CraftingTable _table;
        private CraftingMenuUI _menu;
        private CraftingTablePlacement _place;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;
            _playerGo.transform.rotation = Quaternion.identity; // forward = +Z

            _tableGo = new GameObject("CraftingTable");
            var visual = new GameObject("TableVisual").transform;
            visual.SetParent(_tableGo.transform, false);
            _table = _tableGo.AddComponent<CraftingTable>();
            _table.visual = visual;

            _ghostGo = new GameObject("Ghost");
            var ghostVisual = new GameObject("GhostVisual").transform;
            ghostVisual.SetParent(_ghostGo.transform, false);

            _menuGo = new GameObject("CraftingMenuUI");
            var doc = _menuGo.AddComponent<UIDocument>();
            _menu = _menuGo.AddComponent<CraftingMenuUI>();
            _menu.document = doc;
            _menu.inventory = _inv;
            _menu.table = _table;
            _menu.player = _playerGo.transform;

            _placeGo = new GameObject("CraftingTablePlacement");
            _place = _placeGo.AddComponent<CraftingTablePlacement>();
            _place.inventory = _inv;
            _place.player = _playerGo.transform;
            _place.table = _table;
            _place.ghost = _ghostGo.transform;
            _place.menu = _menu;
            _place.groundMask = 0; // flat-ground fallback → placement is valid on the test rig

            // Ensure the world-input gate starts clean for the modal assertion.
            _menu.Close();
        }

        [TearDown]
        public void TearDown()
        {
            if (_menu != null) _menu.Close(); // pop UiInputGate if left open
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_tableGo);
            Object.Destroy(_ghostGo);
            Object.Destroy(_menuGo);
            Object.Destroy(_placeGo);
        }

        [UnityTest]
        public IEnumerator PlaceTable_OpensMenu_CraftWoodAxe_LandsOnBelt()
        {
            yield return null; // let Awake/OnEnable settle

            // Enough for the table (5 wood + 3 stone) AND a wood axe (3 wood) after.
            _inv.AddWood(10);
            _inv.Model.AddItem(_inv.Catalog.ById(ItemCatalog.StoneId), 3);

            Assert.IsTrue(_place.CanAffordTable(), "10 wood + 3 stone affords the 5w+3s table");
            _place.EnterPlacement();
            yield return null;
            Assert.IsTrue(_place.IsPlacing, "entered placement mode");
            Assert.IsTrue(_place.IsCurrentPlacementValid, "the ghost pose is valid on flat ground");

            Assert.IsTrue(_place.TryConfirm(), "confirm builds the table");
            Assert.IsTrue(_table.IsBuilt, "the table is revealed (placed)");
            Assert.AreEqual(5, _inv.WoodCount, "table debited 5 wood (10 - 5)");
            Assert.AreEqual(0, _inv.StoneCount, "table debited 3 stone (3 - 3)");
            Assert.IsTrue(_menu.IsOpen, "placing the table opens the recipe menu (explicit handoff)");

            // Craft the WOOD axe through the menu's real click handler.
            Recipe woodAxe = null;
            foreach (var r in _menu.Recipes)
                if (r.Tier == CraftTier.Wood && r.Tool == CraftTool.Axe) { woodAxe = r; break; }
            Assert.IsNotNull(woodAxe, "the menu carries the wood-axe recipe");
            Assert.AreEqual(RecipeRowState.Craftable, _menu.RowStateOf(woodAxe),
                "with the table placed + wood in hand, the wood axe row is Craftable");

            Assert.IsTrue(_menu.CraftRecipe(woodAxe), "clicking the wood-axe row crafts it");
            Assert.AreEqual(5 - CraftingRecipeBook.WoodAxeWood, _inv.WoodCount, "the wood axe debited its wood cost");
            Assert.IsTrue(_inv.Model.OwnsItem(ItemCatalog.AxeWoodId), "the wood axe is on the belt");
        }

        // === ③ strip-test (ticket 86camz9vh): IRON tier LIVE — with the table placed + iron ingots owned, the
        // IRON axe row is Craftable + crafts the iron axe to the belt (the forged upgrade earned at the table). ===
        [UnityTest]
        public IEnumerator PlacedTable_WithIronIngots_IronAxeRow_CraftableAndCraftsToBelt()
        {
            yield return null;
            _table.Reveal(Vector3.zero, Quaternion.identity); // table placed → WOOD unlocked; IRON needs an ingot
            _inv.AddWood(10);

            Recipe ironAxe = null;
            foreach (var r in _menu.Recipes)
                if (r.Tier == CraftTier.Iron && r.Tool == CraftTool.Axe) { ironAxe = r; break; }
            Assert.IsNotNull(ironAxe, "the menu carries the iron-axe recipe");
            Assert.IsFalse(ironAxe.Placeholder, "③ flips the IRON row LIVE (no longer a Locked placeholder)");

            // Before any ingot: the IRON tier is LOCKED (unlock = first-iron-ingot-owned, §7-C).
            Assert.AreEqual(RecipeRowState.Locked, _menu.RowStateOf(ironAxe),
                "IRON stays Locked until the player has ever owned an iron ingot (the forge smelt gate)");

            // Smelt an ingot (seed it — the forge #292 loop is tested separately) → IRON unlocks + is affordable.
            _inv.Model.AddItem(_inv.Catalog.ById(ItemCatalog.IronIngotId), 5);
            Assert.AreEqual(RecipeRowState.Craftable, _menu.RowStateOf(ironAxe),
                "with an iron ingot owned + wood in hand, the iron axe row is Craftable");

            Assert.IsTrue(_menu.CraftRecipe(ironAxe), "clicking the iron-axe row crafts it");
            Assert.AreEqual(10 - CraftingRecipeBook.IronAxeWood, _inv.WoodCount, "the iron axe debited its wood cost");
            Assert.AreEqual(5 - CraftingRecipeBook.IronAxeIngot, _inv.Model.CountItem(ItemCatalog.IronIngotId),
                "the iron axe debited its iron-ingot cost (all-or-nothing)");
            Assert.IsTrue(_inv.Model.OwnsItem(ItemCatalog.AxeIronId), "the iron axe (axe_iron) is on the belt");
        }

        [UnityTest]
        public IEnumerator OpenMenu_PushesUiInputGate_SwallowsWorldInput()
        {
            yield return null;
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "gate starts closed");

            _menu.Open();
            Assert.IsTrue(_menu.IsOpen, "menu open");
            Assert.IsTrue(UiInputGate.CaptureWorldInput,
                "opening the menu is MODAL — world verbs (chop/mine/consume/attack/loot) are swallowed, so a " +
                "menu click can never leak to a world verb (the ① click-guard constraint)");

            _menu.Close();
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "closing the menu releases the gate");
        }

        [UnityTest]
        public IEnumerator PlaceTable_ShortMats_EntersButInvalid_NoBuild_NoDebit()
        {
            yield return null;
            _inv.AddWood(4); // short — the table needs 5 wood
            _inv.Model.AddItem(_inv.Catalog.ById(ItemCatalog.StoneId), 3);

            Assert.IsFalse(_place.CanAffordTable(), "4 wood < 5 → can't afford the table");

            // F4: entering placement is NO LONGER gated on affordability — you enter empty-handed to SEE the
            // "need materials" red cue. The ghost reads INVALID and confirm is refused (no build, no debit).
            _place.EnterPlacement();
            yield return null;
            Assert.IsTrue(_place.IsPlacing, "placement STARTS even when short (so the red 'need materials' cue shows)");
            Assert.IsFalse(_place.IsCurrentPlacementValid,
                "F4: with materials short the ghost reads INVALID (red / 'need N wood + M stone')");

            Assert.IsFalse(_place.TryConfirm(), "confirm is REFUSED while unaffordable");
            Assert.IsFalse(_table.IsBuilt, "no table built");
            Assert.AreEqual(4, _inv.WoodCount, "no wood debited (all-or-nothing)");
            Assert.AreEqual(3, _inv.StoneCount, "no stone debited");

            _place.Cancel(); // release the modal gate deterministically
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "cancel releases the placement gate");
        }

        [UnityTest]
        public IEnumerator UseTable_ViaE_OpensMenu_NearestInteractableWins()
        {
            yield return null;

            // DETERMINISM (F5-fix, 2026-07-18): F5 is the E-ARBITER test (nearest-in-range + menu-open +
            // precedence), NOT the placement flow — so build the table DIRECTLY at a KNOWN in-range pose via the
            // same Reveal() the placement confirm calls. This makes the test fully deterministic and
            // camera-independent. The placement ghost path depends on Camera.main + the mouse cursor, and headless
            // Input.mousePosition=(0,0) makes the ghost land at an arbitrary spot: far → OUTSIDE openRadius
            // (ResolveNearestPickable returns null — the CI red), or on top of the player → invalid (TryConfirm
            // refuses). Both are placement-rig artifacts, not F5 production bugs (the shipped build places the
            // table under the real cursor and the player then walks into openRadius). The placement
            // build/debit/menu-open handoff is covered by PlaceTable_OpensMenu_CraftWoodAxe_LandsOnBelt.
            _table.Reveal(new Vector3(0f, 0f, 1.5f), Quaternion.identity); // 1.5u ahead, inside openRadius (2.5)
            Assert.IsTrue(_table.IsBuilt, "table built (revealed at a known in-range pose)");
            Assert.IsFalse(UiInputGate.CaptureWorldInput, "no modal gate held (built directly; menu never opened)");

            // F5: the built table is USED with E via the player-side PickableLooter (nearest-interactable). The
            // menu is an IPickable ("use" verb) — no C-reopen.
            Assert.IsTrue(((IPickable)_menu).CanLoot, "built + closed → the table is E-usable");
            Assert.AreEqual("crafting table", ((IPickable)_menu).DisplayName);
            Assert.AreEqual("use", ((IPickable)_menu).GatherVerb, "the prompt reads 'Press E to use crafting table'");

            var looterGo = new GameObject("PickableLooter");
            var looter = looterGo.AddComponent<PickableLooter>();
            looter.inventory = _inv;
            looter.player = _playerGo.transform; // at origin; table revealed 1.5u out, within openRadius (2.5)
            looter.DiscoverPickables();

            Assert.AreSame(_menu, looter.ResolveNearestPickable(_playerGo.transform.position),
                "the table is the only in-range interactable → it is the nearest");
            Assert.IsTrue(looter.TryLootNearest(), "E on the table opens the menu");
            Assert.IsTrue(_menu.IsOpen, "the menu opened via the E-interact arbiter");
            _menu.Close();

            // Now put a loose STONE CLOSER than the table — the nearer interactable must win (precedence).
            var stoneGo = new GameObject("Stone");
            stoneGo.transform.position = new Vector3(0f, 0f, 1f); // 1u from player < the table's 1.5u
            var stone = stoneGo.AddComponent<StoneProp>();
            stone.inventory = _inv;
            looter.DiscoverPickables();

            Assert.AreSame(stone, looter.ResolveNearestPickable(_playerGo.transform.position),
                "the nearer stone wins over the table (nearest-interactable precedence)");
            int stoneBefore = _inv.StoneCount;
            Assert.IsTrue(looter.TryLootNearest(), "E loots the nearer stone (NOT the table)");
            Assert.IsFalse(_menu.IsOpen, "the table menu did NOT open — the stone was nearer");
            Assert.AreEqual(stoneBefore + 1, _inv.StoneCount, "the stone was looted");

            Object.Destroy(looterGo);
            Object.Destroy(stoneGo);
        }
    }
}
