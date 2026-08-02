using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// HOW a find SITS IN THE WORLD — and therefore whether it is allowed to MOVE.
    ///
    /// === The Sponsor's rule (2026-08-02 soak of PR #351), stated general, not per-item ===
    /// "Motion cues are a property of PLACEMENT. An item DRIVEN INTO or RESTING ON something is STILL. An item
    /// LYING LOOSE may bob."
    ///
    /// Why it is a rule and not a tuning value: a rigid object embedded in a rigid host CANNOT translate or
    /// rotate relative to that host. When it does, the only reading the eye can construct is that the two are
    /// not connected — so the piece reads as HOVERING INSIDE the host rather than DRIVEN INTO it, and the whole
    /// real-world anchor collapses. That is a PHYSICAL-PLAUSIBILITY failure, not an intensity one: no amplitude
    /// is small enough to fix it, because the defect is the EXISTENCE of relative motion, not its size.
    ///
    /// MEASURED, not assumed — the shipped soak build's own gate log
    /// (`[WeaponFindVerifyCapture] REST: bladeTipY=-0.072 gripTopY=1.019 stumpTopY=0.475 peakBob=+/-0.050`)
    /// shows the bob never came close to lifting the blade clear of the wood: at the top of the bob the tip was
    /// still 0.497u BELOW the stump's top face. The pre-soak prediction's two failure modes were "too quiet" and
    /// "beacon-like"; the real one was neither. It was a bobbing sword inside solid wood.
    ///
    /// So the gate is on the PLACEMENT KIND, applied to the whole find-in-world attract cue rather than patched
    /// onto `sword_iron`: any future find authored <see cref="Embedded"/> or <see cref="RestingOn"/> is STILL
    /// for free, and only a <see cref="Loose"/> one is allowed the game-juice.md §1.5 collectible float-bob.
    /// </summary>
    public enum FindPlacement
    {
        /// <summary>DRIVEN INTO a host — the iron sword in the weathered stump, an axe buried in a log, a spear
        /// through a crate. The blade/head is INSIDE solid matter. <b>STILL.</b> This is deliberately the ZERO
        /// value so it is also the SAFE default: a find whose placement someone forgot to author is quiet, which
        /// is never physically wrong, whereas a defaulted-to-bobbing embedded find IS wrong and ships a defect.</summary>
        Embedded = 0,

        /// <summary>SET DOWN ON a surface that supports it — laid across a rock, propped on a crate, on a table.
        /// Not buried, but held by contact and gravity. <b>STILL</b>: a resting object that drifts up off its
        /// support and back reads exactly as broken as an embedded one that does.</summary>
        RestingOn = 1,

        /// <summary>LYING LOOSE, unattached — dropped in the grass, spilled from a wreck, with nothing holding
        /// it in a fixed relationship to anything. <b>MAY bob and sway</b> (the game-juice.md §1.5 collectible
        /// float-bob): there is no host for the motion to contradict, so the cue costs no plausibility.</summary>
        Loose = 2,
    }

    /// <summary>
    /// A WEAPON RESTING IN THE WORLD, waiting to be FOUND (ticket 86cah7y5b — the SECOND acquisition route;
    /// Combat/HP/Death LOCKED design 86cabcdpn decision 4 "acquisition = BOTH craft AND find"; Uma
    /// `team/uma-ux/combat-cluster-design-brief.md` §3.4).
    ///
    /// === REAL-WORLD ANCHOR (lowpoly-quality.md §0 — the plain sentence the build must satisfy) ===
    /// This is an IRON SWORD LEFT BEHIND BY SOMEONE WHO CAME BEFORE: it is driven POINT-DOWN INTO a weathered
    /// stump — blade buried, grip UP at the top where a hand would close on it. You PULL IT UP AND OUT. It is
    /// NOT lying flat on the grass and NOT hovering above the ground: the tip stays INSIDE the wood at every
    /// point of the attract bob (<see cref="bobAmplitude"/> is far smaller than the authored embed depth), and
    /// the stump REMAINS after the loot with an empty slot in it — the story survives the pickup. Judge this
    /// with a SIDE-PROFILE capture (blade-down / grip-up is invisible from above and from player-eye, obvious
    /// side-on) — the -verifyWeaponFind gate shoots one.
    ///
    /// === IPickable — the SHARED E-loot surface, no second pickup path (AC1 constraint) ===
    /// The find is an ordinary <see cref="IPickable"/>: the player-side <see cref="PickableLooter"/> discovers
    /// it, resolves nearest-in-range, paints the shared <see cref="LootPrompt"/>, and calls
    /// <see cref="TryLoot"/> on the E rising edge. There is NO bespoke input here, NO second looter, and NO
    /// proximity-auto pickup ([[active-input-not-proximity-auto-for-actions]] — walking up does nothing until
    /// E). Contrast <see cref="SpearPickup"/> / <see cref="FarHorizon.AxePickup"/>, which are the OLDER
    /// proximity-auto POC pickups: this ticket deliberately rides the E-loot seam instead (AC1's constraint,
    /// and the reason a parallel path is forbidden — it would fork the click/E arbitration settled in round-4
    /// of 86caffwv5).
    ///
    /// === Data, not code (AC1 constraint) ===
    /// <see cref="itemId"/> is an EXISTING canonical id — <c>sword_iron</c> by default (the Sponsor's
    /// 2026-07-27 decision: the find is `sword_iron`, ONE per island region). There is NO `FoundWeapon` /
    /// `UniqueWeapon` type, no second weapon model and no bespoke equip path: the loot lands the SAME
    /// <see cref="FarHorizon.ItemCatalog"/> ItemDef a CRAFTED iron sword lands, via the SAME
    /// <see cref="FarHorizon.Inventory.PickUpWeapon"/> → <c>AddToolToBelt</c> belt seam, so it selects, seats
    /// and swings through the shipped weapon vocabulary. Changing the find to a dagger is a const, not a class.
    ///
    /// === The attract cue (AC3/AC7) — GATED ON PLACEMENT, and why the rim half is absent ===
    /// ⚠ THE CUE ONLY RUNS ON A <see cref="FindPlacement.Loose"/> FIND. See <see cref="FindPlacement"/> for the
    /// Sponsor's rule and the soak that produced it: an item driven into or resting on something is STILL. The
    /// shipped `sword_iron` find is <see cref="FindPlacement.Embedded"/>, so it does NOT move — the motion the
    /// 2026-08-02 soak rejected ("the sword is floating, moving in the stump") is gone at the source rather than
    /// dialled down, because relative motion between an embedded object and its rigid host is wrong at ANY
    /// amplitude. <see cref="CueMoves"/> is the single switch; <see cref="EffectiveBobAmplitude"/> /
    /// <see cref="EffectiveSwayDegrees"/> are what the frame actually uses.
    ///
    /// When placement DOES permit motion, the cue rides TWO independent channels, both transform-only, both on
    /// the weapon child:
    ///   CH1 FLOAT-BOB — local Y translation, ±<see cref="bobAmplitude"/> at <see cref="bobHz"/>;
    ///   CH2 SWAY      — local YAW rotation,  ±<see cref="swayDegrees"/> at <see cref="swayHz"/>,
    ///                   deliberately NON-HARMONIC against CH1 so the two never fuse into one pulse.
    /// Both share the PER-INSTANCE SEEDED PHASE so a pool of finds never pulses in sync (game-juice.md §1.5).
    /// Two channels rather than one because a cue resting on a SINGLE channel dies the moment that channel is
    /// masked — a pure vertical bob is nearly invisible when the camera sits level with it or when the piece
    /// reads against busy scatter.
    ///
    /// The authored <see cref="bobAmplitude"/> / <see cref="swayDegrees"/> values are deliberately LEFT
    /// non-zero on the embedded find rather than zeroed in the scene. Zeroing the fields would hide the rule
    /// inside one scene's serialized data, where the next author re-types a non-zero amplitude and the defect
    /// returns silently. Keeping the tuned values and gating on PLACEMENT keeps the rule in the code, makes the
    /// placement field the whole knob, and means flipping a find to <see cref="FindPlacement.Loose"/> restores a
    /// cue that was already tuned.
    ///
    /// No light beam, no post-process Volume, no particle loop, no emissive, no second material, and no
    /// <c>MaterialPropertyBlock</c> — this component touches TRANSFORMS ONLY, so the world MeshRenderer stays
    /// eligible for the GPU Resident Drawer instanced path (unity6-mastery.md §2 disqualifier list;
    /// game-juice.md §2). Each channel is verified LIVE frame-by-frame in the SHIPPED build by the
    /// -verifyWeaponFind gate — a channel is never assumed live because the code for it exists.
    ///
    /// The ticket also specified a low Fresnel rim via the `_RimIntensity` opt-in. NOT reachable — and this was
    /// re-settled from the SHIPPED ASSET rather than from the generator code, because the generator's
    /// URP/Unlit `Shader.Find` pin was removed (`MovementCameraScene.cs`, R5 / 86cahne3d: "the URP/Unlit pin is
    /// removed — the shared Mat_WeaponPalette ships as a serialized material reference"). Reading the material
    /// file itself: `Assets/Art/Props/WeaponPack/Mat_WeaponPalette.mat` line 24 carries
    /// `m_Shader guid: 650dd9526735d5b46b79224bc6e94025`, which resolves to the URP package's
    /// `Shaders/Unlit.shader` — that shader declares ZERO `_Rim*` properties. `_RimIntensity` exists only on
    /// `FarHorizon/LowPolyVertexColor` (guid `3940cb47c8d8af14e86ed0e91f377a2b`). A `SetFloat("_RimIntensity", …)`
    /// on the shipped weapon material is therefore a SILENT NO-OP, not a subtle effect. Reaching it would need a
    /// new shader or a second material for the weapon family — exactly what the shared-palette / ~1-draw-call
    /// discipline (blender-asset-pipeline.md; ticket OOS "no new material") forbids. So the rim stays out and
    /// the cue rides CH1+CH2; a rim, if wanted, is a scoped follow-up with a real material cost attached.
    ///
    /// === Pickup feel (AC4) — everything that moves is EASED (game-juice.md §1.1) ===
    /// On a successful loot the weapon LEAVES the world on an eased arc to the player's belt: an ease-out
    /// position blend (<see cref="ArcEase01"/>, cubic — never a linear lerp, the single most common
    /// "feels cheap" defect) plus a small mid-flight lift and an eased shrink, then the visual switches off.
    /// The E-prompt is the shared <see cref="LootPrompt"/> widget (no second prompt authored). NO CHIME:
    /// the project ships ZERO audio today (no AudioSource, no AudioClip, no audio asset anywhere in Assets/ —
    /// verified 2026-07-27), so the soft chime AC4 asks for would mean standing up an audio subsystem. That is
    /// a separate ticket, not a silent expansion of this one.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The find GameObject, its stump host, the weapon FBX instance + this component's inventory/player refs
    /// are authored EDITOR-TIME by MovementCameraScene.BuildWeaponFinds and SERIALIZE into Boot.unity — never
    /// added at Awake (the component-in-source-but-not-in-scene / "legs-up" class). The Awake resolves are a
    /// build-safety net only. WeaponFindSceneTests guards the serialized presence + wiring.
    ///
    /// === Trace instrumentation (the no-new-class-without-trace discipline) ===
    /// One-shot `[weaponfind-trace]` lines on the first loot + the first declined loot, EDITOR/dev-only
    /// (<c>[Conditional("UNITY_EDITOR")]</c> strips the call AND its string concat from the shipped release
    /// exe — unity6-mastery.md §5/§10). Never logs from the bob/arc hot path.
    ///
    /// NO MUTABLE STATICS (instance fields only) — no [RuntimeInitializeOnLoadMethod] reset needed
    /// (StaticStateResetTests stays green).
    /// </summary>
    public sealed class WorldWeaponFind : MonoBehaviour, IPickable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory the found weapon lands in (belt-first, via Inventory.PickUpWeapon). Wired at " +
                 "bootstrap by BuildWeaponFinds; scene-found fallback in Awake is a build-safety net only.")]
        public FarHorizon.Inventory inventory;

        [Tooltip("The player the pickup arc flies to. Wired at bootstrap; falls back to the ClickToMove root.")]
        public Transform player;

        [Tooltip("The WEAPON visual (the bobbing child holding the FBX mesh). This is what bobs, arcs to the " +
                 "belt and switches off on loot. Falls back to this transform. The STUMP host is a SIBLING and " +
                 "is deliberately NOT this — it stays put, and stays visible after the loot.")]
        public Transform visual;

        [Header("Identity — DATA, not code (AC1)")]
        [Tooltip("The canonical ItemCatalog/WeaponCatalog id this find grants. sword_iron by default (Sponsor " +
                 "decision 2026-07-27). Changing the piece is a const change, never a new class.")]
        public string itemId = FarHorizon.ItemCatalog.SwordIronId;

        [Tooltip("The word the shared LootPrompt shows — 'Press E to pick up an iron sword'. Reads as a single " +
                 "special object (the article is intentional), unlike the mass nouns the resource pickables use.")]
        public string displayName = DefaultDisplayName;

        [Header("Loot reach")]
        [Tooltip("Planar (XZ) reach for the E loot — this find's own IPickable.LootRange. In the log-pile / " +
                 "berry class (~1.2-2.0u per AC3): you walk up to the stump, you do not loot from across the " +
                 "clearing. default - Sponsor-soak tunes.")]
        public float lootRadius = DefaultLootRadius;

        [Header("Placement (AC7) — HOW it sits in the world, which decides whether it may MOVE")]
        [Tooltip("The Sponsor's rule (2026-08-02 soak): motion cues are a property of PLACEMENT. Embedded " +
                 "(driven into) and RestingOn (set down on) are STILL; only Loose may bob and sway. Embedded " +
                 "is the zero value, so it is also the safe default — a forgotten placement is quiet, never a " +
                 "sword hovering inside solid wood. This field is the WHOLE motion knob for the attract cue.")]
        public FindPlacement placement = FindPlacement.Embedded;

        [Header("Attract cue (AC3) — LOOSE placements only; no rim, no beam, no volume, no particles, no MPB")]
        [Tooltip("Bob amplitude in world units (+/-). 0.05 default (game-juice.md §1.5 collectible float-bob). " +
                 "IGNORED unless `placement` is Loose. On a Loose find it must still stay well under any " +
                 "authored embed depth. default - Sponsor-soak tunes.")]
        public float bobAmplitude = DefaultBobAmplitude;

        [Tooltip("Bob frequency in Hz. 0.8 default — a slow breath, not a videogame pulse.")]
        public float bobHz = DefaultBobHz;

        [Tooltip("Per-instance seeded phase offset (radians) so several finds never bob in sync (the seeded " +
                 "stagger game-juice.md §1.5 calls for; extends the seeded-scatter pattern). Set by the scatter.")]
        public float bobPhase;

        [Tooltip("CHANNEL 2 — sway amplitude in DEGREES of yaw (+/-). A slow rock about the piece's own axis. " +
                 "Transform-only like the bob, and gated by the SAME `placement` field: IGNORED unless the " +
                 "find is Loose. default - Sponsor-soak tunes.")]
        public float swayDegrees = DefaultSwayDegrees;

        [Tooltip("Sway frequency in Hz. DELIBERATELY NON-HARMONIC against bobHz (0.53 vs 0.8) so the two " +
                 "channels never fuse into one perceived motion — that independence is what makes this a " +
                 "second channel rather than a restatement of the first.")]
        public float swayHz = DefaultSwayHz;

        [Header("Pickup feel (AC4) — eased, never linear")]
        [Tooltip("Seconds the weapon takes to arc from the stump to the belt. default - Sponsor-soak tunes.")]
        public float arcSeconds = DefaultArcSeconds;

        [Tooltip("Extra height (world units) the arc lifts through at its midpoint, so the piece rises out of " +
                 "the stump before it travels — a pull-out-and-up read, not a slide.")]
        public float arcLift = DefaultArcLift;

        [Tooltip("Metres above the player root the arc terminates at (roughly belt height on the ~1.8m castaway).")]
        public float beltHeight = DefaultBeltHeight;

        // === Named defaults (a single source the bootstrap + the tests read — never magic literals) ===
        /// <summary>The prompt word: "Press E to pick up an iron sword".</summary>
        public const string DefaultDisplayName = "an iron sword";
        /// <summary>Loot reach default — the log-pile/berry class band AC3 names (~1.2-2.0u).</summary>
        public const float DefaultLootRadius = 1.6f;
        /// <summary>Float-bob amplitude default (+/- world units) — game-juice.md §1.5.</summary>
        public const float DefaultBobAmplitude = 0.05f;
        /// <summary>Float-bob frequency default (Hz) — game-juice.md §1.5.</summary>
        public const float DefaultBobHz = 0.8f;
        /// <summary>Sway amplitude default (+/- degrees of yaw) — channel 2 of the attract cue.</summary>
        public const float DefaultSwayDegrees = 4f;
        /// <summary>Sway frequency default (Hz). NON-HARMONIC against <see cref="DefaultBobHz"/> on purpose.</summary>
        public const float DefaultSwayHz = 0.53f;
        /// <summary>Pickup-arc duration default (seconds).</summary>
        public const float DefaultArcSeconds = 0.45f;
        /// <summary>Pickup-arc mid-flight lift default (world units).</summary>
        public const float DefaultArcLift = 0.55f;
        /// <summary>Arc terminus height above the player root default (world units).</summary>
        public const float DefaultBeltHeight = 0.9f;

        // Runtime state — instance only (no mutable statics).
        private bool _looted;
        private bool _arcing;
        private float _arcT;            // 0..1 along the pickup arc
        private Vector3 _arcFrom;       // world position the weapon left the stump from
        private Vector3 _visualBaseLocal;
        private Vector3 _visualBaseScale;
        private Quaternion _visualBaseLocalRot;
        private bool _tracedFirstLoot;
        private bool _tracedFirstDeclined;
        private bool _stillPoseSettled;

        /// <summary>True until this find has been looted. PlayMode tests + the capture gate read it.</summary>
        public bool IsAvailable => !_looted;

        // ============================================================================================
        // THE PLACEMENT → MOTION GATE (AC7). See FindPlacement for the Sponsor's rule and the soak behind it.
        // PURE statics so the EditMode guards assert the gate with no scene, no Time and no frame loop.
        // ============================================================================================

        /// <summary>
        /// THE RULE, as one pure function: an item DRIVEN INTO or RESTING ON something is STILL; an item LYING
        /// LOOSE may bob. Everything else in this file's cue path reads through here, so there is exactly ONE
        /// place the rule lives and exactly one place to change it.
        /// </summary>
        public static bool MotionAllowedFor(FindPlacement placement) => placement == FindPlacement.Loose;

        /// <summary>The bob amplitude the FRAME actually uses — the authored value on a Loose find, exactly 0
        /// on a still one. Pure, so a test can assert the gate for every placement without a scene.</summary>
        public static float EffectiveBobAmplitudeFor(FindPlacement placement, float authoredAmplitude)
            => MotionAllowedFor(placement) ? authoredAmplitude : 0f;

        /// <summary>The sway amplitude the FRAME actually uses — authored on Loose, exactly 0 otherwise.</summary>
        public static float EffectiveSwayDegreesFor(FindPlacement placement, float authoredDegrees)
            => MotionAllowedFor(placement) ? authoredDegrees : 0f;

        /// <summary>Whether THIS find's attract cue is permitted to move at all. The shipped sword-in-a-stump
        /// answers FALSE. The capture gate and the PlayMode guard branch on this rather than on the item id, so
        /// the rule holds for every future find rather than for `sword_iron` alone.</summary>
        public bool CueMoves => MotionAllowedFor(placement);

        /// <summary>This find's live bob amplitude after the placement gate. 0 on an embedded/resting find —
        /// which is also what the anchor checks must use as their "peak bob", or they measure a motion that
        /// cannot happen.</summary>
        public float EffectiveBobAmplitude => EffectiveBobAmplitudeFor(placement, bobAmplitude);

        /// <summary>This find's live sway amplitude after the placement gate. 0 on an embedded/resting find.</summary>
        public float EffectiveSwayDegrees => EffectiveSwayDegreesFor(placement, swayDegrees);

        /// <summary>True while the eased pickup arc is still flying (the weapon has left the stump but has not
        /// yet reached the belt). Exposed so a test/capture can wait the beat out deterministically.</summary>
        public bool IsArcing => _arcing;

        // ============================================================================================
        // IPickable — the world-item side of the shared E-loot surface.
        // ============================================================================================

        /// <summary>IPickable: loot-able while the weapon is still resting here AND an inventory is wired.
        /// Once looted this goes false, so the looter's nearest-in-range resolve SKIPS it — a second E press
        /// finds nothing here and is a clean no-op (AC6: "a second E does nothing").</summary>
        public bool CanLoot => !_looted && inventory != null;

        /// <summary>IPickable: the find's world position for the nearest-in-range resolve. Deliberately the
        /// STATIONARY root, NOT the bobbing child — otherwise the loot reach would wobble with the cue and the
        /// prompt could flicker in and out at the range boundary. Planar XZ, height-robust (the shared idiom).</summary>
        public Vector3 LootPosition => transform.position;

        /// <summary>IPickable: this find's own loot reach. The looter uses THIS per-item radius, never a global.</summary>
        public float LootRange => lootRadius;

        /// <summary>IPickable: the name the shared LootPrompt shows (no second prompt widget authored).</summary>
        public string DisplayName => displayName;

        /// <summary>
        /// IPickable.TryLoot — the WHOLE loot transaction for the find: add the canonical
        /// <see cref="itemId"/> ItemDef to the inventory BELT (the same seam a crafted weapon lands through)
        /// AND release the world weapon onto its eased arc. Returns true IFF exactly one landed.
        ///
        /// A declined loot (no def for the id, or belt AND pack both full, or the weapon is already owned) is a
        /// clean no-op: the weapon is NOT consumed and stays resting in the stump, so the player can come back.
        /// Uses the wired <see cref="inventory"/>; <paramref name="inv"/> is honoured for the interface contract
        /// and used when this find's own ref is unset (bare test rigs).
        /// </summary>
        public bool TryLoot(FarHorizon.Inventory inv)
        {
            if (inventory == null) inventory = inv;
            if (inventory == null || _looted) return false;

            if (string.IsNullOrEmpty(itemId)) return Declined("no itemId configured");
            if (inventory.Model.OwnsItem(itemId)) return Declined("already owned (" + itemId + ")");
            if (!inventory.PickUpWeapon(itemId)) return Declined("PickUpWeapon declined (unknown id or belt+pack full)");

            _looted = true;
            BeginArc();

            if (!_tracedFirstLoot)
            {
                _tracedFirstLoot = true;
                FindTrace("FOUND + looted '" + itemId + "' -> belt; the stump stays, the weapon arcs out");
            }
            return true;
        }

        private bool Declined(string why)
        {
            if (!_tracedFirstDeclined)
            {
                _tracedFirstDeclined = true;
                FindTrace("loot DECLINED (" + why + ") -> the weapon stays in the stump, clean no-op");
            }
            return false;
        }

        // ============================================================================================
        // Cue + feel. Transforms only — no MaterialPropertyBlock, no material writes, no allocation.
        // ============================================================================================

        private void Awake()
        {
            // Build-safety net only: the SERIALIZED refs authored by BuildWeaponFinds are the source of truth.
            if (inventory == null) inventory = FindObjectOfType<FarHorizon.Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<FarHorizon.ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (visual == null) visual = transform;
            _visualBaseLocal = visual.localPosition;
            _visualBaseScale = visual.localScale;
            // The AUTHORED point-down orientation is the sway's rest pose — the sway is an OFFSET composed
            // onto it, never a replacement. Snapshotting it (rather than rebuilding from Euler) keeps the
            // baked seat exactly where BuildWeaponFindSite put it.
            _visualBaseLocalRot = visual.localRotation;
        }

        private void Update()
        {
            if (visual == null) return;

            if (_arcing) { StepArc(); return; }
            if (_looted) return;

            // === THE PLACEMENT GATE (AC7) — the Sponsor's rule, enforced before any motion is composed ===
            // An item DRIVEN INTO or RESTING ON something is STILL. The shipped sword-in-a-stump takes this
            // branch: it is pinned to its authored seat and NEVER written again. Restoring the base pose exactly
            // once (rather than re-writing it every frame) also keeps the transform CLEAN for the rest of the
            // session — a still find dirties no transform, allocates nothing and costs one bool test per frame,
            // which is strictly better for the instanced/batched draw path than the per-frame writes the moving
            // branch performs (unity6-mastery.md §2/§5).
            if (!CueMoves)
            {
                if (_stillPoseSettled) return;
                _stillPoseSettled = true;
                visual.localPosition = _visualBaseLocal;
                visual.localRotation = _visualBaseLocalRot;
                return;
            }

            // Resting attract cue — TWO independent transform-only channels on the weapon child only. The site
            // root does not move (so LootPosition + the prompt stay rock steady).
            //   CH1 float-bob  — LOCAL Y translation, ±EffectiveBobAmplitude @ bobHz
            //   CH2 sway       — LOCAL yaw rotation,  ±EffectiveSwayDegrees  @ swayHz (non-harmonic vs bobHz)
            // Two channels, not one, because a cue resting on a SINGLE channel dies whenever that channel is
            // masked — a bob alone is invisible when the player's eye is level with it, or when the piece is
            // read against a busy scatter silhouette. Neither channel touches a material, so there is no
            // MaterialPropertyBlock and the world MeshRenderer stays in the GPU Resident Drawer instanced path
            // (unity6-mastery.md §2). The channels are read through the EFFECTIVE accessors, never the raw
            // serialized fields, so the placement gate cannot be bypassed by a future edit here.
            _stillPoseSettled = false;   // placement could be flipped live (inspector / a future authoring tool)

            var p = _visualBaseLocal;
            p.y += BobOffset(Time.time, EffectiveBobAmplitude, bobHz, bobPhase);
            visual.localPosition = p;

            visual.localRotation = _visualBaseLocalRot *
                Quaternion.Euler(0f, SwayOffset(Time.time, EffectiveSwayDegrees, swayHz, bobPhase), 0f);
        }

        // Snapshot the departure pose + hand the weapon to the arc. Reparenting is avoided (the visual keeps its
        // parent; the arc drives WORLD position and uniform scale, then switches the renderers off).
        private void BeginArc()
        {
            _arcing = true;
            _arcT = 0f;
            _arcFrom = visual.position;
        }

        private void StepArc()
        {
            _arcT += Time.deltaTime / Mathf.Max(0.01f, arcSeconds);
            float t = Mathf.Clamp01(_arcT);
            float e = ArcEase01(t);                      // EASE-OUT — never a linear lerp (game-juice.md §1.1)

            Vector3 to = player != null
                ? player.position + Vector3.up * beltHeight
                : _arcFrom + Vector3.up * beltHeight;

            Vector3 pos = Vector3.Lerp(_arcFrom, to, e);
            pos.y += arcLift * ArcLift01(t);             // rises out of the stump, then settles onto the belt
            visual.position = pos;
            visual.localScale = _visualBaseScale * Mathf.Lerp(1f, 0f, e * e); // eased shrink, not a pop

            if (t < 1f) return;

            // Arrived: the weapon is on the belt now, so switch the world visual off. SetActive(false) on the
            // WEAPON child only — the stump host is a sibling and stays, keeping the story in the world.
            _arcing = false;
            visual.localPosition = _visualBaseLocal;
            visual.localRotation = _visualBaseLocalRot;   // undo the sway offset with the rest of the cue
            visual.localScale = _visualBaseScale;
            visual.gameObject.SetActive(false);
        }

        // ============================================================================================
        // PURE seams — static + dependency-free so the EditMode guards assert the cue/feel maths with no
        // scene, no Time, and no frame loop (the ShouldLootOnKey / ResolveInteractionPrompt idiom).
        // ============================================================================================

        /// <summary>
        /// PURE float-bob offset (AC3): a sine of <paramref name="amplitude"/> at <paramref name="hz"/>, phase-
        /// shifted per instance by <paramref name="phase"/> so a pool of finds never pulses in sync. A sine IS
        /// the ease here (continuous velocity through both turning points) — there is no linear segment.
        /// Amplitude 0 (or a non-positive frequency) returns exactly 0, so the cue can be dialled fully off.
        /// </summary>
        public static float BobOffset(float timeSeconds, float amplitude, float hz, float phase)
        {
            if (amplitude == 0f || hz <= 0f) return 0f;
            return Mathf.Sin(timeSeconds * hz * 2f * Mathf.PI + phase) * amplitude;
        }

        /// <summary>
        /// PURE sway offset in DEGREES of yaw — CHANNEL 2 of the attract cue. Same sine shape as the bob (a
        /// sine IS the ease: continuous velocity through both turning points) but at a deliberately
        /// NON-HARMONIC frequency, so the two channels drift in and out of phase instead of fusing into one
        /// perceived pulse. Shares the per-instance <paramref name="phase"/> so a whole pool still desyncs.
        /// Amplitude 0 (or a non-positive frequency) returns exactly 0 — the channel can be dialled fully off
        /// without disturbing the bob.
        /// </summary>
        public static float SwayOffset(float timeSeconds, float degrees, float hz, float phase)
        {
            if (degrees == 0f || hz <= 0f) return 0f;
            return Mathf.Sin(timeSeconds * hz * 2f * Mathf.PI + phase) * degrees;
        }

        /// <summary>
        /// PURE pickup-arc easing (AC4) — a CUBIC EASE-OUT: fast departure, soft arrival. Clamped to [0,1] with
        /// ArcEase01(0) == 0 and ArcEase01(1) == 1 exactly, and strictly increasing in between (the property the
        /// EditMode guard asserts, so nobody can quietly regress it to a linear lerp — the single most common
        /// "feels cheap" defect per game-juice.md §1.1).
        /// </summary>
        public static float ArcEase01(float t)
        {
            float c = Mathf.Clamp01(t);
            float inv = 1f - c;
            return 1f - inv * inv * inv;
        }

        /// <summary>
        /// PURE mid-flight lift profile: a half-sine that is 0 at both ends and 1 at the midpoint, so the weapon
        /// rises OUT of the stump and then settles, rather than sliding sideways out of the wood (the real-world
        /// anchor: you pull a buried blade UP first). Zero at t=0 and t=1 so it never displaces the endpoints.
        /// </summary>
        public static float ArcLift01(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        // [weaponfind-trace] — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call AND its argument
        // evaluation (the string concat) from the shipped IL2CPP release exe (unity6-mastery.md §5 "no Debug.Log
        // in hot paths" / §10 "strip logging from shipping builds"). One-shot guards keep it quiet.
        // Matches the project dev-log gate convention ([stick-trace] / [bush-trace] / [loot-trace]).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void FindTrace(string msg) => Debug.Log("[weaponfind-trace] " + msg);
    }
}
