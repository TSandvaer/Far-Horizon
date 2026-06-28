using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC1 (ticket 86cafdevx — the highest-leverage capture-gate hardening): assert not just that each
    /// capture-gate-critical *VerifyCapture component is SERIALIZED into Boot.unity (CaptureGateSceneTests /
    /// MovementCameraSceneTests already cover bare presence), but that its OWN editor-wired SERIALIZED DEPS
    /// are non-null AND bound to the RIGHT scene instance (binding-identity) — exactly the
    /// BushSceneTests.BootScene_ScatterBerryBushes_AreWiredHarvestable pattern, extended to the verify-capture
    /// harnesses that the 20-min capture GATES ride on.
    ///
    /// WHY THIS IS THE LEVER (from the 2026-06-28 #162 /investigate): the Craft/Chop/Campfire verify-capture
    /// harnesses each carry editor-wired serialized deps (cap.player / cap.inventory / cap.chop / cap.warmth /
    /// cap.campfire — MovementCameraScene.WireCraftVerifyCapture / WireChopVerifyCapture /
    /// WireCampfireVerifyCapture). They ALSO carry an Awake `FindObjectByType` FALLBACK that silently
    /// re-resolves a dropped ref at runtime — so a wiring drop does NOT fail bare presence and does NOT fail
    /// the runtime self-find; it slips PAST EditMode (CI step 2) and only surfaces — opaquely — in the slow
    /// (~20-min) capture gate (CI step 7) if the runtime self-find ALSO mis-resolves (the #162 flake class: a
    /// build-unstable pick lands on the wrong instance). Asserting the SERIALIZED ref here makes a dropped
    /// wiring fail RED in EditMode, where it is fast + legible, never in the capture gate.
    ///
    /// Binary scenes can't be GUID-grepped, so the EditMode scene-presence assert is the only authoritative
    /// reader (unity-conventions.md §Component-in-source-but-not-serialized-into-scene). Regression guard:
    /// drop a `cap.&lt;dep&gt; = ...` line in MovementCameraScene's Wire*VerifyCapture and the matching assert
    /// here goes RED — the dropped wiring is caught at CI step 2, not in the 20-min capture gate.
    /// </summary>
    public class CaptureGateDepsSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        private static Scene OpenBoot()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");
            return scene;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        // === CraftVerifyCapture (-verifyCraft): player (ClickToMove) + inventory ===
        [Test]
        public void BootScene_CraftVerifyCapture_HasWiredPlayerAndInventory_BoundToSceneInstances()
        {
            var scene = OpenBoot();
            var cap = FindInScene<CraftVerifyCapture>(scene);
            Assert.IsNotNull(cap,
                "the Boot scene must carry CraftVerifyCapture serialized (the -verifyCraft shipped-build gate; " +
                "component-not-serialized trap)");

            Assert.IsNotNull(cap.player,
                "CraftVerifyCapture.player must be wired editor-time (the ClickToMove the harness drives to the " +
                "craft spot) — an Awake FindObjectByType fallback exists but is the non-ship path; a dropped " +
                "wiring must fail HERE (CI step 2), not silently re-resolve then mis-resolve in the 20-min gate");
            Assert.IsNotNull(cap.inventory,
                "CraftVerifyCapture.inventory must be wired editor-time (the harness reads HasAxe to gate PASS)");

            var ctm = FindInScene<ClickToMove>(scene);
            var inv = FindInScene<Inventory>(scene);
            Assert.AreSame(ctm, cap.player,
                "CraftVerifyCapture.player must be THE scene's ClickToMove (binding-identity: a stale/duplicate " +
                "ref drives the wrong agent — the #162-class wrong-instance failure the gate exists to prevent)");
            Assert.AreSame(inv, cap.inventory,
                "CraftVerifyCapture.inventory must be THE scene's Inventory (binding-identity)");
        }

        // === ChopVerifyCapture (-verifyChop): player (ClickToMove) + inventory + chop (ChopTree) ===
        [Test]
        public void BootScene_ChopVerifyCapture_HasWiredPlayerInventoryChop_BoundToSceneInstances()
        {
            var scene = OpenBoot();
            var cap = FindInScene<ChopVerifyCapture>(scene);
            Assert.IsNotNull(cap,
                "the Boot scene must carry ChopVerifyCapture serialized (the -verifyChop shipped-build gate; " +
                "component-not-serialized trap)");

            Assert.IsNotNull(cap.player,
                "ChopVerifyCapture.player must be wired editor-time (the ClickToMove the harness drives to the " +
                "tree) — a dropped wiring must fail HERE, not in the 20-min capture gate");
            Assert.IsNotNull(cap.inventory,
                "ChopVerifyCapture.inventory must be wired editor-time (the harness reads WoodCount to gate PASS)");
            Assert.IsNotNull(cap.chop,
                "ChopVerifyCapture.chop must be wired editor-time (the ChopTree the harness RequestChopClick's — " +
                "the left-click-driven chop seam; 86caa4c5c CHANGE 1). A dropped wiring leaves the harness " +
                "self-finding at runtime, the masking the editor-vs-runtime trap exists to surface here");

            var ctm = FindInScene<ClickToMove>(scene);
            var inv = FindInScene<Inventory>(scene);
            var tree = FindInScene<ChopTree>(scene);
            Assert.AreSame(ctm, cap.player, "ChopVerifyCapture.player must be THE scene's ClickToMove (binding-identity)");
            Assert.AreSame(inv, cap.inventory, "ChopVerifyCapture.inventory must be THE scene's Inventory (binding-identity)");
            Assert.AreSame(tree, cap.chop,
                "ChopVerifyCapture.chop must be THE scene's ChopTree (binding-identity: the harness RequestChopClick's " +
                "exactly this tree — a wrong-instance ref drives a chop the gate camera never frames)");
        }

        // === CampfireVerifyCapture (-verifyLoop): player + inventory + warmth + campfire ===
        [Test]
        public void BootScene_CampfireVerifyCapture_HasWiredLoopDeps_BoundToSceneInstances()
        {
            var scene = OpenBoot();
            var cap = FindInScene<CampfireVerifyCapture>(scene);
            Assert.IsNotNull(cap,
                "the Boot scene must carry CampfireVerifyCapture serialized (the -verifyLoop full-cycle gate; " +
                "component-not-serialized trap)");

            Assert.IsNotNull(cap.player,
                "CampfireVerifyCapture.player must be wired editor-time (the ClickToMove the loop drives spawn → " +
                "craft → tree → fire)");
            Assert.IsNotNull(cap.inventory,
                "CampfireVerifyCapture.inventory must be wired editor-time (the loop reads HasAxe/WoodCount)");
            Assert.IsNotNull(cap.warmth,
                "CampfireVerifyCapture.warmth must be wired editor-time (the loop asserts warmth RESTORES at the " +
                "lit fire — the whole 'loop closes' proof rides on this ref)");
            Assert.IsNotNull(cap.campfire,
                "CampfireVerifyCapture.campfire must be wired editor-time (the lit fire the loop stands at)");

            var ctm = FindInScene<ClickToMove>(scene);
            var inv = FindInScene<Inventory>(scene);
            var warmth = FindInScene<WarmthNeed>(scene);
            var fire = FindInScene<Campfire>(scene);
            Assert.AreSame(ctm, cap.player, "CampfireVerifyCapture.player must be THE scene's ClickToMove (binding-identity)");
            Assert.AreSame(inv, cap.inventory, "CampfireVerifyCapture.inventory must be THE scene's Inventory (binding-identity)");
            Assert.AreSame(warmth, cap.warmth, "CampfireVerifyCapture.warmth must be THE scene's WarmthNeed (binding-identity)");
            Assert.AreSame(fire, cap.campfire, "CampfireVerifyCapture.campfire must be THE scene's Campfire (binding-identity)");
        }

        // === PickableLooter (the #162-saga component): inventory + player, bound to the scene instances ===
        // PickableLooterSceneTests already asserts these are non-null; this adds the BINDING-IDENTITY half (the
        // ref points at the RIGHT scene instance) per AC1 "binding-identity where it wires to a named instance",
        // because the #162 failure mode was precisely a WRONG-instance resolve (a scatter bush, not the wired one).
        [Test]
        public void BootScene_PickableLooter_RefsBoundToSceneInstances()
        {
            var scene = OpenBoot();
            var looter = FindInScene<PickableLooter>(scene);
            Assert.IsNotNull(looter, "the Boot scene must carry the wired PickableLooter");

            var inv = FindInScene<Inventory>(scene);
            Assert.IsNotNull(looter.inventory, "PickableLooter.inventory must be wired editor-time");
            Assert.AreSame(inv, looter.inventory,
                "PickableLooter.inventory must be THE scene's Inventory (binding-identity)");

            // The player ref must be the moving agent root the looter measures range from — assert it carries the
            // NavMeshAgent (the resolve origin the #162 teleport seam Warps), not some unrelated transform.
            Assert.IsNotNull(looter.player, "PickableLooter.player must be wired editor-time");
            Assert.IsNotNull(looter.player.GetComponentInParent<UnityEngine.AI.NavMeshAgent>(),
                "PickableLooter.player must resolve to the moving agent root (carries a NavMeshAgent) — the " +
                "position NearestInRange measures from; the #162 teleport seam Warps exactly this agent");
        }

        // === LootPrompt (rides the looter's single source of truth) ===
        [Test]
        public void BootScene_LootPrompt_WiredToTheSameLooter()
        {
            var scene = OpenBoot();
            var prompt = FindInScene<LootPrompt>(scene);
            var looter = FindInScene<PickableLooter>(scene);
            Assert.IsNotNull(prompt, "the Boot scene must carry the wired LootPrompt");
            Assert.IsNotNull(looter, "the Boot scene must carry the wired PickableLooter");
            Assert.IsNotNull(prompt.looter, "LootPrompt.looter must be wired editor-time (its single source of truth)");
            Assert.AreSame(looter, prompt.looter,
                "LootPrompt.looter must be THE scene's PickableLooter (binding-identity) so the prompt + the actual " +
                "loot agree on NearestInRange — a different-instance ref shows a prompt that doesn't match the loot");
        }
    }
}
