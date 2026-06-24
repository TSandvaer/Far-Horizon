# Acceptance / Test Plan — Three-Bar Need-Meter HUD (`86caamkxv`)

**Ticket:** `86caamkxv` — feat(ui): need-meter HUD — three need bars (warmth + hunger + thirst).
**Author (impl):** Devon · **Impl reviewer:** Drew · **QA:** Tess · **Spec author:** Uma (`86caamkxv` direction).
**Implement-to spec (the lock):** `team/uma-ux/hud-three-bar-spec.md` (Uma #125, the reconciled source — supersedes `need-meter-3bar-direction.md` + `need-meter-ui-direction.md` where they conflict).
**Hard-dep:** `86caamkv7` (THIRST need + pond, PR #124) merged to `main` — the HUD references the `ThirstNeed` type. Hunger (`86caamkp8`) is already on main.

> **What this doc is.** The AC→verify map QA runs before sign-off. It pins (a) what each AC means against the SHIPPED `SurvivalHud.cs` ground truth, (b) the exact test/assert that proves it, (c) the regression traps that a green-but-wrong build would slip past, and (d) the shipped-build soak probe list. It is NOT a re-design — Uma's spec is the design lock; this maps her spec + the ticket ACs to a verification.

---

## 0. Ground-truth state (verified on `main` + PR #124, 2026-06-24)

Read these as the FACTS the plan asserts against (not the stale ticket framings):

- **The HUD already ships TWO bars, not one.** `Assets/Scripts/Runtime/SurvivalHud.cs` on main has `DrawWarmthBar()` + `DrawHungerBar()`, serialized refs `public WarmthNeed warmth;` + `public HungerNeed hunger;`, wired editor-time by `BootstrapProject.cs`. The remaining work is the THIRD (thirst) bar + the `DrawWarmthBar`/`DrawHungerBar` → ONE `DrawNeedBar(...)` generalization.
- **Shipped anchors (the no-regression lock):** warmth `y = Screen.height - 44`, hunger `-80`. Both `x=16`, box `260×28`. Ledger currently `-116`.
- **New anchors (Uma §2.3 / §3.1):** thirst `-116` (top of the column); ledger moves UP to `-152`. Warmth `-44` + hunger `-80` UNCHANGED (AC3).
- **Shared grammar already in shipped code:** `SegmentCount = 10`, `PlateAlpha = 0.55f`, `FilledSegments()` = FLOOR rule, right-to-left empty, glyph-left dims to alpha 0.4 at empty, three discrete plates, `Charcoal #2E2A2B` shared emptied-segment color.
- **`ThirstNeed` surface (PR #124, verified):** extends `SurvivalNeed`; exposes the byte-identical HUD contract `Current01` / `Max` / `IsCritical` / `Changed` / `TickSeconds`. Satisfaction hook is **`AddWater(float)` / `AddWater()`** (NOT a generic `Satisfy`/`AddFood` name — the thirst PlayMode assert drives THIS). **⚠ Thirst seeds at `startFraction01 = 0.50` (`ThirstStartFraction01`), so it starts at ~5 segments, NOT full** — the coexistence assert must NOT expect "near-full" for the thirst bar (a hunger/warmth idiom mistake to guard against).

---

## 1. AC → verify map

### AC1 — three bars render together, each subscribes (no per-frame poll)

| | Verify |
|---|---|
| **Means** | The HUD draws warmth + hunger + thirst simultaneously; each bar reads its need's `Current01` from a value cached on `Changed` (the shipped subscribe-never-poll seam #124 verified for thirst); NO `Update()`/`OnGUI` polling of the need. |
| **EditMode** | (existing) `SurvivalHudTests` segment/band math green. NEW: `SurvivalHudThirstTests` — `ThirstBandColor` band mapping + shared `FilledSegments` FLOOR rule (mirrors `SurvivalHudHungerTests`). |
| **PlayMode** | NEW assert in `SurvivalHudPlayModeTests`: all three refs wired (`hud.warmth`, `hud.hunger`, `hud.thirst` all non-null after wire) AND each `FilledSegments(hud.<need>.Current01)` reads a sane lit count — proving the WIRING is live for all three, not just that the type compiles. |
| **Subscribe-never-poll (code read)** | Grep guard: `SurvivalHud.cs` `OnGUI` reads `<need>.Current01` only; NO `FindObjectOfType` in `OnGUI`/`Update` (the Awake fallback is the only `FindObjectOfType`, build-safety net). The bar pulls from the need surface the same way warmth/hunger do — **same HUD subscribe-never-poll seam #124 verified for `ThirstNeed.Changed`.** If the impl adds an `Update()` that polls a need, REQUEST CHANGES. |
| **Scene-presence** | NEW `ThirstNeedSceneTests` (or extend `WarmthNeedSceneTests`): `Boot.unity` carries the `ThirstNeed` serialized AND `hud.thirst` wired editor-time — mirrors `BootScene_CarriesSurvivalHud_WiredToTheNeed`. Catches the editor-vs-runtime serialization trap (a build that ships a null thirst ref → thirst bar silently never drawn). |

### AC2 — each bar distinguishable + consistent critical treatment

| | Verify |
|---|---|
| **Means** | Warmth = `▲` gold, hunger = `●` green, thirst = `◆` water-blue (Uma §2). The three band palettes are visually DISTINCT at every fill. `IsCritical` → the **shared glyph-only ~1.0s alpha breathe** across all three (Uma §4) — NOT a per-bar invented treatment. |
| **EditMode (distinctness)** | NEW: extend the `HungerAndWarmthBands_AreDistinctAtEveryLevel` pattern to a 3-way assert — at each fill `{1, 0.6, 0.45, 0.2, 0}` the warmth / hunger / thirst band colors are pairwise distinct (gold ≠ green ≠ blue). Catches a copy-paste palette bug (thirst accidentally reusing the hunger ramp). |
| **EditMode (thirst is the cool note)** | Assert the thirst band's BLUE channel dominates at the slaked band (`b > r` and `b > g` at `Current01 ≥ 0.60`) — pins Uma's "the one cool note" call so a soak retune can't silently warm-shift thirst into hunger's space without the test flagging it. Soak MAY retune the exact hexes (Uma §6 Q2) — this asserts the COOL-relative-to-warm invariant, not a hex lock. |
| **EditMode (HDR-clamp)** | NEW: thirst band channels all sub-1.0 + no pure-saturated alarm hue (mirror `HungerBandColors_AreAllSubOne_HdrClampSafe`). Parched is dusty grey-blue, never `#FF0000`. |
| **Critical treatment** | If the glyph-pulse lands as a static-testable seam (a `GlyphPulseAlpha(bool isCritical, float time)` static), unit-test it: non-critical → alpha 1.0; critical → oscillates in `[~0.55, 1.0]`. **The pulse is on the GLYPH only, not the bar/row** (Uma §4). Multi-need-critical → all critical glyphs share ONE phase clock (assert two critical needs return the SAME pulse alpha at the same `time`). |

### AC3 — warmth + hunger NO REGRESSION (the `DrawNeedBar` generalization trap)

> **THIS IS THE FALSE-GREEN THIS PLAN EXISTS TO CATCH.** The refactor lifts `DrawWarmthBar`/`DrawHungerBar` into ONE `DrawNeedBar(need, bandColor, glyph, baselineY)` called 3×. A subtle generalization that shifts the EXISTING warmth/hunger render — a different anchor-y, a glyph slot off by a pixel, a band cutoff drifted, the flicker dropped — passes a naive "the HUD still draws something" test. The regression trap is asserting that warmth + hunger render **byte-identical after the refactor.**

| | Verify |
|---|---|
| **Static math unchanged** | (existing, MUST stay green unmodified) `SurvivalHudTests.FilledSegments_FollowsPinnedFloorRule` + `BandColor_*` + `SegmentCount_IsExactlyTen` + `PlateAlpha_MatchesBootHudFamily`; `SurvivalHudHungerTests.HungerBandColor_*`. **If the refactor requires EDITING any of these existing asserts, that is a regression signal — REQUEST CHANGES** (the contract is extend-don't-replace, per AC5 + the ticket). |
| **Anchor lock** | NEW assert: warmth baseline `Screen.height - 44`, hunger `-80` UNCHANGED. If the impl exposes anchors as a `NeedBar[]` table / consts, assert the warmth + hunger entries equal the shipped values; if anchors stay inline, this is a code-read gate in QA (grep `- 44f` / `- 80f` still present, thirst added at `- 116f`, ledger moved to `- 152f`). |
| **Warmth flicker preserved** | Uma §1: ember-flicker stays warmth-ONLY (rightmost-filled segment, ±6% alpha, ~1.5s). After the generalization, confirm hunger + thirst do NOT flicker (flicker = fire) and warmth STILL does. If `DrawNeedBar` drops the warmth flicker or applies it to all three, that's a regression — flag it. (Code-read + soak-visual; the flicker is time-driven so not cleanly unit-testable, but its PRESENCE on warmth / ABSENCE on hunger+thirst is a code-read gate.) |
| **PlayMode warmth+hunger still track** | (existing) `Hud_WarmthSegments_TrackTheLiveNeed_*` + `Hud_HungerSegments_DepleteOnDecay_AndRefillOnEat_*` stay green unmodified — proves the generalized draw still tracks the two existing needs live. |

### AC4 — one shared `DrawNeedBar` widget (now REQUIRED, not conditional)

| | Verify |
|---|---|
| **Means** | Uma §3.2 makes AC4 concrete: warmth/hunger/thirst are ONE `DrawNeedBar(SurvivalNeed need, Func<float,Color> bandColor, string glyph, float baselineY)` over a uniform `NeedBar[]` 3-row table. A 4th need = one array entry + one band function. |
| **Code-read gate** | After the refactor there is ONE bar-draw method, not three copies. The three calls differ ONLY by (need, bandColor func, glyph, baselineY). If thirst lands as a THIRD copy-pasted `DrawThirstBar()`, that FAILS AC4 — REQUEST CHANGES (the spec explicitly requires the unification). |
| **Don't over-engineer** | Uma §3.2 + OOS: NO data-driven ScriptableObject need-UI system, NO IMGUI→UI-Toolkit migration this ticket. If the impl introduces either, flag as scope-creep (out of AC4's "one widget" intent). |
| **`SurvivalNeed` base binding** | The widget takes the `SurvivalNeed` base (not three concrete types), proving the uniform bind. Assert the `NeedBar[]` (or call sites) bind `warmth`/`hunger`/`thirst` all as `SurvivalNeed` through the one path. |

### AC5 — regression-guard tests (paired EditMode + PlayMode)

| | Verify |
|---|---|
| **EditMode** | All three band-color + FLOOR-fill mappings asserted (warmth existing, hunger existing, thirst NEW). The three-way distinctness assert (AC2). Segment count + plate alpha pins (existing). **Extend `SurvivalHudTests`/`SurvivalHudHungerTests`, add `SurvivalHudThirstTests` — do NOT replace.** |
| **PlayMode (the thirst LOOP guard)** | NEW `Hud_ThirstSegments_DepleteOnDecay_AndRefillOnDrink_OverARealWindow` in `SurvivalHudPlayModeTests`, mirroring the hunger test but driving **`_thirst.AddWater()`** as the restore seam. Decay over a REAL `Time.time` window (NOT per-frame — headless `deltaTime≈0`, unity-conventions.md §headless time + Uma §AC5 ref). Assert: lit segments DROP over the window, then RISE after `AddWater()`. **⚠ Seed the test thirst need explicitly (`startFull = true` or a known fraction) — the shipped `ThirstNeed` seeds at 0.50, so a SetUp that copies the hunger idiom verbatim would start thirst at ~5 segments; either seed full like the warmth/hunger tests OR assert the actual ~5-segment start. Pin the assumption, don't inherit it silently.** |
| **PlayMode (coexistence)** | NEW: all three bars wired + each reads its live need in ONE scene (the coexistence assert from AC1). |
| **CI gate reality** | EditMode is the authoritative local gate (local PlayMode can deadlock at play-mode-enter on this machine — unity-conventions.md). The PR's CI `unity` job (EditMode + build + capture) is the authoritative gate; the advisory `playmode` job hangs and is cancelled — judge by per-JOB conclusions, NOT the run-level `conclusion` (which reads `cancelled` even when required gates pass). `Assert.Inconclusive` is a HARD CI RED here — no test may end Inconclusive. |

---

## 2. The regression / false-green traps (the silent killers)

These are the bugs a naive "the HUD draws three bars and CI is green" sign-off would MISS. QA asserts against each explicitly:

1. **`DrawNeedBar` generalization shifts warmth/hunger (AC3).** The headline trap. A green build where warmth moved to a different y, lost its flicker, or drifted a band cutoff during the refactor. → Anchor-lock assert + existing-test-unmodified gate + soak eye-dropper on warmth/hunger.
2. **Thirst starts at 0.50, not full.** A PlayMode test that copy-pastes the hunger SetUp (`startFull = true`) onto thirst, OR asserts "near-full at start" on a need that ships at half. → Pin the thirst seed in the test; assert the ACTUAL start state.
3. **Thirst bar drawn but never bound (null-ref-silent).** The bootstrap doesn't serialize `hud.thirst` → the bar is simply not drawn (the null-guard mirrors hunger), so NO error, NO red — the thirst bar just silently never appears. The "bar count > 0" / "HUD draws something" assert passes the entire missing-thirst era. → Scene-presence test (`hud.thirst` wired) + soak that confirms THREE bars on screen, not "a bar moved".
4. **Thirst palette accidentally reuses hunger's green.** Copy-paste `HungerBandColor` body into `ThirstBandColor`. The bar moves, looks plausible, CI green — but warmth≠hunger=thirst. → 3-way distinctness assert + the blue-channel-dominates (cool-note) assert.
5. **Critical glyph-pulse applied to the bar/row instead of the glyph, or with per-need phase.** Uma §4 is glyph-only, shared phase. A pulse on the whole bar reads "AAA stat-bar" (the explicit anti-pattern Uma's tonal gate forbids). → Glyph-pulse seam unit test (if static-testable) + soak-visual.
6. **Subscribe replaced with per-frame poll.** A refactor that reads `need.Current01` every `OnGUI` frame is fine (IMGUI draws from the cached value); but an added `Update()` that polls + caches defeats the subscribe-never-poll seam. → Code-read: no new `Update()`; `Changed` subscription preserved.
7. **Ledger not moved to -152 (overlap).** Thirst at -116 + ledger still at -116 → they overlap. → Anchor assert ledger == -152; soak confirms no overlap with the top need row.

---

## 3. Shipped-build capture / soak probe list

> **The shipped build is where the FULL thirst LOOP finally becomes VISIBLE.** The soak deferred from #124 (thirst decay + drink) lands HERE — #124 verified the need model + pond drink-action headlessly; this ticket is the first build where the player SEES the thirst bar drop over time and SEES it refill on drink (Q). The HUD capture gate (UX-visible) is mandatory — the headless EditMode suite cannot validate the IMGUI visual surface or the editor-vs-runtime divergence.

**Build-stamp ritual first:** verify the HUD `BUILD <tag> | <UTC> | <sha>` stamp == PR HEAD sha before judging any frame (three-builds-in-play identity confusion is a proven failure class). Capture from the GAMEPLAY orbit cam at real pitch + actual scene lighting/post — NOT an isolated hero shot (a `-verify` capture only proves the bar EXISTS, not how it READS in play; the false-green-capture trap, unity-conventions.md).

**Soak probes (Sponsor-soak targets — each is a pass/fail observation, not a vibe):**

1. **Three bars coexist.** Boot → confirm THREE distinct need bars stacked bottom-left: warmth (`▲` gold) bottom @ -44, hunger (`●` green) middle @ -80, thirst (`◆` blue) top @ -116. The inventory ledger sits clear ABOVE at -152 (or absent when empty), not overlapping the thirst row.
2. **Distinguishable at a glance.** The three read as ONE family but instantly separable by color + glyph — gold / green / blue, three hues far apart. Eye-dropper spot-check (per Uma's palette pins): warmth slaked = `#E8B25C`, hunger fed = `#8CB85C`, thirst slaked ≈ `#3E8FC4` (soak MAY retune the thirst hex — Uma §6 Q2 — but it must read clearly BLUE, the cool note).
3. **Warmth + hunger UNCHANGED (no-regression eye-check).** Warmth bar looks/feels exactly as it did pre-refactor — same gold, same bottom anchor, same right-to-left empty, the ember-flicker STILL on the rightmost-filled segment. Hunger identical to its #101 look. Nothing about the two existing bars shifted.
4. **Thirst LOOP visible — decay.** Stand idle and watch the thirst bar empty right-to-left over the decay window (it starts ~half-full per the 0.50 seed, so depletion is visible quickly). Band cools blue → pale-teal → dusty grey-blue; NEVER an alarm red.
5. **Thirst LOOP visible — drink (Q).** Walk to the freshwater pond, drink-from-hand (Q), watch the thirst bar REFILL on-screen (segments gain right-side, band warms back toward stream-blue). This is the loop the whole ticket exists to make visible — the deferred-from-#124 soak.
6. **Critical treatment consistent.** Drain a need to critical → confirm the GLYPH slow-breathes (~1s, glyph only, NOT the bar/row, NOT a flash/blink). Drain two needs critical at once → both glyphs breathe in SYNC (one phase), not a competing blinker panic. No red vignette / death overlay (OOS).
7. **Plate legibility over saturated terrain.** Per style-guide-v2 §6 watch-item: the three dark plates keep all three bars legible against the saturated beach/grass/water behind them — no bar washes out.
8. **BootHud uncovered.** The title (top-left) + BUILD stamp (top-right) stay uncovered by the taller need column (the stamp is load-bearing for soak identity).

**Difficulty-tier wiring (probe).** The need decay rates are tiered (easy/med/hard) on the `SurvivalNeed` base via `ApplyDifficulty`. This ticket is HUD-only — it does NOT own the tier defaults — but a tier change must drive the bars (a faster-decaying tier empties the bars faster). Confirm the HUD reads the live need regardless of tier; the bars are a pure view of whatever decay the active tier sets. (Tier-default tuning is OOS — the need tickets own it; the HUD just renders the live `Current01`.)

---

## 4. Verdict gates (QA sign-off checklist)

- [ ] Self-Test Report comment posted by Devon (UX-visible HARD gate — missing = REQUEST CHANGES, not a nit).
- [ ] PR #124 (thirst need) MERGED to main before this PR (hard-dep; the HUD references `ThirstNeed`).
- [ ] CI `unity` job SUCCESS (per-JOB, not run-level); EditMode + build + capture green.
- [ ] All existing `SurvivalHudTests` / `SurvivalHudHungerTests` / warmth+hunger PlayMode tests green UNMODIFIED (extend-don't-replace).
- [ ] NEW thirst EditMode (band + fill + distinctness + cool-note) green.
- [ ] NEW thirst PlayMode loop test (decay + `AddWater()` refill over a real window) green; thirst seed pinned, not inherited.
- [ ] NEW scene-presence: `hud.thirst` serialized + wired editor-time.
- [ ] `DrawNeedBar` generalization: ONE widget, not three copies (AC4); warmth/hunger render byte-identical (AC3 anchor lock + flicker preserved).
- [ ] Shipped-build capture: three bars visible + thirst loop (decay + drink) moving on-screen; stamp == HEAD.
- [ ] Soak probes 1–8 + the difficulty-tier probe pass (Sponsor soak — UX-visible).
- [ ] Regression-guard line in the PR's Done clause + Cross-lane integration check in the Self-Test Report.

---

## 5. Cross-references

- `team/uma-ux/hud-three-bar-spec.md` — the implement-to spec (Uma #125, the lock). §1 shipped baseline; §2 thirst identity; §3 generalization; §4 critical treatment; §6 open questions (all soak/tuning, none block impl).
- `Assets/Scripts/Runtime/SurvivalHud.cs` — shipped warmth + hunger bars (ground truth): `DrawWarmthBar`/`DrawHungerBar`, `FilledSegments` FLOOR, `BandColor`/`HungerBandColor`, `PlateAlpha 0.55`, `SegmentCount 10`.
- `Assets/Scripts/Runtime/SurvivalNeed.cs` — the shared base (`Current01`/`IsCritical`/`Changed`/`TickSeconds` + `ApplyDifficulty` tiers); the HUD binds all three through this surface.
- `Assets/Scripts/Runtime/ThirstNeed.cs` (PR #124) — `AddWater(float)`/`AddWater()` satisfaction hook; seeds at `startFraction01 = 0.50`.
- Existing tests this extends: `Assets/Tests/EditMode/SurvivalHudTests.cs`, `SurvivalHudHungerTests.cs`, `WarmthNeedSceneTests.cs`; `Assets/Tests/PlayMode/SurvivalHudPlayModeTests.cs`.
- `.claude/docs/unity-conventions.md` — headless time rule (real `Time.time` window); editor-vs-runtime serialization trap; false-green-capture trap; per-JOB-not-run-level CI verdict; `Assert.Inconclusive` is a hard red.
- `team/TESTING_BAR.md` — paired EditMode/PlayMode + shipped-build capture + Self-Test Report + Tess sign-off bar.
