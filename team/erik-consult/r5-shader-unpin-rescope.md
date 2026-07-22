# R5 Re-scope — URP/Lit + URP/Unlit AlwaysIncludedShaders Unpin

## Question
Ticket `86cahk7k8`. The poly-plan item R5 (`team/analysis/2026-07-01-poly-style/consolidated.md` §5
Tier-1 item 9, sourced from `team/analysis/2026-07-01-poly-style/performance.md` lines 68-72)
proposed unpinning `URP/Lit` + `URP/Unlit` from `AlwaysIncludedShaders` to cut CI build time +
player size, premised on a **single pin site** (`WorldBootstrap.cs:106-108`). Devon's Wave1-A
implementation (PR #227, ticket `86cahhff6`) DESCOPED it after finding the pin topology was far
wider than assumed and that a code-only removal ships nothing (the bootstrap regen ritual
regenerates the settings asset). Does the win survive re-scoping, and if so what does a safe
implementation actually look like?

## Bottom line
**DO, but re-scoped to a multi-site removal + a NEW machine gate, not the 1-line fix the plan
assumed.** The build-time claim is directionally real (Moderate evidence: `AlwaysIncludedShaders`
is community-confirmed — not officially documented — to compile a shader's full addressable
variant space, and a real reported case hit 1.5M variants / 30h+ build time from one custom
shader added there) but the actual *cost on THIS project* is bounded by the URP Asset's own
feature gates (`AdditionalLightShadowsSupported: 0`, single directional light, no light-layers) —
so the realistic win is build-minutes on the single CI runner, not a headline number, and it has
never been measured. The safe route removes the pin from the code path (not the .asset directly)
at the **single upstream site that owns URP/Lit for the scatter/terrain path** (`WorldBootstrap.cs`)
— but ONLY after confirming (it is NOT confirmed) that no *other* independent
`EnsureShaderAlwaysIncluded(litShader/unlitShader)` call site re-adds it in the same bootstrap
pass, because there are at least 21 such call sites across 4 files, not the ~15-site count Devon's
descope cited. A build-time before/after diff plus the existing capture-gate suite (magenta is
visually loud) are both required; neither alone proves safety.

## Evidence

### 1. Is the build-time / player-size win still material given the real pin topology?

**Ground truth on pin topology (own Grep, `c:/Trunk/PRIVATE/Far-Horizon` main checkout, HEAD
`844e8d5`):**

- `WorldBootstrap.cs:106-108` — the ONE call the plan doc cited: `Shader.Find("Universal Render
  Pipeline/Lit")` → `EnsureShaderAlwaysIncluded(litShader)`, commented "pin it so scatter never
  strips."
- But `EnsureShaderAlwaysIncluded` is independently re-implemented (not shared) in **4 files**,
  each with its own idempotent add-if-absent logic against `ProjectSettings/GraphicsSettings.asset`
  `m_AlwaysIncludedShaders`:
  - `WorldBootstrap.cs:720-741` — guards `FarHorizon/LowPolyVertexColor`, `FarHorizon/LowPolyWater`,
    AND `Universal Render Pipeline/Lit` (3 shaders from 1 function, called from `BuildEnvironment()`
    at lines 98/104/108).
  - `MovementCameraScene.cs:3103-3119` — guards URP/Lit AND URP/Unlit. Grep of call sites inside
    the same file: `EnsureShaderAlwaysIncluded(litShader)` at **10 separate call sites** (lines 456,
    567, 1088, 1663, 1702, 2284, 2315, 2413, 2429, 2505) and `EnsureShaderAlwaysIncluded(unlitShader)`
    at **5 more** (lines 489, 793, 840, 1035, 2684) — one per authored prop/material (weapon-pack
    display stand, crafting table, campfire, log pile, hero-axe fallback material, etc.).
  - `CharacterAssetGen.cs:778-793` — a THIRD independent guard, called once at line 359 for URP/Lit
    (the castaway's de-lit toon material).
  - `QualityPassGen.cs:209-225` — a FOURTH independent guard, but for `FarHorizon/GradientSkybox`
    only (not URP/Lit or URP/Unlit) — cited for completeness, not part of the R5 surface.
  - `LowPolyZoneGen.cs` — **zero** local `EnsureShaderAlwaysIncluded` calls despite being the
    heaviest consumer of `Shader.Find("Universal Render Pipeline/Lit")` (12+ material-factory call
    sites for terrain/scatter/canopy/water/rock/trunk materials) — it relies entirely on
    `WorldBootstrap.BuildEnvironment()`'s single upstream pin at line 108, which runs before
    `LowPolyZoneGen`'s factories are invoked in the same function body.
  - `WeaponPackAssetGen.cs:245` (`Shader.Find("Universal Render Pipeline/Unlit")` for
    `Mat_WeaponPalette`) — **zero** local pin call. It relies entirely on `MovementCameraScene.cs`'s
    unlit-pin sites running later in the SAME `BootstrapProject.Run()` pass (`WeaponPackAssetGen.
    PrepareWeaponPack()` runs at `BootstrapProject.cs:94`; `MovementCameraScene.Author()` runs at
    `BootstrapProject.cs:526`, after it).
  - **Committed evidence of the current pin state**: `ProjectSettings/GraphicsSettings.asset:29-42`
    — `m_AlwaysIncludedShaders` presently lists exactly 6 project shaders by GUID:
    `3940cb47…`=`LowPolyVertexColor`, `933532a4…`=URP/Lit, `650dd952…`=URP/Unlit,
    `9c712bf4…`=`BlobShadowVertexColor`, `0f4a0477…`=`GradientSkybox`, `0c932efd…`=`LowPolyWater`
    (GUIDs cross-checked against each shader's own `.meta` file, verified match). This matches the
    "four FarHorizon shaders + URP/Lit + URP/Unlit" framing in the plan doc — the topology finding
    is about the CODE SITES that (re)write this list, not the list's current contents.
  - **Devon's "~15 sites across 4 files" (PR #227 descope, ticket `86cahhff6`) undercounts the
    call-site granularity** — the correct count at the individual `EnsureShaderAlwaysIncluded(lit/
    unlit)` invocation level is **21** across `WorldBootstrap.cs` (1) + `MovementCameraScene.cs`
    (15) + `CharacterAssetGen.cs` (1) + the zero-guard dependents (`LowPolyZoneGen.cs`,
    `WeaponPackAssetGen.cs` — 2 more files whose correctness depends on an upstream pin they don't
    own). Devon's "4 files" is right; "~15 sites" underestimates `MovementCameraScene.cs` alone.
    This does not change the descope verdict — it strengthens it: removing ONE call
    (`WorldBootstrap.cs:106-108`) leaves at least 15 other independent re-adds live in the very
    next function called by the same bootstrap pass (`MovementCameraScene.Author()`,
    `BootstrapProject.cs:526`).

- **Both URP/Lit and URP/Unlit are genuine runtime fallback shaders**, not just authoring-time
  conveniences — confirmed by reading the actual factory code in `LowPolyZoneGen.cs` (e.g. lines
  1888-1891, 1907, 1956-1960, 2017-2021: "Falls back to flat URP/Lit… if unresolved (never
  magenta)") and `MovementCameraScene.cs` (e.g. line 444: "A simple, build-safe URP/Lit material
  kept on the (disabled) renderer so it never ships pink"). The fallback pattern is deliberate and
  load-bearing across the codebase — every custom-shader material factory has a URP/Lit or
  URP/Unlit fallback branch that fires if `FarHorizon/LowPolyVertexColor` /
  `FarHorizon/LowPolyWater` fails to resolve. **Standing constraint confirmed correct**: the four
  FarHorizon shaders stay pinned (they ARE `Shader.Find`-reached at runtime, by name, from
  multiple call sites) — but so, functionally, is URP/Lit/Unlit, just less directly (as the
  fallback target, not the primary target).

**Grade A (project-code ground truth, self-verified via Grep + Read against the actual `main`
checkout — not inferred, not cited secondhand).**

**Does pinning URP/Lit cost anything in variant compilation on THIS URP config?** This is where
the plan doc's premise needed outside verification — Unity's own manual does not spell out the
exact mechanism.

- Official Unity manual pages (`docs.unity3d.com/Manual/shader-variant-stripping.html`,
  `docs.unity3d.com/6000.0/Documentation/Manual/urp/shader-stripping.html`,
  `docs.unity3d.com/6000.1/Documentation/Manual/shader-variant-stripping.html`) confirm
  `AlwaysIncludedShaders` exists specifically to prevent Unity's build-time variant stripper from
  dropping a variant the project needs at runtime, and that URP's OWN stripper still applies on top
  (based on which URP-Asset features are enabled — shadow types, additional-light count, etc.) —
  but **none of the three pages explicitly states whether listing a shader in
  `AlwaysIncludedShaders` compiles its FULL variant space or a bounded subset.** This is a genuine
  documentation gap, not an evidence-quality failure on my part — confirmed by direct fetch of all
  three pages. **(Grade A source, but the specific claim is NOT stated there — cite as "silent",
  not as confirming.)**
- Unity's own blog ("Unity Shader Variants Optimization & Troubleshooting Tips",
  `unity.com/blog/engine-platform/shader-variants-optimization-troubleshooting-tips`) could not be
  fetched (403 Forbidden on this session) — flagging rather than silently omitting.
- Two independent secondary sources DO make the explicit claim, and they agree with each other:
  - dev.to ("Unity Shader Variants Optimisation and Troubleshooting", Attilio Himeki, undated but
    recent per URP-era terminology): *"Shaders that are included as part of Always-included
    shaders list, under Project Settings - Graphics, will have all their variants included in the
    build."* Calls this "problematic" and recommends using the list "only when strictly necessary."
  - Unity Discussions forum thread ("Trying to understand how Shaders/Shader Variants are included
    in build", `discussions.unity.com/t/…/307984`) — community reply (not Unity staff, no staff
    reply present in the thread): *"When you include your custom shader in the 'always include
    shaders' section of the Graphics settings, Unity attempts to compile all possible variants,
    which can result in an extremely long build time."* The thread's original poster reports a
    concrete real-world number: a custom shader added to `AlwaysIncludedShaders` showed **1.5
    million variants and an estimated 30+ hour build time** on their project.
  - **Grade B (Moderate)** — two independently-authored technical write-ups agree, one backed by a
    reported empirical number, but neither is an official Unity statement and no staff reply
    confirms it in the forum thread. Treat as "likely true, community-consensus, not
    Unity-authoritative."
- **What this means concretely for THIS project:** URP/Lit's addressable variant space is large in
  the abstract (it supports many keyword combinations: shadow types × additional-light modes ×
  fog × lightmap modes × GPU instancing × DOTS instancing × ...), but the actual number that
  survives URP's OWN stripper on top of `AlwaysIncludedShaders` is bounded by
  `Assets/Settings/FarHorizonURP.asset`'s feature flags — confirmed from the asset itself:
  `m_MainLightShadowsSupported: 1`, `m_AdditionalLightShadowsSupported: 0`,
  `m_AdditionalLightsRenderingMode: 1` (per-vertex/per-pixel path fixed, not both),
  `m_SupportsLightLayers: 0`, `m_MixedLightingSupported: 1`. This is a modest feature surface (one
  shadowed directional light, one unshadowed point light per the consolidated-plan §Honest
  headline (perf)) — so URP/Lit's compiled count here is realistically in the dozens-to-low-hundreds
  of variants, not the 1.5M anecdote (that case was almost certainly a custom shader with many
  `#pragma multi_compile` keyword sets and no per-feature stripping applied at all — URP's built-in
  shaders DO get URP's stripper applied, unlike a fully custom shader). **No measurement exists on
  this project** — the plan doc's own §5 item 9 admits the estimate is code-derived, and S2a
  (`-development` build path + first profiler capture, also Tier 1) has not yet landed.
- **`FarHorizonBuilder.cs:50-51`** already logs `summary.totalTime` and `summary.totalSize` on
  every CI build (`Debug.Log($"[FarHorizonBuilder] result={summary.result} size={summary.totalSize}
  bytes time={summary.totalTime} -> {exe}")`) — this is the exact instrument needed for a
  before/after diff, and it already exists; no new logging code is required, only a captured
  baseline + a post-change comparison.

**Verdict on Q1:** the win is REAL in direction (build-time and size both drop when fewer variants
compile) but UNMEASURED in magnitude on this project, and the plan doc's framing ("force-compiles
their full variant space") is Moderate-not-Strong evidence, not Unity-manual-confirmed. Given the
project's modest URP feature surface, expect a modest (not dramatic) build-minutes win — material
enough to be worth doing on a single, scarce CI runner, but not a number to promise the Sponsor.

### 2. The SAFE unpin route — sites, order, and WHERE the change must live

**Two candidate approaches; only one is safe given the topology in §1.**

- **UNSAFE (what the original plan implicitly assumed): delete only `WorldBootstrap.cs:106-108`.**
  Ships nothing changed — `MovementCameraScene.Author()` (called at `BootstrapProject.cs:526`,
  after `WorldBootstrap.BuildEnvironment()` at `BootstrapProject.cs:105`, in the SAME
  `BootstrapProject.Run()` pass) independently re-adds URP/Lit via its own
  `EnsureShaderAlwaysIncluded` at 10 call sites, and re-adds URP/Unlit at 5 more.
  `CharacterAssetGen.PrepareCharacter()` (`BootstrapProject.cs:86`, runs BEFORE
  `WorldBootstrap.BuildEnvironment` in the sequence) also independently re-adds URP/Lit. The net
  post-bootstrap `GraphicsSettings.asset` is unchanged — the "fix" is invisible and the whole
  ticket accomplishes nothing while looking done (git diff on `WorldBootstrap.cs` shows a real
  code change; the committed `.asset` after a re-bake shows no change at all). This is almost
  certainly what Devon's descope discovered and is exactly why a code-only removal at one site
  "ships nothing" per the ticket brief.

- **SAFE: remove the pin call from EVERY guard function** (`WorldBootstrap.cs`,
  `MovementCameraScene.cs`, `CharacterAssetGen.cs`) simultaneously, in ONE PR, so that after the
  next `BootstrapProject.Run()` re-bake, NONE of the ~21 call sites re-adds URP/Lit or URP/Unlit.
  Concretely:
  1. `WorldBootstrap.cs:106-108` — delete the `litShader` resolve + `EnsureShaderAlwaysIncluded`
     call (keep the function's other two calls, `vcShader`/`waterShader`, untouched — those are the
     two FarHorizon shaders, standing-constraint-pinned).
  2. `MovementCameraScene.cs` — delete all 10 `EnsureShaderAlwaysIncluded(litShader)` call sites and
     all 5 `EnsureShaderAlwaysIncluded(unlitShader)` call sites (leave the `Shader.Find(...)` resolve
     calls themselves untouched — those still build the materials; only the pin-registration call is
     removed). Leave the file's `FarHorizon/LowPolyVertexColor` and `FarHorizon/BlobShadowVertexColor`
     pin calls at lines 1654/2668 untouched (those ARE the standing-constraint FarHorizon shaders).
  3. `CharacterAssetGen.cs:359` — delete the `EnsureShaderAlwaysIncluded(litShader)` call at the
     castaway-material build site.
  4. **Do NOT hand-edit `ProjectSettings/GraphicsSettings.asset` directly.** Per the plan doc's own
     §1 constraint 6 (and confirmed from `BootstrapProject.cs:76-81`: `Run()` unconditionally calls
     `ConfigureUrp()` — the exact function the plan doc names as the silent-revert chokepoint — on
     every invocation) and per the pattern already established by every other
     `AlwaysIncludedShaders` write in this codebase (all four are SerializedObject writes from
     C#, none are direct `.asset` edits), any code-only OR asset-only partial fix reverts on the
     next regen. The fix lives ONLY in the C# call sites; the `.asset`'s new post-fix content is a
     downstream ARTIFACT of running the bootstrap once more and committing the result — never a
     hand-edit target.
  5. After the code change, run `BootstrapProject.Run` headless once, then commit BOTH the code
     diff AND the regenerated `ProjectSettings/GraphicsSettings.asset` (the standard "committed-
     generated-assets" rule already governing every other bootstrap-authored file in this repo, per
     `unity-conventions.md`'s bootstrap-churn-restore ritual and the plan doc's §1 constraint 6).
     Verify the diff on `GraphicsSettings.asset:37-42` shows exactly 4 remaining entries
     (`3940cb47…`, `9c712bf4…`, `0f4a0477…`, `0c932efd…` — the four FarHorizon shaders) with
     `933532a4…` (URP/Lit) and `650dd952…` (URP/Unlit) GONE.
  6. **Order:** do all three files' removals in one PR (they are one indivisible unit — a partial
     removal is the "ships nothing" failure mode restated, just spread across 2 files instead of
     1). There is no safe partial/staged version of this change given the topology.

- **What breaks (magenta) if a runtime fallback path is reached post-strip:** if URP/Lit is
  ACTUALLY removed from the build and some runtime code path still needs it as a fallback (e.g. a
  `FarHorizon/LowPolyVertexColor` resolve failure at runtime falling back to
  `Shader.Find("Universal Render Pipeline/Lit")`, which then returns a shader object whose GPU
  program was stripped from the player), the fallback material renders **magenta** — this is the
  exact "spike's magenta failure class" every comment in this codebase cites
  (`unity-conventions.md` §Build stripping, `ZoneDLookTests.cs:171-190`,
  `WorldLookSceneTests.cs:410-426`). Concretely at risk: every "Falls back to flat URP/Lit… (never
  magenta)" comment in `LowPolyZoneGen.cs` becomes FALSE post-unpin for the specific failure mode
  it was written to prevent (custom shader missing → URP/Lit fallback → now ALSO stripped →
  magenta after all). This is a genuine, non-hypothetical regression risk — it only fires if the
  PRIMARY custom shader ALSO fails to resolve at runtime (a rare double-failure), but the whole
  point of the fallback chain was defense-in-depth against exactly that, and this change removes
  one layer of it for the fallback target specifically (not the primary shaders, which stay
  pinned).

**Grade A (own topology read + own trace of `BootstrapProject.Run()`'s call order, confirmed line
numbers cited above).**

### 3. What PROVES safety — name the concrete gate(s)

**No single existing gate proves this safe on its own; two are required together, and one does
not yet exist.**

- **The capture-suite magenta check (EXISTS, necessary but not sufficient alone).** The CI
  `unity` job (`.github/workflows/ci.yml`) already runs `capture_gate.sh` (general gameplay
  capture) plus `-verifySettings`, `-verifyPond`, `-verifyLoot`, `-verifyWater`, `-verifyChop`
  windowed capture modes, each producing PNGs uploaded as artifacts. A magenta material anywhere
  in frame during any of these would be visually obvious in the PNGs — this is the loud, cheap,
  already-running check. **Gap: nobody currently automates a pixel-scan for magenta** (RGB
  ≈(1,0,1)) — the check today is a human looking at the PNG, which is fine for a PR review but is
  not a hard CI gate that blocks merge on its own. Given the plan doc's own framing ("any strip
  regression = magenta = instantly caught by the shipped-build capture gates"), a human-reviewed
  capture set is the intended gate here, consistent with every other visual gate in this project's
  Testing Bar (`team/TESTING_BAR.md`, shipped-build capture gate). **Sufficient for catching a
  regression a reviewer actually looks at; NOT sufficient as an unattended machine gate unless a
  pixel-scan script is added** (not currently planned scope — flag as an optional hardening, not a
  blocker).
- **A shader-strip build-log report (DOES NOT EXIST — must be authored).** No current CI step
  parses `build.log` for shader/variant information; `check_unity_log.sh` only greps for compile
  errors, `frame_check.py` only analyzes capture PNGs. Unity's `BuildReport` API (already the
  object `FarHorizonBuilder.cs` reads for `summary.totalTime`/`summary.totalSize`) exposes a
  `BuildReport.strippingInfo` (a `StrippingInfo` scriptable object listing what got stripped and
  why) that is NOT currently logged. Adding one `Debug.Log` line reading
  `report.strippingInfo?.includedModules` / reason strings would give a genuine "URP/Lit variant
  count dropped from N to M" artifact — this is the single most direct proof of the mechanism
  actually changing, and it is cheap to add (one small `FarHorizonBuilder.cs` edit, no new CI
  step, rides the existing `build.log` upload).
- **A variant-count / build-time diff (PARTIALLY EXISTS — instrument present, no baseline
  captured).** `FarHorizonBuilder.cs:50-51` already logs `summary.totalTime` + `summary.totalSize`
  on every build; this is the exact number needed for a before/after comparison. **No baseline
  exists today** — this ticket's implementation PR should capture the PRE-change number (grep the
  most recent green `unity` job's `build.log` on `main` for the `result=` line) alongside the
  POST-change number in the same PR body, per this project's Testing Bar evidence-citation
  convention. This is Devon's-or-whoever's-implementation-PR job, not something this research note
  can produce (no CI access from this dispatch).
- **EditMode pin-flip tests.** `Assets/Tests/EditMode/ZoneDLookTests.cs:171-190` and
  `Assets/Tests/EditMode/WorldLookSceneTests.cs:410-426` assert the FarHorizon shaders remain
  registered — these stay GREEN and unmodified by this change (they don't assert on URP/Lit/Unlit
  at all). **No test currently asserts URP/Lit/Unlit are ABSENT from `AlwaysIncludedShaders`** —
  the implementation PR should add one (mirroring the existing pin-assertion pattern, inverted:
  `Assert.IsFalse(registered, "URP/Lit should no longer be pinned post-R5-unpin")`) so a future
  regression (someone re-adding a pin call) is caught mechanically instead of silently reverting
  the win.

**Named gate set for the implementation PR (all four, not a subset):**
1. Capture-suite magenta check (existing, human-reviewed) — PASS required.
2. NEW: `BuildReport.strippingInfo` logged in `build.log` — read, not asserted (informational).
3. NEW: before/after `summary.totalTime`/`summary.totalSize` diff cited in the PR body (pre-change
   baseline pulled from the last green `main` build log).
4. NEW: EditMode test asserting URP/Lit + URP/Unlit are ABSENT from `m_AlwaysIncludedShaders`
   post-bootstrap (regression guard against a future re-add).

**Grade A** for what exists today (verified by reading the actual CI workflow + scripts + test
files); **Grade A** for what's missing (absence confirmed by Grep returning no hits for
`strippingInfo` / any inverse-pin-assertion pattern anywhere in the repo).

### 4. Recommendation

**DO — re-scoped outline, not the original 1-line plan.**

This is NOT a DROP-and-document case like the gamma/Forward+/Mono truth-ups (those are deliberate,
zero-cost-today deviations with a named future trigger). R5 has a real, if unmeasured, build-time
cost sitting on the project's single scarcest CI resource (the one self-hosted Unity build slot,
per `single-unity-build-slot-serializes-orchestration` and the plan doc's §1 constraint 7) — every
CI run pays this cost today and would keep paying it under DEFER. It is also NOT a DEFER-with-
trigger case (no future event makes this safer or cheaper to do later — the topology only grows as
more `MovementCameraScene`-style authoring call sites get added, per the trend already visible: 15
call sites in one file already). The original plan doc's PR-A batching (item 9, same regen wave as
R1/R2a/G6-2/G3/S3) remains directionally right — this rides an EXISTING scheduled regen, so the
CI-slot cost of doing it is close to zero marginal cost if bundled correctly.

**Re-scoped outline for the implementation ticket:**
1. Remove all ~21 `EnsureShaderAlwaysIncluded(litShader/unlitShader)` call sites across
   `WorldBootstrap.cs` (1), `MovementCameraScene.cs` (15), `CharacterAssetGen.cs` (1) in ONE PR —
   partial removal reproduces the "ships nothing" failure Devon already found.
2. Add the `BuildReport.strippingInfo` log line to `FarHorizonBuilder.cs` (cheap, informational,
   no new CI step).
3. Add the inverse-pin EditMode regression test (mirrors `ZoneDLookTests.cs`'s existing pattern).
4. Re-run `BootstrapProject.Run`, commit the regenerated `GraphicsSettings.asset`, verify the diff
   shows exactly the 4 FarHorizon-shader GUIDs remaining.
5. Cite the pre-change `main` build-log `time=`/`size=` numbers in the PR body against the
   post-change numbers from this PR's own CI run — this is the number that actually tells the
   Sponsor/team whether the win was material, since no measurement exists yet.
6. Fold into the plan doc's existing PR-A "Dead cost + caster policy" regen wave (R1 + R2a/G6-1 +
   G6-2 + G3 + S3 + R5) OR split R5 into its own regen if the reviewer prefers isolating the
   strip-risk class — the plan doc already names this exact optionality
   (consolidated.md:120, "R5 can split out if reviewer prefers isolating the strip-risk class —
   costs one extra build-slot occupation").
7. **If the post-change `build.log` diff shows a build-time delta under ~2-3% on this modest URP
   feature surface** (a real possibility given §1's Grade-B evidence and the project's small
   light/shadow feature set), that is still a valid, cheap, permanent win worth banking — do not
   treat "the number was small" as a reason to revert; the code-hygiene value (one fewer implicit
   fallback-shader class to reason about) stands on its own once the risk is gated.

## Application to Far Horizon

This sits entirely inside the standing constraint the ticket brief already states — the four
FarHorizon shaders (`LowPolyVertexColor`, `LowPolyWater`, `GradientSkybox`,
`BlobShadowVertexColor`) stay pinned, confirmed both by reading their own fallback-chain comments
(they ARE genuinely `Shader.Find`-by-name-reached at runtime with no compile-time reference) and
by the fact this note's proposed change never touches their pin call sites. The change is scoped
strictly to URP/Lit + URP/Unlit, which the codebase treats as a SECONDARY fallback target for
those four shaders' own failure paths, not a primary custom-shader dependency — removing their pin
narrows (does not eliminate) the codebase's defense-in-depth against the magenta failure class,
which is why the gate set in §3 leans on the EXISTING shipped-build capture gate (this project's
standing visual-regression mechanism per `CLAUDE.md`'s "Shipped-build capture gate" hard rule) plus
one new mechanical regression test, rather than inventing a parallel verification system. The
implementation stays inside the single-CI-build-slot discipline (`CLAUDE.md`'s "≤2 Unity-build
tickets in flight" rule + the plan doc's §1 constraint 7) by riding an already-scheduled regen wave
rather than requesting a standalone build-slot occupation, consistent with how every other Tier-1
plan item is sequenced. No Blender / Hyper3D / procedural asset-route implications — this is pure
build-configuration surface, touching none of the three asset-creation routes.
