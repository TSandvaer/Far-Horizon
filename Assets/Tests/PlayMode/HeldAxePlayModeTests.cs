using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the EQUIP behavior of the sourced held axe (ticket 86ca8ce6y — RE-DONE).
    ///
    /// The sourced hatchet is attached to the chibi's right-hand bone and its renderers are GATED on
    /// Inventory.HasAxe by HeldAxe: empty-handed at spawn, holding the axe once crafted (the craft reads
    /// as "the kid picks up the axe"). This proves the GATE actually toggles the renderer through the
    /// Inventory.Changed event — not the bug class where the axe ships always-visible (no pick-up read)
    /// or always-hidden (the hero tool never appears). We isolate HeldAxe from the scene by building a
    /// minimal Inventory + a renderer-bearing axe object, so the test is deterministic.
    /// </summary>
    public class HeldAxePlayModeTests
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

            // A renderer-bearing stand-in for the sourced hatchet mesh (HeldAxe toggles Renderer.enabled
            // over its subtree — the mesh source is irrelevant to the gate logic).
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

        // Spawn (no axe) -> the held axe is HIDDEN. Then CraftAxe -> it becomes VISIBLE via Changed.
        [UnityTest]
        public IEnumerator HeldAxe_HiddenUntilCrafted_ThenShownOnCraft()
        {
            var held = _axeGo.AddComponent<HeldAxe>();
            held.inventory = _inv;
            // Let Awake/OnEnable run (subscribe + apply the initial hidden state).
            yield return null;

            Assert.IsFalse(_inv.HasAxe, "precondition: no axe crafted yet");
            Assert.IsFalse(_renderer.enabled,
                "the held axe must be HIDDEN before the craft (empty-handed at spawn)");

            // Craft the axe — HeldAxe should react to Inventory.Changed and show the renderer.
            Assert.IsTrue(_inv.CraftAxe(), "CraftAxe fires once and returns true");
            yield return null;

            Assert.IsTrue(_renderer.enabled,
                "the held axe must become VISIBLE once HasAxe is set (the kid picks up the axe)");
        }

        // If the axe is already held when HeldAxe enables (e.g. a future reload pre-seeds HasAxe), it
        // shows immediately — the gate reads the CURRENT state on enable, not just the Changed edge.
        [UnityTest]
        public IEnumerator HeldAxe_AlreadyCrafted_ShownImmediatelyOnEnable()
        {
            _inv.CraftAxe();
            Assert.IsTrue(_inv.HasAxe, "precondition: axe already crafted");

            var held = _axeGo.AddComponent<HeldAxe>();
            held.inventory = _inv;
            yield return null; // Awake + OnEnable Apply()

            Assert.IsTrue(_renderer.enabled,
                "with HasAxe already true, the held axe must be visible immediately on enable");
        }
    }
}
