using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// The FORGE place-to-build flow (ticket 86camz9vh / crafting-redesign ③ — the unified invisible-until-placed
    /// system, spec §2). REWRITE of the shipped fixed-spot proximity build (#292): the Sponsor rejected a
    /// pre-visible forge (86camyvzw — "the forge must NOT be visible before it is built — the player builds it by
    /// gathering the ingredients and PLACING it"), so the forge now uses the SAME place-to-build flow as the ①
    /// crafting table: gather wood + stone → press the build key → a translucent GHOST of the forge tracks the
    /// ground under the MOUSE CURSOR → LEFT-CLICK to CONFIRM (debits the materials all-or-nothing, reveals + raises
    /// the real forge at the ghost pose) or Escape to cancel with no debit. There is no marker to see before placement.
    ///
    /// === Reuses the ① placement system (spec §DRAFT③ "REUSE the ① placement system") ===
    /// This is a focused SIBLING of <see cref="CraftingTablePlacement"/> — it reuses ①'s proven PURE truth-tables
    /// (<see cref="CraftingTablePlacement.IsGroundValid"/> / <see cref="CraftingTablePlacement.IsValidPlacement"/> /
    /// <see cref="CraftingTablePlacement.ApplyRotation"/> / <see cref="CraftingTablePlacement.PlaneIntersect"/>),
    /// the shared obstruction seam (<see cref="PlacementObstacleRegistry"/> + its
    /// <see cref="PlacementObstacleRegistry.CollectSessionObstacles"/> scan), and the same UiInputGate-modal ghost
    /// idiom. (A common StructurePlacement base for BOTH is a deferred cleanup — kept separate here so this ③ PR
    /// does not churn the just-soaked ① table placement; both are guarded by their own scene + validity tests.)
    ///
    /// === Much higher stone cost (Sponsor-locked design) ===
    /// The forge costs MUCH more stone than the table (default 6 wood + 12 stone, spec §5 — "forge &gt;&gt;
    /// weapons": a stone furnace). The all-or-nothing gate is the load-bearing negative case (short of EITHER mat
    /// → no forge, no debit); <see cref="CanAfford"/> is the PURE static (unit-testable without a scene) the
    /// shipped I-3 tests already pin.
    ///
    /// === OBJECT-OVERLAP invalid rule + the #302 seam ===
    /// The ghost reads INVALID (red + "[X] BLOCKED — overlaps an object") when its footprint is OFF the walkable
    /// NavMesh (a tree carve / off the island) OR overlaps a discrete interactable — a lit campfire / active ore
    /// node (the shared session scan) OR anything registered via <see cref="PlacementObstacle"/> (a placed
    /// crafting TABLE or another built forge self-register on Reveal/Build; ②'s boulders adopt the same seam). The
    /// NavMesh path is gated on THIS instance's serialized <see cref="groundMask"/> (a real mask = the shipped
    /// world; 0 = a synthetic rig) — NOT the process-global triangulation, which BLEEDS across PlayMode fixtures
    /// (the #302 fixture-bleed lesson).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// This component + the (hidden) real forge + the (hidden) ghost + the Inventory/Forge/player refs are authored
    /// editor-time into Boot.unity (MovementCameraScene.BuildForge), NOT at Awake. NO mutable statics. The camera
    /// is resolved via the MainCamera tag (cached, never per-frame).
    /// </summary>
    public class ForgePlacement : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger the wood + stone forge cost is debited from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The (invisible-until-placed) real forge this flow reveals + raises on confirm. Wired at bootstrap; " +
                 "scene-found fallback.")]
        public Forge forge;

        [Tooltip("The player transform. Used for the off-self distance check + the no-camera fallback pose. Wired " +
                 "at bootstrap; ClickToMove-root fallback.")]
        public Transform player;

        [Tooltip("The translucent GHOST root shown during placement (a see-through copy of the forge). Hidden " +
                 "outside placement; tracks the ground under the cursor; tinted valid/invalid. Wired at bootstrap.")]
        public Transform ghost;

        [Header("Build cost (🎚️ default — Sponsor-soak tunes; §5 — the forge costs MUCH more stone)")]
        [Tooltip("Wood the forge costs (default 6). A stone furnace costs more than the open campfire.")]
        public int woodCost = ForgeWoodCostDefault;
        [Tooltip("Stone the forge costs (default 12 — the STONE half of the gate; 'forge >> weapons', a stone furnace).")]
        public int stoneCost = ForgeStoneCostDefault;

        [Header("Placement")]
        [Tooltip("Key to ENTER forge placement (when no forge is built yet). V — a Danish-safe letter, free of the " +
                 "gameplay keys (C is the table). INTERIM: the general 'build menu of placeables' is a follow-up " +
                 "ticket (86catpvpa); this direct key stands in until then. Confirm/cancel are mouse+Escape.")]
        public KeyCode buildKey = KeyCode.V;
        [Tooltip("Key to cancel placement (exit with no debit).")]
        public KeyCode cancelKey = KeyCode.Escape;
        [Tooltip("Degrees the ghost yaw rotates per scroll notch (the scroll wheel rotates the ghost while placing; " +
                 "the camera zoom is gated off via UiInputGate). Sign-based — one predictable step per notch.")]
        public float rotationStepDegrees = 15f;
        [Tooltip("How far in front of the player the ghost floats in the NO-CAMERA fallback (headless test rig only " +
                 "— the live build tracks the mouse cursor).")]
        public float placeDistance = 2.5f;
        [Tooltip("Ground layer(s) the mouse-cursor ray hits / the ghost snaps onto. If 0/unset, a flat-ground " +
                 "fallback at the player's height is used (test rig).")]
        public LayerMask groundMask;
        [Tooltip("Minimum planar distance the ghost must be from the player to be valid (never build on top of self).")]
        public float minDistFromPlayer = 1.0f;
        [Tooltip("Cosine of the max ground slope the forge may stand on (0.85 ≈ 32°). Steeper / no ground reads BLOCKED.")]
        public float minGroundNormalY = 0.85f;

        [Header("Object-overlap (#302 seam — red over objects)")]
        [Tooltip("Planar (XZ) radius of the forge's footprint for the object-overlap test. A forge is chunkier than " +
                 "the table (~0.74u body). Larger = redder near objects. Soak-tune. Default 0.8.")]
        public float footprintRadius = 0.8f;
        [Tooltip("NavMesh sample search radius under the footprint. If no walkable navmesh is found within this, or " +
                 "the nearest is farther than the tolerance, the spot reads BLOCKED. Soak-tune. Default 1.5.")]
        public float navSampleMaxDist = 1.5f;
        [Tooltip("How far the footprint centre may be from the nearest walkable navmesh before it reads BLOCKED. " +
                 "Soak-tune. Default 0.35.")]
        public float offNavMeshTolerance = 0.35f;

        /// <summary>Default forge wood cost (spec §5) — 🎚️ Sponsor-soak tunes. A stone furnace costs more wood
        /// than the open campfire (3).</summary>
        public const int ForgeWoodCostDefault = 6;
        /// <summary>Default forge STONE cost (spec §5 — "forge &gt;&gt; weapons") — 🎚️ Sponsor-soak tunes. Much
        /// more than the shipped 5, and dwarfing any single weapon so the iron tier feels earned.</summary>
        public const int ForgeStoneCostDefault = 12;

        private bool _built;
        private bool _placing;
        private Vector3 _ghostPos;
        private float _ghostYaw;
        private Quaternion _ghostRot = Quaternion.identity;
        private bool _groundValid;   // ground found + flat enough + off self (the spatial part of validity)
        private bool _obstructed;    // footprint off-navmesh OR over a discrete interactable (#302)
        private bool _valid;         // _groundValid AND CanAffordForge() AND !_obstructed (full validity)
        private Renderer[] _ghostRenderers;
        private MaterialPropertyBlock _mpb;
        private GUIStyle _promptStyle;
        private Camera _cam;
        private bool _gatePlacing;   // tracks our UiInputGate push so it can never stick open

        // Cached OnGUI prompt lines (folded NIT — the lines depend ONLY on woodCost/stoneCost/cancelKey, fixed for
        // a placement session, so BUILD them ONCE on EnterPlacement + CACHE them; OnGUI selects one, never concats
        // per frame — unity6-mastery §5 no per-frame GC.Alloc). Mirrors the ① CraftingTablePlacement fix.
        private string _promptReady, _promptBadGround, _promptObstructed, _promptNeedMats;

        // The discrete NON-self-registering session obstacles (lit campfire / active ore nodes) discovered once at
        // EnterPlacement via the shared registry scan. A placed table / built forge self-registers via
        // PlacementObstacle (caught by IsFootprintBlocked); ②'s boulders come via the registry too.
        private readonly List<PlacementObstacleRegistry.SessionObstacle> _sessionObstacles =
            new List<PlacementObstacleRegistry.SessionObstacle>(32);
        private bool _navMeshAvailable;
        private bool _hasForcedAim;       // capture/test seam: ghost pose forced (AimGhostAt), cursor ignored
        private Vector3 _forcedAimPos;

        // Dual-channel valid/invalid colours (hue channel; the WORD cue is the colour-independent channel).
        private static readonly Color GhostValid = new Color(0.35f, 0.85f, 0.40f, 0.45f);
        private static readonly Color GhostInvalid = new Color(0.90f, 0.30f, 0.25f, 0.45f);
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);

        /// <summary>True once the forge has been built here. Exposed for tests + capture.</summary>
        public bool HasBuilt => _built || (forge != null && forge.IsBuilt);
        /// <summary>True while the placement ghost is active (aiming). Test/UI seam.</summary>
        public bool IsPlacing => _placing;
        /// <summary>The current ghost pose (world) while placing. Test seam.</summary>
        public Vector3 GhostPosition => _ghostPos;
        /// <summary>The current ghost yaw (deg) while placing. Test seam (scroll-rotation).</summary>
        public float GhostYaw => _ghostYaw;
        /// <summary>Whether the current ghost pose is a valid build spot (ground AND affordability AND unobstructed).</summary>
        public bool IsCurrentPlacementValid => _valid;
        /// <summary>Whether the current ghost footprint overlaps an object (off-navmesh OR a discrete interactable).</summary>
        public bool IsCurrentPlacementObstructed => _obstructed;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (forge == null) forge = FindObjectOfType<Forge>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (ghost != null) _ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            _cam = Camera.main; // cached (MainCamera tag); re-resolved lazily only if null (never per-frame)
            useGUILayout = false; // explicit-Rect OnGUI only
            SetGhostShown(false);
        }

        void OnDisable()
        {
            // Never leave the world-input gate stuck if we're torn down mid-placement.
            UiInputGate.SetPanelOpen(false, ref _gatePlacing);
        }

        void Update()
        {
            if (forge != null && forge.IsBuilt) return; // one forge in ③ — nothing left to place

            if (!_placing)
            {
                // Entering is NOT gated on affordability — enter empty-handed to SEE the "need materials" red cue.
                if (Input.GetKeyDown(buildKey)) EnterPlacement();
                return;
            }

            // Placing — rotate on scroll, track the ghost under the cursor, evaluate validity every frame.
            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                _ghostYaw = CraftingTablePlacement.ApplyRotation(_ghostYaw, scroll, rotationStepDegrees);

            UpdateGhostPose();

            if (Input.GetKeyDown(cancelKey)) { Cancel(); return; }
            if (Input.GetMouseButtonDown(0)) TryConfirm(); // LEFT-CLICK confirms
        }

        /// <summary>True iff the pack can afford the forge (both mats). Read-only.</summary>
        public bool CanAffordForge()
            => inventory != null && CanAfford(inventory.WoodCount, inventory.StoneCount, woodCost, stoneCost);

        /// <summary>
        /// Enter placement mode (show the ghost). No-op if already placing / no forge left to place. NOT gated on
        /// affordability — the ghost reads red "need materials" until the player has enough. Pushes the modal
        /// world-input gate so the confirm left-click + scroll never leak to world verbs / camera zoom.
        /// </summary>
        public void EnterPlacement()
        {
            if (_placing) return;
            if (forge != null && forge.IsBuilt) return;
            _placing = true;
            _hasForcedAim = false;
            _ghostYaw = player != null ? player.eulerAngles.y : 0f; // seed from the player's facing
            BuildPromptLines();                                      // cache the OnGUI prompt strings (no per-frame alloc)
            RefreshObstacleSources();                                // discover pools + navmesh availability (once)
            UiInputGate.SetPanelOpen(true, ref _gatePlacing);        // MODAL — swallow world verbs + camera zoom
            SetGhostShown(true);
            UpdateGhostPose();
        }

        /// <summary>Cancel placement — hide the ghost, release the gate, spend NOTHING.</summary>
        public void Cancel()
        {
            _placing = false;
            _hasForcedAim = false;
            SetGhostShown(false);
            UiInputGate.SetPanelOpen(false, ref _gatePlacing);
        }

        /// <summary>
        /// Confirm the placement: if the pose is valid (ground AND affordable AND unobstructed) debit the mats
        /// all-or-nothing, reveal + raise the forge at the ghost pose, exit placement. Returns true iff the forge
        /// was actually built. An invalid pose stays in placement + builds nothing (keep aiming / gather more).
        /// </summary>
        public bool TryConfirm()
        {
            if (!_placing || forge == null || inventory == null) return false;
            if (!_valid) return false; // invalid pose (ground / affordability / obstruction) — keep aiming, no debit

            // All-or-nothing across BOTH mats (pre-check → debit) — the shipped ForgePlacement idiom.
            if (!CanAffordForge()) return false;
            bool woodOk = inventory.SpendWood(woodCost);
            bool stoneOk = inventory.SpendStone(stoneCost);
            if (!woodOk || !stoneOk) return false; // guarded by CanAffordForge; defensive

            forge.Build(_ghostPos, _ghostRot); // reveal + raise + self-register the no-build zone (#302)
            _built = true;
            _placing = false;
            _hasForcedAim = false;
            SetGhostShown(false);
            UiInputGate.SetPanelOpen(false, ref _gatePlacing);
            Debug.Log("[ForgePlacement] built the forge (paid " + woodCost + " wood + " + stoneCost +
                      " stone; wood now=" + inventory.WoodCount + ", stone now=" + inventory.StoneCount + ")");
            return true;
        }

        /// <summary>
        /// Capture / test seam: FORCE the ghost to a world pose and re-evaluate validity through the production
        /// path — input-independent (mirrors CraftingTablePlacement.AimGhostAt). Live placement drives the ghost
        /// from the cursor; this lets a headless capture / PlayMode test aim it deterministically at a known spot
        /// with no camera or mouse. Sticky until Cancel / confirm. No-op if not placing.
        /// </summary>
        public void AimGhostAt(Vector3 worldPos, float yaw = 0f)
        {
            if (!_placing) return;
            _hasForcedAim = true;
            _forcedAimPos = worldPos;
            _ghostYaw = yaw;
            UpdateGhostPose();
        }

        /// <summary>Convenience capture/test seam: enter placement, aim at <paramref name="worldPos"/>, confirm —
        /// the one-shot "place the forge HERE" the shipped-build capture drives (no cursor/camera). Returns true
        /// iff the forge was built (a valid spot). Mirrors the shipped -verifyForge intent through the new flow.</summary>
        public bool RequestBuildAt(Vector3 worldPos, float yaw = 0f)
        {
            EnterPlacement();
            AimGhostAt(worldPos, yaw);
            return TryConfirm();
        }

        // Track the ghost to the ground UNDER THE MOUSE CURSOR, snapped onto the ground; evaluate validity
        // (ground + affordability + object-overlap). Falls back to a point in front of the player with no camera
        // (headless), or to a forced pose (AimGhostAt — capture/test seam).
        private void UpdateGhostPose()
        {
            Vector3 target;
            float normalY;
            bool groundFound;

            if (_hasForcedAim)
            {
                // Capture/test seam: pose forced to a known spot; assume the ground under it is valid so the
                // OBSTRUCTION dimension is what the frame proves. Cursor/fallback path skipped.
                target = _forcedAimPos; normalY = 1f; groundFound = true;
            }
            else
            {
                Camera cam = ResolveCamera();
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    if (groundMask.value != 0 &&
                        Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
                    {
                        target = hit.point; normalY = hit.normal.y; groundFound = true;
                    }
                    else
                    {
                        float planeY = player != null ? player.position.y : 0f;
                        CraftingTablePlacement.PlaneIntersect(ray, planeY, out target);
                        normalY = groundMask.value != 0 ? 0f : 1f;
                        groundFound = groundMask.value == 0;
                    }
                }
                else
                {
                    target = FrontOfPlayer();
                    normalY = groundMask.value != 0 ? 0f : 1f;
                    groundFound = groundMask.value == 0;
                }
            }

            _ghostPos = target;
            _ghostRot = Quaternion.Euler(0f, _ghostYaw, 0f);

            float dist = player != null
                ? Vector2.Distance(new Vector2(player.position.x, player.position.z),
                                   new Vector2(target.x, target.z))
                : float.MaxValue;
            _groundValid = CraftingTablePlacement.IsGroundValid(groundFound, normalY, dist, minDistFromPlayer, minGroundNormalY);
            _obstructed = ComputeObstruction(_ghostPos);
            _valid = CraftingTablePlacement.IsValidPlacement(groundFound, normalY, dist, minDistFromPlayer,
                                                             minGroundNormalY, CanAffordForge(), _obstructed);

            if (ghost != null)
            {
                ghost.SetPositionAndRotation(_ghostPos, _ghostRot);
                TintGhost(_valid ? GhostValid : GhostInvalid);
            }
        }

        // Rebuild the per-session obstruction sources (once, at EnterPlacement — NOT per frame): navmesh
        // availability + the discrete NON-self-registering pool (lit campfire / active ore nodes) via the shared
        // registry scan. A placed table / built forge self-registers via PlacementObstacle (caught by
        // IsFootprintBlocked); ②'s boulders come via the registry too.
        private void RefreshObstacleSources()
        {
            _navMeshAvailable = ComputeNavMeshAvailable();
            PlacementObstacleRegistry.CollectSessionObstacles(_sessionObstacles);
        }

        // Object-overlap truth (#302): (1) the footprint is OFF the walkable navmesh (trees carve it), OR
        // (2) it overlaps a discrete interactable (session pool) OR a registered PlacementObstacle (a placed
        // table / another built forge / ②'s boulders).
        private bool ComputeObstruction(Vector3 pos)
        {
            if (FootprintOffNavMesh(pos)) return true;
            for (int i = 0; i < _sessionObstacles.Count; i++)
            {
                var o = _sessionObstacles[i];
                if (o.t == null) continue;
                Vector3 p = o.t.position;
                if (PlacementObstacleRegistry.CircleOverlaps(pos.x, pos.z, footprintRadius, p.x, p.z, o.radius))
                    return true;
            }
            return PlacementObstacleRegistry.IsFootprintBlocked(pos, footprintRadius, ghost);
        }

        private bool FootprintOffNavMesh(Vector3 pos)
        {
            if (!_navMeshAvailable) return false;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navSampleMaxDist, NavMesh.AllAreas))
                return hit.distance > offNavMeshTolerance;
            return true;
        }

        // FIXTURE-SAFE navmesh-availability gate (the #302 process-global-CalculateTriangulation trap): a prior
        // PlayMode fixture's baked island navmesh BLEEDS into a synthetic rig, so the on/off decision is driven by
        // THIS instance's serialized groundMask (real mask = shipped world; 0 = a bare rig) — deterministic under
        // ANY fixture order. The triangulation read is retained only as a secondary "a navmesh really is baked" guard.
        private bool ComputeNavMeshAvailable()
        {
            if (groundMask.value == 0) return false; // synthetic/headless rig — no real navmesh context
            var tri = NavMesh.CalculateTriangulation();
            return tri.vertices != null && tri.vertices.Length > 0;
        }

        private Vector3 FrontOfPlayer()
        {
            if (player == null) return Vector3.zero;
            Vector3 fwd = Quaternion.Euler(0f, _ghostYaw, 0f) * Vector3.forward;
            Vector3 t = player.position + fwd * placeDistance;
            t.y = player.position.y;
            return t;
        }

        private Camera ResolveCamera()
        {
            if (_cam == null) _cam = Camera.main; // lazy re-resolve ONLY when null (never per-frame under load)
            return _cam;
        }

        /// <summary>
        /// PURE affordability truth-table (unit-testable without a scene): the forge is affordable IFF the pack
        /// holds AT LEAST woodCost wood AND AT LEAST stoneCost stone. The all-or-nothing gate — both must clear.
        /// Kept (the shipped I-3 ForgeSmeltTests pin this) so a build regression to <c>wood || stone</c> reds.
        /// </summary>
        public static bool CanAfford(int wood, int stone, int woodCost, int stoneCost)
            => wood >= woodCost && stone >= stoneCost;

        // Compose the OnGUI prompt lines ONCE (they depend only on woodCost/stoneCost/cancelKey — fixed for a
        // placement session). OnGUI selects one; it never concatenates per frame (the folded OnGUI GC NIT).
        private void BuildPromptLines()
        {
            string tail = "     [LMB] build   [scroll] rotate   [" + cancelKey + "] cancel";
            _promptReady      = "PLACING forge   [OK] READY to build" + tail;
            _promptBadGround  = "PLACING forge   [X] BLOCKED — move to flat open ground" + tail;
            _promptObstructed = "PLACING forge   [X] BLOCKED — overlaps an object" + tail;
            _promptNeedMats   = "PLACING forge   [X] NEED " + woodCost + " wood + " + stoneCost + " stone" + tail;
        }

        private void SetGhostShown(bool on)
        {
            if (_ghostRenderers == null && ghost != null) _ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
            if (_ghostRenderers == null) return;
            foreach (var r in _ghostRenderers) if (r != null) r.enabled = on;
        }

        private void TintGhost(Color c)
        {
            if (_ghostRenderers == null) return;
            foreach (var r in _ghostRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);
                r.SetPropertyBlock(_mpb);
            }
        }

        void OnGUI()
        {
            if (!_placing) return;
            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _promptStyle.normal.textColor = Cream;
            }

            // DUAL-CHANNEL cue: a WORD + [OK]/[X] marker independent of the ghost's green/red tint. NIT: SELECT a
            // pre-built cached line (no per-OnGUI string concat). Priority: valid > bad-ground > obstructed > need-mats.
            if (_promptReady == null) BuildPromptLines(); // lazy guard (OnGUI can't run before EnterPlacement)
            string line = _valid ? _promptReady
                        : (!_groundValid ? _promptBadGround
                        : (_obstructed ? _promptObstructed : _promptNeedMats));

            const float w = 660f, h = 34f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 130f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 5f, w, h - 8f), line, _promptStyle);
        }
    }
}
