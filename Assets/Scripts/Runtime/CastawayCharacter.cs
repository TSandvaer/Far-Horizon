using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// The player's visual avatar: the HYPER3D + MIXAMO generated castaway — a chunky low-poly young/hopeful
    /// castaway (ticket 86ca8rdkp). This SUPERSEDES the Sketchfab "Mini Chibi Kid": the chibi's realistic-
    /// leaning face couldn't carry the identity, and the Sponsor APPROVED the swap (2026-06-15) after the
    /// viability spike (86ca8r72j / PR #45) proved this asset animates clean + on-style in the shipped URP exe.
    /// The character ships sculpted hair + clothing IN THE MESH (no procedural hair-cap / cap-hide needed —
    /// those were chibi-specific). Its big-head toy proportions are INTRINSIC to the mesh — no bone-scale dials.
    ///
    /// WHY A BAKED SKINNED MESH (the durable fix for a bug CLASS): the spike's earlier PROCEDURAL humanoid
    /// shipped BROKEN in the exe — "legs pointing upwards" — because an Awake-assembled hierarchy serialized
    /// differently than the editor-time build (unity-conventions.md §editor-vs-runtime). A skinned mesh with a
    /// baked bone skeleton + imported clips CANNOT exhibit that failure by construction: the skeleton + skin
    /// live in the FBX, not assembled at runtime. The model child is built EDITOR-TIME (BuildInEditor, called
    /// by MovementCameraScene) so it SERIALIZES into Boot.unity; runtime code only animates what's serialized.
    ///
    /// SELF-DRIVING (the U5/U3 seam): this component lives on a CHILD avatar root under the player (so its
    /// avatar-height scale doesn't scale the NavMeshAgent), and reads the player's <see cref="NavMeshAgent"/>
    /// velocity (resolved from the parent) itself each frame to flip the Idle&lt;-&gt;Walk Animator blend and
    /// yaw the model toward travel. It does NOT modify ClickToMove. The agent has updateRotation=false
    /// (ClickToMove owns that contract), so the visual owns facing.
    ///
    /// MATERIALS — a single flat DE-LIT URP/Lit material (CastawayMat) from texture_diffuse, authored by
    /// CharacterAssetGen + bound onto the SkinnedMeshRenderer editor-time by MovementCameraScene. The de-lit
    /// baked albedo reads flat/toon (the project's URP/Lit toon idiom). The IDENTITY RECOLOR (warm the
    /// generated mustard shirt toward tan) lives in the texture_diffuse PNG's repainted pixels
    /// (CharacterAssetGen.RecolorShirtToTan, a luma-preserving HSV remap) — NOT a per-material tint (which
    /// would flatten the toon gradient — the trap this class always warned of).
    /// </summary>
    public class CastawayCharacter : MonoBehaviour
    {
        [Header("Model source (wired by MovementCameraScene at author time)")]
        // The imported with-skin Idle FBX prefab (rig + Idle clip + Humanoid avatar) and the Idle<->Walk
        // controller. CharacterAssetGen produces both; MovementCameraScene also binds the de-lit material.
        public GameObject modelPrefab;
        public RuntimeAnimatorController animatorController;

        [Header("Facing")]
        [Tooltip("How fast the body yaws toward the travel direction (higher = snappier).")]
        public float turnLerp = 12f;

        [Header("Locomotion thresholds")]
        [Tooltip("Planar speed (u/s) above which the character is considered walking (drives the " +
                 "Idle<->Walk blend). Squared internally.")]
        public float walkSpeedThreshold = 0.15f;

        [Header("Ground snap (86ca8rdkp soak-fix #1 — 'walking in the air')")]
        [Tooltip("Snap the avatar feet to the VISIBLE terrain each frame. The NavMeshAgent grounds the " +
                 "player ROOT on the flat NavMesh collider, which sits ABOVE the dipping Zone-D visual " +
                 "terrain (ground-trace 2026-06-15: feet at 0.081 vs visible sand at 0.020 = a 6cm float). " +
                 "A downward raycast onto the Ground layer plants the feet on the VISIBLE-terrain collider " +
                 "(the renderer-ENABLED Ground hit) — skipping the flat NavMesh slab whose renderer is off.")]
        public bool groundSnap = true;
        [Tooltip("Layer mask the ground-snap raycast tests (the VISIBLE terrain — the Ground layer). " +
                 "Wired editor-time; defaults to the Ground layer at runtime if unset.")]
        public LayerMask groundMask;
        [Tooltip("How far above the player root the ground ray starts (must clear the avatar's own height " +
                 "so the ray doesn't originate inside the mesh).")]
        public float groundRayUp = 3f;
        [Tooltip("Max ground-ray distance below the start point.")]
        public float groundRayDown = 12f;

        [Header("Contact shadow (86ca8rdkp re-soak #2 — 'he STILL seems elevated')")]
        [Tooltip("The blob/contact shadow under the feet. The shadow is a child of the PLAYER ROOT and does " +
                 "NOT inherit the avatar ground-snap, so on the dipping foreshore it was STRANDED ~9cm ABOVE " +
                 "the snapped feet — the body read as floating above its own shadow = 'elevated' (foot-trace " +
                 "2026-06-15: feet planted at the sand, but BlobShadow 9cm above them). Driving the shadow's " +
                 "world-Y down onto the snapped feet each frame grounds the contact read. Wired editor-time.")]
        public Transform blobShadow;
        [Tooltip("Small lift (world units) of the shadow above the snapped feet so it doesn't z-fight the sand.")]
        public float blobShadowLift = 0.02f;

        // Animator parameter the controller blends on (set each frame from the agent's velocity).
        public const string MovingParam = "Moving";

        private NavMeshAgent _agent;
        private Animator _animator;
        private Transform _model;       // the instantiated FBX root, yaw-rotated toward facing
        private float _bodyYaw;
        private Vector3 _lastFacing = Vector3.forward;
        private bool _built;

        // Exposed for tests / later systems: current Idle/Walk state read off the agent.
        public bool IsWalking { get; private set; }

        /// <summary>
        /// Force the model to a KNOWN body yaw immediately (verification-only determinism hook). The verify
        /// capture cannot rely on the rest-state facing being a fixed axis, so it pins the facing to a known
        /// value before framing. Inert for normal gameplay (LateUpdate keeps driving facing off velocity).
        /// </summary>
        /// <param name="yawDeg">World Y yaw (0 = the model's rest/intrinsic facing).</param>
        public void FaceWorldYawInstant(float yawDeg)
        {
            _bodyYaw = yawDeg;
            float rad = yawDeg * Mathf.Deg2Rad;
            _lastFacing = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            if (_model != null)
                _model.localRotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }

        /// <summary>Current body yaw (degrees) the model is rotated to — exposed so the verify capture can
        /// derive the camera position from the SAME facing it pinned (no axis assumption).</summary>
        public float BodyYaw => _bodyYaw;

        void Awake()
        {
            // The agent lives on the player ROOT (this component is on a child avatar root so its
            // height-scale doesn't scale the agent). Resolve it from the parent chain.
            _agent = GetComponentInParent<NavMeshAgent>();
            if (_agent == null)
                Debug.LogWarning("[CastawayCharacter] no NavMeshAgent found in parents — the " +
                                 "Idle<->Walk anim + facing won't drive (avatar must be a child of the " +
                                 "player root that carries the agent)");
            // If the author-time build already serialized the Model child into the scene (the ship path),
            // re-bind to it rather than re-instantiate. Otherwise build at runtime (defensive fallback).
            if (transform.childCount > 0 && _model == null) RebindFromHierarchy();
            if (!_built) BuildModel();
        }

        /// <summary>
        /// Editor build entry: MovementCameraScene calls this so the Model child + the Animator controller
        /// reference SERIALIZE into Boot.unity (the editor-vs-runtime serialization lesson). Idempotent:
        /// clears prior children first.
        /// </summary>
        public void BuildInEditor()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _model = null; _animator = null; _built = false;
            BuildModel();
        }

        private void RebindFromHierarchy()
        {
            _model = transform.childCount > 0 ? transform.GetChild(0) : null;
            _animator = GetComponentInChildren<Animator>();
            _built = _model != null;
        }

        private void BuildModel()
        {
            if (modelPrefab == null)
            {
                Debug.LogError("[CastawayCharacter] modelPrefab not wired — cannot build avatar");
                return;
            }

            // Instantiate the imported FBX under the avatar root. In the editor-build path the resulting
            // SkinnedMeshRenderer + bone hierarchy SERIALIZE into Boot.unity, so the shipped exe loads the
            // SAME baked skeleton the editor sees (no Awake-assembled hierarchy to diverge — the legs-up
            // lesson). Works for both editor-build + runtime-fallback with no UnityEditor dependency.
            GameObject go = Instantiate(modelPrefab, transform, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;   // FBX origin is at the feet -> grounded feet
            go.transform.localRotation = Quaternion.identity;
            _model = go.transform;

            _animator = go.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = go.AddComponent<Animator>();
            // Preserve the FBX-imported avatar across the controller assignment: capture it FIRST, assign the
            // controller, then RE-ASSERT — an Animator with a missing/cleared avatar at runtime can't bind the
            // clips to the skeleton and renders a frozen/collapsed skin. 86ca8rdkp.
            Avatar imported = _animator.avatar;
            if (imported == null)
                Debug.LogWarning("[CastawayCharacter] Animator has NO avatar — clips will not bind " +
                                 "(Idle.fbx must import Generic with avatarSetup=CreateFromThisModel)");
            if (animatorController != null) _animator.runtimeAnimatorController = animatorController;
            if (imported != null) _animator.avatar = imported; // re-assert post-controller
            _animator.applyRootMotion = false; // NavMeshAgent drives position; anim is in-place

            // No cap-hide / procedural hair here (chibi-specific, removed for the Hyper3D castaway — it ships
            // sculpted hair in the mesh). The de-lit material is bound editor-time by MovementCameraScene.
            _built = true;
        }


        // The avatar root's local Y is normally 0 (feet on the player root). The ground-snap drives it to
        // a (usually small NEGATIVE) value so the feet plant on the VISIBLE terrain that dips below the
        // agent's NavMesh ground point. Smoothed so terrain undulation doesn't pop the avatar.
        private float _snapLocalY;
        private bool _snapInit;

        /// <summary>The current ground-snap local-Y applied to the avatar root (0 = no snap). Exposed for
        /// the PlayMode grounding regression so it can assert the feet are planted on the visible surface.</summary>
        public float GroundSnapLocalY => _snapLocalY;

        /// <summary>The WORLD Y of the surface the LAST ground-snap raycast SELECTED to plant the feet on
        /// (NaN if no ground was hit). This is the snap TARGET before smoothing — exposed so the PlayMode
        /// regression can assert the snap picks the VISIBLE terrain (not the proxy slab) WITHOUT depending on
        /// the smoothed lerp converging, which it can't in headless runs (Time.deltaTime≈0, the documented
        /// headless-time trap — unity-conventions.md §Headless).</summary>
        public float LastSnapTargetWorldY { get; private set; } = float.NaN;

        // Reusable RaycastAll buffer (no per-frame GC). Sized for the handful of Ground colliders a single
        // down-ray can ever cross (the visible terrain + the flat NavMesh slab + a little headroom).
        private readonly RaycastHit[] _snapHits = new RaycastHit[8];

        // Snap the avatar feet to the VISIBLE terrain (86ca8rdkp soak-fix #1; RE-SOAK fix — "he STILL walks
        // elevated"). The NavMeshAgent grounds the player ROOT on the flat NavMesh collider; the FBX-origin
        // feet ride that root. We raycast the Ground layer down and plant the feet on the VISIBLE-terrain hit.
        //
        // RE-SOAK ROOT CAUSE (ground-trace 2026-06-15, -groundTrace; OVERTURNED the "snap is enough" assumption):
        // the PR #47 snap used a SINGLE Physics.Raycast and took the TOPMOST/closest Ground hit, assuming the
        // visible terrain is always the highest surface. That holds INLAND (the meadow rises above the slab) —
        // but on the SEAWARD FORESHORE the Zone-D terrain DIPS BELOW the flat NavMesh slab (Y=0): trace measured
        // the visible sand at −0.02 … −0.43 while TestGround stayed at 0.000, and the single ray returned the
        // SLAB (the higher surface). So walking shoreward the feet stayed pinned at Y≈0 while the visible sand
        // dropped away — a growing ~0.43u float = the Sponsor's "he still walks elevated" (worst toward the
        // shore/campfire). Idle-at-spawn looked fine because there the sand IS the topmost hit.
        //
        // FIX: RaycastAll and select the VISIBLE terrain — the hit whose collider carries a renderer-ENABLED
        // MeshRenderer. The TestGround slab is a collision/NavMesh PROXY with its renderer DISABLED (the grey-
        // slab soak-fix, unity-conventions.md §NavMesh) — so "renderer enabled" cleanly distinguishes the sand
        // the player SEES from the invisible slab. Among visible Ground hits, take the HIGHEST (defensive — a
        // single visible terrain is expected). Runs in both Idle and Walk (the float is in BOTH; motion just
        // makes it obvious). Smoothed to avoid popping on slopes.
        private void ApplyGroundSnap()
        {
            if (!groundSnap) return;
            Transform root = transform.parent != null ? transform.parent : transform; // the player root
            int mask = groundMask.value != 0 ? groundMask.value : (1 << LayerMask.NameToLayer("Ground"));
            Vector3 origin = root.position + Vector3.up * groundRayUp;
            float maxDist = groundRayUp + groundRayDown;

            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _snapHits, maxDist, mask,
                                            QueryTriggerInteraction.Ignore);
            // Pick the VISIBLE terrain: the highest hit whose collider has a renderer-ENABLED MeshRenderer
            // (skips the renderer-disabled flat NavMesh slab — the bug the re-soak trace exposed).
            bool found = false;
            float bestY = 0f;
            for (int i = 0; i < n; i++)
            {
                var mr = _snapHits[i].collider.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;          // skip the invisible collision-proxy slab
                float y = _snapHits[i].point.y;
                if (!found || y > bestY) { bestY = y; found = true; }
            }
            // Fallback: if NO visible Ground was hit (e.g. a scene with only the proxy slab — the PlayMode
            // rigs, or an inland spot where the proxy is the only collider), use the topmost hit so the snap
            // still grounds rather than going inert.
            if (!found && n > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    float y = _snapHits[i].point.y;
                    if (!found || y > bestY) { bestY = y; found = true; }
                }
            }
            if (!found) { LastSnapTargetWorldY = float.NaN; return; }
            LastSnapTargetWorldY = bestY; // the SELECTED surface (pre-smoothing) — the snap-target the test reads

            // Desired avatar-root WORLD Y = the visible-terrain Y. local Y = worldY - root.position.y
            // (the avatar root is a direct child of the player root, no intermediate offset).
            float desiredLocalY = bestY - root.position.y;
            if (!_snapInit) { _snapLocalY = desiredLocalY; _snapInit = true; }
            else _snapLocalY = Mathf.Lerp(_snapLocalY, desiredLocalY, 1f - Mathf.Exp(-18f * Time.deltaTime));
            Vector3 lp = transform.localPosition;
            lp.y = _snapLocalY;
            transform.localPosition = lp;

            // RE-SOAK #2 — ground the CONTACT SHADOW to the snapped feet. The shadow is a child of the player
            // root (it must NOT inherit the avatar height-scale), so it does not get the avatar's ground-snap;
            // left alone it strands ~9cm ABOVE the feet on the dipping foreshore (body floats above its shadow
            // = the 'elevated' read — foot-trace 2026-06-15). Drive its WORLD-Y onto the snapped sole each
            // frame (the SAME bestY the feet snap to), so the shadow sits AT the feet on flat AND dipping sand.
            if (blobShadow != null)
            {
                Vector3 sp = blobShadow.position;
                sp.y = bestY + blobShadowLift;
                blobShadow.position = sp;
            }
        }

        void LateUpdate()
        {
            // Plant the feet on the VISIBLE terrain FIRST (before facing), so the grounded position is
            // settled this frame (soak-fix #1 — 'walking in the air'). The avatar root's local Y is driven
            // down onto the visible sand the agent's NavMesh ground point floats above.
            ApplyGroundSnap();

            // Read the agent's planar velocity each frame and drive the Idle<->Walk blend + facing.
            // Self-driving keeps this PR's surface to the visual only (no ClickToMove edit).
            Vector3 vel = _agent != null ? _agent.velocity : Vector3.zero;
            vel.y = 0f;
            bool walking = vel.sqrMagnitude > (walkSpeedThreshold * walkSpeedThreshold);
            IsWalking = walking;
            if (walking) _lastFacing = vel.normalized;

            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.SetBool(MovingParam, walking);

            // Yaw the model smoothly toward the travel facing (frame-rate-independent lerp).
            Vector3 face = _lastFacing; face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                float target = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                _bodyYaw = Mathf.LerpAngle(_bodyYaw, target, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));
            }
            if (_model != null)
                _model.localRotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }
    }
}
