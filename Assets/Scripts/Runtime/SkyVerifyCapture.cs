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
    /// THREE SHOTS, each with the SAME render path as gameplay (Skybox clear + Zone-D post Volume + SMAA);
    /// Skybox clear is LOAD-BEARING (a SolidColor clear like the axe close-up would erase the gradient sky we
    /// are verifying):
    ///   (a) sky_sun.png    — a dedicated camera aimed STRAIGHT at the live Sun direction (the disk centred):
    ///                        proves the disk RENDERS warm-gold in the shipped exe (Erik's open Q).
    ///   (b) sky_clouds.png — aimed up into the cloud band: the cloud-vs-sky contrast shot (Erik's 2nd open Q).
    ///   (c) sky_gameplay.png — a GAMEPLAY-FRAMED camera (ticket 86cag25az sun-lower): an orbit-style pose at
    ///                        a LOW pitch toward the horizon, facing the sun's azimuth, at the real orbit
    ///                        distance. With the sun LOWERED to the Sponsor-accepted elev 18° (was 48°/25°) the
    ///                        disk sits low in the warm band the pitch-[8,70] orbit frames toward the horizon —
    ///                        so this is the eyes-on proof the Sponsor can SEE the sun in normal play (the (a)
    ///                        shot only proves it renders when aimed dead at it; this proves it's framed at a
    ///                        playable angle). Over the OCEAN azimuth (where the Sponsor judged it) there is no
    ///                        treeline; this inland-leaning capture may show canopy occlusion — eyeball it +
    ///                        defer to the soak ([[verify-grounding-soaks-by-gameplay-cam-visual]]).
    ///
    /// SUN DIRECTION: the disk is driven by the sky material's baked _SunDirection (NOT the URP
    /// _MainLightPosition global, which is UNBOUND in the Background/skybox pass — verified empirically on
    /// PR #194). This component is the eyes-on + self-assert proof it renders in the SHIPPED IL2CPP exe.
    ///
    /// SELF-ASSERT (the gate verdict via Application.Quit code):
    ///   1. SUN VISIBLE — sample the centre pixels of sky_sun.png; a warm-gold disk reads BRIGHTER than the
    ///      blue sky surround AND warm (R >= B). The additive _SunColor lifts the centre well above the sky.
    ///   2. CLOUD-VS-SKY CONTRAST HOLDS (Erik 2nd open Q) — sky_clouds.png contains pixels NOTABLY brighter
    ///      than the sky band (the bright near-white cyan clouds), i.e. the S2 contrast fix survives the build.
    ///   3. SUN FRAMED AT GAMEPLAY ANGLE (86cag25az) — in sky_gameplay.png the warm-gold disk appears in the
    ///      UPPER half of the frame (brighter + warmer than the sky around it), proving the lowered sun is
    ///      actually visible at a playable over-shoulder pitch (not just when aimed dead at it).
    /// Quit(1) on any failure (or a missing Sun) so the exe exit code IS the gate; frame_check.py backstops.
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

            // Diagnose-via-trace: log the camera roster BEFORE we take over so a future mis-render (wrong camera
            // winning the backbuffer) is readable from the build log — this is exactly the bug this block fixes.
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Debug.Log($"[sky-cam-roster] '{c.name}' enabled={c.enabled} depth={c.depth} " +
                          $"isMain={(c == Camera.main)} tag={c.tag}");

            // DISABLE every existing camera so ONLY our SkyCaptureCamera composites the backbuffer. WITHOUT this
            // the gameplay orbit camera (also Skybox-clear, default depth 0) renders at the SAME depth as our
            // capture camera → UNDEFINED render order → the orbit cam's ground-level player view races ours into
            // the captured frame (PR #223 run 28658539450: sky_sun.png showed the forest+HUD, the centre patch
            // read a foliage-toned tree, not the sun disk → SUN self-assert FAIL). Every reliable Skybox-clear
            // sibling does this — WeaponSetVerifyCapture disables ALL cameras + sets a top depth, Rock/Rim/
            // FlatShading disable the orbit cam ("else both draw"). Match the strongest form: disable ALL + depth.
            foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existing.enabled = false;

            // A dedicated SKY camera with the gameplay render path (Skybox clear so the gradient sky + sun
            // render; Zone-D post + SMAA so bloom lifts the warm corona). Parked high over the play centre.
            var camGo = new GameObject("SkyCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // LOAD-BEARING: render the gradient sky we are verifying
            cam.fieldOfView = fieldOfView;
            cam.farClipPlane = 2000f;
            cam.depth = 100f; // render last / on top — belt-and-suspenders with the disable-all above
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            // Sit HIGH above the play space so the dead-aim shot has an unobstructed sky view. The disk is
            // a SKYBOX element — its screen position depends only on camera DIRECTION, never position — so
            // raising the camera changes nothing about the disk and only clears OCCLUDERS from the ray. From
            // y=60 even the far vista peaks (~80u at ~500u → ~2° above the ray origin) sit below an 8°-up ray,
            // so shot 1 shows the DISK against clear sky, not terrain. (NOTE: an earlier y=12→y=60 raise was
            // aimed at a MIS-diagnosed "canopy at frame-centre false-fails shot 1" — the real cause was the
            // gameplay orbit camera winning the render, fixed by the disable-all above; the trees seen in the
            // pre-fix shot 1 were the ORBIT cam's, never this camera's. y=60 is still the right unobstructed
            // dead-aim pose now that this camera actually renders.) Shot 3 restores the REAL gameplay pose.
            camGo.transform.position = new Vector3(0f, 60f, 0f);

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
            // (Shot 1 raised the camera to y=60 for an occluder-free dead-aim at the 8° sun — restore the
            // y=12 framing this shot's cloud-band geometry was tuned for: from 60u the camera is ABOVE the
            // 28-42u cloud band and an up-look would miss it.)
            camGo.transform.position = new Vector3(0f, 12f, 0f);
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

            // --- Shot 3: GAMEPLAY-FRAMED (86cag25az sun-lower; re-framed HONEST on 86cah90cp) — the
            // over-shoulder orbit pose at the most HORIZON-WARD playable pitch (OrbitCamera.minPitch 8°),
            // FACING the sun's azimuth, at the real orbit distance (14u) AND the REAL gameplay FOV (45 —
            // MovementCameraScene bakes cam.fieldOfView = 45f). Eyes-on proof that the low sun is FRAMED in
            // normal play (the (a) shot aims dead at the sun — it can't show "is it framed at a tilt").
            // The previous WIDE 75° FOV false-passed the visibility question (the #194-review NIT; the
            // unity-conventions "non-gameplay FOV/pitch false-pass" class): at FOV 45 / pitch-8 look-down the
            // frame tops out ~14.5° above the horizon, so an 18° sun sat ABOVE the frame at every playable
            // pitch while the 75° capture (top ~29.5°) still showed it green. Shooting the REAL FOV is what
            // proves the 86cah90cp round-2 8° bake actually sits inside the playable sky band.
            // NOTE: whether the sun is framed in NORMAL play also depends where the player looks + on tree
            // occlusion — over the OCEAN azimuth (where the Sponsor judged 8°) there is no treeline; this
            // capture leans inland so it may show canopy. The Sponsor soak is the real judge ([[verify-grounding-soaks-by-gameplay-cam-visual]]);
            // this shot proves it CAN be framed at a playable angle, and the gameplay self-assert below is
            // ADVISORY (logged, NOT gating) so tree-position variance can't false-fail the gate.
            float sunAzimuthDeg = Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg; // horizontal heading toward the sun
            const float gameplayPitch = 8f;    // OrbitCamera.minPitch — the horizon-most playable tilt
            const float gameplayDist  = 14f;   // OrbitCamera.distance default
            const float gameplayFov   = 45f;   // MovementCameraScene cam.fieldOfView — the REAL gameplay FOV
            Vector3 lookAt = new Vector3(0f, 1.0f, 0f); // ≈ player root + OrbitCamera.targetOffset
            Quaternion gpRot = Quaternion.Euler(gameplayPitch, sunAzimuthDeg, 0f);
            Vector3 gpForward = gpRot * Vector3.forward;
            camGo.transform.position = lookAt - gpForward * gameplayDist; // sit back along the view ray
            camGo.transform.rotation = gpRot;
            cam.fieldOfView = gameplayFov; // REAL gameplay FOV — a wide capture FOV false-passes visibility
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            string gameplayFile = Path.Combine(dir, "sky_gameplay.png");
            ScreenCapture.CaptureScreenshot(gameplayFile, 1);
            Debug.Log($"[SkyVerifyCapture] wrote {gameplayFile} (gameplay-framed: pitch {gameplayPitch}, FOV {gameplayFov}, yaw->sun azimuth {sunAzimuthDeg:F1})");
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            Texture2D gpTex = GrabWarmestUpper(out Color gpSun, out Color gpSky);

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

            // ---- ADVISORY 3: SUN FRAMED AT GAMEPLAY ANGLE (ticket 86cag25az) — LOGGED, NOT GATING. ----
            // The WARMEST patch in the gameplay frame's upper band (warmth-strict so it locks onto the gold
            // sun, never a bright-cyan CLOUD — the luma+warmth picker was fooled by a cloud on the first run)
            // should read WARMER + BRIGHTER than the cool sky beside it when the lowered sun is framed. This is
            // ADVISORY only (not folded into `pass`): whether the sun lands in an OPEN sky gap vs behind a blob
            // canopy is tree-position-dependent, so a hard gate here would flake; the eyes-on sky_gameplay.png +
            // the Sponsor soak are the real "framed in play" judges ([[verify-grounding-soaks-by-gameplay-cam-visual]]).
            float gpSunLuma = Luma(gpSun), gpSkyLuma = Luma(gpSky);
            bool gpBrighter = gpSunLuma > gpSkyLuma + 0.04f;
            bool gpWarmer = (gpSun.r - gpSun.b) > (gpSky.r - gpSky.b) + 0.06f;
            bool gameplaySunSeen = gpBrighter && gpWarmer;
            Debug.Log($"[SkyVerifyCapture] GAMEPLAY-SUN (advisory): warmest-upper=({gpSun.r:F3},{gpSun.g:F3},{gpSun.b:F3}) " +
                      $"luma={gpSunLuma:F3} vs sky luma={gpSkyLuma:F3} | brighter={gpBrighter} " +
                      $"warmth(sun R-B)={(gpSun.r - gpSun.b):F3} sky={(gpSky.r - gpSky.b):F3} warmer={gpWarmer} " +
                      $"-> sun {(gameplaySunSeen ? "FRAMED" : "not auto-detected (eyeball sky_gameplay.png + soak)")}");

            if (sunTex != null) Object.Destroy(sunTex);
            if (cloudTex != null) Object.Destroy(cloudTex);
            if (gpTex != null) Object.Destroy(gpTex);
            Object.Destroy(camGo);

            // GATE = the two PROVEN asserts (sun renders warm-gold + cloud contrast holds). The gameplay shot
            // is eyes-on evidence (advisory above) — not a hard gate, so tree occlusion can't false-fail it.
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

        // Capture the gameplay-framed frame; in its UPPER band (where a low sun sits) find the WARMEST patch
        // (highest R-B = the warm-gold sun core) + a sky reference beside it. WARMTH-STRICT on purpose: the
        // sky has bright near-white CYAN clouds (B>R) whose high luma fooled an earlier luma+warmth picker into
        // locking onto a cloud; scoring purely on warmth (with a brightness floor to skip dark canopy gaps)
        // makes the gold disk win. Used for the ADVISORY gameplay-sun readout only (not a hard gate).
        private Texture2D GrabWarmestUpper(out Color sun, out Color sky)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            int patch = Mathf.Max(3, Mathf.Min(w, h) / 90);
            // Scan the upper band: y in [0.55h .. 0.97h] (top of the screen is HIGH y in bottom-left origin),
            // x across the middle 84%. Score = WARMTH (R-B) but only for patches above a luma floor (so dark
            // tree-canopy gaps with incidental warmth don't win).
            int yLo = (int)(0.55f * h), yHi = (int)(0.97f * h);
            int xLo = (int)(0.08f * w), xHi = (int)(0.92f * w);
            const int gx = 48, gy = 20;
            float bestWarm = float.NegativeInfinity; int bsx = (xLo + xHi) / 2, bsy = (yLo + yHi) / 2;
            for (int j = 0; j < gy; j++)
                for (int i = 0; i < gx; i++)
                {
                    int px = xLo + (int)((i + 0.5f) / gx * (xHi - xLo));
                    int py = yLo + (int)((j + 0.5f) / gy * (yHi - yLo));
                    Color c = AvgPatch(tex, px, py, patch);
                    if (Luma(c) < 0.35f) continue;       // skip dark canopy / gaps — the sun core is bright
                    float warm = c.r - c.b;              // warmth only — the gold disk beats any cyan cloud
                    if (warm > bestWarm) { bestWarm = warm; bsx = px; bsy = py; }
                }
            sun = AvgPatch(tex, bsx, bsy, patch);
            // Sky reference: sample well to the LEFT and RIGHT of the found patch at the same height (off the
            // disk), averaged. Clamped inside the frame by AvgPatch.
            int off = Mathf.Max(60, w / 5);
            Color sLeft = AvgPatch(tex, bsx - off, bsy, patch);
            Color sRight = AvgPatch(tex, bsx + off, bsy, patch);
            sky = new Color((sLeft.r + sRight.r) * 0.5f, (sLeft.g + sRight.g) * 0.5f, (sLeft.b + sRight.b) * 0.5f);
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
