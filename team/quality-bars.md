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
- **When a bar outgrows its row, MOVE the overflow into an appendix — never trim it.** House ceiling: **~400 characters in the `Bar` cell** (bar 9's measured 391 is the house number; bars 1–8 sit at 112–245). A bar over it gets a **`## Bar N — <topic>`** section below, carrying the standard in full plus its provenance and motivating instances; the row keeps the **one-line standard + the channel ranking + `see § Bar N`**, and its `Source` cell keeps memory slugs / ticket ids / soak dates + the same pointer. **The move must be reviewable, which is what makes it a move and not a trim:** the PR body carries a **completeness ledger** — every clause removed from the row → the subsection it now lives in — and the reviewer walks it for orphans. A trim cannot be checked that way. **Precedent: PR #386**, which created `§ Bar 10` for `86caz5na6`'s four checks; this rule generalises that pattern rather than arguing with it (`86cazhjw4`, 2026-07-31). Bars 1–9 predate the rule and are not retrofitted by it.

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
| 10 | **No cue may rest on a SINGLE channel** — colour-only and motion-only fail identically. Every cue must be identifiable on **≥2 channels, ≥1 hue-independent**, each **varying between a cued and a non-cued instance** and each **LIVE on the shipped material/shader**. Rank **FORM → POSITION → MOTION → colour**; text last-resort. Gates: **C1–C4 + desaturate required, invariance a free pre-filter**. Full text: **§ Bar 10**. | HUD bars, status chips, world-anchored readouts, **attract / affordance cues on world objects (rim, glow, outline)**, any new UI element | Sponsor decision 2026-07-27 (`86cah7z2q` AC1) + Uma's three-channel rule, PR #339 `e13a51e`; amended PR #380 `90d024b` and PR #386 (`86caz5na6`); row dieted `86cazhjw4`. **Not soak-confirmed.** Provenance, the three motivating instances and the three evasions: **§ Bar 10 → History**. |

## Bar 10 — the standard in full, and the four checks (`86caz5na6` + `86cazhjw4`, 2026-07-31)

Bar #10 was written, then amended twice, and merged KNOWN-INCOMPLETE because every check it carried
ran on the **cued instance alone** — so all of them measured **presence** and none measured
**discrimination**, which is the entire job of a cue. These four checks replace that single-sided
pair. Each states **what it returns on an instance that should FAIL**, because a check that has only
ever been shown passing is not a check (the "How to use" bullet above; `86caz5nr2`) — **except C4,
which is unbuilt and therefore states PREDICTIONS, labelled as such and not counted as coverage.**

**Two things this section does NOT deliver, stated up front so nobody reads past them.** (a) **The C1
pixel floor is not set** — C1 mandates that a floor be stated and recomputable, and lists the three
inputs whoever sets it must reconcile; it does not pick the number. (b) **C4 is unbuilt**, so C3's
comparison set has no consumer and no cue has yet been judged two-sided. Both are named at their own
headings below as well.

**Scope — the whole class, not one ticket.** These govern every surface in bar #10's Surfaces
column: HUD bars, status chips, world-anchored readouts, attract/affordance cues on world objects,
and any new UI element. The found `sword_iron` (`86cah7y5b`, PR #351) is the instance that exposed
the evasions, not the definition of the governed system; the Open/unconfirmed posture candidate
below (scatter rock vs ore node, driftwood vs choppable log, bush vs berry bush) is the same class
again.

**Cite the SYMBOL, never the line number.** All three line citations in bar #10's Source column
shifted when PR #379 merged (the N1 finding on PR #380). Every value below was re-measured on `main`
at `90d024b`; the symbol is the durable anchor.

### The standard in full

**Moved here verbatim from the Bars-table row** at `0f14b4f`, by `86cazhjw4` — the cell had reached
**2,523 characters** against bar 9's 391 and bars 1–8's 112–245, so the table had stopped being
scannable. This is a MOVE: every clause below is the row's own wording. The single adaptation is the
self-reference — the row said *"spelled out in § 'Bar 10 — the four checks' below"* and now says
*"spelled out below"*, because it is inside that section.

**The dieted row lands at 423 characters, over the ~400 house ceiling, and that is deliberate.** The
standard carries five things the row cannot drop without weakening it — the single-channel invariant,
≥2-with-one-hue-independent, the variance qualifier, the LIVE-on-the-shipped-shader check, and the
rank order — and compressing further starts paraphrasing them. A paraphrased bar is a weakened bar,
which is the failure `86caz5na6` spent a whole review closing, so the ceiling gives way rather than
the standard (`86cazhjw4`; the ceiling is a trigger for this section, not a budget to game).

**No cue may rest on a SINGLE channel.** Colour-only is the most common way it fails, but motion-only
fails identically. Every HUD / world readout / attract cue must be identifiable on **≥2 channels, at
least one of them independent of hue** — **FORM** first (segment count, silhouette, size), then
**POSITION** (a fixed slot per kind; an inactive kind leaves its slot EMPTY, never packed, so "the
third slot is lit" is itself the read), then **MOTION**; colour ranks LAST of the four, never first,
and text is a last-resort fallback.

**A "channel" is a property that DIFFERS between an instance in the cued state and one that is not**
(clause added 2026-07-31) — a property present on **every** instance is **style, not a cue**: it
answers "what KIND of thing is this", never "WHICH one is cued", so it contributes nothing to the ≥2
however well it reads. **Variance is the precondition for being a channel at all**; the FORM →
POSITION → MOTION → colour ranking orders **read-speed among channels that already pass it**, and
never admits one that doesn't.

**Name the ≥2 channels the cue rides on, and verify each is actually LIVE on the shipped
material/shader** — a shader property the assigned shader does not declare is a silent no-op that
collapses the cue to one channel with no error.

**Four checks — C1 amplitude, C2 failure-independence, C3 comparison set, C4 two-sided capture; all
required, spelled out below** (`86caz5na6`, 2026-07-31). Two things that used to be "the checks"
changed status there: the **invariance desk question** — *"what does this channel look like on a
non-cued instance of the same kind? if the answer is 'the same', strike it and re-count"* — is
**retained but DEMOTED to a free pre-filter**, because it needs no build (so it runs at dispatch time
and kills invariant channels cheaply) but its only input is the author's own sentence, so it is **not
evidence**; and **desaturate the shipped-build capture** (if the cue is gone, it failed) stays
**required**, because hue-independence is a different question from discrimination and C4 does not
test it.

WHY: the world is saturated mid-green and will happily eat a hue cue; form survives a colour-blind
player and reads faster at peripheral glance; a cue that silently loses a channel degrades with
nothing anywhere reporting it; and an always-on channel is indistinguishable from the material it is
painted on — the player's question is never "is this a sword", it is "is THIS sword the one I can
take".

**Surfaces** (unchanged, still in the row): HUD bars, status chips, world-anchored readouts,
attract / affordance cues on world objects (rim, glow, outline), any new UI element.

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

**The rule.** Every counted channel carries a **magnitude**, and the magnitude is the **DELTA between
the cued and the non-cued state of that channel** — never the channel's absolute extent on the cued
instance. Stated in pixels, at the default gameplay framing above:

- **Motion channel** — the **peak-to-peak on-screen travel of the DIFFERENCE signal** (cued position
  minus the non-cued instance's position at the same instant), not the cued instance's own travel.
- **Form / position / colour channel** — the on-screen extent the non-cued instance **does not also
  have**: the added mesh, the offset, the repainted area.

State it as both an absolute pixel figure and a fraction of the cued object's own on-screen extent —
3 px on a 900 px HUD bar and 3 px on a 40 px world prop are not the same read. "Identifiable" is not
a magnitude; neither is "varies".

**Why the delta and not the extent — this is bar #10's own root cause, reproduced one level in
(Devon, #386 review, M1).** The row's variance clause only asks whether the property DIFFERS. A
magnitude then taken on the cued instance alone re-answers *"is the channel big"* instead of *"is the
DIFFERENCE big"* — i.e. presence-not-discrimination rebuilt inside the check written to close it.
**The same-instance motion-extremes pair below measures the delta ONLY when the non-cued value is
ZERO** — when the channel is absent from the non-cued instance entirely. Whenever the non-cued
instance carries some of the channel, that pair overstates, and it overstates without bound (second
worked example below).

**Rotation channels — the reading must be NAMED, because a yaw has neither a "displacement" nor an
"extent" until you say what moved (M2).** A rotation's magnitude is the **lateral travel of the
channel's most-displaced point**: for a full angular swing Δ about an axis, a point at horizontal
radius `r` from that axis travels a chord `2 · r · sin(Δ/2)`, so at the framing above
`px_p2p = 2 · r · sin(Δ/2) × 62.0798`. **`r` must be stated** — the measured horizontal distance from
the rotation axis to the vertex the author claims the read from, never the object's bounding radius.
Worked at #351's shipped `WorldWeaponFind.DefaultSwayDegrees = 4f` (its `SwayOffset` returns
`sin(…) · degrees`, so the yaw runs −4° → +4° and the **full swing is Δ = 8°**): `2 · sin 4° =
0.139513` ⇒ **8.6609 px per world-unit of `r`** ⇒ **`r` ≥ 0.1155 u buys 1 px** peak-to-peak. Under a
peak-from-centre reading the same channel needs `r` ≥ 0.2308 u — exactly **2×**. That fork is why the
convention is written down rather than left to the reader: **peak-to-peak** is the reading this
section uses everywhere, and it is the quantity the rendered extremes pair actually captures.

**Displacement and lit-AREA are different claims in different units and may not be traded.** A 4° yaw
of a broad flat blade also changes the lit face's on-screen AREA, and that reading differs from the
displacement reading by an order of magnitude. **Displacement is the default** — it is what makes a
rotation commensurable with the other motion channels and with the px floor. An author claiming the
read comes from area instead is making a FORM/colour claim that owes its own magnitude in **px²**; it
may not be quoted as though it were the displacement figure.

**#351's CH2 currently has NO stated magnitude, and under this wording that is a C1 FAIL rather than
an omission.** `r` for the sway is the horizontal distance from the weapon's yaw axis to its
most-displaced vertex, which rides the import-time `WeaponPackAssetGen.familyGlobalScale`
(`Assets/Scripts/Editor/WeaponPackAssetGen.cs:170`/`:173`, derived from `NewFamilyAxeTargetLongestU`)
and is **not derivable from the serialized defaults alone**. **No px figure for CH2 is claimed here** —
measuring it is the first thing C1 asks of whoever re-serves that cue.

**How it is judged — a pure-geometry EditMode test, no capture, no runner contention.** World-unit
amplitude → on-screen pixels is trigonometry: `px = Δworld × pixelHeight / (2 × distance ×
tan(fov/2))`, where **`Δworld` is the cued-minus-non-cued delta from "The rule" above, not the cued
instance's own extent** — the whole point of C1 is which quantity gets fed in here. This belongs
beside `VerifyCaptureFraming.ComputeFrame`
(`Assets/Scripts/Runtime/VerifyCaptureFraming.cs`, `public static class VerifyCaptureFraming` —
documented DETERMINISTIC, "no floors, no fallbacks"), pinned by the existing
`Assets/Tests/EditMode/VerifyCaptureFramingTests.cs`. **One correction for whoever implements it:**
this is the *inverse* of `ComputeFrame`, which solves distance-from-bounds; it is a new sibling pure
function following that class's pattern and test file, not a reuse of `ComputeFrame` as-is.

**Rendered backstop, and its honest limit.** Geometry cannot see occlusion, fog, contrast or AA, so a
geometry-green channel can still be invisible. `.github/workflows/scripts/frames_differ.py`
(`DEFAULT_MIN_FRAC = 0.0005` at `PIXEL_DELTA = 16` — that is **≥ 26 changed SAMPLES of 51,360**, not
461 px; see "Setting the floor" below, and do not quote the full-frame equivalent) measures a real
rendered delta between two frames. It applies **only where a live frame pair exists**: for a MOTION
channel, the two extremes of the travel on the same instance (live, no second instance needed).
**⚠ That same-instance pair is a valid stand-in for the DELTA only when the non-cued instance's value
in that channel is ZERO** — otherwise it measures the cued instance's own travel, which is the
absolute reading this check no longer accepts. Where the non-cued instance also carries the channel,
the pair is not the magnitude; the difference signal is, and that needs C4's two-instance frame. For
FORM / POSITION / colour there is no live pair (see C4), so geometry is the whole mechanical story
and the rest is C4's human read. **Do not diff a cued frame against a cue-disabled frame on `86cah7y5b`:**
`WeaponFindPool.ApplyActiveCount` does `site.gameObject.SetActive(on)`, so dialling the count down
removes the **whole site including the stump** — that diff measures the site's footprint, not the
channel's magnitude.

**What C1 returns on an instance that should FAIL — example 1, where absolute HAPPENS to equal
delta.** The constructed evasion — a **2 cm marker pebble** (FORM) plus a **±3 mm bob** (MOTION), both
genuinely varying with cue state, both hue-independent, both surviving desaturation. The non-cued
instance has **no pebble and no bob**, so here the delta *is* the absolute figure: at 62.0798 px/m the
pebble spans **1.2416 px** and the bob's full peak-to-peak travel **0.3725 px**. The geometry test
asserts against a stated pixel floor and returns **FAIL on both channels** with the computed values; a
sub-pixel channel cannot pass a pixel-floor assertion. Under the old wording this cue passed every
clause.

**Example 2 — where absolute ≠ delta, which example 1 structurally CANNOT expose (Devon, #386 review,
M1).** A cued sword bobbing **±0.30 u** beside a non-cued sword bobbing **±0.29 u**. The row's
variance clause passes: the property genuinely differs. Under the **absolute** reading C1 returns the
cued instance's own travel — **0.60 u p2p = 37.2479 px** — and clears any plausible floor. Under the
**delta** reading the difference signal is `0.01 · sin(ωt)`, i.e. **0.02 u p2p = 1.2416 px**: a **30×**
overstatement, and the delta lands on *exactly* the **1.2416 px** the marker pebble scores. So the
corrected C1 fails it for the same reason it fails example 1, and the old wording PASSED it at 18.6 px
peak / 37.2 px p2p. **Rule for anyone extending this section: a worked example in which absolute
equals delta demonstrates nothing about which quantity is being measured** — every future example set
must contain at least one case where the two differ.

**Setting the floor — the number is NOT set in this section, and three facts must be reconciled
before anyone sets it.** No perceptual threshold is invented here — inventing one is how a metric goes
green on nonsense (bar #4, the pond-in-a-mound). The floor is a **named number the author states and
the reviewer can recompute**, **corrected by the first soak that exercises it** — the same provenance
discipline the row's own Provenance note uses. What C1 forbids is not a low number; it is an
**unstated** one. Note what that costs this section honestly: C1's verdict on example 1 ("FAIL on both
channels") holds only against a floor **above 1.2416 px**, and that is the entire strength of the
claim. The three inputs, none optional (Devon, #386 review, M3/M5):

1. **`frames_differ.py`'s `DEFAULT_MIN_FRAC = 0.0005` is NOT a linear-pixel floor and must not be
   copied as one.** It is an **area fraction of SUBSAMPLED pixels**. At 1280×720 its `changed_fraction`
   takes `step_x = w // 200 = 6` and `step_y = h // 200 = 3`, so it compares **214 × 240 = 51,360
   samples** and the threshold it actually applies is `0.0005 × 51,360 = 25.68` ⇒ **≥ 26 changed
   SAMPLES**. The "461 changed px" figure (`0.0005 × 1280 × 720 = 460.8`) is a full-frame *equivalent*
   the script never computes. **Authoritative: the ≥26-sample figure**, for the RENDERED backstop only.
   The decision quantum is one sample ≈ **18 full-res px**, and a feature thinner than the 6×3 stride
   can be missed outright — which matters precisely because C1 adjudicates features in the 1–10 px
   band. An earlier draft of this section seeded C1's floor at "461 px"; that number is retired here.
2. **An area fraction INVERTS C1's own scale rule, so it cannot seed the geometry floor at all.** It
   rewards a large object with a tiny motion (a 900 × 20 px HUD bar shifted 1 px repaints ~900 px →
   passes) and punishes a small object with a large motion (a ~40 × 8 px world prop moved fully clear
   of itself changes ≤ ~640 px → marginal) — the exact inversion of this section's own *"3 px on a 900
   px HUD bar and 3 px on a 40 px world prop are not the same read"*. **The geometry half has no seed
   today; say so rather than borrowing this one.** Express the floor as an **expression over the
   framing table above** rather than a bare literal, so it re-derives when the framing moves and can be
   audited against its own justification. A number lifted from an instrument's achievability constant
   guards regressions; it does not establish correctness.
3. **The floor COLLIDES with the project's own prescribed juice value — name that collision when you
   pick the number.** `.claude/docs/game-juice.md` §1 must-have 5 prescribes collectible float-bob at
   **±0.05 u**, and that is #351's shipped `WorldWeaponFind.DefaultBobAmplitude = 0.05f`. At the
   framing above that is **6.2080 px** p2p frame-plane / **3.5607 px** p2p foreshortened / **3.1040 px**
   peak frame-plane / **1.7804 px** peak foreshortened. So **any floor above 1.7804 px reds the house
   value under at least one reading, and any floor above 6.2080 px reds it under all four.** Not
   resolvable inside either doc alone: either the floor sits under the house value, or the house value
   moves, or bar #10 records that it deliberately reds it.

**This is also C1 run against the one LIVE cue on the board, not only against the instance built to
fail it — and the live verdict FLIPS on the reading, which is why the convention above had to be
pinned.** At the shipped `±0.05 u`, a 1 px floor passes the live bob under all four readings; a 4 px
floor passes it **only** under p2p frame-plane and reds it under the other three. That is a real
verdict on shipped values rather than a construct, and it is also the concrete demonstration that
"peak vs peak-to-peak" and "frame-plane vs foreshortened" are not pedantry: the same cue, the same
floor, opposite outcomes. C1's demonstrated-red on a *constructed* instance (examples 1 and 2) and its
verdict on a *live* one are different evidence; both are now present, and neither substitutes for the
floor actually being chosen.

**Derived vs measured — the honesty line.** The pixel figures in this section are **arithmetic from
serialized defaults, not measurements**; no build was run to produce them (this was authored in the
non-build lane). **They are FRAME-PLANE figures and they are upper bounds.** A world-VERTICAL extent
foreshortens at pitch 55 by `cos 55° = 0.573576`, so vertical motion reads at **35.6075 px/m**, not
62.0798 — quote the pair, not one number.

**The one anchor in the tree does NOT validate either reading, and this paragraph previously claimed
that it did (Devon, #386 review, M6).** `OrbitCamera`'s source comment that the castaway renders
≈ 55 × 95 px at pitch 55 / distance 14 in 1280×720 implies **95 / 62.0798 = 1.5303 m** under the
frame-plane scale and **95 / 35.6075 = 2.6680 m** under the foreshortened one. The first is plausible
and the second is not — which is evidence about how rough a source comment is, not a cross-check of
the model, since a genuine validation would have to survive the same foreshortening the very next
sentence invokes. The earlier wording asserted the anchor as confirmation of 62 px/m and then
introduced the foreshortening that refutes it; both cannot hold, so the confirmation claim is
withdrawn. **The conclusion is unchanged and stays in the charitable direction:** both examples above
are sub-pixel under BOTH readings, and an evasion that fails a generous bound fails a strict one. C1
requires the *measurement*; this arithmetic only sizes the evasion.

### C2 — FAILURE INDEPENDENCE. Two channels one null reference kills together are ONE channel

**The rule.** For each counted channel, **name the thing whose absence kills it** — the transform
reference, the GameObject/prefab reference, the material, the shader property, the component, the
guarded code path. **If two channels name the same thing, they count as one** and the cue has not met
≥2. Independence in *kind* (translation vs rotation) is not independence in *failure*.

**Tie-breaker — because "name the thing" is AUTHOR-NAMING, and author-naming has unbounded
granularity (Devon, #386 review).** That is the same property this bar uses two paragraphs up to
demote the invariance desk check to *"not evidence"*, so C2 does not get to keep it uncaveated. **Name
the nearest common dependency on the code path both channels actually traverse — never a leaf
property.** #351 is the demonstration: at leaf granularity CH1 and CH2 name `visual.localPosition` and
`visual.localRotation`, two different things ⇒ count = 2, while one `if (visual == null) return;` kills
both. **Where a count is contested, settle it by INJECTION, not by naming** — null or disable the named
dependency and assert both channels stop. The injection is the evidence; the name is only the claim.

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

**C3 COLLECTS; C4 JUDGES — C3 returns no discrimination verdict about the set it builds (Devon, #386
review).** C3 discriminates against **silence**, not against **sameness**: an author who names ≥ 1
neighbour passes it even where the cue is visually identical to every member named. That is deliberate
— the thing that consumes the set and returns a verdict is **C4**, which is unbuilt. **State the
consequence plainly rather than letting the row read as adjudication: until C4 ships, C3 is a naming
obligation with no consumer, and a cue can pass C1–C3 and still be indistinguishable from the
neighbours it named.** Do not cite a C3 PASS as a discrimination result.

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
`Encapsulate()` over both instances, pose via `VerifyCaptureFraming`). **Its launch mode is decided by
`unity-conventions.md` §Headless/CLI rituals' BOUNDARY SENTENCE, and is NOT asserted here** — an
earlier draft said "must run windowed", which is stronger than the tree supports (M7): a two-prop
world-camera capture with no IMGUI / UI-Toolkit overlay is the RT-readback class, so it MAY run
`-batchmode` (no `-nographics`); it stays windowed only if any judged pixel comes from the backbuffer.
Quote that sentence at implementation time rather than restating it. **Either way it is on-demand
rather than a per-PR blocking step**, and the `86cag93zb` runner-1 pin does not move for either mode
while any windowed gate still shares the `capture` job.

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

**What C4 is PREDICTED to return on an instance that should FAIL — NONE of this is measured (Devon,
#386 review).** C4 returns nothing on any instance today: it is unbuilt, there is no artifact, and no
human has been asked. The three items below are therefore **predictions**, labelled as such per the
project's hypothesis-labelling rule. Publishing an unmeasured claim under a "what it returns" heading
is the exact defect this section exists to prohibit, so the heading is corrected rather than the
claims quietly kept.

- **Predicted — and VACUOUS; do not count it as coverage.** The sub-pixel construct fails C1's
  geometry floor before a capture is ever made, so it never reaches C4. That is **C1's** verdict
  credited to C4: a clearance of an error C4 structurally cannot reach, which is the same
  declared-clean-of-what-it-never-ran-on shape bar #10 was written against.
- **Predicted (analytic; no capture exists).** An always-on channel (the white edge-highlight plane)
  contributes **zero** cued-vs-non-cued difference in `cue_pair.png` by construction, so it cannot be
  the thing carrying the read.
- **Predicted (no human has been asked).** The case that motivates C4's existence: a cue that is
  unmistakably **present** — bright, large, live on the shipped material — whose neighbours are
  equally bright, passes every mechanical check and **fails the human point**.

**C4's own red case is OWED, not delivered — and that is the load-bearing gap in this section.** AC4
is this ticket's load-bearing AC, and C1's delta gap and C3's missing consumer are precisely the holes
C4 was meant to cover, so the one check that would close them is the one that did not ship. C4 is
demonstrated red the first time the artifact is built and a naive viewer points at the wrong prop;
until then it carries predictions only. The present/discriminable split is still why the human half is
not removable, and why C4 does not replace **desaturate**: desaturate tests hue-independence, C4 tests
discrimination, and neither substitutes for the other.

### History — motivating instances

**Moved here verbatim from the row's `Source` column** at `0f14b4f`, by `86cazhjw4` (the column had
reached **4,187 characters**). One class of change: **the four line-number citations the row carried
are converted to § anchors / symbol names** — `LowPolyVertexColor.shader:79` / `:162` / `:323` and
`LowPolyZoneGen.cs:1937`. All four still resolved on `0f14b4f` when converted, so this is not a bug
fix; it is the row ceasing to argue against its own third instance, which is a story about line
numbers drifting. **The `blender-asset-pipeline.md` line numbers inside that instance are NOT
converted and must not be** — they are the evidence of the drift, and rewriting them as anchors would
delete the finding. Nothing else is re-worded.

**Origin.** Sponsor decision 2026-07-27 (HP bar = 5 chunky segments over 10 thin ones — form over
colour; `86cah7z2q` AC1) + Uma's three-channel rule ratified via PR #339 `e13a51e`. **Provenance
note:** ratified by a Sponsor pick + a merged spec, not yet by a shipped soak — re-confirm or correct
at the first HUD soak that exercises it.

**Second motivating instance (Devon, PR #349 review 2026-07-27):** a find-in-world attract cue lost
its Fresnel rim and now rests on motion alone — the earlier colour-only wording of this bar would
have PASSED it, which is why the invariant is single-channel collapse, not colour-only. Mechanism he
verified: `_RimIntensity` is declared on exactly one shader — `Assets/Shaders/LowPolyVertexColor.shader`,
as a `Properties` entry, restated in the `ForwardLit` pass's `CBUFFER_START(UnityPerMaterial)` and
consumed in that pass's fragment (`finalCol += _RimColor.rgb * rim * _RimIntensity`) — and every
setter is `HasProperty`-guarded (`LowPolyZoneGen.RockVertexColorMat`, in
`Assets/Scripts/Editor/LowPolyZoneGen.cs`), so assigning it to a material whose shader does not
declare it is a silent no-op.

**Third motivating instance (PR #379 + PR #351 review, 2026-07-31) — and the reason the "channel"
definition above exists:** the plain **≥2-channels** wording passed **motion + invariant-form** for
exactly the reason the older colour-only wording had passed motion-only — it counted channel *types*, not
channel *information*. #351's find-in-world attract cue rides float-bob + sway, which are both MOTION
(one channel), so a second was needed; the orchestrator and Uma independently proposed §3's white
edge-highlight plane as the FORM channel, and Drew declined it. His reason IS the clause: the plane is
genuinely fork-free (`EdgeWhite #F5F5F0` is a slot on the shared `weapon_palette.png` and §3 UVs the
inset strip to it — same material, no fork), **but** `.claude/docs/blender-asset-pipeline.md` §11's
sign-off checklist mandates it on **every blade** ("White edge-highlight plane exists on every blade")
and §2's palette row scopes it to **all weapons** ("Blade edge-highlight plane (all weapons)") — both
re-verified against `origin/main` at **two** refs: @ `e054aa7` the §2 palette row was `:58`, the §3
rule `:94`, the §11 sign-off `:377`; PR #379 then merged as `fe4af11`, inserting +28 lines into §2,
and **all three shifted** — re-measured on `fe4af11` as `:86` / `:122` / `:405`, texts identical.
Which is the point: the durable citation is the **§ anchor, never the line number**. Invariant across
the whole set ⇒ it cannot answer *"which of these three swords is the pickable one?"* ⇒ the cue stays
collapsed on MOTION by a different route. Hence **a channel that is always on is not a cue** — it
passes "is it fork-free?" and passes "is it FORM?" and still fails.

**Ranking NOT changed:** FORM → POSITION → MOTION → colour orders read-speed, which is orthogonal to
variance; the fix belonged in what qualifies as a channel, not in how qualifying channels rank.
**Provenance:** source-verified from the docs + the #351 review, not soak-confirmed — the re-served
#351 cue is the first soak that exercises it.

**The three evasions the four checks exist to close (`86caz5na6`, 2026-07-31 — this row was merged
KNOWN-INCOMPLETE and is no longer).** The clause above was approved on PR #380 as *"correct
and better; incomplete, not wrong"*, and three cues then passed **every** clause of it while being
useless: variance was tested as a **boolean, never a magnitude** (a 2 cm marker + a 3 mm bob passes
and is invisible at the gameplay framing → now **C1**); two counted channels were allowed to share
**one failure domain** (one marker mesh is both FORM and POSITION, so a single null reference kills
both silently → now **C2**); and the invariance check returned **no verdict at all** on a unique
instance, which is #351's own case at the default dial (→ now **C3**). Root cause: **every check on
this bar ran on the CUED instance alone, so it measured presence and never discrimination** — which
is what **C4**'s two-sided artifact addresses structurally.

**Status: C1–C3 are live now. C4 is SPECIFIED, not built** — it is build-lane, it needs a
purpose-built two-instance scene (no cued/non-cued pair occurs anywhere in live gameplay), and its
verdict is human, not mechanical; feasibility is `team/erik-consult/two-sided-capture-feasibility.md`.

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
