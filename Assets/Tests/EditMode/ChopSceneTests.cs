using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// EditMode guard that the U2-3 chop interaction (ticket 86ca8bdd8) is SERIALIZED into the Boot
    /// scene the exe ships — not added at Awake (the editor-vs-runtime serialization trap,
    /// unity-conventions.md, would mangle/drop an Awake-built component or visual). Sibling of
    /// CraftSceneTests; same regression-guard intent: drop MovementCameraScene.BuildChopTree (or its
    /// wiring) and this goes RED in headless CI, rather than the shipped build silently lacking the
    /// "do work in the world" beat.
    /// </summary>
    public class ChopSceneTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        [Test]
        public void BootScene_CarriesChopTree_WiredToInventoryAndPlayer()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(tree,
                "the Boot scene must carry the ChopTree — the loop's 'do work in the world' beat the " +
                "castaway click-moves to (unity-conventions.md editor-vs-runtime trap: serialized, not " +
                "Awake-built)");
            Assert.IsNotNull(tree.inventory,
                "ChopTree's Inventory reference must be wired editor-time so reaching the tree (with an " +
                "axe) writes the chopped wood without an Awake-time scene search in the build");
            Assert.IsNotNull(tree.player,
                "ChopTree's player reference (the moving agent root) must be wired editor-time so the " +
                "proximity check has a target without an Awake-time scene search in the build");
            Assert.IsNotNull(tree.visual,
                "ChopTree's visual reference must be wired editor-time so the felling tween animates the " +
                "serialized tree mesh (the thin-but-felt feedback)");
            Assert.IsNotNull(tree.character,
                "ChopTree's character reference must be wired editor-time (86caa4c5c change-(b)) so each landed " +
                "chop plays the Mixamo melee swing (CastawayCharacter.TriggerChop) in the shipped build — an " +
                "Awake-only FindObjectOfType fallback exists but is the non-ship path (editor-vs-runtime " +
                "serialization trap, unity-conventions.md)");
            Assert.IsNotNull(tree.inventoryUI,
                "ChopTree's inventoryUI reference must be wired editor-time (86caa4c5c CHANGE 1) so a left-click " +
                "OVER the belt/inventory UI does NOT chop the tree behind it in the shipped build — wired in " +
                "BuildInventoryUI (which runs after BuildChopTree); an Awake FindObjectOfType is the build-safety " +
                "fallback (editor-vs-runtime serialization trap, unity-conventions.md)");
        }

        [Test]
        public void BootScene_ChopTree_WiredToCastaway_ForTheMeleeSwing()
        {
            // change-(b) — the chop swing is now the Mixamo melee Animator Attack state (CastawayCharacter
            // .TriggerChop), NOT a procedural ChopPoseDriver. The ChopTree must hold a SERIALIZED ref to the
            // castaway so each chop plays the swing in the shipped build (no Awake scene-search on the ship path).
            // The castaway that ref points at must itself carry the Animator controller that has the Attack state.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(tree, "the Boot scene must carry the ChopTree");
            Assert.IsNotNull(tree.character,
                "ChopTree.character must be wired editor-time (change-(b)) so each chop plays the melee swing " +
                "via CastawayCharacter.TriggerChop in the shipped build (editor-vs-runtime serialization trap)");
            Assert.IsNotNull(tree.character.animatorController,
                "the wired castaway must carry the Animator controller (with the chop Attack state) so the " +
                "Chop trigger has a state to fire — a null controller would make TriggerChop a silent no-op");
        }

        [Test]
        public void BootScene_ChopTree_ShipsStandingAndUnchopped()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(tree, "the Boot scene must carry the ChopTree");

            Assert.IsFalse(tree.IsFelled, "the tree ships STANDING — the player fells it by chopping");
            Assert.AreEqual(0, tree.Chops, "no chops landed before play");
            Assert.Greater(tree.chopRadius, 0f, "chopRadius must be positive — a zero radius never chops");
            Assert.Greater(tree.chopsToFell, 0, "chopsToFell must be positive — else the tree fells on chop 0");
            Assert.Greater(tree.woodPerChop, 0, "woodPerChop must be positive — a chop must yield wood");
        }

        [Test]
        public void BootScene_ChopTree_HasBlobCanopy_MatchesWorldTreeLanguage()
        {
            // Board v2 (ticket 86ca8ce7j): the choppable tree must read in the SAME blob-canopy language
            // as the scatter trees (a lone single-dome choppable tree next to clustered ones breaks
            // art-direction fidelity). Its canopy is a multi-blob cluster carrying per-vertex greens.
            // ChopTree BEHAVIOR is untouched — this guards only the visual mesh shape.
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            ChopTree tree = FindInScene<ChopTree>(scene);
            Assert.IsNotNull(tree, "the Boot scene must carry the ChopTree");

            var canopy = tree.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(mf => mf.gameObject.name == "Canopy");
            Assert.IsNotNull(canopy, "the chop tree must carry a Canopy mesh child");
            Assert.IsNotNull(canopy.sharedMesh, "the chop tree canopy mesh must be assigned");
            Assert.Greater(canopy.sharedMesh.vertexCount, 40,
                "the chop tree canopy must be a multi-blob CLUSTER (>40 verts), not a single-dome sphere " +
                "— it must match the world's blob-canopy trees (board v2)");
            Assert.Greater(canopy.sharedMesh.colors.Length, 0,
                "the chop tree canopy must carry per-vertex green COLORS (the multi-value blob clustering)");
        }

        [Test]
        public void BootScene_ShipsInventoryEmpty_NoWoodSeededByChopAuthoring()
        {
            // The chop authoring must NOT pre-seed wood (the ledger ships empty; wood comes from chopping).
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Inventory inv = FindInScene<Inventory>(scene);
            Assert.IsNotNull(inv, "the Boot scene must carry the Inventory");
            Assert.AreEqual(0, inv.WoodCount, "no wood seeded — wood is yielded only by chopping (U2-3)");
            Assert.IsFalse(inv.HasAxe, "no axe seeded — the axe is crafted (U2-2), then gates the chop");
        }

        // REGRESSION GUARD (PR #224 chop-capture-gate red, run 28539711263): the Combat POC's SpearPickup is a
        // PROXIMITY-AUTO pickup (fires when the player is within pickupRadius). Originally placed at (2,0,6) —
        // EXACTLY pickupRadius (2.0u planar) from the player spawn (0,0,6) — so it auto-grabbed the spear into
        // belt slot 0 (the default-selected slot) on frame 1. The later-crafted axe then landed in slot 1, so
        // Inventory.IsAxeSelectedInBelt was FALSE → the chop gate never passed → no wood → the shipped chop
        // capture gate failed. Chopping code was byte-identical to main; ONLY the scene author changed. This
        // guard pins the SCENE GEOMETRY: the spear pickup must sit CLEAR of the spawn by MORE than its own
        // pickupRadius, so it can NEVER auto-grab slot 0 at spawn. A future move back onto (or inside) the
        // spawn radius turns this RED in fast headless CI instead of red 15 minutes later in the capture gate.
        // (The model-level sibling guard — the axe-select ordering invariant — lives in InventoryFacadeTests.)
        [Test]
        public void BootScene_SpearPickup_ClearOfPlayerSpawn_CannotAutoGrabSlot0()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "the Boot scene must open clean");

            var pickup = FindInScene<FarHorizon.Combat.SpearPickup>(scene);
            Assert.IsNotNull(pickup,
                "the Boot scene must carry the SpearPickup (Combat POC AC4) — serialized editor-time");
            Assert.IsNotNull(pickup.player,
                "SpearPickup.player must be wired editor-time so the proximity check has the spawn target");

            Vector3 spearPos = pickup.transform.position;
            Vector3 spawnPos = pickup.player.position; // the Player root ships AT its spawn (0,0,6)
            float planarDist = Vector2.Distance(
                new Vector2(spearPos.x, spearPos.z), new Vector2(spawnPos.x, spawnPos.z));

            Assert.Greater(planarDist, pickup.pickupRadius,
                $"the SpearPickup ({spearPos}) must sit CLEAR of the player spawn ({spawnPos}) by MORE than its " +
                $"pickupRadius ({pickup.pickupRadius}u) — else the proximity-auto pickup grabs the spear into " +
                $"belt slot 0 at spawn, de-selecting the later-crafted axe and silently breaking the chop " +
                $"(planarDist={planarDist:F2}u). This IS the PR #224 chop-capture-gate regression.");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
