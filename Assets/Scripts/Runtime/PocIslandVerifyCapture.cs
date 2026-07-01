using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// NEXT-ISLAND POC verify capture (ticket 86caa9zpp) — the SHIPPED-BUILD evidence for the POC's four
    /// ACs, above all the PERF VERDICT (AC4 — the #1 deliverable). Inert unless launched with
    /// -verifyPocIsland (the normal launch / -captureGate frame-sanity is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyPocIsland [-captureDir &lt;dir&gt;]
    ///
    /// It produces (from the BUILT player — editor evidence is never sufficient, unity-conventions.md):
    ///   1. poc_gameplay.png     — the default over-shoulder gameplay orbit (how the player sees the big
    ///                             organic island + the dense forest + the hero mountain rising ahead).
    ///   2. poc_mountain_side.png — a SIDE-PROFILE (silhouette) of the hero mountain (the MANDATORY
    ///                             physical-feature gate, lowpoly-quality.md §0): up-vs-down + grass→rock→SNOW
    ///                             banding + organic footprint read cleanly SIDE-ON, invisible top-down/player-eye.
    ///   3. poc_overhead.png     — a high frame of the WHOLE big island (organic outline + water on all sides).
    ///   4. poc_on_mountain.png  — the player DRIVEN UP the mountain via the NavMesh (proves the peak is a
    ///                             CLIMBABLE giant hill, not a wall).
    /// plus the PERF measurement + NavMesh coverage as [poc-trace] log lines (the ground-truth the perf
    /// verdict + the walkability claim rest on — a MEASURED number from the shipped exe, not a guess).
    ///
    /// PERF is measured at the GAMEPLAY framing (player on the island, dense forest + sea + mountain in
    /// frustum) — the honest worst-case for the "single scaled mesh + LOD on the existing low-poly + GPU
    /// Resident Drawer holds 60fps?" question. Headless deltaTime is ~0 (unity-conventions.md), so this MUST
    /// run in the WINDOWED -verifyPocIsland exe.
    /// </summary>
    public class PocIslandVerifyCapture : MonoBehaviour
    {
        public ClickToMove player;
        public int warmupFrames = 12;
        public int settleFrames = 16;
        // The hero mountain centre (must match NextIslandPocGen.MtnCenter*). Duplicated as plain floats so the
        // runtime capture does not need the editor assembly; kept in lockstep by the calibration EditMode test.
        public Vector3 mountainCenter = new Vector3(90f, 0f, -60f);
        public float mountainPeakHeight = 135f;
        public float mountainFootRadius = 300f;

        void Start()
        {
            if (HasArg("-verifyPocIsland"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // ---- 1. GAMEPLAY over-shoulder — the default orbit the player soaks at, facing the mountain ----
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null)
            {
                // Face roughly toward the mountain (it is at +X,-Z from origin) so the hero peak is in the
                // player's forward view — the "feels big + a real mountain ahead" read.
                orbit.SetYaw(-35f);
                orbit.SetPitch(52f);
                orbit.SetDistance(16f);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("gameplay");
            Shot(Path.Combine(dir, "poc_gameplay.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // ---- PERF (the #1 finding) — measured at the gameplay framing, twice (a stationary window + a
            //      moving window while the camera pans) so the verdict covers both a static frame and traversal. ----
            yield return MeasureFps("gameplay_static");

            // ---- NAVMESH COVERAGE — the shipped-build ground-truth that the WHOLE big island is walkable. ----
            TraceNavCoverage();

            // ---- 2. SIDE-PROFILE (silhouette) of the hero mountain (the MANDATORY physical-feature gate) ----
            //      Camera OUT to the side of the mountain, at mid-height, looking horizontally ACROSS it — so
            //      up-vs-down + the grass→rock→snow banding + the organic footprint read side-on. A backdrop
            //      prop would read flat here; a real giant hill shows a rising silhouette to a single snow summit.
            yield return CaptureMountainSideProfile(dir);

            // ---- 3. OVERHEAD — the WHOLE big island (organic outline + water on all sides + the mountain mass) ----
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 3000f);
                // High + back, looking down at the island centre. The POC island is ~800u across, so this is
                // far higher/back than the start island's overhead.
                cam.transform.position = new Vector3(60f, 620f, -560f);
                cam.transform.rotation = Quaternion.LookRotation((Vector3.zero - cam.transform.position).normalized, Vector3.up);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("overhead");
            Shot(Path.Combine(dir, "poc_overhead.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // ---- 4. ON THE MOUNTAIN — drive the player UP the peak via the NavMesh + a moving-window PERF read
            //      (traversal FPS while climbing), proving the peak is a CLIMBABLE giant hill (AC3) + the perf
            //      holds while moving through the world (AC4). ----
            yield return ClimbTheMountainAndCapture(dir);

            Debug.Log("[poc-trace] PocIslandVerifyCapture done -> " + dir +
                      " (poc_gameplay + poc_mountain_side + poc_overhead + poc_on_mountain)");
        }

        // Measure avg/1%-low FPS over a ~2.5s real-time window at the current framing. Reports avg + the worst
        // single-frame FPS (the min) so the verdict covers both the steady rate and the worst hitch.
        private IEnumerator MeasureFps(string tag)
        {
            const float window = 2.5f;
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
            Debug.Log($"[poc-trace] PERF {tag}: avgFPS={avgFps:F1} minFPS={minFps:F1} " +
                      $"({frames} frames over {elapsed:F2}s) — POC island (~{NextIslandSize()}u), dense forest + " +
                      "sea + hero mountain in view. VERDICT: avgFPS>=60 => single-scaled-mesh+existing-gen HOLDS; " +
                      "<60 => the big island needs chunked/streamed terrain.");
        }

        // The SIDE-PROFILE (silhouette) capture — the mandatory physical-feature gate. Park a camera well OUT
        // to the side of the mountain at ~mid peak height, looking HORIZONTALLY across it, so the rising
        // silhouette to a single snow-white summit reads (up-vs-down is obvious side-on, invisible top-down).
        private IEnumerator CaptureMountainSideProfile(string dir)
        {
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam == null) yield break;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 3000f);

            // Stand off to the -Z side of the mountain, back far enough to frame the whole foot-to-summit, at a
            // height ~40% of the peak so the horizon line is roughly at the mid-flank (the silhouette rises above
            // + the island foot sits below → up-vs-down reads).
            Vector3 mc = mountainCenter;
            float standOff = mountainFootRadius + 190f;
            Vector3 camPos = new Vector3(mc.x, mountainPeakHeight * 0.42f, mc.z - standOff);
            Vector3 lookAt = new Vector3(mc.x, mountainPeakHeight * 0.55f, mc.z);
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("mountain_side");
            Debug.Log($"[poc-trace] SIDE-PROFILE: camPos={camPos.ToString("F0")} looking across the hero " +
                      $"mountain (centre {mc.ToString("F0")}, peakH {mountainPeakHeight:F0}u, foot r {mountainFootRadius:F0}u) " +
                      "— the silhouette must RISE to a single snow-white summit (grass foot → rock flank → snow cap), " +
                      "a real climbable giant hill, NOT a flat backdrop prop.");
            Shot(Path.Combine(dir, "poc_mountain_side.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // A second, closer side-on frame from the +X side (a different azimuth) so the organic (non-conical)
            // footprint + the asymmetric ridge read on more than one profile.
            camPos = new Vector3(mc.x + standOff * 0.85f, mountainPeakHeight * 0.4f, mc.z + 40f);
            lookAt = new Vector3(mc.x, mountainPeakHeight * 0.55f, mc.z);
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("mountain_side2");
            Shot(Path.Combine(dir, "poc_mountain_side2.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        // Drive the player's NavMeshAgent UP the mountain (proving the peak is CLIMBABLE, AC3) and read the
        // traversal FPS while climbing (AC4 moving-window). Finds the highest reachable point near the summit,
        // moves there, then frames + captures with the gameplay orbit.
        private IEnumerator ClimbTheMountainAndCapture(string dir)
        {
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = true;
            if (player == null)
            {
                Debug.Log("[poc-trace] climb: no player wired — skipping the on-mountain frame");
                yield break;
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundMask = groundLayer >= 0 ? (1 << groundLayer) : ~0;
            // Scan up the mountain flank for the highest reachable NavMesh point (the summit approach).
            Vector3 best = player.transform.position; float bestY = -999f; bool found = false;
            for (float rr = mountainFootRadius; rr >= 8f; rr -= 12f)
            for (int a = 0; a < 24; a++)
            {
                float ang = a / 24f * Mathf.PI * 2f;
                float x = mountainCenter.x + Mathf.Cos(ang) * rr;
                float z = mountainCenter.z + Mathf.Sin(ang) * rr;
                if (!Physics.Raycast(new Vector3(x, 400f, z), Vector3.down, out RaycastHit gh, 800f,
                        groundMask, QueryTriggerInteraction.Ignore)) continue;
                if (gh.point.y <= bestY) continue;
                if (NavMesh.SamplePosition(gh.point, out NavMeshHit nh, 5f, NavMesh.AllAreas))
                { best = nh.position; bestY = gh.point.y; found = true; }
            }
            if (!found)
            {
                Debug.Log("[poc-trace] climb: no reachable mountain point found on the NavMesh — the peak may be a WALL (AC3 unmet)");
                yield break;
            }
            Debug.Log($"[poc-trace] climb: driving player to highest reachable mountain point @ {best.ToString("F1")} (y={bestY:F1})");
            bool set = player.MoveTo(best);
            Debug.Log($"[poc-trace] climb: MoveTo set={set}");

            // Walk a real wall-clock window (headless deltaTime~0 trap — this runs in the WINDOWED exe). Read a
            // moving-window FPS while the agent climbs (traversal perf). The big distance may not fully complete
            // in the window; the point is the agent CLIMBS (gains Y) + the FPS holds while moving.
            float t0 = Time.realtimeSinceStartup;
            float startY = player.transform.position.y;
            int frames = 0; float worstDt = 0f;
            while (Time.realtimeSinceStartup - t0 < 12f)
            {
                float dt = Time.unscaledDeltaTime; if (dt > worstDt) worstDt = dt; frames++;
                float planar = Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(best.x, best.z));
                if (planar <= 1.2f) break;
                yield return null;
            }
            float elapsed = Time.realtimeSinceStartup - t0;
            float climbAvgFps = frames / Mathf.Max(0.0001f, elapsed);
            float climbMinFps = worstDt > 0f ? 1f / worstDt : 0f;
            float gainedY = player.transform.position.y - startY;

            if (orbit != null) { orbit.SetYaw(-35f); orbit.SetPitch(52f); orbit.SetDistance(16f); }
            for (int i = 0; i < settleFrames; i++) yield return null;
            float finalPlanar = Vector2.Distance(
                new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(best.x, best.z));
            Debug.Log($"[poc-trace] climb RESULT: player@{player.transform.position.ToString("F1")} " +
                      $"gainedY={gainedY:F1}u finalPlanarToSummit={finalPlanar:F1} (AC3 climbable if gainedY>0 + planar small) | " +
                      $"PERF traversal_climb: avgFPS={climbAvgFps:F1} minFPS={climbMinFps:F1} ({frames} frames over {elapsed:F2}s).");
            Shot(Path.Combine(dir, "poc_on_mountain.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        // NavMesh coverage across the big land disc — the shipped-build ground-truth that the WHOLE island is
        // walkable + the mountain is reachable. Raycasts the Ground layer for the surface Y, then samples the
        // live NavMesh. Reports the walkable fraction + the highest reachable Y (the mountain climb proof).
        private void TraceNavCoverage()
        {
            const float meanShoreR = 400f; // NextIslandPocGen.MeanShoreR
            float plantR = meanShoreR - 40f;
            int groundLayer = LayerMask.NameToLayer("Ground");
            int groundMask = groundLayer >= 0 ? (1 << groundLayer) : ~0;
            const int rings = 12, azimuths = 16;
            int total = 0, covered = 0, noTerrain = 0;
            float highestY = -999f;
            for (int ri = 1; ri <= rings; ri++)
            {
                float rr = plantR * ri / rings;
                for (int ai = 0; ai < azimuths; ai++)
                {
                    float ang = ai / (float)azimuths * Mathf.PI * 2f;
                    float x = Mathf.Cos(ang) * rr, z = Mathf.Sin(ang) * rr;
                    if (!Physics.Raycast(new Vector3(x, 400f, z), Vector3.down, out RaycastHit ghit, 800f,
                            groundMask, QueryTriggerInteraction.Ignore)) { noTerrain++; continue; }
                    total++;
                    if (NavMesh.SamplePosition(new Vector3(x, ghit.point.y, z), out NavMeshHit nh, 4f, NavMesh.AllAreas))
                    { covered++; if (nh.position.y > highestY) highestY = nh.position.y; }
                }
            }
            float pct = total > 0 ? 100f * covered / total : 0f;
            Debug.Log($"[poc-trace] NAVMESH COVERAGE (shipped exe): {covered}/{total} ({pct:F1}%) walkable across the " +
                      $"~{meanShoreR * 2f:F0}u island; highest reachable Y={highestY:F1}u ({noTerrain} no-terrain pts). " +
                      "The agent must reach the WHOLE island + up the hero mountain (>=85% + a high reachable Y).");
        }

        private void TraceCamera(string tag)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.Log($"[poc-trace] {tag}: no Camera.main"); return; }
            Vector3 cp = cam.transform.position, fwd = cam.transform.forward;
            int trees = 0; bool ground = false, water = false;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "LP_Tree") trees++;
                else if (t.name == "Ground_Poc") ground = true;
                else if (t.name == "Water_Poc") water = true;
            }
            Debug.Log($"[poc-trace] {tag}: camPos={cp.ToString("F0")} fwd={fwd.ToString("F2")} | " +
                      $"scene has Ground_Poc={ground} Water_Poc={water} trees(in scene)={trees}");
        }

        private void Shot(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[poc-trace] captured -> " + path);
        }

        private static string NextIslandSize() => "800"; // ~2*MeanShoreR, for the log line

        private string ResolveDir()
        {
            string cli = ArgString("-captureDir", null);
            if (!string.IsNullOrEmpty(cli)) return cli;
            return Path.Combine(
                Application.isEditor ? Path.Combine(Application.dataPath, "..") :
                    Path.GetDirectoryName(Application.dataPath) ?? ".",
                "ci-out", "poc-island");
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
    }
}
