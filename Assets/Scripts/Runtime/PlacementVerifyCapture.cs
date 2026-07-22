using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the placement OBJECT-OVERLAP rule (ticket 86catqxm0 — ① soak
    /// follow-up: "the ghost table should be red when colliding with other objects"). Sibling of
    /// <see cref="ChopVerifyCapture"/>: inert unless launched with <c>-verifyPlacement</c>; captures via
    /// RT-readback (works under -batchmode, no window).
    ///
    /// Proves in the BUILT exe that the placement ghost reads RED over a real object and GREEN on clear ground:
    ///   • aims the ghost at a genuinely CLEAR spot near the player (searched, never a fixed guess that a
    ///     boulder might coincidentally sit on) → asserts <see cref="CraftingTablePlacement.IsCurrentPlacementObstructed"/>
    ///     == false → placement_clear_green.png;
    ///   • teleports the player beside a real seed-42 scatter tree (discovered at runtime, never a hardcoded
    ///     guess) and AIMS the ghost at the trunk via the input-independent <see cref="CraftingTablePlacement.AimGhostAt"/>
    ///     seam → asserts obstructed == true → placement_tree_red.png (navmesh-carve branch);
    ///   • teleports the player beside a real registered BOULDER (86catr49m — the fix this PR proves) and aims
    ///     the ghost at it → asserts obstructed == true → placement_boulder_red.png (PlacementObstacleRegistry
    ///     branch; boulders are collider-free + do NOT carve the navmesh, so ONLY the registry makes this RED).
    /// Exits non-zero if ANY proof fails, so a headless run is auditable.
    ///
    /// Authored into Boot.unity editor-time (MovementCameraScene.WirePlacementVerifyCapture) + wired as the
    /// -verifyPlacement capture gate (ci.yml + verify_placement_gate.sh). Launch shape:
    ///   FarHorizon.exe -batchmode -verifyPlacement -captureDir &lt;dir&gt;
    /// A component-in-source-but-NOT-scene-authored harness NO-OPs the verb AND HANGS the exe (the #302 inert
    /// lesson, unity-conventions §CI) — hence the scene-author wire above.
    ///
    /// NO mutable statics — needs no [RuntimeInitializeOnLoadMethod] reset (StaticStateResetTests).
    /// </summary>
    public class PlacementVerifyCapture : MonoBehaviour
    {
        public CraftingTablePlacement placement;
        public ClickToMove player;
        public Inventory inventory;
        public string subDir = "Captures";
        public int captureWidth = 1280;
        public int captureHeight = 720;

        void Start()
        {
            if (!HasArg("-verifyPlacement")) return;
            if (placement == null) placement = Object.FindAnyObjectByType<CraftingTablePlacement>();
            if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
            if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
            StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Wait up to 3s for the agent to settle on the NavMesh (the tree-carve signal needs a baked navmesh).
            float t = 0f;
            while (t < 3f && (player == null || player.Agent == null || !player.Agent.isOnNavMesh))
            { t += Time.unscaledDeltaTime; yield return null; }

            bool treeObstructed = false, boulderObstructed = false, clearOk = false;

            if (placement == null)
            {
                Debug.LogError("[PlacementVerifyCapture] no CraftingTablePlacement — cannot verify");
                Application.Quit(1);
                yield break;
            }

            placement.EnterPlacement();
            for (int i = 0; i < 5; i++) yield return null;

            // 1) GREEN on genuinely clear ground near the player. Boulder registration (86catr49m) means a FIXED
            // offset could coincidentally land on a boulder's no-build zone → search a small bounded ring for a
            // spot that reads unobstructed, so the GREEN proof is never a flake.
            if (TryFindClearSpotNearPlayer(out Vector3 clear))
            {
                placement.AimGhostAt(clear);
                for (int i = 0; i < 5; i++) yield return null;
                clearOk = !placement.IsCurrentPlacementObstructed;
                Debug.Log("[PlacementVerifyCapture] ghost on clear ground " + clear + " obstructed=" +
                          placement.IsCurrentPlacementObstructed + " (false => GREEN on open ground)");
                ShotTo(Path.Combine(dir, "placement_clear_green.png"));
                yield return null;
            }
            else
            {
                Debug.LogError("[PlacementVerifyCapture] no clear spot found near the player — cannot prove GREEN");
            }

            // 2) RED over a real seed-42 scatter tree (the navmesh-carve obstruction branch).
            if (TryPickReachableScatterTree(out Vector3 tree))
            {
                TeleportPlayer(tree + new Vector3(2.5f, 0f, 0f)); // stand just off the trunk so the cam frames both
                for (int i = 0; i < 5; i++) yield return null;
                placement.AimGhostAt(tree);
                for (int i = 0; i < 5; i++) yield return null;
                treeObstructed = placement.IsCurrentPlacementObstructed;
                Debug.Log("[PlacementVerifyCapture] ghost over tree " + tree + " obstructed=" + treeObstructed +
                          " (true => RED over a real object — 86catqxm0 proven in the shipped exe)");
                ShotTo(Path.Combine(dir, "placement_tree_red.png"));
                yield return null;
            }
            else
            {
                Debug.LogError("[PlacementVerifyCapture] no reachable scatter tree discovered — cannot prove RED-over-tree");
            }

            // 3) RED over a real registered BOULDER (86catr49m — the fix this PR proves). Boulders are
            // collider-free AND do NOT carve the navmesh, so ONLY PlacementObstacleRegistry (driven by
            // MineBoulder off each boulder's IsMineable) can make this footprint read RED — a precise
            // shipped-exe proof of the boulder registration.
            if (TryPickReachableBoulder(out Vector3 boulder))
            {
                TeleportPlayer(boulder + new Vector3(2.5f, 0f, 0f));
                for (int i = 0; i < 5; i++) yield return null;
                placement.AimGhostAt(boulder);
                for (int i = 0; i < 5; i++) yield return null;
                boulderObstructed = placement.IsCurrentPlacementObstructed;
                Debug.Log("[PlacementVerifyCapture] ghost over boulder " + boulder + " obstructed=" +
                          boulderObstructed + " (true => RED over a registered boulder — 86catr49m proven in the " +
                          "shipped exe; boulders are collider-free + do NOT carve navmesh, so this is the registry)");
                ShotTo(Path.Combine(dir, "placement_boulder_red.png"));
                yield return null;
            }
            else
            {
                Debug.LogError("[PlacementVerifyCapture] no reachable boulder discovered — cannot prove RED-over-boulder " +
                               "(the boulder pool / MineBoulder registration is missing from the shipped scene)");
            }

            yield return new WaitForSeconds(0.3f);
            placement.Cancel();
            bool pass = clearOk && treeObstructed && boulderObstructed;
            Debug.Log("[PlacementVerifyCapture] verification complete -> " + dir + " clearOk=" + clearOk +
                      " treeObstructed=" + treeObstructed + " boulderObstructed=" + boulderObstructed +
                      " => PASS=" + pass);
            Application.Quit(pass ? 0 : 1);
        }

        // Search a small BOUNDED ring of candidate offsets around the player for a spot the placement ghost reads
        // as UNOBSTRUCTED (clear ground, off any tree carve + off any registered no-build zone). Deterministic +
        // bounded (never hangs). Returns the first clear candidate; false only if the player is somehow boxed in.
        private bool TryFindClearSpotNearPlayer(out Vector3 clear)
        {
            clear = default;
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            float[] radii = { 4f, 5.5f, 7f };
            for (int r = 0; r < radii.Length; r++)
            {
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * (Mathf.PI * 2f / 8f);
                    Vector3 cand = origin + new Vector3(Mathf.Cos(ang) * radii[r], 0f, Mathf.Sin(ang) * radii[r]);
                    cand.y = origin.y;
                    placement.AimGhostAt(cand);
                    if (!placement.IsCurrentPlacementObstructed) { clear = cand; return true; }
                }
            }
            return false;
        }

        // Discover a real scatter LP_Tree position (from the LowPolyScatter name-scan — the SAME discovery path
        // ChopTree uses; NEVER a hardcoded guess) that is reachable on the NavMesh.
        private bool TryPickReachableScatterTree(out Vector3 chosen)
        {
            chosen = Vector3.zero;
            var found = GameObject.Find("LowPolyScatter");
            if (found == null) return false;
            var positions = new List<Vector3>();
            CollectScatterTreePositions(found.transform, positions);
            Vector3 from = player != null ? player.transform.position : Vector3.zero;
            float best = float.PositiveInfinity;
            bool any = false;
            foreach (var p in positions)
            {
                if (!NavMesh.SamplePosition(p + new Vector3(2.5f, 0f, 0f), out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue; // need a walkable stand-spot beside the trunk so the player can be placed there
                float d = PlanarDist(p, from);
                if (d < best) { best = d; chosen = p; any = true; }
            }
            return any;
        }

        private static void CollectScatterTreePositions(Transform root, List<Vector3> outPositions)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == ChopTree.ScatterTreeName) outPositions.Add(child.position);
                else if (child.childCount > 0) CollectScatterTreePositions(child, outPositions);
            }
        }

        // Discover a real registered boulder (the SAME name-scan MineBoulder uses — "Boulders" root, "Boulder"
        // nodes; never a hardcoded guess) that is reachable on the NavMesh, nearest the player. Mirrors the tree
        // discovery. 86catr49m.
        private bool TryPickReachableBoulder(out Vector3 chosen)
        {
            chosen = Vector3.zero;
            var found = GameObject.Find("Boulders");
            if (found == null) return false;
            var positions = new List<Vector3>();
            CollectBoulderPositions(found.transform, positions);
            Vector3 from = player != null ? player.transform.position : Vector3.zero;
            float best = float.PositiveInfinity;
            bool any = false;
            foreach (var p in positions)
            {
                if (!NavMesh.SamplePosition(p + new Vector3(2.5f, 0f, 0f), out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue; // need a walkable stand-spot beside the boulder so the cam can frame it
                float d = PlanarDist(p, from);
                if (d < best) { best = d; chosen = p; any = true; }
            }
            return any;
        }

        private static void CollectBoulderPositions(Transform root, List<Vector3> outPositions)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == MineBoulder.BoulderNodeName) outPositions.Add(child.position);
                else if (child.childCount > 0) CollectBoulderPositions(child, outPositions);
            }
        }

        private void TeleportPlayer(Vector3 standPos)
        {
            if (player == null) return;
            var agent = player.Agent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 warpTo = standPos;
                if (NavMesh.SamplePosition(standPos, out NavMeshHit hit, 4f, NavMesh.AllAreas)) warpTo = hit.position;
                if (agent.Warp(warpTo)) return;
            }
            player.transform.position = standPos;
        }

        private static float PlanarDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void ShotTo(string file)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[PlacementVerifyCapture] no Camera.main — cannot capture " + file); return; }
            Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
            if (tex != null) Object.Destroy(tex);
            Debug.Log("[PlacementVerifyCapture] wrote " + file);
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
