using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the 86cabh907 soak-ROUND-2 weapon dial handles (PR #100):
    ///   1. The axe HEAD-size dial scales the BLADE-CLUSTER verts toward the eye WITHOUT moving the haft-core
    ///      verts — the head shrinks relative to the handle (the Sponsor's "head still too big, head proportion
    ///      isn't a uniform scale"). It operates on a per-instance CLONE, never the shared asset.
    ///   2. The generalized HELD-weapon nudge edits the per-weapon offset/euler/scale for a NON-axe weapon
    ///      (the bug CLASS the Sponsor hit: "nudged values only work for axe and not for the rest").
    ///   3. The [B] (weapon-cycle) and [N] (F9 arm-switch) bindings are DISTINCT — the soak-round-2 conflict fix.
    ///
    /// These catch the bug CLASS, not the instance: a regression that makes the head dial scale uniformly
    /// (moving the haft) or makes the non-axe nudge a no-op (the original axe-only bug) reds here.
    /// </summary>
    public class HeldWeaponDialPlayModeTests
    {
        // Build a synthetic axe-like mesh: a "haft" column of verts on the local X=0 axis (the handle) plus a
        // "blade cluster" of verts offset along +X (the head). The head-dial must shrink ONLY the blade verts.
        private static Mesh MakeAxeLikeMesh()
        {
            var m = new Mesh { name = "synthetic_axe" };
            var verts = new Vector3[]
            {
                // haft core (x ~ 0; the long axis is Y so the head-dial picks X as the blade axis)
                new Vector3(0f, 0.0f, 0f), new Vector3(0.02f, 0.5f, 0f), new Vector3(-0.02f, 1.0f, 0f),
                new Vector3(0f, 1.5f, 0f), new Vector3(0.02f, 2.0f, 0f),
                // blade cluster (large +X off the haft, near the top — the head)
                new Vector3(0.40f, 1.8f, 0.05f), new Vector3(0.45f, 1.9f, -0.05f),
                new Vector3(0.42f, 2.0f, 0.05f), new Vector3(0.38f, 1.7f, -0.05f),
            };
            m.vertices = verts;
            m.triangles = new[] { 0, 1, 2, 2, 3, 4, 5, 6, 7, 7, 8, 5 };
            m.RecalculateBounds();
            return m;
        }

        private GameObject _go;
        private GameObject _meshChild;
        private HeldWeaponCycleDebug _cycle;
        private MeshFilter _mf;

        [SetUp]
        public void SetUp()
        {
            // HeroAxe with a child MeshFilter (the mesh-holder the cycle component captures in Awake).
            _go = new GameObject("HeroAxe");
            _meshChild = new GameObject("wpn_axe_01");
            _meshChild.transform.SetParent(_go.transform, false);
            _mf = _meshChild.AddComponent<MeshFilter>();
            _mf.sharedMesh = MakeAxeLikeMesh();
            _cycle = _go.AddComponent<HeldWeaponCycleDebug>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mf != null && _mf.sharedMesh != null && _mf.sharedMesh.name.Contains("synthetic"))
                Object.Destroy(_mf.sharedMesh);
            Object.Destroy(_go);
        }

        // (1) The axe HEAD dial shrinks the BLADE verts toward the eye but leaves the HAFT-core verts in place.
        [UnityTest]
        public IEnumerator AxeHeadDial_ShrinksBladeVerts_LeavesHaftUnchanged()
        {
            yield return null; // let Awake run (captures the mesh-holder + seeds the live arrays)

            Mesh src = MakeAxeLikeMesh();
            Vector3[] baseV = src.vertices;
            // Indices: 0-4 = haft (|x| small), 5-8 = blade (|x| large).
            float haftXBefore = baseV[2].x;    // a haft vert
            float bladeXBefore = baseV[6].x;   // a blade vert (max +X)

            // Dial the head DOWN (smaller). Returns true == axe head dialed (index 0 == axe is the default).
            Assert.IsTrue(_cycle.DialAxeHead(0.5f), "DialAxeHead must succeed with the axe held (index 0)");
            Assert.Less(_cycle.AxeHeadFactor, 1f, "the head factor must drop below 1 after a shrink");

            // The displayed mesh is now the per-instance head-dial CLONE (NOT the shared asset).
            Mesh shown = _mf.sharedMesh;
            Assert.IsNotNull(shown);
            Assert.IsTrue(shown.name.Contains("headDial"),
                "the head dial must display a per-instance CLONE, never mutate the shared mesh asset");
            Vector3[] dialV = shown.vertices;

            // Blade vert moved IN toward the eye (|x| shrank); haft vert essentially unchanged.
            float bladeXAfter = dialV[6].x;
            float haftXAfter = dialV[2].x;
            Assert.Less(Mathf.Abs(bladeXAfter), Mathf.Abs(bladeXBefore) - 0.05f,
                $"the blade vert must shrink toward the eye (|x| {bladeXBefore:F3} -> {bladeXAfter:F3})");
            Assert.That(haftXAfter, Is.EqualTo(haftXBefore).Within(0.02f),
                $"the haft-core vert must NOT move (the head is shrunk relative to the handle; " +
                $"x {haftXBefore:F3} -> {haftXAfter:F3})");
            Object.Destroy(src);
        }

        // (2) The generalized HELD nudge edits a NON-axe weapon's per-weapon offset/euler (the axe-only-bug class).
        [Test]
        public void NudgeCurrentWeapon_OnNonAxe_EditsThatWeaponsArrays()
        {
            // Force a non-axe index via reflection (no Resources prefab in the test harness; we only assert the
            // per-weapon live arrays mutate — the bug class is "the nudge did nothing for non-axe weapons").
            var idxField = typeof(HeldWeaponCycleDebug).GetField("_index",
                BindingFlags.NonPublic | BindingFlags.Instance);
            idxField.SetValue(_cycle, 1); // KNIFE

            Vector3 off0 = _cycle.CurrentOffset, eul0 = _cycle.CurrentEuler;
            bool edited = _cycle.NudgeCurrentWeapon(new Vector3(0.1f, 0f, -0.2f), new Vector3(0f, 15f, 0f), 1f);

            Assert.IsTrue(edited, "NudgeCurrentWeapon must edit a NON-axe weapon (the axe-only-bug fix)");
            Assert.AreNotEqual(off0, _cycle.CurrentOffset, "the knife's mesh-holder offset must change");
            Assert.AreNotEqual(eul0, _cycle.CurrentEuler, "the knife's mesh-holder euler must change");
        }

        // (2b) The HELD nudge is INERT on the axe (index 0) — the axe hold is the shared-seat rig baseline, NOT
        // the per-weapon arrays (so the F9 tool routes the axe to the HeldAxeRig, not here).
        [Test]
        public void NudgeCurrentWeapon_OnAxe_IsInert_AxeUsesSharedSeat()
        {
            // Default index is 0 (axe). NudgeCurrentWeapon must return false (axe routes to the rig elsewhere).
            bool edited = _cycle.NudgeCurrentWeapon(Vector3.one, Vector3.one, 1f);
            Assert.IsFalse(edited, "the axe nudge must route to the shared-seat HeldAxeRig, not the per-weapon arrays");
        }

        // (3) The [B] weapon-cycle and the [N] F9 arm-switch are DISTINCT keys (the soak-round-2 conflict fix).
        [Test]
        public void WeaponCycle_AndArmSwitch_UseDistinctKeys()
        {
            var nudge = new GameObject("Boot").AddComponent<AxeNudgeTool>();
            Assert.AreEqual(KeyCode.B, _cycle.cycleKey, "the weapon-cycle must stay on [B]");
            Assert.AreEqual(KeyCode.N, nudge.armSwitchKey,
                "the F9 arm-switch must move to [N] so it never cross-fires with the always-on weapon-cycle [B]");
            Assert.AreNotEqual(_cycle.cycleKey, nudge.armSwitchKey,
                "the weapon-cycle key and the arm-switch key must be DISTINCT (the [B]-binding-conflict fix)");
            Object.Destroy(nudge.gameObject);
        }
    }
}
