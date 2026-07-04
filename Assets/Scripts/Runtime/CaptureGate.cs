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
    /// HEADLESS via RT-readback (86cag93zb): renders Camera.main full-pipeline into an offscreen
    /// RenderTexture (RenderTextureCapture.CaptureCameraToTexture → SubmitRenderRequest) and reads
    /// the pixels back — so it runs under -batchmode with NO window/swapchain. This replaces the old
    /// ScreenCapture.CaptureScreenshot backbuffer path, which returns "Failed to capture screen shot"
    /// under -batchmode (empirically re-confirmed 2026-07-03) and thus required a windowed launch.
    ///
    ///   FarHorizon.exe -batchmode -captureGate [-captureFrames N] [-captureDir <dir>]
    ///
    /// Writes capture_00.png .. capture_{N-1}.png (the gameplay SCENE frame — an RT captures the
    /// camera's render only, so the HUD build-stamp OVERLAY is NOT in the frame; the stamp is
    /// verified separately by verify_build_stamp.py), logs a manifest line per frame, then quits.
    /// The PASS/FAIL decision is made OUT of engine by frame_check.py (a black/empty/all-magenta
    /// frame fails) — the editor-vs-runtime backstop the testing bar requires.
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
        // RT-readback capture resolution (86cag93zb). Fixed for determinism (the old windowed launch used
        // 1280x720); under -batchmode there is no window whose size to inherit, so the RT size is explicit.
        public int captureWidth = 1280;
        public int captureHeight = 720;

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

            Debug.Log($"[CaptureGate] start frames={frames} dir={dir} stamp={BuildInfo.Stamp} " +
                      $"device={SystemInfo.graphicsDeviceType}");

            // Warm-up so the first shot has real content (scene/lighting/clear settled).
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // HEADLESS RT-READBACK (86cag93zb): render Camera.main (the gameplay orbit cam) full-pipeline
            // into an offscreen RenderTexture and write the PNG. This works under -batchmode (no swapchain),
            // unlike ScreenCapture.CaptureScreenshot which reads the backbuffer ("Failed to capture screen
            // shot" headless — empirically confirmed). NOTE: an RT captures ONLY what the CAMERA renders —
            // the HUD build-stamp (a screen-overlay) is NOT in the frame; frame_check gates on scene CONTENT
            // (non-black/varied/non-magenta), and the stamp is verified separately (verify_build_stamp.py).
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[CaptureGate] no Camera.main — cannot capture the gameplay frame " +
                               "(build-side regression: no camera tagged MainCamera in Boot.unity)");
                Application.Quit(1);
                yield break;
            }

            for (int n = 0; n < frames; n++)
            {
                string file = Path.Combine(dir, $"capture_{n:00}.png");
                Texture2D tex = RenderTextureCapture.CaptureCameraToTexture(cam, captureWidth, captureHeight, file);
                if (tex != null) Object.Destroy(tex);
                // The frame_check.py gate scans the dir for these PNGs; the line is a diagnostic trail.
                Debug.Log($"[CaptureGate] wrote frame {n} -> {file}");

                // Advance a few frames before the next shot so any animation/camera settle advances.
                for (int i = 0; i < framesBetween; i++) yield return null;
            }

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
