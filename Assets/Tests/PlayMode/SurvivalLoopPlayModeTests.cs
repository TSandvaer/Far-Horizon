using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// U2-7 (ticket 86ca8bdhy) — the M-U2 EXIT coverage: the FULL survival cycle driven END-TO-END
    /// in ONE PlayMode sequence on ONE shared rig (Inventory + WarmthNeed + Player), the DELTA the
    /// per-ticket suites do NOT cover. WarmthNeedPlayModeTests / CraftSpotPlayModeTests /
    /// ChopTreePlayModeTests / CampfirePlayModeTests each prove a single BEAT in isolation with their
    /// OWN throwaway rig; none threads the whole spine through a single play session, so a regression
    /// in the HAND-OFF between beats (the chopped wood reaching the placement gate; the lit fire
    /// reaching the SAME warmth instance the decay drained) could pass every isolated suite and still
    /// ship a broken loop. This suite closes that gap.
    ///
    /// Everything runs through REAL Update + a REAL Time.time window (Time.deltaTime~=0 headless trap,
    /// unity-conventions.md §headless time) — the same model the shipped-build -verifyLoop hook
    /// (CampfireVerifyCapture, "LOOP CLOSED=") exercises in the exe. We drive the player transform
    /// directly (NavMesh/click-move is the capture's job), isolating the gameplay seams from pathfinding.
    ///
    /// Success-test (ticket "a deliberate loop break ... fails the suite"): see
    /// FullCycle_EndToEnd_ClosesTheLoop — its final RESTORE assertion is the catch. The PR body
    /// documents the demonstrated break (Campfire.AddWarmth no-op'd -> red) + restore (-> green).
    /// </summary>
    public class SurvivalLoopPlayModeTests
    {
        private GameObject _invGo, _warmthGo, _playerGo, _spotGo, _treeGo, _fireGo;
        private Inventory _inv;
        private WarmthNeed _warmth;
        private CraftSpot _spot;
        private ChopTree _tree;
        private Campfire _fire;
        private CampfirePlacement _place;

        // World layout — distinct spots so moving the single player between them mirrors the real
        // craft-spot / tree / fire-pit triangle. FAR_AWAY parks the player out of every radius.
        private static readonly Vector3 FarAway   = new Vector3(50f, 0f, 50f);
        private static readonly Vector3 CraftPos  = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 TreePos   = new Vector3(10f, 0f, 0f);
        private static readonly Vector3 FirePos   = new Vector3(-10f, 0f, 0f);

        [SetUp]
        public void SetUp()
        {
            // ONE inventory + ONE warmth need — the SHARED state every beat reads/writes. This is the
            // whole point of the end-to-end rig: the wood the tree adds is the wood the fire spends; the
            // warmth the campfire restores is the warmth that decayed. Isolated suites never prove this.
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _warmthGo = new GameObject("Warmth");
            _warmth = _warmthGo.AddComponent<WarmthNeed>();
            _warmth.max = 100f;
            _warmth.decayPerSecond = 4f;   // brisk so the cold reads quickly, but << restoreRate
            _warmth.floor01 = 0.05f;
            _warmth.criticalThreshold01 = 0.25f;
            // startFull=true so WarmthNeed.Start() deterministically SEEDS _current=max regardless of
            // when Start fires relative to the test body (AddComponent in SetUp runs Start before the
            // body executes — a startFull=false rig would latch _current=0, BELOW the floor, with nothing
            // to decay). Each test then seeds the warmth value it wants via SatisfyFull/AddWarmth AFTER
            // the first yield, never relying on the inspector default surviving the lifecycle.
            _warmth.startFull = true;

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = FarAway;

            // Craft spot.
            _spotGo = new GameObject("CraftSpot");
            _spotGo.transform.position = CraftPos;
            _spot = _spotGo.AddComponent<CraftSpot>();
            _spot.inventory = _inv;
            _spot.player = _playerGo.transform;
            _spot.craftRadius = 2.0f;

            // Tree.
            _treeGo = new GameObject("ChopTree");
            _treeGo.transform.position = TreePos;
            _tree = _treeGo.AddComponent<ChopTree>();
            _tree.inventory = _inv;
            _tree.player = _playerGo.transform;
            _tree.visual = _treeGo.transform;
            _tree.chopRadius = 2.2f;
            _tree.woodPerChop = 1;
            _tree.chopsToFell = 3;
            _tree.chopInterval = 0f;       // CHANGE 1: chop is per LEFT-CLICK; no cooldown in the test
            _tree.inventoryUI = null;      // no UI rig → over-UI guard skipped (modal/RMB false in headless)

            // Campfire + placement gate.
            _fireGo = new GameObject("Campfire");
            _fireGo.transform.position = FirePos;
            _fire = _fireGo.AddComponent<Campfire>();
            _fire.warmth = _warmth;
            _fire.player = _playerGo.transform;
            _fire.warmRadius = 3f;
            _fire.restoreRate = 40f;       // >> decay (4) so the climb is unambiguous in a short window

            _place = _fireGo.AddComponent<CampfirePlacement>();
            _place.inventory = _inv;
            _place.campfire = _fire;
            _place.player = _playerGo.transform;
            _place.warmth = _warmth;
            _place.woodCost = 3;           // == one felled tree's yield (chopsToFell*woodPerChop)
            _place.buildRadius = 2.2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_warmthGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_spotGo);
            Object.Destroy(_treeGo);
            Object.Destroy(_fireGo);
        }

        // Move the single player to a world spot and let several real frames pass so the beat's Update
        // polls the new proximity. Returns after the dwell window.
        private IEnumerator GoTo(Vector3 pos, float dwellSeconds)
        {
            _playerGo.transform.position = pos;
            float start = Time.time;
            while (Time.time - start < dwellSeconds) yield return null;
        }

        // === THE DELTA: the WHOLE cycle in ONE sequence on ONE shared rig ===
        // decay (cold) -> craft axe -> chop tree for wood -> carry wood to the pit -> build+light -> warmth restores.
        // Each beat asserts on the SAME _inv / _warmth instance the previous beat mutated — the hand-offs
        // the isolated suites can't see. The final restore assertion is the ticket's success-test catch.
        [UnityTest]
        public IEnumerator FullCycle_EndToEnd_ClosesTheLoop()
        {
            yield return null; // WarmthNeed.Start() seeds the tick clock

            // --- Beat 0: the WHY. Cold castaway, no tools, warmth DECAYS while we stand out in the open. ---
            float warmthAtSpawn = _warmth.Current01;
            yield return GoTo(FarAway, 1.0f); // out in the cold, away from any spot
            float warmthAfterExposure = _warmth.Current01;
            Assert.Less(warmthAfterExposure, warmthAtSpawn,
                "Beat 0: warmth must DECAY over a real window out in the cold — the need that drives the loop");
            Assert.IsFalse(_inv.HasAxe, "Beat 0: start with no axe");
            Assert.AreEqual(0, _inv.WoodCount, "Beat 0: start with no wood");

            // --- Beat 1: CRAFT. Reach the craft spot -> the single recipe fires -> axe in hand. ---
            yield return GoTo(CraftPos, 0.3f);
            Assert.IsTrue(_inv.HasAxe, "Beat 1: reaching the craft spot crafts the axe (the loop's entry)");
            Assert.IsTrue(_spot.HasCrafted, "Beat 1: CraftSpot latched");

            // --- Beat 2: CHOP. Carry the axe to the tree -> LEFT-CLICK to chop -> wood ticks up -> the tree
            //     fells. --- This is the load-bearing HAND-OFF #1: the axe crafted in Beat 1 is what unlocks the
            // chop here (same _inv). CHANGE 1 (86caa4c5c): the chop is now per LEFT-CLICK (not proximity-auto),
            // so drive chopsToFell click-requests via the programmatic seam (a headless run can't inject a real
            // mouse button) — one chop per click — until the tree fells.
            _playerGo.transform.position = TreePos;
            for (int i = 0; i < _tree.chopsToFell && !_tree.IsFelled; i++)
            {
                _tree.RequestChopClick();
                yield return null;
            }
            Assert.IsTrue(_tree.IsFelled, "Beat 2: the axe-holding castaway fells the tree by clicking (CHANGE 1)");
            Assert.GreaterOrEqual(_inv.WoodCount, _place.woodCost,
                "Beat 2: one felled tree yields ENOUGH wood to afford the fire (the loop closes from one chop session)");
            int woodBeforeBuild = _inv.WoodCount;

            // --- Beat 3: PLACE + LIGHT. Carry the wood to the pit -> the wood gate is paid -> fire lit. ---
            // Hand-off #2: the wood chopped in Beat 2 is what the placement gate spends here (same _inv).
            _playerGo.transform.position = FirePos;
            float buildStart = Time.time;
            while (Time.time - buildStart < 2f && !_fire.IsLit) yield return null;
            Assert.IsTrue(_place.HasBuilt, "Beat 3: reaching the pit WITH wood builds the fire");
            Assert.IsTrue(_fire.IsLit, "Beat 3: the built fire is lit");
            Assert.AreEqual(woodBeforeBuild - _place.woodCost, _inv.WoodCount,
                "Beat 3: the wood gate debited exactly woodCost from the SAME ledger the chop filled");

            // --- Beat 4: RESTORE — THE LOOP CLOSES. Stand at the lit fire -> warmth measurably RISES. ---
            // Hand-off #3 (the success-test catch): the fire restores the SAME _warmth instance Beat 0
            // drained. restoreRate (40) >> decay (4) so the bar net-CLIMBS. Break Campfire.AddWarmth (or
            // unbind the need, or stop lighting) and THIS assertion goes red — the documented loop break.
            float warmthAtLight = _warmth.Current01;
            float restoreStart = Time.time;
            while (Time.time - restoreStart < 1.5f) yield return null;
            float warmthRestored = _warmth.Current01;
            Assert.Greater(warmthRestored, warmthAtLight + 0.05f,
                "Beat 4 (LOOP CLOSED): warmth RISES at the lit fire — the restore outpaces decay on the " +
                "same need that decayed in Beat 0. THIS is the success-test catch: break the campfire's " +
                "warmth restore and this assertion fails.");
        }

        // === WARMTH FLOOR through Update over a real window (the PlayMode complement to the EditMode
        // TickSeconds floor test). Proves the floor isn't just a TickSeconds-math property but actually
        // HOLDS when decay integrates through the live Update loop in a build — a floor that only the
        // deterministic path respected would still let a shipped build drain to 0. ===
        [UnityTest]
        public IEnumerator Warmth_DecaysToFloor_ThenHolds_ThroughUpdate()
        {
            // Reconfigure for a fast, unambiguous drain to a high floor so the window stays short.
            _warmth.startFull = true;
            _warmth.decayPerSecond = 200f; // blast past the floor within the window
            _warmth.floor01 = 0.30f;       // floor at 30 of 100
            float floorValue = _warmth.floor01 * _warmth.max;

            yield return null; // Start() seeds full (startFull) + the tick clock

            // Park the player far from every spot so NOTHING restores — pure decay through Update.
            _playerGo.transform.position = FarAway;

            // Let it drain well past where the floor sits.
            float start = Time.time;
            while (Time.time - start < 1.5f) yield return null;
            float atFloor = _warmth.Current;
            Assert.AreEqual(floorValue, atFloor, 0.5f,
                "warmth decays to the floor (floor01*max) and NO FURTHER through the live Update loop — " +
                "a simple floor, not a fail-state, holding in a build and not just in TickSeconds math");
            // Floor (0.30) sits ABOVE the critical threshold (0.25), so resting at the floor is NOT critical:
            // the thin-loop floor deliberately keeps the castaway out of a permanent fail-state readout.
            Assert.IsFalse(_warmth.IsCritical,
                "resting at a 0.30 floor is above the 0.25 critical threshold — the floor is not a fail-state");
        }

        // === The floor is genuinely a RESTING point: once there, more wall-clock never pushes it lower. ===
        [UnityTest]
        public IEnumerator Warmth_AtFloor_NeverDropsBelow_OverExtendedWindow()
        {
            _warmth.startFull = true;
            _warmth.decayPerSecond = 200f;
            _warmth.floor01 = 0.20f; // floor at 20
            float floorValue = _warmth.floor01 * _warmth.max;

            yield return null;
            _playerGo.transform.position = FarAway;

            // Drain to the floor.
            float start = Time.time;
            while (Time.time - start < 1f) yield return null;
            float firstAtFloor = _warmth.Current;
            Assert.AreEqual(floorValue, firstAtFloor, 0.5f, "drained to the floor");

            // Keep standing in the cold a good while longer — the floor must HOLD, never creep below.
            start = Time.time;
            while (Time.time - start < 1.5f) yield return null;
            float laterAtFloor = _warmth.Current;
            Assert.AreEqual(floorValue, laterAtFloor, 0.5f,
                "the floor is a RESTING point — extended exposure through Update never pushes below floor01*max");
        }
    }
}
