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
        // Thirst tweakables (ticket 86caamkv7 AC5). Labels are the AC-mandated names ("thirst decay rate",
        // "water scoop amount") — the gameplay-wave/hunger naming convention. LIVE-bound to the ThirstNeed.
        public const string ThirstDecayId  = "thirst_decay_rate";
        public const string WaterScoopId   = "water_scoop_amount";
        // Chop tweakable (ticket 86caa4c5c AC3). The `tree regrowth time` row drives the ChopTree's
        // regrowthMin/Max (a RANGE — organic regrowth within [min,max]). The `tool-use speed` row above
        // (ToolSpeedId) is FLIPPED LIVE to the chop swing speed by PopulateChop (ticket V1).
        public const string TreeRegrowthId = "tree_regrowth_time";

        // Range hard-limits (the absolute band each range can be dialed within — generous around the
        // current OrbitCamera defaults so the Sponsor has real room, but bounded so a dial can't break the
        // camera). Distances in world units; angles in degrees.
        public const float ZoomLower = 2f,  ZoomUpper = 40f;
        public const float PitchLower = 2f, PitchUpper = 85f;
        // Walk-speed slider band (around the moveSpeed 5.5 default).
        public const float WalkMin = 1f, WalkMax = 12f;
        // Run-speed slider band (around the runSpeed 9.5 default) — used when the hook is wired live.
        public const float RunMin = 2f, RunMax = 18f;
        // Thirst-decay slider band (around the ThirstMedDecayPerSecond 0.45 default) — gentle..punishing.
        public const float ThirstDecayMin = 0.05f, ThirstDecayMax = 1.5f;
        // Water-scoop slider band (around the waterScoopAmount 14 default) — a sip..a big gulp.
        public const float WaterScoopMin = 2f, WaterScoopMax = 40f;
        // Tool-use-speed slider band (ticket V1 — flips the reserved ToolSpeedId row LIVE to the chop swing
        // speed). Keep in sync with CastawayCharacter.ChopSpeedMin/Max (a slow..fast chop). (86caa4c5c change-(b):
        // the chop swing is the Mixamo melee Animator state now; tool-use speed scales that clip's playback rate
        // via CastawayCharacter.chopSpeed → the Attack-state ChopSpeed param, replacing ChopPoseDriver.swingSpeed.)
        public const float ToolSpeedMin = 0.25f, ToolSpeedMax = 3f;
        // Tree-regrowth range hard-limits in SECONDS — the band the `tree regrowth time` range can be dialed
        // within. Generous around the ~10-min default (instant..30 min) so the Sponsor can soak fast OR set a
        // realistic ecology. Range row → drives ChopTree.regrowthMin/Max (organic regrowth within [min,max]).
        public const float TreeRegrowthLower = 0f, TreeRegrowthUpper = 1800f;

        /// <summary>
        /// Build the standard Far Horizon settings registry against the live systems. A null target simply
        /// SKIPS the settings that bind to it (a bare test rig may pass only what it exercises), so the
        /// catalog never null-refs. <paramref name="wasd"/> drives walk speed; <paramref name="orbit"/>
        /// drives both ranges.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd)
            => Build(orbit, wasd, null);

        /// <summary>
        /// Build the standard registry AND register the thirst tweakables (ticket 86caamkv7 AC5) bound to the
        /// live <paramref name="thirst"/> need. A null thirst SKIPS the thirst settings (the catalog never
        /// null-refs), so existing callers / bare test rigs are unaffected.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst)
            => Build(orbit, wasd, thirst, null, null);

        /// <summary>
        /// Build the standard registry AND the thirst tweakables AND the CHOP tweakables (ticket 86caa4c5c):
        /// `tool-use speed` (flips the reserved <see cref="ToolSpeedId"/> row LIVE, bound to the chop swing
        /// speed — V1) + `tree regrowth time` (a new RANGE row driving the stump regrow min/max — V2/V3). A
        /// null character/tree SKIPS the chop settings (the catalog never null-refs), leaving `tool-use speed` as
        /// its greyed extension hook — so a chop-less rig / bare test is unaffected. (86caa4c5c change-(b): the
        /// chop swing is the Mixamo melee Animator state; tool-use speed binds to CastawayCharacter.chopSpeed.)
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree)
        {
            var reg = new SettingsRegistry();
            Populate(reg, orbit, wasd);
            PopulateThirst(reg, thirst);
            PopulateChop(reg, chopCharacter, chopTree);
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

        /// <summary>
        /// Register the THIRST tweakables (ticket 86caamkv7 AC5) into the registry, LIVE-bound to the given
        /// <paramref name="thirst"/> need: `thirst decay rate` (drives ThirstNeed.decayPerSecond) + `water
        /// scoop amount` (drives ThirstNeed.waterScoopAmount). Idempotent w.r.t. a null target — a null thirst
        /// registers NOTHING (the settings panel for a thirst-less rig simply lacks these rows). The settings
        /// panel host (86caa4bqp / PR #83) is MERGED, so these are LIVE rows, not greyed extension hooks. The
        /// labels are the AC-mandated names ("thirst decay rate" / "water scoop amount").
        /// </summary>
        public static void PopulateThirst(SettingsRegistry reg, FarHorizon.ThirstNeed thirst)
        {
            if (reg == null || thirst == null) return;

            // THIRST DECAY RATE (live) — drives the ACTIVE ThirstNeed.decayPerSecond (the single field the
            // decay path reads). Tightening it makes thirst nag sooner; loosening it, gentler (the difficulty
            // tiers also write this field, but the slider is a direct soak-tuning knob over the active rate).
            reg.AddFloat(ThirstDecayId, "Thirst decay rate",
                () => thirst.decayPerSecond, v => thirst.decayPerSecond = v,
                ThirstDecayMin, ThirstDecayMax, unit: "/s");

            // WATER SCOOP AMOUNT (live) — drives ThirstNeed.waterScoopAmount, the per-scoop restore. Bigger =
            // fewer scoops to comfortable; smaller = more sips (the "small amount with each scoop" cadence).
            reg.AddFloat(WaterScoopId, "Water scoop amount",
                () => thirst.waterScoopAmount, v => thirst.waterScoopAmount = v,
                WaterScoopMin, WaterScoopMax, unit: "");
        }

        /// <summary>
        /// Register the CHOP tweakables (ticket 86caa4c5c AC1+AC3) into the registry:
        /// <list type="bullet">
        /// <item><b>Tool-use speed</b> (V1) — FLIPS the reserved <see cref="ToolSpeedId"/> row LIVE. `Populate`
        /// registers that id GREYED (available:false, dummy getter); here we REMOVE that hook and re-add it
        /// bound to the live <paramref name="chopCharacter"/>.chopSpeed (the chop melee-clip playback rate —
        /// change-(b): scales the Mixamo Attack-state ChopSpeed param). Remove-then-add is the only safe path because
        /// <see cref="SettingsRegistry.Register{T}"/> throws on a duplicate id (V1: "bind + flip live, do NOT
        /// add a second row → duplicate-id collision"). The id stays `tool_use_speed` (one row).</item>
        /// <item><b>Tree regrowth time</b> (V2/V3) — a NEW <see cref="TreeRegrowthId"/> RANGE row driving
        /// <paramref name="chopTree"/>.regrowthMinSeconds / regrowthMaxSeconds (organic regrowth within
        /// [min,max], AC3). Registered as its own row (not on main yet), via THIS method (not by appending to
        /// `Populate` — the PopulateThirst de-collision precedent).</item>
        /// </list>
        /// A null character leaves `tool-use speed` greyed; a null tree skips the regrowth row — so a chop-less
        /// rig / bare EditMode test never null-refs. Idempotent w.r.t. the greyed hook (Remove no-ops if absent).
        /// </summary>
        public static void PopulateChop(SettingsRegistry reg, FarHorizon.CastawayCharacter chopCharacter,
            FarHorizon.ChopTree chopTree)
        {
            if (reg == null) return;

            // TOOL-USE SPEED (V1) — flip the reserved greyed row LIVE, bound to the chop swing speed. Remove the
            // extension hook `Populate` registered (Remove no-ops if it was never added), then re-add it live so
            // there is exactly ONE `tool_use_speed` row (no duplicate-id throw). Clamp band == CastawayCharacter's.
            // change-(b): the chop swing is the Mixamo melee Attack state; chopSpeed scales its ChopSpeed param.
            if (chopCharacter != null)
            {
                reg.Remove(ToolSpeedId);
                reg.AddFloat(ToolSpeedId, "Tool-use speed",
                    () => chopCharacter.chopSpeed,
                    v => chopCharacter.chopSpeed = Mathf.Clamp(v,
                        FarHorizon.CastawayCharacter.ChopSpeedMin, FarHorizon.CastawayCharacter.ChopSpeedMax),
                    ToolSpeedMin, ToolSpeedMax, unit: "x");
            }

            // TREE REGROWTH TIME (V2/V3) — a RANGE row driving the stump regrow window [min,max] (organic, not
            // uniform — AC3). Tightening it makes a felled stump regrow sooner; widening, more variance. The
            // setter clamps min<=max is enforced inside ChopTree.ScheduleRegrow, so the slider can't invert it.
            if (chopTree != null)
            {
                reg.AddRange(TreeRegrowthId, "Tree regrowth time",
                    () => chopTree.regrowthMinSeconds, v => chopTree.regrowthMinSeconds = v,
                    () => chopTree.regrowthMaxSeconds, v => chopTree.regrowthMaxSeconds = v,
                    TreeRegrowthLower, TreeRegrowthUpper, unit: "s");
            }
        }
    }
}
