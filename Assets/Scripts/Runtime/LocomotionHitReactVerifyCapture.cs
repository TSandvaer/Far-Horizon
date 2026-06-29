using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the LOCOMOTION + HIT-REACT clip integration (ticket 86cackb3j).
    ///
    /// AC3 (the shipped-build capture gate, unity-conventions.md §editor-vs-runtime): prove in the BUILT exe — not
    /// just the editor — that the castaway visibly plays walk→run AND at least one hit reaction, with the SKINNED
    /// MESH STAYING AT THE PLAYER (no cone-explosion — the Humanoid-retarget failure class, 86ca8rdkp). Editor-only
    /// evidence is insufficient (the legs-up divergence class).
    ///
    /// HOW: walk→run is driven via the SAME input-independent WasdMovement seams RunVerifyCapture uses (the real
    /// gameplay Update path); the HIT-REACT is fired by setting the Animator's Hit trigger + HitRegion int DIRECTLY
    /// on the live Animator component (the gameplay DAMAGE system that would normally drive it is OOS for this ticket
    /// — this capture pokes the public Animator param the controller wires, exercising the SAME state route a real
    /// damage event will). Captures from the REAL OrbitCamera (gameplay framing — an isolated hero rig is the
    /// false-green class, unity-conventions.md §"capture must use the GAMEPLAY camera").
    ///
    /// CONE-EXPLOSION GUARD (the load-bearing AC3 assertion): each frame it measures the skinned mesh's world-bounds
    /// center distance from the player root. A Humanoid-retarget explosion flings the mesh thousands of units
    /// off-spawn; Generic binding keeps it at the player. The run FAILS (non-zero exit) if the mesh ever leaves a
    /// sane radius — so a future regression to Humanoid (or a bad bind) reds the gate instead of shipping a cone.
    ///
    /// Inert unless launched with -verifyHitReact (the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyHitReact -captureDir &lt;dir&gt;
    /// Captures: hitreact_walk.png, hitreact_run.png, hitreact_flinch.png, hitreact_after.png.
    /// </summary>
    public class LocomotionHitReactVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        public float walkSeconds = 2.0f;
        public float runSeconds = 2.5f;

        // The Animator params the controller wires (mirror CastawayCharacter — kept local so this runtime file does
        // not depend on the editor asmdef; an EditMode test pins the param-name match).
        private const string HitParam = "Hit";
        private const string HitRegionParam = "HitRegion";
        private const int HitRegionHead = 1;

        // A cone-explosion sends the mesh far off-spawn; a clean Generic bind keeps the mesh bounds within ~a few
        // units of the player root. 8u is a generous ceiling (the castaway is ~1.8u tall) that a cone blows past
        // by orders of magnitude (thousands of units), so this discriminates explosion-vs-clean unambiguously.
        private const float ConeExplosionRadiusU = 8f;

        void Start()
        {
            if (HasArg("-verifyHitReact"))
            {
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            NavMeshAgent agent = player != null ? player.GetComponent<NavMeshAgent>() : null;
            CastawayCharacter castaway = Object.FindAnyObjectByType<CastawayCharacter>();
            Animator animator = castaway != null && castaway.ModelTransform != null
                ? castaway.ModelTransform.GetComponentInChildren<Animator>()
                : Object.FindAnyObjectByType<Animator>();
            SkinnedMeshRenderer smr = castaway != null && castaway.ModelTransform != null
                ? castaway.ModelTransform.GetComponentInChildren<SkinnedMeshRenderer>()
                : Object.FindAnyObjectByType<SkinnedMeshRenderer>();
            Transform playerRoot = player != null ? player.transform : (castaway != null ? castaway.transform : null);

            float t = 0f;
            while (t < 3f && (agent == null || !agent.isOnNavMesh))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log("[HitReactVerifyCapture] agent on NavMesh: " + (agent != null && agent.isOnNavMesh) +
                      " animator=" + (animator != null) + " smr=" + (smr != null));
            for (int i = 0; i < 5; i++) yield return null;

            float worstMeshGap = 0f; // the worst (largest) mesh-center-to-player distance seen — the cone guard.

            // 1. HIT-REACT FIRST — fire it from a STILL spawn so the gameplay orbit cam stays framed on the
            // character (a long walk/run before the flinch swings the cam off the character — the flinch frame
            // must SHOW the reaction, not empty terrain). Fire the Hit trigger (HitRegion=Head) directly on the
            // live Animator (the gameplay DAMAGE system that would drive it is OOS for this ticket — this exercises
            // the SAME state route a real damage event will).
            for (int i = 0; i < 5; i++) yield return null;
            worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
            ShotTo(Path.Combine(dir, "hitreact_idle.png")); // baseline: framed, standing, before the hit
            yield return new WaitForEndOfFrame();
            yield return null;

            bool firedHit = false;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger(HitRegionParam, HitRegionHead);
                animator.SetTrigger(HitParam);
                firedHit = true;
            }
            Debug.Log("[HitReactVerifyCapture] fired Hit trigger (HitRegion=Head): " + firedHit +
                      (firedHit ? "" : " — NO live Animator/controller found; the flinch can't be driven"));

            float start = Time.time;
            bool shotFlinch = false;
            while (Time.time - start < 1.2f)
            {
                worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                if (!shotFlinch && Time.time - start > 0.25f)
                {
                    ShotTo(Path.Combine(dir, "hitreact_flinch.png")); // the flinch, character framed at spawn
                    shotFlinch = true;
                }
                yield return null;
            }
            if (!shotFlinch) ShotTo(Path.Combine(dir, "hitreact_flinch.png"));
            for (int i = 0; i < 20; i++) yield return null; // let the one-shot finish + return to idle

            // 2. WALK (forward, no Shift). The cam follows; the worstMeshGap guard rides the whole window.
            if (player != null) { player.SetInputOverride(new Vector2(0f, 1f)); player.SetSprintOverride(false); }
            start = Time.time;
            while (Time.time - start < walkSeconds)
            {
                worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                yield return null;
            }
            Debug.Log($"[HitReactVerifyCapture] WALK: IsWalking={(castaway != null && castaway.IsWalking)} " +
                      $"worstMeshGap={worstMeshGap:F2}u");
            ShotTo(Path.Combine(dir, "hitreact_walk.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 3. RUN (forward + Shift). Shoot midway once the cam has settled behind the running character.
            if (player != null) player.SetSprintOverride(true);
            start = Time.time;
            float lastShot = -1f;
            while (Time.time - start < runSeconds)
            {
                worstMeshGap = Mathf.Max(worstMeshGap, MeshGap(smr, playerRoot));
                if (lastShot < 0f && Time.time - start > runSeconds * 0.55f)
                {
                    ShotTo(Path.Combine(dir, "hitreact_run.png"));
                    lastShot = Time.time;
                }
                yield return null;
            }
            if (lastShot < 0f) ShotTo(Path.Combine(dir, "hitreact_run.png"));
            Debug.Log($"[HitReactVerifyCapture] RUN: IsRunning={(castaway != null && castaway.IsRunning)} " +
                      $"worstMeshGap={worstMeshGap:F2}u");

            // 4. AFTER — release + settle back to idle (the cam re-frames on the now-stationary character).
            if (player != null) { player.ClearSprintOverride(); player.ClearInputOverride(); }
            if (agent != null && agent.isOnNavMesh) agent.velocity = Vector3.zero;
            for (int i = 0; i < 16; i++) yield return null;
            ShotTo(Path.Combine(dir, "hitreact_after.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.4f);

            // The cone-explosion guard is the load-bearing AC3 assertion. A clean Generic bind keeps the mesh within
            // ~a few units of the player; a Humanoid explosion is thousands. firedHit must also be true (else the
            // flinch was never exercised in the build).
            bool meshStayed = smr != null && worstMeshGap <= ConeExplosionRadiusU;
            bool pass = firedHit && meshStayed;
            Debug.Log($"[HitReactVerifyCapture] verification complete -> {dir} firedHit={firedHit} " +
                      $"worstMeshGap={worstMeshGap:F2}u (<= {ConeExplosionRadiusU} = mesh stayed at the player, " +
                      $"NO cone-explosion — the Generic-rig bind, 86ca8rdkp) meshStayed={meshStayed} => PASS={pass}");
            Application.Quit(pass ? 0 : 1);
        }

        // The skinned mesh's world-bounds center distance (planar+vertical) from the player root. A clean bind keeps
        // this small; a cone-explosion makes it huge. Returns 0 when no SMR (degenerate rig — the boot capture covers
        // the no-mesh case).
        private static float MeshGap(SkinnedMeshRenderer smr, Transform playerRoot)
        {
            if (smr == null || playerRoot == null) return 0f;
            return Vector3.Distance(smr.bounds.center, playerRoot.position);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[HitReactVerifyCapture] wrote " + file);
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
