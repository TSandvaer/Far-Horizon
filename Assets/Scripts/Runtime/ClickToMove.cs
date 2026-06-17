using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Path-of-Exile-style NavMesh click-to-move — the production movement foundation.
    ///
    /// Deliberate clean re-implementation of the engine-eval spike's ClickToMove
    /// (EmbergraveUnitySlice, READ-ONLY working spec). Left-click on the ground raycasts
    /// to a point and sets the NavMeshAgent destination; the agent pathfinds there.
    /// MoveTo() is also exposed so later systems (interaction, quests) can drive the agent
    /// programmatically. A short-lived ClickMarker ring is spawned at the destination for
    /// PoE-feel feedback.
    ///
    /// Carries the spike's hard-won fixes:
    ///  - EnsureOnNavMesh(): NavMeshSurface.AddData() runs in the surface's OnEnable, which
    ///    can fire AFTER this agent's first creation attempt -> "Failed to create agent
    ///    because there is no valid NavMesh" + a DEAD click-to-move in the shipped build.
    ///    Retry-warp the agent onto the mesh for ~2s to beat the init-order race.
    ///  - agent.updateRotation/updateUpAxis = false: the visual (a later 3D character, U6)
    ///    owns facing; the agent must not auto-rotate the transform.
    ///
    /// Out of scope for U3 (per ticket 86ca86fme): the spike's billboard/CharacterVisual
    /// coupling — the 3D character supersedes the feet-pivot billboard infra (rescope §1).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ClickToMove : MonoBehaviour
    {
        [Header("Raycast")]
        [Tooltip("Layers the ground click-raycast hits. Default ~0 (everything) is fine for a " +
                 "single flat test ground; narrow to the Ground layer once U5's environment lands.")]
        public LayerMask groundMask = ~0;
        public float clickRayDistance = 500f;

        [Header("Arrival")]
        public float arriveThreshold = 0.3f;

        [Header("Click feedback (optional)")]
        [Tooltip("Spawned at the move destination for PoE-feel feedback. Null = no marker.")]
        public ClickMarker markerPrefab;

        [Header("Locomotion gate (ticket 86ca9yq2x — WASD replaces click-to-move)")]
        [Tooltip("Whether a left-click sets a move destination. The Sponsor-directed pivot to WASD " +
                 "locomotion (86ca9yq2x) sets this FALSE editor-time (WasdMovement disables it on Start), " +
                 "so click-to-move no longer drives the player. The component stays for its PROGRAMMATIC " +
                 "MoveTo seam — the verify captures + the PlayMode harness still drive scripted paths via " +
                 "MoveTo regardless of this gate; only the input-driven HandleClick respects it. Default " +
                 "true so any rig that builds ClickToMove without WasdMovement keeps click-to-move.")]
        public bool clickEnabled = true;

        private NavMeshAgent _agent;
        private Camera _cam;
        private bool _wasMovingTowardGoal;

        public System.Action onArrived;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // The visual (U6 3D character) owns facing; the agent must not rotate the transform.
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
        }

        void Start()
        {
            _cam = Camera.main;
            StartCoroutine(EnsureOnNavMesh());
        }

        // NavMeshSurface.AddData() (which registers the baked NavMesh) runs in the surface's
        // OnEnable, which can fire AFTER this agent's first creation attempt -> "Failed to
        // create agent because there is no valid NavMesh" + a dead click-to-move in the build.
        // Retry for ~2s: each frame, if the agent isn't on a NavMesh yet, sample for one and
        // warp onto it as soon as the surface has registered. Makes click-to-move robust in
        // the shipped player regardless of component init order. (Spike FINDINGS iter-3.)
        private System.Collections.IEnumerator EnsureOnNavMesh()
        {
            float t = 0f;
            while (t < 2f)
            {
                if (_agent != null && _agent.isOnNavMesh)
                    yield break; // already good
                if (_agent != null &&
                    NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                {
                    if (!_agent.enabled) _agent.enabled = true;
                    if (_agent.Warp(hit.position))
                    {
                        Debug.Log("[ClickToMove] warped onto NavMesh at " + hit.position +
                                  " after " + t.ToString("0.00") + "s");
                        yield break;
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            Debug.LogWarning("[ClickToMove] could not place agent on NavMesh within 2s near " +
                             transform.position);
        }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            HandleClick();
            DetectArrival();
        }

        private void HandleClick()
        {
            // WASD locomotion (86ca9yq2x) replaces click-to-move: when clickEnabled is false a left-click
            // no longer sets a destination. The programmatic MoveTo seam (verify captures + harness) is
            // unaffected — only this input-driven path respects the gate.
            if (!clickEnabled) return;
            // Legacy input (ported from the spike): left mouse button = move.
            if (_cam != null && Input.GetMouseButtonDown(0))
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, clickRayDistance, groundMask))
                {
                    MoveTo(hit.point);
                }
            }
        }

        /// <summary>
        /// Pathfind to the nearest NavMesh point to <paramref name="worldPoint"/>. Returns true
        /// if a valid destination was set (the point sampled onto the NavMesh), false otherwise.
        /// </summary>
        public bool MoveTo(Vector3 worldPoint)
        {
            if (_agent != null && _agent.isOnNavMesh &&
                NavMesh.SamplePosition(worldPoint, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
            {
                _agent.SetDestination(navHit.position);
                _wasMovingTowardGoal = true;
                SpawnMarker(navHit.position);
                return true;
            }
            return false;
        }

        public void Stop()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
            _wasMovingTowardGoal = false;
        }

        private void SpawnMarker(Vector3 pos)
        {
            if (markerPrefab == null) return;
            var m = Instantiate(markerPrefab);
            // Lift a hair off the ground so the ring doesn't z-fight with the floor.
            m.transform.position = pos + Vector3.up * 0.02f;
        }

        private void DetectArrival()
        {
            if (!_wasMovingTowardGoal || _agent == null || !_agent.isOnNavMesh) return;
            if (!_agent.pathPending &&
                _agent.remainingDistance <= Mathf.Max(arriveThreshold, _agent.stoppingDistance) &&
                (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.02f))
            {
                _wasMovingTowardGoal = false;
                onArrived?.Invoke();
            }
        }

        public bool IsMoving => _agent != null && _agent.isOnNavMesh &&
                                _agent.hasPath && _agent.velocity.sqrMagnitude > 0.02f;
        public NavMeshAgent Agent => _agent;
    }
}
