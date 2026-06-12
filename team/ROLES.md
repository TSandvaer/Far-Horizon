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
1. Survival genre — a young, hopeful castaway washes ashore and survives toward the far horizon.
2. Core feel (Sponsor-locked): small character in a big alive world; PoE-style click-to-move; mouse-orbit camera + zoom. North-star: the world feels BIG and ENDLESS — a journey. The survival loop starts THIN (M-U2: one need → craft axe → chop → campfire).

**Sponsor's hands-off rules**:
- All tech and design choices belong to the team. Do not ask Sponsor for opinions.
- Sponsor only tests big deliveries and signs off. **Big delivery = an M-tier wave-completion sign-off** (e.g. M-U1 close, the first M-U2 thin-loop slice). The current target is closing **M-U1 "Fresh Unity foundation + deliberate ports"**, after which M-U2 (thin survival loop) files.
- Sponsor's soak is right-sized — Tess + automated EditMode/PlayMode own mechanical correctness; Sponsor owns subjective-feel slices (1-2 min targeted ask). **Sponsor soak = direct artifact:** any soak ask includes the exact exe path (`Build/Windows/FarHorizon.exe`) + the expected HUD build stamp (`BUILD <tag> | <UTC> | <sha>`). Don't queue "PR needs Sponsor review" — sub-agent peer-review is the gate; don't queue "Sponsor creates ticket X" — ticket creation is PL territory.
- Orchestrator (this conversation) makes any cross-role call the PL escalates.

## ClickUp board

- Workspace: `90151646138`
- Space: TSandvaer Development (`90156932495`)
- List: **Far Horizon** (`901523878268`)
- Archive (Godot-era, read-only history): list **RandomGame** (`901523123922`) — cite for heritage, never resume there.

## Naming convention (mirrors MARIAN-TUTOR / MarianLearning)

Task titles follow conventional-commit format with scope:
`feat(scope): ...`, `fix(scope): ...`, `chore(scope): ...`, `design(spec): ...`, `bug(scope): ...`, `docs(...)`, `test(...)`, `qa(...)`.

Early-week tasks may be person-prefixed: `[Priya] W1 · ...`, `[Devon] W1 · ...`.

Far Horizon tickets use the `U<N>` milestone prefix for M-U1 (e.g. `U7: test(unity): ...`); M-U2 tickets adopt the next milestone's prefix when it files.

Tags: milestone tags (`m-u1`, `m-u2`, ...); theme tags (`survival`, `crafting`, `world`, `ux`, `ci`, `engine`, `input`, `char`, ...); type tags (`bug`, `tech-debt`, `decision-needed`, `parked`, `follow-up`).

Statuses: `to do` → `in progress` (if available) → `ready for qa test` → `complete`.

Priorities: `urgent`, `high`, `normal`, `low` — used honestly.

## Priya — Project Leader responsibilities

Priya owns project coordination, ticket authorship, backlogs, retros, risk register, and institutional memory across the team. Day-to-day surface:

- **Backlog + ticket authorship.** Per-milestone / per-wave ticket pre-shape (e.g. the M-U1 ticket set U1–U10; the forthcoming M-U2 thin-loop backlog) — tickets are dispatch-ready or they don't ship. Authors should be able to pick up the ticket and start work without asking a clarifying question.
- **Sequencing + scope.** Milestone-sequencing docs (e.g. `unity-migration-rescope-2026-06-12.md` shape) — milestones, dependencies, Sponsor-input items. Amendment-block convention preserves historical record.
- **Risk register.** Top 3-5 risks per milestone; fired / held / demoted column; weekly re-score.
- **`team/DECISIONS.md` weekly batch PR (Mondays).** Priya is the SOLE role permitted to PR against `team/DECISIONS.md`. Collect `Decision draft:` lines from merged PRs; batch via `team/priya-pl/decisions-batch-pr-template.md`. Tess enforces by bouncing non-Priya PRs that diff this file.
- **Retros.** See "Retro authorship triggers" subsection below.

### Retro authorship triggers

Priya is the canonical retro author. The orchestrator dispatches Priya in background at merge-pair time when ANY of these four PR-merge classes fires:

1. **Wave/milestone-completion PR** — closes a milestone or planning wave (e.g. the PR that closes M-U1).
2. **Spike PR** — `spike(...)` conventional-commit prefix or `spike` ticket-tag (e.g. the Unity engine-eval spike).
3. **Process-incident PR** — orch-hooks / orch-docs / convention / hook / skill changes.
4. **Multi-iteration PR** — ≥3 reviewer round-trips before merge.

Routine impl PRs (game-side feat/fix, mechanical content adds, single-iteration QA-clean merges) do **NOT** trigger retros. Sponsor can always ask for an ad-hoc retro outside the trigger list.

The orchestrator detects the trigger at merge-pair time (paired with the `gh pr merge --admin` + ClickUp status flip per `clickup-flip-paired-with-merge` memory) and dispatches Priya in background for the retro. Retro reports use the structured digest format per `retrospective-reporting-convention` memory:

- **Grade** (single letter — honest, no sandbagging, no sugar-coating)
- **Patterns** (what worked / what didn't)
- **Hypothesis-verdict** (predictions vs. actuals for the period being retro'd)
- **Mitigations** (what changes going forward)
- **Decision-surface** (anything Sponsor must decide)
- **Cadence** (timing / bundling notes for the next pre-shape)

Retro reports land in `team/priya-pl/` (e.g. `pr-NNN-<short-name>-retro.md` for per-PR retros; `m-u1-retro.md` / wave-named retros for milestone/wave retros).

**Bundled meta-retro at session-save.** Three+ retro triggers in a single session warrants a bundled meta-retro at session-save, capturing cross-cutting patterns the individual retros could not see in isolation. The orchestrator surfaces this as a single paragraph in the session-save state file (not a separate dispatch).

Source-of-truth memory entry: `per-class-retro-trigger-convention` (locked 2026-05-23, first-firing on PR #327).
