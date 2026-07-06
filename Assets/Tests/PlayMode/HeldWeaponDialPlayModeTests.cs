using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the weapon dial handles (86cabh907 generalized nudge), REWORKED for the 86cakkfz9
    /// v3 dial-in (absorbs 86cajuuz0):
    ///
    ///   1. The generalized HELD-weapon nudge actually MOVES the rendered mesh-holder transform for a NON-axe
    ///      weapon — AND that move SURVIVES a HeldToolRig.LateUpdate frame. #100 root cause: the in-house axe
    ///      FBX is a single-node FBX (preserveHierarchy:0) so the MeshFilter collapses onto the HeroAxe ROOT,
    ///      the SAME transform the rig overwrites every frame — so the old code's per-weapon offset/euler was
    ///      STOMPED next frame and the F9 nudge "did nothing" for knife/sword/spear. The fix re-homes the mesh
    ///      onto a child the rig never touches; this test reproduces the rig-on-root topology and asserts the
    ///      VISIBLE holder localPosition/localRotation changed after the nudge AND after a rig frame.
    ///   2. OVERALL HELD-SCALE (86cakkfz9): NudgeCurrentWeapon's scale factor actually resizes a NON-axe weapon's
    ///      held scale live, and the axe nudge stays INERT (its hold is the shared-seat rig baseline).
    ///   3. The Danish-safe O/I overall-held-scale keys are LETTER keys (86cakkfz9 — the ]/=/[/- punctuation keys
    ///      don't register on the Sponsor's Danish laptop), distinct from gameplay/belt keys.
    ///   4. The [B] (weapon-cycle) and [N] (F9 arm-switch) bindings are DISTINCT — the soak-round-2 conflict fix.
    ///
    /// 86cajuuz0 ABSORBED: the runtime axe HEAD-vertex resize dial (DialAxeHead / SetAxeHeadFactor / AxeHeadFactor)
    /// is REMOVED — the axe head is authored Blender geometry now; vertex-scaling the knapped biface is the
    /// rejected "chipping". The removal is proven by this file compiling against the trimmed API surface.
    ///
    /// These catch the bug CLASS, not the instance: a regression that lets the rig stomp the per-weapon nudge, or
    /// makes the non-axe nudge/scale a no-op, or re-binds the scale keys onto punctuation/gameplay keys, reds here.
    /// </summary>
    public class HeldWeaponDialPlayModeTests
    {
        // A taller "axe-like" mesh with a HAFT column (grip y=0) up through a coherent HEAD box (y 1.4..2.0). Used
        // to reproduce the #100 rig-on-root topology for the non-axe nudge test.
        private static Mesh MakeAxeLikeMesh()
        {
            var m = new Mesh { name = "synthetic_axe" };
            var verts = new Vector3[]
            {
                new Vector3(0f, 0.0f, 0f), new Vector3(0.02f, 0.4f, 0f), new Vector3(-0.02f, 0.8f, 0f),
                new Vector3(0f, 1.1f, 0f),
                new Vector3(-0.30f, 1.4f, -0.06f), new Vector3(0.30f, 1.4f, -0.06f),
                new Vector3(0.30f, 1.4f,  0.06f), new Vector3(-0.30f, 1.4f, 0.06f),
                new Vector3(-0.30f, 2.0f, -0.06f), new Vector3(0.30f, 2.0f, -0.06f),
                new Vector3(0.30f, 2.0f,  0.06f), new Vector3(-0.30f, 2.0f, 0.06f),
            };
            m.vertices = verts;
            m.triangles = new[] { 0, 1, 2, 2, 3, 4, 4, 5, 6, 6, 7, 8, 8, 9, 10, 10, 11, 4 };
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

            bool edited = _cycle.NudgeCurrentWeapon(new Vector3(0.1f, 0.05f, -0.2f), new Vector3(0f, 25f, 0f), 1f);
            Assert.IsTrue(edited, "NudgeCurrentWeapon must edit a NON-axe weapon (the axe-only-bug fix)");

            Assert.AreNotEqual(posBefore, holder.transform.localPosition,
                "the rendered knife holder's localPosition must change on a nudge (not just the backing array)");
            Assert.Greater(Quaternion.Angle(rotBefore, holder.transform.localRotation), 1f,
                "the rendered knife holder's localRotation must change on a nudge");

            Vector3 posAfterNudge = holder.transform.localPosition;
            yield return null;
            yield return null;
            Assert.AreEqual(posAfterNudge, holder.transform.localPosition,
                "#100: the per-weapon nudge must SURVIVE a HeldToolRig.LateUpdate frame (the rig drives the " +
                "root, not the child holder — the bug was the rig stomping the nudge every frame)");
        }

        // (2) OVERALL HELD-SCALE (86cakkfz9 — the [O]/[I] repoint target): NudgeCurrentWeapon's scale factor
        // resizes a NON-axe weapon's held scale live (up scales up, down scales down), and the change shows on the
        // rendered holder. The axe scale is Sponsor-LOCKED (its nudge is inert — covered by (2b)).
        [UnityTest]
        public IEnumerator NonAxeHeldScale_NudgeFactor_ResizesLiveScale()
        {
            BuildRigDrivenHeroAxe();
            yield return null;

            var idxField = typeof(HeldWeaponCycleDebug).GetField("_index",
                BindingFlags.NonPublic | BindingFlags.Instance);
            idxField.SetValue(_cycle, 1); // KNIFE

            float scaleBefore = _cycle.CurrentScale;
            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);
            Vector3 holderScaleBefore = holder.transform.localScale;

            Assert.IsTrue(_cycle.NudgeCurrentWeapon(Vector3.zero, Vector3.zero, 1.05f),
                "a scale-up nudge must edit a NON-axe weapon");
            Assert.Greater(_cycle.CurrentScale, scaleBefore, "a +5% scale factor must raise the held scale");
            Assert.Greater(holder.transform.localScale.x, holderScaleBefore.x,
                "the rendered holder must actually scale up (not just the backing value)");

            float scaleUp = _cycle.CurrentScale;
            Assert.IsTrue(_cycle.NudgeCurrentWeapon(Vector3.zero, Vector3.zero, 1f / 1.05f),
                "a scale-down nudge must edit a NON-axe weapon");
            Assert.Less(_cycle.CurrentScale, scaleUp, "a -5% scale factor must lower the held scale");
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

        // (3) The Danish-safe overall-held-scale keys are LETTER keys O / I (86cakkfz9 — the ]/=/[/- punctuation
        // keys don't register on the Sponsor's Danish laptop). Catches a regression that re-binds them onto
        // punctuation or a gameplay/belt key.
        [Test]
        public void DanishSafeScaleKeys_AreLetters_O_And_I_NotGameplayKeys()
        {
            var go = new GameObject("HeroAxe");
            go.AddComponent<MeshFilter>();
            var cycle = go.AddComponent<HeldWeaponCycleDebug>();
            Assert.AreEqual(KeyCode.O, cycle.scaleUpKeyDanish, "Danish-safe scale-up key must be the letter O");
            Assert.AreEqual(KeyCode.I, cycle.scaleDownKeyDanish, "Danish-safe scale-down key must be the letter I");
            foreach (var reserved in new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space,
                KeyCode.B, KeyCode.N, KeyCode.Alpha1, KeyCode.Alpha9 })
            {
                Assert.AreNotEqual(reserved, cycle.scaleUpKeyDanish, "scale-up key must be free of gameplay/belt keys");
                Assert.AreNotEqual(reserved, cycle.scaleDownKeyDanish, "scale-down key must be free of gameplay/belt keys");
            }
            Assert.AreNotEqual(cycle.scaleUpKeyDanish, cycle.scaleDownKeyDanish, "up + down must be distinct keys");
            Object.Destroy(go);
        }

        // (4) The [B] weapon-cycle and the [N] F9 arm-switch are DISTINCT keys (the soak-round-2 conflict fix).
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

        // (5) The F9 nudge tool's TARGET-CYCLE key must NOT be Tab — Tab is the inventory toggle
        // (InventoryUI.toggleKey), and the two conflicted (86cabh907 dial-tool round, Sponsor blocker #3).
        [Test]
        public void NudgeTargetCycle_IsNotTheInventoryToggle_NorAGameplayKey()
        {
            var axeNudge = new GameObject("Boot").AddComponent<AxeNudgeTool>();
            var worldNudge = new GameObject("BootW").AddComponent<WorldLookNudgeTool>();
            Assert.AreNotEqual(KeyCode.Tab, axeNudge.cycleKey,
                "the F9 nudge target-cycle must NOT be Tab (Tab is the inventory toggle — Sponsor blocker #3)");
            Assert.AreNotEqual(KeyCode.Tab, worldNudge.cycleKey,
                "the F10 world-look nudge target-cycle must NOT be Tab either (same inventory conflict)");
            foreach (var reserved in new[] { KeyCode.B, KeyCode.N, KeyCode.Space, KeyCode.LeftShift,
                KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D })
                Assert.AreNotEqual(reserved, axeNudge.cycleKey,
                    "the nudge cycle key must be free of gameplay/belt keys (got " + axeNudge.cycleKey + ")");
            Object.Destroy(axeNudge.gameObject);
            Object.Destroy(worldNudge.gameObject);
        }
    }
}
