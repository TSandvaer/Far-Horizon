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
    ///   2b. poc_range_side.png (+ _nofog) — the WHOLE-RANGE side profile (86cahwx6w Bar 4): all peaks in one
    ///                             side-on skyline — connected ridges of different heights rising from ONE
    ///                             landmass (hero tallest + snow, NE massif small snow crown, SE bare rock).
    ///   3. poc_overhead.png     — a high frame of the WHOLE big island (organic outline + water on all sides;
    ///                             fog off for this frame — the outline is the judged property).
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
        // The island mean shore radius (must match NextIslandPocGen.MeanShoreR — calibration-pinned like the
        // mountain trio above; #226 buried this as a method-local 400f const that a re-size could silently
        // desync, 86cahwx6w promotes + pins it). Drives the coverage-trace extent + the overhead framing.
        public float meanShoreRadius = 600f;
        // The secondary massifs (86cahwx6w — x/z = centre, y = PEAK HEIGHT; calibration-pinned to
        // NextIslandPocGen.Peaks[1]/[2]). Drive the whole-range side-profile framing (Bar 4: several
        // mountains must read as ridges rising from one landmass — judged side-on).
        public Vector3 secondaryPeak1 = new Vector3(330f, 105f, 150f);
        public Vector3 secondaryPeak2 = new Vector3(250f, 78f, -285f);

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
                // Face toward the mountain from the player's ACTUAL spawn (computed, not hardcoded — the spawn
                // moved off-origin to clear the mountain foot, so a fixed yaw would no longer point at the peak).
                // LOW near-horizon pitch (OrbitCamera: high pitch = looking DOWN; the Sponsor's default 55 is
                // top-down-ish and would hide the far peak) so the shot shows the "feels big + a real mountain
                // rising across the island" vista — the far peak on the horizon ahead of the small player.
                orbit.SetYaw(YawToMountain());
                orbit.SetPitch(14f);
                orbit.SetDistance(18f);
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

            // ---- 2b. WHOLE-RANGE side profile (86cahwx6w Bar 4) — all peaks in ONE side-on frame, from the
            //      spawn/west side, so "several mountains = ridges rising from one landmass, not stamped
            //      cones" is judged on the actual skyline: hero tall centre, NE massif stepped down with its
            //      own snow crown, SE bare-rock massif smallest. Shot twice: as-shipped (fog) + fog-off (the
            //      honest geometry read at range — Exp² fog washes ~80% at 1000u and would hide the shape).
            yield return CaptureRangeSideProfile(dir);

            // ---- 2c. ROCK-FEATURE side profiles (island 2.0-B / C2, ticket 86cakk4w8) — the MANDATORY
            //      per-new-class silhouette gate (lowpoly-quality.md §0). A rocky WALL is a near-vertical rock
            //      face you can't walk up; a stone SLAB is a big flat-topped boulder sitting ON the ground. Both
            //      up-vs-down reads (wall RISES vertical / slab sits low + flat on the surface) are invisible
            //      player-eye/top-down + obvious side-on — so shoot each free-standing feature side-on. ----
            yield return CaptureRockFeatureSideProfiles(dir);

            // ---- 3. OVERHEAD — the WHOLE big island (organic outline + water on all sides + the range mass) ----
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            bool overheadFogWas = RenderSettings.fog;
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 4000f);
                // High + back, looking down at the island centre — the #226 vantage scaled with the island
                // size (framing ∝ meanShoreRadius). Fog OFF for this frame only: at the grown island the
                // vantage sits ~1250u out and the Exp² fog (0.0016) would wash ~98% of the outline — the
                // judged property here is the organic OUTLINE, so read it unfogged (restored right after).
                float s = meanShoreRadius / 400f;
                RenderSettings.fog = false;
                cam.transform.position = new Vector3(60f * s, 620f * s, -560f * s);
                cam.transform.rotation = Quaternion.LookRotation((Vector3.zero - cam.transform.position).normalized, Vector3.up);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("overhead");
            Shot(Path.Combine(dir, "poc_overhead.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            RenderSettings.fog = overheadFogWas;

            // ---- 4. ON THE MOUNTAIN — drive the player UP the peak via the NavMesh + a moving-window PERF read
            //      (traversal FPS while climbing), proving the peak is a CLIMBABLE giant hill (AC3) + the perf
            //      holds while moving through the world (AC4). ----
            yield return ClimbTheMountainAndCapture(dir);

            // Final flush before the process exits so the LAST async CaptureScreenshot fully writes (a fast quit
            // dropped the tail captures in run-1). Then quit cleanly (don't rely on the script's timeout -k kill).
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.7f);
            Debug.Log("[poc-trace] PocIslandVerifyCapture done -> " + dir +
                      " (poc_gameplay + poc_mountain_side + poc_mountain_side2 + poc_range_side[+_nofog] + " +
                      "poc_wall_side + poc_slab_side + poc_overhead + poc_on_mountain)");
            Application.Quit();
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

        // The WHOLE-RANGE side profile (86cahwx6w — the multi-mountain Bar-4 gate). Camera WEST of the range
        // (the spawn side — what the player's opening vista faces), near hero-summit height, looking east
        // across the range centroid so all three silhouettes stack in one frame: the skyline must read as
        // connected ridges of DIFFERENT heights rising from one landmass (cols between peaks, no floating
        // cones, hero tallest). Framing is computed from the pinned peak fields so a re-tune re-frames itself.
        private IEnumerator CaptureRangeSideProfile(string dir)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 4000f);

            Vector3 mc = mountainCenter, p1 = secondaryPeak1, p2 = secondaryPeak2;
            // Range centroid on the ground plane (y fields of the secondaries carry HEIGHT, not elevation).
            Vector3 centroid = new Vector3((mc.x + p1.x + p2.x) / 3f, 0f, (mc.z + p1.z + p2.z) / 3f);
            // Stand off west far enough that the widest peak spread (z-extent + feet) fits the horizontal FOV.
            float zSpread = Mathf.Max(Mathf.Abs(p1.z - p2.z), Mathf.Abs(p1.z - mc.z), Mathf.Abs(p2.z - mc.z));
            float standOff = Mathf.Max(700f, zSpread + mountainFootRadius + 300f);
            Vector3 camPos = new Vector3(centroid.x - standOff, mountainPeakHeight * 0.65f, centroid.z);
            Vector3 lookAt = new Vector3(centroid.x, mountainPeakHeight * 0.55f, centroid.z);
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            Debug.Log($"[poc-trace] RANGE-PROFILE: camPos={camPos.ToString("F0")} looking east across the range " +
                      $"(hero {mc.ToString("F0")} h{mountainPeakHeight:F0} | NE {p1.ToString("F0")} | SE {p2.ToString("F0")}) " +
                      "— the skyline must read as connected ridges of different heights rising from ONE landmass " +
                      "(hero tallest with the big snow cap, NE massif smaller snow crown, SE massif bare rock), " +
                      "NOT cones stamped on a pancake.");
            for (int i = 0; i < settleFrames; i++) yield return null;
            TraceCamera("range_side");
            Shot(Path.Combine(dir, "poc_range_side.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // Fog-off variant — the honest geometry read (at ~1000u the Exp² fog washes most silhouette
            // contrast; the judged property here is SHAPE, so capture it unfogged too, then restore).
            bool fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
            for (int i = 0; i < settleFrames; i++) yield return null;
            Shot(Path.Combine(dir, "poc_range_side_nofog.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            RenderSettings.fog = fogWas;
        }

        // The ROCK-FEATURE side profiles (island 2.0-B / C2) — a side-on silhouette of one rocky WALL + one stone
        // SLAB, the mandatory per-new-class physical-feature gate. Finds a representative feature by name (the
        // scatter tags them LP_RockWall / LP_StoneSlab), parks a camera OUT to the side at the feature's own height
        // looking horizontally ACROSS it, so up-vs-down reads: the WALL rises near-vertical (un-walkable face); the
        // SLAB sits low + flat ON the ground. The ground contact line is kept in frame (camera near the base height)
        // so "sits ON the ground" vs "floats" is judged. Skips gracefully if the class is absent (logs it).
        private IEnumerator CaptureRockFeatureSideProfiles(string dir)
        {
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            yield return CaptureFeatureSide(dir, "LP_RockWall", "poc_wall_side.png", "rocky WALL",
                "the silhouette must RISE near-VERTICAL (a rock face you can't walk up), faceted, not a smooth block");
            yield return CaptureFeatureSide(dir, "LP_StoneSlab", "poc_slab_side.png", "stone SLAB",
                "must sit ON the ground with a broad FLAT top (a big boulder resting on the surface), faceted, some part-embedded");
        }

        // Frame one named free-standing rock feature side-on + capture it. Camera stands off along +X at the
        // feature's mid-height, looking across it toward -X, far enough to frame the whole foot-to-top silhouette.
        private IEnumerator CaptureFeatureSide(string dir, string findName, string fileName, string label, string anchor)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var go = GameObject.Find(findName);
            var rend = go != null ? go.GetComponentInChildren<Renderer>() : null;
            if (rend == null)
            {
                Debug.Log($"[poc-trace] SIDE-PROFILE {label}: no '{findName}' found in the scene — the C2 scatter " +
                          "must place at least one (silhouette gate cannot be judged without it).");
                yield break;
            }
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 3000f);
            Bounds b = rend.bounds;
            float span = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float standOff = span * 2.6f + 10f;
            // Stand off along the SMALLER horizontal axis (the depth) so the camera looks across the WIDER face —
            // a wide wall viewed end-on would read thin (a shard). Frame the broad silhouette, not the edge.
            bool offAlongX = b.size.x <= b.size.z;
            Vector3 camPos = offAlongX
                ? new Vector3(b.center.x + standOff, b.min.y + b.size.y * 0.45f, b.center.z)
                : new Vector3(b.center.x, b.min.y + b.size.y * 0.45f, b.center.z + standOff);
            Vector3 lookAt = new Vector3(b.center.x, b.min.y + b.size.y * 0.5f, b.center.z);
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            for (int i = 0; i < settleFrames; i++) yield return null;
            Debug.Log($"[poc-trace] SIDE-PROFILE {label}: camPos={camPos.ToString("F0")} across '{findName}' at " +
                      $"{b.center.ToString("F0")} (size {b.size.ToString("F1")}) — {anchor}.");
            Shot(Path.Combine(dir, fileName));
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
            // DISABLE WasdMovement for the scripted climb: WasdMovement drives the SAME NavMeshAgent by writing
            // agent.velocity every Update (line ~382), and with no WASD input in the verify run it forces
            // velocity=0 EVERY frame — which clobbers ClickToMove.MoveTo's SetDestination path velocity, so the
            // agent never moves (run-1: MoveTo set=True but gainedY=0.0). Turning WasdMovement off hands the agent
            // to the SetDestination path for the climb, then we restore it. (The mountain's climbability is ALSO
            // proven independently by the 100% NavMesh coverage highest-reachable-Y + the PlayMode foot→summit
            // path test; this drive is the on-mountain VISUAL + a genuine traversal-perf reading.)
            var wasd = player.GetComponent<WasdMovement>();
            bool wasdWas = wasd != null && wasd.enabled;
            if (wasd != null) wasd.enabled = false;

            var agent = player.Agent;
            // Warp the agent to the mountain FOOT on the spawn-facing side first, so the 12s window is spent
            // CLIMBING (gaining Y up the flank), not crossing the ~340u flat approach from the far spawn — the
            // point of this frame is to PROVE the peak is climbable (gainedY>0) + read traversal FPS while moving.
            // (The full spawn→peak walk is the Sponsor's soak; this is the shipped-build climb proof.)
            if (agent != null && agent.isOnNavMesh)
            {
                Vector3 toFoot = (player.transform.position - mountainCenter);
                toFoot.y = 0f; toFoot = toFoot.sqrMagnitude > 0.01f ? toFoot.normalized : Vector3.forward;
                Vector3 footApproach = mountainCenter + toFoot * (mountainFootRadius * 0.92f);
                if (NavMesh.SamplePosition(footApproach, out NavMeshHit fh, 12f, NavMesh.AllAreas))
                {
                    agent.Warp(fh.position);
                    Debug.Log($"[poc-trace] climb: warped to mountain foot @ {fh.position.ToString("F1")} (climb starts here)");
                }
                agent.speed = 12f;              // a brisk scripted climb speed (WasdMovement bypasses agent.speed;
                agent.angularSpeed = 720f;      // SetDestination uses it — set a real value for the path-follow)
                agent.acceleration = 40f;
                agent.autoBraking = true;
            }

            Debug.Log($"[poc-trace] climb: driving player to highest reachable mountain point @ {best.ToString("F1")} (y={bestY:F1})");
            bool set = player.MoveTo(best);
            Debug.Log($"[poc-trace] climb: MoveTo set={set} (WasdMovement disabled for the scripted drive: wasEnabled={wasdWas})");

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

            // Low pitch so the shot shows the player ON the flank with the snow summit rising ABOVE them (a
            // top-down pitch would hide the mountain the player just climbed). Look up-slope toward the peak.
            if (orbit != null) { orbit.SetYaw(YawToMountain()); orbit.SetPitch(16f); orbit.SetDistance(18f); }
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

            // Restore WasdMovement (the shipped locomotion) — the scripted-climb override is over.
            if (wasd != null) wasd.enabled = wasdWas;
        }

        // NavMesh coverage across the big land disc — the shipped-build ground-truth that the WHOLE island is
        // walkable + the mountain is reachable. Raycasts the Ground layer for the surface Y, then samples the
        // live NavMesh. Reports the walkable fraction + the highest reachable Y (the mountain climb proof).
        private void TraceNavCoverage()
        {
            float meanShoreR = meanShoreRadius; // pinned to NextIslandPocGen.MeanShoreR by the calibration test
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

        // The OrbitCamera yaw that points the camera FORWARD from the player toward the mountain centre. OrbitCamera
        // forward = Euler(pitch,yaw,0)*forward, whose ground-plane component ∝ (sin yaw, cos yaw) — so the yaw that
        // faces a world direction (dx,dz) is atan2(dx,dz). Computed from the player's LIVE position so the framing
        // follows the spawn wherever it is (the spawn moved off-origin; a hardcoded yaw would miss the peak).
        private float YawToMountain()
        {
            Vector3 from = player != null ? player.transform.position : Vector3.zero;
            float dx = mountainCenter.x - from.x;
            float dz = mountainCenter.z - from.z;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
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

        // Capture the current frame to an ABSOLUTE path. Supersize 1 (matches the working CaptureGate) — the
        // capture is ASYNC (flushes at end-of-frame), so every caller yields WaitForEndOfFrame + a settle frame
        // AFTER Shot, and RunVerification ends with a final WaitForSeconds flush before the process exits, so the
        // last PNG is fully written (a fast quit dropped the last captures in run-1).
        private void Shot(string path)
        {
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("[poc-trace] captured -> " + path);
        }

        private string NextIslandSize() => (meanShoreRadius * 2f).ToString("F0"); // ~2*MeanShoreR, for the log line

        private string ResolveDir()
        {
            // MUST be ABSOLUTE. ScreenCapture.CaptureScreenshot with a RELATIVE path in a BUILT player writes
            // relative to Application.persistentDataPath (NOT the CLI cwd / the -captureDir we intend), so a
            // relative -captureDir silently loses every PNG (run-1 salvage bug: the shipped exe logged
            // "captured -> ci-out/poc/poc-island\poc_gameplay.png" but no file ever landed). Path.GetFullPath
            // pins it to the working dir — the SAME fix CaptureGate.ResolveDir already uses (the working gate).
            string cli = ArgString("-captureDir", null);
            if (!string.IsNullOrEmpty(cli)) return Path.GetFullPath(cli);
            string baseDir = Path.Combine(
                Application.isEditor ? Path.Combine(Application.dataPath, "..") :
                    Path.GetDirectoryName(Application.dataPath) ?? ".",
                "ci-out", "poc-island");
            return Path.GetFullPath(baseDir);
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
