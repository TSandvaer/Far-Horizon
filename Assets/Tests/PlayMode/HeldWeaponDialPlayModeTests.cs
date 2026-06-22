using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the 86cabh907 soak-ROUND-2 weapon dial handles, REWORKED for the #100 soak bugs
    /// (the dial mutated DATA the rendered weapon never picked up — the test tested the wrong layer):
    ///
    ///   1. The generalized HELD-weapon nudge actually MOVES the rendered mesh-holder transform for a NON-axe
    ///      weapon — AND that move SURVIVES a HeldToolRig.LateUpdate frame. #100 root cause: the in-house axe
    ///      FBX is a single-node FBX (preserveHierarchy:0) so the MeshFilter collapses onto the HeroAxe ROOT,
    ///      the SAME transform the rig overwrites every frame — so the old code's per-weapon offset/euler was
    ///      STOMPED next frame and the F9 nudge "did nothing" for knife/sword/spear. The fix re-homes the mesh
    ///      onto a child the rig never touches; this test reproduces the rig-on-root topology and asserts the
    ///      VISIBLE holder localPosition/localRotation changed after the nudge AND after a rig frame.
    ///   2. The axe HEAD-size dial shrinks the rendered BLADE-CLUSTER verts toward the eye WHILE the GRIP/haft
    ///      verts stay anchored (the Sponsor's "head still too big, head proportion isn't a uniform scale").
    ///      It operates on a per-instance CLONE, never the shared asset.
    ///   3. The [B] (weapon-cycle) and [N] (F9 arm-switch) bindings are DISTINCT — the soak-round-2 conflict fix.
    ///
    /// These catch the bug CLASS, not the instance: a regression that lets the rig stomp the per-weapon nudge,
    /// or makes the head dial scale uniformly (moving the grip), or makes the non-axe nudge a no-op, reds here.
    /// </summary>
    public class HeldWeaponDialPlayModeTests
    {
        // A taller "axe-like" mesh: a HAFT column of verts along +Y from the grip (y=0) to the top, plus a
        // BLADE cluster offset along +X near the TOP (the head). The #100 head-dial must shrink ONLY the
        // upper-haft off-centreline blade verts, leaving the grip + lower haft anchored.
        private static Mesh MakeAxeLikeMesh()
        {
            var m = new Mesh { name = "synthetic_axe" };
            var verts = new Vector3[]
            {
                // grip + lower haft (x ~ 0; bottom of the Y span — must NOT move when the head shrinks)
                new Vector3(0f, 0.0f, 0f), new Vector3(0.02f, 0.4f, 0f), new Vector3(-0.02f, 0.8f, 0f),
                new Vector3(0f, 1.2f, 0f),
                // upper haft core (still near centreline — also stays put)
                new Vector3(0.02f, 1.7f, 0f), new Vector3(-0.02f, 2.0f, 0f),
                // blade cluster (large +X off the haft, UPPER Y — the head; must shrink toward the eye)
                new Vector3(0.40f, 1.8f, 0.05f), new Vector3(0.45f, 1.9f, -0.05f),
                new Vector3(0.42f, 2.0f, 0.05f), new Vector3(0.38f, 1.7f, -0.05f),
            };
            m.vertices = verts;
            m.triangles = new[] { 0, 1, 2, 2, 3, 4, 4, 5, 6, 6, 7, 8, 8, 9, 6 };
            m.RecalculateBounds();
            return m;
        }

        private GameObject _go;
        private HeldWeaponCycleDebug _cycle;
        private MeshFilter _mf;

        [TearDown]
        public void TearDown()
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.Contains("synthetic"))
                    Object.Destroy(mf.sharedMesh);
            if (_go != null) Object.Destroy(_go);
        }

        // Build a HeroAxe object that reproduces the REAL runtime topology: the MeshFilter is on the ROOT
        // (the collapsed single-node FBX), the HeldToolRig drives the root, and the cycle component is on the
        // same object. The cycle's Awake re-homes the mesh onto a child holder (the #100 fix).
        private void BuildRigDrivenHeroAxe()
        {
            _go = new GameObject("HeroAxe");
            _mf = _go.AddComponent<MeshFilter>();      // MeshFilter ON THE ROOT (the preserveHierarchy:0 collapse)
            _mf.sharedMesh = MakeAxeLikeMesh();
            _go.AddComponent<MeshRenderer>();
            var hand = new GameObject("RightHand").transform;
            hand.position = new Vector3(2f, 1f, 0f);
            var rig = _go.AddComponent<HeldAxeRig>();  // drives the ROOT transform every LateUpdate
            rig.hand = hand;
            _cycle = _go.AddComponent<HeldWeaponCycleDebug>();
        }

        // (1) The generalized HELD nudge moves the RENDERED holder transform for a NON-axe weapon, and the move
        // SURVIVES a rig frame (the #100 rig-stomp bug class).
        [UnityTest]
        public IEnumerator NonAxeNudge_MovesRenderedHolder_AndSurvivesRigFrame()
        {
            BuildRigDrivenHeroAxe();
            yield return null; // Awake re-homes the mesh onto a child holder; rig LateUpdate runs

            // The displayed mesh must now live on a child holder the rig does NOT drive (not the HeroAxe root).
            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);
            Assert.IsNotNull(holder, "the cycle must have a mesh holder");
            Assert.AreNotEqual(_go.transform, holder.transform,
                "#100: the displayed mesh must be re-homed onto a CHILD holder the rig never touches — not the " +
                "rig-driven HeroAxe root (else the per-weapon nudge is stomped every frame)");

            // Select a NON-axe weapon (knife) via reflection (no Resources prefab in the harness).
            var idxField = typeof(HeldWeaponCycleDebug).GetField("_index",
                BindingFlags.NonPublic | BindingFlags.Instance);
            idxField.SetValue(_cycle, 1); // KNIFE

            Vector3 posBefore = holder.transform.localPosition;
            Quaternion rotBefore = holder.transform.localRotation;

            // Nudge the held knife's offset + euler (what the F9 tool routes for a non-axe weapon).
            bool edited = _cycle.NudgeCurrentWeapon(new Vector3(0.1f, 0.05f, -0.2f), new Vector3(0f, 25f, 0f), 1f);
            Assert.IsTrue(edited, "NudgeCurrentWeapon must edit a NON-axe weapon (the axe-only-bug fix)");

            // The rendered holder transform must have MOVED immediately (the dial shows this frame).
            Assert.AreNotEqual(posBefore, holder.transform.localPosition,
                "the rendered knife holder's localPosition must change on a nudge (not just the backing array)");
            Assert.Greater(Quaternion.Angle(rotBefore, holder.transform.localRotation), 1f,
                "the rendered knife holder's localRotation must change on a nudge");

            // CRITICAL (#100): let a rig LateUpdate frame run — the move must SURVIVE (the rig drives the root,
            // NOT the child holder, so the per-weapon offset is not stomped).
            Vector3 posAfterNudge = holder.transform.localPosition;
            yield return null;
            yield return null;
            Assert.AreEqual(posAfterNudge, holder.transform.localPosition,
                "#100: the per-weapon nudge must SURVIVE a HeldToolRig.LateUpdate frame (the rig drives the " +
                "root, not the child holder — the bug was the rig stomping the nudge every frame)");
        }

        // (2) The axe HEAD dial shrinks the BLADE verts toward the eye but leaves the GRIP/haft-core verts anchored.
        [UnityTest]
        public IEnumerator AxeHeadDial_ShrinksBladeVerts_LeavesGripAnchored()
        {
            BuildRigDrivenHeroAxe();
            yield return null; // Awake captures the holder + seeds the live arrays

            Mesh src = MakeAxeLikeMesh();
            Vector3[] baseV = src.vertices;
            // Indices: 0-5 = haft/grip (|x| small), 6-9 = blade (large +X, upper Y).
            float gripXBefore = baseV[0].x, gripYBefore = baseV[0].y; // the grip vert at y=0
            float bladeXBefore = baseV[7].x;                          // a blade vert (max +X)

            Assert.IsTrue(_cycle.DialAxeHead(0.5f), "DialAxeHead must succeed with the axe held (index 0)");
            Assert.Less(_cycle.AxeHeadFactor, 1f, "the head factor must drop below 1 after a shrink");

            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);
            Mesh shown = holder.sharedMesh;
            Assert.IsNotNull(shown);
            Assert.IsTrue(shown.name.Contains("headDial"),
                "the head dial must display a per-instance CLONE, never mutate the shared mesh asset");
            Vector3[] dialV = shown.vertices;

            float bladeXAfter = dialV[7].x;
            float gripXAfter = dialV[0].x, gripYAfter = dialV[0].y;
            Assert.Less(Mathf.Abs(bladeXAfter), Mathf.Abs(bladeXBefore) - 0.05f,
                $"the blade vert must shrink toward the eye (|x| {bladeXBefore:F3} -> {bladeXAfter:F3})");
            // The GRIP vert (bottom of the haft, on the centreline) must NOT move — the head shrinks relative
            // to the handle (#100: the old classifier moved ~80% of the mesh, so the grip drifted too).
            Assert.That(gripXAfter, Is.EqualTo(gripXBefore).Within(0.001f),
                $"the grip vert X must NOT move (head shrinks vs the handle; x {gripXBefore:F3} -> {gripXAfter:F3})");
            Assert.That(gripYAfter, Is.EqualTo(gripYBefore).Within(0.001f),
                $"the grip vert Y must NOT move (the handle length is preserved; y {gripYBefore:F3} -> {gripYAfter:F3})");
            Object.Destroy(src);
        }

        // (2b) The HELD nudge is INERT on the axe (index 0) — the axe hold is the shared-seat rig baseline.
        [UnityTest]
        public IEnumerator NudgeCurrentWeapon_OnAxe_IsInert_AxeUsesSharedSeat()
        {
            BuildRigDrivenHeroAxe();
            yield return null;
            bool edited = _cycle.NudgeCurrentWeapon(Vector3.one, Vector3.one, 1f);
            Assert.IsFalse(edited, "the axe nudge must route to the shared-seat HeldAxeRig, not the per-weapon arrays");
        }

        // (3) The [B] weapon-cycle and the [N] F9 arm-switch are DISTINCT keys (the soak-round-2 conflict fix).
        [Test]
        public void WeaponCycle_AndArmSwitch_UseDistinctKeys()
        {
            var cycleGo = new GameObject("HeroAxe");
            cycleGo.AddComponent<MeshFilter>();
            var cycle = cycleGo.AddComponent<HeldWeaponCycleDebug>();
            var nudge = new GameObject("Boot").AddComponent<AxeNudgeTool>();
            Assert.AreEqual(KeyCode.B, cycle.cycleKey, "the weapon-cycle must stay on [B]");
            Assert.AreEqual(KeyCode.N, nudge.armSwitchKey,
                "the F9 arm-switch must move to [N] so it never cross-fires with the always-on weapon-cycle [B]");
            Assert.AreNotEqual(cycle.cycleKey, nudge.armSwitchKey,
                "the weapon-cycle key and the arm-switch key must be DISTINCT (the [B]-binding-conflict fix)");
            Object.Destroy(nudge.gameObject);
            Object.Destroy(cycleGo);
        }
    }
}
