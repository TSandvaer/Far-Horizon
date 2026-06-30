#!/usr/bin/env bash
# SessionStart hook — re-arms the auto-status loop if it was left enabled
# before this session started.
#
# The auto-status loop is session-scoped: it dies on session restart / resume /
# clear. This hook reads the durable intent from .claude/auto-status.state and,
# if auto-status was left enabled, injects context telling the orchestrator to
# re-arm the loop in the recorded mode. This is the restart-survival mechanism —
# without it, re-arming depends on the user remembering.
#
# Deliberately NOT matched on `compact`: compaction keeps the same session, so
# the loop may still be alive — re-arming there risks stacking a second loop.
# A loop that dies mid-session (rare) is surfaced via a stale `last_tick`, which
# the no-arg `auto-status` command reports.
#
# On a `resume` start it ALSO injects a one-line FRESH-SCAN nudge (2026-06-30):
# a resumed session's save-state is STALE, not current truth, so before
# concluding the board is drained the orchestrator must run a fresh whole-board
# /whip scan. The nudge is non-blocking additionalContext; it appends to the
# re-arm context when auto-status was enabled, and stands alone otherwise.
#
# Always exits 0; never blocks.

set -eu

# Capture the hook input so we can read the SessionStart `source` (startup /
# resume / clear). grep/sed only — Git Bash on Windows lacks jq. Fail-open: an
# unreadable / sourceless input simply yields an empty SOURCE (no resume nudge).
HOOK_INPUT="$(cat 2>/dev/null || true)"
SOURCE="$(printf '%s' "$HOOK_INPUT" \
  | grep -Eo '"source"[[:space:]]*:[[:space:]]*"[^"]+"' \
  | sed -E 's/.*"source"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/' \
  | head -1 || true)"

STATE_FILE="$CLAUDE_PROJECT_DIR/.claude/auto-status.state"
if [ ! -f "$STATE_FILE" ]; then
  STATE_FILE="$CLAUDE_PROJECT_DIR/../.claude/auto-status.state"
fi

# The fresh-scan nudge fires on a `resume` start regardless of whether
# auto-status was enabled — a resumed orchestrator must re-scan before trusting a
# prior 'drained'. Built once here; emitted standalone below if the state file is
# absent / auto-status was off, or appended to the re-arm context otherwise.
RESUME_NUDGE=""
if [ "${SOURCE:-}" = "resume" ]; then
  RESUME_NUDGE="# Fresh-scan nudge (SessionStart resume)

This session was RESUMED — a prior session's save-state is STALE, not current truth. Before concluding the board is drained, run a FRESH whole-board /whip scan (mcp__clickup__get_tasks on list 901523878268, via a subagent — it overflows) and fill-or-justify every idle persona slot. Do not trust a remembered 'all gated' / 'drained'."
fi

if [ ! -f "$STATE_FILE" ]; then
  # No durable auto-status intent. Still surface the resume nudge if this is a
  # resume start; otherwise nothing to do.
  if [ -n "$RESUME_NUDGE" ]; then
    node -e '
process.stdout.write(JSON.stringify({
  systemMessage: "Resumed session — fresh whole-board scan recommended before trusting prior state.",
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: process.argv[1],
  },
}));
' "$RESUME_NUDGE"
  fi
  exit 0
fi

getval() { grep -E "^$1=" "$STATE_FILE" 2>/dev/null | head -1 | cut -d= -f2- || true; }

ENABLED="$(getval enabled)"
MODE="$(getval mode)"
INTERVAL="$(getval interval)"
LAST_TICK="$(getval last_tick)"

# auto-status disabled: nothing to re-arm. Still surface the resume nudge if
# this is a resume start (the fresh-scan discipline is independent of whether
# the loop was armed).
if [ "$ENABLED" != "true" ]; then
  if [ -n "$RESUME_NUDGE" ]; then
    node -e '
process.stdout.write(JSON.stringify({
  systemMessage: "Resumed session — fresh whole-board scan recommended before trusting prior state.",
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: process.argv[1],
  },
}));
' "$RESUME_NUDGE"
  fi
  exit 0
fi
MODE="${MODE:-local}"
INTERVAL="${INTERVAL:-5m}"

CONTEXT="# Auto-status re-arm (SessionStart)

auto-status was left **enabled** before this session (mode=\`$MODE\`, interval=\`$INTERVAL\`, last_tick=\`${LAST_TICK:-never}\`).

The auto-status loop is session-scoped and does NOT survive a session restart — so it is **not running right now**. Re-arm it as your first action this session: invoke the \`auto-status\` skill with argument \`$MODE\` (use \`on\` if mode is \`local\`). Do this before handling the user's request, unless the user's first message explicitly turns auto-status off."

# On a resume start, append the fresh-scan nudge to the re-arm context so both
# disciplines reach the orchestrator in one injection.
if [ -n "$RESUME_NUDGE" ]; then
  CONTEXT="$CONTEXT

$RESUME_NUDGE"
fi

node -e '
const ctx = process.argv[1];
const mode = process.argv[2];
process.stdout.write(JSON.stringify({
  systemMessage: `Auto-status was enabled (mode=${mode}) — re-arm pending.`,
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: ctx,
  },
}));
' "$CONTEXT" "$MODE"
