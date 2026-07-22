using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The wild BOAR's AI (ticket 86cah7ydt AC2) — wander + proximity-aggro + a TELEGRAPHED CHARGE, the direct
    /// mirror of <see cref="SnakeAI"/> (reuses the enemy-AI/state seam the snake established — NOT a parallel
    /// framework). Drives the EXISTING shared surface (<see cref="BoarEnemy"/> + <see cref="Health"/>); it does
    /// NOT re-declare an enemy-HP type.
    ///
    /// === The state machine (AC2) — Windup→Charge is the boar's signature (why reach matters) ===
    ///   Wander   — idle amble between seeded patrol points inside <see cref="wanderRadius"/> of HOME.
    ///   Chase    — the player entered <see cref="aggroRadius"/>: face + pursue at <see cref="chaseSpeed"/>.
    ///              Gives up (→ Wander) on escape past <see cref="deaggroRadius"/>, drag past
    ///              <see cref="leashRadius"/>, <see cref="maxChaseSeconds"/> timeout, or a dead player.
    ///   Windup   — in <see cref="chargeRange"/> (which is BEYOND a spear's reach): STOP + lower the head /
    ///              paw over <see cref="windupSeconds"/> (the readable, dodgeable tell — the body-rig renders
    ///              it from <see cref="WindupNormT"/>). The charge DIRECTION commits at the tell's END.
    ///   Charge   — a fast straight RUSH along the committed direction covering <see cref="chargeDistance"/>
    ///              over <see cref="chargeSeconds"/>. It reads as a boar rush (NOT a teleport). Gores ONCE per
    ///              charge when the player is inside <see cref="goreRadius"/>: tier-scaled damage through the
    ///              ONE <see cref="Health.ApplyDamage"/> seam via <see cref="BoarEnemy.Gore"/>.
    ///   Cooldown — recover for <see cref="cooldownSeconds"/>, then reassess (charge again or wander off).
    ///   Dead     — <see cref="Health.Died"/> fired: motion stops, the body settles (rig), despawn after
    ///              <see cref="despawnSeconds"/>.
    ///
    /// === Why the charge makes "spear beats boar" EMERGE (AC3) ===
    /// The windup starts at <see cref="chargeRange"/> (4.5u), and the charge RUSHES the boar IN through the
    /// spear's reach band (3.6u) LONG before it reaches gore range (~1.2u). A spear-armed player hits the
    /// charging boar mid-rush — before the gore lands — while a short-reach axe player must let it close to
    /// ~2u (past its gore range) and eats the gore. Nothing here says "spear beats boar" — the advantage is
    /// the COMPOSITION of the charge distance + the weapon's reach (POC AC4) + the boar's pierce tag (AC1).
    ///
    /// === Conventions honoured (the SnakeAI contract) ===
    ///   • All phases are Time.time-ANCHORED (never Time.deltaTime accumulation) + exposed as normalized 0..1
    ///     (<see cref="WindupNormT"/>/<see cref="ChargeNormT"/>) for headless PlayMode (deltaTime≈0).
    ///   • NavMesh movement via the <see cref="NavMeshAgent"/>; a no-agent rig falls back to a planar transform
    ///     move (the SnakeAI/DeathHandler bare-rig precedent) so PlayMode tests need no baked NavMesh.
    ///   • The boar's OWN driver — it never touches the player's Animator → CastawayArmPose → HeldAxeRig chain.
    ///   • Difficulty read LIVE from the active tier surface (<see cref="DeathHandler.tier"/>) on every gore
    ///     via <see cref="BoarEnemy.ApplyDifficulty"/> (AC6).
    ///   • No per-frame allocs (unity6-mastery §5); patrol RNG is an instance System.Random. NO MUTABLE STATICS.
    /// </summary>
    [RequireComponent(typeof(BoarEnemy))]
    public sealed class BoarAI : MonoBehaviour
    {
        /// <summary>The boar's behaviour states (AC2). The body-rig renders pose from this + the NormTs.</summary>
        public enum BoarState { Wander, Chase, Windup, Charge, Cooldown, Dead }

        [Header("Wiring (serialized editor-time — scene-search fallbacks for bare rigs)")]
        [Tooltip("The player ROOT transform proximity is measured against.")]
        public Transform player;
        [Tooltip("The player Health the gore damages through the shared seam. Scene-found fallback.")]
        public Health playerHealth;
        [Tooltip("The active-difficulty surface (the settings stepper drives DeathHandler.tier) the gore " +
                 "damage + HP scale by (AC6). Null → Medium.")]
        public DeathHandler deathHandler;

        [Header("Wander (AC2 — idle amble around HOME = the authored spawn)")]
        [Tooltip("Patrol points are picked inside this radius of home.")]
        public float wanderRadius = 6f;
        [Tooltip("Amble speed while patrolling (u/s). Slow — a rooting animal.")]
        public float wanderSpeed = 1.1f;
        [Tooltip("Re-pick the patrol point at latest every this many seconds (also on arrival).")]
        public float wanderRepickSeconds = 4f;
        [Tooltip("Deterministic patrol RNG seed (reproducible wander; NOT the island bake stream).")]
        public int patrolSeed = 74102;

        [Header("Aggro / chase (AC2)")]
        [Tooltip("The player entering this planar radius aggros the boar (a boar notices you sooner than a snake).")]
        public float aggroRadius = 5.5f;
        [Tooltip("The player escaping past this planar radius ends the chase (> aggroRadius).")]
        public float deaggroRadius = 11f;
        [Tooltip("The boar gives up when dragged farther than this from home (chases briefly, leashed).")]
        public float leashRadius = 15f;
        [Tooltip("Hard cap on one continuous chase (seconds).")]
        public float maxChaseSeconds = 7f;
        [Tooltip("Pursuit speed (u/s) — a boar is quick; faster than the snake's chase.")]
        public float chaseSpeed = 3.2f;

        [Header("NavMesh repath throttle (86cahzycp — SetDestination is a PATH REQUEST, not a cheap setter)")]
        [Tooltip("Re-issue SetDestination only when the wanted destination moved at least this far (u).")]
        public float repathMinMove = 0.5f;
        [Tooltip("...or when this many seconds elapsed since the last issued repath.")]
        public float repathIntervalSeconds = 0.2f;

        [Header("Telegraphed CHARGE (AC2/AC3 — the signature; why reach matters)")]
        [Tooltip("Planar range at which the chase stops and the windup begins. DELIBERATELY BEYOND a spear's " +
                 "reach (3.6) so the boar charges IN through the spear band before it can gore (AC3).")]
        public float chargeRange = 4.5f;
        [Tooltip("Head-lower / paw windup duration — the readable, dodgeable tell before the charge.")]
        public float windupSeconds = 0.7f;
        [Tooltip("Charge rush duration (fast — a rush, not a glide).")]
        public float chargeSeconds = 0.5f;
        [Tooltip("Charge rush distance along the direction captured at windup end (it commits — dodgeable). " +
                 "Long enough to close from chargeRange to inside gore range (the rush reads, not a teleport).")]
        public float chargeDistance = 3.8f;
        [Tooltip("The gore lands when the player is inside this planar radius during the charge.")]
        public float goreRadius = 1.2f;
        [Tooltip("Recovery after a charge before the boar reassesses (charge again / wander).")]
        public float cooldownSeconds = 1.6f;

        [Header("Death (AC5)")]
        [Tooltip("Seconds after death before the boar despawns (deactivates) — the body settles first.")]
        public float despawnSeconds = 4f;

        // --- runtime state (instance-only; no statics) ---
        private BoarEnemy _enemy;
        private Health _health;
        private NavMeshAgent _agent;
        private System.Random _patrolRnd;
        private Vector3 _home;
        private Vector3 _wanderDest;
        private Vector3 _chargeDir;
        private Vector3 _lastRepathDest;
        private float _lastRepathAt = float.NegativeInfinity;
        private float _stateEnteredAt;
        private float _wanderPickedAt;
        private float _despawnAt;
        private bool _goredThisCharge;
        private bool _initialized;

        // === Observable outcomes (AC7 tests + the -verifyBoar shipped capture assert these) ===
        /// <summary>The current behaviour state.</summary>
        public BoarState State { get; private set; } = BoarState.Wander;
        /// <summary>How many charges have been FIRED (windup completed → rush started).</summary>
        public int ChargesFired { get; private set; }
        /// <summary>How many gores have LANDED (player HP actually removed through the seam).</summary>
        public int GoresLanded { get; private set; }
        /// <summary>The HP the LAST landed gore removed (post player-resistance/tier, from ApplyDamage).</summary>
        public float LastGoreDamage { get; private set; }
        /// <summary>NavMesh path requests issued — the 86cahzycp repath-throttle observable (test read).</summary>
        public int RepathsIssued { get; private set; }
        /// <summary>Where HOME (the patrol anchor = the authored spawn) is. Test/diagnostic read.</summary>
        public Vector3 Home { get { EnsureInit(); return _home; } }

        /// <summary>Windup head-lower phase 0→1 (Time.time-anchored; 0 outside Windup). The rig renders the
        /// head-drop tell from this; headless PlayMode asserts it advances.</summary>
        public float WindupNormT => State == BoarState.Windup
            ? Mathf.Clamp01((Time.time - _stateEnteredAt) / Mathf.Max(0.01f, windupSeconds)) : 0f;

        /// <summary>Charge rush phase 0→1 (Time.time-anchored; 0 outside Charge).</summary>
        public float ChargeNormT => State == BoarState.Charge
            ? Mathf.Clamp01((Time.time - _stateEnteredAt) / Mathf.Max(0.01f, chargeSeconds)) : 0f;

        /// <summary>The ACTIVE difficulty tier (AC6) — read live from the DeathHandler surface; Medium unwired.</summary>
        public FarHorizon.SurvivalNeed.DifficultyTier ActiveTier =>
            deathHandler != null ? deathHandler.tier : FarHorizon.SurvivalNeed.DifficultyTier.Medium;

        // ===================== PURE decision guards (the EditMode truth tables — AC7) =====================
        // Static + dependency-free (the SnakeAI.ShouldAggro idiom): unit-testable with no scene/agent/Input rig.

        /// <summary>Wander→Chase: the ALIVE player entered the aggro radius (and the boar is alive).</summary>
        public static bool ShouldAggro(float playerDistXz, float aggroRadius, bool playerAlive, bool selfAlive)
            => selfAlive && playerAlive && playerDistXz <= aggroRadius;

        /// <summary>Chase→Wander: escape / leash / timeout / dead-player — any one ends the chase.</summary>
        public static bool ShouldGiveUpChase(float playerDistXz, float deaggroRadius,
                                             float homeDist, float leashRadius,
                                             float chaseElapsed, float maxChaseSeconds, bool playerAlive)
            => !playerAlive || playerDistXz > deaggroRadius || homeDist > leashRadius
               || chaseElapsed >= maxChaseSeconds;

        /// <summary>Chase→Windup: the player is inside charge range (the tell starts). Charge range is
        /// deliberately &gt; a spear's reach so the rush crosses the spear band before it can gore (AC3).</summary>
        public static bool ShouldCharge(float playerDistXz, float chargeRange, bool playerAlive)
            => playerAlive && playerDistXz <= chargeRange;

        /// <summary>Gore check: the (single) gore lands when the player is inside the gore radius during the
        /// charge, never twice per charge.</summary>
        public static bool GoreConnects(float playerDistXz, float goreRadius, bool alreadyGoredThisCharge)
            => !alreadyGoredThisCharge && playerDistXz <= goreRadius;

        /// <summary>Agent repath throttle (86cahzycp): re-issue SetDestination only on meaningful drift or the
        /// staleness interval; an intent change (secondsSinceLast = +inf) always repaths. Both bounds inclusive.</summary>
        public static bool ShouldRepath(float destMovedDist, float secondsSinceLast, float minMove, float interval)
            => destMovedDist >= minMove || secondsSinceLast >= interval;

        // ==================================================================================================

        private void Awake()
        {
            EnsureInit();
        }

        // Lazy init (EditMode has no Awake on AddComponent — the SnakeAI/Health lazy-resolve precedent).
        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            _enemy = GetComponent<BoarEnemy>();
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
            State = BoarState.Dead;
            _stateEnteredAt = Time.time;
            _despawnAt = Time.time + Mathf.Max(0.1f, despawnSeconds);
            if (_agent != null && _agent.enabled)
            {
                if (_agent.isOnNavMesh) _agent.ResetPath();
                _agent.enabled = false; // a corpse doesn't path
            }
            Debug.Log("[BoarAI] boar DIED — settling, despawn in " + despawnSeconds.ToString("0.0") + "s");
        }

        /// <summary>
        /// Fold an externally-applied kill into the state machine NOW (the SnakeAI.SyncDeathState seam): when
        /// the boar's Health reads dead but the state hasn't transitioned (damage applied while disabled /
        /// before events wired), runs the SAME OnDied path. Update calls it every frame; PUBLIC so EditMode —
        /// which has no component lifecycle — asserts the death transition deterministically. Returns true when
        /// the boar is dead.
        /// </summary>
        public bool SyncDeathState()
        {
            EnsureInit();
            if (State != BoarState.Dead && _health != null && _health.IsDead) OnDied();
            return State == BoarState.Dead;
        }

        private void Update()
        {
            if (SyncDeathState())
            {
                if (Time.time >= _despawnAt && gameObject.activeSelf)
                {
                    Debug.Log("[BoarAI] despawn (death settled)");
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
                case BoarState.Wander:
                    if (ShouldAggro(playerDist, aggroRadius, playerAlive, true)) { Enter(BoarState.Chase); break; }
                    if (PlanarDist(transform.position, _wanderDest) < 0.4f ||
                        Time.time - _wanderPickedAt >= wanderRepickSeconds)
                        PickWanderDest();
                    MoveTowards(_wanderDest, wanderSpeed, dt);
                    break;

                case BoarState.Chase:
                    if (ShouldGiveUpChase(playerDist, deaggroRadius, homeDist, leashRadius,
                                          Time.time - _stateEnteredAt, maxChaseSeconds, playerAlive))
                    {
                        Enter(BoarState.Wander);
                        PickWanderDest();
                        break;
                    }
                    if (ShouldCharge(playerDist, chargeRange, playerAlive)) { Enter(BoarState.Windup); break; }
                    if (player != null) MoveTowards(player.position, chaseSpeed, dt);
                    break;

                case BoarState.Windup:
                    HoldStill();
                    FacePlayerPlanar(dt);
                    if (!playerAlive) { Enter(BoarState.Wander); break; }
                    if (WindupNormT >= 1f)
                    {
                        // Commit the charge to the direction AT THE TELL'S END — stepping aside now dodges it.
                        Vector3 to = player != null ? player.position - transform.position : transform.forward;
                        to.y = 0f;
                        _chargeDir = to.sqrMagnitude > 1e-6f ? to.normalized : PlanarForward();
                        _goredThisCharge = false;
                        ChargesFired++;
                        Enter(BoarState.Charge);
                    }
                    break;

                case BoarState.Charge:
                    // The rush: cover chargeDistance over chargeSeconds along the committed direction.
                    ChargeMove(dt);
                    if (playerAlive && GoreConnects(playerDist, goreRadius, _goredThisCharge))
                    {
                        _goredThisCharge = true;
                        _enemy.ApplyDifficulty(ActiveTier); // AC6 — read the LIVE tier on every gore
                        float removed = _enemy.Gore(playerHealth);
                        if (removed > 0f)
                        {
                            GoresLanded++;
                            LastGoreDamage = removed;
                            Debug.Log("[BoarAI] GORE landed: -" + removed.ToString("0.0") + " HP (tier=" +
                                      ActiveTier + ", base=" + _enemy.goreDamage.ToString("0.0") + ")");
                        }
                    }
                    if (ChargeNormT >= 1f) Enter(BoarState.Cooldown);
                    break;

                case BoarState.Cooldown:
                    HoldStill();
                    if (Time.time - _stateEnteredAt >= cooldownSeconds)
                    {
                        if (ShouldAggro(playerDist, aggroRadius, playerAlive, true)) Enter(BoarState.Chase);
                        else { Enter(BoarState.Wander); PickWanderDest(); }
                    }
                    break;
            }
        }

        private void Enter(BoarState next)
        {
            State = next;
            _stateEnteredAt = Time.time;
            _lastRepathAt = float.NegativeInfinity; // new intent — the next MoveTowards repaths IMMEDIATELY
        }

        private void PickWanderDest()
        {
            _wanderPickedAt = Time.time;
            _lastRepathAt = float.NegativeInfinity;
            float ang = (float)(_patrolRnd.NextDouble() * Mathf.PI * 2.0);
            float r = wanderRadius * (0.35f + 0.65f * (float)_patrolRnd.NextDouble());
            _wanderDest = _home + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }

        // Move toward a destination: NavMeshAgent when available + on-mesh; planar transform fallback otherwise
        // (bare PlayMode rigs — the SnakeAI no-agent precedent). Throttled repath (86cahzycp).
        private void MoveTowards(Vector3 dest, float speed, float dt)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                if (ShouldRepath(PlanarDist(dest, _lastRepathDest), Time.time - _lastRepathAt,
                                 repathMinMove, repathIntervalSeconds))
                {
                    _agent.SetDestination(dest);
                    _lastRepathDest = dest;
                    _lastRepathAt = Time.time;
                    RepathsIssued++;
                }
                return;
            }
            Vector3 to = dest - transform.position;
            to.y = 0f;
            float step = speed * dt;
            if (to.magnitude <= step) { transform.position = new Vector3(dest.x, transform.position.y, dest.z); return; }
            transform.position += to.normalized * step;
        }

        private void ChargeMove(float dt)
        {
            Vector3 delta = _chargeDir * (chargeDistance / Mathf.Max(0.01f, chargeSeconds)) * dt;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Move(delta); // navmesh-constrained rush
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

        // Yaw the ROOT toward the player during the windup so the head-lower tell + charge visibly aim at him.
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
