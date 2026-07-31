# Quality bars — Far Horizon (Sponsor-confirmed)

The Sponsor's **standing quality bars** — the things he holds the game to that aren't
derivable from a ticket or the art board. This file converts the *reactive* taste-memory
(bars learned after a soak-reject) into a *proactive* artifact the orchestrator reads BEFORE a
taste-sensitive dispatch, so the bar is named up front. Maintained by the `/name-the-bar` skill;
referenced by `team/TESTING_BAR.md` § Predict-Before-Soak.

> **Seed provenance:** the rows below are derived from the project memory index (`MEMORY.md`)
> — each cites the memory slug it came from. They were learned reactively over the project's
> life; this file is where future bars get *confirmed up front* instead. Treat each as
> Sponsor-confirmed unless a later soak corrects it (then update the row + the cited memory).

## How to use
- **Before a feel/visual/first-of-class dispatch:** find the bar(s) that apply to the surface,
  paste them into the dispatch brief, and predict against them in the Self-Test Report.
- **When a soak corrects a bar:** update the row here AND the cited memory; note the date.
- **Row shape:** `Bar` — the one-line standard | `Surfaces` — where it applies | `Source` — memory slug / soak date.

## Bars

| # | Bar (the standard) | Surfaces | Source |
|---|---|---|---|
| 1 | World and water read as **organic / irregular**, never geometric — varied coast, irregular pond outline, faceted low-poly. Seed 42 is LOCKED. | island coast, pond, water features | `[[world-is-big-round-island]]`, `[[pond-organic-not-round]]` |
| 2 | Motion defaults **lively / animated**, only lightly damped — axe FOLLOWS the arm, water has MOVING waves, foam PULSES. Don't lock it static. | character, water, foam, props | `[[sponsor-prefers-natural-lively-motion]]` |
| 3 | A surface reads as its **MATERIAL** (stone→flint, metal→steel) — **no arbitrary colors** (a red axe-head was rejected); surface PATTERN is modeled low-poly facets, NOT a detail texture (preserves the shared-palette ~1-draw-call). | weapons, tools, props | `[[weapon-asset-material-honest-pattern-via-geometry]]` |
| 4 | Physical-world features must **look like the real thing on the FIRST try** — open the task with a plain real-world sentence, ship a side-profile capture, fix the CAUSE not the metric, never chase a number into nonsense (the "pond-in-a-mound"). | pond, fire, hill, terrain features | `[[physical-features-anchor-realworld-not-metric]]` |
| 5 | In-hand size/feel is judged **IN-HAND via a discrete mesh-swap picker** — never a bare render and never a broken continuous dial. Bake-and-judge when the team can't verify a dial before serving. | weapon/tool sizing | `[[verify-soak-builds-or-bake-and-judge]]` |
| 6 | The art-direction **board is a GUIDE, not a contract** — a divergence the Sponsor has already praised (e.g. the rustic axe) is NOT a defect to "fix" back to the board. | all visual work | `[[sponsor-taste-overrides-art-direction-board]]` |
| 7 | Every system is designed with **3 difficulty tiers** (easy / medium / hard), kid-friendly → adult-challenging. | needs, enemies, combat, survival | `[[difficulty-settings-easy-medium-hard]]` |
| 8 | When a spatial/visual tweak stalls (~2 soak-rejects), give the Sponsor a **direct-tweak instrument** (nudge tool / slider / discrete picker) so he dials it himself, then bake the values — don't grind blind iterations. | any fiddly placement/sizing | `[[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]`, composes with `/unstick` |
| 9 | A weapon-vs-mob matchup reads as **EMERGENT, not scripted** — the "right tool" (e.g. spear-beats-boar) is LEGIBLE to the player from two independent systemic facts (the weapon's REACH + the mob's damage-type WEAKNESS tag), with NO hardcoded weapon×mob matchup table; the weaker tool stays usable (worse, not blocked). Confirmed emergently at the boar soak (reach + pierce-tag, zero table). | enemies, weapons, combat matchups | boar soak PASS 2026-07-22 (`86cah7ydt` AC8b, PR #332, DECISIONS 2026-07-22) |
| 10 | **No cue may rest on a SINGLE channel.** Colour-only is the most common way it fails, but motion-only fails identically. Every HUD / world readout / attract cue must be identifiable on **≥2 channels, at least one of them independent of hue** — **FORM** first (segment count, silhouette, size), then **POSITION** (a fixed slot per kind; an inactive kind leaves its slot EMPTY, never packed, so "the third slot is lit" is itself the read), then **MOTION**; colour ranks LAST of the four, never first, and text is a last-resort fallback. **A "channel" is a property that DIFFERS between an instance in the cued state and one that is not** (clause added 2026-07-31) — a property present on **every** instance is **style, not a cue**: it answers "what KIND of thing is this", never "WHICH one is cued", so it contributes nothing to the ≥2 however well it reads. **Variance is the precondition for being a channel at all**; the FORM → POSITION → MOTION → colour ranking orders **read-speed among channels that already pass it**, and never admits one that doesn't. **Name the ≥2 channels the cue rides on, and verify each is actually LIVE on the shipped material/shader** — a shader property the assigned shader does not declare is a silent no-op that collapses the cue to one channel with no error. **Two checks, both required.** (1) **Invariance** — for each named channel, say out loud what that channel looks like on a NON-cued instance of the same kind. If the answer is "the same", it is not a channel: strike it and re-count. (2) **Desaturate** the shipped-build capture; if the cue is gone, it failed. WHY: the world is saturated mid-green and will happily eat a hue cue; form survives a colour-blind player and reads faster at peripheral glance; a cue that silently loses a channel degrades with nothing anywhere reporting it; and an always-on channel is indistinguishable from the material it is painted on — the player's question is never "is this a sword", it is "is THIS sword the one I can take". | HUD bars, status chips, world-anchored readouts, **attract / affordance cues on world objects (rim, glow, outline)**, any new UI element | Sponsor decision 2026-07-27 (HP bar = 5 chunky segments over 10 thin ones — form over colour; `86cah7z2q` AC1) + Uma's three-channel rule ratified via PR #339 `e13a51e`. **Provenance note:** ratified by a Sponsor pick + a merged spec, not yet by a shipped soak — re-confirm or correct at the first HUD soak that exercises it. **Second motivating instance (Devon, PR #349 review 2026-07-27):** a find-in-world attract cue lost its Fresnel rim and now rests on motion alone — the earlier colour-only wording of this bar would have PASSED it, which is why the invariant is single-channel collapse, not colour-only. Mechanism he verified: `_RimIntensity` is declared on exactly one shader (`Assets/Shaders/LowPolyVertexColor.shader:79` property / `:162` CBUFFER / `:323` use) and every setter is `HasProperty`-guarded (`LowPolyZoneGen.cs:1937`), so assigning it to a material whose shader does not declare it is a silent no-op. **Third motivating instance (PR #379 + PR #351 review, 2026-07-31) — and the reason the "channel" definition above exists:** the plain **≥2-channels** wording passed **motion + invariant-form** for exactly the reason the older colour-only wording had passed motion-only — it counted channel *types*, not channel *information*. #351's find-in-world attract cue rides float-bob + sway, which are both MOTION (one channel), so a second was needed; the orchestrator and Uma independently proposed §3's white edge-highlight plane as the FORM channel, and Drew declined it. His reason IS the clause: the plane is genuinely fork-free (`EdgeWhite #F5F5F0` is a slot on the shared `weapon_palette.png` and §3 UVs the inset strip to it — same material, no fork), **but** `.claude/docs/blender-asset-pipeline.md` §11's sign-off checklist mandates it on **every blade** ("White edge-highlight plane exists on every blade") and §2's palette row scopes it to **all weapons** ("Blade edge-highlight plane (all weapons)") — both re-verified against `origin/main` @ `e054aa7` (`:377` and `:58`; §3 rule at `:94`; PR #379 inserts +28 lines into §2, so the latter two shift once it merges — cite the § anchors, not the line numbers). Invariant across the whole set ⇒ it cannot answer *"which of these three swords is the pickable one?"* ⇒ the cue stays collapsed on MOTION by a different route. Hence **a channel that is always on is not a cue** — it passes "is it fork-free?" and passes "is it FORM?" and still fails. **Ranking NOT changed:** FORM → POSITION → MOTION → colour orders read-speed, which is orthogonal to variance; the fix belonged in what qualifies as a channel, not in how qualifying channels rank. **Provenance:** source-verified from the docs + the #351 review, not soak-confirmed — the re-served #351 cue is the first soak that exercises it. |

## Open / unconfirmed (drop new inferences here for the next `/name-the-bar` pass)

- **Candidate — interactive-vs-scenery must be readable by POSTURE.** Two world objects that share a
  material family must not share a *posture*. If one carries a verb and the other does not, the
  non-interactive one changes **aspect ratio** — it crosses from taller-than-wide to **wider-than-tall** and
  stays there on **every instance**; the interactive one stands up. **State the cue as a categorical
  inversion, never as a size ratio:** on a procedurally-jittered mesh a height ratio is a *nominal* that
  collapses at the tail (`86cav8ybj` §2.3 — a claimed ≥2× floor re-derived to **1.3× worst-case** once the
  per-instance `sy` and per-vertex `rj` jitter are modelled), whereas an aspect inversion holds at every draw
  and is cheap to assert per instance. **The class that changes is always the
  one with no gameplay contract attached** (no verb, no yield, no navmesh carve, no timer, no capture
  harness) — never the hero prop. **Check: desaturate the shipped-build capture and ask "point at the ones
  you can use"; and gate CI on the measured worst-case aspect, not on a derived constant.** WHY: the mine
  gate can be perfectly correct and the world still invite dead-clicks; a
  shared-palette style deliberately removes hue as a discriminator, so posture is the only channel that
  scales across a whole prop family. **Surfaces:** decorative-vs-interactive prop pairs (scatter rock vs
  minable boulder/ore node; future: driftwood vs choppable log, bush vs berry bush).
  **Source:** ticket `86cav8ybj` direction spec `team/uma-ux/rock-affordance-direction.md` §9; composes with
  Bar 10 (this is Bar 10's FORM rank applied to world props) and Bar 3 (material-honest → hue is unavailable
  as a discriminator by construction). **Provenance:** derived from a source audit, NOT yet soak-confirmed —
  the affordance impl half of `86cav8ybj` is the soak that confirms or corrects it.
