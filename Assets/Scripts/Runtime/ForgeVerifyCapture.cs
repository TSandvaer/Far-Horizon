using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the FORGE build + SMELT interaction (ticket 86cakkmvc / I-3).
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving the forge
    /// loop works in the BUILT exe, not just the editor. The direct sibling of <see cref="MineVerifyCapture"/> /
    /// <see cref="ChopVerifyCapture"/>: it drives the full loop seam — wait for the agent on the NavMesh, GRANT the
    /// build + smelt mats (wood + stone + iron-ore) into the pack, teleport to the forge build spot, BUILD the forge
    /// (proximity gate), then SMELT (proximity auto-tend) until an iron-INGOT lands — then asserts the ingot count
    /// rose, proving end-to-end "gather mats → build furnace → smelt ore → iron ingot" in the shipped player with
    /// the HUD build stamp visible.
    ///
    /// To keep the headless capture fast, it dials the forge's smelt to a SHORT time + a 1-ore/1-fuel batch (the
    /// CONVERSION is what's under test, not the 12s wall — the Sponsor soak judges the real Medium timing). Inert
    /// unless launched with -verifyForge. HEADLESS via RT-readback (86cag93zb): captures render Camera.main into an
    /// offscreen RT, so it runs under -batchmode (no window). Self-asserts are LOGIC (iron_ingot count).
    ///   FarHorizon.exe -batchmode -verifyForge -captureDir &lt;dir&gt;
    /// Captures: forge_before.png (unbuilt) + forge_built.png (raised) + forge_after.png (an ingot in the pack),
    /// then quits non-zero if the build→smelt→ingot proof failed.
    /// </summary>
    public class ForgeVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public Forge forge;
        public ForgePlacement placement;
        public string subDir = "Captures";
        public int captureWidth = 1280;
        public int captureHeight = 720;

        void Start()
        {
            if (HasArg("-verifyForge"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (forge == null) forge = Object.FindAnyObjectByType<Forge>();
                if (placement == null) placement = Object.FindAnyObjectByType<ForgePlacement>();
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
            Debug.Log("[ForgeVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            int ingotBefore = IngotCount();
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log("[ForgeVerifyCapture] before: iron_ingot=" + ingotBefore + " built=" +
                      (forge != null && forge.IsBuilt));
            ShotTo(Path.Combine(dir, "forge_before.png"));
            yield return null;

            // 2. GRANT the mats: enough wood for the build + one fuel, stone for the build, one ore for the smelt.
            int woodCost = placement != null ? placement.woodCost : 4;
            int stoneCost = placement != null ? placement.stoneCost : 5;
            GrantResource(ItemCatalog.WoodId, woodCost + 2);
            GrantResource(ItemCatalog.StoneId, stoneCost);
            GrantResource(ItemCatalog.IronOreId, 2);
            // Dial the smelt SHORT + a cheap 1-ore/1-fuel batch so the headless capture completes fast (the
            // conversion is under test, not the wall-clock duration — the Sponsor soak judges the real timing).
            if (forge != null)
            {
                forge.SetOrePerIngot(1);
                forge.SetFuelPerSmelt(1);
                forge.SetSmeltSeconds(SettingsCatalog.SmeltTimeMin); // the floor (fast) for the capture
            }
            Debug.Log("[ForgeVerifyCapture] granted mats: wood=" + Count(ItemCatalog.WoodId) + " stone=" +
                      Count(ItemCatalog.StoneId) + " iron_ore=" + Count(ItemCatalog.IronOreId));

            // 3. Teleport to the forge build spot + let ForgePlacement's proximity gate build it.
            bool built = false;
            if (placement != null)
            {
                TeleportPlayer(placement.transform.position);
                float bstart = Time.time;
                while (Time.time - bstart < 6f && !placement.HasBuilt)
                {
                    // Proximity build fires in ForgePlacement.Update; force it too (harmless if already built).
                    placement.TryBuild();
                    yield return null;
                }
                built = placement.HasBuilt || (forge != null && forge.IsBuilt);
            }
            Debug.Log("[ForgeVerifyCapture] forge built: " + built);
            for (int i = 0; i < 5; i++) yield return null;
            ShotTo(Path.Combine(dir, "forge_built.png"));
            yield return null;

            // 4. Smelt: standing at the built forge auto-begins a batch; drive RequestSmelt too so a headless rig
            // that mis-reads proximity still starts one. Wait for an ingot to land.
            bool gotIngot = false;
            if (built)
            {
                float sstart = Time.time;
                while (Time.time - sstart < 20f)
                {
                    if (IngotCount() > ingotBefore) break;
                    if (forge != null && !forge.IsSmelting) forge.RequestSmelt();
                    yield return null;
                }
                gotIngot = IngotCount() > ingotBefore;
                Debug.Log("[ForgeVerifyCapture] smelted ingot: " + gotIngot + " (iron_ingot " + ingotBefore +
                          " -> " + IngotCount() + ", batches=" + (forge != null ? forge.CompletedSmelts : 0) + ")");
                for (int i = 0; i < 8; i++) yield return null;
                ShotTo(Path.Combine(dir, "forge_after.png"));
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            bool pass = built && gotIngot;
            Debug.Log("[ForgeVerifyCapture] verification complete -> " + dir +
                      " built=" + built + " gotIngot=" + gotIngot + " => PASS=" + pass);
            Application.Quit(pass ? 0 : 1);
        }

        private int IngotCount() => Count(ItemCatalog.IronIngotId);

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
                Debug.LogError("[ForgeVerifyCapture] no Camera.main — cannot capture " + file);
                return;
            }
            Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
            if (tex != null) Object.Destroy(tex);
            Debug.Log("[ForgeVerifyCapture] wrote " + file);
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
