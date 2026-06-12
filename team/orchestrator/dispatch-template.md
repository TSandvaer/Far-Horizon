# Orchestrator dispatch template

Standard snippets the orchestrator pastes into every Agent brief. Centralizing them here keeps individual briefs short and uniform, and makes future protocol updates a one-file change instead of N-brief change.

**Reference order:** orchestrator authors a task-specific brief, then appends or inlines the snippets below as needed. Don't quote the whole template — pick the relevant blocks.

## When to use this template

Every dispatch to a named persona (Priya / Uma / Devon / Drew / Tess) via the `Agent` tool uses this template. The orchestrator picks the relevant mandatory + situational blocks, inlines them into the task-specific brief, and fires the dispatch (always with `run_in_background: true` per memory `agents-always-in-background.md`).

**Excluded:** ad-hoc Read-only investigations or one-shot survey questions that don't open a PR (e.g., a quick file inspection the orchestrator runs itself, or a non-named general-purpose agent for a research-only probe). The template assumes PR-producing work in a role worktree.

**Mandatory blocks** (always included): Worktree state, Scoped contract, ClickUp lifecycle, Final-report shape, Doc-update reporting, Done clause (which carries the reviewer-track gate), Lesson reminder, STATE.md update, Merge identity.

**Situational blocks** (included when the predicate matches): Self-Test Report (UX-visible PRs), Visual-primitive test bar (tween / modulate / Polygon2D / CPUParticles2D PRs), HTML5-visual-gated merge-gate (gated-class PRs), Vocabulary contract (parallel dispatches sharing a NEW concept).

---

## Wave decomposition — track-based parallel-author routing (Priya-owned)

When Priya decomposes a wave (or any multi-PR batch) into tickets, every ticket carries an `assignee_recommendation` driven by the track-based routing rule below. Mitigates author-concentration (MarianLearning Pattern H: one dev authored 6/11 PRs across two waves because routing was implicit).

**Routing rule (defaults — Priya adjusts on persona-load):**

| Track | Default assignee | Examples |
|---|---|---|
| engine / save / build / CI / harness infra | **Devon** | autoloads, save schema, `.github/workflows/`, Playwright harness, GUT infra |
| content / mobs / loot / level chunks / visual impl | **Drew** | mob state machines, level chunks, tweens/modulate, room gates, prop integration |
| test design / e2e specs / acceptance plans | **Tess** | GUT suites, Playwright specs, acceptance plans, soak bug-bashes |

**Decomposition output shape** — every ticket row in the wave plan carries: Ticket ID / Title / Work-type tag / assignee_recommendation / Files-in-play.

**Parallel-fire discipline (mandatory):** Priya files ALL tickets for the wave in ONE response (parallel `mcp__clickup__create_task` calls), then surfaces the list. The orchestrator dispatches the workers **in the same orchestrator round** — multiple `Agent` spawns per response, not serial rounds. (Consistent with memory `parallel-dispatch-ticket-race`: Priya files first, workers dispatch with pre-filed IDs inline.)

## Scoped contract (mandatory in every dispatch)

Pin the agent's allowed file scope + role boundary so they don't blind-resolve into another agent's lane on conflict. Block goes near the top of the brief, after the task-specific summary and before the worktree state.

```markdown
**Scoped contract:**
- **Owned files / directories (you may edit):** <list — e.g. `team/uma-ux/<your-doc>.md`, `scripts/player/Player.gd`, `tests/test_player_*.gd`>.
- **Read-only references (read but do NOT edit):** <list — e.g. `scripts/combat/Hitbox.gd`, `team/uma-ux/combat-visual-feedback.md`>.
- **Out of scope (do NOT touch — surface a flag instead):** other roles' design docs, other agents' in-flight branches, `team/STATE.md` sections that aren't your role's.
- **Conflict rule:** if your work would require touching a file outside this scope, STOP and surface a one-line note in your run-log + `team/STATE.md` "Open decisions awaiting orchestrator." Don't blind-resolve into another role's area.
```

Replace placeholders with the task-specific scope. Skip the block only for trivial idle-tick state PRs.

## Worktree state (mandatory in every dispatch)

```markdown
**Worktree state — IMPORTANT (W3-A7 option A):**
- Operate ONLY in `C:/Trunk/PRIVATE/RandomGame-<your-role>-wt` (your role-persistent worktree). Do NOT touch other agents' worktrees. Do NOT operate in the main checkout `c:\Trunk\PRIVATE\RandomGame` — that's the orchestrator's surveys, contended.
- Run-start invocation:
  ```bash
  cd C:/Trunk/PRIVATE/RandomGame-<your-role>-wt
  git fetch origin
  git checkout -B <your-role>/<task-name> origin/main
  ```
- Push by refspec: `git push origin <your-role>/<task-name>:<your-role>/<task-name>`.
- The `git checkout -B` always force-creates from `origin/main`. Don't try to recover prior in-flight work — every dispatch starts fresh.
- Other agents may be in flight in parallel; their worktrees + file scopes are documented in your task-specific brief above. No file overlap is expected; if you find one, surface it (don't blind-resolve).
- **Reviewer-side checkout pattern** (when reviewing a PR whose branch is still claimed by the author's worktree): use `git fetch origin pull/<n>/head:pr-<n>-review && git checkout pr-<n>-review` OR `git checkout --detach origin/<author-branch>`. Do NOT use `gh pr checkout` if the author's worktree is still bound to the head ref — it yanks the branch out from under the author (worktree-concurrency race).
```

Replace `<your-role>` with the literal role name (priya / uma / devon / drew / tess) and `<task-name>` with a kebab-case task slug.

## Vocabulary contract (parallel dispatches sharing a NEW concept)

When dispatching two or more agents in parallel where both will reference a NEW shared concept — a new autoload identifier, a new `Resource` subclass, a new signal name, a new scene path, a new constant, or any newly-introduced identifier crossing PR boundaries — include the block below in BOTH briefs verbatim so both agents read identical names.

**Default = Pattern A (sequence).** Dispatch the type-author first (typically the role that owns the canonical location of the new identifier — Devon for autoloads / engine classes / signals; Drew for scene-tree paths / mob `Resource` subclasses; Uma for design-spec constants). Merge their PR. THEN dispatch the consumer(s) against the merged-on-main vocabulary. Costs one merge cycle of latency; eliminates vocabulary divergence by construction.

**Pattern B (parallel with contract)** is acceptable only when the orchestrator has high confidence about all names upfront AND parallelism is load-bearing. Both briefs MUST carry this block verbatim:

```markdown
**Vocabulary contract (both author + reviewer read identical names — divergence = REQUEST_CHANGES, not NIT):**

- **Identifier name:** `<ExactName>` (e.g. autoload `CameraDirector`, class `S2BoneCatalyst`, signal `boss_defeated`)
- **Defining file:** `<exact res:// path>` (e.g. `res://scripts/camera/CameraDirector.gd`)
- **Constant / enum / discriminator values:** `<exact strings or numerics>` (e.g. `STATUS_IN_PROGRESS = "in progress"`)
- **Cross-file consumers:** list each `res://` path that imports / references the identifier
- **Signal payload shape (if applicable):** `signal_name(arg1: Type, arg2: Type)` exact ordering
```

**Cross-review check.** When peer-reviewing one parallel PR sharing a concept with another in-flight PR, grep the sibling branch for the identifier names + verify they match yours. Vocabulary divergence is mergeability-blocking — file `REQUEST_CHANGES`, not `APPROVE_WITH_NITS`. (See "Three-verdict cross-review format" below.)

**Why:** User-global rule `Parallel-agent shared-concept vocabulary discipline` (codified after ClaudeTeam M3-10, 2026-05-25, where Felix + Maya invented divergent type names — `PersonaGroup` vs `CollapsedPersonaGroup` — under a shape-only contract; the second PR was non-mergeable and required a reconciliation re-dispatch). RG hasn't hit this yet but has parallel-dispatch patterns (W3-T7 Stage 3+4 in flight today; Devon ↔ Drew on shared scenes) where it could. Cheap insurance.

## Lesson reminder (mandatory in every dispatch)

```markdown
**Lesson reminder (load-bearing this session):** `agent-verify-evidence.md` — pull actual file contents and CI evidence before refusing or asserting impossibility. The earlier-this-session `Stratum1BossRoom.gd:204` incident — a Drew agent confidently refused to fix a real GDScript parse error citing language-design priors, while CI logs proved the bug was real — is the cautionary tale. **Verify, don't reason from priors.**
```

## Visual-primitive test bar (paste when dispatch touches tweens / modulate / color-anim / particles)

Per `team/TESTING_BAR.md` § "Visual primitives — observable delta required" (added post-PR-#115/#122 white-on-white incident).

```markdown
**Visual-primitive test bar (load-bearing for tween / modulate / color-anim / particle PRs):**
- Tier 1 (mandatory): paired test asserts `target ≠ rest` for any tweened visual property. `assert_ne(target_color, rest_color)`. White-on-white tweens are the cautionary tale — `tween.is_valid() == true` is necessary but **insufficient**.
- Tier 2 (mandatory for cascading modulate on parented nodes): paired test asserts the modulate is applied to the **visible-draw node** (Sprite2D / ColorRect / Polygon2D), not to a parent CharacterBody2D / Node2D whose child has its own non-white modulate. Modulate cascades multiplicatively; flashing the parent body is a no-op for any child whose own modulate is non-white.
- Tier 3 (aspirational): framebuffer pixel-delta sample at the affected region. Deferred until a `--rendering-driver opengl3` headed CI lane lands; today, Tier 1 + Tier 2 are the binding floor.
- HTML5 verification: tween / modulate / Polygon2D / CPUParticles2D PRs are subject to the HTML5 visual-verification gate per `html5-visual-verification-gate.md`. Headless GUT green is not enough; the Self-Test Report must capture an HTML5 export soak before Tess approves.
- Reference: `team/log/2026-05-html5-visual-feedback-no-op-postmortem.md` for the cautionary tale (PR #115 + #122 shipped tween-based feedback that asserted "tween fires" without asserting "visual changes"; bug shipped to production for ~3 days before Sponsor's `[combat-trace]` soak caught it).
```

The block is mandatory for `feat(combat|ui|level|integration|gear|progression)` and `fix(...)` on the same scopes when the PR introduces or modifies a visual primitive. Skip when the PR is `chore(...)` / `docs(...)` / `test(...)` or touches no visual primitives.

## Merge identity (mandatory in every dispatch)

```markdown
**Merge identity (per `team/log/process-incidents.md`):**
- `feat(...)`, `fix(...)`, `test(...)` — Tess sign-off REQUIRED. Don't self-merge. Tess will pick up.
- `chore(ci|build|repo)`, `design(spec)`, `docs(team|scope)` — Tess sign-off NOT required, BUT merge identity must be the orchestrator (or Priya for `chore(triage)` / `docs(team)`). Devs do NOT self-merge in any category.
- Open the PR and stop. Orchestrator picks up.
```

## STATE.md update (mandatory in every dispatch that does material work)

```markdown
**STATE.md update:**
- Update ONLY your `## <Role> (<role-title>)` section in `team/STATE.md`. Don't touch other roles' sections.
- Bump your run number to the next integer (read your section to find the current run; increment by 1).
- Include the update in the same PR as your task work — don't open a separate `chore(state)` PR per task. (Idle-tick state PRs are still allowed; this rule is about per-task efficiency.)
```

## ClickUp lifecycle (paired flips, same tool round as the action)

Every ticket lifecycle event is a paired ClickUp status move that fires in the SAME tool round as the corresponding action. Per `team/GIT_PROTOCOL.md` § "ClickUp lifecycle as hard gate" + orchestrator memory `clickup-status-as-hard-gate.md`.

```markdown
**ClickUp lifecycle (paired flips, NOT advisory):**
- **At run-start** (if orchestrator hasn't already flipped): `mcp__clickup__update_task task_id=<ticket> status="in progress"`. Same tool round as your first work.
- **On PR open** (`gh pr create`): immediately fire `mcp__clickup__update_task task_id=<ticket> status="ready for qa test"` in the same response. The orchestrator-side dispatch flip + your PR-open flip = two complementary safeguards.
- **MCP unreachable:** queue the flip in `team/log/clickup-pending.md` per `team/CLICKUP_FALLBACK.md` (`ENTRY NNN: <ticket_id> -> <new_status> (reason: <one-line>)`). Orchestrator flushes on reconnect.
- **Don't lie to the board.** If you can't open the PR (ran into a blocker), don't flip to `ready for qa test` — keep it at `in progress` and surface the blocker.
```

## Self-Test Report (UX-visible PRs only)

Required for `feat(integration|ui|combat|level|audio|progression|gear)`, `fix(ui|combat|level|audio|integration)`, and `design(spec)` when the spec is consumed by an in-flight `feat` PR. NOT required for `chore(ci|repo|build|state|orchestrator|planning)`, `docs(team|scope)`, `test(...)`, or `.tres`-only data refactors.

Per `team/GIT_PROTOCOL.md` § "Self-Test Report (UX-visible PRs)" + orchestrator memory `self-test-report-gate.md`.

```markdown
**Self-Test Report (REQUIRED before Tess review):**

After `gh pr create`, post a PR comment with the Self-Test Report. Tess's review starts from this report, not from a cold-read of the diff. If you skip it, Tess bounces immediately.

Comment template:

## Self-Test Report

**Build artifact:** <run ID + zip name + sha>
**Scene path:** <res://scenes/Main.tscn or test scene used for verification>
**Verification method:** <browser+local server / godot --headless / GUT integration test waypoint>

### AC walkthrough
- [x] AC1: <description> — observed: <what you saw/heard>
- [x] AC2: ...
- [ ] AC3: <if not personally verified — explain why + what's covered by automated tests>

### Side-effect inventory
- <other surface that might be affected>: <expected vs. observed>

### Cross-lane integration check
List every other role's feature that shares state with this PR (e.g. Inventory + Pickup + Room gate + Loot for any combat PR). Describe what you probed and what you observed. If you cannot probe cross-lane state (no browser, headless only), name it explicitly as a Sponsor-soak probe target so the orchestrator can route it to Tess's journey-probe (see `team/TESTING_BAR.md` § "Milestone-gate journey probe").

### Open concerns / known gaps
<anything noticed but out of this PR's scope>

**Headless fallback:** if your environment has no browser, drive `godot --headless` against the actual entry scene (Devon PR #107 pattern), capture the GUT integration-test output, and note: "verified via headless integration test, no browser repro available — Sponsor's interactive soak is the final gate."
```

For `chore`/`docs`/`test`/data PRs that don't need a Self-Test Report, replace the block with: `**Self-Test:** <one-line confirmation of what was checked — e.g. "diffed sections against memory rules; no contradictions with sibling docs">.`

## Done clause (mandatory in every dispatch)

```markdown
**Done = PR open with: <list of artifacts> + STATE.md <role> section bump + any required ClickUp queue updates (one PR). Brief report (<NNN words): <list of facts to surface>.**

**Regression guard:** Name at least one test (GUT or Playwright spec) that would fail if this feature broke in a future unrelated PR. If none exists, add it in this PR.

**Reviewer track (hard gate — PR cannot merge without it):**
- **Game-side PRs** (combat / mobs / level / save / progression / gear / UI / audio / Player) → **Drew** reviews.
- **Harness / inventory / engine / build / CI / Playwright fixtures** → **Devon** reviews.
- **Tess-authored PRs** → **Drew** OR **Devon** per `tess-cant-self-qa-peer-review` (pick by PR surface — game-side → Drew, harness/inventory → Devon).
- **Priya-authored process / docs PRs** → **Devon** OR **Drew** per `auto-execute-classes-without-sponsor-ack` § peer-reviewer-selection-by-surface (pick a role different from the author; engine-adjacent docs → Devon; game-side docs → Drew).
- Reviewer posts `APPROVE: <reasoning>` via `gh pr comment <N> --body-file <approve-doc>` per `shared-git-identity-blocks-formal-pr-approval` (the harness blocks `gh pr review --approve` on shared git identity). Orchestrator admin-merges after the APPROVE comment lands.
- **No `APPROVE` comment from the tracked reviewer = no merge.** Period. Even `chore` / `docs` / `test` PRs without Tess QA need the peer-review APPROVE for the merge tool-round to fire. Audit finding 2026-05-23: zero formal PR reviews on the last 30 merges (all via `gh pr comment APPROVE` workaround per `shared-git-identity-blocks-formal-pr-approval`) — this hard-line codifies the actual practice so the convention is explicit, not implicit.

Report back when done.
```

Replace `<list of artifacts>`, `<NNN>`, and `<list of facts>` with task-specific values.

The **Regression guard** line is non-negotiable. It forces every dispatch to produce a named, durable regression surface — not just "paired tests for this PR's logic." All four M2 RC Sponsor-soak findings (2026-05-15) were regressions in surfaces that had previously been tested by component-scoped tests; none had system-scoped regression guards. The named test is the artifact a future unrelated PR's CI run flips RED against, so the regression surfaces at PR-time rather than at Sponsor-soak-time.

The **Reviewer-track hard-gate** line is non-negotiable. MARIAN-TUTOR codified the equivalent rule at `:194` of their dispatch template; the audit (2026-05-23) found Embergrave had been operating on the same convention implicitly — every merge had a peer `APPROVE` comment — but the rule was nowhere written down. Codifying it here removes the silent-convention failure mode (a future agent might skip peer-review on a "trivial" PR and merge directly). Foundation: memory `tess-cant-self-qa-peer-review` (the existing rule this generalizes) + `shared-git-identity-blocks-formal-pr-approval` (the workaround the rule cites).

## Three-verdict cross-review format (peer-reviewer-side)

Peer reviewers (Drew / Devon — and Tess on test PRs) post their verdict via `gh pr comment <N> --body-file <path>` per `shared-git-identity-blocks-formal-pr-approval` (the harness blocks `gh pr review --approve` on shared git identity). The verdict header is the FIRST line of the comment body so the orchestrator + future-readers can scan PR threads at a glance.

```markdown
## REVIEW VERDICT: APPROVE | APPROVE_WITH_NITS | REQUEST_CHANGES
```

Three valid verdicts — all are load-bearing. Pick the right one; don't downgrade or upgrade out of conflict-avoidance.

### APPROVE

PR ships as-is. No outstanding issues. Reviewer has nothing to flag beyond an LGTM.

```markdown
## REVIEW VERDICT: APPROVE

Reviewed PR #NNN against AC1-AC3. Paired tests present and exercise the failure mode (tests/test_foo.gd:42-89). CI green on commit <SHA>. No NITs.

Self-Test Report screenshot covers AC1 + AC2 visually; AC3 is harness-asserted via test_foo_ac3.gd:120 — confirmed locally.
```

### APPROVE_WITH_NITS

**The mergeable-with-followup verdict.** PR meets all acceptance criteria and SHIPS as-is — but the reviewer has non-blocking quality issues worth tracking. The orchestrator auto-files a `chore(...): <PR-N> NITs follow-up` ticket scoped to the NITs comment text, then admin-merges the PR.

Per memory `auto-execute-classes-without-sponsor-ack` rule 6, NITs-ticket-creation from APPROVE_WITH_NITS comments is in the auto-execute class when scope is mechanically derivable from a numbered list with file:line refs. Does NOT apply if the reviewer flags any NIT as "needs discussion" or scope-expanding.

```markdown
## REVIEW VERDICT: APPROVE_WITH_NITS

PR meets AC1-AC4. Paired tests present. CI green on commit <SHA>. Mergeable.

NITs (non-blocking, file as follow-up ticket):
1. scripts/foo/Bar.gd:42 — magic number `0.5` should be a named const for clarity
2. tests/test_bar.gd:88 — duplicate assertion block, dedupe candidate
3. resources/level_chunks/zone_a_001.tres:15 — `mob_spawns[2]` position 320 looks 1px off the chunk grid; verify with Drew next pass

NITs are mechanical; no scope expansion. Auto-file follow-up per `auto-execute-classes-without-sponsor-ack` rule 6.
```

**Do NOT downgrade to `APPROVE`** — silently drops the NITs, they regress on the next PR touching the same surface.
**Do NOT upgrade to `REQUEST_CHANGES`** — incorrectly blocks a shippable PR; the NITs aren't AC-blocking.

When file overlap + downstream timing permits, the orchestrator may also absorb the NITs into the next-scheduled downstream PR touching the same files (Path Y pattern per `auto-execute-classes-without-sponsor-ack` rule 6 NITs-absorption); close the NITs ticket as duplicate-of-downstream.

### REQUEST_CHANGES

PR does NOT merge until the listed issues are resolved. Reserved for: AC not met, test gap on the failure mode, vocabulary divergence with parallel PR (see Vocabulary contract block above), HTML5-visual-gated PR missing self-soak section, claim-fidelity violation in the Self-Test Report (per `tightened-final-report-contract` Amendment), regression-guard missing on a feature PR.

```markdown
## REVIEW VERDICT: REQUEST_CHANGES

AC2 not met — the death-tween fires but `tween.is_valid()` is the only assertion; per Visual-primitive test bar Tier 1, need `assert_ne(target_color, rest_color)` to catch the white-on-white class.

Required changes:
1. tests/test_grunt_death.gd:62 — add `assert_ne(end_color, Color.WHITE)` after the tween-fires assertion
2. scripts/mobs/S1Grunt.gd:118 — modulate target is set on parent CharacterBody2D; per Tier 2 needs to set on the visible Sprite2D child instead (cascade trap)

Re-dispatch with these resolved + the same brief.
```

**Reviewer self-discipline:** if you're tempted to `APPROVE` to avoid friction but you have NITs, use `APPROVE_WITH_NITS`. If you're tempted to `REQUEST_CHANGES` because something looks suboptimal but doesn't block AC + doesn't regress quality, use `APPROVE_WITH_NITS`. The three-verdict shape exists precisely to prevent the binary "ship clean or block" trap.

## Final-report shape — TIGHT + cite-able evidence (mandatory in every dispatch)

```markdown
**Final report to orchestrator — TIGHT (≤200 words) + CITE-ABLE EVIDENCE:**

Your task-completion message back to the orchestrator MUST be tight to preserve the orchestrator's main-window context AND any claim about state (CI / GUT / Playwright / soak / artifacts) MUST cite verifiable evidence — not bare assertions. Required content:

- **PR URL** (1 line)
- **Verdict** (1 line — `APPROVE` / `blocked-on-X` / `partial — see follow-up #...`)
- **Cite-able evidence** (the state claims you make in this report — each MUST follow the shape below):
  - **CI state:** `CI: <run-id URL>` or `CI: pass on <commit SHA>`. NOT `"CI passes"` / `"CI green"` / `"CI should be green"`.
  - **GUT results:** `GUT: <N>/<M> on <commit SHA>`. NOT `"GUT clean"` / `"GUT passes"`.
  - **Playwright results:** `Playwright: <run-id URL>` or `Playwright: <N>/<M> on <commit SHA>`. NOT `"Playwright green"`.
  - **Soak verification:** `Soak: <screenshot/video URL>` or `Soak: deferred to Sponsor with probes: <enumerated list>`. NOT `"soak fine"` / `"Sponsor will check"`.
  - **Paired tests added:** `Tests added: <file path>:<line range>`. NOT `"paired tests added"`.
  - **In-flight state (genuinely still running):** cite the run-id URL + last observed status (e.g., `CI: in flight, run https://...26288244641 — last status 'queued' at 12:38 UTC`). NOT `"CI in flight"` with no link.
- **Blockers or follow-ups** (1-3 lines max — only what the orchestrator needs to act on this turn)
- **Doc updates** (1 line — `Doc updates: <file> — <one-line>` or `Doc updates: none`)
- **Decision draft** (omit if none — `Decision draft: <1-3 line bullet describing the architectural or process decision>` — Priya batches into `team/DECISIONS.md` weekly; NEVER edit that file directly)

Detailed content goes in artifacts the orchestrator can read on-demand, NOT in the orchestrator-bound message:

- **Empirical evidence / trace excerpts** → PR body
- **Per-AC verification + AC walkthrough** → Self-Test Report comment on the PR
- **Non-obvious findings** → PR body "Non-obvious findings" section
- **Cross-lane integration check** → Self-Test Report on the PR (template above)
- **Sponsor-input items** → PR body section if applicable
- **8-run sweep evidence** → PR body / Self-Test Report

**Return timing — exit after report, do NOT wait for merge.** Submit your final report at `ready for qa test` (PR open + Self-Test Report posted + ClickUp flipped) and EXIT. Do NOT wait for Tess QA or orchestrator merge before reporting. The merge + ClickUp `→ complete` flip is the orchestrator's lane (per `clickup-flip-paired-with-merge`). Waiting around for merge wastes agent cycles AND delays the orchestrator's visibility into your readiness. If Tess REQUEST CHANGES, the orchestrator re-dispatches you fresh with the rework brief — that's the contract. **Concrete tell:** if your final report describes events that happened AFTER your work was done (merge, Tess approval, ClickUp `→ complete` flip), you waited too long. Submit at PR-open + ClickUp `ready for qa test` and exit.

**Orchestrator-side enforcement of claim-fidelity.** If your final report makes a state claim ("CI passes", "GUT clean", "Soak fine") without the cite-able evidence shape above, the orchestrator will SendMessage-bounce-back asking for the cite BEFORE processing the report or dispatching the next agent. Don't re-do the work — just paste the evidence. Catching the optimism at report time is cheaper than catching it downstream.

**Use `gh pr create --body-file <path>` for PR bodies longer than ~5 lines** (avoids the 600s-stream-watchdog kill observed on long heredoc/inline `--body` patterns in sibling projects). Long review comments already use `gh pr comment --body-file` — same rationale.
```

**Backstory:** the M2 W3 mid-retro investigation (2026-05-15) found that verbose sub-agent final reports flooding the orchestrator's main conversation window was the dominant context-bloat surface. The M3 retrospective (2026-05-22, PR #315) added the claim-fidelity + return-timing amendments after empirical findings — PR #314's "CI in flight" claim when CI had failed 2 min earlier (P2 pattern), Drew's PR #312 agent waiting 42 min for merge before reporting. Tight orchestrator-bound reports + cite-able evidence + return-at-ready-for-qa is the discipline that closes that gap. See orchestrator memory `tightened-final-report-contract.md` (with 2026-05-22 amendments) + `agent-lifecycle-vs-sendmessage.md`. Pair with the persona-file references in `.claude/agents/{role}.md`.

## HTML5-visual-gated merge-gate verification (orchestrator-side, paste when dispatching gated PRs)

Per `team/GIT_PROTOCOL.md` § "Orchestrator merge-gate verification (HTML5-visual-gated PRs)". Sub-agents authoring a PR in the visual-gated class should be aware the orchestrator runs this check at merge time and the author's Self-Test Report needs to carry the section content for the merge to succeed.

```markdown
**HTML5-visual-gated merge-gate (orchestrator runs at merge time; author satisfies in Self-Test Report):**

When your PR touches `Tween / CanvasItem.modulate / Polygon2D / CPUParticles2D / Area2D state mutations / ColorRect with HDR colors / TileMap-scroll / z-index changes that affect rendering`, the orchestrator will verify the following BEFORE `gh pr merge --admin`. Satisfying these in your Self-Test Report up-front avoids a Tess-bounce + re-dispatch round trip:

1. **CI run-id of the latest green build for THIS commit** — your final-report cite (`CI: <run-id URL>`) covers this. The orchestrator confirms by `gh pr view <N> --json statusCheckRollup` against your HEAD SHA.
2. **Build SHA in the release-build artifact name matches PR HEAD** — when a release-build is part of the verification, the artifact-name SHA must match. The orchestrator confirms by `gh api repos/.../actions/runs/<run-id>/artifacts`.
3. **Self-Test Report comment includes either (a) real-browser screenshot/video of the probe target OR (b) explicit Sponsor-soak deferral with concrete probe targets enumerated.** Headless Playwright captures alone are NOT sufficient for visibility-of-effect claims on this class (per `team/TESTING_BAR.md` § "Auto-memory: `html5-visual-gated-author-self-soak`"). Acceptable: real-browser incognito screenshot OR Sponsor-soak deferral that NAMES the concrete probe targets (not "Sponsor will check").
4. **For ineligible-surface PRs (escape-clause does NOT apply):** if your surface is HTML5-visual-gated AND your Self-Test Report does NOT carry a real-browser self-soak section, **pre-merge Sponsor-soak is required — NOT post-merge.** The orchestrator routes the artifact to Sponsor for soak BEFORE the merge tool-round, not after.

If any check fails, Tess bounces with a one-line note naming which check failed. Don't take it personally — the rule exists because PR #291 (M3 Tier 2 W3, 2026-05-21) consumed ~12-15 agent-cycles across 7 author iterations because the gate wasn't enforced at merge time.
```

Skip this block for `chore(...)` / `docs(...)` / `test(...)` PRs or PRs that touch no visual primitives. Paste it in dispatches that match the gated class above.

## Doc-update reporting (mandatory in every dispatch)

```markdown
**Doc updates (`.claude/docs/`):** if your maintain-docs Stop hook ran and produced an update to any file under `.claude/docs/`, list those files + the rationale in your final report. Format: `Doc updates: <file> — <one-line rationale>`. If no docs were updated, state explicitly: `Doc updates: none (early-exit applied — <reason>).` Sponsor wants visibility into this mechanism firing.
```

This block is non-negotiable in every dispatch. The Stop hook already runs for sub-agents, but the orchestrator + Sponsor cannot see what happened unless the agent surfaces it.

## ClickUp queue (when MCP is down)

```markdown
**ClickUp state:**
- MCP server is intermittent this session. If you can't reach `mcp__clickup__*` tools, queue your status updates in `team/log/clickup-pending.md` per `team/CLICKUP_FALLBACK.md`.
- Queue ENTRY format: `ENTRY NNN: <ticket_id> -> <new_status> (reason: <one-line>)`.
- Check the file's last entry number and increment.
- Orchestrator replays the queue when MCP reconnects.
```

## Self-merge denials (rare cases)

The harness denies self-merge of one's own PR via `gh pr review --approve` (returns "can not approve your own pull request" — harness identity matches author). Workaround:
- For test PRs Tess approves: deliver approval via `gh pr comment <PR#> --body "LGTM, signing off"` then merge via `gh api PUT repos/.../pulls/<PR#>/merge -f merge_method=squash`. Tess has authority for this on test PRs.
- For other roles: don't try to self-merge. Open the PR and stop.

## Work-type tag (ticket-level, Priya applies at creation)

Every ticket carries a free-text tag from `impl` / `spec` / `investigation` / `test` / `chore` / `cleanup`. The tag drives which acceptance gates apply: **impl** needs a green paired test; **spec** needs PR-opens-to-template; **investigation** needs question-answered-in-PR-body; **test** needs a failing-first contract; **chore** needs no behavior change; **cleanup** needs comment-only or follow-up reframe. Without the tag, the testing-bar rubric mis-scores spec/investigation tickets as low quality. Priya applies the tag at ticket creation; the orchestrator checks it pre-dispatch. (Imported from MarianLearning's 2026-05-22 retro.)

## Pre-dispatch checklist (orchestrator-side)

Run this checklist BEFORE firing the `Agent` call. Catches missing blocks at dispatch time when fixing them is a one-line brief edit — not after the agent's burned cycles on an under-specified task.

- [ ] **Worktree-concurrency check.** Scan in-flight `Agent` tasks for any whose worktree maps to the target persona. If one exists, do NOT dispatch — queue the new task to fire after the first's `<task-notification>`, or reassign by surface (per user-global `Sub-agent worktree-concurrency discipline`).
- [ ] **Fresh `main` pull.** The role worktree's branch will be force-created from `origin/main` by Step 0 — confirm `git fetch origin` ran in this orchestrator tick (or include it in the brief's Step 0 as the existing template does).
- [ ] **Ticket ID + body included verbatim** in the brief (sub-agents lack `mcp__clickup__*` tools per `sub-agent-mcp-tool-surface-scope`; they read the ticket body from the brief, not from the board).
- [ ] **Ticket-body hard gates.** Ticket body carries an explicit OOS list + a named success-test. Missing either → bounce to Priya for flesh-out BEFORE dispatch (or the orchestrator fills them per the ticket-flesh-out auto-decide class when it has the context).
- [ ] **Work-type tag present** on the ticket (`impl`/`spec`/`investigation`/`test`/`chore`/`cleanup`) — drives which acceptance gates apply.
- [ ] **Branch name** follows `<role>/<id>-<slug>` format.
- [ ] **Scoped contract block present** — owned files, read-only references, OOS, conflict rule. OOS named explicitly; if the agent should not touch a tempting adjacent file, NAME IT.
- [ ] **Reviewer named** per Done clause reviewer-track (game-side → Drew, harness/inventory → Devon, Tess PRs → peer by surface, Priya docs → Devon or Drew by surface).
- [ ] **ClickUp lifecycle block present** — orchestrator pre-flipped to `in progress` in the same tool round as this dispatch.
- [ ] **Final-report contract block present** — TIGHT + cite-able evidence + return at `ready for qa test`, do NOT wait for merge.
- [ ] **Doc-update reporting block present** — agent must surface `Doc updates: ...` line in final report.
- [ ] **Lesson reminder, STATE.md update, Merge identity, Done clause** all present.
- [ ] **If parallel dispatch shares a NEW concept:** Vocabulary contract block present in BOTH briefs verbatim, OR Pattern A sequencing chosen (type-author first → consumer next). See Vocabulary contract above.
- [ ] **If UX-visible:** Self-Test Report block present.
- [ ] **If tween / modulate / Polygon2D / CPUParticles2D / Area2D-state surface:** Visual-primitive test bar block present + HTML5-visual-gated merge-gate block present.
- [ ] **`run_in_background: true`** on the Agent call per `agents-always-in-background`. Foreground dispatch blocks the orchestrator's turn until the slowest parallel agent returns; main thread floods with sub-agent tool calls and Sponsor can't reach the orchestrator.
- [ ] **`name:` set** on the Agent call to a recognizable handle (e.g. `name: "drew-w3-t8"`) so `SendMessage` / `TaskOutput` can address the agent later if needed.

## Persona-specific overrides

Short notes per role. The mandatory + situational blocks above are the contract floor; these are the deltas worth flagging per persona at dispatch time.

### Drew (game content + level chunks)

- **HTML5 visual-verification gate** triggers heavily for Drew's surface (mob death tweens, modulate, Polygon2D mob art, CPUParticles2D effects, room-gate Area2D state, level-chunk visual loading). Default-include the Visual-primitive test bar + HTML5-visual-gated merge-gate blocks unless the PR is strictly `.tres`-data or `chore`.
- **Self-Test Report self-soak in incognito + DevTools** mandatory before posting per memory `html5-visual-gated-author-self-soak`. PR #291's two-iteration GUT+CI-claimed-complete failure is the cautionary tale.
- **Reviewer:** Devon for engine/harness adjacency; otherwise Drew is reviewed by Devon by default (cross-lane per TEAM.md).

### Devon (engine + harness lead)

- **Engine / autoload / `Resource`-class introductions** are common Vocabulary-contract triggers. When Devon introduces a new autoload or signal that Drew/Tess will consume, default to Pattern A (sequence): Devon merges first, Drew/Tess dispatch against the merged vocabulary.
- **Lint sweep PRs (`chore(test|build)`)** skip the Self-Test Report + Visual-primitive blocks but still need Done clause + Reviewer (Drew by default for harness work; engine-adjacent lint can be Drew or Tess).
- **Build / CI / `.github/workflows/` changes** — Devon's lane; reviewer is Drew unless the change is Playwright-harness-specific (then Tess can review the spec side, Devon is still PR author).

### Tess (QA / test design)

- **Tess can't self-QA.** Tess-authored PRs need a Drew or Devon peer reviewer to satisfy the testing-bar's QA gate (memory `tess-cant-self-qa-peer-review`). Pick the peer by PR surface — game-side → Drew, harness/inventory/engine → Devon.
- **Self-merge denied by harness** on `gh pr review --approve` of own PR (shared git identity). Workaround for test PRs: deliver approval via `gh pr comment <PR#> --body "LGTM, signing off"` then merge via `gh api PUT repos/.../pulls/<PR#>/merge -f merge_method=squash`. Tess has authority for this on test PRs.
- **In-flight QA state matters when scheduling Tess as author** — per memory `tess-targeting-brief-checks-inflight-qa`, before dispatching Tess-consumer scaffold work, check Tess's current QA load. If QA-loaded, queue OR re-shape to Drew/Devon for the parallel portion. Treat Tess scaffold throughput as ~0.5× calendar.

### Uma (UX / visual / audio direction)

- **Visual prompts → Drew impl** is the common shape. Uma authors a spec under `team/uma-ux/` (palettes, boss intro choreography, copy, audio cues); the spec consumer is named in the brief; Drew (or Devon if engine-adjacent) implements.
- **No Self-Test Report on spec-only PRs** unless the spec is consumed by an in-flight `feat` PR in the same tool round (per Self-Test Report block above — `design(spec)` is in the required-class only when consumed).
- **MCP-driven asset gen (PixelLab / pixel-mcp / bgclear.ai)** runs in the orch main session, not Uma sub-agent dispatch — per memory `sub-agent-mcp-tool-surface-scope`, non-clickup MCP tools don't inherit to sub-agents. Uma writes the prompt + Drew (or Sponsor for MJ-class) executes the gen.

### Priya (Project Leader / coordination)

- **Priya does NOT spawn peers** — she authors process docs, retros, backlogs, M3 design seeds. The orchestrator dispatches workers based on her recommendations.
- **`team/DECISIONS.md` is Priya-only** (weekly batch-PR cadence, Mondays). Other roles' final reports include `Decision draft:` lines; Priya batches them via `decisions-batch-pr-template.md`. Per the project CLAUDE.md "Doc conventions" section.
- **Reviewer:** Devon OR Drew per `auto-execute-classes-without-sponsor-ack` § peer-reviewer-selection-by-surface (engine-adjacent docs → Devon; game-side / content docs → Drew). Priya orch-docs PRs WITH peer-reviewer attached + CI green are in the auto-merge class per rule 6 — orchestrator merges without Sponsor sign-off.

## Worktree cleanup (orchestrator-side, post-merge)

After a PR merges, the local-branch-delete may fail with `cannot delete branch '<role>/<task>' used by worktree at '<path>'`. This is cosmetic — the GitHub-side state is clean (remote branch deleted via `--delete-branch` admin merge), only the local branch ref lingers. Two options:

- Leave it — next time the agent for that role runs, `git checkout -B <new-task>` overwrites the stale local branch.
- Force-overwrite via `cd <worktree-path> && git fetch origin && git checkout -B <new-branch> origin/main` (the `-B` does both create-or-reset).

Per-task ephemeral worktrees can be removed via `git worktree remove --force <path>` after the PR merges.
