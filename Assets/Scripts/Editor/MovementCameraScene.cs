using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Unity.AI.Navigation;
using FarHorizon;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// Editor-time authoring of the movement + camera foundation INTO the Boot scene
    /// (ticket 86ca86fme — the deliberate port of the spike's PoE click-to-move + orbit camera).
    ///
    /// Why editor-time and not Awake(): the editor-vs-runtime serialization trap
    /// (unity-conventions.md §"Editor-vs-runtime divergence"). A hierarchy assembled in Awake()
    /// passes EditMode checks but can ship MANGLED in the player. The player, orbit camera, flat
    /// walkable ground, and — critically — the BAKED NAVMESH-AS-SAVED-ASSET are all authored here
    /// (executeMethod), saved into Boot.unity, so the shipped exe references serialized assets.
    ///
    /// NavMesh-as-asset: NavMeshSurface bake-in-memory works in the editor but ships a DEAD
    /// click-to-move in the standalone player (no surface) unless the NavMeshData is SAVED as an
    /// asset and assigned (unity-conventions.md §NavMesh; spike FINDINGS iter-3). We save it.
    ///
    /// Scope (U3): player + camera systems + a minimal FLAT walkable test ground only. The real
    /// environment (terrain/lighting/skybox/post) is U5's surface (Drew) — this builder keeps the
    /// boot-scene ground deliberately minimal so the two lanes rebase cleanly.
    /// </summary>
    public static class MovementCameraScene
    {
        private const string NavMeshDir = "Assets/NavMesh";
        private const string PrefabDir = "Assets/Prefabs";
        private const string SettingsDir = "Assets/Settings";
        private const string NavMeshDataPath = NavMeshDir + "/BootNavMesh.asset";
        private const string MarkerPrefabPath = PrefabDir + "/ClickMarker.prefab";

        // Flat test ground extent (X/Z half-size). Big enough to prove pathfinding across a real
        // distance; small enough to stay a "test ground", not an environment (U5 owns that).
        private const float GroundHalf = 30f;

        // On-screen height (world units) for the castaway avatar root. The FBX is normalized to ~1u
        // intrinsic by CharacterAssetGen, so this scale maps directly onto height. Matched to the
        // NavMeshAgent height (1.8u) so the visible character lines up with the agent capsule.
        private const float PlayerVisualHeight = 1.8f;

        // ---- Hero axe (ticket 86ca8ce6y — RE-DONE; re-pointed for the Hyper3D castaway, 86ca8rdkp;
        // re-made IN-HOUSE under 86cabh907 — Route A weapon SET). The procedural HeroAxeMesh wedge AND the
        // earlier CC-BY sourced hatchet are both RETIRED; the axe is the in-house faceted flat-shaded
        // wpn_axe_stone_01 (WeaponPackAssetGen.HeroAxeFbxPath, shared palette material). It is ATTACHED to the
        // castaway's RIGHT HAND bone (mixamorig:RightHand — probe-verified by
        // CharacterAssetGen.CharacterDiagnoseTrace, 2026-06-15) so the castaway HOLDS it, and is shown only
        // once crafted (HeldAxe gates on Inventory.HasAxe). Name kept "HeroAxe" so the verify-capture +
        // scene-presence guards key on it. ----
        public const string HeroAxeObjectName = "HeroAxe";

        // The Hyper3D castaway's right-hand WRIST bone. The Mixamo rig names it "mixamorig:RightHand"; the
        // finger bones ("mixamorig:RightHandIndex1"...) ALSO contain "righthand", so the attach search must
        // match the WRIST (the bone whose name ENDS at "righthand" — no finger/thumb suffix), excluding the
        // "end" helper. Probe-verified (CharacterDiagnoseTrace): NO lossy-scale trap on this rig — every bone
        // reads lossyScale (1,1,1) (the height-normalize rides globalScale, not a compensating bone scale),
        // UNLIKE the chibi's 267× RightHand_010. So held-axe scale/offset are in clean units (no ÷267).
        public const string RightHandBoneToken = "righthand";

        // Attach pose for the sourced hatchet (SOAKFIX2 2026-06-13 — the NO-AXE soak fix). The held axe must
        // read UNMISTAKABLY in the GAMEPLAY orbit view (dist 14u, pitch 55°), not just the -verifyAxe close-up.
        //
        // ROOT CAUSE the gameplay-view trace (the historical axe gameplay-view trace) PROVED the Sponsor's "no axe":
        // the prior pose (scale 0.0015 → 0.43u longest, blade-DOWN at the HIP, max.y=0.71) projected to only
        // ~3.7% of the frame height at the orbit framing — a thin vertical sliver hanging by the leg, lost
        // beside a chibi whose own silhouette is only ~8% of frame from 55° top-down. The -verifyAxe capture
        // ZOOMS its own camera to whatever size the axe is, so it framed a perfect hatchet — FALSE-GREEN, the
        // exact class that bit the castaway. (See gameplay_orbit_axe.png evidence in the PR body.)
        //
        // THE BONE'S LOCAL FRAME IS ROTATED (PROBE-VERIFIED): RightHand_010's local +Y maps to world
        // (0.48,-0.84,0.23) — mostly DOWN. So localPosition.y does NOT lift the axe world-up (a lift sweep via
        // localPos went the WRONG way). Fix: pose the held axe in WORLD space after parenting (Unity back-
        // solves + serializes the local transform), so the pose is intuitive + robust to the bone frame.
        //
        // VALUES (dialed via the historical axe dial-in trace + SOAKFIX4 pose trace against the orbit render):
        // SOAKFIX4 (the Sponsor's "handle sticks out by the EAR"): the prior +0.55 up-offset seated the axe
        // CENTER at world-y 0.66 with its TOP at 1.040 — and the chibi's head bone is at world-y 0.775, so
        // the haft top rose level with the EAR (PoseTrace: HeroAxe max.y 1.040 vs head-bone 0.775; the huge
        // head + short arms put "chest height" at ear height). FIX: DROP the up-offset +0.55 -> +0.05 so the
        // axe rides at the hand at the kid's SIDE (handle top now clears below the shoulder, well under the
        // head), and re-pitch the euler to BLADE-DOWN-AT-THE-SIDE (head/blade hangs toward the hip, handle
        // angled up just past the hand) — NOT the prior blade-flat-up tip that lifted the haft. A residual
        // tilt is kept (not dead-vertical) so the head still reads as an axe-head shape from the 55° top-down
        // orbit (a fully vertical prop foreshortens to a dot — the visibility lesson). Scale 0.0040 (×267
        // lossy ≈ 1.0u longest) UNCHANGED — the size already read as a clear hero hatchet; only height+pitch
        // move. Re-measured by PoseTrace post-change; final read is the SHIPPED build (editor RT is
        // framing-only, not colour — unity-conventions.md).
        // SOAKFIX8 (86ca8ce6y — the held axe ROTATION TRACKS the hand bone in ALL facings; APPROVED, KEPT).
        // The Sponsor proved the rotation bug ("the axe ... points the same way on the x axis all the time"):
        // a fixed WORLD rotation can't survive a turn. The held axe's rotation is HAND-RELATIVE so the haft
        // turns WITH the hand through every facing. soakfix9 keeps this rotation channel unchanged.
        //
        // SOAKFIX9 (86ca8ce6y — the held axe POSITION fix; the real bug THIS wave). The Sponsor proved it
        // with the F9 nudge tool: ONE arrow-left click (a 0.02 step) flung the held axe ~5 METRES off-screen
        // (localPos (0,0.0001,0) -> (-0.02,0.0001,0) sent the axe ~5 m left). ROOT CAUSE: soakfix8 posed the
        // axe POSITION as a localPosition on RightHand_010, which carries a ~267× lossyScale (probe-verified,
        // unity-conventions.md §FBX) — so a 0.02 LOCAL step = ~5.3 WORLD units. This is exactly the documented
        // §FBX lossy-bone-scale trap; soakfix8's all-local pose walked back into it on the POSITION channel.
        //
        // FIX — SPLIT the two channels (HeldAxeRig drives both each frame; unity-conventions.md §FBX):
        //   POSITION → a HAND-LOCAL offset, rotated by the hand rotation (86ca9qwvd — the HAND-LOCAL space
        //     fix): axe.position = hand.position + hand.rotation * offset. soakfix9 applied the offset as a
        //     RAW WORLD vector (hand.position + worldOffset) that did NOT rotate with the hand, so it was only
        //     correct at the SPAWN facing — turning the character swung the axe out of the hand (the bug THIS
        //     wave). Rotating the cm offset by hand.rotation makes it TRACK the hand through every facing; a
        //     pure rotation preserves the magnitude, so the F9 nudge still moves the axe ~2 cm/click and the
        //     bone's lossyScale never touches the offset (it is NOT hand.TransformPoint, which re-applies it).
        //   ROTATION → HAND-RELATIVE: axe.rotation = hand.rotation * Euler(relEuler) — the soakfix8 fix, KEPT,
        //     so the haft still turns with the hand on every facing.
        // SCALE rides the bone hierarchy unchanged (localScale × 267× ≈ 1.0u — the hero-hatchet size). The axe
        // stays a CHILD of the bone (so scale + the EditMode hand-local serialization guards hold); only
        // position+rotation are world-driven by HeldAxeRig. The F9 AxeNudgeTool nudges the RIG's worldOffset
        // (WORLD units) + relEuler (hand-relative) — so dial == baked == in-motion, in SENSIBLE world units.
        // HELD-AXE SCALE (86ca8rdkp — RE-DERIVED for the Hyper3D rig). The OLD 0.0040 was for the chibi's
        // 267× lossy hand bone; THIS rig's bones read lossyScale (1,1,1) (probe-verified), and the axe sits
        // under the avatar root scaled PlayerVisualHeight (1.8). The STONE axe FBX is FAMILY-normalized so its
        // imported longest-axis == the retired axe's ~1.08u (WeaponPackAssetGen.NewFamilyAxeTargetLongestU;
        // 86cajkk7h — a drop-in for THIS locked held scale, so the in-hand size is unchanged from the approved axe).
        // Effective world length ≈ localScale × 1.8 (root) × 1.08 (axe). localScale 0.45 → ~0.49u longest extent
        // — a believable kid-sized hatchet that clears the gameplay-visibility floor (the invisible-sliver soak
        // guard). REASONABLE default — the exact Sponsor F9 dial is a FOLLOW-UP (drives the HeldAxeRig fields).
        public static readonly float HeldAxeLocalScaleUniform = 0.45f;
        // GRIP-POINT SHIFT (86cajkk7h — the STONE axe honors the §6 grip origin, so the shift is ZERO).
        // The retired flint axe had its FBX origin at the weapon MIDPOINT (~49% up the long axis), so a large
        // +0.34235 Y mesh shift was needed to slide the grip down to the hand. The new Sponsor-approved STONE
        // axe (wpn_axe_stone_01) is authored per pipeline §6 — the FBX origin IS the grip (measured at ~24% up
        // the long axis = the lower-haft grip point), blade up +Z (→ Unity +Y after Bake Axis Conversion). So
        // seating the FBX origin at the hand ALREADY grips the lower haft with the head UP TOP (the board-axe
        // read), with NO mesh shift. The WeaponMeshHolder child is still authored (at zero offset) because the
        // single-node FBX collapses its MeshFilter onto the rig-driven root and the rig would stomp a per-weapon
        // TRS on it (#100 BUG-2) — the holder is the rig-untouched home the [B] cycle + belt-sync drive. The F9
        // held target + the soak let the Sponsor micro-dial the grip; 0 is the §6-correct starting point.
        public static readonly float HeldAxeGripShiftY = 0f;
        // HELD-AXE baked defaults consumed by HeldAxeRig + AttachHeroAxeToHand (86ca8rdkp — RE-DERIVED; the
        // OLD chibi-rig values are INVALID on the new skeleton):
        //   - POSITION: a WORLD-space offset from the wrist bone seating the haft in the grip. With no 267×
        //     trap the offset is in plain world units; a small (-Y, +forward) nudge sits the hatchet in the
        //     hand. REASONABLE default; the Sponsor fine-tunes on the re-soak (F9 nudge, world units).
        //   - ROTATION: a hand-relative euler so the haft reads roughly along the forearm/grip. REASONABLE;
        //     the exact dial + the swing-into-head re-check are FOLLOW-UPS (per the ticket OOS).
        // 86ca9zcjn (Sponsor design choice, soak 6bcc1bc): the held axe now FOLLOWS the right arm's natural
        // swing (it rides the RAW hand bone — see HeldAxeRig). FINAL SEAT BAKE (86ca9zcjn): the Sponsor
        // APPROVED the follow-the-arm behavior ("it works perfectly") and dialed the FINAL F9 seat via the
        // AxeNudgeTool. The F9 AxeNudgeTool still drives these fields so he can re-tune.
        // 86caa83wn soak #3 (build 2993c1c, 2026-06-18): walk/run/idle/jump all APPROVED; the Sponsor dialed
        // the FINAL held-axe seat via F9 and asked to bake it as the new shipped default (applies WITHOUT F9):
        // HeldAxeRelEuler = (12,-8,-82). This SUPERSEDES the prior soak-#1 bake (16,2,-82). F9 drives it.
        // 86cabh907 soak ROUND 2 (PR #100, 2026-06-22): the Sponsor re-dialed the held-axe seat via F9 in the
        // shipped build; recovered from Player.log (Danish-locale decimals — "(-186,0f,-168,0f,-84,0f)" =
        // (-186,-168,-84)). Set as the NEW STARTING POINT so the re-soak builds on his dialed placement rather
        // than re-dialing from scratch — NOT a final bake (he re-confirms + may micro-dial in the re-soak; a
        // later pass bakes the locked value). F9 still drives it. SUPERSEDES (12,-8,-82).
        public static readonly Vector3 HeldAxeRelEuler = new Vector3(-186.0f, -168.0f, -84.0f);
        // 86caa83wn soak #4 (cursor-lock OK; the held-axe seat did NOT reproduce his F9-dialed look AFTER
        // PICKUP). ROOT CAUSE: the seat offset was an end-to-end WORLD-frame round-trip — the F9 tool DIALED +
        // DISPLAYED + BAKED a WORLD vector (HeldAxeWorldOffsetFromHand), converted to the rig's hand-local field
        // via Inverse(hand.rotation) AT BAKE-TIME's spawn facing. So the dialed value only described the seat at
        // the FACING he dialed it at; at a different facing (a fresh pickup) the (dial-facing − bake-facing) yaw
        // delta injected into the offset's X/Z (world-Y is yaw-invariant → Y reproduced; X/Z did NOT — exactly
        // his screenshot evidence: baked X=0.0707/Z=-0.0111, fresh pickup X=-0.0600/Z=0.0421). FIX — the offset
        // is now HAND-LOCAL END TO END: the constant below IS the rig's hand-local field (NO Inverse(hand.rotation)
        // conversion at bake), the F9 tool dials + displays it in the SAME hand-local frame, and the rig applies
        // it as hand.rotation * offset every frame. The euler was already hand-relative (facing-invariant) — KEPT.
        // The seat is now IDENTICAL at every facing AND for every acquire path (spawn-in-hand == picked-up),
        // because no hand.rotation ever enters the dial/display/bake. DEFAULT VALUE: the Sponsor's soak-#3
        // APPROVED spawn seat (old world (0.0707,-0.1988,-0.0111)) expressed in the hand-local frame — derived
        // deterministically from the bake-log conversion at the spawn bone rotation (handLocalOffset=
        // (0.0512,0.2009,-0.0407)), so it VISUALLY matches his approved screenshot seat. He does ONE final
        // micro-dial in the FIXED (hand-local) F9 tool to lock it — expected, since the old world dial can't
        // round-trip 1:1 into the new frame. F9 AxeNudgeTool still drives this field.
        // 86caa83wn soak #5 (build 2d90a68, 2026-06-18): the Sponsor LOCKED the FINAL held-axe seat via the F9
        // panel in the now-correct hand-local frame and asked to bake it as the shipped default. SUPERSEDES the
        // derived-from-soak-#3 placeholder (0.0512,0.2009,-0.0407). FINAL hand-local offset below. F9 still drives it.
        // 86cabh907 soak ROUND 2 (PR #100, 2026-06-22): re-dialed via F9 in the shipped build; recovered from
        // Player.log (Danish-locale decimals — "(0,1712f,0,1209f,-0,0007f)" = (0.1712,0.1209,-0.0007), the
        // final logged dial state). Set as the NEW STARTING POINT so the re-soak builds on his dialed placement
        // — NOT a final bake (he re-confirms + may micro-dial; a later pass bakes). F9 still drives it.
        // SUPERSEDES (0.1312,0.1409,0.0593).
        public static readonly Vector3 HeldAxeLocalOffsetFromHand = new Vector3(0.1712f, 0.1209f, -0.0007f);
        // ===== CASTAWAY v2 held-axe seat (86cajx050 AC2 — the MEASURED re-seat; supersedes the 86cajwp23 blind
        // old-rig copy). v2 is a fresh Mixamo Standard A-pose rig; the trace CONFIRMED its mixamorig:RightHand
        // LOCAL FRAME differs materially from the old rig's (old +Y≈(0.05,-1.00,0.06) points ~straight down; v2
        // +Y≈(0.00,-0.88,0.48) tilts ~28° forward), so the old seat did NOT carry 1:1. These values are a
        // MEASURED first-pass derived by CharacterAssetGen.CastawayV2HandAxisTrace: it TRANSFERS the Sponsor-
        // APPROVED old-rig WORLD carry onto v2's hand frame (the axe MESH is identical, so matching its WORLD
        // orientation reproduces the approved look) —
        //   HeldAxeV2RelEuler = eulerAngles( Inv(R_v2hand) * R_oldhand * Euler(HeldAxeRelEuler) )
        //   HeldAxeV2LocalOffsetFromHand = Inv(R_v2hand) * ( R_oldhand * HeldAxeLocalOffsetFromHand )
        // computed with Unity's own Quaternion math (convention-safe) at the two rigs' bind poses. The measured
        // first-pass (20.9,-16.1,74.9)/(0.1819,0.0435,0.0946) got the axe roughly into v2's hand; the Sponsor then
        // F9-DIALED the final seat live in soak-v2-live @ stamp 1ff2f9d and APPROVED the result (axe reads seated in
        // v2's hand) — the values below are those BAKED Sponsor-dialed numbers (per [[verify-soak-builds-or-bake-and-
        // judge]] / [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]). Used ONLY when
        // CharacterAssetGen.UseCastawayV2; the old seat above is byte-unchanged when the toggle is OFF (rollback path).
        public static readonly Vector3 HeldAxeV2RelEuler = new Vector3(20.0f, -16.1f, 74.9f);
        public static readonly Vector3 HeldAxeV2LocalOffsetFromHand = new Vector3(0.0619f, 0.1235f, 0.0454f);
        // ===== CASTAWAY v3 held-axe seat (86cak9kau AC2 — the v3-activation MEASURED re-seat). v3 is a FRESH
        // Mixamo Standard rig (Rodin Smart-Low-poly mesh) whose mixamorig:RightHand LOCAL FRAME differs from BOTH
        // the old rig's AND v2's (trace: v2 +Y≈(0.00,-0.88,0.48), v3 +Y≈(0.17,-0.96,0.24) — v3's hand bone tilts
        // less-forward + more-inward), so neither prior seat carries 1:1. These are a MEASURED first-pass derived
        // by CharacterAssetGen.CastawayV3HandAxisTrace, which TRANSFERS the Sponsor-APPROVED v2 LIVE world carry
        // onto v3's hand frame (the axe MESH is identical, so matching its WORLD orientation reproduces the live-
        // approved look) —
        //   HeldAxeV3RelEuler            = eulerAngles( Inv(R_v3hand) * R_v2hand * Euler(HeldAxeV2RelEuler) )
        //   HeldAxeV3LocalOffsetFromHand = Inv(R_v3hand) * ( R_v2hand * HeldAxeV2LocalOffsetFromHand )
        // computed with Unity's own Quaternion math (convention-safe) at the two rigs' bind poses. This first
        // pass gets the axe into v3's hand (held-belt capture-verified); the Sponsor F9-finalizes the exact grip
        // in the v3 soak, then a follow-up bakes the dialed numbers here ([[verify-soak-builds-or-bake-and-judge]]
        // / [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]). Used when CharacterAssetGen.UseCastawayV3;
        // the v2 + old seats above are byte-unchanged when the toggle is OFF (the rollback path).
        // 86cakkfz9 v3 DIAL-IN BAKE (dial exe stamp d306552, F9 WEAPON NUDGE TOOL): the Sponsor F9-dialed the
        // FINAL v3 held-axe seat in the shipped build and asked to bake it as the shipped default (applies with
        // NO F9). Recovered from Player-prev.log (the dial session; Danish-locale decimals — final resting line
        // "HeldAxeLocalOffsetFromHand=(0,0071f,0,0599f,0,0288f) HeldAxeRelEuler=(-152,5f,-5,9f,108,9f)") and
        // cross-checked against the ticket screenshot table (identical). SUPERSEDES the measured first-pass
        // (17.5,-5.9,92.9)/(0.0271,0.1399,0.0288). SCALE is UNTOUCHED — the axe's held scale comes from the
        // settings HeldScale row, NOT the nudge (ticket: axe scale LOCKED). F9 still drives these fields.
        public static readonly Vector3 HeldAxeV3RelEuler = new Vector3(-152.5f, -5.9f, 108.9f);
        public static readonly Vector3 HeldAxeV3LocalOffsetFromHand = new Vector3(0.0071f, 0.0599f, 0.0288f);
        // ===== CASTAWAY v4 held-axe seat (86catpwc4 dormant integration; ACTIVATED 86catvb6u — MEASURED re-seat).
        // v4 is a FRESH Mixamo Standard rig off OUR Blender export; the trace CONFIRMED its mixamorig:RightHand
        // LOCAL FRAME differs MATERIALLY from v3's — v3 +Y=(0.165,-0.957,0.240) points ~straight down, v4
        // +Y=(0.694,-0.694,0.192) tilts ~45° outward — so v3's seat did NOT carry 1:1 (the earlier v3-verbatim
        // placeholder was genuinely wrong, not a coincidence: diagnose-via-trace overturned the "close enough"
        // assumption). v4 is the LIVE hero, so THIS seat is the one BuildModel bakes (the HeldAxeV4* branch,
        // v4-first); the v3/v2/old seats above are the rollback path.
        //
        // MEASURED first-pass — CharacterAssetGen.CastawayV4HandAxisTrace (run in a live editor 2026-07-19 at the
        // dial-cut) WORLD-TRANSFERS v3's Sponsor-approved live carry onto v4's hand frame (the axe MESH is
        // identical, so matching its WORLD orientation reproduces the approved look):
        //   HeldAxeV4RelEuler            = eulerAngles( Inv(R_v4hand) · R_v3hand · Euler(HeldAxeV3RelEuler) )
        //   HeldAxeV4LocalOffsetFromHand = Inv(R_v4hand) · ( R_v3hand · HeldAxeV3LocalOffsetFromHand )
        // computed with Unity's own Quaternion math (convention-safe) at the two rigs' bind poses. This gets the
        // axe into v4's hand for the dial session's first look; the Sponsor F9-finalizes the grip across all 15
        // held seats (DIAL-STAGED), then a follow-up bakes his dialed numbers here ([[verify-soak-builds-or-bake-
        // and-judge]] / [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]). Used when UseCastawayV4 (the
        // default-ON live hero). SUPERSEDES the v3-verbatim placeholder (-152.5,-5.9,108.9)/(0.0071,0.0599,0.0288).
        public static readonly Vector3 HeldAxeV4RelEuler = new Vector3(-48.9f, -125.0f, -106.3f);
        public static readonly Vector3 HeldAxeV4LocalOffsetFromHand = new Vector3(0.0182f, 0.0415f, 0.0492f);
        // 86ca9zcjn AC2 — OPTIONAL light damp to de-jitter the follow WITHOUT re-locking the swing. Default 0
        // (pure raw-hand follow → the per-step arm-swing is fully visible, the Sponsor's choice). Raise to a
        // SMALL value only if the next soak reads jittery — never enough to re-lock ("damp it, don't lock it").
        public static readonly float HeldAxeFollowDamp = 0f;
        // 86caa83wn soak #2 (2026-06-18) — RUN ARM-LOWER default (the "when i run the axe is no longer in the
        // hand" fix). The earlier axe-side world-Y ceiling clamp DETACHED the axe from the hand during the run
        // arm-swing (it moved the axe-Y but not the hand-X/Z); the Sponsor's chosen approach KEEPS the axe rigidly
        // in the hand and instead LOWERS the right arm while running (CastawayArmPose.runLowerEuler), so the
        // gripped axe — which follows the hand — stays below the head AND in the hand. The run-lower is applied
        // on the rig's raise axis (LOCAL-Z; a negative Z lowers the arm), blended by a smoothed IsRunning weight
        // so WALK/IDLE is byte-unchanged. This is the REASONABLE default the Sponsor dials on the F9 nudge tool
        // (RUN target) WHILE running — what-he-dials-is-what-ships.
        // 86caa83wn soak #3 (build 2993c1c, 2026-06-18): the Sponsor F9-dialed the run arm-lower ("the perfect
        // nudge" screenshot) and asked to bake it as the shipped default: (-10,12,-42). SUPERSEDES (0,0,-22).
        public static readonly Vector3 ArmRunLowerEuler = new Vector3(-10f, 12f, -42f); // soak #3 dialed run carry

        // ===== CASTAWAY v4 FOOT-YAW counter-rotate (86catvb6u — the Sponsor's chosen fix for the v4 pigeon-toe
        // defect). A per-foot OUTWARD yaw offset (CastawayFootYaw, additive-LateUpdate idiom) applied ONLY for v4
        // (0 for v3/v2/old → their feet are byte-unchanged). DEFAULT = the MEASURED v4-vs-v3 bind splay delta
        // (CastawayV4DefectDiag: v4 foot bind yaw ±16.5° vs v3 ±5.0° → ~11° delta) so it starts near-straight;
        // the Sponsor F9-dials the exact angle (FOOT-YAW nudge target), then a follow-up bakes his dialed value
        // here (verify-soak-builds-or-bake-and-judge / sponsor-prefers-direct-tweak-tools). Signed: + = outward.
        public const float CastawayV4FootYawDeg = 11f;

        /// <summary>
        /// Author the player + orbit camera + flat ground + saved NavMesh into the CURRENT open
        /// scene. The caller (BootstrapProject.BuildBootScene) has already created the scene with
        /// a directional light + the BootHud/BootScreenshot object; this adds the movement+camera
        /// layer and REPLACES the static boot camera with the orbit camera. Returns nothing; logs
        /// each load-bearing step so a headless run is auditable.
        /// </summary>
        public static void Author(GameObject existingBootCamera)
        {
            EnsureDirs();

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0)
            {
                Debug.LogError("[MovementCameraScene] 'Ground' layer is not defined in TagManager — " +
                               "the click-raycast + NavMesh layer mask depend on it");
                groundLayer = 0; // fall back to Default so the bake still produces a surface
            }

            GameObject ground = BuildFlatGround(groundLayer);
            ClickMarker markerPrefab = BuildClickMarkerPrefab();
            GameObject player = BuildPlayer(markerPrefab, groundLayer);
            GameObject camGo = BuildOrbitCamera(player, existingBootCamera);

            // WASD keyboard locomotion (ticket 86ca9yq2x) — REPLACES click-to-move as the player's
            // locomotion (Sponsor-directed pivot). Wired AFTER the orbit camera (it needs the camera
            // transform for camera-relative movement) so "W = the way the camera faces". It drives the
            // EXISTING NavMeshAgent's velocity (keeps terrain/NavMesh grounding + the float fix intact) and
            // disables ClickToMove's click handling on Start. CastawayCharacter already reads agent.velocity
            // for the Idle<->Walk blend + facing, so the anim + facing follow with no extra wiring.
            BuildWasdMovement(player, camGo);

            // 86caa4bqp: the in-game tweakable SETTINGS PANEL (UI Toolkit). Wired AFTER the orbit camera +
            // WASD so the live targets the settings bind to (zoom/pitch range → OrbitCamera; walk/run speed
            // → WasdMovement) exist + serialize as references. A dedicated GameObject hosts the UIDocument +
            // SettingsPanel; the PanelSettings + UXML/USS assets are created/loaded here so the panel ships
            // (the component-in-source-but-not-in-scene + asset-not-serialized traps). Esc toggles it in play.
            BuildSettingsPanel(camGo, player);

            // 86camz9uz (crafting-redesign ①): the PLACE-TO-BUILD crafting table + recipe MENU + material-cost
            // craft seam — RETIRES the U2-2 auto-craft stump (CraftSpot). The table ships INVISIBLE (no pre-
            // placed marker — spec §2); the castaway gathers wood+stone, PLACES it (ghost + confirm), then
            // walks up to open a recipe MENU and crafts WOOD-tier tools by SPENDING materials. Authored editor-
            // time so the (hidden) table + ghost + the menu UIDocument + the placement's Inventory/player refs
            // SERIALIZE into Boot.unity (editor-vs-runtime trap). CraftingTableSceneTests guards the presence.
            BuildCraftingTable(player, groundLayer);

            // U2-3 (86ca8bdd8): the choppable tree — the "do work in the world" beat. A Zone-D low-poly
            // tree the castaway click-moves to; reaching it WITH the axe (U2-2) chops it for wood. Authored
            // editor-time so the tree mesh + ChopTree's Inventory/player/visual refs SERIALIZE into
            // Boot.unity (editor-vs-runtime trap). Built AFTER the craft spot (so the loop reads
            // spawn -> craft axe -> chop tree) and BEFORE the NavMesh bake (the tree has no collider, so
            // it neither blocks the ground raycast nor the bake — the player walks up to it).
            BuildChopTree(player, groundLayer);

            // 86caa5zz3 (E-LOOT 86caf7a6q): a wired BERRY BUSH near the loop centre — the food source for the
            // merged hunger loop. The castaway walks up and presses E to LOOT berries into the inventory (the
            // universal loot verb — DECISIONS 2026-06-27; NOT proximity-auto any more). The berries regrow
            // after a tweakable delay. Authored editor-time so the bush mesh + berries visual + BerryBush's
            // Inventory/player refs SERIALIZE into Boot.unity (editor-vs-runtime trap). A RELIABLE,
            // fixed-position berry bush (vs the random scatter ones) so the PlayMode/shipped-build capture has
            // a deterministic foraging target. No collider — the player walks up to loot.
            BuildBerryBush(player, groundLayer);

            // 86caa96rd (E-LOOT 86caf7a6q): a wired fallen STICK near the loop centre — the LOW-yield wood
            // source (1 wood per pickup, far less than chopping a tree). The castaway walks up and presses E
            // to LOOT it (the universal loot verb): 1 wood into the inventory, the stick consumed. A RELIABLE,
            // fixed-position stick (vs the random scatter ones) so the PlayMode/shipped-build capture has a
            // deterministic loot target. Authored editor-time so the stick mesh + StickProp's Inventory ref
            // SERIALIZE into Boot.unity (editor-vs-runtime trap). No collider — the player walks up to loot.
            // Built BEFORE the looter so the looter discovers it (it discovers IPickables at runtime anyway).
            BuildWiredStick(player, groundLayer);

            // 86caa4c96 (E-LOOT 86caf7a6q): a wired small STONE near the loop centre — the small-stone gather
            // (1 stone per pickup; bigger boulders are the FUTURE pickaxe-mining target, OOS). The castaway
            // walks up and presses E to LOOT it: 1 stone into the inventory, then the spot RESPAWNS on the
            // shared StoneRespawner window (AC3). A RELIABLE, fixed-position stone (vs the random scatter ones)
            // so the PlayMode/shipped-build capture has a deterministic loot target. Authored editor-time so
            // the stone mesh (a CHILD visual StoneProp toggles on loot/respawn) + StoneProp's Inventory/
            // respawner refs SERIALIZE into Boot.unity (editor-vs-runtime trap). No collider — the player walks
            // up to loot. Built BEFORE the looter so the looter discovers it (it discovers IPickables anyway).
            BuildWiredStone(player, groundLayer);

            // 86cakkmr0 (I-2 of the iron chain): iron-ore rock NODES + the active-left-click MINE verb + the
            // OrePileSpawner. The castaway finds ore nodes, mines them WITH A PICKAXE SELECTED by left-clicking
            // (the chop-verb sibling), breaks them for a lootable iron-ore pile, then the node regrows. Built
            // BEFORE BuildPickableLooter so the OrePileSpawner exists when the looter back-wires it (mirrors the
            // LogPileSpawner back-wire). Authored editor-time so the node pool + MineOre/OrePileSpawner refs
            // SERIALIZE into Boot.unity (editor-vs-runtime trap). Collider-free — no NavMesh/raycast impact.
            BuildOreNodes(player, groundLayer);

            // 86cakkmr0 (I-2 soak-enabler): a wired stone-PICKAXE pickup so the mine loop is live-triggerable in the
            // soak (the mine gate needs a pickaxe SELECTED). The axe/spear pickup idiom; the real pickaxe mesh
            // (I-1/#283). Placed CLEAR of spawn (> pickupRadius) so it can't auto-grab belt slot 0 (the PR #224 class).
            BuildPickaxePickup(player);

            // 86camz9v7 (crafting-redesign ②): LARGER BOULDERS — the VOLUME stone source. With a WOOD pickaxe
            // selected the castaway mines stone from boulders (the MineOre sibling), breaks them for a lootable
            // stone pile, then the boulder regrows. A DISCRETE mineable pool authored editor-time (distinct from
            // the decorative scatter rocks + the ore nodes). Built BEFORE BuildPickableLooter so the StonePileSpawner
            // exists when the looter back-wires it (mirrors the OrePileSpawner back-wire). Collider-free.
            BuildBoulders(player, groundLayer);

            // 86caf7a6q: the E-LOOT interactor — the PLAYER side of the shared E-loot surface. Pressing E
            // loots the nearest in-range IPickable (the berry bush above; sticks 86caa96rd + stones
            // 86caa4c96 build on the SAME surface) into the inventory. Wired AFTER the bush so the loop reads
            // spawn -> ... -> press E to forage. Authored editor-time onto the PLAYER (so it ships in
            // Boot.unity — the component-in-source-but-not-in-scene trap); its Inventory + player refs are
            // serialized. The looter discovers IPickables at runtime (Awake), so this can run before/after any
            // pickable author. PickableLooterSceneTests guards the serialized presence + wiring.
            BuildPickableLooter(player);

            // 86cakkmr0 (I-2): the shipped-build MINE capture gate — wired AFTER BuildPickableLooter + the pickaxe
            // pickup so all its deps (looter + pickaxe pickup + MineOre) exist. Inert unless launched with -verifyMine.
            WireMineVerifyCapture(player);

            // 86camz9v7 (②): the shipped-build BOULDER capture gate — wired AFTER BuildPickableLooter + BuildBoulders
            // so its deps (looter + MineBoulder) exist. Inert unless launched with -verifyBoulder.
            WireBoulderVerifyCapture(player);

            // 86catr49m: the shipped-build PLACEMENT-obstacle capture gate — authored AFTER BuildCraftingTable
            // (the CraftingTablePlacement driver) + BuildBoulders (so a registered boulder exists to prove
            // RED-over-boulder). Inert unless launched with -verifyPlacement. #302 shipped this harness INERT
            // (never scene-authored → the -verifyPlacement verb NO-OP'd + would HANG the capture exe); this wire
            // + a Boot re-bake is exactly the fix (unity-conventions §CI: an UNWIRED -verifyX verb hangs the exe).
            WirePlacementVerifyCapture(player);

            // 86caamkv7: a wired FRESHWATER POND inland near the loop centre — the thirst source for the merged
            // survival loop. The castaway walks up (no tool) and DRINKS FROM HAND — a small per-scoop restore,
            // repeatable, NOT an inventory item (distinct from the berry harvest). The pond water rides the
            // SAME LowPolyWater shader as the sea via a sibling material with Uma's freshwater deltas (bluer/
            // brighter/calm/tight-foam) so it reads as DIFFERENT, drinkable water. Authored editor-time so the
            // pond mesh + bank + FreshwaterPond's ThirstNeed/player refs SERIALIZE into Boot.unity (editor-vs-
            // runtime trap). A DETERMINISTIC scene-author ADD OUTSIDE the seeded LowPolyZoneGen stream — it
            // provably cannot perturb the seed-42 island/scatter/NavMesh (AC2a). No collider — the player walks
            // up to drink; built BEFORE the bake (collider-free, no raycast/NavMesh impact).
            BuildFreshwaterPond(player, groundLayer);

            // U2-4 (86ca8bdep): the campfire — the loop's CLOSE. A human-scale fire pit the castaway
            // click-moves to; arriving WITH WOOD (from the chop, U2-3) builds + lights it, and the lit
            // fire RESTORES warmth (U2-1's AddWarmth seam) while the castaway stands by it — warmth decays
            // -> craft axe -> chop tree -> build campfire -> warm again. Authored editor-time so the fire
            // mesh + warm Light + Campfire/CampfirePlacement refs SERIALIZE into Boot.unity (editor-vs-runtime
            // trap). Built AFTER the tree (so the loop reads spawn -> craft -> chop -> build fire) and BEFORE
            // the NavMesh bake (the fire-pit has no collider — the player walks up to it).
            BuildCampfire(player, groundLayer);

            // 86cakkmvc (I-3 of the iron chain): the FORGE/FURNACE — a NEW buildable DISTINCT from the campfire
            // (Sponsor Q3). A stone furnace the castaway builds from wood + stone (the ForgePlacement wood+stone
            // gate), then SMELTS iron-ore into iron ingots over a timer (the work-led earn). Authored editor-time
            // so the furnace mesh + glow/Light + Forge/ForgePlacement refs SERIALIZE into Boot.unity (editor-vs-
            // runtime trap). Built AFTER the campfire (the survival arc reads shipwreck -> ... -> fire -> FURNACE ->
            // iron) and BEFORE the NavMesh bake (collider-free — the player walks up to it).
            BuildForge(player, groundLayer);

            // 86catpvpa: the BUILD MENU (C) — the SINGLE build entry point fronting the ① free-cursor ghost-
            // placement flow for ALL placeable structures (crafting table + forge today; ⑤ appends the
            // campfire once it converts to the ghost flow). Authored AFTER BuildCraftingTable + BuildForge so
            // both placement drivers (the IBuildPlaceable rows) exist to register. Retires the interim table-C
            // / forge-V direct build keys — the menu is the ONLY build entry point now (the Sponsor's
            // mid-soak confusion fix). The menu's UIDocument + PanelSettings + placeableSources refs SERIALIZE
            // into Boot.unity (editor-vs-runtime trap). Also wires the -verifyBuildMenu shipped-build capture
            // (inert unless launched with that verb). BuildMenuSceneTests guards the serialized presence.
            BuildBuildMenu(player);

            // 86caa4bya: the INVENTORY pack (Tab) + BELT hotbar UI (UI Toolkit) + a pickable world axe.
            // The InventoryUI's UIDocument + UXML/USS + the Inventory reference SERIALIZE into Boot.unity
            // (editor-vs-runtime trap). The pickable axe is the AC3 PoC pickup (auto-places in belt slot 1).
            // Built collider-free (no NavMesh/raycast impact) so order vs the bake is flexible; placed here
            // so the loop reads spawn -> pick up axe -> craft/chop/build with the belt+pack in play.
            BuildInventoryUI(player);
            BuildAxePickup(player, groundLayer);

            // Combat POC (86cah7xxp): the FIRST combat build — player HP (Health) + needs-gated regen
            // (HealthRegen, AC3) + tiered death (DeathHandler, AC2) on the player root, the left-click melee
            // ATTACK (MeleeAttack, AC5) that swings the selected weapon (axe/spear) at the nearest in-reach
            // enemy, and ONE damageable snake (SnakeEnemy, AC7 — the shared Health surface + a pierce-weak
            // profile). Authored editor-time so the Health/regen/death/attack + snake SERIALIZE into Boot.unity
            // (editor-vs-runtime trap). Wires the SurvivalHud's HP bar (AC9) + the SettingsPanel's per-tier
            // combat rows (AC8b). Built AFTER the inventory (the attack reads the selected belt weapon) and
            // BEFORE the bake (the snake is a collider-free marker — no NavMesh/raycast impact).
            BuildCombat(player, groundLayer);

            // M-U3-SCENE-4 (86ca8feuf): shipwreck debris at the landing. A MODEST washed-ashore scatter
            // — a few weathered planks + a half-buried crate + a barrel — on the beach just SEAWARD of the
            // spawn, narrating "the castaway crawled out of that sea" (Sponsor: NARRATE; Uma §3 beat 4).
            // Diegetic set-dressing ONLY: NO collider on any piece, so it never blocks the click-move
            // ground raycast or the NavMesh bake (built BEFORE the bake; collider-free pieces don't
            // contribute to the PhysicsColliders bake). Authored editor-time so the meshes + inline
            // materials SERIALIZE into Boot.unity (editor-vs-runtime "legs-up" trap); BeachDebrisSceneTests
            // guards its serialized presence + the no-collider contract.
            BuildBeachDebris(groundLayer);

            // Bake AFTER the walkable ground exists, then SAVE the data as an asset so it ships.
            BakeAndSaveNavMesh(ground, groundLayer);

            // Wire the verification-only shipped-build movement capture onto the Boot object so the
            // testing bar's shipped-build gate can prove click-move in the BUILT exe (-verifyMove).
            WireMovementVerifyCapture(player);

            // Wire the verification-only shipped-build SEA capture (drew/beach-water-scene; Uma §4 task F).
            // The default orbit framing looks INLAND; this drives an orbit-to-seaward yaw in the BUILT exe
            // so the beach ocean is captured filling the frame (the inland -captureGate frames can't judge
            // it). Inert unless launched with -verifySea. Sibling of the movement/craft/chop/loop captures.
            WireSeaVerifyCapture();

            // Wire the BIG ROUND ISLAND verify capture (86ca9a7qn) — a gameplay over-shoulder frame + an
            // overhead/high-orbit frame proving the round island + water-all-sides + distant mountain
            // islands + dense tall forest. Inert unless launched with -verifyIsland.
            WireIslandVerifyCapture();

            // Wire the verification-only shipped-build ROCK capture (86ca8m5zu — the boulder soak-fix). The
            // default orbit frames the SPAWN; the rocks live as outcrops in the FIELD band, so this drives a
            // multi-angle orbit onto the outcrop centroid in the BUILT exe so the boulders are judged from
            // gameplay distance (not a hero close-up). Inert unless launched with -verifyRock.
            WireRockVerifyCapture();

            // Wire the verification-only shipped-build FRESHWATER POND capture (86caamkv7 — the THIRST
            // drink-from-hand source). The generic -captureGate is a frame-SANITY check that NEVER frames
            // the pond; this drives a gameplay-pitch over-shoulder orbit onto the pond at (7,0,-3) in the
            // BUILT exe so the pond is judged fresh-blue + grounded from a SHIPPED frame (the gameplay-cam
            // grounding gate, memory verify-grounding-soaks-by-gameplay-cam-visual). Inert unless launched
            // with -verifyPond. Sibling of WireRockVerifyCapture / WireSeaVerifyCapture.
            WireFreshwaterPondVerifyCapture();

            // Wire the verification-only shipped-build SKY-FACING capture (86cabc743 — the SUN-DISK POC,
            // Erik low-poly-sky research). The gameplay OrbitCamera clamps pitch to <=70 and frames the
            // player from above; it cannot tilt up to the Sun (elevation ~48deg), so this parks a dedicated
            // sky camera (Skybox clear + Zone-D post) aimed at the live Sun direction (sun_disk) + up into
            // the cloud band (cloud-vs-sky contrast). Inert unless launched with -verifySky. Sibling of
            // WireFreshwaterPondVerifyCapture / WireWorldLookVerifyCapture.
            WireSkyVerifyCapture();

            Debug.Log("[MovementCameraScene] authored player + orbit camera + flat ground + NavMesh");
        }

        private static void EnsureDirs()
        {
            foreach (var d in new[] { NavMeshDir, PrefabDir, SettingsDir })
                Directory.CreateDirectory(Path.GetFullPath(d));
            AssetDatabase.Refresh();
        }

        // Seaward edge of the flat test ground. The seaward slab spanned Z[-30..+30] at Y=0; the trim
        // to -10 (drew/beach-water) stopped it overhanging the water's near band. Kept at -10 — the
        // ROOT-CAUSE of the invisible sea was NOT this slab occluding the water (the magenta-diff +
        // -seaWaterOnly probe proved the sea rendered ZERO px even with BOTH grounds hidden): it was the
        // water mesh's INVERTED triangle winding (faces pointed DOWN -> Cull Back hid the sea from the
        // above-camera). Fixed in LowPolyZoneGen.BuildWaterEdge. This slab edge stays as the prior trim.
        private const float SeawardGroundZ = -10f;

        // A flat subdivided plane on the Ground layer with a MeshCollider, so the NavMesh bake
        // (PhysicsColliders collection) AND the click-to-move ground raycast both hit it.
        // Subdivided (not a single quad) so the baked NavMesh has clean geometry.
        //
        // NON-RENDERING (drew/ocean-beach-soakfix2, soak #40 stamp 31ce95c). The Sponsor saw a "gray slab
        // on the beach" breaking the sand read. Diagnostic trace (centerline Y compare, MovementCameraScene
        // vs LowPolyZoneGen height fields): this flat Y=0 placeholder slab pokes ABOVE the SANDY Zone-D
        // terrain across the seaward foreshore band (Z ~ -10..+3) — exactly where the beach DIPS toward the
        // sea (terrain Y goes -0.53 -> -0.02, all below this slab's Y=0). Its muted moss-grey (0.42,0.46,0.40)
        // top was therefore the topmost surface on the beach -> the grey slab. (Inland of ~Z+4 the sand rises
        // above Y=0 and already hid it, which is why it only showed ON the beach.) The slab is a pure dev
        // placeholder ("U5 will replace the environment surface; not art") whose ONLY load-bearing role is
        // its COLLIDER (NavMesh bake + click-raycast); the Zone-D terrain is the real visible ground. Fix:
        // keep the collider (NavMesh + click-move unchanged — the player walks the SAME baked surface as
        // before, so no float/path regression), DISABLE the renderer so the sandy terrain is the only thing
        // drawn on the beach. The MeshRenderer is kept-but-disabled (not removed) so .bounds still resolves
        // for WaterSceneTests.Ocean_NotOccludedByFlatGround_SeawardSlabTrimmed.
        private static GameObject BuildFlatGround(int groundLayer)
        {
            var go = new GameObject("TestGround");
            go.layer = groundLayer;
            go.transform.position = Vector3.zero;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            const int seg = 20;
            float sizeX = GroundHalf * 2f;
            // Asymmetric Z span: seaward edge trimmed to SeawardGroundZ; inland edge stays at +GroundHalf.
            float minZ = SeawardGroundZ, maxZ = GroundHalf;
            var verts = new Vector3[(seg + 1) * (seg + 1)];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= seg; z++)
                for (int x = 0; x <= seg; x++)
                {
                    int i = z * (seg + 1) + x;
                    float fx = (float)x / seg, fz = (float)z / seg;
                    verts[i] = new Vector3((fx - 0.5f) * sizeX, 0f, Mathf.Lerp(minZ, maxZ, fz));
                    uvs[i] = new Vector2(fx * seg, fz * seg);
                }
            var tris = new int[seg * seg * 6];
            int ti = 0;
            for (int z = 0; z < seg; z++)
                for (int x = 0; x < seg; x++)
                {
                    int i = z * (seg + 1) + x;
                    tris[ti++] = i; tris[ti++] = i + seg + 1; tris[ti++] = i + 1;
                    tris[ti++] = i + 1; tris[ti++] = i + seg + 1; tris[ti++] = i + seg + 2;
                }
            var mesh = new Mesh { name = "TestGround_mesh" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            // A simple, build-safe URP/Lit material kept on the (disabled) renderer so it never ships pink
            // if anything re-enables it. U5 will replace the environment surface; this is a neutral
            // placeholder, not art — and now it does NOT draw (see the NON-RENDERING note above).
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.42f, 0.46f, 0.40f)); // muted moss-grey
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(mat, SettingsDir + "/TestGroundMat.mat");
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }

            // The slab is a COLLISION/NAVMESH proxy only — the sandy Zone-D terrain is the visible ground.
            // Disable rendering so the grey placeholder slab no longer pokes through the beach (soak #40).
            mr.enabled = false;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            return go;
        }

        // A flat ring quad on the ground that the ClickToMove spawns at the move destination.
        // Built as a prefab asset so ClickToMove's serialized markerPrefab reference ships.
        private static ClickMarker BuildClickMarkerPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "ClickMarker";
            Object.DestroyImmediate(root.GetComponent<Collider>()); // marker must not block raycasts
            // Lie flat on the ground, ~0.8u ring.
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            root.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.5f, 0.9f)); // warm marker
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                AssetDatabase.CreateAsset(mat, SettingsDir + "/ClickMarkerMat.mat");
                root.GetComponent<MeshRenderer>().sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(unlit);
            }

            root.AddComponent<ClickMarker>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MarkerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<ClickMarker>() : null;
        }

        // Legacy world position (8,6) on the flat test ground — formerly the auto-craft stump spot (retired
        // with CraftSpot in 86camz9uz ①). KEPT because it is still a shared WAYPOINT the Chop/Campfire verify
        // captures route the player through + an organic-scatter landmark-avoidance point for the ore nodes.
        // The crafting table is now INVISIBLE until the player places it (no fixed spawn spot), so this is no
        // longer a craft location — just a stable named landmark. Do not repurpose without checking those uses.
        public static readonly Vector3 CraftSpotPosition = new Vector3(8f, 0f, 6f);

        public const string CraftingTableObjectName = "CraftingTable";
        public const string CraftingTableGhostObjectName = "CraftingTableGhost";
        public const string CraftingMenuObjectName = "CraftingMenuUI";
        public const string CraftingTablePlacementObjectName = "CraftingTablePlacement";
        public const string CraftingMenuPanelSettingsAssetPath = SettingsDir + "/CraftingMenuPanelSettings.asset";
        public const string CraftingTableMatPath = SettingsDir + "/CraftingTableMat.mat";
        public const string CraftingGhostMatPath = SettingsDir + "/CraftingGhostMat.mat";

        // 86catpvpa — the BUILD MENU (C) objects.
        public const string BuildMenuObjectName = "BuildMenuUI";
        public const string BuildMenuPanelSettingsAssetPath = SettingsDir + "/BuildMenuPanelSettings.asset";

        // The crafting TABLE + recipe MENU + place-to-build flow (86camz9uz ① — replaces CraftSpot). Authors,
        // all editor-time so they SERIALIZE into Boot.unity (editor-vs-runtime trap): (a) the REAL table (a
        // flat-work-surface-on-legs low-poly mesh — Bar 4 anchor), renderers DISABLED = invisible until placed
        // (spec §2); (b) a translucent GHOST (same mesh) hidden until placement; (c) the recipe-menu UIDocument
        // (modal, its own PanelSettings on the shared runtime theme); (d) the CraftingTablePlacement driver
        // wiring them + the Inventory/player refs. Collider-free (cubes' box colliders stripped) so nothing
        // blocks the ground raycast / NavMesh. CraftingTableSceneTests guards the serialized presence + wiring.
        private static void BuildCraftingTable(GameObject player, int groundLayer)
        {
            var inv = Object.FindObjectOfType<Inventory>();
            if (inv == null)
                Debug.LogError("[MovementCameraScene] no Inventory to wire the crafting table to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            // (a) the REAL table — parked at the legacy landmark until placed; invisible (renderers off).
            var tableGo = new GameObject(CraftingTableObjectName);
            tableGo.transform.position = CraftSpotPosition;
            var tableVisual = MakeCraftingTableMesh("TableVisual", EnsureCraftingTableMat());
            tableVisual.SetParent(tableGo.transform, false);
            var table = tableGo.AddComponent<CraftingTable>();
            table.visual = tableVisual;
            foreach (var r in tableVisual.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false; // invisible-until-placed (serialized hidden; Awake re-asserts)

            // The no-build zone the table self-registers ONCE PLACED (#302 seam, ③ reconciliation) so a later
            // forge/table placement ghost reads RED over it. Authored DISABLED — CraftingTable.Reveal enables it.
            var tableObstacle = tableGo.AddComponent<PlacementObstacle>();
            tableObstacle.footprintRadius = 0.6f; // ~half the 1.1×0.8 table top
            tableObstacle.enabled = false;
            table.placementObstacle = tableObstacle;

            // (b) the placement GHOST — translucent, hidden until the player enters placement mode.
            var ghostGo = new GameObject(CraftingTableGhostObjectName);
            ghostGo.transform.position = CraftSpotPosition;
            var ghostVisual = MakeCraftingTableMesh("GhostVisual", EnsureCraftingGhostMat());
            ghostVisual.SetParent(ghostGo.transform, false);
            foreach (var r in ghostVisual.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false;

            // (c) the recipe MENU (UI Toolkit, modal). Own PanelSettings on the shared resolving runtime theme
            // (a base-less theme HANGS the windowed exe at 0 frames — EnsureRuntimeTheme is the proven fix).
            var menuGo = new GameObject(CraftingMenuObjectName);
            var doc = menuGo.AddComponent<UIDocument>();
            doc.panelSettings = EnsureCraftingMenuPanelSettings();
            doc.sortingOrder = 95f; // above the inventory belt (90), below the dev settings panel (100)
            var menu = menuGo.AddComponent<CraftingMenuUI>();
            menu.document = doc;
            menu.inventory = inv;
            menu.table = table;
            menu.player = player != null ? player.transform : null;

            // (d) the PLACEMENT driver.
            var placeGo = new GameObject(CraftingTablePlacementObjectName);
            var placement = placeGo.AddComponent<CraftingTablePlacement>();
            placement.inventory = inv;
            placement.player = player != null ? player.transform : null;
            placement.table = table;
            placement.ghost = ghostGo.transform;
            placement.menu = menu;
            placement.groundMask = 1 << groundLayer;

            EditorUtility.SetDirty(tableGo); EditorUtility.SetDirty(ghostGo);
            EditorUtility.SetDirty(menuGo); EditorUtility.SetDirty(placeGo);
            Debug.Log("[MovementCameraScene] authored crafting table (invisible-until-placed) + ghost + recipe " +
                      "menu (sortingOrder 95) + placement (inventory wired: " + (inv != null) + ")");
        }

        // Build the low-poly crafting-table mesh — a flat work SURFACE on 4 LEGS standing ON the ground (the
        // Bar 4 anchor: "a crafting table is a flat work surface on legs standing on the ground"). Faceted
        // cube parts (box colliders stripped). Shared by the real table + the translucent ghost (same mesh).
        private static Transform MakeCraftingTableMesh(string name, Material mat)
        {
            var root = new GameObject(name);
            AddTablePart(root.transform, "Top",   new Vector3(1.10f, 0.14f, 0.80f), new Vector3(0f, 0.78f, 0f), mat);
            AddTablePart(root.transform, "LegNE", new Vector3(0.14f, 0.72f, 0.14f), new Vector3( 0.46f, 0.36f,  0.32f), mat);
            AddTablePart(root.transform, "LegNW", new Vector3(0.14f, 0.72f, 0.14f), new Vector3(-0.46f, 0.36f,  0.32f), mat);
            AddTablePart(root.transform, "LegSE", new Vector3(0.14f, 0.72f, 0.14f), new Vector3( 0.46f, 0.36f, -0.32f), mat);
            AddTablePart(root.transform, "LegSW", new Vector3(0.14f, 0.72f, 0.14f), new Vector3(-0.46f, 0.36f, -0.32f), mat);
            return root.transform;
        }

        private static void AddTablePart(Transform parent, string name, Vector3 scale, Vector3 localPos, Material mat)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.DestroyImmediate(cube.GetComponent<Collider>()); // no NavMesh / raycast block
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = scale;
            cube.transform.localPosition = localPos;
            var mr = cube.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        private static Material EnsureCraftingTableMat()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(CraftingTableMatPath);
            if (existing != null) return existing;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.48f, 0.34f, 0.21f)); // warm timber
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, CraftingTableMatPath);
            EnsureShaderAlwaysIncluded(lit);
            return mat;
        }

        // Translucent URP/Unlit for the placement ghost (the standard headless transparent recipe). The
        // placement tints it green/red per-frame via a MaterialPropertyBlock (_BaseColor) — the hue channel
        // of the dual-channel valid/invalid cue (the WORD cue is the colour-independent channel).
        private static Material EnsureCraftingGhostMat()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(CraftingGhostMatPath);
            if (existing != null) return existing;
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(unlit);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.35f, 0.85f, 0.40f, 0.45f));
            AssetDatabase.CreateAsset(mat, CraftingGhostMatPath);
            EnsureShaderAlwaysIncluded(unlit);
            return mat;
        }

        // Own PanelSettings for the crafting-menu UIDocument (mirrors EnsureInventoryPanelSettings) — the
        // shared EnsureRuntimeTheme resolves a USABLE runtime theme (a base-less theme hangs the exe at 0 frames).
        private static PanelSettings EnsureCraftingMenuPanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(CraftingMenuPanelSettingsAssetPath);
            if (existing != null) return existing;
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.match = 0.5f;
            ps.themeStyleSheet = EnsureRuntimeTheme();
            AssetDatabase.CreateAsset(ps, CraftingMenuPanelSettingsAssetPath);
            AssetDatabase.SaveAssets();
            return ps;
        }

        // 86catpvpa — the BUILD MENU (C): the SINGLE build entry point fronting the ① ghost-placement flow for
        // ALL placeable structures. Authors, all editor-time so they SERIALIZE into Boot.unity (editor-vs-
        // runtime trap): (a) the menu UIDocument (modal, its own PanelSettings on the shared resolving runtime
        // theme, sortingOrder 96 — above the recipe menu at 95, below the dev settings panel at 100); (b) the
        // BuildMenuUI wired with the placeableSources = [CraftingTablePlacement, ForgePlacement] (each an
        // IBuildPlaceable row). Retires the interim table-C / forge-V direct build keys (removed from those
        // components). Also wires the -verifyBuildMenu shipped-build capture onto Boot. MUST run AFTER
        // BuildCraftingTable + BuildForge (the placement drivers must exist to register).
        private static void BuildBuildMenu(GameObject player)
        {
            var tablePlacement = Object.FindObjectOfType<CraftingTablePlacement>();
            var forgePlacement = Object.FindObjectOfType<ForgePlacement>();
            var campfirePlacement = Object.FindObjectOfType<CampfirePlacement>();
            if (tablePlacement == null)
                Debug.LogError("[MovementCameraScene] BuildBuildMenu found no CraftingTablePlacement — " +
                               "BuildCraftingTable must run before BuildBuildMenu (the table build-menu row).");
            if (forgePlacement == null)
                Debug.LogError("[MovementCameraScene] BuildBuildMenu found no ForgePlacement — " +
                               "BuildForge must run before BuildBuildMenu (the forge build-menu row).");
            if (campfirePlacement == null)
                Debug.LogError("[MovementCameraScene] BuildBuildMenu found no CampfirePlacement — " +
                               "BuildCampfire must run before BuildBuildMenu (the campfire build-menu row, ⑤).");

            var menuGo = new GameObject(BuildMenuObjectName);
            var doc = menuGo.AddComponent<UIDocument>();
            doc.panelSettings = EnsureBuildMenuPanelSettings();
            doc.sortingOrder = 96f; // above the recipe menu (95), below the dev settings panel (100)
            var menu = menuGo.AddComponent<BuildMenuUI>();
            menu.document = doc;
            // The editor-authored rows: crafting table + forge + campfire (⑤ — the campfire is now a build-menu row,
            // the ONE build entry point; no parallel flow). Each is an IBuildPlaceable.
            var sources = new System.Collections.Generic.List<MonoBehaviour>(3);
            if (tablePlacement != null) sources.Add(tablePlacement);
            if (forgePlacement != null) sources.Add(forgePlacement);
            if (campfirePlacement != null) sources.Add(campfirePlacement);
            menu.placeableSources = sources.ToArray();

            EditorUtility.SetDirty(menuGo);
            Debug.Log("[MovementCameraScene] authored build menu (C, sortingOrder 96) with " +
                      sources.Count + " placeable rows (table wired: " + (tablePlacement != null) +
                      ", forge wired: " + (forgePlacement != null) +
                      ", campfire wired: " + (campfirePlacement != null) + ")");

            WireBuildMenuVerifyCapture(menu, tablePlacement);
        }

        // Own PanelSettings for the build-menu UIDocument (mirrors EnsureCraftingMenuPanelSettings) — the
        // shared EnsureRuntimeTheme resolves a USABLE runtime theme (a base-less theme hangs the exe at 0 frames).
        private static PanelSettings EnsureBuildMenuPanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(BuildMenuPanelSettingsAssetPath);
            if (existing != null) return existing;
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.match = 0.5f;
            ps.themeStyleSheet = EnsureRuntimeTheme();
            AssetDatabase.CreateAsset(ps, BuildMenuPanelSettingsAssetPath);
            AssetDatabase.SaveAssets();
            return ps;
        }

        // 86catpvpa: the shipped-build BUILD-MENU capture gate (-verifyBuildMenu), attached to Boot so it SHIPS
        // in Boot.unity — an UNWIRED -verifyX verb NO-OPs the capture AND HANGS the exe (the #302 inert-harness
        // lesson). Fails LOUD at bootstrap on a dropped dep rather than letting Start's find-fallback mask it.
        private static void WireBuildMenuVerifyCapture(BuildMenuUI menu, CraftingTablePlacement tablePlacement)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host BuildMenuVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<BuildMenuVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<BuildMenuVerifyCapture>();
            cap.menu = menu;
            cap.tablePlacement = tablePlacement;
            cap.inventory = Object.FindObjectOfType<Inventory>();
            if (cap.menu == null)
                Debug.LogError("[MovementCameraScene] BuildMenuVerifyCapture.menu wiring is null — " +
                               "BuildBuildMenu must author the BuildMenuUI before WireBuildMenuVerifyCapture");
            if (cap.tablePlacement == null)
                Debug.LogError("[MovementCameraScene] BuildMenuVerifyCapture.tablePlacement wiring is null — " +
                               "BuildCraftingTable must author the CraftingTablePlacement before this gate");
            if (cap.inventory == null)
                Debug.LogError("[MovementCameraScene] BuildMenuVerifyCapture.inventory wiring is null — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            EditorUtility.SetDirty(bootGo);
        }

        // Attach the IN-HOUSE hero axe (ticket 86cabh907 — Route A weapon SET) to the chibi's RIGHT HAND bone
        // so the castaway HOLDS the axe. The procedural HeroAxeMesh wedge AND the earlier CC-BY sourced
        // hatchet are both RETIRED. The imported FBX (WeaponPackAssetGen.HeroAxeFbxPath — faceted flat-shaded
        // wpn_axe_stone_01, shared palette material) is instantiated as a child of the right-hand bone
        // (probe-verified) so it RIDES the hand's animated transform every frame; the attach-local pose puts
        // the handle in the palm + the blade forward, and the attach-local scale brings the normalized prop
        // to a believable axe size.
        //
        // SERIALIZATION (unity-conventions.md §editor-vs-runtime): called AFTER the avatar is built
        // editor-time (castaway.BuildInEditor in BuildPlayer), so the bone hierarchy exists and the axe
        // child SERIALIZES into Boot.unity under the bone — NOT an Awake-built attach (the "legs-up" /
        // component-not-serialized classes). A HeldAxe runtime component gates the renderer on
        // Inventory.HasAxe (hidden until crafted; the craft reads as "the kid picks up the axe").
        // 86cabh907 (CC-BY retirement): bind the SHARED in-house weapon palette material onto every
        // MeshRenderer of an instantiated flint-axe (held / stump / pickup), so the re-made axe reads
        // with the faceted flat-shaded palette look (URP/Unlit) — NOT the retired CC-BY baked atlas.
        private static void ApplyWeaponPaletteMaterial(GameObject inst)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WeaponPackAssetGen.MaterialPath);
            if (mat == null)
            {
                Debug.LogError("[MovementCameraScene] Mat_WeaponPalette not found at " +
                               WeaponPackAssetGen.MaterialPath + " — run WeaponPackAssetGen.PrepareWeaponPack()");
                return;
            }
            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterial = mat;
        }

        // Re-home a single-node weapon FBX's mesh from the (rig-driven) ROOT onto a "WeaponMeshHolder" child at
        // the given local offset, so a per-weapon mesh shift survives the rig's per-frame root-transform writes
        // (#100 BUG-2). Edit-time mirror of HeldWeaponCycleDebug's runtime re-home — but authored here so the
        // shifted mesh SERIALIZES into Boot.unity (static == runtime). Idempotent: if a holder already exists it
        // just re-applies the offset. Destroys the root MeshFilter/MeshRenderer so only the child holder draws.
        private static void EnsureWeaponMeshHolder(GameObject weapon, Vector3 holderLocalOffset)
        {
            var existing = weapon.transform.Find("WeaponMeshHolder");
            if (existing != null)
            {
                existing.localPosition = holderLocalOffset;
                return;
            }
            var rootMf = weapon.GetComponent<MeshFilter>();
            var rootMr = weapon.GetComponent<MeshRenderer>();
            if (rootMf == null || rootMf.sharedMesh == null)
            {
                // Multi-node FBX (mesh already on a child) — find it + offset that child instead.
                var childMf = weapon.GetComponentInChildren<MeshFilter>(true);
                if (childMf != null && childMf.transform != weapon.transform)
                    childMf.transform.localPosition += holderLocalOffset;
                return;
            }
            var holderGo = new GameObject("WeaponMeshHolder");
            holderGo.transform.SetParent(weapon.transform, false);
            holderGo.transform.localPosition = holderLocalOffset;
            var holderMf = holderGo.AddComponent<MeshFilter>();
            holderMf.sharedMesh = rootMf.sharedMesh;
            var holderMr = holderGo.AddComponent<MeshRenderer>();
            if (rootMr != null)
            {
                holderMr.sharedMaterials = rootMr.sharedMaterials;
                holderMr.shadowCastingMode = rootMr.shadowCastingMode;
                holderMr.receiveShadows = rootMr.receiveShadows;
                Object.DestroyImmediate(rootMr);
            }
            Object.DestroyImmediate(rootMf);
        }

        private static void AttachHeroAxeToHand(GameObject player)
        {
            var castaway = player.GetComponentInChildren<CastawayCharacter>(true);
            if (castaway == null)
            {
                Debug.LogError("[MovementCameraScene] no CastawayCharacter to attach the hero axe to");
                return;
            }

            Transform hand = FindRightHandBone(castaway.transform);
            if (hand == null)
            {
                Debug.LogError("[MovementCameraScene] could not find the castaway's right-hand WRIST bone ('" +
                               RightHandBoneToken + "', mixamorig:RightHand, excl. fingers) — the hero axe has " +
                               "nothing to attach to. Re-run CharacterAssetGen.CharacterDiagnoseTrace to dump the rig.");
                return;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.HeroAxeFbxPath);
            if (fbx == null)
            {
                Debug.LogError("[MovementCameraScene] in-house flint axe FBX not found at " +
                               WeaponPackAssetGen.HeroAxeFbxPath +
                               " — run WeaponPackAssetGen.PrepareWeaponPack() before authoring the scene");
                return;
            }

            // Instantiate the imported hatchet under the hand bone (editor-time -> serializes into
            // Boot.unity). Plain Instantiate (not InstantiatePrefab) — same idiom as the avatar
            // (CastawayCharacter.BuildModel): bakes the mesh/renderer into the scene with no prefab link.
            var axe = Object.Instantiate(fbx);
            axe.name = HeroAxeObjectName;
            axe.transform.SetParent(hand, false);
            ApplyWeaponPaletteMaterial(axe);
            // Scale is uniform-local. The Hyper3D wrist bone reads lossyScale (1,1,1) (probe-verified — NO 267×
            // chibi trap); effective world size = localScale × the avatar-root scale (1.8). 0.45 → ~0.675u longest.
            axe.transform.localScale = Vector3.one * HeldAxeLocalScaleUniform;

            // GRIP-POINT SHIFT (86cabh907 FINAL bake): re-home the displayed mesh onto a WeaponMeshHolder CHILD
            // and slide it +HeldAxeGripShiftY along the long (Unity +Y, head-ward) axis so the hand grips the
            // LOWER-THIRD of the longer straight haft (head up top, board-axe read), NOT near the head. The shift
            // is +Y because the imported axe's long axis is Unity Y, not Z (the §FBX bakeAxisConversion trap —
            // AxeSeatProbe diagnose-via-trace; a first +Z pass shifted off-axis and the held capture still gripped
            // the head). Authored HERE at EDIT-TIME so the shifted pose SERIALIZES into Boot.unity — the static
            // EditMode bounds guards see the SAME lower-third seat the runtime shows (no static-vs-runtime
            // divergence). The single-node axe FBX collapses its MeshFilter onto the rig-driven ROOT
            // (preserveHierarchy:0), and HeldToolRig.LateUpdate stomps the root transform every frame — so the
            // per-weapon mesh-holder MUST be a child the rig never touches (#100 BUG-2: "nudging does nothing,
            // scaling works"). HeldWeaponCycleDebug.Awake DETECTS this pre-authored holder and reuses it (no
            // runtime re-home needed). The FBX origin stays (0,0,0) — only the displayed mesh slides within the
            // holder; the §6 grip-origin semantics are preserved.
            EnsureWeaponMeshHolder(axe, new Vector3(0f, HeldAxeGripShiftY, 0f));

            // SPLIT the pose channels (HeldAxeRig drives both each frame). POSITION is a HAND-LOCAL offset
            // (rotated by the hand rotation, so it TRACKS the hand through every facing — 86ca9qwvd); ROTATION
            // is HAND-RELATIVE so the haft turns WITH the hand on every facing. The rig's field is the HAND-
            // LOCAL offset; the F9 nudge moves it (a pure rotation keeps the ~2 cm/click step).
            var rig = axe.GetComponent<HeldAxeRig>();
            if (rig == null) rig = axe.AddComponent<HeldAxeRig>();
            rig.hand = hand;
            // 86caa83wn soak #4 — the seat offset is HAND-LOCAL END TO END (the seat-doesn't-stick fix). The
            // constant HeldAxeLocalOffsetFromHand IS the rig's hand-local field — baked DIRECTLY with NO
            // Inverse(hand.rotation) conversion. The old code converted a WORLD constant at the bake-time spawn
            // facing, which made the dialed seat facing-specific (the (dial-facing − bake-facing) yaw delta
            // leaked into X/Z → the axe sat wrong after a pickup at a different facing). Now the rig applies
            // axe.position = hand.position + hand.rotation * offset every frame, so the SAME hand-local field
            // seats the axe IDENTICALLY at every facing AND for every acquire path (spawn-in-hand == picked-up).
            // 86cajwp23 AC2 / 86cak9kau AC2 / 86catvb6u — each hero base rides its OWN re-measured seat prior (its
            // mixamorig:RightHand local frame differs); HIGHEST-VERSION-FIRST: v4 (the LIVE default) takes
            // precedence, then v3, then v2, then the old rig. The lower-priority seats are byte-unchanged when
            // their toggle is OFF (the rollback path). NOTE: the v4 seat is the MEASURED first-pass (trace
            // WORLD-TRANSFER of v3's approved carry onto v4's materially-different hand frame — CharacterAssetGen.
            // CastawayV4HandAxisTrace); the Sponsor F9-finalizes it across all 15 held seats. See HeldAxeV4*.
            Vector3 handLocalOffset =
                CharacterAssetGen.UseCastawayV4 ? HeldAxeV4LocalOffsetFromHand :
                CharacterAssetGen.UseCastawayV3 ? HeldAxeV3LocalOffsetFromHand :
                CharacterAssetGen.UseCastawayV2 ? HeldAxeV2LocalOffsetFromHand : HeldAxeLocalOffsetFromHand;
            Vector3 relEuler =
                CharacterAssetGen.UseCastawayV4 ? HeldAxeV4RelEuler :
                CharacterAssetGen.UseCastawayV3 ? HeldAxeV3RelEuler :
                CharacterAssetGen.UseCastawayV2 ? HeldAxeV2RelEuler : HeldAxeRelEuler;
            rig.worldOffsetFromHand = handLocalOffset; // HAND-LOCAL units (field name kept for serialization/F9)
            rig.relEuler = relEuler;                   // hand-relative — turns with the hand
            // 86ca9zcjn (Sponsor design choice, soak 6bcc1bc) — the held axe now FOLLOWS the right arm's
            // natural swing during locomotion: it rides the RAW hand bone. The prior swing-stabilizer /
            // grip-anchor (86ca8rdkp) + the vertical-decouple bounce/ratchet fix (86ca9ykp0) are REMOVED
            // (the Sponsor explicitly reversed the old "the axe changes position when I walk" preference).
            // The raw hand returns to pose each walk cycle → bounded by construction, no ratchet; facing
            // passes through immediately (the raw hand carries the facing yaw). A LIGHT damp is available to
            // de-jitter without re-locking (followDamp); it defaults to 0 so the per-step swing is visible.
            rig.followDamp = HeldAxeFollowDamp;
            // 86caa83wn soak #2 — NO axe-side vertical clamp. The earlier per-state world-Y ceiling clamp (which
            // moved the AXE down independently of the hand) DETACHED the axe from the grip during the run
            // arm-swing ("when i run the axe is no longer in the hand"). It is REMOVED — the axe rides the hand
            // RIGIDLY at all times. The run into-head is now fixed on the ARM side (CastawayArmPose.runLowerEuler,
            // wired in AddArmPose) by lowering the right arm while running, so the gripped axe (which follows the
            // hand) stays below the head AND in the hand.

            // Bake an EQUIVALENT STATIC local pose so a STATIC editor load (the EditMode bounds guards, which
            // run with no play loop -> the rig's LateUpdate never fires) sees the SAME seated pose the rig
            // re-asserts at runtime. Mirrors the NEW hand-local runtime formula
            // (position = hand.position + hand.rotation * handLocalOffset): the seated world position uses the
            // SAME rotated offset, so the static bake matches the runtime pose at the spawn facing. The
            // hand-local field is facing-invariant, so this static pose == the runtime pose at EVERY facing
            // (re-expressed per facing). localRot = Euler(relEuler) (hand-relative).
            Vector3 seatedWorldPos = hand.position + hand.rotation * handLocalOffset;
            Quaternion seatedWorldRot = hand.rotation * Quaternion.Euler(relEuler);
            axe.transform.localPosition = hand.InverseTransformPoint(seatedWorldPos);
            axe.transform.localRotation = Quaternion.Inverse(hand.rotation) * seatedWorldRot;
            Debug.Log("[MovementCameraScene] held axe 86caa83wn hand-local pose: handLocalOffset=" +
                      handLocalOffset.ToString("F4") +
                      " relEuler=" + relEuler.ToString("F1") +
                      (CharacterAssetGen.UseCastawayV4 ? " [v4 seat — MEASURED trace baseline, Sponsor F9-finalizes]" :
                       CharacterAssetGen.UseCastawayV3 ? " [v3 seat prior]" :
                       CharacterAssetGen.UseCastawayV2 ? " [v2 seat prior]" : "") +
                      " (static-baked localPos=" + axe.transform.localPosition.ToString("F4") +
                      " localEuler=" + axe.transform.localEulerAngles.ToString("F1") + ")");

            // Gate visibility on HasAxe: hidden until the craft fires, then the kid is holding it.
            var held = axe.GetComponent<HeldAxe>();
            if (held == null) held = axe.AddComponent<HeldAxe>();
            held.inventory = Object.FindObjectOfType<Inventory>();

            // DEBUG / SOAK-VIEWING handle (86cabh907) — cycle the HELD mesh axe->knife->sword->spear with [B]
            // so the Sponsor can SEE each weapon wielded in-engine. NOT the real equip gameplay (belt->wield
            // is a later ticket): it only swaps the displayed mesh on THIS shared seat (+ a rough per-weapon
            // offset) and starts on the LOCKED axe, so a soak that never presses [B] sees the shipped axe.
            if (axe.GetComponent<HeldWeaponCycleDebug>() == null) axe.AddComponent<HeldWeaponCycleDebug>();

            // SHAFT-LENGTH PICKER RETIRED (86cajkk7h): the [L] length picker + its wpn_axe_01_len11..14 variants
            // were the unstick instrument that let the Sponsor pick the retired flint axe's haft length. The
            // STONE axe (wpn_axe_stone_01) is the Sponsor-approved authored mesh — there is no length to pick —
            // so the picker, its AxeLengthVariants prefab, and the len11..14 FBXs are all removed.

            // HELD-WEAPON PLACEMENT SEAM (86caffwuz) — the single binding surface the unified settings console's
            // 7 held-weapon rows (pos X/Y/Z, rot pitch/yaw/roll, scale) drive. Lives on THIS seat object so it
            // resolves the axe rig + the weapon-cycle from the same GameObject; serialized into Boot.unity so the
            // console binds it with no runtime FindObjectOfType. Authored AFTER the rig + cycle so its references
            // wire directly. It NEVER adds a new attach path — it reads/writes the existing seat fields.
            var placement = axe.GetComponent<HeldWeaponPlacement>();
            if (placement == null) placement = axe.AddComponent<HeldWeaponPlacement>();
            placement.axeRig = axe.GetComponent<HeldAxeRig>();
            placement.weaponCycle = axe.GetComponent<HeldWeaponCycleDebug>();
            EditorUtility.SetDirty(placement);
            // NOTE: the SettingsPanel.heldWeapon back-wire is NOT here — BuildPlayer (which calls this) runs
            // BEFORE BuildSettingsPanel in Author, so the panel doesn't exist yet. BuildSettingsPanel does the
            // cross-wire (it runs after the player/axe exist), the same ordering the orbit/wasd binds rely on.

            // URP/Unlit (the shared weapon palette material) must survive the stripped build.
            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader != null) EnsureShaderAlwaysIncluded(unlitShader);

            // Wire the verification-only shipped-build AXE CLOSE-UP capture onto the Boot object — sibling
            // of the craft/chop/movement verify captures. Inert unless launched with -verifyAxe. It frames
            // the held hatchet close so the silhouette/leather-wrap read rides a committed shipped-build path.
            WireAxeVerifyCapture();

            // Wire the BUILD-GATED debug AxeNudgeTool (86ca8ce6y SOAKFIX5) — inert behind the F9 toggle, lets
            // the Sponsor dial + read off the final held/stump axe transforms in-game (the axe-nudge reframe).
            WireAxeNudgeTool();

            int rendCount = axe.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log("[MovementCameraScene] attached HeroAxe (in-house wpn_axe_stone_01) to bone '" + hand.name +
                      "' (renderers=" + rendCount + ", HasAxe-gated)");
        }

        // (86camz9uz ①) AttachStumpAxe is RETIRED with the CraftSpot stump — the free auto-craft stump +
        // its planted always-visible axe are replaced by the place-to-build table + AxePickup (now the single
        // visible spawn axe). The StumpAxe.cs component is retained (a reusable inverse-HasAxe visibility gate)
        // but is no longer authored into the scene.

        // === 86caa4bya — INVENTORY pack + BELT hotbar UI (UI Toolkit) ============================
        public const string InventoryPanelSettingsAssetPath = SettingsDir + "/InventoryPanelSettings.asset";
        public const string InventoryUxmlPath = "Assets/UI/InventoryPanel.uxml";
        public const string InventoryPaletteUssPath = "Assets/UI/InventoryPalette.uss";
        public const string InventoryPanelUssPath = "Assets/UI/InventoryPanel.uss";
        public const string InventoryUiObjectName = "InventoryUI";
        public static readonly Vector3 AxePickupPosition = new Vector3(3f, 0f, 2f);
        public const string AxePickupObjectName = "AxePickup";

        // The inventory/belt UI lives on its own GameObject hosting a UIDocument + the InventoryUI view.
        // The UXML/USS + the Inventory reference are loaded/wired here so they SERIALIZE into Boot.unity
        // (editor-vs-runtime trap). InventoryUI.BuildView owns the single CloneTree (we do NOT assign
        // doc.visualTreeAsset — same duplicate-shell pitfall the settings panel documented).
        private static void BuildInventoryUI(GameObject player)
        {
            var go = new GameObject(InventoryUiObjectName);

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = EnsureInventoryPanelSettings();
            doc.sortingOrder = 90f; // below the settings panel (100), above the IMGUI HUD

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(InventoryUxmlPath);
            var palette = AssetDatabase.LoadAssetAtPath<StyleSheet>(InventoryPaletteUssPath);
            var panelUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(InventoryPanelUssPath);

            var ui = go.AddComponent<InventoryUI>();
            ui.document = doc;
            ui.panelUxml = uxml;
            ui.paletteUss = palette;
            ui.panelUss = panelUss;
            ui.inventory = Object.FindObjectOfType<Inventory>();

            if (uxml == null || palette == null || panelUss == null)
                Debug.LogWarning("[MovementCameraScene] InventoryUI assets missing (uxml=" + (uxml != null) +
                                 ", palette=" + (palette != null) + ", panelUss=" + (panelUss != null) +
                                 ") — check Assets/UI/Inventory*");
            if (ui.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire InventoryUI to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            // CHANGE 1 (86caa4c5c) — wire the ChopTree's over-UI left-click guard ref now that the InventoryUI
            // exists (BuildChopTree ran BEFORE this, so its serialized inventoryUI was unresolvable then). A
            // left-click OVER the belt/inventory UI must NOT chop the tree behind it; ChopTree asks
            // InventoryUI.IsPointerOverUI. Serialized here so the ref ships in Boot.unity (an Awake
            // FindObjectOfType is the build-safety fallback). Null-graceful: a missing ref just skips the
            // over-UI guard (the modal-panel + RMB guards still apply).
            var chop = Object.FindObjectOfType<ChopTree>();
            if (chop != null)
            {
                chop.inventoryUI = ui;
                EditorUtility.SetDirty(chop);
            }

            // 86caf7a30 — wire the LeftClickConsume's over-UI left-click guard the SAME way (the consume sibling
            // of the chop guard above): a left-click OVER the belt/inventory UI must NOT consume the selected
            // item behind it; LeftClickConsume asks InventoryUI.IsPointerOverUI. Serialized here so the ref ships
            // in Boot.unity (BootstrapProject added LeftClickConsume BEFORE this, so its serialized inventoryUI
            // was unresolvable then — like ChopTree's). Null-graceful: a missing ref skips the over-UI guard.
            var consume = Object.FindObjectOfType<LeftClickConsume>();
            if (consume != null)
            {
                consume.inventoryUI = ui;
                EditorUtility.SetDirty(consume);
            }

            EditorUtility.SetDirty(go);
            Debug.Log("[MovementCameraScene] authored InventoryUI (UI Toolkit; Tab pack + bottom belt, sortingOrder 90)" +
                      " (chop over-UI guard wired: " + (chop != null) + ", consume over-UI guard wired: " +
                      (consume != null) + ")");
        }

        // Create-or-load the runtime PanelSettings for the inventory UIDocument (own asset; reconciled to
        // share the settings panel's PanelSettings when 86caa4bqp/PR #83 merges — cross-lane follow-up).
        private static PanelSettings EnsureInventoryPanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(InventoryPanelSettingsAssetPath);
            if (existing != null) return existing;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.match = 0.5f;
            ps.themeStyleSheet = EnsureRuntimeTheme();
            AssetDatabase.CreateAsset(ps, InventoryPanelSettingsAssetPath);
            AssetDatabase.SaveAssets();
            return ps;
        }

        public const string InventoryRuntimeThemePath = SettingsDir + "/InventoryRuntimeTheme.tss";

        // The ONLY content that makes a .tss a USABLE runtime theme: it must @import Unity's built-in
        // runtime base theme (resolved internally by the .tss importer from the `unity-theme://default`
        // URL). This pulls in the default control styles + the base USS variables every UIElements panel
        // resolves against. An EMPTY .tss imports to a NON-NULL but BASE-LESS ThemeStyleSheet — which is
        // exactly the PR #90 capture-gate HANG (see EnsureRuntimeTheme below). This is the same string the
        // editor writes into Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss when you create a
        // PanelSettings from the menu.
        private const string DefaultRuntimeThemeTss = "@import url(\"unity-theme://default\");\n";

        // Resolve a runtime ThemeStyleSheet for the inventory PanelSettings that ACTUALLY RESOLVES STYLES
        // (BLOCKER-1 fix, PR #90 — round 2). History of this bug:
        //   • round 0: the theme was loaded from two HARDCODED package paths that don't exist in this
        //     project's package layout → both loads returned null → the panel shipped with a NULL theme →
        //     the UIDocument threw at panel init in the windowed exe → capture gate = 0 frames (run
        //     27810143643).
        //   • round 1 (commit 8563b42): created a .tss but wrote it EMPTY, on the FALSE belief that "an
        //     empty .tss inherits Unity's base runtime defaults". It does NOT — an empty .tss imports to a
        //     NON-NULL but BASE-LESS ThemeStyleSheet (so the null-guard + the `themeStyleSheet != null`
        //     test went GREEN — a false-green PROXY guard), and a UIDocument resolving its first repaint
        //     against a base-less theme NEVER completes the first frame's layout in the shipped player →
        //     the exe HANGS before the CaptureGate coroutine reaches a rendered frame → "did not self-quit
        //     within 120s", 0 frames (run 27810923815; the exe boots fully — D3D12/physics/input/NavMesh
        //     all log — then stalls BEFORE `[CaptureGate] start`). The null→hang transition is exactly the
        //     symptom the brief flagged.
        // The real fix: write the .tss with the `@import url("unity-theme://default")` content
        // (DefaultRuntimeThemeTss) so the created theme carries Unity's runtime base styles — a USABLE
        // theme, not just a non-null one. This NEVER returns null:
        //   1. Reuse a project ThemeStyleSheet that ALREADY imports the default (Unity's auto-generated
        //      UnityDefaultRuntimeTheme.tss if the editor created one) — verified by reading its source,
        //      NOT trusted blindly (a base-less found theme would re-hang).
        //   2. Otherwise CREATE/REPAIR our own at InventoryRuntimeThemePath with the proper import content.
        private static ThemeStyleSheet EnsureRuntimeTheme()
        {
            // 1. Reuse any project ThemeStyleSheet whose SOURCE imports the runtime default (path-
            //    independent — picks up Unity's auto-generated theme wherever the editor imported it). We
            //    READ the .tss text (not just the loaded object) because a base-less theme is non-null but
            //    re-triggers the hang — guard the resolving condition, not the non-null proxy.
            string[] guids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                if (!ThemeImportsRuntimeDefault(path)) continue;   // skip empty / base-less themes
                var found = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (found != null) return found;
            }

            // 2. None usable — create (or repair, if a prior empty one exists) ours with the import content
            //    so the PanelSettings always has a theme that resolves Unity's runtime base styles. The
            //    inventory's OWN look still comes from InventoryPalette.uss + InventoryPanel.uss; the theme
            //    only supplies the base default styles the panel layout resolves against.
            Directory.CreateDirectory(SettingsDir);
            bool needsWrite = !File.Exists(InventoryRuntimeThemePath)
                              || !ThemeImportsRuntimeDefault(InventoryRuntimeThemePath);
            if (needsWrite)
            {
                File.WriteAllText(InventoryRuntimeThemePath, DefaultRuntimeThemeTss);
                AssetDatabase.ImportAsset(InventoryRuntimeThemePath, ImportAssetOptions.ForceUpdate);
            }
            var created = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(InventoryRuntimeThemePath);
            if (created == null)
                Debug.LogError("[MovementCameraScene] FAILED to create a runtime UI theme at " +
                               InventoryRuntimeThemePath + " — the inventory UIDocument will throw/hang at " +
                               "startup (theme-less PanelSettings). Capture gate will produce 0 frames.");
            return created;
        }

        // True iff the .tss at <path> imports Unity's runtime default theme (the one line that makes a
        // .tss a USABLE runtime theme vs an empty/base-less shell). Reads the SOURCE text — the imported
        // ThemeStyleSheet object is non-null either way, so the source is the only honest signal.
        private static bool ThemeImportsRuntimeDefault(string path)
        {
            try { return File.ReadAllText(path).Contains("unity-theme://default"); }
            catch { return false; }
        }

        // 86caa4bya AC3 — a PICKABLE axe lying in the world: walk near it → it lands in belt slot 1. Uses
        // the SAME sourced hatchet FBX as the held/stump axe (one asset, identical read). Collider-free so
        // it never blocks the click-raycast or the NavMesh bake. Authored editor-time so the mesh + the
        // AxePickup wiring SERIALIZE into Boot.unity (editor-vs-runtime trap).
        private static void BuildAxePickup(GameObject player, int groundLayer)
        {
            var go = new GameObject(AxePickupObjectName);
            go.transform.position = AxePickupPosition;

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.HeroAxeFbxPath);
            Transform visual = go.transform;
            if (fbx != null)
            {
                var mesh = Object.Instantiate(fbx);
                mesh.name = "AxePickupVisual";
                mesh.transform.SetParent(go.transform, false);
                mesh.transform.localPosition = new Vector3(0f, 0.4f, 0f); // float just above the sand
                mesh.transform.localRotation = Quaternion.Euler(0f, 45f, 90f); // lying/leaning read
                mesh.transform.localScale = Vector3.one * 1.0f;
                visual = mesh.transform;
                ApplyWeaponPaletteMaterial(mesh);
                var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader != null) EnsureShaderAlwaysIncluded(unlitShader);
            }
            else
            {
                Debug.LogWarning("[MovementCameraScene] in-house flint axe FBX not found at " +
                                 WeaponPackAssetGen.HeroAxeFbxPath +
                                 " — AxePickup has no visual mesh (pickup logic still wires)");
            }

            var pickup = go.AddComponent<AxePickup>();
            pickup.inventory = Object.FindObjectOfType<Inventory>();
            pickup.player = player != null ? player.transform : null;
            pickup.visual = visual;
            // 86camz9uz (crafting-redesign ①): the AxePickup is now the SINGLE visible spawn axe → authored
            // ACTIVE. The #100 "axe in two places" concern is GONE (the StumpAxe/CraftSpot stump that was the
            // other visible axe is RETIRED this ticket), so re-activating the one remaining pickup keeps the
            // #100 intent (ONE clear acquisition axe) AND keeps the stone "axe" obtainable in ① — so chop +
            // held-axe stay LIVE-triggerable in the shipped build (the stone axe becomes a STONE-tier table
            // recipe in ②, which reworks/retires this world pickup; it is the ①-interim source).
            pickup.activeAtSpawn = true;
            if (visual != null)
                foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    if (r != null) r.enabled = true; // visible in Boot.unity (the single spawn axe)

            EditorUtility.SetDirty(go);
            Debug.Log("[MovementCameraScene] authored AxePickup at " + AxePickupPosition +
                      " (auto-belt-slot-1; ① spawn-ACTIVE — the single visible spawn axe; stump retired)");
        }

        // Bind the flat DE-LIT material (CastawayMat) onto the avatar's SkinnedMeshRenderer(s) editor-time
        // so it SERIALIZES into Boot.unity (86ca8rdkp). The FBX imports its own ImportStandard material; we
        // override every SMR slot with the single shared de-lit toon mat (texture_diffuse albedo, warm-tan
        // recolored shirt, smoothness ~0) so the look matches the project's URP/Lit toon idiom + carries the
        // recolor. URP/Lit is always-included so it never strips to magenta in the stripped player.
        private static void BindCastawayMaterial(CastawayCharacter castaway)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CharacterAssetGen.MaterialPath);
            if (mat == null)
            {
                Debug.LogError("[MovementCameraScene] CastawayMat not found at " + CharacterAssetGen.MaterialPath +
                               " — run CharacterAssetGen.PrepareCharacter() before authoring the scene");
                return;
            }
            int bound = 0;
            foreach (var smr in castaway.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                if (mats.Length == 0) mats = new[] { mat };
                smr.sharedMaterials = mats;
                bound++;
            }
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);
            Debug.Log("[MovementCameraScene] bound de-lit CastawayMat onto " + bound + " SkinnedMeshRenderer(s)");
        }

        // The Mixamo UPPER-ARM bone tokens (the bone that spreads the arm from the torso). The rig names the
        // upper arm "mixamorig:RightArm" / "mixamorig:LeftArm" (NOT RightShoulder, which is the clavicle, and
        // NOT RightForeArm, the elbow). After stripping the "mixamorig:" namespace the lowered names are
        // exactly "rightarm" / "leftarm". Probe-matched from the same skeleton FindRightHandBone uses.
        public const string RightUpperArmToken = "rightarm";
        public const string LeftUpperArmToken = "leftarm";

        // Wire the additive post-anim arm pose (86ca8rdkp soak-fix #2 + #3) onto the avatar. Resolves the two
        // UPPER-arm bones from the skinned-mesh bone array (the real skeleton) and serializes the component +
        // the bone refs into Boot.unity (editor-vs-runtime trap). RE-SOAK #1: the per-arm eulers ship BAKED
        // from the Sponsor's F9 dial (CastawayArmPose.rightArmEuler/leftArmEuler defaults, seed flag FALSE) —
        // RebuildCached composes those verbatim (no re-seed). The Sponsor can re-dial later via F9.
        private static void AddArmPose(CastawayCharacter castaway)
        {
            var pose = castaway.GetComponent<CastawayArmPose>();
            if (pose == null) pose = castaway.gameObject.AddComponent<CastawayArmPose>();
            pose.rightUpperArm = FindBoneByExactToken(castaway.transform, RightUpperArmToken);
            pose.leftUpperArm = FindBoneByExactToken(castaway.transform, LeftUpperArmToken);
            if (pose.rightUpperArm == null || pose.leftUpperArm == null)
                Debug.LogError("[MovementCameraScene] could not resolve upper-arm bones for CastawayArmPose " +
                               "(right='" + RightUpperArmToken + "' found=" + (pose.rightUpperArm != null) +
                               ", left='" + LeftUpperArmToken + "' found=" + (pose.leftUpperArm != null) +
                               ") — the arm-pose soak-fix will be inert. Re-run CharacterDiagnoseTrace.");
            // 86caa83wn soak #2 — wire the RUN-swing reduction: the CastawayCharacter (whose IsRunning weights the
            // run-lower) + the baked run-lower euler. Wired editor-time so the run-lower ships in Boot.unity (the
            // component-not-serialized trap; a runtime Awake fallback resolves the character but must not be the
            // ship path). The run-lower is INERT at walk/idle (run weight 0 → the Sponsor's locked WALK pose is
            // byte-unchanged); the Sponsor dials runLowerEuler on the F9 RUN target while running, then bakes it.
            pose.character = castaway;
            pose.runLowerEuler = ArmRunLowerEuler;
            pose.RebuildCached();
            Debug.Log("[MovementCameraScene] CastawayArmPose wired (rightArm='" +
                      (pose.rightUpperArm != null ? pose.rightUpperArm.name : "<null>") + "', leftArm='" +
                      (pose.leftUpperArm != null ? pose.leftUpperArm.name : "<null>") +
                      "', character='" + (pose.character != null ? pose.character.name : "<null>") +
                      "', runLowerEuler=" + ArmRunLowerEuler.ToString("F1") + ")");

            // CHOP SWING (86caa4c5c change-(b)) — the swing is now the Mixamo melee Animator Attack state
            // (CastawayCharacter.TriggerChop), NOT a procedural bone offset. The rejected ChopPoseDriver and its
            // authoring here were REMOVED. The Attack state + Chop trigger live in CastawayAnimator.controller
            // (CharacterAssetGen.BuildAnimatorController); ChopTree.character is wired in BuildChopTree.
        }

        // The right-hand FINGER + THUMB bone tokens to curl (86ca8rdkp re-soak #4). Index/Middle/Ring proximal
        // ..distal (1-3; the 4 bone is the fingertip end-helper, not curled) + Thumb 1-3. Exact-token resolved
        // from the SMR bone array. The Hyper3D hand is 4-fingered (no pinky — verified by -fingerTrace).
        public static readonly string[] RightFingerCurlTokens =
        {
            "righthandindex1", "righthandindex2", "righthandindex3",
            "righthandmiddle1", "righthandmiddle2", "righthandmiddle3",
            "righthandring1", "righthandring2", "righthandring3",
        };
        public static readonly string[] RightThumbCurlTokens =
        {
            "righthandthumb1", "righthandthumb2", "righthandthumb3",
        };

        // Wire the right-hand FINGER CURL (86ca8rdkp re-soak #4) onto the avatar. The -fingerTrace OVERTURNED
        // the ticket's "bad weights / collapsing finger" hypothesis (the skinning is clean — uniform 1.8 lossy,
        // (1,1,1) localScale, verts tight to their bones); the "mangled" read is the OPEN clip hand around a
        // held haft. So we CURL the fingers into a grip (gated on HasAxe). Resolves the finger/thumb bones from
        // the SMR bone array (the real skeleton) + serializes the component + bone refs into Boot.unity.
        private static void AddFingerCurl(CastawayCharacter castaway)
        {
            var curl = castaway.GetComponent<CastawayFingerCurl>();
            if (curl == null) curl = castaway.gameObject.AddComponent<CastawayFingerCurl>();

            var fingers = new System.Collections.Generic.List<Transform>();
            foreach (var tok in RightFingerCurlTokens)
            {
                var b = FindBoneByExactToken(castaway.transform, tok);
                if (b != null) fingers.Add(b);
            }
            var thumbs = new System.Collections.Generic.List<Transform>();
            foreach (var tok in RightThumbCurlTokens)
            {
                var b = FindBoneByExactToken(castaway.transform, tok);
                if (b != null) thumbs.Add(b);
            }
            curl.fingerBones = fingers.ToArray();
            curl.thumbBones = thumbs.ToArray();
            curl.inventory = Object.FindObjectOfType<Inventory>();
            curl.RebuildCached();

            // v2 (86cajx050) AND v3 (86cak9kau) are both 41-bone Mixamo variants whose RIGHT hand resolves ONLY
            // index 1-3 + thumb 1-3 — no middle/ring/pinky. EMPIRICAL (CastawayV2HandAxisTrace / CastawayV3HandAxisTrace:
            // index/middle/ring=3/9, thumb=3/3, pinky=0/3 for BOTH). v3's advertised "16 finger bones" do NOT
            // surface as separated mixamorig middle/ring/pinky curl targets — v3's curl resolution is IDENTICAL to
            // v2's (index+thumb), so the Sponsor-approved v2 grip idiom carries over unchanged (86cak9kau AC4:
            // premise overturned by trace — no new curl pass needed). So the OLD "expected 9, floor 6" bar is WRONG
            // for the fist-hand variants (they physically cannot resolve middle/ring). The real regression this
            // guards is a resolution FAILURE (the curl wired to fewer bones than the rig actually provides), so the
            // floor is per-rig: fist-hand variant (v2/v3)=3 (index 1-3, all they have), old=6 (a partial old
            // resolve still ships an incomplete grip). A LogError here also reds the EditMode rebuild test via LogAssert.
            // 86cakbe2v item 3: this build-time floor stays a HARDCODED baseline on PURPOSE — deriving it from the
            // rig here would be tautological (fingers.Count IS "the curl tokens the rig provides", so a rig-derived
            // floor could never fire). It catches a rig that provides FEWER than a known-good baseline (e.g. a swap
            // that drops the index chain). The RE-TIGHTENING guard (reds if a future re-export adds middle/ring
            // chains the curl fails to resolve) lives in the TEST, CastawayCharacterTests
            // .Avatar_HasSerializedFingerCurl_WithRightHandFingerBones, which derives the floor from the rig's
            // actual curl-token bone count. If a future rig gains separated middle/ring chains, raise this baseline.
            // 86catpwc4 — v4 is a THIRD fist-hand variant: the 33-bone rig resolves ONLY the Index chain (index
            // 1-3 = 3 curl bones) + NO thumb bones (thumbs.Count=0), which is exactly the fist-hand floor of 3.
            // So v4 fits the SAME floor as v2/v3 (the LogError won't fire spuriously). v4's zero thumbs is fine —
            // the floor only gates fingers.Count; the HasAxe-gated grip curls the index fingers into the haft.
            bool fistHandVariant = CharacterAssetGen.UseCastawayV4 || CharacterAssetGen.UseCastawayV3 || CharacterAssetGen.UseCastawayV2;
            int expectedFingerFloor = fistHandVariant ? 3 : 6;
            // 86catvb6u NIT (#307): the log wording is VERSION-AGNOSTIC — it reads "a fist-hand variant" +
            // derives the thumb clause from thumbs.Count, instead of hardcoding "v2/v3/v4" (which goes stale the
            // moment a v5 lands). fingers.Count / thumbs.Count already carry the actual rig facts; the string
            // describes what was resolved, not which named version is live.
            if (fingers.Count < expectedFingerFloor)
                Debug.LogError("[MovementCameraScene] CastawayFingerCurl resolved only " + fingers.Count +
                               " right-hand finger bones (expected >= " + expectedFingerFloor +
                               (fistHandVariant ? " for a fist-hand variant: Index 1-3" : ": Index/Middle/Ring 1-3") +
                               ") — the grip curl will be partial/unwired. Re-run the active hero's " +
                               "CharacterAssetGen.Castaway*HandAxisTrace to dump the rig.");
            else
                Debug.Log("[MovementCameraScene] CastawayFingerCurl wired (" + fingers.Count + " finger + " +
                          thumbs.Count + " thumb bones, HasAxe-gated" +
                          (fistHandVariant ? "; fist-hand-variant index" + (thumbs.Count > 0 ? "+thumb" : " (no thumb)") + " only" : "") + ")");
        }

        // The foot bone tokens (colon-stripped mixamorig names) the FOOT-YAW counter-rotate drives (86catvb6u).
        public const string LeftFootToken = "leftfoot";
        public const string RightFootToken = "rightfoot";

        // Wire the per-foot YAW counter-rotate (86catvb6u — the Sponsor's chosen v4 pigeon-toe fix) onto the
        // avatar. Resolves both foot bones from the SMR bone array + serializes the component + bone refs into
        // Boot.unity (editor-vs-runtime trap, same as AddArmPose/AddFingerCurl). The offset is applied ONLY for v4
        // (footYawDeg = CastawayV4FootYawDeg); v3/v2/old ship 0 → their feet are byte-unchanged (LateUpdate
        // early-returns at 0). The Sponsor F9-dials footYawDeg (FOOT-YAW target) live, then a follow-up bakes it.
        private static void AddFootYaw(CastawayCharacter castaway)
        {
            var yaw = castaway.GetComponent<CastawayFootYaw>();
            if (yaw == null) yaw = castaway.gameObject.AddComponent<CastawayFootYaw>();
            yaw.leftFoot = FindBoneByExactToken(castaway.transform, LeftFootToken);
            yaw.rightFoot = FindBoneByExactToken(castaway.transform, RightFootToken);
            // v4 gets the measured bind-delta default (starts near-straight); every other hero ships 0 (feet
            // untouched — the rollback path stays byte-identical). The Sponsor dials the exact angle at the soak.
            yaw.footYawDeg = CharacterAssetGen.UseCastawayV4 ? CastawayV4FootYawDeg : 0f;
            if (yaw.leftFoot == null || yaw.rightFoot == null)
                Debug.LogError("[MovementCameraScene] could not resolve foot bones for CastawayFootYaw (left='" +
                               LeftFootToken + "' found=" + (yaw.leftFoot != null) + ", right='" + RightFootToken +
                               "' found=" + (yaw.rightFoot != null) + ") — the v4 foot-yaw fix will be inert.");
            else
                Debug.Log("[MovementCameraScene] CastawayFootYaw wired (footYawDeg=" + yaw.footYawDeg.ToString("F1") +
                          (CharacterAssetGen.UseCastawayV4 ? " [v4 — measured bind-delta default; Sponsor F9-finalizes]" : " [non-v4 — 0, feet byte-unchanged]") + ")");
        }

        // Resolve a bone whose colon-stripped lowered name EXACTLY equals the token (excludes fingers/dummy/
        // end helpers), from the SMR bone array (the real skeleton — not mesh-group nodes). Same discipline
        // as FindRightHandBone/IsRightWristBone, generalized to any exact upper-arm token.
        private static Transform FindBoneByExactToken(Transform avatarRoot, string token)
        {
            var smr = avatarRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null)
                foreach (var bone in smr.bones)
                    if (bone != null && ExactBoneToken(bone.name) == token) return bone;
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
                if (ExactBoneToken(t.name) == token) return t;
            return null;
        }

        // Strip the "mixamorig:" namespace + lower-case, returning the bare bone token for an exact compare.
        private static string ExactBoneToken(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return "";
            string n = boneName.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1);
            return n;
        }

        // Find the castaway's right-hand WRIST bone from the SKINNED-MESH BONE ARRAY (the actual skeleton).
        // The Mixamo rig names the wrist "mixamorig:RightHand"; the FINGER bones ("mixamorig:RightHandIndex1"
        // ...) ALSO contain "righthand", so a bare Contains match would grab a finger by bone-array order.
        // Match the bone whose name ENDS at the token (no finger/thumb suffix after "righthand"), excluding
        // the "end" helper. Probe-verified bone name (CharacterDiagnoseTrace, 2026-06-15). Falls back to a
        // transform scan with the same exact-suffix discipline.
        private static Transform FindRightHandBone(Transform avatarRoot)
        {
            var smr = avatarRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null)
            {
                foreach (var bone in smr.bones)
                {
                    if (bone != null && IsRightWristBone(bone.name)) return bone;
                }
            }
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (IsRightWristBone(t.name)) return t;
            }
            return null;
        }

        // True iff the bone name is the right WRIST (ends at "righthand"), NOT a finger/thumb/dummy/end. The
        // Mixamo wrist is "mixamorig:RightHand" → after stripping any "mixamorig:" prefix the lowered name is
        // exactly "righthand". Fingers are "righthandindex1" etc. (extra chars after the token).
        private static bool IsRightWristBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return false;
            string n = boneName.ToLowerInvariant();
            int colon = n.LastIndexOf(':');
            if (colon >= 0) n = n.Substring(colon + 1); // strip "mixamorig:" namespace
            return n == RightHandBoneToken; // exactly "righthand" — excludes fingers/thumb/dummy/end
        }

        private static void WireAxeVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host AxeVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<AxeVerifyCapture>() == null)
                bootGo.AddComponent<AxeVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the BUILD-GATED debug AxeNudgeTool onto the Boot object so it SERIALIZES into Boot.unity (the
        // editor-vs-runtime trap — a component in source but not in the scene ships inert). The tool is INERT
        // in normal play (asleep behind the F9 toggle), so it never affects a soak; it lets the Sponsor dial
        // + read off the final axe transforms in-game (86ca8ce6y SOAKFIX5 — the axe-nudge reframe).
        private static void WireAxeNudgeTool()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host AxeNudgeTool");
                return;
            }
            if (bootGo.GetComponent<AxeNudgeTool>() == null)
                bootGo.AddComponent<AxeNudgeTool>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the BUILD-GATED debug CameraFollowNudgeTool onto the Boot object so it SERIALIZES into Boot.unity
        // (the editor-vs-runtime trap — a component in source but not in the scene ships inert). INERT in normal
        // play (asleep behind the F7 toggle); lets the Sponsor dial the OrbitCamera follow gains (horizontal /
        // vertical lerp + lead time) in-game while jumping in every direction and read the values to bake
        // (86caaqhj5 ATTEMPT 2 — the jump-pull-back precision handoff).
        private static void WireCameraFollowNudgeTool()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host CameraFollowNudgeTool");
                return;
            }
            if (bootGo.GetComponent<CameraFollowNudgeTool>() == null)
                bootGo.AddComponent<CameraFollowNudgeTool>();
            EditorUtility.SetDirty(bootGo);
        }

        private static void WireCastawayVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host CastawayVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<CastawayVerifyCapture>() == null)
                bootGo.AddComponent<CastawayVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        private static void WireHandsVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host HandsVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<HandsVerifyCapture>() == null)
                bootGo.AddComponent<HandsVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the BUILD-GATED held-axe WALK-BOUNCE/RATCHET trace (86ca9ykp0) onto the Boot object so it
        // SERIALIZES into Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert
        // otherwise). INERT in normal play; on -axeWalkTrace it drives a scripted walk + dumps every Y-reference
        // per frame so the ratchet source is pinned (the DIAGNOSE-BEFORE-FIX instrument).
        private static void WireAxeWalkTrace()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host AxeWalkTrace");
                return;
            }
            if (bootGo.GetComponent<AxeWalkTrace>() == null)
                bootGo.AddComponent<AxeWalkTrace>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the BUILD-GATED LIVE FLOAT-DIAGNOSTIC onto the Boot object so it SERIALIZES into Boot.unity (the
        // component-in-source-but-not-in-scene trap — it would ship inert otherwise). INERT in normal play
        // (asleep behind the F8 toggle); shows feet/ground/GAP live so the Sponsor dials GROUND-Y to GAP≈0
        // and the orchestrator reads the ~1Hz [FloatTrace] log (86ca8rdkp — the instrument, not a blind tweak).
        private static void WireFloatDiagnostic()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host FloatDiagnostic");
                return;
            }
            if (bootGo.GetComponent<FloatDiagnostic>() == null)
                bootGo.AddComponent<FloatDiagnostic>();
            EditorUtility.SetDirty(bootGo);
        }

        private static void WireFloatDiagnosticVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host FloatDiagnosticVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<FloatDiagnosticVerifyCapture>() == null)
                bootGo.AddComponent<FloatDiagnosticVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the BUILD-GATED SNEAK-WALK ISOLATION tool (86caa3kur re-soak attempt-3 /unstick instrument) onto
        // Boot so it SERIALIZES into Boot.unity (the editor-vs-runtime serialization trap — it would ship inert
        // otherwise). F10 flips the shared DebugOverlays.Visible master so every dev overlay reveals together;
        // default state = clean screen (master hidden). Sibling of WireFloatDiagnostic. (86caju054 — re-homed off
        // the RETIRED SneakIsolationTool: the sneak readout + its F5/F6 handles are gone, F10 stays the master.)
        private static void WireDebugOverlayMaster()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host DebugOverlayMaster");
                return;
            }
            var tool = bootGo.GetComponent<DebugOverlayMaster>();
            if (tool == null)
                tool = bootGo.AddComponent<DebugOverlayMaster>();
            // EXPLICITLY re-assert the toggle key every bootstrap — a bare AddComponent leaves a stale SERIALIZED
            // KeyCode value in the committed binary Boot.unity when the component already exists, so a code-only
            // default change would NEVER reach the shipped exe (editor-vs-runtime serialization trap +
            // [[unity-procedural-committed-assets-go-stale]]). Setting the field makes the baked scene
            // authoritative-from-code. F10 = the SINGLE debug-overlay master (86cah90cp; the legacy F2 master was
            // removed round-3) — flips DebugOverlays.Visible so F10 reveals the WorldLookNudgeTool + nudge panels
            // together; F1 stays the console, F2 is UNBOUND.
            tool.overlayToggleKey = KeyCode.F10;
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the gameplay-cam walk-grounding capture (86ca8rdkp attempt-9). Serializes onto Boot (the
        // component-in-source-but-not-in-scene trap) so -verifyWalkGround ships in the exe; inert otherwise.
        private static void WireWalkGroundingVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host WalkGroundingVerifyCapture");
                return;
            }
            if (bootGo.GetComponent<WalkGroundingVerifyCapture>() == null)
                bootGo.AddComponent<WalkGroundingVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // (86camz9uz ①) WireCraftVerifyCapture is RETIRED with CraftSpot — CraftVerifyCapture.cs is deleted
        // (it drove the player to the stump + asserted the free craft, a flow that no longer exists). The
        // place-to-build table's visual proof is the CraftingTable side-profile capture + the PlayMode gate.

        // World position of the choppable tree on the flat test ground (U2-3, 86ca8bdd8). Distinct from
        // spawn (origin) and the craft spot (8,6) so the loop is a real journey: spawn -> craft axe ->
        // chop tree. Comfortably inside the GroundHalf=30 walkable extent + on the NavMesh.
        // ChopVerifyCapture drives the player here (after the craft spot) to prove the chop in the exe.
        public static readonly Vector3 ChopTreePosition = new Vector3(-9f, 0f, -7f);

        // The choppable tree (U2-3, 86ca8bdd8): a Zone-D low-poly tree (welded trunk + faceted canopy,
        // the same smooth-shaded mesh idiom as LowPolyZoneGen's scatter trees — we RIDE the established
        // Zone-D look, not invent a fresh prop, per the art-direction gate). The castaway click-moves to
        // it; reaching it WITH the axe chops it for wood. NO collider on the tree so it never blocks the
        // ground raycast or the NavMesh (the player walks up to the trunk). ChopTree's Inventory + player
        // + visual refs are wired editor-time so they serialize into Boot.unity (editor-vs-runtime trap).
        private static void BuildChopTree(GameObject player, int groundLayer)
        {
            var tree = new GameObject("ChopTree");
            tree.transform.position = ChopTreePosition;

            // The visual: a low-poly trunk + canopy, parented so ChopTree can tween the whole visual on
            // felling (sink + tip) without moving the interaction's proximity origin. Built editor-time
            // (meshes assigned to MeshFilters + inline materials) so it serializes into the scene.
            var visual = new GameObject("TreeVisual");
            visual.transform.SetParent(tree.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // Warm bark trunk (sub-1.0, HDR-safe). The canopy is now a BLOB CANOPY (board v2,
            // 86ca8ce7j): a cluster of overlapping faceted spheroids in multi-value greens, so the
            // choppable tree reads in the SAME blob-canopy language as the scatter trees (it must NOT
            // be a lone single-dome tree next to clustered ones — art-direction fidelity). Only the
            // VISUAL mesh changes here; ChopTree's behavior/wiring/tests are untouched (the canopy is a
            // child under the same tweened visual root, so felling still sinks+tips the whole tree).
            Color trunkCol = new Color(0.42f, 0.30f, 0.19f); // warm bark (LowPolyZoneGen.TrunkCol)

            const float trunkH = 1.8f;
            BuildTreePart(visual, "Trunk", LowPolyMeshes.TaperedCylinder(0.22f, 0.15f, trunkH, 6),
                trunkCol, Vector3.zero, "ChopTrunkMat");
            BuildBlobCanopyPart(visual, "Canopy",
                LowPolyMeshes.BlobCanopy(1.30f, 5, ChopCanopyBody, ChopCanopyTop, ChopCanopyShadow, 8123),
                new Vector3(0f, trunkH + 0.65f, 0f));

            var chop = tree.AddComponent<ChopTree>();
            chop.player = player.transform;
            chop.inventory = Object.FindObjectOfType<Inventory>();
            chop.visual = visual.transform;
            if (chop.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire ChopTree to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            // Wire the SWING (AC1 / change-(b)) — the castaway (CastawayCharacter) whose TriggerChop() plays the
            // Mixamo melee Attack state, authored in BuildPlayer BEFORE this. Each landed chop calls
            // character.TriggerChop() so the arm swings; the held axe (HeldAxeRig order 100) follows the swung hand
            // automatically. Serialized here so the ref ships in Boot.unity (null is graceful at runtime — the chop
            // still yields wood). REPLACES the rejected procedural ChopPoseDriver.
            chop.character = Object.FindObjectOfType<CastawayCharacter>();
            if (chop.character == null)
                Debug.LogWarning("[MovementCameraScene] no CastawayCharacter in scene to wire ChopTree.character " +
                                 "to — the chop will yield wood but the arm won't swing (BuildPlayer must author " +
                                 "the castaway before BuildChopTree)");

            // REWORK 86caf9u5t — author the shared LogPileSpawner (the factory that mints a lootable LogPile when a
            // tree fells + the host the `tree-chop wood yield` / `log-pile despawn` settings bind to). Serialized
            // editor-time so the settings bind to a stable instance + the chop wires to it without a scene search.
            // Its logMaterial is the chop tree's TRUNK material (the shared opaque LowPolyVertexColor on the
            // ~1-draw-call path), so every spawned pile reads as the same wood. (The scatter trees use the same
            // material family; the felled tree's own trunk material is the per-spawn fallback in ApplyChopEffect.)
            var spawnerGo = new GameObject("LogPileSpawner");
            var logPileSpawner = spawnerGo.AddComponent<LogPileSpawner>();
            var trunkMr = FindTrunkRenderer(visual.transform);
            if (trunkMr != null) logPileSpawner.logMaterial = trunkMr.sharedMaterial;
            // #165 FIX — the spawner's PickableLooter ref (so every spawned pile registers for E-loot) is wired
            // LATER in BuildPickableLooter: the looter is added on the player AFTER BuildChopTree in Author (the
            // player exists, the looter does not yet), so wiring it here would self-find null. BuildPickableLooter
            // back-wires logPileSpawner.looter once both exist (mirrors the post-creation cross-wire pattern).
            chop.logPileSpawner = logPileSpawner;
            EditorUtility.SetDirty(chop);
            EditorUtility.SetDirty(spawnerGo);

            // Wire the SETTINGS PANEL's chop refs (86caa4c5c V1/V2 / change-(b) + REWORK 86caf9u5t) now that the
            // castaway, the tree, AND the spawner exist (BuildSettingsPanel ran before this, so its serialized
            // chop refs were not yet resolvable at panel-author time). `tool-use speed` flips live to chopSpeed;
            // `tree regrowth time` + `chops-to-fell` bind to the tree; `tree-chop wood yield` + `log-pile despawn`
            // bind to the spawner. A runtime Awake FindObjectOfType is the fallback; this serializes the refs so
            // the rows are live in the shipped build without a scene search.
            var settingsPanel = Object.FindObjectOfType<SettingsPanel>();
            if (settingsPanel != null)
            {
                settingsPanel.chopCharacter = chop.character;
                settingsPanel.chopTree = chop;
                settingsPanel.logPileSpawner = logPileSpawner;
                EditorUtility.SetDirty(settingsPanel);
            }

            // Wire the verification-only shipped-build CHOP capture (drives the player to craft the axe,
            // then to the tree, proves wood is yielded in the BUILT exe) onto the Boot object — sibling
            // of the craft/movement verify captures. Inert unless launched with -verifyChop.
            WireChopVerifyCapture(player);

            Debug.Log("[MovementCameraScene] authored ChopTree at " + ChopTreePosition +
                      " (inventory wired: " + (chop.inventory != null) + ")");
        }

        // CHANGE (a) 86caa4c5c — serialize the ChopTree.scatterRoot ref onto the LowPolyScatter root so EVERY
        // world scatter tree is choppable in the shipped build (the chop resolves the nearest in-range tree —
        // AC5). Called by BootstrapProject AFTER WorldBootstrap.BuildEnvironment authors the scatter root (it
        // does NOT exist at BuildChopTree time — the player/craft/chop are authored before the environment).
        // READ-only: this wires a reference to the existing scatter; it never re-authors / re-rolls the
        // seed-42 placement (ScatterIslandProps is untouched → byte-identical world). The ChopTree.Start()
        // GameObject.Find("LowPolyScatter") name-scan is the build-safety net if this wiring ever no-ops.
        public static void WireChopScatterRoot()
        {
            var chop = Object.FindObjectOfType<ChopTree>();
            if (chop == null)
            {
                Debug.LogWarning("[MovementCameraScene] WireChopScatterRoot: no ChopTree in scene to wire " +
                                 "scatterRoot to (the runtime name-scan fallback still finds LowPolyScatter)");
                return;
            }
            var scatter = GameObject.Find("LowPolyScatter");
            if (scatter == null)
            {
                Debug.LogWarning("[MovementCameraScene] WireChopScatterRoot: no LowPolyScatter root found — " +
                                 "scatter trees won't be choppable until the runtime fallback (none in a bare scene)");
                return;
            }
            chop.scatterRoot = scatter.transform;
            EditorUtility.SetDirty(chop);
            int treeCount = 0;
            foreach (var mf in scatter.GetComponentsInChildren<Transform>(true))
                if (mf.name == ChopTree.ScatterTreeName) treeCount++;
            Debug.Log("[MovementCameraScene] wired ChopTree.scatterRoot -> LowPolyScatter (" + treeCount +
                      " scatter trees now choppable, CHANGE (a))");
        }

        // F3 FIX (86caa4c96 / Devon REQUEST_CHANGES) — UNIFY to ONE shared StoneRespawner AFTER the scatter
        // exists. Mirrors WireChopScatterRoot's pre-scatter/post-scatter ordering solution: BuildWiredStone runs
        // at BuildBootScene (BEFORE WorldBootstrap.BuildEnvironment authors the scatter root), so its
        // FindObjectOfType<StoneRespawner> was null and it used to add a SECOND respawner on the player that
        // bound NOTHING — while ScatterIslandProps then added the scatter-root respawner that the 70 scatter
        // stones bind. Two respawners => SettingsPanel.FindObjectOfType picked one ARBITRARILY => the `stone
        // respawn time` slider tuned an arbitrary population, a DEAD KNOB. This step runs AFTER the scatter and
        // CANONICALISES: pick the scatter-bound respawner as the ONE truth (or, on a scatter-less rig, author a
        // single one), DESTROY any stray others, then point the wired stone's StoneProp + the SettingsPanel at
        // THAT instance. END STATE: exactly ONE StoneRespawner; the slider, the wired stone, and the 70 scatter
        // stones all bind the SAME instance. READ/wire-only — the seed-42 scatter placement is untouched.
        public static void WireStoneScatterRoot()
        {
            // Collect every StoneRespawner currently in the scene (the scatter-root one + the historical
            // player-added stray, if a stale path produced it).
            var respawners = Object.FindObjectsOfType<StoneRespawner>(true);

            // The CANONICAL respawner = the one parented under the scatter root (LowPolyScatter), because the 70
            // scatter stones already bind to it (LowPolyZoneGen.BuildStone). Prefer it; else fall back to the
            // first found; else author one on the scatter root (or a new holder on a scatter-less rig).
            StoneRespawner canonical = null;
            var scatter = GameObject.Find("LowPolyScatter");
            foreach (var r in respawners)
            {
                if (scatter != null && r.transform.IsChildOf(scatter.transform)) { canonical = r; break; }
            }
            if (canonical == null && respawners.Length > 0) canonical = respawners[0];
            if (canonical == null)
            {
                // Scatter-less rig (no scatter stones to bind): author the single shared respawner so the
                // wired stone + the settings slider still resolve a real window.
                var host = scatter != null ? scatter : GameObject.Find("WiredStone");
                if (host == null) host = new GameObject("StoneRespawnerHost");
                canonical = host.AddComponent<StoneRespawner>();
            }

            // DESTROY every stray (non-canonical) StoneRespawner so EXACTLY ONE survives (the dead-knob root
            // cause). Object.DestroyImmediate is the editor-time delete (this runs in the bootstrap editor pass).
            int destroyed = 0;
            foreach (var r in respawners)
            {
                if (r != null && r != canonical) { Object.DestroyImmediate(r); destroyed++; }
            }

            // Point the WIRED stone's StoneProp at the canonical respawner (BuildWiredStone left it null pre-
            // scatter; the scatter stones already bind canonical by construction).
            foreach (var sp in Object.FindObjectsOfType<StoneProp>(true))
            {
                if (sp.respawner != canonical) { sp.respawner = canonical; EditorUtility.SetDirty(sp); }
            }

            // Point the SETTINGS PANEL's stoneRespawner at the canonical instance — serialized so the `stone
            // respawn time` slider drives the SAME respawner the 70 scatter stones read (BuildSettingsPanel did
            // NOT set this; the Awake FindObjectOfType then resolved arbitrarily — the dead knob). The runtime
            // Awake FindObjectOfType<StoneRespawner> stays as the bare-scene fallback, but now there is only ONE
            // to find, so even that resolves the right one.
            var settingsPanel = Object.FindObjectOfType<SettingsPanel>();
            if (settingsPanel != null)
            {
                settingsPanel.stoneRespawner = canonical;
                EditorUtility.SetDirty(settingsPanel);
            }

            Debug.Log("[MovementCameraScene] WireStoneScatterRoot: unified to ONE StoneRespawner (destroyed " +
                      destroyed + " stray, canonical on '" + canonical.gameObject.name + "', SettingsPanel + " +
                      "wired stone + scatter stones now bind the SAME instance — F3 dead-knob fix)");
        }

        // 86cabn67w (Devon NIT #1) — wire the SettingsPanel.berryBushes array EDITOR-TIME (serialized into
        // Boot.unity), NOT just via the runtime Awake FindObjectsByType fallback. POST-scatter, mirroring
        // WireStoneScatterRoot's pre/post ordering: BuildSettingsPanel runs at BuildBootScene (BEFORE
        // WorldBootstrap.BuildEnvironment authors the ~32 scatter LP_BerryBush instances), AND even the
        // fixed-position BerryBush (BuildBerryBush) is authored AFTER BuildSettingsPanel — so at panel-build
        // time NO bush exists to serialize. This step runs AFTER BuildEnvironment (from BootstrapProject,
        // alongside WireStoneScatterRoot), when EVERY bush (wired + scatter) exists, and serializes the full
        // set so the `Berry regrowth time` row's fan-out reaches all of them in the SHIPPED build without
        // relying on a runtime Find. UNLIKE stones, berries have NO shared manager to canonicalise — each
        // BerryBush holds its own regrow window — so this only COLLECTS + serializes the array; it never
        // destroys/re-parents anything (READ/wire-only; the seed-42 scatter is untouched). The Awake
        // FindObjectsByType<BerryBush> stays as the belt-and-suspenders bare-scene fallback. The
        // editor-vs-runtime ship-path discipline (the stone-respawner runtime-Find that went DEAD is the
        // cautionary precedent).
        public static void WireBerryBushes()
        {
            var settingsPanel = Object.FindObjectOfType<SettingsPanel>();
            if (settingsPanel == null)
            {
                Debug.LogWarning("[MovementCameraScene] WireBerryBushes: no SettingsPanel in scene to wire " +
                                 "berryBushes onto (the runtime Awake FindObjectsByType fallback still resolves them)");
                return;
            }

            // Collect EVERY BerryBush (the fixed-position wired bush + the ~32 scatter LP_BerryBush instances),
            // including inactive, so the serialized set is complete and deterministic (InstanceID sort matches
            // the Awake fallback's ordering, so the representative bush is identical either resolution path).
            var bushes = Object.FindObjectsByType<BerryBush>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            settingsPanel.berryBushes = bushes;
            EditorUtility.SetDirty(settingsPanel);

            Debug.Log("[MovementCameraScene] WireBerryBushes: serialized " + bushes.Length + " BerryBush refs " +
                      "onto SettingsPanel.berryBushes editor-time (the `Berry regrowth time` row fans out across " +
                      "ALL of them in the shipped build — Devon NIT #1, editor-time ship-path not the Awake fallback)");
        }

        // 86caber95 AC2 — back-wire SettingsPanel.worldLook to the WorldLookTunables seam EDITOR-TIME (serialized
        // into Boot.unity), NOT just via the runtime Awake FindObjectOfType fallback. POST-environment, mirroring
        // WireStoneScatterRoot/WireBerryBushes: BuildSettingsPanel runs at BuildBootScene (BEFORE
        // WorldBootstrap.BuildEnvironment adds the WorldLookTunables seam onto hudGo), so at panel-build time the
        // seam does not exist to serialize. This step runs AFTER BuildEnvironment (from BootstrapProject), when the
        // seam exists, and serializes the ref so the F10-migrated fog/sky/cloud/mountain/sun rows ship LIVE without
        // a runtime Find (the editor-vs-runtime ship-path discipline the stone-respawner dead-knob taught). The
        // Awake FindObjectOfType<WorldLookTunables> stays the bare-scene safety net. READ/wire-only — the seam
        // resolves its own live handles lazily at runtime, so nothing here depends on the world being built yet.
        public static void WireWorldLookConsole()
        {
            var settingsPanel = Object.FindObjectOfType<SettingsPanel>();
            if (settingsPanel == null)
            {
                Debug.LogWarning("[MovementCameraScene] WireWorldLookConsole: no SettingsPanel in scene to wire " +
                                 "worldLook onto (the runtime Awake FindObjectOfType fallback still resolves it)");
                return;
            }
            var seam = Object.FindObjectOfType<WorldLookTunables>();
            if (seam == null)
            {
                Debug.LogWarning("[MovementCameraScene] WireWorldLookConsole: no WorldLookTunables seam in scene — " +
                                 "the F10-migrated world-look rows will not appear (check BootstrapProject added it to hudGo)");
                return;
            }
            settingsPanel.worldLook = seam;
            EditorUtility.SetDirty(settingsPanel);
            Debug.Log("[MovementCameraScene] WireWorldLookConsole: serialized WorldLookTunables onto " +
                      "SettingsPanel.worldLook editor-time (the F10 fog/sky/cloud/mountain/sun rows ship live)");
        }

        // Blob-canopy greens for the choppable tree (board v2, 86ca8ce7j) — same 3-value palette family
        // as LowPolyZoneGen's scatter canopies (style-guide-v2 §6), so the choppable tree matches the
        // world's trees. Multi-value greens are baked into the mesh's vertex color by BlobCanopy.
        private static readonly Color ChopCanopyBody   = new Color(0.30f, 0.58f, 0.24f);
        private static readonly Color ChopCanopyTop    = new Color(0.48f, 0.74f, 0.34f);
        private static readonly Color ChopCanopyShadow = new Color(0.18f, 0.40f, 0.17f);

        // Build the chop tree's blob canopy: a child with the BlobCanopy mesh (multi-value greens in
        // vertex color) + an INLINE vertex-color material (serializes into the scene, no .mat churn).
        // The canopy bakes its greens into vertex color, so it needs the FarHorizon/LowPolyVertexColor
        // shader (URP/Lit ignores vertex color — unity-conventions.md); that shader is registered in
        // AlwaysIncludedShaders so it never strips. Falls back to a flat mid-green URP/Lit if the
        // shader is unresolved (vertex greens lost, but never magenta).
        private static void BuildBlobCanopyPart(GameObject parent, string name, Mesh mesh, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                var mat = new Material(vc) { name = "ChopCanopyMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white);
                mr.sharedMaterial = mat; // inline -> serializes into the scene
                EnsureShaderAlwaysIncluded(vc);
            }
            else
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(litShader) { name = "ChopCanopyMat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ChopCanopyBody);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
                Debug.LogWarning("[MovementCameraScene] vertex-color shader not found; chop canopy flat-green fallback");
            }
        }

        // Build one part of the chop tree's visual (trunk or canopy): a child GameObject with the given
        // welded smooth-shaded mesh + an inline URP/Lit material (serializes into the scene, no .mat
        // churn). Matte smoothness so the low-poly reads by shape + shading, not gloss.
        // REWORK 86caf9u5t — the chop tree's TRUNK MeshRenderer (the "Trunk" child built by BuildTreePart), so the
        // LogPileSpawner's logMaterial reads as the same wood. Falls back to the first MeshRenderer under the
        // visual (then null) so a tree whose trunk child is renamed still resolves a material.
        private static MeshRenderer FindTrunkRenderer(Transform visual)
        {
            if (visual == null) return null;
            var trunk = visual.Find("Trunk");
            if (trunk != null)
            {
                var mr = trunk.GetComponent<MeshRenderer>();
                if (mr != null) return mr;
            }
            return visual.GetComponentInChildren<MeshRenderer>(true);
        }

        private static void BuildTreePart(GameObject parent, string name, Mesh mesh, Color color,
            Vector3 localPos, string matName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = matName };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat; // inline -> serializes into the scene, no asset churn
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        private static void WireChopVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host ChopVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<ChopVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<ChopVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            // CHANGE 1 (86caa4c5c) — the verify capture drives the chop via the LEFT-CLICK seam (the chop is no
            // longer proximity-auto), so it needs the ChopTree to RequestChopClick once at the tree. Wired here
            // (BuildChopTree created the ChopTree just before this call) so it serializes; an Awake
            // FindAnyObjectByType is the runtime fallback.
            cap.chop = Object.FindObjectOfType<ChopTree>();
            // REWORK 86caf9u5t — the verify capture now also LOOTS the dropped log pile (E), so it needs the
            // player's PickableLooter (BuildPickableLooter added it earlier on the player). Serialized here; an
            // Awake FindAnyObjectByType is the runtime fallback.
            cap.looter = Object.FindObjectOfType<PickableLooter>();
            cap.craftSpot = CraftSpotPosition;
            cap.treeSpot = ChopTreePosition;
            // 86cafdevx AC3 — fail LOUD at bootstrap (CI step 1) on a dropped capture-gate dep rather than
            // letting the Awake FindAnyObjectByType fallback mask it into the 20-min capture gate.
            if (cap.player == null)
                Debug.LogError("[MovementCameraScene] ChopVerifyCapture.player wiring is null — the player has " +
                               "no ClickToMove (BuildPlayer must run before WireChopVerifyCapture)");
            if (cap.inventory == null)
                Debug.LogError("[MovementCameraScene] ChopVerifyCapture.inventory wiring is null — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            if (cap.chop == null)
                Debug.LogError("[MovementCameraScene] ChopVerifyCapture.chop wiring is null — BuildChopTree must " +
                               "author the ChopTree before WireChopVerifyCapture (the left-click chop seam, 86caa4c5c)");
            EditorUtility.SetDirty(bootGo);
        }

        // World position of the wired berry bush (86caa5zz3). Near the loop centre but off the craft
        // spot (8,6) / tree (-9,-7) / fire (4,-8) so the foraging beat reads as its own spot. Inside the
        // walkable extent + on the NavMesh; the capture/PlayMode can drive the player here to harvest.
        public static readonly Vector3 BerryBushPosition = new Vector3(-6f, 0f, 7f);

        // Berry-bush greens + berry red (86caa5zz3) — the same leafy-blob language as the world bushes
        // (LowPolyZoneGen.Bush*), so the wired bush matches the scatter bushes. Baked into vertex colour.
        private static readonly Color BushBody   = new Color(0.26f, 0.52f, 0.22f);
        private static readonly Color BushTop    = new Color(0.44f, 0.70f, 0.30f);
        private static readonly Color BushShadow = new Color(0.15f, 0.35f, 0.15f);
        private static readonly Color BerryRed   = new Color(0.78f, 0.16f, 0.22f);

        // World position of the wired SNAKE enemy (Combat POC 86cah7xxp). Near the loop centre, clear of the
        // craft spot (8,6), axe (3,2), chop tree (-9,-7), berry bush (-6,7), pond (7,-3), stick (-3,-4) — a
        // deterministic combat target the PlayMode/shipped-build capture walks up to. A DETERMINISTIC scene-
        // author ADD on the flat player-loop ground — OUTSIDE the seeded LowPolyZoneGen stream, so it provably
        // cannot perturb the seed-42 island/scatter/NavMesh (the seed lock is honoured by construction).
        public static readonly Vector3 SnakePosition = new Vector3(5f, 0f, 4f);

        // THE REAL SNAKE's warm banded palette (86caaz4vn AC1 — the findable serpent; the #224 green
        // bush-blob placeholder is retired). CONTRASTING warm rust + dark bands vs the green bushes/grass,
        // material-honest (a banded warm-brown serpent, not an arbitrary tint); saturated (chroma > 0.1 —
        // clear of the near-neutral pink-cast quantizer class, and these are direct colours anyway).
        private static readonly Color SnakeRust = new Color(0.78f, 0.38f, 0.16f); // warm rust-orange band
        private static readonly Color SnakeDark = new Color(0.34f, 0.18f, 0.10f); // dark brown band
        private static readonly Color SnakeHeadCol = new Color(0.85f, 0.45f, 0.18f); // head: a touch brighter
        private static readonly Color SnakeEye = new Color(0.06f, 0.05f, 0.04f);  // near-black eye facets

        // The serpent's build dimensions (AC1: ~1.5–2m nose-to-tail, distinct head + segmented body).
        private const int SnakeBodyLinks = 12;          // body links behind the head
        private const float SnakeLinkLength = 0.16f;    // baked link length (overlaps spacing → continuous)
        private const float SnakeLinkSpacing = 0.14f;   // chain arc spacing (SnakeBodyChain.segmentSpacing)
        private const float SnakeNeckRadius = 0.115f;   // first link half-extent (head neck matches)
        private const float SnakeTailRadius = 0.045f;   // last link half-extent (the taper target)
        private const float SnakeHeadLength = 0.26f;    // nose-to-neck

        // The FIRST combat build (Combat POC 86cah7xxp): player HP + needs-gated regen + tiered death + the
        // left-click melee attack on the player root, and ONE damageable snake. Wires the HUD HP bar (AC9) +
        // the SettingsPanel per-tier combat rows (AC8b). Authored editor-time so it all SERIALIZES into
        // Boot.unity (editor-vs-runtime trap). The snake is a collider-free marker (no NavMesh/raycast impact).
        private static void BuildCombat(GameObject player, int groundLayer)
        {
            // --- PLAYER HP (AC1) on the player ROOT (the transform carrying the NavMeshAgent — DeathHandler
            //     warps it on respawn). Per-tier maxes/damage default to Medium; the difficulty preset drives
            //     the active tier via ApplyDifficulty / the settings rows. ---
            var health = player.GetComponent<FarHorizon.Combat.Health>();
            if (health == null) health = player.AddComponent<FarHorizon.Combat.Health>();
            health.max = 100f;
            health.startFull = true;
            health.resistance = FarHorizon.Combat.ResistanceProfile.Neutral; // the player has no type weakness

            // The player's own status controller (AC6 — an enemy bite's bleed applies here; bleed works both ways).
            var playerStatus = player.GetComponent<FarHorizon.Combat.StatusEffectController>();
            if (playerStatus == null) playerStatus = player.AddComponent<FarHorizon.Combat.StatusEffectController>();
            playerStatus.health = health;

            // --- NEEDS-GATED REGEN (AC3) — reads warmth/hunger/thirst (never writes), heals HP while satisfied. ---
            var regen = player.GetComponent<FarHorizon.Combat.HealthRegen>();
            if (regen == null) regen = player.AddComponent<FarHorizon.Combat.HealthRegen>();
            regen.health = health;
            regen.warmth = Object.FindObjectOfType<WarmthNeed>();
            regen.hunger = Object.FindObjectOfType<HungerNeed>();
            regen.thirst = Object.FindObjectOfType<ThirstNeed>();
            if (regen.warmth == null || regen.hunger == null || regen.thirst == null)
                Debug.LogWarning("[MovementCameraScene] BuildCombat: a need is unwired on HealthRegen (warmth=" +
                    (regen.warmth != null) + " hunger=" + (regen.hunger != null) + " thirst=" + (regen.thirst != null) +
                    ") — BootstrapProject must add the Survival needs before MovementCameraScene.Author");

            // --- TIERED DEATH (AC2) — reuse the campfire as respawn + the Inventory for the hard-tier drop. ---
            var death = player.GetComponent<FarHorizon.Combat.DeathHandler>();
            if (death == null) death = player.AddComponent<FarHorizon.Combat.DeathHandler>();
            death.health = health;
            death.playerRoot = player.transform;
            death.campfire = Object.FindObjectOfType<Campfire>();   // reuse the campfire as respawn (no checkpoint)
            death.inventory = Object.FindObjectOfType<Inventory>(); // reuse the inventory for the drop
            death.tier = SurvivalNeed.DifficultyTier.Medium;        // default tier; the preset/settings drive it

            // --- LEFT-CLICK MELEE ATTACK (AC5) — swings the SELECTED belt weapon (axe/spear) at the nearest
            //     in-reach enemy. The swing is the PLACEHOLDER (existing chop Attack state) pending the AC5
            //     procedural-vs-Mixamo Sponsor decision. ---
            var attack = player.GetComponent<FarHorizon.Combat.MeleeAttack>();
            if (attack == null) attack = player.AddComponent<FarHorizon.Combat.MeleeAttack>();
            attack.player = player.transform;
            attack.inventory = Object.FindObjectOfType<Inventory>();
            attack.character = Object.FindObjectOfType<CastawayCharacter>();
            attack.inventoryUI = Object.FindObjectOfType<InventoryUI>();

            // --- THE SNAKE — the REAL serpent (86caaz4vn): findable banded body + wander/aggro AI +
            //     telegraphed lunge bite, on the 86cah7xxp shared Health surface. ---
            BuildSnake(player, groundLayer);

            // --- THE SPEAR PICKUP (AC4) — the second contrasting craftable weapon's acquisition. The player
            //     walks up to acquire the spear onto the belt, then cycles-selects it to feel the long-reach
            //     pierce contrast vs the axe's medium-reach slash. ---
            BuildSpearPickup(player);

            // --- Wire the HP bar (AC9) onto the SurvivalHud + the per-tier combat rows (AC8b) onto the panel. ---
            var hud = Object.FindObjectOfType<SurvivalHud>();
            if (hud != null) hud.health = health;
            else Debug.LogWarning("[MovementCameraScene] BuildCombat: no SurvivalHud to wire the HP bar (AC9)");

            var panel = Object.FindObjectOfType<SettingsPanel>();
            if (panel != null)
            {
                panel.combatHealth = health;
                panel.combatRegen = regen;
                panel.combatDeath = death;
            }

            Debug.Log("[MovementCameraScene] authored Combat POC (player HP + regen + tiered death + melee " +
                      "attack + 1 snake; HUD HP wired=" + (hud != null) + ", panel wired=" + (panel != null) + ")");
        }

        // A wired damageable SNAKE (AC7): a low-poly coiled body proxy (a placeholder for the snake POC
        // 86caaz4vn art) + Health + a pierce-weak ResistanceProfile + a StatusEffectController (so a weapon's
        // bleed applies to it). Collider-free — the player walks up + left-clicks to attack; never blocks the
        // ground raycast or the NavMesh bake. Authored editor-time (serializes into Boot.unity).
        private static void BuildSnake(GameObject player, int groundLayer)
        {
            var snake = new GameObject("Snake");
            snake.transform.position = SnakePosition;
            // Face AWAY from the player spawn (0,0,6) initially so the authored layout reads as an animal
            // mid-patrol, not a sentry staring at spawn. The chain re-lays the body at runtime.
            snake.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, 0f, -0.8f), Vector3.up);

            // === THE SERPENT BODY (86caaz4vn AC1 — head + tapered banded links, ~1.9m nose-to-tail). ===
            // Segments are CHILDREN with editor-baked meshes (they serialize into Boot.unity — the
            // Awake-built-hierarchies-don't-serialize trap); SnakeBodyChain only MOVES them at runtime.
            // Laid out along local -Z behind the head so the SAVED scene already reads as a full snake.
            var segs = new System.Collections.Generic.List<Transform>();

            var headGo = BuildSnakePart(snake, "SnakeHead",
                LowPolyMeshes.SnakeHead(SnakeNeckRadius, SnakeHeadLength, SnakeHeadCol, SnakeEye, 61404),
                Vector3.zero);
            segs.Add(headGo.transform);

            for (int i = 0; i < SnakeBodyLinks; i++)
            {
                float t0 = i / (float)SnakeBodyLinks;
                float t1 = (i + 1) / (float)SnakeBodyLinks;
                float rBack = Mathf.Lerp(SnakeNeckRadius, SnakeTailRadius, t1); // toward the tail
                float rFront = Mathf.Lerp(SnakeNeckRadius, SnakeTailRadius, t0);
                // ALTERNATING warm bands — the banded contrast read (rust / dark / rust / ...).
                Color band = (i % 2 == 0) ? SnakeRust : SnakeDark;
                var link = BuildSnakePart(snake, "SnakeLink" + i.ToString("00"),
                    LowPolyMeshes.SnakeLink(rBack, rFront, SnakeLinkLength, band, 61410 + i),
                    new Vector3(0f, 0f, -SnakeLinkSpacing * (i + 1)));
                segs.Add(link.transform);
            }

            // === The 86cah7xxp shared combat surface (UNCHANGED — this ticket builds ON it, AC4/AC5). ===
            var health = snake.AddComponent<FarHorizon.Combat.Health>();
            health.max = FarHorizon.Combat.SnakeEnemy.SnakeMaxHp;
            health.startFull = true;
            // The pierce-WEAK profile (AC8a) — a spear beats the soft-bodied snake; neutral to slash/blunt.
            health.resistance = new FarHorizon.Combat.ResistanceProfile
            {
                slashMul = 1f,
                pierceMul = FarHorizon.Combat.SnakeEnemy.SnakePierceWeakness,
                bluntMul = 1f,
            };

            var status = snake.AddComponent<FarHorizon.Combat.StatusEffectController>();
            status.health = health;

            var enemy = snake.AddComponent<FarHorizon.Combat.SnakeEnemy>();
            enemy.biteBleed = FarHorizon.Combat.StatusEffectSpec.MakeBleed(1.5f, 3f); // a light enemy→player bleed
            enemy.easyBiteDamage = FarHorizon.Combat.SnakeEnemy.SnakeEasyBiteDamage;   // AC4 per-tier map
            enemy.medBiteDamage = FarHorizon.Combat.SnakeEnemy.SnakeMedBiteDamage;
            enemy.hardBiteDamage = FarHorizon.Combat.SnakeEnemy.SnakeHardBiteDamage;

            // === NavMesh movement (AC2 — the island NavMesh; WorldBootstrap.BakeNavMesh covers it). ===
            var agent = snake.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.25f;
            agent.height = 0.4f;
            agent.speed = 0.9f;               // SnakeAI drives per-state speed
            agent.acceleration = 8f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.15f;
            agent.autoBraking = true;
            agent.baseOffset = 0f;            // the VISUAL grounds via the chain's terrain snap, not the agent Y

            // === The AI + the body chain (AC2/AC3) — the snake's OWN drivers; the player's Animator →
            //     CastawayArmPose → HeldAxeRig chain is untouched by construction. ===
            var ai = snake.AddComponent<FarHorizon.Combat.SnakeAI>();
            ai.player = player.transform;
            ai.playerHealth = player.GetComponent<FarHorizon.Combat.Health>();
            ai.deathHandler = player.GetComponent<FarHorizon.Combat.DeathHandler>(); // the live tier surface (AC4)

            var chain = snake.AddComponent<FarHorizon.Combat.SnakeBodyChain>();
            chain.ai = ai;
            chain.segments = segs.ToArray();
            chain.segmentSpacing = SnakeLinkSpacing;
            chain.groundMask = 1 << groundLayer; // the SAME Ground layer the player's own snap raycasts

            Debug.Log("[MovementCameraScene] authored REAL Snake (86caaz4vn) at " + SnakePosition +
                      ": head+" + SnakeBodyLinks + " banded links (~" +
                      (SnakeHeadLength + SnakeLinkSpacing * SnakeBodyLinks).ToString("0.00") +
                      "m), wander/aggro AI + telegraphed lunge, pierce-weak HP=" + health.max);
        }

        // One snake segment child: baked mesh + MeshRenderer with the inline vertex-colour material
        // (serializes into the scene — the BuildBlobCanopyPart idiom, snake-named so the material trace
        // reads correctly in the Frame Debugger). Collider-free: MeleeAttack targets by Health distance,
        // the chain raycasts only the Ground mask, and the NavMesh bake never sees the snake.
        private static GameObject BuildSnakePart(GameObject parent, string name, Mesh mesh, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                var mat = new Material(vc) { name = "SnakeMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white);
                mr.sharedMaterial = mat; // inline -> serializes into the scene (no .mat churn)
                EnsureShaderAlwaysIncluded(vc);
            }
            else
            {
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(litShader) { name = "SnakeMat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", SnakeRust);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
                Debug.LogWarning("[MovementCameraScene] vertex-color shader not found; snake flat-rust fallback");
            }
            return go;
        }

        // World position of the wired SPEAR pickup (Combat POC 86cah7xxp AC4). A DETERMINISTIC scene-author ADD
        // OUTSIDE the seeded LowPolyZoneGen stream.
        //
        // REGRESSION FIX (PR #224 review — the chop-capture-gate red on run 28539711263): the ORIGINAL (2,0,6)
        // sat EXACTLY pickupRadius (2.0u planar) from the player spawn (0,0,6) — Vector2.Distance = 2.0, and
        // SpearPickup.Update's guard is `> pickupRadius` (2.0 > 2.0 = false), so the PROXIMITY-AUTO pickup fired
        // on frame 1 at spawn. That landed the spear in belt slot 0 (AddToolToBelt → first free slot) — which is
        // the DEFAULT-SELECTED slot (_selectedBelt=0). The later axe craft then landed in slot 1, so the SELECTED
        // slot held the SPEAR → Inventory.IsAxeSelectedInBelt was FALSE → the chop gate (ShouldChopOnClick needs
        // axeSelected) never passed → no chop → no fell → no wood → the chop-capture gate failed. Chopping code
        // is byte-identical to main; ONLY the scene author changed. Fix: place the spear CLEAR of the spawn (≥5u,
        // mirroring AxePickup at (3,0,2) = 5.0u from spawn) so slot 0 is free when the axe crafts → the axe lands
        // in slot 0 = the selected slot → IsAxeSelectedInBelt holds → chop works. (4,0,9): 5.0u from spawn (0,6)
        // AND from craft (8,6), 5.1u from the snake (5,4), ≥7u from every other loop spot — within GroundHalf=30.
        public static readonly Vector3 SpearPickupPosition = new Vector3(4f, 0f, 9f);

        // A wired SPEAR pickup (AC4). 86cajkk7h: the polished in-house STONE spear (wpn_spear_stone_01) now
        // EXISTS, so the world pickup is the REAL weapon mesh (shared Mat_WeaponPalette) — matching the world
        // axe pickup (both real FBXs), retiring the old shaft+tip primitive proxy. Laid near-horizontal on the
        // ground so it reads as a dropped spear; the player walks up to acquire it onto the belt. Collider-free
        // — never blocks the ground raycast or the NavMesh bake. Authored editor-time (serializes into Boot.unity).
        private static void BuildSpearPickup(GameObject player)
        {
            var spear = new GameObject("SpearPickup");
            spear.transform.position = SpearPickupPosition;

            // The in-house stone spear FBX (family-normalized, blade up +Z → Unity +Y). Instantiate under the
            // pickup root and lay it near-horizontal (rotate the +Y long axis toward the ground) so it reads as
            // a spear resting on the beach, then apply the shared palette material.
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.SpearFbxPath);
            if (fbx != null)
            {
                var mesh = Object.Instantiate(fbx);
                mesh.name = "SpearMesh";
                mesh.transform.SetParent(spear.transform, false);
                mesh.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                mesh.transform.localRotation = Quaternion.Euler(0f, 0f, 78f); // near-horizontal lean (matches the old proxy)
                ApplyWeaponPaletteMaterial(mesh);
                var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader != null) EnsureShaderAlwaysIncluded(unlitShader);
            }
            else
            {
                Debug.LogError("[MovementCameraScene] stone spear FBX not found at " + WeaponPackAssetGen.SpearFbxPath +
                               " — run WeaponPackAssetGen.PrepareWeaponPack() before authoring the scene; no spear pickup mesh");
            }

            var pickup = spear.AddComponent<FarHorizon.Combat.SpearPickup>();
            pickup.inventory = Object.FindObjectOfType<Inventory>();
            pickup.player = player.transform;
            pickup.visual = spear.transform;
            if (pickup.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory to wire SpearPickup to — BootstrapProject " +
                               "must add the Survival Inventory before MovementCameraScene.Author");

            Debug.Log("[MovementCameraScene] authored SpearPickup at " + SpearPickupPosition +
                      " (inventory wired: " + (pickup.inventory != null) + ")");
        }

        // A wired BERRY BUSH (86caa5zz3): a squat leafy blob dome (BushBlob) with a child "Berries" mesh
        // (small red faceted spheres) + a BerryBush component (harvest+regrow+eat-bridge) wired to the
        // scene Inventory + player. The castaway walks up (no tool — the stone-pickup idiom, not the
        // axe-gated chop) and harvests berries into the inventory; berries regrow after a tweakable delay
        // (the bush persists). Authored editor-time so the visual + the wired component SERIALIZE into
        // Boot.unity (editor-vs-runtime trap). NO collider — the player walks up to harvest; never blocks
        // the ground raycast or the NavMesh bake (built BEFORE the bake, collider-free).
        private static void BuildBerryBush(GameObject player, int groundLayer)
        {
            var bush = new GameObject("BerryBush");
            bush.transform.position = BerryBushPosition;

            // Body: a squat leafy blob dome (the same idiom as the world scatter bushes / tree canopies).
            const float bushR = 0.95f;
            BuildBlobCanopyPart(bush, "BushBody",
                LowPolyMeshes.BushBlob(bushR, 5, BushBody, BushTop, BushShadow, 53117), Vector3.zero);

            // Berries: a SEPARATE child so BerryBush toggles JUST the berries on harvest/regrow (the bush
            // body persists). MANY small dense red faceted spheres STUDDING the dome (vertex-colour red —
            // same material) so the deterministic capture bush reads as berries, not flowers (#101 soak-fix).
            var berries = new GameObject("Berries");
            berries.transform.SetParent(bush.transform, false);
            berries.transform.localPosition = Vector3.zero;
            BuildBlobCanopyPart(berries, "BerriesMesh",
                LowPolyMeshes.BerryCluster(bushR, 26, BerryRed, 53119), Vector3.zero);

            var bb = bush.AddComponent<BerryBush>();
            bb.hasBerries = true;
            bb.berriesVisual = berries.transform;
            bb.player = player.transform;
            bb.inventory = Object.FindObjectOfType<Inventory>();
            // Wire the HungerNeed editor-time (serialized) so the no-arg EatBerry() never does a per-use
            // FindObjectOfType in the build (BootstrapProject adds Survival/HungerNeed before this runs).
            bb.hunger = Object.FindObjectOfType<HungerNeed>();
            bb.regrowSeed = 53121; // deterministic regrow roll so headless/capture behavior is stable
            if (bb.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire BerryBush to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            // 86cafdevx AC3 — the hunger ref is the eat-bridge (EatBerry decrements hunger). A null silently
            // ships a bush whose berries don't feed; fail LOUD at bootstrap (CI step 1) instead.
            if (bb.hunger == null)
                Debug.LogError("[MovementCameraScene] no HungerNeed in scene to wire BerryBush.hunger to — " +
                               "BootstrapProject must add the Survival HungerNeed before MovementCameraScene.Author");

            Debug.Log("[MovementCameraScene] authored BerryBush at " + BerryBushPosition +
                      " (inventory wired: " + (bb.inventory != null) + ", berries visual wired: " +
                      (bb.berriesVisual != null) + ")");
        }

        // World position of the wired fallen STICK (86caa96rd). Near the loop centre, clear of the craft
        // spot (8,6), axe (3,2), chop tree (-9,-7), berry bush (-6,7), pond (7,-3) — a deterministic loot
        // target the PlayMode/shipped-build capture walks up to. A DETERMINISTIC scene-author ADD on the flat
        // player-loop ground — OUTSIDE the seeded LowPolyZoneGen generation stream, so it provably CANNOT
        // perturb the seed-42 island silhouette / scatter / NavMesh (the seed lock is honoured by construction).
        public static readonly Vector3 WiredStickPosition = new Vector3(-3f, 0f, -4f);

        // Warm dry dead-wood brown (LowPolyZoneGen.StickCol) — the wired stick matches the scatter sticks.
        private static readonly Color StickCol = new Color(0.46f, 0.34f, 0.22f);

        // A wired fallen STICK (86caa96rd): a thin few-sided tapered cylinder laid HORIZONTAL on the ground +
        // a StickProp component (IPickable — looted on E for 1 wood) wired to the scene Inventory. The
        // castaway walks up and presses E to loot it: 1 wood into the inventory, the stick consumed (the
        // low-yield wood source vs chopping a tree). A RELIABLE fixed-position stick (vs the random scatter
        // ones) so the PlayMode/capture has a deterministic loot target. Authored editor-time so the mesh +
        // the wired StickProp.inventory SERIALIZE into Boot.unity (editor-vs-runtime trap). NO collider — the
        // player walks up to loot; built BEFORE the NavMesh bake (collider-free, never blocks the bake).
        private static void BuildWiredStick(GameObject player, int groundLayer)
        {
            var stick = new GameObject("WiredStick");
            stick.transform.position = WiredStickPosition;
            // Lay the +Y-built shaft HORIZONTAL (90° about Z = points +X), with a small fixed yaw + lift so it
            // rests ON the ground (deterministic — the capture sees the same stick every run).
            stick.transform.rotation = Quaternion.Euler(0f, 35f, 0f) * Quaternion.Euler(0f, 0f, 90f);
            stick.transform.position = WiredStickPosition + Vector3.up * 0.05f;

            // The shaft: a thin tapered cylinder, flat-shaded warm dead-wood brown (the BuildTreePart inline
            // URP/Lit helper — serializes into the scene, no .mat churn).
            BuildTreePart(stick, "StickMesh", LowPolyMeshes.TaperedCylinder(0.05f, 0.03f, 1.2f, 5),
                StickCol, Vector3.zero, "WiredStickMat");

            var sp = stick.AddComponent<StickProp>();
            sp.inventory = Object.FindObjectOfType<Inventory>();
            if (sp.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire StickProp to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            Debug.Log("[MovementCameraScene] authored WiredStick at " + WiredStickPosition +
                      " (inventory wired: " + (sp.inventory != null) + ", yields " +
                      StickProp.WoodPerStickDefault + " wood on E)");
        }

        // World position of the wired small STONE (86caa4c96). Near the loop centre, clear of the craft spot
        // (8,6), axe (3,2), chop tree (-9,-7), berry bush (-6,7), wired stick (-3,-4), pond (7,-3), fire
        // (4,-8) — a deterministic loot target the PlayMode/shipped-build capture walks up to. A DETERMINISTIC
        // scene-author ADD on the flat player-loop ground — OUTSIDE the seeded LowPolyZoneGen generation
        // stream, so it provably CANNOT perturb the seed-42 island silhouette / scatter / NavMesh.
        public static readonly Vector3 WiredStonePosition = new Vector3(0f, 0f, -5f);

        // Warm stone-grey (LowPolyZoneGen.RockCol) — the wired stone matches the scatter stones.
        private static readonly Color StoneCol = new Color(0.62f, 0.60f, 0.555f);

        // A wired small STONE (86caa4c96): a small faceted chunk resting ON the ground + a StoneProp component
        // (IPickable — looted on E for 1 stone, then RESPAWNS on the shared StoneRespawner window) wired to the
        // scene Inventory + the shared respawner. The castaway walks up and presses E to loot it: 1 stone into
        // the inventory, the spot empties + respawns (the small-stone gather; bigger boulders = future
        // pickaxe-mining, OOS). A RELIABLE fixed-position stone (vs the random scatter ones) so the PlayMode/
        // capture has a deterministic loot target. The stone MESH lives in a CHILD ("StoneMesh") so StoneProp
        // toggles JUST the visual on loot while its respawn timer keeps running (deactivating the whole GO
        // would freeze the timer — the BerryBush precedent). Authored editor-time so the mesh + the wired
        // StoneProp refs SERIALIZE into Boot.unity (editor-vs-runtime trap). NO collider — the player walks up
        // to loot; built BEFORE the NavMesh bake (collider-free, never blocks the bake).
        private static void BuildWiredStone(GameObject player, int groundLayer)
        {
            var stone = new GameObject("WiredStone");
            stone.transform.position = WiredStonePosition + Vector3.up * 0.04f;
            stone.transform.rotation = Quaternion.Euler(6f, 40f, 4f); // deterministic yaw + gentle tilt
            stone.transform.localScale = Vector3.one * 0.7f;          // a small stone (< the boulders)

            // The visual in a CHILD so StoneProp.stoneVisual toggles it on loot/respawn without deactivating
            // the parent (which would freeze the respawn timer). FacetedRock (small base radius — a pebble),
            // flat-shaded warm stone-grey via the shared vertex-color shader tinted to StoneCol.
            var visual = new GameObject("StoneMesh");
            visual.transform.SetParent(stone.transform, false);
            var vmf = visual.AddComponent<MeshFilter>();
            vmf.sharedMesh = LowPolyMeshes.FacetedRock(0.22f, 0.34f, 86099);
            var vmr = visual.AddComponent<MeshRenderer>();
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc != null)
            {
                var m = new Material(vc) { name = "WiredStoneMat" };
                if (m.HasProperty("_Tint")) m.SetColor("_Tint", StoneCol);
                vmr.sharedMaterial = m;
            }

            // F3 FIX (86caa4c96 / Devon REQUEST_CHANGES): do NOT author a StoneRespawner here. BuildWiredStone
            // runs at BuildBootScene — BEFORE WorldBootstrap.BuildEnvironment authors the scatter root + its
            // StoneRespawner — so a FindObjectOfType here is null and the prior code added a SECOND respawner on
            // the player that bound nothing (the dead-knob root cause). The shared respawner is now resolved
            // POST-scatter by WireStoneScatterRoot (BootstrapProject, after BuildEnvironment), which canonicalises
            // to the ONE scatter-bound respawner and wires THIS StoneProp + the SettingsPanel to it. Leave
            // sp.respawner null here; WireStoneScatterRoot sets it once the canonical instance exists.
            var sp = stone.AddComponent<StoneProp>();
            sp.inventory = Object.FindObjectOfType<Inventory>();
            sp.stoneVisual = visual.transform;
            sp.respawnSeed = 86097; // deterministic respawn roll so headless/capture behavior is stable
            if (sp.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire StoneProp to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            Debug.Log("[MovementCameraScene] authored WiredStone at " + WiredStonePosition +
                      " (inventory wired: " + (sp.inventory != null) + ", respawner wired POST-scatter by " +
                      "WireStoneScatterRoot, yields " + StoneProp.StonePerPickupDefault + " stone on E)");
        }

        // ============================================================================================
        // 86cakkmr0 (I-2 of the iron chain) — iron-ore NODES + the active-left-click MINE verb.
        // ============================================================================================

        // The world position of the stone-PICKAXE pickup (I-2 soak-enabler). On the way from spawn toward the ore
        // field; CLEAR of spawn (0,6) by MORE than pickupRadius (2.0u) so it can NEVER auto-grab belt slot 0 at
        // spawn (the PR #224 chop-capture regression class — guarded by MineSceneTests). Clear of the axe (3,2)
        // + craft (8,6) too. A DETERMINISTIC scene-author ADD — outside the seeded LowPolyZoneGen stream.
        public static readonly Vector3 PickaxePickupPosition = new Vector3(6f, 0f, 2f);

        // The authored ore-node POOL size = the Easy preset's node count (24 — the largest preset), so the
        // ore-rarity dial can enable up to Easy-many live; Medium (14, the default) + Hard (8) enable fewer.
        // A named source (not a magic literal) mirroring the difficulty-preset data.
        private static int OreNodePoolSize => IronDifficultyPresets.Easy.OreNodeCount;

        // Ore-node materials (shared across the whole pool — 2 material instances of ONE shader = SRP-batched,
        // unity6-mastery §2). GREY faceted rock body + a RUSTY iron-ore vein (pattern via geometry, NOT texture —
        // Bar 3 [[weapon-asset-material-honest-pattern-via-geometry]]). default rusty tint — Sponsor-soak tunes.
        private static readonly Color OreRockGrey = new Color(0.50f, 0.48f, 0.45f); // warm stone grey
        private static readonly Color OreVeinRust = new Color(0.44f, 0.25f, 0.18f); // rusty iron-ore red-brown

        // Author the iron-ore node POOL (I-2). REAL-WORLD ANCHOR: an iron-ore node is a chunky ROCK OUTCROP rising
        // UP out of the ground with rusty ore veins in it (a bump UP, not a hole) — the side-profile capture must
        // read as an outcrop sticking up. A deterministic SEEDED scatter places OreNodePoolSize nodes organically
        // (Bar 1 — no grid) in an annulus around the loop, avoiding the landmarks. The MineOre manager reads the
        // pool + enables the first ActiveNodeCount (the ore-rarity dial). The OrePileSpawner mints an OrePile per
        // broken node. Authored editor-time so the pool + MineOre/OrePileSpawner refs SERIALIZE into Boot.unity
        // (editor-vs-runtime trap). Collider-free — the player walks up to mine; no NavMesh/raycast impact.
        private static void BuildOreNodes(GameObject player, int groundLayer)
        {
            // Shared materials for the whole pool (2 instances of the LowPolyVertexColor shader → SRP-batched).
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material rockMat = null, veinMat = null;
            if (vc != null)
            {
                rockMat = new Material(vc) { name = "OreRockMat" };
                if (rockMat.HasProperty("_Tint")) rockMat.SetColor("_Tint", OreRockGrey);
                veinMat = new Material(vc) { name = "OreVeinMat" };
                if (veinMat.HasProperty("_Tint")) veinMat.SetColor("_Tint", OreVeinRust);
            }

            // The pool root — MineOre.nodeRoot points here; the OreNode children are the pool.
            var root = new GameObject("OreNodes");
            root.transform.position = Vector3.zero;

            // Deterministic seeded organic placement (Bar 1). An annulus band around the loop centre, rejecting
            // positions too close to a landmark or another node. OUTSIDE the seeded LowPolyZoneGen stream (a scene-
            // author ADD, like the pond/stick/stone) so it provably cannot perturb the seed-42 island/scatter.
            var landmarks = new Vector3[]
            {
                new Vector3(0f, 0f, 6f),   // spawn
                CraftSpotPosition,          // craft (8,6)
                ChopTreePosition,           // tree (-9,-7)
                BerryBushPosition,          // bush (-6,7)
                WiredStickPosition,         // stick (-3,-4)
                WiredStonePosition,         // stone (0,-5)
                PondPosition,               // pond (7,-3)
                new Vector3(4f, 0f, -8f),   // campfire
                new Vector3(3f, 0f, 2f),    // axe pickup
                PickaxePickupPosition,      // pickaxe pickup (6,2)
                ForgeSpotPosition,          // forge/furnace build spot (I-3) — keep ore nodes off the furnace
            };
            var placed = new System.Collections.Generic.List<Vector3>();
            var rng = new System.Random(86201); // deterministic — the pool is byte-identical every bootstrap
            int target = OreNodePoolSize;
            int guard = 0;
            while (placed.Count < target && guard < 8000)
            {
                guard++;
                // Annulus 9..17u from origin — findable without heavy exploration (Q2), and inside the PROVEN-
                // walkable loop zone (the campfire r≈9, craft r≈10, chop tree r≈11.4 are all reachable on the
                // NavMesh, per ChopVerifyCapture), so the nodes land on walkable ground for the mine soak + capture.
                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                double rad = 9.0 + rng.NextDouble() * 8.0;
                float x = (float)(System.Math.Cos(ang) * rad);
                float z = (float)(System.Math.Sin(ang) * rad);
                var p = new Vector3(x, 0f, z);
                bool tooClose = false;
                foreach (var lm in landmarks)
                    if (PlanarDistXZ(p, lm) < 3.5f) { tooClose = true; break; }
                if (!tooClose)
                    foreach (var q in placed)
                        if (PlanarDistXZ(p, q) < 3.0f) { tooClose = true; break; } // spaced apart (organic, not clumped)
                if (tooClose) continue;
                placed.Add(p);
            }

            for (int i = 0; i < placed.Count; i++)
                BuildOreNodeVisual(root.transform, placed[i], 86300 + i * 17, rockMat, veinMat);

            // The OrePileSpawner (the ore-drop factory + the `ore yield` / `ore-pile despawn` host). Its looter ref
            // is back-wired LATER in BuildPickableLooter (the looter doesn't exist yet). oreMaterial = the shared
            // rock material so a spawned pile reads as the same ore.
            var spawnerGo = new GameObject("OrePileSpawner");
            var orePileSpawner = spawnerGo.AddComponent<OrePileSpawner>();
            if (rockMat != null) orePileSpawner.oreMaterial = rockMat;

            // The MineOre manager (the mine verb + node resolver). Wired to the inventory/player/character/UI/
            // spawner/nodeRoot editor-time so it ships in Boot.unity (the Awake FindObjectOfType is the fallback).
            var mineGo = new GameObject("MineOre");
            var mine = mineGo.AddComponent<MineOre>();
            mine.player = player.transform;
            mine.inventory = Object.FindObjectOfType<Inventory>();
            mine.character = Object.FindObjectOfType<CastawayCharacter>();
            mine.inventoryUI = Object.FindObjectOfType<InventoryUI>();
            mine.orePileSpawner = orePileSpawner;
            mine.nodeRoot = root.transform;
            mine.strikesToBreak = MineOre.StrikesToBreakDefault;
            mine.mineRadius = 2.2f;
            mine.activeNodeCount = IronDifficultyPresets.Medium.OreNodeCount; // the Medium default (14 of the 24 pool)
            mine.regrowSeed = 86311; // deterministic regrow rolls for headless/capture stability
            if (mine.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire MineOre to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            if (mine.character == null)
                Debug.LogWarning("[MovementCameraScene] no CastawayCharacter to wire MineOre.character to — " +
                                 "the mine will land strikes but the arm won't swing (BuildPlayer before BuildOreNodes)");

            EditorUtility.SetDirty(mineGo);
            EditorUtility.SetDirty(spawnerGo);
            EditorUtility.SetDirty(root);

            // Wire the SETTINGS PANEL's mineOre ref now that MineOre exists (BuildSettingsPanel ran before this, so
            // its serialized ref was not yet resolvable). `iron ore rarity` flips LIVE bound to MineOre.ActiveNodeCount.
            var settingsPanel = Object.FindObjectOfType<SettingsPanel>();
            if (settingsPanel != null)
            {
                settingsPanel.mineOre = mine;
                EditorUtility.SetDirty(settingsPanel);
            }

            Debug.Log("[MovementCameraScene] authored " + placed.Count + " ore nodes (pool=" + OreNodePoolSize +
                      ", active=" + mine.activeNodeCount + "; inventory wired: " + (mine.inventory != null) + ")");
        }

        // One ore-node visual: a chunky GREY faceted ROCK outcrop rising UP from the ground (half-embedded so it
        // reads as an outcrop, not a floating boulder) with a couple of RUSTY ore-vein facets clustered on its
        // upper surface (pattern via geometry — Bar 3). Each is a GameObject named MineOre.OreNodeName under the
        // pool root; MineOre discovers + drives it. Collider-free.
        private static void BuildOreNodeVisual(Transform parent, Vector3 groundPos, int seed,
                                               Material rockMat, Material veinMat)
        {
            var node = new GameObject(MineOre.OreNodeName);
            node.transform.SetParent(parent, false);
            // A small deterministic yaw so the pool doesn't read as identical rocks (organic — Bar 1).
            var rng = new System.Random(seed);
            float yaw = (float)(rng.NextDouble() * 360.0);
            const float rockRadius = 0.58f;
            // Lift so the rock base sits at ~ground and the outcrop rises UP (half-embedded — a bump UP, not a hole).
            node.transform.position = groundPos + Vector3.up * (rockRadius * 0.55f);
            node.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Rock body — a chunky faceted boulder (grey), flat-shaded via the shared LowPolyVertexColor material.
            var body = new GameObject("OreRock");
            body.transform.SetParent(node.transform, false);
            var bmf = body.AddComponent<MeshFilter>();
            bmf.sharedMesh = LowPolyMeshes.FacetedRock(rockRadius, 0.42f, seed);
            var bmr = body.AddComponent<MeshRenderer>();
            if (rockMat != null) bmr.sharedMaterial = rockMat;

            // Ore veins — 3 small rusty faceted lumps clustered on the upper surface (partially embedded), so the
            // node reads as iron ORE in rock (Bar 3). Deterministic positions per node seed.
            var veins = new GameObject("OreVeins");
            veins.transform.SetParent(node.transform, false);
            int veinCount = 3;
            for (int v = 0; v < veinCount; v++)
            {
                float va = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                float vr = rockRadius * 0.55f;
                var vpos = new Vector3(Mathf.Cos(va) * vr, rockRadius * (0.35f + 0.35f * (float)rng.NextDouble()),
                                       Mathf.Sin(va) * vr);
                var vein = new GameObject("Vein" + v);
                vein.transform.SetParent(veins.transform, false);
                vein.transform.localPosition = vpos;
                var vmf = vein.AddComponent<MeshFilter>();
                vmf.sharedMesh = LowPolyMeshes.FacetedRock(0.15f + 0.05f * (float)rng.NextDouble(), 0.5f, seed + 991 + v);
                var vmr = vein.AddComponent<MeshRenderer>();
                if (veinMat != null) vmr.sharedMaterial = veinMat;
            }
        }

        private static float PlanarDistXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // === BOULDER pool (ticket 86camz9v7 / crafting-redesign ② — boulder mining) ===

        // The authored boulder POOL size — a modest DISCRETE pool of the VOLUME stone source (each yields ~5 stone +
        // regrows), NOT a rarity-dialed cloud like the ore nodes. A named source (not a magic literal).
        private const int BoulderPoolSize = 7;

        // Boulder material — the PROVEN warm-light STONE grey (matches the shipped scatter-rock RockCol
        // 0.62/0.60/0.555, LowPolyZoneGen.cs:92) so the boulder reads as the SAME warm-light stone family + does NOT
        // silhouette dark/cool under the Zone-D fog (the 86ca8m5zu dark-rock reject lesson — a side-profile capture at
        // 0.52 read too dark). The FacetedRock vertex-value contrast multiplies onto this _Tint. default — Sponsor-soak tunes.
        private static readonly Color BoulderStoneGrey = new Color(0.62f, 0.60f, 0.555f);

        // Author the boulder POOL (②). REAL-WORLD ANCHOR: a boulder is a LARGE chunky ROCK resting ON the ground —
        // a bump UP, half-embedded (you can walk up to it), NOT a hole. A deterministic SEEDED scatter places
        // BoulderPoolSize boulders organically (Bar 1 — no grid) in the walkable loop annulus, avoiding the
        // landmarks AND the ore nodes (so the two mineable pools read distinct). MineBoulder reads the pool; the
        // StonePileSpawner mints a StonePile per broken boulder. Authored editor-time so the pool + MineBoulder/
        // StonePileSpawner refs SERIALIZE into Boot.unity (editor-vs-runtime trap). A DISCRETE scene-author ADD
        // OUTSIDE the seeded LowPolyZoneGen scatter stream — it provably cannot perturb the seed-42 island/scatter
        // ([[world-is-big-round-island]]). Collider-free — the player walks up to mine; no NavMesh/raycast impact.
        private static void BuildBoulders(GameObject player, int groundLayer)
        {
            // Shared material for the whole pool (ONE instance of the LowPolyVertexColor shader → SRP-batched).
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            Material boulderMat = null;
            if (vc != null)
            {
                boulderMat = new Material(vc) { name = "BoulderStoneMat" };
                if (boulderMat.HasProperty("_Tint")) boulderMat.SetColor("_Tint", BoulderStoneGrey);
            }

            // The pool root — MineBoulder.boulderRoot points here; the Boulder children are the pool.
            var root = new GameObject("Boulders");
            root.transform.position = Vector3.zero;

            // Landmarks + the ore nodes to avoid (boulders are a DISTINCT mineable pool from the ore nodes).
            var landmarks = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(0f, 0f, 6f),   // spawn
                CraftSpotPosition,          // craft
                ChopTreePosition,           // tree
                BerryBushPosition,          // bush
                WiredStickPosition,         // stick
                WiredStonePosition,         // stone
                PondPosition,               // pond
                new Vector3(4f, 0f, -8f),   // campfire
                AxePickupPosition,          // axe pickup
                PickaxePickupPosition,      // pickaxe pickup
                ForgeSpotPosition,          // forge/furnace build spot
            };
            // Avoid the ore nodes too (they were authored just before us into "OreNodes").
            var oreRoot = GameObject.Find("OreNodes");
            if (oreRoot != null)
                foreach (var t in oreRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == MineOre.OreNodeName) landmarks.Add(t.position);

            var placed = new System.Collections.Generic.List<Vector3>();
            var rng = new System.Random(91442); // deterministic — the pool is byte-identical every bootstrap
            int guard = 0;
            while (placed.Count < BoulderPoolSize && guard < 12000)
            {
                guard++;
                // Same walkable loop annulus (9..17u) the ore nodes use — findable without heavy exploration and on
                // the proven-walkable NavMesh loop (for the mine soak + capture).
                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                double rad = 9.0 + rng.NextDouble() * 8.0;
                float x = (float)(System.Math.Cos(ang) * rad);
                float z = (float)(System.Math.Sin(ang) * rad);
                var p = new Vector3(x, 0f, z);
                bool tooClose = false;
                foreach (var lm in landmarks)
                    if (PlanarDistXZ(p, lm) < 4.0f) { tooClose = true; break; } // a boulder is big — keep clearance
                if (!tooClose)
                    foreach (var q in placed)
                        if (PlanarDistXZ(p, q) < 4.5f) { tooClose = true; break; } // spaced apart (organic, not clumped)
                if (tooClose) continue;
                placed.Add(p);
            }

            for (int i = 0; i < placed.Count; i++)
                BuildBoulderVisual(root.transform, placed[i], 91500 + i * 23, boulderMat);

            // The StonePileSpawner (the stone-drop factory + the `stone yield` / `stone-pile despawn` host). Its
            // looter ref is back-wired LATER in BuildPickableLooter (the looter doesn't exist yet). stoneMaterial =
            // the shared boulder material so a spawned pile reads as the same stone.
            var spawnerGo = new GameObject("StonePileSpawner");
            var stonePileSpawner = spawnerGo.AddComponent<StonePileSpawner>();
            if (boulderMat != null) stonePileSpawner.stoneMaterial = boulderMat;

            // The MineBoulder manager (the boulder-mine verb + resolver). Wired editor-time so it ships in Boot.unity
            // (the Awake FindObjectOfType is the fallback).
            var mineGo = new GameObject("MineBoulder");
            var mine = mineGo.AddComponent<MineBoulder>();
            mine.player = player.transform;
            mine.inventory = Object.FindObjectOfType<Inventory>();
            mine.character = Object.FindObjectOfType<CastawayCharacter>();
            mine.inventoryUI = Object.FindObjectOfType<InventoryUI>();
            mine.stonePileSpawner = stonePileSpawner;
            mine.boulderRoot = root.transform;
            mine.strikesToBreak = MineBoulder.StrikesToBreakDefault;
            mine.mineRadius = 2.4f;
            mine.activeNodeCount = -1;   // ALL boulders active (a fixed discrete pool, no rarity dial)
            mine.regrowSeed = 91577;     // deterministic regrow rolls for headless/capture stability
            if (mine.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire MineBoulder to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            if (mine.character == null)
                Debug.LogWarning("[MovementCameraScene] no CastawayCharacter to wire MineBoulder.character to — " +
                                 "the mine will land strikes but the arm won't swing (BuildPlayer before BuildBoulders)");

            EditorUtility.SetDirty(mineGo);
            EditorUtility.SetDirty(spawnerGo);
            EditorUtility.SetDirty(root);

            Debug.Log("[MovementCameraScene] authored " + placed.Count + " boulders (pool=" + BoulderPoolSize +
                      "; inventory wired: " + (mine.inventory != null) + ")");
        }

        // One boulder visual: a LARGE chunky faceted STONE lump resting ON the ground (half-embedded so it reads as
        // a boulder sitting on the surface — a bump UP, not a floating rock or a hole). Named MineBoulder.BoulderNodeName
        // under the pool root; MineBoulder discovers + drives it. The body mesh is named "BoulderMesh" (NOT "RockMesh")
        // so the scatter-rock guards (RockBoulderSceneTests) don't pick it up. Collider-free.
        private static void BuildBoulderVisual(Transform parent, Vector3 groundPos, int seed, Material boulderMat)
        {
            var node = new GameObject(MineBoulder.BoulderNodeName);
            node.transform.SetParent(parent, false);
            var rng = new System.Random(seed);
            float yaw = (float)(rng.NextDouble() * 360.0);
            // A big boulder (radius ~1.05–1.35u — clearly larger than the 0.58u ore outcrops), half-embedded so the
            // base sits at ~ground and the chunk rises UP.
            float radius = 1.05f + 0.30f * (float)rng.NextDouble();
            node.transform.position = groundPos + Vector3.up * (radius * 0.45f);
            node.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var body = new GameObject("BoulderMesh");
            body.transform.SetParent(node.transform, false);
            var bmf = body.AddComponent<MeshFilter>();
            // FacetedRock — flat-shaded angular stone (per-face normals + baked value contrast) — the "reads as stone"
            // percept RockBoulderSceneTests guards; a bigger, chunkier jitter for a boulder read.
            bmf.sharedMesh = LowPolyMeshes.FacetedRock(radius, 0.40f, seed);
            var bmr = body.AddComponent<MeshRenderer>();
            if (boulderMat != null) bmr.sharedMaterial = boulderMat;
        }

        // Wire the shipped-build boulder-mine capture (drives wood-pick → boulder → break → stone pile → E-loot,
        // + a side-profile boulder shot) onto the Boot object — sibling of WireMineVerifyCapture. Inert unless
        // launched with -verifyBoulder.
        private static void WireBoulderVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host BoulderVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<BoulderVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<BoulderVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.mine = Object.FindObjectOfType<MineBoulder>();
            cap.looter = Object.FindObjectOfType<PickableLooter>();
            if (cap.player == null)
                Debug.LogError("[MovementCameraScene] BoulderVerifyCapture.player wiring is null — the player has no " +
                               "ClickToMove (BuildPlayer must run before WireBoulderVerifyCapture)");
            if (cap.mine == null)
                Debug.LogError("[MovementCameraScene] BoulderVerifyCapture.mine wiring is null — BuildBoulders must " +
                               "author the MineBoulder before WireBoulderVerifyCapture");
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the shipped-build PLACEMENT-obstacle capture (86catr49m) onto the Boot object — sibling of
        // WireBoulderVerifyCapture / WireChopVerifyCapture. Inert unless launched with -verifyPlacement, which
        // drives PlacementVerifyCapture: GREEN on clear ground, RED over a real seed-42 scatter TREE (navmesh
        // carve), and RED over a registered BOULDER (86catr49m the fix). Authored editor-time so the harness +
        // its serialized deps SHIP in Boot.unity — a component-in-source-but-not-in-scene harness NO-OPs the
        // -verifyPlacement verb AND HANGS the capture exe (the #302 inert-harness lesson).
        private static void WirePlacementVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host PlacementVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<PlacementVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<PlacementVerifyCapture>();
            cap.placement = Object.FindObjectOfType<CraftingTablePlacement>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            // Fail LOUD at bootstrap (CI step 1) on a dropped capture-gate dep rather than letting the Start
            // FindAnyObjectByType fallback mask it into the -verifyPlacement capture gate (the #162 masking class).
            if (cap.placement == null)
                Debug.LogError("[MovementCameraScene] PlacementVerifyCapture.placement wiring is null — " +
                               "BuildCraftingTable must author the CraftingTablePlacement before WirePlacementVerifyCapture");
            if (cap.player == null)
                Debug.LogError("[MovementCameraScene] PlacementVerifyCapture.player wiring is null — the player has " +
                               "no ClickToMove (BuildPlayer must run before WirePlacementVerifyCapture)");
            if (cap.inventory == null)
                Debug.LogError("[MovementCameraScene] PlacementVerifyCapture.inventory wiring is null — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            EditorUtility.SetDirty(bootGo);
        }

        // A wired stone-PICKAXE pickup (I-2 soak-enabler). Mirrors BuildSpearPickup: the real stone-pickaxe FBX
        // (I-1/#283) on the shared palette, laid on the ground, + a PickaxePickup component (grants pickaxe_stone
        // to the belt on proximity). Placed CLEAR of spawn so it can't auto-grab slot 0 (the PR #224 class).
        private static void BuildPickaxePickup(GameObject player)
        {
            var pick = new GameObject("PickaxePickup");
            pick.transform.position = PickaxePickupPosition;

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPackAssetGen.PickaxeStoneFbxPath);
            if (fbx != null)
            {
                var mesh = Object.Instantiate(fbx);
                mesh.name = "PickaxeMesh";
                mesh.transform.SetParent(pick.transform, false);
                mesh.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                mesh.transform.localRotation = Quaternion.Euler(0f, 0f, 78f); // near-horizontal lean (matches the spear pickup)
                ApplyWeaponPaletteMaterial(mesh);
                var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader != null) EnsureShaderAlwaysIncluded(unlitShader);
            }
            else
            {
                Debug.LogError("[MovementCameraScene] stone pickaxe FBX not found at " + WeaponPackAssetGen.PickaxeStoneFbxPath +
                               " — run WeaponPackAssetGen.PrepareWeaponPack() before authoring the scene; no pickaxe pickup mesh");
            }

            var pickup = pick.AddComponent<PickaxePickup>();
            pickup.inventory = Object.FindObjectOfType<Inventory>();
            pickup.player = player.transform;
            pickup.visual = pick.transform;
            if (pickup.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory to wire PickaxePickup to — BootstrapProject " +
                               "must add the Survival Inventory before MovementCameraScene.Author");

            Debug.Log("[MovementCameraScene] authored PickaxePickup at " + PickaxePickupPosition +
                      " (inventory wired: " + (pickup.inventory != null) + ")");
        }

        // Wire the verification-only shipped-build MINE capture (grants+selects a pickaxe, mines a node, loots the
        // ore pile) onto the Boot object — sibling of WireChopVerifyCapture. Inert unless launched with -verifyMine.
        private static void WireMineVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host MineVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<MineVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<MineVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.mine = Object.FindObjectOfType<MineOre>();
            cap.looter = Object.FindObjectOfType<PickableLooter>();
            cap.pickaxePickup = Object.FindObjectOfType<PickaxePickup>();
            if (cap.player == null)
                Debug.LogError("[MovementCameraScene] MineVerifyCapture.player wiring is null — the player has no " +
                               "ClickToMove (BuildPlayer must run before WireMineVerifyCapture)");
            if (cap.mine == null)
                Debug.LogError("[MovementCameraScene] MineVerifyCapture.mine wiring is null — BuildOreNodes must " +
                               "author the MineOre before WireMineVerifyCapture");
            EditorUtility.SetDirty(bootGo);
        }

        // The E-LOOT interactor (86caf7a6q): the PLAYER side of the shared E-loot surface. Pressing E loots the
        // nearest in-range IPickable (the berry bush; sticks/stones build on the same surface) into the
        // inventory. Authored editor-time onto the PLAYER so the component + its Inventory/player refs SERIALIZE
        // into Boot.unity (the component-in-source-but-not-in-scene trap) — NOT added at Awake. One looter on
        // the player; the bush/sticks/stones are the IPickables it discovers at runtime. PickableLooterSceneTests
        // guards the serialized presence + wiring.
        private static void BuildPickableLooter(GameObject player)
        {
            var looter = player.GetComponent<PickableLooter>();
            if (looter == null) looter = player.AddComponent<PickableLooter>();
            looter.player = player.transform;
            looter.inventory = Object.FindObjectOfType<Inventory>();
            looter.lootKey = KeyCode.E; // the universal loot key (DECISIONS 2026-06-27; letter key = Danish-safe)
            if (looter.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire PickableLooter to — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");

            // #165 FIX — back-wire this looter onto the LogPileSpawner so every runtime-spawned LogPile REGISTERS
            // itself for E-loot. The looter only auto-re-discovers on an EMPTY cache, and the live build always has
            // ≥1 serialized pickable (bush/stick/stone) → a spawned pile is NEVER found unless registered. Done
            // HERE (not in BuildChopTree, where the spawner is created) because BuildPickableLooter runs AFTER
            // BuildChopTree in Author, so the spawner exists by now while the looter did not exist at BuildChopTree.
            // A runtime FindObjectOfType is the spawner's own fallback; this serializes the ref so it ships in
            // Boot.unity (asserted by the #164 capture-gate wiring-guard CaptureGateDepsSceneTests).
            var logPileSpawner = Object.FindObjectOfType<LogPileSpawner>();
            if (logPileSpawner != null)
            {
                logPileSpawner.looter = looter;
                EditorUtility.SetDirty(logPileSpawner);
            }
            else
            {
                // Fail LOUD at bootstrap (CI step 1 console-error gate) on a dropped spawner — the #165 bug rode a
                // missing registration all the way to the soak; a dropped wiring must surface RED in CI, not the gate.
                Debug.LogError("[MovementCameraScene] no LogPileSpawner in scene to back-wire PickableLooter onto — " +
                               "BuildChopTree must author the LogPileSpawner before BuildPickableLooter; a spawned " +
                               "log pile would never be looted (#165)");
            }

            // 86cakkmr0 (I-2) — SAME #165 back-wire for the OrePileSpawner: every runtime-spawned OrePile must
            // register with this looter or it is never looted (the live build always has ≥1 serialized pickable).
            // BuildOreNodes ran BEFORE BuildPickableLooter in Author, so the spawner exists here.
            var orePileSpawner = Object.FindObjectOfType<OrePileSpawner>();
            if (orePileSpawner != null)
            {
                orePileSpawner.looter = looter;
                EditorUtility.SetDirty(orePileSpawner);
            }
            else
            {
                Debug.LogError("[MovementCameraScene] no OrePileSpawner in scene to back-wire PickableLooter onto — " +
                               "BuildOreNodes must author the OrePileSpawner before BuildPickableLooter; a spawned " +
                               "ore pile would never be looted (#165)");
            }

            // 86camz9v7 (②) — SAME #165 back-wire for the StonePileSpawner: every runtime-spawned StonePile must
            // register with this looter or it is never looted. BuildBoulders ran BEFORE BuildPickableLooter, so the
            // spawner exists here.
            var stonePileSpawner = Object.FindObjectOfType<StonePileSpawner>();
            if (stonePileSpawner != null)
            {
                stonePileSpawner.looter = looter;
                EditorUtility.SetDirty(stonePileSpawner);
            }
            else
            {
                Debug.LogError("[MovementCameraScene] no StonePileSpawner in scene to back-wire PickableLooter onto — " +
                               "BuildBoulders must author the StonePileSpawner before BuildPickableLooter; a spawned " +
                               "stone pile would never be looted (#165)");
            }

            // LOOT PROXIMITY PROMPT (86cafc6ud AC2/AC3): the "Press E to pick up {name}" tooltip, authored on
            // the SAME player GO next to the looter, its looter ref SERIALIZED (NOT an Awake add) so the prompt
            // ships in the scene. It reads the looter's NearestInRange (single source of truth) — the prompt and
            // the actual loot agree. LootPromptSceneTests guards the serialized presence + wiring.
            var prompt = player.GetComponent<LootPrompt>();
            if (prompt == null) prompt = player.AddComponent<LootPrompt>();
            prompt.looter = looter;          // serialized ref — the single source of truth the prompt reads
            prompt.lootKey = looter.lootKey; // name the same key the looter actually loots on (E)

            EditorUtility.SetDirty(player);
            Debug.Log("[MovementCameraScene] authored PickableLooter + LootPrompt on the player (E = loot; " +
                      "inventory wired: " + (looter.inventory != null) + ")");
        }

        // World position of the wired FRESHWATER POND (86caamkv7). Inland east of spawn (0,6); clear of the
        // bush (-6,7), tree (-9,-7), fire (4,-8), craft (8,6). Comfortably inside the GroundHalf=30 walkable
        // extent + on the NavMesh (collider-free; the player walks up to drink). A DETERMINISTIC scene-author
        // ADD on the flat player-loop ground — it is OUTSIDE the seeded LowPolyZoneGen generation stream
        // entirely (authored here, not in ScatterIslandProps), so it provably CANNOT perturb the seed-42
        // island silhouette / scatter / NavMesh (AC2a — the seed lock is honoured by construction).
        public static readonly Vector3 PondPosition = new Vector3(7f, 0f, -3f);

        // The pond water-surface sits just BELOW the flat ground (Y=0) so the bank reads as a shallow natural
        // depression the water fills (Uma §1e nestle-don't-stamp) and the depth-fade foam has a waterline to
        // ride. Shallow (the pond is a found pool, not a deep well). The grassy lip rim sits AT ground level.
        private const float PondSurfaceY = -0.06f;
        // WATER-FILLS-THE-BOWL-TO-A-THIN-LIP (ticket 86cadj4g7 #130 ROUND 9 — Sponsor round-8 soak "STILL a walkable
        // dry slope"). The round-8 even-grade wall left the waterline at only ~4.0u = ~0.74 of the 5.4u mouth → a
        // LONG GENTLE DRY SLOPE (4.0→5.4u) the Sponsor walked DOWN into the water. The ROUND-9 cause-fix is the
        // TWO-SEGMENT wall (LowPolyZoneGen.PondDepressionDelta): a submerged gentle lower bowl + a SHORT STEEP shore
        // lip, with the waterline now DEFINED as PondWaterlineFillFraction (0.90) × the bowl mouth = ≈ 4.86u. So the
        // dry band is just 5.4−4.86 = 0.54u — a thin steep lip you STEP OVER, not a slope you walk down.
        //
        // To fill the bowl to that ≈4.86u waterline on EVERY azimuth with the ±18% organic rim (PondRimFactor
        // 0.82–1.18) AND never leave a dry crescent on a min-radius lobe, the disc MIN reach must clear 4.86u:
        // 4.86/0.82 ≈ 5.93, so nominal 6.1u (MIN reach 6.1×0.82 = 5.00u — clears the waterline with margin; mean
        // 6.1u; MAX reach 6.1×1.18 = 7.20u, PAST the 5.4u bowl mouth). The overshoot past ≈4.86u is SUBMERGED in the
        // rising dry lip (lip terrain is ABOVE the −0.30u water surface there) and TERRAIN-OCCLUDED, so the VISIBLE
        // waterline lands exactly on the lip intersection (≈4.86u) — a clean organic shoreline by terrain-clipping,
        // a thin short lip, knee-deep right up to it. The disc Y (−0.30u rel plateau = the recess) is everywhere ≤
        // the terrain so the disc never pokes ABOVE ground (the dry lip + the past-mouth plateau both sit at/above
        // −0.30u). MAX reach 7.20u is well inside the sea-hole cut radius (≈11.6u) so no sea shows. (Was 5.0u for
        // the round-8 even-grade waterline at ~4.0u; the round-9 wall pushed the waterline out, so the disc followed.)
        private const float PondSurfaceRadius = 6.1f; // fills the carved bowl to the ≈4.86u lip waterline (0.90 of mouth); overshoot terrain-occluded

        // The grass-green the bank ACCENTS (tufts) read as (Uma §1e: the meadow-green language so the pool reads
        // FOUND, not stamped). The raised collar RING mesh was removed (#130 round 5 — it was the white-ring
        // source); the collar is now PAINTED into the terrain (LowPolyZoneGen.PondCollarGreen). This tint stays
        // for the dry-rim grass tufts that still nestle the pool.
        private static readonly Color PondBankGrass = new Color(0.30f, 0.52f, 0.24f); // matches the world meadow greens

        // A wired FRESHWATER POND (86caamkv7 / Uma §1): a small still freshwater pool the castaway walks up to
        // and DRINKS FROM HAND (proximity + interact, no tool, NOT an inventory item — distinct from the
        // berry-bush harvest). The water-surface rides the SAME LowPolyWater shader as the sea via the pond
        // SIBLING material (MakePondMaterial) with Uma's freshwater deltas (bluer/brighter/calm/tight-foam) so
        // it reads as DIFFERENT, drinkable water vs the salt sea. A grassy bank lip + a couple of rocks/grass
        // tufts nestle it (found, not placed). Authored editor-time so the pond mesh + bank + FreshwaterPond's
        // ThirstNeed/player refs SERIALIZE into Boot.unity (editor-vs-runtime trap). NO collider — the player
        // walks up to drink; never blocks the ground raycast or the NavMesh bake (built collider-free, BEFORE
        // the bake). FreshwaterPondSceneTests guards the serialized presence + the no-collider contract.
        private static void BuildFreshwaterPond(GameObject player, int groundLayer)
        {
            var pond = new GameObject("FreshwaterPond");
            pond.transform.position = PondPosition;

            // --- Water surface: the fresh-blue faceted disc on the shared LowPolyWater shader (Uma §1a-d) ---
            var water = new GameObject("PondWater");
            water.transform.SetParent(pond.transform, false);
            water.transform.localPosition = new Vector3(0f, PondSurfaceY, 0f);
            water.layer = 0; // not walkable / not NavMesh (no collider)
            var wmf = water.AddComponent<MeshFilter>();
            wmf.sharedMesh = LowPolyZoneGen.BuildPondWaterMesh(PondSurfaceRadius);
            var wmr = water.AddComponent<MeshRenderer>();
            wmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // a flat water sheet casts no shadow
            var pondMat = LowPolyZoneGen.MakePondMaterial(SettingsDir + "/PondWaterMat.mat");
            wmr.sharedMaterial = pondMat;
            if (pondMat != null && pondMat.shader != null) EnsureShaderAlwaysIncluded(pondMat.shader);

            // --- COLLAR: NO raised mesh (ticket 86cadj4g7 #130 ROUND 5 — Sponsor verbatim: "REMOVE the raised
            //     collar entirely — no bank ring mesh. Paint a FLAT darker-green vertex-color ring on the terrain
            //     around the pond instead — no raised geometry, no shadow lip, I should walk on the green at
            //     ground level"). The OLD raised PondBank ring mesh was the PROVEN white-shoreline-ring source
            //     (the #130 round-5 -verifyPondDiag, build e5207d1: toggle c "collar/bank REMOVED" made the pale
            //     ring VANISH while bloom-off, sea-off, and the foam-off water all left it present). Its draped
            //     bowl-wall facets read pale/washed under the warm key — exactly the white ring the Sponsor kept
            //     soaking. So the collar is now PAINTED into the terrain vertex colour (LowPolyZoneGen.IslandColorAt
            //     + PondCollarPaintWeight → PondCollarGreen), a FLAT darker-green ring at ground level with no
            //     geometry to catch light or cast a lip-shadow. The accents (tufts + rock) stay (they nestle the
            //     pool), draped on the carved bowl rim at terrain height.
            var vc = Shader.Find("FarHorizon/LowPolyVertexColor");

            // --- "Found" accents (Uma §1e nestle-don't-stamp): a couple of grass tufts + a small rock on the
            //     bank, in the existing scatter language, so the pool reads tended-by-nature, not built. A
            //     couple of clouds' worth of care; don't over-decorate. Deterministic placement (no RNG) so
            //     the capture is stable. Collider-free (the player walks up to drink).
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.4f;
                // Place the decorative tufts on the DRY shore lip — just inside PondBowlOuterRadius (5.1u), OUTSIDE
                // the waterline (≈4.86u, ROUND 9) — so they sit on the lip ABOVE the water, not submerged on the
                // wet lower wall (ticket 86cadj4g7 #130: the fill moved the waterline out; keep the accents dry).
                float rr = LowPolyZoneGen.PondBowlOuterRadius - 0.3f;
                var tuft = new GameObject("BankTuft" + i);
                tuft.transform.SetParent(pond.transform, false);
                // Nestle the tuft ON the carved bowl rim at its radius (NOT the old flat y=0, which after the
                // bowl drop would float above the sloping wall — ticket 86cadj4g7).
                tuft.transform.localPosition = new Vector3(Mathf.Cos(a) * rr, CollarOuterLocalY(rr), Mathf.Sin(a) * rr);
                var tmf = tuft.AddComponent<MeshFilter>();
                tmf.sharedMesh = LowPolyMeshes.GrassClump(0.6f, 5, 86010 + i);
                var tmr = tuft.AddComponent<MeshRenderer>();
                // WHITE-GRASS FIX (ticket 86cadj4g7, Sponsor #130 soak): GrassClump bakes NO vertex colours, so
                // the mesh vertex colour defaults to WHITE (1,1,1). On the LowPolyVertexColor shader albedo =
                // IN.color.rgb * _Tint.rgb, so a _Tint of white gave white×white = WHITE TUFTS (the defect). Tint
                // with the bank grass-green (PondBankGrass — the world meadow green these tufts must read as) so
                // albedo = white × green = green. (Sibling of the correctly-green scatter tufts, which use the
                // flat-colour URP/Lit path; here we keep the vertex-colour shader + supply the green via _Tint.)
                if (vc != null) { var m = new Material(vc) { name = "BankTuftMat" }; if (m.HasProperty("_Tint")) m.SetColor("_Tint", PondBankGrass); tmr.sharedMaterial = m; }
            }
            // One small rock nestled on the DRY bowl rim (just inside PondBowlOuterRadius, outside the waterline)
            // — sit it ON the carved bowl rim at its radius (not the old flat y=0; the bowl drop would otherwise
            // float it above the sloping wall, or submerge it on the wet wall if too close in — ticket 86cadj4g7).
            var rock = new GameObject("BankRock");
            rock.transform.SetParent(pond.transform, false);
            float rockRad = LowPolyZoneGen.PondBowlOuterRadius - 0.3f;
            float rockAng = Mathf.Atan2(0.6f, -(PondSurfaceRadius + 0.5f)); // keep the original near-bank direction
            float rockX = Mathf.Cos(rockAng) * rockRad, rockZ = Mathf.Sin(rockAng) * rockRad;
            rock.transform.localPosition = new Vector3(rockX, CollarOuterLocalY(rockRad), rockZ);
            var rmf = rock.AddComponent<MeshFilter>();
            rmf.sharedMesh = LowPolyMeshes.FacetedRock(0.55f, 0.35f, 86020);
            var rmr = rock.AddComponent<MeshRenderer>();
            if (vc != null) { var m = new Material(vc) { name = "BankRockMat" }; if (m.HasProperty("_Tint")) m.SetColor("_Tint", new Color(0.55f, 0.55f, 0.55f)); rmr.sharedMaterial = m; }

            // --- The FreshwaterPond: now an IPickable (E loots ONE water into the belt — 86cafc6vx AC1) PLUS the
            //     COVERAGE-ONLY proximity drink seam (DrinkScoop, no longer bound to any input — AC4). The
            //     ThirstNeed + player + INVENTORY refs are wired editor-time (serialized) so the build never
            //     relies on a per-use FindObjectOfType (BootstrapProject adds Survival/ThirstNeed + the Inventory
            //     before this runs). The player-side PickableLooter AUTO-DISCOVERS the pond at runtime (its Awake
            //     FindObjectsOfType scan over the serialized scene) — NO RegisterPickable call (that seam is
            //     RUNTIME-spawn-only, e.g. LogPile; the pond is serialized into Boot.unity — 86cafc6vx AC1). ---
            var fp = pond.AddComponent<FarHorizon.FreshwaterPond>();
            fp.player = player.transform;
            fp.thirst = Object.FindObjectOfType<ThirstNeed>();
            // The inventory the E-looted water lands in (the GET side — 86cafc6vx AC1). Serialized so the build
            // never relies on a per-use Find; CanLoot gates on this being wired (no inventory → not loot-able).
            fp.inventory = Object.FindObjectOfType<Inventory>();
            if (fp.inventory == null)
                Debug.LogWarning("[MovementCameraScene] no Inventory in scene to wire FreshwaterPond's E-loot to — " +
                                 "BootstrapProject must author the Inventory before MovementCameraScene.Author (the " +
                                 "pond is not loot-able without it; the thirst loop's GET side breaks)");
            // Drink-from-the-EDGE reach is keyed to the VISIBLE waterline (~PondWaterlineRadius ≈ 4.0u, where the
            // bowl wall meets the water surface), NOT the disc NOMINAL radius (5.0u, whose overshoot is submerged in
            // the bowl wall + terrain-occluded — invisible). Feeding the nominal disc radius would over-extend the
            // proximity gate to a spot the player can't see water at (ticket 86cadj4g7 #130 ROUND 8 — the disc grew
            // to fill the bowl, but the drink edge must follow the VISIBLE pool, not the buried disc rim).
            fp.pondSurfaceRadius = LowPolyZoneGen.PondWaterlineRadius;
            fp.drinkRadius = 2.0f;
            if (fp.thirst == null)
                Debug.LogWarning("[MovementCameraScene] no ThirstNeed in scene to wire FreshwaterPond to — " +
                                 "BootstrapProject must add the Survival ThirstNeed before MovementCameraScene.Author");

            // Wire the DrinkAction's pond ref now that the pond exists (BootstrapProject added the DrinkAction
            // component to the Survival object; the pond is authored here, AFTER that). Serialized so the build
            // never relies on a per-use FindObjectOfType.
            var drink = Object.FindObjectOfType<FarHorizon.DrinkAction>();
            if (drink != null) drink.pond = fp;
            else Debug.LogWarning("[MovementCameraScene] no DrinkAction in scene to wire the pond to — " +
                                  "BootstrapProject must add the Survival DrinkAction before MovementCameraScene.Author");

            Debug.Log("[MovementCameraScene] authored FreshwaterPond at " + PondPosition +
                      " (IPickable E-loot water; thirst wired: " + (fp.thirst != null) +
                      ", inventory wired: " + (fp.inventory != null) +
                      ", drinkAction wired: " + (drink != null) +
                      ", lootRange/effDrinkR: " + fp.EffectiveDrinkRadius.ToString("F1") + ")");
        }

        /// <summary>TEST SEAM (ticket 86cadj4g7 #130 ROUND 8): build the pond water mesh at the SHIPPED
        /// <see cref="PondSurfaceRadius"/> so the fill-to-rim EditMode guard reads the ACTUAL authored disc reach
        /// (not a literal that could drift out of sync). Keeps PondSurfaceRadius private while letting the guard
        /// assert the real shipped geometry. Build-only, no scene side effects.</summary>
        public static Mesh BuildShippedPondWaterMeshForTest() => LowPolyZoneGen.BuildPondWaterMesh(PondSurfaceRadius);

        // The KNEE-DEEP wade depth — the water surface sits this far ABOVE the carved bowl floor. Sourced from
        // the SHARED LowPolyZoneGen.PondWadeDepth (= WorldBootstrap.PondWaterDepthAboveFloor; GroundPondInBowl
        // positions the root by it). So the dry-rim accent placement knows where the floor is relative to the
        // (local) water surface, in lockstep with the carve + grounding.
        private const float PondKneeDeepDepth = LowPolyZoneGen.PondWadeDepth;

        /// <summary>
        /// The carved bowl-WALL local Y at planar radius <paramref name="rad"/> from the pond centre (ticket
        /// 86cadj4g7). The accent tufts + rock DRAPE on the carved bowl WALL — so this MUST mirror the actual
        /// carved terrain profile, LowPolyZoneGen.PondDepressionDelta. ROUND 9: that profile is now TWO-SEGMENT
        /// (a gentle submerged lower bowl + a short dry lip), NOT a single smoothstep — so we derive the wall Y
        /// DIRECTLY from PondDepressionDelta instead of re-deriving a (now-wrong) single-grade smoothstep here.
        /// The carve is radially symmetric inside the footprint (PondHillFlatten levels it), so sampling along +X
        /// from the pond centre gives the wall profile at any azimuth. Conversion to pond-root-local Y: the water
        /// SURFACE is at PondSurfaceY (root-local) and sits PondRecessKneeDeep below the plateau, so terrain at
        /// radius rad (delta below the plateau) maps to local Y = PondSurfaceY + (PondDepressionDelta + recess).
        /// At the floor that is PondSurfaceY − PondKneeDeepDepth (the old floorLocalY); at the mouth it is
        /// PondSurfaceY + recess (the plateau). Keeps the accents sitting on the carved wall at terrain height
        /// (no float, no submerge) under the new two-segment profile.
        /// </summary>
        private static float CollarOuterLocalY(float rad)
        {
            // Sample the ACTUAL carved profile (two-segment) at this radius along +X from the pond centre. The
            // footprint is radially symmetric (PondHillFlatten levels it), so azimuth doesn't matter.
            float deltaBelowPlateau = LowPolyZoneGen.PondDepressionDelta(
                LowPolyZoneGen.PondCenterX + rad, LowPolyZoneGen.PondCenterZ); // ≤ 0 (a downward carve)
            // Local Y: the water surface (PondSurfaceY) sits PondRecessKneeDeep below the plateau, so the plateau is
            // at PondSurfaceY + recess; the terrain at this radius is `deltaBelowPlateau` below the plateau.
            return PondSurfaceY + LowPolyZoneGen.PondRecessKneeDeep + deltaBelowPlateau;
        }

        // World position of the campfire fire-pit on the flat test ground (U2-4, 86ca8bdep). Distinct from
        // spawn (origin), the craft spot (8,6) and the tree (-9,-7) so the loop is a real journey: spawn ->
        // craft axe -> chop tree -> BUILD FIRE. Comfortably inside the GroundHalf=30 walkable extent + on
        // the NavMesh. CampfireVerifyCapture drives the player here (last) to prove the loop closes in the exe.
        public static readonly Vector3 FirePitPosition = new Vector3(4f, 0f, -8f);

        // The campfire (U2-4, 86ca8bdep): a HUMAN-SCALE fire pit — a ring of low-poly stones around stacked
        // logs with a warm flame, lit by a warm point Light into the Zone-D dusk (art board: the fire "sets
        // the bar the whole world must meet"; warm cohesive palette + human-scale landmark, ~knee-high, NOT a
        // bonfire monument). It ships UNLIT (flame hidden, light off); reaching the pit WITH WOOD builds +
        // lights it. NO collider on the pit so it never blocks the ground raycast or the NavMesh (the player
        // walks up to the fire). Campfire + CampfirePlacement refs are wired editor-time so they serialize
        // into Boot.unity (editor-vs-runtime trap). We RIDE the established Zone-D welded-smooth-shaded mesh
        // idiom (stones = faceted spheres, logs = tapered cylinders, flame = a cone) — not a fresh prop style.
        private static void BuildCampfire(GameObject player, int groundLayer)
        {
            var pit = new GameObject("Campfire");
            pit.transform.position = FirePitPosition;

            var visual = new GameObject("CampfireVisual");
            visual.transform.SetParent(pit.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // Palette (Zone-D, sub-1.0 HDR-safe). Stones: cool grey with tonal variation; logs: warm bark
            // (matches the chop-tree trunk so the wood READS as "the wood you chopped"); flame: saturated
            // warm orange accent (the controlled accent-for-life the art board calls for).
            Color stoneCol = new Color(0.50f, 0.52f, 0.50f); // cool low-poly stone grey
            Color logCol = new Color(0.42f, 0.30f, 0.19f);  // warm bark (== ChopTree trunk)
            Color flameCol = new Color(0.98f, 0.55f, 0.16f); // saturated ember-orange

            // --- ring of fire-stones (human-scale: ~0.22u stones in a ~0.7u ring, knee-high pit) ---
            const int stoneCount = 7;
            const float ringR = 0.62f;
            for (int i = 0; i < stoneCount; i++)
            {
                float a = i / (float)stoneCount * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * ringR, 0.10f, Mathf.Sin(a) * ringR);
                // Per-stone tonal jitter (quantized small, inline material — no asset churn).
                float j = (i % 3) * 0.03f;
                var col = new Color(stoneCol.r - j, stoneCol.g - j, stoneCol.b - j);
                BuildCampfirePart(visual, "Stone" + i,
                    LowPolyMeshes.FacetedSphere(0.20f + (i % 2) * 0.04f, 0, 0.35f, 4100 + i),
                    col, pos, 0.06f, "CampfireStoneMat" + i);
            }

            // --- two crossed logs in the pit (warm bark, the chopped wood) ---
            BuildCampfireLog(visual, "LogA", logCol, new Vector3(0f, 0.16f, 0f), new Vector3(0f, 0f, 18f), 25f);
            BuildCampfireLog(visual, "LogB", logCol, new Vector3(0f, 0.18f, 0f), new Vector3(0f, 90f, 18f), -25f);

            // --- the flame (a warm low-poly tongue) — its OWN child so Campfire can toggle it with lit ---
            var flameGo = new GameObject("Flame");
            flameGo.transform.SetParent(visual.transform, false);
            flameGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            BuildCampfirePart(flameGo, "FlameCone", LowPolyMeshes.Cone(0.22f, 0.55f, 7),
                flameCol, Vector3.zero, 0.0f, "CampfireFlameMat", emissive: true);
            // A small inner brighter flame for depth (warm yellow core).
            BuildCampfirePart(flameGo, "FlameCore", LowPolyMeshes.Cone(0.12f, 0.40f, 6),
                new Color(1f, 0.82f, 0.35f), new Vector3(0f, 0.04f, 0f), 0.0f, "CampfireFlameCoreMat", emissive: true);
            flameGo.SetActive(false); // ships UNLIT — Campfire shows it on Light()

            // --- the warm point Light (the glow into the Zone-D dusk) — disabled until lit ---
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(pit.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.66f, 0.32f); // warm firelight
            // Tuned DOWN from the first soak (intensity 3.2/range 9 blew out to a white orb, not a contained
            // fire — caught in the -verifyLoop loop_warm capture). A knee-high campfire casts a SOFT warm
            // pool, not a floodlight: lower intensity + tighter range read as "a real little fire" per the
            // art board (human-scale landmark, controlled warm accent), letting the emissive flame stay the
            // bright focal point rather than a bloom blob.
            fireLight.intensity = 1.5f;
            fireLight.range = 6f;
            fireLight.shadows = LightShadows.None; // thin: no shadow cost on the placeholder
            fireLight.enabled = false; // ships off — Campfire enables it on Light()

            // INVISIBLE-UNTIL-PLACED (⑤ 86camz9w7, spec §0.1): disable the campfire STRUCTURE renderers (stones +
            // logs) so there is NO pre-visible fire pit. Exclude the flame child (SetActive-driven by the lit state,
            // already off). Campfire.Build reveals them at the confirmed ghost pose.
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.transform.IsChildOf(flameGo.transform)) continue; // flame toggled by the lit state
                r.enabled = false;
            }

            // The no-build zone the campfire self-registers ONCE PLACED (#302 seam) so a later table/forge/campfire
            // placement ghost reads RED over it. Authored DISABLED — Campfire.Build enables it (OnEnable → Register).
            var obstacle = pit.AddComponent<PlacementObstacle>();
            obstacle.footprintRadius = 0.7f; // the fire-stone ring
            obstacle.enabled = false;

            // The Campfire component owns the placed + lit state + warmth restore.
            var fire = pit.AddComponent<Campfire>();
            fire.warmth = Object.FindObjectOfType<WarmthNeed>();
            fire.player = player.transform;
            fire.visual = visual.transform;
            fire.flameVisual = flameGo;
            fire.fireLight = fireLight;
            fire.placementObstacle = obstacle;
            if (fire.warmth == null)
                Debug.LogError("[MovementCameraScene] no WarmthNeed in scene to wire Campfire to — " +
                               "BootstrapProject must add the Survival WarmthNeed before MovementCameraScene.Author");

            // The placement GHOST — a translucent stone-ring + crossed-logs silhouette (reads footprint + facing),
            // a SEPARATE root the placement moves under the cursor. Hidden until the player enters placement.
            var ghostGo = new GameObject("CampfireGhost");
            ghostGo.transform.position = FirePitPosition;
            var ghostMat = EnsureCraftingGhostMat(); // the shared translucent unlit ghost material (tinted per-frame)
            for (int i = 0; i < stoneCount; i++)
            {
                float a = i / (float)stoneCount * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * ringR, 0.10f, Mathf.Sin(a) * ringR);
                AddCampfireGhostPart(ghostGo.transform, "GhostStone" + i,
                    LowPolyMeshes.FacetedSphere(0.20f + (i % 2) * 0.04f, 0, 0.35f, 4100 + i), pos, Quaternion.identity, ghostMat);
            }
            AddCampfireGhostPart(ghostGo.transform, "GhostLogA", LowPolyMeshes.TaperedCylinder(0.07f, 0.05f, 0.9f, 6),
                new Vector3(0f, 0.16f, 0f), Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(25f, 0f, 0f), ghostMat);
            AddCampfireGhostPart(ghostGo.transform, "GhostLogB", LowPolyMeshes.TaperedCylinder(0.07f, 0.05f, 0.9f, 6),
                new Vector3(0f, 0.18f, 0f), Quaternion.Euler(0f, 90f, 90f) * Quaternion.Euler(-25f, 0f, 0f), ghostMat);
            foreach (var r in ghostGo.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false; // shown only while placing

            // The rewritten CampfirePlacement: the wood+STONE place-to-build driver (⑤ — reuses the ① ghost flow).
            // Costs default 3 wood + 2 stone (NIT-3 — the vision's "stone AND wood"). groundMask = the world's Ground
            // layer so the cursor ray hits ground + the navmesh-availability gate is ON in the shipped world.
            var place = pit.AddComponent<CampfirePlacement>();
            place.inventory = Object.FindObjectOfType<Inventory>();
            place.campfire = fire;
            place.player = player.transform;
            place.warmth = fire.warmth;
            place.ghost = ghostGo.transform;
            place.groundMask = 1 << groundLayer;
            place.woodCost = CampfirePlacement.CampfireWoodCostDefault;   // 3
            place.stoneCost = CampfirePlacement.CampfireStoneCostDefault; // 2
            if (place.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire CampfirePlacement to");

            // Wire the verification-only shipped-build capture (-verifyLoop drives: grant mats -> PLACE the campfire
            // -> lit + warmth restored) onto the Boot object.
            WireCampfireVerifyCapture(player);

            EditorUtility.SetDirty(pit); EditorUtility.SetDirty(ghostGo);
            Debug.Log("[MovementCameraScene] authored Campfire (invisible-until-placed) + ghost + placement (" +
                      place.woodCost + "w+" + place.stoneCost + "s) at park " + FirePitPosition +
                      " (ships unlit + hidden; warmth wired: " + (fire.warmth != null) +
                      ", inventory wired: " + (place.inventory != null) + ")");
        }

        // One translucent campfire-ghost part (a ring stone / a crossed log): a MeshFilter+MeshRenderer with the
        // shared ghost material (the placement tints it green/red per-frame via a MaterialPropertyBlock). Sibling of
        // AddForgeGhostPart, +local rotation for the crossed logs. Collider-free set-dressing.
        private static void AddCampfireGhostPart(Transform parent, string name, Mesh mesh, Vector3 localPos,
            Quaternion localRot, Material ghostMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (ghostMat != null) mr.sharedMaterial = ghostMat;
        }

        // One log of the campfire: a tapered cylinder laid down (rotated) + tilted, warm bark inline material.
        private static void BuildCampfireLog(GameObject parent, string name, Color col, Vector3 localPos,
            Vector3 euler, float tiltX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            // Lay the cylinder on its side (rotate 90 on Z so its length runs along X) then yaw/tilt per euler.
            go.transform.localRotation = Quaternion.Euler(euler.x, euler.y, 90f) * Quaternion.Euler(tiltX, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = LowPolyMeshes.TaperedCylinder(0.07f, 0.05f, 0.9f, 6);
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = name + "Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.06f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        // Build one campfire part (stone / flame cone): a child with a welded smooth-shaded mesh + inline
        // URP/Lit material (serializes into the scene, no .mat churn). emissive=true makes the flame GLOW
        // (warm emission) so it reads as fire even before the point light, and survives the stripped build
        // (URP/Lit emission is built-in, no custom shader to strip).
        private static void BuildCampfirePart(GameObject parent, string name, Mesh mesh, Color color,
            Vector3 localPos, float smoothness, string matName, bool emissive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = matName };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (emissive)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", color * 1.15f); // warm glow, trimmed so bloom doesn't blow out
                }
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        // ---- 86cakkmvc (I-3): the FORGE / FURNACE — a NEW buildable, DISTINCT from the campfire ----
        // ANCHOR (the real-world referent this build must satisfy — physical-features rule): a furnace is a
        // SQUAT STONE CHAMBER standing ON the ground with a dark FIREBOX opening at the front and a VENT on top;
        // heat + glow come OUT the front opening (and the top vent) when it is working. It is NOT an open campfire
        // (no exposed flame tongues) — it is a closed masonry block. The build must read that side-on: a chunky
        // stone body sitting UP on the ground (not sunk), a dark mouth, a chimney above.
        public static readonly Vector3 ForgeSpotPosition = new Vector3(-7f, 0f, 0f);

        private static readonly Color ForgeStoneGrey  = new Color(0.46f, 0.47f, 0.48f); // cool masonry stone
        private static readonly Color ForgeStoneDark  = new Color(0.22f, 0.22f, 0.24f); // dark firebox mouth
        private static readonly Color ForgeGlowOrange = new Color(1.0f, 0.50f, 0.14f);  // warm smelt glow (emissive)

        // The FORGE place-to-build flow (86camz9vh ③ — REWRITE of the shipped fixed-spot proximity build). The
        // Sponsor rejected a pre-visible forge (86camyvzw), so the forge now uses the SAME invisible-until-placed
        // place-to-build flow as the ① table (spec §2). Authors, all editor-time so they SERIALIZE into Boot.unity
        // (editor-vs-runtime trap): (a) the REAL forge (stone-furnace mesh + glow/light), renderers DISABLED =
        // invisible until placed; (b) a translucent GHOST (body+vent silhouette) hidden until placement; (c) a
        // PlacementObstacle on the forge (disabled — Build enables it so the placed forge self-registers a no-build
        // zone, #302 seam); (d) the rewritten ForgePlacement driver wiring them + the Inventory/Forge/player refs.
        // ForgeSceneTests guards the serialized presence + invisible-until-placed + wiring.
        private static void BuildForge(GameObject player, int groundLayer)
        {
            var forgeGo = new GameObject("Forge");
            forgeGo.transform.position = ForgeSpotPosition; // parked here, INVISIBLE, until the player places it

            var visual = new GameObject("ForgeVisual");
            visual.transform.SetParent(forgeGo.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // --- body: a squat octagonal stone drum standing ON the ground (base at y=0, rises UP) ---
            BuildCampfirePart(visual, "ForgeBody", LowPolyMeshes.TaperedCylinder(0.74f, 0.60f, 1.05f, 8),
                ForgeStoneGrey, Vector3.zero, 0.05f, "ForgeBodyMat");

            // --- top vent / chimney: a narrower dark stone stub on top (heat escapes here too) ---
            BuildCampfirePart(visual, "ForgeVent", LowPolyMeshes.TaperedCylinder(0.24f, 0.17f, 0.46f, 6),
                new Color(0.34f, 0.34f, 0.35f), new Vector3(0f, 1.05f, 0f), 0.05f, "ForgeVentMat");

            // --- firebox opening: a dark mouth panel PROUD of the +Z front face (the octagon's +Z vertex sits at
            //     ~0.70u at this height, so the panel front at ~0.75u reads as the cavity, not hidden inside the
            //     opaque body). Reads side-on as the dark firebox mouth (the anchor). ---
            BuildForgeBox(visual, "ForgeMouth", ForgeStoneDark,
                new Vector3(0f, 0.34f, 0.64f), new Vector3(0.42f, 0.36f, 0.22f), 0.02f, emissive: false);

            // --- the smelt GLOW: a warm emissive block over the mouth (front ~0.78u, just proud of the dark
            //     mouth panel so it reads as "the mouth lights up"), its OWN child so Forge toggles it with the
            //     smelting state (a furnace glows only while it is working — Bar 2 "life"). Ships OFF. ---
            var glowGo = new GameObject("ForgeGlow");
            glowGo.transform.SetParent(visual.transform, false);
            glowGo.transform.localPosition = Vector3.zero;
            BuildForgeBox(glowGo, "GlowBlock", ForgeGlowOrange,
                new Vector3(0f, 0.34f, 0.68f), new Vector3(0.30f, 0.26f, 0.20f), 0f, emissive: true);
            glowGo.SetActive(false); // ships cold — Forge shows it while smelting

            // INVISIBLE-UNTIL-PLACED (spec §2): disable the forge structure's renderers (NOT the glow — it is
            // smelt-state-driven + already off). Forge.Build reveals them at the confirmed ghost pose.
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.transform.IsChildOf(glowGo.transform)) continue; // glow toggled by the smelt state
                r.enabled = false;
            }

            // --- the warm point Light at the mouth (heat glow into the Zone-D look) — disabled until smelting ---
            var lightGo = new GameObject("ForgeLight");
            lightGo.transform.SetParent(forgeGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.5f, 0.6f);
            var forgeLight = lightGo.AddComponent<Light>();
            forgeLight.type = LightType.Point;
            forgeLight.color = new Color(1f, 0.6f, 0.28f);
            forgeLight.intensity = 2.0f;
            forgeLight.range = 5f;
            forgeLight.shadows = LightShadows.None; // no shadow cost (unity6-mastery §3 — no shadowed point lights)
            forgeLight.enabled = false;             // ships off — Forge enables it while smelting

            // (b) the placement GHOST — a translucent body+vent silhouette (enough to read footprint + facing),
            // a SEPARATE root the placement moves under the cursor. Hidden until the player enters placement.
            var ghostGo = new GameObject("ForgeGhost");
            ghostGo.transform.position = ForgeSpotPosition;
            var ghostMat = EnsureCraftingGhostMat(); // the shared translucent unlit ghost material (tinted per-frame)
            AddForgeGhostPart(ghostGo.transform, "GhostBody",
                LowPolyMeshes.TaperedCylinder(0.74f, 0.60f, 1.05f, 8), Vector3.zero, ghostMat);
            AddForgeGhostPart(ghostGo.transform, "GhostVent",
                LowPolyMeshes.TaperedCylinder(0.24f, 0.17f, 0.46f, 6), new Vector3(0f, 1.05f, 0f), ghostMat);
            foreach (var r in ghostGo.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = false; // shown only while placing

            // (c) the no-build zone the forge self-registers ONCE BUILT (#302 seam). Authored DISABLED — Forge.Build
            // enables it (OnEnable → Register), so an unbuilt/invisible forge never blocks placement.
            var obstacle = forgeGo.AddComponent<PlacementObstacle>();
            obstacle.footprintRadius = 0.9f; // a chunky stone furnace
            obstacle.enabled = false;

            // The Forge component: the built state + the smelt runtime. Smelt-cost dials seeded from the Medium
            // preset (the balanced default; the smelt_* settings flip these live via PopulateSmeltLive).
            var med = IronDifficultyPresets.Medium;
            var forge = forgeGo.AddComponent<Forge>();
            forge.inventory = Object.FindObjectOfType<Inventory>();
            forge.player = player.transform;
            forge.visual = visual.transform;
            forge.glowVisual = glowGo;
            forge.forgeLight = forgeLight;
            forge.placementObstacle = obstacle;
            forge.smeltRadius = 3.0f;
            forge.orePerIngot = med.OrePerIngot;
            forge.fuelPerSmelt = med.FuelPerSmelt;
            forge.smeltSeconds = med.SecondsPerSmelt;
            if (forge.inventory == null)
                Debug.LogError("[MovementCameraScene] no Inventory in scene to wire Forge to — BootstrapProject " +
                               "must add the Survival Inventory before MovementCameraScene.Author");

            // The rewritten ForgePlacement: the wood+STONE place-to-build driver (on the SAME GO as the Forge).
            // Costs default 6 wood + 12 stone (spec §5 — "forge >> weapons"). groundMask = the world's Ground layer
            // so the cursor ray hits ground + the navmesh-availability gate is ON in the shipped world.
            var place = forgeGo.AddComponent<ForgePlacement>();
            place.inventory = forge.inventory;
            place.forge = forge;
            place.player = player.transform;
            place.ghost = ghostGo.transform;
            place.groundMask = 1 << groundLayer;
            place.woodCost = ForgePlacement.ForgeWoodCostDefault;   // 6
            place.stoneCost = ForgePlacement.ForgeStoneCostDefault; // 12

            // Wire the SETTINGS PANEL's forge ref now that the Forge exists (BuildSettingsPanel ran earlier). The
            // three `smelt_*` rows flip LIVE bound to the Forge's smelt-cost fields (the second difficulty dial).
            var settingsPanel = Object.FindObjectOfType<SettingsPanel>();
            if (settingsPanel != null)
            {
                settingsPanel.forge = forge;
                EditorUtility.SetDirty(settingsPanel);
            }

            // Wire the verification-only shipped-build FORGE capture (-verifyForge drives place → build → smelt →
            // ingot) onto the Boot object.
            WireForgeVerifyCapture(player);

            EditorUtility.SetDirty(forgeGo); EditorUtility.SetDirty(ghostGo);
            Debug.Log("[MovementCameraScene] authored Forge (invisible-until-placed) + ghost + placement (" +
                      place.woodCost + "w+" + place.stoneCost + "s) at park " + ForgeSpotPosition +
                      " (ships cold; inventory wired: " + (forge.inventory != null) + "; smelt " + forge.orePerIngot +
                      " ore + " + forge.fuelPerSmelt + " fuel / " + forge.smeltSeconds.ToString("F0") + "s)");
        }

        // One translucent forge-ghost part: a MeshFilter+MeshRenderer with the shared ghost material (the
        // placement tints it green/red per-frame via a MaterialPropertyBlock). Collider-free set-dressing.
        private static void AddForgeGhostPart(Transform parent, string name, Mesh mesh, Vector3 localPos, Material ghostMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (ghostMat != null) mr.sharedMaterial = ghostMat;
        }

        // A blocky forge part (firebox mouth / glow block): a Cube primitive (collider stripped — set-dressing) with
        // an inline URP/Lit material, optionally emissive (the smelt glow survives the stripped build — URP/Lit
        // emission is built-in, no custom shader to strip). Sibling of BuildDebrisPlank's cube idiom.
        private static void BuildForgeBox(GameObject parent, string name, Color color, Vector3 localPos,
            Vector3 localScale, float smoothness, bool emissive)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // never blocks raycast/NavMesh
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mr = go.GetComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = name + "Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (emissive)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 1.4f);
                }
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        // Wire the verification-only shipped-build FORGE capture onto the Boot object — sibling of
        // WireMineVerifyCapture. Inert unless launched with -verifyForge.
        private static void WireForgeVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host ForgeVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<ForgeVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<ForgeVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.forge = Object.FindObjectOfType<Forge>();
            cap.placement = Object.FindObjectOfType<ForgePlacement>();
            if (cap.forge == null)
                Debug.LogError("[MovementCameraScene] ForgeVerifyCapture.forge wiring is null — BuildForge must " +
                               "author the Forge before WireForgeVerifyCapture");
            EditorUtility.SetDirty(bootGo);
        }

        // ---- M-U3-SCENE-4 (86ca8feuf): washed-ashore shipwreck debris ----
        // Centre of the debris scatter: on the beach just SEAWARD of the locked spawn (Z+6), slightly
        // LEFT of centre so it reads in the seaward orbit-cam's foreground WITHOUT sitting on the
        // spawn->craft (8,6) / chop (-9,-7) / fire (4,-8) loop path. Z-3 is on the warm sand band the
        // seaward gameplay view frames in its lower-foreground (between spawn and the waterline ~Z-10.5).
        public static readonly Vector3 BeachDebrisCenter = new Vector3(-3.2f, 0f, -3.0f);

        // Warm-brown weathered wood family — the axe-haft / chop-trunk / campfire-log palette
        // (style-guide-v2 §6; == ChopTree trunkCol / Campfire logCol). Sub-1.0, HDR-clamp-safe.
        private static readonly Color DebrisWood     = new Color(0.42f, 0.30f, 0.19f); // warm bark plank
        private static readonly Color DebrisWoodWorn = new Color(0.36f, 0.27f, 0.18f); // slightly greyed/weathered
        private static readonly Color DebrisCrate    = new Color(0.47f, 0.34f, 0.21f); // a touch lighter crate timber

        // A MODEST, tasteful shipwreck scatter. Chunky-cartoon faceted toy pieces (board v2): a few
        // weathered planks lying flat / askew + a half-buried crate + a barrel on its side. Purposeful,
        // not clutter (style-guide-v2 §5 "decoration serves the anchor"). NO colliders anywhere — pure
        // set-dressing; the player click-moves freely THROUGH the debris (AC2: must not block pathing or
        // the ground raycast). Built editor-time + serialized into Boot.unity (no Awake assembly).
        private static void BuildBeachDebris(int groundLayer)
        {
            var root = new GameObject("BeachDebris");
            root.transform.position = BeachDebrisCenter;
            // The debris sits on the Default layer (NOT Ground) so even if a future change adds a collider
            // by mistake, it wouldn't silently join the Ground raycast mask — but the real guarantee is
            // that no piece gets a collider at all (asserted by BeachDebrisSceneTests).

            // --- a few weathered planks: thin flat boxes lying on the sand at slight yaws + tilts, as if
            //     washed up and dropped. Local offsets keep them a loose, natural-looking pile. ---
            // (localPos, eulerYaw, lengthScale, tiltDeg, color)
            BuildDebrisPlank(root, "PlankA", new Vector3(0.0f, 0.06f, 0.0f),  18f, 1.9f,  2f, DebrisWood);
            BuildDebrisPlank(root, "PlankB", new Vector3(0.7f, 0.05f, 0.5f), -34f, 1.6f, -3f, DebrisWoodWorn);
            BuildDebrisPlank(root, "PlankC", new Vector3(-0.6f, 0.09f, -0.4f), 62f, 1.4f,  6f, DebrisWood);
            BuildDebrisPlank(root, "PlankD", new Vector3(0.2f, 0.14f, -0.7f),  -8f, 1.2f, 14f, DebrisWoodWorn);

            // --- a half-buried crate: a chunky cube tilted + sunk so it reads "dug into the wet sand". ---
            BuildDebrisCrate(root, "Crate", new Vector3(-1.5f, 0.12f, 0.7f), new Vector3(8f, 22f, -6f), 0.62f);

            // --- a barrel on its side: a stout tapered cylinder laid down, weathered, rolled to a stop. ---
            BuildDebrisBarrel(root, "Barrel", new Vector3(1.6f, 0.28f, -0.3f), 74f);

            Debug.Log("[MovementCameraScene] authored BeachDebris at " + BeachDebrisCenter +
                      " (planks+crate+barrel; NO colliders — non-blocking set-dressing)");
        }

        // A weathered plank: a thin flat box (primitive Cube, collider stripped) laid flat on the sand,
        // yawed + slightly tilted. Inline matte URP/Lit warm-brown material (serializes into the scene).
        private static void BuildDebrisPlank(GameObject parent, string name, Vector3 localPos,
            float yaw, float lengthScale, float tilt, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // set-dressing: never blocks raycast/NavMesh
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(tilt, yaw, 0f);
            // Long, narrow, thin: a board. lengthScale ~1.2-1.9u long, ~0.18u wide, ~0.06u thick.
            go.transform.localScale = new Vector3(lengthScale, 0.06f, 0.18f);
            ApplyDebrisMaterial(go, name + "Mat", col);
        }

        // A half-buried crate: a chunky cube, tilted + sunk into the sand (low Y + a downward tilt so the
        // far corner dips below the surface). Collider stripped.
        private static void BuildDebrisCrate(GameObject parent, string name, Vector3 localPos,
            Vector3 euler, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * size;
            ApplyDebrisMaterial(go, name + "Mat", DebrisCrate);
        }

        // A barrel on its side: a stout tapered cylinder laid down (rotated 90 on Z so its length runs
        // along X) + yawed, rolled to rest in the sand. Reuses the faceted low-poly cylinder idiom.
        private static void BuildDebrisBarrel(GameObject parent, string name, Vector3 localPos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 90f);
            var mf = go.AddComponent<MeshFilter>();
            // Slightly barrel-bellied: wider mid via near-equal end radii on a short stout body.
            mf.sharedMesh = LowPolyMeshes.TaperedCylinder(0.26f, 0.26f, 0.7f, 8);
            var mr = go.AddComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = name + "Mat" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", DebrisWoodWorn);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        // Inline matte URP/Lit material for a debris piece (warm-brown, low gloss — the faceted toy read,
        // not realistic driftwood). Serializes into the scene; no .mat asset churn.
        private static void ApplyDebrisMaterial(GameObject go, string matName, Color col)
        {
            var mr = go.GetComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                var mat = new Material(litShader) { name = matName };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                mr.sharedMaterial = mat;
                EnsureShaderAlwaysIncluded(litShader);
            }
        }

        private static void WireCampfireVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host CampfireVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<CampfireVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<CampfireVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            cap.inventory = Object.FindObjectOfType<Inventory>();
            cap.warmth = Object.FindObjectOfType<WarmthNeed>();
            cap.campfire = Object.FindObjectOfType<Campfire>();
            cap.placement = Object.FindObjectOfType<CampfirePlacement>();
            // 86cafdevx AC3 — fail LOUD at bootstrap (CI step 1) on a dropped -verifyLoop dep rather than
            // letting the Awake FindAnyObjectByType fallback mask it into the capture gate.
            if (cap.player == null)
                Debug.LogError("[MovementCameraScene] CampfireVerifyCapture.player wiring is null — the player " +
                               "has no ClickToMove (BuildPlayer must run before WireCampfireVerifyCapture)");
            if (cap.inventory == null)
                Debug.LogError("[MovementCameraScene] CampfireVerifyCapture.inventory wiring is null — " +
                               "BootstrapProject must add the Survival Inventory before MovementCameraScene.Author");
            if (cap.warmth == null)
                Debug.LogError("[MovementCameraScene] CampfireVerifyCapture.warmth wiring is null — " +
                               "BootstrapProject must add the Survival WarmthNeed before MovementCameraScene.Author");
            if (cap.campfire == null)
                Debug.LogError("[MovementCameraScene] CampfireVerifyCapture.campfire wiring is null — the lit " +
                               "campfire the -verifyLoop close-of-loop proof stands at was not authored");
            if (cap.placement == null)
                Debug.LogError("[MovementCameraScene] CampfireVerifyCapture.placement wiring is null — the " +
                               "CampfirePlacement the -verifyLoop place-to-build proof drives was not authored (⑤)");
            EditorUtility.SetDirty(bootGo);
        }

        // The player: NavMeshAgent + ClickToMove + the U6 castaway avatar (replaces the U3 capsule
        // placeholder). ClickToMove owns the agent; the camera follows the player root; the
        // CastawayCharacter self-drives its Idle<->Walk anim + facing off the agent's velocity.
        private static GameObject BuildPlayer(ClickMarker markerPrefab, int groundLayer)
        {
            var player = new GameObject("Player");
            // FIRST-FRAME TUNE (86ca8ce7j, absorbs pale-shore 86ca8a0u6): the castaway still "washes
            // ashore" (Sponsor-locked narrative — spawn near the shore), but the ORIGIN spawn sat on the
            // palest, emptiest sand band with the field+trees too far inland to frame, giving the
            // washed-out first frame. Nudge spawn a short way inland to the damp-sand→grass EDGE (Z+6):
            // still ashore (the shore/water is just behind), but now the warm grass band + the near-spawn
            // blob canopies are in the orbit camera's inland view. Stays comfortably on the NavMesh + the
            // loop spots (craft 8,6 / tree -9,-7 / fire 4,-8) remain a short click-walk away.
            player.transform.position = new Vector3(0f, 0f, 6f);

            var agent = player.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.speed = 5.5f;
            agent.angularSpeed = 999f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;

            // The real 3D player avatar — the "Mini Chibi Kid" sourced CC0-Attribution rigged low-poly
            // chunky-cartoon character (ticket 86ca8ca1m — SUPERSEDES the Quaternius Animated-Men base
            // the realistic head couldn't cartoon-ify). The avatar lives on a child root scaled to the
            // on-screen height; the FBX origin is at the feet (localPosition zero -> grounded feet on
            // the agent's ground point). Built editor-time (BuildInEditor) so the SkinnedMeshRenderer +
            // bones + controller reference SERIALIZE into Boot.unity (the editor-vs-runtime
            // serialization lesson — no Awake-assembled hierarchy to ship mangled). Ensure URP/Lit is
            // always-included so the toon materials don't strip to magenta in the stripped player.
            //
            // NO bone-scale dials: the chibi's big-head toy proportions are INTRINSIC to the mesh (the
            // PR #25 head/limb-scale path is dropped for this base — the mesh ships chunky as imported).
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null) EnsureShaderAlwaysIncluded(litShader);

            var avatarGo = new GameObject("CastawayAvatar");
            avatarGo.transform.SetParent(player.transform, false);
            avatarGo.transform.localPosition = Vector3.zero;
            // The FBX import is normalized to ~1u (spike-exact); scale the avatar root to the agent height
            // (1.8u) so the visible character matches the agent capsule + grounds correctly.
            avatarGo.transform.localScale = Vector3.one * PlayerVisualHeight;

            var castaway = avatarGo.AddComponent<CastawayCharacter>();
            castaway.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetGen.FbxPath);
            castaway.animatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterAssetGen.ControllerPath);
            if (castaway.modelPrefab == null)
                Debug.LogError("[MovementCameraScene] castaway FBX not found at " + CharacterAssetGen.FbxPath +
                               " — run CharacterAssetGen.PrepareCharacter() before authoring the scene");
            // GROUND-SNAP mask (86ca8rdkp soak-fix #1 — 'walking in the air'). The NavMeshAgent grounds the
            // player ROOT on the flat NavMesh collider, which rides ABOVE the dipping Zone-D visual terrain
            // (ground-trace: feet 0.081 vs visible sand 0.020 = a 6cm float). CastawayCharacter raycasts the
            // Ground layer each frame to plant the feet on the surface the player SEES. Wire the mask to the
            // Ground layer editor-time so it serializes (no Awake LayerMask string lookup in the build).
            castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            // RUN threshold (86ca9yq34 — run-on-Shift). The planar agent speed at/above which the character
            // reads as RUNNING (the Walk<->Run blend tree reads a full Run). Pinned to RunBlendSpeed so the
            // IsRunning flag + the blend tree agree, and serialized editor-time so it ships in Boot.unity.
            castaway.runSpeedThreshold = CharacterAssetGen.RunBlendSpeed;
            // JUMP IN-PLACE (86caaqhj5 — the "pulled back on landing" fix). The Mixamo jump clips bake the
            // forward travel into the HIPS BONE (not the root node), so lockRootPositionXZ can't strip it; the
            // lunge double-counts on the agent's real XZ then snaps back on landing. CastawayCharacter cancels
            // the Hips local-XZ to its grounded baseline while airborne so the jump plays vertical-only. Set the
            // enable editor-time so it ships in Boot.unity (the disabled-flag silent-killer family). The
            // Avatar_ShipsWithJumpInPlaceEnabled EditMode guard asserts this.
            castaway.jumpInPlace = true;
            // Build the Model child NOW (editor) so the skinned mesh + bones + Animator serialize into
            // Boot.unity (the editor-vs-runtime serialization lesson).
            castaway.BuildInEditor();

            // POST-ANIM ARM POSE (86ca8rdkp soak-fix #2 + #3): relax both arms away from the torso (the
            // pinched-idle fix) + give the RIGHT arm an away-from-body + raised carry pose for the held axe.
            // The arms are driven by the imported Mixamo clips, so this is an ADDITIVE LateUpdate offset on
            // the upper-arm bones (a sibling driver to HeldAxeRig) — the mechanism the ticket prescribes.
            // Resolved from the SMR bone array + serialized so it ships in Boot.unity. AFTER BuildInEditor
            // (the bones must exist), BEFORE AttachHeroAxeToHand (so the axe seats to the posed hand).
            AddArmPose(castaway);

            // RIGHT-HAND FINGER CURL (86ca8rdkp re-soak #4 — "his right finger is mangled"). The -fingerTrace
            // PROVED the skinning is clean (no degenerate bone/weights) — the "mangled" read is the OPEN clip
            // hand around a held haft. This additive LateUpdate driver CURLS the right-hand fingers into a grip
            // (gated on HasAxe so the empty hand stays open). Resolved from the SMR bone array + serialized.
            // AFTER BuildInEditor (the finger bones must exist).
            AddFingerCurl(castaway);

            // FOOT-YAW counter-rotate (86catvb6u — the Sponsor's chosen v4 pigeon-toe fix). Additive LateUpdate
            // per-foot outward yaw; v4 defaults to the measured bind-delta (~11°), non-v4 ships 0 (feet unchanged).
            // AFTER BuildInEditor (the foot bones must exist); F9-dialable (FOOT-YAW target).
            AddFootYaw(castaway);

            // Bind the flat DE-LIT material (CastawayMat — texture_diffuse toon albedo, warm-tan recolored
            // shirt) onto the avatar's SkinnedMeshRenderer(s) editor-time so it SERIALIZES into Boot.unity.
            // The FBX imports its own ImportStandard material; we override with the single de-lit toon mat so
            // the look matches the project's URP/Lit toon idiom + carries the recolor.
            BindCastawayMaterial(castaway);

            // Wire the verification-only shipped-build CASTAWAY CLOSE-UP capture (drives a dedicated
            // camera onto the avatar's front so the recolored identity — warm khaki shirt, sandy-ginger
            // hair, bare-feet skin — is judgeable from a SHIPPED frame). The gameplay-orbit capture frames
            // the character too small to judge colour; this gives the recolor (86ca8ca1m) a committed,
            // repeatable shipped-build path (the PR #21 lesson — a detail claim needs a committed shot,
            // not a throwaway). Inert unless launched with -verifyCastaway. Sibling of AxeVerifyCapture.
            WireCastawayVerifyCapture();

            // Wire the HANDS close-up capture (PR #186 FINGER re-open). The avatar-wide CastawayVerifyCapture
            // frames the hands too small to judge a finger mangle; this frames EACH hand TIGHTLY (individual
            // fingers resolvable) while the Breathing Idle plays, so the symptom region the Sponsor saw mangled
            // is eyeball-judgeable from a SHIPPED frame. Inert unless launched with -verifyHands.
            WireHandsVerifyCapture();

            // Wire the BUILD-GATED LIVE FLOAT-DIAGNOSTIC (86ca8rdkp — the instrument). Serializes onto Boot so
            // the F8 overlay (feet/ground/GAP live) + the ~1Hz [FloatTrace] log ship; inert until F8/-floatTrace.
            // The Sponsor walks the shoreline, SEES the GAP, dials GROUND-Y (F9) to GAP≈0, reports the value.
            WireFloatDiagnostic();
            // And its committed shipped-build capture path (proves the overlay renders the live GAP in the exe —
            // the shipped-build visual gate; inert unless -verifyFloatDiag). Sibling of CastawayVerifyCapture.
            WireFloatDiagnosticVerifyCapture();

            // Wire the BUILD-GATED SNEAK-WALK ISOLATION tool (86caa3kur re-soak attempt-3 /unstick instrument).
            // Serializes onto Boot so the F2 (foot-sync) + F3 (sneak-speed snap) toggles + the live readout ship;
            // behind the F1 dev-overlay master gate, default = shipped crouch behavior. The Sponsor sneaks, flips
            // F2 off, and reports whether the per-gait-cycle jerk vanishes — the precision handoff (not a fix).
            WireDebugOverlayMaster();

            // Wire the BUILD-GATED CAMERA-FOLLOW nudge tool (86caaqhj5 ATTEMPT 2 — the jump-pull-back precision
            // handoff). Serializes onto Boot so the F7 panel ships; inert until toggled. Lets the Sponsor dial the
            // OrbitCamera follow gains (horizontal/vertical lerp + lead time) live while jumping W/A/S/D, then
            // read the values to bake — the "build the knob, don't grind blind iterations" handle.
            WireCameraFollowNudgeTool();

            // Wire the GAMEPLAY-CAM walk-grounding capture (86ca8rdkp attempt-9 — the WALK-clip body-lift fix).
            // Captures from the REAL OrbitCamera (not an isolated rig — the false-green class) at 3 positions ×
            // standing/mid-stride so the Sponsor/orchestrator judge feet-on-sand exactly as gameplay frames it.
            // Inert unless -verifyWalkGround. Sibling of the float-diagnostic capture.
            WireWalkGroundingVerifyCapture();

            // Wire the BUILD-GATED held-axe WALK-BOUNCE/RATCHET trace (86ca9ykp0 — the DIAGNOSE-BEFORE-FIX
            // instrument). Drives a scripted multi-step walk + dumps every Y-reference per frame so the ratchet
            // source is PINNED (not guessed). Inert unless -axeWalkTrace. Sibling of FloatDiagnostic.
            WireAxeWalkTrace();

            // (No hair-silhouette verify capture — the chibi's procedural hair-spike soak class does not
            // apply to the Hyper3D castaway, which ships sculpted hair in the mesh. 86ca8rdkp.)

            // CONTACT / BLOB SHADOW (ticket 86ca8ca1m — "blob shadow fit to its footprint" AC). A soft
            // dark ground disc under the castaway's feet, fit (radius) to the castaway's stance so
            // the toy-chunky silhouette grounds. Lives on the PLAYER ROOT (NOT the avatar child) so the
            // avatar's height-scale doesn't scale it AND so it stays world-flat under the feet
            // regardless of the avatar's yaw/anim. Editor-time authored (mesh + inline transparent
            // vertex-color material) so it serializes into Boot.unity — the editor-vs-runtime trap.
            BuildBlobShadow(player);

            // RE-SOAK #2 — wire the contact shadow to CastawayCharacter so it GROUNDS the shadow to the
            // SNAPPED feet each frame. The shadow is a child of the player root (must not inherit the avatar
            // height-scale), so without this it strands ~9cm ABOVE the snapped feet on the dipping foreshore
            // (the 'elevated' percept — foot-trace 2026-06-15). CastawayCharacter.ApplyGroundSnap drives its
            // world-Y onto the same visible-terrain Y the feet snap to.
            castaway.blobShadow = player.transform.Find(BlobShadowObjectName);
            if (castaway.blobShadow == null)
                Debug.LogWarning("[MovementCameraScene] BlobShadow not found to wire onto CastawayCharacter — " +
                                 "the contact shadow won't follow the snapped feet (the 'elevated' re-soak #2 fix)");

            var ctm = player.AddComponent<ClickToMove>();
            ctm.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;
            ctm.markerPrefab = markerPrefab;

            // Attach the SOURCED hero axe (ticket 86ca8ce6y) to the chibi's right-hand bone AFTER the
            // avatar is built (the bone hierarchy must exist), so the held hatchet serializes into
            // Boot.unity under the bone (the editor-vs-runtime serialization trap). HasAxe-gated.
            AttachHeroAxeToHand(player);

            return player;
        }

        // ---- Blob/contact shadow anchors (ticket 86ca8ca1m). FIT to the chibi footprint. Radius in
        // world units (the player root is unscaled at 1u, so this is the on-ground size). Tone = a soft
        // warm-neutral dark (not pure black — pure black reads harsh under the warm Zone-D key); alpha =
        // the disc's opaque-core falloff strength. Exposed for the scene-presence test. ----
        public const string BlobShadowObjectName = "BlobShadow";
        public static readonly Color BlobShadowColor = new Color(0.08f, 0.07f, 0.06f); // soft warm-dark
        // The chibi's intrinsic footprint half-extent in its ~1u-normalized local space is ~0.36 (X) /
        // ~0.42 (Z) (probe: intrinsic X 1.44u / Z 1.68u over a 1.82u-tall mesh, normalized to 1u). On
        // the PlayerVisualHeight=1.8 avatar root that is ~0.65 (X) / ~0.76 (Z) world half-extent — but
        // the Z extent is dominated by the toes/heel spread, not the standing footprint. A radius of
        // 0.55 grounds the standing-pose footprint (feet + a soft margin) without a saucer that extends
        // past the silhouette. Soak-tunable if the Sponsor wants a wider/tighter pool.
        public const float BlobShadowRadius = 0.55f;
        public const float BlobShadowCenterAlpha = 0.5f;  // soft, not a hard black disc

        // Build the castaway's contact/blob shadow as a flat ground disc under the player root. The disc
        // mesh bakes a radial alpha falloff (LowPolyMeshes.BlobShadowDisc); an inline transparent
        // vertex-color material (FarHorizon/BlobShadowVertexColor, always-included so it survives the
        // stripped build) renders it as a soft dark pool. Sits just above y=0 to avoid z-fighting the
        // ground. NO collider — it must never block the click raycast or the NavMesh bake.
        private static void BuildBlobShadow(GameObject player)
        {
            var go = new GameObject(BlobShadowObjectName);
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f); // hover a hair to beat z-fight
            go.transform.localRotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = LowPolyMeshes.BlobShadowDisc(BlobShadowRadius, 18, BlobShadowColor, BlobShadowCenterAlpha);
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // a fake shadow casts none
            mr.receiveShadows = false;

            var vc = Shader.Find("FarHorizon/BlobShadowVertexColor");
            if (vc != null)
            {
                var mat = new Material(vc) { name = "BlobShadowMat" };
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Color.white);
                mr.sharedMaterial = mat; // inline -> serializes into the scene, no .mat churn
                EnsureShaderAlwaysIncluded(vc);
            }
            else
            {
                // Fallback: an unlit transparent flat disc (vertex falloff lost — a flat dark disc — but
                // never magenta). Tints the URP/Unlit base to the shadow tone with a fixed alpha.
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit != null)
                {
                    var mat = new Material(unlit) { name = "BlobShadowMat" };
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", new Color(BlobShadowColor.r, BlobShadowColor.g,
                                                             BlobShadowColor.b, BlobShadowCenterAlpha));
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mr.sharedMaterial = mat;
                    EnsureShaderAlwaysIncluded(unlit);
                }
                Debug.LogWarning("[MovementCameraScene] blob-shadow vertex-color shader not found; flat-disc fallback");
            }

            Debug.Log("[MovementCameraScene] authored BlobShadow under Player (radius=" + BlobShadowRadius +
                      ", fit to chibi stance)");
        }

        // Replace the static boot camera with the OrbitCamera rig targeting the player. We reuse
        // the existing camera GameObject (keeps its MainCamera tag + AudioListener) and add the
        // URP camera data + OrbitCamera component, so the boot smoke test still finds a Camera.
        private static GameObject BuildOrbitCamera(GameObject player, GameObject existingBootCamera)
        {
            GameObject camGo = existingBootCamera != null ? existingBootCamera : new GameObject("Main Camera");
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f); // deep dusk blue (U5 owns the skybox)
            cam.fieldOfView = 45f;
            camGo.tag = "MainCamera";
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            if (camGo.GetComponent<UniversalAdditionalCameraData>() == null)
                camGo.AddComponent<UniversalAdditionalCameraData>();

            var orbit = camGo.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = camGo.AddComponent<OrbitCamera>();
            orbit.target = player.transform;
            // 86caaqhj5 — JUMP-PULL-BACK fix. The camera target is the player ROOT, whose Y is constant through a
            // jump (the arc is a local-Y on the avatar CHILD). Wire the avatar so the camera can ADD its live
            // JumpHeight to the follow-point Y and actually track the visual jump arc (without this the camera
            // never rose with the player and the constant horizontal follow-lag read as a directional pull-back).
            // Wired editor-time so it SERIALIZES into Boot.unity (no Awake lookup in the build — the
            // component-in-source-but-not-in-scene trap). The avatar (CastawayCharacter) is a child of the player
            // root, built by BuildPlayer just before this; resolve it from the children.
            orbit.jumpHeightSource = player.GetComponentInChildren<CastawayCharacter>(true);
            // 86caaqhj5 ATTEMPT 2 — HORIZONTAL follow velocity feed-forward (the A/S/D jump-pull-back MECHANISM
            // fix). Wire the player root's NavMeshAgent so the camera LEADS the horizontal follow target by the
            // travel velocity × leadTime — cancelling the exponential follower's steady-state lag (v/k ≈ 0.41u
            // measured) that read as a directional 'pulled back' on strafe/back jumps. Wired editor-time so it
            // SERIALIZES into Boot.unity (no Awake lookup in the build — the component-in-source-but-not-in-scene
            // trap). The agent lives on the player root (BuildPlayer added it just before this).
            orbit.followVelocitySource = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            orbit.followLeadTime = 0f;   // 0 = auto (1/followLerp = the exact lag-cancelling lead); F7-dialable
            // 86caaqhj5 ATTEMPT 3 — the CONFIRMED jump-pull-back fix (diagnose-via-trace, JumpCameraFollowTraceTests).
            // While AIRBORNE the horizontal X/Z follow uses this TIGHT rate (no velocity lead) so the jump has ~zero
            // lag → the avatar stays CENTRED in every heading. The attempt-2 grounded lead cancels the lag only when
            // the agent's reported velocity matches the real travel rate; air-control breaks that mid-air, re-framing
            // the player off-centre by heading (jump+A/D out of view, jump+S into the camera). 60 = match the
            // vertical rate so XZ + Y track the arc equally tight. Set editor-time so it SERIALIZES into Boot.unity
            // (the component-in-source-but-not-in-scene trap); F7-dialable (U/J) for the Sponsor to re-tune.
            orbit.airborneFollowLerp = 60f;
            orbit.defaultPitch = 55f;   // Sponsor-preferred top-down-ish framing (inside 8-70) — LOCKED
            // Floor WIDENED 35->8 (drew/ocean-camera-fix): lets the Sponsor tilt down to the horizon +
            // see the seaward beach ocean (the 35 floor framed the sea as a far fogged "grey pond" —
            // OceanCameraDiag trace, 2026-06-13). Default 55 + max 70 unchanged.
            orbit.minPitch = 8f;
            orbit.maxPitch = 70f;
            orbit.distance = 14f;

            // BIG ROUND ISLAND N2 (86ca9a7qn — "player disappears under a hill"). Wire the terrain-collision
            // mask to the Ground layer editor-time so it SERIALIZES (no Awake LayerMask string lookup in the
            // build — the editor-vs-runtime trap). The OrbitCamera then keeps itself ABOVE the hill surface +
            // pulls IN when a hill occludes the character, so the player never vanishes under/behind a hill.
            // The island terrain (Ground_Play) is on the Ground layer with a MeshCollider, so the camera
            // raycasts hit the real hills.
            int groundLayer = LayerMask.NameToLayer("Ground");
            orbit.terrainMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)0;
            if (groundLayer < 0)
                Debug.LogWarning("[MovementCameraScene] 'Ground' layer missing — OrbitCamera terrain-collision " +
                                 "(N2 hill-clip fix) will be INERT (mask 0 = no collision)");
            return camGo;
        }

        // Wire WASD keyboard locomotion (ticket 86ca9yq2x) onto the player root — REPLACES click-to-move.
        // Added editor-time (serializes onto the Player in Boot.unity — the component-in-source-but-not-in-
        // scene trap; an Awake-added component would ship the player un-driveable). Camera-relative: the
        // cameraTransform is the orbit camera (built just before this), so "W = the way the camera faces"
        // serializes too (no Awake Camera.main lookup needed in the build). The ClickToMove ref lets
        // WasdMovement disable click-to-move on Start while keeping its programmatic MoveTo seam for the
        // verify captures + the PlayMode harness.
        private static void BuildWasdMovement(GameObject player, GameObject camGo)
        {
            var wasd = player.GetComponent<WasdMovement>();
            if (wasd == null) wasd = player.AddComponent<WasdMovement>();
            wasd.moveSpeed = player.GetComponent<NavMeshAgent>() != null
                ? player.GetComponent<NavMeshAgent>().speed
                : 5.5f;
            // RUN speed (86ca9yq34 — run-on-Shift). Faster than the walk speed so holding Shift visibly runs;
            // matches the Run blend-tree threshold (CharacterAssetGen.RunBlendSpeed) so the faster agent
            // velocity lands the Walk<->Run blend on the Run clip. Serialized editor-time (the
            // component-in-source-but-not-in-scene trap — an Awake-set value wouldn't ship).
            wasd.runSpeed = CharacterAssetGen.RunBlendSpeed;
            wasd.cameraTransform = camGo != null ? camGo.transform : null;
            wasd.clickToMove = player.GetComponent<ClickToMove>();
            // JUMP (86ca9yq3q) — wire the avatar that owns the jump arc + the airborne ground-snap gate so
            // Space's rising edge calls CastawayCharacter.TryJump(). Serialized editor-time (the
            // component-in-source-but-not-in-scene trap — an Awake GetComponentInChildren is the fallback only).
            wasd.castaway = player.GetComponentInChildren<CastawayCharacter>(true);
            if (wasd.castaway == null)
                Debug.LogWarning("[MovementCameraScene] WasdMovement castaway (jump owner) not wired — Space will " +
                                 "fall back to GetComponentInChildren at runtime; jump inert if no avatar");
            // AIRBORNE AIR-CONTROL (86caac81y — subtle lateral nudge in flight, NOT a full-speed snap). Subtle
            // accel default; the airborne horizontal-speed cap defaults to the WALK speed so a nudge never builds
            // past a walk (a run-jump's carried-in momentum is still preserved — the cap only clamps speed the
            // nudge would PUSH past it). Serialized editor-time (component-in-source-but-not-in-scene trap).
            // 86caambxh: Sponsor soak 2026-07-01 RAISED airControlAccel 5 → 9 u/s² for a snappier mid-air sideways
            // air-steer. The upright sideways slide IS the intended feel (no body-tilt/lean; the Sponsor confirmed
            // he does NOT want one). This is the value that actually SHIPS (the serialized scene-build site, per
            // [[unity-procedural-committed-assets-go-stale]] — the field default alone never reaches the build).
            wasd.airControlAccel = 9f;
            wasd.airControlMaxSpeed = wasd.moveSpeed > 0.001f ? wasd.moveSpeed : 5.5f;
            if (wasd.cameraTransform == null)
                Debug.LogWarning("[MovementCameraScene] WasdMovement camera not wired — WASD will fall back to " +
                                 "Camera.main at runtime (camera-relative still works, just not serialized)");

            // Wire the verification-only shipped-build WASD capture (holds camera-relative forward via the
            // input-independent seam, proves WASD MOVED the player in the BUILT exe). Inert unless -verifyWasd.
            WireWasdVerifyCapture(player);

            // Wire the RUN-ON-SHIFT shipped-build capture (86ca9yq34) — holds forward + the sprint override and
            // captures the run cycle from the gameplay cam (walk vs run frames + the grounded-while-running gap).
            // Inert unless -verifyRun. Sibling of WasdVerifyCapture.
            WireRunVerifyCapture(player);

            // Wire the SNEAK-WALK SMOOTHNESS shipped-build capture (86caa3kur re-soak) — holds forward + the
            // crouch override and MEASURES the per-frame root step (the stutter ground truth: low step-variance =
            // smooth) + captures the sneak cycle from the gameplay cam. Inert unless -verifySneak. Sibling of
            // WireRunVerifyCapture.
            WireSneakVerifyCapture(player);

            // Wire the LOCOMOTION + HIT-REACT shipped-build capture (86cackb3j) — walk→run then fires the Hit
            // trigger on the live Animator + captures the flinch, with a cone-explosion guard (mesh stays at the
            // player — the Generic-rig bind). Inert unless -verifyHitReact. Sibling of RunVerifyCapture.
            WireHitReactVerifyCapture(player);

            // Wire the REAL-SNAKE shipped-build capture (86caaz4vn AC6/AC7) — walks the player at the snake
            // through the movement seam, proves aggro→telegraph→lunge→bite live + kills it with the real axe
            // WeaponDef, shooting gameplay-cam + side-profile frames. Inert unless -verifySnake.
            WireSnakeVerifyCapture(player);

            Debug.Log("[MovementCameraScene] WASD locomotion wired (camera-relative, speed=" +
                      wasd.moveSpeed.ToString("0.0") + ", click-to-move disabled on Start)");
        }

        // ---- SETTINGS PANEL (86caa4bqp) ----
        public const string PanelSettingsAssetPath = SettingsDir + "/SettingsPanelSettings.asset";
        public const string SettingsPanelUxmlPath = "Assets/UI/SettingsPanel.uxml";
        public const string PaletteUssPath = "Assets/UI/Palette.uss";
        public const string SettingsPanelUssPath = "Assets/UI/SettingsPanel.uss";
        public const string SettingsPanelObjectName = "SettingsPanel";

        // Author the UI Toolkit settings panel into the scene (86caa4bqp). A dedicated GameObject hosts a
        // UIDocument + the SettingsPanel component, with the PanelSettings + UXML/USS assets created/loaded
        // here so they SERIALIZE into Boot.unity (the component-in-source-but-not-in-scene + asset-not-
        // serialized traps). The live targets (OrbitCamera on camGo, WasdMovement on the player) are wired as
        // serialized references so the registry binds them with NO Awake lookup in the build.
        private static void BuildSettingsPanel(GameObject camGo, GameObject player)
        {
            var go = new GameObject(SettingsPanelObjectName);

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = EnsurePanelSettings();
            doc.sortingOrder = 100f; // above any future inventory/HUD overlay

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SettingsPanelUxmlPath);
            var palette = AssetDatabase.LoadAssetAtPath<StyleSheet>(PaletteUssPath);
            var panelUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(SettingsPanelUssPath);
            // Do NOT assign doc.visualTreeAsset — SettingsPanel.BuildView owns the clone(s) (86cah8ukr: it now
            // CloneTree's the serialized panelUxml TWICE, once per drawer — F1 player + F3 dev — into two scoped
            // containers, adds the stylesheets, and re-resolves elements per-container; it also carries the
            // build-safety-net BuildShellInCode for the asset-not-serialized case). Assigning visualTreeAsset
            // here would make the UIDocument ALSO auto-clone the shell on enable → a duplicate, always-visible
            // orphan settings-scrim laid over the world that Q("settings-scrim") never binds (codereview #83).

            var panel = go.AddComponent<SettingsPanel>();
            panel.document = doc;
            panel.panelUxml = uxml;
            panel.paletteUss = palette;
            panel.panelUss = panelUss;
            panel.orbit = camGo != null ? camGo.GetComponent<OrbitCamera>() : null;
            panel.wasd = player != null ? player.GetComponent<WasdMovement>() : null;
            // Thirst tweakables (86caamkv7 AC5) bind to the ThirstNeed BootstrapProject added to the Survival
            // object BEFORE this runs — serialized so the panel never relies on a runtime FindObjectOfType.
            panel.thirst = Object.FindObjectOfType<ThirstNeed>();
            // Hunger tweakables (86cabd75y) bind to the HungerNeed BootstrapProject added to the Survival object
            // BEFORE this runs — serialized (same as thirst) so the `Hunger decay rate` + `Berry restore amount`
            // rows are live in the shipped build without a runtime FindObjectOfType. The Awake fallback stays as
            // the bare-scene safety net. May be null on a bare rig — the rows then simply don't appear.
            panel.hunger = Object.FindObjectOfType<HungerNeed>();
            // Per-need on/off + warmth decay-rate (86cabeqwf, folded into the F1/F3 split 86cah8ukr) — the
            // warmth on/off toggle + warmth decay-rate slider bind to the WarmthNeed BootstrapProject added to
            // the Survival object BEFORE this runs (the hunger/thirst on/off toggles bind to those needs wired
            // above). Serialized so the PLAYER-facing rows ship live without a runtime FindObjectOfType. May be
            // null on a bare rig — the warmth rows then simply don't appear.
            panel.warmth = Object.FindObjectOfType<WarmthNeed>();
            // 86cah8ukr SPLIT — wire BOTH toggle keys editor-time so they serialize into Boot.unity (the
            // field-default-not-serialized trap): F1 opens the player Settings drawer, F3 the dev console
            // (Sponsor-confirmed 2026-07-03). Debug overlays are on F10 (DebugOverlayMaster, the single overlay
            // master since the legacy F2 DebugOverlayToggle was removed in 86cah90cp round-3; F2 is UNBOUND).
            panel.toggleKey = KeyCode.F1;
            panel.devToggleKey = KeyCode.F3;
            // Held-weapon placement seam (86caffwuz) — the 7 held-weapon in-hand rows bind to it. BuildPlayer
            // (which authors the HeroAxe + its HeldWeaponPlacement) runs BEFORE this in Author, so the seam
            // already exists; wire it serialized so the rows never rely on a runtime FindObjectOfType (the
            // editor-vs-runtime ship-path discipline). May be null on a bare rig — the rows then simply don't appear.
            panel.heldWeapon = Object.FindObjectOfType<HeldWeaponPlacement>();
            // Inventory façade (86cabfa4e) — `inventory slots` + `belt slots` + `inventory stack size` bind through
            // it. BootstrapProject adds the Inventory to the Survival object BEFORE MovementCameraScene.Author runs
            // (so CraftSpot can wire it), so it ALREADY exists here — wire it serialized so the rows ship live without
            // a runtime FindObjectOfType (the editor-vs-runtime ship-path discipline the stone-respawner dead-knob
            // taught). The Awake FindObjectOfType<Inventory> stays as the bare-scene safety net. May be null on a
            // bare rig — the inventory rows then simply don't appear.
            panel.inventory = Object.FindObjectOfType<Inventory>();
            // F-KEY MIGRATION (86caber95) — the F9 arm-pose rows bind to the castaway's CastawayArmPose. BuildPlayer
            // (which wires CastawayArmPose onto the castaway) runs BEFORE this in Author, so it already exists —
            // wire it serialized so the arm-pose + run-lower rows ship live without a runtime FindObjectOfType (the
            // editor-vs-runtime ship-path discipline). The Awake fallback stays the bare-scene safety net. (The F7
            // camera-follow rows bind to panel.orbit; ground-Y to panel.chopCharacter — both already wired above/via
            // Awake. The F10 world-look seam is back-wired post-environment by WireWorldLookConsole.)
            panel.armPose = Object.FindObjectOfType<CastawayArmPose>();
            // FPS counter (86cahmxmt) — the `FPS counter` on/off row binds to the FpsCounterHud that
            // BootstrapProject.BuildBootScene added to the Boot object BEFORE this runs (NextIslandPocScene
            // likewise authors it before calling Author). Wire it serialized so the row ships live without a
            // runtime FindObjectOfType (the editor-vs-runtime ship-path discipline the stone-respawner
            // dead-knob taught). The Awake fallback stays the bare-scene safety net. May be null on a bare
            // rig — the row then simply doesn't appear.
            panel.fpsHud = Object.FindObjectOfType<FarHorizon.FpsCounterHud>();

            if (uxml == null || palette == null || panelUss == null)
                Debug.LogWarning("[MovementCameraScene] SettingsPanel UI assets missing (uxml=" + (uxml != null) +
                                 ", palette=" + (palette != null) + ", panelUss=" + (panelUss != null) +
                                 ") — the panel falls back to building its shell in code; check Assets/UI/*");
            if (panel.orbit == null || panel.wasd == null)
                Debug.LogWarning("[MovementCameraScene] SettingsPanel live targets not fully wired (orbit=" +
                                 (panel.orbit != null) + ", wasd=" + (panel.wasd != null) +
                                 ") — those settings fall back to a runtime FindObjectOfType");

            // The shipped-build verify capture (-verifySettings): opens the panel + drives a tweak in the
            // BUILT exe so the capture gate proves it renders + the live param changes. Serialized onto the
            // same GameObject (the component-in-source-but-not-in-scene trap). Inert in normal play.
            go.AddComponent<SettingsVerifyCapture>();

            EditorUtility.SetDirty(go);
            Debug.Log("[MovementCameraScene] authored SettingsPanel (UI Toolkit; orbit+wasd bound, Esc-toggled)");
        }

        // Create-or-load the runtime PanelSettings asset for the settings panel. PanelSettings is a runtime
        // ScriptableObject; it needs a theme StyleSheet (the default runtime theme) or UI Toolkit logs a
        // missing-theme error + renders unstyled. Scale-with-screen-size at 1920x1080 (Uma §1 topology).
        private static PanelSettings EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsAssetPath);
            if (existing != null) return existing;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.match = 0.5f;
            // Assign the default runtime theme so controls (Slider/MinMaxSlider/Button) render with base
            // styles; our USS layers the carved-wood palette on top. The theme ships with UI Toolkit.
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss")
                ?? AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.modules.uielements/PackageResources/StyleSheets/Generated/DefaultRuntimeTheme.tss");
            if (theme != null) ps.themeStyleSheet = theme;
            else Debug.LogWarning("[MovementCameraScene] default runtime UI theme not found — the settings " +
                                  "panel controls may render unstyled (USS palette still applies)");
            AssetDatabase.CreateAsset(ps, PanelSettingsAssetPath);
            AssetDatabase.SaveAssets();
            return ps;
        }

        // Wire the RUN-ON-SHIFT shipped-build verify capture onto the Boot object so it SERIALIZES into
        // Boot.unity (the component-in-source-but-not-in-scene trap). Inert unless launched with -verifyRun
        // (86ca9yq34). Sibling of WireWasdVerifyCapture.
        private static void WireRunVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host RunVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<RunVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<RunVerifyCapture>();
            cap.player = player.GetComponent<WasdMovement>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the SNEAK-WALK SMOOTHNESS shipped-build verify capture (86caa3kur re-soak) onto the Boot object so
        // it SERIALIZES into Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert
        // otherwise). Inert unless launched with -verifySneak. Sibling of WireRunVerifyCapture.
        private static void WireSneakVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host SneakVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<SneakVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<SneakVerifyCapture>();
            cap.player = player.GetComponent<WasdMovement>();
            // Wire the avatar so the 86caa3kur re-soak ANIMATOR LOOP-HITCH trace reads the LIVE Animator state
            // (it serializes into Boot.unity — the component-in-source-but-not-in-scene trap). Resolved off the
            // player's child avatar (the CastawayCharacter lives on a child avatar root under the player).
            cap.castaway = player.GetComponentInChildren<CastawayCharacter>(true);
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the LOCOMOTION + HIT-REACT shipped-build verify capture (86cackb3j) onto the Boot object so it
        // SERIALIZES into Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert otherwise).
        // Inert unless launched with -verifyHitReact. Sibling of WireRunVerifyCapture.
        private static void WireHitReactVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host LocomotionHitReactVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<LocomotionHitReactVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<LocomotionHitReactVerifyCapture>();
            cap.player = player.GetComponent<WasdMovement>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the REAL-SNAKE shipped-build verify capture (86caaz4vn) onto the Boot object so it SERIALIZES
        // into Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert otherwise).
        // Inert unless launched with -verifySnake. Sibling of WireHitReactVerifyCapture.
        private static void WireSnakeVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host SnakeVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<SnakeVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<SnakeVerifyCapture>();
            cap.player = player.GetComponent<WasdMovement>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the WASD shipped-build verify capture onto the Boot object so it SERIALIZES into Boot.unity
        // (the component-in-source-but-not-in-scene trap — it would ship inert otherwise). Inert unless
        // launched with -verifyWasd. Sibling of MovementVerifyCapture/AxeVerifyCapture/etc.
        private static void WireWasdVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] no Boot object found to host WasdVerifyCapture");
                return;
            }
            var cap = bootGo.GetComponent<WasdVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<WasdVerifyCapture>();
            cap.player = player.GetComponent<WasdMovement>();
            EditorUtility.SetDirty(bootGo);
        }

        // NavMesh voxel size (BIG ROUND ISLAND, 86ca9a7qn — N1 "can't walk everywhere"). A FINE voxel size
        // resolves the big 330u terrain grid + the hill slopes + the foreshore dip cleanly (the default voxel,
        // derived from agentRadius/3 ≈ 0.13, is fine; we PIN 0.16 so the bake is deterministic + the hill
        // slopes never coarsen into gaps). The agent must path UP/DOWN/ACROSS the hills — the island slope tops
        // out ~33deg (slope-probed), comfortably under the default agent maxSlope 45deg, so the DEFAULT agent
        // type's slope/climb already cover every hill; the partial-coverage N1 bug was NOT a slope limit (that
        // hypothesis was REFUTED by the slope probe) but a flat-slab-only / layer-restricted bake (see
        // BakeAndSaveNavMesh). NOTE: NavMeshSurface.BuildNavMesh reads slope/climb from the AGENT TYPE
        // (NavMesh.GetSettingsByID) — the surface only overrides voxel/tile size — so we tune voxel here and
        // rely on the default agent type for slope/climb (which the probe proved sufficient).
        private const float NavVoxelSize = 0.16f;    // fine enough to resolve the 330u island slopes cleanly

        // Apply the island-aware voxel override to a NavMeshSurface so its bake resolves the WHOLE sloped
        // island cleanly (the N1 root cause was a flat-slab-only / layer-restricted bake, not a coarse voxel —
        // but pinning the voxel keeps the big-grid bake deterministic + gap-free).
        private static void ConfigureIslandNavSettings(NavMeshSurface surface)
        {
            surface.overrideVoxelSize = true;
            surface.voxelSize = NavVoxelSize;
            surface.overrideTileSize = false;
        }

        // Bake the NavMesh over the WALKABLE ISLAND TERRAIN (not just the flat test slab), then SAVE the data
        // as an asset and assign it so the standalone build SHIPS a NavMesh (else click-to-move is silently
        // dead — the spike's iter-3 lesson + unity-conventions.md §NavMesh).
        //
        // BIG ROUND ISLAND N1 FIX (86ca9a7qn — "click-to-move only covers part of the island"). The OLD bake
        // restricted to `layerMask = Ground` AND ran during BuildBootScene — BEFORE WorldBootstrap builds the
        // island terrain — so it could only see the flat 60×60 `TestGround` slab. The shipped BootNavMesh was
        // therefore a flat disc at the centre; the hilly island beyond ±30u had NO walkable surface in THIS
        // bake, and the slab disc sat at Y=0 disconnected from the dipping/rising island terrain. (WorldBootstrap
        // ALSO baked a PlayNavMesh over the island, but the two overlapping/disconnected meshes are exactly the
        // fragile dual-surface that produced partial coverage.) FIX: this bake now ALSO collects the island
        // terrain collider if it exists yet (it does NOT during BuildBootScene — the slab is all there is here),
        // with hill-aware slope/step/voxel settings; and WorldBootstrap.BakeNavMesh — which DOES run after the
        // island terrain exists — is now the single authoritative whole-island bake (it overwrites this asset's
        // role at runtime via its own surface). We keep this bake (BootNavMesh) so a build WITHOUT the env (a
        // hypothetical movement-only scene) still ships a surface, and so MovementCameraSceneTests' save-asset
        // guard holds, but it is no longer LAYER-RESTRICTED — it collects every walkable collider it can see.
        private static void BakeAndSaveNavMesh(GameObject ground, int groundLayer)
        {
            var surfaceGo = new GameObject("NavMeshSurface");
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            // Collect ALL physics colliders (NOT layer-restricted) so this bake covers the island terrain
            // collider too whenever it is present — the N1 partial-coverage fix. The flat slab + island both
            // contribute; their union is the walkable surface.
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            ConfigureIslandNavSettings(surface);
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                Directory.CreateDirectory(Path.GetFullPath(NavMeshDir));
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(surface);
                Debug.Log("[MovementCameraScene] NavMesh baked + SAVED -> " + NavMeshDataPath +
                          " (voxel=" + NavVoxelSize + ", collectAll; data assigned: " +
                          (surface.navMeshData != null) + ")");
            }
            else
            {
                Debug.LogError("[MovementCameraScene] NavMesh bake produced NO data — " +
                               "click-to-move would be dead in the build");
            }

            // BIG ROUND ISLAND N1 (86ca9a7qn): DISABLE this slab-era surface at runtime so it does NOT add its
            // flat-Y=0 60×60 disc as a SECOND, DISCONNECTED NavMesh that competes with WorldBootstrap's
            // authoritative whole-island PlayNavMesh (the dual-overlap was part of the partial-coverage bug —
            // the agent could warp onto the isolated slab disc and not reach the hills beyond ±30u). The
            // BootNavMesh ASSET still ships (MovementCameraSceneTests' save-asset guard holds, and a
            // hypothetical env-less movement scene could re-enable this surface), but at runtime in the full
            // boot scene ONLY the island PlayNavMesh is live. NavMeshSurface adds its data in OnEnable, so a
            // disabled component never registers — the agent samples the single continuous island surface.
            surface.enabled = false;
            EditorUtility.SetDirty(surfaceGo);
        }

        // Attach the verification-only movement capture to the Boot object (the GameObject that
        // already carries BootHud + BootScreenshot), wiring it to the player. Inert unless the
        // build is launched with -verifyMove, so it never affects the normal game or boot capture.
        private static void WireMovementVerifyCapture(GameObject player)
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — movement-verify " +
                                 "capture not wired (the shipped-build movement gate hook is absent)");
                return;
            }
            var cap = bootGo.GetComponent<MovementVerifyCapture>();
            if (cap == null) cap = bootGo.AddComponent<MovementVerifyCapture>();
            cap.player = player.GetComponent<ClickToMove>();
            EditorUtility.SetDirty(bootGo);
        }

        // Attach the verification-only SEA capture to the Boot object (the -verifySea orbit-to-seaward
        // ocean shot). Inert unless launched with -verifySea. Serialized into the scene editor-time (NOT
        // Awake) per the editor-vs-runtime trap; WaterSceneTests guards its serialized presence.
        private static void WireSeaVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — sea-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<SeaVerifyCapture>() == null)
                bootGo.AddComponent<SeaVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the BIG ROUND ISLAND verify capture (86ca9a7qn) onto the Boot object so it SERIALIZES into
        // Boot.unity (the component-in-source-but-not-in-scene trap). Inert unless launched with
        // -verifyIsland; captures a gameplay over-shoulder frame + an overhead/high-orbit frame proving the
        // round island + water-all-sides + distant mountain islands + dense tall forest.
        private static void WireIslandVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — island-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<FarHorizon.IslandVerifyCapture>() == null)
                bootGo.AddComponent<FarHorizon.IslandVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the verification-only ROCK capture (86ca8m5zu) onto the Boot object so it SERIALIZES into
        // Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert otherwise). Inert
        // unless launched with -verifyRock; never affects a normal play/boot/soak.
        private static void WireRockVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — rock-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<RockVerifyCapture>() == null)
                bootGo.AddComponent<RockVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the verification-only FRESHWATER POND capture (86caamkv7) onto the Boot object so it
        // SERIALIZES into Boot.unity (the component-in-source-but-not-in-scene trap — it would ship inert
        // otherwise). Inert unless launched with -verifyPond; never affects a normal play/boot/soak. Frames
        // the pond at (7,0,-3) from the over-shoulder gameplay orbit pitch + asserts fresh-blue (B>G) from
        // the shipped frame — the pond capture the generic -captureGate never produces.
        private static void WireFreshwaterPondVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — pond-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<FreshwaterPondVerifyCapture>() == null)
                bootGo.AddComponent<FreshwaterPondVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Wire the verification-only SKY-FACING capture (86cabc743 — the SUN-DISK POC) onto the Boot object
        // so it SERIALIZES into Boot.unity (the component-in-source-but-not-in-scene trap — it would ship
        // inert otherwise). Inert unless launched with -verifySky; never affects a normal play/boot/soak.
        // Parks a dedicated sky camera (the orbit cam can't tilt up to the Sun) aimed at the live Sun
        // direction + the cloud band, and self-asserts sun-visible + cloud-vs-sky contrast from the shipped
        // frame — the sky capture the generic -captureGate never produces.
        private static void WireSkyVerifyCapture()
        {
            var bootGo = GameObject.Find("Boot");
            if (bootGo == null)
            {
                Debug.LogWarning("[MovementCameraScene] 'Boot' object not found — sky-verify capture not wired");
                return;
            }
            if (bootGo.GetComponent<SkyVerifyCapture>() == null)
                bootGo.AddComponent<SkyVerifyCapture>();
            EditorUtility.SetDirty(bootGo);
        }

        // Add a shader to GraphicsSettings.AlwaysIncludedShaders so the standalone build does NOT
        // strip it (the iter-2/3 magenta lesson). Idempotent.
        private static void EnsureShaderAlwaysIncluded(Shader shader)
        {
            if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return; // present
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[MovementCameraScene] added shader to AlwaysIncludedShaders -> " + shader.name);
        }
    }
}
