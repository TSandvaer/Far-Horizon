using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// Wires Far Horizon's CURRENTLY-AVAILABLE gameplay params into a <see cref="SettingsRegistry"/>
    /// (ticket 86caa4bqp AC3) and leaves clearly-named EXTENSION HOOKS for the not-yet-built ones.
    ///
    /// This is the one place that knows which live system each setting binds to — so a future ticket
    /// either flips an extension hook to live (run/jump/tool-use) or calls a registry Add* with its own
    /// binding (belt/inventory slots, 86caa4bya). The registry + entries are param-agnostic; THIS catalog
    /// is the Far-Horizon-specific binding map.
    ///
    /// WIRED LIVE NOW (AC3 — the features exist):
    ///   • Zoom range      → OrbitCamera.minDistance / maxDistance (range; clamps the live distance, AC4)
    ///   • View-angle range→ OrbitCamera.minPitch / maxPitch       (range; clamps the live pitch,    AC4)
    ///   • Walk speed      → WasdMovement.moveSpeed                (slider)
    ///
    /// EXTENSION HOOKS (AC3 — Available=false, greyed "(soon)", NOT bound to a fake param):
    ///   • Run speed       → lands with run-on-Shift; the param (WasdMovement.runSpeed) ALREADY exists, so
    ///                       this is wired LIVE when the camera/run feature is confirmed merged — see note.
    ///   • Jump height     → lands with jump-on-Space (param not yet a tunable height field)
    ///   • Tool-use speed  → lands with chop-tree (no chop param exists yet)
    ///
    /// Pure static builder (no Unity lifecycle) so the binding map is unit-testable in EditMode against a
    /// bare OrbitCamera/WasdMovement (AC6).
    /// </summary>
    public static class SettingsCatalog
    {
        // Setting ids (stable — the PlayerPrefs keys + test lookups derive from these).
        public const string ZoomRangeId    = "zoom_range";
        public const string PitchRangeId   = "view_angle_range";
        public const string WalkSpeedId    = "walk_speed";
        public const string RunSpeedId     = "run_speed";
        public const string JumpHeightId   = "jump_height";
        public const string ToolSpeedId    = "tool_use_speed";

        // Range hard-limits (the absolute band each range can be dialed within — generous around the
        // current OrbitCamera defaults so the Sponsor has real room, but bounded so a dial can't break the
        // camera). Distances in world units; angles in degrees.
        public const float ZoomLower = 2f,  ZoomUpper = 40f;
        public const float PitchLower = 2f, PitchUpper = 85f;
        // Walk-speed slider band (around the moveSpeed 5.5 default).
        public const float WalkMin = 1f, WalkMax = 12f;
        // Run-speed slider band (around the runSpeed 9.5 default) — used when the hook is wired live.
        public const float RunMin = 2f, RunMax = 18f;

        /// <summary>
        /// Build the standard Far Horizon settings registry against the live systems. A null target simply
        /// SKIPS the settings that bind to it (a bare test rig may pass only what it exercises), so the
        /// catalog never null-refs. <paramref name="wasd"/> drives walk speed; <paramref name="orbit"/>
        /// drives both ranges.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd)
        {
            var reg = new SettingsRegistry();
            Populate(reg, orbit, wasd);
            return reg;
        }

        /// <summary>Populate an existing registry (so callers can add their own settings before/after).</summary>
        public static void Populate(SettingsRegistry reg, OrbitCamera orbit, WasdMovement wasd)
        {
            if (reg == null) return;

            // --- ZOOM RANGE (live, AC3/AC4) — OrbitCamera clamps its runtime distance to min/max every
            //     LateUpdate, so tightening the range re-clamps the live camera immediately. ---
            if (orbit != null)
            {
                reg.AddRange(ZoomRangeId, "Zoom range",
                    () => orbit.minDistance, v => orbit.minDistance = v,
                    () => orbit.maxDistance, v => orbit.maxDistance = v,
                    ZoomLower, ZoomUpper, unit: "u");

                // --- VIEW-ANGLE (PITCH) RANGE (live, AC3/AC4) — OrbitCamera clamps its runtime pitch to
                //     min/max every Apply, so a tightened range re-clamps the live tilt immediately. ---
                reg.AddRange(PitchRangeId, "View-angle range",
                    () => orbit.minPitch, v => orbit.minPitch = v,
                    () => orbit.maxPitch, v => orbit.maxPitch = v,
                    PitchLower, PitchUpper, unit: "°"); // °
            }

            // --- WALK SPEED (live, AC3) — WasdMovement.moveSpeed drives the agent velocity each frame. ---
            if (wasd != null)
            {
                reg.AddFloat(WalkSpeedId, "Walk speed",
                    () => wasd.moveSpeed, v => wasd.moveSpeed = v,
                    WalkMin, WalkMax, unit: "u/s");

                // --- RUN SPEED — the run-on-Shift param (WasdMovement.runSpeed) EXISTS on the merged WASD
                //     component, so we wire it LIVE (AC3 said "wire when it merges / if the param exists" —
                //     it exists). It only takes effect once Shift-to-run is in play, but the knob is real. ---
                reg.AddFloat(RunSpeedId, "Run speed",
                    () => wasd.runSpeed, v => wasd.runSpeed = v,
                    RunMin, RunMax, unit: "u/s");
            }

            // --- JUMP HEIGHT — EXTENSION HOOK (AC3, Available=false). Jump-on-Space (86ca9yq3q) is queued;
            //     there is no single tunable jump-HEIGHT field bound here yet, so we reserve the row greyed
            //     rather than fake a param. The downstream ticket flips available:true + adds the binding. ---
            reg.AddFloat(JumpHeightId, "Jump height",
                () => 0f, _ => { }, 0f, 5f, available: false, unit: "u");

            // --- TOOL-USE SPEED — EXTENSION HOOK (AC3, Available=false). Lands with chop-tree; no chop
            //     animation-speed param exists yet, so reserve the row greyed. ---
            reg.AddFloat(ToolSpeedId, "Tool-use speed",
                () => 1f, _ => { }, 0.25f, 3f, available: false, unit: "x");
        }
    }
}
