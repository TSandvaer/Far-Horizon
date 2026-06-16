using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// GAMEPLAY-PATH craft → VISIBLE-AXE guard (ticket 86ca8ce6y — SOAKFIX2). This catches the BUG CLASS,
    /// not the instance: the Sponsor reached the stump and saw NO axe THREE times while every isolated
    /// verify-capture passed (false-green). The failure was never "HasAxe didn't flip" — it was "the craft
    /// fired but no axe became VISIBLE in the gameplay view". A guard that only asserts HasAxe (or only the
    /// HeldAxe gate in isolation) would have stayed GREEN through the entire no-axe soak — exactly the
    /// silent-killer pattern the testing bar warns about.
    ///
    /// So this wires the FULL gameplay chain end-to-end — CraftSpot proximity → Inventory.CraftAxe →
    /// HeldAxe (held axe SHOWS) AND StumpAxe (stump axe HIDES) — and asserts the RENDERER-VISIBILITY DELTA
    /// across the craft, both directions. Before: stump axe shown, held axe hidden (an axe IS on screen).
    /// After reaching the spot: held axe shown, stump axe hidden (the kid picks it up). If the wiring breaks
    /// such that reaching the spot leaves NO axe visible (the Sponsor's complaint), this reds.
    /// </summary>
    public class CraftToVisibleAxePlayModeTests
    {
        private GameObject _invGo, _playerGo, _spotGo, _heldGo, _stumpGo;
        private Inventory _inv;
        private CraftSpot _spot;
        private MeshRenderer _heldR, _stumpR;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(20f, 0f, 20f); // out of range at spawn

            _spotGo = new GameObject("CraftSpot");
            _spotGo.transform.position = Vector3.zero;
            _spot = _spotGo.AddComponent<CraftSpot>();
            _spot.inventory = _inv;
            _spot.player = _playerGo.transform;
            _spot.craftRadius = 2.0f;

            // The HELD axe (HasAxe-gated, shown on craft) + the STUMP axe (inverse-gated, hidden on craft).
            _heldGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_heldGo.GetComponent<Collider>());
            _heldR = _heldGo.GetComponent<MeshRenderer>();
            _heldGo.AddComponent<HeldAxe>().inventory = _inv;

            _stumpGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_stumpGo.GetComponent<Collider>());
            _stumpR = _stumpGo.GetComponent<MeshRenderer>();
            _stumpGo.AddComponent<StumpAxe>().inventory = _inv;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo); Object.Destroy(_playerGo); Object.Destroy(_spotGo);
            Object.Destroy(_heldGo); Object.Destroy(_stumpGo);
        }

        // The end-to-end guard: an axe is ALWAYS visible, and reaching the spot swaps stump→held.
        [UnityTest]
        public IEnumerator ReachingTheStump_SwapsStumpAxeForHeldAxe_AnAxeIsAlwaysVisible()
        {
            yield return null; // let the gates apply their initial state

            // BEFORE the craft: an axe IS on screen (the stump axe), the held axe is hidden.
            Assert.IsFalse(_inv.HasAxe, "precondition: no axe crafted yet");
            Assert.IsTrue(_stumpR.enabled,
                "at spawn the STUMP axe must be visible (the Sponsor's always-on-screen axe + walk-here cue)");
            Assert.IsFalse(_heldR.enabled, "at spawn the HELD axe must be hidden (empty-handed)");
            Assert.IsTrue(_stumpR.enabled || _heldR.enabled,
                "INVARIANT: an axe must ALWAYS be visible somewhere — the Sponsor must never see 'no axe'");

            // Walk the player onto the stump — the proximity recipe fires through CraftSpot.Update.
            _playerGo.transform.position = new Vector3(0.5f, 0f, 0.5f);
            yield return null; // CraftSpot polls proximity → CraftAxe → Changed → both gates re-Apply
            yield return null;

            // AFTER reaching the spot: the held axe SHOWS, the stump axe HIDES (the kid picks it up).
            Assert.IsTrue(_inv.HasAxe, "reaching the stump crafts the axe");
            Assert.IsTrue(_heldR.enabled,
                "after the craft the HELD axe MUST be visible — this is the NO-AXE soak guard: a craft that " +
                "fires but leaves the axe invisible is the exact false-green the Sponsor caught 3 times");
            Assert.IsFalse(_stumpR.enabled, "after the craft the STUMP axe must hide (replaced by the held axe)");
            Assert.IsTrue(_stumpR.enabled || _heldR.enabled,
                "INVARIANT (post-craft): an axe must STILL be visible — now in-hand");
        }
    }
}
