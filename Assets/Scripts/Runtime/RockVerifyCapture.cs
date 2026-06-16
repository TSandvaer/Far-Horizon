using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the SCATTER ROCKS (ticket 86ca8m5zu — the
    /// "dark angular spikes... all over" soak-fix: round into boulders + thin + warm-light). Sibling of
    /// SeaVerifyCapture / AxeVerifyCapture: a -verifyRock flag that orbits the GAMEPLAY camera onto the
    /// rock outcrops and shoots MULTI-ANGLE frames at gameplay distance, so a reviewer (Tess / Sponsor)
    /// re-runs it and judges the boulders from a SHIPPED frame — not the editor (the editor-vs-runtime
    /// gate; hero-axe PR #21 proved an editor RenderTexture mis-renders URP).
    ///
    /// WHY a dedicated hook: the default -captureGate frames the SPAWN (yaw 0, looking inland-near). The
    /// rocks now live as outcrops in the FIELD band (inland of the spawn, ~Z16+), so the standard frame
    /// barely shows them. The Sponsor's note was specifically that the rocks "stick up... all over" —
    /// judging the fix requires framing the OUTCROPS at gameplay distance from several angles (the
    /// dispatch's "multi-angle, default gameplay orbit cam, gameplay distance" requirement). This finds
    /// the LP_Rock instances, centres on their centroid, and captures from 3 yaws (front-inland, and two
    /// flanking) so the boulder silhouette is judged all-round — never a hero close-up.
    ///
    /// Inert unless launched with -verifyRock (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyRock [-captureDir &lt;dir&gt;]
    /// Captures rock_a.png / rock_b.png / rock_c.png, then quits. MUST run WINDOWED (ScreenCapture needs a
    /// real swapchain — spike iter-4). Quits non-zero if NO rocks are found in the scene (a build-side
    /// regression signal: the scatter was dropped from Boot.unity).
    /// </summary>
    public class RockVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Gameplay-representative framing: the Sponsor-preferred high orbit PITCH (the over-shoulder orbit the
        // player sees rocks from), but a CLOSER distance than the full-field orbit so the FACETS are READABLE
        // (the rocks are ~1u — at the 16u field-orbit distance they're invisible specks, so the fix can't be
        // judged). This frames ONE dense outcrop at a gameplay-pitch but readable distance, multi-angle — the
        // judgment is "do these read as faceted STONE", which needs the facets visible (NOT a flattering hero
        // close-up — the pitch stays the gameplay orbit pitch).
        public float viewPitch = 50f;
        public float viewDistance = 7f;
        // Three orbit yaws around the chosen outcrop so the stone silhouette is judged all-round.
        public float[] yaws = { 0f, 60f, -60f };
        public int warmupFrames = 8;
        public int settleFrames = 14;

        void Start()
        {
            if (HasArg("-verifyRock"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Find the scatter rocks (LP_Rock roots). Search inactive too so a missing scatter is a HARD
            // failure, not a silent empty frame.
            var rocks = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (t.name == "LP_Rock") rocks.Add(t);

            Debug.Log("[RockVerifyCapture] LP_Rock instances found: " + rocks.Count);
            if (rocks.Count == 0)
            {
                Debug.LogError("[RockVerifyCapture] no LP_Rock instances in scene — the scatter rocks are " +
                               "missing from Boot.unity (build-side regression signal)");
                yield return null;
                Application.Quit(1);
                yield break;
            }

            // Pick the DENSEST outcrop as the look target (the rock with the most neighbours within an
            // outcrop radius), then centre on that local cluster — NOT the average of all widely-spread
            // outcrops (that average lands in empty field between clusters, so every rock is far from the cam).
            // Framing a real dense pile at a readable distance is what lets the facets be JUDGED as stone.
            const float outcropRadius = 4.5f;
            Transform best = rocks[0];
            int bestCount = -1;
            foreach (var a in rocks)
            {
                int near = 0;
                foreach (var b in rocks)
                    if ((a.position - b.position).sqrMagnitude < outcropRadius * outcropRadius) near++;
                if (near > bestCount) { bestCount = near; best = a; }
            }
            // Local centroid of that densest cluster (the chosen rock + its near neighbours).
            Vector3 centroid = Vector3.zero;
            int clusterN = 0;
            foreach (var b in rocks)
                if ((best.position - b.position).sqrMagnitude < outcropRadius * outcropRadius)
                { centroid += b.position; clusterN++; }
            centroid /= Mathf.Max(1, clusterN);
            centroid.y += 0.3f; // raise the look target a touch off the ground so the stone fills, not the sand
            Debug.Log($"[RockVerifyCapture] densest outcrop has {bestCount} rocks; framing {clusterN}-rock " +
                      $"cluster at {centroid}");

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            Debug.Log("[RockVerifyCapture] orbit camera found: " + (orbit != null) +
                      "; outcrop centroid=" + centroid);

            // A dedicated post-enabled camera framed on the centroid (so the framing is INDEPENDENT of the
            // gameplay camera's player-follow). Renders the Zone-D post Volume so the capture sees the SAME
            // warm-graded look the Sponsor's gameplay orbit applies (the false-green fix — post stays on).
            var camGo = new GameObject("RockVerifyCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // the gradient sky behind the boulders, as in gameplay
            cam.fieldOfView = 40f;
            var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            // Disable the gameplay orbit camera so our framing is the one that renders (else both draw).
            if (orbit != null)
            {
                var ocam = orbit.GetComponent<Camera>();
                if (ocam != null) ocam.enabled = false;
            }

            for (int i = 0; i < warmupFrames; i++) yield return null;

            char tag = 'a';
            foreach (float yaw in yaws)
            {
                // Orbit position around the centroid at the gameplay pitch + distance.
                Quaternion rot = Quaternion.Euler(viewPitch, yaw, 0f);
                Vector3 offset = rot * new Vector3(0f, 0f, -viewDistance);
                camGo.transform.position = centroid + offset;
                camGo.transform.LookAt(centroid);
                Debug.Log($"[RockVerifyCapture] frame {tag}: yaw={yaw} pitch={viewPitch} dist={viewDistance} " +
                          $"pos={camGo.transform.position}");

                for (int i = 0; i < settleFrames; i++) yield return null;
                yield return new WaitForEndOfFrame();

                string file = Path.Combine(dir, $"rock_{tag}.png");
                ScreenCapture.CaptureScreenshot(file, 1);
                Debug.Log("[RockVerifyCapture] wrote " + file);
                yield return new WaitForEndOfFrame();
                yield return null;
                tag++;
            }

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[RockVerifyCapture] verification complete -> " + dir);
            Application.Quit(0);
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
