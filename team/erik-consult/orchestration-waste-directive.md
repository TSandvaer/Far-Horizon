# Orchestration Waste Directive — binding, Sponsor-commissioned

**Author:** independent advisor (Sponsor-mandated, 2026-08-01). Every other agent stopped.
**Method note:** all counts below were measured fresh this session; each cites its command or file:line. Working tree is `orch/coordination`; all repo-state claims cite `origin/main` explicitly where it matters. Anything not directly verifiable is labelled `UNVERIFIED`.

---

## VERDICT

**The team has shipped zero gameplay in ten days while running itself to a total stop.**

- Last `feat(...)` commit on `origin/main`: **2026-07-22** (`0dc4844`, wild boar, #332). Measured: `git log origin/main --pretty="%h %ad %s" | grep " feat("`.
- **75 commits since** (`git rev-list --count 0dc4844..origin/main`): **46 docs, 11 chore, 9 fix, 8 test, 1 spike — 0 feat**. Of the 9 "fix", **5 are `fix(docs)`**; only 4 touch the game (all anim). Measured: `git log 0dc4844..origin/main --pretty="%s" | sed … | sort | uniq -c`.
- Overnight 2026-07-31→08-01: **23 merges** (`git log origin/main --since="2026-07-31T22:00:00Z" --oneline` — the orchestrator's "22" was off by one), of which 19 docs/citation-repair, 3 meta-test, 1 spike, **0 gameplay**.
- The away loop redispatched the **full 5-agent team after each of four rate-limit kills** (`.claude/away-queue.md:3` "THIRD RATE-LIMIT CLEARED, FULL TEAM BACK OUT", `:228` "FOURTH rate limit … five agents died", `:463` "~4-HOUR SESSION RATE-LIMIT … KILLED ALL FIVE") until it exhausted the **weekly account limit** (commit `3196ef3` "HARD STOP - account weekly limit until Aug 2"). The night's output was documentation; the price was every agent-hour available for days.

The orchestration machine is not building a game right now. It is documenting, auditing, and reviewing itself, mechanically compelled to do so by hooks and doctrine the project installed on itself. The safety gates (testing bar, capture gates, CI) are NOT the problem and must stay — see KEEP.

---

## RANKED CAUSES (and a correction to the orchestrator's ranking)

The orchestrator ranked: (a) single build slot, (b) unbounded review rounds, (c) relay errors. **That ranking is wrong.** (a) is a ceiling, not a waste generator — 20 of the 23 overnight merges were docs-only PRs that skip Unity CI entirely (`ci.yml:96-109` paths-ignore), so the build runner sat mostly idle while the burn happened. The generator is below.

### Rank 1 — The anti-idle doctrine and its enforcement machinery (the DEMAND engine)

- CLAUDE.md declares "**Idle capacity is a bug — in every mode**" and "The NON-build lane … must NEVER idle" (CLAUDE.md §Autonomous orchestration).
- This is mechanically enforced: `.claude/hooks/orchestrator-anti-idle-stop.sh` (registered at `.claude/settings.json:103`) **BLOCKS the orchestrator from ending a tick** that neither dispatched nor scanned (hook header, lines 1-45: "BLOCKS the stop and orders a scan + fill").
- With the Unity-build cap at 1 (CLAUDE.md; enforced by the absolute `unity-build` concurrency group, `ci.yml:210-212`, and the `unity-capture` pin, `ci.yml:487-489`), at most one persona can do gameplay. The hook then forces work to be found for the other four, every tick, around the clock. The only inexhaustible work supply is docs/meta — so that is what got dispatched, through four rate-limit windows, to the weekly cap.
- **Cost: the single largest item — effectively the week's entire token budget plus a multi-day full stoppage.** An idle persona costs zero tokens. The doctrine has the sign backwards: idle capacity is free; an unnecessary dispatch is the bug.
- Candour the Sponsor asked for: this doctrine was ratified after the real 2026-06-28 3-hour idle failure (hook header lines 10-13). The correction overshot. Three idle hours cost three hours of latency; the anti-idle machine cost the week.

### Rank 2 — Self-referential meta-work (the SUPPLY engine)

The demand engine needs infinite work; the project manufactures it:

- **maintain-docs Stop hook** (`.claude/settings.json:79`, `.claude/hooks/maintain-docs-stop.sh`) fires after every file-touching turn and spawns 3 proposer agents + 1 consolidator per firing (skill description) — a per-turn token tax that emits docs, which then need maintenance.
- **Docs carry file:line citation anchors that drift**, spawning an audit-and-repair industry: #396 "citation + line-anchor audit … fix 2 dead paths" (`bf33b65`) was immediately followed by #398 "repair 3 drifted anchors **that #396's report strands**" (`0fa076e`) — the audit itself created the next repair.
- **NITs recursion, verified on `origin/main` commit subjects:** #383 → #394 ("**#383 NITs** — withdraw 2 over-claims…", `d09a80a`) → #401 ("**#394 NITs** — reconcile the line-number bullet…", `67802b5`). A NITs-fix PR received its own NITs review which spawned a further NITs-fix PR. All merged.
- **Multi-round reviews on docs:** PR #406 — a docs PR ("enemy-HP pip-row re-audited against the amended bar #10") — carries 3 reviewer verdicts + 3 author-reply rounds (`gh pr view 406 --json reviews,comments`; the orchestrator's "four rounds" claim: ≥3 verified, 4th UNVERIFIED). PR #391 is a "**rev3** — re-audit vs amended Bar 10" of a docs note. These are reviews of audits of documentation of a quality bar.
- **quality-bars.md**, designed as a one-line-per-bar table ("Row shape: one-line standard"), is **538 lines for 10 bars on `origin/main`** (`git show origin/main:team/quality-bars.md | wc -l`); PR #395 (`aeeafa0`) exists solely to "put bar #10's cell on a house diet."
- **37 files in `team/erik-consult/`** (measured `ls | wc -l`), including an ~11-note overlapping cluster on trees/grass/sky/world-look (grass-research, lowpoly-grass-technique-research, low-poly-trees-research, lowpoly-trees-spec, lowpoly-trees-grass-sky-research, lowpoly-sky-research, lowpoly-stylized-sky-research, sky-clouds-research, world-look-far-vista-research, world-look-quality-research, world-prop-quality-research — filenames alone establish the overlap).
- **Tests of test harnesses:** #399 adds "scene-presence guards for … verify captures" (204-line test file guarding capture components, `5b5483d`); #392 grows an animation-clip guard file to ~970 lines (`2059ce5`: +927/−45).
- **Cost: 51 of the 75 commits since the last feature (46 docs + 5 fix(docs) = 68%)**, plus their review rounds, plus their future maintenance — this class compounds.

### Rank 3 — Orchestrator relay errors (real, bounded, third)

- Verified in writing: `.claude/away-queue.md:287-291` — the orchestrator declared Drew's swing-gate work "**genuinely gone**" from probing one directory of six worktrees, briefed a from-scratch rebuild, and was overruled by Drew from ground truth ("The work was sitting uncommitted in `Far-Horizon-drew-docs-wt` the entire time").
- Verified: PR #416's own title records "4 follow-ups deduped" — of six follow-ups the orchestrator asked to be filed, four were duplicates a persona had to disprove.
- The remaining relay errors (population-as-drift figure, stale counts): self-reported, `UNVERIFIED` individually, consistent with the verified pattern.
- **Cost: roughly one agent-cycle per incident — hours, not the week.** Correctly on the list, wrongly ranked first-tier. Note the deeper point: six prose anti-fabrication layers already existed and did not stop these. More prose will not either — see MECHANICAL.

### Rank 4 — The single build slot (a ceiling, misdiagnosed as a cause)

The cap is real and correctly motivated (A/B-confirmed capture flake under contention, `ci.yml:26-33`; EPERM history). But it caps gameplay at one lane — it does not force anyone to write docs. Feeding four idle personas was a choice made by Rank 1. **Fix the team size to the cap; do not blame the cap for the docs.** (Raising the cap is a separate, real question — the 86cafz9tg build/capture split plus runner-2 cache isolation, `ci.yml:62-73`, was built exactly to allow it — but it is NOT this directive's lever and spike #387 (`fb2ac24`) shows same-machine concurrency is still unproven.)

### Also named, because nobody else will

The **Sponsor's own mandatory-pre-read stack** is part of the tax: CLAUDE.md requires sub-agents to "Read every `.claude/docs/*.md` before starting work" — that is ~2,000 lines across 12 docs (measured `wc -l`) per dispatch, multiplied by ~13+ verified agent deaths this week (away-queue lines 228, 463, 1433) whose pre-reads were paid for and lost. The content is good; the blanket per-dispatch requirement is ceremony. See STOP #7.

---

## STOP LIST — effective immediately

1. **STOP the unattended away loop.** Do not re-enable after the Aug-2 reset without the budget gates in MECHANICAL below. This week it converted a 4-hour throttle into a weekly-cap stoppage (away-queue:3,228,463; commit `3196ef3`). Orchestration runs Sponsor-attended until further notice.
2. **STOP dispatching work to fill idle personas.** The doctrine inverts today: **idle capacity is free; an unjustified dispatch is the bug.** A persona is dispatched when a ticket needs it, never because it is idle. This supersedes CLAUDE.md's "Idle capacity is a bug" line and the never-idle non-build-lane rule (Sponsor sign-off required to edit CLAUDE.md — queued below).
3. **STOP the docs lane outright — moratorium.** No new `.claude/docs/` additions, no new `team/erik-consult/` research notes without a named consumer ticket that a developer is actually blocked on, no board-hygiene dispatches more than once per day. The 37 erik-consult notes are frozen as-is; do NOT file a consolidation ticket (that would be more meta-work).
4. **STOP citation line-anchor maintenance.** Docs cite by section heading or stable slug, never file:line. The audit→repair→NITs chain (#396→#398, #383→#394→#401) is retired as a work class. Existing drifted anchors stay drifted; they are docs, not code.
5. **STOP multi-round reviews on docs and test-only PRs.** One review round, verdict is final: APPROVE (merge) or REQUEST_CHANGES (author fixes inline, reviewer re-checks the diff, done). No NITs follow-up tickets from docs/test PRs. Cap code-PR reviews at two rounds; a third round escalates to the Sponsor with the ship-with-documented-defect option on the table (existing memory `offer-ship-with-documented-defect-escape`).
6. **STOP building unwired verify-capture components.** Measured: 38 `*VerifyCapture*.cs` files in `Assets/Scripts/Runtime/` (Glob), minus the `VerifyCaptureFraming` helper = 37 components; exactly 13 have CI wrapper gates (`ls .github/workflows/scripts/verify_*_gate.sh`) — **24 are compiled, maintained, tested (#399 guards them!), and never run in CI.** New rule: a verify capture ships CI-wired in its own PR or it does not ship. For the existing 24: when a feature PR next touches one, wire it or delete it in that PR — do not file 24 tickets.
7. **STOP the blanket read-all-12-docs pre-read.** `asset-routing.md` already exists as the routing slip; extend the same model: a dispatch brief names the 1-3 docs its task class requires (Unity code → unity6-mastery + unity-conventions; visual → +lowpoly-quality; Blender → blender-asset-pipeline; anim verbs → procedural-animation-verbs; feel → game-juice). Requires Sponsor sign-off (the mandatory designations are Sponsor-stressed) — queued below.
8. **STOP maintain-docs firing per turn.** Remove the Stop-hook trigger; the skill becomes manual (`/maintain-docs`), invoked at most once per session or after a genuine hard-won discovery. Sponsor-approval required (settings.json change) — queued below.
9. **Fix or delete the Godot-era `TEAM.md`.** `.claude/agents/TEAM.md` instructs every agent to read five docs that do not exist in this repo (combat-architecture.md, html5-export.md, orchestration-overview.md, audio-architecture.md, test-conventions.md — none present in `.claude/docs/`, verified by Glob), and names the wrong repo (`TSandvaer/RandomGame`), wrong board, wrong engine (TEAM.md:58-73). One small PR: delete the stale references, point at CLAUDE.md. This is the one docs change this directive authorizes.
10. **Archive the away-queue.** 2,172 lines / 383 KB / 46 entries (`wc -lc`, `grep -c "^## "`). Move everything but the CURRENT entry to `team/log/away-queue-archive.md`. The staging format itself is good (see KEEP).

## KEEP LIST — safety, and why each earns its cost

1. **The 6-point testing bar** (`team/TESTING_BAR.md:7-14`): paired EditMode/PlayMode tests, XML-parsed green, **shipped-build capture evidence**, Self-Test Report on UX PRs, QA verdict, soak only for subjective feel. Editor-vs-runtime divergence is a proven, recurring failure class (corrupt-build #197 v5, `ci.yml:406-417`; spike iter6). This is what lets the Sponsor never debug. **Safety — untouchable.**
2. **The CI pipeline as built** (`structure`/`build`/`capture`/`playmode` split, absolute concurrency queues, corrupt-build canary, stale-evidence hygiene): every gate in `ci.yml` traces to a documented incident (86caahtbe, 86cagr0zu, 86caammpq, the A/B capture-flake proof). It runs unattended and costs no tokens. **Safety.**
3. **The 13 wired capture gates + generic `capture_gate.sh`.** They are the mechanical form of the shipped-build rule. (The 24 unwired ones are the ceremony — STOP #6.)
4. **Diagnose-Before-Fix and Predict-Before-Soak** (`TESTING_BAR.md:22,25`): one sentence each, they target the project's most expensive measured loop (2-4 soak-overturns per guess-fix; the 8-soak jump-pullback saga). Apply them to `fix(...)` and soak-gated PRs **only** — never to docs.
5. **Peer review on code PRs + Tess QA + the Self-Test Report.** One round each, per STOP #5.
6. **The away-queue staging FORMAT** (current entry, away-queue:8-28: exact merge command + per-gate evidence table). This is how an absent Sponsor safely one-clicks a merge. Keep the format, archive the backlog.
7. **The anti-fabrication mechanics** (producer/consumer turn split, `no-unseen-clickup-ids` hook, liveness-from-probe). Cheap, mechanical, aimed at the verified Rank-3 failure. Prose layers failed; these operate at the right layer.
8. **The technique docs' content** (unity6-mastery, unity-conventions, lowpoly-quality, blender-asset-pipeline, procedural-animation-verbs, game-juice, asset-routing, art-direction): hard-won, incident-derived, genuinely reused. The moratorium (STOP #3) freezes growth; it does not delete value. Scoped reads per STOP #7.
9. **The build-stamp ritual and soak-handoff discipline** (exact exe path + expected stamp). Directly protects Sponsor time.

## TEAM SHAPE

Reality: one Unity-build ticket in flight (cap, CLAUDE.md), one open gameplay PR (#351 — the only `feat` among 10 open PRs, `gh pr list`), four game-affecting commits in ten days.

**Directive: at most THREE agents in flight, ever, in this order of entitlement:**

1. **One developer** (Devon or Drew, alternating per ticket) on the gameplay ticket in the build slot.
2. **One reviewer/QA** (Tess on dev PRs; Devon/Drew peer on Tess's) — dispatched when a PR exists, not before.
3. **At most one support slot**, only against a concrete, named need: Priya for a batched once-daily board pass; Uma only when a feature ticket needs a spec this week; Erik only with a consumer ticket a developer is blocked on.

Nobody is "cut" — personas are prompts, not salaries; the cut is in **standing dispatch**. Five-agents-out stops being the default and becomes the exception requiring a stated reason.

**On the honest bigger question:** at its current shape, the orchestration model's overhead (relay hops, liveness rituals, coordination docs, a 383 KB queue) exceeds the value of parallelism that the build cap mostly nullifies. It is worth keeping **only** at the shrunk shape, because the PR-gate machinery (testing bar + CI + independent review) genuinely protects a Sponsor who will not debug. **Review trigger: if two weeks after the Aug-2 reset the docs+chore share of merged commits still exceeds 30%, or another zero-feat week occurs, collapse to a single hands-on session + on-demand QA agent and retire the standing team.**

## SPONSOR-APPROVAL-REQUIRED CHANGES (infrastructure — not ours to make)

1. **Remove `orchestrator-anti-idle-stop.sh` from Stop hooks** (`.claude/settings.json:103`) — or invert it to fire on *unjustified dispatch* instead of idleness. This is the Rank-1 demand engine.
2. **Remove `maintain-docs-stop.sh` from Stop hooks** (`.claude/settings.json:79`); keep the skill manual-only.
3. **CLAUDE.md edits:** replace "Idle capacity is a bug — in every mode" + the never-idle non-build-lane rule with the inverted doctrine (STOP #2); replace "Read every `.claude/docs/*.md`" with the scoped-read table (STOP #7); add the ≤3-agents-in-flight cap and the 1-round-docs/2-round-code review caps.
4. **Away-mode re-enable conditions** (when the Sponsor chooses to): (a) hard per-tick dispatch budget; (b) docs-class dispatches = 0 in away mode (mechanically: a PreToolUse hook on `Agent` denying dispatch briefs whose ticket class is docs/hygiene while away — generation-time enforcement, the layer where prose demonstrably failed six times); (c) after ANY rate-limit kill: salvage, record, **halt — no redispatch until a Sponsor-attended session**.
5. **Branch protection** remains as-is pending the already-open 86cafz9tg required-checks change (`ci.yml:47-52`) — noted, not expanded here.
6. **Deliberately absent:** no new dashboards, no waste-metrics tooling, no process-KPI instruments. Solving process failure by building process artifacts is the disease this directive treats. The only metric that matters is visible in `git log`: `feat` commits per week.
