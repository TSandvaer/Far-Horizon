# ✅ RESOLVED: the `tess` subagent did NOT register — dispatches could not reach her

**Status: RESOLVED 2026-07-27.** The quoted-description fix (below) is applied to
all 11 worktree copies (verified by grep 2026-07-27), and `tess` appears in the
Agent tool's registry in the 2026-07-27 orchestrator session. Kept as a
reference for the CRLF + unquoted-colon-space failure signature — see
"Preventing recurrence" below. Originally diagnosed 2026-07-24 from the
EDC.EDCDK.Website.Nextjs project while investigating an identical failure there.

## The symptom

`tess` is **absent from the Agent tool's registry** in every Far-Horizon session.
Verified 2026-07-24 by running, in `C:\Trunk\PRIVATE\Far-Horizon`:

```
claude -p "List the exact 'subagent_type' values available to you in the Agent tool, one per line, nothing else."
```

Result — the five other personas register, tess does not:

```
claude, claude-code-guide, devon, drew, erik, Explore,
general-purpose, Plan, priya, statusline-setup, uma
```

`tess` is missing. There is **no error, no warning, and nothing in
`claude --debug`** — the definition is silently skipped. This is why the failure
went unnoticed: `.claude/agents/tess.md` exists and looks completely fine.

**Consequence for the orchestrator:** any dispatch that names `subagent_type:
tess` cannot resolve to Tess's persona definition (her system prompt, tool
restrictions and model pin are never applied). Until this is fixed, do not
assume a "Tess" dispatch actually ran as Tess — QA work routed to her has been
running without her persona.

## Root cause

A `.claude/agents/*.md` file is silently dropped when **BOTH** of these are true:

1. the file has **CRLF** (`\r\n`) line endings, **and**
2. a frontmatter value contains an **unquoted `: ` (colon-space)**.

`tess.md` hits both. Its `description:` contains:

```
... (pick by surface: game-side → Drew, harness/inventory/engine → Devon)
```

That `surface: ` is an unquoted colon-space inside a YAML scalar — invalid YAML
("mapping values are not allowed here"). The parser has a recovery pass that
re-quotes such a value, but the recovery does not cope with the trailing `\r`,
so on a CRLF file it fails and the agent is discarded.

**Either condition alone is harmless.** CRLF alone is fine — devon/drew/erik/
priya/uma are all CRLF and register normally. An unquoted colon alone is fine on
an LF file. Only the *combination* breaks.

Established by controlled single-factor bisect (each variant checked in a fresh
session): byte-exact original ❌ · em-dash removed ❌ · colon removed ✅ ·
description quoted ✅ · LF+colon ✅ · CRLF+colon ❌. Ruled out as causes: CRLF
alone, the em-dash, the `model:` value, the agent name, the filename.

## The fix (one line)

Wrap the whole `description:` value in double quotes in
`.claude/agents/tess.md`:

```yaml
description: "QA / Test design on the Embergrave / RandomGame project. ... (pick by surface: game-side → Drew, harness/inventory/engine → Devon)."
```

Quoting is the most robust fix — it survives CRLF and any future colons.
(Converting the file to LF, or removing the `: `, also works, but a later editor
re-saving as CRLF would silently reintroduce the bug.)

**Apply it to the main repo AND all worktree copies** — each worktree has its own
`.claude/agents/tess.md` and each is independently broken. As of 2026-07-24 that
is 11 files:

`Far-Horizon`, `Far-Horizon-caprevert-wt`, `Far-Horizon-config-wt`,
`Far-Horizon-devon-wt`, `Far-Horizon-drew-swings-wt`, `Far-Horizon-drew-wt`,
`Far-Horizon-erik-wt`, `Far-Horizon-priya-wt`, `Far-Horizon-tess-wt`,
`Far-Horizon-uma-wt`, `fh-261-fold`.

## Verifying the fix

Agents load **at session start only** — editing the file mid-session changes
nothing. After the edit, start a fresh session and re-run the registry check
above; `tess` must appear in the list. Do not treat the edit as done until that
output shows her.

## Preventing recurrence

When authoring or editing ANY `.claude/agents/*.md`, quote any frontmatter value
containing `: `. To audit the whole repo, look for agent files whose frontmatter
is CRLF and whose values contain an unquoted colon-space; that pair is the exact
signature of a silently-dead agent.
