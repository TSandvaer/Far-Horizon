---
name: read-obsidian-folder
description: Scan the Far Horizon Obsidian vault folder and turn its markdown into ClickUp tickets (from the "Ticket prompts" folder) and project vision docs (from the "visions" folder), with dedupe, change-detection, a preview→approve gate, and remembered routing for new folders. Use when the user says "read obsidian folder", "/read-obsidian-folder", "sync my obsidian notes", "turn my obsidian prompts into tickets", "import my vault notes", or asks to pull Obsidian vault content into ClickUp / the project docs.
---

# Read Obsidian Folder

Pull markdown out of the Far Horizon Obsidian vault and route it into the project:
**Ticket-prompt** notes become ClickUp tickets; **vision** notes become team-visible
docs under `.claude/docs/`. The skill is idempotent (never duplicates), detects edits to
already-synced files, and asks-then-remembers how to route folders it hasn't seen before.

This skill is **manual / on-demand** — it runs only when the user invokes it. It always
**previews the plan and waits for approval** before any ClickUp write, doc write, or vault
rename. It **never commits or opens PRs** (protected `main`; git is the user's / orchestrator's job).

## Fixed configuration

- **Vault root:** `C:\Users\538252\OneDrive - EDC-Gruppen A S\Documents\EDC Obsidian vault\EDC_Notes\Privat\Far Horizon`
  (recurse all subfolders).
- **ClickUp list:** Far Horizon — list id `901523878268`. New tickets land in status **`to do`**.
- **State file:** `.claude/skills/read-obsidian-folder/sync-state.json` — holds the folder
  **routing config** + the per-file **manifest** (path, type, content hash, ClickUp id / docs path, marker, date).
- **Vision docs land at:** `.claude/docs/vision-<slug>.md` (top-level — the session-start hook
  globs `.claude/docs/*.md` non-recursively, so visions MUST be top-level to reach the team).

## Markers (in the vault filename)

| Destination     | Marker appended after sync | "already done" detection (skip)                |
|-----------------|----------------------------|------------------------------------------------|
| ClickUp ticket  | ` (created)`               | filename contains `(created)` **or** `(sent)`  |
| Vision doc      | ` (integrated)`            | filename contains `(integrated)`               |
| Ignore          | — (none)                   | folder routed to `ignore`                      |

`(sent)` is the legacy manual marker — recognized as "done" for safety. On first run, migrate
any `(sent)` files to `(created)` for consistency (see Step 5).

**Title / slug derivation:** take the filename, drop the `.md`, and strip a trailing
` (sent)` / ` (created)` / ` (integrated)` / ` (read and integrated)` marker (case-insensitive,
allowing an embedded id like `(created 86xxx)`). The result is the ticket **title**; the
kebab-cased result is the vision-doc **slug**.

---

## Step 0 — Load state

Read `.claude/skills/read-obsidian-folder/sync-state.json`. If it is missing, treat the
manifest as empty and the routing config as the seeded default (`Ticket prompts` →
`clickup-ticket`, `visions` → `vision-doc`). Note whether this is a **first run** (empty manifest).

## Step 1 — Scan + classify

1. List every `.md` under the vault root (recurse). Use a single Bash `find`.
2. For each file, determine its **routing folder** = the vault-root-relative folder it sits in
   (e.g. `Ticket prompts`, `visions`). Match against the routing config (case-sensitive on the
   real folder name as stored).
3. Resolve each file's **destination**:
   - `clickup-ticket` | `vision-doc` | `ignore` from the routing config.
   - **Unknown folder** (not in the config): collect it. After the scan, fire ONE
     `AskUserQuestion` per unknown folder: *"How should I route folder `<name>`?"* with options
     **ClickUp tickets** / **Vision doc** / **Ignore** / (Other — let the user type a destination).
     Save the answer into the routing config (persist to `sync-state.json`) so it never re-asks
     for that folder. Files in an `ignore` folder are dropped from the rest of the run.

## Step 2 — Diff against the manifest

For each non-ignored file, compute a content hash (`sha256sum` in the Bash tool) and compare to
its manifest entry (keyed by vault-root-relative path):

- **NEW** — no marker in filename AND not in manifest → will create.
- **UNCHANGED** — marked + hash matches manifest → skip silently.
- **MODIFIED** — in manifest (or marked) + hash differs from manifest → will **update** the
  existing ClickUp ticket / re-copy the vision doc. Never create a duplicate.
- **MIGRATE** (first-run only) — filename has `(sent)` → will rename to `(created)` and
  best-effort backfill its `clickup_id` by matching its title against existing tickets (Step 5).

A file that is marked `(created)`/`(integrated)` but has **no** manifest entry and an unknown
hash (e.g. the legacy `(sent)` set, or files marked by hand) is treated as MIGRATE/backfill,
not as NEW — never re-created.

### Bundle detection (clickup-ticket NEW files only)

If a NEW prompt clearly contains **multiple distinct features** (e.g. "scatter sticks the player
can pick up" **and** "add a settings property for tree-chop wood yield"), do NOT silently decide.
At run time, fire an `AskUserQuestion` for that specific file: *"`<file>` looks like it bundles N
features — split into N tickets or keep as one?"* with options **Keep as one ticket** /
**Split into N tickets** (list the proposed titles). Honor the answer:
- **Keep as one** → one ticket; capture each feature as a separate AC.
- **Split** → create N tickets from the one file; the manifest entry's `clickup_id` becomes a
  **list** of ids (file→many). All N tickets share the same provenance footer + verbatim prompt.

Only prompt for genuinely distinct features — do not interrogate every multi-sentence prompt.

## Step 3 — Preview + approve (hard gate)

Render a concise plan and call `AskUserQuestion` (Approve / Cancel). Show:

- **Tickets to create** — each: title + the FULL drafted ticket body (so the draft ACs can be vetoed).
- **Tickets to update** — title + ClickUp id + what changed.
- **Visions to integrate / update** — source file → `.claude/docs/vision-<slug>.md`.
- **Vault renames** — old name → new name (including the `(sent)`→`(created)` migrations).
- **Routing config additions** — any new folder decisions captured in Step 1.

Do nothing that writes until the user approves. If cancelled, write nothing (routing-config
additions from Step 1 may be persisted regardless, since they only record the user's explicit answer).

## Step 4 — Apply (only after approval)

For each file, in this order, then update its manifest entry:

- **clickup-ticket / NEW** → create the ticket (status `to do`) via `mcp__clickup__create_task`
  with the ticket shape below. Capture the returned id. **Then** (separate tool round — the
  creating call is never the referencing call) rename the vault file to add ` (created)` and
  write the id into the manifest. If you must reference an id you have not yet seen in a tool
  result, write the literal token `<pending>` and fill it next round — never invent an id.
- **clickup-ticket / MODIFIED** → `mcp__clickup__update_task` on the stored id with the refreshed
  body; refresh the manifest hash.
- **vision-doc / NEW** → copy the file **verbatim** to `.claude/docs/vision-<slug>.md`; add an
  index line under CLAUDE.md "Detailed Documentation"
  (`- [<Title>](.claude/docs/vision-<slug>.md) — <one-line hook>`); rename the vault file to add
  ` (integrated)`. If the vision appears to contradict existing direction docs
  (`art-direction.md`, `survival-roadmap.md`), do NOT silently edit those — flag the conflict in
  the final report for the user to reconcile.
- **vision-doc / MODIFIED** → overwrite the existing `.claude/docs/vision-<slug>.md` verbatim;
  refresh the manifest hash.
- Update each manifest entry: `rel_path`, `type`, `title`, `content_hash` (sha256 of current
  content), `clickup_id` or `docs_path`, `marker`, `last_synced` (`date +%F`).

Persist `sync-state.json` after the batch.

## Step 5 — First-run migration of legacy `(sent)` files

When the manifest is empty and `(sent)` files exist:

1. Fetch existing ticket names from list `901523878268`. Match each `(sent)` file's title
   against them by **keyword / semantic** similarity, NOT exact title — the legacy tickets use
   conventional-commit titles (e.g. file `Bushes` ↔ ticket `feat(world): scatter bushes around
   the island …`), so exact-string matching fails. Present each proposed file→ticket binding as
   a **candidate** in the Step-3 preview for the user to confirm before recording the
   `clickup_id`. Only record a binding the user (implicitly, via approval) accepts.
2. Rename each `(sent)` file → `(created)` (only after approval).
3. For any file with no confident candidate, record `clickup_id: null` with
   `"backfill": "unmatched"` and list it in the final report so the user can map it by hand
   (it is NOT re-created — the marker proves it was already sent).

## Step 6 — Report

Terse summary: created (title + id), updated, integrated visions (+ which need a PR to reach
`main`), migrated/backfilled, renamed, unmatched-backfill, and any flagged vision conflicts.
Remind the user that doc + manifest changes are working-tree only and need a PR via the normal
protected-`main` flow.

---

## Ticket body shape (clickup-ticket destination)

Markdown description sent to ClickUp:

```
## Original prompt (verbatim from Obsidian)

<exact md body of the source file>

## Draft acceptance criteria (auto-generated — pending Priya / Sponsor)

- [ ] <criterion derived faithfully from the prompt>
- [ ] ...

## Out of scope (draft)

- <anything the prompt implies is NOT included>

---
_Imported from Obsidian by /read-obsidian-folder · source: `Ticket prompts/<file>.md` · <YYYY-MM-DD>_
```

**Honesty rules for the draft sections (hard):**
- The "Original prompt" block is **verbatim** — never paraphrased or "improved".
- ACs / OOS are **derived only** from what the prompt says — do not invent features, numbers,
  or scope the user didn't write. When the prompt is thin, keep the ACs thin. Label them draft.
- Title = derived title (filename minus marker/extension).

## Guardrails

- **Never commit, stage, push, or open a PR.** Working-tree edits + ClickUp writes only.
- **Never write to ClickUp / docs / rename a vault file before Step 3 approval.**
- **Idempotent:** a file is created/integrated at most once; edits update in place, never duplicate.
- **Never fabricate ids/hashes/paths.** Fetch ticket ids from the real `create_task` result;
  the creating call is never the referencing call (use `<pending>` then fill).
- **Vision files are copied verbatim;** the skill does not rewrite curated team docs — conflicts
  are flagged, not auto-resolved.
- **Do not orchestrate** (no agent dispatch, no team-status moves) — this skill is hands-on import only.
