# Survival Roadmap — Far Horizon

**Author:** Priya (PL). **Status:** macro-arc roadmap. **Ticket:** U9 (`86ca86gmh`).
**Replaces:** the CLOSED Godot-ARPG journey-arc roadmap (RandomGame PR #422 — closed
2026-06-12 by Sponsor decision 6-a; only its macro-vision *spirit* carries, not its
ARPG system-mapping).

> **How to read this doc.** M-U2 is **firm** — its shape is Sponsor-locked (one need →
> craft axe → chop → campfire). Everything past M-U2 is a **PROPOSAL**, labelled as such
> on every line. No commitment beyond M-U2 has been made by the Sponsor; the M-U3+ tables
> are sequencing *options for the Sponsor to shape*, not a plan of record. The macro arc
> (start small → journey toward the far horizon) is the durable north-star; the *order and
> depth* of the survival systems that fill it are the Sponsor's to sequence.

---

## §0 — PO digest (read this first)

The game in one sentence: **a young, hopeful castaway washes ashore with nothing, survives
his way forward, and journeys toward the far horizon through a big, alive world.**

That sentence has three movements, and this roadmap is built on them:

1. **Start small.** You wake on a beach. No tools, no shelter, one pressing need. The world
   is already *big and alive* around you (the art-direction board's small-player / big-world
   feel) — but your reach is tiny. This is M-U2: **the thin survival loop**, Sponsor-locked —
   ONE need → craft an axe → chop a tree → build a campfire → the need is met. One satisfying
   cycle, felt, in a desktop build. Nothing more until that cycle is *fun*.

2. **Deepen.** Once the one-need loop proves fun, survival gains *texture*: a second need,
   simple food, a place that's yours (shelter), day/night and weather giving the needs a
   rhythm. Each addition is a **proposal** here — the Sponsor picks which, and how deep,
   after soaking the thin loop. (M-U3 / M-U4 below.)

3. **Journey out.** The castaway's reach grows from one beach to a **varied world** —
   survival *regions* (forest, highland, wetland, ruins) that each pose a different survival
   problem, with human-scale landmarks and lush purposeful decoration per the inspiration
   board. The far horizon stops being a backdrop and becomes a destination. This is the
   biggest, furthest-out, most **proposal-heavy** part of the arc (M-U5+ below).

**What's firm vs. proposed, in one line each:**

- **M-U2 (FIRM, Sponsor-locked):** the thin loop — one need, axe, chop, campfire. Tickets
  U2-1..U2-7 already shaped (re-scope §4); this roadmap's §2 loop structure traces to them.
- **M-U3+ (PROPOSAL):** depth (second need, food, shelter, day/night), then regions, then a
  progression spine, then the horizon-as-destination. Each milestone below carries a
  **"Sponsor judges"** gate line — nothing past M-U2 is built until the Sponsor shapes it.

**Honest scale note.** Far Horizon is a *re-foundation*, not a half-built game carried over.
M-U1 stands up the engine and ports the four Sponsor-approved systems (click-move + orbit
camera, Zone-D look, castaway); M-U2 proves one survival cycle is fun. We are at **"strong
foundation + first loop"** — closer to the start than the middle. Everything past M-U2 in
this doc is a map of *possible* roads, drawn so the Sponsor can choose which to walk and in
what order. I'd rather draw it honestly than pretend the road past M-U2 is already paved.

---

## §1 — The macro arc (the spine the milestones hang on)

The arc is the journey-arc *spirit* (memory `world-feel-big-and-endless`,
`game-world-journey-arc`) re-cast for survival. The Godot version was a dungeon-crawl
exploration arc (cloister → biomes → grow); the survival version is a **needs → mastery →
expansion** arc. Same emotional shape (humble start → big alive world → wonder of the
horizon), different core loop.

```
  WASH ASHORE            THE THIN LOOP            DEEPEN SURVIVAL          JOURNEY OUT
  (start small)          (one cycle, fun)         (texture + rhythm)       (toward the horizon)
       |                       |                        |                        |
  one need, no tools  →  craft → chop → fire   →  needs / food / shelter  →  survival regions,
  big world, tiny reach   need satisfied            day-night rhythm           landmarks, the far
                                                                                horizon as a goal
       |                       |                        |                        |
     M-U1+M-U2 setup        M-U2 (FIRM)              M-U3/U4 (PROPOSAL)       M-U5+ (PROPOSAL)
```

**The through-line that never changes:** the player is always *small in a big alive world*,
and the world always *rewards looking outward*. Every milestone, firm or proposed, is
measured against that — a survival system earns its place only if it makes the world feel
more alive and the horizon more worth reaching, not just more numbers to manage.

**Art-direction anchor (look at `inspiration/*.png`).** The two board references — the lush
human-scale garden and the lived-in village courtyard — set the world the survival loop lives
*in*: fine multi-tone worn ground, human-scale landmarks (a campfire reads like a real
campfire, not a monument), lush *purposeful* decoration, a warm cohesive palette, the player a
small element in a dense alive scene. This is the Zone-D look ported in M-U1; the survival
content fills it. The horizon the castaway journeys toward must always *look* like a place
worth reaching.

---

## §2 — The survival loop, mapped against the ported Unity baseline (FIRM — feeds M-U2)

This is the load-bearing section: the survival loop's structure, mapped against the
**ported Unity baseline** (the M-U1 ports — NOT shipped Godot systems; the Godot
combat/save/quest/dialogue systems are design-reference only, re-implemented in C# when the
loop needs them).

**The ported Unity baseline (what M-U2 builds ON):**

| Ported system | M-U1 ticket | What the survival loop gets from it |
|---|---|---|
| NavMesh click-to-move + orbit camera | **U3** | The castaway moves to a tree / a crafting spot / a campfire by clicking; the player frames the world by orbiting. The loop is *navigated*, not driven. |
| Zone-D environment look | **U5** | The world the loop lives in — warm lush ground, soft light, the art-board feel. Trees, the shore, the campfire all render in this look. |
| CC0 castaway character + grounding | **U6** | The survivor himself — the small figure in the big world; blob-shadow grounding so he sits in the scene. |
| Desktop build + HUD build-stamp + capture/soak ritual | **U7 / U8** | Every loop beat is verified in the *shipped exe* (editor-green ≠ build-correct), with a soak handoff. |
| EditMode + PlayMode + shipped-build-capture testing bar | **U7** | The loop's tests run on this bar: PlayMode for runtime behavior, build-capture for "it actually works shipped." |

**The thin loop, beat by beat (FIRM — each beat = an M-U2 ticket from re-scope §4):**

| # | Loop beat | What it adds | Built on (ported baseline) | M-U2 ticket |
|---|---|---|---|---|
| 1 | **One need decays** | A single need (energy / hunger / warmth — Sponsor picks the one) that ticks down and creates the *why* of the loop. | Zone-D world (U5) to need-in; HUD scaffold. | **U2-1** |
| 2 | **Craft the axe** | The first crafting interaction — gather/craft an axe, the chop tool. Seeds the inventory readout. | Click-move (U3) to reach the crafting spot. | **U2-2** |
| 3 | **Chop a tree → wood** | The "do work in the world" beat — axe + tree yields wood. Wires a tool to a world-interaction. | Click-move (U3) + Zone-D tree props (U5). | **U2-3** |
| 4 | **Build the campfire → need met** | Wood → campfire; the campfire satisfies the one need. **Closes the loop.** | Click-move (U3) to place/light; castaway (U6) by the fire. Don't-Starve prefab-placement seed. | **U2-4** |
| — | Minimal survival HUD | The one need + collected resources (axe / wood), diegetic-light. | — (Uma specs, Devon wires). | **U2-5** |
| — | Castaway polish pass | "More detailed / polished" (Sponsor iter7 note) — warmer costume/material. | Ported base char (U6). | **U2-6** |
| — | PlayMode loop coverage | need-decays → craft → chop → campfire-satisfies, green in PlayMode + a shipped-build capture of the full loop. | Testing bar (U7). | **U2-7** |

**The loop, in one line:** *need decays → craft axe → chop tree → build campfire → need met
→ (repeat).* That cycle, **felt** in a desktop build the Sponsor soaks, is the whole of M-U2.
If it's fun, the proposals below layer on top; if not, we iterate the loop before layering
(Sponsor decision 4/6).

**Sponsor judges (M-U2 gate):** the Sponsor soaks the thin-loop desktop build and confirms
the one-cycle survival loop *feels* good. That verdict is the gate to opening any M-U3
proposal below.

---

## §3 — PROPOSAL: deepen survival (M-U3 / M-U4)

> **PROPOSAL — not committed.** These milestones layer *texture* onto the proven thin loop.
> Each is a sequencing option for the Sponsor to pick from and order after soaking M-U2.
> Nothing here is built until the Sponsor shapes it. Ticket shapes are illustrative, not filed.

The thin loop has one need and one cycle. "Deepen" means giving survival a *rhythm* and a
*texture* without yet expanding the *map*. The unit of work stays small — each addition is a
loop the player already understands, with one more dimension.

### M-U3 (PROPOSAL) — "Texture: a second need + food + a rhythm"

**Shape (proposed):** add the *minimum* that makes survival feel like survival rather than a
single chore — a second need, a simple food source, and a day/night rhythm that gives the
needs a clock. Still ONE survival region (the beach + its immediate surrounds); no map
expansion yet.

| Proposed ticket (illustrative) | What it adds | Builds on |
|---|---|---|
| `feat(survival): second need + need-interaction model` | A second need (e.g. hunger alongside energy) so survival is a *balance*, not a single bar. Generalizes U2-1's single-need model. | M-U2 loop |
| `feat(survival): simple food source → satisfy the food need` | Forage / catch / cook a simple food (the food analogue of chop→wood→fire). | U2-3 chop pattern, U2-4 campfire (cook) |
| `feat(world): day/night cycle + need rhythm` | A day/night clock that gives the needs cadence (cold at night → fire matters; tired by dusk). Lighting ties to the Zone-D look. | U5 Zone-D, U2-1 need model |
| `feat(ui): HUD — two needs + expanded inventory` | The HUD grows to two needs + a slightly richer resource readout. | U2-5 HUD |
| `test(unity): PlayMode coverage — two-need balance + day/night` | Coverage for the deepened loop on the M-U1 bar. | U7 |

**Sponsor judges (M-U3 gate):** does the deepened loop make survival feel like a *texture*
worth managing, or is one need enough for now? Sponsor picks *which* additions (second need /
food / day-night) and how deep — any subset is a valid M-U3.

### M-U4 (PROPOSAL) — "A place that's yours: shelter + base"

**Shape (proposed):** the castaway stops being purely transient and gets a *foothold* — a
simple shelter that protects against a need (cold / weather), and a small base the loop
returns to. This is the first "I live here now" beat, deferred from the thin loop on purpose
(Sponsor decision 4/6: shelter layers *after* the loop proves fun).

| Proposed ticket (illustrative) | What it adds | Builds on |
|---|---|---|
| `feat(survival): buildable shelter → protects against a need` | Craft/place a simple shelter (the campfire's bigger sibling) that buffers a need (warmth / weather). | U2-4 placement, U2-2 crafting |
| `feat(survival): weather → makes shelter matter` | Light weather (rain / cold snap) that spikes a need, giving shelter a *reason*. | M-U3 day/night, need model |
| `feat(world): base anchor — the loop has a home` | A small base the loop returns to (storage seed, campfire-as-hearth). | U2-4 campfire |
| `test(unity): PlayMode coverage — shelter + weather` | Coverage for the shelter loop. | U7 |

**Sponsor judges (M-U4 gate):** is a fixed base the right shape, or should the castaway stay
mobile (journey-first) before settling? This is a real fork — base-building survival vs.
nomadic-journey survival — and it's the Sponsor's call. It also gates §4 below (a base implies
a *hub* to journey out from; a nomadic loop implies the journey IS the survival).

---

## §4 — PROPOSAL: journey out (M-U5+)

> **PROPOSAL — the furthest-out, least-committed part of the arc.** This is where the macro
> vision (toward the far horizon, big alive world) becomes *content*. It is deliberately drawn
> as options, not a plan — the deeper into this section, the more it depends on choices the
> Sponsor hasn't yet had reason to make. Treat every line as a question, not a commitment.

Once survival has texture (M-U3/U4), the arc's promise — *journey toward the far horizon* —
needs the world to *open*. The unit of new content becomes the **survival region**: a stretch
of world that poses a *different survival problem* and rewards reaching it.

### M-U5 (PROPOSAL) — "The first journey: a second survival region"

**Shape (proposed):** the castaway's reach extends from the beach to ONE new region (e.g. a
forest interior, or a highland) that survival-differs from the start — different resources,
a different dominant need, a human-scale landmark to discover. The first proof that the world
is *traversable* and *varied*, not one screen.

- **Region as survival-problem, not just scenery.** A forest = more wood but colder nights;
  a highland = thin food but a vantage toward the horizon. Each region re-poses the loop.
- **Human-scale landmarks + lush purposeful decoration** (art-board): a ruined shrine, a
  fresh-water spring, an old camp — things that read as *real places* a survivor would seek,
  not set-dressing.
- **The seam between regions** is the genuinely-new technical lift (how the world opens —
  contiguous traversal vs. discrete regions). Flag for a spike when this milestone is shaped.

**Sponsor judges (M-U5 gate):** how does the world open — one big contiguous traversable
space, or discrete regions you travel between? And how *big* should "big" feel —
finite-large-that-reads-endless, or genuinely open? (Recommend finite-large that *reads*
endless; truly-unbounded is a much larger technical bet.)

### M-U6 (PROPOSAL) — "Growth: the survivor becomes more"

**Shape (proposed):** the journey-arc's "humble-start grows into more" beat, survival-cast —
the castaway gains *capability* over the journey (better tools, more carry, survival skill /
upgrades), so reaching a farther region *means* something. Thin or deep is the Sponsor's call.

- A **progression spine** so the farther regions feel earned, not just farther. Re-implements
  the *idea* of the Godot leveling/gear pillar (Sponsor hard-requirement: progress through
  levels & gear, fighting harder challenges) in survival terms — better axe → harder wood →
  colder regions survivable.
- **Sponsor judges (M-U6 gate):** how deep is progression — a light tool-tier ladder, or a
  full skill/gear system? And where does it slot — felt by the first journey region, or later?
  (Recommend at least *thin* growth felt by the first new region, so the journey reads as
  earned.)

### M-U7+ (PROPOSAL, FAR) — "The wide world + the horizon as destination"

**Shape (proposed, far-out):** more survival regions (the "and more" of the journey arc),
each a different survival problem; the far horizon becomes an actual *place* the journey
builds toward — a destination, not a backdrop. This is the content spine, and it is
**explicitly the most speculative** part of this doc.

- Deferred-and-named so it's *visible*, not built: multiple further regions; ambient
  aliveness (animals, weather systems, discoverable treasures per the journey-arc spirit);
  vistas/landscapes/distance (the biggest rendering lift — flag as a Sponsor-gated, possibly
  separate, bet); any narrative frame for *why* the castaway journeys.
- **Re-implement-when-needed (design-reference, NOT carried code):** combat (Godot
  combat-architecture.md) *if* the survival world wants threats; save/load (save-architecture.md)
  when the journey needs persistence across sessions; quest/dialogue (quest-system.md,
  dialogue-system.md) *if* the world wants NPCs/goals. All are C# re-implementations the
  *day the loop needs them*, sourced from the Godot design docs as "what worked + why."

**Sponsor judges (M-U7+ gate):** the entire content spine — how many regions, how alive, does
the horizon get a narrative destination, does the survival world grow threats (combat). None
of this is shaped until M-U5's "how the world opens" answer exists; it's drawn here only so the
Sponsor can see the whole road from the start.

---

## §5 — Cross-cutting threads (apply across every milestone, firm + proposed)

These aren't milestones — they're disciplines that ride every milestone above.

- **World-feel north-star.** Every survival system is measured against *small player / big
  alive world / worth-reaching horizon* (memory `world-feel-big-and-endless`,
  `game-world-journey-arc`). A mechanic that adds management but not *aliveness* doesn't earn
  its place.
- **Art-direction gate.** Any visual surface (a campfire, a tree, a shelter, a region,
  a landmark) goes through the art-board (`inspiration/*.png`) + the `game-art` skill +
  Uma — fine multi-tone worn materials, human-scale landmarks, lush *purposeful* decoration,
  warm cohesive palette. The campfire in M-U2 sets the bar the whole world must meet.
- **Shipped-build capture gate.** Every UX-visible beat — firm or proposed — is verified in
  the *built exe* with a HUD build-stamp, not just editor/EditMode (the spike's hard-won
  "editor-green ≠ build-correct" lesson; `.claude/docs/unity-conventions.md`).
- **Thin-first, layer-on-proof.** The M-U2 discipline generalizes: every milestone ships the
  *thinnest* version that proves the idea fun, and layers depth only on a Sponsor "this works"
  verdict. ≥2 rejections on the same surface = the *approach* is wrong, not the tuning
  (outcome-over-motion).
- **Re-implement, don't carry.** Godot M1/M2/M3 systems (combat, loot, save, quest, dialogue,
  camera, procgen) are a **design library** — what worked and why — re-implemented in C# the
  day a milestone needs them. They are NOT a codebase that travels with us.

---

## §6 — What this roadmap is NOT (scope honesty)

- **Not the thin-loop implementation.** The M-U2 build tickets (U2-1..U2-7) are the re-scope
  §4 list; this doc gives them their *macro home*, it doesn't re-spec them.
- **Not a commitment past M-U2.** M-U3 onward are proposals. The Sponsor shapes order + depth.
- **Not a content spine.** §4's regions/progression/horizon are an *outline* of the road, not
  a backlog. Each milestone gets its own dispatch-ready backlog *when the Sponsor shapes it*.
- **Not a carry of Godot systems as code.** Combat/save/quest/dialogue are design-reference
  for re-implementation, never code-salvage (re-scope §2).

---

## §7 — Sponsor decision surface (what shapes the road)

Surfaced for Sponsor input — this is a design-partnership doc. None of these block M-U1 or
M-U2 (both are firm/Sponsor-locked); they shape the *proposed* road past M-U2.

1. **(Gates M-U3)** Which is the *one* need for the thin loop, and which deepening additions
   come first — second need, food, or day/night? Any subset is a valid M-U3.
2. **(Gates M-U4 — a real fork)** Base-building survival (a place that's yours) vs.
   nomadic-journey survival (the journey IS the survival)? This forks the whole shape of §4.
3. **(Gates M-U5)** How does the world open — one contiguous traversable space, or discrete
   regions? And how big should "big" feel? *(Recommend finite-large that reads endless.)*
4. **(Gates M-U6)** How deep is progression, and where does it slot? *(Recommend at least thin
   growth felt by the first journey region.)*
5. **(Gates M-U7+)** The content spine — how many regions, how alive, does the survival world
   grow threats (combat), does the horizon get a narrative destination?

**The one durable ask:** confirm the macro arc itself — *start small → thin survival loop →
deepen → journey toward the far horizon* — matches the vision. If the arc is right, the
milestones can be shaped one at a time as each prior one proves out.

---

## Cross-references

- **Re-scope (the plan of record):** RandomGame `team/priya-pl/unity-migration-rescope-2026-06-12.md`
  — §3 (PR #422 disposition), §4 (M-U1 + M-U2 backlogs, the U2-1..U2-7 thin-loop tickets this
  roadmap's §2 traces to).
- **Founding decisions:** `team/DECISIONS.md` (2026-06-12 entries) + RandomGame ticket
  `86ca85ttd` (the 7-decision Sponsor walkthrough — authoritative source for M-U2's lock).
- **Closed predecessor:** RandomGame PR #422 (`journey-arc-roadmap.md`, Godot-ARPG — CLOSED
  2026-06-12; macro spirit carries, ARPG mapping does not).
- **Art north-star:** `.claude/docs/art-direction.md` + `inspiration/*.png` (look at the images).
- **Ported baseline + engine facts:** `.claude/docs/unity-conventions.md`, `CLAUDE.md`,
  RESUME.md (M-U1 ticket map U1–U10); the read-only spike `c:/Trunk/PRIVATE/EmbergraveUnitySlice`.
- **Memory:** `world-feel-big-and-endless`, `game-world-journey-arc`,
  `player-char-castaway-vision`, `sponsor-direction-shift-poe-camera-unity`.
