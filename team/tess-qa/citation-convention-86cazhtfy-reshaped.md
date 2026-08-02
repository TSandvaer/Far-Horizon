# `86cazhtfy` reshaped onto the measurement — citation convention

**Measured at:** `origin/main` @ `fb2ac24` for everything new here; `0f14b4f` where I reproduce the
`86caz4yhj` audit (that audit's own pin). Every figure below states the rule that produced it, per the
standing requirement in the merged audit § 4 (*"a count with no stated scoping rule is not a measurement"*).

**Input:** `team/drew-dev/citation-anchor-audit-86caz4yhj.md`, merged `bf33b65` (PR #396), measured at `0f14b4f`.
I read the merged artifact, reproduced what I could, and measured five things it did not.

**Status:** proposal reshaped, still a proposal. Nothing here is adopted; no guard is built (the ticket's
own OOS, and the audit's § 6 sequencing blocker on PR #370, both still hold).

**Revision 3** (peer review, PR #412 comment `5150715894`). Three figures corrected — **T1 1 → 3 sites**
(§ 3), **S1 10% → 15%** (§ 4), **"expected" → "measured" value** (§ 5) — all conclusion-preserving, two of
them undercounts that argue the conclusion *harder*. One thing added that was missing and should not have
been: **§ 8b, the guard's scope, grammar, and day-one red count** — the measurement § 5 demanded of the
alternative and § 8 never produced for its own recommendation. **It kills the repo-wide form of that
recommendation.** Corrections are marked ⚠ CORRECTED in place; nothing is silently patched.

---

## 0. Verdict

**Do NOT adopt the convention as written.** Its measured reach is **1 of ≥44** drifted anchors (2%), its
`team/**` half governs **3 citation sites in the entire repo** across **1 citing file** — two hold and one
was **never resolvable at any point in the repo's history** (§ 3) — its `Assets/**` half would introduce a
syntax with **0 in-repo occurrences**, and its remedy — the § anchor — has a **measured 15% failure rate**
on the population that already uses it (**6 of 40**, § 4).

**Adopt instead the rule the ticket already wrote down as its own AC3 edge case:** *every citation to a
location in this repo carries a checkable payload — an exact string greppable in the target — beside
whatever address form it uses.* That rule is form-agnostic, it is already the practiced house form in
three places in this repo, and it is the only candidate that moves the audit's § 6 Tier-3 number
(*"catches 0 of ≥44"*) off zero.

> ### ⛔ …but NOT at repo-wide guard scope. I killed my own recommendation on my own standard.
> Revision 2 of this document rejected candidate 2 on a **day-one red count** and then recommended
> candidate 3 **without producing one**. That asymmetry was the correct thing to be caught on. The count
> now exists (§ 8b, rule PA, stated grammar + stated window): **repo-wide = 137 day-one reds on 606 form-A
> anchors.** A gate cannot be switched on already-red, and § 8 step 1 says *forward-only*, which repo-wide
> scope contradicts outright. **Candidate 3 is therefore NOT adoptable repo-wide.** It survives at exactly
> two scopes, and only because they were measured: the **audited doc set (0 day-one reds on 93 anchors)**
> and **changed-lines-only (0 by construction, ~21% ongoing per-PR red rate)**. The recommendation below is
> scoped to those two and to nothing else.

**This is not a rejection of the author's thinking.** The ticket's AC3 — *"what to do when a doc section
has no heading to anchor to (add one, or quote a distinctive phrase)"* — **is** the payload rule. The
measurement says AC3 is the main rule and the AC1/AC2 file-type split is the part that does not survive.

---

## 1. The relayed figures, checked one by one

I was handed six figures second-hand and told to treat each as a claim. Four reproduce, one is stale,
one does not reproduce as stated.

| Relayed claim | Verdict | What the measurement says |
|---|---|---|
| ~165 anchor sites **across the repo** | **Population reproduces; "across the repo" does NOT** | 165 is exact and I am the **fourth** independent tokenizer to land on it (§ 2). But the audit's citing scope is 11 `.claude/docs/*.md` + `CLAUDE.md` = **12 files of 165 tracked `.md`**. The coincidence that the repo has 165 markdown *files* and the audit found 165 anchor *sites* invites exactly this misread. |
| ~43 have drifted | **STALE** — superseded in the merged doc | Its § 0 and § 8c publish **`≥44` drifted / `≤113` holds** as a floor/ceiling, after `AxeNudgeTool.cs:563` was found post-audit. 43 was revision 1. Decide against the floor. |
| the convention prevents ~**1** of those 43 | **REPRODUCES** | Merged § 5. The one is the `unity-conventions.md` self-reference — and see § 5 below for what form that one anchor is written in, which is the sharpest fact in this document. |
| ~**67%** of drift in `Assets/**`, where the proposal keeps line numbers | **REPRODUCES** (29/43) and is **understated** | Merged § 1: every form-E/F reference targets a C# file or shader under `Assets/**`, pushing the share up. |
| ~**30%** in `.github/**`, outside scope | **REPRODUCES** (13/43), two-observer ±1 | Uma's independent pass gives 12; the two differ only on where "explicitly historical" starts. |
| largest class = ~**82** bare `:NNN`, invisible to the sweep, **four** syntaxes not one | **Half reproduces, half does NOT** | `B = 82` is exact on three observers and now four (§ 2), and it *is* invisible to a form-A-only sweep (form A sees 71/165 = 43%). But there are **six** forms, not four — E and F were added in the merged revision 2 — and I find a **seventh** (§ 6). More importantly: **82 is a *population* figure. The merged audit publishes no per-form *drift* breakdown at all.** Tested in § 5: of the 13 individually-named, twice-confirmed drifted anchors, **12 are form A** and 1 is form B. |

**The one that matters is the last row.** "The largest class is invisible to the sweep, therefore aim the
remedy at detection" moves silently from *how citations are written* to *how citations break*. Those are
two different denominators, and nobody had measured the second one.

---

## 2. Fourth-observer reproduction of the population

> **Rule P-T:** over `git show 0f14b4f:<file>` for the 11 `.claude/docs/*.md` + `CLAUDE.md`, extract every
> backticked span; classify by the merged audit's § 1 taxonomy.

| Form | Merged audit | This pass | Match |
|---|---|---|---|
| **A** `path.ext:NNN` | 71 (68 strict + 2 arrow + 1 unbackticked) | **68 strict**, +2 arrow spans, +1 unbackticked = **71** | exact, and the decomposition matches line for line |
| **B** bare `` `:NNN` `` | 82 | **82** | exact |
| **C** `Identifier:NNN` | 11 (8 clean + 3 slash-continuation heads) | **8 clean + 3** = **11** | exact |
| **D** `sha:NNN` | 1 | **1** | exact |
| excluded `property:value` look-alikes | 4 | **4** — `loopBlend:1` ×2, `inset:0`, `preserveHierarchy:0` | exact |

**165 now has four independent observers with the class boundaries agreeing.** I also reproduce the
notation finding exactly: glued `path:NNN@<hex>` = **0 occurrences**; the only `@<hex>` in the doc set is
`Library/PackageCache/com.unity.render-pipelines.universal@d3aed158d698`, a directory name.

That matters for the decision on its own: **the `Assets/**` half of this ticket proposes introducing a
syntax with no in-repo track record**, not formalising an existing habit.

---

## 3. What the convention would actually govern — measured

The proposal has two halves. I measured the population of each.

> **Rule T1:** an anchor targets `team/**` if a backticked span in any tracked `*.md` at `fb2ac24` matches
> `` `[^ `]*team/[^ `]*\.md:[0-9]+` ``.

**Result: 3 sites / 2 distinct targets / 1 citing file.**

> ⚠ **CORRECTED (revision 3).** Revisions 1–2 published **1 site**. Re-running T1 verbatim at `fb2ac24`
> returns **3**. The undercount was mine; the peer review caught it. All three live in
> `team/erik-consult/enemy-hit-feedback-hitflash-particle-flinch.md`:

| Citing site | Anchor as written | Verdict |
|---|---|---|
| `:296` | `` `team/uma-ux/combat-cluster-design-brief.md:248` @ `fee2604` `` | **HOLDS** — opened, this pass |
| `:327` | `` `origin/main:team/uma-ux/combat-cluster-design-brief.md:248` @ `fee2604` `` | **HOLDS** — same target |
| `:191` | `` `team/STATE.md:682` `` | **DEAD — and it was born dead (below)** |

**The two live ones: I opened them rather than inheriting a verdict.** Revisions 1–2 supported "that site is
not broken" with *"the spaced sha form that the merged audit § 4 reports as holding."* That was
**form-transfer**: the audit's two spaced-sha instances are `AttackClipPoseDiag.cs:184`–`:190` and
`SwingVerifyCapture.cs:187`, **neither of which is this citation**, and the audit's § 8e states `team/**`
was never swept as a citing surface — so no audit verdict over this cite exists to inherit. Right answer,
wrong warrant. The check I actually ran: `git show fee2604:team/uma-ux/combat-cluster-design-brief.md`
(409 lines) line 248 = *"animal analog of the castaway hit-react states"*, matching the phrase the citing
prose quotes. Superseded at tip, and the citing prose self-discloses that.

**`team/STATE.md:682` is dead, and resolving it produced the sharpest single fact in this section.** The
file is **323 lines** at `fb2ac24`. I did not re-anchor it by guess; I resolved it: across **all 116
commits that touch `team/STATE.md` on every ref**, its **maximum length ever** is **481 lines**.

> **`team/STATE.md:682` never resolved at any point in this repository's history. It is not drift — it is
> a born-dead anchor**, wrong at the instant it was typed, and nothing caught it. *(Mitigation, stated: the
> citing prose narrates it historically — "two cites … had NOT been re-checked and were both wrong" — so it
> is a quoted-dead anchor, not a live claim. It still counts under T1 as written, and revisions 1–2
> disclosed no such exclusion, so counting it is the honest call.)*

**And the corrected data argues the recommendation better than the wrong figure did.** The two survivors
hold **because each carries a quoted phrase pinned to a sha** — i.e. they hold under the **payload** rule
(§ 8), not under the § rule. The one that died is a bare line anchor into a run log with **no payload at
all** — where a `§` anchor would have died exactly as silently. The `team/**` sample is n=3, but every
member of it points the same way.

> **The `team/**` half of this convention governs 3 citations, 2 of which are not broken and 1 of which no
> anchoring convention could have saved (it never existed to be anchored). Its measured prevention count
> is 0.**

The `.claude/docs/**` half prevents **1** — the merged § 5 figure, which reproduces.

**Combined: 1 of ≥44 = 2.3%.** The corrected site count does not move it: 3 of ≥44 is still ~0%.

### 3b. And the audit saw ~17% of the surface

> **Rule U1:** one loose A+B counter (`` `path.ext:NNN` `` or bare `` `:NNN` ``), run over *both* surfaces at
> `fb2ac24`, excluding the audit artifact's own 149 `Cited from` bookkeeping references.

| Surface | Refs |
|---|---|
| Audited as *citing* files (`.claude/docs/*.md` + `CLAUDE.md`) | **195** |
| Never swept as citing files (`team/**`, `.claude/agents/**`, everything else) | **945** |

≈ **1 : 4.8**. The counter is loose — it reports 195 where the strict A–D population is 165 — so the
**absolute** numbers are sweeper-dependent exactly as the merged § 8d warns. The **ratio** is not: it is one
implementation, one pin, both sides. **Roughly 83% of the repo's line-anchor surface has never been given
a hold/drift verdict**, and the heaviest unswept citers are `team/uma-ux/*.md` (112 / 105 / 70 / 67 refs)
— i.e. the very tree this convention's `team/**` half is aimed at, measured only as a *target* and never as
a *source*.

---

## 4. The remedy's own failure rate — § anchors are not drift-proof

The ticket's stated basis is that `team/**` and `.claude/docs/**` are *"prose with stable headings, where a
`§` anchor is both more readable and drift-proof."* **Drift-proof is testable, and it is false.**

> **Rule S1:** population = every `§ "<string>"` occurrence in a tracked `*.md` at `fb2ac24` (**43 sites /
> 35 distinct**). The target is the file named on the same line. A site **RESOLVES** if `<string>` occurs
> anywhere in that target at `fb2ac24`. **FAILS-SECTION** = target file exists, string absent.
> **FAILS-PATH** = target file absent from the repo. **OUT-OF-SCOPE** = the named target is not a repo file.

| Class | Sites | Instances |
|---|---|---|
| OUT-OF-SCOPE (named target is not a repo file) | 3 | `team/DECISIONS.md:174` and `:178` → **PR #362 body**; `team/DECISIONS.md:182` → **PR comment `5129792893`** |
| **FAILS-PATH** | **3** | `.claude/agents/drew.md:67` and `.claude/agents/uma.md:10` → `combat-architecture.md` § "Harness coverage gap"; `.claude/agents/devon.md:76` → same file § "Every loot-table item must be in STARTER_ITEM_PATHS." — **`combat-architecture.md` has 0 tracked files here**, it is a Godot-era doc |
| **FAILS-SECTION** | **6** | see the table below |
| RESOLVES | 31 | |
| **In-scope population** | **40** | **FAILS-SECTION = 6 = 15%** |

> ⚠ **CORRECTED (revision 3).** Revisions 1–2 published **4 of 40 = 10%**. The peer review re-derived **6 of
> 40 = 15%** and I reproduce it: every one of the six below returns **0** under `grep -cF` against the file
> its own citing line names, at `fb2ac24`. **Direction: my figure was an undercount — the § remedy fails
> *worse* than I reported, which strengthens this section rather than softening it.**

| # | Citing site | Named target | § string | Hits |
|---|---|---|---|---|
| 1 | `team/orchestrator/dispatch-template.md:255` | `team/TESTING_BAR.md` | `Milestone-gate journey probe` | **0** |
| 2 | `team/orchestrator/dispatch-template.md:134` | `team/TESTING_BAR.md` | `Visual primitives — observable delta required` | **0** |
| 3 | `team/devon-dev/snowcap-facet-plan.md:69` | `unity-conventions.md` | `procedural committed assets go stale` | **0** |
| 4 | `team/orchestrator/dispatch-template.md:393` | `team/GIT_PROTOCOL.md` | `Orchestrator merge-gate verification (HTML5-visual-gated PRs)` | **0** |
| 5 | `team/orchestrator/dispatch-template.md:402` | `team/TESTING_BAR.md` | ``Auto-memory: `html5-visual-gated-author-self-soak` `` | **0** |
| 6 | `.claude/docs/blender-asset-pipeline.md:419` | `unity-conventions.md` | `DEFAULT gameplay capture does NOT frame a HELD weapon` | **0** |

**Two corrections inside the correction, and both are self-indicting:**

- **`.claude/agents/tess.md` is DROPPED.** Revisions 1–2 counted it as a FAILS-SECTION. It **is not in the
  S1 population my own rule defines**: at `fb2ac24`, `tess.md:51` reads ``Per testing-bar `Milestone-gate
  journey probe (mandatory at RC boundary)`:`` — backticks, no `§`, no double quotes. The only
  `§ "Milestone-gate journey probe"` in the repo is `dispatch-template.md:255`. **Citing an instance the
  stated rule does not reach is the exact class this document indicts**, committed in this document, about
  my own persona file. It is removed from the count and left here as the record.
- **#5 was a misclassification, not a miss.** Revisions 1–2 filed it OUT-OF-SCOPE as *"auto-memory name,
  not a repo file."* But the citing line names `` `team/TESTING_BAR.md` `` as the target and the
  memory-entry name sits **inside the section title**. Under my own rule (target = the file named on the
  same line) it is in scope, and it fails.

> ### ⚠ Rule-sensitivity, disclosed — TWO independent axes on this one measurement
> **Axis 1 — heading shape.** Require the string to sit on a `#` heading line and S1 flags **17**, a **4×
> spread** on the same population, same pin, same files. Most "§" in this repo names a **bold bullet**, not
> a markdown heading.
> **Axis 2 — string normalisation (NEW in revision 3, and it bit me while re-running the correction).**
> The 6 above are under a normalising match (fold quote glyphs `' " ' "`, strip a trailing `…`/`...`, strip
> a trailing `.`, case-fold). Under a **strict** `grep -F` with no normalisation the same population gives
> **11 of 40 = 28%** — five more failures, every one a punctuation or case artefact rather than a real
> staleness: `developer-accuracy-performance-research.md:31` ×3 (the cite writes `'…'` where the target
> heading has `"…"`, and one cite ends in `...`), `dispatch-template.md:174` (cite adds a sentence-final
> `.` the heading does not carry), `rock-affordance-direction.md:512` (cites `hard don'ts`; heading reads
> `Hard don'ts`).
>
> **So S1 is 15% or 28% depending on a normalisation rule nobody had stated.** That is the merged § 8d
> finding (`128` vs `101`) reproducing for the **third** time in this area — and this time inside the
> correction to my own figure. **Consequence for whoever builds a guard: check the payload as a STRING,
> never as a heading, and ship the normalisation rule *with* the number — quote-glyph folding, trailing
> ellipsis, trailing period, case. Without it the guard's own output is unreadable.**

> ### A figure carries its population, or it is not a figure
> Every number in this area has now moved by 1.8×–5× purely on an unstated rule: S1 heading-shape (4×),
> S1 normalisation (1.9×), form-A payload compliance (5×, § 8b). This is the same defect class as a
> pixel measurement quoted without the population it was computed over — **the number is not wrong, it is
> unattached**, and an unattached number is indistinguishable from a wrong one to every later reader.
> **Every percentage in this document is written as `N of M under rule X at pin Y`.** Where I failed that,
> the failure is marked ⚠ CORRECTED rather than silently patched.

### The asymmetry that actually decides this ticket

All six FAILS-SECTION were found by **one exact-string grep**. Per the merged § 6 Tier 3, **0 of ≥44**
line-anchor drifts are findable that way — every one passes exists-and-long-enough.

> **A § cite that goes stale goes RED. A line anchor that goes stale goes GREEN.**

**That is the Sponsor's instinct, and it is correct.** But it is a property of **the payload**, not of the
file type — the § text is checkable *because it is a string*, and a symbol name beside a line number is
checkable for exactly the same reason. Scoping the rule by file type keeps the wrong half.

### 4b. The repo's dominant § form has no payload at all

> **Rule O1:** a cross-file ordinal cite is `` `<file>.md` `` followed within 12 characters by `§N` (or `§N.N`),
> at `fb2ac24`. It "carries a payload" if a parenthetical of 3–60 characters follows the ordinal.

| | Sites |
|---|---|
| Cross-file ordinal § cites, total | **422** |
| — **WITHOUT** payload (`` `game-juice.md` §2 ``) | **368 (87%)** |
| — WITH payload (`` `game-juice.md` §2 (hard don'ts) ``) | **54** |
| Quoted-heading § cites (§ 4's population) | 43 |

**A bare `§2` is an address, not a payload.** Insert a section above it and all 30 `` `game-juice.md` §2 ``
cites silently point at the wrong section — and `§2` still exists, so nothing goes red. That is the exact
failure this ticket exists to cure, reproduced inside the ticket's own preferred syntax, at **368 sites**.

**So "cite by § anchor, never line number" would, on this repo's observed habit, mostly mandate a bare
ordinal — the same silent-drift class in different clothes.** The 54 payload-carrying ones (Uma's own
`§2 (hard don'ts)` habit in the spec family) are the shape that works, and they are the minority.

---

## 5. Drift by citation FORM — the "aim at the largest class" instruction, tested

The merged audit measures drift by **target area** (`Assets` / `.github` / `.claude`) and never by **form**.
So "the largest class, form B, is where to aim" was never measured. It is measurable on the sample the
audit names and confirms twice.

> **Rule F1:** for each of the 13 individually-named-and-twice-confirmed drifted anchors in the merged
> § 4 table, read the *citing line* at `0f14b4f` and classify the anchor's written form per § 1.

| Form | Count | Which |
|---|---|---|
| **A** `path.ext:NNN` | **12** | `ci.yml:207` `:209` `:485` `:487` `:1234` `:1259` — all six written out as `ci.yml:NNN` on `unity-conventions.md`'s job-shape bullet · `MovementCameraScene.cs:537` `:2515` · `CastawayCharacter.cs:1182-1185` · `HeldWeaponCycleDebug.cs:260` `:281` · `Assets/Scripts/Runtime/AxeNudgeTool.cs:563` |
| **B** bare `` `:NNN` `` | **1** | `` `:189` `` — the `unity-conventions.md` self-reference |

**Twelve of thirteen confirmed drifts are form A — the form the standard single-form sweep already sees.**

And the single form-B one **is the single anchor the §-anchor proposal would have saved**. The two facts
are the same fact: the one citation the convention rescues is also the one a form-A-only guard cannot see.

> ### ⚠ Selection bias, stated rather than hidden
> These 13 are the audit's *representative confirmations*, not a random draw from the ≥44. A form-A anchor
> names its own target, so it is cheaper to confirm — which plausibly biases the sample toward A. **Honest
> bound: form A carries ≥12 of ≥44; form B carries ≥1; ~31 remain unclassified by form.**
> Classifying those ~31 is the cheapest next measurement in this whole area and it is the *only* thing that
> settles candidate 2 below. It is not done here.

**What this does to the instruction I was given.** "If the largest class is bare `:NNN` invisible to the
sweep, a detection change may be worth more than a citation-style change" — tested, and it does not
survive on the available evidence. Form B is the largest *citation population* (82/165) but carries **1 of
13** confirmed drifts, and widening a guard to reach it costs a measured **23 false reds on day one**
(merged § 6: 23/82 = 28% attribution failure). **Negative *measured* value on the available evidence, as a
first move.** *(⚠ CORRECTED in revision 3 from "negative expected value" — an expected value needs the
per-form drift distribution that § 5's own ⚠ box says I do not have. The stronger phrase was reaching past
the data by exactly the margin this document indicts elsewhere.)*

---

## 6. A seventh citation form — and why sweep-widening is not convergent

At `0f14b4f`, `unity-conventions.md`'s `ci.yml` job-shape bullet — **the worst-drifting line in the repo,
6/6 drifted** — also carries `` `211-212` ``, `` `220` ``, `` `488` ``, `` `490` ``, `` `492-493` ``.

**Backticked bare line numbers. No colon, no filename.** Five more line references, on that one line,
outside every form in the merged audit's A–F net: no `:NNN` sweep sees them (no colon), no path sweep sees
them (no filename). They inherit their target from `ci.yml` earlier on the same line — via the same
nearest-preceding-filename attribution that fails 28% of the time.

Call it **form G**. It is uncounted, unaudited, and it sits inside the single worst-drifting block in the
repository. I counted it on **one line only** and did not sweep for it (§ 9).

> **The general point is worth more than the finding.** Revision 1 found four forms. Revision 2 found six.
> I found a seventh, in the block everyone had already read twice. **Widening the sweep is not a convergent
> strategy** — each widening reveals another syntax, and every reader who sees a green guard infers a
> coverage that does not exist. **Payload-checking is convergent**, because it checks *content* and
> therefore does not care how the address is written.

---

## 7. Candidate remedies, with what each would and would not have prevented

| Candidate | **Prevents** (of ≥44) | **Detects** (of ≥44) | **Day-one reds** | In-repo track record | Verdict |
|---|---|---|---|---|---|
| **1. This ticket as written** — § for `team/**` + `.claude/docs/**`; `file:line @ sha` for `Assets/**` | **1** (2%). `team/**` half: **0** — it governs 3 sites (§ 3), 2 hold and 1 was born dead | **0** | n/a — no guard proposed | § half: **15% measured failure** (§ 4, and 28% under a strict match) and **87% of its dominant form carries no payload** (§ 4b). `Assets/**` half: `path:NNN@sha` has **0 occurrences** — new practice, no track record | **Do not adopt as written** |
| **2. Detection change — widen the guard to form B** | **0** | ≥1 of the 13 named | **23 FALSE reds** (28% of 82) | — | **Do not do first.** Not convergent either — form G (§ 6). Revisit only after the ~31 unclassified drifts are classified by form |
| **3a. Payload rule, guard at REPO scope** | **0** | ≥12 of the 13 named | **137 TRUE reds** on 606 anchors (§ 8b, rule PA) | as 3b | **NOT ADOPTABLE — killed on § 5's own standard.** A gate cannot be switched on already-red; forward-only and repo-wide are contradictory |
| **3b. Payload rule, guard at CHANGED-LINES scope + advisory doc-set** (the ticket's own AC3, generalised) | **0 — it does not stop drift, and I will not claim it does** | **≥12 of the 13 named**, and in principle all ≥44 — the only candidate that moves § 6 Tier 3 off zero | **0** by construction; **0** on the audited doc set (93 anchors, 100% compliant today); **~21% ongoing** per-PR rate (86/416) | **Yes, three places:** the two 100%-holding blocks in `unity-conventions.md` (18/18 and 10/10, merged § 4); Uma's `§N (payload)` habit at 54 sites (§ 4b); and `team/TESTING_BAR.md` § "Doc-staleness greps" already sanctions the form *for that file only* | **Adopt — this scope only** |
| **4. Do nothing** | 0 | 0 | 0 | — | ≥50 findings live on `main` today (merged § 8e) |

**Candidate 3 prevents nothing.** It is a detection remedy and must be sold as one. The reason to prefer it
is not that it stops an author mistyping a number — nothing does — but that it is the only option under
which a stale citation *announces itself*.

**And candidate 3 splits in two once the missing count exists.** Revisions 1–2 wrote one row where there
are two, and the row that got recommended was the one never measured. **3a is dead**; 3b is what survives.
The lesson is exactly the one this document was written to make: *the remedy that has not been counted is
not a remedy, it is a preference* — and I applied that to the alternative before I applied it to my own.

### The ticket's own five anecdotes, scored against its own rule

The ticket's evidence is five instances in one day. Scored against the convention it proposes:

| # | Instance | Would the rule have prevented it? |
|---|---|---|
| 1 | Uma `86cay4k73` — `unity-conventions.md` cites drifted `:182`→`:197`, `:185`→`:200` | **Yes** — prose target, § rule reaches it |
| 2 | Uma `86cav8ybj` — *every* `MovementCameraScene.cs` cite drifted, plus three `LowPolyZoneGen.cs` | **No** — `Assets/**`, where the proposal keeps line numbers |
| 3 | Devon PR #389 — 7 anchors re-verified, **held** | n/a — no drift occurred |
| 4 | Drew PR #383 — anchors verified across three branches, **held** | n/a — no drift occurred |
| 5 | "two standing tickets exist" | not an instance |

**1 of 4 concrete instances.** The ticket's own evidence base carries the same ratio the repo-wide
measurement found — which is the cleanest possible statement of what went wrong here. Nothing was
fabricated and nothing was sloppy; **an accumulation of anecdotes was read as a distribution, and the
remedy was fitted to the anecdotes.** The anecdotes were real. They were just not representative, and
four of them were never counted against each other.

---

## 8. Recommendation

**Drop the file-type split. Keep the instinct, restated as a payload rule.**

> **Proposed convention, one sentence:** *every citation to a location in this repo carries a checkable
> payload — an exact string that can be grepped in the target — beside whatever address form it uses.*

- **Prose target** (`team/**`, `.claude/docs/**`): the payload is the § heading or bullet text, quoted
  exactly. A line number or ordinal MAY accompany it; it is not the anchor. **A bare `§N` does not satisfy
  the rule** — that is the § 4b finding, and it is the single most load-bearing clause, because it is where
  the naive reading of this ticket would have landed 368 sites.
- **Code / config target** (`Assets/**`, `.github/**`): the payload is the symbol name or a short snippet,
  written **beside** the number — `` `AxeNudgeTool.ComposeLocalRot` (`AxeNudgeTool.cs:1008`) ``. This is
  what the two 100%-holding blocks already do. **"Beside" is defined, not left to the reader: it means
  *anywhere on the same citing line*, and the payload token must not itself be an address** — full grammar,
  window, and its ±120/±60 sensitivity band in **§ 8b, rule PA**. Writing this bullet without that
  definition is what let the compliance rate range 18%–97% in an independent reader's hands, and the
  written-order variant (`` `Foo.cs:12` — see `Foo.Bar` ``) is compliant under rule PA precisely so the two
  existing 100%-holding blocks, which name their symbols in the *other* order, are not false-redded.
- **The sha is optional and orthogonal.** Merged § 4 proves it is not the discriminating variable — it
  labels the repo's best-holding block and its worst-drifting block alike. It makes a cite *recoverable*,
  not *verifiable*. Do not spend the convention's mandate on it.

**Answering the ticket's ACs directly:**

- **AC1 (pick ONE home, say why): `team/TESTING_BAR.md` § "Doc-staleness greps — negate the marker context,
  and DEMONSTRATE the red".** Not because it is the most-read file, but because **the payload form is
  already stated there and deliberately scoped to that file alone** — *"sanctioned for this file, on the
  strength of an already-merged precedent, not minted as a rule for anyone else."* The real decision in
  front of the Sponsor is **whether to promote that existing local sanction to project-wide**. Putting the
  rule anywhere else would leave two rules about anchoring in two files, which is the divergence that
  bullet's own ⚠ scope-note already had to be added to prevent.
- **AC2 (state the `Assets/**` exception with the `@ sha` pairing):** **reshaped.** There is no file-type
  exception under the payload rule — code and prose differ only in what the payload *is* (symbol vs § text).
  The `@ sha` pairing is **demoted from requirement to option**, on the merged § 4 evidence.
- **AC3 (what if a section has no heading):** **promoted from edge case to the rule.** Quote a distinctive
  phrase. That was always the right answer; the measurement says it is the whole answer.
- **AC4 (no sweep here):** unchanged, and honoured — this document rewrites no citation.

**Answering the Sponsor question the ticket asks** — *"is this worth a project-wide convention, or is
per-PR reviewer diligence sufficient?"*: diligence found **4 instances in one day** and the audit found
**≥44 live on `main`** at the same time. **Diligence catches what a reviewer happens to open.** That is an
argument for a convention — but for one aimed at making staleness *visible*, not at re-scoping which file
types may carry a number.

**Sequencing, if adopted:**
1. Land the rule as one bullet in `team/TESTING_BAR.md`, worded form-agnostically. **Forward-only**, and
   § 8b's scope sentence is part of the bullet — not a separate decision left to whoever builds the guard.
2. Guard **after PR #370 merges** (merged § 6 sequencing blocker): **form A only**, **rule PA below**,
   **changed-lines scope**, with forms B / C / E / F / **G** on a **documented residual list** so a
   green never implies they were checked.
3. **Do not widen to form B** until the ~31 unclassified drifted anchors are classified by form (§ 5).
   That measurement decides it. Nothing else does.
4. **Do not widen to repo scope at all** without the 137-line remediation in § 8b priced first.

---

## 8b. The guard: its scope, its grammar, and its day-one red count

> ⚠ **This section did not exist in revisions 1–2, and its absence was the correct blocker.** § 5 killed
> candidate 2 on a **day-one red count** and § 8 then recommended candidate 3 **with no equivalent figure**.
> Worse, § 8's own prescription (*"the symbol name or a short snippet, written **beside** the number"*) has
> **no window and no token grammar** — so "beside" is unstated, and an independent reader trying to produce
> the missing number got **18% compliance under an adjacency reading and 97% under a line reading: a 5×
> spread on a rule this artifact never wrote down.** That is § 4's own rule-sensitivity finding reproducing
> *inside my recommendation*. The fix is not to pick the flattering number. It is to state the rule.

### Rule PA — stated in full, so the 5× spread collapses to one number per scope

> **Anchor** — a backticked span `` `<path>.<ext>:NNN` `` or `` `<path>.<ext>:NNN-MMM` ``, where `<ext>` is
> in an explicit known-extension set (`cs md yml yaml sh shader json txt png asset meta prefab unity hlsl
> cginc py ps1 log fbx mat xml cmd bat uxml uss …`). This is form A of the merged audit's § 1 taxonomy.
>
> **Payload token** — on the citing line, a backticked span **or** a double-quoted span of ≥3 characters,
> which is **not itself an address token**. Address tokens (excluded): a path with a known extension, a
> bare `:NNN` / `:NNN-MMM`, a bare number, a 7–40-char hex sha, a bare `§N`.
>
> **Window** — **the whole citing line** (primary rule). Reported alongside: ±120 chars and ±60 chars, so
> the window's contribution to the number is visible instead of assumed.
>
> **Compliant** = at least one payload token inside the window. **Red** = none.

> #### ⚠ The grammar false-redded on the rule's own canonical example. Fourth reproduction, ten minutes in.
> My first implementation excluded any `word.word` token as "address-shaped". It therefore flagged
> § 8's own worked example — `` `AxeNudgeTool.ComposeLocalRot` (`AxeNudgeTool.cs:1008`) `` — as **RED**,
> because `.ComposeLocalRot` parses as a file extension. **The prescribed form failed the guard written to
> enforce it.** Fixed by whitelisting extensions; the sanity assertion (that exact line ⇒ 1 anchor, 0 reds)
> is now the first thing rule PA runs. Stated here rather than quietly corrected, because it is the cheapest
> possible demonstration of why "beside the number" is not a specification.

### Day-one red counts — measured at `fb2ac24`, rule PA, three scopes

| Guard scope | form-A anchors | **day-one RED** (line window) | compliance | ±120 / ±60 band |
|---|---|---|---|---|
| **Repo-wide** — every tracked `*.md` | 606 | **137** | 77% | 158 / 190 |
| **Audited doc set** — `.claude/docs/*.md` + `CLAUDE.md` | 93 | **0** | **100%** | 4 / 11 |
| **Changed-lines only** — added `*.md` lines, last 60 commits on `main` | 416 | **0 by construction** | 79% ongoing | 95 red ongoing |

### The verdict, held to § 5's own standard

**Repo-wide: candidate 3 does NOT survive. I am killing my own recommendation at that scope.**
**137** pre-existing anchors go red the moment the guard is switched on. A gate cannot be switched on
already-red — every subsequent PR is blocked by lines it did not touch — so repo-wide adoption is not one
sentence in `TESTING_BAR.md`; it is a **137-line remediation PR or a 137-entry grandfather list**, either of
which is a larger change than the convention it enforces. § 8 step 1 says *forward-only*; repo scope
contradicts that outright. **Not adoptable.**

> **One distinction I will not blur in my own favour.** § 5 killed candidate 2 on **23 *false* reds** —
> misattributions, where the guard is wrong. Rule PA's 137 are **true** reds — those citations genuinely
> carry no payload. 137 > 23 is therefore **not** a like-for-like defeat, and I am not claiming it is.
> The kill stands on the *other* half of the same standard: **a day-one red count is what decides whether a
> guard can be switched on, and 137 says no** regardless of whether each red is deserved.

**Audited doc set: 0 day-one reds on 93 anchors — 100% compliant, today.** The surface the audit actually
swept already satisfies the rule. Adoption there costs **nothing** and turns the existing habit into a
checked one. This is the strongest result in the document and it is the narrowest.

**Changed-lines-only: 0 day-one reds by construction, and a ~21% ongoing rate.** 86 of 416 form-A anchors
added to `*.md` in the last 60 commits on `main` carry no payload — **roughly one in five newly-written
anchors would go red.** That is the real, non-zero price of forward-only adoption, and no reader should
take "0 day-one reds" as "free." It is a fair price *because each red is true and is fixable by the author
who wrote the line, in the PR that wrote it* — the property candidate 2's false reds do not have.

**Adoptable scope, stated as one sentence — the sentence whose absence blocked this PR:**

> **The guard runs on CHANGED LINES ONLY** (added or modified `*.md` lines in the PR under test), **plus a
> repo-wide advisory-only report** over the audited doc set, which is at **0** today and can therefore be
> made blocking for that path set immediately. **It never runs blocking repo-wide** until the 137 are
> remediated or grandfathered, and that remediation is **out of scope for this ticket** (AC4: this document
> rewrites no citation).

**Still no guard is built here** — the ticket's OOS and the merged § 6 sequencing blocker on PR #370 both
hold unchanged. Rule PA is stated to the level where the builder invents **nothing**: anchor grammar,
payload-token grammar with its exclusion set, window, and a sanity assertion that must pass before any
count is believed. That was the gap Item 4 of the review named, and it is closed by specification, not by
code. If a future builder finds a rule PA clause they had to invent, that clause is a defect in this
section and should be filed against it.

---

## 9. Bounded convergence claim

**Bars tested here:**
- The A–D population at `0f14b4f` — fourth-observer reproduction, exact, class boundaries stated (§ 2).
- Glued `path:NNN@sha` occurrences — **0**, reproduces (§ 2).
- The population the `team/**` half governs — **3 sites / 2 distinct targets / 1 citing file**, rule T1
  (§ 3), with both live targets **opened this pass** rather than inherited, and the dead one **resolved**
  against all 116 commits that touch `team/STATE.md` (max length ever = 481 lines ⇒ born dead).
- Audited-vs-unswept citing surface — **1 : 4.8**, one counter on both sides, rule U1 (§ 3b).
- Quoted-§ resolution — **6 of 40 fail (15%)**, rule S1, plus **two** disclosed rule-sensitivity axes:
  heading-shape **4×** and string-normalisation **1.9×** (15% vs 28%) (§ 4).
- **Form-A payload compliance and the guard's day-one red count — rule PA, three scopes** (§ 8b):
  repo-wide **137 red / 606**, audited doc set **0 / 93**, changed-lines ongoing **86 / 416**; ±120/±60
  window band reported for each.
- Ordinal-§ payload presence — **368 of 422 carry none**, rule O1 (§ 4b).
- Form classification of the 13 named drifts — **12 A / 1 B**, rule F1 (§ 5).

**Bars NOT tested — named so no reader infers coverage:**
- **I did not re-derive hold-vs-drift for a single anchor.** I take `≥44` / `≤113` from the merged artifact.
  Per its § 8c that is a human read; re-doing it is a second full audit, not a check.
- **The ~31 unnamed drifted anchors are unclassified by form.** § 5's 12:1 comes from a non-random 13, and
  I have stated the direction of the likely bias rather than assuming it away.
- **Form G is counted on ONE line** (5 references). I did not sweep the repo for it. Its true population is
  unknown and could be large.
- **The `§N` ordinal population is measured for payload presence, not for correctness.** I did not open
  368 targets to see how many ordinals currently point at the wrong section. That is the natural successor
  measurement and it would size the § half of this problem properly for the first time.
- **`.claude/agents/*.md` and `team/**` were never swept for hold-vs-drift.** Rule PA (§ 8b) sweeps them for
  **payload presence** repo-wide, which is a different question: it says whether a citation is *checkable*,
  never whether it is *correct*. The merged § 8e gap is unclosed.
- **Rule PA measures compliance, not correctness.** A payload token can be present and wrong. The 137 / 0 /
  86 counts are the guard's **red** counts, not a drift count — no anchor's target was opened for them.
- **Rule PA's anchor regex is sweeper-dependent** in exactly the way merged § 8d warns: it finds **93** form-A
  anchors in the doc set at `fb2ac24` where the audit's § 1 taxonomy finds **71** at `0f14b4f` (different
  pin, looser path grammar). The **ratios and the red counts** are internally rule-consistent; the
  **absolutes are not comparable across the two sweepers**, and I do not compare them.
- **The changed-lines figure is a 60-commit sample of `main`, not a per-PR distribution.** 86/416 is the
  pooled rate; I did not compute variance across PRs, so "~1 in 5" is a pooled average and a single
  citation-heavy PR could sit far off it.
- **I did not build the guard, so its false-red rate on real PRs is unmeasured.** The one false-red I *do*
  have is the grammar's own (§ 8b) — found by asserting against the rule's canonical example, which is the
  minimum bar, not evidence of a clean grammar.
- **The 945-vs-195 absolutes are sweeper-dependent.** Only the ratio is rule-consistent. Per merged § 8d, a
  path/anchor denominator is a property of the sweeper, not of the repo.
- **Nothing here is mechanically enforced.** No guard was built; PR #370 still holds the file it must live in.

**Falsifiable prediction 1 — the convention as written.** If the convention is adopted **as written** and a
re-audit is run at a later pin, I predict the drifted-anchor count will **not** fall by more than ~5%,
because 97% of measured drift is in `Assets/**` and `.github/**` where the rule keeps line numbers
unchanged — and I predict at least one **new** § failure will appear, because § cites already fail at
**15%** (§ 4) and the rule adds cites without adding a check. If a re-audit shows a materially larger drop
than that, this analysis is wrong and the file-type split was doing work I could not see.

**Falsifiable prediction 2 — the guard, and this one grades ME.** If rule PA is built at **changed-lines
scope** and run over the next 20 merged PRs, I predict its red rate lands in **10–30%** of new form-A
anchors (pooled 21% here) and that **the audited doc set stays at 0** blocking reds. If the observed rate
lands materially **above 30%**, the forward-only scoping is not the cheap adoption I have claimed and § 8b's
verdict should be re-opened, not re-argued. If it lands materially **below 10%**, my 86/416 sample was
unrepresentative and the case for the rule is *weaker* than stated, not stronger — a rule that almost never
fires is not earning its sentence in `TESTING_BAR.md`.
