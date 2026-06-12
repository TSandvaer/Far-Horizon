#!/usr/bin/env bash
# Stop hook — agent-liveness verification gate.
#
# Layer-2 mechanical enforcement of the behavioral rule in CLAUDE.md / memory
# `never-report-agent-state-from-assumption`: NEVER report an agent as
# "in flight" / "running" / "waiting-on" from the assumption that you dispatched
# it. Report liveness ONLY from a fresh probe.
#
# What it does:
#   Greps THIS session's transcript for background `Agent` dispatches (their
#   spawn results contain `agentId: <hex>`). For each dispatched agentId it
#   checks whether the transcript ALSO contains, anywhere:
#     - a SendMessage probe to that id  (`"to":"<id>"` in a tool_use), OR
#     - a completion notification        (`<task-id><id></task-id>`).
#   If a dispatched id has NEITHER, and the dispatch happened in a PRIOR turn
#   (before the most-recent real user message — so a just-launched agent in the
#   CURRENT turn is never flagged), it BLOCKS the stop and instructs the
#   orchestrator to probe before reporting state or ending the turn.
#
# Why condition-gated: a fire-every-turn reminder becomes noise the model tunes
# out. This fires ONLY when an unverified prior-turn background dispatch actually
# exists — no alarm fatigue.
#
# Timing honesty: a Stop hook fires AFTER the turn's text is emitted, so it
# cannot prevent the first wrong sentence — it forces a probe + self-correction
# before the turn truly ends.
#
# Known v1 gap: on turns where the maintain-docs Stop hook blocks (code/doc
# edits), the re-entry sets stop_hook_active=true and this hook is suppressed.
# The dominant failure surface (coordination-doc edits + pure-response turns
# that assert liveness) is tick-class — maintain-docs exits silently there — so
# this hook still runs on exactly the turns the incident occurred on.
#
# grep/sed only — Git Bash on Windows lacks jq.

set -eu

input=$(cat)

# Re-entry guard: another Stop hook (maintain-docs) already blocked this turn —
# let Claude proceed; we get another clean pass next turn.
if printf '%s' "$input" | grep -Eq '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi

transcript_path=$(printf '%s' "$input" \
  | grep -Eo '"transcript_path"[[:space:]]*:[[:space:]]*"[^"]+"' \
  | sed -E 's/.*"transcript_path"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/' \
  | head -1)

# Fail-open SILENT: if we cannot read the transcript, do not block. This is a
# safety reminder, not a correctness gate — a false block is worse than a miss.
if [[ -z "${transcript_path:-}" || ! -r "$transcript_path" ]]; then
  exit 0
fi

# Most-recent real user message (role:user, not a tool_result wrapper). Dispatches
# at or after this line are CURRENT-turn (just launched) and are not flagged.
last_user_line=$(grep -n '"role":"user"' "$transcript_path" \
  | grep -v '"tool_result"' \
  | tail -1 \
  | cut -d: -f1 || true)
if [[ -z "${last_user_line:-}" ]]; then
  last_user_line=1
fi
slice_end=$(( last_user_line > 1 ? last_user_line - 1 : 0 ))

# Background dispatches in PRIOR turns: their spawn result carries `agentId: <hex>`.
# (Foreground Agent calls return the report inline and have no such line, so this
# pattern inherently captures only background dispatches.)
dispatched=$(head -n "$slice_end" "$transcript_path" 2>/dev/null \
  | grep -oE 'agentId: [0-9a-f]{12,}' \
  | sed -E 's/agentId: //' \
  | sort -u || true)

if [[ -z "${dispatched:-}" ]]; then
  exit 0
fi

# Resolved ids = probed (SendMessage "to") OR completed (<task-id>) — searched
# across the WHOLE transcript (resolution can come after the dispatch line).
# Note: the spawn result's own `to: '<id>'` is single-quoted prose and does NOT
# match the JSON `"to":"<id>"` tool_use shape, so it is not a false "probe".
probes=$(grep -oE '"to":"[0-9a-f]{12,}"' "$transcript_path" 2>/dev/null \
  | grep -oE '[0-9a-f]{12,}' || true)
comps=$(grep -oE 'task-id>[0-9a-f]{12,}' "$transcript_path" 2>/dev/null \
  | grep -oE '[0-9a-f]{12,}' || true)
resolved=$(printf '%s\n%s\n' "$probes" "$comps" | grep -E '[0-9a-f]{12,}' | sort -u || true)

unresolved=""
for id in $dispatched; do
  if ! printf '%s\n' "$resolved" | grep -qx "$id"; then
    unresolved="$unresolved $id"
  fi
done
unresolved=$(printf '%s' "$unresolved" | sed -E 's/^ +//; s/ +$//')

if [[ -z "${unresolved:-}" ]]; then
  exit 0
fi

reason="Unverified background agent(s) dispatched earlier this session with no liveness probe and no completion: ${unresolved}. Before ending this turn or stating/writing ANY claim about their state (in-flight / running / waiting-on), probe each via SendMessage to the agentId (message: status?) and report ONLY from the result plus git log on the worktree plus gh pr view. A returned agentId means launched, not running (memory: never-report-agent-state-from-assumption). If a probe shows the agent already exited, correct STATE.md from that truth."

printf '{"decision":"block","reason":"%s"}' "$reason"
exit 0
