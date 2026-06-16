using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// BUILD-GATED diagnostic instrument for the HELD-AXE WALK FOLLOW (repurposed for 86ca9zcjn — the held axe
    /// now FOLLOWS the right arm's natural swing, the Sponsor's design choice, soak 6bcc1bc). It was originally
    /// the 86ca9ykp0 walk-bounce/ratchet trace; the AC3 contract is now repurposed: the axe SHOULD swing
    /// per-step WITH the arm (no longer a bug), but it must NOT drift CUMULATIVELY across steps (no ratchet —
    /// guaranteed by construction now that the axe rides the RAW hand with no anchor integration, but GUARDED).
    /// This instrument is the DIAGNOSE-BEFORE-FIX step: it drives a scripted WALK cycle on the REAL player (real
    /// NavMeshAgent + the real Animator ticking the Mixamo WALK clip + the real modelSoleGround Y-bob), and dumps
    /// PER FRAME every Y-reference so we can SEE the per-step swing AND confirm there is no cumulative drift:
    ///   - axeWorldY        — HeldAxeRig'd axe transform world Y (what the Sponsor SEES swing with the arm)
    ///   - handWorldY       — the right-hand bone world Y (the raw animated swing the axe now rides)
    ///   - modelLocalY      — CastawayCharacter._model.localPosition.y (the modelSoleGround per-clip cancel)
    ///   - modelWorldY      — _model world Y (root snap + scale × modelLocalY)
    ///   - followY          — HeldAxeRig.FollowPos.y (the raw — or lightly damped — hand the axe seats to)
    ///   - soleGroundOffset — same as modelLocalY, named for the brief's checklist line
    ///
    /// It also tracks, ACROSS a multi-step walk, the per-STEP SETTLED axe-Y (sampled at the bottom of each idle
    /// pause between scripted segments) and reports the cumulative DRIFT (last settled − first settled). A drift
    /// that GROWS monotonically with step count would be a RATCHET (FORBIDDEN); a bounded value that returns to
    /// baseline between legs is correct (the per-step swing is ALLOWED — it's what the Sponsor wants).
    ///
    /// Inert unless launched with -axeWalkTrace (a normal soak / boot capture is unaffected — the build-gated,
    /// asleep-by-default contract every debug instrument here follows; sibling of FloatDiagnostic's -floatTrace).
    /// Serialized onto the Boot object editor-time (MovementCameraScene.WireAxeWalkTrace) so it SHIPS but stays
    /// inert (the component-in-source-but-not-in-scene trap — unity-conventions.md §editor-vs-runtime).
    ///
    ///   FarHorizon.exe -screen-fullscreen 0 -axeWalkTrace
    /// Reads the per-frame [AxeWalkTrace] lines + the [AxeWalkTrace] STEP/SUMMARY lines from the player log.
    /// </summary>
    public class AxeWalkTrace : MonoBehaviour
    {
        private const string Arg = "-axeWalkTrace";

        [Tooltip("How many scripted WALK steps to drive (each is a short out-then-pause leg). ≥5 so a cumulative " +
                 "ratchet is unmistakable across the run.")]
        public int steps = 6;

        [Tooltip("Per-step walk distance (world units) the player is driven each leg (kept small + along ±Z so " +
                 "the beach is flat-ish under the path and the model bob, not terrain, dominates the axe Y).")]
        public float stepDistance = 1.4f;

        void Start()
        {
            if (HasArg(Arg)) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            var agent = castaway != null ? castaway.GetComponentInParent<NavMeshAgent>() : null;
            var player = agent != null ? agent.transform : null;
            var rig = Object.FindAnyObjectByType<HeldAxeRig>();
            if (castaway == null || agent == null || player == null || rig == null)
            {
                Debug.LogError("[AxeWalkTrace] missing castaway/agent/player/HeldAxeRig — cannot trace");
                Application.Quit(1);
                yield break;
            }

            // Wait for the agent to land on the NavMesh (the documented first-frame init race).
            float t = 0f;
            while (t < 3f && !agent.isOnNavMesh) { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log($"[AxeWalkTrace] agent on NavMesh: {agent.isOnNavMesh} after {t:0.00}s — steps={steps} dist={stepDistance}");

            // Settle at spawn so the grip anchor + the snaps seat before the first step (no startup transient).
            for (int i = 0; i < 40; i++) yield return null;
            float baselineSettledAxeY = rig.transform.position.y;
            DumpFrame(rig, castaway, "spawn_settled", 0);
            Debug.Log($"[AxeWalkTrace] STEP 0 (spawn) settledAxeY={baselineSettledAxeY:F5}");

            Vector3 spawn = player.position;
            int dir = 1; // alternate +Z / −Z each step so we don't run off the beach
            float prevSettled = baselineSettledAxeY;

            for (int s = 1; s <= steps; s++)
            {
                Vector3 dest = spawn + new Vector3(0f, 0f, dir * stepDistance * ((s + 1) / 2));
                if (NavMesh.SamplePosition(dest, out var hit, 6f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);

                // WALK leg: dump every frame while clearly walking (the WALK clip mid-cycle = the bob's worst
                // moment) so the per-frame bounce is captured.
                float legStart = Time.time;
                int wf = 0;
                while (Time.time - legStart < 4f)
                {
                    bool walking = castaway.IsWalking && agent.velocity.sqrMagnitude > 0.5f;
                    if (walking) { DumpFrame(rig, castaway, $"step{s}_walk", wf++); }
                    // arrived?
                    float planar = Vector2.Distance(new Vector2(player.position.x, player.position.z),
                                                    new Vector2(hit.position.x, hit.position.z));
                    if (!agent.pathPending && planar <= 0.5f && agent.velocity.sqrMagnitude < 0.05f) break;
                    yield return null;
                }

                // SETTLE (idle pause) — let the model bob + grip anchor settle, then sample the SETTLED axe Y.
                // This is the number the Sponsor judges "settles HIGHER each step": compare across steps.
                for (int i = 0; i < 45; i++) yield return null;
                float settledAxeY = rig.transform.position.y;
                DumpFrame(rig, castaway, $"step{s}_settled", 0);
                Debug.Log($"[AxeWalkTrace] STEP {s} settledAxeY={settledAxeY:F5} " +
                          $"dStep={(settledAxeY - prevSettled):+0.00000;-0.00000} " +
                          $"dCumulative={(settledAxeY - baselineSettledAxeY):+0.00000;-0.00000} " +
                          $"groundHitY={castaway.GroundHitWorldY:F5}");
                prevSettled = settledAxeY;
                dir = -dir;
            }

            float finalSettled = prevSettled;
            float totalDrift = finalSettled - baselineSettledAxeY;
            Debug.Log($"[AxeWalkTrace] SUMMARY steps={steps} firstSettledAxeY={baselineSettledAxeY:F5} " +
                      $"lastSettledAxeY={finalSettled:F5} CUMULATIVE_DRIFT={totalDrift:+0.00000;-0.00000} " +
                      $"({(Mathf.Abs(totalDrift) > 0.02f ? "RATCHET — axe drifts across steps" : "bounded — no ratchet")})");
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
        }

        // ONE greppable per-frame line carrying every Y-reference the ratchet/bounce could live in.
        private void DumpFrame(HeldAxeRig rig, CastawayCharacter c, string where, int f)
        {
            float axeY = rig.transform.position.y;
            float handY = rig.hand != null ? rig.hand.position.y : float.NaN;
            float modelLocalY = c.ModelLocalY;
            float modelWorldY = c.ModelTransform != null ? c.ModelTransform.position.y : float.NaN;
            float followY = rig.FollowPos.y;
            Debug.Log(
                $"[AxeWalkTrace] {where} f={f} axeWorldY={Fmt(axeY)} handWorldY={Fmt(handY)} " +
                $"modelLocalY={Fmt(modelLocalY)} modelWorldY={Fmt(modelWorldY)} " +
                $"followY={Fmt(followY)} soleGroundOffset={Fmt(modelLocalY)} groundHitY={Fmt(c.GroundHitWorldY)}");
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F5");

        private static bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
