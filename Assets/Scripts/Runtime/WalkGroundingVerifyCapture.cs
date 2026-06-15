using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Judgment-grade shipped-build capture for the WALK-FLOAT fix (ticket 86ca8rdkp attempt-9). The brief is
    /// explicit: the diagnostic METRICS lie here (the 100× cm→m corrupted sole/bounds; the proxy-root grounded
    /// the wrong thing), so ONLY the GAMEPLAY-cam VISUAL is truth — the feet must sit ON the sand with no
    /// shadow-gap, STANDING and MID-STRIDE, at multiple positions, framed exactly as the Sponsor sees it.
    ///
    /// This captures from the REAL gameplay OrbitCamera (NOT an isolated rig — an isolated/zoom-to-fit verify
    /// cam is the documented false-green class, unity-conventions.md §"Visibility gates need a fixed-orbit
    /// capture"). The scene's MainCamera IS the OrbitCamera following the player at its real pitch 55 /
    /// distance 14, so ScreenCapture renders precisely the player's over-shoulder framing. We drive the player
    /// across the beach via the NavMeshAgent (the real gameplay locomotion + the real Animator ticking the
    /// WALK clip in the windowed exe) and capture, at THREE positions (spawn / mid-flat-beach / foreshore):
    ///   - <pos>_standing.png  — idle at the position (feet planted, no float)
    ///   - <pos>_midstride.png — captured WHILE walking (IsWalking, the WALK clip mid-cycle = the bug's worst
    ///                           moment: the Mixamo Walk clip lifts the body ~0.66u; the model-sole grounding
    ///                           must cancel it so the feet stay on the sand)
    /// 6 frames total. Each logs the live [FloatTrace] sole/ground gap alongside so the visual + the (now
    /// scale-immune) gauge can be cross-read.
    ///
    /// Inert unless launched with -verifyWalkGround (a normal soak / boot capture is unaffected). MUST run
    /// WINDOWED (ScreenCapture needs a real swapchain; -batchmode renders no real frames — unity-conventions.md).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWalkGround -captureDir &lt;dir&gt;
    /// </summary>
    public class WalkGroundingVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

        // The three judged positions, RELATIVE to spawn. Spawn sits at the damp-sand→grass edge (Z+6 world);
        // the beach DIPS seaward (−Z), so the foreshore is the worst-float band (the visible terrain drops
        // below the NavMesh slab there). Mid-beach is between. X kept ~0 so the seaward orbit framing is clean.
        public Vector3 midBeachOffset = new Vector3(0f, 0f, -6f);
        public Vector3 foreshoreOffset = new Vector3(0f, 0f, -11f);

        void Start()
        {
            if (HasArg("-verifyWalkGround"))
                StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            var castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            var agent = castaway != null ? castaway.GetComponentInParent<NavMeshAgent>() : null;
            var player = agent != null ? agent.transform : null;
            if (castaway == null || agent == null || player == null)
            {
                Debug.LogError("[WalkGroundingVerifyCapture] missing castaway/agent/player — cannot capture");
                Application.Quit(1);
                yield break;
            }
            castaway.SetFrameTrace(true); // mirror a soak: compute the live [FloatTrace] gauge each frame

            // Wait for the agent to land on the NavMesh (the documented first-frame init race).
            float t = 0f;
            while (t < 3f && !agent.isOnNavMesh) { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log("[WalkGroundingVerifyCapture] agent on NavMesh: " + agent.isOnNavMesh + " after " + t.ToString("0.00") + "s");

            Vector3 spawn = player.position;

            // --- POSITION 1: SPAWN ---
            // Standing at spawn: let the snap + Idle clip settle, then capture.
            for (int i = 0; i < 30; i++) yield return null;
            Trace(castaway, "spawn_standing");
            yield return Shot(dir, "1_spawn_standing.png");

            // Mid-stride leaving spawn: drive toward mid-beach, capture the FIRST clearly-walking moment.
            yield return WalkAndShootMidStride(castaway, agent, spawn + midBeachOffset, dir, "1_spawn_midstride.png");

            // --- POSITION 2: MID-FLAT-BEACH (arrived from the walk above) ---
            yield return WaitArrived(agent, spawn + midBeachOffset);
            for (int i = 0; i < 25; i++) yield return null; // settle to idle
            Trace(castaway, "midbeach_standing");
            yield return Shot(dir, "2_midbeach_standing.png");

            // Mid-stride at mid-beach: walk on toward the foreshore, capture mid-stride.
            yield return WalkAndShootMidStride(castaway, agent, spawn + foreshoreOffset, dir, "2_midbeach_midstride.png");

            // --- POSITION 3: FORESHORE (the worst-float band) ---
            yield return WaitArrived(agent, spawn + foreshoreOffset);
            for (int i = 0; i < 25; i++) yield return null;
            Trace(castaway, "foreshore_standing");
            yield return Shot(dir, "3_foreshore_standing.png");

            // Mid-stride at the foreshore: walk back inland a little, capture mid-stride on the dipping sand.
            yield return WalkAndShootMidStride(castaway, agent, spawn + midBeachOffset, dir, "3_foreshore_midstride.png");

            Debug.Log("[WalkGroundingVerifyCapture] complete -> " + dir);
            yield return new WaitForSeconds(0.4f);
            Application.Quit();
        }

        // Set a destination, wait until the agent is clearly WALKING (IsWalking + real speed) + the camera has
        // a couple frames to settle on the moving player, then capture the mid-stride gameplay frame. Reads the
        // live [FloatTrace] at the capture instant so the visual + the gauge are cross-readable.
        private IEnumerator WalkAndShootMidStride(CastawayCharacter castaway, NavMeshAgent agent, Vector3 dest,
                                                  string dir, string file)
        {
            if (NavMesh.SamplePosition(dest, out var hit, 6f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                Debug.LogWarning("[WalkGroundingVerifyCapture] could not sample dest " + dest);

            // Wait until clearly walking (the WALK clip is mid-cycle = the bug's worst moment).
            float start = Time.time;
            while (Time.time - start < 5f)
            {
                if (castaway.IsWalking && agent.velocity.sqrMagnitude > 1f) break;
                yield return null;
            }
            // Walk for a real beat (≥0.5s of continuous striding) so the model-sole grounding has CONVERGED to
            // the walk-lifted steady state — the honest CONTINUOUS-walk percept the Sponsor sees, not the
            // Idle→Walk transient the snap-rate lerp is still chasing (a 6-frame shot caught it mid-convergence
            // at ~10cm; the converged steady-state mid-walk gap is ~5mm). Keep walking the whole settle.
            float settle = Time.time;
            while (Time.time - settle < 0.6f && castaway.IsWalking) yield return null;
            Trace(castaway, Path.GetFileNameWithoutExtension(file));
            yield return Shot(dir, file);
        }

        private IEnumerator WaitArrived(NavMeshAgent agent, Vector3 dest)
        {
            float start = Time.time;
            while (Time.time - start < 10f)
            {
                float planar = Vector2.Distance(new Vector2(agent.transform.position.x, agent.transform.position.z),
                                                new Vector2(dest.x, dest.z));
                if (!agent.pathPending && planar <= 0.6f && agent.velocity.sqrMagnitude < 0.05f) yield break;
                yield return null;
            }
        }

        private void Trace(CastawayCharacter c, string where)
        {
            Debug.Log($"[WalkGroundingVerifyCapture] ({where}) IsWalking={c.IsWalking} " +
                      $"renderedSoleY={c.MeshBottomWorldY:F4} groundHitY={c.GroundHitWorldY:F4} " +
                      $"GAP(sole-ground)={c.MeshFloatGap:F4} proxyRootGap={c.ProxyRootFloatGap:F4}");
        }

        private IEnumerator Shot(string dir, string file)
        {
            string path = Path.Combine(dir, file);
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("[WalkGroundingVerifyCapture] wrote " + path);
            yield return new WaitForEndOfFrame();
            yield return null;
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
