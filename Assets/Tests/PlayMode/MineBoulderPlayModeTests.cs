using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the BOULDER-MINE mechanic (ticket 86camz9v7 / ② — the MineOre sibling). Proves, driving
    /// the player transform directly + the mine-click via the programmatic <see cref="MineBoulder.RequestMineClick"/>
    /// seam (mining is active-left-click, NOT proximity-auto; a headless run can't inject a real mouse button):
    ///
    ///   NO-CLICK — wood pickaxe selected + in range + NO click ⇒ NO strike/no stone (the #224 proximity-auto lock-out);
    ///   the TRIGGER — ONE click in range (wood pickaxe selected) lands exactly ONE strike;
    ///   the WIDENED GATE — a WOOD pickaxe (the ② entry tool) mines; no pickaxe / an AXE selected does nothing;
    ///   the LOOT LOOP — strikesToBreak clicks BREAK a boulder → a STONE PILE spawns → E-loot adds stone to the inventory;
    ///   HOLD-TO-MINE — holding LMB repeats swings until release;
    ///   REGROW — a broken boulder regrows after the (tweakable) timer;
    ///   the STAY-LIVE GUARD — the next held swing waits for the CLIP to finish (a machine-gun cadence regression REDS).
    ///
    /// The pure guard truth-table + the widened tier gate are unit-asserted headlessly in EditMode
    /// (MineBoulderClickGateTests). These PlayMode tests cover the live click-drives-a-strike loop.
    ///
    /// === Headless time discipline (mirrors MineOrePlayModeTests / 86camf3xe) ===
    /// The test OWNS the clock MineBoulder reads (via the UNITY_INCLUDE_TESTS-stripped <see cref="MineBoulder.TestClock"/>
    /// seam) + advances it a fixed step per stepped frame — a WORKING captureDeltaTime that also drives the tween _dt.
    /// A bare `yield return null` is used ONLY where the assertion is that NOTHING happens.
    /// </summary>
    public class MineBoulderPlayModeTests
    {
        private GameObject _invGo, _playerGo, _charGo, _spawnerGo, _looterGo, _mineGo, _boulderRoot;
        private Inventory _inv;
        private CastawayCharacter _character;
        private StonePileSpawner _spawner;
        private PickableLooter _looter;
        private MineBoulder _mine;

        private const float StableStepSeconds = 0.01f;
        private float _now;
        private bool _gateTracked;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            LogAssert.ignoreFailingMessages = true;

            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;

            _charGo = new GameObject("CastawayAvatar");
            _character = _charGo.AddComponent<CastawayCharacter>();

            _spawnerGo = new GameObject("StonePileSpawner");
            _spawner = _spawnerGo.AddComponent<StonePileSpawner>();
            _spawner.StoneYield = 5;
            _spawner.DespawnSeconds = 180f;

            _looterGo = new GameObject("PickableLooter");
            _looter = _looterGo.AddComponent<PickableLooter>();
            _looter.inventory = _inv;
            _looter.player = _playerGo.transform;

            // A small boulder POOL: boulder 0 in range at origin; 1/2 out of range.
            _boulderRoot = new GameObject("Boulders");
            MakeBoulder(_boulderRoot.transform, new Vector3(0.5f, 0f, 0f));  // in range
            MakeBoulder(_boulderRoot.transform, new Vector3(10f, 0f, 0f));   // out of range
            MakeBoulder(_boulderRoot.transform, new Vector3(12f, 0f, 0f));   // out of range

            _mineGo = new GameObject("MineBoulder");
            _mine = _mineGo.AddComponent<MineBoulder>();
            _mine.inventory = _inv;
            _mine.player = _playerGo.transform;
            _mine.character = _character;
            _mine.stonePileSpawner = _spawner;
            _mine.boulderRoot = _boulderRoot.transform;
            _mine.inventoryUI = null;
            _mine.mineRadius = 2.4f;
            _mine.strikesToBreak = 3;
            _mine.strikeInterval = 0f;
            _mine.swingImpactDelaySeconds = 0.1f;
            _mine.swingClipLengthSeconds = 0.3f;
            _mine.regrowthMinSeconds = 0.4f;
            _mine.regrowthMaxSeconds = 0.6f;
            _mine.regrowSeed = 24680;
            _mine.activeNodeCount = -1; // all boulders active

            _mine.TestClock = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo); Object.Destroy(_playerGo); Object.Destroy(_charGo);
            Object.Destroy(_spawnerGo); Object.Destroy(_looterGo); Object.Destroy(_mineGo); Object.Destroy(_boulderRoot);
            foreach (var pile in Object.FindObjectsByType<StonePile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(pile.gameObject);
            LogAssert.ignoreFailingMessages = false;
        }

        private static void MakeBoulder(Transform parent, Vector3 pos)
        {
            var b = new GameObject(MineBoulder.BoulderNodeName);
            b.transform.SetParent(parent, false);
            b.transform.position = pos;
            b.AddComponent<MeshRenderer>();
        }

        // Put a WOOD pickaxe on the belt AND make it the selected belt item (the ② entry gate).
        private void SelectWoodPickaxe()
        {
            var slot = _inv.Model.AddToolToBelt(_inv.Catalog.ById(ItemCatalog.PickaxeWoodId));
            Assert.IsTrue(slot.HasValue, "precondition: the wood pickaxe lands on the belt");
            _inv.Model.SelectBelt(slot.Value.Index);
            Assert.IsTrue(MineBoulder.IsBoulderPickaxeSelected(_inv), "precondition: the wood pickaxe is SELECTED");
        }

        private int StoneCount() => _inv.Model.CountItem(ItemCatalog.StoneId);

        private IEnumerator Step()
        {
            _now += StableStepSeconds;
            yield return null;
        }
        private IEnumerator StepFrames(int n) { for (int i = 0; i < n; i++) yield return Step(); }
        private IEnumerator StepUntil(System.Func<bool> done, int maxFrames = 6000)
        {
            int f = 0;
            while (!done() && f++ < maxFrames) yield return Step();
        }

        private IEnumerator ClickMine()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.RequestMineClick();
            yield return Step();
            yield return StepUntil(() => !_mine.ImpactPending);
            yield return Step();
        }

        // === NO-CLICK — at a boulder WITH a wood pickaxe selected but NO click does NOTHING (#224 lock-out) ===
        [UnityTest]
        public IEnumerator AtBoulderWithSelectedPickaxe_NoClick_DoesNotMine()
        {
            yield return null;
            SelectWoodPickaxe();
            yield return StepFrames(50);
            Assert.AreEqual(0, StoneCount(), "standing at the boulder WITHOUT a click must not mine (no proximity-auto)");
            Assert.AreEqual(0, _mine.StrikesOn(0), "no click → no strikes");
            Assert.IsFalse(_mine.IsBrokenOn(0), "no click → never broken");
        }

        // === THE TRIGGER — ONE click in range (wood pickaxe selected) lands EXACTLY ONE strike ===
        [UnityTest]
        public IEnumerator OneClickInRange_WithWoodPickaxe_LandsExactlyOneStrike()
        {
            yield return null;
            SelectWoodPickaxe();

            yield return ClickMine();
            Assert.AreEqual(1, _mine.StrikesOn(0), "ONE click → exactly ONE strike");
            Assert.IsFalse(_mine.IsBrokenOn(0), "one strike of three does not break the boulder");

            yield return null;
            Assert.AreEqual(1, _mine.StrikesOn(0), "no further strike without a NEW click");
        }

        // === WIDENED GATE (negative) — NO pickaxe at all + a click does nothing ===
        [UnityTest]
        public IEnumerator ClickWithoutPickaxe_DoesNothing()
        {
            yield return null;
            Assert.IsFalse(MineBoulder.IsBoulderPickaxeSelected(_inv), "precondition: no pickaxe selected");
            for (int i = 0; i < 5; i++) yield return ClickMine();
            Assert.AreEqual(0, _mine.StrikesOn(0), "no pickaxe → no strikes");
            Assert.AreEqual(0, StoneCount(), "no pickaxe → no stone");
        }

        // === WIDENED GATE (negative) — an AXE selected is NOT a pickaxe → no boulder mine ===
        [UnityTest]
        public IEnumerator ClickWithAxeSelected_DoesNothing()
        {
            yield return null;
            var slot = _inv.Model.AddToolToBelt(_inv.Catalog.ById(ItemCatalog.AxeId));
            Assert.IsTrue(slot.HasValue, "precondition: the axe lands on the belt");
            _inv.Model.SelectBelt(slot.Value.Index);
            Assert.IsFalse(MineBoulder.IsBoulderPickaxeSelected(_inv), "precondition: an axe is not a pickaxe");
            for (int i = 0; i < 5; i++) yield return ClickMine();
            Assert.AreEqual(0, _mine.StrikesOn(0), "an axe selected → no boulder strikes");
        }

        // === THE LOOT LOOP — strikesToBreak clicks BREAK the boulder → a stone pile spawns → E loots stone ===
        [UnityTest]
        public IEnumerator BreakBoulder_DropsStonePile_ELoots_StoneIntoInventory()
        {
            yield return null;
            SelectWoodPickaxe();
            Assert.AreEqual(0, StoneCount(), "precondition: no stone yet");

            for (int i = 0; i < _mine.strikesToBreak; i++) yield return ClickMine();

            Assert.IsTrue(_mine.IsBrokenOn(0), "strikesToBreak clicks in range break the boulder");
            Assert.AreEqual(0, StoneCount(), "breaking never banks stone directly — the stone is in the dropped pile until looted");

            var pile = Object.FindObjectOfType<StonePile>();
            Assert.IsNotNull(pile, "a stone pile spawned on break");
            Assert.AreEqual(_spawner.StoneYield, pile.StoneRemaining, "the pile holds the boulder's whole stone yield");

            _looter.RequestLoot();
            yield return null;
            Assert.AreEqual(_spawner.StoneYield, StoneCount(),
                "one E grabs the whole pile → the boulder's full stone yield lands in the inventory (stone Resource)");
            Assert.IsFalse(pile.IsAvailable, "the emptied pile is consumed");
        }

        // === HOLD-TO-MINE — holding LMB repeats swings, stops on release ===
        [UnityTest]
        public IEnumerator HoldingLmb_RepeatsSwings_UntilReleased()
        {
            yield return null;
            SelectWoodPickaxe();
            _mine.strikesToBreak = 6;

            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.SetMineHeld(true);
            for (int i = 0; i < 3; i++) yield return StepHeldSwing();

            Assert.AreEqual(3, _mine.StrikesOn(0), "holding LMB repeats swings — 3 strikes with NO extra presses");
            Assert.IsTrue(_mine.IsMineChainActive, "the chain is active while LMB is held + the boulder stands");

            _mine.SetMineHeld(false);
            yield return Step();
            Assert.IsFalse(_mine.IsMineChainActive, "releasing LMB stops the chain");
            yield return StepFrames(10);
            Assert.AreEqual(3, _mine.StrikesOn(0), "after release, NO further strikes land");
        }

        private IEnumerator StepHeldSwing()
        {
            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            int before = _mine.StrikesOn(0);
            yield return StepUntil(() => _mine.StrikesOn(0) > before);
        }

        // === REGROW — a broken boulder regrows after the timer into a mineable boulder ===
        [UnityTest]
        public IEnumerator BrokenBoulder_RegrowsAfterTimer()
        {
            yield return null;
            SelectWoodPickaxe();

            for (int i = 0; i < _mine.strikesToBreak; i++) yield return ClickMine();
            Assert.IsTrue(_mine.IsBrokenOn(0), "boulder broken");

            yield return StepUntil(() => !_mine.IsBrokenOn(0));
            Assert.IsFalse(_mine.IsBrokenOn(0), "the boulder regrew after the timer");
            Assert.AreEqual(0, _mine.StrikesOn(0), "a regrown boulder resets its strike count");

            yield return ClickMine();
            Assert.AreEqual(1, _mine.StrikesOn(0), "the regrown boulder is mineable again");
        }

        // === STAY-LIVE GUARD — the next held swing waits for the CLIP to finish (a machine-gun regression REDS).
        //     86caffwv5 soak-3: the cadence tracks the clip at the pickaxe's EFFECTIVE playback (clip ÷ chopSpeed×
        //     SwingSpeedPickaxe) — the sped-up 1.5× swing finishes sooner, closing the idle-to-next-swing gap (mirrors
        //     MineOre; the boulder mine is the pickaxe sibling verb). ===
        [UnityTest]
        public IEnumerator HoldChain_NextStrikeWaitsForClipToFinish_NotImpactDelay()
        {
            yield return null;
            SelectWoodPickaxe();
            _mine.strikesToBreak = 10;
            _mine.swingImpactDelaySeconds = 0.1f;
            _mine.swingClipLengthSeconds = 0.6f;

            float effPlayback = CastawayCharacter.EffectiveSwingPlaybackSpeed(_character.chopSpeed, CastawayCharacter.WeaponClassPickaxe);
            float effCadence = _mine.swingClipLengthSeconds / effPlayback; // 0.6 / 1.5 = 0.4s
            Assert.Less(effCadence, _mine.swingClipLengthSeconds / _character.chopSpeed,
                "soak-3 IDLE-GAP FIX: the effective boulder-mine cadence (clip ÷ chopSpeed×SwingSpeedPickaxe) is SHORTER " +
                "than the raw-clip cadence — the pickaxe speed-up closes the idle-to-next-swing gap while holding");

            UiInputGate.SetPanelOpen(false, ref _gateTracked);
            _mine.SetMineHeld(true);
            yield return Step();
            Assert.IsTrue(_mine.ImpactPending, "the first held swing scheduled its impact");

            yield return StepUntil(() => _mine.StrikesOn(0) >= 1);
            float firstStrikeAt = _now;
            Assert.AreEqual(1, _mine.StrikesOn(0), "the first completed swing landed exactly one strike");
            Assert.IsTrue(_mine.SwingInProgress, "the clip is still playing after its impact");

            yield return StepFrames(20); // 0.2s: past the 0.1s impact but inside the 0.4s effective clip
            Assert.AreEqual(1, _mine.StrikesOn(0), "NO 2nd strike within the impact-delay window — the next swing waits for the CLIP");

            yield return StepUntil(() => _mine.StrikesOn(0) >= 2);
            float secondStrikeAt = _now;
            Assert.AreEqual(2, _mine.StrikesOn(0), "after the clip FINISHED, exactly ONE more strike landed");
            Assert.GreaterOrEqual(secondStrikeAt - firstStrikeAt, effCadence - 0.05f,
                "the 2nd strike lands ≥ one EFFECTIVE clip length (clip ÷ chopSpeed×SwingSpeedPickaxe) after the 1st — " +
                "cadence tracks the sped-up pickaxe swing completion (soak-3), not the raw clip length");

            _mine.SetMineHeld(false);
            yield return Step();
        }
    }
}
