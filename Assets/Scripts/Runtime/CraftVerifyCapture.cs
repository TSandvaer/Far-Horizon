using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the U2-2 craft interaction (ticket 86ca8bdaq).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving
    /// the craft works in the BUILT exe, not just the editor. Sibling of MovementVerifyCapture: it waits
    /// for the agent to land on the NavMesh, programmatically drives MoveTo(craftSpot) (the same seam a
    /// real click uses), waits for the CraftSpot's proximity recipe to fire, then captures — proving
    /// end-to-end "click-move to the spot -> axe crafted -> readout shows it" in the shipped player, with
    /// the HUD build stamp visible. Asserts HasAxe in the log so a headless run is auditable.
    ///
    /// Inert unless launched with -verifyCraft (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyCraft -captureDir &lt;dir&gt;
    /// Captures: craft_before.png (at spawn, no axe) + craft_after.png (at the spot, axe in readout),
    /// then quits non-zero if the axe was NOT crafted (the build-side failure signal).
    /// </summary>
    public class CraftVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public Inventory inventory;
        public Vector3 craftSpot = new Vector3(8f, 0f, 6f);
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyCraft"))
            {
                if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
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
            Debug.Log("[CraftVerifyCapture] agent on NavMesh: " + onMesh + " after " + t.ToString("0.00") + "s");

            // 'before' shot: at spawn, no axe yet.
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log("[CraftVerifyCapture] before: HasAxe=" + (inventory != null && inventory.HasAxe));
            ShotTo(Path.Combine(dir, "craft_before.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. Drive the player to the craft spot (the input-independent seam a real LMB click calls).
            bool set = player != null && player.MoveTo(craftSpot);
            Debug.Log("[CraftVerifyCapture] MoveTo craft spot set: " + set + " target=" + craftSpot);

            // 3. Wait for the craft to fire (CraftSpot's proximity recipe) over a real wall-clock window.
            float start = Time.time;
            while (Time.time - start < 12f)
            {
                if (inventory != null && inventory.HasAxe) break;
                yield return null;
            }
            bool crafted = inventory != null && inventory.HasAxe;
            Debug.Log("[CraftVerifyCapture] axe crafted: " + crafted +
                      " (true means click-move REACHED the spot AND the recipe fired)");

            // Let the camera settle, then capture the 'after' shot with the axe in the readout.
            for (int i = 0; i < 8; i++) yield return null;
            ShotTo(Path.Combine(dir, "craft_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[CraftVerifyCapture] verification complete -> " + dir + " crafted=" + crafted);
            // Fail loud in the shipped build if the craft never happened — the build-side gate signal.
            Application.Quit(crafted ? 0 : 1);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[CraftVerifyCapture] wrote " + file);
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
