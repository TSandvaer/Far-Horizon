using System.Collections;
using System.IO;
using UnityEngine;
using FarHorizon.Combat;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the REAL SNAKE (ticket 86caaz4vn AC6/AC7): prove in the
    /// BUILT exe — never just the editor (the legs-up/editor-vs-runtime class) — that the FULL loop is
    /// LIVE-TRIGGERABLE at gameplay framing (the #224 UNSOAKABLE anti-goal):
    ///
    ///   find → approach → AGGRO → TELEGRAPH → LUNGE → BITE (player HP drops through the shared seam)
    ///   and: player-weapon hits → snake DIES → despawns.
    ///
    /// HOW: walks the REAL player toward the snake via the SAME input-independent WasdMovement override
    /// seam the run/sneak captures use (the actual gameplay Update path — nothing teleports), so the
    /// aggro/telegraph/lunge fire exactly as they will for the Sponsor; the kill drives
    /// <see cref="MeleeAttack.PerformAttack"/> (the public deterministic seam) with the REAL axe WeaponDef.
    /// Captures come from the REAL OrbitCamera (gameplay framing — an isolated hero rig is the false-green
    /// class), PLUS one dedicated SIDE-PROFILE frame (lowpoly-quality §0: a snake is a LONG LOW belly-down
    /// animal; up-vs-down/low-vs-blob is obvious side-on, invisible top-down) shot by a temporary camera
    /// pose that is restored afterwards.
    ///
    /// PASS gates (exit 0/1 + [SnakeVerifyCapture] GATE lines in the player log):
    ///   aggro seen · telegraph seen · lunge fired · bite removed the TIER-EXPECTED HP · snake visible in
    ///   the gameplay frame (viewport-presence + apparent size) · snake died on weapon hits · despawned.
    ///
    /// Inert unless launched with -verifySnake:
    ///   FarHorizon.exe -screen-fullscreen 0 -verifySnake -captureDir &lt;dir&gt;
    /// Captures: snake_findable.png, snake_aggro.png, snake_telegraph.png, snake_lunge_bite.png,
    ///           snake_side_profile.png, snake_death.png, snake_despawned.png.
    /// </summary>
    public class SnakeVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        // NOTE — apparent-size is a LOGGED DIAGNOSTIC, not a pass/fail floor. Two calibration attempts
        // both mis-gated a plainly-findable snake (a 0.30m HEIGHT probe read 0.0154; the nose-to-tail
        // CHORD read 0.0233 because a slithering body CURLS — pose-dependent), and any transform-projection
        // metric is blind to the buried/washed classes anyway (it measures transforms, not rendered
        // pixels). The findability VISUAL is carried by: the eyeballed captures (author + reviewer +
        // Sponsor soak) and the DETERMINISTIC EditMode pins (banding/warm/winding/length/head-shape in
        // SnakeAITests + SnakeSceneTests). The hard machine gate here is FRUSTUM PRESENCE (in-frame).

        void Start()
        {
            if (HasArg("-verifySnake"))
            {
                // The project ships runInBackground: 0 (a desktop game pauses unfocused — correct for
                // play). A VERIFY run launched from a script has no focus guarantee: an unfocused window
                // pauses the player mid-coroutine and the gate hangs forever with zero captures (observed
                // locally 2026-07-02: Player.log froze at 3111 bytes right after the wired line). The gate
                // must not depend on desktop focus — run-in-background for THIS launch only (flag-scoped;
                // normal play + soak launches keep the shipped pause-on-unfocus behavior).
                Application.runInBackground = true;
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            SnakeAI ai = Object.FindAnyObjectByType<SnakeAI>();
            SnakeEnemy enemy = ai != null ? ai.GetComponent<SnakeEnemy>() : Object.FindAnyObjectByType<SnakeEnemy>();
            Health snakeHealth = enemy != null ? enemy.Health : null;
            Health playerHealth = player != null ? player.GetComponent<Health>() : null;
            MeleeAttack attack = player != null ? player.GetComponent<MeleeAttack>() : null;
            Camera cam = Camera.main;

            bool wired = ai != null && enemy != null && snakeHealth != null && playerHealth != null &&
                         attack != null && cam != null && player != null;
            Debug.Log("[SnakeVerifyCapture] wired=" + wired + " (ai=" + (ai != null) + " enemy=" + (enemy != null) +
                      " snakeHp=" + (snakeHealth != null) + " playerHp=" + (playerHealth != null) +
                      " attack=" + (attack != null) + " cam=" + (cam != null) + ")");
            if (!wired)
            {
                Debug.Log("[SnakeVerifyCapture] GATE-FAIL: scene wiring incomplete");
                Application.Quit(1);
                yield break;
            }

            // Settle: agents on mesh, camera framed.
            for (int i = 0; i < 30; i++) yield return null;

            // --- 1. FINDABLE (AC1/AC6): frame the wandering snake from the gameplay cam at spawn range.
            //     The player freely mouse-orbits in real play, so LOOKING toward the snake is legitimate
            //     gameplay framing — yaw the REAL orbit rig at it (pitch / distance / FOV stay the true
            //     gameplay values; only the look direction is chosen, as a player would). ---
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            Vector3 toSnake = ai.transform.position - player.transform.position;
            if (orbit != null && toSnake.sqrMagnitude > 1e-4f)
                orbit.SetYaw(Mathf.Atan2(toSnake.x, toSnake.z) * Mathf.Rad2Deg);
            for (int i = 0; i < 5; i++) yield return null; // let the yawed framing settle
            var chain = ai.GetComponent<SnakeBodyChain>();
            bool visibleAtStart = SnakeInFrame(cam, ai.transform, chain, out float spanFrac);
            Debug.Log($"[SnakeVerifyCapture] findable: inFrame={visibleAtStart} apparentSpanFrac={spanFrac:F4} " +
                      $"state={ai.State}");
            ShotTo(Path.Combine(dir, "snake_findable.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // --- 2. SIDE-PROFILE (lowpoly-quality §0 silhouette gate): a LOW side-on frame of the snake —
            //     the author + reviewer eyeball this against "a long LOW animal ON the ground". The
            //     OrbitCamera COMPONENT must be disabled while we drive Camera.main or its LateUpdate
            //     re-poses the transform the same frame (the pond/sea captures' proven pattern); restored
            //     right after the shot so every later frame is REAL gameplay framing. ---
            Vector3 camPos = cam.transform.position;
            Quaternion camRot = cam.transform.rotation;
            if (orbit != null) orbit.enabled = false; // stop the rig re-driving the transform each LateUpdate
            Vector3 snakeP = ai.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, ai.transform.forward).normalized;
            cam.transform.position = snakeP + side * 3.2f + Vector3.up * 0.55f;
            cam.transform.LookAt(snakeP + Vector3.up * 0.15f);
            yield return null;
            ShotTo(Path.Combine(dir, "snake_side_profile.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            cam.transform.position = camPos;
            cam.transform.rotation = camRot;
            if (orbit != null) orbit.enabled = true; // gameplay framing back on for every remaining shot
            yield return null;

            // --- 3. APPROACH (AC2/AC6): WALK the real player at the snake — the SAME path the Sponsor
            //     takes. The aggro must fire from proximity alone (nothing is poked programmatically). ---
            float hpBefore = playerHealth.Current;
            bool aggroSeen = false, telegraphSeen = false, lungeSeen = false;
            bool shotAggro = false, shotTelegraph = false, shotLunge = false;
            int lungesBefore = ai.LungesFired;
            float walkDeadline = Time.time + 25f;
            while (Time.time < walkDeadline)
            {
                // Steer the player toward the snake through the REAL movement seam (camera-relative WASD:
                // resolve the world direction into the camera frame the way WasdMovement expects).
                Vector3 to = ai.transform.position - player.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (ai.State == SnakeAI.SnakeState.Telegraph || ai.State == SnakeAI.SnakeState.Lunge || dist < 1.2f)
                    player.SetInputOverride(Vector2.zero); // arrived / being struck — stand and take it
                else
                    player.SetInputOverride(WorldDirToInput(cam, to.normalized));

                if (ai.State == SnakeAI.SnakeState.Chase) aggroSeen = true;
                if (ai.State == SnakeAI.SnakeState.Telegraph) telegraphSeen = true;
                if (ai.LungesFired > lungesBefore) lungeSeen = true;

                if (aggroSeen && !shotAggro)
                {
                    shotAggro = true;
                    ShotTo(Path.Combine(dir, "snake_aggro.png"));
                }
                if (telegraphSeen && !shotTelegraph && ai.TelegraphNormT > 0.5f)
                {
                    shotTelegraph = true;
                    ShotTo(Path.Combine(dir, "snake_telegraph.png")); // the rear-up tell, mid-pose
                }
                if (lungeSeen && !shotLunge)
                {
                    shotLunge = true;
                    ShotTo(Path.Combine(dir, "snake_lunge_bite.png"));
                }
                if (ai.BitesLanded > 0 && shotLunge) break; // the loop closed player-ward
                yield return null;
            }
            player.SetInputOverride(Vector2.zero);
            yield return null;

            float hpAfter = playerHealth.Current;
            float removed = hpBefore - hpAfter;
            // The tier-EXPECTED bite: the enemy's per-tier base × the player's damageTakenMul (both read
            // live — the same math the seam applies; resistance is neutral on the player).
            var tier = ai.ActiveTier;
            float expectedBase = tier == SurvivalNeed.DifficultyTier.Easy ? enemy.easyBiteDamage
                               : tier == SurvivalNeed.DifficultyTier.Hard ? enemy.hardBiteDamage
                               : enemy.medBiteDamage;
            float expected = expectedBase * Mathf.Max(0f, playerHealth.damageTakenMul) * ai.BitesLanded;
            bool biteLanded = ai.BitesLanded > 0 && removed > 0f;
            bool biteAmountOk = !biteLanded || Mathf.Abs(removed - expected) < 0.5f + 0.01f * expected;
            Debug.Log($"[SnakeVerifyCapture] bite: landed={ai.BitesLanded} removedHP={removed:F1} " +
                      $"expected={expected:F1} (tier={tier}) amountOk={biteAmountOk} " +
                      $"aggro={aggroSeen} telegraph={telegraphSeen} lunge={lungeSeen}");

            // --- 4. KILL (AC5): the player's REAL axe WeaponDef through MeleeAttack's deterministic seam.
            //     SnakeMaxHp 24 / axe 14 slash-neutral = dead in 2 hits. ---
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            WeaponDef axe = catalog.ById(WeaponCatalog.AxeId);
            int hits = 0;
            while (!snakeHealth.IsDead && hits < 4)
            {
                attack.PerformAttack(axe, snakeHealth);
                hits++;
                yield return new WaitForSeconds(0.45f); // let the strike read + the death reaction start
            }
            bool died = snakeHealth.IsDead;
            ShotTo(Path.Combine(dir, "snake_death.png")); // the settled body, pre-despawn
            yield return new WaitForEndOfFrame();
            yield return null;
            Debug.Log($"[SnakeVerifyCapture] kill: axeHits={hits} snakeDead={died} state={ai.State}");

            // --- 5. DESPAWN (AC5): the corpse deactivates after despawnSeconds. ---
            float despawnDeadline = Time.time + (ai != null ? ai.despawnSeconds : 4f) + 2f;
            bool despawned = false;
            while (Time.time < despawnDeadline)
            {
                if (ai == null || !ai.gameObject.activeInHierarchy) { despawned = true; break; }
                yield return null;
            }
            ShotTo(Path.Combine(dir, "snake_despawned.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.4f); // let the last screenshot flush to disk

            bool pass = visibleAtStart && aggroSeen && telegraphSeen && lungeSeen && biteLanded &&
                        biteAmountOk && died && despawned;
            Debug.Log($"[SnakeVerifyCapture] GATE {(pass ? "PASS" : "FAIL")}: inFrame={visibleAtStart} " +
                      $"spanFracDiag={spanFrac:F4} (diagnostic only — see header note) " +
                      $"aggro={aggroSeen} telegraph={telegraphSeen} lunge={lungeSeen} bite={biteLanded} " +
                      $"biteAmountOk={biteAmountOk} died={died} despawned={despawned} -> {dir}");
            Application.Quit(pass ? 0 : 1);
        }

        // Is the snake inside the camera frustum, and how LONG does it read on screen? Projects the head
        // and the tail segment to the viewport and measures the 2D nose-to-tail span — the dimension the
        // eye actually finds a long low animal by (a 0.3m height probe framing-failed a plainly-findable
        // snake). A culled / terrain-buried / microscopic snake fails the span floor; the PNG stays the
        // subjective half the author + Sponsor eyeball.
        private static bool SnakeInFrame(Camera cam, Transform snake, SnakeBodyChain chain, out float spanFrac)
        {
            spanFrac = 0f;
            if (cam == null || snake == null) return false;
            Vector3 pRoot = cam.WorldToViewportPoint(snake.position);
            if (pRoot.z <= 0f) return false;
            bool inFrame = pRoot.x > 0f && pRoot.x < 1f && pRoot.y > 0f && pRoot.y < 1f;

            Transform head = chain != null && chain.segments != null && chain.segments.Length > 0
                ? chain.segments[0] : snake;
            Transform tail = chain != null && chain.segments != null && chain.segments.Length > 1
                ? chain.segments[chain.segments.Length - 1] : snake;
            Vector3 pHead = cam.WorldToViewportPoint(head.position);
            Vector3 pTail = cam.WorldToViewportPoint(tail.position);
            if (pHead.z > 0f && pTail.z > 0f)
                spanFrac = Vector2.Distance(new Vector2(pHead.x, pHead.y), new Vector2(pTail.x, pTail.y));
            return inFrame;
        }

        // World direction → the camera-relative Vector2 WasdMovement expects (its input is WASD in the
        // camera frame — the same resolve the run/sneak captures use to steer the real movement path).
        private static Vector2 WorldDirToInput(Camera cam, Vector3 worldDir)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = cam.transform.right; right.y = 0f; right.Normalize();
            return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd)).normalized;
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[SnakeVerifyCapture] wrote " + file);
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
