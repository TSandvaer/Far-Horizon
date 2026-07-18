using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The PLACE-TO-BUILD flow for the crafting table (ticket 86camz9uz / crafting-redesign ① — the unified
    /// invisible-until-placed system, spec §2). Gather wood + stone → press the build key → a translucent
    /// GHOST of the table tracks the ground UNDER THE MOUSE CURSOR → LEFT-CLICK to CONFIRM (debits the
    /// materials all-or-nothing, reveals the real table at the ghost pose) or Escape to cancel with no debit.
    /// Retires the pre-placed <c>CraftSpot</c> stump: there is no marker to see before placement.
    ///
    /// === FREE-CURSOR mouse ghost placement (soak fix F1/F2/F3, 2026-07-18) ===
    /// The shipped-first build tracked the ghost IN FRONT OF THE PLAYER (walk-anchored) with a keyboard
    /// confirm — the Sponsor's soak REJECTED that. The Sponsor-accepted default (§7-A) is FREE-CURSOR:
    ///   • F1 — the ghost tracks the ground under the MOUSE CURSOR (a camera ray → ground raycast);
    ///   • F2 — LEFT-CLICK confirms the placement; ESCAPE cancels;
    ///   • F3 — the SCROLL WHEEL rotates the ghost (yaw), gated so the camera zoom is unaffected.
    /// While placing, this pushes the <see cref="UiInputGate"/> world-input gate (modal) so (a) the confirm
    /// left-click can NEVER leak to a world verb (chop/mine/attack), and (b) the scroll wheel never zooms the
    /// camera (the OrbitCamera swallows scroll while <see cref="UiInputGate.CaptureWorldInput"/> is set — the
    /// F3 "UiInputGate pattern"). Placement reads its OWN Input.* directly, so the gate never blocks it.
    /// A full free-cursor build MENU per structure is the ②/③ generalisation (`86catpvpa`).
    ///
    /// === Reuses the proven debit seam (spec §2 regression boundary) ===
    /// The confirm ends in the SAME all-or-nothing <see cref="Inventory.SpendWood"/> / <see cref="Inventory.SpendStone"/>
    /// debit the campfire/forge builds use — it does not fork it. On success it reveals the <see cref="CraftingTable"/>
    /// + opens the <see cref="CraftingMenuUI"/> (an explicit handoff, so the menu-open never races the confirm frame).
    ///
    /// === Real invalid rules incl. insufficient materials (soak fix F4) + Uma NIT-2 (kid + colour-blind) ===
    /// The ghost reads valid ONLY when: ground was found under the cursor, the ground is flat enough, the pose
    /// is off the player, AND the pack can afford the table. Insufficient materials therefore reads INVALID
    /// (red) too. To make that state reachable, entering placement is NO LONGER gated on affordability — the
    /// player can enter empty-handed and SEE the "need N wood + M stone" cue. The valid/invalid feedback is
    /// DUAL-CHANNEL: the ghost tint (green/red — hue) PLUS a WORD + [OK]/[X] marker in the prompt that names
    /// the SPECIFIC block reason (bad ground vs missing materials) independent of colour.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// This component + the (hidden) real table + the (hidden) ghost + the Inventory/player/menu refs are
    /// authored editor-time into Boot.unity (MovementCameraScene), NOT at Awake. NO mutable statics. The camera
    /// is resolved via the MainCamera tag (cached, never per-frame — unity6-mastery §5).
    /// </summary>
    public class CraftingTablePlacement : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger the wood + stone table cost is debited from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform. Used for the off-self distance check + the no-camera fallback pose. " +
                 "Wired at bootstrap; ClickToMove-root fallback.")]
        public Transform player;

        [Tooltip("The (invisible-until-placed) real table this flow reveals on confirm. Wired at bootstrap.")]
        public CraftingTable table;

        [Tooltip("The translucent GHOST root shown during placement (a see-through copy of the table). Hidden " +
                 "outside placement; tracks the ground under the cursor; tinted valid/invalid. Wired at bootstrap.")]
        public Transform ghost;

        [Tooltip("The recipe menu opened when the table is placed (explicit handoff). Optional; scene-found fallback.")]
        public CraftingMenuUI menu;

        [Header("Cost (🎚️ default — Sponsor-soak tunes; §5)")]
        [Tooltip("Wood the table costs (default 5, bootstrap-affordable from hand-gathered sticks).")]
        public int woodCost = CraftingRecipeBook.TableWoodCost;
        [Tooltip("Stone the table costs (default 3, bootstrap-affordable from hand-gathered pebbles).")]
        public int stoneCost = CraftingRecipeBook.TableStoneCost;

        [Header("Placement")]
        [Tooltip("Key to ENTER placement (when no table is built yet). C — a Danish-safe letter, free of the " +
                 "gameplay keys. INTERIM: the general 'build menu of placeables' (also C) is a follow-up " +
                 "ticket (86catpvpa); this direct key stands in until then. Confirm/cancel are mouse+Escape (F2).")]
        public KeyCode buildKey = KeyCode.C;
        [Tooltip("Key to cancel placement (exit with no debit).")]
        public KeyCode cancelKey = KeyCode.Escape;
        [Tooltip("Degrees the ghost yaw rotates per scroll notch (F3 — the scroll wheel rotates the ghost while " +
                 "placing; the camera zoom is gated off via UiInputGate). Sign-based so it is one predictable step " +
                 "per notch regardless of the platform's scroll magnitude.")]
        public float rotationStepDegrees = 15f;
        [Tooltip("How far in front of the player the ghost floats in the NO-CAMERA fallback (headless test rig " +
                 "only — the live build tracks the mouse cursor).")]
        public float placeDistance = 2.5f;
        [Tooltip("Ground layer(s) the mouse-cursor ray hits / the ghost snaps onto. If 0/unset, a flat-ground " +
                 "fallback at the player's height is used (test rig).")]
        public LayerMask groundMask;
        [Tooltip("Minimum planar distance the ghost must be from the player to be valid (never build on top of self).")]
        public float minDistFromPlayer = 1.0f;
        [Tooltip("Cosine of the max ground slope the table may stand on (0.85 ≈ 32°). Steeper ground / no ground " +
                 "(e.g. over water or off the island edge) reads BLOCKED — the invalid cue.")]
        public float minGroundNormalY = 0.85f;

        private bool _placing;
        private Vector3 _ghostPos;
        private float _ghostYaw;
        private Quaternion _ghostRot = Quaternion.identity;
        private bool _groundValid;   // ground found + flat enough + off self (the spatial part of validity)
        private bool _valid;         // _groundValid AND CanAffordTable() (the full F4 validity)
        private Renderer[] _ghostRenderers;
        private MaterialPropertyBlock _mpb;
        private GUIStyle _promptStyle;
        private Camera _cam;
        private bool _gatePlacing;   // tracks our UiInputGate push so it can never stick open

        // FOLDED NIT (② — Drew's PR #294 review, comment 4919859554): the three OnGUI prompt lines depend ONLY on
        // woodCost/stoneCost/cancelKey (fixed for a placement session), so BUILD them ONCE on EnterPlacement and
        // CACHE them — OnGUI (which runs multiple times per frame while placing) then just SELECTS one, instead of
        // concatenating fresh strings every OnGUI call (unity6-mastery §5 no per-frame GC.Alloc).
        private string _promptReady, _promptBadGround, _promptNeedMats;

        // Dual-channel valid/invalid colours (hue channel; the WORD cue is the colour-independent channel).
        private static readonly Color GhostValid = new Color(0.35f, 0.85f, 0.40f, 0.45f);
        private static readonly Color GhostInvalid = new Color(0.90f, 0.30f, 0.25f, 0.45f);
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);

        /// <summary>True while the placement ghost is active (aiming). Test/UI seam.</summary>
        public bool IsPlacing => _placing;
        /// <summary>The current ghost pose (world) while placing. Test seam.</summary>
        public Vector3 GhostPosition => _ghostPos;
        /// <summary>The current ghost yaw (deg) while placing. Test seam (scroll-rotation).</summary>
        public float GhostYaw => _ghostYaw;
        /// <summary>Whether the current ghost pose is a valid build spot (ground AND affordability). Test/prompt seam.</summary>
        public bool IsCurrentPlacementValid => _valid;

        void Awake()
        {
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (player == null)
            {
                var ctm = FindObjectOfType<ClickToMove>();
                if (ctm != null) player = ctm.transform;
            }
            if (table == null) table = FindObjectOfType<CraftingTable>();
            if (menu == null) menu = FindObjectOfType<CraftingMenuUI>();
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
            if (table != null && table.IsBuilt) return; // one table in ① — nothing left to place

            if (!_placing)
            {
                // F4: entering is NOT gated on affordability — enter empty-handed to SEE the "need materials"
                // red cue. Confirm is what enforces the cost.
                if (Input.GetKeyDown(buildKey)) EnterPlacement();
                return;
            }

            // Placing — rotate on scroll, track the ghost under the cursor, evaluate validity every frame.
            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f) _ghostYaw = ApplyRotation(_ghostYaw, scroll, rotationStepDegrees);

            UpdateGhostPose();

            if (Input.GetKeyDown(cancelKey)) { Cancel(); return; }
            if (Input.GetMouseButtonDown(0)) TryConfirm(); // F2 — LEFT-CLICK confirms
        }

        /// <summary>True iff the pack can afford the table (both mats). Read-only.</summary>
        public bool CanAffordTable()
            => inventory != null &&
               ForgePlacement.CanAfford(inventory.WoodCount, inventory.StoneCount, woodCost, stoneCost);

        /// <summary>
        /// Enter placement mode (show the ghost). No-op if already placing / no table left to place. NOT gated
        /// on affordability (F4) — the ghost then reads red "need materials" until the player has enough. Pushes
        /// the modal world-input gate so the confirm left-click + scroll never leak to world verbs / camera zoom.
        /// </summary>
        public void EnterPlacement()
        {
            if (_placing) return;
            if (table != null && table.IsBuilt) return;
            _placing = true;
            _ghostYaw = player != null ? player.eulerAngles.y : 0f; // seed from the player's facing
            BuildPromptLines();                                      // cache the OnGUI prompt strings (NIT — no per-frame alloc)
            UiInputGate.SetPanelOpen(true, ref _gatePlacing);        // MODAL — swallow world verbs + camera zoom
            SetGhostShown(true);
            UpdateGhostPose();
        }

        /// <summary>Cancel placement — hide the ghost, release the gate, spend NOTHING.</summary>
        public void Cancel()
        {
            _placing = false;
            SetGhostShown(false);
            UiInputGate.SetPanelOpen(false, ref _gatePlacing);
        }

        /// <summary>
        /// Confirm the placement: if the pose is valid (ground AND affordable) debit the mats all-or-nothing,
        /// reveal the table at the ghost pose, exit placement, and open the menu. Returns true iff the table was
        /// actually built. An invalid pose (bad ground OR short mats) stays in placement + builds nothing (the
        /// player keeps aiming / gathers more). Test seam (Update calls it on the left-click).
        /// </summary>
        public bool TryConfirm()
        {
            if (!_placing || table == null || inventory == null) return false;
            if (!_valid) return false; // invalid pose (ground or affordability) — keep aiming, no debit

            // All-or-nothing across BOTH mats (pre-check → debit) — the ForgePlacement idiom.
            if (!CanAffordTable()) return false;
            bool woodOk = inventory.SpendWood(woodCost);
            bool stoneOk = inventory.SpendStone(stoneCost);
            if (!woodOk || !stoneOk) return false; // guarded by CanAffordTable; defensive

            table.Reveal(_ghostPos, _ghostRot);
            _placing = false;
            SetGhostShown(false);
            UiInputGate.SetPanelOpen(false, ref _gatePlacing); // release our gate before the menu pushes its own
            if (menu != null) menu.Open(); // explicit handoff — never races the confirm frame
            return true;
        }

        // Track the ghost to the ground UNDER THE MOUSE CURSOR (F1), snapped onto the ground; evaluate validity.
        // Falls back to a point in front of the player when no camera is resolvable (headless test rig).
        private void UpdateGhostPose()
        {
            Vector3 target;
            float normalY;
            bool groundFound;

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
                    // No ground hit under the cursor: project onto a flat plane at the player's height. Under a
                    // REAL ground mask a miss means "not over ground" (over water / off the edge / at the sky) →
                    // invalid. With no mask wired (test rig) the plane IS the ground → valid.
                    float planeY = player != null ? player.position.y : 0f;
                    PlaneIntersect(ray, planeY, out target);
                    normalY = groundMask.value != 0 ? 0f : 1f;
                    groundFound = groundMask.value == 0;
                }
            }
            else
            {
                // No-camera fallback (headless): a point in front of the player, flat-ground height.
                target = FrontOfPlayer();
                normalY = groundMask.value != 0 ? 0f : 1f;
                groundFound = groundMask.value == 0;
            }

            _ghostPos = target;
            _ghostRot = Quaternion.Euler(0f, _ghostYaw, 0f);

            float dist = player != null
                ? Vector2.Distance(new Vector2(player.position.x, player.position.z),
                                   new Vector2(target.x, target.z))
                : float.MaxValue;
            _groundValid = IsGroundValid(groundFound, normalY, dist, minDistFromPlayer, minGroundNormalY);
            _valid = _groundValid && CanAffordTable();

            if (ghost != null)
            {
                ghost.SetPositionAndRotation(_ghostPos, _ghostRot);
                TintGhost(_valid ? GhostValid : GhostInvalid);
            }
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
        /// PURE ground-validity truth-table (the spatial part of placement validity — unit-testable without a
        /// scene): valid iff ground was found, the ground is flat enough (<paramref name="normalY"/> ≥
        /// <paramref name="minNormalY"/>), and the ghost is at least <paramref name="minDist"/> from the player
        /// (never on top of self). Affordability is layered on top by <see cref="IsValidPlacement"/>.
        /// </summary>
        public static bool IsGroundValid(bool groundFound, float normalY, float distFromPlayer,
                                         float minDist, float minNormalY)
            => groundFound && normalY >= minNormalY && distFromPlayer >= minDist;

        /// <summary>
        /// PURE full placement-validity truth-table (F4): the ground is valid (<see cref="IsGroundValid"/>) AND
        /// the pack can afford the table (<paramref name="canAfford"/>). Insufficient materials therefore reads
        /// INVALID (red). Static + dependency-free so the EditMode guard pins every cell without a scene.
        /// </summary>
        public static bool IsValidPlacement(bool groundFound, float normalY, float distFromPlayer,
                                            float minDist, float minNormalY, bool canAfford)
            => IsGroundValid(groundFound, normalY, distFromPlayer, minDist, minNormalY) && canAfford;

        /// <summary>
        /// PURE ghost-yaw rotation step (F3): rotate <paramref name="currentYaw"/> by one <paramref name="step"/>
        /// in the SIGN of <paramref name="scrollDelta"/> (so it is one predictable notch regardless of the
        /// platform's raw scroll magnitude), normalized to [0, 360). Static + dependency-free (EditMode-tested).
        /// </summary>
        public static float ApplyRotation(float currentYaw, float scrollDelta, float step)
        {
            float y = currentYaw;
            if (Mathf.Abs(scrollDelta) > 0.0001f) y += Mathf.Sign(scrollDelta) * step;
            y %= 360f;
            if (y < 0f) y += 360f;
            return y;
        }

        /// <summary>
        /// PURE ray/horizontal-plane intersection (the F1 no-ground-hit fallback — testable without physics):
        /// intersect <paramref name="ray"/> with the plane y = <paramref name="planeY"/>. Returns false (and the
        /// ray origin) when the ray is parallel to the plane or points away from it.
        /// </summary>
        public static bool PlaneIntersect(Ray ray, float planeY, out Vector3 hit)
        {
            float dy = ray.direction.y;
            if (Mathf.Abs(dy) < 1e-6f) { hit = ray.origin; return false; }
            float t = (planeY - ray.origin.y) / dy;
            if (t < 0f) { hit = ray.origin; return false; }
            hit = ray.origin + ray.direction * t;
            return true;
        }

        // NIT (② — no per-frame alloc): compose the three OnGUI prompt lines ONCE (they depend only on
        // woodCost/stoneCost/cancelKey, fixed for a placement session). OnGUI selects one; it never concatenates.
        private void BuildPromptLines()
        {
            string tail = "     [LMB] build   [scroll] rotate   [" + cancelKey + "] cancel";
            _promptReady     = "PLACING crafting table   [OK] READY to build" + tail;
            _promptBadGround = "PLACING crafting table   [X] BLOCKED — move to flat open ground" + tail;
            _promptNeedMats  = "PLACING crafting table   [X] NEED " + woodCost + " wood + " + stoneCost + " stone" + tail;
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

            // DUAL-CHANNEL cue (Uma NIT-2): a WORD + [OK]/[X] marker independent of the ghost's green/red tint.
            // The block reason is SPECIFIC (bad ground vs missing materials) so the colour-blind/kid read is
            // unambiguous (F4). NIT: SELECT a pre-built cached line (no per-OnGUI string concat).
            if (_promptReady == null) BuildPromptLines(); // lazy guard (OnGUI can't run before EnterPlacement, but be safe)
            string line = _valid ? _promptReady : (!_groundValid ? _promptBadGround : _promptNeedMats);

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
