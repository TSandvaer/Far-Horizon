using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// The campfire PLACE-TO-BUILD flow (ticket 86camz9w7 / crafting-redesign ⑤ — the unified invisible-until-placed
    /// system, spec §0.1/§2). REWRITE of the shipped fixed-spot proximity build (U2-4 86ca8bdep): the Sponsor-locked
    /// design makes the campfire one of the THREE invisible-until-placed structures — there is NO pre-visible fire
    /// pit. The campfire now uses the SAME place-to-build flow as the ① crafting table + ③ forge: gather wood+stone →
    /// open the build menu (C, 86catpvpa) → select the Campfire row → a translucent GHOST tracks the ground under the
    /// MOUSE CURSOR → LEFT-CLICK to CONFIRM (debits the mats all-or-nothing, reveals + LIGHTS the campfire at the ghost
    /// pose) or Escape to cancel with no debit. Placing == lighting: the mats buy a LIT fire (Campfire.Build → Light).
    ///
    /// === Reuses the ① placement system VERBATIM (spec §2 constraint 1 — no parallel flow) ===
    /// A focused SIBLING of <see cref="ForgePlacement"/> / <see cref="CraftingTablePlacement"/> — it reuses ①'s proven
    /// PURE truth-tables (<see cref="CraftingTablePlacement.IsGroundValid"/> / <see cref="CraftingTablePlacement.
    /// IsValidPlacement"/> / <see cref="CraftingTablePlacement.ApplyRotation"/> / <see cref="CraftingTablePlacement.
    /// PlaneIntersect"/>), the shared affordability truth-table (<see cref="ForgePlacement.CanAfford"/>), the shared
    /// obstruction seam (<see cref="PlacementObstacleRegistry"/> + its <see cref="PlacementObstacleRegistry.
    /// CollectSessionObstacles"/> scan), and the same UiInputGate-modal ghost idiom. (A common StructurePlacement base
    /// for all three is a deferred cleanup — kept separate here so this ⑤ PR does not churn the just-soaked ①/③
    /// placements; each is guarded by its own scene + validity tests.)
    ///
    /// === Keeps the shipped warmth/campfire runtime (spec §2 constraint 2 — placement FRONT only) ===
    /// The confirm ends in the SAME all-or-nothing <see cref="Inventory.SpendWood"/> / <see cref="Inventory.SpendStone"/>
    /// debit the table/forge builds use — it does not fork it — then hands off to <see cref="Campfire.Build"/>, which
    /// reveals the structure and calls the UNCHANGED <see cref="Campfire.Light"/> (warmth-binding) seam. The lit fire's
    /// proximity warmth-restore runtime is untouched (the regression boundary — CampfirePlayModeTests stays green).
    ///
    /// === Cost re-aligned to the vision's "stone AND wood" (NIT-3 — 🎚️ Predict-Before-Soak) ===
    /// The campfire now costs wood AND stone (default 3 wood + 2 stone, <see cref="CampfireWoodCostDefault"/>/
    /// <see cref="CampfireStoneCostDefault"/>) — the vision + the campfire's own mesh (a ring of fire-STONES around
    /// crossed LOGS) both read "stone and wood", and it stays the CHEAPEST structure (5 mats; table 8, forge 18). The
    /// all-or-nothing gate is the load-bearing negative case (short of EITHER mat → no fire, no debit). 🎚️ Sponsor-soak
    /// tunes; the 3-wood-only baseline is the fallback if the stone reads as friction.
    ///
    /// === OBJECT-OVERLAP invalid rule + the #302 seam ===
    /// The ghost reads INVALID (red + "[X] BLOCKED — overlaps an object") when its footprint is OFF the walkable
    /// NavMesh (a tree carve / off the island) OR overlaps a discrete interactable / a registered
    /// <see cref="PlacementObstacle"/> (a placed table / built forge / another campfire; ②'s boulders adopt the same
    /// seam). The NavMesh path is gated on THIS instance's serialized <see cref="groundMask"/> (a real mask = the
    /// shipped world; 0 = a synthetic rig) — NOT the process-global triangulation, which BLEEDS across PlayMode
    /// fixtures (the #302 fixture-bleed lesson).
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// This component + the (hidden) real campfire + the (hidden) ghost + the Inventory/Campfire/player/warmth refs are
    /// authored editor-time into Boot.unity (MovementCameraScene.BuildCampfire), NOT at Awake. NO mutable statics. The
    /// camera is resolved via the MainCamera tag (cached, never per-frame).
    /// </summary>
    public class CampfirePlacement : MonoBehaviour, IBuildPlaceable
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger the wood + stone campfire cost is debited from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The (invisible-until-placed) real campfire this flow reveals + lights on confirm. Wired at " +
                 "bootstrap; scene-found fallback.")]
        public Campfire campfire;

        [Tooltip("The player transform. Used for the off-self distance check + the no-camera fallback pose. Wired " +
                 "at bootstrap; ClickToMove-root fallback.")]
        public Transform player;

        [Tooltip("The translucent GHOST root shown during placement (a see-through copy of the fire pit). Hidden " +
                 "outside placement; tracks the ground under the cursor; tinted valid/invalid. Wired at bootstrap.")]
        public Transform ghost;

        [Tooltip("The need the lit campfire restores — passed into Campfire.Build → Light so the fire is bound to " +
                 "it. Wired at bootstrap; falls back to a scene search.")]
        public WarmthNeed warmth;

        [Header("Build cost (🎚️ default — Sponsor-soak tunes; NIT-3 — the vision's 'stone AND wood')")]
        [Tooltip("Wood the campfire costs (default 3 — the crossed logs; the §5 baseline, affordable from one felled tree).")]
        public int woodCost = CampfireWoodCostDefault;
        [Tooltip("Stone the campfire costs (default 2 — the ring of fire-stones; the STONE half of the vision's " +
                 "'stone AND wood'. Kept small so the campfire stays the CHEAPEST structure).")]
        public int stoneCost = CampfireStoneCostDefault;

        [Header("Placement")]
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
        [Tooltip("Cosine of the max ground slope the campfire may stand on (0.85 ≈ 32°). Steeper / no ground reads BLOCKED.")]
        public float minGroundNormalY = 0.85f;

        [Header("Object-overlap (#302 seam — red over objects)")]
        [Tooltip("Planar (XZ) radius of the campfire's footprint for the object-overlap test (~the fire-stone ring). " +
                 "Larger = redder near objects. Soak-tune. Default 0.7.")]
        public float footprintRadius = 0.7f;
        [Tooltip("NavMesh sample search radius under the footprint. If no walkable navmesh is found within this, or " +
                 "the nearest is farther than the tolerance, the spot reads BLOCKED. Soak-tune. Default 1.5.")]
        public float navSampleMaxDist = 1.5f;
        [Tooltip("How far the footprint centre may be from the nearest walkable navmesh before it reads BLOCKED. " +
                 "Soak-tune. Default 0.35.")]
        public float offNavMeshTolerance = 0.35f;

        /// <summary>Default campfire WOOD cost (spec §5 baseline — the crossed logs) — 🎚️ Sponsor-soak tunes. Kept
        /// &lt;= one felled tree's yield so the loop closes from one chop session.</summary>
        public const int CampfireWoodCostDefault = 3;
        /// <summary>Default campfire STONE cost (NIT-3 — the ring of fire-stones, the vision's "stone AND wood") —
        /// 🎚️ Sponsor-soak tunes. Small so the campfire stays the CHEAPEST structure (below the table's 3).</summary>
        public const int CampfireStoneCostDefault = 2;

        private bool _built;
        private bool _placing;
        private Vector3 _ghostPos;
        private float _ghostYaw;
        private Quaternion _ghostRot = Quaternion.identity;
        private bool _groundValid;   // ground found + flat enough + off self (the spatial part of validity)
        private bool _obstructed;    // footprint off-navmesh OR over a discrete interactable (#302)
        private bool _valid;         // _groundValid AND CanAffordCampfire() AND !_obstructed (full validity)
        private Renderer[] _ghostRenderers;
        private MaterialPropertyBlock _mpb;
        private GUIStyle _promptStyle;
        private Camera _cam;
        private bool _gatePlacing;   // tracks our UiInputGate push so it can never stick open

        // Cached OnGUI prompt lines (the lines depend ONLY on woodCost/stoneCost/cancelKey, fixed for a placement
        // session, so BUILD them ONCE on EnterPlacement + CACHE them; OnGUI selects one, never concats per frame —
        // unity6-mastery §5 no per-frame GC.Alloc). Mirrors the ①/③ placement fix.
        private string _promptReady, _promptBadGround, _promptObstructed, _promptNeedMats;

        // The discrete NON-self-registering session obstacles (a built forge / active ore nodes) discovered once at
        // EnterPlacement via the shared registry scan. A placed table / built forge / placed campfire self-registers
        // via PlacementObstacle (caught by IsFootprintBlocked); ②'s boulders come via the registry too.
        private readonly List<PlacementObstacleRegistry.SessionObstacle> _sessionObstacles =
            new List<PlacementObstacleRegistry.SessionObstacle>(32);
        private bool _navMeshAvailable;
        private bool _hasForcedAim;       // capture/test seam: ghost pose forced (AimGhostAt), cursor ignored
        private Vector3 _forcedAimPos;

        // Dual-channel valid/invalid colours (hue channel; the WORD cue is the colour-independent channel).
        private static readonly Color GhostValid = new Color(0.35f, 0.85f, 0.40f, 0.45f);
        private static readonly Color GhostInvalid = new Color(0.90f, 0.30f, 0.25f, 0.45f);
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);

        /// <summary>True once the campfire has been placed here. Exposed for tests + capture.</summary>
        public bool HasBuilt => _built || (campfire != null && campfire.IsPlaced);
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
            if (campfire == null) campfire = FindObjectOfType<Campfire>();
            if (warmth == null) warmth = FindObjectOfType<WarmthNeed>();
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
            if (campfire != null && campfire.IsPlaced) return; // one campfire in ⑤ — nothing left to place

            if (!_placing)
            {
                // The BUILD MENU (BuildMenuUI, C) is the SINGLE build entry point (86catpvpa) — it calls
                // EnterPlacement() when the Campfire row is selected. No self-poll build key here (the interim
                // proximity fixed-spot build is RETIRED). Placement owns Escape/scroll/LMB once active.
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

        /// <summary>True iff the pack can afford the campfire (both mats). Read-only.</summary>
        public bool CanAffordCampfire()
            => inventory != null &&
               ForgePlacement.CanAfford(inventory.WoodCount, inventory.StoneCount, woodCost, stoneCost);

        // ============================================================================================
        // IBuildPlaceable — the shared build-menu seam (86catpvpa). BuildMenuUI lists this as a row and,
        // on an affordable selection, calls BeginBuildPlacement() → the ① ghost flow (EnterPlacement).
        // ============================================================================================

        /// <summary>IBuildPlaceable: the row label + per-structure identifier.</summary>
        public string BuildDisplayName => "Campfire";
        /// <summary>IBuildPlaceable: wood cost (the §5 baseline; soak-tunable).</summary>
        public int BuildWoodCost => woodCost;
        /// <summary>IBuildPlaceable: stone cost (NIT-3 — the vision's "stone AND wood"; soak-tunable).</summary>
        public int BuildStoneCost => stoneCost;
        /// <summary>IBuildPlaceable: affordable iff the pack can afford the campfire (both mats).</summary>
        public bool CanAffordBuild => CanAffordCampfire();
        /// <summary>IBuildPlaceable: built once the campfire is placed (one campfire in ⑤).</summary>
        public bool IsBuildComplete => HasBuilt;
        /// <summary>IBuildPlaceable: enter the ① free-cursor ghost-placement flow (== EnterPlacement).</summary>
        public void BeginBuildPlacement() => EnterPlacement();

        /// <summary>
        /// Enter placement mode (show the ghost). No-op if already placing / no campfire left to place. NOT gated on
        /// affordability — the ghost reads red "need materials" until the player has enough. Pushes the modal
        /// world-input gate so the confirm left-click + scroll never leak to world verbs / camera zoom.
        /// </summary>
        public void EnterPlacement()
        {
            if (_placing) return;
            if (campfire != null && campfire.IsPlaced) return;
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
        /// all-or-nothing, reveal + LIGHT the campfire at the ghost pose, exit placement. Returns true iff the
        /// campfire was actually built. An invalid pose stays in placement + builds nothing (keep aiming / gather more).
        /// </summary>
        public bool TryConfirm()
        {
            if (!_placing || campfire == null || inventory == null) return false;
            if (!_valid) return false; // invalid pose (ground / affordability / obstruction) — keep aiming, no debit

            // All-or-nothing across BOTH mats (pre-check → debit) — the shipped ForgePlacement idiom.
            if (!CanAffordCampfire()) return false;
            bool woodOk = inventory.SpendWood(woodCost);
            bool stoneOk = inventory.SpendStone(stoneCost);
            if (!woodOk || !stoneOk) return false; // guarded by CanAffordCampfire; defensive

            campfire.Build(_ghostPos, _ghostRot, warmth); // reveal + LIGHT + self-register the no-build zone (#302)
            _built = true;
            _placing = false;
            _hasForcedAim = false;
            SetGhostShown(false);
            UiInputGate.SetPanelOpen(false, ref _gatePlacing);
            Debug.Log("[CampfirePlacement] built + lit the campfire (paid " + woodCost + " wood + " + stoneCost +
                      " stone; wood now=" + inventory.WoodCount + ", stone now=" + inventory.StoneCount + ")");
            return true;
        }

        /// <summary>
        /// Capture / test seam: FORCE the ghost to a world pose and re-evaluate validity through the production
        /// path — input-independent (mirrors ForgePlacement.AimGhostAt). Live placement drives the ghost from the
        /// cursor; this lets a headless capture / PlayMode test aim it deterministically at a known spot with no
        /// camera or mouse. Sticky until Cancel / confirm. No-op if not placing.
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
        /// the one-shot "place the campfire HERE" the shipped-build capture drives (no cursor/camera). Returns true
        /// iff the campfire was built + lit (a valid spot). Mirrors ForgePlacement.RequestBuildAt.</summary>
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
                                                             minGroundNormalY, CanAffordCampfire(), _obstructed);

            if (ghost != null)
            {
                ghost.SetPositionAndRotation(_ghostPos, _ghostRot);
                TintGhost(_valid ? GhostValid : GhostInvalid);
            }
        }

        // Rebuild the per-session obstruction sources (once, at EnterPlacement — NOT per frame): navmesh
        // availability + the discrete NON-self-registering pool (a built forge / active ore nodes) via the shared
        // registry scan. A placed table / built forge / placed campfire self-registers via PlacementObstacle
        // (caught by IsFootprintBlocked); ②'s boulders come via the registry too.
        private void RefreshObstacleSources()
        {
            _navMeshAvailable = ComputeNavMeshAvailable();
            PlacementObstacleRegistry.CollectSessionObstacles(_sessionObstacles);
        }

        // Object-overlap truth (#302): (1) the footprint is OFF the walkable navmesh (trees carve it), OR
        // (2) it overlaps a discrete interactable (session pool) OR a registered PlacementObstacle (a placed
        // table / built forge / another campfire / ②'s boulders).
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

        // Compose the OnGUI prompt lines ONCE (they depend only on woodCost/stoneCost/cancelKey — fixed for a
        // placement session). OnGUI selects one; it never concatenates per frame (the folded OnGUI GC NIT).
        private void BuildPromptLines()
        {
            string tail = "     [LMB] build   [scroll] rotate   [" + cancelKey + "] cancel";
            _promptReady      = "PLACING campfire   [OK] READY to build" + tail;
            _promptBadGround  = "PLACING campfire   [X] BLOCKED — move to flat open ground" + tail;
            _promptObstructed = "PLACING campfire   [X] BLOCKED — overlaps an object" + tail;
            _promptNeedMats   = "PLACING campfire   [X] NEED " + woodCost + " wood + " + stoneCost + " stone" + tail;
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

            // DUAL-CHANNEL cue: a WORD + [OK]/[X] marker independent of the ghost's green/red tint. SELECT a
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
