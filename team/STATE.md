# Team State — Far Horizon

**This file is a RESUME HEADER, not a log.** One current section. When it is superseded, it is
**replaced**, not appended to — the previous content stays retrievable with
`git log -p team/STATE.md`. It grew to 479 lines of stacked superseded headers before
2026-08-02; do not let that happen again.

Decisions go in `team/DECISIONS.md` (append-only). Operational scrollback goes nowhere.

---

## RESUME NEXT-ACTION — 2026-08-02 (Sponsor PRESENT; auto-status OFF and staying off)

**Agents in flight: ONE — Drew on `86caxjx26` (empty-hands stone blades), branch
`drew/86caxjx26-empty-hands` off `484d43f`, dispatched 2026-08-02 ~15:4xZ. Open PRs: ZERO.**

**If this session dies right now:** check `git -C ../Far-Horizon-drew-wt log --oneline` and
`gh pr list`. No branch pushed → re-dispatch fresh (agent resume IDs die with the session).
PR open → dispatch **Devon** as reviewer, one round, `APPROVE` or `REQUEST_CHANGES` only.

**Read `CLAUDE.md` § "Orchestration doctrine" before doing anything else** — twelve Sponsor
rulings from today, several inverting prior standing rules. Short version: idle is free and an
unjustified dispatch is the bug; one dev + one reviewer + at most one support; reviews may never
create tickets; docs require a paid-for incident; agents may not create tickets except for bugs
reproduced in a built exe.

### The drought ended

`main` @ `484d43f`. **`feat` commits in the last 7 days: 1** — `01e9c03`
`feat(combat): find-in-world weapon acquisition — sword_iron in a stump, E-looted` (#351),
the first since **2026-07-22**. The kill switch (any calendar week with zero `feat` merges
retires the standing team) is satisfied; re-check with:

```
git log origin/main --since="7 days ago" --pretty=%s | grep -c "^feat"
```

### Merged today

#391, #406, #411, #412, #414, #415, #416 (backlog clear-out) · #422 doctrine rewrite ·
#423 tess agent registration · #424 CRLF+colon trap doc · #425 recovered game-juice work ·
#426 STATE · **#351 the feature** · #427 runner keep-alive. Plus #369 and #370, merged by the
Sponsor by hand — **unnecessarily**, see the correction below.

### ⛔ CORRECTION — the `.github/` label carve-out is RETIRED

Earlier revisions of this file, PR comments, and a dispatch brief all asserted that PRs touching
`.github/` cannot be label-merged. **False.** #351 touched `ci.yml` **and** a workflow script and
merged **via the `auto-merge` label** at `14:40:07Z`.

`AUTO_MERGE_PAT` (`far-horizon-auto-merge`) carries **Workflows RW** and has **no expiration
date**. The claim was inherited from a note written *after* the PAT landed and repeated without
testing; the test was one `gh pr edit --add-label`.

**Standing rule: the Sponsor approves a merge, the ORCHESTRATOR labels it. Never route a merge
to him.** Full incident: [[verify-before-answering-when-the-check-is-cheap]],
[[auto-merge-fails-on-workflow-file-prs]].

### Infrastructure fixed today

The self-hosted runner `far-horizon-local` was **dead for ~4.5 h**, starving every self-hosted
job. It presented as a repo-wide Actions dispatch outage — disproved by `docs-markup` (also
hosted) succeeding throughout.

Root cause: `FarHorizonRunnerAutostart` has an **at-logon trigger with no repetition**. It fired
`09:47:41` (result 0), the runner died after `09:56`, `NextRunTime` was empty. Nothing could ever
recover a mid-session death, and `FarHorizon-RunnerDisconnectWatchdog` covers only
*connection*-dead, not *process*-dead.

Now installed and verified (Sponsor ran the elevated installer; all Task Scheduler writes are
denied to a non-elevated user): **`FarHorizon-RunnerKeepAlive`**, 5-minute repeat, proven by a
real scheduled fire. `FarHorizon-RunnerDisconnectWatchdog` re-enabled. Tooling on `main` at
`tools/ops/`.

### ✅ RESOLVED — `main` is fully green at `619940a`

Run `30753181110`: **`structure` · `build` · `capture` · `playmode` all success.** No rerun was
needed — the #427 label-merge spawned a fresh `main` run on a newer head, which superseded the
cancelled one.

Kept because the mechanism recurs: the earlier `main` run `30752613219` was **cancelled by
capture contention**. The repo-wide `unity-capture` group permits exactly ONE capture across the
entire repo, so a merge-to-`main` and any open PR reliably take each other out — twice today.
**A cancelled `capture` is usually contention, not signal.** It self-heals via the next run, or
`gh run rerun --job <id>`.

Corollary worth knowing: a **label-merge with the PAT DOES spawn a `main` push run** (a PAT is a
user credential). The older memory claim that label-merges spawn no run holds only for the
default `GITHUB_TOKEN` actor.

### Open threads (none blocking)

- **`86cau4za2`** (castaway right-hand re-weight) stays `to do` + `deferred`. Sponsor re-confirmed
  ship-as-is against the current build 2026-08-02: *"the hands looked much better in that one"* /
  *"still had the thumbs though, but that would be acceptable"*. Re-activation trigger is a
  scheduled rig change; none exists. **Board scans skip it.**
- **Fossil worktrees** awaiting the Sponsor's word to clear: `fh-369-review`, `fh-351-merge`,
  `fh-261-fold`, and `Far-Horizon-drew-docs-wt` (holds `drew/86cau4za2-block-hands`, 5 unpushed
  commits whose content landed via #420 — verified fossil, nothing stranded).
- **`orch/coordination` is retired.** Orchestrator works on `main`. Both tips archived remotely as
  `archive/orch-coordination-2026-06-24` and `archive/orch-coordination-2026-08-02`; never merge
  either — they fossilise code `main` deliberately deleted.
- **Destination past #351: Sponsor picked `86caxjx26`** (2026-08-02, popup) — the empty-hands
  defect, remaining scope `dagger_stone` + `sword_stone` + the AC3 no-orphan guard (#351 closed
  the four iron blades). Nothing is queued behind it. `team/survival-roadmap.md` remains stale as
  a plan (it stops at M-U2 while combat, enemies and weapons have shipped) — **ask the Sponsor
  again before dispatching whatever follows.**
- ⚠ **`team/orchestrator/dispatch-template.md` contradicts itself.** Its § "Read BEFORE any code"
  table (the new scoped routing) is directly undercut by the "Lesson reminder (mandatory in every
  dispatch)" block below it, which still orders agents to *"read EVERY `.claude/docs/*.md`"* —
  the exact ~1,855-line-per-dispatch cost the 2026-08-02 doctrine retired. **Do not paste that
  block.** Needs the Sponsor's yes before anyone edits it.
