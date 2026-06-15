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

        private Vector3[] SampleTips()
        {
            var v = new Vector3[3];
            for (int i = 0; i < 3; i++) v[i] = _fingerTips[i].position;
            return v;
        }
    }
}
