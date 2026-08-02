# A `.claude/agents/*.md` file is silently dropped by CRLF + an unquoted colon-space

**Status: fixed on `main` 2026-08-02 (PR #423, `7d75a66`).** Previously "fixed"
on 2026-07-27 — but that fix never reached anyone, for two independent reasons.
Read "Why the first fix didn't stick" before trusting any future claim that this
class is resolved.

Originally diagnosed 2026-07-24 from the EDC.EDCDK.Website.Nextjs project while
investigating an identical failure there.

## The symptom

The agent is **absent from the Agent tool's registry**, with **no error, no
warning, and nothing in `claude --debug`** — the definition is silently skipped.
That silence is the whole problem: the file exists and looks completely fine.

Registry check (run in the project root):

```
claude -p "List the exact 'subagent_type' values available to you in the Agent tool, one per line, nothing else."
```

On 2026-07-24 that returned the five other personas but not `tess`:

```
claude, claude-code-guide, devon, drew, erik, Explore,
general-purpose, Plan, priya, statusline-setup, uma
```

**Consequence:** a dispatch naming a dropped `subagent_type` cannot resolve that
persona's definition — its system prompt, tool restrictions and model pin are
never applied. QA work routed to a dropped `tess` ran **without her persona**.

## Root cause

A `.claude/agents/*.md` file is silently dropped when **BOTH** are true:

1. the file has **CRLF** (`\r\n`) line endings, **and**
2. a frontmatter value contains an **unquoted `: ` (colon-space)**.

`tess.md` hit both. Its `description:` contained:

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

Wrap the whole `description:` value in double quotes:

```yaml
description: "QA / Test design ... (pick by surface: game-side → Drew, harness/inventory/engine → Devon)."
```

Quoting is the most robust fix — it survives CRLF and any future colons.
(Converting the file to LF, or removing the `: `, also works, but a later editor
re-saving as CRLF would silently reintroduce the bug.)

## ⚠ Why the first fix didn't stick — the part that actually matters

The 2026-07-27 fix was real and correct. It still left the agent dead for a
week, and this doc previously said **RESOLVED** the whole time. Two independent
mechanisms defeated it — both are traps for any future `.claude/` fix:

**1. It was applied to worktree copies as UNCOMMITTED edits.** The original
guidance said "apply it to the main repo AND all worktree copies — each worktree
has its own `.claude/agents/tess.md` and each is independently broken," and named
11 paths. Those edits were verified by grep and were genuinely present. But the
dispatch template mandates, at the start of **every** dispatch:

```bash
git checkout -B <your-role>/<task-name> origin/main
```

That force-creates the branch from `origin/main` — **wiping any uncommitted
working-tree edit in that worktree**. So every dispatch silently reverted the
fix. Editing a worktree copy is not a fix; it is a fix with a half-life of one
dispatch.

**2. The commit landed on a branch that never merges.** The fix was committed as
`9011b2c` on `orch/coordination` — a long-lived orchestrator branch that, by
documented convention, is harvested by PORT and never merged (a straight merge
resurrects code `main` deleted). `main` never received it. **Every persona
worktree branches off `main`**, so the fix reached zero worktrees.

**The rule this gives you:** a fix to anything under `.claude/` — agents, hooks,
settings, skills — is only real once it is **committed and merged to `main`**.
Not when it is edited in a worktree. Not when it is committed to a coordination
branch. Verify with a blob comparison against `origin/main`, not by grepping your
own checkout:

```bash
git ls-tree -r origin/main | grep "agents/<name>.md"   # then: git cat-file -p <blob>
```

## Verifying the fix

Agents load **at session start only** — editing the file mid-session changes
nothing. After merging to `main`, start a fresh session and re-run the registry
check above; the agent must appear. Do not treat it as done until that output
shows it.

## Preventing recurrence

- When authoring or editing ANY `.claude/agents/*.md`, **quote any frontmatter
  value containing `: `**.
- Audit signature: an agent file whose frontmatter is CRLF **and** whose values
  contain an unquoted colon-space. Either alone is a false positive.
- Whole-repo audit (2026-08-02: `tess.md` was the only hit across seven files):

```bash
for f in .claude/agents/*.md; do
  d=$(sed -n '/^description:/p' "$f" | head -1)
  case "$d" in 'description: "'*) continue ;; esac
  printf '%s' "$d" | grep -q ': .*: ' && echo "AT RISK: $f"
done
```

- Do not mark this class resolved from a grep of your own working tree. See
  "Why the first fix didn't stick".
