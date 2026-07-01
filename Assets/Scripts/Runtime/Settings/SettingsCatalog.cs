using System.Collections.Generic;
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
        // Air-control accel (ticket 86caambxh). The `Air-control accel` row drives WasdMovement.airControlAccel —
        // how strongly A/D steers the player WHILE AIRBORNE (u/s²). The Sponsor soak-APPROVED the locomotion but
        // asked to dial the mid-air A/D nudge subtler; the shipped default is lowered 8→5 AND this live slider lets
        // him fine-tune it in the soak ([[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]] +
        // [[sponsor-wants-unified-dev-tweak-console]]). GROUNDED movement is unaffected (it commands full speed).
        public const string AirControlAccelId = "air_control_accel";
        public const string JumpHeightId   = "jump_height";
        public const string ToolSpeedId    = "tool_use_speed";
        // Thirst tweakables (ticket 86caamkv7 AC5). Labels are the AC-mandated names ("thirst decay rate",
        // "water scoop amount") — the gameplay-wave/hunger naming convention. LIVE-bound to the ThirstNeed.
        public const string ThirstDecayId  = "thirst_decay_rate";
        public const string WaterScoopId   = "water_scoop_amount";
        // Hunger tweakables (ticket 86cabd75y — the 86caamkp8 AC4 settings-registration follow-up). Labels are
        // the AC-mandated names ("Hunger decay rate", "Berry restore amount") — the same gameplay-wave/needs
        // naming convention as thirst. LIVE-bound to the HungerNeed (PopulateThirst mirror; the WarmthNeed→base
        // refactor 86cabgvgw is NOT a hard-dep, so this binds HungerNeed directly per the ticket default).
        public const string HungerDecayId  = "hunger_decay_rate";
        public const string BerryRestoreId = "berry_restore_amount";
        // Berry-regrowth tweakable (ticket 86cabn67w — the 86caa5zz3 AC4 settings-registration follow-up).
        // The `Berry regrowth time` row drives EVERY BerryBush's regrowMinSeconds / regrowMaxSeconds (a RANGE
        // — RANDOM regrowth within [min,max], like tree-regrowth / stone-respawn). DISTINCT id from #183's
        // HungerNeed `berry_restore_amount` (BerryRestoreId above) — this is the per-bush REGROW TIMER, not the
        // per-berry hunger satisfaction. Registered by PopulateBerry (the PopulateThirst/Stones de-collision
        // precedent — each feature adds its OWN Populate method, never grows the base Populate signature).
        public const string BerryRegrowthId = "berry_regrowth_time";
        // Chop tweakable (ticket 86caa4c5c AC3). The `tree regrowth time` row drives the ChopTree's
        // regrowthMin/Max (a RANGE — organic regrowth within [min,max]). The `tool-use speed` row above
        // (ToolSpeedId) is FLIPPED LIVE to the chop swing speed by PopulateChop (ticket V1).
        public const string TreeRegrowthId = "tree_regrowth_time";
        // Tree-chop REWORK tweakables (ticket 86caf9u5t). Three NEW rows registered by PopulateChop:
        //   • `tree-chop wood yield`  → LogPileSpawner.WoodYield  (int, logs per FALLEN tree; default 10, 1–50).
        //   • `chops-to-fell`         → ChopTree.chopsToFell      (int, chops to fell;        default 3, 1–10).
        //   • `log-pile despawn`      → LogPileSpawner.DespawnSeconds (float, pile lifetime;  default 180s).
        public const string TreeWoodYieldId = "tree_chop_wood_yield";
        public const string ChopsToFellId   = "chops_to_fell";
        public const string LogPileDespawnId = "log_pile_despawn";
        // Fallen-tree fade-out (ticket 86caff4ad — the #165-soak NIT). A FLOAT row driving
        // ChopTree.fadeOutDelaySeconds (seconds a felled tree rests before it fades out + disappears; default 2s,
        // range 0–30s). Registered by PopulateChop, bound to the chop tree (like chops-to-fell).
        public const string TreeFadeOutId = "tree_fade_out";
        // Stone tweakable (ticket 86caa4c96 AC3). The `stone respawn time` row drives the StoneRespawner's
        // RespawnMin/Max (a RANGE — a RANDOM respawn within [min,max]; every StoneProp reads this shared
        // window). Registered by PopulateStones (the PopulateThirst/PopulateChop de-collision precedent).
        public const string StoneRespawnId = "stone_respawn_time";
        // Held-weapon in-hand placement tweakables (ticket 86caffwuz — "nudge all weapons in place"). SEVEN rows
        // bound to the CURRENTLY-held weapon's seat via HeldWeaponPlacement (the single binding seam over the axe
        // rig + the per-weapon arrays): position X/Y/Z, rotation pitch/yaw/roll, and a uniform scale. MOUSE-driven
        // sliders → Danish-keyboard-safe + on the unified console ([[sponsor-wants-unified-dev-tweak-console]] +
        // [[sponsor-danish-keyboard-layout]]). Registered by PopulateHeldWeapon.
        public const string HeldPosXId  = "held_weapon_pos_x";
        public const string HeldPosYId  = "held_weapon_pos_y";
        public const string HeldPosZId  = "held_weapon_pos_z";
        public const string HeldPitchId = "held_weapon_pitch";
        public const string HeldYawId   = "held_weapon_yaw";
        public const string HeldRollId  = "held_weapon_roll";
        public const string HeldScaleId = "held_weapon_scale";
        // Inventory tweakables (ticket 86cabfa4e — the #90 / 86caa4bya AC1/AC2/AC7 settings-registration follow-up,
        // deferred until the SettingsRegistry foundation #83 landed). THREE INT-STEPPER rows bound to the live
        // Inventory façade: `inventory slots` (AC1) / `belt slots` (AC2) drive Inventory.InventorySlotCount /
        // BeltSlotCount (the authoring counts the model is built from; a dev-console change REBUILDS the model so
        // it takes effect live — a dev tool, not a player-facing live-resize of a populated grid) + `inventory
        // stack size` (AC7) drives ItemDef.ResourceStackSize (the shared resource/consumable per-slot cap, default
        // seeded from ItemDef.DefaultResourceStack=20). Registered by PopulateInventory (the PopulateThirst /
        // PopulateStones / PopulateHeldWeapon de-collision precedent — each feature adds its OWN Populate method,
        // never grows the base Populate signature). DEV-CONSOLE only ([[sponsor-wants-unified-dev-tweak-console]]).
        public const string InventorySlotsId = "inventory_slots";
        public const string BeltSlotsId      = "belt_slots";
        public const string StackSizeId      = "inventory_stack_size";
        // F-KEY MIGRATION (ticket 86caber95 — consolidate the standalone F7/F9/F10 live-tune panels into the
        // console as entries). Each Tab-cycled dial target becomes its OWN row (AC1/AC2); vectors are DECOMPOSED
        // into per-axis float rows (AC4 — reuses the slider/nudge path, no new archetype). The legacy F-key
        // panels stay LIVE in parallel until the console version is soak-confirmed (AC5). Ids are stable
        // (PlayerPrefs keys + test lookups derive from them). Registered by PopulateCameraFollow /
        // PopulateArmAndGround / PopulateWorldLook (the per-feature Populate de-collision precedent).

        // F7 CameraFollowNudgeTool → OrbitCamera follow gains (AC3; PR #77 is MERGED so F7 IS in scope).
        public const string CamFollowLerpId    = "cam_follow_lerp";
        public const string CamVertFollowLerpId = "cam_vertical_follow_lerp";
        public const string CamFollowLeadTimeId = "cam_follow_lead_time";
        public const string CamAirborneLerpId   = "cam_airborne_follow_lerp";

        // F9 AxeNudgeTool → ground-Y + arm-pose (AC1). Held-axe offset is ALREADY migrated (PopulateHeldWeapon,
        // 86caffwuz); stump-axe is a named scene transform kept on the legacy F9 tool for now (OOS-scoped here).
        // Ground-Y (the single-float VALIDATION entry, per AC1 "start with the single-float ground-Y").
        public const string GroundYOffsetId = "ground_y_offset";
        // Arm-pose per-axis eulers (AC4 decompose): RIGHT arm pitch/yaw/roll, LEFT arm pitch/yaw/roll.
        public const string ArmRightPitchId = "arm_right_pitch";
        public const string ArmRightYawId   = "arm_right_yaw";
        public const string ArmRightRollId  = "arm_right_roll";
        public const string ArmLeftPitchId  = "arm_left_pitch";
        public const string ArmLeftYawId    = "arm_left_yaw";
        public const string ArmLeftRollId   = "arm_left_roll";
        // Run arm-lower per-axis eulers (the F9 RUN target — the calmer run carry).
        public const string RunLowerPitchId = "run_lower_pitch";
        public const string RunLowerYawId   = "run_lower_yaw";
        public const string RunLowerRollId  = "run_lower_roll";

        // F10 WorldLookNudgeTool → world-look dials (AC2). Colour STOPS decomposed per-channel (AC4).
        public const string FogDensityId    = "fog_density";
        public const string FogColorRId     = "fog_color_r";
        public const string FogColorGId     = "fog_color_g";
        public const string FogColorBId     = "fog_color_b";
        public const string SkyHorizonRId   = "sky_horizon_r";
        public const string SkyHorizonGId   = "sky_horizon_g";
        public const string SkyHorizonBId   = "sky_horizon_b";
        public const string CloudScaleId    = "cloud_scale";
        public const string CloudAltitudeId = "cloud_altitude";
        public const string MtnDistanceId   = "mountain_distance";
        public const string MtnPeakScaleId  = "mountain_peak_scale";
        public const string MtnWarmthId     = "mountain_warmth";
        public const string MtnBrightnessId = "mountain_brightness";
        public const string SunElevationId  = "sun_elevation";
        public const string SunSizeId       = "sun_size";

        // Console UI scale (86cabeqj9 soak NIT — the panel/text read very large at the Sponsor's resolution).
        // A FLOAT slider multiplying the panel element's transform.scale so he dials the whole console (plate +
        // text) live. NOT a gameplay param — bound to a SettingsPanel-owned scale field (registered by the panel
        // itself in Start, NOT here in the catalog, since it is a pure-UI concern the catalog has no target for).
        // The id is exposed here only so the panel + tests share the stable PlayerPrefs-key/lookup string.
        public const string ConsoleUiScaleId = "console_ui_scale";

        // The UI TEXT scale id (86cabeqj9 soak NIT). DISTINCT from ConsoleUiScaleId: this scales the panel
        // FONT size only (the Sponsor makes the text bigger/smaller), independent of the chrome-scaling
        // Console UI scale above. Panel-owned (registered by SettingsPanel.Start), exposed here only so the
        // panel + tests share the stable PlayerPrefs-key/lookup string.
        public const string ConsoleTextScaleId = "console_text_scale";

        // Range hard-limits (the absolute band each range can be dialed within — generous around the
        // current OrbitCamera defaults so the Sponsor has real room, but bounded so a dial can't break the
        // camera). Distances in world units; angles in degrees.
        public const float ZoomLower = 2f,  ZoomUpper = 40f;
        public const float PitchLower = 2f, PitchUpper = 85f;
        // Walk-speed slider band (around the moveSpeed 5.5 default).
        public const float WalkMin = 1f, WalkMax = 12f;
        // Run-speed slider band (around the runSpeed 9.5 default) — used when the hook is wired live.
        public const float RunMin = 2f, RunMax = 18f;
        // Air-control-accel slider band (ticket 86caambxh; around the lowered 5 u/s² default). From 0 (NO mid-air
        // steer — pure ballistic coast) up to 12 (the pre-#71-fix territory), so the Sponsor can soak the A/D
        // nudge from "none" through the shipped-subtle 5 to "as speedy as before" and pick the feel.
        public const float AirControlAccelMin = 0f, AirControlAccelMax = 12f;
        // Thirst-decay slider band (around the ThirstMedDecayPerSecond 0.45 default) — gentle..punishing.
        public const float ThirstDecayMin = 0.05f, ThirstDecayMax = 1.5f;
        // Water-scoop slider band (around the waterScoopAmount 14 default) — a sip..a big gulp.
        public const float WaterScoopMin = 2f, WaterScoopMax = 40f;
        // Hunger-decay slider band (around the HungerMedDecayPerSecond 0.35 default) — gentle..punishing. The
        // band brackets 0.35 sanely (0.1..1.0), mirroring the ThirstDecayMin/Max band shape (a touch tighter
        // than thirst's 0.05..1.5 since hunger is the SLOWER background pressure — food is found, not lost).
        public const float HungerDecayMin = 0.1f, HungerDecayMax = 1.0f;
        // Berry-restore slider band (around the berryRestoreAmount 18 default) — a nibble..a hearty handful.
        public const float BerryRestoreMin = 5f, BerryRestoreMax = 50f;
        // Tool-use-speed slider band (ticket V1 — flips the reserved ToolSpeedId row LIVE to the chop swing
        // speed). Keep in sync with CastawayCharacter.ChopSpeedMin/Max (a slow..fast chop). (86caa4c5c change-(b):
        // the chop swing is the Mixamo melee Animator state now; tool-use speed scales that clip's playback rate
        // via CastawayCharacter.chopSpeed → the Attack-state ChopSpeed param, replacing ChopPoseDriver.swingSpeed.)
        public const float ToolSpeedMin = 0.25f, ToolSpeedMax = 3f;
        // Tree-regrowth range hard-limits in SECONDS — the band the `tree regrowth time` range can be dialed
        // within. Generous around the ~10-min default (instant..30 min) so the Sponsor can soak fast OR set a
        // realistic ecology. Range row → drives ChopTree.regrowthMin/Max (organic regrowth within [min,max]).
        public const float TreeRegrowthLower = 0f, TreeRegrowthUpper = 1800f;
        // Berry-regrowth range hard-limits in SECONDS — the band the `Berry regrowth time` range can be dialed
        // within. The shipped BerryBush defaults are 20s..40s; the band is generous (instant..30 min) so the
        // Sponsor can soak fast (berries return almost immediately) OR set a slower, more deliberate forage
        // cadence. Range row → drives EVERY BerryBush.regrowMin/MaxSeconds (random regrowth within [min,max]).
        // Same band shape as tree-regrowth / stone-respawn (the two sibling regrow/respawn timers).
        public const float BerryRegrowthLower = 0f, BerryRegrowthUpper = 1800f;
        // Log-pile despawn band in SECONDS (ticket 86caf9u5t AC5) — the band the `log-pile despawn` slider can be
        // dialed within. Generous around the ~180s default (instant..10 min): the Sponsor can soak fast (a pile
        // that vanishes quickly) OR keep the wood around. Float row → LogPileSpawner.DespawnSeconds.
        public const float LogPileDespawnLower = 0f, LogPileDespawnUpper = 600f;
        // Stone-respawn range hard-limits in SECONDS — the band the `stone respawn time` range can be dialed
        // within. Generous around the ~10-min default (instant..30 min) so the Sponsor can soak fast OR a
        // realistic scarcity. Range row → drives StoneRespawner.RespawnMin/Max (random respawn within [min,max]).
        public const float StoneRespawnLower = 0f, StoneRespawnUpper = 1800f;
        // Held-weapon placement slider bands (ticket 86caffwuz). The position offset is hand-local cm-scale for
        // the axe (± a generous 0.6u covers a full re-seat in the grip); rotation is a full 360° band per axis
        // (the axe relEuler accumulates raw — e.g. (-186,-168,-84) — so the band must span the wrap, not clamp at
        // ±180); scale is 0.2..3x (a knife small..a spear large, the axe at 1.0 locked baseline).
        public const float HeldPosMin = -0.6f, HeldPosMax = 0.6f;
        public const float HeldRotMin = -360f, HeldRotMax = 360f;
        public const float HeldScaleMin = 0.2f, HeldScaleMax = 3f;
        // Inventory slot/stack INT bands (ticket 86cabfa4e). Generous around the AC defaults (inventory 20, belt 5,
        // stack 20) so the Sponsor can soak a cramped pack OR a roomy one. Belt is capped at 12 (the bottom-of-
        // screen hotbar has finite horizontal room + number-key selection 1–9; AC2's default is 5). Stack cap is
        // generous (1..99) — a single-item-per-slot survival OR a deep-stack convenience.
        public const int InventorySlotsMin = 1,  InventorySlotsMax = 60;
        public const int BeltSlotsMin = 1,       BeltSlotsMax = 12;
        public const int StackSizeMin = 1,       StackSizeMax = 99;
        // Console UI scale band (86cabeqj9 soak NIT). 0.5x (half-size, for a Sponsor who finds the panel huge) ..
        // 1.5x (larger). Default 1.0x = the shipped panel, byte-identical untouched (the differs-badge stays off).
        public const float ConsoleUiScaleMin = 0.5f, ConsoleUiScaleMax = 1.5f;
        // Console TEXT scale band (86cabeqj9 soak NIT). 0.6x (denser) .. 2.0x (big-text accessibility). Default
        // 1.0x = the shipped font sizes, untouched (the differs-badge stays off).
        public const float ConsoleTextScaleMin = 0.6f, ConsoleTextScaleMax = 2.0f;

        // --- F-KEY MIGRATION bands (86caber95) — generous around the shipped defaults so the Sponsor has real
        //     soak room, bounded so a dial can't break the camera/world. ---
        // Camera follow gains (F7). followLerp/vertical/airborne are 1/s rates; lead-time is seconds.
        public const float CamFollowLerpMin = 0f,  CamFollowLerpMax = 60f;   // horiz follow-lerp (default 18)
        public const float CamVertFollowMin = 0f,  CamVertFollowMax = 120f;  // vertical follow-lerp (default 60)
        public const float CamAirborneMin   = 0f,  CamAirborneMax   = 120f;  // airborne follow-lerp (default 60)
        public const float CamLeadTimeMin   = 0f,  CamLeadTimeMax   = 0.25f; // lead-time seconds (0 = AUTO; == OrbitCamera.maxLeadTime)
        // Ground-Y offset (F9) — a small world-Y nudge on the feet (default 0). ±0.5u covers a full re-plant.
        public const float GroundYMin = -0.5f, GroundYMax = 0.5f;
        // Arm-pose + run-lower eulers (F9) — full ±180° per axis (the euler accumulates raw; a broad band).
        public const float ArmEulerMin = -180f, ArmEulerMax = 180f;
        // Fog (F10). Density is a small Exp² coefficient; colour channels are 0..1.
        public const float FogDensityMin = 0f, FogDensityMax = 0.02f;
        public const float ColorChannelMin = 0f, ColorChannelMax = 1f;
        // Clouds (F10). Scale is a multiplier; altitude is an additive +y offset in world units.
        public const float CloudScaleMin = 0.1f, CloudScaleMax = 4f;
        public const float CloudAltMin = -60f,  CloudAltMax = 60f;
        // Mountains (F10). Distance/peak are multipliers; warmth is an additive tint offset; brightness a multiply.
        public const float MtnDistanceMin = 0.1f, MtnDistanceMax = 3f;
        public const float MtnPeakScaleMin = 0.1f, MtnPeakScaleMax = 3f;
        public const float MtnWarmthMin = -0.5f, MtnWarmthMax = 0.5f;
        public const float MtnBrightnessMin = 0.2f, MtnBrightnessMax = 2f;
        // Sun (F10). Elevation degrees above the horizon (2..80, matching the F10 clamp); size is the shader's
        // disk-edge dot threshold (0.95..0.9999; HIGHER = smaller disk).
        public const float SunElevationMin = 2f, SunElevationMax = 80f;
        public const float SunSizeMin = 0.95f, SunSizeMax = 0.9999f;

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
            => Build(orbit, wasd, thirst, chopCharacter, chopTree, null);

        /// <summary>
        /// Build the standard registry AND thirst AND chop tweakables AND the STONE tweakable (ticket
        /// 86caa4c96): `stone respawn time` (a RANGE row driving the shared <paramref name="stoneRespawner"/>'s
        /// respawn min/max — AC3). A null respawner SKIPS the stone setting (the catalog never null-refs), so a
        /// stone-less rig / bare test is unaffected.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree,
            FarHorizon.StoneRespawner stoneRespawner)
            => Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, null);

        /// <summary>
        /// Build the standard registry AND thirst AND chop AND stone AND the tree-chop REWORK tweakables (ticket
        /// 86caf9u5t): `tree-chop wood yield` + `log-pile despawn` (bound to the shared
        /// <paramref name="logPileSpawner"/>) + `chops-to-fell` (bound to <paramref name="chopTree"/>). A null
        /// spawner SKIPS the yield/despawn rows; a null tree skips chops-to-fell (the catalog never null-refs), so
        /// a chop-less rig / bare test is unaffected.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree,
            FarHorizon.StoneRespawner stoneRespawner, FarHorizon.LogPileSpawner logPileSpawner)
            => Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, null);

        /// <summary>
        /// Build the standard registry AND thirst AND chop AND stone AND tree-chop rework AND the HELD-WEAPON
        /// PLACEMENT tweakables (ticket 86caffwuz): seven rows (pos X/Y/Z, rot pitch/yaw/roll, scale) bound to the
        /// CURRENTLY-held weapon via <paramref name="held"/>. A null seam SKIPS the held-weapon rows (the catalog
        /// never null-refs), so existing callers / bare test rigs are unaffected.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree,
            FarHorizon.StoneRespawner stoneRespawner, FarHorizon.LogPileSpawner logPileSpawner,
            FarHorizon.HeldWeaponPlacement held)
            => Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, held, null);

        /// <summary>
        /// Build the standard registry AND every prior need/feature AND the HUNGER tweakables (ticket 86cabd75y —
        /// the 86caamkp8 AC4 settings-registration follow-up): `Hunger decay rate` (drives HungerNeed.decayPerSecond)
        /// + `Berry restore amount` (drives HungerNeed.berryRestoreAmount), LIVE-bound via <paramref name="hunger"/>.
        /// A null hunger SKIPS the hunger rows (the catalog never null-refs), so existing callers / bare test rigs are
        /// unaffected. Mirrors the PopulateThirst de-collision precedent (each need adds its OWN Populate method).
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree,
            FarHorizon.StoneRespawner stoneRespawner, FarHorizon.LogPileSpawner logPileSpawner,
            FarHorizon.HeldWeaponPlacement held, FarHorizon.HungerNeed hunger)
            => Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, held, hunger, null);

        /// <summary>
        /// Build the standard registry AND every prior need/feature AND the BERRY-REGROWTH tweakable (ticket
        /// 86cabn67w — the 86caa5zz3 AC4 settings-registration follow-up): a `Berry regrowth time` RANGE row
        /// driving <c>regrowMinSeconds</c> / <c>regrowMaxSeconds</c> across EVERY <paramref name="berryBushes"/>
        /// (the wired bush AND the ~32 scatter bushes — unlike trees, each BerryBush holds its OWN regrow window,
        /// so the row FANS OUT to all of them). A null/empty list SKIPS the berry row (the catalog never
        /// null-refs), so existing callers / bare test rigs are unaffected. Mirrors the PopulateStones/PopulateChop
        /// RANGE de-collision precedent (each feature adds its OWN Populate method).
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree,
            FarHorizon.StoneRespawner stoneRespawner, FarHorizon.LogPileSpawner logPileSpawner,
            FarHorizon.HeldWeaponPlacement held, FarHorizon.HungerNeed hunger,
            IReadOnlyList<FarHorizon.BerryBush> berryBushes)
            => Build(orbit, wasd, thirst, chopCharacter, chopTree, stoneRespawner, logPileSpawner, held, hunger,
                berryBushes, null);

        /// <summary>
        /// Build the standard registry AND every prior need/feature AND the INVENTORY tweakables (ticket 86cabfa4e —
        /// the #90 / 86caa4bya AC1/AC2/AC7 settings-registration follow-up): `inventory slots` (AC1) + `belt slots`
        /// (AC2) + `inventory stack size` (AC7), bound through the live <paramref name="inventory"/> façade. A null
        /// inventory SKIPS the inventory rows (the catalog never null-refs), so existing callers / bare test rigs are
        /// unaffected. Mirrors the PopulateStones/PopulateHeldWeapon de-collision precedent (each feature adds its OWN
        /// Populate method). DEV-CONSOLE only — not player-facing.
        /// </summary>
        public static SettingsRegistry Build(OrbitCamera orbit, WasdMovement wasd, FarHorizon.ThirstNeed thirst,
            FarHorizon.CastawayCharacter chopCharacter, FarHorizon.ChopTree chopTree,
            FarHorizon.StoneRespawner stoneRespawner, FarHorizon.LogPileSpawner logPileSpawner,
            FarHorizon.HeldWeaponPlacement held, FarHorizon.HungerNeed hunger,
            IReadOnlyList<FarHorizon.BerryBush> berryBushes, FarHorizon.Inventory inventory)
        {
            var reg = new SettingsRegistry();
            Populate(reg, orbit, wasd);
            PopulateThirst(reg, thirst);
            PopulateChop(reg, chopCharacter, chopTree, logPileSpawner);
            PopulateStones(reg, stoneRespawner);
            PopulateHeldWeapon(reg, held);
            PopulateHunger(reg, hunger);
            PopulateBerry(reg, berryBushes);
            PopulateInventory(reg, inventory);
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

                // --- AIR-CONTROL ACCEL (live, 86caambxh) — WasdMovement.airControlAccel is how strongly A/D
                //     steers the player WHILE AIRBORNE (u/s², the capped-accel airborne branch). The Sponsor
                //     soaked #71's 8 u/s² as "still slightly too speedy"; the shipped default is lowered to 5,
                //     and this LIVE slider lets him fine-tune the mid-air A/D nudge in the soak (a direct-tweak
                //     handle for a fiddly feel dial). GROUNDED movement is untouched (it commands full speed). ---
                reg.AddFloat(AirControlAccelId, "Air-control accel",
                    () => wasd.airControlAccel, v => wasd.airControlAccel = v,
                    AirControlAccelMin, AirControlAccelMax, unit: "u/s²");
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
        /// Register the HUNGER tweakables (ticket 86cabd75y — the 86caamkp8 AC4 settings-registration follow-up)
        /// into the registry, LIVE-bound to the given <paramref name="hunger"/> need: `Hunger decay rate` (drives
        /// HungerNeed.decayPerSecond, the single active-decay field on the SurvivalNeed base) + `Berry restore
        /// amount` (drives HungerNeed.berryRestoreAmount, the per-berry satisfaction). Idempotent w.r.t. a null
        /// target — a null hunger registers NOTHING (the settings panel for a hunger-less rig simply lacks these
        /// rows). The settings panel host (86caa4bqp / PR #83) is MERGED, so these are LIVE rows, not greyed
        /// extension hooks. The labels are the AC-mandated names ("Hunger decay rate" / "Berry restore amount") —
        /// the same gameplay-wave/needs naming convention thirst uses. Binds HungerNeed DIRECTLY (the ticket
        /// default; the WarmthNeed→base refactor 86cabgvgw is NOT a hard-dep of this ticket).
        /// </summary>
        public static void PopulateHunger(SettingsRegistry reg, FarHorizon.HungerNeed hunger)
        {
            if (reg == null || hunger == null) return;

            // HUNGER DECAY RATE (live) — drives the ACTIVE HungerNeed.decayPerSecond (the single field the decay
            // path reads, on the SurvivalNeed base). Tightening it makes hunger nag sooner; loosening it, gentler
            // (the difficulty tiers also write this field, but the slider is a direct soak-tuning knob over the
            // active rate). Hunger is the SLOWER background pressure (food is found, not constantly lost).
            reg.AddFloat(HungerDecayId, "Hunger decay rate",
                () => hunger.decayPerSecond, v => hunger.decayPerSecond = v,
                HungerDecayMin, HungerDecayMax, unit: "/s");

            // BERRY RESTORE AMOUNT (live) — drives HungerNeed.berryRestoreAmount, the per-berry restore. Bigger =
            // a berry is a fuller meal; smaller = more nibbles (the vision's "small satisfaction to his hunger").
            reg.AddFloat(BerryRestoreId, "Berry restore amount",
                () => hunger.berryRestoreAmount, v => hunger.berryRestoreAmount = v,
                BerryRestoreMin, BerryRestoreMax, unit: "");
        }

        /// <summary>
        /// Register the BERRY-REGROWTH tweakable (ticket 86cabn67w — the 86caa5zz3 AC4 settings-registration
        /// follow-up) into the registry: a `Berry regrowth time` RANGE row driving
        /// <see cref="FarHorizon.BerryBush.regrowMinSeconds"/> / <see cref="FarHorizon.BerryBush.regrowMaxSeconds"/>
        /// (RANDOM regrowth within [min,max], like tree-regrowth / stone-respawn). The label is the AC-mandated
        /// name ("Berry regrowth time").
        ///
        /// FAN-OUT (the one constraint that distinguishes this from the tree precedent): trees share ONE
        /// <see cref="FarHorizon.ChopTree"/> controller that passes its regrow window to each tree at chop time,
        /// so the tree row binds to that single component. Berries are different — EACH <see cref="FarHorizon.BerryBush"/>
        /// instance holds its OWN <c>regrowMin/MaxSeconds</c> and rolls its own regrow in
        /// <c>ScheduleRegrow</c> reading its own fields (there is no shared berry manager). So this row must
        /// FAN OUT: the getter READS the first non-null bush (the representative — all bushes share the same
        /// dialed window once set); the setter WRITES the value to EVERY bush in <paramref name="berryBushes"/>
        /// so the wired bush AND the ~32 scatter bushes retune together (a setting that tuned only one of ~32
        /// bushes is a broken instrument). The setter runs only on a slider drag / ApplyAll / LoadAll — never
        /// per-frame — so iterating the (small, scene-resolved-once) list is fine (unity6-mastery §6: no
        /// per-FRAME Find; this is a per-tweak fan-out over a cached list, mirroring the held-weapon seam).
        ///
        /// A null/empty list registers NOTHING (the settings panel for a bush-less rig simply lacks the row), so
        /// existing callers / bare test rigs never null-ref and never add a dead knob. The setter skips null
        /// elements (a destroyed/regenerated bush is tolerated). The settings panel host (86caa4bqp / PR #83) is
        /// MERGED, so this is a LIVE row, not a greyed extension hook.
        /// </summary>
        public static void PopulateBerry(SettingsRegistry reg, IReadOnlyList<FarHorizon.BerryBush> berryBushes)
        {
            if (reg == null || berryBushes == null) return;

            // The representative bush: the first non-null in the list. Its regrowMin/Max are the getter source.
            // (Once the slider is dialed, the setter writes EVERY bush to the same window, so any bush reads back
            // the dialed value — the representative is just a stable read anchor.)
            FarHorizon.BerryBush rep = null;
            for (int i = 0; i < berryBushes.Count; i++)
            {
                if (berryBushes[i] != null) { rep = berryBushes[i]; break; }
            }
            if (rep == null) return; // a list of only-null entries → no live target → no dead knob.

            // BERRY REGROWTH TIME (live) — a RANGE row over the regrow window [min,max] (random, not uniform).
            // GETTER reads the representative bush. SETTER fans the value out to EVERY bush so one slider retunes
            // the whole forage (the wired bush + all scatter bushes). Tightening it makes berries return sooner;
            // widening, more variance. Band is generous (instant..30 min) — the Sponsor soaks fast OR a slower
            // cadence. The min<=max ordering is enforced inside BerryBush.ScheduleRegrow, so the slider can't
            // invert the window.
            reg.AddRange(BerryRegrowthId, "Berry regrowth time",
                () => rep.regrowMinSeconds,
                v => { for (int i = 0; i < berryBushes.Count; i++) if (berryBushes[i] != null) berryBushes[i].regrowMinSeconds = v; },
                () => rep.regrowMaxSeconds,
                v => { for (int i = 0; i < berryBushes.Count; i++) if (berryBushes[i] != null) berryBushes[i].regrowMaxSeconds = v; },
                BerryRegrowthLower, BerryRegrowthUpper, unit: "s");
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
            FarHorizon.ChopTree chopTree, FarHorizon.LogPileSpawner logPileSpawner = null)
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

                // CHOPS-TO-FELL (REWORK 86caf9u5t AC4) — an INT STEPPER driving ChopTree.chopsToFell (chops needed
                // to fell a tree; default 3, range 1–10). LIVE: the next tree's fell count uses the dialed value.
                // Bound to the tree (not the spawner) — it is shared across every tree, like the regrow window.
                reg.AddInt(ChopsToFellId, "Chops to fell",
                    () => chopTree.chopsToFell,
                    v => chopTree.chopsToFell = Mathf.Clamp(v,
                        FarHorizon.ChopTree.ChopsToFellMin, FarHorizon.ChopTree.ChopsToFellMax),
                    FarHorizon.ChopTree.ChopsToFellMin, FarHorizon.ChopTree.ChopsToFellMax, unit: "");

                // FALLEN-TREE FADE-OUT (86caff4ad — the #165-soak NIT) — a FLOAT SLIDER driving
                // ChopTree.fadeOutDelaySeconds (seconds a felled tree rests before it fades out + disappears;
                // default 2s, range 0–30s). LIVE: the next tree that fells fades after the dialed delay (no
                // restart). Bound to the tree (shared across every tree, like the regrow window + chops-to-fell).
                // The setter clamps to the [Min,Max] band so a dial can't push a negative / runaway delay.
                reg.AddFloat(TreeFadeOutId, "Fallen-tree fade-out",
                    () => chopTree.fadeOutDelaySeconds,
                    v => chopTree.fadeOutDelaySeconds = Mathf.Clamp(v,
                        FarHorizon.ChopTree.FadeOutDelayMin, FarHorizon.ChopTree.FadeOutDelayMax),
                    FarHorizon.ChopTree.FadeOutDelayMin, FarHorizon.ChopTree.FadeOutDelayMax, unit: "s");
            }

            // TREE-CHOP WOOD YIELD + LOG-PILE DESPAWN (REWORK 86caf9u5t AC3/AC5) — bound to the shared
            // LogPileSpawner: `tree-chop wood yield` (int, logs per FALLEN tree; default 10, 1–50) + `log-pile
            // despawn` (float seconds, the uncollected-pile lifetime; default 180s). A null spawner SKIPS both
            // (no dead knob on a spawner-less rig). The setter clamps to the spawner's WoodYieldMin/Max band.
            if (logPileSpawner != null)
            {
                reg.AddInt(TreeWoodYieldId, "Tree-chop wood yield",
                    () => logPileSpawner.WoodYield,
                    v => logPileSpawner.WoodYield = Mathf.Clamp(v,
                        FarHorizon.LogPileSpawner.WoodYieldMin, FarHorizon.LogPileSpawner.WoodYieldMax),
                    FarHorizon.LogPileSpawner.WoodYieldMin, FarHorizon.LogPileSpawner.WoodYieldMax, unit: "");

                reg.AddFloat(LogPileDespawnId, "Log-pile despawn",
                    () => logPileSpawner.DespawnSeconds, v => logPileSpawner.DespawnSeconds = v,
                    LogPileDespawnLower, LogPileDespawnUpper, unit: "s");
            }
        }

        /// <summary>
        /// Register the STONE tweakable (ticket 86caa4c96 AC3) into the registry: a `stone respawn time` RANGE
        /// row driving the shared <paramref name="stoneRespawner"/>'s <see cref="StoneRespawner.RespawnMinSeconds"/>
        /// / <see cref="StoneRespawner.RespawnMaxSeconds"/> (a RANDOM respawn within [min,max] — every StoneProp
        /// reads this one shared window, so the slider retunes EVERY stone). Registered via THIS method (NOT by
        /// appending to <see cref="Populate"/> — the PopulateThirst/PopulateChop de-collision precedent, AC3a/V3).
        /// A null respawner registers NOTHING (the settings panel for a stone-less rig simply lacks the row), so
        /// existing callers / bare test rigs never null-ref. The setter clamps min &lt;= max are enforced where
        /// each StoneProp rolls the delay (ScheduleRespawn), so the slider can't invert the window.
        /// </summary>
        public static void PopulateStones(SettingsRegistry reg, FarHorizon.StoneRespawner stoneRespawner)
        {
            if (reg == null || stoneRespawner == null) return;

            // STONE RESPAWN TIME (live) — a RANGE row driving the shared respawn window [min,max] (random, not
            // uniform — AC3). Tightening it makes a looted stone respawn sooner; widening, more variance. The
            // band is generous (instant..30 min) so the Sponsor can soak fast OR set a realistic scarcity.
            reg.AddRange(StoneRespawnId, "Stone respawn time",
                () => stoneRespawner.RespawnMinSeconds, v => stoneRespawner.RespawnMinSeconds = v,
                () => stoneRespawner.RespawnMaxSeconds, v => stoneRespawner.RespawnMaxSeconds = v,
                StoneRespawnLower, StoneRespawnUpper, unit: "s");
        }

        /// <summary>
        /// Register the HELD-WEAPON in-hand PLACEMENT tweakables (ticket 86caffwuz) into the registry, bound to
        /// the <paramref name="held"/> binding seam (the single surface over the axe rig + the per-weapon arrays).
        /// SEVEN slider rows for the CURRENTLY-held weapon's seat — position X/Y/Z, rotation pitch/yaw/roll, and a
        /// uniform scale — so the Sponsor dials each weapon in-hand with the MOUSE (Danish-keyboard-safe), then we
        /// re-bake the dialed numbers into the committed source constants (the [[verify-soak-builds-or-bake-and-judge]]
        /// workflow). A null seam registers NOTHING (a held-weapon-less rig / bare test simply lacks these rows),
        /// so existing callers never null-ref.
        ///
        /// The rows bind to "the weapon in the hand right now" — the Sponsor cycles the held weapon with [B]
        /// (HeldWeaponCycleDebug) and the same seven rows then drive whichever weapon is shown (the readout label
        /// names it). Because the getter reads the live seat, the rows always reflect the current weapon's values.
        /// </summary>
        public static void PopulateHeldWeapon(SettingsRegistry reg, FarHorizon.HeldWeaponPlacement held)
        {
            if (reg == null || held == null) return;

            // POSITION offset (hand-local for the axe; mesh-holder-local for knife/sword/spear). cm-scale; the
            // ±0.6u band covers a full re-seat in the grip. Each component is its own slider so the Sponsor dials
            // one axis at a time with the mouse.
            reg.AddFloat(HeldPosXId, "Held: pos X",
                () => held.OffsetX, v => held.OffsetX = v, HeldPosMin, HeldPosMax, unit: "u");
            reg.AddFloat(HeldPosYId, "Held: pos Y",
                () => held.OffsetY, v => held.OffsetY = v, HeldPosMin, HeldPosMax, unit: "u");
            reg.AddFloat(HeldPosZId, "Held: pos Z",
                () => held.OffsetZ, v => held.OffsetZ = v, HeldPosMin, HeldPosMax, unit: "u");

            // ROTATION euler (hand-relative for the axe; mesh-holder-local for the rest). Full ±360° band — the
            // axe relEuler accumulates raw (e.g. (-186,-168,-84)), so the band must span the wrap, not clamp ±180.
            reg.AddFloat(HeldPitchId, "Held: pitch",
                () => held.Pitch, v => held.Pitch = v, HeldRotMin, HeldRotMax, unit: "°");
            reg.AddFloat(HeldYawId, "Held: yaw",
                () => held.Yaw, v => held.Yaw = v, HeldRotMin, HeldRotMax, unit: "°");
            reg.AddFloat(HeldRollId, "Held: roll",
                () => held.Roll, v => held.Roll = v, HeldRotMin, HeldRotMax, unit: "°");

            // SCALE — uniform. For the axe this is a multiplier of the LOCKED mesh-holder baseline (1.0 = byte-
            // identical locked seat, so leaving it untouched never regresses the praised grip — bar #6); for
            // knife/sword/spear it is their per-weapon held scale.
            reg.AddFloat(HeldScaleId, "Held: scale",
                () => held.Scale, v => held.Scale = v, HeldScaleMin, HeldScaleMax, unit: "x");
        }

        /// <summary>
        /// Register the INVENTORY tweakables (ticket 86cabfa4e — the #90 / 86caa4bya AC1/AC2/AC7 settings-
        /// registration follow-up) into the registry, bound through the live <paramref name="inventory"/> façade:
        /// <list type="bullet">
        /// <item><b>Inventory slots</b> (AC1) — an INT stepper driving <see cref="FarHorizon.Inventory.InventorySlotCount"/>.</item>
        /// <item><b>Belt slots</b> (AC2) — an INT stepper driving <see cref="FarHorizon.Inventory.BeltSlotCount"/>.</item>
        /// <item><b>Inventory stack size</b> (AC7) — an INT stepper driving <see cref="FarHorizon.ItemDef.ResourceStackSize"/>
        /// (the shared resource/consumable per-slot cap; the axe-Tool cap stays 1 — derived from Kind).</item>
        /// </list>
        ///
        /// CONSTRUCTION-TIME, NOT a live field (the one trait distinguishing these from thirst/chop): the slot
        /// counts are the AUTHORING counts the <see cref="FarHorizon.InventoryModel"/> is built from in its ctor
        /// (the grid + belt arrays are <c>readonly</c>, sized once), and stack size feeds <c>ItemDef.MaxStack</c>
        /// (read per add/merge). A bare "set the count" with no rebuild would be a DEAD knob (the model already
        /// read it). So the slot-count SETTERS rebuild the model via <see cref="FarHorizon.Inventory.SetInventorySlotCount"/>
        /// / <see cref="FarHorizon.Inventory.SetBeltSlotCount"/> (which re-construct an empty model — a DEV-console
        /// re-size, NOT a player-facing live resize that preserves contents). Stack size needs no rebuild — the
        /// NEXT add/merge reads the new <c>ItemDef.ResourceStackSize</c>. NO gameplay-loop behavior changes: the
        /// add/stack/move/select rules are byte-identical; only the construction inputs become console-tunable.
        ///
        /// A null inventory registers NOTHING (the settings panel for an inventory-less rig simply lacks the rows),
        /// so existing callers / bare test rigs never null-ref and never add a dead knob. The setters clamp to the
        /// [Min,Max] band so a dial can't push a zero/runaway count. The settings panel host (86caa4bqp / PR #83)
        /// is MERGED, so these are LIVE rows, not greyed extension hooks.
        /// </summary>
        public static void PopulateInventory(SettingsRegistry reg, FarHorizon.Inventory inventory)
        {
            if (reg == null || inventory == null) return;

            // INVENTORY SLOTS (AC1) — INT stepper over the grid count. The SETTER rebuilds the model (dev-console
            // re-size). Getter reads the current authoring count. Clamp to the band so the grid can't go to 0.
            reg.AddInt(InventorySlotsId, "Inventory slots",
                () => inventory.InventorySlotCount,
                v => inventory.SetInventorySlotCount(Mathf.Clamp(v, InventorySlotsMin, InventorySlotsMax)),
                InventorySlotsMin, InventorySlotsMax, unit: "");

            // BELT SLOTS (AC2) — INT stepper over the hotbar count. The SETTER rebuilds the model (dev-console
            // re-size). Number-key selection 1–N follows the count (InventoryModel.SelectBelt clamps to length).
            reg.AddInt(BeltSlotsId, "Belt slots",
                () => inventory.BeltSlotCount,
                v => inventory.SetBeltSlotCount(Mathf.Clamp(v, BeltSlotsMin, BeltSlotsMax)),
                BeltSlotsMin, BeltSlotsMax, unit: "");

            // INVENTORY STACK SIZE (AC7) — INT stepper over the shared resource/consumable per-slot cap
            // (ItemDef.ResourceStackSize). NO rebuild needed: the next AddItem/TryMove merge reads the new cap.
            // Tools (axe) still cap at 1 (ItemDef.MaxStack derives that from Kind, untouched). The static default
            // is seeded from ItemDef.DefaultResourceStack (=20), so an untouched build is byte-identical.
            reg.AddInt(StackSizeId, "Inventory stack size",
                () => FarHorizon.ItemDef.ResourceStackSize,
                v => FarHorizon.ItemDef.ResourceStackSize = Mathf.Clamp(v, StackSizeMin, StackSizeMax),
                StackSizeMin, StackSizeMax, unit: "");
        }

        /// <summary>
        /// F-KEY MIGRATION — register the F7 CAMERA-FOLLOW gains (ticket 86caber95 AC3) as four float rows bound
        /// LIVE to the <paramref name="orbit"/> camera: horizontal <c>followLerp</c>, <c>verticalFollowLerp</c>,
        /// <c>airborneFollowLerp</c> (1/s rates) + <c>followLeadTime</c> (seconds; 0 = AUTO). These are the exact
        /// fields the standalone F7 <see cref="FarHorizon.CameraFollowNudgeTool"/> dials — the console is a second
        /// front-end onto the same live fields (AC5: legacy F7 stays live in parallel until soak-confirmed). A null
        /// orbit registers NOTHING (the catalog never null-refs), so a camera-less rig / bare test is unaffected.
        /// Lead-time clamps to [0, maxLeadTime] to match the F7 tool + OrbitCamera's own clamp.
        /// </summary>
        public static void PopulateCameraFollow(SettingsRegistry reg, OrbitCamera orbit)
        {
            if (reg == null || orbit == null) return;

            reg.AddFloat(CamFollowLerpId, "Cam follow lerp",
                () => orbit.followLerp, v => orbit.followLerp = Mathf.Max(0f, v),
                CamFollowLerpMin, CamFollowLerpMax, unit: "/s");
            reg.AddFloat(CamVertFollowLerpId, "Cam vertical follow",
                () => orbit.verticalFollowLerp, v => orbit.verticalFollowLerp = Mathf.Max(0f, v),
                CamVertFollowMin, CamVertFollowMax, unit: "/s");
            reg.AddFloat(CamAirborneLerpId, "Cam airborne follow",
                () => orbit.airborneFollowLerp, v => orbit.airborneFollowLerp = Mathf.Max(0f, v),
                CamAirborneMin, CamAirborneMax, unit: "/s");
            // Lead-time: clamp to the camera's own maxLeadTime (0 = AUTO = 1/followLerp). The band ceiling equals
            // the shipped maxLeadTime default; the setter re-clamps to the LIVE maxLeadTime so a future default
            // change stays honoured.
            reg.AddFloat(CamFollowLeadTimeId, "Cam lead time",
                () => orbit.followLeadTime,
                v => orbit.followLeadTime = Mathf.Clamp(v, 0f, orbit.maxLeadTime),
                CamLeadTimeMin, CamLeadTimeMax, unit: "s");
        }

        /// <summary>
        /// F-KEY MIGRATION — register the F9 GROUND-Y + ARM-POSE dials (ticket 86caber95 AC1) LIVE-bound to the
        /// <paramref name="castaway"/> (ground-Y) + <paramref name="armPose"/> (arm eulers). The VALIDATION entry
        /// is the single-float GROUND-Y (per AC1 "start with the single-float ground-Y"); the arm-pose + run-lower
        /// VECTORS are DECOMPOSED into per-axis float rows (AC4 — no new archetype). Each row drives the SAME live
        /// field the standalone F9 <see cref="FarHorizon.AxeNudgeTool"/> dials (AC5: legacy F9 stays live in
        /// parallel). The arm setters mirror the F9 tool's dial contract: set <c>seedEulersFromDegFields=false</c>
        /// (so a RebuildCached can't clobber the live dial) + call <see cref="FarHorizon.CastawayArmPose.RebuildCached"/>
        /// so the new pose composes immediately. Held-axe offset is ALREADY on the console (PopulateHeldWeapon,
        /// 86caffwuz); the stump-axe (a named scene transform) stays on the legacy F9 tool for now. A null target
        /// SKIPS only its own rows (the catalog never null-refs).
        /// </summary>
        public static void PopulateArmAndGround(SettingsRegistry reg, FarHorizon.CastawayCharacter castaway,
            FarHorizon.CastawayArmPose armPose)
        {
            if (reg == null) return;

            // GROUND-Y OFFSET (the AC1 validation single-float) — a constant world-Y added to the snapped feet +
            // shadow (rest AND walk). Same field the F9 GROUND-Y target dials.
            if (castaway != null)
            {
                reg.AddFloat(GroundYOffsetId, "Ground-Y offset",
                    () => castaway.groundYOffset, v => castaway.groundYOffset = v,
                    GroundYMin, GroundYMax, unit: "u");
            }

            // ARM POSE (per-axis, AC4) — the RIGHT + LEFT arm local-euler offsets (pitch=spread, yaw≈twist,
            // roll=raise) + the RUN arm-lower. Every setter goes through ArmWrite so the dial contract (freeze
            // the deg-field seed + rebuild the cached quats) is applied on EVERY axis write, matching the F9 tool.
            if (armPose != null)
            {
                reg.AddFloat(ArmRightPitchId, "Arm R pitch",
                    () => armPose.rightArmEuler.x, v => ArmWrite(armPose, ref armPose.rightArmEuler, 0, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(ArmRightYawId, "Arm R yaw",
                    () => armPose.rightArmEuler.y, v => ArmWrite(armPose, ref armPose.rightArmEuler, 1, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(ArmRightRollId, "Arm R roll",
                    () => armPose.rightArmEuler.z, v => ArmWrite(armPose, ref armPose.rightArmEuler, 2, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(ArmLeftPitchId, "Arm L pitch",
                    () => armPose.leftArmEuler.x, v => ArmWrite(armPose, ref armPose.leftArmEuler, 0, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(ArmLeftYawId, "Arm L yaw",
                    () => armPose.leftArmEuler.y, v => ArmWrite(armPose, ref armPose.leftArmEuler, 1, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(ArmLeftRollId, "Arm L roll",
                    () => armPose.leftArmEuler.z, v => ArmWrite(armPose, ref armPose.leftArmEuler, 2, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");

                // RUN arm-lower (the F9 RUN target) — INERT at walk/idle (run weight 0), so it's judged while
                // RUNNING. Same runLowerEuler field the F9 RUN target dials.
                reg.AddFloat(RunLowerPitchId, "Run-lower pitch",
                    () => armPose.runLowerEuler.x, v => ArmWrite(armPose, ref armPose.runLowerEuler, 0, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(RunLowerYawId, "Run-lower yaw",
                    () => armPose.runLowerEuler.y, v => ArmWrite(armPose, ref armPose.runLowerEuler, 1, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
                reg.AddFloat(RunLowerRollId, "Run-lower roll",
                    () => armPose.runLowerEuler.z, v => ArmWrite(armPose, ref armPose.runLowerEuler, 2, v),
                    ArmEulerMin, ArmEulerMax, unit: "°");
            }
        }

        /// <summary>Write one axis (0=x,1=y,2=z) of a CastawayArmPose euler field + apply the F9 dial contract
        /// (freeze the deg-field seed so RebuildCached can't clobber the live dial; rebuild the cached quats so
        /// the new pose composes THIS frame). Mirrors AxeNudgeTool's arm-target write.</summary>
        private static void ArmWrite(FarHorizon.CastawayArmPose armPose, ref Vector3 euler, int axis, float v)
        {
            if (axis == 0) euler.x = v; else if (axis == 1) euler.y = v; else euler.z = v;
            armPose.seedEulersFromDegFields = false;
            armPose.RebuildCached();
        }

        /// <summary>
        /// F-KEY MIGRATION — register the F10 WORLD-LOOK dials (ticket 86caber95 AC2) LIVE-bound through the
        /// <paramref name="world"/> seam (<see cref="FarHorizon.WorldLookTunables"/>, which resolves + mutates the
        /// SAME RenderSettings/skybox-material/cloud/vista handles the standalone F10
        /// <see cref="FarHorizon.WorldLookNudgeTool"/> does — AC5: legacy F10 stays live in parallel). Each dial is
        /// a SCALAR float row; the fog + sky-horizon COLOURS are DECOMPOSED per-channel (R/G/B) per AC4 (no new
        /// archetype). Setting a fog or sky-horizon channel keeps the seam-kill (fog colour == horizon stop) via the
        /// seam. A null seam registers NOTHING (the catalog never null-refs), so a world-less rig / bare test is
        /// unaffected.
        /// </summary>
        public static void PopulateWorldLook(SettingsRegistry reg, FarHorizon.WorldLookTunables world)
        {
            if (reg == null || world == null) return;

            // FOG — density + per-channel colour (seam-kill applied in the seam).
            reg.AddFloat(FogDensityId, "Fog density",
                () => world.FogDensity, v => world.FogDensity = v, FogDensityMin, FogDensityMax, unit: "");
            reg.AddFloat(FogColorRId, "Fog colour R",
                () => world.FogColorR, v => world.FogColorR = v, ColorChannelMin, ColorChannelMax, unit: "");
            reg.AddFloat(FogColorGId, "Fog colour G",
                () => world.FogColorG, v => world.FogColorG = v, ColorChannelMin, ColorChannelMax, unit: "");
            reg.AddFloat(FogColorBId, "Fog colour B",
                () => world.FogColorB, v => world.FogColorB = v, ColorChannelMin, ColorChannelMax, unit: "");

            // SKY HORIZON stop — per-channel colour (the seam-driving stop; locks the fog colour to it).
            reg.AddFloat(SkyHorizonRId, "Sky horizon R",
                () => world.SkyHorizonR, v => world.SkyHorizonR = v, ColorChannelMin, ColorChannelMax, unit: "");
            reg.AddFloat(SkyHorizonGId, "Sky horizon G",
                () => world.SkyHorizonG, v => world.SkyHorizonG = v, ColorChannelMin, ColorChannelMax, unit: "");
            reg.AddFloat(SkyHorizonBId, "Sky horizon B",
                () => world.SkyHorizonB, v => world.SkyHorizonB = v, ColorChannelMin, ColorChannelMax, unit: "");

            // CLOUDS — scale + additive altitude offset.
            reg.AddFloat(CloudScaleId, "Cloud scale",
                () => world.CloudScale, v => world.CloudScale = v, CloudScaleMin, CloudScaleMax, unit: "x");
            reg.AddFloat(CloudAltitudeId, "Cloud altitude",
                () => world.CloudAltitude, v => world.CloudAltitude = v, CloudAltMin, CloudAltMax, unit: "u");

            // MOUNTAINS — distance + peak scale + warmth + brightness.
            reg.AddFloat(MtnDistanceId, "Mountain distance",
                () => world.MountainDistanceScale, v => world.MountainDistanceScale = v, MtnDistanceMin, MtnDistanceMax, unit: "x");
            reg.AddFloat(MtnPeakScaleId, "Mountain peak scale",
                () => world.MountainPeakScale, v => world.MountainPeakScale = v, MtnPeakScaleMin, MtnPeakScaleMax, unit: "x");
            reg.AddFloat(MtnWarmthId, "Mountain warmth",
                () => world.MountainWarmth, v => world.MountainWarmth = v, MtnWarmthMin, MtnWarmthMax, unit: "");
            reg.AddFloat(MtnBrightnessId, "Mountain brightness",
                () => world.MountainBrightness, v => world.MountainBrightness = v, MtnBrightnessMin, MtnBrightnessMax, unit: "x");

            // SUN — elevation + size.
            reg.AddFloat(SunElevationId, "Sun elevation",
                () => world.SunElevationDeg, v => world.SunElevationDeg = v, SunElevationMin, SunElevationMax, unit: "°");
            reg.AddFloat(SunSizeId, "Sun size",
                () => world.SunSize, v => world.SunSize = v, SunSizeMin, SunSizeMax, unit: "");
        }
    }
}
