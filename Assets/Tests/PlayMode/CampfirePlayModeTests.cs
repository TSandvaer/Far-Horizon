using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the ⑤ campfire PLACE-TO-BUILD flow (ticket 86camz9w7 — REWRITE of the U2-4
    /// proximity fire-pit, 86ca8bdep). Proves the load-bearing seams fire through the production placement path
    /// over a REAL Time.time window (headless Time.deltaTime~=0, unity-conventions.md §headless time):
    ///   - the ALL-OR-NOTHING mats gate (negative): confirming WITHOUT enough wood OR stone does NOT build the
    ///     fire and spends nothing (the vision's "stone AND wood" — short of EITHER mat refuses);
    ///   - the positive build: with wood+stone, confirming PLACES + LIGHTS the fire and debits both mats;
    ///   - the warmth RESTORE (the SHIPPED runtime — spec §2 regression boundary): a lit fire with the player in
    ///     range makes warmth measurably CLIMB (the loop closes); leaving the radius stops the restore.
    /// We drive the placement's input-independent RequestBuildAt seam (enter → aim → confirm), isolating the
    /// place-to-build + gate logic from the cursor/camera (the shipped exe drives it under the mouse — proven by
    /// CampfireVerifyCapture). groundMask=0 selects the headless flat-ground fallback (the pure validity truth-table).
    /// </summary>
    public class CampfirePlayModeTests
    {
        private GameObject _invGo, _warmthGo, _playerGo, _fireGo;
        private Inventory _inv;
        private WarmthNeed _warmth;
        private Campfire _fire;
        private CampfirePlacement _place;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _warmthGo = new GameObject("Warmth");
            _warmth = _warmthGo.AddComponent<WarmthNeed>();
            _warmth.max = 100f;
            _warmth.decayPerSecond = 2f;
            _warmth.floor01 = 0.05f;
            _warmth.startFull = false; // start cold so a restore has headroom to climb into

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(2f, 0f, 0f); // >= minDistFromPlayer from the origin build spot

            _fireGo = new GameObject("Campfire");
            _fireGo.transform.position = Vector3.zero;
            _fire = _fireGo.AddComponent<Campfire>();
            _fire.warmth = _warmth;
            _fire.player = _playerGo.transform;
            _fire.warmRadius = 3f;
            _fire.restoreRate = 30f; // fast so the climb is obvious within a short CI window

            _place = _fireGo.AddComponent<CampfirePlacement>();
            _place.inventory = _inv;
            _place.campfire = _fire;
            _place.player = _playerGo.transform;
            _place.warmth = _warmth;
            _place.ghost = null;                 // no ghost in the bare rig (SetGhostShown/TintGhost no-op)
            _place.groundMask = default;         // 0 → the headless flat-ground fallback (valid ground)
            _place.woodCost = CampfirePlacement.CampfireWoodCostDefault;   // 3
            _place.stoneCost = CampfirePlacement.CampfireStoneCostDefault; // 2
        }

        [TearDown]
        public void TearDown()
        {
            if (_place != null) _place.Cancel(); // release the modal UiInputGate if a test left placement active
            Object.Destroy(_invGo);
            Object.Destroy(_warmthGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_fireGo);
        }

        // Grant stone into the pack (Inventory has AddWood but no AddStone — stone routes through the model, the
        // proven CraftingMenuPlayModeTests idiom). Call AFTER Awake settles (Model/Catalog are ready).
        private void GrantStone(int n) => _inv.Model.AddItem(_inv.Catalog.ById(ItemCatalog.StoneId), n);

        // === THE MATS GATE (negative case) — "no mats -> no campfire" ===
        [UnityTest]
        public IEnumerator PlaceWithoutMats_DoesNotBuild_SpendsNothing()
        {
            yield return null; // let Awake settle
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood");
            Assert.AreEqual(0, _inv.StoneCount, "precondition: no stone");

            bool built = _place.RequestBuildAt(Vector3.zero);

            Assert.IsFalse(built, "no mats -> the confirm is refused");
            Assert.IsFalse(_place.HasBuilt, "no mats -> the campfire is never built");
            Assert.IsFalse(_fire.IsPlaced, "no mats -> the campfire stays invisible (unplaced)");
            Assert.IsFalse(_fire.IsLit, "no mats -> the fire stays unlit");
            Assert.AreEqual(0, _inv.WoodCount, "no wood was spent on a refused build");
            Assert.AreEqual(0, _inv.StoneCount, "no stone was spent on a refused build");
        }

        // === Short of EITHER mat is still the gate (all-or-nothing — the "stone AND wood" NIT-3) ===
        [UnityTest]
        public IEnumerator PlaceWithTooLittleStone_DoesNotBuild_KeepsMats()
        {
            yield return null; // let Awake settle (Model/Catalog ready)
            _inv.AddWood(3);   // enough wood
            GrantStone(1);     // stone cost is 2 -> short

            bool built = _place.RequestBuildAt(Vector3.zero);

            Assert.IsFalse(built, "3 wood but only 1 stone (< cost 2) -> no fire (all-or-nothing)");
            Assert.IsFalse(_fire.IsLit, "short stone -> the fire stays unlit");
            Assert.AreEqual(3, _inv.WoodCount, "an unaffordable build spends NOTHING (wood preserved)");
            Assert.AreEqual(1, _inv.StoneCount, "an unaffordable build spends NOTHING (stone preserved)");
        }

        // === Positive build: with wood+stone, confirming places + lights the fire and pays BOTH mats ===
        [UnityTest]
        public IEnumerator PlaceWithMats_BuildsLightsAndPaysBoth()
        {
            yield return null; // let Awake settle (Model/Catalog ready)
            _inv.AddWood(4);   // cost 3 -> 1 left
            GrantStone(3);     // cost 2 -> 1 left

            bool built = _place.RequestBuildAt(Vector3.zero);

            Assert.IsTrue(built, "with enough wood+stone, confirming builds the fire");
            Assert.IsTrue(_place.HasBuilt, "the placement latches built");
            Assert.IsTrue(_fire.IsPlaced, "the campfire is revealed at the placed pose (invisible-until-placed lifted)");
            Assert.IsTrue(_fire.IsLit, "the placed fire is lit (placing == lighting — the mats buy a lit fire)");
            Assert.AreEqual(1, _inv.WoodCount, "the wood cost (3) was debited (4 -> 1)");
            Assert.AreEqual(1, _inv.StoneCount, "the stone cost (2) was debited (3 -> 1)");
        }

        // === The loop CLOSES: a lit fire with the player near makes warmth measurably RISE (shipped runtime) ===
        [UnityTest]
        public IEnumerator LitFire_RestoresWarmth_WhenPlayerNear()
        {
            yield return null;
            _inv.AddWood(3); GrantStone(2);
            Assert.IsTrue(_place.RequestBuildAt(Vector3.zero), "placed + lit");
            Assert.IsTrue(_fire.IsLit, "fire lit");

            // Stand the player AT the fire (within warmRadius 3) so the restore ticks.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            float before = _warmth.Current01;
            float start = Time.time;
            while (Time.time - start < 1.5f) yield return null; // restoreRate (30) >> decay (2) -> warmth climbs
            float after = _warmth.Current01;

            Assert.Greater(after, before + 0.02f,
                "warmth measurably RISES at the lit fire (restore outpaces decay — the loop closes)");
        }

        // === Leaving the fire's radius stops the restore (warmth then decays again) ===
        [UnityTest]
        public IEnumerator LeavingLitFire_StopsRestore_WarmthDecaysAgain()
        {
            yield return null;
            _inv.AddWood(3); GrantStone(2);
            Assert.IsTrue(_place.RequestBuildAt(Vector3.zero), "placed + lit");
            Assert.IsTrue(_fire.IsLit, "fire lit");

            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // at the fire -> climb a bit
            float start = Time.time;
            while (Time.time - start < 0.8f) yield return null;
            float warmAtFire = _warmth.Current01;

            // Walk WELL out of warmRadius — restore stops, decay (WarmthNeed.Update) resumes.
            _playerGo.transform.position = new Vector3(40f, 0f, 40f);
            start = Time.time;
            while (Time.time - start < 1.2f) yield return null;
            float warmAway = _warmth.Current01;

            Assert.Less(warmAway, warmAtFire,
                "out of the fire's radius the restore stops and warmth decays again (cold away from the fire)");
            Assert.IsTrue(_fire.IsLit, "the fire stays lit even when the player leaves (thin: no burn-down)");
        }
    }
}
