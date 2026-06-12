# Embergrave / RandomGame — Agent Team

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

- [CLAUDE.md](../../CLAUDE.md) — project brief
- [.claude/docs/combat-architecture.md](../docs/combat-architecture.md) — combat runtime + harness conventions
- [.claude/docs/html5-export.md](../docs/html5-export.md) — HTML5 quirks (HDR clamp, Polygon2D, service-worker cache)
- [.claude/docs/orchestration-overview.md](../docs/orchestration-overview.md) — orchestration conventions
- [.claude/docs/audio-architecture.md](../docs/audio-architecture.md) — audio bus + AudioDirector autoload
- [.claude/docs/test-conventions.md](../docs/test-conventions.md) — universal warning gate (Playwright + GUT)
- [team/TESTING_BAR.md](../../team/TESTING_BAR.md) — paired-tests + Self-Test Report + journey-probe gates
- [team/GIT_PROTOCOL.md](../../team/GIT_PROTOCOL.md) — PR workflow + Cross-lane integration check
- [team/orchestrator/dispatch-template.md](../../team/orchestrator/dispatch-template.md) — dispatch brief + tightened final-report contract

## Operational IDs

- **ClickUp workspace:** `90151646138`
- **ClickUp list (RandomGame board):** `901523123922`
- **ClickUp space (TSandvaer Development):** `90156932495`
- **GitHub repo:** `TSandvaer/RandomGame`
- **Engine:** Godot 4.3 stable (HTML5 = `gl_compatibility` / WebGL2; desktop = `forward_plus`/`mobile`)

## Worktree map

- Project root (orchestrator survey, READ-ONLY): `c:\Trunk\PRIVATE\RandomGame`
- Orchestrator commits via: `c:\Trunk\PRIVATE\RandomGame-orch-wt`
- Per-role: `c:\Trunk\PRIVATE\RandomGame-{priya,uma,devon,drew,tess}-wt`

## Models

All five agents are `opus` by default. Embergrave values correctness + Sponsor-soak-finding minimization over throughput. Downgrade to `sonnet` only if a specific lane proves consistently throughput-bound without quality regression.

## Forward-compat note

`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` is set in `.claude/settings.json` for forward-compat — currently inert. If Anthropic ships native nested-Agent or subagent_type matching for named personas, the persona files in this directory become harness-loadable automatically.
