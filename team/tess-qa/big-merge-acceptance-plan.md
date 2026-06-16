# THE BIG MERGE — Combined #50 Build · QA Acceptance Plan

**Status:** PLAN ONLY (pre-stage, ticket `86ca9z1a6`). **QA EXECUTION IS NOT AUTHORIZED YET** —
it is gated on (a) the served combined #50 soak exe + (b) the Sponsor's look-approval of that soak.
Do NOT run any item below until the combined build is reconciled, served, and Sponsor-approved.
Reviewer at QA time: **Drew** (game-side).

**What this build is:** the single re-baked `Boot.unity` folding the seed-42 organic island
(`86ca9qwr3`) + the float-grounding fix (`86ca8rdkp`) + the warm-wide coast (`86ca9xyqa`) + the
moving/distinct water (`86ca9yn57`) + the held-axe facing-sweep (`86ca9xz00`) + the held-axe
no-bounce/no-ratchet (`86ca9ykp0`), plus the carries (stump axe, trees, debris/wreckage).

---

## 0. INFRASTRUCTURE NOTE — read before citing any flag or test below

The verify/diagnostic flags + named tests fall into TWO classes. Mark each result accordingly:

- **CONFIRMED on `main` today** (verified 2026-06-16 on `origin/main` @ `d9dbf62`): the soak chain
  `serve_soak.sh` → `verify_build_stamp.py` → `capture_gate.sh` (launches **WINDOWED** via
  `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`) → `frame_check.py`; the verify
  flags **`-verifyAxe / -verifyCastaway / -verifyChop / -verifyCraft / -verifyLoop / -verifyMove /
  -verifySea`**; named tests `WaterSceneTests`, `HeroAxeSceneTests`, `CastawayCharacterTests`,
  `MovementCameraSceneTests` (EditMode) + `HeldAxePlayModeTests`, `MovementCameraTests` (PlayMode).
- **EXPECTED FROM A #50 SOURCE PR — NOT on main yet, CONFIRM IT SHIPPED at QA time.** The
  combined build's NEW surfaces (`CastawayCharacter.modelSoleGround` + ground-snap; `FullscreenBoot`;
  seed-42 island gen; coast widening; water-wave motion; axe facing-sweep / walk-trace) and any new
  verify/diag flags those PRs add (the ticket's `-verifyCoast` / `-coastWaves` / `-axeWalkTrace`
  are illustrative "e.g." names — `-axeWalkTrace` IS a documented per-frame axe-Y instrument per
  unity-conventions §FBX axe-ratchet; the coast ones may differ). **For any expected flag/test:
  first `grep` the combined HEAD to confirm the real name; if a surface ships NO capture flag,
  it is verified by the GAMEPLAY-CAM shipped-exe capture, never skipped.**

**The float-saga lesson is law here:** grounding / visual items are judged by the **DEFAULT
GAMEPLAY-CAM (player over-shoulder) VISUAL**, NOT by any metric. Every automated metric lied across
9 float iterations (root-snap, bounds-snap, per-frame BakeMesh all read "planted" while the player
view floated). A higher-angle / subject-framing / zoom-to-fit capture MASKS a defect — capture from
the anchored gameplay orbit at real pitch/zoom (~14u / 55°), MULTIPLE angles.

---

## 1. PRE-FLIGHT (do FIRST — before judging anything)

- [ ] **P1 · Stamp identity == merged SHA.** Launch the served exe; read the HUD
      `BUILD <tag> | <UTC> | <sha>` (top-right). The `<sha>` MUST equal the combined-build merged
      HEAD. Independently confirm by grepping the baked stamp out of
      `Build/Windows/FarHorizon_Data/resources.assets` (or trust `serve_soak.sh`'s stamp-vs-HEAD
      guard). **NEVER trust the `FarHorizon.exe` stub mtime** — it is an unchanging engine stub; only
      `FarHorizon_Data/*` is rewritten per build. Stamp ≠ HEAD → STOP, stale artifact (the
      load-bearing failure mode behind "verify the stamp before judging"). · `86ca9z1a6`
- [ ] **P2 · Fresh serve, no runner contention.** Soak exe built by a FRESH `serve_soak.sh` from a
      CLEAN detached checkout at the merged SHA (detach to the SHA — do NOT `git checkout <branch>`
      if the integrator's worktree holds it). Confirm no in-flight CI (`gh run list`) and no resident
      Unity/exe before building — two Unity builds race the single `far-horizon-local` runner (EPERM).
- [ ] **P3 · Self-Test Report present on the #50 PR.** UX-visible PR → author Self-Test Report
      comment is a HARD gate. Missing → REQUEST CHANGES (combined builds may keep the report in the
      PR body if every source feature PR carried its own; one-line audit comment still expected).
- [ ] **P4 · Binary-scene carry proof — ALL feature-sets' scene-presence EditMode tests green
      TOGETHER on the merged SHA.** A regenerate-on-rebase can silently DROP one feature's content
      from the single re-baked `Boot.unity` (binary scenes can't be GUID-grepped — the EditMode
      scene-presence test is the only authoritative reader). Confirm each set's presence suite passes
      in the SAME run: island/terrain, float/castaway, coast, water, held-axe, stump-axe, debris.

---

## 2. FLOAT GROUNDING — `CastawayCharacter.modelSoleGround` (`86ca8rdkp`)

**The defect class:** the visible mesh-bottom floats above the sand the player SEES, even while the
NavMeshAgent root is correctly grounded (the slab rides above the dipping foreshore terrain; the
Walk clip authors the hips ~0.66u higher than Idle). Fixed via the scale-immune model-child local-Y
offset `modelSoleGround`. **Verify by gameplay-cam VISUAL only — every metric lied here.**

- [ ] **F1 · Feet planted STANDING.** From the default over-shoulder gameplay orbit at spawn, the
      castaway's soles sit ON the sand — no air gap, no sinking. Multi-angle (over-shoulder + a low
      orbit that grazes the waterline). · `86ca8rdkp`
- [ ] **F2 · Feet planted WALKING.** Click-to-move across flat sand + grass; watch the FEET frame-to
      -frame through the WALK clip (the Walk-clip hip-lift is the float trigger — Idle alone can pass
      while Walk floats). Soles stay on the surface the whole traverse, no per-step lift. · `86ca8rdkp`
- [ ] **F3 · Feet planted ON HILLS.** Walk UP and ACROSS a hill slope; soles track the sloped
      visible terrain (the snap must pick the renderer-ENABLED ground hit, not the topmost slab).
      No float on the climb, no clipping into the slope. · `86ca8rdkp`
- [ ] **F4 · Blob shadow tracks the feet.** The ground shadow sits UNDER the snapped soles, not
      stranded above them (a shadow floated ~9cm reads as "floating" even when the body is grounded).
- **Verify method:** shipped-exe `-verifyCastaway` capture (CONFIRMED flag) for the standing read
  + a gameplay-cam **walk-traverse capture sequence** (multi-frame, walking + on a hill) — the
  walk/hill reads are NOT provable from a static existence capture. Confirm `modelSoleGround` shipped
  (grep the combined HEAD). **Pass criteria:** soles visibly ON the sand in EVERY frame (standing,
  mid-walk, mid-climb) from the player-default orbit; shadow under the feet. **A metric reading
  "GAP≈0" is NOT a pass — the eyes-on gameplay-cam frame is.**

---

## 3. SEED-42 ORGANIC ISLAND (`86ca9qwr3`)

**The defect class:** a procedural island that reads as a SQUARE, a straight LINE/edge, or a strip
with land running off one side — instead of an irregular landmass with water on all sides.

- [ ] **I1 · Irregular organic shape.** From a high/wide orbit (or the seaward gameplay orbit panned
      around), the island outline is IRREGULAR (radial heightmap, lumpy coast) — **NO square corner,
      NO straight machined edge, NO ruler-line shoreline.** · `86ca9qwr3`
- [ ] **I2 · Water on ALL sides.** Orbit the full 360° (or sample N≥4 cardinal seaward views): water
      meets land on every side — the island does not run off-frame as a strip. · `86ca9qwr3`
- [ ] **I3 · Dense jungle / scatter reads** on the island interior (trees + scatter present, not a
      bald dome) at gameplay distance. · `86ca9qwr3`
- **Verify method:** shipped-exe gameplay-cam captures at N≥4 azimuths (cardinal seaward + a wide
  high orbit) + `-verifySea` (CONFIRMED) for the water-meets-land read from the seaward orbit. If the
  island PR ships a dedicated island/shape EditMode test (grep the combined HEAD — e.g. an
  irregularity / no-straight-edge / water-on-all-sides guard), cite it AND eyes-on the captures
  (a guard can pass while the shape still reads wrong — guard the PERCEPT). **Pass criteria:** no
  square / no straight line / no off-side land-runoff at ANY azimuth; water all sides; interior not bald.

---

## 4. NAVMESH — WALK EVERYWHERE, 100% COVERAGE (`86ca9qwr3`)

- [ ] **N1 · Click-to-move reaches everywhere on the island,** including UP onto and across the
      hills (the new terrain must bake a connected NavMesh — flat connectors don't stitch to sloped
      low-poly meshes beyond `agentClimb`; seam columns must be pinned). No dead zones, no
      unreachable interior, no "click does nothing" patches. · `86ca9qwr3`
- [ ] **N2 · No float introduced by the new terrain** (composes with §2 — walk the hills and confirm
      the soles plant on the SLOPED visible surface, not the flat slab above it). · `86ca8rdkp` + `86ca9qwr3`
- **Verify method:** `-verifyMove` (CONFIRMED — agent-on-NavMesh + click-move-reached planar
  tolerance) for the mechanical proof + a hands-on / scripted click-traverse on the SHIPPED exe
  walking spawn → interior → up a hill → opposite coast (the voxelizer auto-bridges synthetic gaps,
  so connectivity must be judged on the REAL scene, not a test stub). **Pass criteria:** every
  region the player can see they can reach; hills walkable; no float on slopes; `-verifyMove` green.

---

## 5. CAMERA — NO CLIP UNDER HILLS · WINDOWED LAUNCH (`86ca9qwr3`)

- [ ] **C1 · No camera clip under hills/terrain.** Orbit the camera while standing near/under a
      hill; the orbit camera does NOT punch through the terrain and show the underside / skybox-
      through-ground. Walk to the base of the tallest hill and orbit low. · `86ca9qwr3`
- [ ] **C2 · WINDOWED launch.** The build launches in WINDOWED mode for the normal soak (the capture
      gate runs windowed 1280×720; the normal launch mode is what the Sponsor judges).
      `FullscreenBoot` (if it shipped in this combine) must stay INERT on `-verify*`/`-captureGate`
      flags so QA captures stay windowed — confirm the gating, and confirm the NORMAL launch window
      mode matches the spec. · `86ca9qwr3`
- **Verify method:** shipped-exe orbit-around-hill capture sequence (multi-frame, low orbit at the
  tallest hill) for C1; for C2 confirm `capture_gate.sh` produced windowed 1280×720 frames AND
  (if `FullscreenBoot` shipped) the runtime log shows capture/verify launches NOT forcing fullscreen.
  **Pass criteria:** no ground-clip / underside-reveal at any orbit angle near hills; windowed
  launch confirmed.

---

## 6. COAST — WARM WIDE SAND · NO GAP · FOAM (`86ca9xyqa`)

- [ ] **K1 · Warm WIDE sand band.** The shoreline carries a generous warm-sand band (not a thin
      strip), readable at gameplay distance, warm-toned (not grey/cold). · `86ca9xyqa`
- [ ] **K2 · Water-to-sand: NO dry gap.** The water meets the sand at the waterline with NO dry void
      band between them and NO floating-water-slab read (check the foam/surf sits at the REAL
      waterline, not metres out to sea — a "water floats above the beach" percept is usually a
      composition/sea-extent problem, diagnose reach first). · `86ca9xyqa`
- [ ] **K3 · Foam / surf band reads** at the shoreline (the wet edge), from the seaward gameplay
      orbit. · `86ca9xyqa`
- **Verify method:** shipped-exe gameplay-cam captures from the seaward orbit + a low waterline orbit
  (multi-angle). If the coast PR ships a dedicated flag (the ticket's illustrative `-verifyCoast`)
  or a coast-width / waterline-gap EditMode test, grep the combined HEAD for the REAL name and cite
  it alongside eyes-on. **Pass criteria:** wide warm sand; zero dry gap at the waterline; foam present;
  no floating-water read.

---

## 7. WATER — DISTINCT FROM SKY · MOVING WAVES (`86ca9yn57`)

**The defect class:** the sea reads IDENTICAL to the sky (no horizon — backface-culled water shows
the skybox through it; or fog washes it out), and/or the water surface is STATIC (no wave motion).

- [ ] **W1 · Water DISTINCT from sky.** From the seaward gameplay orbit there is a clear HORIZON —
      the water is a different value/hue from the sky, not a single washed band. (The water top must
      be the FRONT face — `Cull Back` culls by triangle WINDING, not the normal; a normal-guard can
      be green while the mesh is culled and shows sky-through.) · `86ca9yn57`
- [ ] **W2 · Waves MOVING — multi-frame.** Capture the water across MULTIPLE frames (N≥3, spaced in
      time); the surface visibly MOVES (wave displacement / animation) between frames — a single
      frame cannot prove motion. · `86ca9yn57`
- **Verify method:** `-verifySea` (CONFIRMED) for the distinct-from-sky horizon read by sampling
  actual WATER PIXELS (not the normal attribute — `WaterSceneTests` + the winding/up-facing guards);
  for motion, a **multi-frame** capture sequence (the ticket's illustrative `-coastWaves` — grep the
  combined HEAD for the real wave-capture flag; if none, capture N≥3 timed frames and pixel-diff the
  water region). **Pass criteria:** non-zero water pixels at the horizon, distinct from the
  `SkyHorizon` color (W1); measurable per-frame water-pixel displacement across N≥3 frames (W2).
  **A static-looking single frame is NOT a pass for W2.**

---

## 8. HELD AXE — SEATED ALL FACINGS · NO BOUNCE/RATCHET · ARM-STABLE · FINGER-CURL (`86ca9xz00` + `86ca9ykp0`)

**The defect class (facing, `86ca9xz00`):** the held axe is seated only when facing ONE direction
and LAGS facing when the character turns (the grip anchor must be in the facing-carrying `_model`
frame, not the root — else the anchor eats the facing yaw). **A STATIC per-facing snapshot
FALSE-GREENS this** — the anchor settles to each pinned facing; you MUST verify with a DYNAMIC
facing sweep across multiple frames.

**The defect class (bounce/ratchet, `86ca9ykp0`):** the held axe BOUNCES per walk step and
RATCHETS upward (settles higher every step) because the slow grip-anchor integrates the Walk-clip
hip-lift / sole-grounding bob in a frame whose local-Y oscillates. Fixed by reconstructing the prop
output-Y from a stable grounded reference. **Verify the OUTPUT vertical for cumulative drift over
N walk steps, not that the anchor "settles."**

- [ ] **A1 · Seated across ALL facings — DYNAMIC sweep.** ROTATE the character through a continuous
      sweep of headings (N≥8 facings, captured WHILE turning across multiple frames). The axe stays
      gripped in the hand and its head/haft re-orients WITH the body at every heading — NOT pinned to
      one world direction, NOT lagging the turn by seconds. · `86ca9xz00`
- [ ] **A2 · NO walk-bounce, NO ratchet — multi-step walk.** Walk N≥8 steps; the held axe does NOT
      bob per step and does NOT climb cumulatively (it sits at the same grip height after the
      traverse as before). · `86ca9ykp0`
- [ ] **A3 · Arm-swing-stable.** The per-step arm-swing is damped out of the prop (the axe tracks the
      body + facing but is not flung by the clip arm-swing). · `86ca9ykp0`
- [ ] **A4 · Finger-curl on the haft.** While the axe is held, the fingers CURL around the haft (the
      Mixamo clip poses the hand OPEN; a HasAxe-gated curl driver closes it) — no "mangled / broken
      open finger" read with the haft passing through an open hand. · `86ca9xz00`
- **Verify method:** for A1, a DYNAMIC mid-turn facing sweep on the SHIPPED exe (N≥8 headings across
  frames) — NEVER static per-facing snapshots (the documented false-green). For A2/A3, a multi-step
  walk trace (the ticket's `-axeWalkTrace` — a documented per-frame axe-Y decomposition instrument;
  grep the combined HEAD to confirm it shipped) + eyes-on the walk-capture sequence. Pair with the
  PlayMode invariants if present (rel-to-hand-invariant-across-facings; bounded one-step move;
  cumulative-drift bounded over N steps). **Pass criteria:** A1 — axe re-orients with the body at all
  N≥8 headings, no facing-lag (DYNAMIC, not static); A2 — bounded per-step Y AND zero cumulative drift
  over N≥8 steps; A3 — arm-swing damped; A4 — fingers visibly curl on the haft. **N≥8 — do NOT claim
  "stable" on a 3-sample sweep.**

---

## 9. CARRIES — STUMP AXE · TREES · DEBRIS/WRECKAGE (must hold, no regression)

- [ ] **R1 · Stump axe** planted upright + BITING the chopping block from spawn (the diegetic
      "walk-here" cue); reads as bitten into the wood, not floating above / clipping through.
      Exactly-one-axe swap holds (stump hides, held appears — no dual-axe or zero-axe frame). ·
      `HeroAxeSceneTests` / `HeldAxePlayModeTests` (CONFIRMED) + `-verifyCraft` (CONFIRMED).
- [ ] **R2 · Trees / blob canopy + scatter** present and on-style (chunky faceted) at gameplay
      distance — not bald, not dark-sharding.
- [ ] **R3 · Beach debris / wreckage** intact at the landing (the castaway's shipwreck props
      present). · debris scene-presence + PlayMode tests.
- **Verify method:** the soak orbit capture (`capture_gate.sh` frames) + `-verifyCraft` for the swap
  + the debris/stump scene-presence suites (part of §1 P4). **Pass criteria:** stump axe bites; one
  axe at a time; trees + scatter + debris all present; no regression vs the last approved build.

---

## 10. MECHANICAL GATES — CI green on the merged SHA

- [ ] **M1 · EditMode suite green on the merged SHA** — `result=Passed`, `total > 0` from the
      `-testResults` XML `<test-run result="Passed">` line (NOT exit code). Record the EditMode count.
- [ ] **M2 · PlayMode suite green on the merged SHA** — same, record the PlayMode count.
- [ ] **M3 · Structure** (`structure_check.sh`) — no committed `Build/`/`Captures/`, metas present,
      entry points intact.
- [ ] **M4 · Console-error** (`check_unity_log.sh`) — no `error CS####` / `Compilation failed` /
      `Fatal error` / `Unhandled exception`; no `USER WARNING:` / `USER ERROR:` (URP first-import
      Terrain-layer noise + recovered-NavMesh-race allowlisted by SHAPE only).
- [ ] **M5 · Build-result** — `[FarHorizonBuilder] result=Succeeded size=<bytes>` in the build log.
- [ ] **M6 · Shipped-build capture gate** (`capture_gate.sh` + `frame_check.py`) — N real frames,
      not black / empty / uniform / all-magenta; windowed 1280×720.

---

## 11. VERDICT GATES — what BLOCKS the combined build from Sponsor soak

**REQUEST CHANGES (hard blockers):**
- Any grounding / visual AC failing from the DEFAULT GAMEPLAY-CAM at a non-convenient angle (a green
  isolated `-verify*` close-up is NOT a pass — the false-green class).
- Float: soles float standing, walking, OR on hills (judged by the gameplay-cam visual).
- Island reads as a square / straight line / land-runoff strip, OR water not on all sides.
- NavMesh dead zone / unreachable region / hills not walkable.
- Camera clips under hills (underside / sky-through-ground reveal).
- Coast: cold/grey or thin sand, a dry water-to-sand gap, OR no foam.
- Water: identical to sky (no horizon / culled-water-shows-sky) OR static (no wave motion over N≥3 frames).
- Held axe: NOT seated across a DYNAMIC facing sweep (facing-lag), OR walk-bounce / cumulative
  ratchet over N≥8 steps, OR open-finger "mangled" read.
- Stump axe not biting / dual-axe or zero-axe swap frame; trees or debris dropped.
- Either feature-set's scene-presence suite NOT green TOGETHER on the merged SHA (binary-scene carry
  dropped one set).
- Stamp ≠ HEAD (stale artifact) or missing Self-Test Report on the #50 PR.
- EditMode/PlayMode not green on the merged SHA.

**APPROVE_WITH_NITS (file follow-up, do NOT block closure):** cosmetic micro-pose the Sponsor can
finalize via the F9 nudge (held-axe micro-offset, stump bite depth); rock/coast warmth dialing;
verify-cam framing artifacts where the load-bearing fix is unambiguous.

---

## 12. EXECUTION DISCIPLINE (when QA is finally authorized)

- **Sample size N≥8** on every sweep claim (facings, walk steps, island azimuths). Do NOT claim
  "deterministic" / "stable" on N=3.
- **Multi-angle, gameplay-scale, every visual AC** (over-shoulder default + top-down + front +
  behind where the defect class lives there). One convenient screenshot is not a pass.
- **Detached SHA checkout** for the combined build (do not `git checkout <branch>` an integ branch
  the integrator's worktree holds — detach to the SHA or review from `origin/<branch>`).
- **Grep the combined HEAD** to confirm each EXPECTED-class flag/test (§0) actually shipped before
  citing it; if a surface ships no flag, verify by the gameplay-cam shipped-exe capture — never skip.
- Record the final verdict + the EditMode/PlayMode counts + the merged SHA + the baked-stamp grep in
  a VERDICT block appended to the bottom of this file at QA time.
