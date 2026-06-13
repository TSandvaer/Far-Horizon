using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// The player's visual avatar: the "Mini Chibi Kid" sourced CC0-Attribution rigged low-poly
    /// chunky-cartoon character (Sketchfab, joaobaltieri) — the purpose-built chibi castaway base
    /// (ticket 86ca8ca1m). This SUPERSEDES the Quaternius Animated-Men character (the realistic
    /// Quaternius head could not be cartoon-ified): the chibi's big-head toy proportions are INTRINSIC
    /// to the mesh, so there are NO bone-scale dials — the mesh ships chunky as imported.
    ///
    /// WHY A BAKED SKINNED MESH (the durable fix for a bug CLASS): the spike's earlier PROCEDURAL
    /// humanoid shipped BROKEN in the exe — "legs pointing upwards" — because an Awake-assembled
    /// hierarchy serialized differently than the editor-time build (unity-conventions.md
    /// §editor-vs-runtime). A skinned mesh with a baked bone skeleton + imported clips CANNOT exhibit
    /// that failure by construction: the skeleton + skin live in the FBX, not assembled at runtime.
    /// The model child is built EDITOR-TIME (BuildInEditor, called by MovementCameraScene) so it
    /// SERIALIZES into Boot.unity; runtime code only animates what's serialized.
    ///
    /// SELF-DRIVING (the U5/U3 seam): this component lives on a CHILD avatar root under the player
    /// (so its avatar-height scale doesn't scale the NavMeshAgent), and reads the player's
    /// <see cref="NavMeshAgent"/> velocity (resolved from the parent) itself each frame to flip the
    /// Idle&lt;-&gt;Walk Animator blend and yaw the model toward travel. It does NOT modify ClickToMove.
    /// The agent has updateRotation=false (ClickToMove owns that contract), so the visual owns facing.
    ///
    /// MATERIALS — KEEP THE FBX'S OWN FLAT TOON MATERIALS (identity/recolor OUT OF SCOPE per the
    /// ticket): the chibi ships two URP/Lit toon materials (mini_material / mini_material_secondary)
    /// that bind a 256² atlas (mini_material_baseColor) — that flat-shaded toon look IS the look we
    /// ship. We do NOT recolor here (the prior base's 6-part Quaternius recolor is dropped — recoloring
    /// would replace the materials and WIPE the atlas to a flat tint). A sandy-hair/khaki recolor is a
    /// TUNABLE follow-up the Sponsor judges from the soak; not fought here.
    /// </summary>
    public class CastawayCharacter : MonoBehaviour
    {
        [Header("Model source (wired by MovementCameraScene at author time)")]
        // The imported FBX prefab (the chibi: rig + bundled Idle/Walk clips + toon materials/atlas) and
        // the Idle<->Walk controller. CharacterAssetGen produces both.
        public GameObject modelPrefab;
        public RuntimeAnimatorController animatorController;

        [Header("Facing")]
        [Tooltip("How fast the body yaws toward the travel direction (higher = snappier).")]
        public float turnLerp = 12f;

        [Header("Locomotion thresholds")]
        [Tooltip("Planar speed (u/s) above which the character is considered walking (drives the " +
                 "Idle<->Walk blend). Squared internally.")]
        public float walkSpeedThreshold = 0.15f;

        // Animator parameter the controller blends on (set each frame from the agent's velocity).
        public const string MovingParam = "Moving";

        // CAP -> HAIR (86ca8ca1m soak-fix). The Sponsor soaked 46f2a9d and the castaway read as wearing
        // a CAP, not hair ("still wearing the yellow cap"). The chibi's cap is TWO separate skinned-mesh
        // nodes (empirically mapped via the diagnose-via-trace IsolateMeshes probe): Object_41 is the
        // CROWN DOME, Object_42 is the flat forward BRIM/VISOR. We HIDE BOTH: hiding only the brim left
        // the dome's cap-shaped front ARC sticking up off the head (it read as a floating sandy loop, not
        // hair — proven in the shipped close-up). With both cap meshes hidden, a clean procedural
        // sandy-ginger HAIR skull-cap is added on top instead (MovementCameraScene.AttachHair). Hiding
        // (not deleting) keeps the bone hierarchy intact for the proportion/clip guards, and the disabled
        // state SERIALIZES into Boot.unity on the editor-build path.
        public static readonly string[] CapMeshNames = { "Object_41", "Object_42" }; // dome + brim — both hidden

        private NavMeshAgent _agent;
        private Animator _animator;
        private Transform _model;       // the instantiated FBX root, yaw-rotated toward facing
        private float _bodyYaw;
        private Vector3 _lastFacing = Vector3.forward;
        private bool _built;

        // Exposed for tests / later systems: current Idle/Walk state read off the agent.
        public bool IsWalking { get; private set; }

        void Awake()
        {
            // The agent lives on the player ROOT (this component is on a child avatar root so its
            // height-scale doesn't scale the agent). Resolve it from the parent chain.
            _agent = GetComponentInParent<NavMeshAgent>();
            if (_agent == null)
                Debug.LogWarning("[CastawayCharacter] no NavMeshAgent found in parents — the " +
                                 "Idle<->Walk anim + facing won't drive (avatar must be a child of the " +
                                 "player root that carries the agent)");
            // If the author-time build already serialized the Model child into the scene (the ship
            // path), re-bind to it rather than re-instantiate. Otherwise build at runtime (defensive
            // fallback — should not happen for the shipped scene).
            if (transform.childCount > 0 && _model == null) RebindFromHierarchy();
            if (!_built) BuildModel();
        }

        /// <summary>
        /// Editor build entry: MovementCameraScene calls this so the Model child + the Animator
        /// controller reference SERIALIZE into Boot.unity (the editor-vs-runtime serialization lesson —
        /// the shipped scene must reference a serialized skinned/boned avatar, not assemble a hierarchy
        /// in Awake). Idempotent: clears prior children first.
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

            // Instantiate the imported FBX under the avatar root. In the editor-build path the
            // resulting SkinnedMeshRenderer + bone hierarchy SERIALIZE into Boot.unity, so the
            // shipped exe loads the SAME baked skeleton the editor sees (no Awake-assembled hierarchy
            // to diverge — the legs-up lesson). Works for both editor-build + runtime-fallback with no
            // UnityEditor dependency.
            GameObject go = Instantiate(modelPrefab, transform, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;   // FBX origin is at the feet -> grounded feet
            go.transform.localRotation = Quaternion.identity;
            _model = go.transform;

            _animator = go.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = go.AddComponent<Animator>();
            // Preserve the FBX-imported avatar (the Generic-rig skeleton built by CreateFromThisModel)
            // — assigning only the controller keeps _animator.avatar, which the clips bind to. A null
            // avatar means the FBX imported with NoAvatar and the model freezes in its bind/T-pose.
            if (_animator.avatar == null)
                Debug.LogWarning("[CastawayCharacter] Animator has NO avatar — clips will not bind " +
                                 "(FBX must import with avatarSetup=CreateFromThisModel)");
            if (animatorController != null) _animator.runtimeAnimatorController = animatorController;
            _animator.applyRootMotion = false; // NavMeshAgent drives position; anim is in-place

            // CAP -> HAIR (86ca8ca1m soak-fix): hide BOTH cap meshes (dome + brim) so the castaway reads
            // as having sandy-ginger HAIR (a clean procedural skull-cap added by MovementCameraScene),
            // not wearing a hat. Disabling the renderers (not destroying) keeps the bone hierarchy whole
            // for the proportion/clip guards, and the disabled state serializes into Boot.unity on the
            // editor-build path. Idempotent + defensive: a renamed node simply won't match (logged).
            HideCap();

            // The chibi's imported toon materials + atlas carry the identity recolor (the atlas PNG is
            // repainted per-cell by CharacterAssetGen.RecolorIdentityAtlas — warm-khaki shirt, sandy hair
            // on the kept dome, toned skin). We do NOT tint the materials here (a per-material tint would
            // flatten the toon gradient — the trap this class always warned of).
            _built = true;
        }

        // Hide BOTH cap meshes (dome + brim) so the castaway reads as having hair, not a cap (86ca8ca1m
        // soak-fix). Matches by node name from the empirical mesh map (the IsolateMeshes diagnose-trace).
        // Searches inactive too so the editor-build path (which serializes the disabled state) finds them.
        private void HideCap()
        {
            if (_model == null) return;
            int hid = 0;
            foreach (var smr in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var capName in CapMeshNames)
                {
                    if (smr.name == capName)
                    {
                        smr.enabled = false; // serializes disabled into Boot.unity on the editor-build path
                        hid++;
                    }
                }
            }
            if (hid < CapMeshNames.Length)
                Debug.LogWarning("[CastawayCharacter] hid " + hid + "/" + CapMeshNames.Length +
                                 " cap meshes — the castaway may still read as wearing a cap");
        }

        void LateUpdate()
        {
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
