using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the MINE interaction (ticket 86cakkmr0 / I-2). The testing bar's
    /// shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving the mine loop works in the
    /// BUILT exe, not just the editor. The direct sibling of <see cref="ChopVerifyCapture"/>: it drives the full
    /// loop seam — wait for the agent on the NavMesh, GRANT + SELECT a stone pickaxe (the mine gate), teleport to a
    /// real ore node, drive the active-left-click mine via <see cref="MineOre.RequestMineClick"/> until the node
    /// BREAKS + drops an ore pile, press E (<see cref="PickableLooter.RequestLoot"/>) to loot it — then asserts
    /// iron_ore landed in the inventory, proving end-to-end "select pickaxe → mine node → break → loot iron ore"
    /// in the shipped player with the HUD build stamp visible.
    ///
    /// The mine is per active-left-click (not proximity-auto), so standing at the node alone does nothing — the
    /// shipped exe can't inject a real mouse button into this scripted capture, so it drives the mine via the
    /// programmatic <see cref="MineOre.RequestMineClick"/> seam (the SAME input-independent seam the PlayMode tests
    /// use) — exercising the SAME mine path a real left-click takes.
    ///
    /// Inert unless launched with -verifyMine. HEADLESS via RT-readback (86cag93zb): captures render Camera.main
    /// into an offscreen RT, so it runs under -batchmode (no window). Self-asserts are LOGIC (iron_ore count).
    ///   FarHorizon.exe -batchmode -verifyMine -captureDir &lt;dir&gt;
    /// Captures: mine_before.png (at spawn, no ore) + mine_after.png (at the node, ore in the inventory), then
    /// quits non-zero if the mine→break→loot proof failed.
    /// </summary>
    public class MineVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public MineOre mine;
        public PickableLooter looter;
        public PickaxePickup pickaxePickup;
        public string subDir = "Captures";
        public int captureWidth = 1280;
        public int captureHeight = 720;

        void Start()
        {
            if (HasArg("-verifyMine"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (mine == null) mine = Object.FindAnyObjectByType<MineOre>();
                if (looter == null) looter = Object.FindAnyObjectByType<PickableLooter>();
                if (pickaxePickup == null) pickaxePickup = Object.FindAnyObjectByType<PickaxePickup>();
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
            Debug.Log("[MineVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            int oreBefore = OreCount();
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log("[MineVerifyCapture] before: iron_ore=" + oreBefore + " pickaxeSelected=" +
                      MineOre.IsPickaxeSelected(inventory));
            ShotTo(Path.Combine(dir, "mine_before.png"));
            yield return null;

            // 2. GRANT + SELECT the stone pickaxe (the mine gate). Prefer the real pickup's grant (proves the
            // acquisition seam); then SELECT the belt slot it landed in so IsPickaxeSelected is satisfied.
            bool granted = pickaxePickup != null && pickaxePickup.TryGrant();
            if (!granted && inventory != null)
            {
                // Fallback grant (bare rig / already-owned) — add directly through the model.
                var cat = inventory.Catalog; var model = inventory.Model;
                if (cat != null && model != null && !model.OwnsItem(ItemCatalog.PickaxeStoneId))
                {
                    var p = cat.ById(ItemCatalog.PickaxeStoneId);
                    if (p != null) model.AddToolToBelt(p);
                }
            }
            SelectPickaxeBeltSlot();
            Debug.Log("[MineVerifyCapture] pickaxe granted=" + granted + " selected=" +
                      MineOre.IsPickaxeSelected(inventory));

            // 3. Teleport to a real ore node + drive left-click mine strikes + E-loot until iron_ore lands.
            bool haveNode = TryPickReachableOreNode(out Vector3 node);
            Debug.Log("[MineVerifyCapture] ore node picked: " + haveNode + " at " + node);
            bool gotOre = false;
            if (haveNode)
            {
                bool setNode = TeleportPlayer(node);
                Debug.Log("[MineVerifyCapture] teleport to ore node set: " + setNode + " target=" + node);
                float start = Time.time;
                while (Time.time - start < 25f)
                {
                    if (OreCount() > oreBefore) break;
                    // Drive the active-left-click mine (fells the node → a pile spawns), AND press E (loot the
                    // pile) every frame. Both are harmless until in range / a pile exists — the ore arrives only
                    // once the node has BROKEN AND the pile is looted, proving the whole break → pile → E-loot path.
                    if (mine != null) mine.RequestMineClick();
                    if (looter != null) looter.RequestLoot();
                    yield return null;
                }
                gotOre = OreCount() > oreBefore;
                Debug.Log("[MineVerifyCapture] mined ore (via break -> pile -> E-loot): " + gotOre +
                          " (iron_ore " + oreBefore + " -> " + OreCount() + ")");
                for (int i = 0; i < 8; i++) yield return null;
                ShotTo(Path.Combine(dir, "mine_after.png"));
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            bool pass = haveNode && gotOre;
            Debug.Log("[MineVerifyCapture] verification complete -> " + dir +
                      " haveNode=" + haveNode + " gotOre=" + gotOre + " => PASS=" + pass);
            Application.Quit(pass ? 0 : 1);
        }

        private int OreCount()
        {
            if (inventory == null || inventory.Model == null) return 0;
            return inventory.Model.CountItem(ItemCatalog.IronOreId);
        }

        // Select the belt slot holding a pickaxe (stone or iron) so the mine gate (IsPickaxeSelected) passes.
        private void SelectPickaxeBeltSlot()
        {
            if (inventory == null || inventory.Model == null) return;
            var belt = inventory.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                var s = belt[i];
                if (!s.IsEmpty && s.Def != null &&
                    (s.Def.Id == ItemCatalog.PickaxeStoneId || s.Def.Id == ItemCatalog.PickaxeIronId))
                {
                    inventory.Model.SelectBelt(i);
                    return;
                }
            }
        }

        // Discover the authored ore-node positions at runtime (from mine.nodeRoot, or an "OreNodes" name-scan — the
        // SAME discovery path MineOre uses; NEVER a hardcoded guess) and pick the one reachable on the NavMesh
        // within mineRadius. Only ENABLED, standing nodes count.
        private bool TryPickReachableOreNode(out Vector3 chosen)
        {
            chosen = Vector3.zero;
            Transform root = mine != null ? mine.nodeRoot : null;
            if (root == null)
            {
                var found = GameObject.Find("OreNodes");
                if (found != null) root = found.transform;
            }
            if (root == null) return false;

            var positions = new List<Vector3>();
            CollectOreNodePositions(root, positions);
            if (positions.Count == 0) return false;

            float radius = mine != null ? mine.mineRadius : 2.2f;
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

        // Only ENABLED (active) ore-node visuals count — a disabled pool node (beyond the rarity dial) is inactive
        // in the hierarchy, so its GameObject.activeInHierarchy is false and it is skipped.
        private static void CollectOreNodePositions(Transform root, List<Vector3> outPositions)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == MineOre.OreNodeName)
                {
                    if (child.gameObject.activeInHierarchy) outPositions.Add(child.position);
                }
                else if (child.childCount > 0)
                {
                    CollectOreNodePositions(child, outPositions);
                }
            }
        }

        private static float PlanarDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void ShotTo(string file)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[MineVerifyCapture] no Camera.main — cannot capture " + file);
                return;
            }
            Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
            if (tex != null) Object.Destroy(tex);
            Debug.Log("[MineVerifyCapture] wrote " + file);
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
