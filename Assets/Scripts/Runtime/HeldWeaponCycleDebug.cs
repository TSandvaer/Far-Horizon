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

        // LIVE AXE HEAD-SIZE DIAL (86cabh907 soak round 2 — "axe head still too big, head proportion isn't a
        // uniform scale"). The whole-weapon scale dial ([ ]) scales the ENTIRE axe uniformly; it cannot shrink
        // the HEAD relative to the handle. This separate dial scales ONLY the axe BLADE-CLUSTER vertices (the
        // head) toward the eye, leaving the haft length + grip-point origin + forward axis untouched — so the
        // Sponsor dials the head proportion by eye in-game. It operates on a per-instance CLONE of the held
        // mesh (never the shared asset), so it is reversible and safe. ONLY the axe has a head; on knife/sword/
        // spear the dial is inert. The Sponsor reads the factor off the HUD/log to bake into the .blend head
        // re-author later (this round does NOT bake — OOS). Keys chosen free in normal play + outside the F9
        // AxeNudgeTool's key set (F9/Tab/N/arrows/PgUp-Dn/TGYHUJ/Shift/Ctrl) and the [ ]/= - scale dial:
        //   ' (apostrophe)  -> head SMALLER (-5%)
        //   ; (semicolon)   -> head BIGGER  (+5%)
        public KeyCode headSmallerKey = KeyCode.Quote;      // '
        public KeyCode headBiggerKey = KeyCode.Semicolon;   // ;
        [Tooltip("Per-keypress multiplicative head-size step for the axe head dial (1.05 = +5%/-5%).")]
        public float headStep = 1.05f;
        [Tooltip("Local-mesh-space |X| threshold (after FBX import) above which a vertex belongs to the axe " +
                 "BLADE CLUSTER (the head), not the haft core. 0.06 cleanly selects the 44 blade verts vs the " +
                 "72 haft-core verts (Blender-probed in art-src/weapon-pack/weapon_set_src.blend). Resolved " +
                 "against the imported mesh's blade-bulge axis at runtime; this is the fraction of the mesh's " +
                 "half-width on that axis.")]
        public float headBladeFraction = 0.30f;

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
        // Per-weapon mesh-holder LOCAL-euler offset (86cabh907 soak round 2 — the F9 nudge tool was AXE-ONLY;
        // the Sponsor could not angle the knife/sword/spear in-hand). The non-axe weapons seat on the SAME
        // shared seat the axe uses; this is a per-weapon rotation tweak composed ON TOP of the seat so each
        // sits at a believable in-hand angle. Axe = zero/identity (the axe's hold is the shared-seat baseline,
        // dialed via the F9 held target's rig fields — not here). First-guess identity for all; the F9 tool's
        // generalized HELD target dials each weapon's offset+euler+scale by eye and the Sponsor reads the
        // values to bake here.
        public static readonly Vector3[] WeaponMeshLocalEuler =
        {
            Vector3.zero,   // axe — shared-seat baseline (rig fields), no mesh-holder euler
            Vector3.zero,   // knife
            Vector3.zero,   // sword
            Vector3.zero,   // spear
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
        // changed by the SCALE dial (the axe's uniform size is locked); its HEAD is dialed separately below.
        // The HUD/log surface the live value to bake.
        private float[] _liveScale;
        // LIVE per-weapon mesh-holder offset + euler — seeded from the baked WeaponMeshLocalOffset/Euler, then
        // mutated by the F9 AxeNudgeTool's generalized HELD target (86cabh907 soak round 2) so the Sponsor
        // positions+angles each weapon in-hand. Index 0 (axe) stays at zero (its hold is the shared-seat rig
        // baseline). The F9 tool reads/writes these via the public accessors below.
        private Vector3[] _liveOffset;
        private Vector3[] _liveEuler;

        // LIVE AXE HEAD-SIZE dial (86cabh907 soak round 2). The held axe FBX is a SINGLE mesh; to shrink only
        // the HEAD relative to the haft we clone the axe mesh per-instance and scale the blade-cluster verts
        // toward the eye by this live factor. _axeHeadFactor=1 == the shipped (already 0.8x-baked) head; the
        // Sponsor dials it DOWN further by eye and reports the factor to bake into the .blend.
        private float _axeHeadFactor = 1f;
        private Mesh _axeHeadDialMesh;          // per-instance clone we deform (never the shared asset)
        private Vector3[] _axeBaseVerts;        // the axe mesh's ORIGINAL local verts (factor=1 baseline)
        private int[] _axeHeadVertIdx;          // indices of the blade-cluster (head) verts in _axeBaseVerts
        private Vector3 _axeHeadPivot;          // local-space eye the head verts scale toward
        private bool _axeHeadResolved;

        /// <summary>The currently-held weapon index (0=axe,1=knife,2=sword,3=spear) — read by the F9 tool so
        /// its generalized HELD target dials whichever weapon is shown.</summary>
        public int CurrentIndex => _index;
        /// <summary>Label of the currently-held weapon (AXE/KNIFE/SWORD/SPEAR) — for the F9 tool's panel.</summary>
        public string CurrentLabel => WeaponLabels[Mathf.Clamp(_index, 0, WeaponLabels.Length - 1)];
        /// <summary>Live per-weapon mesh-holder offset for the F9 tool to read (the bake value).</summary>
        public Vector3 CurrentOffset => _liveOffset != null ? _liveOffset[_index] : WeaponMeshLocalOffset[_index];
        /// <summary>Live per-weapon mesh-holder euler for the F9 tool to read (the bake value).</summary>
        public Vector3 CurrentEuler => _liveEuler != null ? _liveEuler[_index] : WeaponMeshLocalEuler[_index];
        /// <summary>Live per-weapon held scale for the F9 tool to read (the bake value).</summary>
        public float CurrentScale => _liveScale != null ? _liveScale[_index] : WeaponMeshScale[_index];
        /// <summary>The live axe head-size factor (1 == shipped head) — for the F9 tool's HEAD-SIZE target.</summary>
        public float AxeHeadFactor => _axeHeadFactor;

        /// <summary>
        /// F9-tool entry point (86cabh907 soak round 2): nudge the CURRENTLY-HELD weapon's in-hand placement —
        /// mesh-holder offset (dp) + euler (dr) + a multiplicative scale factor (scaleFactor; 1 = no change).
        /// The AXE (index 0) routes scale/offset/euler nudges to NOTHING here (its hold is the shared-seat rig
        /// baseline the F9 tool nudges directly on the HeldAxeRig); for the axe this method only re-applies. For
        /// knife/sword/spear it edits the live per-weapon arrays + re-seats the mesh-holder immediately so the
        /// dial shows this frame. Returns true if a non-axe weapon was actually edited.
        /// </summary>
        public bool NudgeCurrentWeapon(Vector3 dp, Vector3 dr, float scaleFactor)
        {
            if (_index == 0) return false; // axe hold = shared-seat rig (F9 tool nudges HeldAxeRig directly)
            if (!_resolved) ResolveMeshes();
            _liveOffset[_index] += dp;
            _liveEuler[_index] += dr;
            if (!Mathf.Approximately(scaleFactor, 1f))
                _liveScale[_index] = Mathf.Max(0.01f, _liveScale[_index] * scaleFactor);
            ApplyCurrent();
            return true;
        }

        /// <summary>
        /// F9-tool entry point (86cabh907 soak round 2): dial the AXE HEAD size by a multiplicative factor
        /// (1.05 = +5%). Inert unless the axe is the currently-held weapon. Returns true if the axe head was
        /// dialed. Mirrors the always-on ;/' dial so the Sponsor can drive the head from either panel.
        /// </summary>
        public bool DialAxeHead(float factor)
        {
            if (_index != 0) return false;          // only the axe has a head
            if (!_axeHeadResolved) ResolveAxeHead();
            if (_axeHeadVertIdx == null || _axeHeadVertIdx.Length == 0) return false;
            _axeHeadFactor = Mathf.Clamp(_axeHeadFactor * factor, 0.2f, 2f);
            ApplyAxeHead();
            return true;
        }

        private void Awake()
        {
            // Seed the LIVE scale/offset/euler from the baked defaults (copy — never mutate the static arrays).
            // The dials edit these per-weapon; index 0 (axe) stays at the locked defaults.
            _liveScale = (float[])WeaponMeshScale.Clone();
            _liveOffset = (Vector3[])WeaponMeshLocalOffset.Clone();
            _liveEuler = (Vector3[])WeaponMeshLocalEuler.Clone();

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
                return;
            }

            // LIVE AXE HEAD-SIZE DIAL (; / ') — shrink/grow ONLY the axe BLADE-CLUSTER toward the eye, leaving
            // the haft length + origin untouched, so the Sponsor dials the head proportion by eye + reads the
            // factor to bake into the .blend head re-author later (86cabh907 soak round 2). ONLY the axe has a
            // head; on knife/sword/spear the dial logs that it is inert.
            bool headBigger = Input.GetKeyDown(headBiggerKey);
            bool headSmaller = Input.GetKeyDown(headSmallerKey);
            if (headBigger || headSmaller)
            {
                if (_index != 0)
                {
                    Debug.Log("[HeldWeaponCycleDebug] head-size dial only applies to the AXE — cycle [" +
                              cycleKey + "] to the axe to dial its head.");
                    return;
                }
                if (DialAxeHead(headBigger ? headStep : 1f / headStep))
                    Debug.Log("[HeldWeaponCycleDebug] AXE head factor -> " + _axeHeadFactor.ToString("F3") +
                              "  (bake into the .blend head re-author; 1.000 == current shipped head)");
            }
        }

        // Swap the displayed mesh + apply the per-weapon mesh-holder compensation. The axe (index 0)
        // RESTORES the captured original local TRS so its Sponsor-locked seat is byte-unchanged.
        private void ApplyCurrent()
        {
            Mesh m = (_meshes != null && _index < _meshes.Length) ? _meshes[_index] : null;
            if (m == null) m = _axeOriginalMesh; // fall back to the axe if a family mesh failed to resolve

            var t = _meshHolder.transform;
            if (_index == 0)
            {
                // AXE — Sponsor-LOCKED seat: restore the captured original local TRS (byte-unchanged). The
                // displayed mesh is the head-dial CLONE if the head has been dialed (so the shrink shows),
                // else the original axe mesh. The head dial NEVER touches the seat transform — only the verts.
                _meshHolder.sharedMesh = (_axeHeadDialMesh != null) ? _axeHeadDialMesh : _axeOriginalMesh;
                t.localPosition = _holderOrigPos;
                t.localRotation = _holderOrigRot;
                t.localScale = _holderOrigScale;
            }
            else
            {
                // knife / sword / spear — look-soak seat on the SHARED seat. Compose the per-weapon LIVE
                // offset/euler/scale ON TOP of the axe's captured baseline so the weapon rides the same seat
                // the axe does, just nudged to seat + angle reasonably in the hand. All three come from the
                // LIVE arrays (seeded from the WeaponMeshLocal* defaults) so the F9 dial shows immediately.
                _meshHolder.sharedMesh = m;
                t.localPosition = _holderOrigPos + _liveOffset[_index];
                t.localRotation = _holderOrigRot * Quaternion.Euler(_liveEuler[_index]);
                t.localScale = _holderOrigScale * _liveScale[_index];
            }
        }

        // Resolve the axe head dial: capture the axe mesh's original verts, classify the BLADE-CLUSTER (head)
        // verts vs the haft core, and compute the eye pivot the head scales toward. Lazy (first head dial).
        // The blade-bulge axis = the local mesh axis with the LARGEST asymmetric extent off the haft centre
        // (the head fans out along it). Verts whose |coord on that axis| exceeds headBladeFraction × the
        // half-extent are the head; the rest are the haft core. The pivot is the eye: the head verts' centroid
        // projected back onto the haft axis (so scaling toward it shrinks the blade while keeping it on the haft).
        private void ResolveAxeHead()
        {
            _axeHeadResolved = true;
            if (_axeOriginalMesh == null) return;
            _axeBaseVerts = _axeOriginalMesh.vertices;
            int n = _axeBaseVerts.Length;
            if (n == 0) return;

            // Mesh-local bounds centre + per-axis extent.
            Vector3 bMin = _axeBaseVerts[0], bMax = _axeBaseVerts[0];
            for (int i = 1; i < n; i++)
            {
                bMin = Vector3.Min(bMin, _axeBaseVerts[i]);
                bMax = Vector3.Max(bMax, _axeBaseVerts[i]);
            }
            Vector3 ext = (bMax - bMin) * 0.5f;
            Vector3 ctr = (bMax + bMin) * 0.5f;
            // The HAFT is the long thin axis (largest extent); the BLADE bulges along a SHORT axis. Pick the
            // blade axis = the axis (other than the longest) along which verts spread most asymmetrically. In
            // the imported axe the haft runs along local-Y (height); the blade fans along local-X. Choose the
            // axis with the SMALLEST extent-vs-others that still carries a clear off-centre cluster: simplest
            // robust pick = the axis perpendicular to the longest, with the larger remaining extent.
            int longAxis = (ext.x >= ext.y && ext.x >= ext.z) ? 0 : (ext.y >= ext.z ? 1 : 2);
            int a1 = (longAxis + 1) % 3, a2 = (longAxis + 2) % 3;
            int bladeAxis = (Comp(ext, a1) >= Comp(ext, a2)) ? a1 : a2;
            float halfExt = Comp(ext, bladeAxis);
            float thresh = headBladeFraction * Mathf.Max(1e-4f, halfExt);

            // Classify head (off-haft) vs haft-core verts by their offset on the blade axis from the blade-axis
            // CENTRE LINE. The haft runs along the long axis at the blade-axis centre; the head bulges off it.
            // The reference centre for "off the haft" is the HAFT-CORE centroid on the blade axis (the verts
            // near the centre line), NOT the full-mesh centre (which the bulky head pulls off-axis).
            float refCentre = Comp(ctr, bladeAxis);
            var idx = new System.Collections.Generic.List<int>(n);
            Vector3 headSum = Vector3.zero;
            float haftSum = 0f; int haftN = 0;
            for (int i = 0; i < n; i++)
            {
                float off = Mathf.Abs(Comp(_axeBaseVerts[i], bladeAxis) - refCentre);
                if (off > thresh) { idx.Add(i); headSum += _axeBaseVerts[i]; }
                else { haftSum += Comp(_axeBaseVerts[i], bladeAxis); haftN++; }
            }
            _axeHeadVertIdx = idx.ToArray();
            if (_axeHeadVertIdx.Length == 0) return;
            float haftAxisCoord = haftN > 0 ? haftSum / haftN : refCentre; // where the haft sits on the blade axis

            // Pivot = the head centroid pulled onto the HAFT centre line (blade-axis coord = the haft's coord),
            // so scaling the head verts toward it shrinks the BLADE while keeping it attached at the haft (the
            // long-axis/depth coords stay at the head centroid so it shrinks in plane, not down the handle).
            Vector3 headCtr = headSum / _axeHeadVertIdx.Length;
            _axeHeadPivot = headCtr;
            SetComp(ref _axeHeadPivot, bladeAxis, haftAxisCoord); // on the haft centre line, not the mesh centre
            Debug.Log($"[HeldWeaponCycleDebug] axe head dial resolved: {_axeHeadVertIdx.Length}/{n} blade verts " +
                      $"(bladeAxis={bladeAxis}, thresh={thresh:F4}, haftCoord={haftAxisCoord:F3}), pivot={_axeHeadPivot.ToString("F3")}");
        }

        // Apply the live head factor to the per-instance clone (never the shared asset) + re-display it.
        private void ApplyAxeHead()
        {
            if (_axeBaseVerts == null || _axeHeadVertIdx == null) return;
            if (_axeHeadDialMesh == null)
            {
                _axeHeadDialMesh = Object.Instantiate(_axeOriginalMesh);
                _axeHeadDialMesh.name = _axeOriginalMesh.name + "_headDial";
            }
            var verts = (Vector3[])_axeBaseVerts.Clone();
            foreach (int i in _axeHeadVertIdx)
                verts[i] = _axeHeadPivot + (_axeBaseVerts[i] - _axeHeadPivot) * _axeHeadFactor;
            _axeHeadDialMesh.vertices = verts;
            _axeHeadDialMesh.RecalculateBounds();
            // Do NOT RecalculateNormals — the faceted flat-shaded normals are load-bearing (lowpoly-quality.md
            // §1). Scaling toward a pivot preserves face planarity, so the baked per-face normals stay valid.
            if (_index == 0 && _meshHolder != null) _meshHolder.sharedMesh = _axeHeadDialMesh;
        }

        private static float Comp(Vector3 v, int a) => a == 0 ? v.x : a == 1 ? v.y : v.z;
        private static void SetComp(ref Vector3 v, int a, float val) { if (a == 0) v.x = val; else if (a == 1) v.y = val; else v.z = val; }

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
            // Taller now: carries the live scale/head read-out + both dial-key hints (soak round 2).
            float w = 470f, h = 82f;
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);
            float y = Screen.height - h - 10f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Line 1: which weapon + its LIVE read-out. The AXE shows its HEAD factor (the round-2 dial); the
            // others show their held SCALE (the round-1 dial). Both are the bake numbers.
            string readOut = _index == 0
                ? "head " + _axeHeadFactor.ToString("F3") + "  (scale 1.000 LOCKED)"
                : "scale " + (_liveScale != null ? _liveScale[_index].ToString("F3") : WeaponMeshScale[_index].ToString("F3"));
            GUI.Label(new Rect(x + 10f, y + 5f, w - 20f, 20f),
                "DEBUG — held weapon: " + WeaponLabels[_index] + "   " + readOut, _labelStyle);
            // Line 2: the cycle key. Line 3: scale dial (non-axe). Line 4: HEAD-size dial (axe).
            GUI.Label(new Rect(x + 10f, y + 24f, w - 20f, 18f),
                "[" + cycleKey + "] cycle  axe -> knife -> sword -> spear   (soak view, not equip)", _keyStyle);
            GUI.Label(new Rect(x + 10f, y + 42f, w - 20f, 18f),
                "[ ] / [=] bigger   [[] / [-] smaller   whole weapon (±5%; knife/sword/spear)", _keyStyle);
            GUI.Label(new Rect(x + 10f, y + 60f, w - 20f, 18f),
                "[;] / ['] axe HEAD bigger/smaller (±5%)   F9: position+angle each weapon", _keyStyle);
        }
    }
}
