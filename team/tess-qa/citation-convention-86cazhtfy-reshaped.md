# `86cazhtfy` reshaped onto the measurement — citation convention

**Measured at:** `origin/main` @ `fb2ac24` for everything new here; `0f14b4f` where I reproduce the
`86caz4yhj` audit (that audit's own pin). Every figure below states the rule that produced it, per the
standing requirement in the merged audit § 4 (*"a count with no stated scoping rule is not a measurement"*).

**Input:** `team/drew-dev/citation-anchor-audit-86caz4yhj.md`, merged `bf33b65` (PR #396), measured at `0f14b4f`.
I read the merged artifact, reproduced what I could, and measured five things it did not.

**Status:** proposal reshaped, still a proposal. Nothing here is adopted; no guard is built (the ticket's
own OOS, and the audit's § 6 sequencing blocker on PR #370, both still hold).

---

## 0. Verdict

**Do NOT adopt the convention as written.** Its measured reach is **1 of ≥44** drifted anchors (2%), its
`team/**` half governs **1 citation site in the entire repo** and that site is not broken, its `Assets/**`
half would introduce a syntax with **0 in-repo occurrences**, and its remedy — the § anchor — has a
**measured 10% failure rate** on the population that already uses it.

**Adopt instead the rule the ticket already wrote down as its own AC3 edge case:** *every citation to a
location in this repo carries a checkable payload — an exact string greppable in the target — beside
whatever address form it uses.* That rule is form-agnostic, it is already the practiced house form in
three places in this repo, and it is the only candidate that moves the audit's § 6 Tier-3 number
(*"catches 0 of ≥44"*) off zero.

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

**Result: 1 site in the entire repository.**

`team/erik-consult/enemy-hit-feedback-hitflash-particle-flinch.md` cites
`` `origin/main:team/uma-ux/combat-cluster-design-brief.md:248` @ `fee2604` `` — in the spaced sha form
that the merged audit § 4 reports as **holding** (both instances of that form were opened and hold).

> **The `team/**` half of this convention is a rule for one citation, and that citation is not broken.
> Its measured prevention count is 0.**

The `.claude/docs/**` half prevents **1** — the merged § 5 figure, which reproduces.

**Combined: 1 of ≥44 = 2.3%.**

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
| OUT-OF-SCOPE (target is a PR body / PR comment / memory-entry name) | 3 | PR #362 body; PR comment `5129792893`; auto-memory `html5-visual-gated-author-self-soak` |
| **FAILS-PATH** | **3** | `.claude/agents/drew.md` and `.claude/agents/uma.md` → `combat-architecture.md` § "Harness coverage gap"; `.claude/agents/devon.md` → same file § "Every loot-table item must be in STARTER_ITEM_PATHS." — **`combat-architecture.md` has 0 tracked files here**, it is a Godot-era doc |
| **FAILS-SECTION** | **4** | `.claude/agents/tess.md` **and** `team/orchestrator/dispatch-template.md` → `team/TESTING_BAR.md` § "Milestone-gate journey probe" (2 sites); `team/orchestrator/dispatch-template.md` → `team/TESTING_BAR.md` § "Visual primitives — observable delta required"; `team/devon-dev/snowcap-facet-plan.md` → `unity-conventions.md` §"procedural committed assets go stale" |
| RESOLVES | 33 | |
| **In-scope population** | **40** | **FAILS-SECTION = 4 = 10%** |

One of those four is in **my own persona file**. I am not exempt from the class I am auditing.

> ### ⚠ Rule-sensitivity, disclosed
> A heading-shape variant of S1 — require the string to sit on a `#` heading line — flags **17**, a **4×
> spread** on the same population, same pin, same files. The reason: **most "§" in this repo names a bold
> bullet, not a markdown heading.** This is the merged § 8d finding (`128` vs `101` on one sentence of path
> rule) reproducing in a new place. **Consequence for whoever builds a guard: check the payload as a
> STRING, never as a heading.**

### The asymmetry that actually decides this ticket

All four FAILS-SECTION were found by **one exact-string grep**. Per the merged § 6 Tier 3, **0 of ≥44**
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
(merged § 6: 23/82 = 28% attribution failure). **Negative expected value as a first move.**

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

| Candidate | **Prevents** (of ≥44) | **Detects** (of ≥44) | In-repo track record | Verdict |
|---|---|---|---|---|
| **1. This ticket as written** — § for `team/**` + `.claude/docs/**`; `file:line @ sha` for `Assets/**` | **1** (2%). `team/**` half: **0** — it governs 1 site (§ 3) and that site holds | **0** | § half: exists, but **10% measured failure** (§ 4) and **87% of its dominant form carries no payload** (§ 4b). `Assets/**` half: `path:NNN@sha` has **0 occurrences** — new practice, no track record | **Do not adopt as written** |
| **2. Detection change — widen the guard to form B** | **0** | ≥1 of the 13 named, at a cost of **23 false reds on day one** (28%) | — | **Do not do first.** Not convergent either — form G (§ 6). Revisit only after the ~31 unclassified drifts are classified by form |
| **3. Checkable payload beside every address** (the ticket's own AC3, generalised) | **0 — it does not stop drift, and I will not claim it does** | **≥12 of the 13 named**, and in principle all ≥44 — the only candidate that moves § 6 Tier 3 off zero | **Yes, three places:** the two 100%-holding blocks in `unity-conventions.md` (18/18 and 10/10, merged § 4); Uma's `§N (payload)` habit at 54 sites (§ 4b); and `team/TESTING_BAR.md` § "Doc-staleness greps" already sanctions the form *for that file only* | **Adopt** |
| **4. Do nothing** | 0 | 0 | — | ≥50 findings live on `main` today (merged § 8e) |

**Candidate 3 prevents nothing.** It is a detection remedy and must be sold as one. The reason to prefer it
is not that it stops an author mistyping a number — nothing does — but that it is the only option under
which a stale citation *announces itself*.

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
  what the two 100%-holding blocks already do.
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
1. Land the rule as one bullet in `team/TESTING_BAR.md`, worded form-agnostically. Forward-only.
2. Guard **after PR #370 merges** (merged § 6 sequencing blocker): **form A only**, payload-within-±N-lines,
   with forms B / C / E / F / **G** on a **documented residual list** so a green never implies they were
   checked.
3. **Do not widen to form B** until the ~31 unclassified drifted anchors are classified by form (§ 5).
   That measurement decides it. Nothing else does.

---

## 9. Bounded convergence claim

**Bars tested here:**
- The A–D population at `0f14b4f` — fourth-observer reproduction, exact, class boundaries stated (§ 2).
- Glued `path:NNN@sha` occurrences — **0**, reproduces (§ 2).
- The population the `team/**` half governs — **1 site**, rule T1 (§ 3).
- Audited-vs-unswept citing surface — **1 : 4.8**, one counter on both sides, rule U1 (§ 3b).
- Quoted-§ resolution — **4 of 40 fail**, rule S1, plus its 4× rule-sensitivity (§ 4).
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
- **`.claude/agents/*.md` and `team/**` were not swept as citing files for line anchors** — only the § cites
  in them (§ 4). The merged § 8e names the same gap.
- **The 945-vs-195 absolutes are sweeper-dependent.** Only the ratio is rule-consistent. Per merged § 8d, a
  path/anchor denominator is a property of the sweeper, not of the repo.
- **Nothing here is mechanically enforced.** No guard was built; PR #370 still holds the file it must live in.

**Falsifiable prediction, for grading against whatever is adopted.** If the convention is adopted **as
written** and a re-audit is run at a later pin, I predict the drifted-anchor count will **not** fall by more
than ~5%, because 97% of measured drift is in `Assets/**` and `.github/**` where the rule keeps line
numbers unchanged — and I predict at least one **new** § failure will appear, because § cites already fail
at 10% and the rule adds cites without adding a check. If a re-audit shows a materially larger drop than
that, this analysis is wrong and the file-type split was doing work I could not see.
