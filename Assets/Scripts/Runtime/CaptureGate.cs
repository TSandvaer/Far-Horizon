using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// STANDARD shipped-build capture component (the testing-bar's capture gate, ticket 86ca86g7k).
    ///
    /// The testing bar (team/TESTING_BAR.md §3) requires evidence captured from the BUILT exe, not
    /// the editor — the editor-vs-runtime divergence class (Awake-no-serialize, shader stripping,
    /// NavMesh-not-shipped; unity-conventions.md) is proven by spike incidents (iter6 "legs-up").
    /// The one-off capture hooks (BootScreenshot `-shot`, MovementVerifyCapture `-verifyMove`) each
    /// reinvented "render N frames windowed, write a PNG, quit". This is the STANDARD, reusable
    /// version every future PR's capture step uses: a fixed-cadence multi-frame capture that the
    /// out-of-engine frame_check.py gate then inspects for black/empty frames.
    ///
    /// Inert unless launched with -captureGate (so the normal game / boot launch is unaffected).
    /// MUST run WINDOWED, not -batchmode: ScreenCapture.CaptureScreenshot returns
    /// "Failed to capture screen shot" under -batchmode (no swapchain / no real GPU frame) —
    /// spike iter-4 lesson (FINDINGS.txt). That is exactly the point: a real rendered frame from
    /// the shipped player is the only evidence the bar trusts.
    ///
    ///   FarHorizon.exe -screen-fullscreen 0 -captureGate [-captureFrames N] [-captureDir <dir>]
    ///
    /// Writes capture_00.png .. capture_{N-1}.png (HUD build-stamp visible in each), logs a
    /// machine-greppable manifest line per frame, then quits. The PASS/FAIL decision is made
    /// OUT of engine by frame_check.py (a black/empty/all-magenta frame fails) — an in-engine
    /// self-assert could not prove the swapchain actually rendered, which is the whole gate.
    /// </summary>
    public class CaptureGate : MonoBehaviour
    {
        public string subDir = "Captures";
        public int defaultFrames = 4;

        // How many real frames to let render before the FIRST capture, so the scene/HUD/clear
        // colour are present (the boot capture's 5-frame warm-up earned its keep).
        public int warmupFrames = 6;
        // Frames to wait between captures so any animation/camera settle advances between shots.
        public int framesBetween = 8;

        void Start()
        {
            if (HasArg("-captureGate"))
                StartCoroutine(RunCaptureGate());
        }

        private IEnumerator RunCaptureGate()
        {
            int frames = ResolveFrameCount();
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            Debug.Log($"[CaptureGate] start frames={frames} dir={dir} stamp={BuildInfo.Stamp}");

            // Warm-up so the first shot has real content, not a blank first-frame backbuffer.
            for (int i = 0; i < warmupFrames; i++) yield return null;

            for (int n = 0; n < frames; n++)
            {
                string file = Path.Combine(dir, $"capture_{n:00}.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                // The frame_check.py gate greps this exact line to know which files to inspect.
                Debug.Log($"[CaptureGate] wrote frame {n} -> {file}");

                // Let the capture flush to disk + advance a few frames before the next shot.
                yield return new WaitForEndOfFrame();
                yield return null;
                for (int i = 0; i < framesBetween; i++) yield return null;
            }

            // Final flush before quit so the last PNG is fully written.
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[CaptureGate] complete frames={frames} -> {dir}");
            Application.Quit();
        }

        private int ResolveFrameCount()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-captureFrames" &&
                    int.TryParse(args[i + 1], out int n) && n > 0)
                    return n;
            }
            return Mathf.Max(1, defaultFrames);
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
