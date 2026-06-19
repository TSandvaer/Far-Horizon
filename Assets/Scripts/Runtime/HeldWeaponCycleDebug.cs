using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// DEBUG / SOAK-VIEWING handle (ticket 86cabh907) — lets the Sponsor SEE each member of the in-house
    /// weapon family HELD by the castaway, in-engine, by pressing a key to CYCLE the held mesh through
    ///   axe -> knife -> sword -> spear -> (wrap)
    /// at runtime. The castaway visibly wields each weapon as it is cycled.
    ///
    /// THIS IS NOT THE REAL EQUIP GAMEPLAY. The belt -> wield flow (selecting a weapon from the hotbar to
    /// actually equip it) is a LATER ticket. This handle ONLY swaps the displayed mesh on the existing held
    /// seat so the Sponsor can confirm the look of each weapon in the hand — it does not touch the
    /// inventory, the belt, the chop gate, or any gameplay state.
    ///
    /// HOW IT WORKS — swap the MESH on the SHARED HeldTool seat:
    ///   - The component is serialized onto the HeroAxe object (the held seat) editor-time (MovementCamera
    ///     Scene.AttachHeroAxeToHand). HeroAxe is pose-driven by <see cref="HeldAxeRig"/> and visibility-
    ///     gated by <see cref="HeldAxe"/>; this handle NEVER touches the seat transform, the rig, or the
    ///     visibility gate — it only swaps the MeshFilter.sharedMesh (+ a per-weapon mesh-holder offset).
    ///   - The AXE (index 0) is the Sponsor-LOCKED default: it applies ZERO compensation and restores the
    ///     mesh-holder child's ORIGINAL local TRS captured in Awake, so the axe seat is byte-unchanged. The
    ///     handle starts on the axe — a soak that never presses the key sees exactly the shipped axe.
    ///   - knife / sword / spear seat on the SAME shared seat. Their FBX grip-origin is (0,0,0) with the
    ///     blade up +Z (bl_10/bl_11/bl_12), whereas the axe FBX uses a grip-MIDPOINT origin (z=0.45) and is
    ///     height-normalized; they are also un-normalized (taller). So each needs a SMALL per-weapon mesh-
    ///     holder offset/scale to seat REASONABLY in the hand. These are ROUGH look-soak values, NOT the
    ///     precise grip — the precise per-weapon grip is the later equip ticket.
    ///
    /// MESH SOURCE AT RUNTIME: AssetDatabase is editor-only, so the weapon meshes are pulled from
    /// Resources/WeaponSetLineup.prefab (built by WeaponPackAssetGen.BuildLineupPrefab — the same prefab
    /// the WeaponSetVerifyCapture uses). The prefab carries all four weapon meshes (axe/knife/sword/spear
    /// child objects named after their FBX), so the build path has a runtime-loadable source with no extra
    /// asset plumbing.
    ///
    /// INPUT + HUD: pure legacy-Input + IMGUI (the project idiom — AxeNudgeTool / BootHud / SurvivalHud),
    /// build-safe (no new-Input-System / shader dependency). The HUD label is ALWAYS shown (this is a soak-
    /// viewing aid, not a gated tuning panel) so the Sponsor always knows which weapon is held + the key.
    ///
    /// STATIC STATE: instance fields only — NO mutable runtime statics, so no SubsystemRegistration reset
    /// is needed (unity-conventions.md §Configurable Enter Play Mode; StaticStateResetTests stays green).
    /// </summary>
    public class HeldWeaponCycleDebug : MonoBehaviour
    {
        [Tooltip("Cycle the held weapon (axe -> knife -> sword -> spear -> wrap). B is free in normal play " +
                 "(WASD+Space+1..9 are the gameplay keys; B is only otherwise consumed inside the F9 nudge " +
                 "panel, which is mutually exclusive with normal play).")]
        public KeyCode cycleKey = KeyCode.B;

        // LIVE SCALE DIAL (this soak's reframe — the held knife/sword/spear read MUCH SMALLER than the axe).
        // Scale the CURRENT held weapon's mesh-holder up/down LIVE so the Sponsor dials each weapon's in-hand
        // size BY EYE and reads the number off the HUD/log to bake into WeaponMeshScale. ~5% steps. Two key
        // pairs each direction so a non-US-layout keyboard still has a working pair. The AXE (index 0) is
        // Sponsor-LOCKED — the dial REFUSES to scale it (it always restores the captured original TRS).
        //   ] or =  -> scale the held weapon UP   (+5%)
        //   [ or -  -> scale the held weapon DOWN (-5%)
        // Chosen to NOT collide with the always-on [B] cycle NOR the F9 AxeNudgeTool's keys (F9/Tab/B/arrows/
        // PgUp-Dn/TGYHUJ/Shift/Ctrl) — bracket+equals+minus are free in both normal play and that panel.
        public KeyCode scaleUpKey = KeyCode.RightBracket;   // ]
        public KeyCode scaleUpKeyAlt = KeyCode.Equals;      // =
        public KeyCode scaleDownKey = KeyCode.LeftBracket;  // [
        public KeyCode scaleDownKeyAlt = KeyCode.Minus;     // -
        [Tooltip("Per-keypress multiplicative scale step for the live dial (1.05 = +5% up / -5% down).")]
        public float scaleStep = 1.05f;

        // The four family meshes' names inside Resources/WeaponSetLineup.prefab (the child object names =
        // the FBX file-name-without-extension; see WeaponPackAssetGen.BuildLineupPrefab). Order = cycle order.
        // PUBLIC + static so the EditMode guard reads the cycle contract directly (no reflection): the lineup
        // prefab MUST carry a mesh node for each of these, or cycling resolves nothing.
        public static readonly string[] WeaponNodeNames = { "wpn_axe_01", "wpn_knife_01", "wpn_sword_01", "wpn_spear_01" };
        public static readonly string[] WeaponLabels = { "AXE", "KNIFE", "SWORD", "SPEAR" };
        public const string LineupResourcePath = "WeaponSetLineup"; // Assets/Resources/WeaponSetLineup.prefab

        // Per-weapon mesh-holder compensation (look-soak — read proportionate to the AXE in the hand; the
        // exact precise grip is OOS, the later equip ticket). Index 0 (axe) is ALWAYS zero/identity — the axe
        // seat is Sponsor-LOCKED and is restored to its captured original.
        //
        // SOAK REFRAME (this ticket): the FIRST values {1, 0.55, 0.50, 0.42} read MUCH SMALLER than the axe in
        // the hand — the down-scale was far too aggressive. The MODELS are correctly sized (the Blender family
        // render was Sponsor-accepted); only the HELD scale was wrong. So these are BUMPED UP to read
        // proportionate to the axe's in-hand presence: a knife a solid knife, a sword sword-sized, a spear
        // long — all much closer to 1.0. The axe holds at 1.0 and looks right; match that apparent presence.
        // These are the best FIRST-GUESS; the LIVE SCALE DIAL ([ ] / - =) lets the Sponsor dial each by eye +
        // read the number to bake here, so we don't blind-iterate.
        // PUBLIC so the EditMode guard pins the axe-locked-default contract (index 0 == identity).
        public static readonly float[] WeaponMeshScale = { 1f, 0.85f, 0.95f, 0.90f };
        // Local-space drop applied to the mesh-holder child for the non-axe weapons (their origin is the grip
        // BASE, so they need pulling back along the blade axis to sit the grip in the palm). Axe = zero.
        // Re-seated DEEPER for the larger scale above: a bigger mesh's grip-base sits further from the palm, so
        // the pull-back along the blade axis scales up too (≈ old-offset × new-scale/old-scale) — keeps the
        // grip in the hand + the tip clear of the ground at the bumped size. The live dial only changes scale;
        // if a weapon clips after dialing, re-tune these offsets in the same bake.
        public static readonly Vector3[] WeaponMeshLocalOffset =
        {
            Vector3.zero,                       // axe — LOCKED, no compensation
            new Vector3(0f, 0f, -0.34f),        // knife
            new Vector3(0f, 0f, -0.80f),        // sword
            new Vector3(0f, 0f, -1.50f),        // spear (longest — pull the grip furthest back)
        };

        private MeshFilter _meshHolder;     // the child MeshFilter on HeroAxe (the FBX mesh node)
        private Mesh[] _meshes;             // resolved family meshes, indexed to match WeaponNodeNames
        private Mesh _axeOriginalMesh;      // the shipped axe mesh — restored when cycling back to the axe
        private Vector3 _holderOrigPos;     // captured original mesh-holder local TRS (axe = LOCKED default)
        private Quaternion _holderOrigRot;
        private Vector3 _holderOrigScale;
        private int _index;                // 0 = axe (default), 1 = knife, 2 = sword, 3 = spear
        private bool _resolved;
        private GUIStyle _labelStyle, _keyStyle;

        // LIVE per-weapon scale — seeded from the baked WeaponMeshScale defaults, then mutated by the live dial
        // ([ ] / - =) so the Sponsor can dial the CURRENT weapon's in-hand size by eye. Index 0 (axe) is never
        // changed (Sponsor-LOCKED; the dial refuses to touch it). The HUD/log surface the live value to bake.
        private float[] _liveScale;

        private void Awake()
        {
            // Seed the LIVE scale from the baked defaults (copy — never mutate the static array). The dial
            // edits this per-weapon; index 0 (axe) stays at the locked default and is never dialed.
            _liveScale = (float[])WeaponMeshScale.Clone();

            // The MeshFilter lives on a CHILD of HeroAxe (the imported FBX mesh node). Capture its ORIGINAL
            // local TRS so cycling BACK to the axe restores the Sponsor-locked seat byte-for-byte.
            _meshHolder = GetComponentInChildren<MeshFilter>(true);
            if (_meshHolder != null)
            {
                _axeOriginalMesh = _meshHolder.sharedMesh;
                var t = _meshHolder.transform;
                _holderOrigPos = t.localPosition;
                _holderOrigRot = t.localRotation;
                _holderOrigScale = t.localScale;
            }
            else
            {
                Debug.LogWarning("[HeldWeaponCycleDebug] no MeshFilter under HeroAxe — cannot swap held weapon meshes");
            }
        }

        // Resolve the family meshes from the lineup prefab lazily (the first cycle), so a soak that never
        // presses the key pays nothing and the axe never depends on the lineup prefab being present.
        private void ResolveMeshes()
        {
            _resolved = true;
            _meshes = new Mesh[WeaponNodeNames.Length];
            _meshes[0] = _axeOriginalMesh; // the shipped held-axe mesh is the source of truth for index 0

            var prefab = Resources.Load<GameObject>(LineupResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[HeldWeaponCycleDebug] Resources/" + LineupResourcePath +
                                 " missing — knife/sword/spear cannot be resolved; only the axe will show");
                return;
            }
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                // Match the family node by name (the lineup child names = FBX names). Skip the axe (index 0
                // is the SHIPPED held mesh, not the lineup's un-normalized axe — they read identically, but
                // the shipped one is the Sponsor-locked source).
                for (int i = 1; i < WeaponNodeNames.Length; i++)
                    if (mf.name == WeaponNodeNames[i]) _meshes[i] = mf.sharedMesh;
            }
            for (int i = 1; i < _meshes.Length; i++)
                if (_meshes[i] == null)
                    Debug.LogWarning("[HeldWeaponCycleDebug] lineup prefab missing mesh node '" + WeaponNodeNames[i] + "'");
        }

        private void Update()
        {
            if (_meshHolder == null) return;

            // [B] — cycle the held weapon.
            if (Input.GetKeyDown(cycleKey))
            {
                if (!_resolved) ResolveMeshes();
                _index = (_index + 1) % WeaponNodeNames.Length;
                ApplyCurrent();
                Debug.Log("[HeldWeaponCycleDebug] held weapon -> " + WeaponLabels[_index] +
                          " (" + WeaponNodeNames[_index] + ")  [DEBUG cycle, key=" + cycleKey + "]");
                return;
            }

            // LIVE SCALE DIAL ([ ] / - =) — scale the CURRENT held weapon's mesh-holder up/down in ~5% steps so
            // the Sponsor dials its in-hand size by eye + reads the value to bake. REFUSES the axe (Sponsor-
            // LOCKED): on index 0 the dial only logs that the axe is locked, never changes the seat.
            bool up = Input.GetKeyDown(scaleUpKey) || Input.GetKeyDown(scaleUpKeyAlt);
            bool down = Input.GetKeyDown(scaleDownKey) || Input.GetKeyDown(scaleDownKeyAlt);
            if (up || down)
            {
                if (_index == 0)
                {
                    Debug.Log("[HeldWeaponCycleDebug] AXE seat is Sponsor-LOCKED — scale dial refused. " +
                              "Cycle [" + cycleKey + "] to a knife/sword/spear to dial its held size.");
                    return;
                }
                if (!_resolved) ResolveMeshes(); // dialing before any cycle still resolves + applies cleanly
                float factor = up ? scaleStep : 1f / scaleStep;
                _liveScale[_index] = Mathf.Max(0.01f, _liveScale[_index] * factor);
                ApplyCurrent();
                Debug.Log("[HeldWeaponCycleDebug] " + WeaponLabels[_index] + " held scale -> " +
                          _liveScale[_index].ToString("F3") + "  (bake into WeaponMeshScale[" + _index + "])");
            }
        }

        // Swap the displayed mesh + apply the per-weapon mesh-holder compensation. The axe (index 0)
        // RESTORES the captured original local TRS so its Sponsor-locked seat is byte-unchanged.
        private void ApplyCurrent()
        {
            Mesh m = (_meshes != null && _index < _meshes.Length) ? _meshes[_index] : null;
            if (m == null) m = _axeOriginalMesh; // fall back to the axe if a family mesh failed to resolve

            _meshHolder.sharedMesh = m;
            var t = _meshHolder.transform;
            if (_index == 0)
            {
                // AXE — Sponsor-LOCKED default: restore the exact captured original local TRS (byte-unchanged).
                t.localPosition = _holderOrigPos;
                t.localRotation = _holderOrigRot;
                t.localScale = _holderOrigScale;
            }
            else
            {
                // knife / sword / spear — look-soak seat on the SHARED seat (precise grip is OOS). Compose the
                // per-weapon offset/scale ON TOP of the axe's captured baseline so the weapon rides the same
                // seat the axe does, just nudged to seat reasonably in the hand. Scale comes from the LIVE
                // dial (_liveScale, seeded from WeaponMeshScale) so the Sponsor's dial shows immediately.
                t.localPosition = _holderOrigPos + WeaponMeshLocalOffset[_index];
                t.localRotation = _holderOrigRot;
                t.localScale = _holderOrigScale * _liveScale[_index];
            }
        }

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = new Color(1f, 0.85f, 0.45f); // warm-gold, matches the nudge-tool header
                _keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _keyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            }

            // Bottom-CENTER, clear of SurvivalHud's bottom-left hotbar + the AxeNudgeTool's right panel.
            // Taller now: it carries the live scale read-out + the scale-dial key hint (this soak's reframe).
            float w = 430f, h = 64f;
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);
            float y = Screen.height - h - 10f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Line 1: which weapon + its LIVE held scale (the bake number). Axe shows LOCKED (no dial).
            string scaleText = _index == 0
                ? "scale 1.000 (LOCKED)"
                : "scale " + (_liveScale != null ? _liveScale[_index].ToString("F3") : WeaponMeshScale[_index].ToString("F3"));
            GUI.Label(new Rect(x + 10f, y + 5f, w - 20f, 20f),
                "DEBUG — held weapon: " + WeaponLabels[_index] + "   " + scaleText, _labelStyle);
            // Line 2: the cycle key. Line 3: the live scale-dial keys (so the Sponsor dials each size by eye).
            GUI.Label(new Rect(x + 10f, y + 24f, w - 20f, 18f),
                "[" + cycleKey + "] cycle  axe -> knife -> sword -> spear   (soak view, not equip)", _keyStyle);
            GUI.Label(new Rect(x + 10f, y + 42f, w - 20f, 18f),
                "[ ] or [=] bigger   [[] or [-] smaller  (±5% — dial held size; axe locked)", _keyStyle);
        }
    }
}
