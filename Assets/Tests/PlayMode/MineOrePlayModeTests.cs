using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the MINE mechanic (ticket 86cakkmr0 / I-2 — the iron-chain sibling of the chop verb).
    /// Proves, driving the player transform directly + the mine-click via the programmatic
    /// <see cref="MineOre.RequestMineClick"/> seam (mining is active-left-click, NOT proximity-auto; a headless run
    /// can't inject a real mouse button):
    ///
    ///   FOLDED NEGATIVE (Tess PR #268 NIT 2) — pickaxe selected + in range + NO click ⇒ NO strike/no ore (locks
    ///        out the #224 proximity-auto regression class);
    ///   the TRIGGER — ONE click in range (pickaxe selected) lands exactly ONE strike (one strike per click);
    ///   the SELECTION GATE — no pickaxe / pickaxe owned but not selected ⇒ a click does nothing;
    ///   the LOOT LOOP — strikesToBreak clicks BREAK a node → an ore pile spawns → E-loot adds iron_ore to the
    ///        inventory (iron_ore is a Resource → inventory-grid, the wood/LogPile path — NOT the belt strip);
    ///   the SWING — each strike fires the melee swing (CastawayCharacter.TriggerChop latches ChopTriggered);
    ///   HOLD-TO-MINE — holding LMB repeats swings until the node breaks / release;
    ///   REGROW — a broken node regrows after the (tweakable) timer into a mineable node;
    ///   the RARITY DIAL — ActiveNodeCount enables/disables pool nodes live (the ore-rarity difficulty dial).
    ///
    /// The pure guard truth-table is unit-asserted headlessly in EditMode (MineClickGateTests over
    /// MineOre.ShouldMineOnClick). Headless time discipline (unity-conventions.md): all waits are real
    /// Time.time / WaitForSeconds windows; the stable-clock harness (Time.captureDeltaTime) pins the cadence
    /// gates deterministically (the 86cajt6j8 fix — the chop-cadence over-count trap applies identically here).
    /// </summary>
    public class MineOrePlayModeTests
    {
        private GameObject _invGo, _playerGo, _charGo, _spawnerGo, _looterGo, _mineGo, _nodeRoot;
        private Inventory _inv;
        private CastawayCharacter _character;
        private OrePileSpawner _spawner;
        private PickableLooter _looter;
        private MineOre _mine;

        private const float StableStepSeconds = 0.01f;
        private bool _gateTracked;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = StableStepSeconds; // fixed virtual clock → deterministic cadence (86cajt6j8)
            LogAssert.ignoreFailingMessages = true;    // bare-rig CastawayCharacter "modelPrefab not wired" etc.

            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;

            _charGo = new GameObject("CastawayAvatar");
            _character = _charGo.AddComponent<CastawayCharacter>();

            _spawnerGo = new GameObject("OrePileSpawner");
            _spawner = _spawnerGo.AddComponent<OrePileSpawner>();
            _spawner.OreYield = 3;
            _spawner.DespawnSeconds = 180f;

            _looterGo = new GameObject("PickableLooter");
            _looter = _looterGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;

            // A small ore-node POOL: node 0 in range of the player at origin; nodes 1/2 out of range.
            _nodeRoot = new GameObject("OreNodes");
            MakeNode(_nodeRoot.transform, new Vector3(0.5f, 0f, 0f));  // node 0 — in range
            MakeNode(_nodeRoot.transform, new Vector3(10f, 0f, 0f));   // node 1 — out of range
            MakeNode(_nodeRoot.transform, new Vector3(12f, 0f, 0f));   // node 2 — out of range

            _mineGo = new GameObject("MineOre");
            _mine = _mineGo.AddComponent<MineOre>();
            _mine.inventory = _inv;
            _mine.player = _playerGo.transform;
            _mine.character = _character;
            _mine.orePileSpawner = _spawner;
            _mine.nodeRoot = _nodeRoot.transform;
            _mine.inventoryUI = null;      // no UI rig → the over-UI guard is skipped
            _mine.mineRadius = 2.2f;
            _mine.strikesToBreak = 3;
            _mine.strikeInterval = 0f;     // no click cooldown (each requested click strikes)
            _mine.swingImpactDelaySeconds = 0.1f;
            _mine.swingClipLengthSeconds = 0.3f;
            _mine.regrowthMinSeconds = 0.4f;
            _mine.regrowthMaxSeconds = 0.6f;
            _mine.regrowSeed = 24680;
            _mine.activeNodeCount = 3;     // all 3 pool nodes enabled for the mine tests
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo); Object.Destroy(_playerGo); Object.Destroy(_charGo);
            Object.Destroy(_spawnerGo); Object.Destroy(_looterGo); Object.Destroy(_mineGo); Object.Destroy(_nodeRoot);
            foreach (var pile in Object.FindObjectsByType<OrePile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pile.gameObject);
            LogAssert.ignoreFailingMessages = false;
            Time.captureDeltaTime = 0f;
        }

        private static void MakeNode(Transform parent, Vector3 pos)
        {
            var node = new GameObject(MineOre.OreNodeName);
            node.transform.SetParent(parent, false);
            node.transform.position = pos;
            node.AddComponent<MeshRenderer>(); // so IsVisible has a renderer to toggle
        }

        // Put a stone pickaxe on the belt AND make it the selected belt item (the mine gate).
        private void SelectPickaxe()
        {
            var slot = _inv.Model.AddToolToBelt(_inv.Catalog.ById(ItemCatalog.PickaxeStoneId));
            Assert.IsTrue(slot.HasValue, "precondition: the stone pickaxe lands on the belt");
            _inv.Model.SelectBelt(slot.Value.Index);
            Assert.IsTrue(MineOre.IsPickaxeSelected(_inv), "precondition: the pickaxe is the SELECTED belt item");
        }

        private int OreCount() => _inv.Model.CountItem(ItemCatalog.IronOreId);

        // Request ONE mine-click then advance frames until the strike EFFECT has LANDED AT IMPACT. Mirrors the chop
        // ClickChop() helper. A rejected click schedules nothing → ImpactPending stays false → the wait is one frame.
        private IEnumerator ClickMine()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.RequestMineClick();
            yield return null; // consume the latch → schedule the impact (or reject the click)
            float start = Time.time;
            while (_mine.ImpactPending && Time.time - start < 1f) yield return null;
            yield return null; // one more frame for the impact-resolve Update to apply the effect
        }

        // === FOLDED NEGATIVE (Tess NIT 2) — at a node WITH a pickaxe selected but NO click does NOTHING ===
        [UnityTest]
        public IEnumerator AtNodeWithSelectedPickaxe_NoClick_DoesNotMine()
        {
            yield return null; // let MineOre.Start discover the pool
            SelectPickaxe();

            // Stand in range, pickaxe selected, but NEVER request a click — the #224 proximity-auto would have
            // mined here; the active-left-click trigger must NOT.
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.AreEqual(0, OreCount(), "standing at the node WITHOUT a click must not mine (no proximity-auto)");
            Assert.AreEqual(0, _mine.StrikesOn(0), "no click -> no strikes land (proximity-auto is not present)");
            Assert.IsFalse(_mine.IsBrokenOn(0), "no click -> the node is never broken");
        }

        // === THE TRIGGER — ONE click in range (pickaxe selected) lands EXACTLY ONE strike ===
        [UnityTest]
        public IEnumerator OneClickInRange_LandsExactlyOneStrike()
        {
            yield return null;
            SelectPickaxe();

            yield return ClickMine();
            Assert.AreEqual(1, _mine.StrikesOn(0), "ONE click -> exactly ONE strike (one strike per click)");
            Assert.IsFalse(_mine.IsBrokenOn(0), "one strike of three does not break the node");

            // A second frame WITHOUT a new click does not strike again.
            yield return null;
            Assert.AreEqual(1, _mine.StrikesOn(0), "no further strike without a NEW click (one strike per click)");
        }

        // === SELECTION GATE (negative) — NO pickaxe at all + a click at the node does nothing ===
        [UnityTest]
        public IEnumerator ClickAtNodeWithoutPickaxe_DoesNothing()
        {
            yield return null;
            Assert.IsFalse(MineOre.IsPickaxeSelected(_inv), "precondition: no pickaxe selected");

            for (int i = 0; i < 5; i++) yield return ClickMine();

            Assert.AreEqual(0, _mine.StrikesOn(0), "no pickaxe -> no strikes land");
            Assert.AreEqual(0, OreCount(), "no pickaxe -> no ore, even clicking at the node");
            Assert.IsFalse(_mine.IsBrokenOn(0), "no pickaxe -> the node is never broken");
        }

        // === SELECTION GATE (negative) — pickaxe OWNED but NOT the selected belt item does nothing ===
        [UnityTest]
        public IEnumerator ClickWithPickaxeOwnedButNotSelected_DoesNothing()
        {
            yield return null;
            var slot = _inv.Model.AddToolToBelt(_inv.Catalog.ById(ItemCatalog.PickaxeStoneId));
            Assert.IsTrue(slot.HasValue, "precondition: pickaxe owned (on the belt)");
            _inv.Model.SelectBelt((slot.Value.Index + 1) % _inv.BeltSlotCount); // select a different slot
            Assert.IsFalse(MineOre.IsPickaxeSelected(_inv), "precondition: pickaxe owned but NOT selected");

            for (int i = 0; i < 5; i++) yield return ClickMine();
            Assert.AreEqual(0, _mine.StrikesOn(0), "pickaxe not SELECTED -> no strikes (the selection gate)");
            Assert.AreEqual(0, OreCount(), "pickaxe not selected -> no ore");
        }

        // === THE LOOT LOOP — strikesToBreak clicks BREAK the node → an ore pile spawns → E loots iron_ore ===
        [UnityTest]
        public IEnumerator BreakNode_DropsOrePile_ELoots_IronOreIntoInventory()
        {
            yield return null;
            SelectPickaxe();
            Assert.AreEqual(0, OreCount(), "precondition: no ore yet");

            for (int i = 0; i < _mine.strikesToBreak; i++) yield return ClickMine();

            Assert.IsTrue(_mine.IsBrokenOn(0), "strikesToBreak clicks in range break the node");
            Assert.AreEqual(0, OreCount(), "breaking never banks ore directly — the ore is in the dropped pile until looted");

            var pile = Object.FindObjectOfType<OrePile>();
            Assert.IsNotNull(pile, "an ore pile spawned on break");
            Assert.AreEqual(_spawner.OreYield, pile.OreRemaining, "the pile holds the node's whole ore yield");

            // E loots the WHOLE pile → iron_ore lands in the inventory (via the pile's registration with the looter).
            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(_spawner.OreYield, OreCount(),
                "one E grabs the whole pile -> the node's full ore yield lands in the inventory (iron_ore Resource)");
            Assert.IsFalse(pile.IsAvailable, "the emptied pile is consumed");
        }

        // === THE SWING — a landed strike FIRES the melee swing (TriggerChop latches ChopTriggered) ===
        [UnityTest]
        public IEnumerator Strike_FiresTheSwing()
        {
            yield return null;
            SelectPickaxe();
            Assert.IsFalse(_character.ConsumeChopTriggered(), "precondition: no swing fired yet");

            _mine.Mine(); // drive a single strike directly (isolates the swing-trigger seam)
            Assert.AreEqual(1, _mine.StrikesOn(0), "the direct strike landed");
            Assert.IsTrue(_character.ChopTriggered, "a landed strike must fire the melee swing (TriggerChop)");
            Assert.IsTrue(_character.ConsumeChopTriggered(), "consuming the latch reports the fired trigger");
            yield return null;
        }

        // === HOLD-TO-MINE — holding LMB repeats swings (N swings land N strikes), stops on release ===
        [UnityTest]
        public IEnumerator HoldingLmb_RepeatsSwings_UntilReleased()
        {
            yield return null;
            SelectPickaxe();
            _mine.strikesToBreak = 6; // headroom to observe several repeat swings before break

            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.SetMineHeld(true);
            for (int i = 0; i < 3; i++) yield return StepHeldSwing();

            Assert.AreEqual(3, _mine.StrikesOn(0), "holding LMB repeats swings — 3 strikes with NO extra presses");
            Assert.IsTrue(_mine.IsMineChainActive, "the chain is active while LMB is held + the node stands");
            Assert.IsFalse(_mine.IsBrokenOn(0), "not yet broken (strikesToBreak=6, only 3 swings)");

            _mine.SetMineHeld(false);
            yield return null;
            Assert.IsFalse(_mine.IsMineChainActive, "releasing LMB stops the chain");
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(3, _mine.StrikesOn(0), "after release, NO further strikes land");
        }

        private IEnumerator StepHeldSwing()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            yield return null;
            float start = Time.time;
            while (_mine.ImpactPending && Time.time - start < 2f) yield return null;
            yield return null;
            start = Time.time;
            while (_mine.SwingInProgress && Time.time - start < 2f) yield return null;
        }

        // === REGROW — a broken node regrows after the timer into a mineable node ===
        [UnityTest]
        public IEnumerator BrokenNode_RegrowsAfterTimer_IntoAMineableNode()
        {
            yield return null;
            SelectPickaxe();

            for (int i = 0; i < _mine.strikesToBreak; i++) yield return ClickMine();
            Assert.IsTrue(_mine.IsBrokenOn(0), "node broken");

            // Wait past the max regrow + tweens — the node regrows, mineable anew.
            float start = Time.time;
            while (Time.time - start < _mine.regrowthMaxSeconds + 2.5f && _mine.IsBrokenOn(0)) yield return null;
            Assert.IsFalse(_mine.IsBrokenOn(0), "the node regrew after the timer");
            Assert.AreEqual(0, _mine.StrikesOn(0), "a regrown node resets its strike count");

            // And it is mineable again — a click lands a fresh strike.
            yield return ClickMine();
            Assert.AreEqual(1, _mine.StrikesOn(0), "the regrown node is mineable again — a click lands a strike");
        }

        // === THE RARITY DIAL — ActiveNodeCount enables/disables pool nodes live (ore-rarity difficulty) ===
        [UnityTest]
        public IEnumerator RarityDial_EnablesDisablesPoolNodesLive()
        {
            yield return null; // Start discovers the pool + applies activeNodeCount=3 (all enabled)
            Assert.AreEqual(3, _mine.NodeCount, "the pool has 3 nodes");
            Assert.IsTrue(_mine.IsNodeEnabled(0) && _mine.IsNodeEnabled(1) && _mine.IsNodeEnabled(2),
                "activeNodeCount=3 enables all 3 pool nodes");

            // Drop the rarity dial to 1 (sparse) — only the first node stays enabled; the rest disable.
            _mine.SetActiveNodeCount(1);
            yield return null;
            Assert.AreEqual(1, _mine.ActiveNodeCount, "the dial clamped to the pool + applied");
            Assert.IsTrue(_mine.IsNodeEnabled(0), "node 0 stays enabled at rarity 1");
            Assert.IsFalse(_mine.IsNodeEnabled(1), "node 1 is disabled at rarity 1 (sparse)");
            Assert.IsFalse(_mine.IsNodeEnabled(2), "node 2 is disabled at rarity 1 (sparse)");

            // Raise it back to 3 — the nodes re-enable (a live dial, no rebuild).
            _mine.SetActiveNodeCount(3);
            yield return null;
            Assert.IsTrue(_mine.IsNodeEnabled(1) && _mine.IsNodeEnabled(2), "raising the dial re-enables nodes live");

            // A dial ABOVE the pool size clamps to the pool (no over-reach).
            _mine.SetActiveNodeCount(99);
            yield return null;
            Assert.AreEqual(3, _mine.ActiveNodeCount, "a dial above the pool clamps to the pool size (no over-reach)");
        }
    }
}
