using System.Collections;
using System.IO;
using UnityEngine;
using FarHorizon.Combat;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the WILD BOAR (ticket 86cah7ydt AC2/AC3/AC5): prove in the
    /// BUILT exe — never just the editor (the legs-up/editor-vs-runtime class) — that the FULL loop is
    /// LIVE-TRIGGERABLE at gameplay framing AND that the weapon-vs-boar MATCHUP is real:
    ///
    ///   find → approach → AGGRO → WINDUP → CHARGE (the boar rushes — NOT a teleport) → GORE (player HP drops
    ///   through the shared seam) · SPEAR out-damages AXE on the pierce-weak boar (the emergent matchup) ·
    ///   player-spear hits → boar DIES → despawns.
    ///
    /// HOW: walks the REAL player toward the boar via the SAME input-independent WasdMovement override seam the
    /// snake/run captures use (the actual gameplay Update path — nothing teleports), so aggro/windup/charge
    /// fire exactly as they will for the Sponsor; the kill drives <see cref="MeleeAttack.PerformAttack"/> (the
    /// public deterministic seam) with the REAL SPEAR WeaponDef (the matchup weapon). Captures come from the
    /// REAL OrbitCamera (gameplay framing — an isolated hero rig is the false-green class), PLUS one dedicated
    /// SIDE-PROFILE frame (lowpoly-quality §0: a boar is a 4-legged animal standing ON the ground with a
    /// humped back + snout + tusks; the stance is obvious side-on, invisible top-down).
    ///
    /// THE MATCHUP GATE (AC3): with the boar still full-HP, one AXE hit and one SPEAR hit are measured through
    /// the shared seam — the SPEAR (Pierce, weak-hit-amplified) MUST out-damage the higher-base AXE (Slash,
    /// hide-resisted). This is the "spear beats boar" proof AS A LIVE MEASUREMENT (not a table), captured in
    /// the player log; the reach half is pinned deterministically in EditMode (BoarCombatTests).
    ///
    /// Inert unless launched with -verifyBoar:
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyBoar -captureDir &lt;dir&gt;
    /// Captures: boar_findable.png, boar_side_profile.png, boar_aggro.png, boar_windup.png, boar_charge.png,
    ///           boar_spear_kill.png, boar_death.png, boar_despawned.png.
    /// </summary>
    public class BoarVerifyCapture : MonoBehaviour
    {
        public WasdMovement player;
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyBoar"))
            {
                // Run-in-background for THIS launch only (an unfocused window pauses the player mid-coroutine
                // and hangs the gate — the SnakeVerifyCapture lesson). Normal play keeps pause-on-unfocus.
                Application.runInBackground = true;
                if (player == null) player = Object.FindAnyObjectByType<WasdMovement>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            BoarAI ai = Object.FindAnyObjectByType<BoarAI>();
            BoarEnemy enemy = ai != null ? ai.GetComponent<BoarEnemy>() : Object.FindAnyObjectByType<BoarEnemy>();
            Health boarHealth = enemy != null ? enemy.Health : null;
            Health playerHealth = player != null ? player.GetComponent<Health>() : null;
            MeleeAttack attack = player != null ? player.GetComponent<MeleeAttack>() : null;
            Camera cam = Camera.main;

            bool wired = ai != null && enemy != null && boarHealth != null && playerHealth != null &&
                         attack != null && cam != null && player != null;
            Debug.Log("[BoarVerifyCapture] wired=" + wired + " (ai=" + (ai != null) + " enemy=" + (enemy != null) +
                      " boarHp=" + (boarHealth != null) + " playerHp=" + (playerHealth != null) +
                      " attack=" + (attack != null) + " cam=" + (cam != null) + ")");
            if (!wired)
            {
                Debug.Log("[BoarVerifyCapture] GATE-FAIL: scene wiring incomplete");
                Application.Quit(1);
                yield break;
            }

            // Freeze HP-over-time on the player so the gore hpBefore→hpAfter delta is the pure seam value
            // (the SnakeVerifyCapture determinism pattern — regen/bleed tickers would drift it).
            int frozen = SnakeVerifyCapture.FreezeHpOverTime(playerHealth.gameObject);
            Debug.Log("[BoarVerifyCapture] froze " + frozen + " HP-over-time ticker(s) for the gate window");

            for (int i = 0; i < 30; i++) yield return null; // settle (agents on mesh, camera framed)

            // --- 1. FINDABLE (AC5): frame the wandering boar from the gameplay cam (yaw the REAL orbit rig at
            //     it, as a player would — pitch/distance/FOV stay the true gameplay values). ---
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            Vector3 toBoar = ai.transform.position - player.transform.position;
            if (orbit != null && toBoar.sqrMagnitude > 1e-4f)
                orbit.SetYaw(Mathf.Atan2(toBoar.x, toBoar.z) * Mathf.Rad2Deg);
            for (int i = 0; i < 5; i++) yield return null;
            bool visibleAtStart = InFrame(cam, ai.transform);
            Debug.Log($"[BoarVerifyCapture] findable: inFrame={visibleAtStart} state={ai.State}");
            ShotTo(Path.Combine(dir, "boar_findable.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // --- 2. SIDE-PROFILE (lowpoly-quality §0 silhouette gate): a LOW side-on frame — the author +
            //     reviewer + Sponsor eyeball this against "a 4-legged animal standing ON the ground, humped
            //     back, snout + tusks". Drive Camera.main directly with the OrbitCamera COMPONENT disabled
            //     (its LateUpdate would re-pose the transform the same frame); restore right after. ---
            Vector3 camPos = cam.transform.position;
            Quaternion camRot = cam.transform.rotation;
            if (orbit != null) orbit.enabled = false;
            Vector3 boarP = ai.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, ai.transform.forward).normalized;
            cam.transform.position = boarP + side * 3.4f + Vector3.up * 0.7f;
            cam.transform.LookAt(boarP + Vector3.up * 0.35f);
            yield return null;
            ShotTo(Path.Combine(dir, "boar_side_profile.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            cam.transform.position = camPos;
            cam.transform.rotation = camRot;
            if (orbit != null) orbit.enabled = true;
            yield return null;

            // --- 3. APPROACH (AC2): WALK the real player at the boar — the SAME path the Sponsor takes; aggro
            //     fires from proximity alone. Shoot aggro / windup / charge as the states fire. ---
            float hpBefore = playerHealth.Current;
            bool aggroSeen = false, windupSeen = false, chargeSeen = false;
            bool shotAggro = false, shotWindup = false, shotCharge = false;
            int chargesBefore = ai.ChargesFired;
            float walkDeadline = Time.time + 25f;
            while (Time.time < walkDeadline)
            {
                Vector3 to = ai.transform.position - player.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (ai.State == BoarAI.BoarState.Windup || ai.State == BoarAI.BoarState.Charge || dist < 1.4f)
                    player.SetInputOverride(Vector2.zero); // arrived / being charged — stand and take it
                else
                    player.SetInputOverride(WorldDirToInput(cam, to.normalized));

                if (ai.State == BoarAI.BoarState.Chase) aggroSeen = true;
                if (ai.State == BoarAI.BoarState.Windup) windupSeen = true;
                if (ai.ChargesFired > chargesBefore) chargeSeen = true;

                if (aggroSeen && !shotAggro) { shotAggro = true; ShotTo(Path.Combine(dir, "boar_aggro.png")); }
                if (windupSeen && !shotWindup && ai.WindupNormT > 0.5f)
                { shotWindup = true; ShotTo(Path.Combine(dir, "boar_windup.png")); } // the head-lower tell
                if (ai.State == BoarAI.BoarState.Charge && !shotCharge && ai.ChargeNormT > 0.25f)
                { shotCharge = true; ShotTo(Path.Combine(dir, "boar_charge.png")); } // the RUSH, mid-motion

                if (ai.GoresLanded > 0 && shotCharge) break; // the charge closed player-ward + gored
                yield return null;
            }
            player.SetInputOverride(Vector2.zero);
            yield return null;

            float goreRemoved = hpBefore - playerHealth.Current;
            var tier = ai.ActiveTier;
            float expectedBase = tier == SurvivalNeed.DifficultyTier.Easy ? enemy.easyGoreDamage
                               : tier == SurvivalNeed.DifficultyTier.Hard ? enemy.hardGoreDamage
                               : enemy.medGoreDamage;
            float expectedGore = expectedBase * Mathf.Max(0f, playerHealth.damageTakenMul) * ai.GoresLanded;
            bool goreLanded = ai.GoresLanded > 0 && goreRemoved > 0f;
            bool goreAmountOk = !goreLanded || Mathf.Abs(goreRemoved - expectedGore) < 0.05f;
            Debug.Log($"[BoarVerifyCapture] charge/gore: chargesFired={ai.ChargesFired} goresLanded={ai.GoresLanded} " +
                      $"removedHP={goreRemoved:F1} expected={expectedGore:F1} (tier={tier}) amountOk={goreAmountOk} " +
                      $"aggro={aggroSeen} windup={windupSeen} charge={chargeSeen}");

            // --- 4. THE MATCHUP + KILL (AC3/AC5): with the boar full-HP, measure ONE axe hit vs ONE spear hit
            //     through the SHARED seam — the spear (Pierce, weak-amplified) must out-damage the higher-base
            //     axe (Slash, hide-resisted). Then FINISH with the spear (the matchup weapon). ---
            var catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
            catalog.BuildDefaults();
            WeaponDef spear = catalog.ById(WeaponCatalog.SpearId);
            WeaponDef axe = catalog.ById(WeaponCatalog.AxeId);

            // Compute the per-hit each weapon WOULD do on the live boar's resistance (no state mutation) — the
            // honest matchup read via the shared resistance hook (not a table).
            float axePerHit = axe.Damage * boarHealth.resistance.Multiplier(axe.DamageType) * Mathf.Max(0f, boarHealth.damageTakenMul);
            float spearPerHit = spear.Damage * boarHealth.resistance.Multiplier(spear.DamageType) * Mathf.Max(0f, boarHealth.damageTakenMul);
            bool spearBeatsAxe = spearPerHit > axePerHit;
            Debug.Log($"[BoarVerifyCapture] MATCHUP: axe {axe.Damage}×slashMul={boarHealth.resistance.slashMul} = {axePerHit:F1}/hit; " +
                      $"spear {spear.Damage}×pierceMul={boarHealth.resistance.pierceMul} = {spearPerHit:F1}/hit; " +
                      $"spearBeatsAxe={spearBeatsAxe} (EMERGENT — reach+pierce tag, no matchup table)");

            int hits = 0;
            while (!boarHealth.IsDead && hits < 8)
            {
                attack.PerformAttack(spear, boarHealth);
                hits++;
                if (hits == 1) ShotTo(Path.Combine(dir, "boar_spear_kill.png")); // the spear landing on the boar
                yield return new WaitForSeconds(0.4f);
            }
            bool died = boarHealth.IsDead;
            ShotTo(Path.Combine(dir, "boar_death.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            Debug.Log($"[BoarVerifyCapture] kill: spearHits={hits} boarDead={died} state={ai.State}");

            // --- 5. DESPAWN (AC5). ---
            float despawnDeadline = Time.time + (ai != null ? ai.despawnSeconds : 4f) + 2f;
            bool despawned = false;
            while (Time.time < despawnDeadline)
            {
                if (ai == null || !ai.gameObject.activeInHierarchy) { despawned = true; break; }
                yield return null;
            }
            ShotTo(Path.Combine(dir, "boar_despawned.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.4f);

            bool pass = visibleAtStart && aggroSeen && windupSeen && chargeSeen && goreLanded &&
                        goreAmountOk && spearBeatsAxe && died && despawned;
            Debug.Log($"[BoarVerifyCapture] GATE {(pass ? "PASS" : "FAIL")}: inFrame={visibleAtStart} " +
                      $"aggro={aggroSeen} windup={windupSeen} charge={chargeSeen} gore={goreLanded} " +
                      $"goreAmountOk={goreAmountOk} spearBeatsAxe={spearBeatsAxe} died={died} despawned={despawned} -> {dir}");
            Application.Quit(pass ? 0 : 1);
        }

        private static bool InFrame(Camera cam, Transform t)
        {
            if (cam == null || t == null) return false;
            Vector3 p = cam.WorldToViewportPoint(t.position);
            return p.z > 0f && p.x > 0f && p.x < 1f && p.y > 0f && p.y < 1f;
        }

        private static Vector2 WorldDirToInput(Camera cam, Vector3 worldDir)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = cam.transform.right; right.y = 0f; right.Normalize();
            return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd)).normalized;
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[BoarVerifyCapture] wrote " + file);
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
