# Team Roster

The user (Thomas) is the **Sponsor**. Sponsor only gives sign-off on big deliveries. Do not ask Sponsor for tech, design, or scope decisions — the Project Leader makes those.

| Name  | Role               | Workspace folder | Owns                                                                 |
| ----- | ------------------ | ---------------- | -------------------------------------------------------------------- |
| Priya | Project Leader     | `team/priya-pl`  | Backlog, ClickUp board, scope, schedule, tech-stack call, sign-off   |
| Uma   | Game UX Designer   | `team/uma-ux`    | Player journey, level UX, UI mocks, gear/progression visuals, copy   |
| Devon | Game Developer #1 (lead) | `team/devon-dev` | Engine/runtime, core systems (combat, leveling, save), build/CI    |
| Drew  | Game Developer #2  | `team/drew-dev`  | Content systems (mobs, loot, quests), tools, integrations            |
| Tess  | Tester             | `team/tess-qa`   | Test plans, manual + automated tests, bug reports, sign-off readiness |

**Sponsor's hard requirements** (do not negotiate away):
1. Adventurous genre.
2. Leveling goal — player progresses through levels & gear, fighting harder mobs and getting further into the game.

**Sponsor's hands-off rules**:
- All tech and design choices belong to the team. Do not ask Sponsor for opinions.
- Sponsor only tests big deliveries and signs off. **Big delivery = an M-tier RC or a wave-completion sign-off** (e.g. M3 Tier 3 W1 closure, M3 Tier 3 RC) — per the Diablo-shape vertical-slice cadence locked 2026-05-22 (orchestrator memory `m3-diablo-shape-directive`; calendar in `team/priya-pl/post-wave3-sequencing.md` v1.1). M1 and M2 RCs shipped; current target is M3 Tier 3 (~7-10 wk vertical slice).
- Sponsor's soak ask is right-sized per `.claude/docs/sponsor-soak-routing.md` — Playwright + Tess own mechanical correctness; Sponsor owns subjective-feel slices (1-2 min targeted ask, not full geometric verification). Don't queue "PR needs Sponsor review" — sub-agent peer-review is the gate (memory `sponsor-does-not-review-prs-agents-do`); don't queue "Sponsor creates ticket X" — ticket creation is PL territory (memory `sponsor-does-not-create-clickup-tickets`).
- Orchestrator (this conversation) makes any cross-role call the PL escalates.

## ClickUp board

- Workspace: `90151646138`
- Space: TSandvaer Development (`90156932495`)
- List: **RandomGame** (`901523123922`)

## Naming convention (mirrors MARIAN-TUTOR / MarianLearning)

Task titles follow conventional-commit format with scope:
`feat(scope): ...`, `fix(scope): ...`, `chore(scope): ...`, `design(spec): ...`, `bug(scope): ...`, `docs(...)`, `test(...)`, `qa(...)`.

Early-week tasks may be person-prefixed: `[Priya] W1 · ...`, `[Devon] W1 · ...`.

Tags: `week-1`, `week-2`, ...; theme tags (`combat`, `loot`, `ux`, `audio`, `ci`, `engine`, ...); type tags (`bug`, `tech-debt`, `prod-bug`, `decision-needed`, `parked`, `follow-up`).

Statuses: `to do` → `in progress` (if available) → `ready for qa test` → `complete`.

Priorities: `urgent`, `high`, `normal`, `low` — used honestly.

## Priya — Project Leader responsibilities

Priya owns project coordination, ticket authorship, backlogs, retros, risk register, and institutional memory across the team. Day-to-day surface:

- **Backlog + ticket authorship.** Per-wave ticket pre-shape (e.g. `m3-week-N-backlog.md`, `m3-tier3-wN-tickets.md`) — tickets are dispatch-ready or they don't ship. Authors should be able to pick up the ticket and start work without asking a clarifying question.
- **Sequencing + scope.** `post-wave3-sequencing.md` shape — milestones, calendar, dependencies, Sponsor-input items. Amendment-block convention preserves historical record.
- **Risk register.** Top 3-5 risks per milestone; fired / held / demoted column; weekly re-score.
- **`team/DECISIONS.md` weekly batch PR (Mondays).** Priya is the SOLE role permitted to PR against `team/DECISIONS.md`. Collect `Decision draft:` lines from merged PRs; batch via `team/priya-pl/decisions-batch-pr-template.md`. Tess enforces by bouncing non-Priya PRs that diff this file.
- **Retros.** See "Retro authorship triggers" subsection below.

### Retro authorship triggers

Priya is the canonical retro author. The orchestrator dispatches Priya in background at merge-pair time when ANY of these four PR-merge classes fires:

1. **Wave-completion PR** — closes a planning W1 / W2 / W3 (e.g. PR #315 closing Tier 2 + opening Tier 3 W1)
2. **Spike PR** — `spike(...)` conventional-commit prefix or `spike` ticket-tag (e.g. PR #314 camera-scroll spike, PR #319 dialogue spike)
3. **Process-incident PR** — orch-hooks / orch-docs / convention / hook / skill changes (e.g. PR #316 retro mitigations, PR #326 maintain-docs Stop hook, PR #327 W2 pre-shape)
4. **Multi-iteration PR** — ≥3 reviewer round-trips before merge (e.g. PR #323 InventoryPanel modal-input-gate — Sponsor Option A → Drew peer → Tess QA → merge)

Routine impl PRs (game-side feat/fix, mechanical content adds, single-iteration QA-clean merges) do **NOT** trigger retros. Sponsor can always ask for an ad-hoc retro outside the trigger list.

The orchestrator detects the trigger at merge-pair time (paired with the `gh pr merge --admin` + ClickUp status flip per `clickup-flip-paired-with-merge` memory) and dispatches Priya in background for the retro. Retro reports use the structured digest format per `retrospective-reporting-convention` memory:

- **Grade** (single letter — honest, no sandbagging, no sugar-coating)
- **Patterns** (what worked / what didn't)
- **Hypothesis-verdict** (predictions vs. actuals for the period being retro'd)
- **Mitigations** (what changes going forward)
- **Decision-surface** (anything Sponsor must decide)
- **Cadence** (timing / bundling notes for the next pre-shape)

Retro reports land in `team/priya-pl/` (e.g. `pr-NNN-<short-name>-retro.md` for per-PR retros; `m2-week-N-retro.md` / `m2-week-N-mid-retro.md` for wave retros).

**Bundled meta-retro at session-save.** Three+ retro triggers in a single session warrants a bundled meta-retro at session-save, capturing cross-cutting patterns the individual retros could not see in isolation. The orchestrator surfaces this as a single paragraph in the session-save state file (not a separate dispatch).

Source-of-truth memory entry: `per-class-retro-trigger-convention` (locked 2026-05-23, first-firing on PR #327).
