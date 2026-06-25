using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the U2-3 chop interaction (ticket 86ca8bdd8).
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
    /// Inert unless launched with -verifyChop (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyChop -captureDir &lt;dir&gt;
    /// Captures: chop_before.png (at spawn, no wood) + chop_after.png (at the tree, wood in readout),
    /// then quits non-zero if NO wood was yielded (the build-side failure signal).
    /// </summary>
    public class ChopVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public ChopTree chop;
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

            // 2. First get the axe — drive to the craft spot and wait for HasAxe (U2-2's seam).
            bool setCraft = player != null && player.MoveTo(craftSpot);
            Debug.Log("[ChopVerifyCapture] MoveTo craft spot set: " + setCraft + " target=" + craftSpot);
            float start = Time.time;
            while (Time.time - start < 12f)
            {
                if (inventory != null && inventory.HasAxe) break;
                yield return null;
            }
            Debug.Log("[ChopVerifyCapture] axe crafted: " + (inventory != null && inventory.HasAxe));

            // 3. Now chop — drive to the tree, then (CHANGE 1) drive LEFT-CLICK chop requests once in range
            // until wood is yielded. The chop is per-click now, so standing at the tree alone does nothing —
            // RequestChopClick() is the input-independent analog of a real left-click (range + axe-selected +
            // over-UI/RMB guards still apply, exactly like a real click). Request a chop EACH frame while at
            // the tree (the click cooldown paces the actual chops); the chop is a no-op until truly in range.
            bool setTree = player != null && player.MoveTo(treeSpot);
            Debug.Log("[ChopVerifyCapture] MoveTo tree set: " + setTree + " target=" + treeSpot);
            start = Time.time;
            while (Time.time - start < 14f)
            {
                if (inventory != null && inventory.WoodCount > 0) break;
                // Drive the left-click chop (CHANGE 1). Harmless until the player is actually in range with the
                // selected axe — mirrors a real off-target click being ignored.
                if (chop != null) chop.RequestChopClick();
                yield return null;
            }
            bool gotWood = inventory != null && inventory.WoodCount > 0;
            Debug.Log("[ChopVerifyCapture] wood yielded: " + gotWood + " (wood=" +
                      (inventory != null ? inventory.WoodCount : -1) +
                      ", true means click-move REACHED the tree AND the left-click-triggered, axe-gated chop fired)");

            // Let the camera settle, then capture the 'after' shot with wood in the readout.
            for (int i = 0; i < 8; i++) yield return null;
            ShotTo(Path.Combine(dir, "chop_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[ChopVerifyCapture] verification complete -> " + dir + " gotWood=" + gotWood);
            // Fail loud in the shipped build if no wood was ever yielded — the build-side gate signal.
            Application.Quit(gotWood ? 0 : 1);
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
