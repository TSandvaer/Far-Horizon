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

        void Start()
        {
            if (HasArg("-verifyPond"))
                StartCoroutine(RunVerification());
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

            // A dedicated post-enabled camera framed on the pond, INDEPENDENT of the gameplay camera's
            // player-follow. Renders the Zone-D post Volume so the capture sees the SAME warm-graded look the
            // Sponsor's gameplay orbit applies (the false-green fix — post must stay ON; an editor/no-post
            // cam lies, the castaway-recolor false-green precedent).
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            var camGo = new GameObject("PondVerifyCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // the gradient sky behind the pond, as in gameplay
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            if (orbit != null)
            {
                var ocam = orbit.GetComponent<Camera>();
                if (ocam != null) ocam.enabled = false; // else both cameras draw
            }

            for (int i = 0; i < warmupFrames; i++) yield return null;

            char tag = 'a';
            bool anyFreshBlue = false;
            foreach (float yaw in yaws)
            {
                Quaternion rot = Quaternion.Euler(viewPitch, yaw, 0f);
                Vector3 offset = rot * new Vector3(0f, 0f, -viewDistance);
                camGo.transform.position = target + offset;
                camGo.transform.LookAt(target);
                Debug.Log($"[FreshwaterPondVerifyCapture] frame {tag}: yaw={yaw} pitch={viewPitch} " +
                          $"dist={viewDistance} pos={camGo.transform.position.ToString("F1")}");

                for (int i = 0; i < settleFrames; i++) yield return null;
                yield return new WaitForEndOfFrame();

                string file = Path.Combine(dir, $"pond_{tag}.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                Debug.Log("[FreshwaterPondVerifyCapture] wrote " + file);
                yield return new WaitForEndOfFrame();
                yield return null;

                // Perceptual read on the FRONT frame (yaw 0): sample the pond-water region (frame centre,
                // where the camera looks at the disc) vs the surround, and assert fresh-blue (B > G). Guard
                // the PERCEPT, not a geometry proxy: a wrong (teal/green) water that still 'renders fine'
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
