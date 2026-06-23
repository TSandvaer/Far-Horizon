using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// SHAFT-LENGTH PICKER (ticket 86cabh907 — the unstick instrument that STOPS the guess-and-bake loop).
    /// After 6+ iterations the Sponsor rejected 2.0x and 1.5x hafts as "too long" + "still bend before the
    /// head". This handle lets him PICK the haft length IN-HAND, live, instead of us guessing: a key cycles
    /// the held axe's displayed mesh through FOUR pre-baked length variants —
    ///   1.1x -> 1.2x -> 1.3x -> 1.4x  (of the original short haft; the answer is between the rejected
    ///   stubby 1.06x and the too-long 1.5x)
    /// — each with the head LOCKED (0.65x) + RIGID-ROTATED FULLY COAXIAL (junction angle 0.0198deg, bl_19 —
    /// killing the 2.71deg residual dogleg bl_18 left), haft STRAIGHT. He picks the length that looks right,
    /// re-seats the pose with the F9 nudge if wanted, reports the factor, and a FINAL bake writes that exact
    /// length into wpn_axe_01.fbx.
    ///
    /// MECHANISM — reuse the PROVEN mesh-swap (NOT the broken vertex-resize dial). This swaps
    /// <see cref="HeldWeaponCycleDebug.MeshHolder"/>.sharedMesh through the variant meshes exactly the way
    /// the [B] weapon-cycle swaps axe->knife->sword->spear (a per-instance reference swap on the shared held
    /// seat). It NEVER deforms verts, NEVER touches the seat transform / rig / visibility gate, NEVER touches
    /// localScale. The #100 head-resize DIAL failed because it deformed mesh verts at runtime and silently
    /// no-opped (rig-stomp + data-layer-only); a mesh-REFERENCE swap is the mechanism that always worked
    /// (the weapon cycle), so the picker uses that.
    ///
    /// VARIANT MESH SOURCE: AssetDatabase is editor-only, so the four variant meshes are pulled at runtime
    /// from Resources/AxeLengthVariants.prefab (built by WeaponPackAssetGen.BuildLengthVariantsPrefab). The
    /// prefab carries one child per variant FBX (wpn_axe_01_len11..len14) so the BUILT exe has a runtime-
    /// loadable source with no editor plumbing.
    ///
    /// SCOPE GATE: only acts while the AXE is the held weapon (HeldWeaponCycleDebug.IsAxeHeld) — knife/sword/
    /// spear have no shaft-length variants. When the cycle restores the axe (its ApplyCurrent re-asserts the
    /// captured original mesh), the picker re-applies the chosen variant the next frame so the picked length
    /// survives a weapon-cycle round-trip.
    ///
    /// INPUT + HUD: legacy-Input + IMGUI (the project idiom). The cycle key is LAYOUT-AGNOSTIC (a LETTER key,
    /// [[sponsor-danish-keyboard-layout]] — NEVER US-position punctuation, which shifts on the Sponsor's
    /// Danish laptop). The HUD always shows the current length factor + haft:head ratio + the key.
    ///
    /// STATIC STATE: instance fields only — NO mutable runtime statics, so no SubsystemRegistration reset is
    /// needed (unity-conventions.md §Configurable Enter Play Mode; StaticStateResetTests stays green).
    /// </summary>
    public class HeldAxeLengthPicker : MonoBehaviour
    {
        // LAYOUT-AGNOSTIC cycle key (Danish-safe LETTER — [[sponsor-danish-keyboard-layout]]). L = "length".
        // L is free: not WASD/Space/Shift, not 1..9 belt, not [B] weapon-cycle, not [N] arm-switch, not [K]
        // target-cycle, not [O]/[I] head dial, not the F9 TGYHUJ rotation keys / arrows / PgUp-Dn.
        [Tooltip("Cycle the held axe shaft length (1.1x -> 1.2x -> 1.3x -> 1.4x -> wrap). L = layout-safe " +
                 "LETTER (Danish-keyboard-safe; never US punctuation).")]
        public KeyCode lengthCycleKey = KeyCode.L;

        // The four length-variant child node names inside Resources/AxeLengthVariants.prefab (the child names
        // = the FBX file-name-without-extension; see WeaponPackAssetGen.BuildLengthVariantsPrefab). Order =
        // cycle order, shortest->longest. PUBLIC + static so the EditMode guard reads the contract directly.
        public static readonly string[] VariantNodeNames =
            { "wpn_axe_01_len11", "wpn_axe_01_len12", "wpn_axe_01_len13", "wpn_axe_01_len14" };
        // The haft length FACTOR (of the original short haft) for each variant, in cycle order — surfaced on
        // the HUD/log so the Sponsor reports the picked factor to bake. MUST match VariantNodeNames order.
        public static readonly float[] VariantLengthFactor = { 1.1f, 1.2f, 1.3f, 1.4f };
        // The haft:head RATIO each variant produces (measured in bl_19 from the baked FBX, head locked) — a
        // human-readable companion to the factor on the HUD. MUST match VariantNodeNames order.
        public static readonly float[] VariantHaftHeadRatio = { 1.035f, 1.129f, 1.223f, 1.317f };
        public const string VariantsResourcePath = "AxeLengthVariants"; // Assets/Resources/AxeLengthVariants.prefab

        // The picker starts UNSELECTED (index -1) = the shipped wpn_axe_01.fbx (the current committed length).
        // A soak that never presses [L] sees exactly the shipped axe; pressing [L] enters the variant cycle at
        // index 0 (1.1x). This way the picker is purely additive over the shipped default.
        private int _index = -1;            // -1 = shipped axe (no variant); 0..3 = the four variants
        private HeldWeaponCycleDebug _cycle;
        private Mesh[] _variantMeshes;      // resolved variant meshes, indexed to VariantNodeNames
        private bool _resolved;
        private GUIStyle _labelStyle, _keyStyle;

        /// <summary>The currently-picked length factor, or 0 while on the shipped axe (no variant picked).</summary>
        public float CurrentLengthFactor => _index >= 0 ? VariantLengthFactor[_index] : 0f;

        /// <summary>
        /// VERIFICATION entry point (-verifyAxeLengths shipped-build capture, 86cabh907): force-select a
        /// specific length variant by index (0..3) and apply it to the held holder, so the shipped-build
        /// capture proves the mesh actually swaps per length (the #100 dial passed tests but no-opped at
        /// runtime — this drives the SAME [L] mesh-swap path the Sponsor uses). Resolves the variants if
        /// needed. Returns the applied length factor, or 0 if the index is out of range / unresolved.
        /// </summary>
        public float ForceSelectVariant(int index)
        {
            if (index < 0 || index >= VariantNodeNames.Length) return 0f;
            if (!_resolved) ResolveVariants();
            _index = index;
            ApplyVariant();
            return VariantLengthFactor[_index];
        }

        /// <summary>The MeshFilter the picker drives (shared with the cycle) — for the verify capture to read
        /// the held bounds + confirm the swapped mesh's vertex count per length. Null until the cycle's Awake.</summary>
        public MeshFilter HolderForVerify => _cycle != null ? _cycle.MeshHolder : null;

        private void Awake()
        {
            _cycle = GetComponent<HeldWeaponCycleDebug>();
            if (_cycle == null)
                Debug.LogWarning("[HeldAxeLengthPicker] no HeldWeaponCycleDebug on the held seat — the length " +
                                 "picker shares its mesh holder; cannot operate without it");
        }

        // Resolve the variant meshes from the prefab lazily (first [L] press), so a soak that never presses
        // the key pays nothing and the shipped axe never depends on the variants prefab being present.
        private void ResolveVariants()
        {
            _resolved = true;
            _variantMeshes = new Mesh[VariantNodeNames.Length];
            var prefab = Resources.Load<GameObject>(VariantsResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[HeldAxeLengthPicker] Resources/" + VariantsResourcePath +
                                 " missing — shaft-length variants cannot be resolved; the [L] picker is inert");
                return;
            }
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                for (int i = 0; i < VariantNodeNames.Length; i++)
                    if (mf.name == VariantNodeNames[i]) _variantMeshes[i] = mf.sharedMesh;
            }
            for (int i = 0; i < _variantMeshes.Length; i++)
                if (_variantMeshes[i] == null)
                    Debug.LogWarning("[HeldAxeLengthPicker] variants prefab missing mesh node '" + VariantNodeNames[i] + "'");
        }

        private void Update()
        {
            if (_cycle == null) return;

            // [L] — cycle the held axe shaft length. Only meaningful while the AXE is held.
            if (Input.GetKeyDown(lengthCycleKey))
            {
                if (!_cycle.IsAxeHeld)
                {
                    Debug.Log("[HeldAxeLengthPicker] shaft-length only applies to the AXE — cycle [" +
                              _cycle.cycleKey + "] to the axe first (knife/sword/spear have no shaft variants).");
                    return;
                }
                if (!_resolved) ResolveVariants();
                // -1 (shipped) -> 0 (1.1x) -> 1 -> 2 -> 3 (1.4x) -> wrap to 0 (stay in the variant set once
                // entered, so repeated presses sweep the four candidate lengths; the shipped axe is only the
                // pre-press default).
                _index = (_index < 0) ? 0 : (_index + 1) % VariantNodeNames.Length;
                ApplyVariant();
                Debug.Log("[HeldAxeLengthPicker] axe shaft length -> " + VariantLengthFactor[_index].ToString("F1") +
                          "x  (haft:head " + VariantHaftHeadRatio[_index].ToString("F3") + ", node=" +
                          VariantNodeNames[_index] + ")  [length picker, key=" + lengthCycleKey + "]");
                return;
            }

            // Re-assert the chosen variant if the weapon-cycle restored the shipped axe mesh on the holder
            // (cycling axe->...->axe re-applies the captured original). Cheap reference compare per frame.
            if (_index >= 0 && _cycle.IsAxeHeld)
            {
                var holder = _cycle.MeshHolder;
                if (holder != null && _variantMeshes != null && _variantMeshes[_index] != null &&
                    holder.sharedMesh != _variantMeshes[_index])
                    holder.sharedMesh = _variantMeshes[_index];
            }
        }

        // Swap the holder mesh to the chosen variant (the proven mesh-REFERENCE swap — never a vert deform).
        // The seat transform, rig, scale, and visibility gate are untouched; only MeshFilter.sharedMesh moves.
        private void ApplyVariant()
        {
            if (_index < 0) return;
            var holder = _cycle != null ? _cycle.MeshHolder : null;
            if (holder == null) return;
            Mesh m = (_variantMeshes != null && _index < _variantMeshes.Length) ? _variantMeshes[_index] : null;
            if (m == null) { Debug.LogWarning("[HeldAxeLengthPicker] variant mesh " + _index + " unresolved — keeping current"); return; }
            holder.sharedMesh = m;
        }

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = new Color(0.55f, 0.85f, 1f); // cool-blue, distinct from the cycle HUD's gold
                _keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _keyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            }

            // Top-CENTER, clear of the cycle HUD (bottom-center) + the nudge tool (right) + hotbar (bottom-left).
            float w = 470f, h = 44f;
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);
            float y = 10f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            string lenRead = _index < 0
                ? "SHIPPED (press [" + lengthCycleKey + "] to pick a length)"
                : VariantLengthFactor[_index].ToString("F1") + "x   (haft:head " +
                  VariantHaftHeadRatio[_index].ToString("F3") + ")";
            GUI.Label(new Rect(x + 10f, y + 4f, w - 20f, 20f),
                "AXE SHAFT LENGTH: " + lenRead, _labelStyle);
            GUI.Label(new Rect(x + 10f, y + 23f, w - 20f, 18f),
                "[" + lengthCycleKey + "] cycle  1.1x -> 1.2x -> 1.3x -> 1.4x   (head LOCKED + coaxial; pick + report)", _keyStyle);
        }
    }
}
