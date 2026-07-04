using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarHorizon
{
    /// <summary>
    /// RT-readback capture helper (SPIKE, AC1 of 86cag93zb).
    ///
    /// The standard CaptureGate path (ScreenCapture.CaptureScreenshot) reads the BACKBUFFER, which
    /// requires a real Win32 swapchain / present loop — that is WHY the capture gate must launch the
    /// exe WINDOWED (-screen-fullscreen 0) and WHY a 2nd online CI runner breaks captures (GPU /
    /// compositor contention on the shared present path; A/B-confirmed, runner pinned to runner-1).
    ///
    /// This helper renders a camera into an OFFSCREEN RenderTexture and reads the pixels back from
    /// the RT — NO backbuffer, NO swapchain, NO window — so (hypothesis) it can run headless without
    /// the windowed dependency that causes the contention. AC1 proves whether it produces a VALID
    /// (non-black, content-correct) frame under -batchmode on THIS codebase. AC2/AC3/AC4 (OUT OF
    /// SCOPE for the spike) then wire it into CaptureGate + the ci.yml gates and unpin the runner.
    /// </summary>
    public static class RenderTextureCapture
    {
        /// <summary>
        /// Render <paramref name="cam"/> into a fresh width x height RenderTexture, read the pixels
        /// back on the CPU, encode a PNG and write it to <paramref name="path"/>. Returns true on a
        /// successful write. Touches neither the backbuffer nor a window — the whole point of the
        /// spike. Restores the camera's prior target + the prior active RT and frees both temporaries.
        /// </summary>
        public static bool CaptureCameraToPng(Camera cam, int width, int height, string path)
        {
            if (cam == null) { Debug.LogError("[RTCapture] null camera"); return false; }
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            // 24-bit depth so opaque + depth-tested geometry resolves; sRGB so the CPU readback
            // matches what a windowed backbuffer capture would have shown for the same scene.
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB);
            rt.Create();

            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render(); // explicit offscreen render — no swapchain, no present loop

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                string full = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);
                Debug.Log($"[RTCapture] wrote {width}x{height} -> {full} ({png.Length} bytes) " +
                          $"device={SystemInfo.graphicsDeviceType}");
                return true;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                Destroy(tex);
                rt.Release();
                Destroy(rt);
            }
        }

        /// <summary>
        /// FULL-PIPELINE offscreen capture (AC2 of 86cag93zb; folds Drew's #248 N1). Renders
        /// <paramref name="cam"/> into an offscreen RenderTexture via
        /// <see cref="RenderPipeline.SubmitRenderRequest{T}"/> — the URP-authored path that runs the
        /// WHOLE scriptable render pipeline for the camera (opaque/transparent passes, post-processing
        /// Volume — bloom/tonemap/grading — AND custom Renderer Features), then reads the pixels back
        /// and writes a PNG. This is the false-green fix for N1: a bare <c>Camera.Render()</c> into an
        /// RT can SKIP post-processing / renderer-feature passes on URP, so a frame_check-green capture
        /// would NOT prove the shipped gameplay look survived. SubmitRenderRequest is the API URP
        /// exposes precisely to render a camera's FULL result to an arbitrary target off the backbuffer.
        ///
        /// Falls back to <see cref="CaptureCameraToPng"/>'s <c>cam.Render()</c> path only when the active
        /// pipeline does not support a StandardRequest for this camera (e.g. built-in RP in a stray editor
        /// context) — logged so a silent downgrade is visible. Touches neither the backbuffer nor a window;
        /// this is the headless (-batchmode, NO -nographics, NO windowed swapchain) capture the gates use.
        ///
        /// IMPORTANT — this captures ONLY what the CAMERA renders (scene + post + renderer features). It
        /// does NOT contain ScreenSpace-Overlay uGUI, IMGUI/OnGUI, or screen-target UI Toolkit panels —
        /// those composite onto the SCREEN after all cameras, never into a camera's target texture. Gates
        /// whose deliverable is an overlay (loot IMGUI band, settings UI Toolkit panel, inventory drag
        /// ghost) MUST route their panel to this RT (PanelSettings.targetTexture) or stay windowed — a
        /// camera-RT capture alone would silently drop the very overlay they gate on. (86cag93zb)
        /// </summary>
        public static bool CaptureCameraFullPipelineToPng(Camera cam, int width, int height, string path)
        {
            Texture2D tex = CaptureCameraToTexture(cam, width, height, path);
            if (tex == null) return false;
            Destroy(tex);
            return true;
        }

        /// <summary>
        /// The reusable AC2 core: render <paramref name="cam"/> full-pipeline into an offscreen RT (see
        /// <see cref="CaptureCameraFullPipelineToPng"/> for the SubmitRenderRequest rationale + the
        /// overlay caveat), read the pixels back into a fresh RGB24 <see cref="Texture2D"/>, optionally
        /// write a PNG to <paramref name="pngPath"/>, and RETURN the readback texture so a caller can run
        /// its own perceptual self-asserts on the SAME frame it captured (pond fresh-blue, sun disk, etc.).
        ///
        /// Returns null on failure (null camera / render failure). On success the CALLER OWNS the returned
        /// texture and MUST <c>Destroy</c> it. The readback uses <c>RenderTexture.active = rt</c> +
        /// <c>ReadPixels</c>, so the returned texture's coordinate origin is BOTTOM-LEFT — identical to the
        /// old backbuffer <c>ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0)</c> the verify
        /// components used, so their existing sample-band math carries over UNCHANGED when they read
        /// <c>tex.width</c>/<c>tex.height</c> instead of <c>Screen.width</c>/<c>Screen.height</c>.
        ///
        /// This is SYNCHRONOUS (SubmitRenderRequest renders on demand), so callers do NOT need
        /// <c>WaitForEndOfFrame</c> — which is load-bearing headless: <c>WaitForEndOfFrame</c> never
        /// resumes under -batchmode (unity-conventions.md), so the RT path both fixes the capture AND
        /// removes the batchmode-hostile yield the old backbuffer path depended on.
        /// </summary>
        public static Texture2D CaptureCameraToTexture(Camera cam, int width, int height, string pngPath = null)
        {
            if (cam == null) { Debug.LogError("[RTCapture] null camera"); return null; }
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB);
            rt.Create();

            RenderTexture prevActive = RenderTexture.active;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            bool ok = false;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    // No URP StandardRequest support (built-in RP / unusual context) — fall back to the
                    // plain Camera.Render into the RT so the capture still produces a frame, but SAY SO
                    // (the fallback skips URP post/renderer-features — a fidelity downgrade, not silent).
                    Debug.LogWarning("[RTCapture] SubmitRenderRequest unsupported for this camera/pipeline " +
                                     "— falling back to Camera.Render (post-processing/renderer-features may " +
                                     "be skipped). device=" + SystemInfo.graphicsDeviceType);
                    RenderTexture prevTarget = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = prevTarget;
                }

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                ok = true;

                if (!string.IsNullOrEmpty(pngPath))
                {
                    byte[] png = tex.EncodeToPNG();
                    string full = Path.GetFullPath(pngPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllBytes(full, png);
                    Debug.Log($"[RTCapture] (full-pipeline) wrote {width}x{height} -> {full} ({png.Length} bytes) " +
                              $"device={SystemInfo.graphicsDeviceType}");
                }
            }
            finally
            {
                RenderTexture.active = prevActive;
                rt.Release();
                Destroy(rt);
            }

            if (!ok) { Destroy(tex); return null; }
            return tex;
        }

        // Destroy is illegal from edit-mode (the spike runs edit-mode headless) — route accordingly
        // so the SAME helper is reusable from the player at runtime (AC2) without spewing errors.
        private static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
