using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarHorizon
{
    /// <summary>
    /// AC3-FIRST GATE probe (86cag93zb): prove the SHIPPED STANDALONE PLAYER initializes a REAL GPU
    /// device under <c>-batchmode</c> (NO <c>-nographics</c>, NO windowed swapchain) and that an
    /// offscreen RenderTexture-readback frame is VALID (non-black, content-correct) — the load-bearing
    /// unknown the #248 spike did NOT prove (the spike only exercised EDITOR-batchmode; a built Mono
    /// player is a different runtime and could plausibly come up on a Null device).
    ///
    /// If the player inits a Null device here, the whole RT-readback headless-capture plan is dead for
    /// the standalone player and the refactor MUST NOT proceed — so this probe is the STOP/GO gate that
    /// runs BEFORE any gate conversion.
    ///
    /// SELF-CONTAINED: auto-spawns via [RuntimeInitializeOnLoadMethod] when launched with
    /// <c>-probeRtDevice</c> — it does NOT depend on Boot.unity wiring, so it works no matter which
    /// scene the player boots. It builds its own camera (Skybox clear + URP post-processing so the
    /// FULL-pipeline SubmitRenderRequest path is exercised, not a bare clear) + three Unlit coloured
    /// cubes (guaranteed luminance + spatial variance + NO magenta), renders the camera into an
    /// offscreen RT via <see cref="RenderTextureCapture.CaptureCameraFullPipelineToPng"/>, writes
    /// rt_probe_00.png, logs a machine-greppable manifest line (incl. the graphics device type), then
    /// <c>Application.Quit(0)</c> iff the device is non-Null AND the capture wrote — else <c>Quit(1)</c>.
    /// frame_check.py judges the PNG OUT OF ENGINE, exactly as it judges the real gates.
    ///
    ///   FarHorizon.exe -batchmode -probeRtDevice [-captureDir &lt;dir&gt;] -logFile &lt;log&gt;
    ///
    /// (No -nographics, no -screen-fullscreen 0 — that is the whole point: headless, window-less.)
    /// Inert unless launched with -probeRtDevice; the normal game / boot launch is unaffected.
    /// No mutable runtime static (the RuntimeInitializeOnLoadMethod only reads argv + spawns a GO), so
    /// no SubsystemRegistration reset is required (unity-conventions.md §Configurable Enter Play Mode).
    /// </summary>
    public class BatchmodeRtProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (!HasArg("-probeRtDevice")) return;
            var go = new GameObject("BatchmodeRtProbe");
            go.AddComponent<BatchmodeRtProbe>();
        }

        private IEnumerator Start()
        {
            int exit = 1;
            try
            {
                string dir = ResolveDir();
                Directory.CreateDirectory(dir);

                bool nullDevice = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
                string pipeline = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.name : "NULL(built-in)";
                Debug.Log($"[RtProbe] start device={SystemInfo.graphicsDeviceType} nullDevice={nullDevice} " +
                          $"pipeline={pipeline} batchmode={Application.isBatchMode} dir={dir}");

                if (nullDevice)
                {
                    // DECISIVE FAILURE: the shipped player came up on a Null device under -batchmode.
                    // RT-readback cannot render on a Null device → the headless plan is dead for the
                    // player. Fail loud + non-zero so the STOP/GO gate reports NO-GO.
                    Debug.LogError("[RtProbe] FAIL — shipped player initialized a NULL graphics device under " +
                                   "-batchmode. RT-readback offscreen capture is NOT possible in the standalone " +
                                   "player; the gate refactor MUST NOT proceed. (Likely cause: the player needs a " +
                                   "real GPU device flag the editor-batchmode spike had implicitly.)");
                    Application.Quit(1);
                    yield break;
                }

                // Build a self-contained deterministic scene: a camera with the GAMEPLAY render path
                // (Skybox/SolidColor clear + URP post) + three Unlit coloured cubes.
                var camGo = new GameObject("RtProbeCam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.30f, 0.55f, 0.85f); // sky-ish non-black clear
                cam.fieldOfView = 45f;
                cam.transform.position = new Vector3(0f, 1.5f, -6f);
                cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true; // exercise the FULL URP post path via SubmitRenderRequest

                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null)
                    Debug.LogError("[RtProbe] URP/Unlit shader NOT found — active pipeline may not be URP; " +
                                   "frame would be magenta");
                MakeCube(unlit, new Color(0.90f, 0.30f, 0.20f), new Vector3(-2.2f, 0.5f, 0f));
                MakeCube(unlit, new Color(0.20f, 0.80f, 0.35f), new Vector3(0.0f, 0.9f, 0f));
                MakeCube(unlit, new Color(0.95f, 0.85f, 0.20f), new Vector3(2.2f, 0.3f, 0f));

                // Let a few frames tick so the pipeline + shaders are fully warm before the render request.
                for (int i = 0; i < 4; i++) yield return null;

                string outPath = Path.Combine(dir, "rt_probe_00.png");
                bool ok = RenderTextureCapture.CaptureCameraFullPipelineToPng(cam, 512, 512, outPath);
                Debug.Log($"[RtProbe] complete ok={ok} device={SystemInfo.graphicsDeviceType} -> {outPath}");
                exit = ok ? 0 : 1;
            }
            finally
            {
                // finally cannot yield; quit is issued after the try so the frame flushes first.
            }

            yield return null;
            Application.Quit(exit);
        }

        private static void MakeCube(Shader unlit, Color c, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos;
            if (unlit != null)
            {
                var m = new Material(unlit);
                m.SetColor("_BaseColor", c);
                go.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
        }

        private string ResolveDir()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", "Captures")
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Captures");
            return Path.GetFullPath(baseDir);
        }

        private static bool HasArg(string flag)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
