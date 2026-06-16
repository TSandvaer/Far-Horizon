using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// BIG ROUND ISLAND verify capture (ticket 86ca9a7qn — Sponsor: "make the island much much bigger,
    /// round, with water on all sides ... dense forest/jungle with high trees ... mountains on other
    /// islands"). The feasibility proof's VISUAL evidence: two shipped-exe frames that prove the
    /// round-island world basis from the BUILT player (the shipped-build capture gate — editor evidence
    /// is never sufficient, unity-conventions.md).
    ///
    ///   1. island_gameplay.png — the DEFAULT over-shoulder gameplay orbit (pitch 55, dist 14): how the
    ///      player actually sees the dense tall jungle + the round shore + the sea on the framed side.
    ///   2. island_overhead.png — a HIGH free camera ~well above origin looking DOWN at an angle, framing
    ///      the WHOLE round island: the round landmass + WATER ON ALL SIDES + the distant mountain ISLANDS
    ///      ringing the sea + the dense forest. This camera is NOT the orbit rig (the orbit clamps pitch to
    ///      [8,70] + distance to [6,26], far too tight to frame a 240u-diameter island) — it's a temporary
    ///      world camera positioned to prove the geometry.
    ///
    /// Inert unless launched with -verifyIsland (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyIsland [-captureDir &lt;dir&gt;]
    ///   [-overheadHeight &lt;y&gt;] [-overheadDist &lt;d&gt;]  (so the Sponsor / a diagnostic run can re-frame).
    ///
    /// Per the no-new-class-without-trace discipline, this dumps [world-trace] lines for both captures
    /// (camera transform + what the centre ray hits + the island/sea/vista object counts in view) so the
    /// orchestrator can read the geometry ground-truth from the player log, not just judge pixels.
    /// </summary>
    public class IslandVerifyCapture : MonoBehaviour
    {
        public int warmupFrames = 10;
        public int settleFrames = 16;
        // Overhead framing defaults: high enough + far enough to frame the ~240u round island + a margin of
        // sea + the nearest mountain islands (~385u). A 35deg-down look (not straight top-down) so the hills,
        // the round coast, and the distant island silhouettes all read in one frame.
        public float overheadHeight = 230f;
        public float overheadDistance = 230f;

        void Start()
        {
            if (HasArg("-diagLine"))
                StartCoroutine(RunLineDiagnostic());
            else if (HasArg("-verifyIsland"))
                StartCoroutine(RunVerification());
        }

        // ============================================================================================
        // AC0 "LINE THROUGH THE ISLAND" DIAGNOSTIC (ticket 86ca9qwr3). The Sponsor sees a dead-straight,
        // world-fixed dark streak across the grass. ISOLATION PROBE (the -noFog/-flatSky/-hideVista family):
        // capture a clean grass-only overhead with the scatter/vista/clouds HIDDEN, then toggle the Sun's
        // SHADOWS off, then toggle POST/FOG off — comparing the frames tells us WHAT the line is:
        //   - vanishes with -noShadows -> it's a SHADOW (a thin world-fixed occluder, or a shadowmap artifact)
        //   - persists with -noShadows -> it's baked into the terrain (vertex colour band) or a mesh seam
        //   - vanishes with -noPost    -> it's a post-processing artifact (grade/vignette/bloom band)
        // Launch: FarHorizon.exe -screen-fullscreen 0 -diagLine [-noShadows] [-noPost] [-noFog] [-captureDir <d>]
        // The default frame is a HIGH near-top-down overhead of the bare grass (no trees to clutter the line).
        // ============================================================================================
        private IEnumerator RunLineDiagnostic()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // ---- ISOLATION: hide the scatter (trees/rocks/grass), the vista, and the clouds so ONLY the
            //      bare island terrain grass is in frame — the line reads against a clean surface. ----
            HideByName("LowPolyScatter");   // the scatter root (trees/rocks/grass under the zone)
            HideByName("Vista");            // distant mountain islands
            HideByName("Clouds");           // cloud blobs
            // Also hide the player so the over-shoulder body doesn't occlude the centre.
            var castaway = GameObject.Find("Castaway") ?? GameObject.Find("Player");

            // ---- TOGGLE: Sun shadows / post / fog per CLI so the probe isolates the cause. ----
            bool noShadows = HasArg("-noShadows");
            bool noPost = HasArg("-noPost");
            bool noFog = HasArg("-noFog");
            string tags = "iso";
            if (noShadows)
            {
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) l.shadows = LightShadows.None;
                tags += "_noShadows";
            }
            if (noPost)
            {
                var camV = Camera.main;
                if (camV != null)
                {
                    var universal = camV.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (universal != null) universal.renderPostProcessing = false;
                }
                tags += "_noPost";
            }
            if (noFog) { RenderSettings.fog = false; tags += "_noFog"; }

            // SHADOW-DISTANCE probe: override the URP main-light shadow distance. If the line MOVES with
            // this value, the line is the shadow-distance / cascade-fade BOUNDARY (a camera-relative ring),
            // not a cast shadow from an occluder. Read via the active URP asset's shadowDistance.
            float shadowDist = ArgFloat("-shadowDist", -1f);
            if (shadowDist > 0f)
            {
                var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                          as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (urp != null)
                {
                    Debug.Log($"[line-trace] URP shadowDistance was {urp.shadowDistance:F0} -> setting {shadowDist:F0}");
                    urp.shadowDistance = shadowDist;
                }
                else Debug.Log("[line-trace] no UniversalRenderPipelineAsset found to set shadowDistance");
                tags += "_sd" + Mathf.RoundToInt(shadowDist);
            }
            // Always report the current URP shadow distance / cascade count (the cascade-seam suspects).
            {
                var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                          as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (urp != null)
                    Debug.Log($"[line-trace] URP shadowDistance={urp.shadowDistance:F0} cascades={urp.shadowCascadeCount}");
            }

            // Report the Sun + shadow-distance ground-truth (the cascade/shadow-distance suspects).
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                    Debug.Log($"[line-trace] Sun '{l.name}': rot={l.transform.eulerAngles.ToString("F0")} " +
                              $"shadows={l.shadows} intensity={l.intensity:F2}");
            Debug.Log($"[line-trace] RenderSettings.fog={RenderSettings.fog} ambientMode={RenderSettings.ambientMode}");

            // ---- HIGH overhead (near top-down) of the bare grass — the line reads clean here. ----
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000f);
                // 70deg-down look from high + a little back so the diagonal world-axis orientation reads.
                cam.transform.position = new Vector3(0f, 200f, -70f);
                cam.transform.rotation = Quaternion.LookRotation((Vector3.zero - cam.transform.position).normalized, Vector3.up);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera(tags + "_overhead");
            ShotTo(Path.Combine(dir, "diagline_" + tags + "_overhead.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // ---- A LOWER over-shoulder-ish framing too (the angle the Sponsor reported the diagonal at). ----
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 45f, -75f);
                cam.transform.rotation = Quaternion.LookRotation((new Vector3(0f, 2f, 10f) - cam.transform.position).normalized, Vector3.up);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera(tags + "_oblique");
            ShotTo(Path.Combine(dir, "diagline_" + tags + "_oblique.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // ---- CAMERA-RELATIVE PROOF: the SAME oblique look but SHIFTED +40u in world X. If the dark
            //      line tracks the CAMERA (shifts +40u in world too), it's the shadow-distance ring (camera-
            //      relative); if it stays put in world, it's baked. (Sponsor reads it as 'world-fixed' because
            //      in normal play the orbit cam stays ~over the spawn at origin, so the ring barely moves.) ----
            if (cam != null)
            {
                cam.transform.position = new Vector3(40f, 45f, -75f);
                cam.transform.rotation = Quaternion.LookRotation((new Vector3(40f, 2f, 10f) - cam.transform.position).normalized, Vector3.up);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera(tags + "_oblique_shift");
            ShotTo(Path.Combine(dir, "diagline_" + tags + "_oblique_shift.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            Debug.Log("[line-trace] RunLineDiagnostic done (" + tags + ") -> " + dir);
        }

        private static void HideByName(string n)
        {
            var go = GameObject.Find(n);
            if (go != null) { go.SetActive(false); Debug.Log("[line-trace] hidden: " + n); }
            else Debug.Log("[line-trace] (not found, skip hide): " + n);
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Warm-up so the first shot has real content (skybox/fog/post all present + scatter visible).
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // ---- 1. GAMEPLAY over-shoulder frame (the default orbit the player soaks at) ----
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null)
            {
                orbit.SetYaw(0f);          // inland-facing default
                orbit.SetPitch(55f);       // the default gameplay pitch
                orbit.SetDistance(14f);    // the default gameplay distance
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("gameplay");
            ShotTo(Path.Combine(dir, "island_gameplay.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // ---- PERF MEASUREMENT (the #1 feasibility risk per the ticket) ----
            // Sample frame time over a real Time.time window at the gameplay framing (player on the island
            // with the dense jungle + sea + vista all in frustum). Report avg/min FPS so the perf headroom
            // at the target island SIZE is a measured number, not a guess. If prohibitive -> the showstopper
            // signal to surface. (Headless deltaTime is ~0 — this MUST run in the WINDOWED -verifyIsland exe.)
            yield return MeasureFps("gameplay");

            // ---- N1 NAVMESH COVERAGE (the shipped-build ground-truth for "can't walk everywhere") ----
            // Sample the LIVE shipped NavMesh across the whole land disc + report the walkable fraction, so
            // the soak-fix has a measured number from the BUILT exe, not just the bootstrap-time bake trace.
            TraceNavCoverage();

            // ---- N1+N2 ON-A-HILL FRAME: drive the player UP a hill (click-move via NavMesh), follow with the
            // gameplay orbit, and capture — proving the agent REACHES the hill (N1) + the camera stays ABOVE
            // the terrain with the player visible (N2). ----
            yield return MoveOntoAHillAndCapture(dir);

            // ---- 2. OVERHEAD / high-orbit frame (proves the WHOLE round island) ----
            float h = ArgFloat("-overheadHeight", overheadHeight);
            float d = ArgFloat("-overheadDist", overheadDistance);
            var cam = Camera.main;
            // Disable the orbit rig so it doesn't fight our manual placement this frame, then place the
            // camera high + back, looking down at the island centre (origin). A wide far clip so the distant
            // mountain islands (~385-510u) are in frustum.
            if (orbit != null) orbit.enabled = false;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000f);
                // Position: up by `h`, back by `d` along -Z, looking at the island centre.
                cam.transform.position = new Vector3(0f, h, -d);
                cam.transform.rotation = Quaternion.LookRotation((Vector3.zero - cam.transform.position).normalized, Vector3.up);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("overhead");
            ShotTo(Path.Combine(dir, "island_overhead.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            Debug.Log("[world-trace] IslandVerifyCapture done -> " + dir +
                      " (island_gameplay.png + island_overhead.png)");
        }

        // Measure avg/min FPS over a ~2s real-time window at the current framing. The perf headroom at the
        // target island size is the ticket's #1 feasibility risk; this prints it as a measured number.
        private IEnumerator MeasureFps(string tag)
        {
            const float window = 2.0f;
            float t0 = Time.realtimeSinceStartup;
            int frames = 0;
            float worstDt = 0f;
            while (Time.realtimeSinceStartup - t0 < window)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > worstDt) worstDt = dt;
                frames++;
                yield return null;
            }
            float elapsed = Time.realtimeSinceStartup - t0;
            float avgFps = frames / Mathf.Max(0.0001f, elapsed);
            float minFps = worstDt > 0f ? 1f / worstDt : 0f;
            Debug.Log($"[world-trace] PERF {tag}: avgFPS={avgFps:F1} minFPS={minFps:F1} " +
                      $"({frames} frames over {elapsed:F2}s) — island target size, dense jungle + sea + vista in view");
        }

        // N1 NAVMESH COVERAGE TRACE (the shipped-build "can't walk everywhere" ground-truth). Sample the LIVE
        // NavMesh across the walkable land disc (rings × azimuths) at the real terrain surface Y (raycast down
        // onto the Ground-layer terrain), and report the walkable fraction. ≥90% = the agent reaches the whole
        // island; a low number = the partial-coverage N1 bug. Runs in the BUILT exe, so it is the shipped truth.
        private void TraceNavCoverage()
        {
            const float islandShoreR = 120f; // LowPolyZoneGen.IslandShoreR
            float plantR = islandShoreR - 6f;
            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundMask = groundLayer >= 0 ? (1 << groundLayer) : ~0;
            const int rings = 12, azimuths = 16;
            int total = 0, covered = 0, noTerrain = 0;
            float worstRing = -1f; int worstMiss = 0;
            for (int ri = 1; ri <= rings; ri++)
            {
                float rr = plantR * ri / rings;
                int miss = 0;
                for (int ai = 0; ai < azimuths; ai++)
                {
                    float ang = ai / (float)azimuths * Mathf.PI * 2f;
                    float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                    float y;
                    if (Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out RaycastHit ghit, 200f,
                            groundMask, QueryTriggerInteraction.Ignore))
                        y = ghit.point.y;
                    else { noTerrain++; continue; }
                    total++;
                    if (NavMesh.SamplePosition(new Vector3(x, y, z), out _, 3f, NavMesh.AllAreas)) covered++;
                    else miss++;
                }
                if (miss > worstMiss) { worstMiss = miss; worstRing = rr; }
            }
            float pct = total > 0 ? 100f * covered / total : 0f;
            Debug.Log($"[world-trace] NAVMESH COVERAGE (shipped exe): {covered}/{total} ({pct:F1}%) walkable " +
                      $"across the round island (worst ring r={worstRing:F0}u missed {worstMiss}/{azimuths}; " +
                      $"{noTerrain} no-terrain pts). N1: the agent must reach the WHOLE island (≥90%).");
        }

        // N1+N2 ON-A-HILL capture: find the highest reachable hill point on the NavMesh, drive the player's
        // ClickToMove there, follow with the gameplay orbit, and capture. Proves (N1) the agent paths UP the
        // hill, and (N2) the orbit camera keeps the player visible / above the terrain on the elevation.
        private IEnumerator MoveOntoAHillAndCapture(string dir)
        {
            var ctm = Object.FindAnyObjectByType<ClickToMove>();
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = true; // re-enable if a prior step disabled it
            if (ctm == null)
            {
                Debug.Log("[world-trace] hill-walk: no ClickToMove found — skipping the on-a-hill frame");
                yield break;
            }

            // Find a high reachable hill point: scan a ring of candidate inland points, pick the highest that
            // samples onto the NavMesh (the agent can actually reach it).
            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundMask = groundLayer >= 0 ? (1 << groundLayer) : ~0;
            Vector3 best = ctm.transform.position; float bestY = -999f; bool found = false;
            for (float r = 30f; r <= 95f; r += 8f)
            for (int a = 0; a < 24; a++)
            {
                float ang = a / 24f * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                if (!Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out RaycastHit gh, 200f,
                        groundMask, QueryTriggerInteraction.Ignore)) continue;
                if (gh.point.y <= bestY) continue;
                if (NavMesh.SamplePosition(gh.point, out NavMeshHit nh, 3f, NavMesh.AllAreas))
                { best = nh.position; bestY = gh.point.y; found = true; }
            }

            if (!found)
            {
                Debug.Log("[world-trace] hill-walk: no reachable hill point found on the NavMesh — N1 may be unfixed");
                yield break;
            }
            Debug.Log($"[world-trace] hill-walk: driving player to highest reachable hill @ {best.ToString("F1")} (y={bestY:F1})");

            bool set = ctm.MoveTo(best);
            Debug.Log($"[world-trace] hill-walk: MoveTo set={set}");

            // Walk a real wall-clock window so the agent traverses up the hill (headless deltaTime~0 trap —
            // this runs in the WINDOWED exe so Time advances). Follow with the gameplay orbit.
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                float planar = Vector2.Distance(
                    new Vector2(ctm.transform.position.x, ctm.transform.position.z),
                    new Vector2(best.x, best.z));
                if (planar <= 1.0f) break;
                yield return null;
            }

            // Frame the player on the hill with the default gameplay orbit.
            if (orbit != null) { orbit.SetYaw(0f); orbit.SetPitch(55f); orbit.SetDistance(14f); }
            for (int i = 0; i < settleFrames; i++) yield return null;

            float finalPlanar = Vector2.Distance(
                new Vector2(ctm.transform.position.x, ctm.transform.position.z),
                new Vector2(best.x, best.z));
            var cam = Camera.main;
            float camAboveGround = 999f;
            if (cam != null && Physics.Raycast(cam.transform.position + Vector3.up * 200f, Vector3.down,
                    out RaycastHit ch, 400f, groundMask, QueryTriggerInteraction.Ignore))
                camAboveGround = cam.transform.position.y - ch.point.y;
            Debug.Log($"[world-trace] hill-walk RESULT: player@{ctm.transform.position.ToString("F1")} " +
                      $"finalPlanarToHill={finalPlanar:F1} (N1 reached if small) | camAboveGround={camAboveGround:F1} " +
                      $"(N2 ok if > 0). camPos={(cam != null ? cam.transform.position.ToString("F1") : "<none>")}");
            ShotTo(Path.Combine(dir, "island_on_hill.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        // Dump the camera ground-truth (transform + centre-ray ground hit + how many island/sea/vista
        // objects sit in the view) so the geometry is readable from the log, not just the pixels.
        private void TraceCamera(string tag)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.Log($"[world-trace] {tag}: no Camera.main"); return; }
            Vector3 cp = cam.transform.position, fwd = cam.transform.forward;
            string hit = "(no plane hit)";
            if (Mathf.Abs(fwd.y) > 1e-4f)
            {
                float t = (-0.20f - cp.y) / fwd.y; // hit the WaterY plane
                if (t > 0) hit = "seaPlaneHit @ " + (cp + fwd * t).ToString("F0");
            }
            // Count island / sea / mountain objects (presence-in-scene ground truth for the frame).
            int trees = 0, peaks = 0, landmasses = 0; bool sea = false, ground = false;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "LP_Tree") trees++;
                else if (t.name == "LP_Mountain") peaks++;
                else if (t.name == "LP_Landmass") landmasses++;
                else if (t.name == "Water_Play") sea = true;
                else if (t.name == "Ground_Play") ground = true;
            }
            Debug.Log($"[world-trace] {tag}: camPos={cp.ToString("F0")} fwd={fwd.ToString("F2")} {hit} | " +
                      $"scene has Ground_Play={ground} Water_Play={sea} trees={trees} peaks={peaks} islands={landmasses}");
        }

        private void ShotTo(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[world-trace] captured -> " + path);
        }

        private string ResolveDir()
        {
            string cli = ArgString("-captureDir", null);
            if (!string.IsNullOrEmpty(cli)) return cli;
            return Path.Combine(Application.dataPath, "..", "ci-out", "island");
        }

        private static bool HasArg(string name)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == name) return true;
            return false;
        }

        private static string ArgString(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        private static float ArgFloat(string name, float fallback)
        {
            string s = ArgString(name, null);
            return (s != null && float.TryParse(s, out float v)) ? v : fallback;
        }
    }
}
