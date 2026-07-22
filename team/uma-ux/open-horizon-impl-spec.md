# Open-Horizon (Option A) — IMPLEMENTATION SPEC for Devon

**Ticket:** `86cagfn8h` (impl) · **Direction record:** `86cafffe8` / Uma #199 exploratory spec (`team/uma-ux/open-horizon-direction-spec.md`, merged) · **Author surface:** Uma (UX / Visual Direction)

**Status:** BUILD PLAN — spec only, no code/shader/world-gen changes in this PR. This doc turns the Sponsor's locked **Option A** pick into a Devon-buildable plan he can quote in his dispatch. It cites real files/functions read from the live tree.

**Decision being implemented (verified ground truth):** Sponsor vision decision 2026-06-30 (present-mode walkthrough) = **Option A — full open ocean: remove the distant horizon mountains; open blue water dissolving into warm sky 360°.** Logged to `DECISIONS.md` via PR #206. Reveal feel = **NATURAL FOG-HAZE** (Approach 1), carried to the future next-island/journey POC — **NOT this ticket** (`86cagfn8h` ticket body, read 2026-06-30).

---

## The tonal anchor (read this first)

> **You stand on a little island in the middle of a huge bright ocean. You orbit the camera a full 360° and every direction is the same: warm teal water running out to a clean, soft horizon where the sea melts into the sky. There is no land anywhere — no mountains, no edge, no wall. The world doesn't STOP; it just keeps going, calm and endless, into a bright haze. You are alone at sea, and it is beautiful, not bleak.**

That is the feel the impl must deliver. Bigness here comes from **emptiness** — the world reads endless because you can see how far the nothing goes. The single biggest risk (called out in #199 and the ticket) is that an empty horizon reads *cheap/flat/boring* instead of *calm/endless/beautiful*. **The whole job of this impl is: remove the mountains (easy) AND make the resulting emptiness gorgeous (the real work).** The beauty is carried by FOUR things working together — the sea's teal at the horizon, the warm sky stop it dissolves into, the #194 warm-gold sun, and the cloud puffs keeping the sky alive. None of these is new; the impl re-points the existing dissolve at empty water instead of at mountains.

> **Anchor discipline** (`[[physical-features-anchor-realworld-not-metric]]` + `lowpoly-quality.md` §0): the gate is "does it read like *standing on a little island lost in a calm bright ocean* to the human eye at gameplay framing" — NOT a fog-density number. Every metric below is a START value the soak dials; the feeling is the test.

---

## 0. Where #194 sits (HARD gate — read before branching)

This impl ticket is **gated on PR #194** (`drew/86cabc743-sky-poc`, the warm-gold sun-disk) merging first, because **#194 and this impl touch the same three files**: `WorldBootstrap.cs`, `QualityPassGen.cs`, `GradientSkybox.shader`. Verified via `git diff --name-only origin/main origin/drew/86cabc743-sky-poc`.

- **Branch the impl off POST-#194 main**, never off today's main. The #194 sun is not optional decoration here — **a warm-gold sun low over an empty sea is the single best beat available to make Option A's horizon beautiful** (#199 cross-ref). The impl COMPOSES with #194's sun; it does not fight or remove it.
- **Constraint (OOS of THIS spec PR):** I do not touch any of #194's files in this doc-only PR. The IMPL ticket touches `WorldBootstrap.cs` (mountain removal) but inherits #194's sun work in the post-merge versions.
- If #194 changes the `GradientSkybox` shader or `QualityPassGen` fog in ways that move the horizon-stop or fog values, re-read those two functions before tuning — the values quoted below are today's pre-#194 live values.

---

## 1. Mountain removal — WHERE they are, HOW to remove

### Where the mountains are actually generated (the ticket's hint is WRONG — corrected here)

The impl ticket body says "confirm the exact spawn site in `LowPolyZoneGen.cs`." **That is incorrect — the distant mountains are NOT in `LowPolyZoneGen.cs`.** Verified by reading the live tree:

- **Spawn site:** `Assets/Scripts/Editor/WorldBootstrap.cs` — method **`BuildVista(GameObject envRoot, int seed)`** (defined at **line 398**), CALLED once at **line 150** inside `BuildEnvironment` as `BuildVista(envRoot, ZoneSeed + 9001);`.
- **What `BuildVista` builds:** a `Vista` root GameObject holding **5 discrete mountain "islands"** — the `clusters[]` array (lines ~444-455), each a `MtnCluster` struct (`Vista_Island_N` / `_NE` / `_E` / `_SW` / `_W`) with explicit azimuth / distance (620-820u) / peak-count / snowline. For each cluster it calls:
  - **`BuildMountainCluster(...)`** (line ~500) → instances `LowPolyMeshes.FacetedMountain(...)` (the peaks) — `LowPolyMeshes.cs` line **853**.
  - **`BuildLandmassBase(...)`** (line ~560) → instances `LowPolyMeshes.FacetedLandmass(...)` (the island shelf each cluster stands on) — `LowPolyMeshes.cs` line **1029**.
- **The mountain palette + fade knobs** (do NOT need editing for removal, listed so the impl knows what becomes dead code): `MtnBody`/`MtnSnow`/`MtnRimBody`/`MtnRimSnow` constants (`WorldBootstrap.cs` lines ~74-83); `WorldLookConfig.MtnFadeCap` (0.25) + `WorldLookConfig.MtnDistanceScale` (**0.62** — live value; an older DECISIONS entry cites 0.55, which is stale) in `Assets/Scripts/Runtime/WorldLookConfig.cs`.

`LowPolyZoneGen.cs` owns the **start island** (radial heightmap, seed 42) — which is **LOCKED and must NOT be touched** (`[[world-is-big-round-island]]`). The ticket conflated the two; the spec corrects it so Devon edits the right file.

### How to remove (recommended approach + the two alternatives)

**Recommended — disable the `BuildVista` call (one-line removal, cleanest, most reversible):**

- At `WorldBootstrap.cs` line 150, **remove / comment out** the `BuildVista(envRoot, ZoneSeed + 9001);` call. No `Vista` root is created → no peaks, no landmass shelves, no mountain materials. Open ocean → horizon in every direction.
- Leave `BuildVista` + `BuildMountainCluster` + `BuildLandmassBase` **defined but uncalled** (dead-but-present), so Option B (faint-rim fallback, see §5) is a one-line re-enable + a knob tune, NOT a rebuild-from-scratch. This is the Option-A→B safety-net sequencing #199 recommends.

> ⚠ **Committed-asset trap (`[[unity-procedural-committed-assets-go-stale]]`):** `Boot.unity` ships the COMMITTED generated snapshot. Removing the `BuildVista` call in source is NOT enough — the impl MUST **re-run the bootstrap** (`-executeMethod FarHorizon.EditorTools.BootstrapProject.Run`) so the regenerated `Boot.unity` (with the Vista root gone) is committed. A source-only change with a stale committed `Boot.unity` ships the mountains unchanged. Verify the built scene has no `Vista` GameObject.

**Companion edit (cheap, do it in the same PR):** at `WorldBootstrap.cs` line ~174, the camera `farClipPlane = Mathf.Max(mainCam.farClipPlane, 1600f)` was sized to reach past the far vista ring. With no rings, that reach is no longer load-bearing — but the **sea plane and fog still want a far clip that comfortably contains the visible ocean.** Do NOT drop it aggressively; keep it generous (≥ the sea-plane extent + margin) so the ocean never frustum-clips into a hard edge before the fog dissolves it. Treat any change here as soak-verified (a too-near far clip would create exactly the "visible edge" Option A exists to remove). **Safe default: leave the 1600f as-is** — it costs ~nothing (nothing to draw out there now) and guarantees no clip seam. Reducing it is an optional micro-optimization, not a requirement.

**Alternatives (NOT recommended, listed for completeness):**
- *Empty the `clusters[]` array* — works, but leaves the method running over zero clusters (wasteful, and a future edit could refill it accidentally). The call-disable is cleaner.
- *Delete the methods entirely* — burns the Option-B fallback. Don't; keep them dead-but-present.

---

## 2. Natural fog-haze horizon dissolve — making the empty horizon BEAUTIFUL

The mechanism that makes empty water read as *calm endless sea dissolving into bright sky* (not a flat teal line under a flat sky) **already exists and ships today** — this impl re-points it, it does not build it. It is the SAME fog/sky dissolve specced in `world-look-polish-direction.md` §3 and reused as the #199 reveal mechanism.

### The four load-bearing pieces (all live today; cite these in the dispatch)

1. **Exponential-squared distance fog** — `QualityPassGen.EnableGlobalFog()`:
   - `RenderSettings.fogMode = FogMode.ExponentialSquared`
   - `RenderSettings.fogDensity = 0.0016f` (keeps the near/mid field CRISP, accelerates into a haze band at distance)
   - `RenderSettings.fogColor = SkyHorizon` — **THE SEAM-KILL. Do NOT drift this constant** (`lowpoly-quality.md` §1 + §3-ruled-out; Erik Q2 lockstep). URP does not fog the skybox, so a fog-colour ≠ sky-horizon-stop mismatch creates a visible horizon seam.

2. **The warm-bright sky horizon stop** — `WorldLookPalette.SkyHorizon = (0.80, 0.89, 0.92)` = **`#CCE3EB`** (bright cheerful sky-haze horizon; live value, HDR-clamp-safe sub-1.0). The bottom of the `GradientSkybox` gradient (`_HorizonColor`) reads this same stop, so the sky fades to the exact colour the fog fades to → seamless dissolve, no horizon line. *(Note: the `GradientSkybox.shader` header comment still cites the old `#DCE8E4`; the LIVE bound constant is `#CCE3EB`. Cite the constant, never the comment.)*

3. **`_FogCap` sea-teal floor** — `LowPolyVertexColor.shader` `_FogCap` property (line ~49; default 0 = full fog for terrain/canopy/rock; the **water material instance** sets a floor so the sea keeps at least `_FogCap` of its own teal no matter how far out). **THIS is what makes the empty horizon read as SEA rather than as more sky.** Without it the far sea fades 100% to `SkyHorizon` and the ocean vanishes into the sky with no teal at all — a dead flat frame. **With it**, the sea holds a soft teal that eases UP into the warm sky haze: a gradient, not a line. **Do NOT touch `_FogCap` or the water material's floor value in the removal PR** — it is doing exactly the right job already; it just had mountains in front of it before. (`lowpoly-quality.md` §1: never drift the fog colour / fog-cap.)

4. **The #194 warm-gold sun + the clouds** — these are the *visual interest* the mountains used to provide. With the mountains gone, the sun-over-empty-sea and the slow cloud drift are now the ONLY things on the horizon, so they carry the whole frame. **Preserve both** (ticket constraint). The clouds (`BuildClouds`, `WorldBootstrap.cs` line ~144) are MORE valuable now, not less — an empty sky needs them to stay alive.

### Why this is "natural" and reuses Option A's own mechanism

No new shader, no new system, no scripted reveal. The fog physically dissolves whatever is far away into the warm haze; with mountains removed, what's far away is just more ocean, and it dissolves into the sky exactly as atmospheric haze does in real life. **The same fog that makes the empty horizon beautiful is the one that will later hide the next island (§3).** One technique, both jobs.

### What the dissolve should LOOK like (the soak target, in plain words)

Near water: clear, saturated teal with visible facets and the water shimmer. Mid-distance: teal softening, facets smoothing in the haze. Far (horizon band): a soft teal-into-cream-blue gradient where you genuinely cannot point at the line where sea becomes sky — it's a melt, not a seam. The #194 sun sits warm and low; a few cyan cloud puffs drift through the upper sky. Calm. Bright. Endless. **If on soak it reads as a hard teal stripe under a flat blue band, the dissolve is too abrupt → the fog density / `_FogCap` floor are the two dials (see §5), NOT a redesign.**

---

## 3. Next-island occlusion-reveal HOOK (downstream consumer — NOT spec'd here)

This impl makes the start-island horizon empty. The future **next-island/journey POC** (`86caa9zpp` destination + the deferred boat `86caa9zju`) is a DOWNSTREAM CONSUMER of exactly the fog mechanism this impl re-points — but it is **separate, vision/POC-gated, and OUT OF SCOPE here.** This section is a one-paragraph hook so whoever picks up that POC inherits the seam; it is **not** a full spec of the reveal.

**The hook (from #199's natural-fog-haze decision, Approach 1 "distance fog wall"):** the next island is always physically present in the world, but the same global Exp² fog (the one this impl re-points at empty water) is tuned so that at start-island distance the next island is **fully dissolved into the horizon haze** — indistinguishable from empty sea/sky. As the boat closes the distance, the island crosses out of the fog band and **fades up naturally** — faint silhouette → colour → detail. No pop-in, no scripted trigger; the fog does it for free, reading as real-life atmospheric horizon occlusion. **Recommended starting band (the POC + Sponsor-soak tune, NOT a lock):** next island at ~600-900u; ≥95% dissolved past ~400-500u; faint silhouette by ~350-400u of approach; clearly readable by ~200u.

> **Why this impl SETS UP that hook for free:** by making the start-island horizon a clean fog-dissolve into empty sea, this impl proves the fog tuning that the reveal depends on, AND buys the payoff — when the early game has shown only empty water, the first real island rising over the edge from the boat becomes the single best moment in the game. **Do NOT build the reveal / island / boat in THIS ticket** (`86caa9zpp` / `86caa9zju` are separate + gated). Just don't break the fog dissolve, and the hook is preserved by construction.

---

## 4. Acceptance criteria + shipped-build capture evidence

### ACs (the impl ticket carries these; restated for the build plan)

1. **Mountains removed at the source** — the `BuildVista` call at `WorldBootstrap.cs` line 150 is disabled; the regenerated + committed `Boot.unity` has **no `Vista` GameObject** (no `Vista_Island_*` clusters, no `LP_Mountain`, no `LP_Landmass` under env root). Prove via a scene-presence test (EditMode): assert `Boot.unity` env root contains no child named `Vista`.
2. **Start island byte-unchanged** — the seed-42 start landmass + coast + beach are intact (`[[world-is-big-round-island]]`). Prove via a scene-presence test: the `Grounds`/`Play` zone + its coast are present and unchanged. `LowPolyZoneGen.cs` is NOT modified.
3. **Fog seam-kill intact** — `RenderSettings.fogColor == WorldLookPalette.SkyHorizon` (`#CCE3EB`); `_FogCap` and the water material's floor value are unchanged. Prove via the existing `ZoneDLookTests` / a fog-colour assertion.
4. **Empty horizon reads beautiful at gameplay framing** — open teal water dissolving into warm sky 360°, with the #194 sun + clouds preserved, no land/edge/wall anywhere. **Judged by the Sponsor soak** (feel gate; see §5).
5. **The dissolve is a gradient, not a seam** — at the horizon the sea-to-sky transition is a soft melt (the `_FogCap` teal easing into `SkyHorizon`), verified by a horizon-pixel sampler (not a metric — §4 capture gate).

### Shipped-build capture evidence (the impl needs ALL of these)

Per the **shipped-build capture gate** (`unity-conventions.md` §Editor-vs-runtime; the false-green-capture + "legs-up" failure class) — **editor screenshots do NOT count**; evidence must come from the BUILT exe (`Build/Windows/FarHorizon.exe`), build-stamp verified:

- **Gameplay-framing horizon orbit** — captures from the GAMEPLAY over-shoulder orbit camera **at its real pitch (~55)**, NOT an isolated hero/high-angle shot. The empty horizon + warm dissolve must show in the actual play framing. *(The over-shoulder orbit framing is itself a proven gotcha — it frames the LOW dir.y sky band, which is why the sky mid-point was lowered to 0.18; the capture must use that same framing.)*
- **A FULL 360° orbit** — a sequence (or turntable) proving there is no land in ANY direction, no azimuth where a stray cluster survived. Option A's entire promise is "no land anywhere," so one-angle evidence is insufficient.
- **A horizon-band pixel read** — a `-seaDiag`-class sampler (per `lowpoly-quality.md` §3 / the ticket's `-seaDiag` reference) that samples actual horizon-band PIXELS to confirm the sea holds teal and eases into the sky stop (proves the `_FogCap` gradient survived). **Sample pixels, never trust a metric or the normal attribute** (`[[verify-grounding-soaks-by-gameplay-cam-visual]]` — metrics + high angles have lied here before; open the gameplay-cam image and SEE it).
- **Build-stamp check** — the HUD `BUILD <tag> | <UTC> | <sha>` stamp matches the soak build (the CI soak build's stamp is the merge-ref sha = the artifact suffix, per `[[soak-build-stamp-is-merge-ref-not-headsha]]`).

> **Predict-Before-Soak (the impl author states this, graded against the soak):** *"Removing the `BuildVista` call + the existing fog-cap dissolve makes the sea read endless and calm with no visible edge or land at the gameplay orbit pitch; the sea-to-sky transition reads as a soft teal-into-warm-haze gradient, not a hard line."*
> **Bounded convergence:** the soak tests the open-horizon / lost-at-sea feel ONLY; it does NOT test island generation, water material quality, the sun-disk (that's #194's gate), or the boat/next-island (separate scope).

---

## 5. Aesthetic choices to surface for the Sponsor's SOAK (what HE judges)

The impl ships **dial-from defaults** (the current live values) and the soak decides feel. These are the subjective-feel calls — the Sponsor's, not the team's (orchestrator-autonomy never-auto-decide list; `[[sponsor-merge-approval-soak-or-complete]]`). Surface this exact list with the soak hand-off so he knows what he's judging and which knob moves each one (`[[soak-handoff-path-and-explicit-test-checklist]]`):

| # | What the Sponsor judges | The knob (if he wants it different) | Ship-from default |
|---|---|---|---|
| 1 | **Fog dissolve DISTANCE/DENSITY** — does the sea dissolve too NEAR (claustrophobic, world feels small) or too FAR (hard teal stripe, dissolve invisible)? | `QualityPassGen.EnableGlobalFog()` `fogDensity` (also live-tunable via `WorldLookNudgeTool` F10 world-look dials) | `0.0016` (current) |
| 2 | **Horizon COLOR / sea-teal-at-horizon** — does the sea hold enough teal at the horizon, or fade too much to sky (looks empty) / too little (hard seam)? | `LowPolyVertexColor.shader` water-instance `_FogCap` floor | current water `_FogCap` value (unchanged) |
| 3 | **Sun interplay** — is the #194 warm-gold sun positioned/sized to make the empty sea beautiful (low + warm = best beat), or does it need repositioning for the open-ocean composition? | #194's sun params (compose with, don't fight) | #194's shipped sun |
| 4 | **Cloud density** — enough cloud puffs to keep the now-empty sky alive, or does the empty sky want more/fewer? | `BuildClouds` density (`WorldBootstrap.cs` ~line 144) | current cloud count |
| 5 | **THE feel verdict** — does it read "calm bright endless sea, alone but not bleak" (Option A succeeds) or "flat/cheap/empty" (trigger the fallback)? | — (this is the gate) | — |

**Pre-planned fallback (Sponsor pre-approved, do NOT build preemptively):** if the fully-empty horizon reads cheap/flat on soak, **Option B = re-enable `BuildVista` with a heavily-reduced `clusters[]`** (1-2 faint, low, far, ~80-90% dissolved silhouettes — a "is that… something way out there?" rim hint, NOT the old chunky wall). Because §1 keeps `BuildVista` dead-but-present, B is a one-line re-enable + a knob tune, not a rebuild. This is the A→B safety net from #199.

> **Name the bar:** run `/name-the-bar` at impl-dispatch time (per the ticket) to confirm the **open-horizon / lost-at-sea feel bar** before the soak, if not already confirmed in `quality-bars.md`.

---

## What must NOT regress (hard constraints — carried from #199 + the ticket)

- **Seed-42 start island is LOCKED** (`[[world-is-big-round-island]]`). This impl touches `WorldBootstrap.BuildVista` ONLY; `LowPolyZoneGen.cs` (island heightmap / coast / scatter) is NOT modified.
- **`_FogCap` seam-kill MUST stay intact** — `RenderSettings.fogColor == WorldLookPalette.SkyHorizon`; never drift the fog constant (`lowpoly-quality.md` §1). Transparent-water-without-`_FogCap` is explicitly ruled out (§3-ruled-out) — the removal PR does not touch the water shader at all.
- **Honor the warm-bright anchor** — the empty horizon must read as a *bright calm day at sea*, NOT a cold grey overcast void. The warm horizon sky stop (`#CCE3EB`) + warm fog tint + the #194 warm-gold sun stay warm; a cold empty horizon would flip "hopeful castaway" into "bleak shipwreck-horror" and break the locked tone.
- **Preserve the #194 sun + the clouds** — they are the visual interest the mountains used to carry; do not remove them when removing the mountains.
- **Do NOT build the next-island / boat / reveal** (§3) — separate, gated tickets. Just don't break the fog dissolve.
- **Commit the regenerated `Boot.unity`** — a source-only change ships stale (`[[unity-procedural-committed-assets-go-stale]]`).

---

## Cross-references

- **Ticket** `86cagfn8h` (this impl) + `86cafffe8` (the direction ticket) + Uma #199 spec `team/uma-ux/open-horizon-direction-spec.md` (the options + the reveal-mechanism rationale).
- **`DECISIONS.md` 2026-06-30** (Option A, via PR #206) — the Sponsor's pick.
- **Real files cited (verified live):** `Assets/Scripts/Editor/WorldBootstrap.cs` (`BuildVista` L398, called L150; `BuildMountainCluster` L500; `BuildLandmassBase` L560; `BuildClouds` L144; far-clip L174); `Assets/Scripts/Editor/LowPolyMeshes.cs` (`FacetedMountain` L853; `FacetedLandmass` L1029); `Assets/Scripts/Editor/QualityPassGen.cs` (`EnableGlobalFog`, `BuildGradientSkybox`); `Assets/Shaders/LowPolyVertexColor.shader` (`_FogCap` ~L49); `Assets/Shaders/GradientSkybox.shader` (`_HorizonColor`); `Assets/Scripts/Runtime/WorldLookPalette.cs` (`SkyHorizon` = `#CCE3EB`, L37); `Assets/Scripts/Runtime/WorldLookConfig.cs` (`MtnFadeCap` 0.25 / `MtnDistanceScale` 0.62).
- **PR #194** (`drew/86cabc743-sky-poc`, warm-gold sun) — the HARD gate; touches the same 3 files; the impl branches off post-#194 main and composes with the sun.
- **`team/uma-ux/world-look-polish-direction.md`** §2 (the mountains being removed) + §3 (the fog/sky dissolve being re-pointed) + **`beach-water-direction.md`** (the ocean teal look + orbit-to-horizon capture convention).
- **`.claude/docs/lowpoly-quality.md`** §0 (real-world anchor) / §1 (`_FogCap` + seam-kill, don't drift the fog colour; `FacetedMountain`/`FacetedLandmass` in `LowPolyMeshes.cs`) / §3 (transparent-water-without-fog-cap ruled out) + **`unity-conventions.md`** §Editor-vs-runtime (false-green-capture + committed-asset-stale).
- **Memory:** `[[world-is-big-round-island]]`, `[[physical-features-anchor-realworld-not-metric]]`, `[[unity-procedural-committed-assets-go-stale]]`, `[[verify-grounding-soaks-by-gameplay-cam-visual]]`, `[[soak-build-stamp-is-merge-ref-not-headsha]]`, `[[soak-handoff-path-and-explicit-test-checklist]]`, `[[sponsor-merge-approval-soak-or-complete]]`.
