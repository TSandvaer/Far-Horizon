# Dev Tweak Console — spec

Status: spec / dispatch-source. Owner: Priya (PL). Source: Sponsor directive 2026-06-19 (verbatim below).
Built on: #83 settings-panel + registry infra (`86caa4bqp`, branch `devon/86caa4bqp-settings-panel`, `in review`).

## Sponsor directive (verbatim, 2026-06-19)

> "the settings panel [is] not meant to be player-facing (final product) — it's a tool I (sponsor) can use to
> tweak values while testing, long before any release. It could seem player-facing because I want to be able to
> tweak while I soak. It would be nice if all the existing tweak mechanisms F7, F8, F9 etc could be built into
> this settings panel instead. Bear in mind I should be able to have this panel open and tweak while I play —
> typing values or using the nudge keys as well."

## 1. Purpose — this is a DEV TOOL, not a player-facing menu

The "settings panel" from #83 is **not** a shipping options screen. It is the Sponsor's **soak-tuning console**:
a single unified surface where he dials live gameplay/look/feel values while testing, reads the dialed numbers,
and reports them back to bake as new defaults (the give-him-the-knob pattern, cf. the F9 axe-nudge tool). It may
*look* player-facing because he keeps it open during play — but nothing here is meant for the final product. A
real player-facing options menu (audio/graphics/key-rebind) is a SEPARATE future surface; the registry could grow
into one later, but that is explicitly out of scope here.

Naming: refer to it as the **Dev Tweak Console** in code/docs going forward (the #83 ticket calls it "settings
panel"; same surface, clarified intent). No rename of the existing `FarHorizon.Settings` namespace/files is
required — that churn is out of scope; the clarification is about *intent*, not identifiers.

## 2. The two hard requirements that change #83's behavior

### 2a. Open-while-play (NON-PAUSING, non-modal)

The Sponsor must be able to keep the console **open AND tweak WHILE he plays** — walk, run, jump, orbit-cam, all
live, with the panel up. This is a direct change from #83's current behavior:

- **#83 today is MODAL.** On open, `SettingsPanel.SetOpen(true)` calls `UiInputGate.SetPanelOpen(open)`, which
  swallows all world/locomotion input while the panel is up (`WasdMovement`/`OrbitCamera` skip their input when
  `UiInputGate.CaptureWorldInput` is true). That is the OPPOSITE of what the console needs.
- **The console must NOT swallow locomotion/world input.** WASD + run(Shift) + jump(Space) + mouse-orbit keep
  working while the console is open. The panel only captures input for the **focused entry being typed into**
  (so a number key typed into a text field doesn't also move the player), and releases world input otherwise.
- **No time-pause.** The console never touches `Time.timeScale`. The world keeps simulating (needs decay, motion
  continues) so the Sponsor can watch a tweak's effect in real time.
- **Input-focus rule:** world input is swallowed ONLY while a typed-value field has keyboard focus (so typing a
  value isn't also read as movement). Nudge keys (see 2b) are chosen to NOT collide with locomotion keys, so they
  work without stealing focus. When no field is focused, ALL gameplay input passes through untouched.

This is the central design pivot for the FOUNDATION ticket. The existing `UiInputGate` ref-counted modal pattern
stays available for genuinely-modal future panels (inventory Tab), but the Dev Tweak Console opts OUT of it.

### 2b. Each entry tweakable by TYPING a value OR by NUDGE KEYS

Every console entry supports **two** input methods, usable interchangeably while playing:

- **Typed value** — a text field next to the control; type a number, commit (Enter/blur), value applies live.
- **Nudge keys** — the F-key nudge-tool idiom carried into the console: select an entry (or the panel tracks a
  "focused entry"), then nudge it up/down by a step, with **Shift = 5× / Ctrl = 0.2×** step modifiers (the exact
  convention every existing nudge tool uses — `AxeNudgeTool`/`WorldLookNudgeTool`/`CameraFollowNudgeTool`).
- The slider/range/stepper controls from #83 stay too — so an entry is dial-able by drag, type, OR nudge.

## 3. The entry model (extends #83's registry)

#83 shipped a clean extensible registry (`SettingsRegistry` + typed `SettingEntry` subclasses) with **three**
archetypes: `Slider` (float), `Range` (min/max pair), `Stepper` (int). It already does live-bind via
getter/setter delegates, immediate apply (no restart), PlayerPrefs persistence, and reset-to-defaults. The console
**extends** this; it does not replace it.

Gaps the console foundation must add to the registry/panel (NONE exist in #83 today):

| Need | #83 status | Console adds |
| --- | --- | --- |
| **Toggle (bool) archetype** | absent | a `BoolSettingEntry` / `Toggle` archetype for per-need on/off (and any future on/off flag). |
| **Typed-value field** | sliders/steppers only | a numeric text field bound to each entry's value, commit-on-Enter, clamps to the entry's min/max. |
| **Nudge-key input** | absent (panel has no key-driven adjust) | per-entry nudge up/down + Shift/Ctrl step modifiers, the existing nudge-tool convention. |
| **Focused-entry concept** | absent | a way to mark which entry the nudge keys drive (selection/focus), so one entry nudges at a time. |
| **Open-while-play** | modal (swallows input) | non-modal, non-pausing; world input passes through except a focused typed field (§2a). |

Entry-model summary (what an author registers):

- **Slider** (float) — bind get/set, min/max, unit. *(exists)*
- **Range** (min/max) — bind get/set for both ends, lower/upper hard-limits, unit. *(exists)*
- **Stepper** (int) — bind get/set, min/max, step, unit. *(exists)*
- **Toggle** (bool) — bind get/set; renders an on/off switch. *(new — console foundation)*
- Every archetype gets the typed-field + nudge-key affordances generically (new — console foundation), so a
  future setting still registers in "a few lines" and gets type/nudge for free.

Persistence + reset-to-defaults from #83 (PlayerPrefs per `SettingEntry.PrefsKey`) carry to every new entry for
free — subject to the Sponsor open in §6 on whether dialed values SHOULD persist across runs for a dev console.

## 4. F-key migration — consolidate ALL live-tune handles into the console

The Sponsor wants F7/F8/F9/etc. **built into** the console instead of separate panels. Inventory of every existing
F-key live-tune handle (verified from source 2026-06-19):

| Key | Tool | What it tunes | File | Entry mechanism today | Status |
| --- | --- | --- | --- | --- | --- |
| **F7** | `CameraFollowNudgeTool` | camera follow gains: horiz follow-lerp, vertical follow-lerp, lead-time, airborne follow-lerp (`OrbitCamera.followLerp` / `verticalFollowLerp` / `followLeadTime` / `airborneFollowLerp`) | `Assets/Scripts/Runtime/CameraFollowNudgeTool.cs:38` | arrow/PageUp etc. nudge keys + Shift(5×)/Ctrl(0.2×); on-screen IMGUI panel | **on UNMERGED branch** `devon/86caa83wn-axe-in-hand-run-jump` (PR #77) |
| **F8** | `FloatDiagnostic` | feet-vs-ground float READOUT (overlay + ~1Hz log) + surfaces the GAP the F9 ground-Y dial drives; `groundYOffset` is dialed on F9, not F8 | `Assets/Scripts/Runtime/FloatDiagnostic.cs:41` | toggle-only overlay (a measurement instrument, not a value dial) | merged (`main`) |
| **F9** | `AxeNudgeTool` | held-axe world-offset+relEuler, stump-axe local transform, per-arm pose euler (`CastawayArmPose`), `CastawayCharacter.groundYOffset` (ground-Y) — cycled with Tab | `Assets/Scripts/Runtime/AxeNudgeTool.cs:65` | arrow/PageUp/T-G-Y-H-U-J nudge keys + Shift(5×)/Ctrl(0.2×); Tab cycles target; IMGUI panel | merged (`main`) |
| **F10** | `WorldLookNudgeTool` | world-look dials: sky-gradient stops, fog density/color, cloud scale/altitude, mountain dist/scale/tint/brightness — cycled with Tab | `Assets/Scripts/Runtime/WorldLookNudgeTool.cs:38` | arrow/PageUp/bracket nudge keys + Shift(5×)/Ctrl(0.2×); Tab cycles target; IMGUI panel | merged (`main`) |

Notes for migration:
- These are heterogeneous: F7/F9/F10 are **value dials** (nudge a number); F8 is a **measurement readout**
  (no value to dial — it shows the GAP). F8's pattern (a live read-only diagnostic row) is itself a useful
  console archetype to consider, but migrating a readout is lower priority than migrating the dials.
- F9 and F10 are **multi-target** (Tab cycles between several tunables). In the console each underlying tunable
  becomes its OWN entry (e.g. held-axe-offset, stump-axe-pos, arm-pose-L/R, ground-Y are four+ entries), which is
  cleaner than the cycle-with-Tab model.
- F9/F10 nudge several values via **localPosition / localEulerAngles vectors** (XYZ + pitch/yaw/roll). The console
  needs a **Vector3 / euler entry archetype** (or per-axis float entries) to carry these — flag as a likely
  additional archetype the migration tickets will need beyond the bool toggle. (Decision deferred to the migration
  ticket's scoping; called out here so it isn't a surprise.)
- F7 lives on an **unmerged branch (PR #77)** — its migration is gated on #77 merging first (else there's nothing
  on `main` to migrate). F8/F9/F10 are on `main` and migratable as soon as the console foundation lands.
- Migration is sequenced AFTER the console foundation + after foundation proves the nudge-key + typed-value model.
  The first migration target should be the SIMPLEST dial (candidate: F9 ground-Y, a single float) to validate the
  pattern before the multi-axis F9/F10 vectors. **Which F-key migrates first is a Sponsor open (§6).**

## 5. Per-need on/off + decay-rate entries

The survival loop carries three needs (warmth + hunger + thirst — M-U2 expanded 2026-06-17). The console exposes,
per need:

- an **on/off toggle** (disable a need's decay while soaking another system) — the new Bool/Toggle archetype, and
- a **decay-rate slider** (tune how fast the need falls).

Need bindings (decay params live on each need component):

- **Warmth** — `WarmthNeed` (merged on `main`).
- **Hunger** — `86caamkp8` (#93, hunger/berries) — `in review`/gated; the hunger need component + decay param land
  with it.
- **Thirst** — `86caamkv7` (thirst/pond) — batch-2, not yet built; register as an **extension hook**
  (`Available=false`, greyed "(soon)") until it lands, then flip to live (the #83 extension-hook pattern).

Difficulty-preset bake (memory: difficulty-settings-easy-medium-hard): the per-need decay-rate values the Sponsor
dials here are the values that BAKE into the **easy/med/hard** difficulty presets. The game ships three difficulty
tiers; each tier is largely a different set of need-decay rates (+ later enemy/combat tuning). So the per-need
decay entries are the primary authoring surface for the difficulty tiers — the console is where the tiers get
dialed before they're baked. (Whether the console also gets a "load preset / save-as preset" affordance is a
larger design question, OOS for this wave; flagged for a future ticket.)

## 6. Sponsor opens (decisions needed before/with dispatch)

1. **Console toggle key.** #83 uses **Esc**. Esc currently doubles as the modal-open key AND would be the natural
   "close console" key — but if the console is non-modal and stays open during play, does Esc still toggle it, or
   does the Sponsor want a different key (e.g. a backtick `` ` ``/F1 dev key) so Esc stays free for a future
   pause/menu? **Recommendation:** a dedicated dev key (backtick or F1) distinct from Esc, since the console is a
   dev tool that lives alongside play, not a pause menu.
2. **Persist dialed values across runs?** #83 persists every entry to PlayerPrefs (survives relaunch). For a dev
   console that's convenient (tweaks survive a rebuild) BUT risks a stale dialed value silently masking a default
   regression in a later soak. **Recommendation:** keep per-run persistence (matches #83) BUT add a visible
   "values differ from baked defaults" indicator + the existing reset-to-defaults, so a stale dial is never
   invisible. Sponsor to confirm.
3. **Which F-key migrates first?** F8/F9/F10 are on `main`; F7 is gated on PR #77. **Recommendation:** migrate
   **F9 ground-Y** first (single float — validates nudge+type), then the multi-axis F9/F10 vectors, then F7 once
   #77 merges, then F8's readout last. Sponsor to confirm the order / whether the legacy F-keys stay live in
   parallel during migration (recommend: keep them live until the console version is soak-confirmed, then retire).
4. **Vector3/euler archetype scope.** F9/F10 carry XYZ + euler vectors. Confirm whether the migration tickets get
   a dedicated Vector3 entry archetype or decompose each vector into per-axis float entries (recommendation:
   per-axis floats — reuses the existing slider/nudge path, no new archetype). Lower-priority; can be decided at
   the migration ticket's dispatch.

## 7. Ticket decomposition (see ClickUp list 901523878268)

- **(a) Console FOUNDATION** — on #83 infra: non-modal open-while-play (no input-swallow, no pause), the
  type-or-nudge entry model (typed field + nudge keys + Shift/Ctrl steps + focused-entry), and the new Bool/Toggle
  archetype. **Hard-dep: #83 (`86caa4bqp`) merging.**
- **(b) Per-need on/off + decay-rate entries** — warmth/hunger/thirst toggle + decay slider; thirst as an
  extension hook until `86caamkv7`; values bake into easy/med/hard. **Hard-dep: foundation (a); soft-dep: hunger
  `86caamkp8` for the live hunger binding (thirst is a hook regardless).**
- **(c) F-key migration** — migrate F8/F9/F10 (and F7 once PR #77 merges) into the console as entries; retire the
  separate panels once soak-confirmed. **Hard-dep: foundation (a); F7 sub-step gated on PR #77.**

All three are dev-tool / soak-tuning instruments (Sponsor-gated; give-him-the-knob). Author rec: Devon (UI Toolkit
+ live-param binding, owns #83). Reviewer: Drew. Test bar: AC EditMode/PlayMode guard + shipped-build capture
(console opens, a tweak takes effect live while playing) + Self-Test Report + Tess QA + Sponsor soak.
