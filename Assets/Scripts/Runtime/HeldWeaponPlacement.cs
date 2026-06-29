using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The SINGLE binding seam between the unified dev-tweak SETTINGS CONSOLE (<see cref="FarHorizon.SettingsPanel"/>)
    /// and the live in-hand placement of WHICHEVER weapon the castaway is currently holding (ticket 86caffwuz —
    /// "nudge all weapons in place (in-hand)").
    ///
    /// WHY THIS EXISTS. The held weapon's seat is calibrated by a baked + committed transform (the ticket's hard
    /// constraint: bake, don't runtime-only). But the seat data lives in TWO places by weapon:
    ///   • AXE → <see cref="HeldAxeRig.worldOffsetFromHand"/> (hand-local POSITION) + <see cref="HeldAxeRig.relEuler"/>
    ///     (hand-relative ROTATION); the axe's SCALE rides the bone hierarchy and is Sponsor-LOCKED.
    ///   • knife / sword / spear → <see cref="HeldWeaponCycleDebug"/>'s per-weapon mesh-holder arrays
    ///     (WeaponMeshLocalOffset / WeaponMeshLocalEuler / WeaponMeshScale), composed on the SAME shared seat.
    /// This component presents ONE uniform get/set surface over those two backends for the CURRENTLY-held weapon,
    /// so the unified console can show 7 generic rows (pos X/Y/Z, rot pitch/yaw/roll, scale) bound to "the weapon
    /// in the hand right now" — exactly the give-him-the-knob, layout-agnostic, MOUSE-driven instrument the Sponsor
    /// wants ([[sponsor-wants-unified-dev-tweak-console]] + [[sponsor-danish-keyboard-layout]]), without a parallel
    /// attach mechanism (the ticket integration constraint — it drives the EXISTING equip-path seat).
    ///
    /// IT NEVER ADDS A NEW ATTACH PATH. It only READS/WRITES the existing seat fields (axe rig) or routes through
    /// <see cref="HeldWeaponCycleDebug.NudgeCurrentWeapon"/> (non-axe). The committed defaults are unchanged; the
    /// console is the dial, and the team re-bakes the dialed numbers into the source constants
    /// (MovementCameraScene.HeldAxeLocalOffsetFromHand / HeldAxeRelEuler for the axe; HeldWeaponCycleDebug's
    /// per-weapon arrays for the rest) — the [[verify-soak-builds-or-bake-and-judge]] workflow.
    ///
    /// AXE SCALE is the axe's Sponsor-LOCKED channel. The scale row, for the axe, drives a UNIFORM multiplier on
    /// the mesh-holder localScale whose default is 1.0 — so leaving it untouched keeps the axe seat byte-identical
    /// to the shipped lock (bar #6 — don't regress a praised grip). For knife/sword/spear the scale row drives
    /// their per-weapon held scale (the round-1 dial channel).
    ///
    /// STATIC STATE: instance fields only — NO mutable runtime statics, so no SubsystemRegistration reset is
    /// needed (unity-conventions.md §Configurable Enter Play Mode; StaticStateResetTests stays green).
    ///
    /// SERIALIZATION (unity-conventions.md §editor-vs-runtime): authored editor-time onto the HeroAxe seat object
    /// (MovementCameraScene.AttachHeroAxeToHand) so it ships in Boot.unity riding the seat; the Awake resolves are
    /// build-safety fallbacks, not the ship path.
    /// </summary>
    public class HeldWeaponPlacement : MonoBehaviour
    {
        [Tooltip("The axe's pose-driving rig (worldOffsetFromHand / relEuler). Wired editor-time; Awake resolves " +
                 "from this object as a fallback.")]
        public HeldAxeRig axeRig;

        [Tooltip("The weapon-cycle debug handle that owns the per-weapon (knife/sword/spear) mesh-holder offset/" +
                 "euler/scale + the current-held index. Wired editor-time; Awake resolves from this object.")]
        public HeldWeaponCycleDebug weaponCycle;

        // The axe's mesh-holder localScale is the axe scale channel (default 1.0 multiplier = byte-locked seat).
        // Captured at Awake so the scale row reads/writes a UNIFORM multiplier about that captured baseline.
        private float _axeScaleBaseline = 1f;
        private bool _axeScaleCaptured;

        private void Awake()
        {
            if (axeRig == null) axeRig = GetComponent<HeldAxeRig>() ?? GetComponentInParent<HeldAxeRig>();
            if (weaponCycle == null) weaponCycle = GetComponent<HeldWeaponCycleDebug>() ?? GetComponentInParent<HeldWeaponCycleDebug>();
            CaptureAxeScaleBaseline();
        }

        private void CaptureAxeScaleBaseline()
        {
            if (_axeScaleCaptured) return;
            if (weaponCycle != null && weaponCycle.MeshHolder != null)
            {
                // The axe mesh-holder's uniform localScale at authoring time IS the locked baseline; the scale
                // row drives a multiplier of 1.0 about it (so untouched == byte-identical locked axe seat).
                _axeScaleBaseline = weaponCycle.MeshHolder.transform.localScale.x;
                if (_axeScaleBaseline <= 1e-5f) _axeScaleBaseline = 1f;
                _axeScaleCaptured = true;
            }
        }

        /// <summary>True when the AXE is the currently-held weapon (its seat lives on the axe rig, not the
        /// per-weapon arrays). Used by the console rows to choose the backend + label.</summary>
        public bool IsAxeCurrent => weaponCycle == null || weaponCycle.CurrentIndex == 0;

        /// <summary>The currently-held weapon's label (AXE/KNIFE/SWORD/SPEAR) for the console row readouts.</summary>
        public string CurrentLabel => weaponCycle != null ? weaponCycle.CurrentLabel : "AXE";

        // -------- POSITION (hand-local offset for the axe; mesh-holder local offset for the rest) --------

        /// <summary>The current weapon's in-hand position offset (hand-local for the axe; mesh-holder-local for
        /// knife/sword/spear). The console binds X/Y/Z to the components of this.</summary>
        public Vector3 Offset
        {
            get
            {
                if (IsAxeCurrent) return axeRig != null ? axeRig.worldOffsetFromHand : Vector3.zero;
                return weaponCycle.CurrentOffset;
            }
        }

        /// <summary>Set the current weapon's offset (whole vector). Routes to the axe rig or the per-weapon
        /// array. Drives the live seat immediately so the console dial shows this frame.</summary>
        public void SetOffset(Vector3 v)
        {
            if (IsAxeCurrent)
            {
                if (axeRig != null) axeRig.worldOffsetFromHand = v;
            }
            else
            {
                // Route through the weapon-cycle's per-weapon nudge (delta from current), re-seating the holder.
                weaponCycle.NudgeCurrentWeapon(v - weaponCycle.CurrentOffset, Vector3.zero, 1f);
            }
        }

        public float OffsetX { get => Offset.x; set { var o = Offset; o.x = value; SetOffset(o); } }
        public float OffsetY { get => Offset.y; set { var o = Offset; o.y = value; SetOffset(o); } }
        public float OffsetZ { get => Offset.z; set { var o = Offset; o.z = value; SetOffset(o); } }

        // -------- ROTATION (hand-relative euler for the axe; mesh-holder euler for the rest) --------

        /// <summary>The current weapon's in-hand rotation euler (hand-relative for the axe; mesh-holder-local
        /// for knife/sword/spear). The console binds pitch/yaw/roll to the components of this.</summary>
        public Vector3 Euler
        {
            get
            {
                if (IsAxeCurrent) return axeRig != null ? axeRig.relEuler : Vector3.zero;
                return weaponCycle.CurrentEuler;
            }
        }

        /// <summary>Set the current weapon's euler (whole vector). Routes to the axe rig or the per-weapon array.</summary>
        public void SetEuler(Vector3 v)
        {
            if (IsAxeCurrent)
            {
                if (axeRig != null) axeRig.relEuler = v;
            }
            else
            {
                weaponCycle.NudgeCurrentWeapon(Vector3.zero, v - weaponCycle.CurrentEuler, 1f);
            }
        }

        public float Pitch { get => Euler.x; set { var e = Euler; e.x = value; SetEuler(e); } }
        public float Yaw   { get => Euler.y; set { var e = Euler; e.y = value; SetEuler(e); } }
        public float Roll  { get => Euler.z; set { var e = Euler; e.z = value; SetEuler(e); } }

        // -------- SCALE (uniform; axe drives a 1.0-default multiplier on the locked mesh-holder; rest the
        //          per-weapon held scale) --------

        /// <summary>The current weapon's uniform in-hand scale. For the axe this is a multiplier of the LOCKED
        /// mesh-holder baseline (1.0 = byte-identical locked seat); for knife/sword/spear it is their per-weapon
        /// held scale (the round-1 dial channel).</summary>
        public float Scale
        {
            get
            {
                if (IsAxeCurrent)
                {
                    CaptureAxeScaleBaseline();
                    if (weaponCycle != null && weaponCycle.MeshHolder != null && _axeScaleBaseline > 1e-5f)
                        return weaponCycle.MeshHolder.transform.localScale.x / _axeScaleBaseline;
                    return 1f;
                }
                return weaponCycle.CurrentScale;
            }
            set
            {
                float s = Mathf.Max(0.05f, value);
                if (IsAxeCurrent)
                {
                    CaptureAxeScaleBaseline();
                    if (weaponCycle != null && weaponCycle.MeshHolder != null)
                    {
                        // UNIFORM multiplier about the captured locked baseline — 1.0 leaves the seat byte-locked.
                        weaponCycle.MeshHolder.transform.localScale = Vector3.one * (_axeScaleBaseline * s);
                    }
                }
                else
                {
                    // Multiplicative delta toward the target so the per-weapon held scale lands at `s`.
                    float cur = Mathf.Max(0.01f, weaponCycle.CurrentScale);
                    weaponCycle.NudgeCurrentWeapon(Vector3.zero, Vector3.zero, s / cur);
                }
            }
        }
    }
}
