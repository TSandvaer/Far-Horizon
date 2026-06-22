#!/usr/bin/env bash
# PreToolUse guard (Far Horizon) -- LAYER 2 of the destructive-command block.
#
# Layer 1 = the permissions.deny globs in settings.json. Those are prefix-globs
# and can be dodged by flag reordering (e.g. `git --hard reset`, `git push origin
# main --force`). This hook is the arg-order-robust backstop: it returns a
# PreToolUse "deny" (eval order is deny -> ask -> allow, so deny overrides
# bypassPermissions per the 2026-06-18 permission-precedence research) whenever a
# command matches force-push / reset --hard / recursive-force delete (bash or
# PowerShell) / repo delete / force branch delete -- regardless of flag order.
#
# Destructive actions are on the never-auto-decide list: the orchestrator must
# STAGE them to .claude/away-queue.md, not run them. The deny reason says so.
#
# SCOPING (ticket 86cabe3gx, memory destructive-bash-hook-matches-whole-input):
# we match ONLY the parsed `.tool_input.command` field -- NEVER the whole flattened
# JSON -- AND we strip QUOTED SPANS from that command before matching. Matching the
# whole blob (or the raw command) caused two failure classes:
#   (1) FALSE POSITIVE -- a destructive phrase appearing only in NON-executed text:
#       the `description` field, OR a quoted commit-message / PR-body argument
#       (e.g. `git commit -m "explain why rm -rf is dangerous"`, `gh pr create
#       --body "...git push --force..."`). The message/body is data, not a command.
#       Field-scoping drops the `description`; quote-stripping drops the -m / --body
#       text so a destructive phrase inside it can no longer trip the pattern.
#   (2) FALSE NEGATIVE -- a command that STARTS with the destructive verb slipped
#       past, because in the flattened JSON the verb was preceded by `"command":"`
#       (a double-quote, not in the leading-anchor class), so a leading `rm -rf ...`
#       failed the `(^|[[:space:]...])rm` anchor. Extracting the command string puts
#       the verb genuinely at `^`, so leading recursive-deletes are caught.
#
# A REAL destructive command keeps its verb + flags OUTSIDE the quotes
# (`rm -rf "/path with spaces"`, `git push --force origin main`), so quote-stripping
# never removes the part that matters -- it only removes message/body payloads.
#
# Fail-OPEN: any read/parse problem -> exit 0 (allow). This is a safety backstop,
# not a correctness gate; a missed block is better than a false wall.
#
# grep only for matching (Git Bash on Windows lacks jq). One Python step extracts
# `.tool_input.command`, strips quoted spans, and flattens newlines (Python ships
# with Git Bash / the runner); on any failure it emits empty -> fail-open.

set -eu
input="$(cat)" || exit 0

# Extract `.tool_input.command`, strip quoted spans (double- and single-quoted,
# honoring backslash-escaped quotes), and flatten newlines -- all in one Python
# step (a sibling .py file, so its regexes are not subject to shell quoting).
# Prefer python3 (ubuntu CI) then fall back to python (Git Bash / self-hosted
# Windows runner). Fail-open to empty on any parse problem -> no pattern can
# match -> allow.
_PY="$(command -v python3 || command -v python || true)"
[ -n "$_PY" ] || exit 0   # no interpreter -> fail-open (allow)
cmd="$(printf '%s' "$input" | "$_PY" "$(dirname "${BASH_SOURCE[0]}")/_extract_command.py" 2>/dev/null || true)"

# Nothing to inspect (non-Bash tool, or parse failed) -> allow.
[ -n "$cmd" ] || exit 0

deny() {
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}' "$1"
  exit 0
}

# --- git force-push (any flag order) ---
if printf '%s' "$cmd" | grep -Eqi 'git[^"]*[[:space:]]push([[:space:]]|"|$)' \
   && printf '%s' "$cmd" | grep -Eqi -- '(--force-with-lease|--force-if-includes|--force|(^|[[:space:]])-[a-zA-Z]*f([[:space:]]|"|$))'; then
  deny "Blocked: git force-push is never an auto-decide. Stage it to .claude/away-queue.md for the sponsor instead."
fi

# --- git reset --hard ---
if printf '%s' "$cmd" | grep -Eqi 'git[^"]*[[:space:]]reset([[:space:]]|"|$)' \
   && printf '%s' "$cmd" | grep -Eqi -- '--hard'; then
  deny "Blocked: git reset --hard discards work and is never an auto-decide. Use a soft reset or stage the request for the sponsor."
fi

# --- bash recursive+force delete (rm) ---
# The command string now starts the verb at ^, so a LEADING `rm -rf ...` is caught.
if printf '%s' "$cmd" | grep -Eqi '(^|[[:space:];|&(])rm[[:space:]]' \
   && printf '%s' "$cmd" | grep -Eqi -- '(-[a-zA-Z]*r[a-zA-Z]*f|-[a-zA-Z]*f[a-zA-Z]*r|-r[[:space:]].*-f|-f[[:space:]].*-r|--recursive|--no-preserve-root)'; then
  deny "Blocked: recursive/force rm is a destructive delete (never-auto-decide). Stage it for the sponsor."
fi

# --- PowerShell recursive delete ---
if printf '%s' "$cmd" | grep -Eqi 'remove-item' \
   && printf '%s' "$cmd" | grep -Eqi -- '-recurse'; then
  deny "Blocked: Remove-Item -Recurse is a destructive delete (never-auto-decide). Stage it for the sponsor."
fi

# --- gh repo delete ---
if printf '%s' "$cmd" | grep -Eqi 'gh[[:space:]]+repo[[:space:]]+delete'; then
  deny "Blocked: gh repo delete is irreversible infra. Never auto-decide; stage for the sponsor."
fi

# --- git force branch delete ---
if printf '%s' "$cmd" | grep -Eqi 'git[^"]*[[:space:]]branch[^"]*[[:space:]]-D([[:space:]]|"|$)'; then
  deny "Blocked: git branch -D force-deletes a branch (never-auto-decide). Use gh pr merge --delete-branch for merge cleanup, or stage the request."
fi

exit 0
