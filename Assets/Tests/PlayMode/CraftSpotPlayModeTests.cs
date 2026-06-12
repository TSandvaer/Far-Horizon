using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the U2-2 craft interaction (ticket 86ca8bdaq).
    ///
    /// Proves the proximity recipe actually FIRES through CraftSpot.Update when the player reaches the
    /// spot — the runtime complement to the deterministic EditMode Inventory math. The craft is the
    /// loop's entry; this confirms "click-move to the spot -> axe crafted" works end-to-end at runtime
    /// (the seam the shipped-build CraftVerifyCapture also exercises), and that being OUT of range never
    /// crafts (no spurious axe). No NavMesh needed — we drive the player transform directly, isolating
    /// CraftSpot's proximity logic from pathfinding (that's covered by the movement-verify capture).
    /// </summary>
    public class CraftSpotPlayModeTests
    {
        private GameObject _invGo;
        private GameObject _playerGo;
        private GameObject _spotGo;
        private Inventory _inv;
        private CraftSpot _spot;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // far from the spot

            _spotGo = new GameObject("CraftSpot");
            _spotGo.transform.position = new Vector3(0f, 0f, 0f);
            _spot = _spotGo.AddComponent<CraftSpot>();
            _spot.inventory = _inv;
            _spot.player = _playerGo.transform;
            _spot.craftRadius = 2.0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_playerGo);
            Object.Destroy(_spotGo);
        }

        [UnityTest]
        public IEnumerator OutOfRange_DoesNotCraft()
        {
            // Player far away — let several Update frames run; the recipe must NOT fire.
            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsFalse(_inv.HasAxe, "no axe crafted while the player is out of range");
            Assert.IsFalse(_spot.HasCrafted, "CraftSpot has not latched while out of range");
        }

        [UnityTest]
        public IEnumerator ReachingTheSpot_CraftsTheAxe_Once()
        {
            // Confirm it starts uncrafted out of range.
            yield return null;
            Assert.IsFalse(_inv.HasAxe, "starts with no axe");

            // Walk the player onto the spot — the proximity recipe should fire on the next Update.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // within craftRadius
            yield return null; // one Update for CraftSpot to poll proximity
            yield return null;

            Assert.IsTrue(_inv.HasAxe, "reaching the spot crafts the axe (the loop's entry interaction)");
            Assert.IsTrue(_spot.HasCrafted, "CraftSpot latches after crafting so it never re-fires");

            // Move away again and run more frames — must remain a single craft, no re-fire.
            int firesAfter = 0;
            _inv.Changed += () => firesAfter++;
            _playerGo.transform.position = new Vector3(20f, 0f, 20f);
            for (int i = 0; i < 5; i++) yield return null;
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f); // back in range
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(0, firesAfter, "the craft fires exactly once — re-entering range never re-crafts");
        }
    }
}
