using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the HELD-WEAPON PLACEMENT seam (ticket 86caffwuz) — the single binding surface the
    /// unified settings console's 7 held-weapon rows drive over the two seat backends (the axe rig + the
    /// per-weapon arrays). This proves the seam ROUTES correctly and the dialed change SURVIVES a HeldToolRig
    /// frame (the #100 rig-stomp class) for BOTH the axe and a non-axe weapon:
    ///
    ///   1. AXE current → SetOffset/SetEuler drive the SAME HeldAxeRig fields the equip path uses (one shared
    ///      seat, no parallel attach), and the rig re-applies them every frame (the dial holds).
    ///   2. AXE scale defaults to 1.0 (a multiplier of the LOCKED mesh-holder baseline) and a 1.0 set leaves the
    ///      seat byte-identical (bar #6 — never regress the praised grip); a >1 set scales the holder uniformly.
    ///   3. NON-AXE current (knife) → SetOffset/SetEuler/Scale route through the per-weapon arrays and MOVE the
    ///      rendered child holder, surviving a rig frame.
    ///
    /// These catch the bug CLASS: a seam that bound a parallel transform, didn't survive the rig, or mis-routed
    /// the axe-vs-non-axe backend, reds here. (The EditMode SettingsCatalogHeldWeaponTests prove the catalog
    /// rows bind the seam; this proves the seam drives the live seat.)
    /// </summary>
    public class HeldWeaponPlacementPlayModeTests
    {
        private GameObject _go;
        private HeldAxeRig _rig;
        private HeldWeaponCycleDebug _cycle;
        private HeldWeaponPlacement _placement;
        private Transform _hand;

        private static Mesh MakeAxeLikeMesh()
        {
            var m = new Mesh { name = "synthetic_axe" };
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0.02f, 0.4f, 0f), new Vector3(-0.02f, 0.8f, 0f),
                new Vector3(0f, 1.1f, 0f),
                new Vector3(-0.30f, 1.4f, -0.06f), new Vector3(0.30f, 1.4f, -0.06f),
                new Vector3(0.30f, 2.0f, 0.06f), new Vector3(-0.30f, 2.0f, 0.06f),
            };
            m.triangles = new[] { 0, 1, 2, 2, 3, 4, 4, 5, 6, 6, 7, 4 };
            m.RecalculateBounds();
            return m;
        }

        private void BuildSeat()
        {
            _go = new GameObject("HeroAxe");
            var mf = _go.AddComponent<MeshFilter>();
            mf.sharedMesh = MakeAxeLikeMesh();
            _go.AddComponent<MeshRenderer>();
            _hand = new GameObject("RightHand").transform;
            _hand.position = new Vector3(2f, 1f, 0f);
            _rig = _go.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.worldOffsetFromHand = new Vector3(0.1712f, 0.1209f, -0.0007f);
            _rig.relEuler = new Vector3(-186f, -168f, -84f);
            _cycle = _go.AddComponent<HeldWeaponCycleDebug>();
            _placement = _go.AddComponent<HeldWeaponPlacement>();
            _placement.axeRig = _rig;
            _placement.weaponCycle = _cycle;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.Contains("synthetic"))
                    Object.Destroy(mf.sharedMesh);
            if (_go != null) Object.Destroy(_go);
            if (_hand != null) Object.Destroy(_hand.gameObject);
        }

        // (1) AXE current — the seam drives the SAME HeldAxeRig seat the equip path uses, and the rig holds it.
        [UnityTest]
        public IEnumerator AxeCurrent_SeamDrivesTheRigSeat_AndSurvivesRigFrame()
        {
            BuildSeat();
            yield return null; // Awake

            Assert.IsTrue(_placement.IsAxeCurrent, "axe is the default current weapon (index 0)");

            // SetOffset drives the rig's hand-local field — the same one AttachHeroAxeToHand bakes.
            _placement.SetOffset(new Vector3(0.2f, 0.25f, 0.05f));
            Assert.AreEqual(new Vector3(0.2f, 0.25f, 0.05f), _rig.worldOffsetFromHand,
                "the seam's SetOffset must drive HeldAxeRig.worldOffsetFromHand (one shared seat, no parallel attach)");

            // SetEuler drives relEuler.
            _placement.SetEuler(new Vector3(-180f, -160f, -90f));
            Assert.AreEqual(new Vector3(-180f, -160f, -90f), _rig.relEuler,
                "the seam's SetEuler must drive HeldAxeRig.relEuler");

            // The read-back matches (the rows reflect the live seat).
            Assert.AreEqual(0.25f, _placement.OffsetY, 1e-4f, "OffsetY reads the live rig offset");
            Assert.AreEqual(-90f, _placement.Roll, 1e-3f, "Roll reads the live rig euler");

            // The rig re-applies the seat every frame — let a frame run; the dialed FIELDS persist (the dial holds).
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector3(0.2f, 0.25f, 0.05f), _rig.worldOffsetFromHand,
                "the dialed offset must persist across rig frames (the rig drives FROM the field, not over it)");
        }

        // (2) AXE scale — defaults to 1.0 (locked baseline multiplier); a 1.0 set leaves the holder byte-identical.
        [UnityTest]
        public IEnumerator AxeScale_DefaultsToOne_AndUniformlyScalesTheLockedHolder()
        {
            BuildSeat();
            yield return null; // Awake re-homes the mesh onto the child holder + captures the scale baseline

            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);
            Assert.IsNotNull(holder, "the cycle must have a mesh holder");

            Assert.AreEqual(1f, _placement.Scale, 1e-3f, "the axe scale defaults to 1.0x (locked-baseline multiplier)");
            Vector3 lockedScale = holder.transform.localScale;

            // A 1.0 set is a no-op on the locked holder (byte-identical — bar #6 don't regress the praised grip).
            _placement.Scale = 1f;
            Assert.AreEqual(lockedScale, holder.transform.localScale,
                "setting the axe scale to 1.0 must leave the locked mesh-holder byte-identical (no grip regression)");

            // A >1 set scales the holder uniformly about the captured baseline.
            _placement.Scale = 1.5f;
            Assert.AreEqual(lockedScale.x * 1.5f, holder.transform.localScale.x, 1e-3f,
                "the axe scale row must uniformly scale the holder about the locked baseline (1.5x)");
            Assert.That(_placement.Scale, Is.EqualTo(1.5f).Within(1e-3f), "the scale read-back reflects the set value");
        }

        // (3) NON-AXE current (knife) — the seam routes through the per-weapon arrays + moves the rendered holder.
        [UnityTest]
        public IEnumerator NonAxeCurrent_SeamRoutesThroughPerWeaponArrays_MovesHolder_SurvivesRigFrame()
        {
            BuildSeat();
            yield return null; // Awake

            // Select the knife (index 1) directly (no Resources prefab in the harness).
            var idxField = typeof(HeldWeaponCycleDebug).GetField("_index",
                BindingFlags.NonPublic | BindingFlags.Instance);
            idxField.SetValue(_cycle, 1);
            Assert.IsFalse(_placement.IsAxeCurrent, "knife is the current weapon → not the axe path");
            Assert.AreEqual("KNIFE", _placement.CurrentLabel, "the seam reports the current weapon label");

            var holderField = typeof(HeldWeaponCycleDebug).GetField("_meshHolder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var holder = (MeshFilter)holderField.GetValue(_cycle);
            Vector3 posBefore = holder.transform.localPosition;

            // Drive the knife offset through the seam — it must move the rendered holder (per-weapon array path),
            // NOT the axe rig (the axe seat must stay untouched while a non-axe weapon is current).
            Vector3 rigBefore = _rig.worldOffsetFromHand;
            _placement.SetOffset(new Vector3(0.1f, 0.05f, -0.2f));
            Assert.AreNotEqual(posBefore, holder.transform.localPosition,
                "the seam's SetOffset on a knife must move the rendered child holder (per-weapon array path)");
            Assert.AreEqual(rigBefore, _rig.worldOffsetFromHand,
                "driving a NON-axe weapon must NOT touch the axe rig seat (the axe stays locked while knife is current)");

            // The move survives a rig frame (the rig drives the root, not the child holder — #100).
            Vector3 posAfter = holder.transform.localPosition;
            yield return null;
            yield return null;
            Assert.AreEqual(posAfter, holder.transform.localPosition,
                "#100: the per-weapon nudge via the seam must survive a HeldToolRig.LateUpdate frame");
        }
    }
}
