using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// RE-SOAK #4 regression guard for the right-hand FINGER CURL (ticket 86ca8rdkp — "his right finger is
    /// mangled").
    ///
    /// DIAGNOSE-VIA-TRACE — OVERTURNED the ticket hypothesis: -fingerTrace proved the skinning is CLEAN (every
    /// right-hand finger bone reads lossyScale (1.8,1.8,1.8), localScale (1,1,1), verts tight to their bones —
    /// no degenerate bone / collapsed weight). The "mangled" read is a POSE mismatch: the imported clips pose
    /// the hand OPEN, so a held axe in an open splayed hand reads as broken fingers. FIX is NOT re-weighting —
    /// it is CURLING the fingers into a grip (CastawayFingerCurl), gated on the axe being HELD (HasAxe).
    ///
    /// THE BUG CLASS (not just the instance): the load-bearing invariants are (a) EMPTY-handed the fingers keep
    /// their natural OPEN clip pose (no curl — we only grip a real haft), and (b) once the axe is HELD the
    /// fingers CURL into a grip (the fingertips move toward the palm). The gate must flip with HasAxe (the craft
    /// event). A regression that always-curls (clenched empty hand) or never-curls (open 'mangled' grip) reds.
    /// </summary>
    public class CastawayFingerCurlPlayModeTests
    {
        private GameObject _root;          // the avatar root carrying CastawayFingerCurl
        private GameObject _invGo;
        private GameObject _seatGo;        // the [B] debug-cycle seat (built only by the debug-view test)
        private Inventory _inv;
        private CastawayFingerCurl _curl;
        private Transform[] _fingerBones;
        private Transform[] _fingerTips;

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("Survival");
            _inv = _invGo.AddComponent<Inventory>();

            _root = new GameObject("Avatar");
            // Three finger chains (Index/Middle/Ring), each proximal -> mid -> distal -> tip. The fingers
            // extend along +Z (like the real rig); a +local-X curl rotates the tip down/forward toward the palm.
            _fingerBones = new Transform[3];
            _fingerTips = new Transform[3];
            string[] names = { "mixamorig:RightHandIndex", "mixamorig:RightHandMiddle", "mixamorig:RightHandRing" };
            for (int i = 0; i < 3; i++)
            {
                Transform parent = _root.transform;
                Transform proximal = null, tip = null;
                for (int seg = 1; seg <= 4; seg++)
                {
                    var go = new GameObject(names[i] + seg);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = seg == 1 ? new Vector3(0.1f * i, 0f, 0.1f) : new Vector3(0f, 0f, 0.06f);
                    if (seg == 1) proximal = go.transform;
                    if (seg == 4) tip = go.transform;
                    parent = go.transform;
                }
                _fingerBones[i] = proximal;
                _fingerTips[i] = tip;
            }

            _curl = _root.AddComponent<CastawayFingerCurl>();
            _curl.fingerBones = _fingerBones;
            _curl.thumbBones = new Transform[0];
            _curl.inventory = _inv;
            _curl.fingerCurlDeg = 26f;
            _curl.RebuildCached();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_invGo);
            if (_seatGo != null) Object.Destroy(_seatGo);
        }

        // EMPTY-handed (HasAxe false): the fingers keep their OPEN clip pose — the curl must NOT fire (we only
        // grip a real haft; an always-clenched empty hand is also wrong).
        [UnityTest]
        public IEnumerator EmptyHanded_FingersStayOpen_NoCurl()
        {
            yield return null; // OnEnable applied the gate (HasAxe false -> not gripping)
            Vector3[] restTips = SampleTips();

            yield return null;
            yield return null;
            for (int i = 0; i < 3; i++)
                Assert.That(Vector3.Distance(_fingerTips[i].position, restTips[i]), Is.LessThan(1e-4f),
                    $"finger {i} tip moved while EMPTY-handed — the curl must NOT fire without a held axe " +
                    "(an always-curled empty hand is wrong; the open clip pose must stand).");
            Assert.IsFalse(_curl.IsGripping, "the curl must not be gripping while empty-handed");
        }

        // Once the axe is HELD (CraftAxe -> HasAxe), the fingers CURL into a grip: each fingertip moves a
        // bounded non-zero amount toward the palm. The gate flips on the craft event (no per-frame polling).
        [UnityTest]
        public IEnumerator AxeHeld_FingersCurlIntoAGrip_OnCraft()
        {
            yield return null;
            Vector3[] openTips = SampleTips();
            Assert.IsFalse(_curl.IsGripping, "precondition: not gripping before the craft");

            _inv.CraftAxe(); // fires Inventory.Changed -> the curl gate flips to gripping
            yield return null;
            yield return null;

            Assert.IsTrue(_curl.IsGripping, "after CraftAxe (HasAxe) the curl must be gripping");
            for (int i = 0; i < 3; i++)
            {
                float moved = Vector3.Distance(_fingerTips[i].position, openTips[i]);
                Assert.Greater(moved, 0.01f,
                    $"finger {i} tip must MOVE (curl into the grip) once the axe is held (moved {moved:F4}u) — " +
                    "a zeroed curl ships the open 'mangled' hand (#4).");
                Assert.Less(moved, 0.5f,
                    $"finger {i} curl must be a bounded grip ({moved:F4}u), not a wild clench-through.");
            }
        }

        // soak-239-v2 FOUNDATION-GAP regression (86cahnmjv — "the finger does not wrap around the handle",
        // observed on the debug-cycle SWORD). The sword/knife have NO belt items; the [B] cycle only SHOWS them
        // with no axe/spear selected — so the OLD belt-selection-only gate was OFF while the sword was in hand
        // (open 'mangled' hand). Drives the REAL HeldWeaponCycleDebug seam: with NO weapon selected, [B]-cycle to
        // a look-soak weapon (DebugViewActive) and assert the curl now GRIPS + the fingers close. A gate that
        // ignores the debug view reds here (IsGripping stays false; the fingers never curl).
        [UnityTest]
        public IEnumerator DebugViewWeapon_FingersCurl_EvenWithNoBeltSelection()
        {
            // A minimal debug-cycle seat: a Cube = MeshFilter+MeshRenderer on the root (the collapsed
            // single-node-FBX topology). HeldWeaponCycleDebug.Awake captures the root MeshFilter as the holder.
            _seatGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(_seatGo.GetComponent<Collider>());
            var cycle = _seatGo.AddComponent<HeldWeaponCycleDebug>();
            var gate = _seatGo.AddComponent<HeldAxe>();
            gate.inventory = _inv;
            _curl.weaponCycle = cycle; // wire the curl to the [B] handle (runtime-resolved on the real rig)
            yield return null; // Awake/OnEnable wiring

            Assert.IsFalse(_curl.IsGripping,
                "precondition: no belt weapon selected + no debug view -> open hand (not gripping)");
            Vector3[] openTips = SampleTips();

            // [B]-cycle off the axe (index 0) to the next look-soak weapon (knife) — no belt weapon is selected,
            // so the cycle is NOT refused and DebugViewActive turns on. This is the exact seam the Sponsor used.
            Assert.IsTrue(cycle.CycleHeldWeaponDebug(),
                "empty-handed the [B] debug cycle must engage (the knife/sword look-soak view)");
            yield return null; // the curl re-reads DebugViewActive live next LateUpdate
            yield return null;

            Assert.IsTrue(cycle.DebugViewActive, "the debug view is active (a look-soak weapon is shown)");
            Assert.IsTrue(_curl.IsGripping,
                "THE FOUNDATION-GAP GUARD: the curl must GRIP a [B] debug-view weapon even though NO belt weapon " +
                "is selected — the old belt-selection-only gate left the sword in an open hand (soak-239-v2)");
            for (int i = 0; i < 3; i++)
            {
                float moved = Vector3.Distance(_fingerTips[i].position, openTips[i]);
                Assert.Greater(moved, 0.01f,
                    $"finger {i} tip must CURL into the grip around the debug-view weapon (moved {moved:F4}u) — " +
                    "a gate that ignores the debug view ships the open 'mangled' hand on the sword/knife.");
                Assert.Less(moved, 0.5f, $"finger {i} curl must be a bounded grip ({moved:F4}u), not a clench-through.");
            }

            // An inventory change CLEARS the debug view (the cycle's sync) -> the hand re-opens (no false grip).
            _inv.CraftAxe();          // axe now owned+selected -> selection owns the visual, debug view cleared
            _inv.Model.SelectBelt(FindEmptyBelt()); // deselect to an empty slot -> nothing shown
            yield return null;
            yield return null;
            Assert.IsFalse(cycle.DebugViewActive, "an inventory change clears the [B] debug view");
            Assert.IsFalse(_curl.IsGripping,
                "with the debug view cleared AND no weapon selected the hand re-opens (no lingering false grip)");
        }

        // The first empty belt slot (to deselect all weapons) — mirrors HandsVerifyCapture.SelectEmptyBeltSlot.
        private int FindEmptyBelt()
        {
            var belt = _inv.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++) if (belt[i].IsEmpty) return i;
            return 0;
        }

        private Vector3[] SampleTips()
        {
            var v = new Vector3[3];
            for (int i = 0; i < 3; i++) v[i] = _fingerTips[i].position;
            return v;
        }
    }
}
