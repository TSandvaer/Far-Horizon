using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// The player's visual avatar: a real rigged CC0 low-poly CLOTHED character (Quaternius
    /// "Animated Men" pack, Smooth_Male_Casual.fbx, CC0 1.0) with the warm castaway recolor — the
    /// Sponsor-approved "appealing" character from the engine-eval spike (iter7/8,
    /// EmbergraveUnitySlice). This is U6 (ticket 86ca86fz9): it REPLACES the U3 capsule placeholder
    /// visual on the merged movement rig.
    ///
    /// WHY A BAKED SKINNED MESH (the durable fix for a bug CLASS): the spike's earlier PROCEDURAL
    /// humanoid shipped BROKEN in the exe — "legs pointing upwards" — because an Awake-assembled
    /// hierarchy serialized differently than the editor-time build (unity-conventions.md
    /// §editor-vs-runtime). A skinned mesh with a baked bone skeleton + imported clips CANNOT exhibit
    /// that failure by construction: the skeleton + skin live in the FBX, not assembled at runtime.
    /// The model child + materials are built EDITOR-TIME (BuildInEditor, called by
    /// MovementCameraScene) so they SERIALIZE into Boot.unity; runtime code only animates what's
    /// serialized.
    ///
    /// SELF-DRIVING (the U5/U3 seam): this component lives on a CHILD avatar root under the player
    /// (so its avatar-height scale doesn't scale the NavMeshAgent), and reads the player's
    /// <see cref="NavMeshAgent"/> velocity (resolved from the parent) itself each frame to flip the
    /// Idle&lt;-&gt;Walk Animator blend and yaw the model toward travel. It does NOT modify ClickToMove
    /// — keeping this PR's surface to the player visual/rig only, so it rebases cleanly against Drew's
    /// U5 environment PR. The agent has updateRotation=false (ClickToMove owns that contract), so the
    /// visual owns facing.
    ///
    /// CASTAWAY RECOLOR (iter-8, 6-part; U2-6 polish re-tune): the Animated-Men FBX has SIX distinct
    /// materials (Shirt / Skin / Pants / Eyes / Socks / Hair — verified by probe). A 4-slot assumption
    /// silently erased the face (Eyes-&gt;skin) — see
    /// <see cref="CastawayColorFor(string,Color,Color,Color,Color,Color,Color)"/>. All map distinctly to
    /// the BRIGHTER, HIGHER-KEY palette from the v4 design reference (young + hopeful) — the iter-8
    /// values had drifted dark/grizzled, which the U2-6 polish corrects. A leather accent slot maps any
    /// belt/strap/satchel material to a warm leather brown for a more detailed silhouette.
    /// </summary>
    public class CastawayCharacter : MonoBehaviour
    {
        [Header("Model source (wired by MovementCameraScene at author time)")]
        // The imported FBX prefab (the Animated-Men clothed character: rig + bundled Man_Idle/Man_Walk
        // clips) and the Idle<->Walk controller. CharacterAssetGen produces both.
        public GameObject modelPrefab;
        public RuntimeAnimatorController animatorController;

        // U2-6 POLISH PASS (ticket 86ca8bdhb): the iter-8 palette had drifted DARK/muddy ("rust
        // ragged shirt", "dark walnut pants") toward a grizzled-survivor read — which violates the
        // approved iter7 identity (RandomGame _castaway_judge v3/v4 sheets: a YOUNG, HOPEFUL castaway).
        // Re-tuned to the v4 design reference: copper/ginger hair, a LIGHT warm khaki/sand shirt
        // (rolled sleeves), muted teal-blue rolled trousers, a healthy warm tan skin, and a distinct
        // warm-leather accent (belt/strap/satchel) so the silhouette reads detailed. Brighter +
        // higher-key = "more detailed/polished, same character" (the Sponsor's iter7 ask). All
        // channels sub-1.0 HDR-safe. Identity guard: NOT a dark/ragged survivor — light + fresh.
        [Header("Castaway recolor — per-part palette (U2-6 polish: v4 design reference, brighter/hopeful)")]
        public Color skin    = new Color(0.86f, 0.64f, 0.47f);  // healthy warm tan (Skin/Body/Socks=bare feet)
        public Color shirt   = new Color(0.72f, 0.60f, 0.42f);  // warm mid-khaki shirt (rolled sleeves) — U2-6 Uma tune: separates the torso from the bright sandy Zone-D ground (was 0.82,0.72,0.52, merged into the sand at distance); still well above the >0.6 shirt-luminance identity guard (luma 0.615)
        public Color pants   = new Color(0.34f, 0.46f, 0.50f);  // muted teal-blue rolled trousers
        public Color hair    = new Color(0.84f, 0.50f, 0.22f);  // copper/ginger sun-bleached hair
        public Color eyes    = new Color(0.18f, 0.13f, 0.11f);  // dark eyes so the face reads
        public Color leather = new Color(0.45f, 0.30f, 0.18f);  // warm leather belt/strap/satchel accent

        // CARTOONISH STYLIZATION (ticket 86ca8ca1m, Uma's castaway-style-v2 brief): the chunky
        // proportions land primarily as the BAKED MESH the orchestrator delivers via Blender MCP
        // (head-ratio 3.0 baseline). This bone-baseline-scale pass is the COMPLEMENTARY, rig-safe
        // soak-tuning lever + working-fallback: setting a baseline localScale on named rig bones
        // (Head about the neck, Hands, Feet) COMPOSES with the imported animation (clips drive bone
        // ROTATION/TRANSLATION; a baseline SCALE multiplies through skinning) — so Idle/Walk survive
        // BY CONSTRUCTION (the hard 86ca8ca1m AC), no re-rig, no avatar rebuild. It serializes into
        // Boot.unity exactly like the rest of the avatar (the bone transforms are part of the baked
        // skeleton — no Awake-assembled hierarchy, so the legs-up class can't recur).
        //
        // WHY A DIAL, not a fixed re-export: Uma's brief §2 says the Sponsor soak may push the head
        // ratio from 3.0 toward 2.5 ("cuter"). A bone-scale dial lets that happen WITHOUT a Blender
        // re-export round — cheap soak iteration. headScale=1.0 = "trust the delivered mesh as-is"
        // (no extra chunking); >1.0 = chunk further on top. The mesh ships the baseline; this tunes it.
        [Header("Cartoonish stylization — rig-safe bone-baseline scale (86ca8ca1m)")]
        [Tooltip("Uniform extra scale on the Head bone (about the neck pivot). The delivered mesh " +
                 "carries the 3.0-head baseline; >1.0 chunks the head further toward the 'cuter' 2.5 " +
                 "soak target. 1.0 = no extra head scaling (trust the mesh).")]
        public float headScale = 1.0f;
        [Tooltip("Uniform extra scale on both Hand bones (oversized mitten read).")]
        public float handScale = 1.0f;
        [Tooltip("Uniform extra scale on both Foot bones (chunky blocky bare feet).")]
        public float footScale = 1.0f;

        // Rig bone names the stylization scale targets (Quaternius Animated-Men armature — verified by
        // probing the FBX: Head / Hand.L / Hand.R / Foot.L / Foot.R). Matched by .Contains so the
        // "HumanArmature|" / "mixamorig" style prefixes don't break the lookup (the same clip-prefix
        // lesson — unity-conventions.md §FBX). If a delivered mesh renames a bone, the scale is a no-op
        // for that bone (logged once) rather than a hard failure — the mesh baseline still ships.
        private static readonly string[] HeadBoneTokens = { "Head" };
        private static readonly string[] HandBoneTokens = { "Hand.L", "Hand.R", "Hand_L", "Hand_R" };
        private static readonly string[] FootBoneTokens = { "Foot.L", "Foot.R", "Foot_L", "Foot_R" };

        [Header("Facing")]
        [Tooltip("How fast the body yaws toward the travel direction (higher = snappier).")]
        public float turnLerp = 12f;

        [Header("Locomotion thresholds")]
        [Tooltip("Planar speed (u/s) above which the character is considered walking (drives the " +
                 "Idle<->Walk blend). Squared internally.")]
        public float walkSpeedThreshold = 0.15f;

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
        /// Editor build entry: MovementCameraScene calls this so the Model child + materials + the
        /// Animator controller reference SERIALIZE into Boot.unity (the editor-vs-runtime
        /// serialization lesson — the shipped scene must reference a serialized skinned/boned avatar,
        /// not assemble a hierarchy in Awake). Idempotent: clears prior children first.
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

            ApplyCastawayRecolor(go);
            ApplyStylizationBoneScale(go);
            _built = true;
        }

        // Apply the cartoonish-stylization bone-baseline scale (86ca8ca1m). Sets a baseline localScale
        // on the Head / Hand / Foot bones so the chunky read can be tuned on the rig (the soak dial)
        // ON TOP OF the delivered chunky mesh, WITHOUT disturbing the imported clips. The scale is a
        // BASELINE on the bone's local transform: the Animator writes rotation/translation each frame
        // (and, for these locomotion clips, NOT scale), so the baseline scale survives animation —
        // Idle/Walk keep driving the rig with the chunked bones. Public+static helper does the lookup
        // so the EditMode proportion guard can assert the same bones the runtime scales.
        private void ApplyStylizationBoneScale(GameObject root)
        {
            if (Mathf.Approximately(headScale, 1f) &&
                Mathf.Approximately(handScale, 1f) &&
                Mathf.Approximately(footScale, 1f))
                return; // no-op: trust the delivered mesh baseline as-is

            var bones = root.GetComponentsInChildren<Transform>(true);
            int hitHead = ScaleBones(bones, HeadBoneTokens, headScale);
            int hitHands = ScaleBones(bones, HandBoneTokens, handScale);
            int hitFeet = ScaleBones(bones, FootBoneTokens, footScale);
            if (hitHead == 0 && !Mathf.Approximately(headScale, 1f))
                Debug.LogWarning("[CastawayCharacter] stylization: no Head bone matched " +
                                 "(headScale ignored — the delivered rig may use a different bone name)");
        }

        // Multiply each matched bone's localScale by `scale`. Returns the number of bones scaled.
        // Matched by .Contains (case-insensitive) so armature-prefixed names ("HumanArmature|Head")
        // still match — the same prefix lesson as the clip lookup. Idempotent within a single build
        // pass (BuildInEditor clears + rebuilds the Model child, so the FBX-fresh bone scales are 1).
        private static int ScaleBones(Transform[] bones, string[] tokens, float scale)
        {
            if (Mathf.Approximately(scale, 1f)) return 0;
            int hits = 0;
            foreach (var b in bones)
            {
                string n = b.name.ToLowerInvariant();
                foreach (var t in tokens)
                {
                    if (n.Contains(t.ToLowerInvariant()))
                    {
                        b.localScale = b.localScale * scale;
                        hits++;
                        break;
                    }
                }
            }
            return hits;
        }

        // Tint the character's flat per-part materials toward the castaway read. The Animated-Men
        // character carries SIX SEPARATE materials (Shirt / Skin / Pants / Eyes / Socks / Hair); map
        // each name to the castaway palette so the silhouette reads as a detailed young sun-worn
        // survivor, not the pack default OR a featureless single-tone collapse. Per-part SMOOTHNESS
        // varies (cloth matte, skin/hair glossier, eyes specular) for a "detailed/polished" read.
        private void ApplyCastawayRecolor(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                var copy = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    string name = src != null ? src.name : null;
                    copy[i] = MakeLitMaterial(CastawayColorFor(name),
                                              SmoothnessFor(name),
                                              "Castaway_" + (name ?? i.ToString()));
                }
                r.sharedMaterials = copy;
            }
        }

        /// <summary>
        /// Map a source material name to the castaway palette part-color (case-insensitive substring).
        /// Public + static so the EditMode recolor test can assert the mapping without instantiating.
        /// Handles all SIX Animated-Men materials + a leather accent. Eyes get a dark tone (the face
        /// reads); Socks read as bare tan feet (the castaway is barefoot); belt/strap/satchel read as
        /// warm leather (the U2-6 detail accent). Unknown names fall back to skin (safe warm).
        /// Order matters: leather is tested BEFORE pants/skin so a "Belt" material doesn't fall through.
        /// </summary>
        public static Color CastawayColorFor(string materialName,
            Color skin, Color shirt, Color pants, Color hair, Color eyes, Color leather)
        {
            string n = materialName != null ? materialName.ToLowerInvariant() : "";
            if (n.Contains("eye")) return eyes;
            if (n.Contains("hair")) return hair;
            // Leather accent (belt / strap / satchel / bag) — the U2-6 detail pass. Tested before the
            // cloth/skin fall-throughs so a leather part never collapses into trousers or skin.
            if (n.Contains("belt") || n.Contains("strap") || n.Contains("satchel") ||
                n.Contains("bag") || n.Contains("leather")) return leather;
            if (n.Contains("shirt") || n.Contains("cloth") || n.Contains("top")) return shirt;
            if (n.Contains("pant") || n.Contains("trouser") || n.Contains("leg") || n.Contains("short")) return pants;
            // Skin / Body / Head / Face / Socks (bare feet) / default -> healthy warm tan skin.
            return skin;
        }

        private Color CastawayColorFor(string materialName)
            => CastawayColorFor(materialName, skin, shirt, pants, hair, eyes, leather);

        /// <summary>
        /// Per-part smoothness: cloth (shirt/pants) matte; skin + hair catch a touch more of the warm
        /// key; eyes are the glossiest (a small specular dot for life). Kept low overall — the
        /// low-poly look reads by shape + shading, not gloss. Public + static for the EditMode test.
        /// </summary>
        public static float SmoothnessFor(string materialName)
        {
            string n = materialName != null ? materialName.ToLowerInvariant() : "";
            if (n.Contains("eye")) return 0.45f;
            // Leather (belt/strap/satchel) catches a soft worn sheen between cloth and skin — adds a
            // distinct material read for the U2-6 detail accent.
            if (n.Contains("belt") || n.Contains("strap") || n.Contains("satchel") ||
                n.Contains("bag") || n.Contains("leather")) return 0.22f;
            if (n.Contains("hair")) return 0.18f;
            if (n.Contains("skin") || n.Contains("body") || n.Contains("sock")) return 0.14f;
            return 0.06f; // cloth (shirt/pants) — matte
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

        // Build-safe URP Lit material WITHOUT editor code (the magenta-strip lesson: URP/Lit must be
        // registered in AlwaysIncludedShaders by the author step so it survives in the stripped exe).
        public static Material MakeLitMaterial(Color color, float smoothness, string name)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
