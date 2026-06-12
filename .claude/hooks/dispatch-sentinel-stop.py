#!/usr/bin/env python3
"""
Stop hook: enforce a tripwire pairing on every background Agent dispatch.

Per the orchestrator wake-signal discipline (user-global CLAUDE.md) every
Agent({run_in_background: true}) must be paired with a ScheduleWakeup (or
one-shot CronCreate recurring=false) at ~2x expected duration so a stale
agent gets noticed even during active conversation. Sibling-project
incidents (MarianLearning 2026-05-13 + 2026-05-15) proved the behavioral
rule alone is insufficient.

Imported from MarianLearning 2026-06-11 (alignment-plan-MarianLearning-2026-06-11.md
change 1); mechanism unchanged, memory cites adapted to this project.

If the latest assistant turn dispatched one or more background Agents and
did NOT include a paired ScheduleWakeup or CronCreate(recurring:false) call
in the same turn, this hook BLOCKS the turn-end and instructs Claude to add
the tripwire before stopping.

Defensive posture: any parse / file-read failure exits 0 silently (no block).
This hook MUST never produce a non-zero exit code in a way that breaks
Claude's turn — silent passthrough beats false-positive blocking.

Re-entry safety: stop_hook_active is handled in the bash wrapper.
"""

import json
import sys
from pathlib import Path


def detect_unpaired_dispatches(transcript_lines):
    """
    Walk the transcript JSONL backwards, accumulating tool_use blocks from
    the latest contiguous assistant span (i.e. until we hit a user entry).
    Returns (agent_bg_count, tripwire_count).
    """
    agent_bg_count = 0
    tripwire_count = 0

    for raw in reversed(transcript_lines):
        raw = raw.strip()
        if not raw:
            continue
        try:
            entry = json.loads(raw)
        except json.JSONDecodeError:
            continue

        entry_type = entry.get("type")
        # Hit the start of the current turn — stop walking back.
        if entry_type == "user":
            break
        if entry_type != "assistant":
            continue

        message = entry.get("message")
        if not isinstance(message, dict):
            continue
        content = message.get("content")
        if not isinstance(content, list):
            continue

        for block in content:
            if not isinstance(block, dict):
                continue
            if block.get("type") != "tool_use":
                continue
            name = block.get("name")
            inp = block.get("input")
            if not isinstance(inp, dict):
                continue

            if name == "Agent":
                if inp.get("run_in_background") is True:
                    agent_bg_count += 1
            elif name == "ScheduleWakeup":
                tripwire_count += 1
            elif name == "CronCreate":
                # One-shot crons count as tripwires; recurring crons are
                # heartbeat loops, not per-dispatch tripwires.
                if inp.get("recurring") is False:
                    tripwire_count += 1

    return agent_bg_count, tripwire_count


def main():
    try:
        event = json.loads(sys.stdin.read())
    except (json.JSONDecodeError, ValueError):
        return 0

    if not isinstance(event, dict):
        return 0

    # Re-entry safety (also gated in the bash wrapper).
    if event.get("stop_hook_active") is True:
        return 0

    transcript_path = event.get("transcript_path")
    if not transcript_path or not isinstance(transcript_path, str):
        return 0

    p = Path(transcript_path)
    if not p.exists():
        return 0

    try:
        text = p.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return 0

    lines = text.splitlines()
    if not lines:
        return 0

    try:
        agent_bg_count, tripwire_count = detect_unpaired_dispatches(lines)
    except Exception:
        # Catch-all: never block on an unexpected parser error.
        return 0

    if agent_bg_count > 0 and tripwire_count == 0:
        reason = (
            "This turn dispatched {n} background Agent(s) without arming a "
            "tripwire. Per the orchestrator wake-signal discipline "
            "(user-global CLAUDE.md) every "
            "Agent({{run_in_background: true}}) MUST be paired with a "
            "ScheduleWakeup (or one-shot CronCreate recurring=false) at "
            "~2x expected duration so a stale agent gets noticed even "
            "during active conversation. Sibling-project incidents "
            "(MarianLearning 2026-05-13 + 2026-05-15) proved the "
            "behavioral rule alone is insufficient; "
            "this hook is the structural enforcement.\n\n"
            "Add the tripwire NOW before the turn ends. Typical values:\n"
            "  - small fix / review: delaySeconds 600 (10 min)\n"
            "  - content PR / spec author: delaySeconds 1800 (30 min)\n"
            "  - research note + spec: delaySeconds 3600 (1 hr)\n\n"
            "Tripwire prompt should re-enter THIS orchestrator session to "
            "check branch tips on the in-flight dispatch(es) and "
            "SendMessage-ping any silent agent (per memory "
            "stale-agent-detection-and-aggressive-drain)."
        ).format(n=agent_bg_count)
        print(json.dumps({"decision": "block", "reason": reason}))
        return 0

    return 0


if __name__ == "__main__":
    sys.exit(main())
