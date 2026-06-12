using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the U2-4 campfire — and, via -verifyLoop, the FULL
    /// M-U2 survival cycle end-to-end in the BUILT exe (ticket 86ca8bdep). The strongest possible
    /// evidence the loop CLOSES in the shipped player, not just the editor (unity-conventions.md
    /// §editor-vs-runtime: editor evidence is necessary, never sufficient).
    ///
    /// -verifyLoop drives, in order, the whole spine through the real NavMesh + proximity seams:
    ///   1. let warmth DECAY a while at spawn (prove the need creates pressure)
    ///   2. MoveTo the craft spot      -> axe crafted (U2-2)
    ///   3. MoveTo the tree            -> wood yielded, tree felled (U2-3)
    ///   4. MoveTo the fire pit        -> campfire BUILT + LIT, wood paid (U2-4)
    ///   5. stand at the lit fire      -> warmth RESTORES (the loop closes)
    /// Captures loop_cold.png (warmth low, no fire) + loop_warm.png (at the lit fire, warmth climbing),
    /// asserts in the log that warmth measurably ROSE while at the fire, and quits non-zero if the loop
    /// did NOT close (the build-side failure signal). Sibling of ChopVerifyCapture.
    ///
    /// Inert unless launched with -verifyLoop (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyLoop -captureDir &lt;dir&gt;
    /// </summary>
    public class CampfireVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public WarmthNeed warmth;
        public Campfire campfire;
        public Vector3 craftSpot = new Vector3(8f, 0f, 6f);
        public Vector3 treeSpot = new Vector3(-9f, 0f, -7f);
        public Vector3 firePit = new Vector3(4f, 0f, -8f);
        // The fire's wood cost — the loop must carry ENOUGH wood to the pit, not just any wood. Wired from
        // CampfirePlacement.woodCost at bootstrap. We must stand at the tree until the FULL cost is chopped
        // (the tree felled), else we walk to the pit underfunded and the wood gate (correctly) refuses.
        public int woodCost = 3;
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyLoop"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (warmth == null) warmth = Object.FindAnyObjectByType<WarmthNeed>();
                if (campfire == null) campfire = Object.FindAnyObjectByType<Campfire>();
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

            // Let warmth decay a while at spawn so the 'cold' shot reads as real pressure.
            float decayStart = Time.time;
            while (Time.time - decayStart < 3f) yield return null;
            float coldWarmth = warmth != null ? warmth.Current01 : -1f;
            Debug.Log("[CampfireVerifyCapture] cold: warmth01=" + coldWarmth.ToString("0.00"));
            ShotTo(Path.Combine(dir, "loop_cold.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. Craft the axe.
            Drive(craftSpot, "craft spot");
            yield return WaitUntil(() => inventory != null && inventory.HasAxe, 12f);
            Debug.Log("[CampfireVerifyCapture] axe crafted: " + (inventory != null && inventory.HasAxe));

            // 3. Chop the tree for wood — wait until we've chopped ENOUGH to afford the fire (the tree
            // fells after chopsToFell chops; we need WoodCount >= woodCost). Waiting only for >0 walks us
            // to the pit after the FIRST chop, underfunded, and the wood gate correctly refuses.
            Drive(treeSpot, "tree");
            yield return WaitUntil(() => inventory != null && inventory.WoodCount >= woodCost, 16f);
            Debug.Log("[CampfireVerifyCapture] wood after chop: " + (inventory != null ? inventory.WoodCount : -1) +
                      " (need " + woodCost + " for the fire)");

            // 4. Build + light the campfire.
            Drive(firePit, "fire pit");
            yield return WaitUntil(() => campfire != null && campfire.IsLit, 14f);
            bool lit = campfire != null && campfire.IsLit;
            Debug.Log("[CampfireVerifyCapture] campfire lit: " + lit);

            // 5. Stand at the lit fire and confirm warmth RISES (the loop closes).
            float warmthAtLight = warmth != null ? warmth.Current01 : -1f;
            float warmStart = Time.time;
            while (Time.time - warmStart < 4f) yield return null; // restoreRate >> decay -> bar climbs
            float warmAfter = warmth != null ? warmth.Current01 : -1f;
            bool warmthRose = warmAfter > warmthAtLight + 0.02f; // measurably above noise
            Debug.Log("[CampfireVerifyCapture] warmth at light=" + warmthAtLight.ToString("0.00") +
                      " -> after=" + warmAfter.ToString("0.00") + " rose=" + warmthRose);

            for (int i = 0; i < 8; i++) yield return null;
            ShotTo(Path.Combine(dir, "loop_warm.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            bool loopClosed = lit && warmthRose;
            Debug.Log("[CampfireVerifyCapture] LOOP CLOSED=" + loopClosed +
                      " (lit=" + lit + ", warmthRose=" + warmthRose + ") -> " + dir);
            // Fail loud in the shipped build if the loop did not close — the build-side gate signal.
            Application.Quit(loopClosed ? 0 : 1);
        }

        private void Drive(Vector3 target, string label)
        {
            bool set = player != null && player.MoveTo(target);
            Debug.Log("[CampfireVerifyCapture] MoveTo " + label + " set: " + set + " target=" + target);
        }

        private IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
        {
            float start = Time.time;
            while (Time.time - start < timeout)
            {
                if (cond()) yield break;
                yield return null;
            }
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
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
