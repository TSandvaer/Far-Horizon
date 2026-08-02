---
name: maintain-docs
description: MANUAL-ONLY. Capture a hard-won finding into `.claude/docs/` after an incident that actually cost something — a wasted rebuild, an overturned soak, a dead agent-hour. Invoked only as /maintain-docs, never automatically. Refuses to write anything whose motivating incident cannot be named with its cost.
---

# Maintain Docs (manual)

Capture knowledge that was **paid for** into `<PROJECT_ROOT>/.claude/docs/` so future Claude sessions don't pay for it again.

## ⛔ This skill is MANUAL-ONLY (Sponsor, 2026-08-02)

It previously ran as a Stop hook after every turn that touched a file or dispatched an agent — i.e. after **every orchestrator tick**, spawning 4 sonnet agents each time whose literal task was "find something to document." Three agents asked that question will always find something; returning `NO_PROPOSALS` reads as failing the task. Over ten days that engine helped produce **79 commits with 47 docs and zero `feat`** (measured on `origin/main`, 2026-08-02).

The Stop-hook registration was removed from `.claude/settings.json`. **Do not re-add it.** This skill now runs only when a human types `/maintain-docs`.

## Step 1: The incident gate (hard precondition — no exceptions)

A doc entry may be written **only** if you can state both of these concretely:

1. **The incident** — a specific thing that went wrong, named with a verifiable reference (a PR number, a commit SHA, a ticket ID, a soak that overturned a claim, an agent that died mid-task).
2. **What it cost** — a rebuild, a soak round, a wasted dispatch, hours of agent time, a wrong merge. Concrete, not "confusion" or "could have been clearer."

Write it in this shape before proposing anything:

> **Incident:** <what broke, cited> — **Cost:** <what was actually spent>

**If you cannot name the incident and its cost, there is no doc to write. Stop and end silently.** "This seems useful," "future Claude would benefit," "worth capturing," and "non-obvious" are NOT incidents — that bar was already in this skill and it did not hold. An incident is something that already happened and already cost something.

Additionally, still stop if the finding is already covered in an existing `.claude/docs/` file. Read before proposing.

**Corollary — the docs are not a growth surface.** Prefer amending an existing doc over creating a new one. A new file needs its own incident, not just a new topic.

**Unmerged-API defer rule (Drew PR #318 finding 2026-05-22).** Even once the incident gate passes, captures that would cite a function / API / file / commit only present on an UNMERGED feature branch should DEFER until the parent PR merges. The alternative is to keep the proposal but tag it explicitly as "pending PR #N merge" so peer-reviewers know the cite cannot be verified against `main` yet. Empirical case: PR #318's initial draft included a `CameraDirector.follow_target` cite from the still-open PR #314 spike branch; Drew's peer-review caught the premature cite and the capture was scope-reduced + deferred until PR #314 merges. The consolidator (Step 4) should reject proposals that violate this rule unless the "pending PR #N" tag is present.

**Ticket-id cites > scratch `.md` cites (Tess PR #321 finding 2026-05-22).** When a capture would cite a source artifact, prefer cite shapes that are durable in `git log`:

- **PREFER:** ClickUp ticket IDs (`86c9xw8xd`), PR numbers (`PR #321`), commit SHAs (`a885d56`), file:line refs against a known commit (`tests/playwright/specs/equip-flow.spec.ts:142 @ eb6714e`).
- **AVOID:** paths to scratch markdown files that aren't yet committed (`team/<role>/_pr<N>-review.md`, `team/<role>/<topic>-investigation-2026-05-22.md`). These vanish on branch switch, get cleaned during workspace passes, and aren't retrievable from `git log` by future readers.

Empirical case: PR #318's initial draft cited `team/tess-qa/playwright-red-main-investigation-2026-05-22.md` (Tess's morning investigation doc). The doc was uncommitted scratch in the worktree at cite-time; the PR couldn't merge until the cite was replaced with the parent P0 ticket reference (`86c9xw8xd`). Future maintain-docs captures should default to ticket-id cites when the source artifact isn't yet in `git log` — the ticket is durable; the scratch doc may not be.

## Step 2: Inventory + conversation brief

- List `<PROJECT_ROOT>/.claude/docs/` contents.
- Read the "Detailed Documentation" section of `<PROJECT_ROOT>/CLAUDE.md` to get the current index.
- Write a brief of **at most 300 words** stating the Step-1 incident and its cost, then the one lesson that would have prevented it. Nothing else — no survey of the turn, no list of things touched, no "other candidates worth considering."

## Step 3: Three parallel proposer agents (single message, 3 Agent calls)

Call the Agent tool 3 times **in the same message** with `subagent_type: general-purpose` and `model: sonnet`. Identical prompt for each (label them A, B, C):

```
You are proposing documentation updates for <PROJECT_ROOT>/.claude/docs/ based on a recent conversation turn.

## Conversation brief
<BRIEF FROM STEP 2>

## Existing docs inventory
<FILE LIST FROM STEP 1>

## Existing index (from CLAUDE.md "Detailed Documentation" section)
<INDEX SECTION>

## Your task — ONE question only
**Would this doc entry have PREVENTED the named incident?** For each candidate, decide: skip, or amend an existing doc (which one and where). Creating a new file requires its own named incident.

Do NOT answer "how could the documentation be improved" — that question is banned here. It always has an answer, and answering it is what produced 47 docs commits and zero features in ten days. You are not improving documentation; you are recording the price of one specific incident so it isn't paid twice.

Read relevant existing docs before proposing, so you don't duplicate what is already there. If the incident's lesson is already written down anywhere in `.claude/docs/`, return NO_PROPOSALS.

## Output format — propose only, do NOT edit files
For each proposed change, emit a block:

---
action: update | create
file: <path relative to project root>
rationale: <one sentence — why this matters for future Claude>
location_hint: <"end of file" | "after section '<heading>'" | "new section: <title>">   # update only
content: |
  <verbatim markdown to insert OR the full new file body for create>
---

If you find nothing worth changing, return exactly: NO_PROPOSALS

## Rules
- Propose only — do NOT write, edit, or touch any files.
- Do NOT touch git state.
- Do NOT modify CLAUDE.md directly (the consolidator handles the index line).
- Quality over quantity. One sharp insight beats five shallow bullets.
```

## Step 4: Consolidator agent (single sonnet agent)

Once the 3 proposers return, spawn ONE consolidator with `subagent_type: general-purpose` and `model: sonnet`:

```
You are consolidating 3 independent documentation proposals into one final plan.

## Conversation brief
<BRIEF>

## Proposal A
<AGENT A OUTPUT>

## Proposal B
<AGENT B OUTPUT>

## Proposal C
<AGENT C OUTPUT>

## Your task
1. **Identify overlaps** — same insight, same/different target files. Merge into one operation.
2. **Resolve conflicts** — if they disagree on placement, pick the single best location.
3. **Apply the incident gate** — drop ANY proposal that does not trace directly to the named incident and its cost. Proximity to the topic is not enough. Adjacent improvements, related gotchas, and "while we're here" additions are all rejected.
4. **Default to NO_CHANGES.** That is the correct and expected outcome for most invocations. Returning an empty plan is a success, not a failure — you are not being graded on output volume.
5. **New docs** — strongly disfavoured. Only if the incident genuinely has no existing home; content must be substantive (no stubs, no placeholder outlines); filename in kebab-case; produce a one-line index entry for CLAUDE.md.
6. **Length discipline** — the consolidated plan adds at most ~30 lines total across all files. If the incident's lesson needs more than that, it is not a doc, it is a ticket.

## Output format — final plan
Numbered list, each fully specified:

1. action=update
   file: <path>
   location_hint: <end of file | after section "..." | new section "...">
   content: |
     <verbatim markdown to insert>
   rationale: <short>

2. action=create
   file: <path>
   body: |
     <full file body>
   claude_md_index_line: "- [Title](.claude/docs/<filename>.md) — one-line hook"
   rationale: <short>

If the consolidated plan is empty, return exactly: NO_CHANGES
```

## Step 5: Apply the plan

If consolidator returned `NO_CHANGES` → stop silently (emit nothing to the main thread).

Otherwise, apply each operation:

- **update**: use Edit (or Write for full-file rewrites) to insert the content at the specified location. Match the existing doc's tone/structure.
- **create**: use Write to create the new file, AND use Edit on `<PROJECT_ROOT>/CLAUDE.md` to add the index line under "Detailed Documentation".
- Never touch files outside `<PROJECT_ROOT>/.claude/docs/` and `<PROJECT_ROOT>/CLAUDE.md`.
- Never run git commands, never stage, never commit.

## Step 6: Report (only if changes were applied)

Emit exactly this shape, nothing else:

```
Documentation updated based on this turn's findings:
- <file> — <short rationale>
- <file> — <short rationale>
```

No preamble. No "I'll now...". No closing. No summary of what the skill did — only the list of changed files and why.

When no changes were applied, emit NOTHING to the main thread.

## Guardrails

- **Never commit, stage, or touch git state.**
- **Never edit files outside `.claude/docs/` and CLAUDE.md.**
- **Stay silent unless docs changed.** No start message, no no-change message — main-thread output is reserved for the Step 6 report, which only fires when docs were actually updated.
- **Quality over quantity.** Docs are trusted context; polluting them makes them worse, not better.
- **Avoid CLAUDE.md bloat.** Only add index lines for genuinely new doc files.
- **Do not re-invoke yourself**, and do not spawn nested maintain-docs calls.
- **Never re-register this skill as a Stop hook** (or any other automatic trigger). Sponsor decision 2026-08-02; the automatic firing is the thing that broke.
- **NO_CHANGES is the expected result.** If several consecutive invocations all produce edits, the incident gate is being read too loosely — tighten it, don't celebrate the throughput.
