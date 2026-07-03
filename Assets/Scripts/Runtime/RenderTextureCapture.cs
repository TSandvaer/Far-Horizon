using System.IO;
using UnityEngine;

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
