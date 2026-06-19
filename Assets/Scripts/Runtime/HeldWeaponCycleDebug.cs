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

        // The four family meshes' names inside Resources/WeaponSetLineup.prefab (the child object names =
        // the FBX file-name-without-extension; see WeaponPackAssetGen.BuildLineupPrefab). Order = cycle order.
        // PUBLIC + static so the EditMode guard reads the cycle contract directly (no reflection): the lineup
        // prefab MUST carry a mesh node for each of these, or cycling resolves nothing.
        public static readonly string[] WeaponNodeNames = { "wpn_axe_01", "wpn_knife_01", "wpn_sword_01", "wpn_spear_01" };
        public static readonly string[] WeaponLabels = { "AXE", "KNIFE", "SWORD", "SPEAR" };
        public const string LineupResourcePath = "WeaponSetLineup"; // Assets/Resources/WeaponSetLineup.prefab

        // ROUGH per-weapon mesh-holder compensation (look-soak only, NOT the precise grip — that is the
        // later equip ticket). Index 0 (axe) is ALWAYS zero/identity — the axe seat is Sponsor-LOCKED and is
        // restored to its captured original. knife/sword/spear: the axe FBX is height-normalized (~1u) with a
        // grip-MIDPOINT origin (z=0.45); these are un-normalized (taller) with a grip origin at the base
        // (0,0,0, blade +Z), so without compensation they would float ABOVE the hand and read oversized. A
        // uniform down-scale brings them to a hand-sized read, and a small drop along the mesh-holder's local
        // axis sinks the grip into the palm. These are REASONABLE defaults; the exact per-weapon grip is OOS.
        // PUBLIC so the EditMode guard pins the axe-locked-default contract (index 0 == identity).
        public static readonly float[] WeaponMeshScale = { 1f, 0.55f, 0.50f, 0.42f };
        // Local-space drop applied to the mesh-holder child for the non-axe weapons (their origin is the grip
        // BASE, so they need pulling back along the blade axis to sit the grip in the palm). Axe = zero.
        public static readonly Vector3[] WeaponMeshLocalOffset =
        {
            Vector3.zero,                       // axe — LOCKED, no compensation
            new Vector3(0f, 0f, -0.22f),        // knife
            new Vector3(0f, 0f, -0.42f),        // sword
            new Vector3(0f, 0f, -0.70f),        // spear (longest — pull the grip furthest back)
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

        private void Awake()
        {
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
            if (!Input.GetKeyDown(cycleKey)) return;

            if (!_resolved) ResolveMeshes();
            _index = (_index + 1) % WeaponNodeNames.Length;
            ApplyCurrent();
            Debug.Log("[HeldWeaponCycleDebug] held weapon -> " + WeaponLabels[_index] +
                      " (" + WeaponNodeNames[_index] + ")  [DEBUG cycle, key=" + cycleKey + "]");
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
                // knife / sword / spear — ROUGH look-soak seat on the SHARED seat (precise grip is OOS).
                // Compose the per-weapon offset/scale ON TOP of the axe's captured baseline so the weapon
                // rides the same seat the axe does, just nudged to seat reasonably in the hand.
                t.localPosition = _holderOrigPos + WeaponMeshLocalOffset[_index];
                t.localRotation = _holderOrigRot;
                t.localScale = _holderOrigScale * WeaponMeshScale[_index];
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
            float w = 360f, h = 44f;
            float x = Mathf.Max(8f, (Screen.width - w) * 0.5f);
            float y = Screen.height - h - 10f;
            var panel = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 10f, y + 5f, w - 20f, 20f),
                "DEBUG — held weapon: " + WeaponLabels[_index], _labelStyle);
            GUI.Label(new Rect(x + 10f, y + 24f, w - 20f, 18f),
                "[" + cycleKey + "] cycle  axe -> knife -> sword -> spear   (soak view, not equip)", _keyStyle);
        }
    }
}
