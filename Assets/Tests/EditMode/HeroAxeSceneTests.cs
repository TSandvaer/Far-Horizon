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

        private static GameObject FindHeroAxe()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindByName(root.transform, MovementCameraScene.HeroAxeObjectName);
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
