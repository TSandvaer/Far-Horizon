#!/usr/bin/env python
# Helper for block-destructive-bash.sh (ticket 86cabe3gx).
#
# Reads a PreToolUse hook JSON payload on stdin and prints, on stdout, the
# `.tool_input.command` string with all QUOTED SPANS removed and newlines
# flattened to spaces -- the string the destructive-pattern grep should match.
#
# Why strip quoted spans: a destructive phrase inside a quoted argument (a commit
# `-m "..."` message, a `gh pr create --body "..."` body) is DATA, not an executed
# command, and must not trip the guard. A REAL destructive command keeps its verb
# and flags OUTSIDE the quotes (`rm -rf "/path with spaces"`), so stripping the
# quoted spans removes only message/body payloads, never the dangerous verb+flags.
#
# Fail-open: on ANY error (bad JSON, missing field, non-Bash tool), print nothing.
# The caller treats empty output as "nothing to inspect" -> allow.

import sys
import json
import re

# Double-quoted spans, honoring backslash-escaped quotes inside.
_DQ = re.compile(r'"(?:\\.|[^"\\])*"')
# Single-quoted spans (POSIX sh single quotes do not honor backslash escapes).
_SQ = re.compile(r"'[^']*'")


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        return  # fail-open

    if not isinstance(data, dict):
        return
    tool_input = data.get("tool_input")
    if not isinstance(tool_input, dict):
        return
    command = tool_input.get("command")
    if not isinstance(command, str) or not command:
        return

    stripped = _DQ.sub(" ", command)
    stripped = _SQ.sub(" ", stripped)
    stripped = stripped.replace("\n", " ").replace("\r", " ")
    sys.stdout.write(stripped)


if __name__ == "__main__":
    main()
