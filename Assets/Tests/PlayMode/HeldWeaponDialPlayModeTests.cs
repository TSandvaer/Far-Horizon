using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the 86cabh907 weapon dial handles, REWORKED for the "STOP chipping" blocker
    /// (Sponsor: "everytime you make the axe head smaller it looks worse. its like youre chipping off the axe
    /// head instead of just resizing it"):
    ///
    ///   1. The generalized HELD-weapon nudge actually MOVES the rendered mesh-holder transform for a NON-axe
    ///      weapon — AND that move SURVIVES a HeldToolRig.LateUpdate frame. #100 root cause: the in-house axe
    ///      FBX is a single-node FBX (preserveHierarchy:0) so the MeshFilter collapses onto the HeroAxe ROOT,
    ///      the SAME transform the rig overwrites every frame — so the old code's per-weapon offset/euler was
    ///      STOMPED next frame and the F9 nudge "did nothing" for knife/sword/spear. The fix re-homes the mesh
    ///      onto a child the rig never touches; this test reproduces the rig-on-root topology and asserts the
    ///      VISIBLE holder localPosition/localRotation changed after the nudge AND after a rig frame.
    ///   2. The axe HEAD-size dial resizes the WHOLE head UNIFORMLY about the head<->haft junction — it scales
    ///      the head's bounding box by the SAME factor on EVERY axis (no axis-squish) and PRESERVES the head's
    ///      aspect ratio, WHILE the grip/haft below the junction stays anchored. This is the STOP-chipping
    ///      contract: the OLD dial classified an off-centreline blade SUBSET and scaled it toward an eye pivot,
    ///      squishing the head into a sliver. A regression back to a non-uniform / subset scale (head aspect
    ///      ratio changes, or one axis squishes more than another) reds here. Operates on a per-instance CLONE.
    ///   3. The [B] (weapon-cycle) and [N] (F9 arm-switch) bindings are DISTINCT — the soak-round-2 conflict fix.
    ///
    /// These catch the bug CLASS, not the instance: a regression that lets the rig stomp the per-weapon nudge,
    /// or makes the head dial NON-uniform / squish the head / move the grip, or makes the non-axe nudge a
    /// no-op, reds here.
    /// </summary>
    public class HeldWeaponDialPlayModeTests
    {
        // The head<->haft junction fraction the dial cuts at for THIS synthetic mesh. The PRODUCTION default
        // (HeldWeaponCycleDebug.headJunctionFraction) is geometry-specific — it was re-measured to 0.50 for the
        // restored 4208067 stone-axe FBX (86cabh907; the old 0.62 was tuned for the rejected flat-wood mesh and
        // mis-grabbed haft verts on the stone mesh). This is an ALGORITHM test on a SYNTHETIC mesh, so it SETS
        // the fraction EXPLICITLY (in BuildRigDrivenHeroAxe) to decouple from the real-FBX default — the dial's
        // uniform-scale contract is what's under test, not the real axe's specific junction. For the synthetic
        // mesh the haft runs y=0..2.0, head box y=1.4..2.0, grip y=0..1.1; 0.62 → junction y=1.24 sits in the
        // clean gap (1.1..1.4) so verts above it are exactly the WHOLE head box.
        private const float JunctionFraction = 0.62f;

        // A taller "axe-like" mesh whose HEAD is a clean island ABOVE the head<->haft junction (a coherent
        // box-ish head with distinct width/height/depth so an aspect-ratio check is meaningful), plus a HAFT
        // column from the grip (y=0) up THROUGH the junction. The STOP-chipping head dial must resize the WHOLE
        // head (every vert above the junction) UNIFORMLY — preserving its aspect ratio — and leave the
        // grip/lower-haft anchored.
        private static Mesh MakeAxeLikeMesh()
        {
            var m = new Mesh { name = "synthetic_axe" };
            var verts = new Vector3[]
            {
                // --- HAFT / grip: below the junction (y = 1.24). These must NOT move when the head resizes. ---
                new Vector3(0f, 0.0f, 0f), new Vector3(0.02f, 0.4f, 0f), new Vector3(-0.02f, 0.8f, 0f),
                new Vector3(0f, 1.1f, 0f),
                // --- HEAD: a coherent box ABOVE the junction (y 1.4..2.0; x -0.30..0.30; z -0.06..0.06). A clear
                //     width(0.60) != height(0.60) != depth(0.12) so a UNIFORM resize is provable by aspect ratio.
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

        // Axis-aligned bounds of a vert subset (for the head aspect-ratio check).
        private static Bounds BoundsOf(Vector3[] v, params int[] idx)
        {
            var b = new Bounds(v[idx[0]], Vector3.zero);
            foreach (int i in idx) b.Encapsulate(v[i]);
            return b;
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
            // ALGORITHM test: pin the junction fraction to THIS synthetic mesh's gap (0.62 → y=1.24 in the
            // 1.1..1.4 gap), decoupled from the production default (0.50 for the real stone FBX). The uniform-
            // scale contract is what's under test, not the real axe's specific junction.
            _cycle.headJunctionFraction = JunctionFraction;
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

        // (2) STOP-chipping: the axe HEAD dial resizes the WHOLE head UNIFORMLY about the junction — same factor
        // on every axis (no axis-squish), head ASPECT RATIO preserved — while the grip/haft below the junction
        // stays anchored. The OLD dial scaled an off-centreline blade SUBSET toward an eye pivot, squishing the
        // head into a sliver (the chipping). This is the regression guard for that whole class.
        [UnityTest]
        public IEnumerator AxeHeadDial_ResizesWholeHeadUniformly_PreservesAspect_LeavesGripAnchored()
        {
            BuildRigDrivenHeroAxe();
            yield return null; // Awake captures the holder + seeds the live arrays

            Mesh src = MakeAxeLikeMesh();
            Vector3[] baseV = src.vertices;
            // Indices: 0-3 = haft/grip (below junction y=1.24), 4-11 = the head box (above the junction).
            int[] headIdx = { 4, 5, 6, 7, 8, 9, 10, 11 };
            float gripXBefore = baseV[0].x, gripYBefore = baseV[0].y; // the grip vert at y=0 (below junction)
            Bounds headBefore = BoundsOf(baseV, headIdx);

            const float factor = 0.5f;
            Assert.IsTrue(_cycle.DialAxeHead(factor), "DialAxeHead must succeed with the axe held (index 0)");
            Assert.Less(_cycle.AxeHeadFactor, 1f, "the head factor must drop below 1 after a shrink");

            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);
            Mesh shown = holder.sharedMesh;
            Assert.IsNotNull(shown);
            Assert.IsTrue(shown.name.Contains("headDial"),
                "the head dial must display a per-instance CLONE, never mutate the shared mesh asset");
            Vector3[] dialV = shown.vertices;
            Bounds headAfter = BoundsOf(dialV, headIdx);

            // --- UNIFORM: each axis of the head's bounding box scaled by the SAME factor (within tolerance).
            // (A directional / single-axis squish — the chipping — would scale one axis far more than another.)
            Vector3 sBefore = headBefore.size, sAfter = headAfter.size;
            float fx = sAfter.x / sBefore.x, fy = sAfter.y / sBefore.y, fz = sAfter.z / sBefore.z;
            Assert.That(fx, Is.EqualTo(factor).Within(0.02f), $"head WIDTH must scale by {factor} (got {fx:F3}) — uniform, no squish");
            Assert.That(fy, Is.EqualTo(factor).Within(0.02f), $"head HEIGHT must scale by {factor} (got {fy:F3}) — uniform, no squish");
            Assert.That(fz, Is.EqualTo(factor).Within(0.02f), $"head DEPTH must scale by {factor} (got {fz:F3}) — uniform, no squish");
            Assert.That(fx, Is.EqualTo(fy).Within(0.02f), "head x-scale must equal y-scale (x==y==z — no axis-squish / chipping)");
            Assert.That(fy, Is.EqualTo(fz).Within(0.02f), "head y-scale must equal z-scale (x==y==z — no axis-squish / chipping)");

            // --- ASPECT RATIO preserved: the head's width:height:depth ratios are unchanged after the resize.
            float arWH_before = sBefore.x / sBefore.y, arWH_after = sAfter.x / sAfter.y;
            float arWD_before = sBefore.x / sBefore.z, arWD_after = sAfter.x / sAfter.z;
            Assert.That(arWH_after, Is.EqualTo(arWH_before).Within(0.02f),
                $"head width:height aspect must be preserved ({arWH_before:F3} -> {arWH_after:F3}) — a resize, not a chip");
            Assert.That(arWD_after, Is.EqualTo(arWD_before).Within(0.02f),
                $"head width:depth aspect must be preserved ({arWD_before:F3} -> {arWD_after:F3}) — a resize, not a chip");

            // --- the head actually got SMALLER (the dial does something).
            Assert.Less(sAfter.x, sBefore.x - 0.05f, "the head must actually shrink (width down)");

            // --- the GRIP vert (below the junction, on the centreline) must NOT move — handle length preserved.
            Assert.That(dialV[0].x, Is.EqualTo(gripXBefore).Within(0.001f),
                $"the grip vert X must NOT move (head resizes vs the handle; x {gripXBefore:F3} -> {dialV[0].x:F3})");
            Assert.That(dialV[0].y, Is.EqualTo(gripYBefore).Within(0.001f),
                $"the grip vert Y must NOT move (the handle length is preserved; y {gripYBefore:F3} -> {dialV[0].y:F3})");
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

        // (2c) DANISH-KEYBOARD MOUSE SLIDER driver (86cabh907). The F9 panel's mouse slider calls
        // SetAxeHeadFactor(absolute) — it must (a) set the factor to the requested ABSOLUTE value within the
        // clamp range, (b) drive the SAME uniform-scale path the multiplicative dial uses (a per-instance CLONE
        // resized uniformly about the junction — NOT a new scale path, NOT the shared asset), and (c) CLAMP out
        // of range. This catches the bug class: a slider that drove a separate/non-uniform scale path, mutated
        // the shared mesh, or didn't clamp, reds here. It proves the slider == the dial's resize, just absolute.
        [UnityTest]
        public IEnumerator SetAxeHeadFactor_AbsoluteSet_ResizesViaTheSameUniformClonePath_AndClamps()
        {
            BuildRigDrivenHeroAxe();
            yield return null; // Awake captures the holder + seeds the live arrays

            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);

            // (a) absolute set lands the factor exactly (within the clamp range).
            Assert.IsTrue(_cycle.SetAxeHeadFactor(0.6f), "SetAxeHeadFactor must succeed with the axe held (index 0)");
            Assert.That(_cycle.AxeHeadFactor, Is.EqualTo(0.6f).Within(1e-4f),
                "the slider's ABSOLUTE set must land the factor at exactly the requested value (not a relative step)");

            // (b) it drives the SAME uniform-clone path the multiplicative dial uses — a per-instance CLONE, never
            // the shared asset, and the head box scales uniformly (same factor every axis) about the junction.
            Mesh shown = holder.sharedMesh;
            Assert.IsTrue(shown.name.Contains("headDial"),
                "SetAxeHeadFactor must display the per-instance CLONE (the same ApplyAxeHead path as the dial) — " +
                "never mutate the shared mesh asset, never a separate scale path");
            int[] headIdx = { 4, 5, 6, 7, 8, 9, 10, 11 };
            Vector3[] baseV = MakeAxeLikeMeshVertsForCheck();
            Bounds headBefore = BoundsOf(baseV, headIdx);
            Bounds headAfter = BoundsOf(shown.vertices, headIdx);
            Vector3 sB = headBefore.size, sA = headAfter.size;
            float fx = sA.x / sB.x, fy = sA.y / sB.y, fz = sA.z / sB.z;
            Assert.That(fx, Is.EqualTo(0.6f).Within(0.02f), $"head WIDTH must scale to the absolute factor (got {fx:F3})");
            Assert.That(fx, Is.EqualTo(fy).Within(0.02f), "uniform: x-scale == y-scale (no axis-squish)");
            Assert.That(fy, Is.EqualTo(fz).Within(0.02f), "uniform: y-scale == z-scale (no axis-squish)");

            // (c) out-of-range absolute values CLAMP to [HeadFactorMin..HeadFactorMax].
            _cycle.SetAxeHeadFactor(99f);
            Assert.That(_cycle.AxeHeadFactor, Is.EqualTo(HeldWeaponCycleDebug.HeadFactorMax).Within(1e-4f),
                "an above-range slider value must clamp to HeadFactorMax");
            _cycle.SetAxeHeadFactor(-5f);
            Assert.That(_cycle.AxeHeadFactor, Is.EqualTo(HeldWeaponCycleDebug.HeadFactorMin).Within(1e-4f),
                "a below-range slider value must clamp to HeadFactorMin");
            yield return null;
        }

        // The synthetic axe's base verts (factor=1 baseline) for the resize-ratio check above — built fresh so
        // the test does not depend on a shared mesh instance that ApplyAxeHead may have cloned/replaced.
        private static Vector3[] MakeAxeLikeMeshVertsForCheck()
        {
            var m = MakeAxeLikeMesh();
            var v = m.vertices;
            Object.Destroy(m);
            return v;
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

        // (4) The F9 nudge tool's TARGET-CYCLE key must NOT be Tab — Tab is the inventory toggle
        // (InventoryUI.toggleKey), and the two conflicted (86cabh907 dial-tool round, Sponsor blocker #3).
        // Catches the bug CLASS: any rebind of the nudge cycle back onto Tab (or onto the belt 1..9 / [B] / [N]
        // / a dial key) reds here.
        [Test]
        public void NudgeTargetCycle_IsNotTheInventoryToggle_NorAGameplayKey()
        {
            var axeNudge = new GameObject("Boot").AddComponent<AxeNudgeTool>();
            var worldNudge = new GameObject("BootW").AddComponent<WorldLookNudgeTool>();
            Assert.AreNotEqual(KeyCode.Tab, axeNudge.cycleKey,
                "the F9 nudge target-cycle must NOT be Tab (Tab is the inventory toggle — Sponsor blocker #3)");
            Assert.AreNotEqual(KeyCode.Tab, worldNudge.cycleKey,
                "the F10 world-look nudge target-cycle must NOT be Tab either (same inventory conflict)");
            // Not a reserved gameplay / belt / dial key.
            foreach (var reserved in new[] { KeyCode.B, KeyCode.N, KeyCode.Space, KeyCode.LeftShift,
                KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D })
                Assert.AreNotEqual(reserved, axeNudge.cycleKey,
                    "the nudge cycle key must be free of gameplay/belt keys (got " + axeNudge.cycleKey + ")");
            Object.Destroy(axeNudge.gameObject);
            Object.Destroy(worldNudge.gameObject);
        }
    }
}
