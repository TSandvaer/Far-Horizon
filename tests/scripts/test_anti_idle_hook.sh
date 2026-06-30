#!/usr/bin/env bash
# test_anti_idle_hook.sh — unit-style checks for the orchestrator anti-idle Stop
# hook + the SessionStart resume-nudge (ticket: harden-antiidle-partial-stale-scan).
#
# The hook is a Stop-hook that reads a transcript_path from stdin JSON and greps
# the JSONL transcript to decide whether to BLOCK (emit {"decision":"block",...})
# or pass silently. We build deterministic JSONL transcript fixtures + feed the
# stdin envelope, asserting BLOCK vs no-block per the two fire branches:
#
#   BRANCH A (full-idle)  — tick + team idle + neither dispatched nor scanned.
#   BRANCH B (stale-scan) — tick + no get_tasks this turn + no get_tasks since
#                           the PRIOR tick. Fires even with agents in flight.
#
# Plus the SessionStart resume-nudge in session-start-auto-status.sh: on a
# `resume` start it injects a fresh-scan additionalContext; on `startup` it must
# not.
#
#   tests/scripts/test_anti_idle_hook.sh
#
# Each check asserts the BLOCK decision AND the reason substring (so a block "for
# the wrong reason" still fails). grep/sed-only fixtures; zero Unity dependency.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
HOOK="$ROOT/.claude/hooks/orchestrator-anti-idle-stop.sh"
AUTOSTATUS="$ROOT/.claude/hooks/session-start-auto-status.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

pass=0; fail=0
ok()  { printf '[ OK ] %s\n' "$1"; pass=$((pass+1)); }
bad() { printf '[FAIL] %s\n' "$1"; fail=$((fail+1)); }

# run_hook <transcript-file> -- feed the Stop-hook its stdin envelope (a JSON
# object naming the transcript_path) and echo whatever it writes to stdout.
run_hook() {
  local transcript="$1"
  printf '{"stop_hook_active":false,"transcript_path":"%s"}' "$transcript" \
    | bash "$HOOK"
}

# assert_block <transcript> <reason-needle> <label> — hook must emit a block
# decision whose reason contains the needle.
assert_block() {
  local transcript="$1" needle="$2" label="$3"
  local out; out="$(run_hook "$transcript")"
  if printf '%s' "$out" | grep -qF '"decision":"block"' \
     && printf '%s' "$out" | grep -qF "$needle"; then
    ok "$label (blocked, matched '$needle')"
  else
    bad "$label — expected block + '$needle'; got: ${out:-<empty>}"
  fi
}

# assert_noblock <transcript> <label> — hook must emit NOTHING (pass silently).
assert_noblock() {
  local transcript="$1" label="$2"
  local out; out="$(run_hook "$transcript")"
  if [ -z "$out" ]; then
    ok "$label (no block)"
  else
    bad "$label — expected no block; got: $out"
  fi
}

# ---- JSONL line builders (one transcript line == one JSON object) -----------
# A real orchestration-tick user message.
tick_line()      { printf '{"type":"user","message":{"role":"user","content":"orchestration tick — scan the board first, the team must never idle"}}\n'; }
# An ordinary (non-tick) user message.
plain_user()     { printf '{"type":"user","message":{"role":"user","content":"%s"}}\n' "$1"; }
# A get_tasks board-scan tool_use.
scan_line()      { printf '{"type":"assistant","message":{"content":[{"type":"tool_use","name":"mcp__clickup__get_tasks","input":{"list_id":"901523878268"}}]}}\n'; }
# An Agent dispatch tool_use.
dispatch_line()  { printf '{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Agent","input":{"subagent_type":"devon"}}]}}\n'; }
# An in-flight background-agent spawn result (agentId, no completion).
agent_inflight() { printf '{"type":"user","message":{"role":"user","content":[{"type":"tool_result","content":"agentId: abc123def456 (internal ID for the background agent)"}]}}\n'; }
# A completion notification for that agentId.
agent_complete() { printf '{"type":"user","message":{"role":"user","content":[{"type":"tool_result","content":"<task-id>abc123def456</task-id> finished"}]}}\n'; }
# A plain assistant text line (no tool_use).
asst_text()      { printf '{"type":"assistant","message":{"content":[{"type":"text","text":"%s"}]}}\n' "$1"; }

echo "=== orchestrator-anti-idle-stop.sh ==="

# ---------------------------------------------------------------------------
# BRANCH B — STALE-SCAN GATE
# ---------------------------------------------------------------------------

# B1. STALE-SCAN tick → FIRES. A scan at line N happened, then a PRIOR tick, then
#     the current tick, with NO scan since the prior tick. Agents are in flight
#     (busy critical path) — branch A would NOT fire, but branch B must.
f="$TMP/b1_stale.jsonl"
{
  scan_line                       # 1: last scan, BEFORE the prior tick
  tick_line                       # 2: prior tick
  dispatch_line                   # 3: dispatched build work (agents busy)
  agent_inflight                  # 4: agent in flight
  tick_line                       # 5: current tick (most-recent user msg)
  asst_text "watching the build, all quiet"   # 6
} > "$f"
assert_block "$f" "STALE-SCAN GATE" "B1: stale board (scan before prior tick) + agents in flight → FIRES"

# B2. SCAN-THIS-TURN → no fire. The current tick turn ran a get_tasks.
f="$TMP/b2_fresh_this_turn.jsonl"
{
  tick_line                       # 1: prior tick
  asst_text "idle pass"           # 2
  tick_line                       # 3: current tick
  scan_line                       # 4: scan THIS turn
} > "$f"
assert_noblock "$f" "B2: fresh scan THIS turn → no fire"

# B3. SCAN-SINCE-PRIOR-TICK → no fire. A scan happened between the prior tick and
#     the current tick (not this turn) — the board is still fresh.
f="$TMP/b3_scan_since_prior.jsonl"
{
  tick_line                       # 1: prior tick
  scan_line                       # 2: scan since the prior tick
  dispatch_line                   # 3
  agent_inflight                  # 4: agents busy
  tick_line                       # 5: current tick
  asst_text "all quiet"           # 6
} > "$f"
assert_noblock "$f" "B3: scan since prior tick (board fresh) → no fire"

# B4. NEVER-SCANNED + ≥2 ticks → FIRES (no get_tasks anywhere).
f="$TMP/b4_never_scanned.jsonl"
{
  tick_line                       # 1: prior tick
  dispatch_line                   # 2
  agent_inflight                  # 3
  tick_line                       # 4: current tick
  asst_text "still busy"          # 5
} > "$f"
assert_block "$f" "STALE-SCAN GATE" "B4: never scanned + ≥2 ticks → FIRES"

# B5. SINGLE TICK ONLY → branch B does NOT fire (no prior interval to be stale
#     across). Branch A also does not fire here (an Agent was dispatched this
#     turn → engaged). So the whole hook is silent.
f="$TMP/b5_single_tick.jsonl"
{
  plain_user "hello"              # 1
  tick_line                       # 2: the ONLY tick (current)
  dispatch_line                   # 3: dispatched this turn
  agent_inflight                  # 4
} > "$f"
assert_noblock "$f" "B5: single tick (no prior) + dispatched this turn → no fire"

# ---------------------------------------------------------------------------
# BRANCH A — FULL-IDLE GATE (existing behaviour — must still fire)
# ---------------------------------------------------------------------------

# A1. FULL-IDLE tick → still FIRES. Tick + no agents in flight + neither
#     dispatched nor scanned this turn. A get_tasks since the prior tick keeps
#     branch B silent, so this exercises branch A in isolation.
f="$TMP/a1_full_idle.jsonl"
{
  tick_line                       # 1: prior tick
  scan_line                       # 2: scan since prior tick (board fresh → B silent)
  asst_text "scanned, nothing to do"   # 3
  tick_line                       # 4: current tick
  asst_text "all quiet, going idle"    # 5: neither dispatched nor scanned this turn
} > "$f"
assert_block "$f" "ANTI-IDLE GATE" "A1: full-idle tick (B silent via fresh scan) → branch A still FIRES"

# A2. FULL-IDLE with a COMPLETED agent → branch A still fires (no agent in
#     flight; the dispatched id has a completion). Fresh scan keeps B silent.
f="$TMP/a2_completed_agent.jsonl"
{
  tick_line                       # 1: prior tick
  scan_line                       # 2: fresh scan → B silent
  dispatch_line                   # 3
  agent_inflight                  # 4: agentId abc123def456
  agent_complete                  # 5: <task-id>abc123def456</task-id>
  tick_line                       # 6: current tick
  asst_text "everyone done, idle" # 7
} > "$f"
assert_block "$f" "ANTI-IDLE GATE" "A2: completed agent → team idle → branch A FIRES (B silent)"

# ---------------------------------------------------------------------------
# Shared no-fire guards
# ---------------------------------------------------------------------------

# N1. NON-TICK turn → no fire (neither branch). Most-recent user msg is ordinary
#     conversation, even though a prior tick + no scan exist.
f="$TMP/n1_non_tick.jsonl"
{
  tick_line                       # 1: a prior tick
  asst_text "did stuff"           # 2
  plain_user "what's the status?" # 3: current msg is NOT a tick
  asst_text "here is the status"  # 4
} > "$f"
assert_noblock "$f" "N1: non-tick turn (Sponsor message) → no fire (gated on tick signature)"

# N2. Re-entry guard — stop_hook_active:true → no fire regardless of state.
f="$TMP/n2_reentry.jsonl"
{
  tick_line
  dispatch_line
  agent_inflight
  tick_line
} > "$f"
out="$(printf '{"stop_hook_active":true,"transcript_path":"%s"}' "$f" | bash "$HOOK")"
if [ -z "$out" ]; then ok "N2: stop_hook_active=true → no fire (re-entry guard)"; else bad "N2: re-entry guard — got: $out"; fi

# N3. Unreadable transcript → fail-open silent (no block).
out="$(printf '{"stop_hook_active":false,"transcript_path":"%s/does_not_exist.jsonl"}' "$TMP" | bash "$HOOK")"
if [ -z "$out" ]; then ok "N3: unreadable transcript → fail-open silent"; else bad "N3: fail-open — got: $out"; fi

echo "=== session-start-auto-status.sh resume nudge ==="

# A throwaway project dir with NO auto-status.state, so we test the standalone
# resume-nudge path (independent of the re-arm context).
PROJ_NOSTATE="$TMP/proj_nostate"; mkdir -p "$PROJ_NOSTATE/.claude"

# R1. resume start, no state file → emits the fresh-scan nudge.
out="$(printf '{"source":"resume","hook_event_name":"SessionStart"}' \
       | CLAUDE_PROJECT_DIR="$PROJ_NOSTATE" bash "$AUTOSTATUS")"
if printf '%s' "$out" | grep -qF 'Fresh-scan nudge' \
   && printf '%s' "$out" | grep -qF '/whip'; then
  ok "R1: resume start → emits fresh-scan nudge"
else
  bad "R1: resume nudge — got: ${out:-<empty>}"
fi

# R2. startup start, no state file → emits NOTHING.
out="$(printf '{"source":"startup","hook_event_name":"SessionStart"}' \
       | CLAUDE_PROJECT_DIR="$PROJ_NOSTATE" bash "$AUTOSTATUS")"
if [ -z "$out" ]; then ok "R2: startup start → no nudge"; else bad "R2: startup should be silent — got: $out"; fi

# R3. resume start, auto-status ENABLED → re-arm context PLUS the appended nudge.
PROJ_ENABLED="$TMP/proj_enabled"; mkdir -p "$PROJ_ENABLED/.claude"
printf 'enabled=true\nmode=away\ninterval=15m\nlast_tick=2026-06-30T00:00:00Z\n' \
  > "$PROJ_ENABLED/.claude/auto-status.state"
out="$(printf '{"source":"resume","hook_event_name":"SessionStart"}' \
       | CLAUDE_PROJECT_DIR="$PROJ_ENABLED" bash "$AUTOSTATUS")"
if printf '%s' "$out" | grep -qF 'Auto-status re-arm' \
   && printf '%s' "$out" | grep -qF 'Fresh-scan nudge'; then
  ok "R3: resume + enabled → re-arm context AND appended fresh-scan nudge"
else
  bad "R3: combined re-arm+nudge — got: ${out:-<empty>}"
fi

echo "==================================="
printf '%d passed, %d failed\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || { echo "ANTI-IDLE HOOK TESTS FAILED"; exit 1; }
echo "ANTI-IDLE HOOK TESTS PASSED"
