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
        public void BootScene_HeldAxe_PosedHandLocal_SoRotationTracksTheBone()
        {
            // SOAKFIX8 regression guard (the bug CLASS at the serialization layer). The Sponsor's bug ("the
            // axe points the same way on the x axis all the time") was the held axe posed via a WORLD rotation
            // after parenting — which pinned it to a fixed world heading that couldn't survive a turn. The FIX
            // is a HAND-LOCAL pose: the axe is a CHILD of the right-hand bone with a stable localRotation, so
            // BOTH position and rotation ride the bone via the hierarchy in every facing.
            //
            // This pins the serialized contract: the axe is a direct child of the right-hand bone (so its
            // localRotation IS its hand-relative rotation), and that localRotation is non-trivial (a real
            // grip, not identity). The PlayMode test (HeldAxeRotationTracksHandPlayModeTests) proves the
            // INVARIANCE across facings; this EditMode guard proves the scene actually SHIPS the hand-local
            // parenting that makes that invariance hold (a regression that re-introduced a world-set rotation
            // would still serialize SOME local transform, so the load-bearing pin is the direct-child parenting
            // under the bone — asserted by BootScene_CarriesHeroAxe — plus a non-identity grip here).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");

            // The axe's parent must BE the right-hand bone (a DIRECT child), so localRotation == the
            // hand-relative rotation the axe rides. (BootScene_CarriesHeroAxe pins "under the bone" loosely;
            // this pins the DIRECT-child relationship the hand-local pose depends on.)
            Transform parent = axe.transform.parent;
            Assert.IsNotNull(parent, "the held axe must be parented (under the right-hand bone)");
            string pn = parent.name.ToLowerInvariant();
            Assert.IsTrue(pn.Contains(MovementCameraScene.RightHandBoneToken) && !pn.Contains("dummy") && !pn.Contains("end"),
                $"the held axe's DIRECT parent must be the right-hand bone (got '{parent.name}') so its " +
                "localRotation is the hand-relative grip — a free-standing or deeper-nested re-parent breaks the track.");

            // The grip must be a real non-identity hand-local rotation (the dialed pose), not left at identity
            // (which would read as the axe lying along the bone's local axes — not a held hatchet).
            float fromIdentity = Quaternion.Angle(axe.transform.localRotation, Quaternion.identity);
            Assert.Greater(fromIdentity, 5f,
                $"the held axe's hand-local rotation must be a real grip (got {fromIdentity:F1}° from identity) — " +
                "a near-identity localRotation means the dialed pose was lost.");
        }

        [Test]
        public void BootScene_HeldAxe_ReadsAtTheSide_NotByTheEar()
        {
            // SOAKFIX2/4/7/8 regression guard (the bug CLASS, both directions). SOAKFIX2's bug was the held axe
            // as a ~0.43u sliver hanging at the hip (max.y 0.71), invisible (the "no axe" soak). The placement
            // band has MOVED with each Sponsor-confirmed dial: SOAKFIX7's seat measured ~1.022u; SOAKFIX8 (the
            // ROTATION-TRACKS-HAND fix) bakes the Sponsor's LATEST dialed grip, which seats the axe LOWER —
            // held down at the side, axe top max.y ≈ 0.53u (PoseTrace; the grip the Sponsor dialed via the F9
            // tool). The band is re-centred on THAT Sponsor-dialed default. It still guards both failure
            // directions: the SIZE floor (longest-extent ≥0.7u below) catches the invisible-sliver regression,
            // and this top-y band catches a foot-stick (too low) / a 267×-bone giant or wild-euler fling (too
            // high). A regression in EITHER direction reds in CI. (Final visual read is still the SHIPPED build.)
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

            // The axe top must sit at the HAND, around the SOAKFIX8 Sponsor-dialed seat (max.y ≈ 0.53u — held
            // down at the side). Floor 0.35u catches a regression that drops the axe to the feet/below ground;
            // ceiling 0.95u catches a 267×-bone giant or a wild euler that flings the axe up to the ear. The
            // band is centred on the dialed-in default the Sponsor judged — re-bake the default and it moves.
            Assert.That(b.max.y, Is.InRange(0.35f, 0.95f),
                $"held axe top y {b.max.y:F2} must sit at the hand near the SOAKFIX8 dialed-in seat (~0.53u): " +
                "<0.35 = dropped to the feet/below ground, >0.95 = a 267x-bone giant / wild-euler fling to the ear.");
        }

        [Test]
        public void BootScene_HeldAxeRig_RidesTheRawHandBone_FollowsTheArmSwing_NoStabilizer()
        {
            // 86ca9zcjn regression guard (the FOLLOW-THE-ARM design choice, at the serialization layer). The
            // Sponsor (soak 6bcc1bc) chose for the held axe to FOLLOW the right arm's natural swing during
            // locomotion — it rides the RAW hand bone (the prior swing-stabilizer / grip-anchor + the
            // vertical-decouple are REMOVED). This pins that the SHIPPED scene serializes the follow wiring:
            //   (a) the rig's hand IS the right-hand bone (so it follows the arm, and the facing yaw the raw
            //       hand carries passes through — the 86ca9xz00 facing-follow contract, now via the raw hand),
            //   (b) followDamp is 0 (the per-step swing is VISIBLE by default — the Sponsor's choice; AC2 says
            //       a LIGHT damp is OK to de-jitter but must NOT re-lock the swing).
            // A regression that re-introduced a stabilizer (followDamp cranked to re-lock) or detached the axe
            // from the hand reds in CI rather than re-shipping a locked axe. (The PlayMode HeldAxeWalkBounce /
            // HeldAxeSwingStabilize tests prove the BEHAVIOUR; this proves the scene carries the wiring.)
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");

            var rig = axe.GetComponent<HeldAxeRig>();
            Assert.IsNotNull(rig, "the held axe must carry the HeldAxeRig driver");
            Assert.IsNotNull(rig.hand,
                "HeldAxeRig.hand must be wired editor-time (serialized) to the right-hand bone — the axe rides " +
                "the RAW hand so it follows the arm's natural swing AND the facing yaw the hand carries (86ca9zcjn).");

            var castaway = Object.FindObjectOfType<CastawayCharacter>();
            Assert.IsNotNull(castaway, "the Boot scene must carry the CastawayCharacter");

            // The hand the rig rides must live UNDER the castaway model (it's the rig's right-hand bone, an
            // animated bone whose per-step swing the axe now follows) — not some detached/world transform.
            Assert.IsTrue(rig.hand.IsChildOf(castaway.transform),
                $"HeldAxeRig.hand ('{rig.hand.name}') must be a bone under the CastawayCharacter — the axe rides " +
                "the animated hand so it swings with the arm (86ca9zcjn follow-the-arm).");

            // followDamp must be 0 (the per-step arm-swing is fully visible — the Sponsor's design choice). A
            // value large enough to re-lock the swing would re-ship the detached-mid-stride read the Sponsor
            // rejected; AC2: "if it reads wild, damp it, don't lock it" — so a small de-jitter is allowed but
            // the shipped default is the raw follow.
            Assert.That(rig.followDamp, Is.EqualTo(0f),
                $"HeldAxeRig.followDamp must ship at 0 (raw-hand follow → the per-step arm-swing is visible — the " +
                $"Sponsor's choice, 86ca9zcjn AC2), got {rig.followDamp}. A non-zero shipped damp re-locks the " +
                "swing the Sponsor explicitly chose to keep.");
        }

        [Test]
        public void BootScene_RunSwingReduction_WiredOnArmPose_CharacterAndRunLowerSerialized()
        {
            // 86caa83wn soak #2 regression guard (the component-not-serialized trap, applied to the RUN-swing
            // reduction). The Sponsor's chosen fix REVERSED the earlier axe-side world-Y ceiling clamp (which
            // DETACHED the axe from the hand during the run arm-swing — "when i run the axe is no longer in the
            // hand"): the axe now rides the hand RIGIDLY, and the run into-head is fixed by LOWERING the RIGHT
            // ARM while running (CastawayArmPose.runLowerEuler), weighted by the character's IsRunning. The
            // character ref + a non-zero run-lower MUST be wired editor-time (serialized into Boot.unity) — a
            // runtime Awake fallback resolves the character, but if the SHIP path doesn't carry them the run-lower
            // silently goes INERT and the axe rides into the head at run again. Drop the wiring in AddArmPose →
            // RED here, not at the soak.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            var castaway = Object.FindObjectOfType<CastawayCharacter>();
            Assert.IsNotNull(castaway, "the Boot scene must carry the CastawayCharacter");
            var pose = castaway.GetComponent<CastawayArmPose>();
            Assert.IsNotNull(pose, "the castaway must carry the CastawayArmPose driver (the run-swing reduction lives here)");

            Assert.IsNotNull(pose.rightUpperArm,
                "CastawayArmPose.rightUpperArm must be wired (serialized) — the run-lower is applied to it.");
            Assert.AreEqual(castaway, pose.character,
                "CastawayArmPose.character must be the scene's CastawayCharacter — its IsRunning weights the " +
                "run-lower. An unresolved character makes the run-swing reduction INERT (axe rides into the head).");
            Assert.AreNotEqual(Vector3.zero, pose.runLowerEuler,
                "CastawayArmPose.runLowerEuler must ship NON-ZERO — it is the RUN-swing reduction (86caa83wn). " +
                "A zero run-lower re-opens 'the axe swings into the head when running'.");
            // The run-lower lowers the arm on the rig's raise axis (LOCAL-Z; a negative Z lowers). Pin the
            // direction so a future tweak that flips it (raising the arm = INTO the head) is caught here.
            Assert.Less(pose.runLowerEuler.z, 0f,
                $"CastawayArmPose.runLowerEuler.z must be NEGATIVE to LOWER the right arm while running (got " +
                $"{pose.runLowerEuler.z}); a positive Z would RAISE the arm into the head — the bug this fixes.");
        }

        [Test]
        public void BootScene_HeldAxeRig_NoVerticalClampField_RidesHandRigidly()
        {
            // 86caa83wn soak #2 — the axe-side world-Y clamp that detached the axe from the hand is GONE. Assert
            // the held axe rides the hand RIGIDLY (no clamp state on the rig). This guards the revert: the axe
            // must NOT have a separate vertical clamp that could pull it off the grip during the run arm-swing.
            // Reflection so the test compiles even as the field set evolves — it asserts the clamp API is absent.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");
            var rig = axe.GetComponent<HeldAxeRig>();
            Assert.IsNotNull(rig, "the held axe must carry the HeldAxeRig driver");

            var t = typeof(HeldAxeRig);
            Assert.IsNull(t.GetField("clampVigorousLocomotion"),
                "HeldAxeRig must NOT carry a vertical-clamp field (86caa83wn soak #2 revert) — the axe-side clamp " +
                "DETACHED the axe from the hand during the run arm-swing. The axe must ride the hand rigidly; the " +
                "run into-head is fixed on the arm side (CastawayArmPose.runLowerEuler).");
            Assert.IsNull(t.GetField("clampCeilingAboveShoulder"),
                "HeldAxeRig.clampCeilingAboveShoulder must be removed (the detaching world-Y clamp is reverted).");
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

        [Test]
        public void BootScene_CarriesAxeNudgeTool_OnTheBootObject()
        {
            // SOAKFIX5 regression guard (the axe-nudge reframe): the BUILD-GATED debug AxeNudgeTool must be
            // SERIALIZED onto the Boot object (sibling of the verify captures), else the Sponsor's in-game
            // axe-dialing path silently vanishes from the shipped build (the component-not-serialized class,
            // unity-conventions.md). The tool is INERT in normal play (asleep behind the F9 toggle — see the
            // PlayMode inertness test), so its presence does NOT affect a soak. Drop WireAxeNudgeTool -> RED.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var boot = GameObject.Find("Boot");
            Assert.IsNotNull(boot, "the Boot scene must carry the 'Boot' object (host of the debug nudge tool)");
            Assert.IsNotNull(boot.GetComponent<AxeNudgeTool>(),
                "the Boot object must carry the AxeNudgeTool component (the build-gated in-game axe-nudge " +
                "tool), serialized into the scene — not Awake-added");
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
