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
    ///   the RARITY DIAL — ActiveNodeCount enables/disables pool nodes live (the ore-rarity difficulty dial);
    ///   the STAY-LIVE GUARD — the next held swing waits for the CLIP to finish (cadence ≥ clip length), so a real
    ///        machine-gun / un-pinned-clock mining-cadence regression still REDS the suite (86camf3xe).
    ///
    /// The pure guard truth-table is unit-asserted headlessly in EditMode (MineClickGateTests over
    /// MineOre.ShouldMineOnClick); these PlayMode tests cover the live click-drives-a-strike loop.
    ///
    /// === Headless time discipline (86camf3xe — mirrors ChopTreePlayModeTests / PR #288 / 86camdk1h) ===
    /// The hold-to-mine cadence gates (swingImpactDelaySeconds + the clip-completion window) and each node's
    /// break/fade/regrow timers are TIME-SPACED across many frames. Two headless -batchmode traps invalidated the
    /// prior <c>Time.captureDeltaTime</c> pin here: (A) the old <c>while (SwingInProgress)</c>/wall-clock steppers
    /// let a HELD chain auto-re-arm the next swing before the loop saw a stable edge → OVER-COUNT (HoldingLmb 3→6);
    /// (B) the break/fade/regrow tweens rode <c>Time.unscaledDeltaTime</c>, which is ≈0 in headless -batchmode, so
    /// the regrow tween FROZE — the node never regrew (BrokenNode_RegrowsAfterTimer stayed Broken=True). So instead
    /// the TEST OWNS the clock MineOre reads (via the behavior-neutral, UNITY_INCLUDE_TESTS-stripped
    /// <see cref="MineOre.TestClock"/> seam) and ADVANCES it a fixed <see cref="StableStepSeconds"/> per stepped
    /// frame — a WORKING captureDeltaTime that also drives the tween <c>_dt</c>. Each cadence/fade/regrow window
    /// then spans a DETERMINISTIC frame count; the production gate logic is UNCHANGED (null clock → Time.time in the
    /// ship build). A bare <c>yield return null</c> is used ONLY where the assertion is that NOTHING happens.
    /// </summary>
    public class MineOrePlayModeTests
    {
        private GameObject _invGo, _playerGo, _charGo, _spawnerGo, _looterGo, _mineGo, _nodeRoot;
        private Inventory _inv;
        private CastawayCharacter _character;
        private OrePileSpawner _spawner;
        private PickableLooter _looter;
        private MineOre _mine;

        // 86camf3xe — OWNED DETERMINISTIC CLOCK step (supersedes the ineffective Time.captureDeltaTime pin). 0.01s
        // (100Hz) keeps every "tick N frames" assertion comfortably inside the smallest impact window (0.1s here).
        private const float StableStepSeconds = 0.01f;
        // The fake clock MineOre.Now reads via TestClock. Advanced a fixed StableStepSeconds per frame by Step().
        private float _now;
        private bool _gateTracked;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;                                 // reset the owned clock each test (86camf3xe)
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

            // 86camf3xe — inject the OWNED deterministic clock. The nodes are built in Start (first stepped frame)
            // via MineOre.NewNodeState, which propagates this clock to each MineableNodeState (setter + Start-time).
            _mine.TestClock = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo); Object.Destroy(_playerGo); Object.Destroy(_charGo);
            Object.Destroy(_spawnerGo); Object.Destroy(_looterGo); Object.Destroy(_mineGo); Object.Destroy(_nodeRoot);
            foreach (var pile in Object.FindObjectsByType<OrePile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pile.gameObject);
            LogAssert.ignoreFailingMessages = false;
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

        // 86camf3xe — advance the OWNED clock one fixed StableStep + tick one frame (the deterministic analog of a
        // rendered capture frame). EVERY wait that expects the production cadence/fade/regrow to progress steps via
        // these — a bare `yield return null` no longer advances the clock MineOre reads, so bare yields are used
        // ONLY where the assertion is that NOTHING happens (no click / no strike over N frames).
        private IEnumerator Step()
        {
            _now += StableStepSeconds;
            yield return null;
        }
        private IEnumerator StepFrames(int n) { for (int i = 0; i < n; i++) yield return Step(); }
        // Advance until a condition holds, bounded by a DETERMINISTIC frame budget (never a wall-clock timeout — the
        // owned clock is decoupled from real time) so a stuck condition fails fast instead of hanging the run.
        private IEnumerator StepUntil(System.Func<bool> done, int maxFrames = 6000)
        {
            int f = 0;
            while (!done() && f++ < maxFrames) yield return Step();
        }

        // Request ONE mine-click (the input-independent seam) then advance the owned clock until the strike EFFECT
        // has LANDED AT IMPACT (the click fires the swing now; the effect lands ~impactDelay later). Mirrors the chop
        // ClickChop() helper. A rejected click (out of range / no pickaxe / over-UI) schedules nothing → ImpactPending
        // stays false → StepUntil returns immediately (a ~2-frame no-op). UiInputGate is forced closed.
        private IEnumerator ClickMine()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.RequestMineClick();
            yield return Step();                                // consume the latch → schedule the impact (or reject)
            yield return StepUntil(() => !_mine.ImpactPending); // advance across the impact window → effect lands
            yield return Step();                                // one more frame for the impact-resolve Update to apply
        }

        // === FOLDED NEGATIVE (Tess NIT 2) — at a node WITH a pickaxe selected but NO click does NOTHING ===
        [UnityTest]
        public IEnumerator AtNodeWithSelectedPickaxe_NoClick_DoesNotMine()
        {
            yield return null; // let MineOre.Start discover the pool
            SelectPickaxe();

            // Stand in range, pickaxe selected, but NEVER request a click — the #224 proximity-auto would have
            // mined here; the active-left-click trigger must NOT. Advance the owned clock so a time-based
            // proximity-auto regression would fire during this window (keeps the test LIVE, not clock-frozen).
            yield return StepFrames(50);

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

            // A second frame WITHOUT a new click does not strike again (bare yield — asserting NOTHING happens).
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
            yield return Step(); // the release-frame drops the lock
            Assert.IsFalse(_mine.IsMineChainActive, "releasing LMB stops the chain");
            yield return StepFrames(10);
            Assert.AreEqual(3, _mine.StrikesOn(0), "after release, NO further strikes land");
        }

        // 86camf3xe — advance the OWNED clock across exactly ONE more COMPLETED held swing (its impact = one strike).
        // With the deterministic clock a HELD chain re-arms the next swing the instant the current clip completes, so
        // "wait until SwingInProgress is false" never sees a stable edge — the robust primitive is to advance until
        // the STRIKE count increments by one. The production single-flight (one impact per swing) + clip-completion
        // (next swing waits for the clip) gates guarantee that's exactly one swing at a time; a machine-gun /
        // over-pacing regression is caught by the count-window tests, not by this stepper. Bounded; called when due.
        private IEnumerator StepHeldSwing()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            int before = _mine.StrikesOn(0);
            yield return StepUntil(() => _mine.StrikesOn(0) > before);
        }

        // === REGROW — a broken node regrows after the timer into a mineable node ===
        [UnityTest]
        public IEnumerator BrokenNode_RegrowsAfterTimer_IntoAMineableNode()
        {
            yield return null;
            SelectPickaxe();

            for (int i = 0; i < _mine.strikesToBreak; i++) yield return ClickMine();
            Assert.IsTrue(_mine.IsBrokenOn(0), "node broken");

            // Advance past the break-crumble + max regrow + rise tweens — the node regrows, mineable anew. (Under the
            // owned clock the regrow tween ADVANCES deterministically; the prior Time.unscaledDeltaTime≈0 froze it.)
            yield return StepUntil(() => !_mine.IsBrokenOn(0));
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

        // === STAY-LIVE GUARD (86camf3xe — the ticket's named regression proof, the sibling of the chop
        //     HoldChain_NextSwingWaitsForClipToFinish guard) — the next held swing is GATED on the swing CLIP
        //     completing, NOT the shorter impact delay. A machine-gun regression (cadence gated on impact, or the
        //     clock un-pinned) lands a 2nd strike inside the impact-delay window → this REDS.
        //     86caffwv5 soak-3: the cadence now tracks the CLIP at the pickaxe's EFFECTIVE playback speed (clip ÷
        //     (chopSpeed × SwingSpeedPickaxe)), NOT the raw clip length — the sped-up 1.5× pickaxe swing finishes
        //     sooner, so the next hold-swing begins sooner (the Sponsor's "long waiting from idle to next swing" fix).
        [UnityTest]
        public IEnumerator HoldChain_NextStrikeWaitsForClipToFinish_NotImpactDelay()
        {
            yield return null; // Start discovers the pool
            SelectPickaxe();
            _mine.strikesToBreak = 10;
            _mine.swingImpactDelaySeconds = 0.1f;  // impact lands EARLY (mid-clip)...
            _mine.swingClipLengthSeconds = 0.6f;   // ...but the CLIP runs longer — the cadence tracks the EFFECTIVE clip

            // soak-3 — the mine cadence divides the clip by the pickaxe's EFFECTIVE playback (chopSpeed×SwingSpeedPickaxe),
            // so the effective cadence is SHORTER than the raw clip length (the idle gap the Sponsor reported is closed).
            float effPlayback = CastawayCharacter.EffectiveSwingPlaybackSpeed(_character.chopSpeed, CastawayCharacter.WeaponClassPickaxe);
            float effCadence = _mine.swingClipLengthSeconds / effPlayback; // 0.6 / 1.5 = 0.4s
            Assert.Less(effCadence, _mine.swingClipLengthSeconds / _character.chopSpeed,
                "soak-3 IDLE-GAP FIX: the effective mine cadence (clip ÷ chopSpeed×SwingSpeedPickaxe) is SHORTER than " +
                "the raw-clip cadence — the per-class pickaxe speed-up closes the idle-to-next-swing gap while holding");

            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.SetMineHeld(true);
            yield return Step();                   // begin the first swing (schedules the impact)
            Assert.IsTrue(_mine.ImpactPending, "the first held swing scheduled its impact");

            // Let the first swing's impact resolve (one strike); the CLIP is still playing afterward (clip > impact).
            yield return StepUntil(() => _mine.StrikesOn(0) >= 1);
            float firstStrikeAt = _now;
            Assert.AreEqual(1, _mine.StrikesOn(0), "the first completed swing landed exactly one strike at impact");
            Assert.IsTrue(_mine.SwingInProgress,
                "the swing CLIP is still playing AFTER its impact (cadence gated on clip completion, not impact)");

            // The over-pacing regression guard: advance a window LONGER than the impact delay (0.1s) but SHORTER than
            // the EFFECTIVE clip (0.4s) and prove NO 2nd strike lands — the next swing must WAIT for the CLIP. A
            // machine-gun regression (cadence gated on impact, not clip) would land a 2nd strike inside this window.
            yield return StepFrames(20); // 0.2s: past the 0.1s impact delay but well inside the 0.4s effective clip
            Assert.AreEqual(1, _mine.StrikesOn(0), "NO 2nd strike within the impact-delay window — the next swing waits for the CLIP");
            Assert.IsTrue(_mine.SwingInProgress, "the clip is STILL playing 0.2s after the impact (cadence ≥ effective clip length)");

            // Once the clip finishes, the next swing begins — exactly ONE more strike, ≥ one EFFECTIVE clip length
            // after the first (cadence rides the sped-up clip completion, not the shorter impact delay).
            yield return StepUntil(() => _mine.StrikesOn(0) >= 2);
            float secondStrikeAt = _now;
            Assert.AreEqual(2, _mine.StrikesOn(0), "after the clip FINISHED, exactly ONE more strike landed (the next swing)");
            Assert.GreaterOrEqual(secondStrikeAt - firstStrikeAt, effCadence - 0.05f,
                "the 2nd strike lands ≥ one EFFECTIVE clip length (clip ÷ chopSpeed×SwingSpeedPickaxe) after the 1st — " +
                "the mine cadence tracks the SPED-UP pickaxe swing completion (soak-3), not the raw clip length");

            _mine.SetMineHeld(false);
            yield return Step();
        }
    }
}
