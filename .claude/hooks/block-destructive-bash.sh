#!/usr/bin/env bash
# PreToolUse guard (Far Horizon) — LAYER 2 of the destructive-command block.
#
# Layer 1 = the permissions.deny globs in settings.json. Those are prefix-globs
# and can be dodged by flag reordering (e.g. `git --hard reset`, `git push origin
# main --force`). This hook is the arg-order-robust backstop: it returns a
# PreToolUse "deny" (eval order is deny -> ask -> allow, so deny overrides
# bypassPermissions per the 2026-06-18 permission-precedence research) whenever a
# command matches force-push / reset --hard / recursive-force delete (bash or
# PowerShell) / repo delete / force branch delete — regardless of flag order.
#
# Destructive actions are on the never-auto-decide list: the orchestrator must
# STAGE them to .claude/away-queue.md, not run them. The deny reason says so.
#
# Fail-OPEN: any read/parse problem -> exit 0 (allow). This is a safety backstop,
# not a correctness gate; a missed block is better than a false wall.
#
# grep only (Git Bash on Windows lacks jq). Registered matcher: Bash|PowerShell.

set -eu
input="$(cat)" || exit 0

# Flatten to one line so multi-line / chained commands match cleanly.
cmd="$(printf '%s' "$input" | tr '\n' ' ')"

deny() {
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}' "$1"
  exit 0
}

# --- git force-push (any flag order) ---
if printf '%s' "$cmd" | grep -Eqi 'git[^"]*[[:space:]]push([[:space:]]|")' \
   && printf '%s' "$cmd" | grep -Eqi -- '(--force-with-lease|--force-if-includes|--force|(^|[[:space:]])-[a-zA-Z]*f([[:space:]]|"|$))'; then
  deny "Blocked: git force-push is never an auto-decide. Stage it to .claude/away-queue.md for the sponsor instead."
fi

# --- git reset --hard ---
if printf '%s' "$cmd" | grep -Eqi 'git[^"]*[[:space:]]reset([[:space:]]|")' \
   && printf '%s' "$cmd" | grep -Eqi -- '--hard'; then
  deny "Blocked: git reset --hard discards work and is never an auto-decide. Use a soft reset or stage the request for the sponsor."
fi

# --- bash recursive+force delete (rm) ---
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
if printf '%s' "$cmd" | grep -Eqi 'git[^"]*[[:space:]]branch[^"]*[[:space:]]-D([[:space:]]|")'; then
  deny "Blocked: git branch -D force-deletes a branch (never-auto-decide). Use gh pr merge --delete-branch for merge cleanup, or stage the request."
fi

exit 0
