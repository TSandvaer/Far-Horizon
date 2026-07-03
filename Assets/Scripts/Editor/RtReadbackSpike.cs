using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// AC1 SPIKE (86cag93zb): prove the RenderTexture-readback capture pattern produces a VALID
    /// (non-black, content-correct) frame HEADLESS — with NO window / swapchain — the load-bearing
    /// unknown for removing the 1-runner capture pin. The investigation flagged RT-readback as
    /// technically sound but it had NEVER been prototyped on THIS codebase; this proves/refutes it.
    ///
    /// This is an EDIT-mode probe (deliberately NOT PlayMode: local -batchmode PlayMode DEADLOCKS at
    /// play-mode-enter on this machine — unity-conventions.md). Edit-mode exercises the SAME API
    /// sequence the real gate would (RenderTexture + Camera.Render + Texture2D.ReadPixels +
    /// EncodeToPNG) and is decisive on the load-bearing question: is a graphics device present +
    /// does an offscreen render read back non-black, headless?
    ///
    /// It builds a minimal deterministic URP scene (a sky-coloured clear + three Unlit coloured cubes
    /// → guaranteed luminance + spatial variance + NO magenta), renders the camera to an offscreen
    /// RenderTexture via RenderTextureCapture, writes rt_capture_00.png, and logs a machine-greppable
    /// manifest line (incl. the graphics device type + active render pipeline). frame_check.py then
    /// judges the PNG OUT OF ENGINE, exactly as it judges the real capture gate — same authoritative
    /// black/uniform/magenta gate, decoupled from HOW the PNG was produced.
    ///
    ///   Unity -batchmode [-nographics] -quit -projectPath . \
    ///     -executeMethod FarHorizon.EditorTools.RtReadbackSpike.Run \
    ///     -captureDir ci-out/rt-spike -logFile ci-out/rt-spike.log
    /// </summary>
    public static class RtReadbackSpike
    {
        public static void Run()
        {
            try
            {
                string dir = ResolveDir();
                Directory.CreateDirectory(dir);
                bool nullDevice = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
                Debug.Log($"[RTSpike] start device={SystemInfo.graphicsDeviceType} " +
                          $"nullDevice={nullDevice} " +
                          $"pipeline={(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "NULL(built-in)")} " +
                          $"dir={dir}");

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camGo = new GameObject("SpikeCam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.30f, 0.55f, 0.85f); // sky-ish, non-black clear
                cam.transform.position = new Vector3(0f, 1.5f, -6f);
                cam.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

                // Unlit URP shader: no lighting dependency (isolates the device/readback question)
                // and — critically — a CORRECT shader, so a valid frame is never mistaken for a
                // magenta shader-strip by frame_check.py.
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit == null)
                    Debug.LogError("[RTSpike] URP/Unlit shader NOT found — active pipeline may not be URP; frame would be magenta");

                MakeCube(unlit, new Color(0.90f, 0.30f, 0.20f), new Vector3(-2.2f, 0.5f, 0f));
                MakeCube(unlit, new Color(0.20f, 0.80f, 0.35f), new Vector3(0.0f, 0.9f, 0f));
                MakeCube(unlit, new Color(0.95f, 0.85f, 0.20f), new Vector3(2.2f, 0.3f, 0f));

                string outPath = Path.Combine(dir, "rt_capture_00.png");
                bool ok = FarHorizon.RenderTextureCapture.CaptureCameraToPng(cam, 512, 512, outPath);
                if (ok)
                    Debug.Log($"[RTSpike] complete ok=True -> {outPath}");
                else
                    Debug.LogError($"[RTSpike] complete ok=False (capture failed) -> {outPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RTSpike] EXCEPTION {e}");
            }
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

        private static string ResolveDir()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            return Path.GetFullPath("ci-out/rt-spike");
        }
    }
}
