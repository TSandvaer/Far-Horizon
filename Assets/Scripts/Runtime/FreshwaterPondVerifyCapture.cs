using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the FRESHWATER POND (ticket 86caamkv7 — the THIRST
    /// drink-from-hand source). Sibling of RockVerifyCapture / SeaVerifyCapture: a -verifyPond flag that
    /// frames the GAMEPLAY camera onto the pond at its authored world position and shoots gameplay-pitch
    /// frames, so a reviewer (Tess / Sponsor) re-runs it and judges the pond from a SHIPPED frame — not the
    /// editor (the editor-vs-runtime gate; hero-axe PR #21 proved an editor RenderTexture mis-renders URP).
    ///
    /// WHY THIS HOOK EXISTS (the honesty gap it closes): the standard CI -captureGate is a GENERIC
    /// frame-SANITY check (it shoots the spawn frame and fails only on black/empty/uniform/magenta) — it
    /// NEVER frames the pond, so "the pond renders fresh-blue + grounded in the shipped build" was an
    /// UNPROVEN claim. This hook positions a post-enabled camera on the pond at the OVER-SHOULDER gameplay
    /// orbit pitch (memory verify-grounding-soaks-by-gameplay-cam-visual: judge from the player-framing
    /// gameplay cam, NOT a high-angle/editor view — the prior grounding saga served FLOATING builds twice
    /// off high-angle caps) and captures the pond from three yaws.
    ///
    /// PERCEPTUAL ASSERTIONS (guard the PERCEPT, not a proxy — unity-conventions.md §editor-vs-runtime):
    ///   1. FRESH-BLUE: sample the pond-water region (frame centre, where the camera looks at the disc) and
    ///      assert the dominant water tone reads B > G — the FRESHWATER tell (PondShallow/PondDeep have
    ///      B>G; the sea's teal never does). A green/teal central read = the wrong water shipped.
    ///   2. VISIBLE: the pond-water pixels must differ measurably from the sky/grass surround (the disc is
    ///      actually IN frame, not a fogged sliver).
    /// Logs the sampled means + the verdict; quits non-zero if the pond is NOT visibly fresh-blue (the
    /// build-side regression signal), so a reviewer's re-run is a real gate, not a smoke test.
    ///
    /// Inert unless launched with -verifyPond (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyPond [-captureDir &lt;dir&gt;]
    /// Captures pond_a.png / pond_b.png / pond_c.png, then quits. MUST run WINDOWED (ScreenCapture needs a
    /// real swapchain — spike iter-4). Quits non-zero if the FreshwaterPond is missing from the scene (a
    /// build-side regression signal: the pond was dropped from Boot.unity).
    /// </summary>
    public class FreshwaterPondVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Gameplay-representative framing: the over-shoulder orbit PITCH the player actually sees the pond
        // from (NOT a high-angle/editor top-down — that hid a floating build twice in the grounding saga,
        // memory verify-grounding-soaks-by-gameplay-cam-visual). A readable distance so the fresh-blue disc
        // + its grassy bank fill the frame (the pond is ~2.6u across; the full 14u gameplay orbit would
        // shrink it to a speck and the fresh-blue read would be unjudgeable — same "facets must be visible"
        // reasoning as RockVerifyCapture's closer distance).
        public float viewPitch = 38f;
        public float viewDistance = 7.5f;
        // Three orbit yaws around the pond so the disc + bank are judged all-round (a single angle can hide
        // a one-sided defect — the multi-angle lesson from the hair false-green wave).
        public float[] yaws = { 0f, 70f, -70f };
        public int warmupFrames = 8;
        public int settleFrames = 14;

        // SIDE-PROFILE capture (ticket 86cadj4g7 #130 — Sponsor demands a ground-level horizontal look across
        // the pond that unambiguously shows recessed-NOT-mound + no foam, alongside the player-eye gameplay
        // frame). A near-ground camera looking horizontally across the pond: a raised lens/mound reads as a
        // BULGE above the horizon line; a true recess reads as the water dipping BELOW the surrounding grass.
        // A LOW look ACROSS the pond — close + just above the surrounding grass, pitched gently down so the
        // pool + its NEAR rim + the FAR rim + the grass behind all land in frame. The recess then reads
        // unambiguously: the water surface sits BELOW the surrounding-grass line (a mound would bulge ABOVE
        // it). Tuned off the d6bf755 side-frames (sideHeight 0.55 / pitch 4° framed grass+trees, pond occluded
        // below frame): raise the eye + pitch down so the pond fills the lower-centre, grass horizon up top.
        public float sidePitch = 13f;         // gentle downward pitch — pond fills lower-centre, grass line up top
        public float sideDistance = 6.8f;     // close enough the ~5.5u-wide pool + both rims read big in frame
        public float sideHeight = 1.35f;      // camera eye ~waist-high above the pond surface (a low cross-look)

        void Start()
        {
            if (HasArg("-verifyPondSide"))
                StartCoroutine(RunSideProfile());
            else if (HasArg("-verifyPond"))
                StartCoroutine(RunVerification());
        }

        // Ground-level SIDE-PROFILE pass (ticket 86cadj4g7 #130). Parks Camera.main near the ground looking
        // horizontally across the pond from three yaws so the Sponsor/Tess judges recessed-vs-mound + no-foam
        // from a SHIPPED frame. Writes pond_side_a/b/c.png. Reuses the proven Camera.main + disable-OrbitCamera
        // framing (the gameplay render path → the warm-graded look the Sponsor sees). No perceptual assert here
        // (the profile is a human eyeball gate, per the dispatch); quits 0 after the captures.
        private System.Collections.IEnumerator RunSideProfile()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var pond = Object.FindAnyObjectByType<FreshwaterPond>();
            Debug.Log("[FreshwaterPondVerifyCapture] (side) FreshwaterPond found: " + (pond != null));
            if (pond == null)
            {
                Debug.LogError("[FreshwaterPondVerifyCapture] (side) no FreshwaterPond — build-side regression");
                yield return null; Application.Quit(1); yield break;
            }

            Vector3 target = pond.transform.position;
            var waterT = pond.transform.Find("PondWater");
            if (waterT != null) target = waterT.position; // aim at the water surface so the dip is centred

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[FreshwaterPondVerifyCapture] (side) no Camera.main");
                yield return null; Application.Quit(1); yield break;
            }
            cam.fieldOfView = 42f;
            var camGo = cam.gameObject;

            for (int i = 0; i < warmupFrames; i++) yield return null;

            char tag = 'a';
            foreach (float yaw in yaws)
            {
                // Position the camera near ground level, sideDistance back, almost horizontal. Look at a point
                // at the water surface height so the surrounding-grass horizon line crosses the frame and a
                // recess reads as the water sitting BELOW that line (a mound would bulge above it).
                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 back = yawRot * new Vector3(0f, 0f, -sideDistance);
                Vector3 pos = target + back; pos.y = target.y + sideHeight;
                camGo.transform.position = pos;
                // Nearly-horizontal look across the pond with a small downward pitch so both the near rim + the
                // far rim land in frame; the surrounding-grass horizon line then reveals recess (water below it)
                // vs mound (water bulging above it).
                camGo.transform.rotation = Quaternion.Euler(sidePitch, yaw, 0f);

                for (int i = 0; i < settleFrames; i++) yield return null;
                yield return new WaitForEndOfFrame();
                Debug.Log($"[FreshwaterPondVerifyCapture] side frame {tag}: yaw={yaw} pitch={sidePitch} " +
                          $"pos={cam.transform.position.ToString("F2")} fwd={cam.transform.forward.ToString("F2")}");
                string file = Path.Combine(dir, $"pond_side_{tag}.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                Debug.Log("[FreshwaterPondVerifyCapture] wrote " + file);
                yield return new WaitForEndOfFrame();
                yield return null;
                tag++;
            }
            yield return new WaitForSeconds(0.4f);
            Debug.Log("[FreshwaterPondVerifyCapture] side-profile verification complete -> " + dir);
            Application.Quit(0);
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the pond (FreshwaterPond root). A missing pond is a HARD failure (build-side regression:
            // the pond was dropped from Boot.unity), not a silent empty frame.
            var pond = Object.FindAnyObjectByType<FreshwaterPond>();
            Debug.Log("[FreshwaterPondVerifyCapture] FreshwaterPond found: " + (pond != null));
            if (pond == null)
            {
                Debug.LogError("[FreshwaterPondVerifyCapture] no FreshwaterPond in scene — the pond is missing " +
                               "from Boot.unity (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Look target: the pond water surface. Raise the target a touch so the fresh-blue disc fills the
            // frame centre (where the perceptual sample reads), not the grass beyond it.
            Vector3 target = pond.transform.position;
            var waterT = pond.transform.Find("PondWater");
            if (waterT != null) target = waterT.position;
            target.y += 0.1f;
            Debug.Log("[FreshwaterPondVerifyCapture] framing pond at " + target.ToString("F2") +
                      " (effDrinkR=" + pond.EffectiveDrinkRadius.ToString("F1") + ")");

            // REUSE the gameplay camera (Camera.main) and PARK it on the pond — the PROVEN sibling pattern
            // (SeaVerifyCapture.RunCoastVantage: drive Camera.main, disable the OrbitCamera COMPONENT so it
            // stops re-driving the transform each LateUpdate, then position by hand). The prior version built
            // a NEW PondVerifyCamera and only disabled the orbit's Camera (not the OrbitCamera component) —
            // the gameplay camera kept rendering the player-follow vista, so the pond disc landed off-centre
            // (frac x≈0.20) and the fixed central sample box read terrain-green not water (the false
            // B-G=-0.139; ticket 86cadj4g7 trace). Reusing Camera.main + disabling the component is the
            // framing the gate's perceptual sample assumes (pond at frame centre). Camera.main already runs
            // the gameplay render path (Zone-D post + skybox) so the warm-graded look matches the Sponsor's
            // view — no separate post-enable needed (the castaway-recolor false-green lesson: ride the real
            // gameplay camera, don't author a divergent capture rig).
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false; // stop the rig re-driving the transform each LateUpdate
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[FreshwaterPondVerifyCapture] no Camera.main — cannot frame the pond");
                yield return null;
                Application.Quit(1);
                yield break;
            }
            cam.fieldOfView = 40f;
            var camGo = cam.gameObject;

            // One-shot camera-roster trace (diagnose-via-trace): which cameras are enabled + their depth, so a
            // future mis-render (wrong camera winning) is readable from the build log. One-shot at warmup —
            // negligible cost on a verify-only launch.
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Debug.Log($"[pond-cam-roster] '{c.name}' enabled={c.enabled} depth={c.depth} " +
                          $"isMain={(c == Camera.main)} tag={c.tag}");

            for (int i = 0; i < warmupFrames; i++) yield return null;

            char tag = 'a';
            bool anyFreshBlue = false;
            foreach (float yaw in yaws)
            {
                Quaternion rot = Quaternion.Euler(viewPitch, yaw, 0f);
                Vector3 offset = rot * new Vector3(0f, 0f, -viewDistance);
                camGo.transform.position = target + offset;
                camGo.transform.LookAt(target);

                for (int i = 0; i < settleFrames; i++) yield return null;
                yield return new WaitForEndOfFrame();

                // ACTUAL camera pose at capture time (vs the intended pos) — a component re-driving the
                // transform during settle would diverge here (ticket 86cadj4g7 framing guard).
                Debug.Log($"[FreshwaterPondVerifyCapture] frame {tag}: yaw={yaw} pitch={viewPitch} dist={viewDistance} " +
                          $"ACTUAL pos={cam.transform.position.ToString("F2")} fwd={cam.transform.forward.ToString("F2")} " +
                          $"fov={cam.fieldOfView:F1} orbitEnabled={(orbit != null ? orbit.enabled.ToString() : "n/a")}");

                // Perceptual read on the FRONT frame (yaw 0): sample the pond-water region (frame centre, where
                // the camera looks at the disc) vs the surround, and assert fresh-blue (B > G). Read INSIDE the
                // WaitForEndOfFrame block (the rendered frame is resolved) — ReadPixels needs a settled frame.
                // Guard the PERCEPT, not a geometry proxy: a wrong (teal/green) water that still 'renders fine'
                // would slip a frame-sanity gate but FAILS this.
                if (tag == 'a')
                {
                    if (CheckFreshBlue(out Color centre, out Color surround, out float bMinusG, out float visDelta))
                    {
                        anyFreshBlue = true;
                        Debug.Log($"[FreshwaterPondVerifyCapture] FRESH-BLUE PASS: centre={centre.ToString("F3")} " +
                                  $"(B-G={bMinusG:F3} > 0) surround={surround.ToString("F3")} " +
                                  $"visDelta={visDelta:F3} (pond reads distinct + fresh-blue)");
                    }
                    else
                    {
                        Debug.LogError($"[FreshwaterPondVerifyCapture] FRESH-BLUE FAIL: centre={centre.ToString("F3")} " +
                                       $"(B-G={bMinusG:F3}) surround={surround.ToString("F3")} visDelta={visDelta:F3} " +
                                       "— the pond does NOT read fresh-blue/visible at frame centre (wrong water shipped?)");
                    }
                }

                string file = Path.Combine(dir, $"pond_{tag}.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                Debug.Log("[FreshwaterPondVerifyCapture] wrote " + file);
                yield return new WaitForEndOfFrame();
                yield return null;
                tag++;
            }

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[FreshwaterPondVerifyCapture] verification complete (freshBlue=" + anyFreshBlue + ") -> " + dir);
            // Fail loud in the shipped build if the pond did not read fresh-blue + visible — the build-side gate.
            Application.Quit(anyFreshBlue ? 0 : 1);
        }

        /// <summary>
        /// Read back the current frame and sample a central pond-water band vs a surround band. Returns true
        /// when the centre reads FRESH-BLUE (B > G by a clear margin — the freshwater tell) AND differs
        /// measurably from the surround (the disc is actually in frame, not a fogged/uniform sliver).
        /// </summary>
        private bool CheckFreshBlue(out Color centre, out Color surround, out float bMinusG, out float visDelta)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            // Centre band: the pond disc sits at frame centre (the camera looks at it). A compact central
            // box averages the water tone, robust to a stray bank pixel.
            int cx0 = (int)(w * 0.42f), cx1 = (int)(w * 0.58f);
            int cy0 = (int)(h * 0.40f), cy1 = (int)(h * 0.56f);
            centre = MeanBand(tex, cx0, cx1, cy0, cy1);

            // Surround band: the upper frame (sky / far grass), well away from the water disc, to prove the
            // water is DISTINCT (visible), not the whole frame washed one tone.
            int sx0 = (int)(w * 0.10f), sx1 = (int)(w * 0.90f);
            int sy0 = (int)(h * 0.78f), sy1 = (int)(h * 0.92f);
            surround = MeanBand(tex, sx0, sx1, sy0, sy1);
            Object.Destroy(tex);

            bMinusG = centre.b - centre.g;                                  // freshwater tell: B > G
            visDelta = Mathf.Abs(centre.r - surround.r) + Mathf.Abs(centre.g - surround.g) +
                       Mathf.Abs(centre.b - surround.b);                    // distinct from surround
            // Fresh-blue: a clear B>G margin (sea teal has G>=B → fails). Visible: a clear surround delta.
            return bMinusG > 0.04f && visDelta > 0.05f;
        }

        private static Color MeanBand(Texture2D tex, int x0, int x1, int y0, int y1)
        {
            double r = 0, g = 0, b = 0; int n = 0;
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                Color c = tex.GetPixel(x, y);
                r += c.r; g += c.g; b += c.b; n++;
            }
            if (n == 0) return Color.black;
            return new Color((float)(r / n), (float)(g / n), (float)(b / n));
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
