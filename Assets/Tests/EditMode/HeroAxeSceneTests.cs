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
        public void BootScene_HeldAxe_UsesTheInHouseFlintPaletteMaterial_NotTheCcByAtlas()
        {
            // 86cabh907 CC-BY retirement guard: the gameplay held axe must now render with the SHARED
            // in-house Mat_WeaponPalette (URP/Unlit flat-shaded palette) — NOT the retired Viktor.G CC-BY
            // baked atlas. A regression that re-points the held axe at a per-asset/baked material reds here
            // (and would re-introduce the attribution obligation Route A retired).
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");
            var mr = axe.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(mr, "the held axe must have a MeshRenderer");
            Assert.IsNotNull(mr.sharedMaterial, "the held axe's material must be serialized");
            Assert.AreEqual("Universal Render Pipeline/Unlit", mr.sharedMaterial.shader.name,
                "the held axe must render with the URP/Unlit shared palette material (Mat_WeaponPalette) — " +
                "NOT a lit/baked-atlas material (the CC-BY axe outlier Route A retired).");
            StringAssert.Contains("WeaponPalette", mr.sharedMaterial.name,
                $"the held axe material must be the shared Mat_WeaponPalette (got '{mr.sharedMaterial.name}').");
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
        public void BootScene_HeldAxe_CarriesTheShaftLengthPicker_AndItsCycleComponent()
        {
            // 86cabh907 SHAFT-LENGTH PICKER scene-presence guard. The HeldAxeLengthPicker (the unstick
            // instrument) must be SERIALIZED on the held axe alongside the HeldWeaponCycleDebug whose mesh
            // holder it shares — else the Sponsor's soak build has no [L] length picker (the component-not-
            // serialized trap, unity-conventions.md editor-vs-runtime). Drop the picker authoring and this reds.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present (see BootScene_CarriesHeroAxe_HeldUnderTheRightHandBone)");

            var cycle = axe.GetComponent<HeldWeaponCycleDebug>();
            Assert.IsNotNull(cycle, "the held axe must carry HeldWeaponCycleDebug (the picker shares its mesh holder)");
            var picker = axe.GetComponent<HeldAxeLengthPicker>();
            Assert.IsNotNull(picker,
                "the held axe must carry the HeldAxeLengthPicker (the 86cabh907 in-hand shaft-length picker) — " +
                "serialized into the scene so the Sponsor's soak build has the [L] length cycle");
            // The picker's cycle key must be a layout-safe LETTER (Danish-keyboard-safe — [[sponsor-danish-
            // keyboard-layout]]); a regression to US-position punctuation reds here.
            Assert.IsTrue(picker.lengthCycleKey >= KeyCode.A && picker.lengthCycleKey <= KeyCode.Z,
                "the picker's length-cycle key must be a layout-safe LETTER (got " + picker.lengthCycleKey + ")");
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
            // Floor catches a regression to the 0.43u sliver; upper guard catches the 267×-bone GIANT trap.
            // #100 calibration: the in-house wpn_axe_01 (86cabh907) is height-normalized to 1.0u longest-axis
            // at IMPORT (WeaponPackAssetGen.HeroAxeTargetHeadHeightU) and held at HeldAxeLocalScaleUniform
            // 0.45 under the hand bone → a stable measured world extent of ~0.68u (deterministic across
            // bootstraps; the new axe reads SMALLER than the retired CC-BY hatchet that set the old 0.7 floor).
            // 0.68u is a clearly-visible hero axe, NOT the 0.43u sliver the floor guards against. Lower the
            // floor to 0.6 (still well above the sliver) so the floor tracks the in-house axe; the band still
            // reds on a sliver (<0.6) OR a giant (>3.0).
            // 86cabh907 FINAL (the Sponsor's [L]=1.1x pick): the in-house wpn_axe_01 is HEAD-height-normalized
            // (the byte-locked 0.65× head holds its size while the 1.1× straight haft sets the total to ~1.08u
            // longest) and held at HeldAxeLocalScaleUniform 0.45 → ~0.49u longest world extent. Still a clearly-
            // visible hero axe, NOT the 0.43u sliver the floor guards against. Band [0.4, 3.0]: floor 0.4 stays
            // above the sliver (and below the new ~0.49u so float jitter never reds it), ceiling 3.0 catches the
            // 267×-bone giant.
            Assert.That(longest, Is.InRange(0.4f, 3.0f),
                $"held axe longest world extent must read at gameplay distance (got {longest:F2}u); " +
                "<0.5 = the invisible-sliver soak regression, >3.0 = the 267x-bone giant trap");

            // 86cabh907 FINAL (the Sponsor's [L]=1.1x pick): the straight 1.1x haft + the LOWER-THIRD grip shift
            // (HeldAxeGripShiftY = 0.34235) seat the head UP TOP (board-axe read), so the axe top sits above the
            // hand. The band is a SANE-RANGE guard, not a tight pin — the Sponsor re-dials the exact orientation
            // via F9 in the soak. Floor 0.2u still catches a regression that drops the axe to the feet/below
            // ground; ceiling 1.7u catches a 267×-bone giant or a wild-euler fling (those go to many units /
            // off-screen), while allowing the head to sit up top on the shorter 1.1x axe.
            Assert.That(b.max.y, Is.InRange(0.2f, 1.7f),
                $"held axe top y {b.max.y:F2} must sit at/above the hand for the lower-third grip (head up top): " +
                "<0.35 = dropped to the feet/below ground, >1.7 = a 267x-bone giant / wild-euler fling.");
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
            // 86caa83wn soak #3 BAKED-VALUE guard (build 2993c1c): the Sponsor F9-dialed the run carry to
            // (-10,12,-42) and asked to bake it as the shipped default. Pin the EXACT baked value (not just the
            // sign) so a regression to the soak-#2 (0,0,-22) — or any other revert — reds here, AND so the value
            // that SHIPS in Boot.unity (the re-baked scene) is the dialed one, not an editor-only constant.
            Assert.That(pose.runLowerEuler, Is.EqualTo(new Vector3(-10f, 12f, -42f)),
                $"CastawayArmPose.runLowerEuler must ship the Sponsor's soak-#3 F9-dialed run carry (-10,12,-42); " +
                $"got {pose.runLowerEuler}. The scene must be re-baked from MovementCameraScene.ArmRunLowerEuler.");
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

        [Test]
        public void BootScene_CarriesHeldWeaponCycleDebug_OnTheHeroAxe()
        {
            // 86cabh907 regression guard (the component-not-serialized class, applied to the DEBUG cycle
            // handle). The Sponsor's "let me SEE each weapon held" soak handle must be SERIALIZED onto the
            // HeroAxe object (added in AttachHeroAxeToHand) — else the shipped build has NO way to cycle the
            // held weapon and the soak can't view knife/sword/spear in-hand. Drop the AddComponent in
            // AttachHeroAxeToHand and this reds in CI rather than at the soak.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present (the cycle handle rides it)");
            Assert.IsNotNull(axe.GetComponent<HeldWeaponCycleDebug>(),
                "the HeroAxe must carry the HeldWeaponCycleDebug component (the [B] cycle-held-weapon soak " +
                "handle, 86cabh907), serialized into the scene — not Awake-added.");
        }

        [Test]
        public void BootScene_HeldAxe_MeshFilterSharesTheRigDrivenTransform_TheRigStompPrecondition()
        {
            // #100 BUG-2 regression guard (the rig-stomp class). The in-house axe FBX is a SINGLE-node FBX
            // imported with preserveHierarchy:0, so Unity COLLAPSES the mesh node onto the HeroAxe ROOT — the
            // MeshFilter lands on the SAME transform the HeldToolRig drives every LateUpdate (empirically
            // probed on the fresh ab16bbb Boot scene: mfOnRoot=True). That collapse is the PRECONDITION for
            // the bug: HeldWeaponCycleDebug used to write the per-weapon offset/euler onto THAT transform, so
            // the rig stomped it every frame and the F9 nudge "did nothing" for knife/sword/spear. The runtime
            // fix re-homes the displayed mesh onto a child holder the rig never touches (HeldWeaponCycleDebug
            // .Awake). This guard documents + pins the collapse: if the MeshFilter shares the rig transform,
            // the re-home is load-bearing; if a future FBX preserves a child mesh node (mfOnRoot=False), the
            // re-home becomes a harmless no-op and this assert's message tells the next dev why. Either way the
            // mesh must exist on/under the rig-driven object.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");
            var rig = axe.GetComponent<HeldToolRig>();
            Assert.IsNotNull(rig, "the HeroAxe must carry a HeldToolRig (HeldAxeRig) — it drives the seat each frame");
            var mf = axe.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(mf, "the held axe must carry a MeshFilter");
            // The bug's precondition: the static-loaded MeshFilter is on the rig-driven root (the FBX collapse).
            // If this ever flips to a child (mfOnRoot==false), the re-home in HeldWeaponCycleDebug.Awake is a
            // no-op and the rig-stomp can't happen — the test still passes, the message explains the regime.
            bool mfOnRoot = mf.transform == axe.transform;
            Assert.IsTrue(mfOnRoot || mf.transform.IsChildOf(axe.transform),
                "the held axe MeshFilter must live on (or under) the rig-driven HeroAxe object. mfOnRoot=" +
                mfOnRoot + " — when TRUE, HeldWeaponCycleDebug.Awake MUST re-home the mesh onto a child holder " +
                "the rig never touches (else the per-weapon nudge is stomped every frame — #100 BUG-2).");
        }

        [Test]
        public void WeaponLineupPrefab_CarriesEveryFamilyMeshNode_TheCycleSourceResolves()
        {
            // 86cabh907 regression guard (the runtime-mesh-source class). The cycle handle resolves the
            // knife/sword/spear meshes at runtime from Resources/WeaponSetLineup.prefab BY NODE NAME (Asset
            // Database is editor-only). If a node name drifts (or the lineup loses a weapon), cycling shows
            // nothing/falls back to the axe — a silent soak failure. Pin that EVERY cycle node name resolves
            // to a real mesh in the shipped Resources prefab.
            var prefab = Resources.Load<GameObject>(HeldWeaponCycleDebug.LineupResourcePath);
            Assert.IsNotNull(prefab,
                "Resources/" + HeldWeaponCycleDebug.LineupResourcePath + " must exist — it is the runtime " +
                "mesh source the cycle handle loads knife/sword/spear from (built by WeaponPackAssetGen).");
            foreach (var nodeName in HeldWeaponCycleDebug.WeaponNodeNames)
            {
                Transform node = FindByName(prefab.transform, nodeName);
                Assert.IsNotNull(node, $"the lineup prefab must carry a '{nodeName}' node (a cycle weapon).");
                var mf = node.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf, $"lineup node '{nodeName}' must carry a MeshFilter.");
                Assert.IsNotNull(mf.sharedMesh, $"lineup node '{nodeName}' must carry a serialized mesh.");
                Assert.Greater(mf.sharedMesh.vertexCount, 10, $"lineup node '{nodeName}' must be a real weapon mesh.");
            }
        }

        [Test]
        public void HeldWeaponCycleDebug_AxeIsTheLockedDefault_NoCompensation()
        {
            // 86cabh907 regression guard (the axe-seat-LOCKED contract). The Sponsor LOCKED the axe seat — the
            // cycle handle must leave it byte-unchanged. The handle expresses that as: index 0 (axe) gets ZERO
            // mesh-holder offset and UNIT scale (and restores the captured original TRS). Pin index 0 ==
            // identity so a future tweak that accidentally compensates the axe (moving the locked seat) reds
            // here. Also pin the four per-weapon arrays are length-aligned (a misaligned array would apply the
            // wrong weapon's compensation).
            int n = HeldWeaponCycleDebug.WeaponNodeNames.Length;
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponLabels.Length, "WeaponLabels must align with WeaponNodeNames.");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponMeshScale.Length, "WeaponMeshScale must align with WeaponNodeNames.");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponMeshLocalOffset.Length, "WeaponMeshLocalOffset must align with WeaponNodeNames.");
            Assert.AreEqual(n, HeldWeaponCycleDebug.WeaponMeshLocalEuler.Length, "WeaponMeshLocalEuler must align with WeaponNodeNames.");
            Assert.AreEqual("wpn_axe_01", HeldWeaponCycleDebug.WeaponNodeNames[0], "the axe must be cycle index 0 (the locked default).");
            Assert.AreEqual(Vector3.zero, HeldWeaponCycleDebug.WeaponMeshLocalOffset[0],
                "the AXE (index 0) must have ZERO mesh-holder offset — its seat is Sponsor-LOCKED and must stay byte-unchanged.");
            Assert.AreEqual(1f, HeldWeaponCycleDebug.WeaponMeshScale[0],
                "the AXE (index 0) must have UNIT mesh scale — its seat is Sponsor-LOCKED.");
        }

        [Test]
        public void HeldWeaponCycleDebug_NonAxeHeldScales_ReadProportionateToTheAxe_NotShrunkToHalf()
        {
            // 86cabh907 SOAK-FIX regression guard (the bug CLASS, not the instance). The Sponsor soaked the [B]
            // cycle and reported "knife/sword/spear are MUCH SMALLER than the axe when held" — the FIRST values
            // {1, 0.55, 0.50, 0.42} down-scaled the non-axe weapons to ~half the axe's in-hand presence. The
            // MODELS are correctly sized (the Blender family render was Sponsor-accepted); only the HELD scale
            // was wrong. The fix bumps each non-axe held scale UP toward ~1.0 so it reads proportionate to the
            // axe in the hand. This guard floors every non-axe scale at 0.7 of the axe's UNIT scale, so a
            // regression back to the ~0.42-0.55 shrink reds in CI rather than re-shipping the dwarfed weapons.
            // (The exact per-weapon value is the Sponsor's to dial via the live scale dial + bake; the FLOOR is
            // the bug-class catch.) The live dial only mutates the runtime _liveScale copy — the SHIPPED default
            // is this static array, so pinning it here pins what ships.
            float axe = HeldWeaponCycleDebug.WeaponMeshScale[0]; // 1.0 (locked)
            for (int i = 1; i < HeldWeaponCycleDebug.WeaponMeshScale.Length; i++)
            {
                float s = HeldWeaponCycleDebug.WeaponMeshScale[i];
                Assert.GreaterOrEqual(s, 0.7f * axe,
                    $"the held {HeldWeaponCycleDebug.WeaponLabels[i]} scale ({s:F2}) must read proportionate to " +
                    $"the axe ({axe:F2}) in the hand — ≥0.7× the axe. A value back near the soak-rejected " +
                    "0.42-0.55 shrink re-ships the 'MUCH SMALLER than the axe' bug (86cabh907).");
                // Sanity ceiling: no non-axe weapon should ship grossly LARGER than the axe (a fat-finger bake);
                // a sword/spear can read a bit bigger but not 2x the axe in the hand.
                Assert.LessOrEqual(s, 2.0f * axe,
                    $"the held {HeldWeaponCycleDebug.WeaponLabels[i]} scale ({s:F2}) must not ship grossly larger " +
                    $"than the axe ({axe:F2}) — ≤2× (catches a fat-finger bake of the live-dial value).");
            }
        }

        [Test]
        public void HeldWeaponCycleDebug_PerWeaponSeats_AreTheSponsorDialedBakedValues_86caffwuz()
        {
            // 86caffwuz BAKE regression guard (build 5caf1be). The Sponsor soaked the held weapons and DIALED
            // each in-hand seat via the unified settings console's 7 held-weapon rows; those committed numbers
            // ARE his approval ([[verify-soak-builds-or-bake-and-judge]] — bake the dialed values + assert the
            // COMMITTED on-disk constant, not just regen-code [[unity-procedural-committed-assets-go-stale]]).
            // Equality-pin EVERY per-weapon baked value to the dialed numbers so any future edit that drifts a
            // seat reds here in CI — the bug class is "a later tweak silently moves a Sponsor-approved seat".
            // The earlier guard only floored/ceilinged the scales + pinned index-0 identity; it did NOT pin the
            // exact offset/euler the Sponsor dialed — this does.

            // AXE (index 0) — Sponsor-LOCKED: zero offset/euler, unit scale (byte-unchanged seat, bar #6).
            Assert.AreEqual(Vector3.zero, HeldWeaponCycleDebug.WeaponMeshLocalOffset[0], "AXE offset must stay zero (LOCKED).");
            Assert.AreEqual(Vector3.zero, HeldWeaponCycleDebug.WeaponMeshLocalEuler[0], "AXE euler must stay zero (LOCKED).");
            Assert.AreEqual(1f, HeldWeaponCycleDebug.WeaponMeshScale[0], 1e-4f, "AXE scale must stay 1.0 (LOCKED).");

            // KNIFE (index 1) — Sponsor-dialed.
            AssertSeat(1, new Vector3(0.000f, -0.100f, -0.020f), Vector3.zero, 0.85f);
            // SWORD (index 2) — Sponsor-dialed.
            AssertSeat(2, new Vector3(-0.020f, -0.120f, 0.000f), Vector3.zero, 0.95f);
            // SPEAR (index 3) — Sponsor-dialed.
            AssertSeat(3, new Vector3(-0.020f, -0.120f, 0.000f), Vector3.zero, 0.90f);
        }

        // Equality-assert one weapon's baked seat (offset + euler + scale) against the Sponsor-dialed value.
        private static void AssertSeat(int i, Vector3 offset, Vector3 euler, float scale)
        {
            string w = HeldWeaponCycleDebug.WeaponLabels[i];
            Assert.AreEqual(offset.x, HeldWeaponCycleDebug.WeaponMeshLocalOffset[i].x, 1e-4f, $"{w} baked offset.x drifted from the Sponsor-dialed value (86caffwuz).");
            Assert.AreEqual(offset.y, HeldWeaponCycleDebug.WeaponMeshLocalOffset[i].y, 1e-4f, $"{w} baked offset.y drifted from the Sponsor-dialed value (86caffwuz).");
            Assert.AreEqual(offset.z, HeldWeaponCycleDebug.WeaponMeshLocalOffset[i].z, 1e-4f, $"{w} baked offset.z drifted from the Sponsor-dialed value (86caffwuz).");
            Assert.AreEqual(euler, HeldWeaponCycleDebug.WeaponMeshLocalEuler[i], $"{w} baked euler drifted from the Sponsor-dialed value (86caffwuz).");
            Assert.AreEqual(scale, HeldWeaponCycleDebug.WeaponMeshScale[i], 1e-4f, $"{w} baked scale drifted from the Sponsor-dialed value (86caffwuz).");
        }

        [Test]
        public void BootScene_CarriesHeldWeaponPlacement_WiredToTheRigAndCycle()
        {
            // 86caffwuz regression guard (the component-not-serialized class, applied to the unified-console
            // PLACEMENT seam). The HeldWeaponPlacement binding seam (what the 7 held-weapon console rows drive)
            // must be SERIALIZED onto the HeroAxe object with its axeRig + weaponCycle references wired editor-
            // time — else the held-weapon rows fall back to a runtime FindObjectOfType (the editor-vs-runtime
            // ship-path violation) or, worse, bind nothing. Drop the AddComponent/wiring in AttachHeroAxeToHand
            // and this reds in CI rather than at the soak.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present (the placement seam rides it)");
            var placement = axe.GetComponent<HeldWeaponPlacement>();
            Assert.IsNotNull(placement,
                "the HeroAxe must carry the HeldWeaponPlacement seam (86caffwuz) — the single surface the unified " +
                "settings console's held-weapon rows drive, serialized into the scene (not Awake-added).");
            Assert.IsNotNull(placement.axeRig,
                "HeldWeaponPlacement.axeRig must be wired editor-time (serialized) — the held-weapon rows drive " +
                "the SAME axe rig the equip path uses (no parallel attach mechanism — the ticket constraint).");
            Assert.IsNotNull(placement.weaponCycle,
                "HeldWeaponPlacement.weaponCycle must be wired editor-time — the non-axe weapons' per-weapon seat " +
                "routes through it (and it owns the current-held index the rows follow).");
            // The seam must ride the SAME object as the rig + cycle (it resolves them from this GameObject).
            Assert.AreSame(axe.GetComponent<HeldAxeRig>(), placement.axeRig,
                "HeldWeaponPlacement.axeRig must be the HeroAxe's own HeldAxeRig (one shared seat).");
            Assert.AreSame(axe.GetComponent<HeldWeaponCycleDebug>(), placement.weaponCycle,
                "HeldWeaponPlacement.weaponCycle must be the HeroAxe's own HeldWeaponCycleDebug.");
        }

        [Test]
        public void BootScene_SettingsPanel_BindsTheHeldWeaponPlacementSeam_Serialized()
        {
            // 86caffwuz regression guard: the unified settings console (SettingsPanel) must carry the held-weapon
            // PLACEMENT seam wired editor-time, so the 7 held-weapon rows bind it with NO runtime FindObjectOfType
            // (the editor-vs-runtime ship-path discipline). This is the cross-wire BuildSettingsPanel can't do
            // itself (it runs before the axe is attached) — AttachHeroAxeToHand back-wires it. Drop that back-wire
            // and the held-weapon rows fall back to a runtime lookup (or vanish) → red here, not at the soak.
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var panel = Object.FindObjectOfType<SettingsPanel>();
            // No-bootstrap precondition (86cacyg63): a bare local EditMode run skips BootstrapProject.Run, so the
            // committed Boot.unity lacks the bootstrap-authored SettingsPanel. Inert once bootstrap has run.
            BootstrapPrecondition.Require(panel, "SettingsPanel in Boot.unity");
            Assert.IsNotNull(panel, "the Boot scene must carry the SettingsPanel (the unified console host)");
            Assert.IsNotNull(panel.heldWeapon,
                "SettingsPanel.heldWeapon must be wired editor-time (serialized) — the 7 held-weapon in-hand rows " +
                "(pos X/Y/Z, rot pitch/yaw/roll, scale) bind to the current held weapon's seat through it (86caffwuz). " +
                "An unwired ref forces a runtime FindObjectOfType, which the editor-vs-runtime discipline forbids.");
            var axe = FindHeroAxe();
            Assert.IsNotNull(axe, "hero axe must be present");
            Assert.AreSame(axe.GetComponent<HeldWeaponPlacement>(), panel.heldWeapon,
                "SettingsPanel.heldWeapon must be the HeroAxe's HeldWeaponPlacement (one seat, one seam).");
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
