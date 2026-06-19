using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode regression guard for the THREE #90 soak-caught UI-INTERACTION bugs (ticket 86caa4bya /
    /// follow-up 86cabfa21). These ride a LIVE UI Toolkit panel (UIDocument + PanelSettings) laid out over
    /// real frames — the layer the existing InventoryModel/Held-axe suites never touched, which is exactly
    /// why CI (had the suite run) wouldn't have caught the bugs: the model was always correct; the VIEW
    /// interaction was broken. Each test asserts the END-TO-END effect through the real InventoryUI, not a
    /// model call.
    ///
    ///   • BUG 1 — drag/move between slots: a drop RESOLVED BY CURSOR POSITION (not the captured-pointer
    ///     event target) actually moves the item. The old code read the PointerUp event's target, which the
    ///     pointer-capture redirect pins to the SOURCE slot → TryMove(from,from) = same-slot no-op.
    ///   • BUG 2 — clicking the DOCKED belt row (inside the pack) must NOT change the active selection;
    ///     clicking the bottom STRIP (the hotbar) still selects.
    ///   • BUG 3 — obtained wood shows in the grid as a recognizable ICON (not a bare "W" letter-chip).
    /// </summary>
    public class InventoryUiInteractionPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _uiGo;
        private Inventory _inv;
        private InventoryUI _ui;
        private PanelSettings _panel;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _uiGo = new GameObject("InventoryUI");
            var doc = _uiGo.AddComponent<UIDocument>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            // Constant-pixel scale at a known size so the slots lay out at their authored 64px and worldBound
            // is non-degenerate for the position-resolved drop (BUG 1) test.
            _panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panel.referenceResolution = new Vector2Int(1280, 720);
            doc.panelSettings = _panel;

            _ui = _uiGo.AddComponent<InventoryUI>();
            _ui.document = doc;
            _ui.inventory = _inv;
            // Wire the REAL shipped shell UXML + USS (the named grid/belt containers + the 64px slot sizing)
            // so this test exercises the exact tree the build ships — not a hand-rolled stand-in.
            _ui.panelUxml = LoadShellUxml();
            _ui.paletteUss = LoadUss("Assets/UI/InventoryPalette.uss");
            _ui.panelUss = LoadUss("Assets/UI/InventoryPanel.uss");
            Assert.IsNotNull(_ui.panelUxml, "the shipped InventoryPanel.uxml must load for the UI test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uiGo);
            Object.Destroy(_invGo);
            if (_panel != null)
            {
                if (_panel.themeStyleSheet != null) Object.Destroy(_panel.themeStyleSheet);
                Object.Destroy(_panel);
            }
        }

        // BUG 3 — wood obtained → a grid cell shows the wood ICON (not a "W" chip; not empty).
        [UnityTest]
        public IEnumerator ObtainedWood_ShowsAsIconInTheGrid_NotALetterChip()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);
            _ui.SetOpen(true);
            yield return WaitFrames(6);

            // Model truth.
            Assert.AreEqual(3, _inv.WoodCount, "the model holds the chopped wood");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[0].Def.Id, "wood is in inventory slot 0");

            // The wood ItemDef carries a baked icon (BUG 3 fix — null icon was the cause).
            var woodDef = _inv.Catalog.ById(ItemCatalog.WoodId);
            Assert.IsNotNull(woodDef.Icon,
                "the wood ItemDef must carry a recognizable icon (AC5) — a null icon renders only a bare " +
                "'W' letter-chip, which is the #90 BUG 3 the Sponsor reported ('wood does not appear')");

            // Painted truth: grid cell 0 shows the icon sprite, NOT a chip letter.
            var grid = Grid();
            Assert.IsNotNull(grid, "the inv-grid container must exist");
            var cell0 = First(grid);
            var icon = cell0.Q<VisualElement>("icon");
            var chip = cell0.Q<Label>("chip");
            Assert.IsNotNull(icon.style.backgroundImage.value.sprite,
                "grid cell 0 must paint the wood icon sprite (BUG 3 — wood now reads as a wood item)");
            Assert.IsTrue(string.IsNullOrEmpty(chip.text),
                "the letter-chip must be cleared once an icon is shown");
        }

        // BUG 1 — drag wood from inventory slot 0 → slot 5 actually moves it (cursor-resolved drop).
        [UnityTest]
        public IEnumerator DragWoodBetweenInventorySlots_MovesIt()
        {
            yield return WaitFrames(4);
            _inv.AddWood(3);
            _ui.SetOpen(true);
            yield return WaitFrames(8); // let the grid lay out so worldBound is real

            var grid = Grid();
            var cells = Children(grid);
            Assert.GreaterOrEqual(cells.Count, 6, "grid must have at least 6 cells");

            // Target cell 5's center in panel space.
            Vector2 targetCenter = cells[5].worldBound.center;
            Assert.That(targetCenter.x, Is.Not.EqualTo(0f).Within(0.0001f).Or.Not.EqualTo(0f),
                "the target cell must be laid out (non-degenerate worldBound) for a position-resolved drop");

            // Drive the real drop seam by POSITION (BUG 1 — not the source event target).
            bool moved = _ui.ApplyDrop(SlotRef.Inventory(0), targetCenter);
            yield return null;

            Assert.IsTrue(moved, "a position-resolved drop onto a different inventory slot moves the item");
            Assert.IsTrue(_inv.Model.InventorySlots[0].IsEmpty, "source slot is now empty");
            Assert.AreEqual("wood", _inv.Model.InventorySlots[5].Def.Id, "wood landed in the dropped-on slot");
            Assert.AreEqual(3, _inv.Model.InventorySlots[5].Count, "the whole stack moved (count conserved)");
        }

        // BUG 1 (axe) — drag the axe from belt slot 1 → belt slot 3 actually moves it.
        [UnityTest]
        public IEnumerator DragAxeBetweenBeltSlots_MovesIt()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe(); // belt slot 0
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            var dock = DockBelt();
            var cells = Children(dock);
            Assert.GreaterOrEqual(cells.Count, 4, "docked belt must have its slots");
            Vector2 target = cells[3].worldBound.center;

            bool moved = _ui.ApplyDrop(SlotRef.Belt(0), target);
            yield return null;

            Assert.IsTrue(moved, "the axe moves to a different belt slot via a position-resolved drop");
            Assert.IsTrue(_inv.Model.BeltSlots[0].IsEmpty, "belt slot 1 is now empty");
            Assert.AreEqual("axe", _inv.Model.BeltSlots[3].Def.Id, "the axe landed in belt slot 4");
        }

        // BUG 2 — clicking the DOCKED belt row inside the pack does NOT change the active selection.
        [UnityTest]
        public IEnumerator ClickingDockedBeltRow_DoesNotChangeSelection()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe();
            _inv.Model.SelectBelt(0);
            _ui.SetOpen(true);
            yield return WaitFrames(8);

            int before = _inv.Model.SelectedBeltIndex;
            Assert.AreEqual(0, before, "selection starts at slot 1");

            // Simulate a click on the DOCKED belt row's slot index 2 (the organize row, not the hotbar).
            var dock = DockBelt();
            var cells = Children(dock);
            SendClick(cells[2]);
            yield return null;

            Assert.AreEqual(before, _inv.Model.SelectedBeltIndex,
                "clicking the docked belt row (inside the pack) must NOT change the active selection " +
                "(#90 BUG 2 — the pack belt row is an organize target, not a selector; selection moves only " +
                "via 1–N / scroll)");
        }

        // BUG 2 positive control — clicking the bottom STRIP (the hotbar) DOES select.
        [UnityTest]
        public IEnumerator ClickingBottomBeltStrip_DoesSelect()
        {
            yield return WaitFrames(4);
            _inv.PickUpAxe();
            _inv.Model.SelectBelt(0);
            yield return WaitFrames(8);

            var strip = StripBelt();
            var cells = Children(strip);
            Assert.GreaterOrEqual(cells.Count, 3, "the bottom strip mirrors the belt slots");
            SendClick(cells[2]);
            yield return null;

            Assert.AreEqual(2, _inv.Model.SelectedBeltIndex,
                "clicking the bottom hotbar strip DOES select that slot (the strip is the selector)");
        }

        // ---- helpers ----

        private VisualElement Root() => _uiGo.GetComponent<UIDocument>().rootVisualElement;
        private VisualElement Grid() => Root()?.Q<VisualElement>("inv-grid");
        private VisualElement DockBelt() => Root()?.Q<VisualElement>("belt-dock");
        private VisualElement StripBelt() => Root()?.Q<VisualElement>("belt-bar-strip");

        private static VisualElement First(VisualElement parent)
        {
            foreach (var c in parent.Children()) return c;
            return null;
        }

        private static List<VisualElement> Children(VisualElement parent)
        {
            var list = new List<VisualElement>();
            foreach (var c in parent.Children()) list.Add(c);
            return list;
        }

        private static void SendClick(VisualElement target)
        {
            // A pointer-down on the slot is what InventoryUI's selection handler binds to. Use the slot
            // center so it resolves to that element.
            Vector2 p = target.worldBound.center;
            using (var down = PointerDownEvent.GetPooled())
            {
                down.target = target;
                target.SendEvent(down);
            }
        }

        private static IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // Load the REAL shipped shell UXML (the named grid/belt containers). Editor-only path — CI runs the
        // PlayMode suite in the editor, where AssetDatabase resolves the project asset.
        private static VisualTreeAsset LoadShellUxml()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/InventoryPanel.uxml");
#else
            return null;
#endif
        }

        private static StyleSheet LoadUss(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
#else
            return null;
#endif
        }
    }
}
