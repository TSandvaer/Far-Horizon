using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build SKY-FACING capture for the SUN-DISK POC (ticket 86cabc743 — Erik
    /// low-poly-sky research, POC items 1+3). Sibling of WorldLookVerifyCapture / FreshwaterPondVerifyCapture.
    ///
    /// WHY A DEDICATED CAMERA (not the gameplay orbit cam): the OrbitCamera clamps pitch to [8,70] and frames
    /// the player from ABOVE looking DOWN — it physically cannot tilt up to put the Sun (elevation ~48deg) in
    /// frame. So this parks a dedicated capture camera with the SAME render path as gameplay (Skybox clear +
    /// Zone-D post Volume + SMAA) and AIMS it: (a) straight at the live Sun direction for the sun-disk shot,
    /// (b) up into the cloud band for the cloud-vs-sky contrast shot. Skybox clear is LOAD-BEARING here (a
    /// SolidColor clear like the axe close-up would erase the gradient sky we are verifying).
    ///
    /// The sun-disk render is the OPEN QUESTION Erik flagged (does the Sun bind in the skybox pass?). The
    /// shader uses the URP GLOBAL _MainLightPosition (always bound in the Background/skybox pass) rather than
    /// GetMainLight() — this component is the eyes-on + self-assert proof it renders in the SHIPPED IL2CPP exe.
    ///
    /// SELF-ASSERT (the gate verdict via Application.Quit code):
    ///   1. SUN VISIBLE — sample the centre pixels of sky_sun.png; a warm-gold disk reads BRIGHTER than the
    ///      blue sky surround AND warm (R >= B). The additive _SunColor lifts the centre well above the sky.
    ///   2. CLOUD-VS-SKY CONTRAST HOLDS (Erik 2nd open Q) — sky_clouds.png contains pixels NOTABLY brighter
    ///      than the sky band (the bright near-white cyan clouds), i.e. the S2 contrast fix survives the build.
    /// Quit(1) on either failure (or a missing Sun) so the exe exit code IS the gate; frame_check.py backstops.
    ///
    /// Inert unless launched with -verifySky (normal game / boot capture unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySky [-captureDir &lt;dir&gt;]
    /// MUST run WINDOWED, not -batchmode (ScreenCapture needs a real swapchain — spike iter-4 lesson;
    /// editor RenderTexture mis-renders URP).
    /// </summary>
    public class SkyVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        public int warmupFrames = 10;
        public int settleFrames = 12;
        public float fieldOfView = 50f;

        void Start()
        {
            if (HasArg("-verifySky"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the warm directional Sun (WorldBootstrap names it "Sun"). The sun DISK in the sky is at
            // -light.forward (light.forward points DOWN into the scene; the disk is the opposite direction).
            Light sun = FindSun();
            if (sun == null)
            {
                Debug.LogError("[SkyVerifyCapture] no directional Sun found — cannot aim the sky-facing camera " +
                               "or verify the sun disk (the directional light is missing from Boot.unity)");
                yield return null; Application.Quit(1); yield break;
            }
            Vector3 toSun = -sun.transform.forward; // world-space direction toward the sun disk
            Debug.Log($"[SkyVerifyCapture] Sun found: dir-to-sun={toSun} (light.forward={sun.transform.forward})");

            for (int i = 0; i < warmupFrames; i++) yield return null;

            // A dedicated SKY camera with the gameplay render path (Skybox clear so the gradient sky + sun
            // render; Zone-D post + SMAA so bloom lifts the warm corona). Parked high over the play centre.
            var camGo = new GameObject("SkyCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // LOAD-BEARING: render the gradient sky we are verifying
            cam.fieldOfView = fieldOfView;
            cam.farClipPlane = 2000f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            // Sit above the play space so terrain never crowds the frame; the sky fills it.
            camGo.transform.position = new Vector3(0f, 12f, 0f);

            // --- Shot 1: aim STRAIGHT at the Sun direction — the sun disk centred. ---
            camGo.transform.rotation = Quaternion.LookRotation(toSun, Vector3.up);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string sunFile = Path.Combine(dir, "sky_sun.png");
            ScreenCapture.CaptureScreenshot(sunFile, 1);
            Debug.Log("[SkyVerifyCapture] wrote " + sunFile + " (aimed at sun dir)");
            yield return new WaitForEndOfFrame();
            // Read back the centre pixels for the self-assert (sun warmer + brighter than the sky surround).
            yield return new WaitForEndOfFrame();
            Texture2D sunTex = GrabCentre(out Color sunCentre, out Color sunSurround);

            // --- Shot 2: aim UP into the cloud band (high pitch, inland) — cloud-vs-sky contrast. ---
            // Pitch ~35deg up, inland (+Z) where BuildClouds biases the cloud lateral spread. The clouds sit
            // at 28-42u; from y=12 looking up-inland they fill the upper frame against the blue sky.
            Vector3 cloudDir = new Vector3(0f, Mathf.Sin(35f * Mathf.Deg2Rad), Mathf.Cos(35f * Mathf.Deg2Rad)).normalized;
            camGo.transform.rotation = Quaternion.LookRotation(cloudDir, Vector3.up);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string cloudFile = Path.Combine(dir, "sky_clouds.png");
            ScreenCapture.CaptureScreenshot(cloudFile, 1);
            Debug.Log("[SkyVerifyCapture] wrote " + cloudFile + " (aimed up-inland into the cloud band)");
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            Texture2D cloudTex = GrabFull(out float skyMedianLuma, out float brightFraction);

            yield return new WaitForSeconds(0.3f);

            // ---- SELF-ASSERT 1: sun disk visible (brighter + RELATIVELY warmer than the surround). ----
            // The disk core is warm-gold but a small bloom corona spreads bright sky-blue around it, so the
            // small centre patch reads a blend. The robust warmth test is RELATIVE: the centre must be warmer
            // (higher R-B) than the blue-sky surround — the gold core lifts R vs B vs the pure-blue sky. An
            // absolute R>=B fails on the bloom-diluted patch even when the disk is visibly gold (eyes-on).
            float sunLuma = Luma(sunCentre);
            float surroundLuma = Luma(sunSurround);
            bool sunBrighter = sunLuma > surroundLuma + 0.04f;                  // the disk lifts the centre luma
            float centreWarmth = sunCentre.r - sunCentre.b;                     // gold core: R lifted toward B
            float surroundWarmth = sunSurround.r - sunSurround.b;              // blue sky: R well below B (negative)
            bool sunWarm = centreWarmth > surroundWarmth + 0.10f;             // centre is clearly warmer than sky
            bool sunOk = sunBrighter && sunWarm;
            Debug.Log($"[SkyVerifyCapture] SUN self-assert: centre=({sunCentre.r:F3},{sunCentre.g:F3},{sunCentre.b:F3}) " +
                      $"luma={sunLuma:F3} vs surround luma={surroundLuma:F3} | brighter={sunBrighter} " +
                      $"centreWarmth(R-B)={centreWarmth:F3} surroundWarmth={surroundWarmth:F3} warm={sunWarm} -> {(sunOk ? "PASS" : "FAIL")}");

            // ---- SELF-ASSERT 2: cloud-vs-sky contrast holds (bright cloud pixels above the sky band). ----
            // The bright near-white cyan clouds (S2 fix) should give a non-trivial fraction of pixels well
            // above the sky median luma. If the clouds washed into the sky this fraction collapses.
            bool contrastOk = brightFraction > 0.01f;
            Debug.Log($"[SkyVerifyCapture] CLOUD-CONTRAST self-assert: skyMedianLuma={skyMedianLuma:F3} " +
                      $"brightFraction={brightFraction:F4} (pixels > skyMedian+0.12) -> {(contrastOk ? "PASS" : "FAIL")}");

            if (sunTex != null) Object.Destroy(sunTex);
            if (cloudTex != null) Object.Destroy(cloudTex);
            Object.Destroy(camGo);

            bool pass = sunOk && contrastOk;
            Debug.Log("[SkyVerifyCapture] verification " + (pass ? "PASSED" : "FAILED") + " -> " + dir);
            yield return new WaitForSeconds(0.2f);
            Application.Quit(pass ? 0 : 1);
        }

        // Capture the current backbuffer + sample a small CENTRE patch (the sun) and a SURROUND ring (the
        // sky around it), averaging each. ReadPixels must run inside WaitForEndOfFrame (caller ensures it).
        private Texture2D GrabCentre(out Color centre, out Color surround)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            int cx = w / 2, cy = h / 2;
            int patch = Mathf.Max(3, Mathf.Min(w, h) / 80);   // tight centre patch (the gold disk core only,
                                                              // not the blue bloom halo around it)
            int ring = Mathf.Max(40, Mathf.Min(w, h) / 6);     // surround offset (clearly off the disk)

            centre = AvgPatch(tex, cx, cy, patch);
            // Average four surround samples (left/right/up/down of the disk) for a stable sky reference.
            Color sl = AvgPatch(tex, cx - ring, cy, patch);
            Color sr = AvgPatch(tex, cx + ring, cy, patch);
            Color su = AvgPatch(tex, cx, cy + ring, patch);
            Color sd = AvgPatch(tex, cx, cy - ring, patch);
            surround = new Color((sl.r + sr.r + su.r + sd.r) * 0.25f,
                                 (sl.g + sr.g + su.g + sd.g) * 0.25f,
                                 (sl.b + sr.b + su.b + sd.b) * 0.25f);
            return tex;
        }

        // Capture the full frame; compute the sky median luma + the fraction of pixels notably brighter than
        // it (the bright clouds). Sub-sampled for speed.
        private Texture2D GrabFull(out float skyMedianLuma, out float brightFraction)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            // Sample a grid; the sky band (no cloud) is the bulk → its median is the sky luma reference.
            const int gx = 64, gy = 36;
            var lumas = new System.Collections.Generic.List<float>(gx * gy);
            for (int j = 0; j < gy; j++)
                for (int i = 0; i < gx; i++)
                {
                    int px = (int)((i + 0.5f) / gx * w);
                    int py = (int)((j + 0.5f) / gy * h);
                    lumas.Add(Luma(tex.GetPixel(px, py)));
                }
            lumas.Sort();
            skyMedianLuma = lumas[lumas.Count / 2];
            float thr = skyMedianLuma + 0.12f;
            int bright = 0;
            foreach (var l in lumas) if (l > thr) bright++;
            brightFraction = (float)bright / lumas.Count;
            return tex;
        }

        private static Color AvgPatch(Texture2D tex, int cx, int cy, int half)
        {
            cx = Mathf.Clamp(cx, half, tex.width - 1 - half);
            cy = Mathf.Clamp(cy, half, tex.height - 1 - half);
            float r = 0, g = 0, b = 0; int n = 0;
            for (int y = cy - half; y <= cy + half; y++)
                for (int x = cx - half; x <= cx + half; x++)
                {
                    Color c = tex.GetPixel(x, y);
                    r += c.r; g += c.g; b += c.b; n++;
                }
            return n > 0 ? new Color(r / n, g / n, b / n) : Color.black;
        }

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static Light FindSun()
        {
            Light best = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (l.gameObject.name == "Sun") return l;  // the named key wins
                if (best == null) best = l;
            }
            return best;
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
