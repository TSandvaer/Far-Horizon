using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The PLACE-TO-BUILD flow for the crafting table (ticket 86camz9uz / crafting-redesign ① — the unified
    /// invisible-until-placed system, spec §2). Gather wood + stone → press the build key → a translucent
    /// GHOST of the table tracks the ground in front of the castaway → press build again to CONFIRM (debits
    /// the materials all-or-nothing, reveals the real table at the ghost pose) or cancel to exit with no
    /// debit. Retires the pre-placed <c>CraftSpot</c> stump: there is no marker to see before placement.
    ///
    /// === Keyboard-driven (no mouse) so it never collides with the world left-click verbs ===
    /// Confirm/cancel are KEYS (build key + Escape), not a mouse click — so entering placement never fires a
    /// chop/mine/consume left-click, and the ghost tracks IN FRONT OF THE PLAYER (WASD stays live to aim it).
    /// This is the ①-foundation mechanic; a full free-cursor build menu per structure is the ②/③
    /// generalisation (§7-A). 🎚️ default (ghost + confirm) — Sponsor-soak tunes.
    ///
    /// === Reuses the proven debit seam (spec §2 regression boundary) ===
    /// The confirm ends in the SAME all-or-nothing <see cref="Inventory.SpendWood"/> / <see cref="Inventory.SpendStone"/>
    /// debit the campfire/forge builds use — it does not fork it. On success it reveals the <see cref="CraftingTable"/>
    /// + opens the <see cref="CraftingMenuUI"/> (an explicit handoff, so the menu-open never races the confirm frame).
    ///
    /// === Uma NIT-2 (kid + colour-blind audience) ===
    /// The valid/invalid feedback is DUAL-CHANNEL: the ghost tint (green/red — hue) PLUS a WORD cue in the
    /// on-screen prompt ("READY to build" vs "BLOCKED — move to flat open ground") + a distinct [OK]/[X]
    /// marker — the word/marker conveys the state independent of colour.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// This component + the (hidden) real table + the (hidden) ghost + the Inventory/player/menu refs are
    /// authored editor-time into Boot.unity (MovementCameraScene), NOT at Awake. NO mutable statics.
    /// </summary>
    public class CraftingTablePlacement : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The ledger the wood + stone table cost is debited from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The player transform the ghost tracks in front of. Wired at bootstrap; ClickToMove-root fallback.")]
        public Transform player;

        [Tooltip("The (invisible-until-placed) real table this flow reveals on confirm. Wired at bootstrap.")]
        public CraftingTable table;

        [Tooltip("The translucent GHOST root shown during placement (a see-through copy of the table). Hidden " +
                 "outside placement; tracks the ground in front of the player; tinted valid/invalid. Wired at bootstrap.")]
        public Transform ghost;

        [Tooltip("The recipe menu opened when the table is placed (explicit handoff). Optional; scene-found fallback.")]
        public CraftingMenuUI menu;

        [Header("Cost (🎚️ default — Sponsor-soak tunes; §5)")]
        [Tooltip("Wood the table costs (default 5, bootstrap-affordable from hand-gathered sticks).")]
        public int woodCost = CraftingRecipeBook.TableWoodCost;
        [Tooltip("Stone the table costs (default 3, bootstrap-affordable from hand-gathered pebbles).")]
        public int stoneCost = CraftingRecipeBook.TableStoneCost;

        [Header("Placement")]
        [Tooltip("Key to enter placement (when affordable + no table yet) AND to confirm the placement. C — a " +
                 "Danish-safe letter, free of the gameplay keys (WASD/Space/Shift/1-9/E/Q/B/Tab/F-keys).")]
        public KeyCode buildKey = KeyCode.C;
        [Tooltip("Key to cancel placement (exit with no debit).")]
        public KeyCode cancelKey = KeyCode.Escape;
        [Tooltip("How far in front of the player the ghost floats (world units).")]
        public float placeDistance = 2.5f;
        [Tooltip("Ground layer(s) the ghost snaps down onto (a downward raycast). If 0/unset, the ghost sits at " +
                 "the player's ground height (flat-ground fallback).")]
        public LayerMask groundMask;
        [Tooltip("Minimum planar distance the ghost must be from the player to be valid (never build on top of self).")]
        public float minDistFromPlayer = 1.0f;
        [Tooltip("Cosine of the max ground slope the table may stand on (0.85 ≈ 32°). Steeper ground / no ground " +
                 "(e.g. over water or off the island edge) reads BLOCKED — the invalid cue.")]
        public float minGroundNormalY = 0.85f;

        private bool _placing;
        private Vector3 _ghostPos;
        private Quaternion _ghostRot = Quaternion.identity;
        private bool _valid;
        private Renderer[] _ghostRenderers;
        private MaterialPropertyBlock _mpb;
        private GUIStyle _promptStyle;

        // Dual-channel valid/invalid colours (hue channel; the WORD cue is the colour-independent channel).
        private static readonly Color GhostValid = new Color(0.35f, 0.85f, 0.40f, 0.45f);
        private static readonly Color GhostInvalid = new Color(0.90f, 0.30f, 0.25f, 0.45f);
        private static readonly Color Cream = new Color(0.92f, 0.85f, 0.72f);

        /// <summary>True while the placement ghost is active (aiming). Test/UI seam.</summary>
        public bool IsPlacing => _placing;
        /// <summary>The current ghost pose (world) while placing. Test seam.</summary>
        public Vector3 GhostPosition => _ghostPos;
        /// <summary>Whether the current ghost pose is a valid build spot. Test/prompt seam.</summary>
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
            useGUILayout = false; // explicit-Rect OnGUI only
            SetGhostShown(false);
        }

        void Update()
        {
            if (table != null && table.IsBuilt) return; // one table in ① — nothing left to place

            if (!_placing)
            {
                if (Input.GetKeyDown(buildKey) && CanAffordTable())
                    EnterPlacement();
                return;
            }

            // Placing — track the ghost + evaluate validity every frame.
            UpdateGhostPose();

            if (Input.GetKeyDown(cancelKey)) { Cancel(); return; }
            if (Input.GetKeyDown(buildKey)) TryConfirm();
        }

        /// <summary>True iff the pack can afford the table (both mats). Read-only.</summary>
        public bool CanAffordTable()
            => inventory != null &&
               ForgePlacement.CanAfford(inventory.WoodCount, inventory.StoneCount, woodCost, stoneCost);

        /// <summary>Enter placement mode (show the ghost). No-op if already placing / no ghost / can't afford.</summary>
        public void EnterPlacement()
        {
            if (_placing || !CanAffordTable()) return;
            _placing = true;
            SetGhostShown(true);
            UpdateGhostPose();
        }

        /// <summary>Cancel placement — hide the ghost, spend NOTHING.</summary>
        public void Cancel()
        {
            _placing = false;
            SetGhostShown(false);
        }

        /// <summary>
        /// Confirm the placement: if the pose is valid AND the mats can be paid, debit them all-or-nothing,
        /// reveal the table at the ghost pose, exit placement, and open the menu. Returns true iff the table
        /// was actually built. An invalid pose (or a debit that fails the re-check) stays in placement + builds
        /// nothing (the player keeps aiming / gathers more). Test seam (Update calls it on the build key).
        /// </summary>
        public bool TryConfirm()
        {
            if (!_placing || table == null || inventory == null) return false;
            if (!_valid) return false; // invalid pose — keep aiming, no debit

            // All-or-nothing across BOTH mats (pre-check → debit) — the ForgePlacement idiom.
            if (!CanAffordTable()) return false;
            bool woodOk = inventory.SpendWood(woodCost);
            bool stoneOk = inventory.SpendStone(stoneCost);
            if (!woodOk || !stoneOk) return false; // guarded by CanAffordTable; defensive

            table.Reveal(_ghostPos, _ghostRot);
            _placing = false;
            SetGhostShown(false);
            if (menu != null) menu.Open(); // explicit handoff — never races the confirm frame
            return true;
        }

        // Track the ghost to a point in front of the player, snapped down onto the ground; evaluate validity.
        private void UpdateGhostPose()
        {
            if (player == null) return;
            Vector3 fwd = player.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; else fwd.Normalize();
            Vector3 target = player.position + fwd * placeDistance;

            bool groundFound;
            float normalY;
            if (groundMask.value != 0 &&
                Physics.Raycast(target + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundMask))
            {
                target.y = hit.point.y;
                normalY = hit.normal.y;
                groundFound = true;
            }
            else
            {
                target.y = player.position.y; // flat-ground fallback (no ground layer wired / no hit)
                normalY = groundMask.value != 0 ? 0f : 1f; // no hit under a real mask => off-ground => invalid
                groundFound = groundMask.value == 0;
            }

            _ghostPos = target;
            _ghostRot = Quaternion.LookRotation(fwd, Vector3.up);
            float dist = Vector2.Distance(new Vector2(player.position.x, player.position.z),
                                          new Vector2(target.x, target.z));
            _valid = IsValidPlacement(groundFound, normalY, dist, minDistFromPlayer, minGroundNormalY);

            if (ghost != null)
            {
                ghost.SetPositionAndRotation(_ghostPos, _ghostRot);
                TintGhost(_valid ? GhostValid : GhostInvalid);
            }
        }

        /// <summary>
        /// PURE placement-validity truth-table (unit-testable without a scene): valid iff ground was found,
        /// the ground is flat enough (<paramref name="normalY"/> ≥ <paramref name="minNormalY"/>), and the
        /// ghost is at least <paramref name="minDist"/> from the player (never on top of self). Steep ground /
        /// no ground (over water, off the island edge) → invalid — the BLOCKED cue.
        /// </summary>
        public static bool IsValidPlacement(bool groundFound, float normalY, float distFromPlayer,
                                            float minDist, float minNormalY)
            => groundFound && normalY >= minNormalY && distFromPlayer >= minDist;

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
            string status = _valid ? "[OK] READY to build" : "[X] BLOCKED — move to flat open ground";
            string line = "PLACING crafting table   " + status + "     [" + buildKey + "] build   [" +
                          cancelKey + "] cancel";

            const float w = 620f, h = 34f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 130f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 5f, w, h - 8f), line, _promptStyle);
        }
    }
}
