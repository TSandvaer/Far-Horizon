using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the axe-on-the-stump gate (ticket 86ca8ce6y — SOAKFIX2). StumpAxe is the
    /// INVERSE of HeldAxe: the axe planted in the chopping-block stump is VISIBLE from spawn (the always-
    /// on-screen hero axe + walk-here cue) and HIDES once the axe is crafted (the held axe appears at the
    /// same instant → "the kid picks it up"). This proves the inverse gate actually toggles the renderer
    /// through Inventory.Changed — not the bug class where the stump axe ships always-visible (the Sponsor
    /// would see TWO axes after crafting) or always-hidden (the Sponsor's "stump is there but no axe").
    /// </summary>
    public class StumpAxePlayModeTests
    {
        private GameObject _invGo;
        private GameObject _axeGo;
        private Inventory _inv;
        private MeshRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Inventory");
            _inv = _invGo.AddComponent<Inventory>();
            _axeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_axeGo.GetComponent<Collider>());
            _renderer = _axeGo.GetComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_invGo);
            Object.Destroy(_axeGo);
        }

        // Spawn (no axe) -> the stump axe is SHOWN. Then CraftAxe -> it HIDES via Changed (picked up).
        [UnityTest]
        public IEnumerator StumpAxe_ShownUntilCrafted_ThenHiddenOnCraft()
        {
            var stump = _axeGo.AddComponent<StumpAxe>();
            stump.inventory = _inv;
            yield return null; // Awake/OnEnable: subscribe + apply the initial shown state

            Assert.IsFalse(_inv.HasAxe, "precondition: no axe crafted yet");
            Assert.IsTrue(_renderer.enabled,
                "the stump axe must be VISIBLE before the craft (the always-on-screen axe at spawn)");

            Assert.IsTrue(_inv.CraftAxe(), "CraftAxe fires once and returns true");
            yield return null;

            Assert.IsFalse(_renderer.enabled,
                "the stump axe must HIDE once HasAxe is set (the kid picks it up — the held axe replaces it)");
        }

        // If the axe is already held when StumpAxe enables (e.g. a future reload pre-seeds HasAxe), the
        // stump axe is hidden immediately — the gate reads the CURRENT state on enable, not just the edge.
        [UnityTest]
        public IEnumerator StumpAxe_AlreadyCrafted_HiddenImmediatelyOnEnable()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "precondition: axe already crafted");

            var stump = _axeGo.AddComponent<StumpAxe>();
            stump.inventory = _inv;
            yield return null; // Awake + OnEnable Apply()

            Assert.IsFalse(_renderer.enabled,
                "with HasAxe already true, the stump axe must be hidden immediately on enable (no double axe)");
        }
    }
}
