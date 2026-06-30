# Open Blue-Water Horizon — Visual-Direction DECISION SPEC (exploratory)

**Ticket:** `86cafffe8` · **Author surface:** Uma (UX / Visual Direction) · **Status:** EXPLORATORY decision-support spec — the Sponsor has **NOT** committed to removing the horizon mountains. This doc lays out 2–3 options + trade-offs for him to judge in a vision review. **Nothing here is a decided scope cut.** No code, no build, no merge to `main`.

---

## The real-world sentence (read this first)

> **You are a small castaway alone on a little island in the middle of a huge ocean. You turn all the way around and there is nothing but blue water meeting the bright sky in every direction — no land in sight. Somewhere out there is another island, but, just like in real life, you cannot see it until you have sailed close enough that it rises over the edge of the world.**

That is the feeling this ticket is chasing. It comes verbatim from the Sponsor's own two prompts:

- **This ticket (Obsidian, verbatim):** *"there should not be nearby mountains. Instead the player should see a horizon of blue water, feeling lost in the middle of the ocean on a little island. … It should not be possible to see the next big island before you get near in the boat (just like in real life)."*
- **The next-island POC (`86caa9zpp`, verbatim):** *"a mountain, not like the ones you drew in the horizon, but an actual giant hill … **if this works we could get rid of the chunky horizon mountains.**"*

So this is not a fresh idea dropped on the team — it is the Sponsor circling the same instinct twice, from two angles. The question this spec answers is: *what does "lost at sea, open blue-water horizon" look like for our chunky low-poly Zone-D world, and what would it cost to get there?*

### Reference imagery (ground truth — viewed for this spec)

The board doesn't have a literal open-ocean shot (it's a land-vista board), so I read the two images that pin the **technique** the open-horizon look depends on — **atmospheric fade** — plus the current water look:

- **`inspiration/2026-06-12_21h13_31.png`** (rolling grassland feel shot) — this is the load-bearing reference. The distant trees and faint hills **dissolve into a soft pale haze** at the back of the frame; nothing reads as a hard edge or a wall. That exact "far stuff melts into the bright sky" treatment IS the open-horizon dissolve and IS the next-island occlusion-reveal. We already know how to do this — it's the same fog/grading dissolve specced in `world-look-polish-direction.md` §3, just pointed at empty water instead of mountains.
- **`inspiration/2026-06-12_21h16_52.png`** (lake-cabin) — the canonical water look: a flat bright saturated-teal sheet meeting a faceted shoreline. Our ocean is this material; the open horizon is this teal sheet extended to where it dissolves into the sky.
- **`inspiration/2026-06-12_21h16_13.png`** (mountain-valley vista) — included as the **counter-reference**: this is the chunky-horizon-mountain look the Sponsor is now questioning. Worth keeping on screen during the review so the trade-off is concrete (this is what we'd be giving up).

> **Anchor discipline (`[[physical-features-anchor-realworld-not-metric]]`):** the gate for every option below is "does it read like *standing on a little island lost in a huge ocean* to the human eye on the first look" — NOT a fog-density number. The metrics in this spec are recommended starting points the impl + Sponsor soak dial; the real test is the feeling.

---

## What ships today (the thing we'd be changing)

Per `[[world-is-big-round-island]]` (seed 42, **LOCKED**) and `world-look-polish-direction.md`:

- The start island is a big organic procedural island with water on **all** sides — **this is "close to perfect" and must not regress.** Nothing in any option below touches island generation (`LowPolyZoneGen` radial heightmap, seed 42).
- **Horizon mountains exist as a deliberate, Sponsor-approved surface.** `world-look-polish-direction.md` §2 calls the far-horizon vista *"the north-star surface"* — 2–3 overlapping faceted grey-to-snow mountain ranges on separate distant landmasses, fading into the sky for a "the world is bigger than where you stand, it goes on" read. That brief's whole §2 is built around those mountains being a **destination you can see but not yet touch.**

**The tension this ticket surfaces, stated plainly:** the world-look-polish brief made the visible far mountains *the* payoff of bigness. This ticket proposes the opposite payoff — bigness through **emptiness and isolation** (you can't see anything, and that's the point). Both are legitimate "big world" feelings; they're just different feelings. The Sponsor is the only one who can pick which one this game wants. That's why this is a vision review, not a team call.

Two distinct surfaces are in play and they can be decided independently:

1. **The horizon mountains** (the chunky grey-to-snow ranges on the rim). These are the thing the Sponsor named twice.
2. **The next-island occlusion-reveal** (the next big island stays invisible until the boat gets near). This is a *new mechanic* regardless of which mountain option is chosen — see the dedicated section after the options.

---

## The three options

Each option = how the world reads, the small-player/big-alive-world north-star impact, and trade-offs vs. today's mountains-on-separate-islands look.

### Option A — Full open ocean (remove the horizon mountains entirely) — **RECOMMENDED**

**How the world reads:** stand anywhere on the start island, orbit the camera 360°, and every direction is the same: bright teal ocean running out to a clean warm horizon line where the water dissolves into the gradient sky. No land anywhere on the rim. The island feels genuinely alone in a huge sea. The "lost at sea" feeling the Sponsor described arrives instantly and completely — there is literally nothing else out there.

**North-star impact:** this is the *strongest* delivery of the Sponsor's stated feeling. Bigness here comes from **emptiness** — the world reads endless because you can see how far the nothing goes. It also makes the eventual next-island reveal land much harder: when you've spent the early game seeing only empty water, the first sight of a real island rising over the horizon from the boat is a genuine event. Emptiness now buys payoff later.

**What changes / what stays:**
- *Changes:* the distant-landmass / horizon-mountain surface from `world-look-polish-direction.md` §2 is **removed** (the far mesh ranges OR baked horizon band, whichever was built). The fog/grading dissolve (§3) **stays and does more work** — it's now the ocean-into-sky dissolve instead of the mountain-into-sky dissolve.
- *Stays untouched:* seed-42 island generation; the start-island shore, water, foam, trees, grass; the gradient skybox; the clouds (§1 — the cheerful cyan puffs are *more* valuable here, they keep an empty sky alive); the warm-bright sky-tint.

**Trade-offs vs. today's mountains:**
- **(−)** Loses the `21h16_13` "see a destination on the horizon" payoff that §2 was built around — the rim goes from "there's somewhere to go" to "you're alone." (This is the deliberate point, but it IS a loss of one specific charm.)
- **(−)** An empty horizon is harder to make *beautiful* than a mountain-studded one. A flat teal line under a flat sky can read boring/cheap if the dissolve isn't gorgeous. The clouds, a gentle horizon glow, and a subtle near→far water-color gradient have to carry the visual interest the mountains used to. **This is the real risk of Option A and the thing the soak must judge.**
- **(+)** Simplest world to reason about; removes the "are the distant mountains an island I can reach?" ambiguity (today they're decoration you can never visit, which can quietly frustrate a curious player).
- **(+)** Cheapest to maintain and the cleanest seam for the boat/next-island arc.

**Build cost:** **S.** Mostly subtraction — remove/disable the far-landmass surface + re-point the existing fog dissolve at the ocean. The hard part is *art polish on the empty horizon* (the dissolve quality), not engineering.

---

### Option B — Reduce and distance (keep faint mountains, push them to the absolute rim)

**How the world reads:** mostly open ocean, but on a clear orbit toward the horizon there's the faintest suggestion of land — one or two extremely faded, low, distant silhouettes barely separable from the sky, far smaller and fainter than today's chunky ranges. Not "mountains in your face," more "is that… something, way out there?" The island still feels fairly isolated, but the world isn't *empty*.

**North-star impact:** a middle path — keeps a whisper of "there's more world out there" without the bold chunky-mountain wall the Sponsor reacted against. Bigness comes from **depth + mystery** rather than pure emptiness. Weaker "lost at sea" than A (you're not truly alone), but more visual interest on the horizon.

**What changes / what stays:**
- *Changes:* the §2 far ranges are **kept but heavily reduced** — fewer, lower, much fainter (push the farthest-range tint almost all the way to the sky stop so they're 80–90% dissolved), and moved well out so they're tiny. Effectively dial §2 from "the north-star surface" down to "a faint rim hint."
- *Stays:* everything Option A keeps, plus a vestige of the mountain surface.

**Trade-offs vs. today's mountains:**
- **(−)** Risks satisfying neither goal: not empty enough to feel truly lost-at-sea, not bold enough to be the `21h16_13` destination payoff. Middle paths on a feel call often read as "didn't commit."
- **(−)** Directly contradicts the Sponsor's own "there should **not** be nearby mountains" / "get rid of the chunky horizon mountains" — even faint ones are still *some* mountains. Only choose this if, on reflection, he finds a fully empty horizon too plain.
- **(+)** Hedges: if a fully empty horizon turns out to read cheap (Option A's risk), a faint rim hint is the cheapest insurance and keeps visual depth.
- **(+)** Preserves the option to bring mountains back later without re-architecting.

**Build cost:** **S.** Tune existing §2 dials down + reposition; no new systems.

---

### Option C — Keep but reframe (mountains stay; lean the framing/spawn toward open water)

**How the world reads:** essentially today's world — the chunky horizon mountains remain as the `21h16_13` destination — but the default camera framing and the castaway's spawn orientation are tuned so the player's *first* and *default* view is the open sea, with the mountains only found by deliberately orbiting toward them. The "lost at sea" feeling is delivered by **composition** rather than by removing anything.

**North-star impact:** keeps the approved §2 payoff fully intact while nodding at the open-water feeling. But it's the **weakest** delivery of the Sponsor's stated ask — the mountains are still right there the moment you orbit, so you're never actually "lost in the middle of the ocean with no land in sight." This option essentially declines the ticket's premise and keeps the status quo with a framing tweak.

**What changes / what stays:** only camera-default + spawn-orientation tuning. The entire §2 mountain surface stays exactly as approved.

**Trade-offs vs. today's mountains:**
- **(−)** Does not actually deliver the ticket. The Sponsor said "no land in sight" twice; this keeps land one orbit-drag away.
- **(+)** Zero risk to the approved look; zero new work; fully reversible in an afternoon.
- **(+)** Sensible *only* as a "not yet — let's keep the mountains for now and revisit when the next-island POC lands" answer.

**Build cost:** **XS.** Framing/spawn tune only.

---

## My recommendation

**Option A — full open ocean.** Reasoning, in order:

1. **It's what the Sponsor actually described, twice, in his own words.** "No nearby mountains," "feeling lost in the middle of the ocean," "no land in sight," "get rid of the chunky horizon mountains." Options B and C each soften that to avoid losing the `21h16_13` payoff — but the Sponsor is the one telling us he's willing to lose it. The honest read of his prompts is A.
2. **It makes the whole next-island arc pay off.** The boat + giant-real-mountain island (`86caa9zpp`/`86caa9zju`) is the headline journey. If the horizon is empty for the entire start-island experience, the first real island rising over the edge from the boat becomes the single best moment in the game. Mountains-on-the-rim-now spends that payoff early and cheaply.
3. **It's cheap and low-risk to the locked world** — it's subtraction plus re-pointing an existing dissolve; seed-42 generation is never touched.

**The one caveat I'd flag honestly:** A's success rides entirely on the *empty horizon being beautiful, not boring.* That is a subjective-feel call only the Sponsor's eye settles in a soak. So my real recommendation is: **commit to A, but treat the empty-horizon polish (the water-into-sky dissolve, a soft horizon glow, the cloud density keeping the sky alive) as the thing the impl ticket's soak must prove.** If the first soak reads cheap, **B is the pre-planned fallback** (add the faintest rim hint back) — not a redesign, just turning two dials. That sequencing gives the Sponsor the bold version first with a cheap safety net.

> **This is a subjective-feel call and therefore the Sponsor's, not the team's** (orchestrator-autonomy never-auto-decide list). The recommendation above is direction; the pick is his in the vision review.

---

## The next-island occlusion-reveal mechanic (needed under A or B; the heart of the ticket's second half)

Regardless of the mountain option, the Sponsor wants the next big island **invisible until the boat gets near, then it rises into view — just like real life.** This is a new mechanic and it's the seam that makes the `86caa9zpp` next-island POC and the `86caa9zju` boat POC actually land. Three approaches:

### Reveal Approach 1 — Distance fog wall (the island sits beyond the fog limit until you sail toward it) — **RECOMMENDED**

The island is always physically present in the world, but the global distance fog (the same warm dissolve from `world-look-polish-direction.md` §3) is tuned so that at the start-island's distance the next island is **fully dissolved into the horizon haze** — indistinguishable from empty sky/sea. As the boat closes the distance, the island crosses out of the fog band and **fades up naturally** — first a faint silhouette, then color, then detail. No pop-in, no scripted trigger; the fog does it for free and it reads exactly like real-life atmospheric horizon occlusion.

- **(+)** Reuses the existing fog dissolve — same system that does the open-horizon look in Option A. One technique serves both halves of this ticket.
- **(+)** Smooth, automatic, framerate-cheap, no per-frame logic, no scripted reveal moment to get wrong.
- **(+)** Bidirectional and continuous — works the same whether you're sailing out or back, at any approach angle.
- **(−)** Real curvature-of-the-earth occlusion (island hidden *below* the horizon line, then its peak appears first) is NOT modeled — the island fades in *through haze* rather than rising *over a curve*. For our toy-diorama scale this reads correct and nobody will miss the geometric curvature; flag it only so we're honest about what "just like real life" means here (atmospheric haze, not planetary curvature).
- **Recommended starting values (impl + Sponsor-soak tunes):** next island placed at **~600–900u** from the start island; fog far-distance tuned so anything past **~400–500u** is ≥95% dissolved; the island becomes a faint silhouette around **~350–400u** of boat approach and is clearly readable by **~200u**. *Defaults — the "near by boat" reveal distance is a feel value the Sponsor dials in the soak.*

### Reveal Approach 2 — Draw-distance / streaming reveal (the island literally isn't rendered until you're close)

The island geometry is culled/unloaded beyond a radius and snaps/streams in when the boat enters range.

- **(+)** Can stream the giant 10-minute island in/out for perf — genuinely useful for a `86caa9zpp`-scale landmass.
- **(−)** Without a fade it **pops** — an island appearing from nothing is the opposite of "just like real life." Must be paired with a fade band anyway, at which point Approach 1 is doing the visible work regardless.
- **(−)** More engineering (culling/streaming logic) than fog tuning.
- **Verdict:** valuable as a *perf companion* to Approach 1 for the huge island, **not** as the visible reveal mechanic on its own.

### Reveal Approach 3 — Scripted fade-in band (a trigger volume fades the island's material alpha as the boat crosses a ring)

A distance-ring trigger drives the island's material opacity from 0→1.

- **(+)** Precise authored control over exactly where/how fast the reveal happens.
- **(−)** Bespoke per-island scripting; alpha-fading a whole opaque landmass is a transparency/sorting headache in URP and fights the flat-shaded toy materials; reveal feel has to be hand-authored instead of falling out of the world physics.
- **Verdict:** over-engineered for the need; only reach for it if the Sponsor wants a *dramatic authored* reveal moment (a specific "and there it is" beat) rather than a natural one.

**Reveal recommendation: Approach 1 (distance fog wall), optionally backed by Approach 2's culling purely for perf on the giant island.** It's the same dissolve that powers the open horizon, it's automatic and cheap, and it reads as real-life horizon haze with zero scripted seams. Approach 3 only if the Sponsor specifically wants a hand-authored dramatic reveal.

> **Seam to the POCs:** the `86caa9zpp` next-island POC builds the island; the `86caa9zju` boat POC moves the player toward it; **this reveal mechanic is what makes that journey feel like a discovery instead of a teleport.** Whoever picks up the next-island impl should wire Approach 1's fog band as part of placing the island, and the boat POC's sail distance should be set so the reveal lands at a satisfying point in the crossing (the ~200–400u readable band above). Cross-referenced so the impl tickets inherit this.

---

## What must NOT regress (hard constraint)

- **Seed-42 island generation is LOCKED** (`[[world-is-big-round-island]]`). No option touches `LowPolyZoneGen` radial heightmap / island shape / coast / scatter. This spec only changes what is **visible on the horizon FROM** the island (the far mountains) and the **fog/skybox framing** — never the island itself.
- **The start-island shore, water, foam, trees, grass, gradient sky, clouds, and warm-bright sky-tint all stay** as shipped/approved. Option A *removes* the far-landmass surface and *re-points* the existing fog; it adds nothing to the island.
- **Don't re-break the serialized post stack** — the fog re-tune is a DIAL on the already-correct `VolumeProfile` (`unity-conventions.md` §Editor-vs-runtime, `VolumeProfile.Add<T>`), not a re-add.
- **Honor the warm-bright anchor** — an empty horizon must still read as a *bright nice day at sea*, not a cold grey overcast void. The warm horizon sky-stop (`#DCE8E4`, `world-look-polish-direction.md` §3) and fog tint stay warm; a cold empty horizon would flip "hopeful castaway" into "bleak shipwreck-horror" and break the locked tone.

---

## How the impl follow-up gets judged (carries to the impl ticket)

This spec makes the impl ticket dispatch-ready. When that ticket is spun:

- **Predict-Before-Soak (Option A):** *"From the start island, the player can orbit the camera a full 360° and see only open blue water dissolving into a warm bright sky in every direction — no land, no mountains, anywhere on the horizon. The empty horizon reads as a bright calm sea, not a void."*
- **Predict-Before-Soak (reveal, when next-island lands):** *"Sailing from the start island, the next island is invisible (fully dissolved in horizon haze) until the boat is within ~350–400u, then fades up naturally — faint silhouette → color → detail — with no pop-in, by ~200u clearly readable."*
- **Name the bar** (`/name-the-bar`) at impl-spin time if not yet confirmed: the *open-horizon / lost-at-sea* feel bar.
- **Bounded convergence:** the soak tests the open-horizon feel + (if next-island is in) the reveal feel; it does NOT test island generation, water material, or boat handling (separate scope).
- **Capture gate:** shipped-exe orbit captures (gameplay cam, real post stack) at the default pitch AND a full orbit — the empty horizon and the warm dissolve must show in the BUILT exe, not just the editor (`unity-conventions.md` §Editor-vs-runtime false-green-capture class).
- **QA / soak:** with Tess as the consistency-pin reviewer (vs. board + HDR-clamp sub-1.0 swatches) and the Sponsor soak as the feel gate (`[[qa-gate-when-tess-unavailable]]` if Tess is out: the cross-reviewer absorbs the QA checklist + the Sponsor soak is the gate).

---

## Sponsor-input items (vision review)

1. **THE call — pick the horizon direction: A (full open ocean, recommended) / B (faint rim hint) / C (keep mountains, reframe) — or veto the whole change and keep today's mountains.** This is a subjective-feel call and entirely the Sponsor's. My recommendation is A with B as a pre-planned soak fallback.
2. **Reveal feel — natural (Approach 1 fog, recommended) or a dramatic authored "and there it is" moment (Approach 3)?** Default if unanswered: natural fog reveal.
3. **The empty-horizon polish risk — accept that A's success rides on the dissolve being beautiful (with B as the cheap fallback if the first soak reads plain)?** Confirms the A→B safety-net sequencing.
4. **Reveal distance ("near by boat") is a feel value** the Sponsor dials in the soak once the next-island + boat POCs exist; the ~200–400u starting band is a recommendation, not a lock.

---

## Cross-references

- **Ticket** `86cafffe8` (this spec). Source: Obsidian prompt, imported 2026-06-28.
- **`86caa9zpp`** (next-island POC — the giant real-mountain island) + **`86caa9zju`** (boat POC). Same source vision ("if this works we could get rid of the chunky horizon mountains"); the reveal mechanic here is the seam that makes both POCs land. The impl ticket spun from this spec should wire Reveal Approach 1's fog band into the next-island placement.
- **`team/uma-ux/world-look-polish-direction.md`** (`86ca8rd6a`) — the brief this spec proposes to partially *reverse*: its §2 far-horizon-vista mountains are exactly what Options A/B remove/reduce; its §3 fog/sky dissolve is the technique all options + the reveal reuse. Read §2 + §3 alongside this doc in the review.
- **`team/uma-ux/beach-water-direction.md`** — the ocean look (flat bright teal sheet, smooth, sub-1.0) the open horizon extends to the dissolve; the orbit-to-horizon capture-gate convention.
- **`.claude/docs/art-direction.md`** + **`inspiration/`** board v2 — ground truth viewed for this spec: `21h13_31` (the atmospheric-fade / dissolve technique = the open-horizon + reveal look), `21h16_52` (the teal-water look), `21h16_13` (the counter-reference: the chunky horizon mountains being questioned).
- **Memory:** `[[world-is-big-round-island]]` (seed-42 LOCKED, mountains-on-separate-islands = the status quo being questioned), `[[physical-features-anchor-realworld-not-metric]]` (real-world sentence + reference imagery first), `[[qa-gate-when-tess-unavailable]]`.
- **`.claude/docs/unity-conventions.md`** — §Editor-vs-runtime (`VolumeProfile.Add<T>` — re-tune fog dials, don't re-add; false-green-capture class for the soak), §Headless rituals (serve_soak / build-stamp gate for the impl).
- **Sky work** `86cabc743` / PR #194 (sun-disk) — the open horizon shares the gradient-sky + sun framing; the impl should compose with it (a sun low over an empty sea is the single best beat available to make Option A's horizon beautiful).
