using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The wild boar's BODY driver (ticket 86cah7ydt AC5) — poses the editor-baked child parts (body / head /
    /// 4 legs / tail) every LateUpdate so the boar reads ALIVE: it stands ON the visible terrain, breathes +
    /// leg-pumps while moving (quality-bar #2 — creatures may move; the still-rule is for grass), LOWERS its
    /// head during the windup/charge (the gore tell + thrust rendered from <see cref="BoarAI"/>'s Time.time-
    /// anchored NormT phases), leans into the rush, and settles flat when dead.
    ///
    /// === The boar's OWN driver (the SnakeBodyChain sibling idiom) ===
    /// A SIBLING to <see cref="SnakeBodyChain"/> / CastawayArmPose / HeldAxeRig — it drives ONLY the boar's own
    /// part transforms and never touches the player's Animator → CastawayArmPose → HeldAxeRig chain. There is
    /// NO Animator, NO rig, NO skinned mesh: the parts are plain baked meshes and the pose IS this LateUpdate.
    /// This is precisely why the boar sidesteps the rigged-Mixamo-FBX-round-trip helicopter class (the v7700
    /// canary) — there is no armature to rebake.
    ///
    /// === Agent-safe ground plant (the SnakeBodyChain contract) ===
    /// The NavMeshAgent owns the ROOT's position (x/z on the mesh, y at baseOffset) — this driver NEVER writes
    /// the root transform (a raw per-frame root write desyncs the agent's sim; the DeathHandler.Warp lesson).
    /// Instead it computes a BODY ORIGIN = the root's XZ snapped to the VISIBLE terrain (a renderer-ENABLED
    /// down-raycast — the SnakeBodyChain / ApplyGroundSnap pattern; the NavMesh rides the invisible collision
    /// slab, the eye sees the dipping visual terrain) + <see cref="groundClearance"/>, and writes each part's
    /// WORLD transform relative to that origin (the SnakeBodyChain seg.position idiom). So the boar's feet
    /// plant on the hills the player sees while the agent's pathing is untouched (lowpoly-quality §0).
    ///
    /// === Additive-offset idiom (procedural-animation-verbs) ===
    /// Each part's AUTHORED local transform is captured ONCE at init as its HOME; every frame the verb pose is
    /// an ADDITIVE OFFSET onto home (never an absolute overwrite), so the authored boar silhouette is the rest
    /// pose the motion plays on top of.
    ///
    /// No per-frame allocs (unity6-mastery §5): raycasts are NonAlloc into a cached buffer. NO MUTABLE STATICS.
    /// </summary>
    [DefaultExecutionOrder(60)] // after gameplay Updates (agent has moved the root); poses in LateUpdate
    public sealed class BoarBodyRig : MonoBehaviour
    {
        // Part-array index convention (the bootstrap authors parts in THIS order; the poser + tests read it).
        public const int BodyIndex = 0;
        public const int HeadIndex = 1;
        public const int LegFrontL = 2;
        public const int LegFrontR = 3;
        public const int LegBackL  = 4;
        public const int LegBackR  = 5;
        public const int TailIndex = 6;
        public const int PartCount = 7;

        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The AI whose state + NormT phases select the pose (windup/charge/death).")]
        public BoarAI ai;
        [Tooltip("The boar parts, indexed 0=body 1=head 2..5=legs(FL,FR,BL,BR) 6=tail (BuildBoar authors these " +
                 "as children + serializes the refs). The poser writes each part's WORLD transform from the " +
                 "terrain-snapped body origin + its HOME local offset + the additive verb pose.")]
        public Transform[] parts;

        [Header("Stance / ground plant")]
        [Tooltip("Height of the body origin above the VISIBLE ground (so the legs reach down to the terrain). " +
                 "BuildBoar sets this to the assembled boar's foot-drop.")]
        public float groundClearance = 0.62f;
        [Tooltip("Ground layers. Zero = the 'Ground' layer.")]
        public LayerMask groundMask;
        [Tooltip("Raycast start height above the root.")] public float groundRayUp = 2.5f;
        [Tooltip("Raycast reach below the root.")] public float groundRayDown = 6f;

        [Header("Motion feel (quality-bar #2 — lively, not static)")]
        [Tooltip("Leg fore/aft swing amplitude at full amble (degrees). Scaled by actual root speed.")]
        public float legSwingDeg = 18f;
        [Tooltip("Gait cycles per second at full amble.")]
        public float gaitHz = 2.2f;
        [Tooltip("Idle breathing bob amplitude (u) — the tiny alive-sway when standing.")]
        public float breatheAmplitude = 0.015f;
        [Tooltip("Head-lower angle at full windup/charge (degrees, pitch DOWN — the gore tell + thrust).")]
        public float headLowerDeg = 34f;
        [Tooltip("Body forward-lean at full charge (degrees) — leaning into the rush.")]
        public float chargeLeanDeg = 12f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Vector3[] _homePos;
        private Quaternion[] _homeRot;
        private Vector3 _prevRootPos;
        private float _speedSmoothed;
        private float _gaitPhase;
        private bool _initialized;

        // Diagonal-gait phase offset per leg (FL/BR together, FR/BL together — a natural trot).
        private static readonly float[] LegPhase = { 0f, Mathf.PI, Mathf.PI, 0f };

        private void Awake() => EnsureInit();

        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (ai == null) ai = GetComponent<BoarAI>();
            if (groundMask.value == 0) groundMask = 1 << LayerMask.NameToLayer("Ground");
            CaptureHome();
            _prevRootPos = transform.position;
        }

        // Capture each part's AUTHORED local transform as its HOME pose (the additive-offset rest pose).
        private void CaptureHome()
        {
            int n = parts != null ? parts.Length : 0;
            _homePos = new Vector3[n];
            _homeRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                if (parts[i] == null) continue;
                _homePos[i] = parts[i].localPosition;
                _homeRot[i] = parts[i].localRotation;
            }
        }

        private void LateUpdate()
        {
            EnsureInit();
            if (parts == null || parts.Length == 0) return;
            if (_homePos == null || _homePos.Length != parts.Length) CaptureHome();

            // --- 1. BODY ORIGIN: the root's XZ (owned by the agent) snapped to the VISIBLE terrain + clearance.
            //     The agent's root transform is NEVER written here (no sim desync). All parts hang off this. ---
            Vector3 rootP = transform.position;
            float groundY = SampleVisibleGroundY(rootP);
            Vector3 bodyOrigin = new Vector3(rootP.x, groundY + groundClearance, rootP.z);
            Quaternion rootRot = transform.rotation;

            // --- 2. Amble speed (drives gait amplitude/rate; smoothed so the pump doesn't flicker). ---
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            Vector3 planarDelta = rootP - _prevRootPos; planarDelta.y = 0f;
            float rawSpeed = planarDelta.magnitude / dt;
            _prevRootPos = rootP;
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, rawSpeed, 1f - Mathf.Exp(-6f * dt));
            float speedFactor = Mathf.Clamp01(_speedSmoothed / 3.2f);
            _gaitPhase += dt * gaitHz * Mathf.PI * 2f * Mathf.Lerp(0.35f, 1f, speedFactor);

            bool dead = ai != null && ai.State == BoarAI.BoarState.Dead;
            float windup = ai != null ? ai.WindupNormT : 0f;
            float charge = ai != null ? ai.ChargeNormT : 0f;
            float windupEase = 1f - (1f - windup) * (1f - windup); // deliberate wind-up ease
            float headDrop = dead ? 0.6f : Mathf.Max(windupEase, charge); // head DOWN through tell + thrust
            float lean = dead ? 0f : charge; // lean into the rush

            // Per-part additive verb rotations (local frame), then composed with the root rotation below.
            for (int i = 0; i < parts.Length; i++)
            {
                Transform part = parts[i];
                if (part == null) continue;

                Vector3 localPos = _homePos[i];
                Quaternion localRot = _homeRot[i];

                if (i == BodyIndex)
                {
                    float breathe = dead ? -0.04f
                        : Mathf.Sin(Time.time * 2.2f) * breatheAmplitude * (1f - speedFactor * 0.6f);
                    localPos += new Vector3(0f, breathe, 0f);
                    localRot = localRot * Quaternion.Euler(chargeLeanDeg * lean, 0f, 0f);
                }
                else if (i == HeadIndex)
                {
                    localRot = localRot * Quaternion.Euler(headLowerDeg * headDrop, 0f, 0f);
                }
                else if (i >= LegFrontL && i <= LegBackR)
                {
                    float swing = dead ? 0f : Mathf.Sin(_gaitPhase + LegPhase[i - LegFrontL]) * legSwingDeg * speedFactor;
                    localRot = localRot * Quaternion.Euler(swing, 0f, 0f);
                }
                else if (i == TailIndex)
                {
                    float wag = dead ? 0f : Mathf.Sin(Time.time * 3.1f) * 8f;
                    localRot = localRot * Quaternion.Euler(0f, wag, 0f);
                }

                // Write the part's WORLD transform from the terrain-snapped body origin (the SnakeBodyChain
                // seg.position idiom — decouples the visual from the agent-owned root y).
                part.position = bodyOrigin + rootRot * localPos;
                part.rotation = rootRot * localRot;
            }
        }

        // The CastawayCharacter.ApplyGroundSnap / SnakeBodyChain pattern: prefer the highest renderer-ENABLED
        // Ground hit (the VISIBLE terrain), fall back to the topmost hit (bare rigs), else keep current height.
        private float SampleVisibleGroundY(Vector3 at)
        {
            Vector3 origin = new Vector3(at.x, at.y + groundRayUp, at.z);
            float maxDist = groundRayUp + groundRayDown;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, maxDist, groundMask.value,
                                            QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestY = 0f;
            for (int i = 0; i < n; i++)
            {
                var mr = _hits[i].collider.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue; // skip the invisible collision-proxy slab
                float y = _hits[i].point.y;
                if (!found || y > bestY) { bestY = y; found = true; }
            }
            if (!found)
            {
                for (int i = 0; i < n; i++)
                {
                    float y = _hits[i].point.y;
                    if (!found || y > bestY) { bestY = y; found = true; }
                }
            }
            return found ? bestY : at.y - groundClearance;
        }
    }
}
