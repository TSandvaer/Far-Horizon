using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The REAL snake's BODY (ticket 86caaz4vn AC1/AC3) — poses the editor-baked segment children (head +
    /// tapered body links, warm-banded — the findable serpent silhouette) every LateUpdate so the snake
    /// visibly SLITHERS: the body trails the moving root along its actual PATH (follow-the-trail — the body
    /// bends around turns like a snake, never drags as a rigid stick) with a travelling lateral sine wave,
    /// and renders the AI's verb poses (telegraph rear-up / lunge strike / death settle) from
    /// <see cref="SnakeAI"/>'s Time.time-anchored NormT phases.
    ///
    /// === The snake's OWN driver (AC3 hard rule) ===
    /// This is a SIBLING idiom to CastawayArmPose/HeldAxeRig — it drives ONLY the snake's own segment
    /// transforms and never touches the player's Animator → CastawayArmPose → HeldAxeRig chain. There is no
    /// Animator here at all: the segments are plain baked meshes; the pose IS this LateUpdate.
    ///
    /// === Editor-vs-runtime discipline ===
    /// The segment hierarchy + meshes are authored EDITOR-time by MovementCameraScene.BuildSnake and
    /// serialize into Boot.unity (the Awake-built-hierarchies-don't-serialize trap); this component only
    /// MOVES what is already serialized. Segment world positions are written absolutely each frame, so the
    /// root's NavMeshAgent motion + this pose compose cleanly.
    ///
    /// === Ground honesty ===
    /// Every segment plants on the VISIBLE terrain via a renderer-ENABLED-filtered down-raycast (the
    /// CastawayCharacter.ApplyGroundSnap pattern — the NavMesh rides the invisible collision slab, the eye
    /// sees the dipping/rising visual terrain; snapping to the topmost hit re-opens the float class).
    /// Real-world anchor (lowpoly-quality §0): a snake is a LONG, LOW animal that slithers ON ITS BELLY —
    /// so every segment sits ON the ground (belly contact), only the telegraph rears the FRONT up.
    ///
    /// No per-frame allocs (unity6-mastery §5): the trail is a pre-sized ring-ish List, raycasts are
    /// NonAlloc into a cached buffer. NO MUTABLE STATICS (instance state only).
    /// </summary>
    [DefaultExecutionOrder(60)] // after gameplay Updates (agent has moved the root); poses in LateUpdate
    public sealed class SnakeBodyChain : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The AI whose state + NormT phases select the pose (telegraph/lunge/death).")]
        public SnakeAI ai;
        [Tooltip("The segment transforms, index 0 = HEAD, then body links front→tail (BuildSnake authors " +
                 "these as children and serializes the refs).")]
        public Transform[] segments;

        [Header("Chain")]
        [Tooltip("Arc-length spacing between consecutive segment centers along the trail (slight overlap " +
                 "with the baked link length reads as one continuous body).")]
        public float segmentSpacing = 0.14f;
        [Tooltip("How far the root must move before a new trail sample is recorded.")]
        public float trailSampleDist = 0.03f;

        [Header("Slither (AC2 — the idle/travel undulation)")]
        [Tooltip("Lateral sine amplitude at full crawl (u). Scaled by actual root speed.")]
        public float slitherAmplitude = 0.055f;
        [Tooltip("Wave phase step per segment (radians) — the travelling-wave look.")]
        public float slitherPhasePerSegment = 1.1f;
        [Tooltip("Wave speed (cycles/second) at full crawl.")]
        public float slitherHz = 1.6f;
        [Tooltip("Tiny ambient sway when stationary so the snake reads ALIVE, never statue-frozen " +
                 "(sponsor-prefers-natural-lively-motion; creatures may move — the still rule is for grass).")]
        public float idleSwayAmplitude = 0.012f;

        [Header("Verb poses (AC3 — rendered from SnakeAI's anchored phases)")]
        [Tooltip("How high the HEAD rears at full telegraph (u). Front links rear proportionally less.")]
        public float telegraphLift = 0.32f;
        [Tooltip("How many front links (after the head) join the rear-up.")]
        public int telegraphLinks = 3;

        [Header("Ground snap (the visible-terrain plant — ApplyGroundSnap pattern)")]
        [Tooltip("Ground layers. Zero = the 'Ground' layer.")]
        public LayerMask groundMask;
        [Tooltip("Raycast start height above the segment.")]
        public float groundRayUp = 2f;
        [Tooltip("Raycast reach below the segment.")]
        public float groundRayDown = 6f;

        private readonly List<Vector3> _trail = new List<Vector3>(256); // [0] = newest (at the root)
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private float[] _plantOffsets;   // per-segment half-height (belly-to-center) from the baked mesh
        private Vector3 _lastSampled;
        private Vector3 _prevRootPos;
        private float _speedSmoothed;
        private bool _initialized;

        /// <summary>Nose-to-tail arc length the chain maintains (diagnostic/test read).</summary>
        public float BodyArcLength => segments == null ? 0f : segmentSpacing * Mathf.Max(0, segments.Length - 1);

        // ===================== PURE helpers (EditMode-tested chain math — AC7) =====================

        /// <summary>The travelling lateral slither offset for a segment: a sine wave whose phase runs down
        /// the body, scaled by how fast the snake actually crawls (0 speed → only the tiny idle sway).</summary>
        public static float SlitherOffset(float time, int segmentIndex, float hz, float phasePerSegment,
                                          float amplitude, float idleAmplitude, float speedFactor01)
        {
            float amp = Mathf.Lerp(idleAmplitude, amplitude, Mathf.Clamp01(speedFactor01));
            return Mathf.Sin(time * hz * Mathf.PI * 2f - segmentIndex * phasePerSegment) * amp;
        }

        /// <summary>
        /// The point (+ forward tangent) a given arc DISTANCE back along a newest-first trail. Walks the
        /// polyline [0]=newest backwards; when the trail is shorter than the request, extrapolates past the
        /// oldest sample along the last available direction (a freshly-spawned snake has a short trail).
        /// Pure — EditMode asserts monotonic spacing with no scene.
        /// </summary>
        public static Vector3 PointAlongTrail(IReadOnlyList<Vector3> trail, float distBack,
                                              Vector3 fallbackBackDir, out Vector3 forwardTangent)
        {
            forwardTangent = -fallbackBackDir;
            if (trail == null || trail.Count == 0) { return Vector3.zero; }
            if (trail.Count == 1 || distBack <= 0f)
            {
                Vector3 head = trail[0];
                if (trail.Count > 1) forwardTangent = (trail[0] - trail[1]).normalized;
                return distBack <= 0f ? head : head + fallbackBackDir * distBack;
            }
            float walked = 0f;
            for (int i = 0; i < trail.Count - 1; i++)
            {
                Vector3 a = trail[i];      // newer
                Vector3 b = trail[i + 1];  // older
                float seg = Vector3.Distance(a, b);
                if (seg < 1e-6f) continue;
                if (walked + seg >= distBack)
                {
                    float t = (distBack - walked) / seg;
                    forwardTangent = (a - b).normalized;
                    return Vector3.Lerp(a, b, t);
                }
                walked += seg;
            }
            // Past the oldest sample: extrapolate straight back along the oldest recorded direction.
            Vector3 last = trail[trail.Count - 1];
            Vector3 prev = trail[trail.Count - 2];
            Vector3 backDir = (last - prev).sqrMagnitude > 1e-10f ? (last - prev).normalized : fallbackBackDir;
            forwardTangent = -backDir;
            return last + backDir * (distBack - walked);
        }

        // ==========================================================================================

        private void Awake()
        {
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (ai == null) ai = GetComponent<SnakeAI>();
            if (groundMask.value == 0) groundMask = 1 << LayerMask.NameToLayer("Ground");

            int n = segments != null ? segments.Length : 0;
            _plantOffsets = new float[Mathf.Max(1, n)];
            for (int i = 0; i < n; i++)
            {
                float half = 0.06f;
                if (segments[i] != null)
                {
                    var mf = segments[i].GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        half = Mathf.Max(0.02f, mf.sharedMesh.bounds.extents.y * segments[i].lossyScale.y);
                }
                _plantOffsets[i] = half;
            }

            // Seed the trail from the AUTHORED layout (head at root, body laid out behind) so frame 1
            // already poses a full snake — no pop-in while the real trail accumulates.
            _trail.Clear();
            Vector3 rootP = transform.position;
            _trail.Add(rootP);
            Vector3 back = SeedBackDir();
            for (int i = 1; i < Mathf.Max(2, n); i++)
                _trail.Add(rootP + back * (segmentSpacing * i));
            _lastSampled = rootP;
            _prevRootPos = rootP;
        }

        // The initial "behind the head" direction: from the authored layout when available (head → link1),
        // else the root's planar -forward.
        private Vector3 SeedBackDir()
        {
            if (segments != null && segments.Length >= 2 && segments[0] != null && segments[1] != null)
            {
                Vector3 d = segments[1].position - segments[0].position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) return d.normalized;
            }
            Vector3 f = transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? -f.normalized : Vector3.back;
        }

        private void LateUpdate()
        {
            EnsureInit();
            if (segments == null || segments.Length == 0) return;

            // --- 1. Trail upkeep: sample the root's path; the body follows THIS polyline. ---
            Vector3 rootP = transform.position;
            if ((rootP - _lastSampled).sqrMagnitude >= trailSampleDist * trailSampleDist)
            {
                _trail.Insert(0, rootP);
                _lastSampled = rootP;
                // Cap: keep only the arc the body needs (+50% margin) — no unbounded growth.
                float need = BodyArcLength * 1.5f + 1f;
                float walked = 0f;
                for (int i = 0; i < _trail.Count - 1; i++)
                {
                    walked += Vector3.Distance(_trail[i], _trail[i + 1]);
                    if (walked > need)
                    {
                        _trail.RemoveRange(i + 1, _trail.Count - (i + 1));
                        break;
                    }
                }
            }

            // --- 2. Crawl speed (drives slither amplitude/rate; smoothed so the wave doesn't flicker). ---
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            float rawSpeed = (rootP - _prevRootPos).magnitude / dt;
            _prevRootPos = rootP;
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, rawSpeed, 1f - Mathf.Exp(-6f * dt));
            float speedFactor = Mathf.Clamp01(_speedSmoothed / 1.2f);

            bool dead = ai != null && ai.State == SnakeAI.SnakeState.Dead;
            float telegraphT = ai != null ? ai.TelegraphNormT : 0f;
            // Ease the tell (starts fast, settles into the held rear — reads as a deliberate wind-up).
            float rear = 1f - (1f - telegraphT) * (1f - telegraphT);

            // --- 3. Pose every segment along the trail. ---
            Vector3 fallbackBack = SeedBackDir();
            for (int i = 0; i < segments.Length; i++)
            {
                Transform seg = segments[i];
                if (seg == null) continue;

                Vector3 tangent;
                Vector3 p = PointAlongTrail(_trail, segmentSpacing * i, fallbackBack, out tangent);

                // Slither: lateral to the local path direction. Dead snakes don't slither.
                if (!dead)
                {
                    Vector3 lateral = Vector3.Cross(Vector3.up, tangent).normalized;
                    p += lateral * SlitherOffset(Time.time, i, slitherHz, slitherPhasePerSegment,
                                                 slitherAmplitude, idleSwayAmplitude, speedFactor);
                }

                // Belly plant on the VISIBLE terrain (+ the verb lift on the front links).
                float ground = SampleVisibleGroundY(p);
                float lift = 0f;
                if (!dead && i <= telegraphLinks && rear > 0f)
                {
                    // Head rears full; following links taper off — the coiled-up strike tell.
                    float k = 1f - (i / (float)(telegraphLinks + 1));
                    lift = telegraphLift * rear * k * k;
                }
                float plant = dead ? _plantOffsets[i] * 0.7f : _plantOffsets[i]; // a dead snake settles flatter
                p.y = ground + plant + lift;
                seg.position = p;

                // Orient along the path; the head pitches up with the rear (the readable tell) and the
                // whole chain lies flat when dead.
                if (tangent.sqrMagnitude > 1e-8f)
                {
                    Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
                    if (flat.sqrMagnitude > 1e-8f)
                    {
                        Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        if (!dead && i == 0 && rear > 0f)
                            look = look * Quaternion.Euler(-40f * rear, 0f, 0f); // head-up tell
                        seg.rotation = look;
                    }
                }
            }
        }

        // The CastawayCharacter.ApplyGroundSnap pattern: prefer the highest renderer-ENABLED Ground hit
        // (the VISIBLE terrain), fall back to the topmost hit (bare rigs / proxy-only scenes), else keep
        // the segment's current height (no ground at all — degenerate test rig).
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
            return found ? bestY : at.y - (_plantOffsets.Length > 0 ? _plantOffsets[0] : 0.06f);
        }
    }
}
