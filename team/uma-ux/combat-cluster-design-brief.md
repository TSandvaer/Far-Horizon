# Combat-Cluster Design Brief — Far Horizon

**Author:** Uma (UX / Visual / Audio Direction). **Status:** PREP — Sponsor released combat prep
2026-07-19; implementation still waits on the v4-castaway-activation merge (`86catvb6u`). This is a
**doc-only** direction spec: it gives Drew/Devon a concrete, buildable target per surface so they
don't guess, and it captures the FEEL the player should get. The Sponsor's eye/ear is the final judge.

**Sponsor-locked order (do not reorder):** SWINGS first (`86caffwv5`), BOAR second (`86cah7ydt`).
Everything else in this cluster is a lighter follow-on (roster `86cah7ym9`, status effects `86cah7yuh`,
HP HUD `86cah7z2q`, find-in-world `86cah7y5b`) and is deliberately specced lighter here.

**Locked rulings this brief must not contradict** (read before touching any of it):
- **Swings = Sponsor-provided Mixamo CLIPS per weapon class, animator-driven — NOT a procedural
  swing** (`[[chop-swing-mixamo-clip-not-procedural]]`; ticket `86caffwv5`). The axe swing already
  shipped this way (`Assets/Art/Character/Castaway/Melee_Attack.fbx` = Mixamo "Standing Melee Attack
  Downward", chop PR #140). This brief specs the FEEL + clip-selection *criteria* — it does not author
  animation.
- **One click = one strike. Active input, never proximity-auto.** Left-click uses the equipped belt
  item; guarded against UI-clicks / camera-drag (`[[active-input-not-proximity-auto-for-actions]]`).
- **Calm/hopeful north-star governs combat too.** Juice reads "solid and satisfying," never "violent
  or chaotic" (`game-juice.md` §0). Amplitude is the whole tuning variable; when in doubt, smaller.
- **The soak is the interaction gate** — the PlayMode CI job is advisory/unreliable for interaction
  (`[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`). Verify every swing/charge in the
  BUILT exe, not just the editor.
- **3 difficulty tiers** (easy / medium / hard), kid-friendly → adult (`quality-bars.md` #7).

---

## 0. Tonal anchor for the WHOLE combat cluster

> **Combat in Far Horizon is the survival loop baring its teeth for a moment — a grumpy animal, a
> weapon that lands solid — then the calm returns. It is a hopeful journey that occasionally has to
> defend itself, NOT an action game. A child should lean *forward* when the boar snorts, never put the
> controller down. A hit should feel *purposeful and weighty*, never gory.**

Every beat below serves that anchor. If a combat beat reads louder than "the world got briefly tense
and I handled it," it's miscalibrated — turn it down. Concretely, cluster-wide:

- **No blood, no gore, no gibs — at any difficulty tier.** Impact reads through *motion, dust, sound,
  and a brief flinch*, never through red spray. Death is a *topple*, not a kill-cam. This is the
  kid-safe line and it is non-negotiable; "scariness" is tuned by timing and pace (§2.3), not by horror.
- **Restrained juice.** Hit-stop capped at 2–3 frames (`game-juice.md` §1.2); micro Cinemachine
  **Impulse** only, never sustained screen-shake (`game-juice.md` §2); faceted warm particle puffs,
  ≤12 per burst; warm/organic audio, never a shrill stinger.
- **Material-honest everywhere** (`quality-bars.md` #3): a wooden club *thuds*, a stone edge *cracks*, an
  iron blade has a slightly brighter *shk* — but even iron stays warm and restrained, never a
  metal-clang action-game register.

---

## 1. SWINGS — `86caffwv5` (ships FIRST — DEEP)

### 1.0 What this ticket actually is

The Sponsor supplies one Mixamo attack clip per weapon; the team wires each as an animator-driven
attack state on `CastawayAnimator.controller`, triggered by left-click on the equipped belt item,
following the existing chop trigger / single-flight / impact-timing pattern
(`CastawayCharacter.TriggerChop`, `swingImpactDelaySeconds`). The axe is done. **This brief's job is to
tell the Sponsor + devs what each weapon class should FEEL like, so the right clip gets chosen and the
impact timing gets tuned to it.**

### 1.1 Swing feel per weapon class

Four motion signatures. Each is a *class* — the material tier (wood/stone/iron) does not change the
motion, only the weight/sound flavor (§3.1). `swingImpactDelaySeconds` defaults below are the
tunable the Sponsor dials in soak (baseline ~0.4s from chop; per-class starting points given).

| Class | Motion signature | Wind-up → impact → recovery | `swingImpactDelaySeconds` start | Reads as |
|---|---|---|---|---|
| **Axe / Pickaxe** (heavy overhead arc) | Vertical overhead chop, whole-body commit, heavy follow-through past the low point | Slow deliberate wind-up (arm up + back), impact LATE at the bottom of the arc, long settle | **~0.40s** (the shipped chop value) | "A heavy tool landed with my whole weight behind it." The slowest, weightiest, most committed swing. |
| **Knife / Dagger** (quick jab) | Short forward stab / quick slash, elbow-driven, minimal body rotation | Almost no wind-up, impact EARLY, snappy recovery — the fastest cadence in the set | **~0.18s** | "Quick and light — I can throw three of these before the axe lands one." Low commitment, low reach. |
| **Sword** (slash) | Wide diagonal or horizontal sweep, hips leading, arcing follow-through | Moderate wind-up (blade cocked to one side), impact MID-sweep, sweeping recovery | **~0.28s** | "A clean sweeping cut" — covers a wider arc than the knife, more reach, more commitment than the jab. |
| **Spear** (thrust) | Forward linear lunge, both hands, body drives behind the point; longest reach in the set | Moderate wind-up (draw back), impact at FULL EXTENSION, retract recovery | **~0.34s** | "Reach and pierce." The load-bearing weapon for the boar matchup (§2.4) — its reach lands first. |

**Mixamo clip-selection criteria per class (hand this list to the Sponsor when he sources clips):**
1. **Humanoid, imports clean.** Confirm the clip's avatar imports correctly against the castaway rig
   before wiring (Generic-vs-Humanoid trap — per the chop-clip import experience; ticket constraint).
2. **One-shot, returns to idle.** Non-looping; a clear start-and-return so it wires like `Attack`/`Jump`
   (base-layer overlay state, `AnyState→Attack` on trigger; `procedural-animation-verbs.md`
   "full-body clip verbs → base-layer Animator OVERLAY states").
3. **A single, unambiguous impact frame.** The motion must have one obvious "this is where it connects"
   moment so `swingImpactDelaySeconds` can be tuned to it. Reject clips with a mushy or multi-hit peak.
4. **Matches the class motion signature above** (overhead / jab / sweep / thrust). A "slash" clip on the
   spear breaks the reach read; a "thrust" on the axe breaks the weight read.
5. **Reads at orbit-cam distance.** Big, legible arm motion — subtle wrist-only clips vanish at the
   rear-orbit gameplay camera. Bigger silhouette change = better read.

**Design economy call (recommend to Sponsor):** **Axe and Pickaxe share ONE overhead clip** — they are
the same heavy-overhead-arc motion, and the shipped `Melee_Attack.fbx` already IS that motion. So only
**three new clips** are actually needed (knife-jab, sword-slash, spear-thrust), not four. This cuts the
Sponsor's sourcing burden by one and keeps the two heavy tools feeling like siblings.

### 1.2 Impact feedback — game-juice patterns inside the calm low-poly style

On the impact frame (fired from the impact-timing event, same seam as chop), layer these — **all capped
to the calm-tone amplitudes in `game-juice.md`**, scaled by class weight:

- **Hit-stop (T2):** `Time.timeScale = 0` for a capped micro-freeze — **axe/pickaxe 3 frames**,
  **sword/spear 2 frames**, **knife 1–2 frames**. Restore to 1. Camera + UI run on
  `unscaledDeltaTime`. HARD CAP 3 — 4+ frames reads as violence, wrong for the tone.
- **Faceted impact puff (T3), NOT gore:** a pooled, chunky, warm-palette particle burst at the contact
  point — **≤12 particles**, pooled via `ObjectPool<T>` + `OnParticleSystemStopped`. On wood = the
  existing wood-chips read; on the **boar = a small earthy dust/impact puff** (dust-brown, sub-1.0),
  never red. Separate `Unlit/Particle` material — NOT the world palette material, NOT an MPB.
- **Micro Cinemachine Impulse (T4), never Noise:** ~0.05u (knife) → ~0.10u (axe), single-frame decay.
  No sustained shake (`game-juice.md` §2 — motion-sickness + tone).
- **Material-honest audio (T6):** 4–6 impact clips per material with ±10% `Random.Range` pitch via
  `PlayOneShot`. Axe-on-flesh = a dull heavy *thud*; spear = a *thunk*; sword = a *whoosh + cut*; knife
  = a light *shk*. On a tree it stays woody. Iron tier gets a *slightly* brighter honed edge, still
  warm — never a metallic action-clang. One clip = "broken record" fatigue on the highest-frequency
  verb; 4–6 is the floor.
- **Hit-flash on the struck enemy (restrained):** a brief sub-1.0 warm-white tint pulse (~0.08s,
  eased out) so the hit *reads* on the boar's chunky body. **PRIMITIVE DISCIPLINE:** drive it via a
  `_HitFlash` float inside the shader's `CBUFFER_START(UnityPerMaterial)` on a **per-enemy material
  instance** — NOT a `MaterialPropertyBlock` (MPB disqualifies the renderer from the GPU Resident
  Drawer instanced path, `unity6-mastery.md` §2 / `game-juice.md` §2) and NOT a full-screen
  post-process Volume pulse (needs a Render-Graph pass; ruled out `game-juice.md` §2). Every channel
  sub-1.0 (HDR-clamp, `style-guide-v2.md` §5) so the flash doesn't bloom-blow-out.

**Hard don'ts (from `game-juice.md` §2, combat-relevant):** no sustained/high-amplitude screen shake;
no hit-stop > 3 frames; no chromatic-aberration / lens-distortion damage pulse; no squash/stretch on
the rigged character body; no per-action juice on ambient traversal (juice fires only on the discrete
strike/impact moment).

### 1.3 Concrete tunables devs will touch

- `swingImpactDelaySeconds` (per weapon class — §1.1 table; the existing chop tunable, extended per-class).
- Attack Animator state + trigger + swing-speed multiplier (clip cadence × tool-use-speed, per ticket).
- Hit-stop frame count (per class, 1–3 cap).
- Cinemachine Impulse amplitude (per class, 0.05–0.10u).
- Impact-puff burst count (≤12) + material choice (dust vs wood-chip).
- Audio clip bank size (4–6) + pitch-jitter range (±10%).
- `_HitFlash` intensity + decay (material-instance property, sub-1.0).
- Per-tier damage numbers (easy/medium/hard, `quality-bars.md` #7) — data, not motion.

### 1.4 Predict-Before-Soak + Sponsor-input items (swings)

- **Predict-before-soak template (per weapon):** "Left-click plays the [weapon] [motion] once and
  returns to idle; impact lands at [delay]s when the swing visually connects; hit-stop + puff + sound
  fire on that frame; a second click before recovery is ignored (single-flight)."
- **Sponsor-input:** per-class `swingImpactDelaySeconds` feel; hit-stop frame counts (does the axe want
  3 or does even 3 feel too "stunned"?); Impulse amplitude; whether the knife wants a hit-stop at all or
  should feel frictionless; iron-tier audio brightness (how far toward "shk" before it reads metallic).

---

## 2. WILD BOAR — `86cah7ydt` (ships SECOND — DEEP)

**Depends on the combat POC `86cah7xxp`** (shared enemy-HP surface, damage-type↔resistance hook, bleed
framework). The boar's weakness is a TAG on that shared model, not new code. Mesh via Hyper3D→Mixamo or
Blender per the asset-routing index. Charge AI mirrors the snake-enemy pattern.

### 2.0 Tonal anchor

> **A wild boar that belongs to the toy-band world — chunky, bristly, and grumpy, like the trees and
> rocks around it grew a temper. It reads dangerous through its POSTURE and its TELEGRAPH, not through
> horror. When it snorts and scrapes, a kid should feel "uh oh, here it comes" and grab the spear —
> and feel clever, not traumatized, when they win.**

### 2.1 Visual read — chunky toy-band silhouette

Ground it in the board's chunky nature kit (`inspiration/2026-06-12_21h10_44.png` faceted blob
trees/rocks; `21h13_31` rolling grassland) and the castaway's own chunky proportions (`21h00_32`). The
boar is a **faceted low-poly animal in the same faceted-smooth family as the world** — same shading
technique as the props (Shade Smooth + Mark Sharp / `_FlatShading` opt-in), warm earthy palette, big
confident planes.

**Silhouette — a front-heavy wedge that reads "charge" even at rest:**
- **Big blocky head + low snout**, hunched heavy shoulders, tapering to a smaller rump and stubby
  faceted legs. Mass sits FORWARD and DOWN — the classic boar "about to shove" stance.
- **The threat-triad that reads at orbit distance:** (1) the **low snout**, (2) a pair of **curved
  off-white tusks** (the single clearest danger cue — modeled geometry, chunky, not tiny), (3) a
  **bristled back ridge** — a row of faceted spike-planes down the spine (geometry, NOT a fur texture;
  material-honest per `quality-bars.md` #3). These three make it unmistakably a boar from behind the
  rear-orbit gameplay cam.
- Small dark eyes (the same big-friendly-eye language as the castaway, but smaller + set lower = reads
  "animal, a bit cross"). Chunky toy proportions; NO thin/spindly legs.

**Palette — reuse the world's warm earth tones, sub-1.0, no new hexes without escalation:**
- Body = dark warm brown / grey-brown (the world trunk/rock browns already in `LowPolyZoneGen.cs`).
- Snout + underside = a lighter warm tone (reads "soft nose"). Tusks = off-white bone (`bone-fitting`
  family `#CFC6AD`-ish, sub-1.0, never pure white → no bloom). Bristle ridge = a darker facet break.
- **Material-honest:** it reads as a bristly earth-brown animal; pattern is modeled facets + the ridge,
  not a detail texture (preserves the shared ~1-draw-call model).

**Real-world anchor + silhouette gate** (`lowpoly-quality.md` §0, `quality-bars.md` #4): *A boar is a
low, front-heavy, four-legged animal with a big head, a snout near the ground, curved tusks, and a
bristled back — standing on the ground, never floating.* Verify with a **side-profile capture** before
QA/Sponsor: mass-forward, snout low, grounded on all four hooves.

### 2.2 The telegraphed charge — the load-bearing feel

The charge is the whole boar. It must be **READABLE and FAIR** — the player is given time to see it
coming and answer (raise spear / sidestep). The telegraph is where the game is won or lost on feel.

**The charge sequence (four beats):**
1. **Alert.** Head lifts, body orients to the player, a low **"huff"** audio cue. "It noticed me."
2. **Wind-up telegraph — THE fair-warning beat.** Boar plants its forelegs, **scrapes/paws the ground**
   (a foreleg scrape), lowers its head, a rising **"snort,"** and a **small dust puff at the hooves**
   (pooled faceted dust, §1.2). *This beat's DURATION is the fairness/difficulty dial* (§2.3). It must
   be a distinct, legible pose change — the player reads it and reacts.
3. **Charge.** A fast, low, straight-line lunge toward the player's position **at the moment of
   commit** — it commits to a direction and does NOT home. This is what makes a sidestep or a well-timed
   spear-thrust *work*, and it is why the matchup emerges systemically (§2.4) rather than being scripted.
4. **Overshoot + recovery.** On a miss it overshoots, then has to turn around — the **punish window**
   where an axe/sword player lands their hit. (A spear player already connected during beat 3 via reach.)

**Motion in-style:** heavy, faceted, weighty — eased acceleration into the charge (T1 easing), a bit of
squash-free body-lean (lean via the whole transform, never non-uniform scale on a rig). Dust at the
hooves on the charge, not a speed-line VFX.

### 2.3 Kid-safe scariness across the 3 tiers

**The mesh and the death are IDENTICAL across tiers. Only TIMING, PACE, audio intensity, and status
potency change.** Scariness is tuned by *how much reaction time and punish you get*, never by visual
horror. (`quality-bars.md` #7, `[[difficulty-settings-easy-medium-hard]]`.)

| Tier | Telegraph length | Charge speed | Damage | Bleed-on-gore | Audio | Reads as |
|---|---|---|---|---|---|---|
| **Easy** (kid) | LONG (~1.2s — generous reaction window) | Slow | Low | OFF (or a tiny gentle tick) | Softer huff/snort | "A grumpy piggy I can out-think." Comedic overshoot-tumble on a miss is welcome. |
| **Medium** | Standard (~0.7s) | Medium | Medium | Short | Standard | "A real threat I respect." |
| **Hard** (adult) | SHORT (~0.4s — tight window) | Fast | High | Longer | Aggressive, faster re-charge | "Genuinely dangerous — I must commit to the right weapon." |

**Never, at any tier:** blood, gore, a maiming animation, a violent death. Easy tier may add *charm*
(a stumble, a shake-off) but never *cruelty*. The scary-dial is the telegraph clock, full stop.

### 2.4 Matchup legibility — "spear beats boar" must READ, not be told

Per the ticket, "spear beats boar" must EMERGE from (a) the spear's long reach hitting the charging boar
FIRST and (b) boar being weak-to-pierce (a resistance tag) — **not a hardcoded matchup table.** The
UX job is to make that emergence *legible* so the player learns the lesson themselves:

- **Make the reach visible.** The spear is the longest weapon in the set (§3.1) and its thrust extends
  toward the boar at full reach (§1.1). The impact should land at a clear **tusk-to-tip gap** — the
  player SEES the spear connect while the boar's tusks are still short of them. That visible gap IS the
  lesson: "the spear kept it off me."
- **Make weakness read through feedback, not a number popup.** A pierce hit on the boar lands with a
  slightly meatier `_HitFlash` + a more satisfying *thunk* + a bigger flinch than a slash/chop hit —
  the player feels "that one really worked" without a damage number. (Numbers are for tuning, not for
  the calm-tone HUD.)
- **The wrong weapon should feel plausibly survivable but worse** — a knife-user CAN win but eats the
  charge (short reach = they get shoved). That contrast is the systemic proof; don't gate it behind a
  hard counter.

### 2.5 Impact / hit-react / death feedback (boar)

- **Hit-react flinch:** the boar needs its own flinch (a brief recoil / head-toss) on taking a hit — the
  animal analog of the castaway hit-react states (`procedural-animation-verbs.md`). Reads "that
  connected"; interrupts nothing at hard tier (it keeps coming), staggers briefly at easy tier.
- **Hit-flash + dust puff + thunk** per §1.2 — dust-brown, never red.
- **Death = a gentle TOPPLE, not a gib.** The boar tips over, a soft dust puff, a descending
  "huff-out." Then it settles as a lootable (meat/hide per the survival vision) or fades — the calm
  returns. Kid-safe: "it's out," never "it's slaughtered." No blood pool.

### 2.6 Concrete tunables + Sponsor-input items (boar)

- **Tunables:** telegraph duration (per tier), charge speed (per tier), charge commit-distance / trigger
  range, overshoot distance, flinch stagger duration, per-tier damage + bleed potency, hit-flash
  intensity, dust-puff burst size, tusk length (silhouette read), audio intensity per tier.
- **Sponsor-input (soaked):** does the easy-tier telegraph feel long enough for a kid? Is the charge
  legible from the orbit cam (can you SEE the wind-up in time)? Does the spear-reach gap read the
  matchup lesson without a tooltip? Does the topple death land as gentle-not-grim? Is the boar
  "grumpy-charming" at easy and "genuinely dangerous" at hard from the SAME mesh?

---

## 3. LIGHTER direction notes (later in the cluster)

### 3.1 Weapon roster expansion — `86cah7ym9` (dagger / sword / material tiers)

- **Every weapon comes from the ONE unified in-house Blender set** — shared `weapon_palette.png` +
  one URP/Unlit material, faceted-chunky, material-honest, ~1 draw call
  (`blender-asset-pipeline.md`, `[[weapon-tool-unified-style-inhouse-blender-set]]`,
  `quality-bars.md` #3). New weapon = new data + assets, not new code (the POC's promise).
- **⚠ LIVE-STATE reconciliation — honor the current Sponsor decision over the old spec.** The family now
  ships in **THREE Sponsor-approved tiers** (`[[weapon-two-tier-style-stone-iron]]` + the 2026-07-18
  WOOD-tier add, PR #304): **WOOD** (whittled haft-brown + tan cut facets + fire-hardened spear tip,
  ~60–90 tris), **STONE** (knapped grey biface + straight wood haft + WOOD_DARK grip band), **IRON**
  (flat-smooth single-tone blades + iron handles + segmented leather grips; hammered faceting stays
  ONLY on the axe head). **The red lashing / grip-wrap-red is REMOVED** — the Sponsor said "remove the
  red things." **This supersedes `weapon-tool-style-spec.md` §2 (W8 `grip-wrap-red`) + §4 (axe lashing
  + sword grip) — treat those rows as STALE; follow the live three-tier recipes.** (Flagging, not
  silently editing my own spec — a spec correction PR should follow.)
- **Tier progression must read as "better gear" at a glance:** wood = crude/pale/light; stone =
  knapped/grey/rugged; iron = clean/forged/darker-cool. Each reads as its MATERIAL (bar #3).
- **Swing per class** (§1.1) — the dagger IS the knife class (quick jab); sword is the slash class. New
  types map to one of the four motion signatures; no new motion invented per tier.
- **In-hand size/feel is judged IN-HAND via the discrete mesh-swap picker, never a bare render or a
  broken continuous dial** (`quality-bars.md` #5, `[[verify-soak-builds-or-bake-and-judge]]`). Slots via
  the shared `HeldTool` rig (grip-point pivot, +Z-forward — `weapon-tool-style-spec.md` §3).

### 3.2 Additional status effects — `86cah7yuh` (poison / stun / slow)

Data-driven instances of the POC's general framework (bleed proved the DoT shape). Each needs a
**legible but non-alarming** cue — combat status in a calm/kid game telegraphs *state*, never *panic*:

| Effect | World-space cue (on the afflicted body) | HUD cue | Audio | Notes |
|---|---|---|---|---|
| **Poison** (DoT) | Small gentle green bubble/pip drift, sub-1.0 green | Green status pip on HP bar + faint green tick on damage | Soft bubbling tick | The non-red DoT sibling of bleed. |
| **Stun** (disable action) | **Classic toy stars/swirl above the head** (kid-friendly, instantly readable) | "Can't act" pip | Light dizzy chime | **Cancels the active-click strike** (ticket) — the swing input locks; the star swirl MUST make "you can't act now" obvious so a dead click doesn't confuse. |
| **Slow** (movement debuff) | A cool-tint weight cue + slowed footstep cadence | Slow pip | Dragged/heavier footstep | Reads "heavy legs," not "frozen." |

- **Bleed (existing):** keep it SUBTLE — a small red pip + a brief low-alpha red vignette *tick*, not a
  sustained red screen. (Bleed's red is a status convention, distinct from the removed weapon-lashing
  red — no conflict.)
- **PRIMITIVE DISCIPLINE:** status overlays live in the **UI layer** (UI Toolkit panel / UI Image), NOT
  a post-process Volume pulse (Render-Graph cost + tonally wrong, `game-juice.md` §2). World-space cues
  are **pooled faceted particles**. Every tint sub-1.0 (HDR-clamp). Status pips sit on the HP HUD (§3.3).
- **Per-tier potency** (`quality-bars.md` #7): effect duration/magnitude scales by difficulty.

### 3.3 HP HUD polish + heal sources — `86cah7z2q` (Uma HUD + Drew heal-wiring)

- **Render pattern:** follow the SurvivalHud 3-bar / need-meter lineage — do NOT re-spec it; reuse
  `hud-three-bar-spec.md` + `need-meter-3bar-direction.md` + `u2-5-survival-hud-spec.md`. HP binds to the
  POC Health component (`Current`/`Max`/`Current01`/`Changed`).
- **HP polish beyond the reused need-bar:** a distinct HP read (a warm-red heart/health color is the
  universal kid-legible convention and is appropriate here — this is HUD semantics, unrelated to the
  removed weapon-red); a **damage-flash** (bar flashes + a small eased shake on hit, T1); a **low-HP
  warning** (pulse the BAR — 0.8–1.2× ~2Hz like the campfire flicker, `game-juice.md` §1.5 — NOT a
  full-screen red pulse; keep it in the HUD, calm-tone).
- **Status pips** (§3.2) dock on/beside the HP bar — one small faceted icon per active effect.
- **Heal sources:** a **heal item** (reuse the eat/consume seam, like berries→hunger) and
  **rest-at-campfire** (reuse the campfire interaction). Heal FEEL = a gentle warm/green restore pulse
  up the bar (eased fill, T1) + a soft ascending chime. Restrained — recovery reads *relief*, not a
  power-up flash.
- **Per-tier heal potency** (`quality-bars.md` #7).

### 3.4 Find-in-world weapon acquisition — `86cah7y5b`

The "find" half of the dual acquisition model (craft is the other). UX direction — a found weapon is a
**discovery, a small warm reward beat**, not a loot-explosion:

- **Resting pose with a story:** the weapon reads as "someone left this here" — embedded in a stump,
  stuck point-down in the ground, or leaning on a rock (a human-scale landmark beat, `art-direction.md`
  carry-over). Placement follows the existing seeded-scatter/prop pattern.
- **Attract cue (reads "special" vs a common prop):** a gentle float-bob (±0.05u, ~0.8Hz — T7 ambient
  micro-animation) + a soft rim/glint (Fresnel `_RimIntensity` low, `lowpoly-quality.md` Rec 4 — the
  cheap opt-in, sub-1.0, no bloom). Just enough to say "pick me up," never a beam of light.
- **Pickup feel:** the proximity E-prompt via the shared PickableLooter path (as the log-pile spec
  uses); on pickup, a soft chime + the weapon arcs to the belt with an ease (T1). Equips via the
  existing data-driven weapon type (no parallel model).
- **Meshes from the unified Blender set** — a found/unique weapon is a good place to surface a higher
  tier (an **iron** find as the "special" reward vs the crafted wood/stone).

---

## 4. Cross-cutting primitive & HDR-clamp discipline (Unity translation of Uma's hard rules)

Carried from the Godot-era "ColorRect not Polygon2D / HDR-clamp / no `gl_compatibility` break" rules,
translated to this Unity/URP project:

- **Enemy tints / hit-flash → a shader property inside `CBUFFER_START(UnityPerMaterial)` on a per-enemy
  material instance, NEVER a `MaterialPropertyBlock`** (MPB disqualifies the GPU Resident Drawer
  instanced path, `unity6-mastery.md` §2 / `game-juice.md` §2).
- **Full-screen status/damage overlays → UI layer (UI Toolkit / UI Image), NEVER a post-process Volume
  pulse** (Render-Graph pass cost; chromatic-aberration/lens pulse ruled out, `game-juice.md` §2).
- **World-space feedback → pooled faceted particles** (`Unlit/Particle`, separate from the world palette
  material; pooled via `ObjectPool<T>`), ≤12/burst, warm/chunky, never wispy smoke, never gore.
- **Every tint/flash channel sub-1.0** (HDR-clamp, `style-guide-v2.md` §5) so nothing bloom-blows-out
  and the crisp faceted read survives.
- **No non-uniform scale on the rigged character body** for any combat squash/stretch — props/UI only
  (`game-juice.md` §2). Body-lean via the whole transform, not scale.

---

## 5. Sponsor-input items (consolidated — for the soak, not decided here)

**Swings:** per-class `swingImpactDelaySeconds`; hit-stop frame counts (is 3 too "stunned" on the
axe?); Impulse amplitude; does the knife want zero hit-stop (frictionless)?; iron-tier audio brightness
threshold. **Confirm the "axe+pickaxe share one clip, three new clips total" economy call.**

**Boar:** easy-tier telegraph length (long enough for a kid?); charge legibility from orbit cam; does the
spear-reach gap teach the matchup without a tooltip?; is the topple death gentle-not-grim?; does ONE
mesh read grumpy-charming (easy) → dangerous (hard) purely on timing?

**Roster:** does the wood→stone→iron progression read as "better gear" at a glance? Confirm the
red-lashing removal is fully reflected (my `weapon-tool-style-spec.md` §2/§4 need a correction PR).

**Status:** is the stun star-swirl the right kid-legible "you can't act" cue? Are the world-space cues
readable but calm (not panic-inducing)?

**HP HUD:** is warm-red the right HP color given the weapon-red removal (HUD semantics vs prop color)?
Damage-flash + low-HP pulse amplitude (calm-tone).

**Find-in-world:** does the resting-pose + rim-glint read "special find" without over-VFXing it?

---

## Cross-references

- **Tickets:** `86caffwv5` (swings), `86cah7ydt` (boar), `86cah7ym9` (roster), `86cah7yuh` (status),
  `86cah7z2q` (HP HUD), `86cah7y5b` (find-in-world); dependency POC `86cah7xxp`; combat design lock
  `86cabcdpn`.
- **Docs:** `.claude/docs/game-juice.md` (the five must-haves + hard don'ts — the whole §1.2/§4 basis),
  `.claude/docs/procedural-animation-verbs.md` (overlay-state idiom + the chop-clip ruling),
  `.claude/docs/art-direction.md` + `inspiration/*.png` (chunky toy-band north-star; nature-kit
  `21h10_44`, grassland `21h13_31`, tools `21h06_54`/`21h07_20`/`21h07_42`/`21h08_08`, character
  `21h00_32`), `.claude/docs/lowpoly-quality.md` (§0 anchor+silhouette, Rec 4 rim), `.claude/docs/
  vision-far-horizon-game-concept.md` (kid→adult difficulty), `.claude/docs/unity6-mastery.md` §2
  (GRD/MPB), `.claude/docs/blender-asset-pipeline.md` (weapon family).
- **Uma specs:** `weapon-tool-style-spec.md` (⚠ §2/§4 red-lashing STALE — see §3.1),
  `hud-three-bar-spec.md` / `need-meter-3bar-direction.md` / `u2-5-survival-hud-spec.md` (HUD pattern),
  `tree-chop-logpile-visual-spec.md` (PickableLooter loot-prompt precedent, faceted-prop palette),
  `style-guide-v2.md` §5 (HDR-clamp).
- **Bars/memories:** `quality-bars.md` #2 (lively motion), #3 (material-honest), #4 (real-world anchor),
  #5 (in-hand picker), #7 (3 tiers); `[[chop-swing-mixamo-clip-not-procedural]]`,
  `[[active-input-not-proximity-auto-for-actions]]`,
  `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`,
  `[[weapon-two-tier-style-stone-iron]]`, `[[difficulty-settings-easy-medium-hard]]`.
