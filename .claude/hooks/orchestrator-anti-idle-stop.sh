#!/usr/bin/env bash
# Stop hook — orchestrator anti-idle gate.
#
# Layer-2 mechanical enforcement of CLAUDE.md / memory
# `orchestrator-fill-nongated-slots-scan-whole-board`: idle capacity is a bug
# even when the critical path is sponsor-gated. The orchestrator must scan the
# WHOLE board every tick and fill every non-gated / non-dependent slot — it must
# NOT conclude "everything's gated" from its mental model of the active tickets.
#
# The failure this prevents (2026-06-28): the team idled ~3h across ~12 cron
# ticks while #165 was soak-staged + water was decision-gated. The orchestrator
# never re-scanned the board; a full get_tasks showed 33 open "to do" tickets,
# many non-gated + unrelated (perf, locomotion, refactors, visuals, spikes).
#
# Two INDEPENDENT fire branches, each gated on the turn being an ORCHESTRATION
# TICK (the cron/pulse prompt signature is in the most-recent real user message).
# Fires ONLY on the precise failure state, no alarm fatigue.
#
# BRANCH A — FULL-IDLE GATE (original):
#   (1) the turn is an orchestration tick, AND
#   (2) the team is FULLY IDLE — zero in-flight background agents (no dispatched
#       agentId left unresolved by a probe or a completion), AND
#   (3) this turn did NEITHER dispatch (no `Agent` tool_use) NOR a fresh
#       full-board scan (no `mcp__clickup__get_tasks` tool_use).
#   In that state it BLOCKS the stop and orders a scan + fill (or a per-slot
#   machine-checkable reason). A busy team (agents in flight) or an engaged tick
#   (dispatched or scanned this turn) is never flagged by this branch.
#
# BRANCH B — STALE-SCAN GATE (2026-06-30):
#   (1) the turn is an orchestration tick, AND
#   (b) this turn ran NO `mcp__clickup__get_tasks`, AND
#   (c) no `get_tasks` occurred since the PRIOR orchestration tick (≥1 full tick
#       interval elapsed with no fresh whole-board scan).
#   This branch fires REGARDLESS of whether agents are in flight or whether an
#   `Agent` was dispatched this turn — it closes the hole where the orchestrator
#   gets absorbed in the build-lane critical path (agents busy → branch A never
#   fires) while the NON-build lane (Erik/Uma/Priya) starves behind a board that
#   was last scanned >1 tick ago. A scan THIS turn or since the prior tick is
#   never flagged (no alarm fatigue). Branch B is checked FIRST: a stale board is
#   the stronger signal, and a Stop hook can emit only one decision.
#
# Timing honesty: a Stop hook fires AFTER the turn's text — it cannot stop the
# first "quiet" sentence, but it forces a scan + fill before the turn truly ends.
#
# grep/sed only — Git Bash on Windows lacks jq.

set -eu

input=$(cat)

# Re-entry guard: another Stop hook (maintain-docs) already blocked this turn.
if printf '%s' "$input" | grep -Eq '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi

transcript_path=$(printf '%s' "$input" \
  | grep -Eo '"transcript_path"[[:space:]]*:[[:space:]]*"[^"]+"' \
  | sed -E 's/.*"transcript_path"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/' \
  | head -1)

# Fail-open SILENT: a false block is worse than a miss for a safety reminder.
if [[ -z "${transcript_path:-}" || ! -r "$transcript_path" ]]; then
  exit 0
fi

# Most-recent real user message (role:user, not a tool_result wrapper).
last_user_line=$(grep -n '"role":"user"' "$transcript_path" \
  | grep -v '"tool_result"' \
  | tail -1 \
  | cut -d: -f1 || true)
if [[ -z "${last_user_line:-}" ]]; then
  last_user_line=1
fi

# (1) Is this an ORCHESTRATION TICK? The cron/pulse prompt carries a distinctive
# signature. Only fire on those turns — never on ordinary conversation.
tick_text=$(sed -n "${last_user_line}p" "$transcript_path" 2>/dev/null || true)
if ! printf '%s' "$tick_text" | grep -Eq 'orchestration tick|[Oo]rchestration pulse|scan the board first|team must never idle|never idle'; then
  exit 0
fi

# The tick signature for the COUNTING grep below — used to find ALL tick line
# numbers so the PRIOR tick can anchor the staleness window. The bare `never
# idle` alternative is intentionally OMITTED here (it IS kept in the gate-entry
# grep above): a real Sponsor message that merely *contains* the phrase
# `never idle` must not be miscounted as a cron tick and mis-anchor `prior_tick`,
# which would fire a (self-correcting, but noisy) false STALE-SCAN block. The
# fuller, cron-prompt-specific anchors stay so genuine ticks still count.
TICK_SIG='orchestration tick|[Oo]rchestration pulse|scan the board first|team must never idle'

# ---------------------------------------------------------------------------
# BRANCH B — STALE-SCAN GATE. Fire when the WHOLE board has not been scanned
# since before the PRIOR orchestration tick — i.e. ≥1 full tick interval has
# elapsed with no fresh `get_tasks`. This catches the "absorbed in the build
# lane" hole (agents in flight → branch A's full-idle check exits early) where
# the non-build personas starve behind a stale board. Checked BEFORE branch A
# because a Stop hook emits only one decision and a stale board is the stronger
# signal.
#
# Line-number arithmetic (grep -n on user-message lines only, so a tick
# signature quoted inside an assistant message or a tool_result can't be
# mistaken for a real tick):
#   - tick_lines  = every real-user-message line carrying the tick signature.
#   - prior_tick  = the 2nd-from-last such line (the tick BEFORE this one). If
#                   only one tick exists in the transcript there is no prior
#                   interval to be stale across → branch B does not fire.
#   - last_scan   = the line of the most-recent `get_tasks` tool_use.
#   FIRE if (no get_tasks at all) OR (last_scan < prior_tick). A get_tasks this
#   turn (last_scan >= last_user_line) trivially satisfies last_scan >= prior_tick
#   → no fire, so a scan THIS turn is never flagged.
tick_lines=$(grep -n '"role":"user"' "$transcript_path" 2>/dev/null \
  | grep -v '"tool_result"' \
  | grep -E "$TICK_SIG" \
  | cut -d: -f1 || true)
prior_tick=$(printf '%s\n' "$tick_lines" | grep -E '^[0-9]+$' | tail -2 | head -1 || true)

# Only evaluate staleness when a PRIOR tick exists (≥2 ticks in the transcript)
# AND the prior tick is strictly before this turn's tick (a guard for the
# single-tick / degenerate case where tail -2|head -1 == the current tick line).
if [[ -n "${prior_tick:-}" && "$prior_tick" -lt "$last_user_line" ]]; then
  last_scan=$(grep -nE '"type":"tool_use"' "$transcript_path" 2>/dev/null \
    | grep -E '"name":"mcp__clickup__get_tasks"' \
    | tail -1 \
    | cut -d: -f1 || true)
  if [[ -z "${last_scan:-}" || "$last_scan" -lt "$prior_tick" ]]; then
    reasonB="STALE-SCAN GATE: no fresh whole-board scan since before the last tick — idle capacity hides behind a busy critical path. Run mcp__clickup__get_tasks on list 901523878268 (via a subagent — it overflows) and fill-or-justify EVERY non-build persona slot (Erik/Uma/Priya) independent of the single Unity build slot. A prior 'drained' is not current truth."
    printf '{"decision":"block","reason":"%s"}' "$reasonB"
    exit 0
  fi
fi

# ---------------------------------------------------------------------------
# BRANCH A — FULL-IDLE GATE (original).
# (3) Did THIS turn dispatch (Agent) OR scan the board (get_tasks)? If so, the
# orchestrator is engaged — do not flag.
# Hardened: match `"type":"tool_use"` and the target `"name":...` as two
# INDEPENDENT field-matches on the same JSONL line (chained greps) rather than
# pinning their exact field adjacency/order — a serializer that reorders the
# type/id/name fields can no longer silently break dispatch/scan detection.
if tail -n "+${last_user_line}" "$transcript_path" 2>/dev/null \
    | grep -E '"type":"tool_use"' \
    | grep -Eq '"name":"(Agent|mcp__clickup__get_tasks)"'; then
  exit 0
fi

# (2) Is the team FULLY IDLE? Background dispatches carry `agentId: <hex>` in
# their spawn result; a completion carries `task-id><hex>`. A dispatched id with
# NO completion record => the agent is in flight => the team is busy, so a
# do-nothing monitor tick is correct; do not flag. Only when EVERY dispatched
# agent has completed (or none were dispatched) is the team truly idle.
dispatched=$(grep -oE 'agentId: [0-9a-f]{12,} \(internal ID' "$transcript_path" 2>/dev/null \
  | grep -oE '[0-9a-f]{12,}' | sort -u || true)
if [[ -n "${dispatched:-}" ]]; then
  comps=$(grep -oE 'task-id>[0-9a-f]{12,}' "$transcript_path" 2>/dev/null \
    | grep -oE '[0-9a-f]{12,}' | sort -u || true)
  for id in $dispatched; do
    if ! printf '%s\n' "$comps" | grep -qx "$id"; then
      exit 0  # a dispatched agent with no completion => in flight => team busy => skip
    fi
  done
fi

# All three conditions hold: orchestration tick + team idle + neither dispatched
# nor scanned this turn. BLOCK and order a scan + fill.
reason="ANTI-IDLE GATE: this is an orchestration tick, the team is idle (no agents in flight), and you neither dispatched work nor ran a fresh full-board scan this turn. Idle capacity is a bug. Run mcp__clickup__get_tasks on list 901523878268 (it overflows context — pipe it through a subagent that returns id+name+status) to see EVERY open ticket, then dispatch every non-gated / non-dependent ready ticket to an idle persona or the build slot (NITs / perf / cleanup / research-spikes / specs / reviews need no sponsor priority; reserve the single build slot for the priority feature). Only go quiet after you can name, for EACH idle slot, a machine-checkable reason it cannot be filled (sponsor-gate / hard-dep-unmerged / build-slot-occupied). 'I think it's all gated' from memory is exactly the failure this gate exists to prevent (memory: orchestrator-fill-nongated-slots-scan-whole-board)."

printf '{"decision":"block","reason":"%s"}' "$reason"
exit 0
