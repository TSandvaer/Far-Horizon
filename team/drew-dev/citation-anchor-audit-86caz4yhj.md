# Citation + line-anchor audit — `86caz4yhj` (absorbs `86caz4wwx`)

**Measured at:** `origin/main` @ `0f14b4f` (`docs(bars): bar #10 … (#386)`), 2026-07-31.
**Scope audited:** every `.claude/docs/*.md` (11 files) + `CLAUDE.md`.
**Method:** every anchor and every cited path was OPENED against `git show origin/main:<file>` and compared to the claim its own prose makes about it. No count in this file is extrapolated.

Re-measured independently of the ticket's seed table, per AC1's 🔒. The seed table's `18 sites / 10 distinct / 2 dead` for the `team/erik-consult/` prefix **reproduces exactly**; the widened sweep below finds three MORE dead paths outside that prefix.

> ### ⚠ REVISION 2 (2026-08-01) — corrections after Uma's independent re-measurement (PR #396, comment `5148047794`)
>
> Uma re-measured this audit at the same `0f14b4f` pin with her own tokenizer. **The decision-relevant results reproduced**: population 165 with all four sub-counts exact (A=71 / B=82 / C=11 / D=1), 3 dead anchors, 5 dead paths with §3's skip-list catching all of her extra hits, both 100%-holding blocks opened line-by-line (18/18 and 10/10), all six named `ci.yml` drifts, `.github/**`=13, `Assets/**`=29 against her independent proxy of 28, and "the guard catches 0 of 43".
>
> **Four things did not hold. All four are corrected in place below**, each with a re-measurement in this revision (nothing here is carried over on trust):
>
> 1. **§7's "all 49 are in `unity-conventions.md`" was FALSE** — 3 findings are cited from elsewhere. §4 listed 2 of them as bare *target* anchors with no citing-file column, so the summary swept them into the wrong file; the 3rd was never counted at all. **§4 now carries a `Cited from` column** — the structural fix, so a target anchor can no longer lose its origin. See §7.
> 2. **"9 of the 43 already carry sha pins" is WITHDRAWN** — not reproducible under any attribution rule. Replaced with a rule-stated measurement that proves the same point causally. See §4's box.
> 3. **`@1588996`-style pinning does not exist in this repo** (0 occurrences, re-measured). §4/§5 corrected. This changes what `86cazhtfy` is actually proposing.
> 4. **Two further citation forms exist** (slash-continuation, prose `line NNN`). Folded into §1; they *strengthen* the distribution finding.
> 5. **Uma's "What I took on trust" list is now answered figure-by-figure in §8.** She flagged that she had *not* re-derived the `114/43` split, the `128/89` path denominators, or the `~20%` form-B ratio. **§8a is a new observer ledger** giving every headline figure its observer count and whether its scoping rule is written down. Outcome: the **population 165 reproduces exactly on a third independent tokenizer** (§8b), the split is **bounded** rather than re-asserted (`≥44` / `≤113`, §8c), **`128` sites reproduces but `89` distinct is WITHDRAWN** (§8d), and the `~20%` is superseded by §6's rule-stated **28%**.
>
> **Drift count: `43` is a FLOOR, not a total.** 43 anchors were audited as drifted; `AxeNudgeTool.cs:563` was found afterwards and is a 44th. No claim of a new exact total is made here — the full 165 have not been re-swept in this revision, and the fifth/sixth forms (§1) are unaudited. **≥44.**

---

## 0. Headline

| | count |
|---|---|
| Anchor citation sites audited (forms A–D) | **165** *(exact — reproduced by **three** independent tokenizers; §8b)* |
| — holds | **≤113** — a CEILING *(derived, not counted; §8c)* |
| — **DRIFTED** (resolves, but to the wrong line) | **≥44** — a FLOOR *(43 audited + `AxeNudgeTool.cs:563`, found post-audit)* |
| — **DEAD** (points at nothing) | **3** *(independently reproduced exactly)* |
| — not tip-verifiable by construction (historical prose pins) | 5 *(⚠ single-observer; §8a)* |
| Line references OUTSIDE the A–D net (forms E/F, §1) | **13** across 8 sites — **unaudited** |
| Total line references in the doc set | **≈178** |
| Path citation sites audited | **128** under a stated rule (§8d) — *but the same rule in a second implementation gives **101**; the denominator is sweeper-dependent* |
| — distinct path targets | ~~89~~ **WITHDRAWN — not reproducible** (87 under the stated rule; §8d) |
| — **DEAD distinct targets** | **5** *(rule-independent; reproduced exactly — unaffected by the above)* |

**DEAD vs DRIFTED is the distinction that matters.** Dead is loud — you click, nothing is there, you go look. Drifted is silent: the file opens, a line is there, it just isn't the one the sentence describes. **≥44 drifted vs 3 dead** means the failure mode this class actually ships is the quiet one, and it is ~15× more common than the loud one. The ratio is what carries the argument, and it survives every revision above.

---

## 1. The SIX citation FORMS (this is a guard-design input)

The docs use **six** distinct line-reference syntaxes — four backticked-anchor forms (A–D) plus two that sit outside any anchor net (E–F). Anyone building the AC3 guard must know all six: a guard that only understands form A sees **43%** of the A–D population and **40%** of all line references in the doc set.

| Form | Example | Sites | Line refs | Machine-resolvable? |
|---|---|---|---|---|
| **A** `path.ext:NNN` | `` `MineOre.cs:96` `` | 71 | 71 | Yes — unambiguous |
| **B** bare `` `:NNN` `` continuation | ``…at `:282` / `:287` `` | 82 | 82 | **Only via nearest-preceding-filename attribution** (28% failure rate — §6) |
| **C** `ClassName:NNN` (no extension) | `` `WasdMovementPlayModeTests:119` `` | 11 | 11 | Ambiguous (see §6) |
| **D** `sha:NNN` | `` `202a4db:59` `` | 1 | 1 | Not at tip, by design |
| **E** slash-continuation `Name:NN/NN/NN` | `` `CastawayGroundSnap:92/266/482` `` | **3** | **8** | Only the LEADING number parses as form C; the rest are invisible |
| **F** prose `line(s) NNN` (unbackticked) | "`LowPolyVertexColor.shader` lines 60-66" | **5** | **8** | No — no backtick, no colon |

**Forms E and F are NEW in revision 2** (Uma's finding 4), re-measured here at `0f14b4f`:

- **E — 3 sites, all on `unity-conventions.md:26`**: `CastawayGroundSnap:92/266/482` (3 refs), `FloatDiagnostic:98/158` (2), `CombatPlayModeTests:139/171/192` (3) = **8 line references**. The A–D net captures at most the 3 leading numbers (as form C), so **≥5 references are outside it**.
- **F — 5 sites**: `lowpoly-quality.md:35` ("lines 60-66"), `unity-conventions.md:46` ("~line 34"), `:228` ("lines 272/725/775" = 3), `:229` ("line 41") and `:229` ("lines 28-37, 131-146" = 2) = **8 line references**, none in any backticked form.

**Net effect: ~13 line references sit outside the A–D net, so the true population is ≈178, not 165 — a ~7% undercount.**

**This STRENGTHENS the report's conclusion rather than weakening it.** Every E/F target is a C# file or a shader under `Assets/**` — the bucket that §5 shows the `86cazhtfy` proposal does **not** cover. Counting them pushes the `Assets/**` share *up* from 67%. They are recorded here because §1 is explicitly a guard-design input, and a guard built to four forms will silently skip these too (see §6's residual list).

Form B remains the single largest population and the one a naive guard misses entirely.

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

## 4. DRIFTED anchors (≥44) — the silent class

All of them pass "file exists" AND "file is long enough". **Every one would go GREEN under a naive existence+length guard.** That is the central finding for AC3, and it is the one Uma re-verified directly.

> **Count discipline (revision 2):** 43 anchors were audited as drifted at `0f14b4f`. A 44th (`AxeNudgeTool.cs:563`) was found afterwards, inside declared scope, and is added below. **Treat 43 as a FLOOR.** The full 165 have not been re-swept in this revision, and forms E/F (§1) are unaudited — so the total may be higher. No corrected exact total is asserted.

### Where the drift points (this is the evidence base for `86cazhtfy`)

| Target area | Drifted anchors | Share |
|---|---|---|
| `Assets/**` (game + editor + test C#) | **29** | 67% |
| `.github/**` (all of it `ci.yml`) | **13** | 30% |
| `.claude/docs/**` (a doc self-reference) | **1** | 2% |

### Representative confirmations, re-derived at `0f14b4f`

> **⚠ The `Cited from` column is the structural fix of revision 2.** The original table listed only *target* anchors. Two of them (`HeldWeaponCycleDebug.cs:260` / `:281`) are cited from `procedural-animation-verbs.md`, not `unity-conventions.md` — but with no citing-file column, §7's summary swept them into the wrong file and would have mis-routed them to a ticket that never touches their doc. **A drift finding without its citing file cannot be routed. Never record one that way.**

| Cited from | Target anchor | Claim the prose makes | What the line actually is |
|---|---|---|---|
| `procedural-animation-verbs.md:128` | `HeldWeaponCycleDebug.cs:260` | `WeaponMeshScale` (really at `:274`) | comment — `// extension), so it seats identically. axe_iron←axe(1.0)…` |
| `procedural-animation-verbs.md:128` | `HeldWeaponCycleDebug.cs:281` | `WeaponMeshLocalOffset` (really at `:295`) | comment — `// the committed on-disk constant). The earlier {-0.34/-0.80/-1.50}…` |
| **`unity6-mastery.md:87`** | **`AxeNudgeTool.cs:563`** | **`AxeNudgeTool.ComposeLocalRot`** (really at **`:1008`**) | **`else if (_target == 7)`** — unrelated dispatch branch |
| `unity-conventions.md:242` | `MovementCameraScene.cs:537` | "Ground carries MeshColliders" | crafting-table authoring comment |
| `unity-conventions.md:242` | `MovementCameraScene.cs:2515` | "rocks/ore/props ship Collider-free" | **blank** |
| `unity-conventions.md:351` | `CastawayCharacter.cs:1182-1185` | the `_bodyYaw` `LerpAngle` facing drive | doc comment for `ProxyRootFloatGap` |
| `unity-conventions.md:10` | `unity-conventions.md:189` | the editor `Camera.Render()` multi-submesh caution | **blank** (the doc citing ITSELF) |
| `unity-conventions.md:389` | `ci.yml:207` / `:209` / `:485` / `:487` / `:1234` / `:1259` | the `build:`/`capture:` job keys | **all six land on comment prose**, none on the key they name |

**Provenance:** rows 1-2 were Tess's leads and rows 4-6 Devon's; all were confirmed at `0f14b4f`, and Uma independently re-derived all of them plus the six `ci.yml` lines exactly. **Row 3 is new in revision 2 and is a genuine miss:** `unity6-mastery.md` was inside declared scope, the anchor was never counted, and it appeared nowhere in revision 1 (grep for `AxeNudgeTool` / `ComposeLocalRot` over the original artifact and the full PR diff → **0 hits**). Found by Uma. Verified here: `AxeNudgeTool.cs` is 1278 lines at `0f14b4f`; `ComposeLocalRot` is *defined* at `:1008`, with call sites at `:461` / `:477` / `:541`; `:563` is `else if (_target == 7)`.

### ⚠ A sha does NOT prevent the drift a reader experiences — but the sha is NOT the discriminating variable

> **WITHDRAWN (revision 2): "at least 9 of the 43 drifted anchors carry an explicit sha pin `@1588996` / `@51f4623` / `@c8ce948`."**
>
> **The figure is not reproducible, and the notation is wrong.** A sha-pin count is undefined without an attribution window, and revision 1 never stated one. Three rules, three answers: per-anchor adjacency (a sha within ~90 chars of the anchor) gives **6**; line-scope restricted to those three shas gives **77**; line-scope over any backticked 7–12-hex token gives **116 of 159** (re-measured here). **None yields 9.** The notation is separately wrong — see the box below: `@sha` glued to an anchor has **0** occurrences in this repo.
>
> This figure was relayed to the Sponsor as fact and had to be retracted. **A count with no stated scoping rule is not a measurement.** Recording the rule beside the number is now the standing requirement for every count in this artifact.

**The causally correct statement — which this artifact's own data proves, and which is stronger:**

> **The sha is not what separates a holding cite from a drifting one. It appears on both. What separates them is whether the SYMBOL is named beside the number.**

Re-measured at `0f14b4f` under a **stated rule — "line scope": an anchor is sha-labelled if its citing doc line also contains a backticked 7–12-char hex token.** Under that rule **116 of 159 form-A–D anchors are sha-labelled** — a majority, on both sides of the split:

| Citing line | Its sha | Anchors | Outcome |
|---|---|---|---|
| `unity-conventions.md:16` (`AxeVerifyCapture.cs` block) | `c8ce948` / `1588996` | 30 on the line | **HOLDS 18/18** on the `AxeVerifyCapture.cs` anchors |
| `unity-conventions.md:13` (`test_gate_scripts.sh` block) | `c8ce948` (+ `1588996`, `a5fee62`) | — | **HOLDS 10/10** |
| `unity-conventions.md:389` (`ci.yml` job shape) | `51f4623` (+ `d5c3e7d`) | 6 on the line | **ALL SIX DRIFT** |
| `unity-conventions.md:10` (self-reference `:189`) | `1588996` (+ 3 others) | 5 on the line | **DRIFTS** (line 189 is blank) |

The best-holding block in the repo and the worst-drifting block in the repo are **both** sha-labelled under the same rule. So the sha cannot be the causal variable. `:13` and `:16` differ in one respect the drifters lack: they write the symbol next to the number (`:562` *"RunFacingsVerification"*, `:861` *"`assert_launch_headless()`"*), and `:13` goes further and writes the re-find rule into the prose.

**A sha makes a cite RECOVERABLE — you can check that revision out. Only a checkable payload makes it VERIFIABLE.** That is §6's payload rule, and it subsumes both the §-anchor proposal and marker-relative addressing, since a marker is only as good as its checkability.

### ⚠ The `path:NNN@sha` pinning syntax has NEVER been adopted in this repo

**Re-measured at `0f14b4f` across all 11 `.claude/docs/*.md` + `CLAUDE.md`:**

| Sweep | Hits |
|---|---|
| Glued `path:NNN@<7-40 hex>` (the syntax revision 1 wrote as `@1588996`) | **0** |
| Any `@<7-40 hex>` anywhere in the doc set | **1** — `Library/PackageCache/com.unity.render-pipelines.universal@d3aed158d698` (`lowpoly-quality.md:82`), a **package-cache directory name**, not an anchor pin |
| Spaced `` `path:NNN` @ `sha` `` | **2** — `AttackClipPoseDiag.cs:184`–`:190` @ `eb0abc4` (`procedural-animation-verbs.md:106`) and `SwingVerifyCapture.cs:187` @ `eb0abc4` (`:118`). **Both HOLD** — opened and verified in revision 2. |

What the docs actually write is **prose-adjacent and applied unevenly**: "range re-derived at `c8ce948`", "`:847-880` at `1588996`", "read off `origin/main` @ `51f4623`". There is no convention — no fixed position, no fixed separator, no requirement.

**Why this matters beyond the typo:** revision 1's `@1588996` notation implies a `path:NNN@sha` convention was already adopted here and already failed. **It was never adopted.** `86cazhtfy` therefore proposes **introducing a new practice, not formalising an existing one** — a materially different decision, with no in-repo track record to reason from. See §5.

### Counter-example worth preserving — the pattern that WORKED

Two blocks in `unity-conventions.md` hold **100%**:
- the `AxeVerifyCapture.cs` block at `:16` — **18/18 anchors exact**;
- the `test_gate_scripts.sh` block at `:13`–`:15` — **10/10 exact**.

> **The second block already declares itself self-verifying** — `:13` names `test_gate_scripts.sh:861-922` as *"the MACHINE-CHECKED source of truth"* and carries its own re-find rule. **So the 10/10 above is a confirmation that the block's own mechanism works, not a fresh derivation of a number nobody had** *(Uma's soft note on the #391 "cite the canonical figure" rule — recorded rather than left implicit)*. That it holds while the sha-labelled `ci.yml` block next door drifts 6/6 is the whole argument of this section.

Both were re-derived by hand in PR #364 and both **name the symbol next to the number** (`:562`-`:635` *"RunFacingsVerification"*, `:861` *"`assert_launch_headless()`"*). `:13` goes further and writes the re-find rule into the prose: *"re-find by NAME if the numbers have drifted again — a range that no longer contains `HEADLESS_GATES=(` is stale, not empty."* **That is the shape that survives, and it is also the shape a guard can check.** See §6.

---

## 5. Bearing on `86cazhtfy` (§-anchor proposal) — INFORMING, not pre-empting

`86cazhtfy` proposes citing `team/**` and `.claude/docs/**` by **§ anchor**, reserving line anchors for `Assets/**` with a verifying sha. Awaiting the Sponsor; this ticket does **not** adopt it. The measurement he asked for:

> **Of the 43 audited drifted anchors, exactly 1 would have been immune under that convention** — the `unity-conventions.md:189` doc self-reference.

Because:
- **29 (67%)** point into `Assets/**`, where the proposal **keeps line anchors**. A verifying sha makes staleness *detectable* by someone who goes looking, but §4 shows the sha is not the discriminating variable — it labels the repo's best-holding block and its worst-drifting block alike. This bucket stays broken unless the cite carries a checkable payload.
- **13 (30%)** point into `.github/**` — **outside both of the proposal's named buckets**. Nothing in the convention as written reaches them, and `ci.yml` is the single worst-drifting file in the repo. *(Uma's independent pass: 16 `ci.yml` anchors → 1 hold + 3 explicitly historical-pinned + 12 drifted. Her 12 and this artifact's 13 differ only on where the historical-pin boundary is drawn.)*
- **1 (2%)** is `.claude/docs/**` → § anchor → immune.

**Revision 2 strengthens this, in two ways:**
1. **The `Assets/**` share is understated.** Every one of the ~13 form-E/F line references (§1) targets a C# file or a shader under `Assets/**`. Counting them pushes the 67% up, not down.
2. **⚠ The proposal's `Assets/**` half is a NEW practice, not a formalisation.** `path:NNN@sha` has **0 occurrences** in this repo (§4's notation box). What exists is an uneven prose habit ("at `1588996`"). Revision 1's `@1588996` notation wrongly implied the syntax was already in use and had already failed — a reader would have inferred a track record that does not exist. **The Sponsor is deciding whether to introduce a convention, with no in-repo evidence of how it performs once mandatory.** The two spaced-form instances that do exist (`@ `eb0abc4``, §4) both HOLD — a two-point sample, and both also name their symbol, so they do not isolate the sha's contribution either.

**Read plainly: the §-anchor half is correct but addresses ~2% of the observed drift, because the drift is overwhelmingly into CODE and CI CONFIG, not into prose docs.** If the goal is to stop the drift, the lever is §6's payload rule, not the file-type split. The two are complementary, not alternatives — recommend the Sponsor sees this table before deciding.

---

## 6. AC3 guard feasibility (REPORTED ONLY — not built here, per dispatch scope)

**Verdict: the dead half is cheap and worth building now. The drifted half — the 93% majority — is NOT catchable without a doc-convention change.**

| Tier | Checks | Catches | Cost |
|---|---|---|---|
| **1. Path existence** | cited path ∈ `git ls-tree origin/main` | **5/5 dead paths** | Low. Needs the §3 skip-list (engine source / build output / placeholders / slash-glue). |
| **2. Line-range viability** | target exists AND `wc -l` ≥ N | **1/3 dead anchors** (other 2 are dead-file → Tier 1) | Low. |
| **3. Drift** | — | **0 of ≥44** | **Not possible from the anchor alone.** |

**Why Tier 3 fails today:** an anchor is a bare number. Nothing in `MineOre.cs:96` tells a machine what line 96 is supposed to say, so "the file is 1082 lines long" is the only assertion available — and every drifted anchor passes it. *(Independently verified by Uma: every drifted anchor she opened resolves to a file that exists and is long enough; Tiers 1 and 2 green on all of them.)*

**The feasible upgrade** (a follow-up, not this ticket): require every `file.ext:NNN` to carry a **checkable payload** — a backticked symbol or snippet in a fixed position — and have the guard assert that token appears within ±N lines of the cited line. The `:13`/`:16` blocks in §4 **already write in this style**, so the convention is proven in-repo rather than hypothetical; it just isn't mandatory or machine-parseable yet. This converts drift from invisible to red. **Note the payload rule subsumes both `86cazhtfy`'s §-anchor half and marker-relative addressing** — a § heading and a marker are each just a payload, and each is only as good as its checkability.

**Three hazards for whoever builds it:**

1. **Form B attribution is genuinely hard — re-measured in revision 2 at 28%.**

   Revision 1 said "~20%, from a heuristic run I did not preserve". Replaced with a stated-rule measurement, re-run at `0f14b4f`:

   > **Rule:** for each of the 82 form-B sites, bind to the nearest preceding backticked file-ish token on the SAME doc line; count a bind as failed if the target's basename is not in `git ls-tree 0f14b4f`, if it has no source extension, or if there is no preceding filename at all.
   >
   > **Result: 23 of 82 = 28% would emit a FALSE RED on day one.**

   | Failure class | Sites | Example |
   |---|---|---|
   | Binds to a **capture-output PNG** | 10 | `:98` → `chop_before.png`, `:73`/`:98`/`:124`… → `chop_scatter.png` |
   | **Binds to a `Class.Method` token** — indistinguishable from `file.ext` to the attributor | 5 | `:545` → `RenderTextureCapture.CaptureCameraToTexture`; `:85`/`:92` → `CastawayCharacter.Awake`; `:62`/`:63` → `LowPolyZoneGen.BuildZone` |
   | **No preceding filename on the line** — nothing to bind to | 6 | `unity-conventions.md:27` (×4), `:376` (×2) |
   | Binds to a **branch-only file** | 2 | `:26`/`:906` → `verify_weaponfind_gate.sh` |

   **The `Class.Method` class is NEW and is the sharpest one** — `RenderTextureCapture.CaptureCameraToTexture` matches a `name.ext` regex exactly, so a naive attributor treats a method call as a file and never recovers. Uma hit the same class independently with a separately-built attributor (`:545`/`:625`/`:721` mis-bound away from `AxeVerifyCapture.cs`, `:1182` away from `CastawayCharacter.cs`), which is two-observer confirmation of the hazard.

   *Correction:* revision 1 also cited `:63` → the binary `Boot.unity`. **That instance does not reproduce under the stated rule** — `:63` binds to `LowPolyZoneGen.BuildZone`. It came from the unpreserved earlier run; flagged rather than silently dropped.

   Per AC3's "false RED not false GREEN" the direction is safe, but **23 false reds on day one kills trust in the gate. Start the guard at Form A only, and disclose Form B as documented residual.**

2. **Form C is ambiguous by construction — and worse, its shape does not even mean "anchor" (sharpened in revision 2).** `FloatDiagnostic:98` suffix-matches three tracked files (`FloatDiagnostic.cs`, `FloatDiagnosticVerifyCapture.cs`, `FloatDiagnosticPlayModeTests.cs`). It resolves to the *tests* file and holds — but only a human knows that. **And 4 of the 12 form-C-shaped spans in the doc set are not citations at all** — `loopBlend:1` (×2), `inset:0`, `preserveHierarchy:0` are config `property:value` pairs, byte-identical in shape to a line anchor (§8b). A form-C tier therefore opens at a **33% false-red rate before it resolves anything**. Form C belongs on the residual list with E/F, not in the guard.

3. **Forms E and F are outside every anchor net (NEW in revision 2).** A guard built to forms A–D silently skips **~13 line references across 8 sites** (§1): the slash-continuation tails (`CastawayGroundSnap:92/266/482` — only `92` parses) and every unbackticked prose `line NNN`. **These belong on the guard's documented residual list from day one**, because they are invisible to it *and* they all target `Assets/**` — the highest-drift bucket. Do not let a green guard imply they were checked.

**⛔ Sequencing blocker:** AC3 mandates the unit test live in `tests/scripts/test_gate_scripts.sh`, and **PR #370 is open against exactly that file**. Building the guard now collides. **Build after #370 merges.**

---

## 7. What this PR fixes, and what it deliberately does not

**Fixed (2)** — both reference-only, both in files no in-flight ticket holds:
- `blender-asset-pipeline.md:14` — restored the dropped `2026-06-12_` prefix.
- `game-juice.md:55` — reworded so the deleted draft is named without a path that looks openable. Per AC2b's 🎚️ default: **reworded, NOT resurrected.**

### ⚠ CORRECTED in revision 2 — the locality claim was FALSE

Revision 1 said the unfixed findings were **"all in `.claude/docs/unity-conventions.md`"**. **They are not.** Re-swept in revision 2: every form-A–D anchor in the 11 non-`unity-conventions.md` docs was opened individually at `0f14b4f` — **19 anchors across 4 files** — and **3 of them drift**:

| Citing file | Anchors | Verdict |
|---|---|---|
| `blender-asset-pipeline.md` (`:54`, `:60`, `:308`) | 7 | **all HOLD** — `WeaponPackAssetGen.cs:273`/`:282`/`:287` and `LowPolyVertexColor.shader:77-79`/`:160-162`/`:322-323` are exact; `:24` self-ref is exact |
| `lowpoly-quality.md` (`:54`, `:72`) | 4 | **all HOLD** — `LowPolyVertexColor.shader:64`/`:159`, `WeaponPackAssetGen.cs:33`/`:98` exact |
| `procedural-animation-verbs.md` (`:106`, `:118`, `:128`) | 7 | 5 hold (`AttackClipPoseDiag.cs:184`–`:190`, `SwingVerifyCapture.cs:187`, `…_repaired.anim:10569`, `CastawayCharacter.cs:282`) · **2 DRIFT** at `:128` |
| `unity6-mastery.md` (`:87`) | 1 | **1 DRIFT** |

**How the claim went wrong — and the fix.** Two of the three (`HeldWeaponCycleDebug.cs:260` / `:281`) *were* found and recorded in §4 — but as bare **target** anchors with no citing-file column, so the §7 summary swept them into `unity-conventions.md`. That is a **mis-routing defect, not an omission**: had it shipped, both would have been handed to `86cazhtn1`, which never touches their file, and they would have **stranded**. §4 now carries a `Cited from` column so a target anchor cannot lose its origin again.

**The third (`unity6-mastery.md:87` → `AxeNudgeTool.cs:563`) is a genuine miss.** It was inside declared scope, it was never counted, and it appears nowhere in revision 1 or the PR diff (0 grep hits). Stated plainly rather than folded into the routing correction: the audit missed one anchor in a file it claimed to have swept, which is why **43 is published as a floor** and not as a total.

**Routing (corrected):**
- **The 3 outside `unity-conventions.md` → Tess, PR #398**, already in flight, fixing all three citing sites by pairing the symbol with the number rather than replacing it. **Not touched here** — that file pair is out of scope for this PR.
- **The remainder → `unity-conventions.md` only**, still held in flight by `86cazhtn1`. Hand §2 + §4 of this file over as a work item, or file a successor scoped to that one file once the ticket lands. Every anchor is listed with its true current content, so the repair is mechanical.

**Why they were not fixed here:** `unity-conventions.md` is out of scope because `86cazhtn1` holds it; `procedural-animation-verbs.md` + `unity6-mastery.md` are out of scope because #398 holds them. AC2's 🔒 also applies: several are other tickets' prose, and fixing a reference correctly means naming the symbol — which touches the sentence.

---

## 8. Bounded convergence claim

**Guaranteed:** every anchor and every cited path in `.claude/docs/*.md` + `CLAUDE.md` was opened at `0f14b4f` and carries a verdict. The two fixed citations resolve.

### 8a. Observer ledger — every headline figure, with its observer count

Uma's review closed with a **"What I took on trust"** section naming four figures nobody had re-derived. Recording that was the right call, and it exposed that this artifact published *verified* and *single-observer* numbers in the same typeface. **Fixed here: every figure now carries how many independent observers produced it and whether its scoping rule is written down.** The rule from §4's withdrawal generalises — *a count with no stated scoping rule is not a measurement* — and its sibling is: **a count with one observer is not a convergence.**

| Figure | Value | Observers | Status |
|---|---|---|---|
| Population, forms A–D | **165** | **3** | **EXACT.** Third implementation written in this revision reproduces it — decomposition in §8b |
| — form B (the load-bearing class) | **82** | **3** | exact, all three |
| — form A | **71** | **3** | exact, but only with two non-obvious inclusions named (§8b) |
| — form C | **11** | **3** | exact, but only after excluding 4 `property:value` look-alikes (§8b) |
| — form D | **1** | **3** | exact |
| DEAD anchors | **3** | **2** | exact — Uma re-derived independently |
| DEAD paths (distinct targets) | **5** | **2** | exact, and **rule-independent** — the one figure no scoping choice moves |
| DRIFTED | **≥44** | 2 on the *shape*, **1** on the integer | **FLOOR** — see §8c |
| HOLDS | **≤113** | derived, not counted | **CEILING** — see §8c |
| Drift distribution (`Assets` / `.github` / `.claude`) | **29 / 13 / 1** | **2** | reproduced to **±1** (Uma's proxy: 28 / 12 + boundary) |
| "the guard catches 0 of them" (§6 Tier 3) | — | **2** | reproduced — every drifted anchor she opened is Tier-1/2 green |
| Not tip-verifiable (historical prose pins) | **5** | **1** | ⚠ **single-observer, unverified.** Re-deriving it needs a judgement call on where "explicitly historical" starts — the same boundary that put Uma's `ci.yml` count at 12 and this one at 13. Labelled, not shored up |
| Path citation **sites** | **128** | **2 implementations, one stated rule** | reproduces exactly here — **but a second implementation of the same one-sentence rule gives 101.** See §8d |
| Path **distinct** targets | ~~89~~ | 1 | ⚠ **WITHDRAWN — not reproducible.** §8d |
| Form-B attribution failure rate | **23/82 = 28%** | **1**, rule stated | supersedes revision 1's unpreserved "~20%". Single-observer but **rule-stated, so anyone can re-run it**; Uma corroborated the *class* (4 independent false-binds) but not the ratio |

**The distinction that matters in this table is not verified-vs-unverified, it is `reproducible` vs `not`.** A single-observer figure with its rule written down (the 28%) is recoverable by the next reader. A multi-observer figure whose rule was never stated (the withdrawn 9, the withdrawn 89) is not — and it is exactly the shape that reached the Sponsor as fact and had to be retracted.

### 8b. The 165 reproduces on a third implementation — with its class boundaries stated

A tokenizer written fresh in this revision, independent of both revision 1's and Uma's, over the same 12 files at `0f14b4f`. **`A=71 · B=82 · C=11 · D=1 → 165`, exact.** But a strict `` `path.ext:NNN` `` full-match rule alone lands on **68**, not 71 — the gap is not noise, it is three specific editorial shapes that any future guard must decide about explicitly:

| Form A — 71 | n | What it is |
|---|---|---|
| strict backticked `` `path.ext:NNN` `` full match | **68** | the uncontroversial core |
| **arrow "moved-from→to" spans** | **+2** | `` `ci.yml:161→172` `` (`unity-conventions.md:29`), `` `SettingsVerifyCapture.cs:191→213` `` (`:380`) — two anchors' worth of intent in one span |
| **one occurrence outside backticks entirely** | **+1** | `auto-merge.yml:52` at `unity-conventions.md:394` — a form-A anchor that no backtick-gated sweep sees |

| Form C — 11 | n | What it is |
|---|---|---|
| clean `` `Identifier:NNN` `` test-name anchors | **8** | `WasdMovementPlayModeTests:119`, `RunOnShift:119`, `WasdCrouch:97`, `JumpOnSpace:116`, `JumpClipSelectionAndLanding:104`, `AirborneAirControl:104`, `LocomotionSamplingHarness:143`, `CastawayAnimation:58` |
| leading numbers of the three form-E slash-continuations (§1) | **+3** | only the head of `CastawayGroundSnap:92/266/482` etc. parses |
| **excluded by hand: `property:value` look-alikes** | *(4)* | `loopBlend:1` (×2), `inset:0`, `preserveHierarchy:0` — **identical in shape to a form-C anchor** |

**Two guard-design consequences fall straight out**, and both belong on §6's residual list beside forms E/F: a backtick-gated guard misses at least one real form-A anchor, and **form C cannot be separated from `property:value` prose by shape alone** — 4 of the 12 form-C-shaped spans in the doc set are config-property pairs, so a form-C guard starts at a **33% false-red rate** before it checks anything. That sharpens §6 hazard 2 from "ambiguous *target*" to "not even reliably an *anchor*".

### 8c. The 114 / 43 split — now bounded, not asserted

Uma reproduced both buckets to ±1 by independent methods but did not open all 165, so the exact integers stood on one observer. **They cannot be shored up by machine — that is this artifact's own §6 Tier-3 finding turned back on itself:** nothing in a bare `file.ext:NNN` tells a machine what the line should say, so hold-vs-drift is a human read, and a second human read of all 165 is a second full audit, not a check. So the split is **bounded instead**:

> **Population 165 is exact (three observers). Dead 3 and historical 5 are fixed. Therefore `holds + drifted = 157`, and since `drifted ≥ 44`, `holds ≤ 113`.**

Revision 1 published `114 + 43 + 3 + 5 = 165`. The arithmetic closed then and **it still closes now** — `113 + 44 + 3 + 5 = 165` — because `AxeNudgeTool.cs:563` was never outside the population. It is the single form-A hit for that file, at `unity6-mastery.md:87`, and both Uma's tokenizer and this revision's count it inside the 71. It was **tokenized and then never given a verdict**, so it sat silently in the holds bucket. Verified at `0f14b4f`: `AxeNudgeTool.cs` is **1278** lines, `ComposeLocalRot` is *defined* at `:1008` with call sites at `:461`/`:477`/`:541`, and `:563` is `else if (_target == 7)`.

**That is the honest shape of the correction: the population was never wrong, the boundary was.** A miss of this class moves an anchor from holds to drifted without changing the total — which is precisely why it survived revision 1 undetected, and why `≥44` / `≤113` is published instead of a new pair of exact integers.

### 8d. The path denominator — `128` reproduces, `89` is withdrawn

Uma flagged `128 / 89` as a broader rule she could not reconstruct. Re-measured here under an **explicitly stated** rule:

> **Rule P1:** a backticked span, containing `/`, ending in a `.ext` after stripping any trailing `:NNN` anchor, with no spaces.

| | sites | distinct |
|---|---|---|
| **P1, this implementation** | **128** — reproduces the artifact's figure exactly | **87** |
| **P1 as Uma implemented the same sentence** | **101** | **66** |
| P2 — extension only, slash optional (counts bare `ci.yml`, `MineOre.cs`) | 607 | 315 |
| P3 — slash only, extension optional (counts directory cites like `Assets/Scripts/Runtime/`) | 227 | 152 |

**`89` is withdrawn.** Under P1 the distinct count is **87**; no normalisation tried yields 89 (raw span text → 91, basename-only → 83). It goes the way of the "9 sha pins" — a number whose rule was never recorded and which therefore cannot be defended. *(Revision 2 earlier drafted a "three rules give 101/66, 128/89, 189/112" line; `189/112` could not be reproduced under any rule stated here and has been removed rather than restated.)*

**The sharper finding is the first two rows.** The rule was stated in one sentence, and two people implementing that one sentence got **128 and 101** — a 27% spread on the denominator, with the *same* pin, the *same* files, and no disagreement about intent. **A path-cite denominator is not a property of the repo; it is a property of the sweeper.** Every path figure in this artifact is therefore published rule-first.

**What survives all of it: the 5 dead paths.** They reproduce exactly across both implementations and all three rules, because a dead target is dead under any net wide enough to see it. **The decision-relevant number was never the denominator** — which is why §3 and §6 Tier 1 are unaffected by any of this, and why `86cazhtfy` is not gated on it.

### 8e. NOT guaranteed
- **Nothing is mechanically enforced.** No guard was built (dispatch scope). **≥52 findings** (≥44 drifted + 3 dead anchors + 5 dead paths) can recur, and the **≥50** unfixed ones are still live on `main` today. *(Revision 1 said 51/49; the count moves with the drift floor, so it is a floor too.)*
- **The exact hold/drift integers are single-observer and cannot be machine-checked** — §8c. Only the bounds (`≥44` / `≤113`) and the population (165) are multi-observer.
- **The "5 not tip-verifiable" figure has one observer** and depends on a judgement boundary that two observers already drew differently (§8a).
- **Forms E/F (§1) are counted but NOT audited** — 13 line references have no hold/drift verdict at all.
- **`.claude/agents/*.md`, `team/**/*.md`, `.github/**` were NOT swept** as *citing* files — only as citation *targets*. `86cazhmtj`'s `decisions-batch-pr-template.md` family (5 cites from `.claude/agents/priya.md`, `team/ROLES.md`, `team/GIT_PROTOCOL.md`, `team/orchestrator/dispatch-template.md`) is **outside this audit on the citing axis** and remains that ticket's.
- **This audit is a snapshot.** It was accurate at `0f14b4f` and starts decaying with the next merge into any cited file — which is precisely the argument for the §6 guard.
