# Citation + line-anchor audit — `86caz4yhj` (absorbs `86caz4wwx`)

**Measured at:** `origin/main` @ `0f14b4f` (`docs(bars): bar #10 … (#386)`), 2026-07-31.
**Scope audited:** every `.claude/docs/*.md` (11 files) + `CLAUDE.md`.
**Method:** every anchor and every cited path was OPENED against `git show origin/main:<file>` and compared to the claim its own prose makes about it. No count in this file is extrapolated.

Re-measured independently of the ticket's seed table, per AC1's 🔒. The seed table's `18 sites / 10 distinct / 2 dead` for the `team/erik-consult/` prefix **reproduces exactly**; the widened sweep below finds three MORE dead paths outside that prefix.

---

## 0. Headline

| | count |
|---|---|
| Anchor citation sites audited | **165** |
| — holds | **114** |
| — **DRIFTED** (resolves, but to the wrong line) | **43** |
| — **DEAD** (points at nothing) | **3** |
| — not tip-verifiable by construction (historical `sha:line` pins) | 5 |
| Path citation sites audited | **128** (89 distinct targets) |
| — **DEAD distinct targets** | **5** |

**DEAD vs DRIFTED is the distinction that matters.** Dead is loud — you click, nothing is there, you go look. Drifted is silent: the file opens, a line is there, it just isn't the one the sentence describes. **43 drifted vs 3 dead** means the failure mode this class actually ships is the quiet one, and it is ~14× more common than the loud one.

---

## 1. The four citation FORMS (this is a guard-design input)

The docs use four distinct anchor syntaxes. Anyone building the AC3 guard must know this — a guard that only understands form A sees **43%** of the anchor population.

| Form | Example | Sites | Machine-resolvable? |
|---|---|---|---|
| **A** `path.ext:NNN` | `` `MineOre.cs:96` `` | 71 | Yes — unambiguous |
| **B** bare `` `:NNN` `` continuation | `…at `:282` / `:287`` | 82 | **Only via nearest-preceding-filename attribution** |
| **C** `ClassName:NNN` (no extension) | `` `WasdMovementPlayModeTests:119` `` | 11 | Ambiguous (see §4) |
| **D** `sha:NNN` | `` `202a4db:59` `` | 1 | Not at tip, by design |

Form B is the single largest population and the one a naive guard misses entirely.

---

## 2. DEAD anchors (3) — points at nothing

| Cited from | Anchor | Why dead |
|---|---|---|
| `unity-conventions.md:338` | `HeldAxeRig.cs:141` | `Assets/Scripts/Runtime/HeldAxeRig.cs` is **114 lines**. Line 141 does not exist. (Devon flagged this on PR #364 and deliberately left it — confirmed still dead at `0f14b4f`.) |
| `unity-conventions.md:15` | `verify_weaponfind_gate.sh:63` | Target absent from `main`. **Self-disclosed**: the citing bullet names it as PR #351's added file (`drew/86cah7y5b-find-in-world`), still OPEN. |
| `unity-conventions.md:15` | `verify_weaponfind_gate.sh:26` | Same file, same reason. |

## 3. DEAD paths (5) — cited path not on `main`

| Cited from | Path | Verdict |
|---|---|---|
| `game-juice.md:55` | `team/erik-consult/game-juice-concepts.md` | Added `a113cda` (#112), deleted `187e486` (#201) — **#201 wrote the citation and killed the file in the same commit**, so it shipped dead from birth. **FIXED in this PR** (reworded per AC2b default — not resurrected). |
| `unity-conventions.md:12` | `team/erik-consult/what-still-needs-a-window.md` | Genuinely absent. Citing line **self-labels the port as pending** (`currently `orch/coordination`-only, pending port from `ac58d4b``), so the absence is disclosed, not silent. **NOT fixed here** — `unity-conventions.md` is held by in-flight `86cazhtn1` (see §7). |
| `blender-asset-pipeline.md:14` | `inspiration/21h08_08.png` | **NEW — not in the ticket's seed data.** The real file is `inspiration/2026-06-12_21h08_08.png`; the date prefix was dropped. `lowpoly-quality.md:70` cites the SAME image with the correct full path, which is what makes this a typo rather than a missing asset. **FIXED in this PR.** |
| `unity-conventions.md:400` | `.github/workflows/scripts/verify_swings_gate.sh` | Absent from `main`. **Self-disclosed** as PR #369's branch file, OPEN. |
| `unity-conventions.md:15` | `.github/workflows/scripts/verify_weaponfind_gate.sh` | Absent from `main`. **Self-disclosed** as PR #351's branch file, OPEN. |

### Explicitly NOT defects (a guard must skip these or it false-reds)

`Editor/Mono/EditorSettings.bindings.cs` (Unity **engine** source, external) · `FarHorizon_Data/resources.asset` (build output, `Build/` is gitignored) · `Library/PackageCache/com.unity` (gitignored cache) · `path/to/script.py`, `path/to/source.blend` (prose placeholders) · `Boot.unity/BuildStamp.txt` (**not a path** — prose "…on Boot.unity/BuildStamp.txt" naming two files, glued by a slash) · `Combat/MeleeAttack.cs`, `Resources/BuildStamp.txt` (valid partial paths that suffix-match a real file).

---

## 4. DRIFTED anchors (43) — the silent class

All 43 pass "file exists" AND "file is long enough". **Every one of them would go GREEN under a naive existence+length guard.** That is the central finding for AC3.

### Where the drift points (this is the evidence base for `86cazhtfy`)

| Target area | Drifted anchors | Share |
|---|---|---|
| `Assets/**` (game + editor + test C#) | **29** | 67% |
| `.github/**` (all of it `ci.yml`) | **13** | 30% |
| `.claude/docs/**` (a doc self-reference) | **1** | 2% |

### Representative confirmations (Tess's leads, re-derived at `0f14b4f`)

- `HeldWeaponCycleDebug.cs:260` — claim is `WeaponMeshScale`; **line 260 is a comment** (`// extension), so it seats identically. axe_iron←axe(1.0)…`). Tess's lead **CONFIRMED**.
- `HeldWeaponCycleDebug.cs:281` — claim is `WeaponMeshLocalOffset`; line 281 is also a comment.
- `MovementCameraScene.cs:537` — claim is "Ground carries MeshColliders"; line 537 is a **crafting-table authoring comment**. (Devon's flag, confirmed.)
- `MovementCameraScene.cs:2515` — claim is "rocks/ore/props ship Collider-free"; line 2515 is **blank**.
- `CastawayCharacter.cs:1182-1185` — claim is the `_bodyYaw` `LerpAngle` facing drive; lines 1182-1185 are the **doc comment for `ProxyRootFloatGap`**. (Devon's flag, confirmed.)
- `unity-conventions.md:189` — the doc citing **ITSELF** by line number; line 189 at tip is **blank**.
- `ci.yml:207` / `:209` / `:485` / `:487` / `:1234` / `:1259` — a six-anchor block describing the `build:`/`capture:` job shape; **all six now land on comment prose**, none on the key they name.

### ⚠ A sha pin does NOT prevent the drift a reader experiences

At least **9 of the 43** drifted anchors carry an explicit sha pin (`@1588996`, `@51f4623`, `@c8ce948`) and drifted anyway. The pin makes the cite *recoverable* (you can check that sha out) but a reader at tip still lands on the wrong line and has no signal that they did. **Pinning is an audit aid, not a drift fix.**

### Counter-example worth preserving — the pattern that WORKED

Two blocks in `unity-conventions.md` hold **100%**:
- the `AxeVerifyCapture.cs` block at `:16` — **18/18 anchors exact**;
- the `test_gate_scripts.sh` block at `:13`–`:15` — **10/10 exact**.

Both were re-derived by hand in PR #364 and both **name the symbol next to the number** (`:562`-`:635` *"RunFacingsVerification"*, `:861` *"`assert_launch_headless()`"*). `:13` goes further and writes the re-find rule into the prose: *"re-find by NAME if the numbers have drifted again — a range that no longer contains `HEADLESS_GATES=(` is stale, not empty."* **That is the shape that survives, and it is also the shape a guard can check.** See §6.

---

## 5. Bearing on `86cazhtfy` (§-anchor proposal) — INFORMING, not pre-empting

`86cazhtfy` proposes citing `team/**` and `.claude/docs/**` by **§ anchor**, reserving line anchors for `Assets/**` with a verifying sha. Awaiting the Sponsor; this ticket does **not** adopt it. The measurement he asked for:

> **Of the 43 drifted anchors, exactly 1 would have been immune under that convention** — the `unity-conventions.md:189` doc self-reference.

Because:
- **29 (67%)** point into `Assets/**`, where the proposal **keeps line anchors**. A verifying sha makes staleness *detectable*, but §4's finding is that 9 already-sha-pinned cites drifted regardless, so this bucket stays broken.
- **13 (30%)** point into `.github/**` — **outside both of the proposal's named buckets**. Nothing in the convention as written reaches them, and `ci.yml` is the single worst-drifting file in the repo (13 of 43).
- **1 (2%)** is `.claude/docs/**` → § anchor → immune.

**Read plainly: the § -anchor half is correct but addresses ~2% of the observed drift, because the drift is overwhelmingly into CODE and CI CONFIG, not into prose docs.** If the goal is to stop the 43, the lever is §6's payload rule, not the file-type split. The two are complementary, not alternatives — recommend the Sponsor sees this table before deciding.

---

## 6. AC3 guard feasibility (REPORTED ONLY — not built here, per dispatch scope)

**Verdict: the dead half is cheap and worth building now. The drifted half — the 93% majority — is NOT catchable without a doc-convention change.**

| Tier | Checks | Catches | Cost |
|---|---|---|---|
| **1. Path existence** | cited path ∈ `git ls-tree origin/main` | **5/5 dead paths** | Low. Needs the §3 skip-list (engine source / build output / placeholders / slash-glue). |
| **2. Line-range viability** | target exists AND `wc -l` ≥ N | **1/3 dead anchors** (other 2 are dead-file → Tier 1) | Low. |
| **3. Drift** | — | **0 of 43** | **Not possible from the anchor alone.** |

**Why Tier 3 fails today:** an anchor is a bare number. Nothing in `MineOre.cs:96` tells a machine what line 96 is supposed to say, so "the file is 1082 lines long" is the only assertion available — and all 43 drifted anchors pass it.

**The feasible upgrade** (a follow-up, not this ticket): require every `file.ext:NNN` to carry a **checkable payload** — a backticked symbol or snippet in a fixed position — and have the guard assert that token appears within ±N lines of the cited line. The `:13`/`:16` blocks in §4 **already write in this style**, so the convention is proven in-repo rather than hypothetical; it just isn't mandatory or machine-parseable yet. This converts drift from invisible to red.

**Two hazards for whoever builds it:**
1. **Form B attribution is genuinely hard.** Bare `` `:NNN` `` needs nearest-preceding-filename attribution, and building this audit my own heuristic was **wrong on ~20%** of them — it bound `:98` to `chop_before.png` (a capture *output*, not a source), `:63` to the **binary** `Boot.unity`, and `:545` to the wrong sibling `.cs`. Every one of those would have been a **false RED**. Per AC3's "false RED not false GREEN" the direction is at least safe, but 17 false reds on day one kills trust in the gate. **Recommend starting the guard at Form A only, and disclosing Form B as the documented residual.**
2. **Form C is ambiguous by construction.** `FloatDiagnostic:98` suffix-matches three tracked files (`FloatDiagnostic.cs`, `FloatDiagnosticVerifyCapture.cs`, `FloatDiagnosticPlayModeTests.cs`). It resolves to the *tests* file and holds — but only a human knows that.

**⛔ Sequencing blocker:** AC3 mandates the unit test live in `tests/scripts/test_gate_scripts.sh`, and **PR #370 is open against exactly that file**. Building the guard now collides. **Build after #370 merges.**

---

## 7. What this PR fixes, and what it deliberately does not

**Fixed (2)** — both reference-only, both in files no in-flight ticket holds:
- `blender-asset-pipeline.md:14` — restored the dropped `2026-06-12_` prefix.
- `game-juice.md:55` — reworded so the deleted draft is named without a path that looks openable. Per AC2b's 🎚️ default: **reworded, NOT resurrected.**

**Deliberately NOT fixed (49)** — 43 drifted + 2 dead anchors + 2 dead paths, **all in `.claude/docs/unity-conventions.md`**, which the dispatch put out of scope because **`86cazhtn1` holds that file in flight right now**. Editing it here would conflict. AC2's 🔒 also applies: several are other tickets' prose, and fixing the reference correctly means naming the symbol — which touches the sentence.

**Recommended routing:** hand §2 + §4 of this file to `86cazhtn1` as a work item, or file a successor scoped to `unity-conventions.md` alone once that ticket lands. Every anchor is listed with its true current content, so the repair is mechanical.

---

## 8. Bounded convergence claim

**Guaranteed:** every anchor and every cited path in `.claude/docs/*.md` + `CLAUDE.md` was opened at `0f14b4f` and carries a verdict. The two fixed citations resolve.

**NOT guaranteed:**
- **Nothing is mechanically enforced.** No guard was built (dispatch scope). All 51 findings can recur, and the 49 unfixed ones are still live on `main` today.
- **`.claude/agents/*.md`, `team/**/*.md`, `.github/**` were NOT swept** as *citing* files — only as citation *targets*. `86cazhmtj`'s `decisions-batch-pr-template.md` family (5 cites from `.claude/agents/priya.md`, `team/ROLES.md`, `team/GIT_PROTOCOL.md`, `team/orchestrator/dispatch-template.md`) is **outside this audit on the citing axis** and remains that ticket's.
- **This audit is a snapshot.** It was accurate at `0f14b4f` and starts decaying with the next merge into any cited file — which is precisely the argument for the §6 guard.
