using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// THROWAWAY DIAGNOSTIC (86ca8rdkp 4th-attempt — "STILL elevated WHILE WALKING"). Removed before the PR.
    ///
    /// The prior 3 fixes were all verified STANDING / after-walk; the Sponsor's residual elevation is seen
    /// DURING the walk (while MOVING). Standing is grounded (Tess confirmed feet planted + shadow tracks).
    /// So the hole is the MOVING case. This trace drives the player shoreward (into the dipping foreshore,
    /// the worst-case for the snap) and dumps per-frame GROUND TRUTH so we diagnose the actual during-walk
    /// cause instead of guessing (diagnostic-traces-before-hypothesized-fixes):
    ///   - agent root world Y (where the NavMeshAgent plants the root while moving)
    ///   - the snap TARGET it SELECTED (visible terrain) vs the TOPMOST hit (what the old single-ray picked)
    ///   - the SETTLED avatar-feet world Y (the smoothed snap result THIS frame, while moving)
    ///   - the gap feet-vs-visible-terrain (the actual float the Sponsor sees) — at REST it should be ~0
    ///   - the blob-shadow world Y vs the feet (the contact read)
    ///   - the agent planar speed (to correlate the gap with motion)
    ///
    /// Inert unless launched with -walkTrace. Logs lines tagged [walk-trace] for grep.
    ///   FarHorizon.exe -batchmode -walkTrace -logFile &lt;log&gt;   (or windowed)
    /// </summary>
    public class WalkGroundTrace : MonoBehaviour
    {
        public ClickToMove player;
        public CastawayCharacter castaway;
        public Transform blobShadow;
        // Walk shoreward from spawn (Z+6) toward the campfire (4,-8) — across the dipping foreshore band.
        public Vector3 destination = new Vector3(-2f, 0f, -14f);

        void Start()
        {
            if (HasArg("-walkTrace")) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (player == null) player = Object.FindAnyObjectByType<ClickToMove>();
            if (castaway == null) castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            if (blobShadow == null && castaway != null) blobShadow = castaway.blobShadow;
            int groundMask = 1 << LayerMask.NameToLayer("Ground");

            // Wait for the agent onto the NavMesh.
            float t = 0f;
            while (t < 4f && (player == null || player.Agent == null || !player.Agent.isOnNavMesh))
            { t += Time.unscaledDeltaTime; yield return null; }
            NavMeshAgent agent = player != null ? player.Agent : null;
            Debug.Log("[walk-trace] agent on NavMesh: " + (agent != null && agent.isOnNavMesh) +
                      "  baseOffset=" + (agent != null ? agent.baseOffset.ToString("F4") : "n/a") +
                      "  updatePosition=" + (agent != null ? agent.updatePosition.ToString() : "n/a"));

            Vector3 spawn = player != null ? player.transform.position : Vector3.zero;
            // Log a few REST frames first (the standing-grounded baseline the prior fixes verified).
            for (int i = 0; i < 5; i++) { Sample("REST", agent, groundMask); yield return null; }
            // WINDOWED capture (only if launched non-batchmode + given a captureDir): a standing frame.
            yield return Shot("walk_standing");

            bool set = player != null && player.MoveTo(spawn + destination);
            Debug.Log("[walk-trace] MoveTo set=" + set + " target=" + (spawn + destination));

            // Sample EVERY frame for the whole walk (wall-clock window; headless deltas ~0).
            float start = Time.time;
            bool midShot = false;
            while (Time.time - start < 12f)
            {
                Sample("WALK", agent, groundMask);
                // Grab a MID-STRIDE frame once the agent is genuinely moving (the during-walk percept).
                if (!midShot && agent != null &&
                    new Vector2(agent.velocity.x, agent.velocity.z).sqrMagnitude > 1f)
                { midShot = true; yield return Shot("walk_midstride"); }
                if (agent != null && !agent.pathPending && agent.hasPath &&
                    agent.remainingDistance <= 0.3f && agent.velocity.sqrMagnitude < 0.04f)
                    break;
                yield return null;
            }
            // A few REST frames at the destination (does the gap close once stopped?).
            for (int i = 0; i < 6; i++) { Sample("ARRIVED", agent, groundMask); yield return null; }
            yield return Shot("walk_arrived");

            Debug.Log("[walk-trace] complete");
            Application.Quit();
        }

        // Windowed-only shipped-build PNG (ScreenCapture needs a real swapchain — no-op in -batchmode).
        private IEnumerator Shot(string name)
        {
            string dir = CaptureDir();
            if (dir == null || Application.isBatchMode) yield break; // batchmode trace run: log only
            Directory.CreateDirectory(dir);
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[walk-trace] wrote " + file);
            yield return null;
        }

        private static string CaptureDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            return null;
        }

        private void Sample(string phase, NavMeshAgent agent, int groundMask)
        {
            if (player == null || castaway == null) return;
            Transform root = player.transform;
            Vector3 origin = root.position + Vector3.up * 3f;

            // RaycastAll the Ground layer: report BOTH the topmost hit and the highest VISIBLE
            // (renderer-enabled) hit, so we see whether the snap is picking the right surface WHILE MOVING.
            var hits = Physics.RaycastAll(origin, Vector3.down, 15f, groundMask, QueryTriggerInteraction.Ignore);
            float topY = float.NaN, visY = float.NaN;
            string topName = "-", visName = "-";
            foreach (var h in hits)
            {
                if (float.IsNaN(topY) || h.point.y > topY) { topY = h.point.y; topName = h.collider.name; }
                var mr = h.collider.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled && (float.IsNaN(visY) || h.point.y > visY))
                { visY = h.point.y; visName = h.collider.name; }
            }

            // The avatar root is the CastawayCharacter's transform; its world Y == the snapped feet.
            float feetY = castaway.transform.position.y;
            float shadowY = blobShadow != null ? blobShadow.position.y : float.NaN;
            float speed = agent != null ? new Vector2(agent.velocity.x, agent.velocity.z).magnitude : 0f;
            float gapFeetVisible = float.IsNaN(visY) ? float.NaN : feetY - visY;

            var sb = new StringBuilder("[walk-trace] ").Append(phase)
                .Append(" rootY=").Append(root.position.y.ToString("F4"))
                .Append(" spd=").Append(speed.ToString("F2"))
                .Append(" | top=").Append(topName).Append('@').Append(topY.ToString("F4"))
                .Append(" vis=").Append(visName).Append('@').Append(visY.ToString("F4"))
                .Append(" | snapTarget=").Append(castaway.LastSnapTargetWorldY.ToString("F4"))
                .Append(" snapLocalY=").Append(castaway.GroundSnapLocalY.ToString("F4"))
                .Append(" | feetY=").Append(feetY.ToString("F4"))
                .Append(" gapFeet-vis=").Append(gapFeetVisible.ToString("F4"))
                .Append(" | shadowY=").Append(shadowY.ToString("F4"))
                .Append(" gapShadow-vis=").Append((float.IsNaN(visY) ? float.NaN : shadowY - visY).ToString("F4"));
            Debug.Log(sb.ToString());
        }

        private static bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs()) if (a == flag) return true;
            return false;
        }
    }
}
