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
        public IEnumerator PlaceTable_ShortMats_NoBuild_NoDebit()
        {
            yield return null;
            _inv.AddWood(4); // short — the table needs 5 wood
            _inv.Model.AddItem(_inv.Catalog.ById(ItemCatalog.StoneId), 3);

            Assert.IsFalse(_place.CanAffordTable(), "4 wood < 5 → can't afford the table");
            _place.EnterPlacement();
            Assert.IsFalse(_place.IsPlacing, "placement refuses to start when the table is unaffordable");
            Assert.IsFalse(_table.IsBuilt, "no table built");
            Assert.AreEqual(4, _inv.WoodCount, "no wood debited (all-or-nothing)");
            Assert.AreEqual(3, _inv.StoneCount, "no stone debited");
        }
    }
}
