# Far Horizon — Agent Team

Five named agents handle the Embergrave game build. The Sponsor (Thomas) talks to the **orchestrator** (the Claude Code session). The orchestrator fans out directly to Priya, Uma, Devon, Drew, and Tess via the `Agent` tool. **Nested-Agent spawning is unsupported** in this Claude Code build (see *Topology* below) — top-level fan-out is the permanent model.

## Roster

| Agent | Role | Workspace folder | Owns |
|---|---|---|---|
| [Priya](priya.md) | Project Leader | `team/priya-pl/` | Backlog, ClickUp board, scope, schedule, retros, M3 design seeds, process docs |
| [Uma](uma.md) | UX / Visual / Audio Direction | `team/uma-ux/` | Player journey, level UX, palettes, audio direction, boss intros, copy |
| [Devon](devon.md) | Game Developer #1 (engine + harness lead) | `team/devon-dev/` | Engine/runtime, core systems (combat, leveling, save), build/CI, harness infra |
| [Drew](drew.md) | Game Developer #2 (content + level chunks) | `team/drew-dev/` | Content systems (mobs, loot, rooms), level chunks, boss state machines, Playwright fixtures |
| [Tess](tess.md) | QA / Test design | `team/tess-qa/` | Test plans, GUT + Playwright authoring, acceptance plans, sign-off readiness |
| [Erik](erik.md) | Engine / Graphics Evaluation (consultant) | `team/erik-consult/` | Engine-capability research, rendering/export constraints, asset-pipeline fit, engine-decision briefs |

## Communication topology

```
              Thomas (Sponsor)
                    │
                    ▼
              Orchestrator  ◄── single fan-out / fan-in point
              ┌──┬──┬──┬──┬──┐
              ▼  ▼  ▼  ▼  ▼  ▼
            Priya Uma Devon Drew Tess
                     │     │
                     │     ↕ (peer PR review)
                     ▼     │
              (Devon ↔ Drew for cross-lane review;
               Drew/Devon for Tess-authored PR peer review)
```

- **Sponsor talks to the orchestrator**, not to any single agent. Per `sponsor-decision-delegation`: Sponsor only signs off big deliveries (milestone RCs); orchestrator makes recommended cross-role calls.
- **Devon ↔ Drew peer-review** for both engine-side and game-side PRs as appropriate.
- **Drew or Devon peer-reviews Tess-authored PRs** per `tess-cant-self-qa-peer-review` — pick by surface: game-side → Drew; harness/inventory/engine → Devon.
- **Tess QAs UX-visible PRs from Devon/Drew/Uma** before merge per the testing bar.
- **Priya does NOT spawn peers** — she authors process docs, retros, backlogs, M3 design seeds. The orchestrator dispatches based on her recommendations.
- **Erik is consulted, not assigned tickets.** When Priya or the Sponsor wants engine/graphics-capability evidence, the orchestrator dispatches Erik with a self-contained brief; he returns evidence-graded research notes under `team/erik-consult/`. He never moves cards or owns specs. Model: `sonnet` (research/synthesis lane — the opus precision premium is less load-bearing for consults than for impl/review).

**Why this topology and not Priya-as-fan-out:** Anthropic's Claude Code runtime filters the `Agent` tool out of the toolset exposed to sub-agents (hard-coded in `AgentTool/prompt.ts`), so a spawned Priya cannot itself spawn Devon/Drew/etc. The experimental flag `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` is **confirmed inert in this Claude Code build** (per MARIAN-TUTOR's probes 2026-04-24 → 2026-04-25). Top-level fan-out is the permanent model. Re-probe if Anthropic ships native nested-Agent.

## Task lifecycle

1. **Sponsor → Orchestrator:** feature request / soak feedback / direction.
2. **Orchestrator → Priya:** "decompose this" or "add to backlog." Priya drafts ClickUp task(s) with acceptance criteria, suggests assignees + priority. Returns plan.
3. **Orchestrator → Uma** (if UX/visual/audio needed): writes a spec under `team/uma-ux/`. Returns spec.
4. **Orchestrator → Devon or Drew:** branches `{role}/<id>-<slug>`, implements, opens PR. Returns PR # + tight final report (per `tightened-final-report-contract`).
5. **Orchestrator → the other developer:** peer-reviews via `gh pr review`. Approves or blocks.
6. **Orchestrator → Tess:** QA per testing bar. Returns APPROVE / REQUEST CHANGES.
7. **Merge** (only after Tess approval; orchestrator triggers via `gh pr merge --admin --squash --delete-branch`).
8. **ClickUp status flip** (paired with merge in same tool round per `clickup-status-as-hard-gate`).

## Shared references

Every agent reads these before a first substantive task:

> ⚠ **Corrected 2026-08-02.** This file previously listed five `.claude/docs/` links that do
> not exist in this repo (`combat-architecture.md`, `html5-export.md`,
> `orchestration-overview.md`, `audio-architecture.md`, `test-conventions.md`) and pointed at
> the wrong repo, board, engine and worktrees — all Godot/RandomGame-era carryover. Values
> below are verified against `git remote -v`, `git worktree list` and `CLAUDE.md`.

- [CLAUDE.md](../../CLAUDE.md) — project brief + **§ "Orchestration doctrine"** (read this first)
- [team/TESTING_BAR.md](../../team/TESTING_BAR.md) — the 6-point bar + what it does NOT ask for
- [team/GIT_PROTOCOL.md](../../team/GIT_PROTOCOL.md) — PR workflow + Cross-lane integration check
- [team/orchestrator/dispatch-template.md](../../team/orchestrator/dispatch-template.md) — dispatch brief, the per-task-class doc routing table, the two-verdict review format

**Do NOT read every `.claude/docs/*.md`.** That rule was retired 2026-08-02; your dispatch brief
names the 1–3 docs your task class needs. The routing table is in the dispatch template.

## Operational IDs

- **ClickUp list (Far Horizon board):** `901523878268`
- **ClickUp space (TSandvaer Development):** `90156932495`
- **GitHub repo:** `TSandvaer/Far-Horizon`
- **Engine:** Unity 6 `6000.4.11f1` / URP, desktop-first Windows (`Build/Windows/FarHorizon.exe`)

## Worktree map

- Project root (orchestrator survey, READ-ONLY): `c:\Trunk\PRIVATE\Far-Horizon`
- Per-role: `c:\Trunk\PRIVATE\Far-Horizon-{priya,uma,devon,drew,tess,erik}-wt`
- Several task-specific worktrees also exist (e.g. `Far-Horizon-drew-swings-wt`). **Use the one
  your brief names** — never assume the plain `-wt` path. Confirm with `git worktree list`.
- **One agent per worktree at a time.** Two concurrent dispatches against the same worktree race
  on `git checkout` / `gh pr checkout` and can corrupt in-progress work.

## Models

All agents are `opus` by default. Far Horizon values correctness + Sponsor-soak-finding minimization over throughput. Downgrade to `sonnet` only if a specific lane proves consistently throughput-bound without quality regression.

**⛔ Roster ≠ headcount (Sponsor decision 2026-08-02).** Six personas are *defined*; at most
**three** may be in flight, and typically fewer: one developer, one reviewer (only once a PR
exists), and at most one support persona against a named concrete need. Personas are prompts,
not salaries — an idle persona costs nothing, an unjustified dispatch costs the week. See
`CLAUDE.md` § "Orchestration doctrine".

## Forward-compat note

`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` is set in `.claude/settings.json` for forward-compat — currently inert. If Anthropic ships native nested-Agent or subagent_type matching for named personas, the persona files in this directory become harness-loadable automatically.
