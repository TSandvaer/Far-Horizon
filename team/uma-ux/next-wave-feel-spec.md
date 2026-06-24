# Next-Wave Feel Spec — Chop · Sticks · Stones

**Tickets:** chop `86caa4c5c` · sticks/branches `86caa96rd` · stones `86caa4c96` — this doc is the
INTERACTION-FEEL + VISUAL-FEEDBACK direction those three impl tickets consume. **Owner:** Uma (direction)
→ Drew (impl, world-scatter + interaction) with Devon (inventory hook). **Reviewer:** Drew.
**Status:** DIRECTION — docs only, no implementation here, no Assets/, no code, no new shader.

**Source of truth (look before reading):** the board PNGs in [`inspiration/`](../../inspiration/) — esp.
`2026-06-12_21h12_49.png` (Blender nature kit: log / stump / rocks / sticks), `21h08_08.png` (the axe),
`21h22_33.png` (forest-meadow ground cover) — and [`art-direction.md`](../../.claude/docs/art-direction.md)
+ [`lowpoly-quality.md`](../../.claude/docs/lowpoly-quality.md). The images are ground truth; this is the
translation.

**Extends, does NOT rewrite — the two SHIPPED interaction precedents this whole wave mirrors:**
- **`BerryBush.cs`** (harvest, ticket `86caa5zz3`, ON MAIN) — proximity + edge-trigger, **no tool**,
  ripe→bare→regrow visual toggle, generous radius scaled by visual size, one-shot trace. **Sticks + stones
  are this idiom verbatim** (pickup-by-hand). Do not invent a new input.
- **`ChopTree.cs`** (chop, ON MAIN) — **axe-gated** proximity, paced discrete chops (`chopInterval 0.6s`),
  a runtime sink+tip felling tween (`FellDuration 0.5s`, smoothstep, 0.6u down + 70° tip) on the serialized
  visual. **Chop's richer feel is this doc's main job** — ChopTree's own comment hands the feedback pass to
  Uma ("Uma's HUD/feel pass owns richer feedback; the per-chop hook here is the stub the ticket permits").

> **Style-guide parent:** [`style-guide-v2.md`](style-guide-v2.md) §0–1 — *Far Horizon is a warm toy you
> want to pick up.* Every feedback beat below inherits the toy-warm gate: **if a beat makes the moment
> louder, harsher, or more "AAA juice," it's wrong even if it's clear.** Cheerful, faceted, hand-made.

---

## 0. Tonal anchor (read this first)

**Gathering should feel like a happy little chore in a toy world — each grab is a small, warm "got it!",
each chop a satisfying THOCK that the tree leans into and the ground answers with a puff of chips.** The
castaway is a hopeful kid filling his arms with the island's gifts; the world is generous and it shows him
so — it bobs, it shakes, it puffs, it chimes a little — and then it settles right back to calm. Nothing is
violent. A felled tree is not destruction, it's the world handing you wood and tipping over with a soft
*whump*. A picked-up stick is the island saying *here, take this.*

The three reads, one family:
- **CHOP** — the loud, earned one. Effort, rhythm, a tree that reacts to each blow and topples generously.
  The only beat with build-up and payoff.
- **STICK** — the quietest "free gift": stoop, a tiny bob, it's gone into your arms. One wood, one warm tick.
- **STONE** — the same quiet gift with a cooler, harder voice (a *tok*, not a rustle). Stone respawns; the
  spot quietly refills later.

**The gate (carried from the HUD + style specs):** *the world is the star.* Feedback is a glance-and-feel
garnish on a world you're looking AT, never a screen-filling effect that pulls focus. Lively, then quickly
calm — **lively + lightly-damped** per [[sponsor-prefers-natural-lively-motion]] (the axe FOLLOWS the arm,
water has MOVING waves, foam PULSES — motion is animated, only lightly damped, never locked static and never
twitchy-jittery). Every overshoot resolves back to rest in well under a second.

**The single most important call:** chop, stick, and stone must read as ONE interaction FAMILY (same warm
"got it" grammar, same chip/puff vocabulary, same quick settle) yet be **instantly distinguishable by their
voice + reaction** — chop is the rhythmic earned one, stick is a soft rustle, stone is a hard *tok*. A
blindfolded player should know which of the three they just did from the feel alone.

---

## 1. Cohesion contract — the shared "got it!" grammar

Every gather/chop beat in the wave is built from the SAME four cheap ingredients, tuned per-interaction.
This is what makes them one family. **All four are pure transform/instantiate work on already-serialized
visuals — no new framework, no shader, no Awake-built hierarchy** (the legs-up class; mirror BerryBush's
"authored editor-time into Boot.unity" serialization note).

| Ingredient | What it is | Why it's cheap / safe |
|---|---|---|
| **A. Reaction tween** | A short transform nudge on the target's *serialized* visual (squash-bob, shake, or sink+tip), smoothstepped, that overshoots a hair then settles to rest. | Same pattern as ChopTree's `StepFelling` — runtime transform anim, no hierarchy built at Awake. `Time.unscaledDeltaTime`-paced so a headless run lands the end-state fast without touching the gameplay assertions. |
| **B. Chip/leaf puff** | A tiny BURST of 4–8 faceted low-poly bits (reuse the canopy/rock vertex-color material — NO new shader, NO atlas; per `lowpoly-quality.md` §1 SRP-Batcher / shared-palette rule) that pop out, arc, and fade/sink over ~0.4–0.6s. | A pooled handful of shared-material quads/tris; ~1 draw call on the batched material. NOT a Unity `ParticleSystem` with a bespoke material (that breaks the shared-palette ~1-draw-call rule). A simple instantiate-arc-despawn loop is enough and stays faceted-on-brand. |
| **C. Pickup pop** | The item's own quick scale-up-then-vanish (or a small arc toward the HUD/player) as it leaves the world — the "it went into my arms" read. | One transform tween on the consumed prop before it's destroyed/hidden; mirrors BerryBush hiding `berriesVisual`. |
| **D. Audio tick** | One short, soft cue per beat (rustle / *tok* / THOCK + a felling *whump*). | Single one-shot per interaction; **Devon owns the audio bus wiring** — flag it, don't spec dB here (no audio-direction.md authority needed for one-shots this small; sub-1.0 "calm" loudness, never a startle). |

**Damping rule (the lively-but-calm contract):** A and C use a light overshoot — scale/▿position goes ~10–18%
past target then smoothsteps back to rest in **≤0.35s** (gathers) / **≤0.5s** (the chop reaction). Never a
hard snap (reads dead), never a long wobble (reads broken/twitchy). One gentle overshoot, then calm. This IS
[[sponsor-prefers-natural-lively-motion]] made concrete for gathers.

**Difficulty-tier awareness** (per `difficulty-settings-easy-medium-hard` memory — design every system tier-aware):
the FEEL is identical across tiers (a kid and an adult both get the warm "got it"); only the *numbers behind*
the feel scale, and they scale through the SAME named serialized fields the gameplay tickets already expose —
this doc adds **no new tier knob**. Easy = faster chop / fewer chops-to-fell / shorter respawn (more
generous, less waiting); Hard = slower chop / more chops / longer respawn. The reaction tween, chip puff, and
audio tick are tier-INVARIANT (you never make the toy feel worse on Hard — you make it scarcer, not uglier).

---

## 2. CHOP — `86caa4c5c` (the earned one)

**Tonal sub-beat:** *effort with a generous payoff.* Each blow lands with weight, the tree flinches, and on
the last blow it tips over and hands you the wood. This is the only beat with rhythm and a climax.

ChopTree already ships the skeleton (paced chops + a sink+tip fell tween). This spec LEVELS UP the per-chop
moment and the fell, staying inside that existing seam — Drew extends `Chop()` / `StepFelling()`, he does
not rebuild.

### 2.1 Per-chop reaction (ingredient A + B + D), each `Chop()`

- **Tree flinch (A):** on each landed chop, a quick directional SHAKE of the serialized `visual` — a small
  recoil away from the player (~3–5° tip + a few cm, axis = horizontal from player→tree) that smoothsteps
  back to standing in **~0.18s**. Lively (the tree visibly *takes* the hit) and lightly damped (one recoil,
  no wobble). Reuse the `StepFelling` tween mechanic; this is a smaller, faster sibling that returns to rest
  rather than committing to the fall.
- **Wood chips (B):** a small BURST of 4–6 faceted brown chips at the chop contact height, popping out toward
  the player + arcing down, fading/sinking over ~0.4s. Trunk-brown, sub-1.0 (route any near-neutral brown
  through `QuantizeFine` per `lowpoly-quality.md` Rec 1 so it doesn't pink-cast). Shared vertex-color
  material — NOT a bespoke particle material.
- **Chop THOCK (D):** one short woody impact per chop; soft, warm, not a sharp crack. Devon buses it.
- **Rhythm:** unchanged — paced by ChopTree's `chopInterval` (0.6s default, tier-scalable via the existing
  `tool-use speed` named field per the ticket's AC1/AC5a). The chips + THOCK + flinch fire together ON each
  paced chop, so the rhythm IS the feedback cadence (no extra timer).

### 2.2 The fell (the climax) — extend `StepFelling()`

- Keep the existing sink+tip (0.6u down, 70° tip, 0.5s smoothstep) — it already reads as a generous topple.
- **Add at fell-start:** a slightly BIGGER chip/leaf puff (8–10 bits, green leaf flecks mixed with brown) at
  the canopy + a soft settling *dust* puff at the base where it meets the ground — the world's "whump."
- **Add a felling *whump* (D):** one low soft thud as the tip completes (Devon buses it; pairs with the
  visual landing, not the first frame). Warm, not a crash.
- **Stump persists** (ticket AC4): the felled tree leaves a stump through the regrowth window — the stump is
  the quiet promise the tree comes back. No feedback on the stump itself; it's calm scenery until regrow.

### 2.3 Regrow (ticket AC3) — the gentle return

When the regrow timer elapses (random within the serialized `tree regrowth time` min/max), the tree returns
with a small **grow-in pop** — scale 0→1 with one light overshoot (~12%, ≤0.35s smoothstep), the mirror of
the pickup pop. The island quietly heals. Optional soft chime (D) — Sponsor's call at soak; default OFF (a
distant tree regrowing should not ping the player). Flagged §6 Q1.

---

## 3. STICK / BRANCH — `86caa96rd` (the soft free gift)

**Tonal sub-beat:** *here, take this.* The lowest-effort, lowest-yield gather — a stoop and it's in your
arms. One wood, one warm tick. It should feel almost TOO easy (that's the point — it's the early thin-wood
path before the axe).

Mirror BerryBush's harvest seam EXACTLY (proximity + edge-trigger, **no tool**), but the stick is CONSUMED
(removed from world) like the ticket says, not toggled bare like the persistent bush.

- **Reaction (A):** on pickup, the stick does a tiny **squash-bob** — a quick ~12% squash + lift, ≤0.3s —
  as it's plucked, then C takes over. Lively (it reacts to being grabbed), feather-light damping.
- **Pickup pop (C):** the stick scales up ~15% then vanishes (or arcs a short hop toward the player) over
  ~0.25s, then is destroyed. The "into my arms" read. This is the consumed-prop version of BerryBush hiding
  its berries visual.
- **Soft rustle (D):** one short, dry, leafy *rustle* — the quietest cue in the wave. Devon buses it; barely
  there (a kid picking up a twig, not an event).
- **No chip burst** (B is omitted for sticks) — a twig doesn't shed chips; keeping it clean is what makes the
  stick read as *lighter* than the chop and *softer* than the stone. The ABSENCE is the distinguisher.
- **Variety feeds feel:** the ticket scatters sticks in VARIOUS SIZES — let the squash-bob amplitude scale
  with the stick's visual size (a big fallen branch bobs a touch more than a twig), same `localScale.x`
  scaling BerryBush already uses for its radius. Cheap, and it makes the world feel hand-placed.
- **Finite gather:** sticks don't respawn in v1 (ticket AC5) — so no grow-in pop. They're a one-time gift;
  the world doesn't refill them. (If the Sponsor wants respawn at soak, it inherits the stone grow-in pop
  §4.3 — flagged in the ticket already.)

---

## 4. STONE — `86caa4c96` (the hard cool gift)

**Tonal sub-beat:** *the same warm gift, with a cooler harder voice.* Identical pickup grammar to the stick
(proximity + edge-trigger, no tool, consumed) — but everything reads a shade COOLER and HARDER so the player
feels the material difference without thinking about it. Stone respawns; the spot quietly refills later.

- **Reaction (A):** on pickup, a small **hop-and-settle** rather than a squash — a pebble doesn't squash, it
  pops up a few cm and settles (~10%, ≤0.28s). Harder, snappier than the stick's soft squash; same family,
  different material voice.
- **Pickup pop (C):** same scale-up-and-vanish as the stick, but a touch quicker/crisper (~0.2s) — stone is
  less "yielding" than a twig.
- **Stone *tok* (D):** one short, dry, hard *tok / click* — cooler and harder than the stick's rustle. This
  is the single clearest stick-vs-stone distinguisher; Devon buses it. NOT a heavy boulder thud (these are
  small pebbles, ticket AC1) — a light pleasant click.
- **Tiny grit puff (B, minimal):** an OPTIONAL 2–3-bit faint grey grit puff at the lift point, ≤0.3s — much
  smaller than the chop's chips (a pebble lifting off dirt, not a tree being hit). Grey, near-neutral →
  route through `QuantizeFine` (Rec 1) so it doesn't pink-cast. Keep it tiny or omit; the *tok* carries the
  beat. Flagged §6 Q2 if it reads as too much.
- **4.3 Respawn grow-in (ticket AC3):** when the `stone respawn time` timer elapses (random within the
  serialized min/max), the stone returns at the spot with the **grow-in pop** (scale 0→1, ~12% overshoot,
  ≤0.35s) — same gentle-return motion as the tree regrow §2.3. The island refills its gifts. No audio on a
  distant respawn (default OFF, parity with tree regrow).

---

## 5. Visual-primitive + cost discipline (the hard-rule check)

- **No new shader, no atlas, no bespoke particle material.** Every chip/puff/flinch reuses an EXISTING
  vertex-color shared-palette material (`LowPolyVertexColor.shader` family) — this is the
  `weapon-asset-material-honest-pattern-via-geometry` + `lowpoly-quality.md` §1 SRP-Batcher rule (one
  material, ~1 draw call, no per-asset texture). A bespoke `ParticleSystem` material is the trap; a pooled
  instantiate-arc-despawn of shared-material faceted bits is the on-brand, on-budget route.
- **Near-neutral colors (brown chips, grey grit) go through `QuantizeFine`** (`lowpoly-quality.md` Rec 1) —
  the coarse 12-step quantizer pink-casts R≈G≈B props; this is a confirmed bug, not a maybe.
- **Sub-1.0 on every channel** (style-guide §1.4 / HUD-spec HDR-clamp discipline carries as a TONAL rule —
  warm not neon, even though this is Windows-desktop, not WebGL).
- **No Awake-built hierarchy** for any feedback visual — pre-author the puff pool / reaction targets
  editor-time into Boot.unity if persistent, or pool-instantiate at runtime from a serialized prefab ref
  (NOT `new GameObject` per chop). Mirror BerryBush/ChopTree's "serialized, not Awake-built" note.
- **`Time.unscaledDeltaTime`-paced tweens** (like `StepFelling`) so headless/PlayMode runs land the
  end-state fast and the cosmetic feedback never perturbs the wood-count / felled / stacks assertions the
  tickets' AC6/AC5 tests guard.
- **Pooling, not per-event alloc** (unity6-mastery §5 GC / hot-path) — the chip burst is the only
  multi-instance effect; pool the bits, don't `Instantiate`+`Destroy` per chop in a tight chop rhythm.

---

## 6. Open questions for Sponsor (soak-confirmable; defaults shipped, none block impl)

These are FEEL calls — ship the default, let the soak adjust. None gate the mechanic; all are one-line tweaks.

- **Q1 — Regrow/respawn audio.** Default OFF (a distant tree/stone returning should not ping the player). If
  the Sponsor wants a soft chime so the refill is noticed, it's a one-line enable on the grow-in pop.
- **Q2 — Stone grit puff.** Default = tiny (2–3 bits) or omitted; the *tok* carries the beat. Soak decides
  whether the faint puff adds or clutters.
- **Q3 — Chip burst size on the fell.** Default 8–10 bits. Sponsor's eye at soak: bigger generous burst vs.
  keeping it subtle so the topple (not the confetti) is the star.
- **Q4 — Stick squash-bob amplitude.** Default scales with stick size; soak confirms it reads as charming,
  not jittery.

---

## 7. Hand-off to Drew (implementable mirror, no new framework)

Everything here is implementable by extending the two shipped seams:
1. **Chop:** extend `ChopTree.Chop()` (add flinch tween + chip burst + THOCK hook) and `StepFelling()` (add
   the fell puff + whump hook). No new component; no new input; the axe gate + pacing are unchanged.
2. **Stick:** a new pickup component in the **BerryBush idiom** (proximity + edge-trigger, no tool,
   consumed) + the squash-bob/pickup-pop tween + rustle hook. Reuse BerryBush's serialization + trace shape.
3. **Stone:** the SAME pickup component (or a sibling) in the BerryBush idiom + the hop-settle/pop tween +
   *tok* hook + the grow-in pop on respawn (mirror tree-regrow's return motion).
4. **Audio:** four one-shot hooks (chop THOCK, fell whump, stick rustle, stone tok) — **Devon buses them**;
   this spec names the cue + voice, not the dB/bus (one-shots this small need no audio-direction.md entry).

Self-Test / capture: each interaction's feedback is UX-visible → shipped-build capture per the wave's
existing gate (the tickets already require it). No capture spec'd here beyond "the reaction is visible in the
built-exe capture, not just the editor" (the legs-up rule).

---

*Cross-refs: `BerryBush.cs` + `ChopTree.cs` (shipped seams) · `style-guide-v2.md` §0–1 (toy-warm gate) ·
`lowpoly-quality.md` §1 (SRP-Batcher / shared palette) + Rec 1 (`QuantizeFine` pink-cast) ·
`need-meter-3bar-direction.md` (the interaction family the HUD reflects) · memories
`sponsor-prefers-natural-lively-motion`, `weapon-asset-material-honest-pattern-via-geometry`,
`difficulty-settings-easy-medium-hard`.*
