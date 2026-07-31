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
- **When WRITING or AMENDING a bar — state what its check returns on an instance that should FAIL it.** A check that runs only on the passing case measures **presence, not discrimination**: it cannot catch a thing that is technically present and useless. This is the two-sidedness the project already demands of its test gates (PR #363 made `assert_launch_windowed` **red** on an added `-batchmode`, not merely green on a good launch) — bar #10 was written and then amended twice without anyone asking it of the bar itself, which is how three evasions survived to review (`86caz5na6`).
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
| 10 | **No cue may rest on a SINGLE channel.** Colour-only is the most common way it fails, but motion-only fails identically. Every HUD / world readout / attract cue must be identifiable on **≥2 channels, at least one of them independent of hue** — **FORM** first (segment count, silhouette, size), then **POSITION** (a fixed slot per kind; an inactive kind leaves its slot EMPTY, never packed, so "the third slot is lit" is itself the read), then **MOTION**; colour ranks LAST of the four, never first, and text is a last-resort fallback. **A "channel" is a property that DIFFERS between an instance in the cued state and one that is not** (clause added 2026-07-31) — a property present on **every** instance is **style, not a cue**: it answers "what KIND of thing is this", never "WHICH one is cued", so it contributes nothing to the ≥2 however well it reads. **Variance is the precondition for being a channel at all**; the FORM → POSITION → MOTION → colour ranking orders **read-speed among channels that already pass it**, and never admits one that doesn't. **Name the ≥2 channels the cue rides on, and verify each is actually LIVE on the shipped material/shader** — a shader property the assigned shader does not declare is a silent no-op that collapses the cue to one channel with no error. **Four checks — C1 amplitude, C2 failure-independence, C3 comparison set, C4 two-sided capture; all required, spelled out in § "Bar 10 — the four checks" below** (`86caz5na6`, 2026-07-31). Two things that used to be "the checks" changed status there: the **invariance desk question** — *"what does this channel look like on a non-cued instance of the same kind? if the answer is 'the same', strike it and re-count"* — is **retained but DEMOTED to a free pre-filter**, because it needs no build (so it runs at dispatch time and kills invariant channels cheaply) but its only input is the author's own sentence, so it is **not evidence**; and **desaturate the shipped-build capture** (if the cue is gone, it failed) stays **required**, because hue-independence is a different question from discrimination and C4 does not test it. WHY: the world is saturated mid-green and will happily eat a hue cue; form survives a colour-blind player and reads faster at peripheral glance; a cue that silently loses a channel degrades with nothing anywhere reporting it; and an always-on channel is indistinguishable from the material it is painted on — the player's question is never "is this a sword", it is "is THIS sword the one I can take". | HUD bars, status chips, world-anchored readouts, **attract / affordance cues on world objects (rim, glow, outline)**, any new UI element | Sponsor decision 2026-07-27 (HP bar = 5 chunky segments over 10 thin ones — form over colour; `86cah7z2q` AC1) + Uma's three-channel rule ratified via PR #339 `e13a51e`. **Provenance note:** ratified by a Sponsor pick + a merged spec, not yet by a shipped soak — re-confirm or correct at the first HUD soak that exercises it. **Second motivating instance (Devon, PR #349 review 2026-07-27):** a find-in-world attract cue lost its Fresnel rim and now rests on motion alone — the earlier colour-only wording of this bar would have PASSED it, which is why the invariant is single-channel collapse, not colour-only. Mechanism he verified: `_RimIntensity` is declared on exactly one shader (`Assets/Shaders/LowPolyVertexColor.shader:79` property / `:162` CBUFFER / `:323` use) and every setter is `HasProperty`-guarded (`LowPolyZoneGen.cs:1937`), so assigning it to a material whose shader does not declare it is a silent no-op. **Third motivating instance (PR #379 + PR #351 review, 2026-07-31) — and the reason the "channel" definition above exists:** the plain **≥2-channels** wording passed **motion + invariant-form** for exactly the reason the older colour-only wording had passed motion-only — it counted channel *types*, not channel *information*. #351's find-in-world attract cue rides float-bob + sway, which are both MOTION (one channel), so a second was needed; the orchestrator and Uma independently proposed §3's white edge-highlight plane as the FORM channel, and Drew declined it. His reason IS the clause: the plane is genuinely fork-free (`EdgeWhite #F5F5F0` is a slot on the shared `weapon_palette.png` and §3 UVs the inset strip to it — same material, no fork), **but** `.claude/docs/blender-asset-pipeline.md` §11's sign-off checklist mandates it on **every blade** ("White edge-highlight plane exists on every blade") and §2's palette row scopes it to **all weapons** ("Blade edge-highlight plane (all weapons)") — both re-verified against `origin/main` at **two** refs: @ `e054aa7` the §2 palette row was `:58`, the §3 rule `:94`, the §11 sign-off `:377`; PR #379 then merged as `fe4af11`, inserting +28 lines into §2, and **all three shifted** — re-measured on `fe4af11` as `:86` / `:122` / `:405`, texts identical. Which is the point: the durable citation is the **§ anchor, never the line number**. Invariant across the whole set ⇒ it cannot answer *"which of these three swords is the pickable one?"* ⇒ the cue stays collapsed on MOTION by a different route. Hence **a channel that is always on is not a cue** — it passes "is it fork-free?" and passes "is it FORM?" and still fails. **Ranking NOT changed:** FORM → POSITION → MOTION → colour orders read-speed, which is orthogonal to variance; the fix belonged in what qualifies as a channel, not in how qualifying channels rank. **Provenance:** source-verified from the docs + the #351 review, not soak-confirmed — the re-served #351 cue is the first soak that exercises it. **The three evasions the four checks exist to close (`86caz5na6`, 2026-07-31 — this row was merged KNOWN-INCOMPLETE and is no longer).** The clause above was approved on PR #380 as *"correct and better; incomplete, not wrong"*, and three cues then passed **every** clause of it while being useless: variance was tested as a **boolean, never a magnitude** (a 2 cm marker + a 3 mm bob passes and is invisible at the gameplay framing → now **C1**); two counted channels were allowed to share **one failure domain** (one marker mesh is both FORM and POSITION, so a single null reference kills both silently → now **C2**); and the invariance check returned **no verdict at all** on a unique instance, which is #351's own case at the default dial (→ now **C3**). Root cause: **every check on this bar ran on the CUED instance alone, so it measured presence and never discrimination** — which is what **C4**'s two-sided artifact addresses structurally. **Status: C1–C3 are live now. C4 is SPECIFIED, not built** — it is build-lane, it needs a purpose-built two-instance scene (no cued/non-cued pair occurs anywhere in live gameplay), and its verdict is human, not mechanical; feasibility is `team/erik-consult/two-sided-capture-feasibility.md`. |

## Bar 10 — the four checks (`86caz5na6`, 2026-07-31)

Bar #10 was written, then amended twice, and merged KNOWN-INCOMPLETE because every check it carried
ran on the **cued instance alone** — so all of them measured **presence** and none measured
**discrimination**, which is the entire job of a cue. These four checks replace that single-sided
pair. Each states **what it returns on an instance that should FAIL**, because a check that has only
ever been shown passing is not a check (the "How to use" bullet above; `86caz5nr2`).

**Scope — the whole class, not one ticket.** These govern every surface in bar #10's Surfaces
column: HUD bars, status chips, world-anchored readouts, attract/affordance cues on world objects,
and any new UI element. The found `sword_iron` (`86cah7y5b`, PR #351) is the instance that exposed
the evasions, not the definition of the governed system; the Open/unconfirmed posture candidate
below (scatter rock vs ore node, driftwood vs choppable log, bush vs berry bush) is the same class
again.

**Cite the SYMBOL, never the line number.** All three line citations in bar #10's Source column
shifted when PR #379 merged (the N1 finding on PR #380). Every value below was re-measured on `main`
at `90d024b`; the symbol is the durable anchor.

### The default gameplay framing — the one framing a magnitude claim may be stated against

| Quantity | Value | Symbol (re-measured at `90d024b`) |
|---|---|---|
| Orbit pitch | **55°** | `OrbitCamera.defaultPitch` — LOCKED per its own doc comment; band is `minPitch` 8° → `maxPitch` 70° |
| Orbit distance | **14 u** | `OrbitCamera.distance`; reachable band `minDistance` **6 u** → `maxDistance` **26 u** |
| Vertical FOV | **45°** | the gameplay `cam.fieldOfView` set in `MovementCameraScene` |
| Capture size | **1280 × 720**, fixed for determinism | `CaptureGate.captureWidth` / `.captureHeight` |
| Frame-plane scale | **720 / (2 × 14 × tan 22.5°) = 720 / 11.60 ≈ 62 px per world metre** | arithmetic over the four rows above |

**Why the framing clause is load-bearing and not pedantry.** `WeaponFindVerifyCapture` — the capture
that would judge #351's cue — frames at `viewDistance` **5.5 u**, `viewPitch` **18°**, FOV **40**
(symbols on PR #351's head). That is **2.9× more px per world metre** than the gameplay framing, and
**5.5 u sits BELOW `OrbitCamera.minDistance` (6 u)** — a distance the player cannot reach in normal
play at all, since only the F10-gated front-snap bypasses the zoom floor and `OrbitCamera`'s own
comment says it "never runs in a normal soak." So without a stated framing, an amplitude check runs
on a view that does not exist in play. That is the presence-not-discrimination failure one layer
down, inside the instrument.

### C1 — AMPLITUDE. A channel's magnitude is measured, at a stated framing, in pixels

**The rule.** Every counted channel carries a **magnitude**, stated as **peak on-screen displacement
(for a motion channel) or on-screen extent (for a form/position/colour channel), in pixels, at the
default gameplay framing above**. State it as both an absolute pixel figure and a fraction of the
cued object's own on-screen extent — 3 px on a 900 px HUD bar and 3 px on a 40 px world prop are not
the same read. "Identifiable" is not a magnitude; neither is "varies".

**How it is judged — a pure-geometry EditMode test, no capture, no runner contention.** World-unit
amplitude → on-screen pixels is trigonometry: `px = amplitude × pixelHeight / (2 × distance ×
tan(fov/2))`. This belongs beside `VerifyCaptureFraming.ComputeFrame`
(`Assets/Scripts/Runtime/VerifyCaptureFraming.cs`, `public static class VerifyCaptureFraming` —
documented DETERMINISTIC, "no floors, no fallbacks"), pinned by the existing
`Assets/Tests/EditMode/VerifyCaptureFramingTests.cs`. **One correction for whoever implements it:**
this is the *inverse* of `ComputeFrame`, which solves distance-from-bounds; it is a new sibling pure
function following that class's pattern and test file, not a reuse of `ComputeFrame` as-is.

**Rendered backstop, and its honest limit.** Geometry cannot see occlusion, fog, contrast or AA, so a
geometry-green channel can still be invisible. `.github/workflows/scripts/frames_differ.py`
(`DEFAULT_MIN_FRAC = 0.0005` of 1280×720 = **461 changed px** at `PIXEL_DELTA = 16`) measures a real
rendered delta between two frames. It applies **only where a live frame pair exists**: for a MOTION
channel, the two extremes of the travel on the same instance (live, no second instance needed). For
FORM / POSITION / colour there is no live pair (see C4), so geometry is the whole mechanical story
and the rest is C4's human read. **Do not diff a cued frame against a cue-disabled frame on `86cah7y5b`:**
`WeaponFindPool.ApplyActiveCount` does `site.gameObject.SetActive(on)`, so dialling the count down
removes the **whole site including the stump** — that diff measures the site's footprint, not the
channel's magnitude.

**What C1 returns on an instance that should FAIL.** The constructed evasion — a **2 cm marker
pebble** (FORM) plus a **±3 mm bob** (MOTION), both genuinely varying with cue state, both
hue-independent, both surviving desaturation. At 62 px/m the pebble spans **≈ 1.2 px** and the bob's
full peak-to-peak travel **≈ 0.4 px**. The geometry test asserts against a stated pixel floor and
returns **FAIL on both channels** with the computed values; a sub-pixel channel cannot pass a
pixel-floor assertion. Under the old wording this cue passed every clause.

**Setting the floor.** No perceptual threshold is invented here — inventing one is how a metric goes
green on nonsense (bar #4, the pond-in-a-mound). The floor is a **named number the author states and
the reviewer can recompute**, seeded at `frames_differ.py`'s existing 461-changed-px "visibly
different" floor for the rendered half, and **corrected by the first soak that exercises it** — the
same provenance discipline the row's own Provenance note uses. What C1 forbids is not a low number;
it is an **unstated** one.

**Derived vs measured — the honesty line.** The pixel figures in this section are **arithmetic from
serialized defaults, not measurements**; no build was run to produce them (this was authored in the
non-build lane). Cross-checked against the one measured anchor in the tree — `OrbitCamera`'s comment
that the castaway renders ≈ 55 × 95 px at pitch 55 / distance 14 in 1280×720, consistent with ≈ 62
px/m for a ~1.5 m figure. A vertical world extent additionally foreshortens at pitch 55, so the
62 px/m figures above are **generous upper bounds** — the charitable direction, deliberately: an
evasion that fails a generous bound fails a strict one. C1 requires the *measurement*; this
arithmetic only sizes the evasion.

### C2 — FAILURE INDEPENDENCE. Two channels one null reference kills together are ONE channel

**The rule.** For each counted channel, **name the thing whose absence kills it** — the transform
reference, the GameObject/prefab reference, the material, the shader property, the component, the
guarded code path. **If two channels name the same thing, they count as one** and the cue has not met
≥2. Independence in *kind* (translation vs rotation) is not independence in *failure*.

Shared-domain forms, each from a real project incident: one **transform reference** driving both
channels; one **GameObject/prefab reference** whose null makes both vanish; one **material or shader
property** (PR #349's `_RimIntensity`, `HasProperty`-guarded, a silent no-op on a material whose
shader does not declare it); one **early return** in one `Update` guarding both.

**The live instance, in the code the bar was written about.** `WorldWeaponFind`'s resting-cue tick
(PR #351 head) opens `if (visual == null) return;` and then writes `visual.localPosition` (CH1
float-bob) and `visual.localRotation` (CH2 sway). Both channels name **`visual`**, and both also die
together on the `_arcing` and `_looted` early returns. The source comment calls them "TWO independent
transform-only channels": they are independent in kind and **identical in failure domain**. (Under
the amended row they are already one channel by perception — both are MOTION — so C2 does not change
#351's count; it is the clearest live illustration of the shape.)

**What C2 returns on an instance that should FAIL.** The constructed evasion — one marker mesh
floating 1.5 m above the object, its *presence* counted as FORM and its *elevation* counted as
POSITION. Both named domains resolve to the same prefab reference, so C2 returns **count = 1** and
the cue **FAILS ≥2**, naming the shared reference. A passing pairing names two different things — a
mesh-presence FORM channel (dies with its prefab ref) plus a shader-driven colour channel (dies with
the material); note the shader channel then still owes the row's existing LIVE-property check.

### C3 — THE COMPARISON SET. Never empty, because it is defined by the player's confusion

**The rule.** The comparison set is **what the player could confuse the cued instance with**, not
what shares its class name. In priority order:

1. **A non-cued instance of the same kind, if one is visible in the same frame.** Strongest; use it
   whenever it exists. For HUD surfaces this is always available — the row's own "an inactive kind
   leaves its slot EMPTY" *is* the pair — so the empty-set problem is world-object-specific.
2. **If none is visible** — and *not visible* includes present-but-`SetActive(false)` — the set is
   **the world objects sharing the frame at the default gameplay framing that share the cued
   object's material family, silhouette family, or scale.** Name them explicitly, by asset, in the
   Self-Test Report.
3. **The set is empty only if the frame is empty.** An author who can name no neighbour has a wrong
   capture, not a waiver: re-frame.

**"Kind" may not be narrowed to empty the set.** The check's subject is the player's question —
*"which of the things I can see is the one I can take"* — so narrowing the class only moves the
comparison to step 2. It never yields "no verdict."

**What C3 returns on an instance that should FAIL.** The evasion was answering *"there is no non-cued
instance of the same kind"* and collecting silence, which reads as not-failed. C3 returns **step 2**,
which obliges the author to name neighbours; if they name none, C3 returns **FAIL** (a frame with no
context cannot evidence discrimination). Silence is no longer an available output.

**Applied to `86cah7y5b` / PR #351 — the bar CAN now adjudicate it.** Step 1 is genuinely empty at
the default dial, verified on PR #351's head: `WeaponFindPool.DefaultFindCount = 1` with easy ==
medium == hard == 1, `WeaponFindSiteCount = FindCountMax = 4` authored sites, and `ApplyActiveCount`
switching the rest off via `site.gameObject.SetActive(on)` — so no non-cued `sword_iron` is visible,
and no non-cued weathered stump either. Step 2 is populated and was **already named by the author
from a shipped-build capture**: draft 2 (0.34 u proud) was rejected because it "read as a dark-brown
LUMP on dark-brown wood: no sword silhouette, no metal, **indistinguishable from the rust-capped
scatter rocks nearby**", and the stump bark is drawn from `LowPolyZoneGen.TrunkCol`, the family the
chop tree reuses. So #351's comparison set at the default dial is **the rust-capped scatter rocks
plus the chop-tree trunks sharing the frame**. Two consequences: the bar was never actually silent on
#351 — step 2 was always available and merely unwritten; and step 2 has **already discriminated once
on this exact feature**, unrecorded, since it is what moved the proud height 0.34 u → 0.60 u. That is
a precedent for C3, not a hypothesis about it.

### C4 — THE TWO-SIDED ARTIFACT. Cued and non-cued in one frame; a naive viewer points

**SPECIFIED, NOT BUILT.** This is build-lane and must be sequenced against the single Unity build
slot. Feasibility is `team/erik-consult/two-sided-capture-feasibility.md` (harvested via PR #381);
its architecture claims were re-measured against `90d024b` before being relied on here.

**It is a QA/soak-judged aid, not a CI gate — and that is the right shape.** "Which one draws the
eye" is a Gestalt judgment with no valid mechanical proxy in this project's stack: a pixel difference
can be large while reading as *worse* (darker, occluded) rather than *more special*, so a threshold
on it would pass cues a human rejects. Manufacturing an automated verdict here would rebuild exactly
the weakness the merged desk check had — consuming a claim instead of an artifact — dressed as code.

**But the artifact's PRODUCTION is mechanically gateable even though its VERDICT is not**, and that
half is not optional: assert the capture is at the **stated framing**, that **every named comparison
member is inside the frustum**, and that the frames are non-degenerate (`frame_check.py` already
covers black / uniform / all-magenta). Without it the human is judging a capture that may frame the
wrong thing at the wrong distance — the `-verifyPond`-green-on-a-mound failure applied to the
instrument, and precisely the 5.5 u framing defect found above. Gate the artifact's validity; leave
the read to the human.

**It cannot be captured from ordinary gameplay.** No cued/non-cued pair occurs anywhere in the live
world at any dial setting: every found weapon gets the same cue uniformly, and `86cah7y5b`'s
count/rarity dial raises **how many cued** instances spawn rather than producing a cued+non-cued
pairing. So C4 needs a **purpose-built two-instance scene** — a new component on the established
`*VerifyCapture` pattern (`WeaponSetVerifyCapture` is the closest sibling; bounds via
`Encapsulate()` over both instances, pose via `VerifyCaptureFraming`), and it must run **windowed**
on the runner-1-pinned capture lane, so on-demand invocation rather than a per-PR blocking step.

**What the capture must contain:**

- **In-scene, not a product shot.** Spawn both instances at a real in-scene position so they inherit
  the actual directional light, fog Volume and post-processing, with the camera held at the **stated
  default gameplay framing** (55° / 14 u / FOV 45 / 1280×720). An isolated neutral-clear rig is
  cheaper and answers the wrong question — per `unity-conventions.md`, an isolated verify capture is
  a smoke-test that the asset EXISTS, not proof of how it READS in play.
- **`cue_pair.png`** — the cued instance and at least one named C3 comparison member both in frame.
- **For a motion channel, `cue_ext_a.png` / `cue_ext_b.png`** — the two extremes of the travel from
  one camera pose, because a motion channel's magnitude cannot be read from a still. This pair is
  also C1's rendered backstop.
- **The human half, recorded.** `cue_pair.png` is shown to someone who has not read the PR, with one
  question — *"point at the one you can pick up"* — asked **before** they see any diff number. The
  Self-Test Report records who was asked, the answer, and whether it was right **first try, no second
  look**. Hesitation or a wrong point is a FAIL.

**What C4 returns on an instance that should FAIL.** The sub-pixel construct fails C1's geometry
floor before a capture is ever made, so it never reaches a human. An always-on channel (the white
edge-highlight plane) contributes **zero** cued-vs-non-cued difference in `cue_pair.png` by
construction, so it cannot be the thing carrying the read. And the case that motivates C4's
existence: a cue that is unmistakably **present** — bright, large, live on the shipped material —
whose neighbours are equally bright, passes every mechanical check and **fails the human point**.
That split is the whole reason the human half is not removable, and the reason C4 does not replace
**desaturate**: desaturate tests hue-independence, C4 tests discrimination, and neither substitutes
for the other.

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
