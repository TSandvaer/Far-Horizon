using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Boot-scene verification capture. When the player is launched with -shot it grabs one
    /// screenshot then quits, so the headless build pipeline can prove the shipped exe actually
    /// launches and renders a frame WITHOUT a human eyeballing the window.
    ///
    /// MUST run WINDOWED, not -batchmode: ScreenCapture.CaptureScreenshot returns
    /// "Failed to capture screen shot" when the standalone player runs with -batchmode (no
    /// swapchain / no real GPU frame) — the spike's iter-4 lesson (FINDINGS.txt). Application.Quit()
    /// at the end so the verification run terminates instead of hanging the window.
    ///
    /// Args:
    ///   -shot                 enable capture-then-quit mode (default: idle, no capture)
    ///   -captureDir &lt;path&gt;    output directory (default: &lt;exe-dir&gt;/Captures)
    /// </summary>
    public class BootScreenshot : MonoBehaviour
    {
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-shot"))
                StartCoroutine(CaptureThenQuit());
        }

        private IEnumerator CaptureThenQuit()
        {
            // Let the first frames render so the capture has real content (HUD + clear color).
            for (int i = 0; i < 5; i++) yield return null;

            string dir = ResolveDir();
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "boot_launch.png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[BootScreenshot] wrote " + file);

            // Let the capture flush to disk before quitting.
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[BootScreenshot] capture complete -> " + dir);
            Application.Quit();
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
