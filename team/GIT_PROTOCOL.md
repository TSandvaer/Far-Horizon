# Git Protocol

Remote: `https://github.com/TSandvaer/Far-Horizon.git`. Default branch: `main`. **`main` is protected — direct push is blocked by harness policy. Every change lands via PR + `gh pr merge`.** (Bootstrap exception: U1/U2 root scaffolding landed direct, recorded on their tickets.)

## Per-task workflow (mandatory for every role)

**On task start — mandatory ClickUp visibility flip:**

Before doing any work on a task, flip its ClickUp status from `to do` to **`in progress`** with `mcp__clickup__update_task`. This gives the Sponsor live visibility into what's currently in flight. If MCP is disconnected, queue the flip in `team/log/clickup-pending.md` per `team/CLICKUP_FALLBACK.md` and proceed; orchestrator flushes on next reconnect. Skip this for trivial run-state PRs (e.g. your own `chore(state): <role> idle` PR — that's not a backlog task).

If you finish the task in the same run (typical for design docs, doc tasks, small fixes), the status will progress through `in progress → ready for qa test` (feature) or `in progress → complete` (docs/chore exempt) by the end of your run. The `in progress` window is meaningful for the Sponsor even if it's brief — it's the live signal of "what's being worked on right now."

**On task completion (push + PR + merge):**

When you finish a task (or a coherent chunk of work):

1. `git status --short` to confirm what's staged.
2. `git pull --rebase origin main` so you're rebased on the latest before branching.
3. Stage only files relevant to your task (`git add <files>` — not `git add .` or `git add -A`).
4. Commit with a conventional-commit title matching the ClickUp task shape:
   - `feat(scope): ...` for new features
   - `fix(scope): ...` for bug fixes
   - `chore(scope): ...` for tooling, repo housekeeping
   - `design(spec): ...` for design docs landing
   - `docs(scope): ...` for written documentation
   - `test(scope): ...` for tests
   Body of the commit message — one short paragraph describing **why**, not **what**. Reference the ClickUp task ID if applicable (`Closes #86ca...`). Always include this trailer:
   ```
   Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
   ```
5. **Push to a feature branch**, never main: `git push origin HEAD:<role>/<task-scope-kebab>`. Examples:
   - `git push origin HEAD:devon/click-to-move-port`
   - `git push origin HEAD:drew/axe-craft-recipe`
   - `git push origin HEAD:tess/m-u1-testing-bar`
   - `git push origin HEAD:uma/thin-loop-ux`
   - `git push origin HEAD:priya/m-u2-backlog`
   - `git push origin HEAD:orchestrator/<scope>` (orchestrator only)
6. **Open a PR** with `gh pr create --base main --head <branch> --title "<commit title>" --body "<short why + ClickUp link>"`.
7. **Merge via the GitHub API** — the orchestrator's actual merge ritual (used on PRs #3–#16), in order:
   1. **Detach the author's worktree first.** Before merging, the worktree that pushed the branch must NOT have that branch checked out (`gh pr merge --delete-branch` fails to delete a branch that's checked out somewhere). Detach with `git -C <author-worktree> checkout --detach` (or reset it to `origin/main`). The merging identity is the orchestrator (or Priya for `chore(triage)` / `docs(team)`) — never the author (§ "Tess sign-off via PR").
   2. **Merge:** `gh pr merge <PR#> -R TSandvaer/Far-Horizon --admin --squash --delete-branch`. The `-R TSandvaer/Far-Horizon` repo flag pins the target repo so the call doesn't depend on the cwd's remote. The `--admin` flag is required because `main` is protected; only the orchestrator-class identity has it. Squash-merge is the default — keeps `main` linear.
   3. **Pull:** `git fetch origin && git reset --hard origin/main` to sync your local main back to the squashed tip.
   4. **Verify no divergence:** `git log origin/main..main` must be **empty** after the pull. A non-empty result means a local commit didn't make it onto the squashed tip — stop and reconcile before continuing (memory `gh-merge-local-divergence-guard`).

`git push origin main` is **denied** by the harness — don't try; you'll waste a tool call. Force-pushes (`--force`, `--force-with-lease`) are also denied.

## Tess sign-off via PR

The Tess-only `ready for qa test → complete` gate (per `TESTING_BAR.md`) maps cleanly to PR review:

- Devs open PRs and label them `ready for qa test` (`gh pr edit <PR#> --add-label "ready-for-qa"` if the label exists; otherwise the ClickUp status flip is the signal).
- Devs do **NOT** merge their own feature PRs. They push, open the PR, and stop.
- **Tess** reviews via `gh pr diff <PR#>` plus `gh pr checkout <PR#>` for local exploratory testing, runs the relevant manual cases from her current test plan (`team/tess-qa/`), then either:
  - **Approves and merges**: `gh pr review <PR#> --approve --body "<sign-off note>"` then `gh pr merge <PR#> --squash --delete-branch --admin`. Then flips ClickUp to `complete`.
  - **Bounces**: `gh pr review <PR#> --request-changes --body "<bug list with severity>"`. Files `bug(scope):` ClickUp tasks per `team/tess-qa/bug-template.md` and leaves the PR open until devs push fixes.

Pure docs / `chore(repo|ci|build)` / `design(spec)` PRs — Tess sign-off is **not** required, **but the merging identity must still be the orchestrator (or Priya for `chore(triage)` / `docs(team)`). Devs do NOT self-merge their own PRs in any category.** The exemption is from Tess sign-off, not a self-merge license. (Precipitating incident in the Godot-era archive: `c:/Trunk/PRIVATE/RandomGame/team/log/process-incidents.md` 2026-05-02 entry.)

## ClickUp lifecycle as hard gate

Every ticket lifecycle event has a paired ClickUp status move that fires in the **same tool round** as the action — not "remember to do later." This is a hard gate, not advisory bookkeeping. The Sponsor relies on the ClickUp board as live ground truth; a lying board destroys that trust.

| Event | ClickUp move | Who fires it |
|---|---|---|
| Orchestrator dispatches an agent on a ticket | `to do` → `in progress` | Orchestrator (or agent at run-start) |
| Agent opens PR (feat/fix) | `in progress` → `ready for qa test` | Agent in PR-open flow |
| Agent opens PR (chore/docs/design exempt) | `in progress` → `ready for qa test` (for orchestrator visibility) | Agent in PR-open flow |
| Tess merges (feat/fix) | `ready for qa test` → `complete` | Tess in merge step |
| Orchestrator merges (chore/docs/design) | `ready for qa test` → `complete` | Orchestrator in merge step |
| Tess bounces | `ready for qa test` → `in progress` | Tess in bounce comment |

**Rules:**

1. **Same tool round.** The ClickUp `update_task` call goes in the same response as the dispatch / `gh pr create` / `gh pr merge` — not a follow-up. "Queue and forget" is the failure mode.
2. **MCP down → fallback queue.** If `mcp__clickup__*` is unreachable, queue the flip in `team/log/clickup-pending.md` per `team/CLICKUP_FALLBACK.md`, and the orchestrator flushes on reconnect.
3. **Heartbeat audit sweep.** Every heartbeat tick the orchestrator runs `mcp__clickup__get_tasks list_id="901523878268" statuses=["in progress","ready for qa test"]` and reconciles each ticket against reality (agent running? PR open? merged?). Discrepancies are fixed in the same tick.
4. **Tickets created mid-flight.** If Tess (or any role) discovers a bug and files a `bug(...)` ticket, set the initial status correctly: `complete` if the fix already shipped, default if the work is upcoming.

**Mantra:** the ClickUp board is the truth. Don't lie to it.

## Orchestrator merge-gate verification (shipped-build-capture-gated PRs)

**Rule:** Before `gh pr merge --admin` on any PR matching the shipped-build-capture-gated class, the orchestrator MUST verify the merge prerequisites against actual evidence — not against agent assertions, not against a stale earlier snapshot. This is a hard gate, not a "should-check." This is the Unity successor to the Godot HTML5-visual-gate; the editor-vs-runtime divergence class is the proven failure mode it guards (see `.claude/docs/unity-conventions.md` § "Editor-vs-runtime divergence").

**Shipped-build-capture-gated class** (anything where the built exe can diverge from the editor — per `team/TESTING_BAR.md` item 3 + `.claude/docs/unity-conventions.md`):
- Procedural hierarchies assembled at runtime (`Awake()`-built structure that must ship — the "legs-up" serialization class).
- `VolumeProfile` / post-processing stack changes (bloom / grading / vignette / fog added via editor code — they ship empty without `AssetDatabase.AddObjectToAsset` + save).
- Custom shaders (must be in `AlwaysIncludedShaders` or the build strips them).
- NavMesh / click-to-move surface changes (bake-in-memory ships a dead surface — must be saved as an asset and assigned).
- Character mesh / rig / recolor swaps (import-height normalization, material enumeration, avatar T-pose).
- Any UX/visually-visible surface — the world look, HUD, camera framing, spawn framing.

**Pre-merge verification (mandatory; same tool round as `gh pr merge`):**

1. **CI run-id of the latest green build for THIS commit** — fetch via `gh pr view <N> --json statusCheckRollup`. "Should be green" / "agent said it's green" is NOT acceptable. The run-id + green status must be verifiable at merge time. Query CI runs by HEAD SHA (`--commit`), not `--branch --limit 1` — the limit-1 query races against GitHub workflow registration. CI must show `result=Passed` from the `-testResults` XML and `result=Succeeded` from the builder, not exit codes alone.
2. **Built-exe capture present, not editor-only evidence.** The PR/ticket carries a capture from the WINDOWED built exe (`Build/Windows/FarHorizon.exe`, `-screen-fullscreen 0`, in-game capture component) with the **HUD build-stamp visible** (`BUILD <tag> | <UTC> | <sha>`) and the stamp's sha matching PR HEAD. Editor screenshots are necessary, never sufficient — the divergence class proves it.
3. **Self-Test Report comment present** and includes either (a) the built-exe capture of the probe target OR (b) an explicit Sponsor-soak deferral naming concrete probe targets (e.g., "Sponsor must verify: pale-shore spawn framing on first gameplay frame; castaway grounding on slope traverse"). Vague "Sponsor will check" is not a deferral.

**If any of (1)-(3) fails:** the orchestrator does NOT merge. Bounce back to Tess (not the author) with a one-line note naming which check failed; Tess either requests the missing evidence from the author or routes to Sponsor. The orchestrator does NOT make the author/Sponsor decision itself; that's Tess's lane.

**Why:** carried from the M3-era weak-evidence merges (Godot PR #291: Tess APPROVED twice on green-CI, Sponsor overturned both — no orchestrator-side check verified the capture was from the real running build, not the editor). The Unity spike's iter6 "legs-up" incident is the same lesson in the new engine: editor-green is not build-correct. The gate exists at merge-time, not just dispatch-brief time.

**Composition with sibling rules:**
- The merge identity rule (§ "Tess sign-off via PR") is unchanged: Tess approves feat/fix, orchestrator merges. This rule adds an orchestrator-side verification step BEFORE the merge tool call, after Tess approval.
- This rule does NOT apply to non-capture-gated PRs (pure data refactors, code-only refactors without visual/runtime-serialized surface, `chore/docs/test` PRs). Those merge under existing rules.
- This rule operates orthogonally to the Self-Test Report gate. The Self-Test Report gate is author-side (no report → Tess bounces). The merge-gate verification is orchestrator-side (report present but missing the built-exe capture → orchestrator bounces back to Tess).

---

## Self-Test Report (UX-visible PRs)

Any PR that touches a **player-visible surface** (scene/prefab, UI, visual feedback, audio cue, input affordance, save format, world/level content, character) MUST include a **Self-Test Report comment from the author** before Tess reviews. Tess's review starts from the report, not from a cold-read of the diff.

**Categories that REQUIRE a Self-Test Report:**

- `feat(input)`, `feat(world)`, `feat(char)`, `feat(ui)`, `feat(survival)`, `feat(crafting)`, `feat(integration)`, `feat(audio)`
- `fix(input)`, `fix(world)`, `fix(char)`, `fix(ui)`, `fix(build)` (when it changes shipped output), `fix(integration)`
- `design(spec)` only when the spec is consumed by an in-flight `feat` PR (otherwise design is paper-only)

**Categories that do NOT require it (CI green is sufficient):**

- `chore(ci|repo|build)`
- `docs(team|scope|pl)`
- `chore(state|orchestrator|planning)`
- `test(...)` (test-only PRs)
- pure ScriptableObject / data-asset refactors with no rendered surface

**Report format (paste as a PR comment after `gh pr create`):**

```markdown
## Self-Test Report

**Build artifact:** <CI run ID + build sha + the HUD build-stamp it produced, e.g. `BUILD <tag> | <UTC> | <sha>`>
**Scene path:** <e.g. Assets/Scenes/Boot.unity or the scene used for verification>
**Verification method:** <windowed built-exe capture / EditMode test / PlayMode integration test waypoint>

### AC walkthrough
- [x] AC1: <description> — observed: <what you saw/heard>
- [x] AC2: ...
- [ ] AC3: <if not personally verified — explain why and what's covered by automated tests>

### Side-effect inventory
- <other surface that might be affected>: <expected vs. observed>

### Cross-lane integration check
List every other role's feature that shares state with this PR (e.g. click-to-move + camera + NavMesh + character grounding for any input/movement PR). Describe what you probed and what you observed. If you cannot probe cross-lane state (headless-only environment), name it explicitly as a Sponsor-soak probe target so the orchestrator can route it to Tess's journey-probe.

### Sponsor-soak steps (code-verified) — REQUIRED whenever this PR's handoff includes a Sponsor-soak ask
Every instruction line below MUST be code-verified or omitted — never speculated, never pattern-completed from engine-API knowledge. For each line, the action/param/expected-observation traces to a real input-handler or scene-wiring you confirmed.
- **Artifact (exe path):** the windowed built exe, `Build/Windows/FarHorizon.exe` (per `CLAUDE.md` "Sponsor soak = direct artifact" — always include the exact exe path).
- **Build-stamp check:** launch windowed, confirm the HUD reads `BUILD <tag> | <UTC> | <sha>` and the sha matches PR HEAD BEFORE judging (three-builds-in-play identity confusion is a proven failure mode — `.claude/docs/unity-conventions.md`).
- **Steps:** <exact verified actions: "left-click a far point and watch the castaway path to it" / "scroll to zoom, drag to orbit" — each confirmed against the actual input binding/scene>
- **Expected observation per step + concrete success criteria:** <what the Sponsor should see, traced to a real code path>
If this PR has no Sponsor-soak ask, write: "N/A — no Sponsor-soak handoff for this PR."

### Open concerns / known gaps
<anything you noticed but is out of this PR's scope>
```

**Headless-environment fallback:** runtime captures must run WINDOWED, not `-batchmode` (`-batchmode` produces no real rendered frames — see `.claude/docs/unity-conventions.md`). If the author cannot launch a windowed build, the Self-Test Report uses EditMode/PlayMode tests to drive the play loop and notes "verified via headless integration test, no windowed-build capture available — Sponsor's interactive soak is the final gate." Editor evidence is necessary, never sufficient (the editor-vs-runtime divergence class).

**Cross-lane discipline.** The Cross-lane integration check subsection is non-negotiable for every UX-visible PR. Author-side verification accurately describes what THAT PR's author probed — but cross-PR / cross-lane failures slip through when no report cross-checks adjacent-lane state. A character-swap PR that doesn't touch the camera can still break shadow/size calibration if the import-height normalization drifts. The check is "what adjacent surface shares state with this PR's mutation, and what did you observe when you exercised it?" Honest "I couldn't probe — please route to Tess's journey-probe" is acceptable and expected for headless authors; silent omission is not.

**Tess's review path:** read the Self-Test Report first; spot-check ≥1 AC + ≥1 side-effect against the report; then sign off or bounce. **If the report is missing on a UX-visible PR, bounce it back immediately with "Self-Test Report missing" — don't burn review budget cold-reading the diff.**

**Why:** carried from the Godot-era M1 `Main.tscn`-stub miss (~30 PRs of "feature-complete" claims while the runnable build was a week-1 boot stub) — it would have been caught on the first PR if every author had to point at the actual playable surface. The Unity translation: the built-exe capture gate is that "point at the actual playable surface" discipline. See `team/log/process-incidents.md` and the archive's process history at `c:/Trunk/PRIVATE/RandomGame/team/log/`.

## Concurrent agents — role-persistent worktrees (W3-A7 option A)

The harness `isolation: "worktree"` Agent flag is **inactive in our setup** — it requires `WorktreeCreate` hooks the harness doesn't have. Without it, agents share the main checkout's `.git/HEAD`, which produced shared-HEAD-stomp incidents in the Godot-era project. The role-persistent-worktree pattern (carried from RandomGame; see the archive's `team/log/w3-a7-worktree-isolation-proposal.md` for the original evidence + option-A decision) is the standing fix.

**Operative pattern: each role owns a persistent worktree.** Agents work in their role's sticky worktree; the orchestrator-class checkout `c:\Trunk\PRIVATE\Far-Horizon` is reserved for orchestrator surveys.

| Role | Worktree path |
|------|---------------|
| Priya | `C:/Trunk/PRIVATE/Far-Horizon-priya-wt` |
| Uma | `C:/Trunk/PRIVATE/Far-Horizon-uma-wt` |
| Devon | `C:/Trunk/PRIVATE/Far-Horizon-devon-wt` |
| Drew | `C:/Trunk/PRIVATE/Far-Horizon-drew-wt` |
| Tess | `C:/Trunk/PRIVATE/Far-Horizon-tess-wt` |
| Orchestrator (surveys) | `c:\Trunk\PRIVATE\Far-Horizon` |

**Standard run-start invocation** in every dispatch (orchestrator pastes this into briefs from `team/orchestrator/dispatch-template.md`):

```bash
cd C:/Trunk/PRIVATE/Far-Horizon-<your-role>-wt
git fetch origin
git checkout -B <your-role>/<task-name> origin/main
# ... do work ...
git push origin <your-role>/<task-name>:<your-role>/<task-name>
```

**Rules:**

1. **Operate ONLY in your role's worktree.** Don't `cd` into another agent's worktree. Don't operate in the main checkout `c:\Trunk\PRIVATE\Far-Horizon` — that's the orchestrator's surveys and is contended.
2. **Reset cleanly at run start.** `git checkout -B` always force-creates the new branch from `origin/main`. Don't try to recover prior in-flight work — every dispatch starts fresh.
3. **Push by refspec.** `git push origin <branch>:<branch>` is robust against the worktree's local-tracking state.
4. **Don't try to delete your sticky worktree on cleanup.** It's role-persistent; the orchestrator manages worktree lifecycle.
5. **One agent per worktree at a time.** If the orchestrator needs to dispatch two agents from the same role concurrently (rare), the orchestrator creates an ephemeral second worktree and includes its path in the second brief.

What you can rely on:

- Your `git checkout`, `git commit`, branch state, and untracked files are isolated.
- `git fetch origin` always works — `origin` is shared.
- Pushes, PRs, and merges work normally.

Conflict resolution on rebase is unchanged:

1. If rebase conflicts in your own area, resolve and continue.
2. If conflicts are in another role's area, abort the rebase, leave a note in `team/log/<your-role>-conflict.md`, and surface via STATE.md "Open decisions awaiting orchestrator" — don't blind-resolve another role's code.

> Per-task ephemeral worktrees (`Far-Horizon-<role>-<task-slug>`) are also valid for one-off long-form work and may be created at the orchestrator's discretion. They are removed post-merge. The sticky-per-role pattern is the default.

## CI

CI runs on every PR (GitHub Actions, from U4 onward — structure gate on hosted runners + EditMode/PlayMode/Windows build on a self-hosted runner; chains `BootstrapProject.Run → tests → FarHorizonBuilder.BuildWindows`). PRs that red CI cannot be merged — fix forward in the same branch with another commit, push, CI re-runs.

## What to commit, what not to commit

- **Commit**: code (C# runtime/editor), Unity scenes/prefabs/ScriptableObjects + their `.meta` files (including the `.meta` files that keep empty `Assets/` dirs alive), design docs, test plans, asset source files, CI configs.
- **Don't commit**: build outputs (`Build/`, `*.exe`), `*.log`, `test-results*.xml`, `Captures/` (all gitignored — CI must upload artifacts before cleanup), secrets, large binaries (>10 MB), `.claude/` (already gitignored).

Add to `.gitignore` before staging if in doubt.

## STATE.md and DECISIONS.md edits

These are highly contended. Conventions:

- `STATE.md` — only your own role's section. Use the Edit tool with the section header line as the unique anchor. The orchestrator may edit "Phase" and "Open decisions awaiting orchestrator" sections.
- `DECISIONS.md` — **centralized; see subsection below.** No direct edits by agents.
- Always `git pull --rebase` immediately before editing these files. PR title `chore(state): <role> idle` or `docs(decisions): <topic>`. Merge fast (squash + delete-branch) so contention windows are short.

## Decisions log — no direct edits

**`team/DECISIONS.md` is centralized. Only Priya's weekly batch-PR may open a PR that targets this file.** All other roles are hard-prohibited from editing it directly.

**Why:** N parallel agents appending under the same date heading produce N-1 rebase conflicts. This pattern fired 3× in M2 W3 (PR #213 ×2, PR #219 ×1). Centralizing via a weekly batch eliminates the contention surface entirely.

**The protocol:**

1. **Agents** — if your task produces an architectural or process decision worth logging, record it as a `Decision draft:` line in your final report to the orchestrator. Format: `Decision draft: <1-3 line bullet — what was decided, why, reversibility, who it affects>`. Do NOT edit `team/DECISIONS.md` directly. Do NOT open a PR against it.
2. **Priya** — collects `Decision draft:` lines from merged PRs each Monday. Batches them into a single weekly PR (title: `pm(decisions): weekly batch — YYYY-MM-DD`). Template at `team/priya-pl/decisions-batch-pr-template.md`.
3. **Orchestrator** — may append urgent cross-role calls or Sponsor directives directly (in-session only, when the decision is blocking and cannot wait for the next batch). Must still use PR-flow; no force-push to main.

**Enforcement:** Tess bounces any PR whose diff includes changes to `team/DECISIONS.md` and whose author is not Priya (exception: orchestrator urgent escalations noted above). No exceptions for "I just needed to add one line."

## Branch naming

`<role>/<task-scope-kebab>` — kebab-case the task scope, prefix with role. Used for branch names AND for your run-log filenames in `team/log/`. Examples:

- `drew/axe-craft-recipe`, `drew/chop-interaction`
- `devon/click-to-move-port`, `devon/build-windows-fix`
- `tess/m-u1-testing-bar`, `tess/qa-bash-m-u1`
- `uma/thin-loop-ux`, `uma/microcopy-pass`
- `priya/m-u2-backlog`, `priya/risk-register`
- `orchestrator/decisions-<topic>`

Keep branches **short-lived** — open, merge, delete in the same agent run. Branches that linger >1 day are stale and must be rebased.
