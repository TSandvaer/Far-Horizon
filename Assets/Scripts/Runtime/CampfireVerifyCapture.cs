using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the ⑤ campfire PLACE-TO-BUILD flow (ticket 86camz9w7 —
    /// REWRITE of the U2-4 proximity-fire-pit capture 86ca8bdep). Proves in the BUILT exe that the campfire is
    /// invisible-until-placed and that PLACING it (ghost + confirm, wood+stone paid) reveals + LIGHTS it and the
    /// warmth RESTORES — the loop CLOSES in the shipped player, not just the editor (unity-conventions.md
    /// §editor-vs-runtime: editor evidence is necessary, never sufficient). The direct sibling of
    /// <see cref="ForgeVerifyCapture"/>: it GRANTS the build mats (removing the dead CraftSpot/chop-loop
    /// dependency the old -verifyLoop carried — the folded-in #294 flag), PLACES the campfire via the
    /// place-to-build seam (<see cref="CampfirePlacement.RequestBuildAt"/>), then asserts warmth measurably rose.
    ///
    /// -verifyLoop drives, in order:
    ///   1. let warmth DECAY a while at spawn (prove the need creates pressure) + shoot loop_cold.png
    ///   2. GRANT the campfire cost (wood + stone) into the pack
    ///   3. teleport near the park spot, PLACE the campfire at a walkable spot in front → revealed + LIT
    ///   4. stand at the lit fire → warmth RESTORES (the loop closes) + shoot loop_warm.png
    /// Asserts in the log that the campfire LIT and warmth measurably ROSE, and quits non-zero if the loop did
    /// NOT close (the build-side failure signal).
    ///
    /// HEADLESS via RT-readback (86cag93zb): captures render Camera.main into an offscreen RT, so it runs under
    /// -batchmode (no window) — the ScreenCapture backbuffer path is DEAD headless. Inert unless launched with
    /// -verifyLoop:
    ///   FarHorizon.exe -batchmode -verifyLoop -captureDir &lt;dir&gt;
    /// </summary>
    public class CampfireVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public WarmthNeed warmth;
        public Campfire campfire;
        public CampfirePlacement placement;
        public string subDir = "Captures";
        public int captureWidth = 1280;
        public int captureHeight = 720;

        void Start()
        {
            if (HasArg("-verifyLoop"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (warmth == null) warmth = Object.FindAnyObjectByType<WarmthNeed>();
                if (campfire == null) campfire = Object.FindAnyObjectByType<Campfire>();
                if (placement == null) placement = Object.FindAnyObjectByType<CampfirePlacement>();
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
            Debug.Log("[CampfireVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            // SEED the castaway COLD so the restore has visible headroom to climb into (the shipped warmth
            // decays at only 0.55/100·s — ~182s to empty — so a short capture window can't drain enough to
            // show a rise; the runtime restore is proven, we just need a low starting point). Drain warmth to
            // ~10% via the AddWarmth(-) seam (Satisfy clamps to [0,max]). The campfire ships INVISIBLE
            // (invisible-until-placed, ⑤) — nothing to see at the old fire-pit spot in the 'cold' shot.
            for (int i = 0; i < 3; i++) yield return null; // let WarmthNeed.Start seed _current=max first
            if (warmth != null) warmth.AddWarmth(0.10f * warmth.Max - warmth.Current); // → ~10%
            float coldWarmth = warmth != null ? warmth.Current01 : -1f;
            bool preVisible = campfire != null && campfire.IsPlaced;
            Debug.Log("[CampfireVerifyCapture] cold (seeded): warmth01=" + coldWarmth.ToString("0.00") +
                      " campfirePlaced(should be false)=" + preVisible);
            ShotTo(Path.Combine(dir, "loop_cold.png"));
            yield return null;

            // 2. GRANT the campfire cost (wood + stone) into the pack — the build mats. Drops the dead CraftSpot
            // axe-acquisition + chop loop the old -verifyLoop carried (the folded #294 flag): what ⑤ proves is the
            // PLACE-TO-BUILD, so we grant the mats directly (the forge-capture idiom).
            int woodCost = placement != null ? placement.woodCost : CampfirePlacement.CampfireWoodCostDefault;
            int stoneCost = placement != null ? placement.stoneCost : CampfirePlacement.CampfireStoneCostDefault;
            GrantResource(ItemCatalog.WoodId, woodCost + 1);
            GrantResource(ItemCatalog.StoneId, stoneCost + 1);
            Debug.Log("[CampfireVerifyCapture] granted mats: wood=" + Count(ItemCatalog.WoodId) + " stone=" +
                      Count(ItemCatalog.StoneId) + " (cost " + woodCost + "w + " + stoneCost + "s)");

            // 3. Teleport near the park spot, then PLACE the campfire via the place-to-build seam (⑤ — the campfire
            // is invisible-until-placed now; there is no proximity gate). Aim the ghost at a walkable navmesh spot
            // ~1.5u in front of the player (off-self, on-navmesh so the obstruction gate passes) and confirm —
            // RequestBuildAt enters placement, forces the pose, and confirms in one shot (reveals + lights).
            bool lit = false;
            if (placement != null)
            {
                TeleportPlayer(placement.transform.position);
                for (int i = 0; i < 3; i++) yield return null; // let the agent settle on the navmesh
                Vector3 stand = player != null ? player.transform.position : placement.transform.position;
                Vector3 buildSpot = stand + new Vector3(0f, 0f, 1.5f);
                if (NavMesh.SamplePosition(buildSpot, out NavMeshHit bh, 4f, NavMesh.AllAreas)) buildSpot = bh.position;
                placement.RequestBuildAt(buildSpot, 0f);
                lit = campfire != null && campfire.IsLit;
            }
            Debug.Log("[CampfireVerifyCapture] campfire placed + lit: " + lit +
                      " (placed=" + (campfire != null && campfire.IsPlaced) + ")");
            for (int i = 0; i < 5; i++) yield return null;

            // 4. Stand at the lit fire and confirm warmth RISES (the loop closes). The build spot is within
            // warmRadius of the player, so restoreRate (>> decay) makes the bar climb.
            float warmthAtLight = warmth != null ? warmth.Current01 : -1f;
            float warmStart = Time.time;
            while (Time.time - warmStart < 4f) yield return null;
            float warmAfter = warmth != null ? warmth.Current01 : -1f;
            bool warmthRose = warmAfter > warmthAtLight + 0.02f; // measurably above noise
            Debug.Log("[CampfireVerifyCapture] warmth at light=" + warmthAtLight.ToString("0.00") +
                      " -> after=" + warmAfter.ToString("0.00") + " rose=" + warmthRose);

            for (int i = 0; i < 8; i++) yield return null;
            ShotTo(Path.Combine(dir, "loop_warm.png"));
            yield return null;
            yield return new WaitForSeconds(0.5f);

            bool loopClosed = lit && warmthRose;
            Debug.Log("[CampfireVerifyCapture] LOOP CLOSED=" + loopClosed +
                      " (lit=" + lit + ", warmthRose=" + warmthRose + ") -> " + dir);
            // Fail loud in the shipped build if the loop did not close — the build-side gate signal.
            Application.Quit(loopClosed ? 0 : 1);
        }

        private int Count(string id)
        {
            if (inventory == null || inventory.Model == null) return 0;
            return inventory.Model.CountItem(id);
        }

        private void GrantResource(string id, int amount)
        {
            if (inventory == null || inventory.Model == null || inventory.Catalog == null || amount <= 0) return;
            var def = inventory.Catalog.ById(id);
            if (def != null) inventory.Model.AddItem(def, amount);
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

        private void ShotTo(string file)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[CampfireVerifyCapture] no Camera.main — cannot capture " + file);
                return;
            }
            Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
            if (tex != null) Object.Destroy(tex);
            Debug.Log("[CampfireVerifyCapture] wrote " + file);
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
