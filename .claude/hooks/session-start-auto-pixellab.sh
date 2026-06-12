#!/usr/bin/env bash
# SessionStart hook — re-arms the auto-pixellab loop if it was left enabled
# before this session started.
#
# Mirrors session-start-auto-status.sh. The auto-pixellab loop is session-
# scoped: it dies on session restart / resume / clear. This hook reads the
# durable intent from .claude/auto-pixellab.state and, if auto-pixellab was
# left enabled, injects context telling the orchestrator to re-arm the loop.
#
# Deliberately NOT matched on `compact`: compaction keeps the same session,
# so the loop may still be alive — re-arming there risks stacking a second
# loop. A loop that dies mid-session is surfaced via a stale `last_tick`,
# which the no-arg `auto-pixellab` command reports.
#
# Always exits 0; never blocks.

set -eu

STATE_FILE="$CLAUDE_PROJECT_DIR/.claude/auto-pixellab.state"
[ -f "$STATE_FILE" ] || exit 0

getval() { grep -E "^$1=" "$STATE_FILE" 2>/dev/null | head -1 | cut -d= -f2- || true; }

ENABLED="$(getval enabled)"
INTERVAL="$(getval interval)"
QUEUE_FILE="$(getval queue_file)"
LAST_TICK="$(getval last_tick)"

[ "$ENABLED" = "true" ] || exit 0
INTERVAL="${INTERVAL:-5m}"
QUEUE_FILE="${QUEUE_FILE:-.claude/anim-dispatch-queue.md}"

CONTEXT="# Auto-pixellab re-arm (SessionStart)

auto-pixellab was left **enabled** before this session (interval=\`$INTERVAL\`, queue_file=\`$QUEUE_FILE\`, last_tick=\`${LAST_TICK:-never}\`).

The auto-pixellab loop is session-scoped and does NOT survive a session restart — so it is **not running right now**. Re-arm it as your first action this session: invoke the \`auto-pixellab\` skill with argument \`on\`. Do this before handling the user's request, unless the user's first message explicitly turns auto-pixellab off."

node -e '
const ctx = process.argv[1];
process.stdout.write(JSON.stringify({
  systemMessage: `Auto-pixellab was enabled — re-arm pending.`,
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: ctx,
  },
}));
' "$CONTEXT"
