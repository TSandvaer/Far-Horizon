using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the SOURCED hero axe (ticket 86ca8ce6y — RE-DONE: the procedural HeroAxeMesh
    /// wedge is RETIRED; the axe is now the sourced rustic hatchet HELD in the chibi's hand) is
    /// SERIALIZED into the Boot scene the exe ships — attached under the chibi's RIGHT HAND bone, not
    /// added at Awake (the editor-vs-runtime serialization trap, unity-conventions.md, would mangle/drop
    /// an Awake-built attach — the "legs-up" / component-not-serialized classes).
    ///
    /// Binary scenes can't be GUID-grepped, so this EditMode reader is the authoritative check that the
    /// held axe MESH actually lives in Boot.unity UNDER THE HAND BONE with its HasAxe-gating HeldAxe
    /// component wired. Break the attach (or the bone search, or the HeldAxe wiring) and this goes RED in
    /// headless CI, rather than the shipped build silently lacking the loop's hero tool.
    ///
    /// NOTE on DROPPED guards (PR #28): the prior procedural-axe asserts — 3-submesh count + the per-
    /// submesh barn-red/pale-steel/warm-brown anchor-color checks (MovementCameraScene.AxeHeadColor etc.)
    /// — are REMOVED. They were specific to the retired HeroAxeMesh; the sourced hatchet ships its own
    /// baseColor atlas material (no per-submesh color anchors to assert).
    /// </summary>
    public class HeroAxeSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesHeroAxe_HeldUnderTheRightHandBone()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe,
                $"the Boot scene must carry the '{MovementCameraScene.HeroAxeObjectName}' GameObject (the " +
                "sourced hatchet) — the loop's hero tool, serialized into the scene (not Awake-built; " +
                "unity-conventions.md editor-vs-runtime trap)");

            // It must be a real model (the sourced FBX), not an empty placeholder.
            var rends = axe.GetComponentsInChildren<MeshRenderer>(true);
            Assert.Greater(rends.Length, 0, "the held axe must have at least one MeshRenderer (the sourced FBX mesh)");
            var mf = axe.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(mf, "the held axe must carry a MeshFilter");
            Assert.IsNotNull(mf.sharedMesh, "the held axe's mesh must be serialized into the scene");
            Assert.Greater(mf.sharedMesh.vertexCount, 20,
                "the held axe must be the real sourced hatchet mesh, not a placeholder primitive");

            // The ATTACH guard: the axe must be parented UNDER the chibi's right-hand bone (RightHand_010,
            // excl. the RightHand.Dummy helper) — that's what makes it RIDE the hand's animated transform.
            Transform t = axe.transform;
            bool underRightHand = false;
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains(MovementCameraScene.RightHandBoneToken) && !n.Contains("dummy") && !n.Contains("end"))
                { underRightHand = true; break; }
                t = t.parent;
            }
            Assert.IsTrue(underRightHand,
                "the held axe must be parented under the chibi's right-hand bone ('" +
                MovementCameraScene.RightHandBoneToken + "', excl. dummy) so it rides the hand — a regression " +
                "to a free-standing prop (or attaching to the dummy bone) reds here");
        }

        [Test]
        public void BootScene_HeldAxe_GatesVisibilityOnHasAxe()
        {
            // The HeldAxe component (HasAxe-gated visibility) must be SERIALIZED on the held axe with its
            // Inventory reference wired editor-time — else the axe ships always-visible (no "pick up the
            // axe" read) or, worse, the wiring silently absent (the component-not-serialized failure class,
            // unity-conventions.md). Drop the HeldAxe authoring / its wiring and this goes RED.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present (see BootScene_CarriesHeroAxe_HeldUnderTheRightHandBone)");

            var held = axe.GetComponent<HeldAxe>();
            Assert.IsNotNull(held, "the held axe must carry the HeldAxe component (HasAxe-gated visibility)");
            Assert.IsNotNull(held.inventory,
                "HeldAxe.inventory must be wired editor-time (serialized) — an Awake FindObjectOfType in " +
                "the build is the component-not-serialized trap this guards");
        }

        [Test]
        public void BootScene_HeldAxe_ReadsAtTheSide_NotByTheEar()
        {
            // SOAKFIX2+SOAKFIX4 regression guard (the bug CLASS, both directions): SOAKFIX2's bug was the
            // held axe was a ~0.43u sliver hanging at the hip (max.y 0.71), invisible (the "no axe" soak).
            // SOAKFIX4's bug was the OPPOSITE — the chibi's huge head + short arms put the SOAKFIX2 "chest"
            // pose at EAR height (the handle stuck out by the ear; PoseTrace: max.y 1.040 vs head-bone 0.775).
            // This guard now pins the held axe in the GOLDILOCKS band: big enough to read at gameplay distance
            // AND its top BELOW the head bone (clears the ear) BUT above hip-level (still visible at the hand,
            // not a foot-stick). A regression in EITHER direction reds in CI. (Final visual read is still the
            // SHIPPED build — this is the size/placement floor the eyes verified.)
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");
            var mr = axe.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(mr, "held axe must have a MeshRenderer to measure");

            // Force-show so renderer bounds are valid in the editor (HasAxe-gated → disabled in a static load).
            foreach (var r in axe.GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = true;
            Bounds b = mr.bounds;
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            // ~1.0u target; floor 0.7u catches a regression to the 0.43u sliver. (Upper guard catches the
            // 267×-bone GIANT trap — a >3u axe is the giant that already shipped once.)
            Assert.That(longest, Is.InRange(0.7f, 3.0f),
                $"held axe longest world extent must read at gameplay distance (got {longest:F2}u); " +
                "<0.7 = the invisible-sliver soak regression, >3.0 = the 267x-bone giant trap");

            // The axe top must clear the EAR — BELOW the chibi head bone (~0.775u world) — the SOAKFIX4 fix
            // for "handle sticks out by the ear". But it must still read at the hand (top above hip ~0.45u),
            // not hang at the feet (the SOAKFIX2 no-axe direction). Goldilocks band on the axe TOP.
            Assert.That(b.max.y, Is.InRange(0.45f, 0.78f),
                $"held axe top y {b.max.y:F2} must sit at the SIDE: >0.78 = up by the EAR (the SOAKFIX4 soak), " +
                "<0.45 = hanging at the feet (the SOAKFIX2 no-axe direction). The chibi head bone is ~0.775u.");
        }

        [Test]
        public void BootScene_CarriesStumpAxe_VisibleFromSpawn_InverseGated()
        {
            // SOAKFIX2: the Sponsor's literal "stump is there but no axe" — an axe must be PLANTED in the
            // chopping-block stump and visible FROM SPAWN (the always-on-screen hero axe + the walk-here
            // cue). It is gated as the INVERSE of HasAxe (StumpAxe): shown at spawn, hidden once crafted.
            // Drop the plant or its wiring and this reds in CI rather than re-shipping the empty stump.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var stumpAxe = FindByNameInScene(MovementCameraScene.StumpAxeObjectName);
            Assert.IsNotNull(stumpAxe,
                $"the Boot scene must carry the '{MovementCameraScene.StumpAxeObjectName}' GameObject — the " +
                "axe planted in the stump, visible from spawn (the Sponsor's 'stump is there but no axe')");

            var mf = stumpAxe.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(mf, "the stump axe must carry a MeshFilter (the sourced hatchet mesh)");
            Assert.IsNotNull(mf.sharedMesh, "the stump axe's mesh must be serialized into the scene");
            Assert.Greater(mf.sharedMesh.vertexCount, 20,
                "the stump axe must be the real sourced hatchet mesh, not a placeholder primitive");

            // The INVERSE gate must be wired editor-time (serialized), so the stump axe shows at spawn and
            // hides on craft without an Awake-time scene search in the build.
            var stump = stumpAxe.GetComponent<StumpAxe>();
            Assert.IsNotNull(stump, "the stump axe must carry the StumpAxe component (inverse-HasAxe gate)");
            Assert.IsNotNull(stump.inventory,
                "StumpAxe.inventory must be wired editor-time (serialized) — the component-not-serialized trap");

            // It must be parented under the CraftSpot (rides the stump), not free-floating.
            bool underCraftSpot = false;
            Transform t = stumpAxe.transform;
            while (t != null) { if (t.GetComponent<CraftSpot>() != null) { underCraftSpot = true; break; } t = t.parent; }
            Assert.IsTrue(underCraftSpot, "the stump axe must be parented under the CraftSpot (planted in the stump)");
        }

        [Test]
        public void BootScene_CarriesAxeVerifyCapture_OnTheBootObject()
        {
            // Regression guard (carried from PR #21/#26): the committed -verifyAxe close-up capture path
            // must be SERIALIZED onto the Boot object (sibling of the craft/chop/movement captures), else
            // the held-axe shipped-build evidence path silently vanishes. Drop WireAxeVerifyCapture -> RED.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the verify captures)");
            Assert.IsNotNull(boot.GetComponent<AxeVerifyCapture>(),
                "the Boot object must carry the AxeVerifyCapture component (the committed -verifyAxe " +
                "close-up capture path), serialized into the scene — not Awake-added");
        }

        private static GameObject FindHeroAxe() => FindByNameInScene(MovementCameraScene.HeroAxeObjectName);

        private static GameObject FindByNameInScene(string name)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindByName(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindByName(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindByName(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
