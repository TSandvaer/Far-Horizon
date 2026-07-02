using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The REAL snake's AI (ticket 86caaz4vn AC2/AC3/AC4/AC5) — wander + proximity-aggro + a TELEGRAPHED
    /// lunge bite, on top of the 86cah7xxp shared surface (<see cref="SnakeEnemy"/> + <see cref="Health"/> —
    /// this ticket does NOT re-declare an enemy-HP type; it drives the EXISTING seam).
    ///
    /// === The state machine (AC2/AC3) ===
    ///   Wander    — idle slither between seeded patrol points inside <see cref="wanderRadius"/> of HOME.
    ///   Chase     — the player entered <see cref="aggroRadius"/>: face + pursue at <see cref="chaseSpeed"/>.
    ///               Gives up (back to Wander) when the player escapes <see cref="deaggroRadius"/>, the snake
    ///               is dragged past <see cref="leashRadius"/> from home, the chase exceeds
    ///               <see cref="maxChaseSeconds"/>, or the player is dead (a snake never mauls a corpse —
    ///               the enemy-disengage the DeathHandler docstring assigns to this ticket).
    ///   Telegraph — in <see cref="strikeRange"/>: STOP + rear up over <see cref="telegraphSeconds"/> (the
    ///               readable tell — <see cref="SnakeBodyChain"/> renders the rear-up from
    ///               <see cref="TelegraphNormT"/>). Dodgeable by design: the strike only comes after the tell.
    ///   Lunge     — a fast dash along the direction CAPTURED at lunge start (it commits — stepping aside
    ///               dodges it). Bites ONCE per lunge when the player is inside <see cref="biteRadius"/>:
    ///               tier-scaled damage through the ONE <see cref="Health.ApplyDamage"/> seam via
    ///               <see cref="SnakeEnemy.Bite"/> (AC4).
    ///   Cooldown  — recover for <see cref="cooldownSeconds"/>, then reassess (chase again or wander off).
    ///   Dead      — <see cref="Health.Died"/> fired (weapon-vs-snake through the SAME seam — AC5): motion
    ///               stops, the body settles (chain), and the snake DESPAWNS after <see cref="despawnSeconds"/>.
    ///
    /// === Conventions honoured ===
    ///   • All phases are Time.time-ANCHORED (never Time.deltaTime accumulation) and exposed as normalized
    ///     0..1 properties (<see cref="TelegraphNormT"/>/<see cref="LungeNormT"/>) for headless PlayMode
    ///     (procedural-animation-verbs.md: deltaTime≈0 headlessly; anchored phases still complete).
    ///   • NavMesh-based movement (AC2 — the island NavMesh) via the <see cref="NavMeshAgent"/>; a rig with
    ///     NO agent falls back to a planar transform move (the DeathHandler bare-test-rig precedent) so
    ///     PlayMode tests need no baked NavMesh.
    ///   • The snake's own driver — it never touches the player's Animator → CastawayArmPose → HeldAxeRig
    ///     chain (AC3 hard rule).
    ///   • Difficulty is read LIVE from the active tier surface (<see cref="DeathHandler.tier"/> — the
    ///     settings stepper drives it) on every bite via <see cref="SnakeEnemy.ApplyDifficulty"/> (AC4).
    ///   • No per-frame allocs (unity6-mastery §5); patrol RNG is an instance System.Random (deterministic
    ///     from <see cref="patrolSeed"/> — reproducible wander in tests). NO MUTABLE STATICS.
    /// </summary>
    [RequireComponent(typeof(SnakeEnemy))]
    public sealed class SnakeAI : MonoBehaviour
    {
        /// <summary>The snake's behavior states (AC2/AC3). The chain renders pose from this + the NormTs.</summary>
        public enum SnakeState { Wander, Chase, Telegraph, Lunge, Cooldown, Dead }

        [Header("Wiring (serialized editor-time — scene-search fallbacks for bare rigs)")]
        [Tooltip("The player ROOT transform (the moving NavMeshAgent) proximity is measured against.")]
        public Transform player;
        [Tooltip("The player Health the bite damages through the shared seam. Scene-found fallback.")]
        public Health playerHealth;
        [Tooltip("The active-difficulty surface (the settings stepper drives DeathHandler.tier) the bite " +
                 "damage scales by (AC4). Null → Medium.")]
        public DeathHandler deathHandler;

        [Header("Wander (AC2 — idle slither patrol around HOME = the authored spawn)")]
        [Tooltip("Patrol points are picked inside this radius of home.")]
        public float wanderRadius = 6f;
        [Tooltip("Slither speed while patrolling (u/s). Slow — an idle animal.")]
        public float wanderSpeed = 0.9f;
        [Tooltip("Re-pick the patrol point at latest every this many seconds (also on arrival).")]
        public float wanderRepickSeconds = 4f;
        [Tooltip("Deterministic patrol RNG seed (reproducible wander; NOT the island bake stream).")]
        public int patrolSeed = 61405;

        [Header("Aggro / chase (AC2)")]
        [Tooltip("The player entering this planar radius aggros the snake.")]
        public float aggroRadius = 4.5f;
        [Tooltip("The player escaping past this planar radius ends the chase (> aggroRadius).")]
        public float deaggroRadius = 9f;
        [Tooltip("The snake gives up when dragged farther than this from home (chase 'briefly', leashed).")]
        public float leashRadius = 12f;
        [Tooltip("Hard cap on one continuous chase (seconds) — 'chases briefly' by design.")]
        public float maxChaseSeconds = 6f;
        [Tooltip("Pursuit speed (u/s) — faster than wander, slower than the player's run.")]
        public float chaseSpeed = 2.6f;

        [Header("Telegraphed lunge bite (AC3/AC4)")]
        [Tooltip("Planar range at which the chase stops and the telegraph (rear-up) begins.")]
        public float strikeRange = 1.7f;
        [Tooltip("Rear-up duration — the readable, dodgeable tell before the lunge.")]
        public float telegraphSeconds = 0.6f;
        [Tooltip("Lunge dash duration (fast — a strike, not a glide).")]
        public float lungeSeconds = 0.28f;
        [Tooltip("Lunge dash distance along the direction captured at lunge start (it commits — dodgeable).")]
        public float lungeDistance = 1.3f;
        [Tooltip("The bite lands when the player is inside this planar radius during the lunge.")]
        public float biteRadius = 1.0f;
        [Tooltip("Recovery after a lunge before the snake reassesses (chase again / wander).")]
        public float cooldownSeconds = 1.4f;

        [Header("Death (AC5)")]
        [Tooltip("Seconds after death before the snake despawns (deactivates) — the body readably settles first.")]
        public float despawnSeconds = 4f;

        // --- runtime state (instance-only; no statics) ---
        private SnakeEnemy _enemy;
        private Health _health;
        private NavMeshAgent _agent;
        private System.Random _patrolRnd;
        private Vector3 _home;
        private Vector3 _wanderDest;
        private Vector3 _lungeDir;
        private float _stateEnteredAt;
        private float _wanderPickedAt;
        private float _despawnAt;
        private bool _bitThisLunge;
        private bool _initialized;

        // === Observable outcomes (AC7 tests + the -verifySnake shipped capture assert these) ===
        /// <summary>The current behavior state.</summary>
        public SnakeState State { get; private set; } = SnakeState.Wander;
        /// <summary>How many lunges have been FIRED (telegraph completed → dash started).</summary>
        public int LungesFired { get; private set; }
        /// <summary>How many bites have LANDED (player HP actually removed through the seam).</summary>
        public int BitesLanded { get; private set; }
        /// <summary>The HP the LAST landed bite removed (post player-resistance/tier, from ApplyDamage).</summary>
        public float LastBiteDamage { get; private set; }
        /// <summary>Where HOME (the patrol anchor = the authored spawn) is. Test/diagnostic read.</summary>
        public Vector3 Home { get { EnsureInit(); return _home; } }

        /// <summary>Telegraph rear-up phase 0→1 (Time.time-anchored; 0 outside Telegraph). The chain
        /// renders the rear-up from this; headless PlayMode asserts it advances.</summary>
        public float TelegraphNormT => State == SnakeState.Telegraph
            ? Mathf.Clamp01((Time.time - _stateEnteredAt) / Mathf.Max(0.01f, telegraphSeconds)) : 0f;

        /// <summary>Lunge dash phase 0→1 (Time.time-anchored; 0 outside Lunge).</summary>
        public float LungeNormT => State == SnakeState.Lunge
            ? Mathf.Clamp01((Time.time - _stateEnteredAt) / Mathf.Max(0.01f, lungeSeconds)) : 0f;

        /// <summary>The ACTIVE difficulty tier (AC4) — read live from the DeathHandler surface the settings
        /// stepper drives; Medium when unwired (a bare rig).</summary>
        public FarHorizon.SurvivalNeed.DifficultyTier ActiveTier =>
            deathHandler != null ? deathHandler.tier : FarHorizon.SurvivalNeed.DifficultyTier.Medium;

        // ===================== PURE decision guards (the EditMode truth tables — AC7) =====================
        // Static + dependency-free, the ShouldChopOnClick / ShouldAttackOnClick idiom: the state machine's
        // transition predicates are unit-testable with no scene/agent/Input rig.

        /// <summary>Wander→Chase: the ALIVE player entered the aggro radius (and the snake is alive).</summary>
        public static bool ShouldAggro(float playerDistXz, float aggroRadius, bool playerAlive, bool selfAlive)
            => selfAlive && playerAlive && playerDistXz <= aggroRadius;

        /// <summary>Chase→Wander: escape / leash / timeout / dead-player — any one ends the chase.</summary>
        public static bool ShouldGiveUpChase(float playerDistXz, float deaggroRadius,
                                             float homeDist, float leashRadius,
                                             float chaseElapsed, float maxChaseSeconds, bool playerAlive)
            => !playerAlive || playerDistXz > deaggroRadius || homeDist > leashRadius
               || chaseElapsed >= maxChaseSeconds;

        /// <summary>Chase→Telegraph: the player is inside strike range (the tell starts).</summary>
        public static bool ShouldStrike(float playerDistXz, float strikeRange, bool playerAlive)
            => playerAlive && playerDistXz <= strikeRange;

        /// <summary>Lunge bite check: the (single) bite lands when the player is inside the bite radius.</summary>
        public static bool BiteConnects(float playerDistXz, float biteRadius, bool alreadyBitThisLunge)
            => !alreadyBitThisLunge && playerDistXz <= biteRadius;

        // ==================================================================================================

        private void Awake()
        {
            EnsureInit();
        }

        // Lazy init (EditMode has no Awake on AddComponent — the SnakeEnemy/Health lazy-resolve precedent).
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            _enemy = GetComponent<SnakeEnemy>();
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
            _wanderDest = _home;
            _patrolRnd = new System.Random(patrolSeed);
            if (playerHealth == null && player != null) playerHealth = player.GetComponentInChildren<Health>();
        }

        private void OnEnable()
        {
            EnsureInit();
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void OnDied()
        {
            State = SnakeState.Dead;
            _stateEnteredAt = Time.time;
            _despawnAt = Time.time + Mathf.Max(0.1f, despawnSeconds);
            if (_agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh) _agent.ResetPath();
                _agent.enabled = false; // a corpse doesn't path
            }
            Debug.Log("[SnakeAI] snake DIED — settling, despawn in " + despawnSeconds.ToString("0.0") + "s");
        }

        /// <summary>
        /// Fold an externally-applied kill into the state machine NOW: when the snake's Health reads dead
        /// but the state hasn't transitioned (damage applied while disabled / before events wired), runs
        /// the SAME OnDied path the Died event drives. Update calls it every frame; PUBLIC so EditMode —
        /// which has no component lifecycle (no OnEnable/Update on AddComponent) — asserts the death
        /// transition deterministically. Returns true when the snake is dead.
        /// </summary>
        public bool SyncDeathState()
        {
            EnsureInit();
            if (State != SnakeState.Dead && _health != null && _health.IsDead) OnDied();
            return State == SnakeState.Dead;
        }

        private void Update()
        {
            if (SyncDeathState())
            {
                if (Time.time >= _despawnAt && gameObject.activeSelf)
                {
                    Debug.Log("[SnakeAI] despawn (death settled)");
                    gameObject.SetActive(false); // despawn (AC5) — runtime-only; the scene asset is untouched
                }
                return;
            }

            float dt = Time.deltaTime;
            bool playerAlive = playerHealth != null && !playerHealth.IsDead;
            float playerDist = player != null ? PlanarDist(transform.position, player.position) : float.MaxValue;
            float homeDist = PlanarDist(transform.position, _home);

            switch (State)
            {
                case SnakeState.Wander:
                    if (ShouldAggro(playerDist, aggroRadius, playerAlive, true)) { Enter(SnakeState.Chase); break; }
                    // Re-pick a patrol point on arrival or on the repick timer (Time.time-anchored).
                    if (PlanarDist(transform.position, _wanderDest) < 0.4f ||
                        Time.time - _wanderPickedAt >= wanderRepickSeconds)
                        PickWanderDest();
                    MoveTowards(_wanderDest, wanderSpeed, dt);
                    break;

                case SnakeState.Chase:
                    if (ShouldGiveUpChase(playerDist, deaggroRadius, homeDist, leashRadius,
                                          Time.time - _stateEnteredAt, maxChaseSeconds, playerAlive))
                    {
                        Enter(SnakeState.Wander);
                        PickWanderDest();
                        break;
                    }
                    if (ShouldStrike(playerDist, strikeRange, playerAlive)) { Enter(SnakeState.Telegraph); break; }
                    if (player != null) MoveTowards(player.position, chaseSpeed, dt);
                    break;

                case SnakeState.Telegraph:
                    HoldStill();
                    FacePlayerPlanar(dt);
                    if (!playerAlive) { Enter(SnakeState.Wander); break; }
                    if (TelegraphNormT >= 1f)
                    {
                        // Commit the lunge to the direction AT THE TELL'S END — stepping aside now dodges it.
                        Vector3 to = player != null ? player.position - transform.position : transform.forward;
                        to.y = 0f;
                        _lungeDir = to.sqrMagnitude > 1e-6f ? to.normalized : PlanarForward();
                        _bitThisLunge = false;
                        LungesFired++;
                        Enter(SnakeState.Lunge);
                    }
                    break;

                case SnakeState.Lunge:
                    // The dash: cover lungeDistance over lungeSeconds along the committed direction.
                    LungeMove(dt);
                    if (playerAlive && BiteConnects(playerDist, biteRadius, _bitThisLunge))
                    {
                        _bitThisLunge = true;
                        _enemy.ApplyDifficulty(ActiveTier); // AC4 — read the LIVE tier on every bite
                        float removed = _enemy.Bite(playerHealth);
                        if (removed > 0f)
                        {
                            BitesLanded++;
                            LastBiteDamage = removed;
                            Debug.Log("[SnakeAI] BITE landed: -" + removed.ToString("0.0") + " HP (tier=" +
                                      ActiveTier + ", base=" + _enemy.biteDamage.ToString("0.0") + ")");
                        }
                    }
                    if (LungeNormT >= 1f) Enter(SnakeState.Cooldown);
                    break;

                case SnakeState.Cooldown:
                    HoldStill();
                    if (Time.time - _stateEnteredAt >= cooldownSeconds)
                    {
                        if (ShouldAggro(playerDist, aggroRadius, playerAlive, true)) Enter(SnakeState.Chase);
                        else { Enter(SnakeState.Wander); PickWanderDest(); }
                    }
                    break;
            }
        }

        private void Enter(SnakeState next)
        {
            State = next;
            _stateEnteredAt = Time.time;
        }

        private void PickWanderDest()
        {
            _wanderPickedAt = Time.time;
            // Seeded planar point in the wander disc around home (deterministic patrol; runtime-only RNG —
            // NOT the island bake stream, so the seed-42 byte-lock is untouched by construction).
            float ang = (float)(_patrolRnd.NextDouble() * Mathf.PI * 2.0);
            float r = wanderRadius * (0.35f + 0.65f * (float)_patrolRnd.NextDouble());
            _wanderDest = _home + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }

        // Move toward a destination: NavMeshAgent when available + on-mesh (AC2 — the island NavMesh);
        // planar transform fallback otherwise (bare PlayMode rigs — the DeathHandler no-agent precedent).
        private void MoveTowards(Vector3 dest, float speed, float dt)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.SetDestination(dest);
                return;
            }
            Vector3 to = dest - transform.position;
            to.y = 0f;
            float step = speed * dt;
            if (to.magnitude <= step) { transform.position = new Vector3(dest.x, transform.position.y, dest.z); return; }
            transform.position += to.normalized * step;
        }

        private void LungeMove(float dt)
        {
            Vector3 delta = _lungeDir * (lungeDistance / Mathf.Max(0.01f, lungeSeconds)) * dt;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Move(delta); // navmesh-constrained dash
            else transform.position += delta;
        }

        private void HoldStill()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
        }

        // Yaw the ROOT toward the player during the telegraph so the rear-up + lunge visibly aim at him.
        // (The chain orients SEGMENTS along the trail; the root yaw feeds the head's facing + the lunge dir.)
        private void FacePlayerPlanar(float dt)
        {
            if (player == null) return;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-6f) return;
            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-10f * dt));
        }

        private Vector3 PlanarForward()
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        private static float PlanarDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
