using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode regression guard for the BELT-SELECTION ⇄ held-axe coherence (ticket 86caa4bya AC4 — the
    /// trickiest AC + the single highest false-green risk per Tess's strategy §S2). It drives the FULL
    /// transition table through the real Inventory + HeldAxe and asserts the RENDERER state after EACH
    /// transition — NOT a single static snapshot (the axe-facing PR #52 false-green precedent: per-state
    /// snapshots green while live transitions broken).
    ///
    /// The transition sequence (each a distinct state, asserted):
    ///   1. axe in selected belt slot 1 -> SHOWN
    ///   2. move axe to belt slot 2, slot 1 still selected -> HIDDEN
    ///   3. select slot 2 -> SHOWN (the transition, not just the end-state)
    ///   4. select an EMPTY slot -> HIDDEN
    ///   5. move the axe OFF the belt into the pack -> HIDDEN (still owned, not the selected belt item)
    ///   6. move it BACK to a belt slot + select it -> SHOWN again
    ///
    /// Guards the PERCEPT path (Renderer.enabled), not just a model flag — a renderer left enabled is the
    /// §editor-vs-runtime class. Also pins AC3 (pickup -> belt slot 1, exactly one) + AC7 (tool no-stack).
    /// </summary>
    public class InventoryBeltHeldAxePlayModeTests
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

        // AC3 — pickup lands the axe in belt slot 1 (EXACTLY, exactly one), not the pack, not slot 2.
        [UnityTest]
        public IEnumerator PickUpAxe_LandsInBeltSlot1_ExactlyOne()
        {
            yield return null;
            Assert.IsTrue(_inv.PickUpAxe(), "pickup places the axe (transition)");
            yield return null;

            Assert.AreEqual("axe", _inv.Model.BeltSlots[0].Def.Id, "axe is in belt SLOT 1 (index 0)");
            Assert.AreEqual(1, _inv.Model.BeltSlots[0].Count, "exactly one axe (a tool never stacks)");
            // Not duplicated anywhere else.
            int axeCount = _inv.Model.CountItem("axe");
            Assert.AreEqual(1, axeCount, "exactly ONE axe across all slots (no dual-pickup duplication)");
            Assert.IsFalse(_inv.PickUpAxe(), "a second pickup is a no-op (already owned)");
        }

        // AC4 — the FULL multi-transition show/hide table, asserting the renderer after EACH switch.
        [UnityTest]
        public IEnumerator SelectedBeltSlot_DrivesHeldAxe_AcrossAllTransitions()
        {
            var held = _axeGo.AddComponent<HeldAxe>();
            held.inventory = _inv;
            yield return null; // OnEnable: subscribe + apply (no axe -> hidden)

            Assert.IsFalse(_renderer.enabled, "spawn: no axe -> held axe hidden");

            // 1. Pick up the axe -> belt slot 1 (selected by default) -> SHOWN.
            _inv.PickUpAxe();
            yield return null;
            Assert.IsTrue(_renderer.enabled, "axe in the SELECTED belt slot 1 -> SHOWN");

            // 2. Move the axe to belt slot 2; slot 1 still selected -> HIDDEN.
            Assert.IsTrue(_inv.Model.TryMove(SlotRef.Belt(0), SlotRef.Belt(1)), "tool moves between belt slots");
            yield return null;
            Assert.IsFalse(_renderer.enabled, "axe in slot 2 with slot 1 selected -> HIDDEN");

            // 3. Select slot 2 -> SHOWN (the transition).
            _inv.Model.SelectBelt(1);
            yield return null;
            Assert.IsTrue(_renderer.enabled, "select slot 2 -> the axe APPEARS");

            // 4. Select an EMPTY slot -> HIDDEN.
            _inv.Model.SelectBelt(3);
            yield return null;
            Assert.IsFalse(_renderer.enabled, "select an empty slot -> nothing in hand");

            // 5. Move the axe OFF the belt into the pack -> HIDDEN (still owned, not the selected belt item).
            Assert.IsTrue(_inv.Model.TryMove(SlotRef.Belt(1), SlotRef.Inventory(0)), "tool moves to the pack");
            _inv.Model.SelectBelt(1); // re-select the (now empty) belt slot the axe used to be in
            yield return null;
            Assert.IsFalse(_renderer.enabled, "axe in the pack -> NOT shown even though still owned");
            Assert.IsTrue(_inv.HasAxe, "...still OWNED (ownership != selection)");

            // 6. Move it BACK to a belt slot + select it -> SHOWN again.
            Assert.IsTrue(_inv.Model.TryMove(SlotRef.Inventory(0), SlotRef.Belt(2)), "tool back onto the belt");
            _inv.Model.SelectBelt(2);
            yield return null;
            Assert.IsTrue(_renderer.enabled, "axe back in the selected belt slot -> SHOWN again");
        }

        // AC6 — the tool-vs-resource gate at the held-axe layer: a resource never becomes the held axe.
        [UnityTest]
        public IEnumerator ResourceCannotBecomeTheHeldAxe()
        {
            var held = _axeGo.AddComponent<HeldAxe>();
            held.inventory = _inv;
            yield return null;

            // Add wood (a resource) — it lands in the pack, can't go on the belt.
            _inv.AddWood(5);
            yield return null;
            Assert.IsFalse(_renderer.enabled, "wood is a resource -> never shown in hand");
            // Attempting to move wood onto the belt is rejected by the model.
            Assert.IsFalse(_inv.Model.TryMove(SlotRef.Inventory(0), SlotRef.Belt(0)),
                "the model rejects a resource onto the belt (AC6 gate)");
            yield return null;
            Assert.IsFalse(_renderer.enabled, "still no held axe (the gate held)");
        }
    }
}
