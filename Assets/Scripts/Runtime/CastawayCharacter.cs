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

        [Header("Ground snap convergence (86ca8rdkp EXTENSIVE-DEBUG round — unified rate, kills the arrival BOB)")]
        [Tooltip("Snap convergence rate (1/s) — UNIFIED across rest AND walk. The prior speed-adaptive split " +
                 "(60 moving / 18 rest) made the rate JUMP at the moving→rest transition, so the still-" +
                 "converging error visibly settled the instant the agent stopped = the arrival BOB the Sponsor " +
                 "reported. With the snap target now the STABLE baked sole (no animation-envelope wobble), one " +
                 "rate tracks the descending foreshore tightly while walking AND doesn't pop at rest — no rate " +
                 "discontinuity at arrival, no bob. 40 = ~49% convergence per 60fps frame.")]
        public float snapRate = 40f;

        // RETAINED (serialized) for back-compat with scenes baked before the unify; no longer read by the snap.
        // The during-walk speed-adaptive split was superseded by the single snapRate above (kills the bob).
        [HideInInspector] public float snapRateRest = 18f;
        [HideInInspector] public float snapRateMove = 60f;

        [Header("Ground Y-OFFSET — Sponsor-dialable (86ca8rdkp 4th-attempt; F9 nudge target)")]
        [Tooltip("A constant world-Y offset added to the snapped feet (and the shadow). 4 attempts on the " +
                 "feet-on-ground placement → give the Sponsor the KNOB: he dials this in-game on the F9 nudge " +
                 "tool's GROUND-Y target (PageUp/Down) until the feet sit EXACTLY on the visible sand, reads " +
                 "the value off the HUD/log, and reports it to bake here. What-you-dial-is-what-you-get. " +
                 "Default 0 = plant exactly on the raycast hit (the geometric ground).")]
        public float groundYOffset = 0f;

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

        // === LIVE FLOAT-DIAGNOSTIC readouts (86ca8rdkp — the instrument, F8 overlay + F9 GROUND-Y panel +
        // ~1Hz log). The Sponsor + orchestrator measure the residual feet-vs-sand float from ground truth
        // instead of arguing "is it fixed". Updated every frame inside ApplyGroundSnap. ===

        /// <summary>The avatar feet WORLD-Y this frame (the avatar root's world position Y — the FBX origin
        /// sits at the feet, so the avatar-root Y IS the feet Y). NaN until the first snap frame.</summary>
        public float FeetWorldY { get; private set; } = float.NaN;

        /// <summary>The RAW visible-ground raycast-hit WORLD-Y directly under the player this frame (the
        /// selected renderer-ENABLED Ground surface, BEFORE the groundYOffset is added). NaN if no visible
        /// ground was hit. This is the surface the player SEES under his feet — the GAP measures against it.</summary>
        public float GroundHitWorldY { get; private set; } = float.NaN;

        /// <summary>The live FLOAT GAP = feet world-Y − ground-hit world-Y. ~0 = feet planted on the visible
        /// sand; &gt;~0.01 (1 cm) = the avatar is floating (the bug). NaN until both readings are valid. This
        /// is THE number the Sponsor dials groundYOffset to drive to ~0.</summary>
        public float FloatGap { get; private set; } = float.NaN;

        // === THE BREAKTHROUGH READOUT (86ca8rdkp final — Sponsor + instrument, 2026-06-15). The Sponsor dialed
        // GROUND-Y until FloatGap reads 0.0000 "✓ planted" — YET the character STILL visibly floated above its
        // (grounded) shadow. So FloatGap's "feet" (the avatar ROOT world-Y, == the snap reference) is a PROXY
        // point DECOUPLED from the actual RENDERED mesh bottom: the proxy grounds (gap 0) while the visible mesh
        // sits a FIXED OFFSET above it. The TRUE visible sole is SkinnedMeshRenderer.bounds.min.y. We expose
        // BOTH the rendered-sole world-Y and the TRUE gap (rendered sole − ground) so the gauge can flip to the
        // honest number (GAP=0 ⟺ soles on the sand), and the snap can ground the SOLE, not the proxy root. ===

        /// <summary>The TRUE rendered mesh bottom WORLD-Y this frame — the lowest visible vertex (the visible
        /// SOLE), measured from the union of every child SkinnedMeshRenderer's world-space bounds. This is what
        /// the player SEES touch the sand. NaN until a renderer is resolved. (Read from the renderer's runtime
        /// world bounds, which DO reflect the avatar-root scale + the snapped position — NOT the stale
        /// import-baked .bounds the proportion guards must use BakeMesh for; here the renderer is live + rendered
        /// every frame, so its world .bounds are current.)</summary>
        public float MeshBottomWorldY { get; private set; } = float.NaN;

        /// <summary>The CONSERVATIVE SMR.bounds.min.y (the OLD proxy / animation-max AABB floor) this frame —
        /// DUMP-ONLY. Exposed so the [FloatTrace] line and the regression guard can show it BELOW the baked
        /// actual sole (MeshBottomWorldY), proving the bounds floor is the deeper false-green (grounding to it
        /// floats the real soles while a bounds-reading gauge agrees). NOT used for snapping or the gauge.</summary>
        public float SmrBoundsMinWorldY { get; private set; } = float.NaN;

        /// <summary>The TRUE float gap = rendered-mesh-bottom world-Y − ground-hit world-Y. ~0 ⟺ the visible
        /// SOLES sit on the visible sand (the HONEST "planted" the Sponsor judges). Identical to
        /// <see cref="FloatGap"/> now that the gauge reads the rendered sole; kept as the explicit-name alias
        /// the [FloatTrace] dump + regression guard reference. NaN until both valid.</summary>
        public float MeshFloatGap { get; private set; } = float.NaN;

        /// <summary>The OLD proxy gap = avatar-ROOT world-Y − ground-hit world-Y (the misleading reading that
        /// went to 0 while the rendered mesh floated — the false "✓ planted" the Sponsor caught). Kept exposed
        /// so the regression guard can prove the gauge flipped from this proxy to the rendered sole, and so the
        /// [FloatTrace] dump always carries both. NaN until valid.</summary>
        public float ProxyRootFloatGap { get; private set; } = float.NaN;

        /// <summary>Whether the agent is moving (above walkSpeedThreshold) this frame — surfaced so the
        /// diagnostic shows rest-vs-move (the during-walk float was state-dependent).</summary>
        public bool IsMovingForSnap { get; private set; }

        /// <summary>The snap convergence rate (1/s) active this frame (snapRateRest at rest, snapRateMove
        /// while moving) — surfaced so the Sponsor/orchestrator see which smoothing rate is in play.</summary>
        public float ActiveSnapRate { get; private set; }

        /// <summary>
        /// PURE GAP math (the unit-testable core of the diagnostic): the float gap between the avatar feet
        /// and the visible ground directly under them. Both args are WORLD-Y. Returns feet − ground; ~0 means
        /// planted, positive means floating ABOVE the sand. Static + dependency-free so the PlayMode test can
        /// assert "feet at a known Y over ground at a known Y yields the expected gap" with no scene rig.
        /// </summary>
        public static float ComputeFloatGap(float feetWorldY, float groundHitWorldY)
            => feetWorldY - groundHitWorldY;

        // Reusable RaycastAll buffer (no per-frame GC). Sized for the handful of Ground colliders a single
        // down-ray can ever cross (the visible terrain + the flat NavMesh slab + a little headroom).
        private readonly RaycastHit[] _snapHits = new RaycastHit[8];

        // Cached child SkinnedMeshRenderer(s) — the TRUE rendered-sole reference (86ca8rdkp). We bake the LIVE
        // skinned mesh each frame and take the actual lowest world-Y VERTEX — NOT SMR.bounds.min.y.
        private SkinnedMeshRenderer[] _smrs;

        // Reusable bake target per SMR (no per-frame Mesh alloc — BakeMesh reuses the buffer; allocating a Mesh
        // every frame leaks until GC). Sized lazily to match _smrs.
        private Mesh[] _bakeMeshes;
        // Reusable vertex scratch — BakeMesh writes into the mesh; mesh.vertices allocates a fresh array each
        // call, so we use GetVertices(List<>) into a reused List to avoid the per-frame array alloc.
        private readonly System.Collections.Generic.List<Vector3> _bakeVerts =
            new System.Collections.Generic.List<Vector3>(4096);

        /// <summary>
        /// The TRUE rendered SOLE world-Y this frame: the actual lowest skinned VERTEX across all child
        /// SkinnedMeshRenderers, computed by BAKING the live skinned mesh (current bone poses) and taking the
        /// min world-Y over its real vertices. NaN if no renderer is resolved.
        ///
        /// WHY NOT SMR.bounds.min.y (the deeper FALSE-GREEN the Sponsor's F8 gauge exposed, 86ca8rdkp final):
        /// Unity's SkinnedMeshRenderer.bounds is the CONSERVATIVE animation-MAX AABB — a single box sized to
        /// contain the mesh across the WHOLE animation range, NOT the current frame's mesh. So `bounds.min.y`
        /// sits BELOW the real visible soles (by the per-clip foot-lift envelope), and it WOBBLES as the idle
        /// anim plays (the −0.003↔+0.0003 standing-still GAP jitter the Sponsor reported). Grounding to
        /// bounds.min.y floats the character (the box floor is below the soles) while a gauge ALSO reading
        /// bounds.min.y agrees with the snap → GAP=0 "✓ planted" yet visibly floating. Baking the live mesh
        /// gives the ACTUAL current-frame lowest vertex — the surface the player sees touch the sand —
        /// deterministic and stable per pose (no animation-envelope wobble).
        ///
        /// TRANSFORM (the cm→m / lossy-scale trap, unity-conventions/character-pipeline): verts bake in the
        /// SMR transform's LOCAL space; we transform to WORLD via the SMR's own `localToWorldMatrix`, which
        /// carries the full parent chain INCLUDING the SMR node's own (large, cm→m-compensating) scale. Using
        /// BakeMesh(useScale:FALSE) here so the node scale is applied ONCE — by the matrix — not double-applied
        /// (useScale:true would bake the renderer's lossyScale into the verts, then the matrix scales again →
        /// a hugely-wrong Y, the 56u-tall trap CastawayVerifyCapture documents).
        /// </summary>
        private float MeasureRenderedSoleWorldY()
        {
            if (_smrs == null || _smrs.Length == 0)
                _smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_smrs == null || _smrs.Length == 0) return float.NaN;
            if (_bakeMeshes == null || _bakeMeshes.Length != _smrs.Length)
                _bakeMeshes = new Mesh[_smrs.Length];

            float minY = float.PositiveInfinity;
            bool any = false;
            for (int i = 0; i < _smrs.Length; i++)
            {
                var smr = _smrs[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (_bakeMeshes[i] == null) _bakeMeshes[i] = new Mesh { name = "CastawaySoleBake" + i };
                // useScale:false — verts come back in the SMR's authored mesh space (no renderer-node scale).
                smr.BakeMesh(_bakeMeshes[i], false);
                _bakeMeshes[i].GetVertices(_bakeVerts);
                if (_bakeVerts.Count == 0) continue;
                // SCALE-IMMUNE world matrix (86ca8rdkp attempt-8 ROOT-CAUSE fix): use the SMR's world POSITION
                // + ROTATION with UNIT scale — NOT smr.transform.localToWorldMatrix. This Hyper3D/Mixamo FBX
                // bakes a 100× cm→m node scale onto the SkinnedMeshRenderer's OWN transform ("model" node,
                // probe-verified localScale=(100,100,100)). smr.localToWorldMatrix carries that 100× (×1.8 root
                // = 180×), and BakeMesh(false) verts are already in the mesh's authored metres space, so
                // multiplying by l2w DOUBLE-APPLIES the 100× and blows the world Y to ~±280 (the ±68 runaway
                // that drove the snap divergence — sole-probe.log: formula (1) bake(false)+smr.l2w gave 4.45
                // vs GT 4.85 at root-Y 5, and exploded to 282 at root-Y 0). A unit-scale TRS matches the
                // rendered bounds floor (sole-probe formula 4 = 4.9970 ≈ GT). character-pipeline.md §cm→m trap.
                Matrix4x4 l2w = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int v = 0; v < _bakeVerts.Count; v++)
                {
                    float y = l2w.MultiplyPoint3x4(_bakeVerts[v]).y;
                    if (y < minY) minY = y;
                }
                any = true;
            }
            return any ? minY : float.NaN;
        }

        /// <summary>The CONSERVATIVE SMR.bounds.min.y across all child SMRs (the OLD proxy — the
        /// animation-max AABB floor). Kept ONLY so the [FloatTrace] dump can show it side-by-side with the
        /// baked actual sole, proving the bounds floor sits below the real soles (the deeper false-green).
        /// NOT used for snapping or the gauge anymore.</summary>
        private float MeasureSmrBoundsMinY()
        {
            if (_smrs == null || _smrs.Length == 0)
                _smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_smrs == null || _smrs.Length == 0) return float.NaN;
            float minY = float.PositiveInfinity;
            bool any = false;
            for (int i = 0; i < _smrs.Length; i++)
            {
                if (_smrs[i] == null) continue;
                float y = _smrs[i].bounds.min.y;
                if (y < minY) minY = y;
                any = true;
            }
            return any ? minY : float.NaN;
        }

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
            if (!found)
            {
                // No visible ground under the feet — diagnostic readouts go invalid (the overlay shows N/A).
                LastSnapTargetWorldY = float.NaN;
                GroundHitWorldY = float.NaN;
                FloatGap = float.NaN;
                FeetWorldY = transform.position.y;
                return;
            }
            // The RAW visible-ground hit (pre-offset) — the surface the player SEES under his feet. The live
            // FLOAT GAP is measured against THIS, so the Sponsor watches the gap to the actual sand (not the
            // offset-shifted plant target). 86ca8rdkp diagnostic.
            GroundHitWorldY = bestY;
            // The plant Y = the visible-terrain hit + the Sponsor-dialable ground offset (default 0 = plant
            // exactly on the geometric ground). The OFFSET is the F9-dialable knob (4th-attempt — give the
            // Sponsor the handle instead of the team guessing the exact feet-on-ground value).
            float plantY = bestY + groundYOffset;
            LastSnapTargetWorldY = plantY; // the SELECTED plant surface (pre-smoothing) — the snap-target the test reads

            // ===== THE SOURCE FIX (86ca8rdkp attempt-8 — ROOT-GROUND with a FIXED K; abandon per-frame mesh
            // chasing). The ~6-iteration float saga + the ±68 runaway all came from CHASING the rendered sole
            // each frame: snapping to SMR.bounds.min.y (anim-max AABB, wobbles), then to a per-frame BakeMesh
            // sole whose world matrix DOUBLE-APPLIED the FBX's intrinsic 100× cm→m node scale (scale-trace.log:
            // the "model" SMR node is localScale=100; BakeMesh+smr.l2w blew world Y to 282 → the snap drove
            // avatarRootY to ~−68 in a false-green equilibrium while the character left frame).
            //
            // ROOT CAUSE (probe-MEASURED, not hypothesized): the 100× node is INTRINSIC to the FBX hierarchy —
            // no importer flag removes it (useFileScale=false → 218u tall + still 100× node; bakeAxisConversion
            // → still 100× node; both refuted by fix-probe). The mesh RENDERS correct (Unity skinning handles
            // the node scale via bone matrices) — ONLY world-space BakeMesh measurements explode. So we STOP
            // measuring the sole to ground it: the FBX origin is AT THE FEET (BuildModel sets the model child
            // localPos=0; the bind sole sits ≈ the avatar root — sole-probe: bind sole 4.997 ≈ root 5.000), and
            // the Idle/Walk clips are imported IN-PLACE (lockRootPositionXZ, root-motion off), so the root-to-
            // sole offset is a FIXED CONSTANT, not pose-dependent. Ground the ROOT directly:
            //     avatarRoot.worldY = groundHit + groundYOffset(K)
            // K is the small Sponsor-dialable constant (default 0 = feet on the geometric ground). NO BakeMesh
            // in the snap path → no 100× exposure → no runaway. The BakeMesh sole is now read ONLY for the F8
            // gauge (a sane, scale-immune honest readout — MeasureRenderedSoleWorldY uses a unit-scale matrix).
            float bakedSoleNow = MeasureRenderedSoleWorldY(); // gauge-only (scale-immune); NOT used to snap
            float boundsMinNow = MeasureSmrBoundsMinY();      // dump-only (prove bounds floor diverges)

            // Desired avatar-root WORLD Y = the plant target (groundHit + K). The avatar root is a direct child
            // of the player root, so localY = worldY − root.position.y. NO pose-offset subtraction — grounding
            // the root, not the chased sole, is what kills the divergence.
            float desiredLocalY = plantY - root.position.y;

            bool moving = _agent != null &&
                          new Vector2(_agent.velocity.x, _agent.velocity.z).sqrMagnitude >
                          (walkSpeedThreshold * walkSpeedThreshold);
            IsMovingForSnap = moving;          // diagnostic: rest-vs-move
            ActiveSnapRate = snapRate;         // diagnostic: the single unified rate now in play

            // KILL-THE-BOB (86ca8rdkp — one UNIFIED rate). A speed-adaptive split (fast moving / slow at rest)
            // made the convergence rate JUMP at the moving↔rest transition → the still-converging error visibly
            // settled the instant the agent stopped = the arrival BOB. The snap target is now the STABLE plant
            // target (groundHit + K — a smooth function of terrain only, no animation-envelope wobble, no mesh
            // chasing), so a SINGLE unified rate tracks the descending foreshore tightly while walking AND
            // doesn't pop at rest — no rate discontinuity at arrival, no bob. (snapRateMove/snapRateRest retained
            // as [HideInInspector] serialized fields for scene back-compat; snapRate is the live one.)
            if (!_snapInit) { _snapLocalY = desiredLocalY; _snapInit = true; }
            else _snapLocalY = Mathf.Lerp(_snapLocalY, desiredLocalY, 1f - Mathf.Exp(-snapRate * Time.deltaTime));
            Vector3 lp = transform.localPosition;
            lp.y = _snapLocalY;
            transform.localPosition = lp;

            // LIVE FLOAT-DIAGNOSTIC — the gauge reads the HONEST gap: SCALE-IMMUNE baked sole − ground. GAP≈0 ⟺
            // the visible SOLES are on the visible sand. The sole is now measured with a UNIT-SCALE world matrix
            // (MeasureRenderedSoleWorldY) so the FBX 100× node never blows it up — the gauge reads a sane sole-Y,
            // not the ±68 garbage the old smr.l2w path produced. Re-measure AFTER applying this frame's snap so
            // the gauge reflects the corrected position. (Snap grounds the ROOT to groundHit+K; since the bind
            // sole ≈ the root and the clips are in-place, the sole lands on the sand too — verified by the WALK
            // [FloatTrace]: GAP stays bounded standing + walking + at arrival.)
            MeshBottomWorldY = MeasureRenderedSoleWorldY();
            SmrBoundsMinWorldY = boundsMinNow;       // dump-only proxy for the [FloatTrace] discrepancy line
            MeshFloatGap = float.IsNaN(MeshBottomWorldY) ? float.NaN
                                                         : ComputeFloatGap(MeshBottomWorldY, GroundHitWorldY);
            // FeetWorldY is the gauge's "feet" line — the TRUE baked sole (falls back to the avatar-root world Y
            // only if no SMR is resolvable). FloatGap is the HONEST sole-vs-sand gap the overlay shows.
            FeetWorldY = float.IsNaN(MeshBottomWorldY) ? transform.position.y : MeshBottomWorldY;
            FloatGap = float.IsNaN(MeshFloatGap) ? ComputeFloatGap(transform.position.y, GroundHitWorldY)
                                                 : MeshFloatGap;
            // The proxy avatar-root gap (kept for the regression guard + dump — proves the gauge reads the sole).
            ProxyRootFloatGap = ComputeFloatGap(transform.position.y, GroundHitWorldY);

            // PER-FRAME [FloatTrace] (86ca8rdkp EXTENSIVE-DEBUG round). The Sponsor demanded extensive logging;
            // this dumps EVERY Y-reference EACH FRAME (gated on _frameTrace = -floatTrace OR the F8 overlay) so
            // the orchestrator reads the discrepancy (bounds.min vs baked-actual vs shadow vs ground) standing,
            // walking, AND at arrival from the player log. Throttled to once per render frame (not per physics).
            if (_frameTrace) EmitFrameTrace(root, bakedSoleNow, boundsMinNow, plantY, moving);

            // CONTACT SHADOW — ground it to the VISIBLE-SAND CONTACT LEVEL (the plant target). The snap grounds
            // the avatar ROOT to plantY (= groundHit + K), and the bind feet sit at the root, so plantY IS the
            // contact level the feet touch. Planting the shadow at plantY tracks the sand the feet touch in
            // motion AND at rest — the shadow is a child of the player root and would otherwise stay at a fixed
            // Y and strand above/below the snapped feet on the dipping foreshore (the 'floats above its shadow'
            // percept from earlier soaks). No pose coupling, no mesh chasing.
            if (blobShadow != null)
            {
                Vector3 sp = blobShadow.position;
                sp.y = plantY + blobShadowLift;
                blobShadow.position = sp;
            }
        }

        // Per-frame [FloatTrace] gate (86ca8rdkp EXTENSIVE-DEBUG round). The Sponsor EXPLICITLY demanded
        // extensive per-frame logging. Driven ON by either the -floatTrace launch arg (an orchestrator-driven
        // log-only capture) OR FloatDiagnostic toggling the F8 overlay (so the interactive soak also logs).
        // FloatDiagnostic sets this via SetFrameTrace when its overlay/-floatTrace activates.
        private bool _frameTrace;
        private int _traceFrame;

        /// <summary>FloatDiagnostic (or the verify capture) turns per-frame [FloatTrace] on/off — so the
        /// extensive log fires every frame while the overlay is up or -floatTrace is set, and is silent in a
        /// normal soak (zero log spam). Idempotent.</summary>
        public void SetFrameTrace(bool on) => _frameTrace = on;

        // Cache the foot bones + shadow once (avoids a per-frame bone scan inside the trace).
        private Transform _leftFootBone, _rightFootBone, _hipsBone;
        private bool _bonesResolved;

        private void ResolveBones()
        {
            _bonesResolved = true;
            if (_smrs == null || _smrs.Length == 0) _smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (_smrs == null) return;
            foreach (var smr in _smrs)
            {
                if (smr == null || smr.bones == null) continue;
                foreach (var b in smr.bones)
                {
                    if (b == null) continue;
                    string n = b.name.ToLowerInvariant();
                    if (_leftFootBone == null && (n.EndsWith("leftfoot") || n.EndsWith("lefttoebase") ||
                                                  n.EndsWith("lefttoe_end"))) _leftFootBone = b;
                    else if (_rightFootBone == null && (n.EndsWith("rightfoot") || n.EndsWith("righttoebase") ||
                                                        n.EndsWith("righttoe_end"))) _rightFootBone = b;
                    else if (_hipsBone == null && (n.EndsWith("hips") || n == "mixamorig:hips")) _hipsBone = b;
                }
                if (_leftFootBone != null && _rightFootBone != null) break;
            }
        }

        // The EXTENSIVE per-frame [FloatTrace] line (86ca8rdkp). Dumps ALL the references the Sponsor + the
        // orchestrator asked for in ONE greppable line each frame: root.y / SMR.bounds.min.y (the OLD proxy) /
        // the BAKED actual-lowest-vertex (the TRUE sole) / L+R foot-bone world-Y / ground raycast hit-Y /
        // blob-shadow world-Y / the snap delta applied / moving-state / snap-rate. The DISCREPANCY the whole
        // saga turned on (bounds.min BELOW the baked sole while the gauge agreed) is the load-bearing field.
        private void EmitFrameTrace(Transform root, float bakedSole, float boundsMin, float plantY, bool moving)
        {
            if (!_bonesResolved) ResolveBones();
            float lFootY = _leftFootBone != null ? _leftFootBone.position.y : float.NaN;
            float rFootY = _rightFootBone != null ? _rightFootBone.position.y : float.NaN;
            float shadowY = blobShadow != null ? blobShadow.position.y : float.NaN;
            float boundsVsBaked = (float.IsNaN(boundsMin) || float.IsNaN(bakedSole)) ? float.NaN : (bakedSole - boundsMin);
            _traceFrame++;
            Debug.Log(
                $"[FloatTrace] f={_traceFrame} rootY={root.position.y:F4} avatarRootY={transform.position.y:F4} " +
                $"boundsMinY(OLD proxy)={Fmt4(boundsMin)} BAKED_SOLE(TRUE)={Fmt4(bakedSole)} " +
                $"boundsBelowSoleBy={Fmt4(boundsVsBaked)} lFootBoneY={Fmt4(lFootY)} rFootBoneY={Fmt4(rFootY)} " +
                $"groundHitY={Fmt4(GroundHitWorldY)} plantY={Fmt4(plantY)} shadowY={Fmt4(shadowY)} " +
                $"snapLocalY={_snapLocalY:F4} GAP(sole-ground)={Fmt4(FloatGap)} " +
                $"proxyRootGap={Fmt4(ProxyRootFloatGap)} offset={groundYOffset:F4} moving={moving} rate={ActiveSnapRate:F0}");
        }

        private static string Fmt4(float v) => float.IsNaN(v) ? "N/A" : v.ToString("F4");

        /// <summary>
        /// ONE-FRAME GROUND-TRUTH DUMP (86ca8rdkp diagnostic — diagnostic-traces-before-fixes). Dumps EVERY
        /// Y-reference in a single frame so the hidden offset between the OLD bounds.min.y proxy and the BAKED
        /// actual-lowest-vertex TRUE sole is MEASURED, not guessed. Called by FloatDiagnosticVerifyCapture at
        /// spawn + shore so the [FloatTrace] log carries the full picture. Pure read-only (never mutates state).
        /// </summary>
        public string DumpGroundTruth(string where)
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            float bakedSole = MeasureRenderedSoleWorldY();
            float boundsMin = MeasureSmrBoundsMinY();
            if (!_bonesResolved) ResolveBones();
            string hips = _hipsBone != null ? _hipsBone.position.y.ToString("F4") : "N/A";
            string lFoot = _leftFootBone != null ? _leftFootBone.position.y.ToString("F4") : "N/A";
            string rFoot = _rightFootBone != null ? _rightFootBone.position.y.ToString("F4") : "N/A";
            string modelLocalY = _model != null ? _model.localPosition.y.ToString("F4") : "N/A";
            string shadowY = blobShadow != null ? blobShadow.position.y.ToString("F4") : "N/A";
            float boundsBelowSoleBy = (float.IsNaN(boundsMin) || float.IsNaN(bakedSole)) ? float.NaN
                                                                                         : (bakedSole - boundsMin);
            string msg =
                $"[FloatTrace] DUMP ({where}): rootY={root.position.y:F4} avatarRootY(gauge feetY)={transform.position.y:F4} " +
                $"hipsY={hips} leftFootBoneY={lFoot} rightFootBoneY={rFoot} " +
                $"BOUNDS_MIN_Y(OLD proxy=anim-max AABB floor)={Fmt4(boundsMin)} " +
                $"BAKED_SOLE_Y(TRUE current-frame lowest vertex)={Fmt4(bakedSole)} " +
                $"boundsBelowTrueSoleBy={Fmt4(boundsBelowSoleBy)} " +
                $"modelChildLocalY={modelLocalY} groundHitY={Fmt4(GroundHitWorldY)} " +
                $"shadowY={shadowY} | PROXY_ROOT_GAP(avatarRoot-ground)={Fmt4(ProxyRootFloatGap)} " +
                $"TRUE_GAP(bakedSole-ground)={Fmt4(MeshFloatGap)} " +
                $"poseOffset(bakedSole-avatarRoot)={Fmt4(float.IsNaN(bakedSole) ? float.NaN : bakedSole - transform.position.y)} " +
                $"snapLocalY={_snapLocalY:F4} groundYOffset={groundYOffset:F4} moving={IsMovingForSnap}";
            Debug.Log(msg);
            return msg;
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
