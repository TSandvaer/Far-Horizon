using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the BOULDER-MINING interaction (ticket 86camz9v7 / crafting ②).
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving the boulder
    /// loop works in the BUILT exe, not just the editor. The direct sibling of <see cref="MineVerifyCapture"/>: it
    /// drives the full loop seam — wait for the agent on the NavMesh, GRANT + SELECT a WOOD pickaxe (the ② entry
    /// gate), teleport to a real boulder, drive the active-left-click mine via <see cref="MineBoulder.RequestMineClick"/>
    /// until the boulder BREAKS + drops a stone pile, press E (<see cref="PickableLooter.RequestLoot"/>) to loot it —
    /// then asserts stone landed in the inventory, proving end-to-end "select wood pickaxe → mine boulder → break →
    /// loot stone" in the shipped player.
    ///
    /// PHYSICAL-WORLD ANCHOR (Sponsor directive 2026-06-24): a boulder is a big ROCK sitting ON the ground — a bump
    /// UP, half-embedded, that you can walk up to; it is NOT a hole. The SIDE-PROFILE shot (boulder_side.png) must
    /// read as a rounded/chunky rock resting on the ground surface (Bar 4). The build must satisfy that sentence,
    /// not just the "stone landed" logic metric.
    ///
    /// Inert unless launched with -verifyBoulder. HEADLESS via RT-readback (86cag93zb): captures render a camera into
    /// an offscreen RT, so it runs under -batchmode (no window). Self-asserts are LOGIC (stone count).
    ///   FarHorizon.exe -batchmode -verifyBoulder -captureDir &lt;dir&gt;
    /// Captures: boulder_before.png (at spawn, no stone), boulder_side.png (a boulder side-profile, Bar 4),
    /// boulder_blocked.png (86caffwv5 round-7 TASK 2 — the player BLOCKED at the boulder surface by the runtime
    /// carve, informational), and boulder_after.png (at the boulder, stone in the inventory), then quits non-zero
    /// if the mine→break→loot proof failed.
    /// </summary>
    public class BoulderVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public MineBoulder mine;
        public PickableLooter looter;
        public string subDir = "Captures";
        public int captureWidth = 1280;
        public int captureHeight = 720;

        void Start()
        {
            if (HasArg("-verifyBoulder"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (mine == null) mine = Object.FindAnyObjectByType<MineBoulder>();
                if (looter == null) looter = Object.FindAnyObjectByType<PickableLooter>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // 1. Wait up to 3s for the agent to be placed on the NavMesh.
            float t = 0f;
            while (t < 3f && (player == null || player.Agent == null || !player.Agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            bool onMesh = player != null && player.Agent != null && player.Agent.isOnNavMesh;
            Debug.Log("[BoulderVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            int stoneBefore = StoneCount();
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log("[BoulderVerifyCapture] before: stone=" + stoneBefore + " woodPickaxeSelected=" +
                      MineBoulder.IsBoulderPickaxeSelected(inventory));
            ShotMain(Path.Combine(dir, "boulder_before.png"));
            yield return null;

            // 2. GRANT + SELECT a WOOD pickaxe (the ② boulder-mine entry gate — a wood pickaxe you'd craft at the
            // ① table). Add it through the model directly (the shipped acquisition is the table craft; this capture
            // proves the MINE, not the craft — CraftingMenuPlayModeTests + the ② soak prove the craft).
            bool haveBoulder = TryPickReachableBoulder(out Vector3 boulderPos);
            if (inventory != null && inventory.Model != null && inventory.Catalog != null)
            {
                if (!inventory.Model.OwnsItem(ItemCatalog.PickaxeWoodId))
                {
                    var p = inventory.Catalog.ById(ItemCatalog.PickaxeWoodId);
                    if (p != null) inventory.Model.AddToolToBelt(p);
                }
                SelectWoodPickaxeBeltSlot();
            }
            Debug.Log("[BoulderVerifyCapture] wood pickaxe selected=" + MineBoulder.IsBoulderPickaxeSelected(inventory));

            // Side-profile shot of a boulder (Bar 4 — a boulder is a rock resting ON the ground, a bump UP).
            if (haveBoulder) ShotSideProfile(boulderPos, Path.Combine(dir, "boulder_side.png"));
            yield return null;

            // 3. Teleport to a real boulder + drive left-click mine strikes + E-loot until stone lands.
            Debug.Log("[BoulderVerifyCapture] boulder picked: " + haveBoulder + " at " + boulderPos);
            bool gotStone = false;
            if (haveBoulder)
            {
                bool setNode = TeleportPlayer(boulderPos);
                Debug.Log("[BoulderVerifyCapture] teleport to boulder set: " + setNode + " target=" + boulderPos);

                // 86caffwv5 round-7 (TASK 2) — COLLISION EVIDENCE: with the runtime carving NavMeshObstacle, warping
                // toward the boulder CENTRE snaps the agent to the nearest walkable navmesh = the boulder SURFACE (the
                // carve removed the centre from the navmesh), so the player is BLOCKED at the surface, ~carveRadius
                // from centre — pre-carve this landed at ~0u (the centre was walkable). Log the stand-off + capture the
                // blocked-at-surface frame. Informational (PASS stays the mine→loot logic); the reviewer/Sponsor
                // eyeballs boulder_blocked.png + confirms the player can still MINE from here (distToCenter < mineRadius).
                float distToCenter = player != null ? PlanarDist(player.transform.position, boulderPos) : -1f;
                Debug.Log("[BoulderVerifyCapture] BLOCKED-AT-SURFACE: warp-toward-centre landed the player at planarDist=" +
                          distToCenter.ToString("F2") + "u from the boulder centre (the carve blocks entry; pre-carve ~0u); " +
                          "mineRadius=" + (mine != null ? mine.mineRadius : 2.4f).ToString("F2") +
                          " -> mineable-from-surface=" + (distToCenter >= 0f && distToCenter <= (mine != null ? mine.mineRadius : 2.4f)));
                for (int i = 0; i < 6; i++) yield return null; // let the orbit cam settle on the player at the surface
                ShotMain(Path.Combine(dir, "boulder_blocked.png"));
                yield return null;

                // Mine from the carve-blocked SURFACE until the boulder breaks + drops its stone pile at the
                // (formerly boulder) centre. 86caffwv5 round-7 (TASK 2): the carve holds the player ~carveRadius
                // from the centre — beyond the pile's 1.4u loot range — so once the boulder breaks (IsMineable=false
                // → the carve toggles OFF → the centre becomes walkable), the player WALKS IN to loot. The capture
                // bypasses the NavMeshAgent for the walk-in (an agent.Warp snaps to the nearest navmesh, which the
                // carve has NOT yet healed the frame after the break → it lands back at the surface): DISABLE the
                // agent + raw-place the player ON the pile (loot is a PLANAR-distance test, no navmesh needed).
                // CRUCIALLY, do NOT loot DURING mining — the mining spot is now near scatter pickables (berry/stick/
                // stone) that a per-frame loot would grab + REGROW + re-grab, filling the 20-slot pack so the stone
                // can't be added on break (diagnosed via a packFree=0/20 trace). Loot ONLY after the walk-in.
                float start = Time.time;
                bool walkedToPile = false;
                while (Time.time - start < 25f)
                {
                    if (StoneCount() > stoneBefore) break;
                    if (!walkedToPile)
                    {
                        var pile = Object.FindAnyObjectByType<StonePile>();
                        if (pile != null)
                        {
                            var ag = player != null ? player.Agent : null;
                            if (ag != null && ag.enabled) ag.enabled = false; // stop the agent re-snapping us off the pile
                            if (player != null) player.transform.position = pile.transform.position;
                            if (looter != null && looter.player != null) looter.player.position = pile.transform.position;
                            walkedToPile = true;
                            Debug.Log("[BoulderVerifyCapture] boulder BROKE -> stone pile at " +
                                      pile.transform.position.ToString("F2") + "; carve now off -> walked onto the pile to loot");
                        }
                        else if (mine != null) mine.RequestMineClick(); // still standing -> MINE ONLY (no loot -> keep the pack empty for the stone)
                    }
                    else if (looter != null)
                    {
                        // AFTER the walk-in: E-loot the stone pile (the DIRECT TryLootNearest = the same resolve+
                        // TryLoot the E press drives; deterministic per frame, no latch/Update dependency).
                        looter.TryLootNearest();
                    }
                    yield return null;
                }
                gotStone = StoneCount() > stoneBefore;
                Debug.Log("[BoulderVerifyCapture] mined stone (via break -> pile -> E-loot): " + gotStone +
                          " (stone " + stoneBefore + " -> " + StoneCount() + ")");
                for (int i = 0; i < 8; i++) yield return null;
                ShotMain(Path.Combine(dir, "boulder_after.png"));
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            bool pass = haveBoulder && gotStone;
            Debug.Log("[BoulderVerifyCapture] verification complete -> " + dir +
                      " haveBoulder=" + haveBoulder + " gotStone=" + gotStone + " => PASS=" + pass);
            Application.Quit(pass ? 0 : 1);
        }

        private int StoneCount()
        {
            if (inventory == null || inventory.Model == null) return 0;
            return inventory.Model.CountItem(ItemCatalog.StoneId);
        }

        private void SelectWoodPickaxeBeltSlot()
        {
            if (inventory == null || inventory.Model == null) return;
            var belt = inventory.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                var s = belt[i];
                if (!s.IsEmpty && s.Def != null && s.Def.Id == ItemCatalog.PickaxeWoodId)
                {
                    inventory.Model.SelectBelt(i);
                    return;
                }
            }
        }

        // Discover the authored boulder positions at runtime (from mine.boulderRoot, or a "Boulders" name-scan — the
        // SAME discovery path MineBoulder uses; NEVER a hardcoded guess) and pick the one reachable on the NavMesh
        // within mineRadius. Only ENABLED, active boulders count.
        private bool TryPickReachableBoulder(out Vector3 chosen)
        {
            chosen = Vector3.zero;
            Transform root = mine != null ? mine.boulderRoot : null;
            if (root == null)
            {
                var found = GameObject.Find("Boulders");
                if (found != null) root = found.transform;
            }
            if (root == null) return false;

            var positions = new List<Vector3>();
            CollectBoulderPositions(root, positions);
            if (positions.Count == 0) return false;

            float radius = mine != null ? mine.mineRadius : 2.4f;
            float bestNavGap = float.PositiveInfinity;
            bool any = false;
            foreach (var p in positions)
            {
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                float navGap = PlanarDist(hit.position, p);
                if (navGap > radius) continue;
                if (navGap < bestNavGap) { bestNavGap = navGap; chosen = p; any = true; }
            }
            return any;
        }

        private bool TeleportPlayer(Vector3 standPos)
        {
            if (player == null) return false;
            var agent = player.Agent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 warpTo = standPos;
                if (NavMesh.SamplePosition(standPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    warpTo = hit.position;
                if (agent.Warp(warpTo)) return true;
            }
            player.transform.position = standPos;
            return true;
        }

        private static void CollectBoulderPositions(Transform root, List<Vector3> outPositions)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == MineBoulder.BoulderNodeName)
                {
                    if (child.gameObject.activeInHierarchy) outPositions.Add(child.position);
                }
                else if (child.childCount > 0)
                {
                    CollectBoulderPositions(child, outPositions);
                }
            }
        }

        private static float PlanarDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void ShotMain(string file)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[BoulderVerifyCapture] no Camera.main — cannot capture " + file);
                return;
            }
            Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
            if (tex != null) Object.Destroy(tex);
            Debug.Log("[BoulderVerifyCapture] wrote " + file);
        }

        // SIDE-PROFILE shot (Bar 4 physical feature): a temporary camera placed BESIDE the boulder at ~ground level,
        // looking horizontally, so the up-vs-down read (a rock resting ON the ground, a bump UP) is eyeball-able —
        // top-down/player-eye frames hide it (Sponsor directive 2026-06-24). Rides the gameplay render path (copies
        // Camera.main's clear/skybox so lighting is representative), then is destroyed.
        private void ShotSideProfile(Vector3 boulderPos, string file)
        {
            var main = Camera.main;
            var camGo = new GameObject("BoulderSideCam");
            var cam = camGo.AddComponent<Camera>();
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
                cam.fieldOfView = main.fieldOfView;
            }
            // Beside the boulder, slightly above ground, looking level at it — the silhouette reads side-on.
            Vector3 eye = boulderPos + new Vector3(3.2f, 0.6f, 0f);
            cam.transform.position = eye;
            cam.transform.LookAt(boulderPos + Vector3.up * 0.4f);
            Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
            if (tex != null) Object.Destroy(tex);
            Object.Destroy(camGo);
            Debug.Log("[BoulderVerifyCapture] wrote side-profile " + file);
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
