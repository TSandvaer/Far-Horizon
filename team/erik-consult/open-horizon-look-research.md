# Open-Horizon Look — Selling a Beautiful Empty Ocean Horizon in URP

> **HARVEST NOTE (Priya, 2026-08-01) — read this before citing anything below.**
>
> **Provenance.** Author: Erik (consult). Written for ticket `86cagfn8h`. Erik has no Bash and
> cannot commit, so this note sat uncommitted in `Far-Horizon-erik-wt` and was invisible to
> `gh pr list` / `git ls-remote`. It was **PORTED** here — read out of his worktree and written
> onto a fresh branch off `main` — **not** merged, rebased, or cherry-picked. His worktree was
> not modified.
>
> **⚠ His worktree is pinned at `363c1a0`, roughly 200 commits behind `main`.** Every statement
> below about *this repo's current code* was made against that stale snapshot and is
> **unverified against `main`** unless a harvest annotation says otherwise. Erik flagged this
> himself throughout; it is restated here so a reader who skips to a section still hits it.
>
> **His evidence grades, carried intact** (his own words, not upgraded):
> - **STRONG** — URP fog mechanics (Catlike Coding + Unity 6 official perf docs) and the shipped
>   skybox/fog code *as he read it in his stale worktree*.
> - **MODERATE** — sun-glitter (practitioner implementation) and the Sea of Thieves comparable
>   (GDC/SIGGRAPH, and it is **UE4, not Unity** — the art problem ports, the implementation does not).
> - **WEAK-MODERATE** — cloud-density-bias-near-horizon. This is Erik's **own synthesis**, not a
>   cited external technique.
>
> **⛔ This note does NOT pick a look, and neither does this PR.** Ticket `86cagfn8h` is
> `needs-soak` + `sponsor-gate`; the Sponsor's eye decides. Erik deliberately wrote §3 as "a menu,
> not a recommendation." Do not read any section here — or this PR — as a recommendation.
>
> **Erik's three open questions**, and their status at harvest:
> 1. `LowPolyZoneGen.cs` vs `WorldBootstrap.cs:~447` spawn-site discrepancy — **RESOLVED at harvest**;
>    see the annotation in § Open questions.
> 2. Whether `GradientSkybox.shader` / `QualityPassGen` fog values still match `main` — **STILL OPEN.**
> 3. Whether PR #194's sun is a light property, a sky-shader term, or both — **STILL OPEN.**
>
> **What was added.** Erik's text below is unmodified except for exactly **two** clearly-labelled
> insertions: the "Harvest annotation" block under the first open question, and the closing
> annotation under § Citation-chain note. Nothing was deleted, reworded, or re-graded.

---

## Question

Ticket `86cagfn8h`: the Sponsor chose Option A (2026-06-30, full open ocean) — remove the
distant `FacetedMountain` horizon islands entirely; the sea should dissolve into a warm sky
360° with no visible "edge." The engineering question this note answers: what URP-side
techniques make a bare horizon read as **depth and invitation** rather than **a missing
asset**, and what does each cost? This ticket is `needs-soak` + `sponsor-gate` — the
Sponsor's eye decides the final look. This note presents options and their trade-offs; it
does **not** pick a winner.

## Bottom line

The project's existing foundation — a custom 3-stop gradient skybox + Exp² distance fog
colour-locked to the horizon stop (the "seam-kill") — is already the right *class* of
solution for a geometry-free horizon and needs no re-architecture; removing the mountain
rings is a pure win (fewer draw calls, no downside). Selling "beautiful" rather than "empty"
from there is a set of near-zero-GPU-cost shading/motion additions — sun-glitter on the
water, horizon-band softness/glow tuning, cloud density/motion bias toward the horizon —
not new geometry. The one geometry option on the table (Option B, the pre-approved faint
distant-rim fallback) is cheap in GPU terms but its real cost is aesthetic: it is the exact
"capped-edge" read Option A was chosen to escape, so it belongs in reserve, not built now.

## Evidence

1. **This repo's own prior research — `team/erik-consult/world-look-far-vista-research.md`**
   (read from my pinned worktree snapshot) — Strong *as a description of what was
   implemented*, but **unverified against current `main`** (see Open Questions). It
   established: URP's `RenderSettings` fog (Linear / Exponential / Exponential-Squared) is
   camera-**distance**-based only and does not apply to the skybox by default, so fog colour
   must equal the skybox's horizon colour or a seam appears. `QualityPassGen.cs` (read in
   this worktree) implements exactly this: `FogMode.ExponentialSquared`, density `0.0016`,
   `fogColor` bound to `WorldLookPalette.SkyHorizon` (`#DCE8E4`) — the same constant the
   skybox shader's horizon stop uses.

2. **`Assets/Shaders/GradientSkybox.shader`** (read in this worktree) — a hand-authored
   3-stop vertical-gradient HLSL skybox (zenith `#7FB4D6` / mid `#AAD0E2` / horizon
   `#DCE8E4`), registered via `QualityPassGen` with a `Skybox/Procedural` fallback. The
   shader's own header comments record that the built-in `Skybox/Procedural` (a 2-color
   sun-tint/ground model) was tried and judged insufficient for the desired warm-cream
   horizon control — i.e., **this project already ran the "does URP's built-in option
   suffice" experiment and answered no.** Strong evidence for *this specific project*, but
   again unverified against current `main`.

3. **Catlike Coding, "Rendering 14, Fog" (Jasper Flick)** — Strong, canonical, matches
   Unity's shader source. Confirms URP fog is distance-only with no height/Y component.
   Corroborated by a 2026 Unity Discussions thread on porting HDRP's height/volumetric fog
   override to URP (Moderate — community discussion, but corroborated by the existence of
   several third-party "URP height fog" ports, which exist *because* URP ships none
   natively): https://discussions.unity.com/t/urp-to-hdrp-specifically-for-the-volumetric-fog-override-in-the-global-volume/1614853

4. **Unity Technologies, "Configure for better performance in URP," Unity 6 Manual** —
   Strong (official). No documented desktop barrier to any option below; the real cost axis
   is draw calls / overdraw / added passes, not raw draw distance.

5. **Sun glitter** — Wikipedia, "Sun glitter" (Strong: well-established optical
   phenomenon) + "Sun glitter – Real-time Water Shader in Unity," unitywatershader.wordpress.com,
   2018 (Moderate: practitioner implementation, pre-Shader-Graph but the math ports
   directly). A specular term keyed to view/reflect vs. light direction, broken up by the
   water's own ripple noise, reads as a scattered sparkle path toward the sun — a
   directional "something to travel toward" cue that costs one extra term in an existing
   fragment shader, no new draw call.

6. **Wikipedia, "Aerial perspective"** — Strong (established atmospheric-optics/art
   principle, widely cited in game-art literature). As distance increases, contrast/
   saturation/detail drop and colour shifts toward the background colour. This is the exact
   principle the fog-colour==sky-horizon seam-kill already implements, and it is *why* a
   well-tuned fog-only horizon can read as convincing distance rather than "nothing there" —
   independent of whether a silhouette occupies the far field.

7. **Rare, "Visual Adventures on Sea of Thieves" (GDC 2018, Ryan Stevenson)** +
   **"The technical art of Sea of Thieves" (ACM SIGGRAPH 2018 Talks)** — Moderate-to-Strong
   (GDC/SIGGRAPH talks from the shipped title's own art team; engine is UE4 so
   implementation detail doesn't port, but the *art problem* — an entire stylized world that
   is open ocean to the horizon — is the closest shipped comparable). Their answer leaned on
   an art-directed (non-simulated) cloudscape + a stylized water look, **not** horizon
   geometry, to sell scale. This supports (does not prove) that Far Horizon's
   fog+sky+water-surface route is the right family of solution for this exact problem class.
   https://gdcvault.com/play/1025015/Visual-Adventures-on-Sea-of · https://dl.acm.org/doi/10.1145/3214745.3214820

8. **This project's own `lowpoly-quality.md` / `unity6-mastery.md`** — Strong
   (project-authoritative, already Sponsor-adopted guardrails). SRP Batcher / shared-shader
   batching is the mechanism in play; the skybox is a dedicated render pass, not a
   `MeshRenderer`, so **GPU Resident Drawer does not apply to it at all** — it's neither
   helped nor hurt by any skybox change. Fog is a fragment-shader term with zero new draw
   calls, compatible with the already-audited `CBUFFER_START(UnityPerMaterial)` SRP-Batcher
   pattern.

## Application to Far Horizon

### 1. Gradient skybox / atmospheric treatment

The shipped-as-read 3-stop vertical gradient (zenith → mid → warm horizon cream) is the
right foundation — URP has no first-party gradient skybox, and a custom shader (Shader
Graph or hand-HLSL, functionally equivalent) is the standard industry route for this exact
look (see the prior far-vista note's Keijiro/Coster/Boysen citations). Two additive,
near-zero-cost knobs worth exercising now that mountains are gone:

- **Sun-glow term in the sky shader itself** — a soft radial falloff around the light
  direction blended into the gradient (this is what `Skybox/Procedural`'s sun-disc property
  already does natively; one extra `dot(viewDir, lightDir)` term). Gives the eye a literal
  point to travel toward without adding geometry. Distinct from the directional light's own
  bloom/lens-flare (PR #194's "warm-gold sun," per the ticket text) — open question below on
  whether that's a light property, a sky-shader term, or both.
- **Horizon-band softness** — `GradientSkybox.shader` already exposes `_MidPoint` /
  `_Softness` as tunable properties. Widening the horizon-to-mid transition reads as soft
  haze rather than a hard stripe. Zero code cost — pure Sponsor-soak dial.

### 2. Fog as a compositional tool, not just occlusion

URP's built-in fog (what's shipped, per `QualityPassGen.EnableGlobalFog()`) is
**camera-distance** fog (Exponential Squared), **not height fog** — URP has no built-in
height/Y-axis fog component; that's an HDRP-only override (evidence #3 above). A true
height-fog band — thicker low over the water regardless of camera distance, the way real sea
haze behaves — is a real technique for open-water games but is DIY in URP: author it as a
world-space-Y falloff term in `LowPolyWater.shader`, the same pattern class already used for
`_FogCap`. Not free, but modest — the team has already built this exact math once.

**Compositional framing (the point worth naming explicitly):** fog density here is no
longer just "hide the draw-distance edge" — with the mountains gone, it is the *primary*
thing standing in for them. Too thin and the far sea shows a flat, hard-lit plane out to the
clip plane (a "swimming-pool wall" read). Too thick and it caps the world close-in —
reintroducing the exact "edge" problem, just made of haze instead of rock. The ticket already
frames this correctly as a pure Sponsor-soak tuning call.

**Non-negotiable regardless of tuning:** fog colour stays locked to
`WorldLookPalette.SkyHorizon` / the skybox horizon stop. This is a hard constraint already in
the ticket, restated here because every technique below must respect it or the seam reopens.

### 3. What replaces the mountains as a distance cue

A menu, not a recommendation:

- **Sun-glitter specular streak on the water** (evidence #5) — a "path to the horizon" the
  eye follows without capping the world; anchors to the sun, reinforcing a consistent light
  source rather than a void.
- **Cloud density/motion bias toward the horizon** — clouds already exist as `CloudBlob`
  geometry (per `lowpoly-quality.md` §1's outward-winding list). Biasing more/slower-moving
  cloud instances toward the horizon band is a tuning change on an *already-adopted* pattern,
  not new tech. (Grade this **Weak-Moderate** — my own synthesis from the aerial-perspective
  principle + the existing scatter pattern, not a direct external citation.)
- **Distant birds/gulls** — small animated silhouettes at far range, a common "life at the
  edge of visibility" cue in open-ocean/desert titles. Real but modest **dev-time** cost (new
  mesh + a simple flight-loop), not a shader toggle — flag this as the most expensive
  non-geometry option in authoring time even though its runtime cost is trivial.
- **Wave/swell motion carried to the fog line** — the water plane already extends "well past
  the island to the fog horizon" per its own code comments (`WaterHalfExtent = 700`, as read
  in this worktree). Open question: does the existing swell amplitude/wavelength stay
  visually readable that far out, or flatten near the fog line now that there's nothing else
  in the far field to compare it against? A tuning question, not a new-tech one.
- **(Reserve only, do not build now) Option B — faint distant-island-rim.** Per the prior
  far-vista note, 2 rings of ~150–400-tri faceted meshes at 500–1500u are negligible GPU cost
  and were literally the approach this ticket removes — cheap to re-add if the soak rejects
  Option A. Its cost is not compute; it is that it is the exact read the Sponsor is trying to
  get away from. A fallback of last resort, not a parallel build.

### 4. Cost

| Technique | Draw calls | Fill-rate / overdraw | GRD / batching interaction | Frame-budget risk |
|---|---|---|---|---|
| Remove `FacetedMountain` rings | Fewer (net reduction) | n/a | n/a | None — pure win |
| Gradient skybox (tune existing) | 1 (skybox pass, unchanged) | Non-trivial on THIS scene layout specifically — sky covers most of the upper frame on open water, so skybox fill is a real (if cheap-per-pixel) budget line, not negligible-by-default | None — skybox is a dedicated pass, not a `MeshRenderer`; GRD doesn't touch it | Low |
| Fog (ExpSquared, colour-locked) | 0 new | Near-zero — a few extra ALU ops in existing fragment shaders | Compatible — properties already live in `CBUFFER_START(UnityPerMaterial)` per the audited SRP-Batcher pattern | None |
| Sun-glitter specular term | 0 new | Near-zero — one more ALU term in the water fragment shader | Same as above | None |
| Height-fog-style near-water haze (custom) | 0 new | Low runtime cost, same shader-term class as `_FogCap` | Same as above | Low runtime; real **dev-time** cost (new math + soak-tune) |
| Cloud density/motion bias near horizon | Scales with added instances | Low, if instance count stays modest | SRP Batcher / shared-shader-variant batching already handles this class per `unity6-mastery.md` §2 | Low, but the one option with a real (if small) instance-count cost |
| Distant birds | A few (or one instanced-flock draw call if authored with GPU instancing) | Trivial | Trivial | Trivial GPU; real authoring/dev-time cost |
| Option B distant-rim (fallback only) | Negligible (static-batchable, 150–400 tris/ring) | Negligible | Static-batch or GRD, either is fine at this tri count | None GPU-side — 100% aesthetic risk (see §3) |
| Volumetric/ray-marched fog (ruled out) | New pass | High — the one option that *could* threaten budget | Bypasses SRP-Batcher/GRD entirely (separate pass) | Real risk — this is why it's ruled out |

None of the recommended-tier options (skybox tuning, fog tuning, sun-glitter, wave-motion
tuning) threaten the Windows-desktop frame budget — they're all sub-millisecond ALU additions
to passes that already run every frame. The two options with a *real* cost are dev-time
(birds, height-fog authoring), not GPU-time.

### 5. Ruled out

| Technique | Why NOT |
|---|---|
| Volumetric / ray-marched fog (Buto-class) | Extra render pass; the scene is now geometrically *simpler* (fewer distant objects to atmospherically composite), so there is even less reason for it than when this was ruled out in the prior far-vista note. |
| Real-time / screen-space reflections on the water | Mirror-sharp reflections read as photoreal water, not toy flat-shaded water — fights the low-poly smooth-shaded style directly. Also a real Renderer Feature cost (extra scene-colour pass). Rule out on style grounds regardless of budget. |
| HDRI / photo-sourced cubemap skybox | Same photoreal-mismatch problem as SSR. Also the one thing HDRP's native height-fog would make trivial — and the project is intentionally not adopting HDRP for it (pipeline-locked). |
| Billboard / impostor far-field layer | Over-engineered for a scene that, after this ticket, has **no** far-field geometry to impostor at all. Reserve the technique (if ever) for dense foreground/mid-range foliage, not an empty horizon. |
| HDRP's `Gradient Sky` / native height-fog override | Would solve §1/§2 "for free" but requires switching render pipelines. Off the table — URP is locked. |
| Toon hard-band sky ramp / screen-space outlines | Carried over from `lowpoly-quality.md` §3: the board is faceted **smooth**, not cel-shaded. Nothing in this ticket should introduce a hard-edged sky band. |

## Taste vs. cost — sorted explicitly

- **Pure Sponsor taste (no engineering argument either way):** fog density / dissolve
  distance, sun-glow intensity and size, horizon-band softness, whether sun-glitter reads
  "beautiful" or "distracting," whether birds feel alive or busy.
- **Cost-constrained (the "no" is an engineering call, not taste):** ruling out volumetric
  fog, SSR, and HDRI skybox — these cost render passes or fight the shipped shader/style
  pipeline independent of how they'd look.
- **Mixed — cheap to build, but changes the read, so still a Sponsor call:** whether to add
  birds/denser clouds at all. Low cost either way; the question is how much "life" belongs on
  a horizon that was deliberately emptied of mountains — that's a taste decision wearing an
  engineering-cheap price tag.

## Open questions (repo-state — hand to a Bash-capable persona to verify against current `main`)

- The ticket names `LowPolyZoneGen.cs` as the horizon-mountain spawn site; my grep (against
  this **pinned-stale** worktree) found the `FacetedMountain` spawn call in
  `WorldBootstrap.cs` (~line 447) instead. Reconcile before dispatch — either the ticket text
  is imprecise or the code moved since my worktree's snapshot.

  > **HARVEST ANNOTATION (Priya, 2026-08-01) — RESOLVED. Erik's file is right; his line number
  > is stale; the ticket text is imprecise.** Measured on `origin/main` @
  > `54d69069407829cad141b330d65833d67960f818` (the harvest base) via
  > `git grep -n "FacetedMountain" origin/main -- "*.cs"`:
  > - **The spawn site is `Assets/Scripts/Editor/WorldBootstrap.cs:596`** — `var mesh =
  >   LowPolyMeshes.FacetedMountain(br, h, 9 + rnd.Next(0, 4), c.snowline, c.body, c.snow,
  >   rnd.Next());`, inside `static void BuildMountainCluster(...)` declared at `:563`, creating
  >   a `GameObject("LP_Mountain")` at `:599`. Its caller is `static void BuildVista(GameObject
  >   envRoot, int seed)` at `:461`. (Mesh generator itself: `LowPolyMeshes.cs:872`.)
  > - **`Assets/Scripts/Editor/LowPolyZoneGen.cs` exists on `main` but contains ZERO
  >   `FacetedMountain` references** — the same grep returns no hits in that file. It does carry
  >   mountain-adjacent *fog/material* commentary (around `:2059`–`:2063`), which is the likely
  >   origin of the ticket's mis-naming. An implementer sent to `LowPolyZoneGen.cs` to remove the
  >   mountains would find nothing to remove.
  > - **Additional observed fact the implementer should know (not part of Erik's question):** the
  >   same cluster routine calls `BuildLandmassBase(clusterRoot, c, clusterCentre, footprintR,
  >   tint, mat, rnd)` at `:581`, *before* the peak loop (definition at `:623`). The horizon
  >   clusters are therefore **peaks standing on a faceted landmass shelf**, not bare mountains.
  >   Removing only the `FacetedMountain` peaks would leave the shelves behind. Flagged, not
  >   decided — scoping that belongs to the `86cagfn8h` dispatch brief, not to this harvest.
  >
  > Corroborating comment on the same sha: `NextIslandPocGen.cs:19` — *"NOT horizon-backdrop
  > props (that is what WorldBootstrap.FacetedMountain already does for the start"*.
  > **`86cagfn8h`'s body was NOT edited by this harvest** (its status and text are out of this
  > task's scope); the correction is recorded here and in this PR.

- Confirm `GradientSkybox.shader` + `QualityPassGen.EnableGlobalFog()` (Exp² fog, density
  `0.0016`, colour = `SkyHorizon`) are still the shipped state on `main` — this worktree
  cannot see current `main`.
- Confirm whether PR #194's "warm-gold sun" is a directional-light property (bloom/flare), a
  skybox-shader term, or both — determines whether §1's sun-glow-in-sky-shader suggestion is
  additive or already covered.
- Whether `LowPolyWater.shader`'s existing swell (`_WaveAmp`/`_WaveLen`/`_WaveSpeed`) reads
  visibly out to the fog line, or flattens well before it, now that nothing else occupies the
  far field for comparison.

## Citation-chain note

This note is **not yet committed** to `main`. Per the project's committed-artifact citation
rule, it cannot be cited as locked authority by any implementing PR or spec until a
Bash-capable persona commits it and it merges via the normal PR flow.

> **HARVEST ANNOTATION (Priya, 2026-08-01).** That is exactly what this PR does — Erik's
> sentence above described the state at the time he wrote it and is left standing as the
> record. **On merge, this note becomes a committed artifact and is citable.**
>
> **But citable ≠ verified.** What merging establishes is only that the note EXISTS at a fixed
> path and sha. It does **not** convert Erik's stale-worktree readings into facts about `main`,
> and it does **not** turn his menu into a decision. Specifically, still true after merge:
> - Two of his three repo-state open questions above remain **OPEN** (skybox/fog values;
>   PR #194's sun). Only the spawn-site question was resolved at harvest.
> - His MODERATE and WEAK-MODERATE grades stand at those grades. The
>   cloud-density-bias idea in §3 is his own synthesis, not a cited technique.
> - `86cagfn8h` remains `needs-soak` + `sponsor-gate`. Nothing here is a look decision.
