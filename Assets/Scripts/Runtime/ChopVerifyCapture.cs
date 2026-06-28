using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the chop interaction (tickets 86ca8bdd8 → 86caa4c5c).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving
    /// the chop works in the BUILT exe, not just the editor. Sibling of CraftVerifyCapture: it drives
    /// the full loop seam — wait for the agent on the NavMesh, MoveTo the CRAFT spot (gets the axe,
    /// U2-2), then MoveTo the TREE and wait for the chop to yield wood — then captures, proving
    /// end-to-end "craft axe -> chop tree -> wood in the readout" in the shipped player with the HUD
    /// build stamp visible. Asserts WoodCount > 0 in the log so a headless run is auditable.
    ///
    /// CHANGE 1 (86caa4c5c): the chop is now triggered by an active LEFT-CLICK, not proximity-auto. The shipped
    /// exe can't inject a real mouse button into this scripted capture, so once the player has REACHED the tree
    /// this capture drives the chop via the programmatic <see cref="ChopTree.RequestChopClick"/> seam (the same
    /// input-independent seam the PlayMode tests use) — exercising the SAME chop path a real left-click takes.
    ///
    /// CHANGE (a) GATE-HARDENING (86caa4c5c follow-up): the demo-tree chop alone only proves the pre-PR demo
    /// behaviour — it does NOT prove that a REAL seed-42 SCATTER tree is choppable in the shipped scene (the
    /// Sponsor rejected "only ONE tree chops" twice; this is the gate that should catch a regression here).
    /// So this capture now ALSO:
    ///   • asserts the running BUILT scene has more than one tracked choppable tree
    ///     (<see cref="ChopTree.InstanceCount"/> &gt; 1) — i.e. the scatter trees were discovered, not just the
    ///     demo tree;
    ///   • DISCOVERS a real scatter <c>LP_Tree</c> position at runtime (from <see cref="ChopTree.scatterRoot"/>
    ///     / the <c>LowPolyScatter</c> name-scan — NEVER a hardcoded guessed coordinate, because the seed-42
    ///     placement could move) that is REACHABLE on the NavMesh and far from the demo tree, drives the player
    ///     there, and asserts a SECOND, INDEPENDENT wood gain on top of the demo-tree wood.
    /// Exits non-zero if EITHER the demo-tree chop OR the scatter-tree choppability fails to be proven.
    ///
    /// Inert unless launched with -verifyChop (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyChop -captureDir &lt;dir&gt;
    /// Captures: chop_before.png (at spawn, no wood) + chop_after.png (at the demo tree, wood in readout)
    /// + chop_scatter.png (at a real scatter tree, MORE wood), then quits non-zero if either proof failed.
    /// </summary>
    public class ChopVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public ChopTree chop;
        public PickableLooter looter;   // REWORK 86caf9u5t — loots the LOG PILE a felled tree drops (E-loot)
        public Vector3 craftSpot = new Vector3(8f, 0f, 6f);
        public Vector3 treeSpot = new Vector3(-9f, 0f, -7f);
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyChop"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (chop == null) chop = Object.FindAnyObjectByType<ChopTree>();
                if (looter == null) looter = Object.FindAnyObjectByType<PickableLooter>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // 1. Wait up to 3s for the agent to be placed on the NavMesh (EnsureOnNavMesh retry).
            float t = 0f;
            while (t < 3f && (player == null || player.Agent == null || !player.Agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            bool onMesh = player != null && player.Agent != null && player.Agent.isOnNavMesh;
            Debug.Log("[ChopVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            // 'before' shot: at spawn, no axe, no wood.
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log("[ChopVerifyCapture] before: HasAxe=" + (inventory != null && inventory.HasAxe) +
                      " wood=" + (inventory != null ? inventory.WoodCount : -1));
            ShotTo(Path.Combine(dir, "chop_before.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. First get the axe — teleport to the craft spot and wait for HasAxe (U2-2's seam).
            // WHY TELEPORT, NOT MoveTo (the WASD-pivot fix — 86cafecuj CI-gate triage): the live build drives
            // locomotion via WasdMovement, which HARD-SETS NavMeshAgent.velocity = inputDir*speed EVERY frame
            // (WasdMovement.cs:259). With no WASD input fed, that velocity is ZERO each frame — clobbering the
            // path-following velocity ClickToMove.MoveTo→SetDestination would set up, so the agent never moves
            // and the player freezes at spawn (MoveTo returns true but the agent stays put → axe never crafts →
            // the whole chop loop cascade-fails). MoveTo is a DEAD seam under WASD locomotion. The deterministic,
            // NavMesh-safe drive (the SAME pattern the passing LootPromptVerifyCapture gate uses) is to Warp the
            // agent into range of each target; the chop/loot then fire via the input-independent RequestChopClick/
            // RequestLoot seams below. We teleport ONTO each target (the craft spot / tree trunk): that is well
            // within the proximity radii (craftRadius 2.0u / chopRadius 2.2u), so the craft still fires via its
            // OWN Update proximity poll and the chop via RequestChopClick's own in-range check — we drive the
            // player into range, we do NOT bypass the gameplay seam. (The crafted axe lands in belt slot 0, which
            // is the default-selected slot, so IsAxeSelectedInBelt — the chop's select guard — is satisfied.)
            bool setCraft = TeleportPlayer(craftSpot);
            Debug.Log("[ChopVerifyCapture] teleport to craft spot set: " + setCraft + " target=" + craftSpot);
            float start = Time.time;
            while (Time.time - start < 12f)
            {
                if (inventory != null && inventory.HasAxe) break;
                yield return null;
            }
            Debug.Log("[ChopVerifyCapture] axe crafted: " + (inventory != null && inventory.HasAxe));

            // 3. Now chop the DEMO tree — drive to the tree, then (CHANGE 1) drive LEFT-CLICK chop requests once
            // in range until wood is yielded. The chop is per-click now, so standing at the tree alone does
            // nothing — RequestChopClick() is the input-independent analog of a real left-click (range +
            // axe-selected + over-UI/RMB guards still apply, exactly like a real click). Request a chop EACH
            // frame while at the tree (the click cooldown paces the actual chops); the chop is a no-op until
            // truly in range.
            bool setTree = TeleportPlayer(treeSpot);
            Debug.Log("[ChopVerifyCapture] teleport to tree set: " + setTree + " target=" + treeSpot);
            start = Time.time;
            while (Time.time - start < 18f)
            {
                if (inventory != null && inventory.WoodCount > 0) break;
                // REWORK 86caf9u5t — chopping no longer banks wood per swing; the wood drops on FELL as a lootable
                // LOG PILE. So: drive the left-click chop to FELL the tree (a pile spawns), AND press E (loot the
                // pile) every frame. Both are harmless until in range / a pile exists — the wood arrives only once
                // the tree has felled AND the pile is looted, proving the whole fell → pile → E-loot path (AC8).
                if (chop != null) chop.RequestChopClick();
                if (looter != null) looter.RequestLoot();
                yield return null;
            }
            int woodAfterDemo = inventory != null ? inventory.WoodCount : -1;
            bool gotDemoWood = woodAfterDemo > 0;
            Debug.Log("[ChopVerifyCapture] demo-tree wood (via fell -> pile -> E-loot) yielded: " + gotDemoWood +
                      " (wood=" + woodAfterDemo + ", true means REACHED the demo tree, FELLED it, and LOOTED the " +
                      "dropped log pile — the full REWORK 86caf9u5t path proven in the shipped exe)");

            // Let the camera settle, then capture the 'after' shot with wood in the readout.
            for (int i = 0; i < 8; i++) yield return null;
            ShotTo(Path.Combine(dir, "chop_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 4. CHANGE (a) GATE-HARDENING — prove a REAL seed-42 SCATTER tree is choppable in the BUILT scene
            // (not just the demo tree the pre-PR gate already exercised). First: the resolver must have
            // discovered MORE than one choppable tree (the demo tree + the scatter LP_Trees) — InstanceCount>1.
            int instanceCount = chop != null ? chop.InstanceCount : 0;
            bool scatterDiscovered = instanceCount > 1;
            Debug.Log("[ChopVerifyCapture] choppable tree InstanceCount=" + instanceCount +
                      " (scatterDiscovered=" + scatterDiscovered + "; >1 means the seed-42 scatter LP_Trees " +
                      "were discovered alongside the demo tree, CHANGE (a))");

            // Discover a REAL scatter LP_Tree position at runtime (NEVER a hardcoded guess — the seed-42
            // placement could move). Pick one that is REACHABLE on the NavMesh and far from the demo tree, so
            // the second wood gain is unambiguously a SCATTER tree, not the demo tree.
            bool gotScatterWood = false;
            int woodAfterScatter = woodAfterDemo;
            Vector3 chosen = Vector3.zero;
            bool haveScatterTarget = TryPickReachableScatterTree(out chosen);
            Debug.Log("[ChopVerifyCapture] scatter target picked: " + haveScatterTarget + " at " + chosen);

            if (haveScatterTarget)
            {
                bool setScatter = TeleportPlayer(chosen);
                Debug.Log("[ChopVerifyCapture] teleport to scatter tree set: " + setScatter + " target=" + chosen);
                start = Time.time;
                while (Time.time - start < 20f)
                {
                    if (inventory != null && inventory.WoodCount > woodAfterDemo) break;
                    // REWORK 86caf9u5t — fell the scatter tree (chop) AND loot its dropped pile (E) to gain MORE wood.
                    if (chop != null) chop.RequestChopClick();
                    if (looter != null) looter.RequestLoot();
                    yield return null;
                }
                woodAfterScatter = inventory != null ? inventory.WoodCount : woodAfterDemo;
                gotScatterWood = woodAfterScatter > woodAfterDemo;
                Debug.Log("[ChopVerifyCapture] scatter-tree wood yielded: " + gotScatterWood + " (wood " +
                          woodAfterDemo + " -> " + woodAfterScatter + "; true means a REAL scatter LP_Tree was " +
                          "reached AND chopped — CHANGE (a) proven in the shipped exe)");

                for (int i = 0; i < 8; i++) yield return null;
                ShotTo(Path.Combine(dir, "chop_scatter.png"));
                yield return new WaitForEndOfFrame();
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            bool pass = gotDemoWood && scatterDiscovered && haveScatterTarget && gotScatterWood;
            Debug.Log("[ChopVerifyCapture] verification complete -> " + dir +
                      " demoWood=" + gotDemoWood + " scatterDiscovered=" + scatterDiscovered +
                      " scatterTarget=" + haveScatterTarget + " scatterWood=" + gotScatterWood +
                      " => PASS=" + pass);
            // Fail loud in the shipped build if the demo-tree chop OR the scatter choppability wasn't proven.
            Application.Quit(pass ? 0 : 1);
        }

        // Discover the world's scatter LP_Tree positions at runtime (from chop.scatterRoot, or a
        // LowPolyScatter name-scan — the SAME discovery path ChopTree.Start uses; NEVER a hardcoded guess),
        // and pick the one that is (a) reachable on the NavMesh within chopRadius (so the player can actually
        // stand in chop range) and (b) far enough from the demo tree that a chop there is unambiguously a
        // SCATTER tree. Returns the chosen LP_Tree world position. The capture then click-moves there.
        private bool TryPickReachableScatterTree(out Vector3 chosen)
        {
            chosen = Vector3.zero;
            Transform root = chop != null ? chop.scatterRoot : null;
            if (root == null)
            {
                var found = GameObject.Find("LowPolyScatter");
                if (found != null) root = found.transform;
            }
            if (root == null) return false;

            var positions = new List<Vector3>();
            CollectScatterTreePositions(root, positions);
            if (positions.Count == 0) return false;

            float radius = chop != null ? chop.chopRadius : 2.2f;
            // Where the player can sample to (4u like ClickToMove.MoveTo); for the tree to be choppable, the
            // reachable NavMesh point must be within chopRadius of the LP_Tree (planar). Far from the demo
            // tree (> 6u) so the wood gain can't be mistaken for the demo tree's.
            Vector3 demo = treeSpot;
            float bestNavGap = float.PositiveInfinity;
            bool any = false;
            foreach (var p in positions)
            {
                if (PlanarDist(p, demo) < 6f) continue; // too close to the demo tree to be unambiguous
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                float navGap = PlanarDist(hit.position, p); // distance the player would stand from the trunk
                if (navGap > radius) continue;              // can't get within chop range — skip
                if (navGap < bestNavGap)
                {
                    bestNavGap = navGap;
                    chosen = p;
                    any = true;
                }
            }
            return any;
        }

        // NavMesh-safe teleport (the WASD-pivot drive seam — 86cafecuj). Under WASD locomotion MoveTo is a dead
        // seam (WasdMovement hard-sets agent.velocity each frame, zeroing path-following with no input fed), so we
        // Warp the agent into range of each target instead — the SAME proven pattern the passing
        // LootPromptVerifyCapture gate uses (LootPromptVerifyCapture.TeleportPlayer): Warp the AGENT so its
        // internal position tracks the teleport and the transform is not re-snapped back onto navmesh on the
        // agent's next update. Sample the nearest navmesh point first so the warp lands on valid mesh; fall back
        // to a raw transform set only when there is no agent / no navmesh nearby (a degenerate rig). The chop/loot
        // seams resolve PLANAR distance from player.position, so a sub-unit navmesh snap from the exact target
        // does not change the in-range verdict. Returns true if the player was placed (agent warp or raw set).
        private bool TeleportPlayer(Vector3 standPos)
        {
            if (player == null) return false;
            var agent = player.Agent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 warpTo = standPos;
                if (NavMesh.SamplePosition(standPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    warpTo = hit.position;
                if (agent.Warp(warpTo)) return true; // agent now owns this position; the transform stays put
            }
            player.transform.position = standPos; // no-agent fallback
            return true;
        }

        private static void CollectScatterTreePositions(Transform root, List<Vector3> outPositions)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == ChopTree.ScatterTreeName)
                    outPositions.Add(child.position);
                else if (child.childCount > 0)
                    CollectScatterTreePositions(child, outPositions);
            }
        }

        private static float PlanarDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[ChopVerifyCapture] wrote " + file);
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", subDir)
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", subDir);
            return Path.GetFullPath(baseDir);
        }

        private bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
