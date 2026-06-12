using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the U2-4 campfire (ticket 86ca8bdep) — the loop's CLOSE.
    ///
    /// Proves the load-bearing seams actually FIRE through Update over a REAL Time.time window
    /// (headless Time.deltaTime~=0, unity-conventions.md §headless time):
    ///   - the WOOD GATE (negative case): reaching the pit WITHOUT enough wood does NOT build the fire,
    ///     and spends no wood (the ticket's "no wood -> no campfire");
    ///   - the positive build: reaching the pit WITH wood builds + lights the fire and debits the cost;
    ///   - the warmth RESTORE: a lit fire with the player in range makes warmth measurably CLIMB
    ///     (the loop closes); leaving the radius stops the restore.
    /// We drive the player transform directly, isolating the proximity + gate logic from pathfinding
    /// (NavMesh/click-move is covered by CampfireVerifyCapture in the shipped exe).
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
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the fire

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
            _place.woodCost = 3;
            _place.buildRadius = 2.2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_warmthGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_fireGo);
        }

        // === THE WOOD GATE (negative case) — "no wood -> no campfire" ===
        [UnityTest]
        public IEnumerator AtPitWithoutWood_DoesNotBuild_SpendsNothing()
        {
            Assert.AreEqual(0, _inv.WoodCount, "precondition: no wood");

            // Stand the wood-less player ON the pit; let frames + wall-clock pass.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.IsFalse(_place.HasBuilt, "no wood -> the fire is never built");
            Assert.IsFalse(_fire.IsLit, "no wood -> the fire stays unlit");
            Assert.AreEqual(0, _inv.WoodCount, "no wood was spent on a failed build");
        }

        // === Not-quite-enough wood is still the gate (all-or-nothing) ===
        [UnityTest]
        public IEnumerator AtPitWithTooLittleWood_DoesNotBuild_KeepsWood()
        {
            _inv.AddWood(2); // cost is 3
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            float start = Time.time;
            while (Time.time - start < 0.5f) yield return null;

            Assert.IsFalse(_fire.IsLit, "2 wood < cost 3 -> no fire");
            Assert.AreEqual(2, _inv.WoodCount, "an unaffordable build spends NOTHING (wood preserved)");
        }

        // === Positive build: with wood, reaching the pit builds + lights the fire and pays the cost ===
        [UnityTest]
        public IEnumerator AtPitWithWood_BuildsLightsAndPays()
        {
            _inv.AddWood(4); // cost is 3 -> 1 left after
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);

            float start = Time.time;
            while (Time.time - start < 1f && !_fire.IsLit) yield return null;

            Assert.IsTrue(_place.HasBuilt, "with enough wood, reaching the pit builds the fire");
            Assert.IsTrue(_fire.IsLit, "the built fire is lit");
            Assert.AreEqual(1, _inv.WoodCount, "the wood cost (3) was debited from the ledger (4 -> 1)");
        }

        // === The loop CLOSES: a lit fire with the player near makes warmth measurably RISE ===
        [UnityTest]
        public IEnumerator LitFire_RestoresWarmth_WhenPlayerNear()
        {
            _inv.AddWood(3);
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // at the pit -> builds + lights
            float start = Time.time;
            while (Time.time - start < 1f && !_fire.IsLit) yield return null;
            Assert.IsTrue(_fire.IsLit, "fire lit");

            float before = _warmth.Current01;
            // Stand by the lit fire — restoreRate (30) >> decay (2) so warmth net-climbs.
            start = Time.time;
            while (Time.time - start < 1.5f) yield return null;
            float after = _warmth.Current01;

            Assert.Greater(after, before + 0.02f,
                "warmth measurably RISES at the lit fire (restore outpaces decay — the loop closes)");
        }

        // === Leaving the fire's radius stops the restore (warmth then decays again) ===
        [UnityTest]
        public IEnumerator LeavingLitFire_StopsRestore_WarmthDecaysAgain()
        {
            // Build + light, climb a bit, then walk away.
            _inv.AddWood(3);
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            float start = Time.time;
            while (Time.time - start < 1f && !_fire.IsLit) yield return null;
            Assert.IsTrue(_fire.IsLit, "fire lit");

            start = Time.time;
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
