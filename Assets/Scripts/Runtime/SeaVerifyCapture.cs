using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the BEACH OCEAN (drew/beach-water-scene; Uma
    /// beach-water-direction §4 task F — the orbit-to-sea framing check).
    ///
    /// The default orbit framing (yaw 0) looks INLAND (trees / craft / fire — correct for the survival
    /// loop). The ocean sits seaward, BEHIND the spawn — so the standard -captureGate frames cannot
    /// judge the water. Uma §2: "the player must be able to orbit ~180 and find the bright sea + their
    /// landing point behind them." This hook orbits the camera to the seaward yaw, lets it settle, and
    /// captures the frame the Sponsor will judge: the bright teal sea filling the frame, the foam-lined
    /// shore in front of it, the far edge lost in the warm fog haze. The HUD build stamp is visible so
    /// the capture self-identifies its build (the stale-stamp gate).
    ///
    /// Inert unless launched with -verifySea (so the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySea [-captureDir &lt;dir&gt;]
    /// Captures: sea_inland.png (the default inland view, for contrast) + sea_seaward.png (orbited to
    /// the ocean), then quits. MUST run WINDOWED, not -batchmode (ScreenCapture needs a real swapchain —
    /// the spike iter-4 lesson; editor RenderTexture mis-renders URP, hero-axe PR #21).
    /// </summary>
    public class SeaVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // The seaward orbit yaw — 180 from the inland default points the camera back over the spawn at
        // the sea the castaway washed in from.
        public float seawardYaw = 180f;
        // Drop the pitch toward the horizon (OrbitCamera clamps to minPitch, now widened to 8) and pull
        // back so the seaward view looks OUT to the open sea, not down at the near sand. The trace
        // (OceanCameraDiag, 2026-06-13) proved that at the OLD 35 floor the camera centre only reached
        // the beach (~Z+4) — the sea entered frame as a far fogged sliver (the "grey pond" soak report).
        // Capture at the new pitch FLOOR (8) — this is the "all the way down toward the horizon" view the
        // Sponsor explicitly asked to be able to reach. At pitch 8 the look is near-horizontal so the
        // beach foreground shrinks and the bright teal sea fills the most frame to the fogged horizon.
        public float seawardPitch = 8f;
        public float seawardDistance = 22f;
        public int warmupFrames = 8;
        public int settleFrames = 16;

        // The wave-MOTION sequence (ticket 86ca9yn57 AC2 — "the water should have waves that move"). Number
        // of frames + the real-time gap between them; chosen so the in-shader swell advances a VISIBLE amount
        // between consecutive frames (the swell period is ~2pi/_WaveSpeed ~ 6.6s, so ~0.4s steps walk it
        // through a full cycle over the sequence — consecutive PNGs differ if the water actually animates).
        public int waveFrames = 6;
        public float waveStepSeconds = 0.4f;

        // FPS A/B window (ticket 86caamnmb AC5 — the transparent-water OVERDRAW cost on the ~600u ocean). The
        // seaward orbit (yaw 180) is the worst-case overdraw view (the most transparent water pixels on screen).
        // Sample unscaled frame time over this window; mean + 95th-percentile let the orchestrator/Sponsor A/B
        // the foam-water build against the prior opaque-water build (run -fpsProbe on each, compare the numbers).
        public int fpsSampleFrames = 240;     // ~4s at 60fps — enough to average out hitches
        public int fpsWarmupFrames = 60;      // let the swapchain/post settle before sampling (first-frame spikes)

        void Start()
        {
            if (HasArg("-fpsProbe"))
                StartCoroutine(RunFpsProbe());
            else if (HasArg("-verifyCoast"))
                StartCoroutine(RunCoastVantage());
            else if (HasArg("-verifyWaves"))
                StartCoroutine(RunWaveSequence());
            else if (HasArg("-verifySea"))
                StartCoroutine(RunVerification());
        }

        // -fpsProbe (ticket 86caamnmb AC5): measure shipped-exe frame time at the worst-case overdraw view
        // (the seaward orbit, where the full transparent ocean fills the frame). vSync is the desktop default
        // (unity6-mastery §12) which would CLAMP the measurement to the refresh rate — so we DISABLE vSync for
        // the probe window (targetFrameRate uncapped) to read the true GPU-bound frame time the overdraw costs,
        // then log mean + p95 ms. Run this on the foam-water build AND on a prior opaque-water build; the
        // delta is the AC5 overdraw cost. INERT unless -fpsProbe is passed.
        private System.Collections.IEnumerator RunFpsProbe()
        {
            // Uncap the frame rate so the measurement reflects the true frame time, not the vSync clamp.
            int prevVSync = QualitySettings.vSyncCount;
            int prevTarget = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 0; // 0 = uncapped when vSync is off

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            float pitch = ArgFloat("-seawardPitch", 22f);   // a low seaward pitch so the ocean fills the frame
            float dist = ArgFloat("-seawardDistance", 20f);
            if (orbit != null)
            {
                orbit.SetYaw(seawardYaw);
                orbit.SetPitch(pitch);
                orbit.SetDistance(dist);
            }
            for (int i = 0; i < fpsWarmupFrames; i++) yield return null;

            int n = ArgInt("-fpsFrames", fpsSampleFrames);
            var ms = new System.Collections.Generic.List<float>(n);
            for (int i = 0; i < n; i++)
            {
                ms.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            ms.Sort();
            double sum = 0; foreach (var v in ms) sum += v;
            float mean = n > 0 ? (float)(sum / n) : 0f;
            float p95 = n > 0 ? ms[Mathf.Clamp((int)(n * 0.95f), 0, n - 1)] : 0f;
            float p50 = n > 0 ? ms[n / 2] : 0f;
            float meanFps = mean > 0.0001f ? 1000f / mean : 0f;
            Debug.Log($"[fpsProbe] seaward-orbit overdraw view (yaw {seawardYaw} pitch {pitch} dist {dist}) " +
                      $"frames={n} vSyncOff: meanMs={mean:F3} (~{meanFps:F0}fps) p50Ms={p50:F3} p95Ms={p95:F3}. " +
                      "A/B this against the prior opaque-water build's -fpsProbe to read the transparent-water " +
                      "overdraw cost (ticket 86caamnmb AC5).");

            QualitySettings.vSyncCount = prevVSync;
            Application.targetFrameRate = prevTarget;
            yield return new WaitForSeconds(0.2f);
            Application.Quit();
        }

        // -verifyCoast (ticket 86ca9yn57): the BIG ROUND ISLAND moved the sea ~120u out behind dense jungle,
        // so from the spawn orbit the sea↔sky horizon is NOT in frame (the spawn capture lands on forest).
        // The Sponsor judges the sea by walking OUT TO THE COAST and looking seaward — this vantage
        // REPRODUCES that view: it DETACHES Camera.main from the orbit rig and parks it at the +Z waterline
        // looking out to the open sea, so the far-sea-vs-sky horizon is dead-centre and judgeable. Pairs with
        // -seaDiag (the sea-vs-sky band sampler) + can also run the wave sequence (-coastWaves). Diagnostic /
        // judgement framing only — it bypasses the orbit clamp deliberately (this is the Sponsor's coast view).
        private IEnumerator RunCoastVantage()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            for (int i = 0; i < warmupFrames; i++) yield return null;

            var cam = Camera.main;
            // Stop the orbit rig from re-driving the camera each LateUpdate (we park it by hand).
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;

            // COAST PROBE (INERT): raycast the terrain DOWN along +Z to find where the land actually dips below
            // the sea (the real +Z waterline) + how high the foreshore sits — so the camera is parked from
            // GROUND TRUTH, not guessed radii (the island is azimuth-warped + raised; guessing framed skybox).
            if (HasArg("-coastProbe"))
            {
                var groundGo = GameObject.Find("Ground_Play");
                var col = groundGo != null ? groundGo.GetComponent<MeshCollider>() : null;
                if (col != null)
                {
                    float lastAbove = -1f;
                    for (float r = 80f; r <= 200f; r += 2f)
                    {
                        var ray = new Ray(new Vector3(0f, 60f, r), Vector3.down);
                        if (col.Raycast(ray, out RaycastHit h, 200f))
                        {
                            string tag = h.point.y <= -0.20f ? " <= WaterY (SEA past here)" : "";
                            if (h.point.y > -0.20f) lastAbove = r;
                            Debug.Log($"[coastProbe] +Z r={r:F0} terrainY={h.point.y:F2}{tag}");
                        }
                        else Debug.Log($"[coastProbe] +Z r={r:F0} (no terrain hit — open sea)");
                    }
                    Debug.Log($"[coastProbe] last DRY +Z radius ~ {lastAbove:F0}u (park the cam just past here over open water)");
                }
            }

            // Park at the +Z coast, a little inland of the waterline (IslandShoreR ~120u) and up a few u, then
            // look seaward (+Z) with a gentle downward tilt so the near surf + far sea + sky all sit in frame.
            float coastR = ArgFloat("-coastRadius", 118f);
            float camH = ArgFloat("-coastHeight", 9f);
            float pitch = ArgFloat("-coastPitch", 12f); // degrees DOWN from horizontal
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, camH, coastR);
                cam.transform.rotation = Quaternion.Euler(pitch, 0f, 0f); // face +Z (seaward), tilt down
                Debug.Log($"[SeaCoast] parked at coast pos={cam.transform.position.ToString("F1")} " +
                          $"pitch={pitch} (looking seaward +Z)");
            }
            for (int i = 0; i < settleFrames; i++) yield return null;

            // WATER-MESH PROBE (INERT): the camera-framing diagnosis kept landing on skybox where the sea
            // 'should' be — so dump GROUND TRUTH about the water mesh from THIS camera: renderer enabled?
            // world bounds? material _FogCap? AND ray-march the camera centre + a down-sample ray against the
            // WaterY plane to confirm the sea geometry is actually IN the look direction (vs hidden/culled).
            if (HasArg("-waterProbe") && cam != null)
            {
                var waterGo = GameObject.Find("Water_Play");
                if (waterGo != null)
                {
                    var wmr = waterGo.GetComponent<MeshRenderer>();
                    var wmat = wmr != null ? wmr.sharedMaterial : null;
                    Bounds b = wmr != null ? wmr.bounds : new Bounds();
                    Debug.Log($"[waterProbe] Water_Play rendererEnabled={(wmr != null && wmr.enabled)} " +
                              $"shader={(wmat != null ? wmat.shader.name : "null")} " +
                              $"_FogCap={(wmat != null && wmat.HasProperty("_FogCap") ? wmat.GetFloat("_FogCap").ToString("F2") : "n/a")} " +
                              $"_WaveAmp={(wmat != null && wmat.HasProperty("_WaveAmp") ? wmat.GetFloat("_WaveAmp").ToString("F2") : "n/a")} " +
                              $"boundsCtr={b.center.ToString("F1")} boundsSize={b.size.ToString("F0")}");
                    // Cast a few screen rays at the LOWER frame (where the sea should be) and report whether the
                    // WaterY plane is in front + how far (fog intensity at that distance).
                    foreach (float sy in new[] { 0.20f, 0.35f, 0.45f })
                    {
                        Ray r = cam.ViewportPointToRay(new Vector3(0.5f, sy, 0f));
                        string hit = "(ray never reaches WaterY plane in view)";
                        if (r.direction.y < -1e-4f)
                        {
                            float t = (-0.20f - r.origin.y) / r.direction.y;
                            if (t > 0f)
                            {
                                Vector3 p = r.origin + r.direction * t;
                                float rr = new Vector2(p.x, p.z).magnitude;
                                bool insideMesh = Mathf.Abs(p.x) <= b.extents.x + 1f && Mathf.Abs(p.z - b.center.z) <= b.extents.z + 1f;
                                hit = $"hits WaterY @ {p.ToString("F0")} (r={rr:F0}u, dist={t:F0}u, insideWaterBounds={insideMesh})";
                            }
                        }
                        Debug.Log($"[waterProbe] screenY={sy:F2} {hit}");
                    }
                }
                else Debug.Log("[waterProbe] Water_Play NOT FOUND in scene");
            }

            ShotTo(Path.Combine(dir, "coast_seaward.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            if (HasArg("-seaDiag"))
            {
                yield return new WaitForEndOfFrame();
                SampleHorizonBands();
            }

            // Optional wave sequence from the coast vantage (proves the sea moves AT the horizon the Sponsor sees).
            if (HasArg("-coastWaves") && cam != null)
            {
                int w = Screen.width, h = Screen.height;
                Texture2D prev = null;
                for (int f = 0; f < waveFrames; f++)
                {
                    yield return new WaitForEndOfFrame();
                    ShotTo(Path.Combine(dir, $"coast_wave_{f:00}.png"));
                    var curT = new Texture2D(w, h, TextureFormat.RGB24, false);
                    curT.ReadPixels(new Rect(0, 0, w, h), 0, 0); curT.Apply();
                    if (prev != null)
                    {
                        double acc = 0; int n = 0;
                        for (int y = 0; y < h / 2; y += 3)
                        for (int x = 0; x < w; x += 3)
                        {
                            Color a = prev.GetPixel(x, y), b = curT.GetPixel(x, y);
                            acc += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b); n++;
                        }
                        float md = n > 0 ? (float)(acc / n) : 0f;
                        Debug.Log($"[SeaCoast] wave frame {f} vs {f - 1}: mean sea-region pixel delta = {md:F4} " +
                                  (md > 0.002f ? "(MOVING)" : "(static?)"));
                        Object.Destroy(prev);
                    }
                    prev = curT;
                    float t0 = Time.time;
                    while (Time.time - t0 < waveStepSeconds) yield return null;
                }
                if (prev != null) Object.Destroy(prev);
            }

            yield return new WaitForSeconds(0.3f);
            Debug.Log("[SeaCoast] coast vantage complete -> " + dir);
            Application.Quit();
        }

        // Capture a MULTI-FRAME sequence from a fixed seaward camera over a real-time window — proving the
        // sea SURFACE MOVES (AC2). The camera is pinned (no orbit between shots) so any pixel difference
        // between consecutive frames is the WATER animating, not the camera. A low oblique pitch frames the
        // near-shore swell + foam line so the vertical bob reads as a moving silhouette/foam edge. Pairs with
        // a frame-to-frame pixel-delta dump so the motion is provable from the trace too, not just by eye.
        private IEnumerator RunWaveSequence()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            for (int i = 0; i < warmupFrames; i++) yield return null;
            // A mid pitch toward the shoreline so both the surf line AND the near-sea swell are in frame —
            // the swell's vertical motion reads as the foam/silhouette shifting between frames.
            float pitch = ArgFloat("-seawardPitch", 22f);
            float dist = ArgFloat("-seawardDistance", 20f);
            if (orbit != null)
            {
                orbit.SetYaw(seawardYaw);
                orbit.SetPitch(pitch);
                orbit.SetDistance(dist);
                Debug.Log("[SeaWaveSeq] pinned seaward cam: yaw=" + orbit.Yaw + " pitch=" + orbit.Pitch +
                          " dist=" + orbit.Distance);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;

            int w = Screen.width, h = Screen.height;
            Texture2D prev = null;
            for (int f = 0; f < waveFrames; f++)
            {
                yield return new WaitForEndOfFrame();
                string file = Path.Combine(dir, $"sea_wave_{f:00}.png");
                ShotTo(file);
                // Read back this frame for a frame-to-frame motion delta over the central water region.
                var cur = new Texture2D(w, h, TextureFormat.RGB24, false);
                cur.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                cur.Apply();
                if (prev != null)
                {
                    // Mean abs pixel delta over the LOWER half (the sea region) — > ~0 means the surface moved.
                    double acc = 0; int n = 0;
                    for (int y = 0; y < h / 2; y += 3)
                    for (int x = 0; x < w; x += 3)
                    {
                        Color a = prev.GetPixel(x, y), b = cur.GetPixel(x, y);
                        acc += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b); n++;
                    }
                    float meanDelta = n > 0 ? (float)(acc / n) : 0f;
                    Debug.Log($"[SeaWaveSeq] frame {f} vs {f - 1}: mean sea-region pixel delta = {meanDelta:F4} " +
                              (meanDelta > 0.002f ? "(MOVING)" : "(static?)"));
                    Object.Destroy(prev);
                }
                prev = cur;
                // advance real time so the in-shader swell phase moves before the next shot
                float t0 = Time.time;
                while (Time.time - t0 < waveStepSeconds) yield return null;
            }
            if (prev != null) Object.Destroy(prev);

            Debug.Log("[SeaWaveSeq] wave-motion sequence complete -> " + dir);
            Application.Quit();
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            Debug.Log("[SeaVerifyCapture] orbit camera found: " + (orbit != null));

            // Warm-up so the first shot has real content (skybox/fog/post all present).
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // 1. The default INLAND view (yaw 0) — captured for contrast (the loop-facing framing).
            ShotTo(Path.Combine(dir, "sea_inland.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. Orbit to the SEAWARD yaw, drop the pitch toward the horizon + pull back, let the
            //    follow/lerp settle, then capture the ocean.
            // The pitch is overridable via -seawardPitch <deg> so a diagnostic run can also capture the
            // DEFAULT gameplay pitch (55) — the angle the player actually soaks at — not only the
            // near-horizontal pitch-8 "look out to the open sea" framing (drew shoreline diagnosis).
            float pitch = ArgFloat("-seawardPitch", seawardPitch);
            float dist = ArgFloat("-seawardDistance", seawardDistance);
            if (orbit != null)
            {
                orbit.SetYaw(seawardYaw);
                orbit.SetPitch(pitch);            // clamped to [minPitch,maxPitch] by OrbitCamera
                orbit.SetDistance(dist);          // -seawardDistance overrides (default gameplay dist=14)
                Debug.Log("[SeaVerifyCapture] orbited seaward: yaw=" + orbit.Yaw +
                          " pitch=" + orbit.Pitch + " dist=" + orbit.Distance);
            }
            for (int i = 0; i < settleFrames; i++) yield return null;

            // -camDiag (INERT read-only): the pale-frame shoreline diagnosis (86ca8t9pq). At the gameplay
            // pitch (55) the seaward frame came back as flat pale sky-blue (#CBE1EF) where the beach should
            // be — refuting BOTH the ticket's water-Y framing AND the prior camera-framing diagnosis. Dump
            // the ACTUAL camera world transform + where the centre ray hits the ground plane + a per-object
            // raycast so the pale region is diagnosed from ground truth, not re-hypothesized. No mutation.
            if (HasArg("-camDiag"))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 cp = cam.transform.position, fwd = cam.transform.forward;
                    string hitDesc = "(no ray hit)";
                    if (Mathf.Abs(fwd.y) > 1e-4f)
                    {
                        float t = (-0.20f - cp.y) / fwd.y; // hit the WaterY plane
                        Vector3 h = cp + fwd * t;
                        hitDesc = $"groundPlaneHit(Y=-0.20) @ {h.ToString("F2")} (t={t:F1})";
                    }
                    Debug.Log($"[camDiag] camPos={cp.ToString("F2")} fwd={fwd.ToString("F3")} " +
                              $"fov={cam.fieldOfView} far={cam.farClipPlane} clear={cam.clearFlags} {hitDesc}");
                    // Physics raycast down the centre ray — what does the camera ACTUALLY see at frame centre?
                    if (Physics.Raycast(cp, fwd, out RaycastHit rh, cam.farClipPlane))
                        Debug.Log($"[camDiag] centre PHYSICS ray hit '{rh.collider.name}' @ {rh.point.ToString("F2")} dist={rh.distance:F1}");
                    else
                        Debug.Log("[camDiag] centre PHYSICS ray hit NOTHING (no collider in the centre look direction)");

                    // The terrain renders pale sky-blue (ambient-washed) though the centre ray hits Ground_Play.
                    // Dump the terrain MESH vertex colours + the MATERIAL shader/tint so the wash is pinned to
                    // mesh-colours-lost vs material-fallback vs tint (the SAME shader renders trees/water fine).
                    foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                    {
                        string nm = mf.name ?? "";
                        if (nm.StartsWith("Ground_") || nm.StartsWith("Water_") || nm.Contains("Canopy") || nm.Contains("Trunk"))
                        {
                            var m = mf.sharedMesh;
                            var mr2 = mf.GetComponent<MeshRenderer>();
                            var col = m != null ? m.colors : null;
                            string c0 = (col != null && col.Length > 0) ? col[0].ToString("F2") : "<none>";
                            var sm = mr2 != null ? mr2.sharedMaterial : null;
                            // Dump the mesh's vertex-attribute layout: a missing/wrong-format COLOR stream is the
                            // SRP-batcher trap (IN.color defaults to white -> ambient washes the mesh sky-blue).
                            string attrs = "";
                            if (m != null)
                                foreach (var a in m.GetVertexAttributes())
                                    attrs += a.attribute + ":" + a.format + "x" + a.dimension + " ";
                            Debug.Log($"[camDiag] MESH '{nm}' verts={(m!=null?m.vertexCount:0)} meshColors={(col!=null?col.Length:0)} " +
                                      $"col0={c0} mat='{(sm!=null?sm.name:"null")}' shader='{(sm!=null?sm.shader.name:"null")}' " +
                                      $"attrs=[{attrs.Trim()}]");
                        }
                    }
                }
            }
            // -hideVista (diagnostic): disable every Vista mesh renderer (peaks + landmass bases) so we can
            // tell whether the pale terrain wash is CAUSED by the vista (drawing over the terrain) or is
            // independent terrain shading. If terrain returns to sand with the vista hidden -> vista is the
            // cause; if it stays sky-blue -> the wash is terrain-intrinsic.
            if (HasArg("-hideVista"))
            {
                int hidden = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name == "Vista" || (t.parent != null && (t.parent.name == "Vista" ||
                        (t.parent.parent != null && t.parent.parent.name == "Vista"))))
                        foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = false; hidden++; }
                }
                Debug.Log($"[camDiag] -hideVista: disabled {hidden} vista renderers");
                yield return null;
            }
            // -clearMagenta (diagnostic): force the camera to clear to MAGENTA instead of the skybox, so a
            // 'pale void' region resolves unambiguously: magenta == no geometry there (the rays miss all
            // meshes); anything non-magenta == real geometry. Disambiguates skybox-vs-washed-mesh.
            if (HasArg("-clearMagenta"))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.magenta;
                    Debug.Log("[camDiag] cleared to MAGENTA (geometry shows real colour; void shows magenta)");
                    yield return null;
                }
            }
            ShotTo(Path.Combine(dir, "sea_seaward.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // -seaDiag (INERT read-only, ticket 86ca9yn57): the SEA-vs-SKY separation diagnosis. The
            // Sponsor's "I can't see any difference between water and sky" trace-confirms (or refutes) the
            // PRIME SUSPECT: the Exp^2 distance fog (colour == WorldLookPalette.SkyHorizon, the mountain
            // seam-kill anchor) ALSO washes the FAR SEA to the sky colour, so the sea↔sky horizon
            // disappears. Read back the seaward frame and dump the MEAN colour of a band just BELOW the
            // horizon line (far sea) vs just ABOVE it (sky) + their per-channel delta. A small delta
            // (< ~0.06) PROVES the sea reads as sky; a clear delta proves separation. No mutation.
            if (HasArg("-seaDiag"))
            {
                yield return new WaitForEndOfFrame(); // ensure the backbuffer holds the seaward frame
                SampleHorizonBands();
            }

            yield return new WaitForSeconds(0.5f);

            Debug.Log("[SeaVerifyCapture] verification complete -> " + dir);
            Application.Quit();
        }

        // Read back the seaward frame and dump the mean sea-band vs sky-band colour + delta (the sea-vs-sky
        // separation diagnostic). Sampled around the horizon line: the horizon for a near-horizontal seaward
        // look sits near vertical-centre, so a band a little below centre is FAR SEA and a band a little above
        // is SKY. The mean over a wide horizontal strip averages out clouds/foam so the read is the dominant
        // sea/sky tone the Sponsor judges.
        private void SampleHorizonBands()
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            // The horizon sits near vertical centre at the near-horizontal seaward pitch. Sky is the UPPER
            // band, sea the LOWER band; leave a gap around the exact horizon so neither band straddles it.
            int cx0 = (int)(w * 0.25f), cx1 = (int)(w * 0.75f);   // central 50% (avoid frame-edge vignette)
            int skyY0 = (int)(h * 0.62f), skyY1 = (int)(h * 0.74f); // a band well ABOVE the horizon (sky)
            int seaY0 = (int)(h * 0.30f), seaY1 = (int)(h * 0.42f); // a band well BELOW the horizon (far sea)

            Color sky = MeanBand(tex, cx0, cx1, skyY0, skyY1);
            Color sea = MeanBand(tex, cx0, cx1, seaY0, seaY1);
            float dR = Mathf.Abs(sea.r - sky.r), dG = Mathf.Abs(sea.g - sky.g), dB = Mathf.Abs(sea.b - sky.b);
            float delta = dR + dG + dB;
            Object.Destroy(tex);

            Debug.Log($"[seaDiag] SKY band (y {skyY0}-{skyY1}) mean={sky.ToString("F3")}");
            Debug.Log($"[seaDiag] SEA band (y {seaY0}-{seaY1}) mean={sea.ToString("F3")}");
            Debug.Log($"[seaDiag] sea-vs-sky channel delta = (|dR|{dR:F3} |dG|{dG:F3} |dB|{dB:F3}) sum={delta:F3} " +
                      (delta < 0.06f ? "<<< SEA READS AS SKY (no horizon separation — fog washes the far sea to the sky stop)"
                                     : ">>> sea reads DISTINCT from sky (clear horizon separation)"));
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

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SeaVerifyCapture] wrote " + file);
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

        // Read a float arg (-flag <value>); falls back to the default if absent/unparseable.
        private float ArgFloat(string flag, float fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag && float.TryParse(args[i + 1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                    return v;
            return fallback;
        }

        // Read an int arg (-flag <value>); falls back to the default if absent/unparseable.
        private int ArgInt(string flag, int fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag && int.TryParse(args[i + 1], out int v))
                    return v;
            return fallback;
        }
    }
}
